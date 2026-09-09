using Darkest.Core.Contracts;

namespace Darkest.Gameplay.Sim.Board;

/// <summary>
/// 战斗单位运行时最小实体（T-M1-01 产出定义，本卡不得超前实现 M2/M4 属性）：
/// M1 只承载 Id / Side / Weak 占位（Weak 供向中靠齐读取；写入方 = M4 士气管线，
/// M1 测试以夹具直接置位验证）。
/// </summary>
public sealed class UnitRuntime
{
    public UnitRuntime(UnitId id, FormationSide side, bool weak = false)
    {
        Id = id;
        Side = side;
        Weak = weak;
    }

    /// <summary>原型 id（M1 阶段编制引用，data_schema §3.6）。</summary>
    public UnitId Id { get; }

    /// <summary>本单位所属阵营。</summary>
    public FormationSide Side { get; }

    /// <summary>虚弱标记：靠齐时编号不得减小（换人保护，formation.md §3）。</summary>
    public bool Weak { get; set; }

    public override string ToString() => $"{Id}@({Side}){(Weak ? ":W" : "")}";
}