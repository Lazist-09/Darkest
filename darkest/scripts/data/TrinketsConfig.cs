using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M4 · Trinket（饰品）** —— 契约 `doc/modules/trinkets.md`（策划 `#437`）的**数据 + 校验**件。
///
/// 四件套（纪律 AF）里本件的位置：
///   **数据** = `data/trinkets.json` ✓　**解析+校验** = **本文件** ✓
///   **运行时状态** = 🔴 **不要** `TrinketLedger`（契约 §3 明确）—— 饰品只是"挂在英雄上的 buff 集合"，
///   而**"已装备关系"归 `Roster`**（每英雄 **2 槽**）⇒ 本件**只做结构/合法性**，不持有任何运行时状态 ✓
///
/// 契约给的验收（我照它做成可测的）：
///   **T1** 字段齐全：`id` / `buffs` / `hero_class_requirements` / `rarity` / `price` / `limit` / `origin_dungeon` ✓
///   **T2** `id` 唯一 · `buffs` 引用**必须存在于原语层** · `rarity` ∈ 声明集合 · `price ≥ 0` ✓
///   **T5** `price ≤ 1` ⇒ **不可购买**（商店不列）⇒ 本件给 `IsPurchasable` 一个**单一落点** ✓
/// </summary>
public sealed record TrinketConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("buffs")] IReadOnlyList<string> Buffs,
    [property: JsonPropertyName("hero_class_requirements")] IReadOnlyList<string> HeroClassRequirements,
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("price")] int Price,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("origin_dungeon")] string OriginDungeon,
    // 溯源字段（阶段 A 对齐数据一律标来源；契约 §3 的 `origin` ✓）
    [property: JsonPropertyName("origin")] string? Origin = null);

/// <summary>
/// 🔴 **M4 · Trinket 表根**：`Parse` 只做"读进来 + 校验"，**不改任何数值** ✓
/// </summary>
public sealed class TrinketsConfig
{
    public const string ResPath = "res://data/trinkets.json";

    [JsonPropertyName("rarities")]
    public IReadOnlyList<string> Rarities { get; init; } = Array.Empty<string>();

    [JsonPropertyName("trinkets")]
    public IReadOnlyList<TrinketConfig> Trinkets { get; init; } = Array.Empty<TrinketConfig>();

    /// <summary>
    /// 🔴 **原语层交叉校验是否真的跑了**（**自证**，同 `TrapResistSourceDeclared` 的思路）：
    /// 原语层（M2 的 `dd1_buffs.json`）**尚未就位**时 ⇒ 这一步**跳过** ⇒ 必须**说出来**，不许静默当"通过" ✓
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

    /// <summary>
    /// 读入并校验 ✓
    /// </summary>
    /// <param name="json">`data/trinkets.json` 的内容 ✓</param>
    /// <param name="knownBuffIds">
    /// **原语层**（M2）里存在的 buff id 集合；**null = 原语层未就位** ⇒ 跳过交叉校验并**如实标注** ✓
    /// </param>
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

        var declared = new HashSet<string>(cfg.Rarities, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (TrinketConfig t in cfg.Trinkets)
        {
            if (string.IsNullOrWhiteSpace(t.Id))
            {
                throw new InvalidDataException($"{ResPath}: 存在缺失 id 的饰品。");
            }

            // T2 ①：id 唯一 ✓
            if (!seen.Add(t.Id))
            {
                throw new InvalidDataException($"{ResPath}: 饰品 id \"{t.Id}\" 重复（T2）✓");
            }

            // T2 ③：rarity 必须在**声明的集合**里（不发明第 13 种；集合由数据自己声明 ✓）
            if (!declared.Contains(t.Rarity))
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{t.Id}\" rarity=\"{t.Rarity}\" 不在声明的 rarities 里（T2）✓");
            }

            // T2 ④：price ≥ 0 ✓
            if (t.Price < 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{t.Id}\" price={t.Price} 为负（T2）✓");
            }

            // T2 ②：buffs 必须存在于**原语层** —— 原语层未就位则**跳过且已自证** ✓
            if (knownBuffIds is not null)
            {
                foreach (string b in t.Buffs)
                {
                    if (!knownBuffIds.Contains(b))
                    {
                        throw new InvalidDataException(
                            $"{ResPath}: \"{t.Id}\" 引用了不存在的原语 \"{b}\"（T2）✓");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 🔴 **T5 的单一落点**：`price ≤ 1` ⇒ **不可购买**（商店不列）✓
    /// （契约原文：`price=0/1` 那批与 `award_category` 100% 吻合 ⇒ 它们是"奖励/任务"来源，不是商品 ✓）
    /// </summary>
    public static bool IsPurchasable(TrinketConfig t) => t.Price > 1;

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

    /// <summary>可购买条数（供商店接线前的读数；**不影响任何行为** ✓）。</summary>
    public int PurchasableCount => Trinkets.Count(IsPurchasable);
}
