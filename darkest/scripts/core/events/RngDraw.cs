namespace Darkest.Core.Events;

/// <summary>
/// One audited RNG draw (blueprint §8.1): every draw carries the provider's
/// monotonic serial number (DrawCount) plus the drawn value, so the log is
/// replayable and auditable ("谁消耗了随机"). Placeholder for M2+.
/// </summary>
/// <param name="DrawCount">Provider audit serial of this draw (1-based: value of
/// <c>IRngProvider.DrawCount</c> right after the draw).</param>
/// <param name="Value">Raw drawn value (percent rolls in [0,100); int rolls as-is).</param>
public sealed record RngDraw(ulong DrawCount, double Value) : BattleEvent;
