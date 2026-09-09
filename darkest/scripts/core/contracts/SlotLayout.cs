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

    public IReadOnlyList<int> SupportSlots { get; }

    public SlotLayout(int slotCount, int combatSlots, IReadOnlyList<int> supportSlots)
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

        if (supportSlots is null)
        {
            throw new ArgumentNullException(nameof(supportSlots));
        }

        var seen = new HashSet<int>();
        var copy = new List<int>(supportSlots.Count);
        foreach (int slot in supportSlots)
        {
            if (slot < 1 || slot > slotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(supportSlots),
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
        SupportSlots = copy.AsReadOnly();
    }
}