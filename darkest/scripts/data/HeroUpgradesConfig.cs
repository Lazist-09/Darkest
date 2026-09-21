using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M8（职业）· 英雄升级树的数据结构 + 校验**（**只有解析与校验，不落数据** ✓）。
///
/// 为什么先做解析器（而不是先落数据）：
///   · 卡的验收依赖 **M1/M2**（卡里明写）⇒ **落库时机未到** ✓
///   · 但升级树的**形状已经量清**（一手：`upgrades/heroes/*.upgrades.json` ⇒
///     `reports/dd1_hero_upgrades_source.json`：**15 职业 / 135 树 / 645 等级** ✓）
///   ⇒ 所以先把"**能不能安全吃进这份数据**"变成**可断言**（与 M4/M5/M6 同套路 ✓）
///
/// 🔴 复用：`UpgradeCostConfig` / `UpgradePrerequisiteConfig` 直接沿用 `BuildingsConfig` 的定义 ✓
///   （一手数据里两者的字段完全一致 ⇒ 不重复造 record ✓）
///
/// 🔴 本件**零行为**：**没有** `ResPath` 常量、**没有**任何人调用它 ⇒ 不接生产路径 ✓
///   激活条件：M8 落库时新增 `darkest/data/hero_upgrades.json` 并用 `Parse` + `Validate` ✓
/// </summary>
public sealed record HeroUpgradeLevelConfig(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("currency_cost")] IReadOnlyList<UpgradeCostConfig> CurrencyCost,
    [property: JsonPropertyName("prerequisites")] IReadOnlyList<UpgradePrerequisiteConfig> Prerequisites,
    [property: JsonPropertyName("prerequisite_resolve_level")] int? PrerequisiteResolveLevel = null,
    [property: JsonPropertyName("origin")] string? Origin = null);

/// <summary>一条升级树（如 `abomination.weapon` ✓）。</summary>
public sealed record HeroUpgradeTreeConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("is_instanced")] bool IsInstanced,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("levels")] IReadOnlyList<HeroUpgradeLevelConfig> Levels,
    [property: JsonPropertyName("level_codes")] IReadOnlyList<string>? LevelCodes = null);

/// <summary>一个职业的升级树集合（键 = 职业 id，如 `abomination` ✓）。</summary>
public sealed record HeroUpgradeClassConfig(
    [property: JsonPropertyName("trees")] IReadOnlyList<HeroUpgradeTreeConfig> Trees);

public sealed record HeroUpgradesConfig(
    [property: JsonPropertyName("heroes")] IReadOnlyDictionary<string, HeroUpgradeClassConfig> Heroes)
{
    /// <summary>
    /// 🆕 **M8 落库**（架构授权"按你的建议执行"）：数据文件路径 ✓
    ///   数据来源 = **E 盘一手** `upgrades/heroes/*.upgrades.json`（经 `tools/dsh/extract_dd1_hero_upgrades.py` 量测 ✓）
    ///   ⇒ 每级都带 `origin: "dd1"` ✓（**不编数**：一个数都不是我写的 ✓）
    /// ⚠️ **消费方尚未存在**（职业升级 UI/逻辑属后续）⇒ 本件目前**只做"可加载 + 可校验"** ✓
    /// </summary>
    public const string ResPath = "res://data/hero_upgrades.json";

    /// <summary>全部等级数（= 校验与报表都用得上的读数 ✓）</summary>
    public int LevelCount => Heroes.Values.Sum(c => c.Trees.Sum(t => t.Levels.Count));

    /// <summary>全部树数 ✓</summary>
    public int TreeCount => Heroes.Values.Sum(c => c.Trees.Count);

    /// <summary>解析（**不校验** ⇒ 校验要外部树目录 ⇒ 见 <see cref="Validate"/> 的参数 ✓）</summary>
    public static HeroUpgradesConfig Parse(string json)
    {
        try
        {
            HeroUpgradesConfig? cfg = JsonSerializer.Deserialize<HeroUpgradesConfig>(json, Options);
            return cfg ?? throw new InvalidDataException("hero_upgrades: 反序列化得到 null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"hero_upgrades: JSON 语法错误 —— {ex.Message}");
        }
    }

    /// <summary>
    /// 校验（三条 P 检查 · 与 M6 建筑同风格 ✓）：
    ///   ① **职业与树 id 唯一**（同一职业内树 id 不得重复）
    ///   ② **等级 code 唯一且与 `level_codes` 一致**（一手数据里两者必须对得上 ✓）
    ///   ③ **先决条件可解**：`prerequisites.tree_id` 要么指向**本职业自己的树**，要么指向
    ///      `externalTreeIds`（= 建筑树，如 `blacksmith.weapon` ✓）⇒ **悬空即报错**（不静默 ✓）
    ///   ④ **无自环**（`tree_id` 指向自己且 `requirement_code` 指向本等级 ⇒ 逻辑自杀 ✓）
    /// </summary>
    /// <param name="externalTreeIds">外部树目录（建筑树等）；缺省 ⇒ 只允许"本职业内部"先决 ✓</param>
    public void Validate(IEnumerable<string>? externalTreeIds = null)
    {
        var external = new HashSet<string>(externalTreeIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        var problems = new List<string>();

        if (Heroes.Count == 0)
        {
            problems.Add("heroes 不能为空 ✓");
        }

        foreach ((string heroId, HeroUpgradeClassConfig cls) in Heroes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                problems.Add("职业 id 不能为空 ✓");
                continue;
            }

            var ownTreeIds = new HashSet<string>(cls.Trees.Select(t => t.Id), StringComparer.Ordinal);
            if (ownTreeIds.Count != cls.Trees.Count)
            {
                problems.Add($"{heroId}: 树 id 有重复 ✓");
            }

            foreach (HeroUpgradeTreeConfig tree in cls.Trees)
            {
                var codes = new List<string>();
                foreach (HeroUpgradeLevelConfig lv in tree.Levels)
                {
                    if (codes.Contains(lv.Code, StringComparer.Ordinal))
                    {
                        problems.Add($"{heroId}/{tree.Id}: 等级 code \"{lv.Code}\" 重复 ✓");
                    }

                    codes.Add(lv.Code);

                    foreach (UpgradePrerequisiteConfig p in lv.Prerequisites ?? Array.Empty<UpgradePrerequisiteConfig>())
                    {
                        bool known = ownTreeIds.Contains(p.TreeId) || external.Contains(p.TreeId);
                        if (!known)
                        {
                            problems.Add($"{heroId}/{tree.Id}#{lv.Code}: 先决树 \"{p.TreeId}\" **悬空**（既不是本职业的树，也不在外部目录里）✓");
                        }

                        if (string.Equals(p.TreeId, tree.Id, StringComparison.Ordinal)
                            && string.Equals(p.RequirementCode, lv.Code, StringComparison.Ordinal))
                        {
                            problems.Add($"{heroId}/{tree.Id}#{lv.Code}: **自环**（先决指向自己同一等级）✓");
                        }
                    }
                }

                if (tree.LevelCodes is { Count: > 0 } declared)
                {
                    if (!declared.SequenceEqual(codes, StringComparer.Ordinal))
                    {
                        problems.Add($"{heroId}/{tree.Id}: `level_codes` 与 `levels[].code` **不一致**"
                            + $"（声明 [{string.Join(",", declared)}] vs 实际 [{string.Join(",", codes)}]）✓");
                    }
                }
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException("hero_upgrades 校验失败：" + Environment.NewLine + "  · "
                + string.Join(Environment.NewLine + "  · ", problems));
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
