using Darkest.Core.Events;      // HeroTrinketEquippedEvent ／ HeroTrinketUnequippedEvent（记录 ⇒ 原文可回读）✓
using Darkest.Gameplay.Scene;   // ExpeditionContext（名册 ／ 存档入口）✓
using Darkest.Gameplay.Sim.Run; // Roster（EquipTrinket ／ UnequipTrinket ／ TrinketsOf）✓
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律（用户 2026-09-21）：一律 Darkest.UI（大写 UI），不得写成 Ui ✓

/// <summary>
/// 🆕 **M4u · 角色详情 · 饰品【装卸动作】**（2026-10-02）—— 从 `HamletRoot.HeroDetail.Trinkets.cs` 分片
/// （用户红线：程序文件 ≤600 行 · 目标 ≤400 行）✓
///
/// <para>本片 = **状态一改的四个落点**（全部走 `Roster` 的公开入口；红线 26：屏上读数与真状态同一份）✓</para>
/// <list type="bullet">
/// <item>装上 = `EquipTrinketFromSlot`（落孔回调的唯一落点）✓</item>
/// <item>卸下 = `UnequipTrinketFromUi`（拖出孔外松手 ／ 点方块 的唯一落点）✓</item>
/// <item>收口 = `AfterTrinketChange`（**延迟一帧**刷新 ＋ 状态一改就落盘）✓</item>
/// <item>留痕 = `LastTrinketEventText`（记录 `ToString()` 原文 ⇒ 不是第二份文本）✓</item>
/// </list>
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 🔴 **装上（落孔的唯一入口）** —— 走内核 `Roster.EquipTrinket`（判据与事件的生产点，红线 21 (b)）：
    /// 返回 `null` = 真装上；返回理由 = **内核原样**存下 ⇒ 详情整行呈现（T4），UI 不自己判第二遍 ✓
    /// <para>⚠️ 刷新走 `AfterTrinketChange`（延迟一帧）—— 引擎此刻还在派发拖放的 `NotificationDragEnd` ✓</para>
    /// </summary>
    private void EquipTrinketFromSlot(string heroId, string trinketId)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null || _trinketsCfg is null)
        {
            GD.Print($"[M4u·饰品] 落孔「{trinketId}」⇒ 名册={roster is not null} 库={_trinketsCfg is not null} ⇒ 如实拒绝（不假装装上）✓");
            return;
        }

        string? refused = roster.EquipTrinket(_log, _trinketsCfg, heroId, trinketId, "drop");
        if (refused is null)
        {
            if (_trinketRefusedHeroId == heroId)
            {
                _trinketRefusedHeroId = null;   // 同英雄后一次装上 ⇒ 旧候选读数作废（不留过期读数）✓
                _trinketRefusedTrinketId = null;
                _trinketRefusedReason = null;
            }

            GD.Print($"[M4u·饰品] 落孔：{heroId} ⇐ 「{trinketId}」装上（现持 {roster.TrinketsOf(heroId).Count}/{Roster.MaxTrinketSlots}）✓");
        }
        else
        {
            _trinketRefusedHeroId = heroId;
            _trinketRefusedTrinketId = trinketId;
            _trinketRefusedReason = refused;
            GD.Print($"[M4u·饰品] 落孔：{heroId} ⇐ 「{trinketId}」**被拒**：{refused}（内核原样 ⇒ 详情整行呈现，T4）✓");
        }

        AfterTrinketChange(heroId);
    }

    /// <summary>
    /// 🔴 **卸下（拖出孔外松手 ／ 点方块 的唯一入口）** —— 走内核 `Roster.UnequipTrinket`（必写
    /// `HeroTrinketUnequippedEvent`）；返回 `false` = 本来就没装 ⇒ 如实打印，不假装卸过 ✓
    /// <para>🔴 `reason` 由调用方给（`click` ／ `drag-out`）—— 事件里记的必须是**玩家真做的那件事**：
    /// 两种手势共用一个理由就是假读数（红线 26：留痕 == 真值）✓</para>
    /// </summary>
    private void UnequipTrinketFromUi(string heroId, string trinketId, string reason)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            GD.Print($"[M4u·饰品] 卸下「{trinketId}」⇒ 名册未加载 ⇒ 如实拒绝（不假装卸下）✓");
            return;
        }

        bool removed = roster.UnequipTrinket(_log, heroId, trinketId, reason);
        if (removed && _trinketRefusedHeroId == heroId)
        {
            _trinketRefusedHeroId = null;   // 状态已变 ⇒ 旧的「不可装备」候选读数作废（不留过期读数）✓
            _trinketRefusedTrinketId = null;
            _trinketRefusedReason = null;
        }

        GD.Print(removed
            ? $"[M4u·饰品] 卸下（{reason}）：{heroId} ⇏ 「{trinketId}」（现持 {roster.TrinketsOf(heroId).Count}/{Roster.MaxTrinketSlots}）✓"
            : $"[M4u·饰品] 卸下（{reason}）：{heroId} 身上没有「{trinketId}」⇒ 内核返回 false（如实留痕，不假装卸过）✓");
        AfterTrinketChange(heroId);
    }

    /// <summary>
    /// 🔴 **装卸后的唯一收口** —— ① **延迟一帧**再刷格（引擎此刻还在发信号的那个孔上派发
    /// `NotificationDragEnd`，立刻重建会踩引擎的拖放收尾）② 状态一改就落盘（`AutoSave`）✓
    /// </summary>
    private void AfterTrinketChange(string heroId)
    {
        GD.Print($"[M4u·饰品] 装卸收口：最近事件 = {LastTrinketEventText()}" +
                 $"（现持 {ExpeditionContext.Roster?.TrinketsOf(heroId).Count ?? -1}/{Roster.MaxTrinketSlots}）✓");
        Callable.From(() => RefreshTrinketsDeferred(heroId)).CallDeferred();
        AutoSave("饰品装卸");
    }

    /// <summary>延迟一帧的刷新：**详情仍开着同一个英雄**才刷（那一帧里可能已关详情 ／ 换人 ⇒ 不动别人的屏）✓</summary>
    private void RefreshTrinketsDeferred(string heroId)
    {
        if (!DetailOpen || _detailHeroId != heroId)
        {
            GD.Print($"[M4u·饰品] 延迟刷新跳过：详情={_detailHeroId ?? "（关）"}（装卸的是 {heroId}）⇒ 不刷别人的屏 ✓");
            return;
        }

        RefreshTrinketSlots(heroId);
    }

    /// <summary>回读最近一条饰品事件**原样**（记录 `ToString()`，不是我们拼的第二份文本）；没有 ⇒ 「（无）」✓</summary>
    private string LastTrinketEventText()
    {
        for (int i = _log.Events.Count - 1; i >= 0; i--)
        {
            switch (_log.Events[i])
            {
                case HeroTrinketEquippedEvent e:
                    return e.ToString();
                case HeroTrinketUnequippedEvent u:
                    return u.ToString();
            }
        }

        return "（无）";
    }
}
