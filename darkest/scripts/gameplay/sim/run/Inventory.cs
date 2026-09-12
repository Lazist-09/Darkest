using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 背包物品种类（D2 / `#265` 命名裁定）：
/// · **`SupportCrate` = 支援箱**：`while_carried` —— **在包里每回合 +1 SP**；**必需**（不带 ⇒ SP 完全不恢复 ⇒ 支援位废）
/// · **`SupportPack` = 支援包**：**一次性**消耗 → **+2 SP**（走 `SupportPointEvent(reason:"item")`）；可选
/// 🔴 **两个 id 不得混用**（同类教训：`#247` 同名异义 / `#253` 并列不同层 —— 纪律 15）。
/// </summary>
public enum ItemKind
{
    Firewood,
    Food,
    SupportCrate,
    SupportPack,
}

/// <summary>背包里的一格（口粮**不堆叠** ⇒ 一份口粮占一格）。</summary>
public sealed record InventoryItem(ItemKind Kind, string Id);

/// <summary>
/// M7.5 D2 背包（`#260`）：**12 格**、口粮不堆叠、**整备仅出发前（局内不可改）**。
/// 🔴 **包满禁止静默丢弃**（P21 ⑬）—— `TryAdd` 失败时由调用方走 **「丢弃哪一格」选择**流程；
/// 理由（策划 #264）：**静默丢会让"摸黑搏到的补给"拿不回来 ⇒ 光照计的收益端闭环只剩风险**。
/// </summary>
public interface IInventory
{
    int SlotCap { get; }

    int Count { get; }

    bool IsLockedForRun { get; }

    bool IsFull { get; }

    IReadOnlyList<InventoryItem> Slots { get; }

    int CountOf(ItemKind kind);
}

/// <summary>默认实现（纯状态机 + 事件流留痕）。</summary>
public sealed class Inventory : IInventory
{
    private readonly TuningInventory _config;
    private readonly List<InventoryItem> _slots = new();

    public Inventory(TuningInventory config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public int SlotCap => _config.SlotCap;

    public int Count => _slots.Count;

    public bool IsLockedForRun { get; private set; }

    public bool IsFull => _slots.Count >= SlotCap;

    public IReadOnlyList<InventoryItem> Slots => _slots;

    public int CountOf(ItemKind kind) => _slots.Count(s => s.Kind == kind);

    /// <summary>推荐整备配置（柴火 2 / 口粮 9 / 支援包 1 = 恰满 12；数据驱动）。</summary>
    public IReadOnlyList<InventoryItem> RecommendedLoadout()
    {
        var list = new List<InventoryItem>();
        foreach ((string key, int n) in _config.RecommendedLoadout ?? new Dictionary<string, int>())
        {
            ItemKind kind = key switch
            {
                "firewood" => ItemKind.Firewood,
                "food" => ItemKind.Food,
                _ => ItemKind.SupportCrate,
            };
            for (int i = 0; i < n; i++)
            {
                list.Add(new InventoryItem(kind, $"{key}_{i + 1}"));
            }
        }

        return list;
    }

    /// <summary>整备：**仅出发前可配**（局内调用 → 拒绝）。合计超格 → 拒绝且不改。</summary>
    public bool TryConfigure(IReadOnlyList<InventoryItem> items, out string reason)
    {
        if (IsLockedForRun)
        {
            reason = "locked_for_run"; // 🔴 局内不可改（否则"出发前决策"没有分量）
            return false;
        }

        if (items is null || items.Count > SlotCap)
        {
            reason = "over_slot_cap";
            return false;
        }

        _slots.Clear();
        _slots.AddRange(items);
        reason = "configured";
        return true;
    }

    /// <summary>以推荐配置整备（默认）。</summary>
    public bool ConfigureRecommended(out string reason) => TryConfigure(RecommendedLoadout(), out reason);

    /// <summary>出发：锁定整备（局内不可再改）。</summary>
    public void LockForRun() => IsLockedForRun = true;

    /// <summary>
    /// 拾取一格：**满则失败（不静默丢！）** —— 调用方必须走「丢弃哪一格」选择（P21 ⑬）。
    /// </summary>
    public bool TryAdd(InventoryItem item, out string reason)
    {
        if (IsFull)
        {
            reason = "full_choose_discard"; // 🔴 禁止静默丢弃
            return false;
        }

        _slots.Add(item);
        reason = "added";
        return true;
    }

    /// <summary>包满时由玩家**选择丢弃哪一格**（腾格后再拾取）。</summary>
    public bool TryDiscardAt(int index, out InventoryItem? removed)
    {
        removed = null;
        if (index < 0 || index >= _slots.Count)
        {
            return false;
        }

        removed = _slots[index];
        _slots.RemoveAt(index);
        return true;
    }

    /// <summary>就地消耗一份口粮腾格（策划 #264 给的备选路径）。</summary>
    public bool TryConsumeFoodToFreeSlot(out InventoryItem? consumed)
    {
        consumed = null;
        int idx = _slots.FindIndex(s => s.Kind == ItemKind.Food);
        if (idx < 0)
        {
            return false;
        }

        consumed = _slots[idx];
        _slots.RemoveAt(idx);
        return true;
    }

    /// <summary>是否携带 **支援箱**（`support_crate`，D6 #3：其 buff 为 `while_carried` —— **离开背包即失效**）；
    /// 携带时**每回合 +1 SP**（由战斗层按 `SupportPointEvent(reason:"item")` 计入）。</summary>
    public bool CarriesSupportCrate => CountOf(ItemKind.SupportCrate) > 0;

    /// <summary>携带的 **支援包**（`support_pack`，一次性 +2 SP 消耗品）数量。</summary>
    public int SupportPackCount => CountOf(ItemKind.SupportPack);

    /// <summary>消耗 1 个支援包（**一次性 +2 SP**）：扣格成功 ⇒ 调用方按 `SupportPointEvent(reason:"item")` 记 +2。</summary>
    public bool TryUseSupportPack(out InventoryItem? used)
    {
        used = null;
        int idx = _slots.FindIndex(s => s.Kind == ItemKind.SupportPack);
        if (idx < 0)
        {
            return false;
        }

        used = _slots[idx];
        _slots.RemoveAt(idx);
        return true;
    }
}
