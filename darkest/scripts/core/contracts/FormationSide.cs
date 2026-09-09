namespace Darkest.Core.Contracts;

/// <summary>
/// 阵营（data_schema §2.1 `Side`）。JSON 存小写 `player`/`enemy`，由数据层字符串映射到本枚举。
/// </summary>
public enum FormationSide
{
    Player,
    Enemy,
}