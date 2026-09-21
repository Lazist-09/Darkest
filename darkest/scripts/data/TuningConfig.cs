/// <summary>
/// tuning.json 绑定模型（data_schema §3.7 唯一权威；每键带出处）。启动一次性解析 → 冻结只读。
/// </summary>
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
    [property: JsonPropertyName("start")] int Start,
    // 🔴 数字外置（P29）：**目睹同伴被暴击 ⇒ 连带士气伤害的概率**（原先硬写在 `DamagePipeline` 里是 `50.0`）✓
    [property: JsonPropertyName("witness_crit_shock_chance_percent")] int WitnessCritShockChancePercent);

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
    [property: JsonPropertyName("physical_only")] bool PhysicalOnly,
    // 🔴 被守护者的**代价**（策划 #331①）："看着别人替你挨打" ⇒ 士气 −N（+留痕）—— 没有它 ⇒ **守护纯赚**（红线 24 第一形态）⚠️
    [property: JsonPropertyName("guarded_ally_morale")] int GuardedAllyMorale);

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



public sealed partial record TuningConfig(
    [property: JsonPropertyName("morale")] TuningMorale Morale,
    [property: JsonPropertyName("virtue_rate")] TuningVirtueRate VirtueRate,
    [property: JsonPropertyName("mental_reduction")] TuningMentalReduction MentalReduction,
    [property: JsonPropertyName("physical_mitigation")] TuningPhysicalMitigation PhysicalMitigation,
    // 🔴 数字外置（P29）：连续未命中补偿（原先硬写在 `HitStep` 里是 `* 4`）✓
    [property: JsonPropertyName("consecutive_miss")] TuningConsecutiveMiss ConsecutiveMiss,
    // 🔴 数字外置（P29）：暴击治疗概率（原先硬写在 `SkillExecutor` 里是 12% / 5%）✓
    [property: JsonPropertyName("heal_crit")] TuningHealCrit HealCrit,
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
    [property: JsonPropertyName("collapse")] TuningCollapse Collapse,
    // 🔴 D-2（M8 地牢层）：回头威胁**可选**（记录参数表的**末位**，可选参数只能收尾）——
    //    不给数据加新必需负担；未配置 ⇒ `RevisitSpawner.Roll` 直接返回 None 且**不掷骰不写日志** ✓
    [property: JsonPropertyName("dungeon_layer")] TuningDungeonLayer? DungeonLayer = null)
{
    public const string ResPath = "res://data/tuning.json";

    public static TuningConfig Parse(string json)
    {
        // 🔴 数字外置纪律（P29）第一道：**必需键必须存在于数据**（否则 C# 默认值会静默生效）——
        //    这里列的是"曾经靠记录默认值兜底"的可调数字（缺口由 `tools/check_data_discipline.py` 报出）✓
        DataPresence.RequireKeys(ResPath, json,
            "morale", "hit_clamp", "damage_floor", "crit_multiplier", "damage_float", "speed_float",
            "mental_reduction", "physical_mitigation", "deaths_door", "retreat_formula",
            "battle_goal", "virtue_inspired_morale_per_turn",
            "safety_factor", "wave_interval_rounds", "measured_d", "enemy_full_hp",
            "stun", "buildup_on_apply", "witness_crit_shock_chance_percent", "food_effects",
            "consecutive_miss", "hit_bonus_per_miss", "heal_crit", "single_target_percent", "multi_target_percent");

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
        // 🔴 **M1a/M11 · 域段拆分（架构 §1.2）**：校验**顺序不变** ⇒ 零行为 ✓
        ValidateExpedition(t, rawJson);   // 远征域（207-495）✓
        ValidateCombat(t, rawJson);       // 战斗域（496-810）✓

    }
}
