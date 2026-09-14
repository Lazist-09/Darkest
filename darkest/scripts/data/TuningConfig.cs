using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>士气钳制（data_schema §3.7 morale）。</summary>
public sealed record TuningMorale(
    [property: JsonPropertyName("min")] int Min,
    [property: JsonPropertyName("max")] int Max,
    [property: JsonPropertyName("start")] int Start);

/// <summary>美德率 = 10% + 韧性 ÷ 2（#56/#116/#158）。</summary>
public sealed record TuningVirtueRate(
    [property: JsonPropertyName("base_percent")] int BasePercent,
    [property: JsonPropertyName("resilience_divisor")] int ResilienceDivisor);

/// <summary>精神减免 = 韧性/250，上限 40%（#158）。</summary>
public sealed record TuningMentalReduction(
    [property: JsonPropertyName("resilience_divisor")] int ResilienceDivisor,
    [property: JsonPropertyName("cap_percent")] int CapPercent);

/// <summary>
/// 🔴 物理减免除数（数字外置，用户 2026-09-14）：`减免率 = 物防 / (物防 + divisor)`。
/// 原先 30 **硬写在 `BattleMath.PhysicalMitigation` 函数体里** ⇒ 策划改平衡必须改 C# ⚠️
/// ⇒ 现搬到 `tuning.json` 的 `physical_mitigation.divisor`（**值不变 = 零数值改动**）✓
/// </summary>
public sealed record TuningPhysicalMitigation(
    [property: JsonPropertyName("divisor")] int Divisor);

/// <summary>虚弱减益（GDD §3.3 / #37）：伤害 −50%、速度 −30%、HP 锁 1。</summary>
public sealed record TuningWeak(
    [property: JsonPropertyName("damage_mult")] double DamageMult,
    [property: JsonPropertyName("speed_mult")] double SpeedMult,
    [property: JsonPropertyName("hp_lock")] int HpLock);

/// <summary>虚弱士气回升（GDD §3.3 / #59）。M4 消费。</summary>
public sealed record TuningWeakRecovery(
    [property: JsonPropertyName("base")] int Base,
    [property: JsonPropertyName("per_healthy_support_ally")] int PerHealthySupportAlly,
    [property: JsonPropertyName("resilience_divisor")] int ResilienceDivisor,
    [property: JsonPropertyName("cap")] int Cap);

/// <summary>死门（#123）：折磨 −10%。</summary>
public sealed record TuningDeathsDoor(
    [property: JsonPropertyName("affliction_penalty_percent")] int AfflictionPenaltyPercent);

public sealed record TuningRetreat(
    [property: JsonPropertyName("success_morale")] int SuccessMorale,
    [property: JsonPropertyName("with_death_morale")] int WithDeathMorale,
    [property: JsonPropertyName("fail_morale")] int FailMorale,
    [property: JsonPropertyName("scope")] string Scope);

/// <summary>护卫（#159；O-22 只挡物理）。M4 消费。</summary>
public sealed record TuningGuardRedirect(
    [property: JsonPropertyName("max_per_turn")] int MaxPerTurn,
    [property: JsonPropertyName("physical_only")] bool PhysicalOnly);

