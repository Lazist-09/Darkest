using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>AI 规则条件（data_schema §3.5 谓词词汇表；空 when = 无条件默认项）。</summary>
public sealed record AiWhenSpec(
    [property: JsonPropertyName("self_slot_in")] IReadOnlyList<int>? SelfSlotIn,
    [property: JsonPropertyName("target_slots_occupied_min")] AiOccupiedMinSpec? TargetSlotsOccupiedMin,
    [property: JsonPropertyName("player_morale_all_at_least")] int? PlayerMoraleAllAtLeast);

public sealed record AiOccupiedMinSpec(
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("slots")] IReadOnlyList<int> Slots,
    [property: JsonPropertyName("min")] int Min);

/// <summary>单条规则：技能 + 条件 + 标记权重预留（#96，切片无标记技能不参与决策）。</summary>
public sealed record AiRuleSpec(
    [property: JsonPropertyName("skill_id")] string SkillId,
    [property: JsonPropertyName("when")] AiWhenSpec When,
    [property: JsonPropertyName("mark_weight")] int? MarkWeight = 0);

/// <summary>原型 AI 配置（data_schema §3.5）：固定优先级规则表 + 15% 随机开关（O-19 default off）。</summary>
public sealed record ArchetypeAiConfig(
    [property: JsonPropertyName("archetype_id")] string ArchetypeId,
    [property: JsonPropertyName("random")] AiRandomSpec Random,
    [property: JsonPropertyName("rules")] IReadOnlyList<AiRuleSpec> Rules);

public sealed record AiRandomSpec(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("fallback_probability")] double FallbackProbability);

/// <summary>enemy_ai.json 根模型 + 校验。</summary>
public sealed record EnemyAiConfig(
    [property: JsonPropertyName("archetypes")] IReadOnlyList<ArchetypeAiConfig> Archetypes)
{
    public const string ResPath = "res://data/enemy_ai.json";

    public ArchetypeAiConfig? For(string archetypeId)
        => Archetypes.FirstOrDefault(a => a.ArchetypeId == archetypeId);

    public static EnemyAiConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        EnemyAiConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<EnemyAiConfig>(json, JsonOptions) ?? throw new InvalidDataException($"{ResPath}: null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    private static JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = false,
        };
        return o;
    }

    private static void Validate(EnemyAiConfig cfg)
    {
        if (cfg.Archetypes is null || cfg.Archetypes.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: archetypes 为空。");
        }

        var seen = new HashSet<string>();
        foreach (ArchetypeAiConfig a in cfg.Archetypes)
        {
            if (!seen.Add(a.ArchetypeId))
            {
                throw new InvalidDataException($"{ResPath}: archetype \"{a.ArchetypeId}\" 重复。");
            }

            if (a.Rules is null || a.Rules.Count == 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{a.ArchetypeId}\" rules 为空（须含默认项）。");
            }

            if (a.Random is null || a.Random.FallbackProbability is < 0 or > 1)
            {
                throw new InvalidDataException($"{ResPath}: \"{a.ArchetypeId}\" random 非法。");
            }
        }
    }
}