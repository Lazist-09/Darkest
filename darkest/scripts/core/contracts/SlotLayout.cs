using System;
using System.Collections.Generic;

namespace Darkest.Core.Contracts;

/// <summary>
/// 一侧阵型的槽位布局（data_schema §3.6 `player` / `enemy`）：
/// 我方 {slot_count:6, combat_slots:4, support_slots:[5,6]}；敌方 {4, 4, []}（无支援位）。
/// </summary>
public sealed record SlotLayout
{
    public int SlotCount { get; }

    public int CombatSlots { get; }

    public IReadOnlyList<int> ExtensionSlots { get; }

    public SlotLayout(int slotCount, int combatSlots, IReadOnlyList<int> extensionSlots)
    {
        if (slotCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount), "槽数必须 > 0。");
        }

        if (combatSlots < 0 || combatSlots > slotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(combatSlots),
                $"CombatSlots({combatSlots}) 越界 [0, {slotCount}]。");
        }

        if (extensionSlots is null)
        {
            throw new ArgumentNullException(nameof(extensionSlots));
        }

        var seen = new HashSet<int>();
        var copy = new List<int>(extensionSlots.Count);
        foreach (int slot in extensionSlots)
        {
            if (slot < 1 || slot > slotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(extensionSlots),
                    $"支援位 {slot} 越界 [1, {slotCount}]。");
            }

            if (!seen.Add(slot))
            {
                throw new ArgumentException($"支援位重复：{slot}。");
            }

            copy.Add(slot);
        }

        SlotCount = slotCount;
        CombatSlots = combatSlots;
        ExtensionSlots = copy.AsReadOnly();
    }
}