using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>士气事件对象（data_schema §2.1 EventScope）。JSON 存小写。</summary>
public enum MoraleEventScope
{
    Self,
    Team,
    PerTarget,
}

/// <summary>触发频次（data_schema §3.3 occurrence）。</summary>
public enum MoraleOccurrence
{
    Normal,
    OncePerBattle,
    OncePerTurnMax1,
}

/// <summary>士气增减事件行（data_schema §3.3 morale_events.json；数值唯一来源）。</summary>
public sealed record MoraleEventConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    int Delta,
    MoraleEventScope Scope,
    MoraleOccurrence Occurrence,
    string Source,
    string? Note = null);

/// <summary>morale_events.json 绑定模型 + P9 校验（combat_math §5.2 全 14 行 id 齐备）。</summary>
public sealed record MoraleEventsConfig(
    [property: JsonPropertyName("events")] IReadOnlyList<MoraleEventConfig> Events)
{
    public const string ResPath = "res://data/morale_events.json";

    /// <summary>§5.2 表必选 id（缺行 = 启动报错，P9）。</summary>
    public static readonly IReadOnlyList<string> RequiredIds = Array.AsReadOnly(new[]
    {
        "physical_hit_no_effect", "mental_hit", "mental_crit_hit", "mental_aoe_hit",
        "critical_strike_dealt", "kill_enemy", "ally_enters_weak", "ally_death",
        "support_slot_turn_start", "battle_inspiration", "morale_full_100",
        "retreat_success", "retreat_fail", "weak_hit_any_damage",
    });

    private static readonly IReadOnlyDictionary<string, MoraleEventConfig> EmptyMap =
        new ReadOnlyDictionary<string, MoraleEventConfig>(new Dictionary<string, MoraleEventConfig>());

    private readonly IReadOnlyDictionary<string, MoraleEventConfig> _byId = EmptyMap;

    public IReadOnlyDictionary<string, MoraleEventConfig> ById => _byId;

    /// <summary>按 id 取事件（缺失抛异常——fail-fast，P9 之外还防运行时引用漂移）。</summary>
    public MoraleEventConfig Get(string id)
    {
        if (!_byId.TryGetValue(id, out MoraleEventConfig? e))
        {
            throw new InvalidDataException($"{ResPath}: 引用了不存在的事件 \"{id}\"。");
        }

        return e;
    }

    public static MoraleEventsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        MoraleEventsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<MoraleEventsConfig>(json, JsonOptions)
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
        Converters = { new MoraleEventScopeConverter(), new MoraleOccurrenceConverter(), },
    };

    private static void Validate(MoraleEventsConfig cfg)
    {
        if (cfg.Events is null || cfg.Events.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: events 为空。");
        }

        var ids = new HashSet<string>();
        foreach (MoraleEventConfig e in cfg.Events)
        {
            if (!ids.Add(e.Id))
            {
                throw new InvalidDataException($"{ResPath}: 事件 id \"{e.Id}\" 重复。");
            }

            if (string.IsNullOrWhiteSpace(e.Name) || string.IsNullOrWhiteSpace(e.Source))
            {
                throw new InvalidDataException($"{ResPath}: 事件 \"{e.Id}\" name/source 必填。");
            }
        }

        // P9：§5.2 全 14 行 id 齐备（缺行 = 启动报错）
        foreach (string required in RequiredIds)
        {
            if (!ids.Contains(required))
            {
                throw new InvalidDataException($"{ResPath}: 缺少必选士气事件 \"{required}\"（combat_math §5.2，P9）。");
            }
        }
    }

    private sealed class MoraleEventScopeConverter : JsonConverter<MoraleEventScope>
    {
        public override MoraleEventScope Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        {
            string? s = reader.GetString();
            return s switch
            {
                "self" => MoraleEventScope.Self,
                "team" => MoraleEventScope.Team,
                "per_target" => MoraleEventScope.PerTarget,
                _ => throw new JsonException($"未知 morale scope \"{s}\"。"),
            };
        }

        public override void Write(Utf8JsonWriter writer, MoraleEventScope v, JsonSerializerOptions o)
            => writer.WriteStringValue(v switch
            {
                MoraleEventScope.Self => "self",
                MoraleEventScope.Team => "team",
                _ => "per_target",
            });
    }

    private sealed class MoraleOccurrenceConverter : JsonConverter<MoraleOccurrence>
    {
        public override MoraleOccurrence Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        {
            string? s = reader.GetString();
            return s switch
            {
                "normal" => MoraleOccurrence.Normal,
                "once_per_battle" => MoraleOccurrence.OncePerBattle,
                "once_per_turn_max1" => MoraleOccurrence.OncePerTurnMax1,
                _ => throw new JsonException($"未知 occurrence \"{s}\"。"),
            };
        }

        public override void Write(Utf8JsonWriter writer, MoraleOccurrence v, JsonSerializerOptions o)
            => writer.WriteStringValue(v switch
            {
                MoraleOccurrence.Normal => "normal",
                MoraleOccurrence.OncePerBattle => "once_per_battle",
                _ => "once_per_turn_max1",
            });
    }
}