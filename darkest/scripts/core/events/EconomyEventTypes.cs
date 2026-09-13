namespace Darkest.Core.Events;

/// <summary>
/// M8.0 ②（`#283` 7.1）：**金钱变更事件**（跨趟经济的最小可审计单元）。
/// 🔴 纪律：UI / 报告要显示的金钱数字**必须能从事件流算出**（`Total` 是冗余但可对账，同 `TurnSkippedEvent.MoraleDelta` 的处理）。
/// </summary>
public sealed record GoldChangedEvent(int Delta, string Reason, int Total) : BattleEvent;

/// <summary>
/// M8.0（`#287` = **`#245` 的落地**）：**英雄士气变更事件** —— 士气是**跨趟状态**（存于名册），
/// 每次变更必写事件（数字必须来自事件流；`Total` 冗余但可对账）。
/// </summary>
public sealed record HeroMoraleChangedEvent(string HeroId, int Delta, int Total, string Reason) : BattleEvent;

/// <summary>
/// M8.0 ⑤（`#283` 硬要求③）：**招募事件** —— 招募**免费**（`Cost` 恒 0）、新兵 `level == 1`、`morale == 50`。
/// 事件留下新兵的等级与士气，便于验收"**补的人不比老的强**"。
/// </summary>
public sealed record HeroRecruitedEvent(string HeroId, string Name, string Archetype, int Level, int Morale, int Cost) : BattleEvent;


public sealed record StressReliefEvent(
    string Building,
    string Hero,
    int Cost,
    int MoraleRestored,
    double PenaltyRoll,
    bool PenaltyTriggered,
    int NextRunPenalty) : BattleEvent;

