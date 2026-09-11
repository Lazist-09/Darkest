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

    /// <summary>
    /// 回合号（G0/O-55）：由 <see cref="CombatLog"/> 在 append 时按当前回合盖章（1 起）。
    /// 0 = append 时尚无回合（如初始化期）。语义只增不改，便于按回合分组读日志。
    /// </summary>
    public int Round { get; init; }
}
