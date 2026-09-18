/// <summary>#211/O-60 支援点 SP（战斗级资源，全队共享）：起手 3 / 每回合 +1 / 上限 4；
/// 支援位技能 −1、增援 −2；**战斗位技能与被动、撤退零消耗**。纯计数、零随机。</summary>
// 🔴 **从 `TuningConfig.cs` 拆出**（用户 2026-09-18 红线：程序文件 ≤600 行）——
//    本文件 = **战斗机制 + 远征/光照/拾取/侦察/背包/难度/资源/扎营/食物/浮动/崩溃** 的参数记录族
//    ⇒ **纯文件搬家**（这些是**顶层 record**，不是嵌套类型 ⇒ **不需要 `partial`** ✓）· **零行为改动** ✓
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

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
// M7 远征层（#240 / O-66）：expedition / resources / camp（P20 数据一致性）
// ---------------------------------------------------------------------------

/// <summary>远征层参数（E0/E6）：N 场战斗、夜袭概率、撤退两档惩罚、**难度递进三档（#250）**。</summary>
public sealed record TuningExpedition(
    [property: JsonPropertyName("n_battles")] int NBattles,
    [property: JsonPropertyName("ambush_chance")] double AmbushChance,
    [property: JsonPropertyName("retreat_penalty")] TuningRetreatPenalty RetreatPenalty,
    // 🔴 数字外置：**必需参数放在可选参数之前**（C# 规则：可选参数必须都在最后）
    //    并由 `DataPresence.RequireKeys` 断言该键**存在于数据**（缺键即报错）✓
    [property: JsonPropertyName("battle_goal")] int BattleGoal,
    [property: JsonPropertyName("difficulty_tiers")] IReadOnlyList<TuningDifficultyTier>? DifficultyTiers = null,
    [property: JsonPropertyName("pass_morale_delta")] int PassMoraleDelta = 0);

/// <summary>
/// 难度递进档（#250）：按**场序**施加的乘数（**远征层**，不得写进 `units.json` 的单场基准值）。
/// 🔴 `Target` 为 null = **施加对象待 O-70 ① 裁定**（敌 HP / 敌攻击 / 两者）——**实现方不得自行选定**。
/// </summary>
/// <summary>M7.5 地牢层（#258 / D0）：光照计 + 侦察 + 背包。**效果表 7 项，禁止任何 HP 字段**（P21 ③）。</summary>
public sealed record TuningLight(
    [property: JsonPropertyName("enter_value")] int EnterValue,
    [property: JsonPropertyName("node_step")] int NodeStep,
    [property: JsonPropertyName("brighten_gain")] int BrightenGain,
    [property: JsonPropertyName("brighten_firewood_cost")] int BrightenFirewoodCost,
    [property: JsonPropertyName("camp_restore_to")] int CampRestoreTo,
    [property: JsonPropertyName("event_torch_gain")] int EventTorchGain,
    [property: JsonPropertyName("event_dark_cost")] int EventDarkCost,
    [property: JsonPropertyName("tiers")] IReadOnlyList<TuningLightTier> Tiers,
    [property: JsonPropertyName("loot")] Dictionary<string, TuningLootSpec> Loot,
    [property: JsonPropertyName("effects")] Dictionary<string, TuningLightEffect> Effects);

/// <summary>掉落规格（#276）：**类型 + 份数**（柴火优先 —— 口粮换不来扎营，故只给口粮时补给再多也换不来续航）。</summary>
public sealed record TuningLootSpec(
    [property: JsonPropertyName("firewood")] int Firewood = 0,
    [property: JsonPropertyName("food")] int Food = 0);

public sealed record TuningLightTier(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("min")] int Min,
    [property: JsonPropertyName("max")] int Max);

/// <summary>光照效果（7 项；**无 HP 字段** = #255 已否决的杠杆不得回流）。</summary>
public sealed record TuningLightEffect(
    [property: JsonPropertyName("our_morale_damage_pct")] double OurMoraleDamagePct,
    [property: JsonPropertyName("enemy_acc")] double EnemyAcc,
    [property: JsonPropertyName("enemy_dmg_pct")] double EnemyDmgPct,
    [property: JsonPropertyName("enemy_crit_pct")] double EnemyCritPct,
    [property: JsonPropertyName("our_ambush_pct")] double OurAmbushPct,
    [property: JsonPropertyName("our_crit_pct")] double OurCritPct,
    [property: JsonPropertyName("scouting_pct")] double ScoutingPct);