/// <summary>超时增援（GDD §1.5.2；#194：首波 trigger_round、此后每 wave_interval_rounds 一波、每波补齐全部空位）。</summary>
public sealed record TuningOvertimeReinforcement(
    [property: JsonPropertyName("trigger_round")] int TriggerRound,
    [property: JsonPropertyName("fill_or_buff")] string FillOrBuff,
    [property: JsonPropertyName("buff_attack_delta")] int BuffAttackDelta,
    [property: JsonPropertyName("buff_speed_delta")] int BuffSpeedDelta,
    // 🔴 数字外置：同样**不给默认值**（原先 `= 3` 而 data 里根本没这个键 ⇒ 3 是"藏在代码里的平衡数字"）✓
    [property: JsonPropertyName("wave_interval_rounds")] int WaveIntervalRounds,
    // 🔴 数字外置（用户 2026-09-14）：**不给默认值** —— JSON 若缺 `safety_factor` ⇒ 取 0 ⇒
    //    被下面的 `o.SafetyFactor <= 0` 校验**启动即拦下**（旧写法 `= 0.8` 会让"缺键"静默变成 0.8 ⚠️）
    [property: JsonPropertyName("safety_factor")] double SafetyFactor,
    [property: JsonPropertyName("m_value")] int? MValue = null,
    [property: JsonPropertyName("measured_d")] double MeasuredD = 0,
    [property: JsonPropertyName("enemy_full_hp")] int EnemyFullHp = 0,
    [property: JsonPropertyName("elastic")] TuningElasticSpec? Elastic = null);

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
    [property: JsonPropertyName("effect")] string Effect);

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
    [property: JsonPropertyName("difficulty_tiers")] IReadOnlyList<TuningDifficultyTier>? DifficultyTiers = null,
    [property: JsonPropertyName("pass_morale_delta")] int PassMoraleDelta = 0,
    [property: JsonPropertyName("battle_goal")] int BattleGoal = 3);

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
    [property: JsonPropertyName("respite_base")] int RespiteBase,
    [property: JsonPropertyName("pep_talk_battles")] int PepTalkBattles,
    [property: JsonPropertyName("ambush_chance")] double AmbushChance);

public sealed record TuningFoodTiers(
    [property: JsonPropertyName("starve")] int Starve,
    [property: JsonPropertyName("half")] int Half,
    [property: JsonPropertyName("full")] int Full,
    [property: JsonPropertyName("feast")] int Feast);

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
    [property: JsonPropertyName("virtue_inspired_morale_per_turn")] int VirtueInspiredMoralePerTurn = 3);

