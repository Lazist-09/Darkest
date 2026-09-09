using System;
using System.Collections.Generic;
using System.IO;
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
    [property: JsonPropertyName("fail_morale")] int FailMorale);

/// <summary>护卫（#159；O-22 只挡物理）。M4 消费。</summary>
public sealed record TuningGuardRedirect(
    [property: JsonPropertyName("max_per_turn")] int MaxPerTurn,
    [property: JsonPropertyName("physical_only")] bool PhysicalOnly);

/// <summary>超时增援（GDD §1.5.2，第 6 回合；O-20 数值未定）。M5 消费。</summary>
public sealed record TuningOvertimeReinforcement(
    [property: JsonPropertyName("trigger_round")] int TriggerRound,
    [property: JsonPropertyName("fill_or_buff")] string FillOrBuff);

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

/// <summary>崩溃判定池（morale §4~§6；美德池切片默认仅勇猛）。</summary>
public sealed record TuningCollapse(
    [property: JsonPropertyName("affliction_pool")] IReadOnlyList<string> AfflictionPool,
    [property: JsonPropertyName("virtue_pool")] IReadOnlyList<string> VirtuePool,
    [property: JsonPropertyName("proc")] string Proc);

/// <summary>
/// tuning.json 绑定模型（data_schema §3.7 唯一权威；每键带出处）。启动一次性解析 → 冻结只读。
/// </summary>
public sealed record TuningConfig(
    [property: JsonPropertyName("morale")] TuningMorale Morale,
    [property: JsonPropertyName("virtue_rate")] TuningVirtueRate VirtueRate,
    [property: JsonPropertyName("mental_reduction")] TuningMentalReduction MentalReduction,
    [property: JsonPropertyName("weak")] TuningWeak Weak,
    [property: JsonPropertyName("weak_recovery")] TuningWeakRecovery WeakRecovery,
    [property: JsonPropertyName("weak_exit_hp_ratio")] double WeakExitHpRatio,
    [property: JsonPropertyName("deaths_door")] TuningDeathsDoor DeathsDoor,
    [property: JsonPropertyName("retreat")] TuningRetreat Retreat,
    [property: JsonPropertyName("support_slot_morale_per_turn")] int SupportSlotMoralePerTurn,
    [property: JsonPropertyName("affliction_proc_percent")] int AfflictionProcPercent,
    [property: JsonPropertyName("guard_redirect")] TuningGuardRedirect GuardRedirect,
    [property: JsonPropertyName("overtime_reinforcement")] TuningOvertimeReinforcement OvertimeReinforcement,
    [property: JsonPropertyName("bleed")] TuningBleed Bleed,
    [property: JsonPropertyName("stat_debuff_default")] TuningStatDebuffDefault StatDebuffDefault,
    [property: JsonPropertyName("stun")] TuningStun Stun,
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

        Validate(cfg);
        return cfg;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
    };

    private static void Validate(TuningConfig t)
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

        if (t.Morale.Min >= t.Morale.Max)
        {
            throw new InvalidDataException($"{ResPath}: morale.min 必须 < morale.max。");
        }

        if (t.MentalReduction.CapPercent is <= 0 or > 100 || t.MentalReduction.ResilienceDivisor <= 0)
        {
            throw new InvalidDataException($"{ResPath}: mental_reduction 取值非法。");
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
    }
}