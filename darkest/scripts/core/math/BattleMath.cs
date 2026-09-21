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
    public static int HitRate(int dodge, int hitMod, int clampMin, int clampMax)
        => System.Math.Clamp(100 - dodge + hitMod, clampMin, clampMax);

    /// <summary>物理减免率（§2.1 递减公式，高防边际递减、收敛）：physDef / (physDef + 30)。</summary>
    // 🔴 数字外置（用户 2026-09-14）：`divisor`（原先硬编码 30）**必填** —— 由 `BalanceTable.PhysicalMitigationDivisor`
    //    从 `tuning.json` 的 `physical_mitigation.divisor` 传入 ✓（值不变 = 零数值改动）
    // 🔴 入参 `physDef` 来自**我们自加**的 `Prot`；原版减伤走 `prot`（比例）⇒ 待裁：`reports/def_merge_three_plans.md` ✓
    public static double PhysicalMitigation(int physDef, int divisor)
        => physDef / (double)(physDef + divisor);

    /// <summary>精神减免率（§2.2 / #158 连续公式）：min(韧性/250, capPercent%)。韧性 50 → 20%。</summary>
    // 🔴 数字外置纪律（用户 2026-09-14）：**平衡数字不得以 C# 默认参数形式存在** ——
    //    `divisor` / `capPercent` 必须由调用方从 `BalanceTable`（⇒ `tuning.json`）传入 ✓
    //    （我此前留的默认值 250/40 被 `SpiritHit` **省略参数时真的用到** ⇒ 那就是硬编码的平衡数字 ⚠️）
    public static double MentalMitigation(int resilience, int divisor, int capPercent)
    {
        double ratio = resilience / (double)divisor;
        double cap = capPercent / 100.0;
        return System.Math.Min(ratio, cap);
    }

    /// <summary>附加效果实际触发率（combat_math §4，乘法）：labeled × (1 − resist/100)。</summary>
    public static double ActualEffectChance(int labeledPercent, int resistPercent)
        => labeledPercent * (1.0 - resistPercent / 100.0);

    /// <summary>死门存活概率（combat_math §6 / #123）：抗性 − (折磨中 ? afflictionPenalty% : 0)。判定：rand &lt; 该值。</summary>
    public static int DeathDoorSurvivePercent(int resistPercent, bool afflicted, int afflictionPenaltyPercent)
        => resistPercent - (afflicted ? afflictionPenaltyPercent : 0);

    /// <summary>速度浮动乘子（#163，tuning speed_float）：1 + unitRoll×(percent/100)；unitRoll ∈ [0,1)。</summary>
    public static double SpeedFloatMultiplier(double unitRoll, int percent)
        => 1.0 + unitRoll * (percent / 100.0);

    /// <summary>
    /// 撤退基础成功率（O-11/#169 拍板）：50% + (我方存活平均实际速度 − 敌方存活平均实际速度) × 4%，
    /// 钳制 [clampMin, clampMax]。速度差每 1 点 ±perSpeedDiffPercent%，"先杀最快敌人再撤"成立。
    /// 🔴 数字外置（用户 2026-09-14）：`basePercent/perSpeedDiffPercent/clampMin/clampMax` **必填**
    ///    —— 它们住在 `tuning.json` 的 `retreat_formula`（经 `BalanceTable.RetreatFormula` 传入）✓
    /// </summary>
    public static double RetreatBaseRate(
        double avgSpeedDiff,
        int basePercent,
        int perSpeedDiffPercent,
        int clampMin,
        int clampMax)
        => System.Math.Clamp(basePercent + avgSpeedDiff * perSpeedDiffPercent, (double)clampMin, clampMax);

    /// <summary>
    /// 撤退最终成功率（#169）：基础率 + uniform(−randomRange, +randomRange)，钳制 [randClampMin, randClampMax]。
    /// unitRoll ∈ [0,1)，由注入 RNG 提供（M5 调用点）。
    /// 🔴 数字外置：`randomRange/randClampMin/randClampMax` **必填**（同样来自 `retreat_formula`）✓
    /// </summary>
    public static double RetreatFinalRate(
        double baseRate,
        double unitRoll,
        int randomRange,
        int randClampMin,
        int randClampMax)
        => System.Math.Clamp(baseRate + (unitRoll * 2.0 - 1.0) * randomRange, (double)randClampMin, randClampMax);

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
        int mitigationDivisor,
        double critMultiplier = 1.0,
        double damageFloat = 1.0,
        int damageFloor = 1)
    {
        double baseValue = attack * skillMultiplier;              // §2.1 基础值
        double mitigation = PhysicalMitigation(defense, mitigationDivisor); // §2.1 减免率（除数来自 data ✓）
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
        int mentalDivisor,
        int mentalCapPercent,
        double critMultiplier = 1.0,
        double damageFloat = 1.0,
        int damageFloor = 1)
    {
        double baseValue = attack * skillMultiplier;              // §2.2 基础值
        double mitigation = MentalMitigation(resilience, mentalDivisor, mentalCapPercent); // §2.2 精神减免（数字来自 data ✓）
        double raw = baseValue * (1.0 - mitigation)               // §2.2 实际伤害
                   * critMultiplier
                   * damageFloat;
        return ApplyDamageRounding(raw, damageFloor);             // §2.3
    }

    /// <summary>
    /// Damage rounding — combat_math §2.3: round() 四舍五入（AwayFromZero，钉死"非截断"），
    /// 任何来源伤害最低 damageFloor 点（默认 1，调用点显式传 BalanceTable.DamageFloor）。
    /// </summary>
    // ═══════════════════════════════════════════════════════════════════════════════════
    // 🆕 **M1c 阶段 1（换伤害模型 · 零行为改动）**：原版口径 = **武器区间 × (1 + 技能 dmg%)**
    //   🔴 **本阶段只加纯函数、不接线**（没有任何调用方 ⇒ 旧读数必须不变 ✓）
    //   来源（参考项目一手，`Assets/Resources/Data/Heroes/Info/*.bytes`）：
    //     `weapon: .atk 0% .dmg 6 12 .crit 3% .spd 1`　·　`combat_skill: .dmg 0% / -40% / -75%`
    //   ⇒ 命中修正 = `weapon.atk%`（**不是**我们现用的 `Attack` 直乘）· 伤害 = **区间随机值** × 技能百分比修正 ✓
    //   ⚠️ 与本项目现模型（`DamageStep`：`EffectiveAttack × 段倍率 × (1−减伤) × …`）**是两种形状** ✓
    //      ⇒ 所以 M1c 才要"三步走"：**阶段 2 试点对照 → 阶段 3 才切默认**（走解冻四件）✓
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 🆕 **M1c · 武器区间取值**：`[dmgMin, dmgMax]` 上按 `roll01 ∈ [0,1)` 线性取值（连续，原版 `DamageLow/High` 是浮点）✓
    /// 🔴 纯函数（无随机源依赖）⇒ **可确定性测试** ✓；区间非法（min &gt; max）⇒ 按 min 处理（不抛，交给数据校验）✓
    /// </summary>
    public static double WeaponRoll(int dmgMin, int dmgMax, double roll01)
    {
        if (dmgMax <= dmgMin)
        {
            return dmgMin;
        }

        double r = System.Math.Clamp(roll01, 0.0, 1.0);   // ⚠️ 本文件就在 `Darkest.Core.Math` 里 ⇒ 必须限定 `System.Math`（同名遮蔽，我踩过两次）
        return dmgMin + ((dmgMax - dmgMin) * r);
    }

    /// <summary>
    /// 🆕 **M1c · 原版伤害模型**：`武器区间 × (1 + 技能 dmg%)` —— **未接任何调用方**（阶段 1）✓
    /// </summary>
    /// <param name="dmgMin">武器该阶 `dmg_min`（参考项目 `weapon.dmg` 左值）✓</param>
    /// <param name="dmgMax">武器该阶 `dmg_max` ✓</param>
    /// <param name="skillDmgPct">技能 `dmg%`（参考项目例：`smite 0%` · `zealous_accusation -40%` · `stunning_blow -75%`）✓</param>
    /// <param name="roll01">区间取值（[0,1)）✓</param>
    public static double WeaponRawDamage(int dmgMin, int dmgMax, int skillDmgPct, double roll01)
    {
        double baseRoll = WeaponRoll(dmgMin, dmgMax, roll01);
        double mult = 1.0 + (skillDmgPct / 100.0);
        return baseRoll * System.Math.Max(0.0, mult); // 负到 0 以下 ⇒ 0（不产生负伤害）✓
    }

    public static int ApplyDamageRounding(double raw, int damageFloor = 1)
    {
        int rounded = (int)System.Math.Round(raw, System.MidpointRounding.AwayFromZero);
        return System.Math.Max(damageFloor, rounded);
    }
}