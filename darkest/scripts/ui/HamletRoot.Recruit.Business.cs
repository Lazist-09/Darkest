using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.Recruit.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **M7u 驿站招募 · 业务入口族**（原 `:224-344` 逐字节：点条目 `RecruitOfferAt` ／ 唯一业务入口 `RecruitOne`（转发内核 `Roster.Recruit`）／ 名册上限重算 `RecomputeRosterCap` ／ 上限一行读法 `EffectiveCapNow` ／ 驿站功能行 `RecruitFunctionLine` ／ 刷新入口 `RefreshRecruitOffers` ／ 最近招募事件 `LastRecruitEventText`）✓
/// ③ 🔴 **依赖主类私有成员**：`_recruitOffers`／`_recruitOfferArchetypes`／`_log`／`_cfg`／`_unlockCfg`；主类 `Refresh()`／`AutoSave()`；片间 `GrantRecruitQuirk`（Quirk 片）✓
/// ④ **只搬家、零行为改动**（逐字节同序；主片仅删 1 行纯空白 —— 原 `:223`）✓
/// </summary>
public partial class HamletRoot : Control
{
    // ------------------------------------------------------------------
    // 业务入口（唯一一条：点条目 ／ 调试直招 都走它）✓
    // ------------------------------------------------------------------

    /// <summary>🔴 **点条目 ⇒ 招募**：职业 / 等级 / 是否高级都取自**当前名单**；越界 ⇒ 如实拒绝（不静默换人）✓</summary>
    public void RecruitOfferAt(int index)
    {
        if (index < 0 || index >= _recruitOffers.Count)
        {
            GD.Print($"[HamletRoot] 招募条目 #{index}：今日新兵只有 {_recruitOffers.Count} 个 ⇒ **如实拒绝**（不静默）✓");
            return;
        }

        string archetype = index < _recruitOfferArchetypes.Length ? _recruitOfferArchetypes[index] : "?";
        StagecoachRecruits.Offering offering = _recruitOffers[index];
        RecruitOne(archetype, RecruitLevelFor(offering), $"·条目{index}", offering.Upgraded);
    }

