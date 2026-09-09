using System;

namespace Darkest.Core.Rng;

/// <summary>
/// Fixed-seed deterministic RNG (SplitMix64), blueprint §8.1/§9.8.
/// Same seed ⇒ same draw sequence on every run of the same runtime; algorithm is an
/// implementation choice (T-M0-03 风险/开放：替换算法不影响契约，判据 = 同 seed 同序列).
/// Thread-unsafe by design: one provider per battle session, used on a single thread.
/// </summary>
public sealed class RngProvider : IRngProvider
{
    // SplitMix64 constants.
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private const double TwoPow64 = 18446744073709551616.0; // 2^64 (exact in double)

    private ulong _state;
    private ulong _drawCount;

    /// <summary>Creates a provider from one explicit battle seed. Seed never escapes.</summary>
    public RngProvider(long seed)
    {
        _state = (ulong)seed;
        // Warm-up mixes consecutive low seeds apart before any audited draw.
        _ = Mix();
        _ = Mix();
    }

    /// <inheritdoc />
    public ulong DrawCount => _drawCount;

    /// <inheritdoc />
    public double NextPercent()
    {
        ulong raw = Mix();
        _drawCount++;
        // Map the 64-bit uniform to [0, 100).
        return raw / TwoPow64 * 100.0;
    }

    /// <inheritdoc />
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                $"maxExclusive ({maxExclusive}) must be greater than minInclusive ({minInclusive}).");
        }

        ulong span = (ulong)((long)maxExclusive - (long)minInclusive);
        ulong raw = Mix();
        _drawCount++;
        // Modulo bias ≤ span / 2^64 — negligible for this project's ranges (≤ a few thousand).
        return minInclusive + (int)(raw % span);
    }

    private ulong Mix()
    {
        _state += Gamma;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
