using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M4 · rarity 表**（E 盘一手 `base.rarities.trinkets.json` 的 14 条 ⇒ 排除 `kickstarter` 后 13 条）✓
/// **`award_category` 就在这里** —— 而它是**"能否购买"的唯一判据来源** ✓（策划 `#452` 的更正）
/// </summary>
public sealed record TrinketRarityConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("award_category")] string AwardCategory);

/// <summary>
/// 🔴 **M4 · Trinket（饰品）条目** —— 契约 `doc/modules/trinkets.md`（策划 `#437` / `#452`）。
///
/// 7 个字段（一手 E 盘 `base.entries.trinkets.json` 逐字）：
///   `id` / `buffs` / `hero_class_requirements` / `rarity` / `price` / `limit` / `origin_dungeon` ✓
/// ＋ 我们补的 `award_category`（**由 rarity 派生**，不重复存真值 ⇒ 单一来源在 rarity 表 ✓）
/// ＋ `origin`（溯源：阶段 A 对齐数据一律标来源 ✓）
/// </summary>
public sealed record TrinketConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("buffs")] IReadOnlyList<string> Buffs,
    [property: JsonPropertyName("hero_class_requirements")] IReadOnlyList<string> HeroClassRequirements,
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("price")] int Price,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("origin_dungeon")] string OriginDungeon,
    [property: JsonPropertyName("origin")] string? Origin = null);

/// <summary>
/// 🔴 **M4 · Trinket 表根**：只做"读进来 + 校验"，**不改任何数值** ✓
///
/// 四件套（纪律 AF）里的位置：**数据** = `data/trinkets.json` ✓　**解析+校验** = 本文件 ✓
/// **运行时状态** = 🔴 **不要** `TrinketLedger`（契约 §3）：装备关系归 `Roster`（每英雄 **2 槽**）✓
///
/// 验收（契约，含 `#452` 更正）：
///   **T1** 字段齐全（196 条 · 一手 E 盘 + 排除 `kickstarter`）✓
///   **T2** `id` 唯一 · `buffs` ∈ 原语层 · `rarity` ∈ 声明的 rarity 表 · `price ≥ 0` ✓
///   **T5** 🔴 **不可购买的判据 = `award_category != "universal"`**（**26 条**）——
///        ⚠️ **不是** `price ≤ 1`（那是 **15 条** ⇒ 策划 `#452` 明确更正：用 price 判会漏）✓
/// </summary>
public sealed class TrinketsConfig
{
    public const string ResPath = "res://data/trinkets.json";

    [JsonPropertyName("rarities")]
    public IReadOnlyList<TrinketRarityConfig> Rarities { get; init; } = Array.Empty<TrinketRarityConfig>();

    [JsonPropertyName("trinkets")]
    public IReadOnlyList<TrinketConfig> Trinkets { get; init; } = Array.Empty<TrinketConfig>();

    /// <summary>
    /// 🔴 **原语层交叉校验是否真的跑了**（**自证**，同 `TrapResistSourceDeclared` 思路）：
    /// 原语层（M2 的 `dd1_buffs.json`）未就位时这一步**跳过** ⇒ **必须说出来**，不许静默当通过 ✓
    /// </summary>
    [JsonIgnore]
    public bool BuffsCrossChecked { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static TrinketsConfig Parse(string json, ISet<string>? knownBuffIds = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        TrinketsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<TrinketsConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        cfg.BuffsCrossChecked = knownBuffIds is not null;
        Validate(cfg, knownBuffIds);
        return cfg;
    }

    private static void Validate(TrinketsConfig cfg, ISet<string>? knownBuffIds)
    {
        if (cfg.Trinkets.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: trinkets 为空。");
        }

        var declared = new HashSet<string>(cfg.Rarities.Select(r => r.Id), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (TrinketConfig t in cfg.Trinkets)
        {
            if (string.IsNullOrWhiteSpace(t.Id))
            {
                throw new InvalidDataException($"{ResPath}: 存在缺失 id 的饰品。");
            }

            if (!seen.Add(t.Id))
            {
                throw new InvalidDataException($"{ResPath}: 饰品 id \"{t.Id}\" 重复（T2）✓");
            }

            if (!declared.Contains(t.Rarity))
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{t.Id}\" rarity=\"{t.Rarity}\" 不在 rarity 表里（T2）✓");
            }

            if (t.Price < 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{t.Id}\" price={t.Price} 为负（T2）✓");
            }

            if (knownBuffIds is not null)
            {
                foreach (string b in t.Buffs)
                {
                    if (!knownBuffIds.Contains(b))
                    {
                        throw new InvalidDataException($"{ResPath}: \"{t.Id}\" 引用了不存在的原语 \"{b}\"（T2）✓");
                    }
                }
            }
        }
    }

    /// <summary>某 rarity 的 `award_category`（**购买判据的唯一来源** ✓）。</summary>
    public string AwardCategoryOf(string rarity)
        => Rarities.FirstOrDefault(r => r.Id == rarity)?.AwardCategory ?? "";

    /// <summary>
    /// 🔴 **T5 的单一落点（`#452` 更正后）**：**`award_category == "universal"` ⇒ 可购买**；
    /// 其余（`battle` / `dd` / `trophy` / `quest` …）⇒ **不可购买**（商店不列）✓
    /// ⚠️ 判据**不是** `price`（策划实测：`battle` 12 条里 11 条 `price > 1` ⇒ 用 price 判会漏）✓
    /// </summary>
    public bool IsPurchasable(TrinketConfig t) => AwardCategoryOf(t.Rarity) == "universal";

    /// <summary>可购买条数 / 不可购买条数（读数；**不影响任何行为** ✓）。</summary>
    public int PurchasableCount => Trinkets.Count(IsPurchasable);

    public int NonPurchasableCount => Trinkets.Count - PurchasableCount;

    /// <summary>按 id 取（不存在抛异常 —— fail-fast，照 `BuffDefsConfig.Get` ✓）。</summary>
    public TrinketConfig Get(string id)
    {
        TrinketConfig? t = Trinkets.FirstOrDefault(x => x.Id == id);
        if (t is null)
        {
            throw new InvalidDataException($"{ResPath}: 引用了不存在的饰品 \"{id}\"。");
        }

        return t;
    }
}
