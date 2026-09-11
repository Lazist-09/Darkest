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

/// <summary>原型 AI 配置（data_schema §3.5）：固定优先级规则表 + 目标偏好 + 15% 随机开关（O-19 default off）。</summary>
public sealed record ArchetypeAiConfig(
    [property: JsonPropertyName("archetype_id")] string ArchetypeId,
    [property: JsonPropertyName("random")] AiRandomSpec Random,
    [property: JsonPropertyName("rules")] IReadOnlyList<AiRuleSpec> Rules,
    [property: JsonPropertyName("target_preference")] string? TargetPreference = null);

public sealed record AiRandomSpec(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("fallback_probability")] double FallbackProbability);

/// <summary>enemy_ai.json 根模型 + 校验（含 P13：target_preference 白名单与原型映射、taunt_weight ≥ 1）。</summary>
public sealed record EnemyAiConfig(
    [property: JsonPropertyName("archetypes")] IReadOnlyList<ArchetypeAiConfig> Archetypes,
    [property: JsonPropertyName("taunt_weight")] int TauntWeight = 3)
{
    public const string ResPath = "res://data/enemy_ai.json";

    /// <summary>P13（v0.68）：目标偏好白名单——三原型统一为 **池内随机**（taunt/mark 仍为显式优先级规则）。</summary>
    public static readonly IReadOnlyDictionary<string, string> PreferenceByArchetype = new Dictionary<string, string>
    {
        ["melee_soldier"] = "random",
        ["ranged_archer"] = "random",
        ["caster"] = "random",
    };

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

            // P13：target_preference 白名单 + 原型映射固定（#185/#187）
            if (PreferenceByArchetype.TryGetValue(a.ArchetypeId, out string? expected))
            {
                if (a.TargetPreference != expected)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{a.ArchetypeId}\" target_preference 必须为 \"{expected}\"（实际 \"{a.TargetPreference}\"，P13）。");
                }
            }
            else if (a.TargetPreference is not null)
            {
                throw new InvalidDataException($"{ResPath}: \"{a.ArchetypeId}\" 非白名单元型不得声明 target_preference（P13）。");
            }
        }

        if (cfg.TauntWeight < 1)
        {
            throw new InvalidDataException($"{ResPath}: taunt_weight 必须 ≥ 1（实际 {cfg.TauntWeight}，P13）。");
        }
    }
}