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
    // 🔴 数字外置（P29）：去默认值（`expedition_map.json` 已有该键；`Parse` 里另有存在性断言）✓
    //    ⚠️ **必需参数必须排在可选参数之前**（C# CS1737）—— 这是我第 3 次踩，见提交信息里的教训 ✓
    [property: JsonPropertyName("branch_special_light_gain")] int BranchSpecialLightGain,
    [property: JsonPropertyName("branch_battle_weight")] double BranchBattleWeight = 0,
    [property: JsonPropertyName("branch_special_weight")] double BranchSpecialWeight = 0,
    [property: JsonPropertyName("branch_special_kind")] string BranchSpecialKind = "free_light",
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

    /// <summary>
    /// 支路**特殊房**的合法类型（`#298` (c)）：**只允许"降低撤退风险"类**，
    /// 🔴 **不得只加"更多资源"类**（拓扑下补给已过剩 ⇒ 再给资源无效）。
    /// ⚠️ 本清单**只登记"效果已接线"的类型**（红线 21：不留死声明）—— `free_light` 已接线；
    /// 免费恢复房 / 士气房**待其效果接线后**再加入本清单。
    /// </summary>
    public static readonly IReadOnlyList<string> SpecialBranchKinds = new[] { "free_light" };

    public static ExpeditionMapConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        // 🔴 数字外置（P29）：必需键必须在数据里显式给出（原先 `BranchSpecialLightGain = 20` 会静默兜底）✓
        DataPresence.RequireKeys(ResPath, json, "branch_special_light_gain");

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

        // ⑦ 支路房间类型权重（`O-79` 候选 (a)：让支路也能含战斗房）—— **新增旋钮、默认 0 = 现状**
        if (m.BranchBattleWeight is < 0 or > 100)
        {
            throw new InvalidDataException($"{ResPath}: branch_battle_weight 必须 ∈ [0,100]（P25 ⑦ / O-79）。");
        }

        // ⑧ 支路**特殊房**（`#298` 采纳的 (c)）：🔴 必须是【**降低撤退风险**】类，**不得是"更多资源"类**
        //    （理由：拓扑下补给已过剩 —— 绕支路 14.38/趟 vs 旧线性 2.83，完成率不升 ⇒ 再给资源无效）
        if (m.BranchSpecialWeight is < 0 or > 100)
        {
            throw new InvalidDataException($"{ResPath}: branch_special_weight 必须 ∈ [0,100]（P25 ⑧ / #298）。");
        }

        if (m.BranchSpecialWeight > 0 && !SpecialBranchKinds.Contains(m.BranchSpecialKind))
        {
            throw new InvalidDataException(
                $"{ResPath}: branch_special_kind 必须是【降低撤退风险】类（允许：{string.Join(" ／ ", SpecialBranchKinds)}；" +
                $"**不得只加资源类**；P25 ⑧ / #298）。");
        }

        // ⑨ **净光照代价必须 > 0**（`#299` / 红线 24「净代价」）：
        //    净 = (该段耗 −30) + light_gain 必须 < 0 ⇒ **light_gain < 30**
        //    🔴 否则支路"自己还本" ⇒ 退化成纯赚（实测 X=20 时激进 88% > 保守/均衡 84%）
        if (m.BranchSpecialWeight > 0 && (m.BranchSpecialLightGain <= 0 || m.BranchSpecialLightGain >= 30))
        {
            throw new InvalidDataException(
                $"{ResPath}: branch_special_light_gain 必须 ∈ (0, 30) —— 净光照代价必须 > 0（P25 ⑨ / 红线 24 / #299）。");
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

        // ⑥ 支路必须有"有去有回"的代价（#294 ① 的硬要求）：
        //    探索支路的往返代价 = |new| + |revisit| **必须严格大于**主干一段（|new|）
        //    ⇒ 否则"多探索"是纯赚（多掉落机会 + 无代价）⇒ 支路就不是决策
        if (System.Math.Abs(mv.NewRoomCost) + System.Math.Abs(mv.RevisitCost) <= System.Math.Abs(mv.NewRoomCost))
        {
            throw new InvalidDataException(
                $"{ResPath}: **支路必须有往返代价** —— |new|+|revisit| 必须 > 主干一段（P25 ⑥ / #294 ①）。");
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
