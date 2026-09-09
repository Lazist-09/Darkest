using System;
using Darkest.Core.Events;
using Darkest.Core.Rng;

namespace Darkest.Gameplay.Sim.Director;

/// <summary>
/// Composition-root skeleton of one battle (blueprint §8.1/§4 B3):
/// <c>BattleSession(seed)</c> is the single deterministic entry of a battle;
/// the RNG is injected at this one construction point (实机 BattleRoot 与 headless
/// 直驱共用同一构造点，blueprint §8.3). M0 只保证「seed → RNG → 可注入 + CombatLog」
/// 这条最小确定性链路成立，不含任何业务方法（边界见 T-M0-03 要点 6）。
/// </summary>
public sealed class BattleSession
{
    private readonly long _seed; // seed 只进不出（blueprint §8.1）

    /// <summary>Creates a session and derives its RNG from the seed (default composition).</summary>
    public BattleSession(long seed)
        : this(seed, new RngProvider(seed))
    {
    }

    /// <summary>
    /// Single construction point with explicit injection. <paramref name="seed"/>
    /// is retained for replay identity but never re-derived from; headless drivers
    /// may pass their own provider (e.g. same-seed <see cref="RngProvider"/>).
    /// </summary>
    public BattleSession(long seed, IRngProvider rng)
    {
        if (rng is null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        _seed = seed;
        Rng = rng;
        Log = new CombatLog();
    }

    /// <summary>The injected RNG — kernel's only random outlet for this battle.</summary>
    public IRngProvider Rng { get; }

    /// <summary>The battle's append-only event log (skeleton).</summary>
    public CombatLog Log { get; }
}
