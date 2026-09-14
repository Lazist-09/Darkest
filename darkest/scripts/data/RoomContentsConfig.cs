using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>内容表的一行：**组内权重** + 编成 id（可空）+ 该行的 Curio 池（**引用 `curios.json` 的 id**）。</summary>
public sealed record RoomContentEntry(
    [property: JsonPropertyName("weight")] int Weight,
    [property: JsonPropertyName("encounter")] string? Encounter,
    [property: JsonPropertyName("curio_pool")] IReadOnlyList<string>? CurioPool);

/// <summary>`room_contents.json` 的头部（留版本位，便于将来演进）。</summary>
public sealed record RoomContentsHeader(
    [property: JsonPropertyName("version")] int Version = 1);

/// <summary>
/// 🔴 **内容层**（`O-85` / 合并包片 B）：**房间类型 → 可能的编成池 + Curio 池 + 权重**。
///
/// 三条读法（契约 `tasks/merged_content_layer_pack.md` §4）：
/// ① `weight` 是**组内**权重（不是全局）；
/// ② `encounter` 引用**编成 id**（不存在 ⇒ 启动报错）；
/// ③ `curio_pool` **引用** `curios.json` 的 id（**引用而非拷贝** ⇒ 统一读取层、不统一数据文件）。
///
/// 分层（B2/B5）：**拓扑**（`ExpeditionMapGenerator`：哪里长什么类型的房间）与**内容**（本表：某类型的房间放什么）
/// **不压成一层**；🔴 `branch_battle_weight` 因此**降级为【过渡覆盖项】（默认 0 = 由本表决定）** —— 主 = 内容表。
///
/// 内核层：**零 Godot**（由组合根把文件字符串喂进来）✓
/// </summary>
public sealed record RoomContentsConfig(
    [property: JsonPropertyName("config")] RoomContentsHeader? Config,
    [property: JsonPropertyName("rooms")] IReadOnlyDictionary<string, IReadOnlyList<RoomContentEntry>> Rooms)
{
    public const string ResPath = "res://data/room_contents.json";

    /// <summary>合法的房间类型键（与本项目地图层的类型词汇一致；`branch` 是支路专用覆盖键）。</summary>
    public static readonly IReadOnlySet<string> KnownRoomTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "battle", "event", "branch",
    };

    public IReadOnlyList<RoomContentEntry> ForType(string roomType)
        => Rooms.TryGetValue(roomType, out IReadOnlyList<RoomContentEntry>? list)
            ? list
            : Array.Empty<RoomContentEntry>();

    public static RoomContentsConfig Parse(string json, CuriosConfig? curios = null,
        IReadOnlySet<string>? encounterIds = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        RoomContentsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<RoomContentsConfig>(json, new JsonSerializerOptions
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

        Validate(cfg, curios, encounterIds);
        return cfg;
    }

    /// <summary>
    /// **P26 校验**（合并包片 B 的验收）：① 至少一类房间；② 每行 `weight > 0`；
    /// ③ 房间类型键必须是已知类型；④ 🔴 **引用的 id 必须存在**（`encounter` 在编成目录里、`curio_pool` 在 `curios.json` 里）。
    /// 编成目录当前**尚未建立** ⇒ 传 `null` 时**任何非空 `encounter` 都判错**（这才是"引用必须存在"的正确含义）。
    /// </summary>
    public static void Validate(RoomContentsConfig cfg, CuriosConfig? curios = null,
        IReadOnlySet<string>? encounterIds = null)
    {
        if (cfg.Rooms is null || cfg.Rooms.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: 至少要有一类房间（P26 ①）。");
        }

        var curioIds = curios is null
            ? null
            : curios.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        foreach ((string type, IReadOnlyList<RoomContentEntry> entries) in cfg.Rooms)
        {
            if (!KnownRoomTypes.Contains(type))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 未知房间类型 \"{type}\"（已知：{string.Join("/", KnownRoomTypes)}）（P26 ③）。");
            }

            if (entries is null || entries.Count == 0)
            {
                throw new InvalidDataException($"{ResPath}: 房间类型 \"{type}\" 的行列表为空（P26 ①）。");
            }

            foreach (RoomContentEntry e in entries)
            {
                if (e.Weight <= 0)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{type}\" 有一行 weight = {e.Weight}（必须 > 0 —— 组内权重）（P26 ②）。");
                }

                if (e.Encounter is { } enc)
                {
                    if (encounterIds is null || !encounterIds.Contains(enc))
                    {
                        throw new InvalidDataException(
                            $"{ResPath}: \"{type}\" 引用了不存在的编成 \"{enc}\" —— " +
                            "🔴 编成目录尚未建立（属内容层待补）：**当前不得引用任何编成 id**（P26 ④ / B6/红线 21）。");
                    }
                }

                foreach (string curioId in e.CurioPool ?? Array.Empty<string>())
                {
                    // 🔴 `#316`② / P26：`curio_pool` 的两种写法 —— **id** 或 **`$pool:<name>`**。
                    //    两者都必须是【引用】（不得把 Curio 定义拷进内容表）✓
                    //    ⚠️ 本片**只让校验通过，不实现解析**（解析留给"具名池"那一轮）✓
                    if (curioId.StartsWith("$pool:", StringComparison.Ordinal))
                    {
                        string poolName = curioId["$pool:".Length..];
                        if (curios is null || !curios.HasPool(poolName))
                        {
                            throw new InvalidDataException(
                                $"{ResPath}: \"{type}\" 引用了不存在的具名池 \"{poolName}\" —— " +
                                "池定义放 `curios.json` 的 `pools` 段（**不新增文件**）（P26 ④）。");
                        }

                        continue;
                    }

                    if (curioIds is null)
                    {
                        continue; // 未传目录 ⇒ 不校验（调用方应传；启动路径会传）
                    }

                    if (!curioIds.Contains(curioId))
                    {
                        throw new InvalidDataException(
                            $"{ResPath}: \"{type}\" 的 curio_pool 引用了 `curios.json` 里不存在的 id \"{curioId}\"（P26 ④）。");
                    }
                }
            }
        }
    }
}
