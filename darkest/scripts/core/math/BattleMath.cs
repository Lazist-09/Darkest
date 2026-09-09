namespace Darkest.Core.Math;

// NOTE: 本命名空间名含 "Math"，会遮蔽 System.Math 的简单名解析，故下方一律用全限定 System.Math.*。

/// <summary>
/// BattleMath: pure-function library transcribing combat_math formulas verbatim
/// (blueprint §10「公式单测可复算」; zero Godot). M0 落最小入口；M2（T-M2-01）扩为
/// 完整公式库：命中 / 物理与精神减免 / 附加概率 / 死门存活概率 / 速度浮动 / 取整下限。
/// 每行公式以注释回溯 [设计] doc/modules/combat_math.md 对应 §。
/// 全部数值常量经调用点由 BalanceTable（tuning 快照）显式传入——本库零硬编码起手值
/// （blueprint §7 违规判据）。
/// </summary>
public static class BattleMath
{
    // ------------------------------------------------------------------
    // 命中（combat_math §1 / tuning hit_clamp）
    // ------------------------------------------------------------------

    /// <summary>命中率 = 100 − 目标闪避 + 技能命中修正，钳制 [clampMin, clampMax]（默认 [55,100]）。</summary>
    public static int HitRate(int dodge, int hitMod, int clampMin = 55, int clampMax = 100)
        => System.Math.Clamp(100 - dodge + hitMod, clampMin, clampMax);

    /// <summary>物理减免率（§2.1 递减公式，高防边际递减、收敛）：physDef / (physDef + 30)。</summary>
    public static double PhysicalMitigation(int physDef)
        => physDef / (double)(physDef + 30);

    /// <summary>精神减免率（§2.2 / #158 连续公式）：min(韧性/250, capPercent%)。韧性 50 → 20%。</summary>
    public static double MentalMitigation(int resilience, int divisor = 250, int capPercent = 40)
    {
        double ratio = resilience / (double)divisor;
        double cap = capPercent / 100.0;
        return System.Math.Min(ratio, cap);
    }

    /// <summary>附加效果实际触发率（combat_math §4，乘法）：labeled × (1 − resist/100)。</summary>
    public static double ActualEffectChance(int labeledPercent, int resistPercent)
        => labeledPercent * (1.0 - resistPercent / 100.0);

    /// <summary>死门存活概率（combat_math §6 / #123）：抗性 − (折磨中 ? afflictionPenalty% : 0)。判定：rand &lt; 该值。</summary>
    public static int DeathDoorSurvivePercent(int resistPercent, bool afflicted, int afflictionPenaltyPercent = 10)
        => resistPercent - (afflicted ? afflictionPenaltyPercent : 0);

    /// <summary>速度浮动乘子（#163，tuning speed_float）：1 + unitRoll×(percent/100)；unitRoll ∈ [0,1)。</summary>
    public static double SpeedFloatMultiplier(double unitRoll, int percent = 10)
        => 1.0 + unitRoll * (percent / 100.0);

    // ------------------------------------------------------------------
    // 伤害（§2.1 / §2.2 / §2.3）
    // ------------------------------------------------------------------

    /// <summary>
    /// Physical one-hit damage — combat_math §2.1 + §2.3.
    /// <code>
    /// 基础值 = 攻击 × 技能倍率
    /// 减免率 = 物防 / (物防 + 30)
    /// 实际伤害 = round(基础值 × (1 − 减免率) × 暴击倍率 × 伤害浮动)，下限 damageFloor
    /// </code>
    /// 伤害浮动默认 1.0（关闭，O-01）；暴击倍率默认 1.0（未暴击）。
    /// </summary>
    public static int PhysicalHit(
        int attack,
        double skillMultiplier,
        int defense,
        double critMultiplier = 1.0,
        double damageFloat = 1.0,
        int damageFloor = 1)
    {
        double baseValue = attack * skillMultiplier;              // §2.1 基础值
        double mitigation = PhysicalMitigation(defense);          // §2.1 减免率（递减，收敛）
        double raw = baseValue * (1.0 - mitigation)               // §2.1 实际伤害
                   * critMultiplier
                   * damageFloat;
        return ApplyDamageRounding(raw, damageFloor);             // §2.3
    }

    /// <summary>
    /// Spirit one-hit damage — combat_math §2.2 + §2.3 (#158 连续公式).
    /// <code>
    /// 精神减免 = 韧性 / 250，上限 capPercent%
    /// 基础值   = 攻击 × 技能倍率
    /// 实际伤害 = round(基础值 × (1 − 精神减免) × 暴击倍率 × 伤害浮动)，下限 damageFloor
    /// </code>
    /// 韧性 50 ⇒ 20% 减免（锚点与原 40~70 档一致）。
    /// </summary>
    public static int SpiritHit(
        int attack,
        double skillMultiplier,
        int resilience,
        double critMultiplier = 1.0,
        double damageFloat = 1.0,
        int damageFloor = 1)
    {
        double baseValue = attack * skillMultiplier;              // §2.2 基础值
        double mitigation = MentalMitigation(resilience);         // §2.2 精神减免，上限 40%
        double raw = baseValue * (1.0 - mitigation)               // §2.2 实际伤害
                   * critMultiplier
                   * damageFloat;
        return ApplyDamageRounding(raw, damageFloor);             // §2.3
    }

    /// <summary>
    /// Damage rounding — combat_math §2.3: round() 四舍五入（AwayFromZero，钉死"非截断"），
    /// 任何来源伤害最低 damageFloor 点（默认 1，调用点显式传 BalanceTable.DamageFloor）。
    /// </summary>
    public static int ApplyDamageRounding(double raw, int damageFloor = 1)
    {
        int rounded = (int)System.Math.Round(raw, System.MidpointRounding.AwayFromZero);
        return System.Math.Max(damageFloor, rounded);
    }
}