    /// <summary>
    /// 🔴 **招募一个人**（唯一业务入口；UI 只转发）：转发内核 `Roster.Recruit`
    /// （进名册 ／ 写 `HeroRecruitedEvent` 全在内核）✓
    /// 🔴 满员的理由由内核 `Roster.CanRecruit` 回答（红线 21 (b)）—— 本方法不自造理由 ✓
    /// 🆕 M5u：`upgraded = true`（高级新兵）⇒ 招到后按内核纯函数**掷一条怪癖**（见 `GrantRecruitQuirk`）✓
    /// </summary>
    public void RecruitOne(string archetype, int? rookieLevel, string tag, bool upgraded = false)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            GD.Print($"[HamletRoot] 招募{tag}：名册未加载 ⇒ **如实拒绝**（不静默）✓");
            return;
        }

        int before = roster.Heroes.Count;
        HeroConfig? rookie = roster.Recruit(_log, _cfg.Coach, archetype, $"新兵{before + 1}", rookieLevel);
        if (rookie is not null && upgraded)
        {
            GrantRecruitQuirk(roster, rookie.Id);
        }

        GD.Print(rookie is null
            ? $"[HamletRoot] 招募{tag}：**名册已满**（{before}/{EffectiveCapNow(roster)}）—— 拒绝（不悄悄顶替）✓"
            : $"[HamletRoot] 招募{tag}：{rookie.Name}（{rookie.Archetype} Lv{rookie.Level} 士气{rookie.Morale}）**免费**" +
              $"　名册 {roster.Heroes.Count}/{EffectiveCapNow(roster)}　事件原文 {LastRecruitEventText()} ✓");
        Refresh();
        AutoSave("招募");
    }

    /// <summary>
    /// 🔴 **名册可用上限重算**（**单一来源 = 马车曲线**）：按 `RunProgress.CurrentRosterCap` 写 `Roster.CurrentCap`；
    /// 调用点 = 回城装配（`Build`）＋ 马车升级（`UpgradeBuilding`）✓
    /// ⚠️ 名册 ／ 解锁表未加载 ⇒ **如实拒绝**（不静默写一个数）✓
    /// </summary>
    public void RecomputeRosterCap(string reason)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null || _unlockCfg is null)
        {
            GD.Print($"[HamletRoot] 名册可用上限重算（{reason}）：{(roster is null ? "名册" : "解锁表")}未加载 ⇒ 如实拒绝（不静默写数）✓");
            return;
        }

        int before = roster.CurrentCap;
        int level = ExpeditionContext.Heirlooms?.LevelOf("stagecoach") ?? 0;
        IReadOnlyList<int> curve = ExpeditionContext.StagecoachCapCurve ?? Array.Empty<int>();
        roster.CurrentCap = ExpeditionContext.Progress.CurrentRosterCap(
            _unlockCfg,
            roster.Cap,
            stagecoachCapByLevel: ExpeditionContext.StagecoachCapCurve,
            stagecoachLevel: level);
        GD.Print($"[HamletRoot] 名册可用上限重算（{reason}）：{before} ⇒ {roster.CurrentCap}" +
                 $"（马车 Lv{level} · 曲线 {string.Join("/", curve)} · 硬上限 {roster.Cap}）✓");
    }

    /// <summary>可用上限的一行读法（**真值 = 内核**；曲线未重算时如实标注，不假装有值）✓</summary>
    private static string EffectiveCapNow(Roster? r)
    {
        if (r is null)
        {
            return "—";
        }

        return r.CurrentCap > 0
            ? $"{Math.Min(r.CurrentCap, r.Cap)}（硬上限 {r.Cap}）"
            : $"{r.Cap}（硬上限 {r.Cap}；⚠ 曲线未重算 ⇒ 暂用硬上限）";
    }

    /// <summary>
    /// 🔴 驿站弹窗的**功能行文案**（`RefreshBuildingPopup` 与 `ShowBuildingInfo` **共用同一份** ⇒ 不抄第二份口径）✓
    /// ⚠️ 数字全部**从数据现算**（马车等级 ／ 曲线人数 ／ 高级概率）—— 本方法不写死任何常量（红线 21）✓
    /// </summary>
    private string RecruitFunctionLine()
    {
        int level = ExpeditionContext.Heirlooms?.LevelOf("stagecoach") ?? 0;
        return $"招募新兵（今日新兵列表：马车 Lv{level} ⇒ {StagecoachRecruits.CountAt(_cfg.Coach, level)} 人 · " +
               $"高级概率 {StagecoachRecruits.UpgradedChancePctAt(_cfg.Coach, level):0.##}% · 起始等级 " +
               $"{StagecoachRecruits.StartingLevel(_cfg.Coach, new StagecoachRecruits.Offering(false))} ⇒ 点条目即招）";
    }

    /// <summary>🔴 **刷新今日新兵**（刷新按钮的真实入口）：重掷名单 + 重画（幂等）✓</summary>
    public void RefreshRecruitOffers(string reason)
    {
        RollRecruitOffers(reason);
        RefreshRecruitRow();
        GD.Print($"[HamletRoot] 今日新兵已刷新（{reason}）：{_recruitOffers.Count} 人（高级 {RecruitOfferUpgradedCount} 人）" +
                 $"　原型 {string.Join("、", _recruitOfferArchetypes)} ✓");
    }

    /// <summary>回读最近一条招募事件原文（`HeroRecruitedEvent`；没有 ⇒ "（无）"）✓</summary>
    private string LastRecruitEventText()
    {
        for (int i = _log.Events.Count - 1; i >= 0; i--)
        {
            if (_log.Events[i] is Darkest.Core.Events.HeroRecruitedEvent e)
            {
                return $"hero_recruited: {e.HeroId} {e.Name}（{e.Archetype} Lv{e.Level} 士气{e.Morale} 花费{e.Cost}）";
            }
        }

        return "（无）";
    }
}
