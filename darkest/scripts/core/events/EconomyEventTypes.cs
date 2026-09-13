namespace Darkest.Core.Events;

/// <summary>
/// M8.0 ②（`#283` 7.1）：**金钱变更事件**（跨趟经济的最小可审计单元）。
/// 🔴 纪律：UI / 报告要显示的金钱数字**必须能从事件流算出**（`Total` 是冗余但可对账，同 `TurnSkippedEvent.MoraleDelta` 的处理）。
/// </summary>
public sealed record GoldChangedEvent(int Delta, string Reason, int Total) : BattleEvent;
