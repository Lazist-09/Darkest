using System.Collections.Generic;
using Darkest.Core.Rng;

namespace Darkest.Core.Contracts;

/// <summary>
/// 行动序列契约（blueprint §9.2 / T-M2-03）：
/// 实际速度 = 基础×(1 + 0~10% 随机) 每回合重掷（#163）；同速编号小者先动（#80）；
/// 眩晕 = 跳过本次行动、状态随即结束（GDD §2.5）；死亡/离场实时剔除（OnRemoved）。
/// </summary>
public interface ITurnSequencer
{
    /// <summary>每回合开始调用一次：对全部在列单位重掷速度浮动并排序，返回本回合行动顺序（固定枚举序抽取）。</summary>
    IReadOnlyList<UnitId> BuildRoundOrder(IRngProvider rng);

    /// <summary>依序出列；眩晕单位被跳过（并清除眩晕标记）。</summary>
    UnitId? NextActor();

    /// <summary>死亡/离场实时从剩余序列剔除。</summary>
    void OnRemoved(UnitId who);
}