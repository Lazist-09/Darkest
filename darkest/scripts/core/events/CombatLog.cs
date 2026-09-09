using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Darkest.Core.Events;

/// <summary>
/// Append-only battle log of immutable events = the single ordered source of
/// truth for UI refresh, replay and Monte-Carlo statistics
/// (blueprint §6.2 / §8.3). One writer (kernel append points), read-only
/// everywhere else. Zero Godot.
/// </summary>
public sealed class CombatLog
{
    private readonly List<BattleEvent> _events = new();
    private ulong _nextSequence;

    public CombatLog()
    {
        Events = new ReadOnlyCollection<BattleEvent>(_events);
    }

    /// <summary>Number of events appended so far.</summary>
    public int Count => _events.Count;

    /// <summary>Read-only projection of every event, in append order (live view).</summary>
    public IReadOnlyList<BattleEvent> Events { get; }

    /// <summary>
    /// Appends an immutable copy of <paramref name="e"/> stamped with the next
    /// sequence number and returns the stamped instance. Events already appended
    /// are never mutated by later appends.
    /// </summary>
    public T Append<T>(T e) where T : BattleEvent
    {
        if (e is null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        T stamped = e with { Sequence = _nextSequence };
        _nextSequence++;
        _events.Add(stamped);
        return stamped;
    }
}