/// <summary>侦察（D1）：基础命中率 + 光照加成；**只揭示下一节点类型**。</summary>
public sealed record TuningScouting(
    [property: JsonPropertyName("base_pct")] double BasePct,
    [property: JsonPropertyName("reveal")] string Reveal);

/// <summary>背包（D2）：12 格；口粮不堆叠 ⇒ 默认推荐配置恰满 12；**包满禁止静默丢弃**。</summary>
public sealed record TuningInventory(
    [property: JsonPropertyName("slot_cap")] int SlotCap,
    [property: JsonPropertyName("food_stackable")] bool FoodStackable,
    [property: JsonPropertyName("recommended_loadout")] Dictionary<string, int>? RecommendedLoadout = null,
    [property: JsonPropertyName("full_policy")] string? FullPolicy = null);

public sealed record TuningDifficultyTier(
    [property: JsonPropertyName("battle_from")] int BattleFrom,
    [property: JsonPropertyName("battle_to")] int BattleTo,
    [property: JsonPropertyName("multiplier")] double Multiplier,
    [property: JsonPropertyName("target")] string? Target = null,
    [property: JsonPropertyName("stun_resist_pp")] int StunResistPp = 0);

/// <summary>撤退士气惩罚两档（只作用于存活者）。</summary>
public sealed record TuningRetreatPenalty(
    [property: JsonPropertyName("no_death")] int NoDeath,
    [property: JsonPropertyName("with_death")] int WithDeath);

/// <summary>一趟远征的起手资源（E2）：柴火 = 扎营许可；口粮 = 吃饭。</summary>
public sealed record TuningResources(
    [property: JsonPropertyName("firewood")] int Firewood,
    [property: JsonPropertyName("food")] int Food);

/// <summary>扎营参数（E3）：四档食物 / Respite 基准 / 打气持续场数 / 夜袭概率。</summary>
public sealed record TuningCamp(
    [property: JsonPropertyName("food_tiers")] TuningFoodTiers FoodTiers,
    // 🔴 数字外置（P29）：四档食物的**效果**（原先硬写在 `ExpeditionCampMath.FoodEffect`）✓
    [property: JsonPropertyName("food_effects")] TuningFoodEffects FoodEffects,
    [property: JsonPropertyName("respite_base")] int RespiteBase,
    [property: JsonPropertyName("pep_talk_battles")] int PepTalkBattles,
    [property: JsonPropertyName("ambush_chance")] double AmbushChance);

public sealed record TuningFoodTiers(
    [property: JsonPropertyName("starve")] int Starve,
    [property: JsonPropertyName("half")] int Half,
    [property: JsonPropertyName("full")] int Full,
    [property: JsonPropertyName("feast")] int Feast);

/// <summary>
/// 🔴 单个食物档位的**效果**（数字外置，P29）：HP 百分比增量（可负）与士气增量。
/// 原先这四档效果**硬写在 `ExpeditionCampMath.FoodEffect` 的 switch 里** ⇒ 策划改不了 ⚠️
/// ⇒ 现搬到 `tuning.camp.food_effects`（**值不变 = 零数值改动**）✓
/// </summary>
public sealed record TuningFoodEffect(
    [property: JsonPropertyName("hp_percent")] double HpPercent,
    [property: JsonPropertyName("morale")] int Morale);

/// <summary>四档食物效果（starve / half / full / feast）—— 与 `food_tiers`（成本）配套 ✓</summary>
public sealed record TuningFoodEffects(
    [property: JsonPropertyName("starve")] TuningFoodEffect Starve,
    [property: JsonPropertyName("half")] TuningFoodEffect Half,
    [property: JsonPropertyName("full")] TuningFoodEffect Full,
    [property: JsonPropertyName("feast")] TuningFoodEffect Feast);

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
