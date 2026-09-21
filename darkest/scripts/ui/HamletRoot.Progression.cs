using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **城池 · 养成动作族**（减压 / 招募 / 选人 / 建筑升级 = "养成→再出发"闭环动作）✓
/// ③ 🔴 依赖主类私有成员：`_selectedHero` · `_cfg` · `_log` · `_rng` · `_upgradeButtons` · `_heroButtons` · `_upgradeStatus` · `Refresh()`✓
/// ④ **只搬家、零行为改动**（含 2026-09-21 修复的 rookieLevel / EffectiveMoraleRestore，行为不变）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// **减压**（M8.0 ④）：花钱 → 恢复士气（**唯一**士气出口）→ 副作用掷骰（Tavern 更不稳 / Abbey 更稳）。
    /// 🔴 只转发：钱与士气都在**跨趟持有者**里；UI 不自己算账。
    /// </summary>
    public void DoRelief(string buildingId)
    {
        Economy? economy = ExpeditionContext.Gold;
        Roster? roster = ExpeditionContext.Roster;
        if (economy is null || roster is null)
        {
            return;
        }

        // ② 选人权：**优先用玩家指定的人**（未指定时退化为最低者，便于冒烟）
        string heroId = _selectedHero ?? roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).First().Id;
        StressReliefOutcome o = StressRelief.Apply(_cfg, economy, _rng, _log, buildingId, heroId, roster.MoraleOf(heroId));
        if (o.Paid)
        {
            // 🔴 主程序 2026-09-21（策划 #404 纪律 V：展示值 == 消费值）：恢复量走**生效值**（与 UI 展示同源）✓
            int restore = Darkest.Gameplay.Scene.ExpeditionContext.Heirlooms?.EffectiveMoraleRestore(buildingId, _cfg.Building(buildingId).MoraleRestore)
                ?? _cfg.Building(buildingId).MoraleRestore;
            roster.ApplyRelief(_log, heroId, restore, buildingId);
        }

        // 🔴 `#283` 7.3（2026-09-20 接线）：**副作用的"下一趟开局 −N"必须真的登记** ——
        //    此前它只出现在打印里（`o.NextRunPenalty`），**从不施加** ⇒ 玩家看到代价却毫无影响 ⚠️
        //    ⇒ 登记到名册的待罚表；下一趟开趟时由 `Roster.OpeningMorale` 扣掉（`StressRelief.NextRunOpeningMorale`）✓
        if (o.PenaltyTriggered)
        {
            roster.ScheduleOpeningPenalty(heroId, o.NextRunPenalty);
        }

        GD.Print($"[HamletRoot] 减压·{buildingId}：{(o.Paid ? "成交" : "拒绝（钱不够）")}" +
                 $"　{heroId} 士气 {o.NewMorale}　副作用 {(o.PenaltyTriggered ? $"触发（下趟 −{o.NextRunPenalty}）" : "未触发")}" +
                 $"　剩余金钱 {economy.Gold}");
        Refresh();
    }

    /// <summary>**招募**（M8.0 ⑤）：免费；新兵 Lv1 / 士气 50；满员即拒绝（不悄悄顶替）。</summary>
    public void Recruit()
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            return;
        }

        // 挑一个"人最少的原型"，名字用序号（最小实现；将来由玩家选）
        string archetype = roster.Heroes
            .GroupBy(h => h.Archetype)
            .OrderBy(g => g.Count())
            .First().Key;
                HeroConfig? rookie = roster.Recruit(_log, _cfg.Coach, archetype, $"新兵{roster.Heroes.Count + 1}",
                    rookieLevel: Darkest.Gameplay.Scene.ExpeditionContext.Heirlooms?.EffectiveRookieLevel(_cfg.Coach.RookieLevel));   // 🔴 消费 HamletRoot:1605 的展示值（马车升级起点落到新兵；null ⇒ 内核缺省=旧行为）✓

        GD.Print(rookie is null
            ? $"[HamletRoot] 招募：**名册已满**（{roster.Heroes.Count}/{_cfg.Coach.MaxRoster}）—— 拒绝（不悄悄顶替）"
            : $"[HamletRoot] 招募：{rookie.Name}（{rookie.Archetype} Lv{rookie.Level} 士气{rookie.Morale}）**免费**" +
              $"　名册 {roster.Heroes.Count}/{_cfg.Coach.MaxRoster}");
        Refresh();
    }

    /// <summary>**选中某位英雄**（② 选人权：减压必须由玩家指定对象，不是"自动挑最低的"）。</summary>
    public void SelectHero(string heroId)
    {
        _selectedHero = heroId;
        Roster? roster = ExpeditionContext.Roster;
        int morale = roster?.MoraleOf(heroId) ?? 0;
        GD.Print($"[HamletRoot] 已选中 {heroId}（当前士气 {morale}）⇒ 再点酒馆/修道院减压");
        Refresh();
    }

    /// <summary>**招募指定原型**（⑤：玩家决定招哪种人；免费 / Lv1 / morale 50 / 满员拒绝）。</summary>
    public void RecruitArchetype(string archetype)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            return;
        }

                HeroConfig? rookie = roster.Recruit(_log, _cfg.Coach, archetype, $"新兵{roster.Heroes.Count + 1}",
                    rookieLevel: Darkest.Gameplay.Scene.ExpeditionContext.Heirlooms?.EffectiveRookieLevel(_cfg.Coach.RookieLevel));   // 🔴 消费 HamletRoot:1605 的展示值（马车升级起点落到新兵；null ⇒ 内核缺省=旧行为）✓
        GD.Print(rookie is null
            ? $"[HamletRoot] 招募·{archetype}：**名册已满**（{roster.Heroes.Count}/{roster.Cap}）—— 拒绝（不悄悄顶替）"
            : $"[HamletRoot] 招募·{archetype}：{rookie.Name}（Lv{rookie.Level} 士气{rookie.Morale}）**免费**" +
              $"　名册 {roster.Heroes.Count}/{roster.Cap}");
        Refresh();
    }

    /// <summary>**升级建筑**（M8.1）：按曲线扣传家宝；不足即拒绝（不部分扣）；升级后**生效值真的改变**。</summary>
    public void UpgradeBuilding(string building)
    {
        HeirloomStock? h = ExpeditionContext.Heirlooms;
        if (h is null)
        {
            return;
        }

        UpgradeLevel? next = h.NextLevel(building);
        bool ok = h.TryUpgrade(_log, building);
        GD.Print(ok
            ? $"[HamletRoot] 升级·{building} ⇒ Lv{h.LevelOf(building)}（花 {string.Join("/", next!.Cost.Select(k => $"{k.Key}×{k.Value}"))}）" +
              $"　生效：减压价 {h.EffectiveReliefCost(_cfg.StressReliefCost)}　名册上限 {h.EffectiveRosterCap(baseCap: ExpeditionContext.Roster?.Heroes.Count ?? 0, hardCap: 12)}"
            : $"[HamletRoot] 升级·{building}：**传家宝不足或已满级** ⇒ 拒绝（不部分扣）");
        Refresh();
    }
}
