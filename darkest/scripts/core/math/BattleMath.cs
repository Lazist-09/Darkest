namespace Darkest.Core.Math;

// NOTE: 本命名空间名含 "Math"，会遮蔽 System.Math 的简单名解析，故下方一律用全限定 System.Math.*。

/// <summary>
/// BattleMath: pure-function library transcribing combat_math formulas verbatim
/// (blueprint §10「公式单测可复算」; zero Godot). M0 只落最小入口（物理/精神单次伤害）；
/// M2 扩为完整公式库（钳制/下限/减免极值/多段等）。
/// 每行公式以注释回溯 [设计] doc/modules/combat_math.md 对应 §。
/// </summary>
public static class BattleMath
{
    /// <summary>
    /// Physical one-hit damage — combat_math §2.1 + §2.3.
    /// <code>
    /// 基础值 = 攻击 × 技能倍率
    /// 减免率 = 物防 / (物防 + 30)                     // 递减公式，非减法
    /// 实际伤害 = round(基础值 × (1 − 减免率) × 暴击倍率 × 伤害浮动)，下限 1
    /// </code>
    /// 伤害浮动默认 1.0（关闭，O-01）；暴击倍率默认 1.0（未暴击）。
    /// </summary>
    public static int PhysicalHit(
        int attack,
        double skillMultiplier,
        int defense,
        double critMultiplier = 1.0,
        double damageFloat = 1.0)
    {
        double baseValue = attack * skillMultiplier;              // §2.1 基础值
        double mitigation = defense / (double)(defense + 30);     // §2.1 减免率（递减，收敛）
        double raw = baseValue * (1.0 - mitigation)               // §2.1 实际伤害
                   * critMultiplier
                   * damageFloat;
        return RoundDamage(raw);                                  // §2.3
    }

    /// <summary>
    /// Spirit one-hit damage — combat_math §2.2 + §2.3 (#158 连续公式).
    /// <code>
    /// 精神减免 = 韧性 / 250，上限 40%
    /// 基础值   = 攻击 × 技能倍率
    /// 实际伤害 = round(基础值 × (1 − 精神减免) × 暴击倍率 × 伤害浮动)，下限 1
    /// </code>
    /// 韧性 50 ⇒ 20% 减免（锚点与原 40~70 档一致）。
    /// </summary>
    public static int SpiritHit(
        int attack,
        double skillMultiplier,
        int resilience,
        double critMultiplier = 1.0,
        double damageFloat = 1.0)
    {
        double baseValue = attack * skillMultiplier;              // §2.2 基础值
        double mitigation = System.Math.Min(resilience / 250.0, 0.40);   // §2.2 精神减免，上限 40%
        double raw = baseValue * (1.0 - mitigation)               // §2.2 实际伤害
                   * critMultiplier
                   * damageFloat;
        return RoundDamage(raw);                                  // §2.3
    }

    /// <summary>
    /// Damage rounding — combat_math §2.3: round() 四舍五入（AwayFromZero，钉死"非截断"），
    /// 任何来源伤害最低 1 点。
    /// </summary>
    private static int RoundDamage(double raw)
    {
        if (raw <= 0.0)
        {
            return 1;
        }

        int rounded = (int)System.Math.Round(raw, System.MidpointRounding.AwayFromZero);
        return System.Math.Max(1, rounded);
    }
}
