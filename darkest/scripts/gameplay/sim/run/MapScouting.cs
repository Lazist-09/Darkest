using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Survival;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>一次侦察的结果（M7.6 §4.3③）：成功则给出**前方 1~3 步**的房间拓扑。</summary>
public sealed record MapScoutResult(bool Success, int RevealedDepth, IReadOnlyList<MapRoom> RevealedRooms);

/// <summary>
/// **M7.6 片 ③：侦察揭示前方 1~3 步拓扑**（`m8_roadmap §4.3③`）——
/// 取代旧的"只揭示下一节点类型"。
///
/// 🔴 与已调平参数的关系（O-76 原则①）：
/// · **成功概率沿用** `tuning.scouting.base_pct`（25%）**+ 当前光照档的 `scouting_pct` 加成** —— **不改**；
/// · 只改**揭示内容**：从"下一节点类型"变成"**前方 1~3 步的房间与走廊**"（DD 式）。
/// 🔴 随机必写 `RngDraw`（成功判定 ／ 揭示深度各一条）。
/// </summary>
public static class MapScouting
{
    /// <summary>侦察一次：返回成功与否 + 揭示的房间（失败 ⇒ 空）。</summary>
    public static MapScoutResult Roll(CombatLog log, IRngProvider rng, LightMeter meter,
        TuningScouting scouting, ExpeditionMapConfig mapCfg, ExpeditionMap map, int fromRoomId)
    {
        if (log is null || rng is null || meter is null || scouting is null || mapCfg is null || map is null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        // ① 成功判定（概率 = base_pct + 当前档加成；**沿用已调平口径**）
        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll));
        double chance = scouting.BasePct + meter.Effect.ScoutingPct;
        bool success = roll < chance;

        var revealed = new List<MapRoom>();
        int depth = 0;
        if (success)
        {
            // ② 揭示深度（1~3 步）
            MapScoutConfig sc = mapCfg.Scout!;
            depth = rng.NextInt(sc.RevealDepthMin, sc.RevealDepthMax + 1);
            log.Append(new RngDraw(rng.DrawCount, depth));
            revealed.AddRange(RevealWithin(map, fromRoomId, depth));
        }

        return new MapScoutResult(success, depth, revealed);
    }

    /// <summary>从 `from` 出发、**沿走廊 ≤ depth 步**可达的房间（不含起点自身；**不泄露更深层**）。</summary>
    public static IReadOnlyList<MapRoom> RevealWithin(ExpeditionMap map, int from, int depth)
    {
        var adj = new Dictionary<int, List<int>>();
        foreach (MapEdge e in map.Edges)
        {
            (adj.TryGetValue(e.From, out List<int>? a) ? a : adj[e.From] = new List<int>()).Add(e.To);
            (adj.TryGetValue(e.To, out List<int>? b) ? b : adj[e.To] = new List<int>()).Add(e.From);
        }

        var seen = new HashSet<int> { from };
        var frontier = new List<int> { from };
        var result = new List<MapRoom>();
        for (int step = 0; step < depth; step++)
        {
            var next = new List<int>();
            foreach (int cur in frontier)
            {
                foreach (int n in adj.GetValueOrDefault(cur) ?? new List<int>())
                {
                    if (seen.Add(n))
                    {
                        next.Add(n);
                        result.Add(map.Rooms.First(r => r.Id == n));
                    }
                }
            }

            frontier = next;
        }

        return result;
    }
}
