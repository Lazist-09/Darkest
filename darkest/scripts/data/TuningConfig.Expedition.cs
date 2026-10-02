// ① 来源：从 `TuningConfig.ExpeditionAndCombat.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **M7 远征层族**（`TuningExpedition` 原 :68-77 ／ `TuningDifficultyTier` 原 :79-82＋:128-133 ／ `TuningRetreatPenalty` 原 :135-138 ／
//    `TuningResources`／`TuningCamp`／`TuningFoodTiers`／`TuningFoodEffect`／`TuningFoodEffects` 原 :345-379；M11 ① 预警面第二十三件·第一片）✓
// ② 职责：一趟远征的**外层参数**（场数／夜袭／撤退惩罚／难度递进三档）＋ **起手资源与扎营**（柴火·口粮／四档食物／Respite／打气）——
//    战斗内机制族留在主片 `TuningConfig.ExpeditionAndCombat.cs`；地牢层族 ⇒ `TuningConfig.Expedition.DungeonLayer.cs` ✓
// ③ 🔴 依赖（实测扫描本片）：`TuningExpedition` → `TuningRetreatPenalty` x1 ／ `TuningDifficultyTier` x1；`TuningCamp` → `TuningFoodTiers` x1 ／
//    `TuningFoodEffects` x1；`TuningFoodEffects` → `TuningFoodEffect` x1 ⇒ **全部在本片内闭合**（零跨片依赖）✓
// ④ 只搬家、零行为改动（逐字节原样；**唯一例外** = `TuningDifficultyTier` 的 `<summary>` 原被截留在 `TuningLight` 上方，本件归位到其声明前（纯注释）；
//    using 按需裁剪 ⇒ 2 条；构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Darkest.Data;

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

// ---------------------------------------------------------------------------
// E2/E3 起手资源与扎营（resources / camp / food · P20 数据一致性）
// ---------------------------------------------------------------------------
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
