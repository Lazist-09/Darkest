namespace Darkest.Core.Rng;

/// <summary>
/// Deterministic RNG contract — the kernel's single random outlet
/// (blueprint §8.1「唯一随机出口 = IRngProvider」/ §9.8 signature; zero Godot).
/// </summary>
/// <remarks>
/// Dice convention (combat_math §0): every roll is rand(0,100), compared with
/// <c>&lt;</c> (roll lower than the threshold = success). The provider never
/// reseeds itself; one explicit seed per battle enters via <c>BattleSession(seed)</c>.
/// </remarks>
public interface IRngProvider
{
    /// <summary>Roll in [0, 100).</summary>
    double NextPercent();

    /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
    int NextInt(int minInclusive, int maxExclusive);

    /// <summary>
    /// Monotonic audit counter: total draws issued so far (blueprint §8.1).
    /// Starts at 0 before the first draw and increases by exactly 1 per draw,
    /// so every roll is auditable (serial number written to the CombatLog).
    /// </summary>
    ulong DrawCount { get; }
}
