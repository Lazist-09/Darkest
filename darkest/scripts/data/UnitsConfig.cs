using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>单位原型（data_schema §3.1 units.json）。字段逐键 snake_case 对齐；百分比存整数百分数。</summary>
public sealed record UnitConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("hp")] int Hp,
    [property: JsonPropertyName("attack")] int Attack,
    [property: JsonPropertyName("phys_def")] int PhysDef,
    [property: JsonPropertyName("speed")] int Speed,
    [property: JsonPropertyName("dodge")] int Dodge,
    [property: JsonPropertyName("crit")] int Crit,
    [property: JsonPropertyName("resilience")] int Resilience,
    [property: JsonPropertyName("stun_resist")] int StunResist,
    [property: JsonPropertyName("bleed_resist")] int BleedResist,
    [property: JsonPropertyName("stat_debuff_resist")] int StatDebuffResist,
    [property: JsonPropertyName("displace_resist")] int DisplaceResist,
    [property: JsonPropertyName("deaths_door_resist")] int? DeathsDoorResist,
    [property: JsonPropertyName("skills")] IReadOnlyList<string> Skills)
{
    public bool IsPlayer => Side == "player";
}

/// <summary>units.json 根模型 + fail-fast 校验（data_schema §4.2 P1/P4/P7 同级）。</summary>
public sealed record UnitsConfig(
    [property: JsonPropertyName("units")] IReadOnlyList<UnitConfig> Units)
{
    public const string ResPath = "res://data/units.json";
    private static readonly string[] PlayerIds =
        { "warrior", "tank", "medic", "commissar" };
    private static readonly string[] EnemyIds =
        { "melee_soldier", "ranged_archer", "caster" };

    public static UnitsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        UnitsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<UnitsConfig>(json, JsonOptions)
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

    private static void Validate(UnitsConfig cfg)
    {
        if (cfg.Units is null || cfg.Units.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: units 为空。");
        }

        var ids = new HashSet<string>();
        foreach (UnitConfig u in cfg.Units)
        {
            if (!ids.Add(u.Id))
            {
                throw new InvalidDataException($"{ResPath}: id \"{u.Id}\" 重复（P1）。");
            }

            if (u.Side is not ("player" or "enemy"))
            {
                throw new InvalidDataException($"{ResPath}: \"{u.Id}\" side 非法（player/enemy）。");
            }

            if (u.Hp <= 0 || u.Attack <= 0 || u.Speed <= 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{u.Id}\" hp/attack/speed 必须 > 0。");
            }

            // 百分比字段范围 0~100（data_schema §2.2）
            foreach ((string name, int v) in new[]
                     {
                         ("dodge", u.Dodge), ("crit", u.Crit), ("resilience", u.Resilience),
                         ("stun_resist", u.StunResist), ("bleed_resist", u.BleedResist),
                         ("stat_debuff_resist", u.StatDebuffResist), ("displace_resist", u.DisplaceResist),
                     })
            {
                if (v is < 0 or > 100)
                {
                    throw new InvalidDataException($"{ResPath}: \"{u.Id}\" {name}={v} 越界 [0,100]。");
                }
            }

            if (u.DeathsDoorResist is { } dd && dd is < 0 or > 100)
            {
                throw new InvalidDataException($"{ResPath}: \"{u.Id}\" deaths_door_resist={dd} 越界 [0,100]。");
            }

            // 我方必有死门抗性，敌方必须为 null（P7；enemy §1 敌方无死门）
            if (u.IsPlayer && u.DeathsDoorResist is null)
            {
                throw new InvalidDataException($"{ResPath}: 我方原型 \"{u.Id}\" deaths_door_resist 不得为 null。");
            }

            if (!u.IsPlayer && u.DeathsDoorResist is not null)
            {
                throw new InvalidDataException($"{ResPath}: 敌方原型 \"{u.Id}\" deaths_door_resist 必须为 null。");
            }
        }

        // 原型集合必须包含切片要求的 7 原型（P1 完整性）
        string[] required = PlayerIds.Concat(EnemyIds).ToArray();
        foreach (string id in required)
        {
            if (!ids.Contains(id))
            {
                throw new InvalidDataException($"{ResPath}: 缺少必选原型 \"{id}\"。");
            }
        }
    }

    /// <summary>按 id 取原型（不存在抛异常——fail-fast）。</summary>
    public UnitConfig Get(string id)
    {
        UnitConfig? u = Units.FirstOrDefault(x => x.Id == id);
        if (u is null)
        {
            throw new InvalidDataException($"{ResPath}: 引用了不存在的原型 \"{id}\"。");
        }

        return u;
    }
}