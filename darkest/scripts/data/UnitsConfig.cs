using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
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
    // 🔴 **我们自加**的字段（原版无 `prot`；原版减伤走 `prot`）⇒ 待裁：`reports/def_merge_three_plans.md` ✓
    [property: JsonPropertyName("prot")] int Prot,
    [property: JsonPropertyName("speed")] int Speed,

    [property: JsonPropertyName("dodge")] int Dodge,
    [property: JsonPropertyName("crit")] int Crit,
    [property: JsonPropertyName("resilience")] int Resilience,
    [property: JsonPropertyName("stun_resist")] int StunResist,
    [property: JsonPropertyName("bleed_resist")] int BleedResist,
    [property: JsonPropertyName("stat_debuff_resist")] int StatDebuffResist,
    [property: JsonPropertyName("displace_resist")] int DisplaceResist,
    [property: JsonPropertyName("deaths_door_resist")] int? DeathsDoorResist,
    [property: JsonPropertyName("skills")] IReadOnlyList<string> Skills,
    [property: JsonPropertyName("move_distance")] int MoveDistance = 0,
    // 🆕 **M1a 阶段 1（加字段级 · 零行为改动）**：原版 `weapon`/`armour` 各 **5 阶**（0~4）✓
    //   🔴 可选：缺省 = 不参与（老数据不受影响 ⇒ 旧读数必须不变）；本阶段**无消费点** ✓
    // 🆕 **M1a · 抗性 5→8**：可空 = 未配（不假装 0）✓ 数值由策划给（原版口径）✓
    [property: JsonPropertyName("poison_resist")] int? PoisonResist = null,
    [property: JsonPropertyName("disease_resist")] int? DiseaseResist = null,
    [property: JsonPropertyName("trap_resist")] int? TrapResist = null,
    // 🆕 **M1a · 补 `prot`**：百分比整数 0~85（参考项目 `Character.cs` 把比例钳在 0.85）✓ 未配 = null ✓
    [property: JsonPropertyName("weapon")] IReadOnlyList<WeaponTier>? Weapon = null,
    [property: JsonPropertyName("armour")] IReadOnlyList<ArmourTier>? Armour = null)
{
    public bool IsPlayer => Side == "player";
}

/// <summary>units.json 根模型 + fail-fast 校验（data_schema §4.2 P1/P4/P7 同级）。</summary>
public sealed record UnitsConfig(
    [property: JsonPropertyName("units")] IReadOnlyList<UnitConfig> Units)
{
    public const string ResPath = "res://data/units.json";

    /// <summary>F1（#190）：我方原型集合 = 数据派生（side == "player"），不再硬编码白名单——新增角色只改数据。</summary>
    public IReadOnlyCollection<string> PlayerArchetypes
        => Units.Where(u => u.IsPlayer).Select(u => u.Id).ToArray();

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

            // F1/P14（#191）：我方原型必须有移动射程（池外 move 从单位读距离）
            if (u.IsPlayer && u.MoveDistance < 1)
            {
                throw new InvalidDataException($"{ResPath}: 我方原型 \"{u.Id}\" move_distance 必须 ≥ 1（P14）。");
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

            // 🆕 **M1a 阶段 1 校验（只查结构、不查平衡）**：给了就得是**整 5 阶**（原版 0~4）✓
            ValidateTiers(u, "weapon", u.Weapon?.Count, u.Weapon?.Select(x => x.DmgMin).ToArray(), u.Weapon?.Select(x => x.DmgMax).ToArray());
            ValidateTiers(u, "armour", u.Armour?.Count, null, null);

            // 🆕 **M1a · 抗性 5→8 的校验**：给了就必须在 [0,100]（照 deaths_door_resist 同形）✓
            ValidateResist(u, "poison_resist", u.PoisonResist);
            ValidateResist(u, "disease_resist", u.DiseaseResist);
            ValidateResist(u, "trap_resist", u.TrapResist);

            // 🆕 **M1a · `prot` 的校验**：0~85（上限来自参考项目 `Character.cs`：比例钳在 0.85）✓ 未配 = null 合法 ✓
            if (u.Prot is { } prot && (prot < 0 || prot > 85))
            {
                throw new InvalidDataException($"{ResPath}: \"{u.Id}\" prot={prot} 越界 [0,85]（参考项目把护甲减伤钳在 0.85）✓");
            }
        }

        // F1（#190）：不再硬编码"必选 7 原型"——只要求两侧各至少 1 个（新增角色零代码改动）
        if (!cfg.Units.Any(u => u.IsPlayer) || !cfg.Units.Any(u => !u.IsPlayer))
        {
            throw new InvalidDataException($"{ResPath}: 至少各需 1 个我方/敌方原型（P1 完整性，改为数据派生）。");
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

    /// <summary>
    /// 🆕 **M1a 阶段 1**：tier 数组的**结构**校验（整 5 阶 · 区间 0 ≤ min ≤ max）——
    /// 🔴 **只查结构形状**，不查"哪一阶更强"（那属平衡 ⇒ 解冻清单）✓ 规则来源 `dd1_baseline §27` ✓
    /// </summary>
    /// <summary>🆕 **M1a · 抗性 5→8**：给了就查 [0,100]（**未配 = null 合法**，不假装 0）✓</summary>
    private static void ValidateResist(UnitConfig u, string name, int? value)
    {
        if (value is { } v && v is < 0 or > 100)
        {
            throw new InvalidDataException($"{ResPath}: \"{u.Id}\" {name}={v} 越界 [0,100]。");
        }
    }

    private static void ValidateTiers(UnitConfig u, string name, int? count, int[]? mins, int[]? maxs)
    {
        if (count is null)
        {
            return; // 缺省 = 不参与（阶段 1 允许）✓
        }

        if (count != 5)
        {
            throw new InvalidDataException($"{ResPath}: \"{u.Id}\" {name} 必须是 5 阶（原版 0~4）—— 实际 {count}。");
        }

        if (mins is not null && maxs is not null)
        {
            for (int i = 0; i < mins.Length; i++)
            {
                if (mins[i] < 0 || maxs[i] < 0 || mins[i] > maxs[i])
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{u.Id}\" {name}[{i}] 区间非法（min={mins[i]} max={maxs[i]}；要求 0 ≤ min ≤ max）✓");
                }
            }
        }
    }
}