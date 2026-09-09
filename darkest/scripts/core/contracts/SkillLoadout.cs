using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkest.Core.Contracts;

/// <summary>
/// 战前配装（T-M3-04 / O-29）：每单位战前从原型技能池 9 选 5，冻结进入 BattleSession，战斗中不可换。
/// </summary>
public sealed record SkillLoadout
{
    public const int CarriedCount = 5;

    public UnitId Unit { get; }

    public IReadOnlyList<string> Carried { get; }

    public SkillLoadout(UnitId unit, IReadOnlyList<string> carried)
    {
        Unit = unit;
        if (carried is null)
        {
            throw new ArgumentNullException(nameof(carried));
        }

        if (carried.Count != CarriedCount)
        {
            throw new ArgumentException($"携带集必须恰为 {CarriedCount} 个技能（实际 {carried.Count}）。");
        }

        if (carried.Distinct().Count() != carried.Count)
        {
            throw new ArgumentException("携带集不得重复。");
        }

        Carried = carried.ToArray();
    }
}