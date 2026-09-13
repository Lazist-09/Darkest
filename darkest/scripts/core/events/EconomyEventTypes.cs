namespace Darkest.Core.Events;

/// <summary>
/// M8.0 ②（`#283` 7.1）：**金钱变更事件**（跨趟经济的最小可审计单元）。
/// 🔴 纪律：UI / 报告要显示的金钱数字**必须能从事件流算出**（`Total` 是冗余但可对账，同 `TurnSkippedEvent.MoraleDelta` 的处理）。
/// </summary>
public sealed record GoldChangedEvent(int Delta, string Reason, int Total) : BattleEvent;

/// <summary>
/// M8.0 ④（`#283` 7.3）：**减压结算事件** —— 记录花掉的金钱、恢复的士气、以及
/// **下一趟开局惩罚是否触发**（`PenaltyRoll` 的值一并留痕，供"同价同效、风险不同"可审计）。
/// </summary>
public sealed record StressReliefEvent(
    string Building,
    string Hero,
    int Cost,
    int MoraleRestored,
    double PenaltyRoll,
    bool PenaltyTriggered,
    int NextRunPenalty) : BattleEvent;

