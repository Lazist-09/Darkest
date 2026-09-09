namespace Darkest.Core.Events;

/// <summary>
/// Immutable marker base for every event in the battle log (blueprint §6.2:
/// 战斗日志 = 事件流本身; 一处写入、处处只读; zero Godot).
/// Concrete gameplay event types arrive from M2 on; M0 only carries the audit
/// record <see cref="RngDraw"/> plus this base + <see cref="CombatLog"/> skeleton.
/// </summary>
public abstract record BattleEvent
{
    /// <summary>
    /// 0-based append sequence assigned by <see cref="CombatLog"/> at append time.
    /// Immutable once appended (log is the only writer).
    /// </summary>
    public ulong Sequence { get; init; }
}
