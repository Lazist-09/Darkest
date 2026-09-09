namespace Darkest.Core.Contracts;

/// <summary>
/// 单位静态属性模板（data_schema §3.1 units.json 逐字段映射；百分比存整数百分数）。
/// 运行期实例从模板复制基础值（blueprint §7：运行时模板【只读】，绝不在模板上写回）。
/// </summary>
public sealed record UnitStats(
    int Hp,
    int Attack,
    int PhysDef,
    int Speed,
    int Dodge,
    int Crit,
    int Resilience,
    int StunResist,
    int BleedResist,
    int StatDebuffResist,
    int DisplaceResist,
    int? DeathsDoorResist)
{
    public bool HasDeathsDoor => DeathsDoorResist is not null;
}