// 🔴 **从 `TuningConfig.cs` 拆出**（用户 2026-09-18 红线：程序文件 ≤600 行）——
//    本片 = **战斗机制族**（SP／弹性增援／流血／减益／眩晕／撤退公式 原 :15-62 ＋ 数字外置 原 :381-419）⇒
//    远征层族 ⇒ `TuningConfig.Expedition.cs`；地牢层族 ⇒ `TuningConfig.Expedition.DungeonLayer.cs`（M11 ① 预警面第二十三件 · 2026-10-02 · 只搬家 · 零行为改动）✓

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>#211/O-60 支援点 SP（战斗级资源，全队共享）：起手 3 / 每回合 +1 / 上限 4；
/// 支援位技能 −1、增援 −2；**战斗位技能与被动、撤退零消耗**。纯计数、零随机。</summary>
public sealed record TuningSupportPoints(
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("regen_per_round")] int RegenPerRound,
    [property: JsonPropertyName("cap")] int Cap,
    [property: JsonPropertyName("cost_skill")] int CostSkill,
    [property: JsonPropertyName("cost_reinforce")] int CostReinforce);

/// <summary>#198 弹性增援（橡胶筋）：窗口 K 回合内「未使用 output 技能的存活战斗位 ≥ idle_output_slots」→ M+1；
/// 达标 → M 回落 M_base；浮区 M ∈ [M_base, M_base+max_bonus]（首波固定 trigger_round 不受弹性影响）。</summary>
public sealed record TuningElasticSpec(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("k_rounds")] int KRounds,
    [property: JsonPropertyName("idle_output_slots")] int IdleOutputSlots,
    [property: JsonPropertyName("max_bonus")] int MaxBonus);

/// <summary>敌方每回合行动次数（默认 1；M6 探针验证行动不对称调节，数据可表达，不做代码拍死）。</summary>
public sealed record EnemyActionsPerTurn(int Value);

/// <summary>流血（combat_math §4）：每回合 3 / 2 回合；须与 buff_defs 一致（P6）。</summary>
public sealed record TuningBleed(
    [property: JsonPropertyName("per_round_damage")] int PerRoundDamage,
    [property: JsonPropertyName("rounds")] int Rounds);

/// <summary>属性减益通用默认 −3 / 2 回合（combat_math §4/§10；O-23 与技能韧性减益并存）。</summary>
public sealed record TuningStatDebuffDefault(
    [property: JsonPropertyName("delta")] int Delta,
    [property: JsonPropertyName("rounds")] int Rounds);

/// <summary>眩晕（GDD §2.5）：跳过 1 次行动即结束。</summary>
public sealed record TuningStun(
    [property: JsonPropertyName("effect")] string Effect,
    // 🔴 数字外置（P29）：成功施加眩晕后的**抗性累积 +N**（原先硬写在 `EffectsStep` 里是 `+ 50`）✓
    [property: JsonPropertyName("buildup_on_apply")] int BuildupOnApply);

/// <summary>
/// 撤退公式（O-11/#169 已拍板；M5 撤退按钮用）：
/// 基础 = 50% + (我方存活平均实际速度 − 敌方存活平均实际速度) × 4%，钳制 [15,85]；
/// 实际 = 基础 + uniform(−random_range, +random_range)，钳制 [rand_clamp_min, rand_clamp_max]。
/// </summary>
public sealed record TuningRetreatFormula(
    [property: JsonPropertyName("base_percent")] int BasePercent,
    [property: JsonPropertyName("per_speed_diff_percent")] int PerSpeedDiffPercent,
    [property: JsonPropertyName("clamp_min")] int ClampMin,
    [property: JsonPropertyName("clamp_max")] int ClampMax,
    [property: JsonPropertyName("random_range")] int RandomRange,
    [property: JsonPropertyName("rand_clamp_min")] int RandClampMin,
    [property: JsonPropertyName("rand_clamp_max")] int RandClampMax,
    [property: JsonPropertyName("source")] string Source);

// ---------------------------------------------------------------------------
// P29 数字外置（战斗机制）：连击补偿 / 暴击治疗 / 命中钳制 / 伤害与速度浮动 / 崩溃判定池
// ---------------------------------------------------------------------------
/// <summary>
/// 🔴 连续未命中补偿（数字外置，P29）：每多未命中一次，**隐藏**命中加成 +N。
/// 原先这个 4 **硬写在 `HitStep` 里**（`Math.Max(0, misses - 1) * 4`）⇒ 策划改不了 ⚠️
/// </summary>
public sealed record TuningConsecutiveMiss(
    [property: JsonPropertyName("hit_bonus_per_miss")] int HitBonusPerMiss);

/// <summary>
/// 🔴 暴击治疗概率（数字外置，P29；D7 / `#209`）：单体 12% ／ 多目标 5%（不受任何修正影响）。
/// 原先这两个数**硬写在 `SkillExecutor` 里**（`targets.Length > 1 ? 5.0 : 12.0`）⇒ 策划改不了 ⚠️
/// （暴击治疗后的士气加成**本来就在** `morale_events["critical_heal"].Delta` ✓，无需重复外置）
/// </summary>
public sealed record TuningHealCrit(
    [property: JsonPropertyName("single_target_percent")] int SingleTargetPercent,
    [property: JsonPropertyName("multi_target_percent")] int MultiTargetPercent);

/// <summary>命中率钳制 [55,100]（combat_math §1）。</summary>
public sealed record TuningHitClamp(
    [property: JsonPropertyName("min")] int Min,
    [property: JsonPropertyName("max")] int Max);

/// <summary>伤害浮动（combat_math §2.1；O-01 默认关闭 = 1.0）。</summary>
public sealed record TuningDamageFloat(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("min")] double Min,
    [property: JsonPropertyName("max")] double Max);

/// <summary>速度浮动 0~10%（#163，每回合重掷）。</summary>
public sealed record TuningSpeedFloat(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("percent")] int Percent);

/// <summary>崩溃判定池（morale §4~§6；F2/#193：美德池 4 个全量，振奋 +3/回合为 O-27 本包定值）。</summary>
public sealed record TuningCollapse(
    [property: JsonPropertyName("affliction_pool")] IReadOnlyList<string> AfflictionPool,
    [property: JsonPropertyName("virtue_pool")] IReadOnlyList<string> VirtuePool,
    [property: JsonPropertyName("proc")] string Proc,
    // 🔴 数字外置：同上（去默认 + 存在性断言）✓
    [property: JsonPropertyName("virtue_inspired_morale_per_turn")] int VirtueInspiredMoralePerTurn);
