using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Darkest.Gameplay.Sim.Morale;

namespace Darkest.Data;

// 枚举（data_schema §3.4 词汇，JSON 小写）
public enum BuffPolarity { Positive, Negative }
public enum BuffDurationType { Rounds, ActionSkip, Charges, UntilMorale50, UntilBattleEndOrMoraleZero, NextAttackWithinRounds, NextBattle, UntilNextRecovery, WhileCarried }
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
    [property: JsonPropertyName("effect")] string Effect);

/// <summary>
/// buff 定义（data_schema §3.4；buff=修改器+钩子+生命周期，加 buff 只加数据）。
///
/// 🔴 **字段四分类**（策划 `#408` / 架构 `data_schema §3.4`：**每个 `data/*.json` 字段必须归入四类之一**）——
///   理由（策划原话）：**「填了却没人消费」会同时骗两种人**（填的人以为在生效；读的人以为它是行为）⚠️
///   · **行为字段** = 引擎读它并**改变结果**（例：`duration` / `stack` / `effects`）✓
///   · **约束字段** = **只被 `Validate` 读**（例：`polarity` ⇒ 参与"正面不可驱散"的防火墙 ✓）
///     🔴 **必须标注"参与校验、不参与行为"**（否则改它的人以为游戏会变 ✓）
///   · **说明字段** = **人类可读的行为说明**（真实现在别处 ⇒ 例：`hooks` / `extra_rules`）✓
///     🔴 两条纪律：**① 标注"仅说明、不参与行为" ② 不得写实现里没有的行为**（如 `virtue_inspired` 写 `VALUE MISSING -> O-27` ✓）
///   · **空字段** = schema 有、数据不填、无人读 ⇒ 🔴 **删**（`timing` 已按 `#408` 删除 ✓）
///   🔴 **`dispellable` 已删**（策划 `#409` 裁 (A)）：「能不能驱散」**就是** `polarity == negative` ⇒ 不再独立成字段 ✓
/// </summary>
public sealed record BuffDefConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("polarity")] BuffPolarity Polarity,
    [property: JsonPropertyName("duration")] BuffDurationSpec Duration,
    [property: JsonPropertyName("stack")] BuffStackSpec Stack,
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
        o.Converters.Add(new LowerEnumJsonConverter<BuffPolarity>(
            ("positive", BuffPolarity.Positive), ("negative", BuffPolarity.Negative)));
        o.Converters.Add(new LowerEnumJsonConverter<BuffDurationType>(
            ("rounds", BuffDurationType.Rounds), ("action_skip", BuffDurationType.ActionSkip),
            ("charges", BuffDurationType.Charges), ("until_morale_50", BuffDurationType.UntilMorale50),
            ("until_battle_end_or_morale_zero", BuffDurationType.UntilBattleEndOrMoraleZero),
            ("next_attack_within_rounds", BuffDurationType.NextAttackWithinRounds),
            ("next_battle", BuffDurationType.NextBattle),               // M7 E3：跨场到下一场战斗结束
            ("until_next_recovery", BuffDurationType.UntilNextRecovery), // M7 E3/E6：到下次恢复（扎营/回城）
            // 🔴 M7.5 D6 #3：**第四类**不可合并 —— "只要在背包里就生效"：
            // 扎营/回城**都不清**（与 until_next_recovery 的关键区别），只有丢弃/掏出才失效
            ("while_carried", BuffDurationType.WhileCarried)));
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

            // 🔴 策划 `#409` 裁 **(A)：删 `dispellable`** ⇒ **这条断言整条删** ——
            //    「能不能驱散」现在**就是** `polarity == negative`（**不再有两个字段要对齐/对账** ✓）

            // 🔴 `#289/#290` 红线 21：**按名分发的机制**（`damage_mod` / `prob_mod`）——
            // 判死活的依据**不是"有没有集中消费点"**，而是【**有没有人真的会读它**】：
            // 它们的消费**按 `effect` 名走专用通道**（如 `refuse_skill` 在 `AfflictionProcs`；
            // `dealt_damage_mult` 在 `DamageStep` 的 `buffDamageMult`）。
            // ⇒ 因此这里校验的是 **`effect` 名必须在【已实现清单】里**：**新增一个没人实现的 effect 名即报错**
            //   （这正是"写了但没接上"的唯一可靠防线）。
            if (b.Modifiers is not null)
            {
                foreach (BuffModifierSpec m in b.Modifiers)
                {
                    if (m.Kind is not (BuffModifierKind.DamageMod or BuffModifierKind.ProbMod))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(m.Effect) || !ConsumedEffectNames.Contains(m.Effect))
                    {
                        throw new InvalidDataException(
                            $"{ResPath}: \"{b.Id}\" 的 {m.Kind} effect \"{m.Effect}\" 不在【已实现清单】里 —— " +
                            "新增 effect 名必须同时接上它的消费通道（红线 21：写了但没接上）。");
                    }
                }
            }
        }
    }

    /// <summary>
    /// **已实现的 `effect` 名清单**（`#290`：🔴 判死声明看"**有没有人真的会读它**"，不看"有没有集中 kind 消费点"）。
    /// 新增 effect 名 ⇒ **必须同时接上消费通道**，并把它登记到这里（否则加载即报错）。
    /// </summary>
    public static readonly IReadOnlySet<string> ConsumedEffectNames = new HashSet<string>(StringComparer.Ordinal)
    {
        // damage_mod —— 消费点：`DamageStep` 的 `raw`（`buffDamageMult` 同层相乘；特质也走这一层）
        "per_round_damage", "dealt_damage_mult", "next_attack_mult", "taken_damage_mult",
        // 🔴 打气（`battles:4`）：**士气伤害减免** —— 消费点：`MoraleLedger.Apply`（`delta < 0` 时乘 1+percent/100）
        "taken_morale_damage_mult",
        // prob_mod —— 消费点：按名分发（`AfflictionProcs` / 各自结算处）
        "refuse_skill", "refuse_heal", "randomize_attack_target",
        "deaths_door_resist_bonus", "crit_bonus", "hit_mod",
    };
}