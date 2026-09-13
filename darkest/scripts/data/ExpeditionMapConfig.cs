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

/// <summary>按段移动（M7.6 §4.3②）：新区域 −30（沿用已调平值）／ 重走已探索 −10。</summary>
public sealed record MapMoveConfig(
    [property: JsonPropertyName("new_room_cost")] int NewRoomCost,
    [property: JsonPropertyName("revisit_cost")] int RevisitCost);

/// <summary>侦察（M7.6 §4.3③）：**揭示前方 1~3 步的拓扑**（成功概率沿用已调平口径）。</summary>
public sealed record MapScoutConfig(
    [property: JsonPropertyName("reveal_depth_min")] int RevealDepthMin,
    [property: JsonPropertyName("reveal_depth_max")] int RevealDepthMax);

/// <summary>
/// `expedition_map.json` 根模型 + **P25 校验（M7.6）** —— 🔴 只定"地图生成 + 按段移动 + 侦察深度"三件事，
/// **不碰任何已调平参数**（O-76 原则①：光照 / 掉落 / 扎营一律不动）：
/// ① 房间数区间必须在 **[6, 8]**；
/// ② 概率 ∈ [0,1]；类型权重都 &gt; 0；
/// ③ 分叉数 ∈ [1,3]（**必须存在"分叉点"**）；
/// ④ **按段移动**：两者都 &lt; 0 且 **|重走| &lt; |新区域|**（"回头更便宜"必须**可测**）；
/// ⑤ **侦察揭示深度 ∈ [1,3]**（§4.3③）。
/// </summary>
public sealed record ExpeditionMapConfig(
    [property: JsonPropertyName("map")] MapGenConfig Map,
    [property: JsonPropertyName("move")] MapMoveConfig? Move = null,
    [property: JsonPropertyName("scout")] MapScoutConfig? Scout = null)
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

        // ④ 按段移动（§4.3②）：新区域与重走的代价都必须为负，且**回头更便宜**（可测）
        MapMoveConfig mv = cfg.Move ?? throw new InvalidDataException($"{ResPath}: 缺 move（P25 ④）。");
        if (mv.NewRoomCost >= 0 || mv.RevisitCost >= 0)
        {
            throw new InvalidDataException($"{ResPath}: new_room_cost 与 revisit_cost 都必须 < 0（P25 ④）。");
        }

        if (System.Math.Abs(mv.RevisitCost) >= System.Math.Abs(mv.NewRoomCost))
        {
            throw new InvalidDataException(
                $"{ResPath}: **回头必须更便宜** —— |revisit|({System.Math.Abs(mv.RevisitCost)}) < |new|({System.Math.Abs(mv.NewRoomCost)})（P25 ④ / §4.3②）。");
        }

        // ⑤ 侦察深度（§4.3③）：1~3 步，且 min ≤ max
        MapScoutConfig sc = cfg.Scout ?? throw new InvalidDataException($"{ResPath}: 缺 scout（P25 ⑤）。");
        if (sc.RevealDepthMin < 1 || sc.RevealDepthMax > 3 || sc.RevealDepthMin > sc.RevealDepthMax)
        {
            throw new InvalidDataException(
                $"{ResPath}: reveal_depth 必须 ∈ [1,3] 且 min ≤ max（P25 ⑤ / §4.3③：揭示前方 1~3 步拓扑）。");
        }
    }
}
