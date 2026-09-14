using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>具名编成的一条：槽位 → **`units.json` 原型 id**（引用，不是拷贝）。</summary>
public sealed record EncounterUnitConfig(
    [property: JsonPropertyName("slot")] int Slot,
    [property: JsonPropertyName("unit")] string Unit);

/// <summary>一个具名编成（`encounter` id ⇒ 敌方编成）。</summary>
public sealed record EncounterConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("enemy")] IReadOnlyList<EncounterUnitConfig> Enemy,
    [property: JsonPropertyName("note")] string? Note = null);

public sealed record EncountersHeader(
    [property: JsonPropertyName("version")] int Version = 1);

/// <summary>
/// 🔴 **具名编成目录**（内容层补缺：`#319` 期间登记「**编成目录尚未建立**」⇒ 本文件把它建起来）。
///
/// 背景（侦察所得，避免下次再找）：
/// · **编成已有一个来源** —— `data/formation.json`（`FormationConfig`：`initial_roster.enemy` = 槽位 → 原型 id
///   ＋ `obstacles` ＋ 九条 `rules`，含 P2 校验）；且 `FormationSortie`（`#286`）已把它**降为【阵型模板】**。
/// · 但 `room_contents.json` 的 `encounter` 字段引用的是**具名编成 id** ⇒ 此前**无对象可指**
///   ⇒ P26 只能"拒绝一切非空 encounter"（那是**正确的** fail-fast，但内容层因此缺一块）。
///
/// 🔴 **本表是【追加的覆盖层】，不替换 `formation.json`**（与 B5「不做全量替换」同一条纪律）：
/// · 内容表/房间**指定**了 encounter ⇒ 用它；
/// · **未指定**（或本表为空）⇒ 仍走 `formation.json` 的敌方编成（模板）✓
///
/// **P28 校验**（fail-fast）：① `id` 非空且**唯一**；② `enemy` 非空；③ 槽位在 `1..slotCount` 内、**同槽不得重复**；
/// ④ 🔴 **`unit` 必须是 `units.json` 里真实存在的【敌方】原型**（引用必须存在 —— 与 P26 同族）。
/// 内核层：**零 Godot** ✓
/// </summary>
public sealed record EncountersConfig(
    [property: JsonPropertyName("config")] EncountersHeader? Config,
    [property: JsonPropertyName("encounters")] IReadOnlyList<EncounterConfig> Encounters)
{
    public const string ResPath = "res://data/encounters.json";

    public EncounterConfig? Find(string id)
        => Encounters.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));

    public static EncountersConfig Parse(string json, int slotCount = 4,
        IReadOnlySet<string>? enemyUnitIds = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        EncountersConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<EncountersConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new InvalidDataException($"{ResPath}: 反序列化结果为 null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 解析失败 —— {ex.Message}", ex);
        }

        Validate(cfg, slotCount, enemyUnitIds);
        return cfg;
    }

    /// <summary>**P28 校验**（见类型注释）。</summary>
    public static void Validate(EncountersConfig cfg, int slotCount = 4, IReadOnlySet<string>? enemyUnitIds = null)
    {
        if (cfg.Encounters is null)
        {
            throw new InvalidDataException($"{ResPath}: 缺少 `encounters` 数组（形态要求；可为空但必须存在）。");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (EncounterConfig e in cfg.Encounters)
        {
            if (string.IsNullOrWhiteSpace(e.Id))
            {
                throw new InvalidDataException($"{ResPath}: 有一条编成缺少 `id`（P28 ①）。");
            }

            if (!seenIds.Add(e.Id))
            {
                throw new InvalidDataException($"{ResPath}: 编成 id \"{e.Id}\" **重复**（必须唯一）（P28 ①）。");
            }

            if (e.Enemy is null || e.Enemy.Count == 0)
            {
                throw new InvalidDataException($"{ResPath}: 编成 \"{e.Id}\" 的 `enemy` 为空（P28 ②）。");
            }

            var usedSlots = new HashSet<int>();
            foreach (EncounterUnitConfig u in e.Enemy)
            {
                if (u.Slot < 1 || u.Slot > slotCount)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: 编成 \"{e.Id}\" 的槽位 {u.Slot} 越界（合法 1..{slotCount}）（P28 ③）。");
                }

                if (!usedSlots.Add(u.Slot))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: 编成 \"{e.Id}\" 的槽位 {u.Slot} **重复**（同槽不能两个单位）（P28 ③）。");
                }

                if (enemyUnitIds is not null && !enemyUnitIds.Contains(u.Unit))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: 编成 \"{e.Id}\" 引用了不存在的【敌方】原型 \"{u.Unit}\"（须在 `units.json` 里）（P28 ④）。");
                }
            }
        }
    }
}
