using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M5 · Quirk 条目** —— 一手 E 盘 `shared/quirk/quirk_library.json` 逐字段转写（15 字段 + `origin` 溯源）✓
///
/// schema 来源：派发卡 M5（`is_positive`/`is_disease`/`classification`/`incompatible_quirks`/`curio_tag`）✓
/// 实测补充：另外 10 个字段（`buffs` / `can_be_replaced_by_new_quirk` / `can_modify_in_activity` /
///   `curio_tag_chance` / `keep_loot` / `random_chance` / 三个 `show_explicit_*`）⇒ 一并转写（**不丢字段** ✓）
/// </summary>
public sealed record QuirkConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("buffs")] IReadOnlyList<string> Buffs,
    [property: JsonPropertyName("is_positive")] bool IsPositive,
    [property: JsonPropertyName("is_disease")] bool IsDisease,
    [property: JsonPropertyName("classification")] string Classification,
    [property: JsonPropertyName("incompatible_quirks")] IReadOnlyList<string> IncompatibleQuirks,
    [property: JsonPropertyName("curio_tag")] string CurioTag,
    [property: JsonPropertyName("curio_tag_chance")] double CurioTagChance,
    [property: JsonPropertyName("keep_loot")] bool KeepLoot,
    [property: JsonPropertyName("random_chance")] double RandomChance,
    [property: JsonPropertyName("can_be_replaced_by_new_quirk")] bool CanBeReplacedByNewQuirk,
    [property: JsonPropertyName("can_modify_in_activity")] bool CanModifyInActivity,
    [property: JsonPropertyName("show_explicit_buff_description")] bool ShowExplicitBuffDescription,
    [property: JsonPropertyName("show_flavor_description")] bool ShowFlavorDescription,
    [property: JsonPropertyName("show_explicit_curio_tag_description")] bool ShowExplicitCurioTagDescription,
    [property: JsonPropertyName("origin")] string? Origin = null);

/// <summary>
/// 🔴 **M5 · Quirk 表根** —— 结构与合法性（**含"互斥必须可断言"** ✓）。
///
/// 🔴 **互斥的口径（一手实测得来，不是假设）**：
///   实测 E 盘一手：**悬空引用 0** ✓ · **非对称 0（全对称）** ✓　⇒ 所以两条校验都能钉：
///   ① **引用完整性**：`incompatible_quirks` 里每个 id 必须存在于库中（否则红 ✓）
///   ② **对称性**：`a→b` 必须有 `b→a`（否则红 ✅ —— 数据一旦被改坏会当场暴露 ✓）
///   判定用 `AreIncompatible(a, b)`（**单一落点**：任一方向有边即互斥 ✓）
///
/// ⚠️ 与 M4 一致的协议：**不做 `QuirkLedger`** —— "英雄身上有哪些 quirk"归 `Roster` ✓
/// </summary>
public sealed class QuirksConfig
{
    public const string ResPath = "res://data/quirks.json";

    [JsonPropertyName("quirks")]
    public IReadOnlyList<QuirkConfig> Quirks { get; init; } = Array.Empty<QuirkConfig>();

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static QuirksConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        QuirksConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<QuirksConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    private static void Validate(QuirksConfig cfg)
    {
        if (cfg.Quirks.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: quirks 为空。");
        }

        var byId = new Dictionary<string, QuirkConfig>(StringComparer.Ordinal);
        foreach (QuirkConfig q in cfg.Quirks)
        {
            if (string.IsNullOrWhiteSpace(q.Id))
            {
                throw new InvalidDataException($"{ResPath}: 存在缺失 id 的 quirk。");
            }

            if (!byId.TryAdd(q.Id, q))
            {
                throw new InvalidDataException($"{ResPath}: quirk id \"{q.Id}\" 重复。");
            }
        }

        foreach (QuirkConfig q in cfg.Quirks)
        {
            foreach (string other in q.IncompatibleQuirks)
            {
                // ① 引用完整性（悬空 ⇒ 红）✓
                if (!byId.ContainsKey(other))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{q.Id}\" 的 incompatible_quirks 引用了不存在的 \"{other}\" ✓");
                }

                // ② 自反（自己和自己互斥 ⇒ 逻辑上无解）✓
                if (other == q.Id)
                {
                    throw new InvalidDataException($"{ResPath}: \"{q.Id}\" 与自己互斥 ✓");
                }

                // ③ 对称性（实测一手数据 100% 对称 ⇒ 破对称就是数据被改坏）✓
                if (!byId[other].IncompatibleQuirks.Contains(q.Id))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: 互斥**不对称**：\"{q.Id}\" → \"{other}\"，但 \"{other}\" 没有指回（实测一手全对称）✓");
                }
            }
        }
    }

    /// <summary>🔴 互斥的**单一落点**：任一方向有边即互斥（对称性已由校验保证 ✓）。</summary>
    public bool AreIncompatible(string a, string b)
    {
        QuirkConfig qa = Get(a);
        return qa.IncompatibleQuirks.Contains(b);
    }

    /// <summary>按 id 取（不存在抛异常 —— fail-fast ✓）。</summary>
    public QuirkConfig Get(string id)
    {
        QuirkConfig? q = Quirks.FirstOrDefault(x => x.Id == id);
        if (q is null)
        {
            throw new InvalidDataException($"{ResPath}: 引用了不存在的 quirk \"{id}\"。");
        }

        return q;
    }

    /// <summary>读数：互斥边总数（供报表；**不影响行为** ✓）。</summary>
    public int IncompatibleEdgeCount => Quirks.Sum(q => q.IncompatibleQuirks.Count);
}
