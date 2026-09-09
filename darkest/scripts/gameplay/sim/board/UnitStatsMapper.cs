using Darkest.Core.Contracts;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Board;

/// <summary>units.json 原型 → 内核只读属性模板的映射（sim/board 依赖 B1 数据层，允许方向）。</summary>
public static class UnitStatsMapper
{
    public static UnitStats From(UnitConfig c)
        => new(
            Hp: c.Hp,
            Attack: c.Attack,
            PhysDef: c.PhysDef,
            Speed: c.Speed,
            Dodge: c.Dodge,
            Crit: c.Crit,
            Resilience: c.Resilience,
            StunResist: c.StunResist,
            BleedResist: c.BleedResist,
            StatDebuffResist: c.StatDebuffResist,
            DisplaceResist: c.DisplaceResist,
            DeathsDoorResist: c.DeathsDoorResist);
}