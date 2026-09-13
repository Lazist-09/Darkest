using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>地图生成参数（M7.6 §4.3①：房间+走廊；节点数保持 6~8 的节奏感）。</summary>
public sealed record MapGenConfig(
    [property: JsonPropertyName("room_count_min")] int RoomCountMin,
    [property: JsonPropertyName("room_count_max")] int RoomCountMax,
    [property: JsonPropertyName("branch_chance")] double BranchChance,
    [property: JsonPropertyName("battle_weight")] double BattleWeight,
    [property: JsonPropertyName("event_weight")] double EventWeight,
    [property: JsonPropertyName("max_branches")] int MaxBranches = 2);

/// <summary>
/// `expedition_map.json` 根模型 + **P25 校验（M7.6）** —— 🔴 只定"地图生成"这一件事，
/// **不碰任何已调平参数**（O-76 原则①：光照 / 掉落 / 扎营一律不动）：
/// ① 房间数区间必须在 **[6, 8]**（保留现有节奏感）；
/// ② 概率 ∈ [0,1]；类型权重都 &gt; 0（否则某一类房间永远不出现）；
/// ③ 分叉数 ≥ 1（**必须存在"分叉点"**，否则又退化成线性）；上限 ≤ 3（防止地图爆炸）。
/// </summary>
public sealed record ExpeditionMapConfig(
    [property: JsonPropertyName("map")] MapGenConfig Map)
{
    public const string ResPath = "res://data/expedition_map.json";

    /// <summary>房间数下限（`m8_roadmap §4.3①`）。</summary>
    public const int MinAllowedRooms = 6;

    public const int MaxAllowedRooms = 8;

    public static ExpeditionMapConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        ExpeditionMapConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<ExpeditionMapConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
            }) ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    private static void Validate(ExpeditionMapConfig cfg)
    {
        MapGenConfig m = cfg.Map ?? throw new InvalidDataException($"{ResPath}: 缺 map（P25 ①）。");

        if (m.RoomCountMin < MinAllowedRooms || m.RoomCountMax > MaxAllowedRooms || m.RoomCountMin > m.RoomCountMax)
        {
            throw new InvalidDataException(
                $"{ResPath}: 房间数必须落在 [{MinAllowedRooms}, {MaxAllowedRooms}] 且 min ≤ max（P25 ①）。");
        }

        if (m.BranchChance is < 0 or > 1)
        {
            throw new InvalidDataException($"{ResPath}: branch_chance 必须 ∈ [0,1]（P25 ②）。");
        }

        if (m.BattleWeight <= 0 || m.EventWeight <= 0)
        {
            throw new InvalidDataException($"{ResPath}: battle/event 权重都必须 > 0（P25 ②：否则某类房间不出现）。");
        }

        if (m.MaxBranches < 1 || m.MaxBranches > 3)
        {
            throw new InvalidDataException($"{ResPath}: max_branches 必须 ∈ [1,3]（P25 ③：**至少一条分叉**）。");
        }
    }
}
