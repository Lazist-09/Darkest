namespace Darkest.Core.Contracts;

/// <summary>
/// 单位标识。M1 阶段 = units.json 原型 id（编成引用，data_schema §3.6：`unit` 列）；
/// 若 M2/M3 引入战斗实例 id，此处迁移须评审并同步 IFormation.UnitAt 语义。
/// </summary>
public readonly record struct UnitId(string Value)
{
    public static UnitId Of(string value) => new(value);

    public override string ToString() => Value;
}