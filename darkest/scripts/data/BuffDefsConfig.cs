using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

// 枚举（data_schema §3.4 词汇，JSON 小写）
public enum BuffClass { Buff, UnitState }
public enum BuffPolarity { Positive, Negative }
public enum BuffDurationType { Rounds, ActionSkip, Charges, UntilMorale50, UntilBattleEndOrMoraleZero, NextAttackWithinRounds }
public enum BuffStackRule { Refresh, Stack, None }
public enum BuffModifierKind { StatMod, StateFlag, DamageMod, ProbMod }
public enum BuffHookTiming { BeforeTakingDamage, AfterTakingDamage, BeforeAction, AfterAction, RoundStart, RoundEnd, BeforeUseSkill, BeforeHeal, BeforeAttack, BeforeEnemyAi }

public sealed record BuffDurationSpec(
    [property: JsonPropertyName("type")] BuffDurationType Type,
    [property: JsonPropertyName("value")] int? Value);

public sealed record BuffStackSpec(
    [property: JsonPropertyName("rule")] BuffStackRule Rule,
    [property: JsonPropertyName("max")] int? Max);

public sealed record BuffModifierSpec(
    [property: JsonPropertyName("kind")] BuffModifierKind Kind,
    [property: JsonPropertyName("effect")] string? Effect,
    [property: JsonPropertyName("value")] int? Value,
    [property: JsonPropertyName("percent")] int? Percent,
    [property: JsonPropertyName("stat")] string? Stat,
    [property: JsonPropertyName("delta")] int? Delta);

public sealed record BuffHookSpec(
    [property: JsonPropertyName("timing")] BuffHookTiming Timing,
    [property: JsonPropertyName("effect")] string Effect);

/// <summary>buff 定义（data_schema §3.4；buff=修改器+钩子+生命周期，加 buff 只加数据）。</summary>
public sealed record BuffDefConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("class")] BuffClass Class,
    [property: JsonPropertyName("polarity")] BuffPolarity Polarity,
    [property: JsonPropertyName("duration")] BuffDurationSpec Duration,
    [property: JsonPropertyName("stack")] BuffStackSpec Stack,
    [property: JsonPropertyName("dispellable")] bool Dispellable,
    [property: JsonPropertyName("modifiers")] IReadOnlyList<BuffModifierSpec>? Modifiers = null,
    [property: JsonPropertyName("hooks")] IReadOnlyList<BuffHookSpec>? Hooks = null,
    [property: JsonPropertyName("extra_rules")] JsonElement? ExtraRules = null,
    [property: JsonPropertyName("source")] string? Source = null);

/// <summary>buff_defs.json 根模型 + fail-fast 校验。</summary>
public sealed record BuffDefsConfig(
    [property: JsonPropertyName("buffs")] IReadOnlyList<BuffDefConfig> Buffs)
{
    public const string ResPath = "res://data/buff_defs.json";

    public BuffDefConfig Get(string id)
    {
        foreach (BuffDefConfig b in Buffs)
        {
            if (b.Id == id)
            {
                return b;
            }
        }

        throw new InvalidDataException($"{ResPath}: 引用了不存在的 buff \"{id}\"。");
    }

    public static BuffDefsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        BuffDefsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<BuffDefsConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法/枚举错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    public static string Serialize(BuffDefsConfig cfg) => JsonSerializer.Serialize(cfg, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = false,
        };
        o.Converters.Add(new LowerEnumJsonConverter<BuffClass>(
            ("buff", BuffClass.Buff), ("unit_state", BuffClass.UnitState)));
        o.Converters.Add(new LowerEnumJsonConverter<BuffPolarity>(
            ("positive", BuffPolarity.Positive), ("negative", BuffPolarity.Negative)));
        o.Converters.Add(new LowerEnumJsonConverter<BuffDurationType>(
            ("rounds", BuffDurationType.Rounds), ("action_skip", BuffDurationType.ActionSkip),
            ("charges", BuffDurationType.Charges), ("until_morale_50", BuffDurationType.UntilMorale50),
            ("until_battle_end_or_morale_zero", BuffDurationType.UntilBattleEndOrMoraleZero),
            ("next_attack_within_rounds", BuffDurationType.NextAttackWithinRounds)));
        o.Converters.Add(new LowerEnumJsonConverter<BuffStackRule>(
            ("refresh", BuffStackRule.Refresh), ("stack", BuffStackRule.Stack), ("none", BuffStackRule.None)));
        o.Converters.Add(new LowerEnumJsonConverter<BuffModifierKind>(
            ("stat_mod", BuffModifierKind.StatMod), ("state_flag", BuffModifierKind.StateFlag),
            ("damage_mod", BuffModifierKind.DamageMod), ("prob_mod", BuffModifierKind.ProbMod)));
        o.Converters.Add(new LowerEnumJsonConverter<BuffHookTiming>(
            ("before_taking_damage", BuffHookTiming.BeforeTakingDamage),
            ("after_taking_damage", BuffHookTiming.AfterTakingDamage),
            ("before_action", BuffHookTiming.BeforeAction), ("after_action", BuffHookTiming.AfterAction),
            ("round_start", BuffHookTiming.RoundStart), ("round_end", BuffHookTiming.RoundEnd),
            ("before_use_skill", BuffHookTiming.BeforeUseSkill), ("before_heal", BuffHookTiming.BeforeHeal),
            ("before_attack", BuffHookTiming.BeforeAttack), ("before_enemy_ai", BuffHookTiming.BeforeEnemyAi)));
        return o;
    }

    private sealed class LowerEnumJsonConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        private readonly Dictionary<string, T> _read = new();
        private readonly Dictionary<T, string> _write = new();

        public LowerEnumJsonConverter(params (string Word, T Value)[] vocab)
        {
            foreach ((string word, T value) in vocab)
            {
                _read[word] = value;
                _write[value] = word;
            }
        }

        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            string? raw = reader.GetString();
            if (raw is not null && _read.TryGetValue(raw, out T value))
            {
                return value;
            }

            throw new JsonException($"未知枚举值 \"{raw}\"（{typeof(T).Name}）。");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(_write[value]);
    }

    private static void Validate(BuffDefsConfig cfg)
    {
        if (cfg.Buffs is null || cfg.Buffs.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: buffs 为空。");
        }

        var ids = new HashSet<string>();
        foreach (BuffDefConfig b in cfg.Buffs)
        {
            if (string.IsNullOrWhiteSpace(b.Id) || !ids.Add(b.Id))
            {
                throw new InvalidDataException($"{ResPath}: buff id \"{b.Id}\" 缺失或重复。");
            }

            if (b.Duration is null || b.Stack is null)
            {
                throw new InvalidDataException($"{ResPath}: \"{b.Id}\" duration/stack 必填。");
            }

            if (b.Duration.Type is BuffDurationType.Rounds or BuffDurationType.NextAttackWithinRounds
                && (b.Duration.Value is null or <= 0))
            {
                throw new InvalidDataException($"{ResPath}: \"{b.Id}\" duration.value 必填且 > 0。");
            }

            bool polarityOk = b.Polarity == BuffPolarity.Negative ? b.Dispellable && b.Class == BuffClass.Buff
                : !b.Dispellable && b.Class == BuffClass.Buff;
            if (b.Class == BuffClass.Buff && !polarityOk)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{b.Id}\" polarity↔dispellable 不一致（负面可驱散/正面不可驱散，buff.md §5.1）。");
            }
        }
    }
}