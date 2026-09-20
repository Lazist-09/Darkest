using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>🔴 **M6 · 升级花费的一项**（一手 E 盘 `currency_cost` ✓）：`type` ∈ {gold, crest, bust, portrait, deed} ✓</summary>
public sealed record UpgradeCostConfig(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("origin")] string? Origin = null);

/// <summary>🔴 **M6 · 前置要求**（一手 `prerequisite_requirements` ✓）：指向**同一/别树**的某个 `code` ✓</summary>
public sealed record UpgradePrerequisiteConfig(
    [property: JsonPropertyName("tree_id")] string TreeId,
    [property: JsonPropertyName("requirement_code")] string RequirementCode);

/// <summary>🔴 **M6 · 等级条目**：`code` + 花费 + 前置（策划 `#421` 的 `levels[]` ✓）</summary>
public sealed record BuildingLevelConfig(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("currency_cost")] IReadOnlyList<UpgradeCostConfig> CurrencyCost,
    [property: JsonPropertyName("prerequisites")] IReadOnlyList<UpgradePrerequisiteConfig> Prerequisites,
    [property: JsonPropertyName("origin")] string? Origin = null);

/// <summary>🔴 **M6 · 升级树**（`abbey.meditation` 之类 ✓）</summary>
public sealed record BuildingTreeConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("is_instanced")] bool IsInstanced,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("levels")] IReadOnlyList<BuildingLevelConfig> Levels);

/// <summary>🔴 **M6 · 单个建筑**（`abbey` / `blacksmith` / … 8 个 ✓）</summary>
public sealed record BuildingConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("trees")] IReadOnlyList<BuildingTreeConfig> Trees);

/// <summary>
/// 🔴 **M6 · 建筑结构对齐**（策划 `#421` · 一手 E 盘 `upgrades/building/*.upgrades.json`）。
///
/// 目标形状（`#421`）：`buildings[] { id, trees[] { id, levels[] { code, currency_cost[], prerequisites[] } } }` ✓
/// 契约要求（派发卡）：**`code` / `prerequisites` 各出一条 P 校验** ✓
///
/// 🔴 **三条校验的口径来自一手实测**（不是假设）：
///   实测：`code` 树内重复 **0** ✓ · 悬空前置 **0** ✓ · **前置环 0** ✓ · 资源类型 **5 种** ✓
///   ① **`code` 树内唯一**（跨树可重名 —— `a`/`b`/`c` 是每棵树的档位名）✓
///   ② **前置引用必须存在**（悬空 ⇒ 红）· **且无环**（有环 ⇒ 那些等级永远到不了 ⇒ 红）✓
///   ③ **花费类型必须在实测集合内**（不发明新资源）✓
/// </summary>
public sealed class BuildingsConfig
{
    public const string ResPath = "res://data/buildings.json";

    /// <summary>一手实测用到的 5 种资源（`gold`/`crest`/`bust`/`portrait`/`deed`）—— 不发明第 6 种 ✓</summary>
    public static readonly IReadOnlyList<string> KnownCurrencyTypes = new[] { "gold", "crest", "bust", "portrait", "deed" };

    [JsonPropertyName("buildings")]
    public IReadOnlyList<BuildingConfig> Buildings { get; init; } = Array.Empty<BuildingConfig>();

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static BuildingsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        BuildingsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<BuildingsConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    private static void Validate(BuildingsConfig cfg)
    {
        if (cfg.Buildings.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: buildings 为空。");
        }

        var buildingIds = new HashSet<string>(StringComparer.Ordinal);
        var known = new HashSet<(string, string)>();

        // 先登记全部 (tree_id, code) ⇒ 前置引用才能在**全局**范围内断言 ✓
        foreach (BuildingConfig b in cfg.Buildings)
        {
            if (!buildingIds.Add(b.Id))
            {
                throw new InvalidDataException($"{ResPath}: 建筑 id \"{b.Id}\" 重复。");
            }

            foreach (BuildingTreeConfig t in b.Trees)
            {
                var codes = new HashSet<string>(StringComparer.Ordinal);
                foreach (BuildingLevelConfig lv in t.Levels)
                {
                    // ① code 树内唯一 ✓
                    if (!codes.Add(lv.Code))
                    {
                        throw new InvalidDataException($"{ResPath}: 树 \"{t.Id}\" 内 code \"{lv.Code}\" 重复（P 校验①）✓");
                    }

                    known.Add((t.Id, lv.Code));
                }
            }
        }

        foreach (BuildingConfig b in cfg.Buildings)
        {
            foreach (BuildingTreeConfig t in b.Trees)
            {
                foreach (BuildingLevelConfig lv in t.Levels)
                {
                    foreach (UpgradeCostConfig c in lv.CurrencyCost)
                    {
                        // ③ 花费类型必须在实测集合内 ✓
                        if (!KnownCurrencyTypes.Contains(c.Type))
                        {
                            throw new InvalidDataException(
                                $"{ResPath}: \"{t.Id}.{lv.Code}\" 花费类型 \"{c.Type}\" 不在实测集合（P 校验③）✓");
                        }
                    }

                    foreach (UpgradePrerequisiteConfig p in lv.Prerequisites)
                    {
                        // ② 前置引用必须存在（悬空 ⇒ 红）✓
                        if (!known.Contains((p.TreeId, p.RequirementCode)))
                        {
                            throw new InvalidDataException(
                                $"{ResPath}: \"{t.Id}.{lv.Code}\" 的前置 \"{p.TreeId}.{p.RequirementCode}\" 不存在（P 校验②）✓");
                        }
                    }
                }
            }
        }

        // ② 无环（有环 ⇒ 那些等级永远到不了）✓ —— 迭代 K 次仍能放松 ⇒ 存在环
        var graph = new Dictionary<(string, string), List<(string, string)>>();
        foreach (BuildingConfig b in cfg.Buildings)
        {
            foreach (BuildingTreeConfig t in b.Trees)
            {
                foreach (BuildingLevelConfig lv in t.Levels)
                {
                    graph[(t.Id, lv.Code)] = lv.Prerequisites.Select(p => (p.TreeId, p.RequirementCode)).ToList();
                }
            }
        }

        var reachable = new HashSet<(string, string)>();
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (var kv in graph)
            {
                if (reachable.Contains(kv.Key))
                {
                    continue;
                }

                if (kv.Value.All(reachable.Contains))
                {
                    reachable.Add(kv.Key);
                    grew = true;
                }
            }
        }

        if (reachable.Count != graph.Count)
        {
            var stuck = graph.Keys.Where(k => !reachable.Contains(k)).Take(6)
                .Select(k => $"{k.Item1}.{k.Item2}");
            throw new InvalidDataException(
                $"{ResPath}: 前置成环（这些等级永远到不了）：{string.Join(", ", stuck)}（P 校验②）✓");
        }
    }

    /// <summary>读数：树数 / 等级数（供报表；**不改行为** ✓）。</summary>
    public int TreeCount => Buildings.Sum(b => b.Trees.Count);

    public int LevelCount => Buildings.Sum(b => b.Trees.Sum(t => t.Levels.Count));

    /// <summary>按 id 取建筑（不存在抛异常 ✓）。</summary>
    public BuildingConfig Get(string id)
    {
        BuildingConfig? b = Buildings.FirstOrDefault(x => x.Id == id);
        if (b is null)
        {
            throw new InvalidDataException($"{ResPath}: 引用了不存在的建筑 \"{id}\"。");
        }

        return b;
    }
}
