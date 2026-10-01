using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **M4 · Trinket（饰品）状态半**（`dd1_workstreams.md §3` · **M4u** · 2026-10-01）——
/// `Roster` 的 partial 第四片（`Roster.cs` 状态 ／ `Roster.Save.cs` 存档 ／ `Roster.Quirks.cs` 怪癖 ／ **本文件 饰品**）✓
///
/// <para>**为什么住在名册**：契约 `doc/modules/trinkets.md §3` 明写 **不要 `TrinketLedger`** ——
/// 「哪个英雄身上有哪几件饰品」是**跨趟玩家状态**（入档），与士气 / 经验 / 疾病 / 怪癖**同层同型** ✓</para>
///
/// <para>**每英雄 2 槽**（契约 §3 · 策划 `#437`）；槽位**从 1 起**（与事件字段同一口径 ✓）。
/// 内部用 `List&lt;string&gt;` **保序**（= 槽位稳定）—— 字典的键序在 JSON 里不保证，列表才让存档可 diff ✓</para>
///
/// <para>🔴 **拒绝理由【只在本文件生产】**（红线 21 (b)）：`CanEquipTrinket` 是**唯一判据落点**，
/// `EquipTrinket` 与 UI **共用它** ⇒ 不存在"UI 自己再判一遍"的第二套真值 ✓
/// 返回值契约：**`null` = 可以装 / 已装上**；**非 null = 拒绝理由**（人类可读，UI 原样显示）✓</para>
///
/// <para>⚠️ **`limit`（同件持有上限）本件不判**：我们还没有"饰品库存 / 副本"概念
/// （入手路径未接线 —— 冒烟走 `--hamlet-trinket-seed` 的**公开 API 播种**）
/// ⇒ 凭空判 `limit` 只会造一条**玩家永远触发不到的规则** ⇒ 登记待策划（不预造 API）✓</para>
/// </summary>
public sealed partial class Roster
{
    /// <summary>🔴 每英雄的饰品格数（契约 §3 · `#437`：**2 槽**）✓</summary>
    public const int MaxTrinketSlots = 2;

    private readonly Dictionary<string, List<string>> _trinkets = new(StringComparer.Ordinal);

    /// <summary>
    /// 某英雄当前所持饰品（**按槽位顺序**；没有 ⇒ 空表，不抛 —— 与 `QuirksOf`/`DiseasesOf` 同一口径）✓
    /// </summary>
    public IReadOnlyList<string> TrinketsOf(string heroId)
    {
        _ = MoraleOf(heroId); // 顺带校验英雄存在（同 `QuirksOf`）✓
        return _trinkets.TryGetValue(heroId, out List<string>? list) ? list : Array.Empty<string>();
    }

    /// <summary>
    /// 🔴 **能不能装**（**只读**：不改任何状态）—— 拒绝理由的**唯一生产点** ✓
    /// <para>`null` = 可以装；非 null = 理由（① 非本职业 ② 同件重复 ③ 槽满）✓</para>
    /// <para>⚠️ `trinketId` 不在库里 ⇒ `cfg.Get` **当场抛**（fail-fast，不静默当"不可装"）✓</para>
    /// <para>🔴 **为什么要这个只读口**：UI 要把"装不上的"**置灰 + 原样显示理由**（契约 T4），
    /// 而"试着装一下看返回什么"会**真改状态** ⇒ 必须有一个**不写状态**的同一判据 ✓</para>
    /// </summary>
    public string? CanEquipTrinket(TrinketsConfig cfg, string heroId, string trinketId)
    {
        if (cfg is null)
        {
            throw new ArgumentNullException(nameof(cfg));
        }

        HeroConfig hero = HeroOf(heroId);          // 不存在 ⇒ 抛（fail-fast）✓
        TrinketConfig t = cfg.Get(trinketId);      // 库里没有 ⇒ 抛（fail-fast）✓

        if (t.HeroClassRequirements.Count > 0
            && !t.HeroClassRequirements.Contains(hero.Archetype, StringComparer.Ordinal))
        {
            return $"「{hero.Name}」（{hero.Archetype}）职业不符：「{t.Id}」限定 " +
                   $"{string.Join(" / ", t.HeroClassRequirements)}";
        }

        IReadOnlyList<string> held = TrinketsOf(heroId);
        if (held.Contains(trinketId, StringComparer.Ordinal))
        {
            return $"「{hero.Name}」已经装着「{t.Id}」—— 同件不重复装";
        }

        if (held.Count >= MaxTrinketSlots)
        {
            return $"「{hero.Name}」的饰品格已满（{MaxTrinketSlots} 格）—— 先卸下一件再装「{t.Id}」";
        }

        return null;
    }

    /// <summary>
    /// **装上一件饰品**：判据全走 `CanEquipTrinket`（**同一份理由**，本方法不另判一次）✓
    /// <para>🔴 成功 ⇒ **必写 `HeroTrinketEquippedEvent`**（`Slot` 从 **1** 起）——
    /// 饰品是跨趟状态，"变更必留痕"与怪癖 / 疾病同一条纪律 ✓</para>
    /// <para>⚠️ **不管"能否购买"**（`TrinketsConfig.IsPurchasable` 只用于商店列表）：
    /// 战利品 / 任务奖励 / 事件给的不可购买件**照样能装** ✓</para>
    /// </summary>
    public string? EquipTrinket(CombatLog log, TrinketsConfig cfg, string heroId, string trinketId, string reason)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        string? refused = CanEquipTrinket(cfg, heroId, trinketId);
        if (refused is not null)
        {
            return refused; // 拒绝理由**原样**回传（UI 直显；不静默、不改写）✓
        }

        if (!_trinkets.TryGetValue(heroId, out List<string>? list))
        {
            list = new List<string>();
            _trinkets[heroId] = list;
        }

        list.Add(trinketId);
        log.Append(new HeroTrinketEquippedEvent(heroId, trinketId, list.Count, reason));
        return null;
    }

    /// <summary>
    /// **卸下一件饰品**（没装 ⇒ no-op 返回 `false`）；变更**必写 `HeroTrinketUnequippedEvent`**
    /// （`Slot` = **卸下前**所在槽，从 1 起）✓
    /// </summary>
    public bool UnequipTrinket(CombatLog log, string heroId, string trinketId, string reason)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        _ = MoraleOf(heroId);
        if (!_trinkets.TryGetValue(heroId, out List<string>? list))
        {
            return false;
        }

        int idx = list.IndexOf(trinketId);
        if (idx < 0)
        {
            return false;
        }

        int slot = idx + 1;
        list.RemoveAt(idx);
        if (list.Count == 0)
        {
            _trinkets.Remove(heroId);   // 空表不留孤儿条目（与阵亡清理同一条：存档不涨）✓
        }

        log.Append(new HeroTrinketUnequippedEvent(heroId, trinketId, slot, reason));
        return true;
    }

    /// <summary>按 id 取英雄（不存在 ⇒ 抛 —— 与 `MoraleOf` 同口径，fail-fast）✓</summary>
    private HeroConfig HeroOf(string heroId)
    {
        HeroConfig? hero = _heroes.FirstOrDefault(h => h.Id == heroId);
        if (hero is null)
        {
            throw new InvalidOperationException($"名册里没有英雄「{heroId}」（M8.0 / #287）。");
        }

        return hero;
    }
}