/// <summary>
/// tuning.json 绑定模型（data_schema §3.7 唯一权威；每键带出处）。启动一次性解析 → 冻结只读。
/// </summary>
public sealed record TuningConfig(
    [property: JsonPropertyName("morale")] TuningMorale Morale,
    [property: JsonPropertyName("virtue_rate")] TuningVirtueRate VirtueRate,
    [property: JsonPropertyName("mental_reduction")] TuningMentalReduction MentalReduction,
    [property: JsonPropertyName("physical_mitigation")] TuningPhysicalMitigation PhysicalMitigation,
    [property: JsonPropertyName("weak")] TuningWeak Weak,
    [property: JsonPropertyName("weak_recovery")] TuningWeakRecovery WeakRecovery,
    [property: JsonPropertyName("weak_exit_hp_ratio")] double WeakExitHpRatio,
    [property: JsonPropertyName("deaths_door")] TuningDeathsDoor DeathsDoor,
    [property: JsonPropertyName("retreat")] TuningRetreat Retreat,    [property: JsonPropertyName("expedition")] TuningExpedition Expedition,
    [property: JsonPropertyName("light")] TuningLight? Light,
    [property: JsonPropertyName("scouting")] TuningScouting? Scouting,
    [property: JsonPropertyName("inventory")] TuningInventory? Inventory,
    [property: JsonPropertyName("resources")] TuningResources Resources,
    [property: JsonPropertyName("camp")] TuningCamp Camp,
    [property: JsonPropertyName("support_slot_morale_per_turn")] int SupportSlotMoralePerTurn,
    [property: JsonPropertyName("support_points")] TuningSupportPoints SupportPoints,
    [property: JsonPropertyName("affliction_proc_percent")] int AfflictionProcPercent,
    [property: JsonPropertyName("guard_redirect")] TuningGuardRedirect GuardRedirect,
    [property: JsonPropertyName("overtime_reinforcement")] TuningOvertimeReinforcement OvertimeReinforcement,
    [property: JsonPropertyName("enemy_actions_per_round")] int EnemyActionsPerRound,
    [property: JsonPropertyName("bleed")] TuningBleed Bleed,
    [property: JsonPropertyName("stat_debuff_default")] TuningStatDebuffDefault StatDebuffDefault,
    [property: JsonPropertyName("stun")] TuningStun Stun,
    [property: JsonPropertyName("retreat_formula")] TuningRetreatFormula RetreatFormula,
    [property: JsonPropertyName("damage_floor")] int DamageFloor,
    [property: JsonPropertyName("hit_clamp")] TuningHitClamp HitClamp,
    [property: JsonPropertyName("crit_multiplier")] double CritMultiplier,
    [property: JsonPropertyName("damage_float")] TuningDamageFloat DamageFloat,
    [property: JsonPropertyName("speed_float")] TuningSpeedFloat SpeedFloat,
    [property: JsonPropertyName("collapse")] TuningCollapse Collapse)
{
    public const string ResPath = "res://data/tuning.json";

    public static TuningConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        TuningConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<TuningConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg, json);
        return cfg;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
    };

    private static void ValidateLightHasNoHpFields(string rawJson)
    {
        int lightAt = rawJson.IndexOf("\"light\"", StringComparison.Ordinal);
        if (lightAt < 0)
        {
            return;
        }

        int effectAt = rawJson.IndexOf("\"effects\"", lightAt, StringComparison.Ordinal);
        if (effectAt < 0)
        {
            return;
        }

        int endAt = rawJson.IndexOf("\"expedition\"", effectAt, StringComparison.Ordinal);
        string slice = endAt > effectAt ? rawJson[effectAt..endAt] : rawJson[effectAt..];
        foreach (string bad in new[] { "hp", "max_hp", "enemy_hp", "our_hp" })
        {
            if (slice.Contains($"\"{bad}\"", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"{ResPath}: light.effects **不得出现任何 HP 字段**（发现 \"{bad}\"；P21 ③ / #255 已否决该杠杆）。");
            }
        }
    }

    private static void Validate(TuningConfig t, string rawJson)
    {
        if (t.Morale is null || t.MentalReduction is null || t.Weak is null
            || t.DeathsDoor is null || t.Bleed is null || t.StatDebuffDefault is null
            || t.Stun is null || t.HitClamp is null || t.DamageFloat is null
            || t.SpeedFloat is null || t.Collapse is null || t.VirtueRate is null
            || t.WeakRecovery is null || t.GuardRedirect is null || t.OvertimeReinforcement is null
            || t.Retreat is null)
        {
            throw new InvalidDataException($"{ResPath}: 必填段缺失。");
        }

        // 🔴 P20 ⑭（v0.92 / #250）：难度递进三档——覆盖 1..n_battles **无缝隙无重叠**、乘数**单调不减**、
        // **全部走 tuning（禁止硬编码）**；`target` 允许为 null（**施加对象待 O-70 ① 裁定，实现方不得自行选定**）。
        IReadOnlyList<TuningDifficultyTier>? tiers = t.Expedition.DifficultyTiers;
        if (tiers is null || tiers.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: expedition.difficulty_tiers 必填（P20 ⑭）。");
        }

        int expectedFrom = 1;
        double lastMultiplier = 0;
        int lastResistPp = 0;
        foreach (TuningDifficultyTier tier in tiers.OrderBy(x => x.BattleFrom).ToArray())
        {
            if (tier.BattleFrom != expectedFrom)
            {
                throw new InvalidDataException(
                    $"{ResPath}: difficulty_tiers 必须从第 1 场起**无缝隙无重叠**（期望 from={expectedFrom}，实际 {tier.BattleFrom}；P20 ⑭）。");
            }

            if (tier.BattleTo < tier.BattleFrom)
            {
                throw new InvalidDataException($"{ResPath}: difficulty_tiers battle_to < battle_from（P20 ⑭）。");
            }

            if (tier.Multiplier < lastMultiplier)
            {
                throw new InvalidDataException(
                    $"{ResPath}: difficulty_tiers 乘数必须**单调不减**（{lastMultiplier} → {tier.Multiplier}；P20 ⑭）。");
            }

            lastMultiplier = tier.Multiplier;
            if (tier.StunResistPp < 0)
            {
                throw new InvalidDataException($"{ResPath}: difficulty_tiers stun_resist_pp 必须 ≥ 0（P20 ⑭ / #253）。");
            }

            if (tier.StunResistPp < lastResistPp)
            {
                throw new InvalidDataException(
                    $"{ResPath}: difficulty_tiers stun_resist_pp 必须**单调不减**（{lastResistPp} → {tier.StunResistPp}；P20 ⑭ / #253）。");
            }

            lastResistPp = tier.StunResistPp;
            if (tier.Target is not null && tier.Target is not ("enemy_hp" or "enemy_resist"))
            {
                throw new InvalidDataException(
                    $"{ResPath}: difficulty_tiers target 只能是 enemy_hp / enemy_resist（或 null=待裁定；P20 ⑭）。");
            }

            expectedFrom = tier.BattleTo + 1;
        }

        if (expectedFrom != t.Expedition.NBattles + 1)
        {
            throw new InvalidDataException(
                $"{ResPath}: difficulty_tiers 必须**恰好覆盖第 1~{t.Expedition.NBattles} 场**（实际覆盖到 {expectedFrom - 1}；P20 ⑭）。");
        }

        // 🔴 P21（M7.5 地牢层，加载级 fail-fast）：① 五档覆盖 0~100 无缝隙无重叠 + 边界取档；③ 效果表**不得出现 HP 字段**；
        // ④ 掉落概率 ∈ [0,1] 且随变暗**单调不减**；⑤ 侦察 base ≥ 0；⑥ 背包 slot_cap = 12（**不许调到 15 来"修好"取舍**）。
        if (t.Light is null || t.Scouting is null || t.Inventory is null)
        {
            throw new InvalidDataException($"{ResPath}: light / scouting / inventory 三段必填（P21）。");
        }

        if (t.Light.Tiers.Count != 5)
        {
            throw new InvalidDataException($"{ResPath}: light.tiers 必须**五档**（P21 ①）。");
        }

        int expectMax = 100;
        string[] order = { "radiant", "dim", "shadowy", "dark", "black" };
        for (int i = 0; i < order.Length; i++)
        {
            TuningLightTier tier = t.Light.Tiers.FirstOrDefault(x => x.Id == order[i])
                ?? throw new InvalidDataException($"{ResPath}: light.tiers 缺档 \"{order[i]}\"（P21 ①）。");
            if (tier.Max != expectMax)
            {
                throw new InvalidDataException(
                    $"{ResPath}: light.tiers \"{tier.Id}\" max 应为 {expectMax}（实际 {tier.Max}；P21 ① 无缝隙无重叠）。");
            }

            if (tier.Min < 0 || tier.Min > tier.Max + (tier.Id == "black" ? 0 : 1))
            {
                throw new InvalidDataException($"{ResPath}: light.tiers \"{tier.Id}\" 区间非法（P21 ①）。");
            }

            expectMax = tier.Min - 1;
        }

        if (expectMax != -1)
        {
            throw new InvalidDataException($"{ResPath}: light.tiers 必须覆盖到 0（P21 ①）。");
        }

        // 🔴 P21 ④（#276 改）：掉落 = **类型 + 份数**；校验对象 = **柴火份数按档单调不减**（0/0/1/1/2）
        // ＋ 🔴 **下限：`shadowy` 起必须 ≥ 1 柴火**（否则"摸黑换续航"的链又会断：补给再多也换不来扎营）。
        foreach ((string id, TuningLootSpec spec) in t.Light.Loot)
        {
            if (spec.Firewood < 0 || spec.Food < 0)
            {
                throw new InvalidDataException($"{ResPath}: light.loot[\"{id}\"] 份数必须 ≥ 0（P21 ④）。");
            }
        }

        int lastFirewood = -1;
        bool darkZone = false;
        foreach (string id in order)
        {
            if (!t.Light.Loot.TryGetValue(id, out TuningLootSpec? spec) || spec is null)
            {
                throw new InvalidDataException($"{ResPath}: light.loot 缺档 \"{id}\"（P21 ④）。");
            }

            if (spec.Firewood < lastFirewood)
            {
                throw new InvalidDataException(
                    $"{ResPath}: light.loot 的**柴火份数**必须按档单调不减（{id}；P21 ④ / #276）。");
            }

            if (id == "shadowy")
            {
                darkZone = true;
            }

            if (darkZone && spec.Firewood < 1)
            {
                throw new InvalidDataException(
                    $"{ResPath}: light.loot 从 shadowy 起**必须 ≥ 1 柴火**（{id}；P21 ④ / #276：否则摸黑换不来续航）。");
            }

            lastFirewood = spec.Firewood;
            if (!t.Light.Effects.ContainsKey(id))
            {
                throw new InvalidDataException($"{ResPath}: light.effects 缺档 \"{id}\"（P21 ③）。");
            }
        }

        // ③ 效果表不得出现 HP 字段（用原始 JSON 文本兜底检查，防将来加字段）
        ValidateLightHasNoHpFields(rawJson);

        if (t.Scouting.BasePct < 0 || t.Scouting.Reveal != "next_node_type_only")
        {
            throw new InvalidDataException($"{ResPath}: scouting 必须 base_pct ≥ 0 且 reveal=next_node_type_only（P21 ⑤）。");
        }

        if (t.Inventory.SlotCap != 12)
        {
            throw new InvalidDataException(
                $"{ResPath}: inventory.slot_cap 必须 = 12（P21 ⑥：**不许调到 15 来「修好」设计取舍**）。");
        }

        // P21 ⑥/⑬（v1.04）：推荐配置必须 ≤ slot_cap 且**恰满**（作默认）；包满策略必须是"选择丢弃"（禁止静默丢）
        if (t.Inventory.RecommendedLoadout is null)
        {
            throw new InvalidDataException($"{ResPath}: inventory.recommended_loadout 必填（P21 ⑥「整备」默认配置）。");
        }

        int recommended = t.Inventory.RecommendedLoadout.Values.Sum();
        if (recommended > t.Inventory.SlotCap)
        {
            throw new InvalidDataException(
                $"{ResPath}: recommended_loadout 合计 {recommended} > slot_cap {t.Inventory.SlotCap}（P21 ⑥）。");
        }

        // 🔴 P21 ⑥（#275 改）：推荐配置**必须留 ≥1 格余量** —— 否则"摸黑搏到的补给"必须先丢东西，
        // **收益端在入口就被堵住**（推荐配置 = 1/9/1 = 11 格，留 1 格）。
        if (t.Inventory.SlotCap - recommended < 1)
        {
            throw new InvalidDataException(
                $"{ResPath}: recommended_loadout 必须留 ≥1 格余量（{recommended}/{t.Inventory.SlotCap}；P21 ⑥ / #275）。");
        }

        // 🔴 P20 ② / P21 ⑥（#274）：推荐配置的柴火必须**可实现**（≤ 起手柴火）——
        // 起手柴火降到 1 后，`firewood:2` 就是不可实现的配置。
        if (t.Inventory.RecommendedLoadout.TryGetValue("firewood", out int recFirewood)
            && recFirewood > t.Resources.Firewood)
        {
            throw new InvalidDataException(
                $"{ResPath}: recommended_loadout.firewood({recFirewood}) 必须 ≤ 起手 firewood({t.Resources.Firewood})（P20 ② / #274）。");
        }

        if (t.Inventory.FullPolicy != "choose_what_to_discard")
        {
            throw new InvalidDataException(
                $"{ResPath}: inventory.full_policy 必须 = choose_what_to_discard（P21 ⑬：**禁止静默丢弃**，否则光照计收益端闭环会漏）。");
        }

        // 🔴 P21 ⑭（#265 命名裁定）：推荐配置**必须含 `support_crate`（支援箱）** —— 缺它 ⇒ 默认配置下
        // SP 完全不恢复 ⇒ **支援位废**（玩家看不见的陷阱）。`support_crate`（while_carried，每回合 +1 SP，必需）
        // 与 `support_pack`（一次性 +2 SP，可选）是**两个不同 id**，不得混用。
        if (!t.Inventory.RecommendedLoadout.ContainsKey("support_crate"))
        {
            throw new InvalidDataException(
                $"{ResPath}: recommended_loadout **必须包含 support_crate（支援箱）**（P21 ⑭ / #265：缺它 ⇒ 默认配置下 SP 完全不恢复）。");
        }

        // 🔴 P20（M7 远征层）：①②③ —— 键齐备与值域，**加载级 fail-fast**
        if (t.Expedition is null || t.Resources is null || t.Camp is null)
        {
            throw new InvalidDataException($"{ResPath}: expedition / resources / camp 三段必填（P20）。");
        }

        if (t.Expedition.NBattles < 1)
        {
            throw new InvalidDataException($"{ResPath}: expedition.n_battles 必须 ≥ 1（P20 ①）。");
        }

        // 🔴 P20 ①（#273）：完成目标 —— `battle_goal ≥ 1` 且 `≤ n_battles`（"打赢 ≥ N 场"是完成的一部分）
        if (t.Expedition.BattleGoal < 1 || t.Expedition.BattleGoal > t.Expedition.NBattles)
        {
            throw new InvalidDataException(
                $"{ResPath}: expedition.battle_goal 必须 ∈ [1, n_battles={t.Expedition.NBattles}]（P20 ① / #273）。");
        }

        if (t.Expedition.AmbushChance is < 0 or > 1 || t.Camp.AmbushChance is < 0 or > 1)
        {
            throw new InvalidDataException($"{ResPath}: ambush_chance 必须 ∈ [0,1]（P20 ①）。");
        }

        if (t.Expedition.RetreatPenalty.NoDeath < 0 || t.Expedition.RetreatPenalty.WithDeath < t.Expedition.RetreatPenalty.NoDeath)
        {
            throw new InvalidDataException($"{ResPath}: retreat_penalty 必须 ≥0 且 with_death ≥ no_death（P20 ①）。");
        }

        if (t.Resources.Firewood < 0 || t.Resources.Food < 0)
        {
            throw new InvalidDataException($"{ResPath}: resources.firewood / food 必须 ≥ 0（P20 ②）。");
        }

        TuningFoodTiers ft = t.Camp.FoodTiers;
        if (!(ft.Starve < ft.Half && ft.Half < ft.Full && ft.Full < ft.Feast))
        {
            throw new InvalidDataException($"{ResPath}: camp.food_tiers 四档必须单调 0 < half < full < feast（P20 ③）。");
        }

        if (t.Camp.RespiteBase < 1 || t.Camp.PepTalkBattles < 1)
        {
            throw new InvalidDataException($"{ResPath}: camp.respite_base ≥ 1 且 pep_talk_battles ≥ 1（P20 ③）。");
        }

        // ⑥ 撤退对账：morale_events 两档必须与 tuning.retreat 一致、且 scope = survivors
        if (t.Retreat.Scope != "survivors"
            || t.Retreat.SuccessMorale != -t.Expedition.RetreatPenalty.NoDeath
            || t.Retreat.WithDeathMorale != -t.Expedition.RetreatPenalty.WithDeath)
        {
            throw new InvalidDataException(
                $"{ResPath}: retreat 与 retreat_penalty 对账失败（P20 ⑥）：scope=survivors、" +
                $"success_morale=-no_death、with_death_morale=-with_death。");
        }

        if (t.Morale.Min >= t.Morale.Max)
        {
            throw new InvalidDataException($"{ResPath}: morale.min 必须 < morale.max。");
        }

        // 🔴 P16（#196/#198 + v0.68）：`m_value` 必须与公式一致，**写错即启动报错**。
        //    M = ceil(敌方满编总HP ÷ (实测 D × safety_factor))；护栏 M ≥ 3。
        TuningOvertimeReinforcement o = t.OvertimeReinforcement;
        if (o.MValue is not { } mValue)
        {
            throw new InvalidDataException($"{ResPath}: overtime_reinforcement.m_value 必填（不得为 null；P16）。");
        }

        if (o.MeasuredD <= 0 || o.EnemyFullHp <= 0 || o.SafetyFactor <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: overtime_reinforcement 非法（measured_d / enemy_full_hp / safety_factor 必须 > 0）" +
                " —— 🔴 这几项**必须来自数据**，不许靠 C# 默认值兜底（数字外置纪律）。");
        }

        if (o.WaveIntervalRounds <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: overtime_reinforcement.wave_interval_rounds 必须 > 0（数字外置：data 里没有它就算缺键）。");
        }

        int expectedM = Math.Max(3, (int)Math.Ceiling(o.EnemyFullHp / (o.MeasuredD * o.SafetyFactor)));
        if (mValue != expectedM)
        {
            throw new InvalidDataException(
                $"{ResPath}: m_value={mValue} 与公式不符（P16）：ceil({o.EnemyFullHp} ÷ ({o.MeasuredD} × {o.SafetyFactor})) = {expectedM}。" +
                "改数值后必须重算 m_value（或同步 measured_d / enemy_full_hp）。");
        }

        if (t.MentalReduction.CapPercent is <= 0 or > 100 || t.MentalReduction.ResilienceDivisor <= 0)
        {
            throw new InvalidDataException($"{ResPath}: mental_reduction 取值非法。");
        }

        // 🔴 物理减免除数（数字外置，用户 2026-09-14）：**必填且 > 0** ⇒ 不许回落到代码里的默认值 ✓
        if (t.PhysicalMitigation is null || t.PhysicalMitigation.Divisor <= 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: physical_mitigation.divisor 缺失或 ≤ 0 —— 物理减免除数**必须**来自数据（数字外置纪律）。");
        }

        if (t.DamageFloor < 1)
        {
            throw new InvalidDataException($"{ResPath}: damage_floor 必须 ≥ 1（combat_math §2.3）。");
        }

        if (t.HitClamp.Min >= t.HitClamp.Max || t.HitClamp.Min < 0 || t.HitClamp.Max > 100)
        {
            throw new InvalidDataException($"{ResPath}: hit_clamp 取值非法（应 [55,100]）。");
        }

        if (t.CritMultiplier <= 0)
        {
            throw new InvalidDataException($"{ResPath}: crit_multiplier 必须 > 0。");
        }

        if (t.SpeedFloat.Percent is < 0 or > 100)
        {
            throw new InvalidDataException($"{ResPath}: speed_float.percent 越界 [0,100]。");
        }

        if (t.RetreatFormula is null
            || t.RetreatFormula.BasePercent is < 0 or > 100
            || t.RetreatFormula.ClampMin < 0 || t.RetreatFormula.ClampMin > 100
            || t.RetreatFormula.ClampMax < t.RetreatFormula.ClampMin || t.RetreatFormula.ClampMax > 100)
        {
            throw new InvalidDataException($"{ResPath}: retreat_formula 取值非法（O-11/#169）。");
        }
    }
}