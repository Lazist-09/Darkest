using System.Collections.Generic;
namespace Darkest.Core.Contracts;

/// <summary>
/// buff 台账契约（blueprint §9.6）：buff=修改器+钩子+生命周期（buff.md §1）；
/// 叠层：refresh（刷新时长）/ stack（叠层上限）/ none（上限 1，#94）；属性修正加法先于乘法；
/// 驱散 1 次 1 个。M4 最小实现承载挂/摘/查询/叠层/护盾次数与终态加成。
/// </summary>
public interface IBuffLedger
{
    void Add(UnitId u, string buffId, UnitId? source);
    void Remove(UnitId u, string buffId);
    bool Has(UnitId u, string buffId);
    IReadOnlyList<string> Buffs(UnitId u);

    /// <summary>护盾剩余次数（shield）；无 → 0。</summary>
    int Charges(UnitId u, string buffId);

    /// <summary>消耗 1 次（AOE 算 1 次攻击，combat_math §8.1）；归 0 移除。</summary>
    void ConsumeCharge(UnitId u, string buffId);

    /// <summary>属性修正累加（攻/防/韧/速），tuning 通用 −3 与技能标注 −15/−10 并存（O-23）。</summary>
    int StatModTotal(UnitId u, string stat);

    /// <summary>状态标记（如 stunned / immune_fear）。</summary>
    bool HasStateFlag(UnitId u, string flag);

    /// <summary>清除形态标记一次（眩晕=跳过本次行动随即结束）。</summary>
    void ClearStateFlag(UnitId u, string flag);

    /// <summary>per-cent 修改器汇总（如 勇猛 dealt_damage_mult +25）。</summary>
    int PercentMod(UnitId u, string effect);

    /// <summary>回合推进：Rounds 型时长递减，归零移除（回合钩子由导演调用）。</summary>
    void TickRounds();

    /// <summary>driving flag: 是否已有（供 RollCollapse 判断美德上限/折磨替换）。</summary>
    bool AnyOfKind(UnitId u, string kindPrefix);
}