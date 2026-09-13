using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>一次移动的结果：代价、是否重走、到达的房间。</summary>
public sealed record MoveOutcome(bool Moved, int Cost, bool Revisited, int ToRoomId, string Reason);

/// <summary>
/// **M7.6 片 ②：按段移动**（`m8_roadmap §4.3②`）—— 在**生成好的地图**上走：
/// · **新区域 −30**（沿用已调平的 `light.node_step`，O-76 原则①**不动它**）；
/// · **重走已探索 −10**（新增）⇒ **回头更便宜、绕路有代价**。
///
/// 🔴 边界：**只能走相邻房间**（走廊）；**未访问过的房间 = 新区域**（首次进入才标记）；
/// 代价全部走 `LightMeter`（同一 `LightChangedEvent` 通道）⇒ 事件流可审计。
/// </summary>
public static class MapTraversal
{
    /// <summary>已访问房间集合（由调用方持有；本类是纯函数）。</summary>
    public static bool IsAdjacent(ExpeditionMap map, int from, int to)
        => map.Edges.Any(e => (e.From == from && e.To == to) || (e.From == to && e.To == from));

    /// <summary>
    /// 从 `from` 走到 `to`：**必须相邻**（否则拒绝、不耗光）；按"是否已访问"取代价并推进光照。
    /// </summary>
    public static MoveOutcome Step(CombatLog log, ExpeditionMap map, MapMoveConfig move, LightMeter meter,
        int from, int to, ISet<int> visited)
    {
        if (log is null || map is null || move is null || meter is null || visited is null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        if (!IsAdjacent(map, from, to))
        {
            return new MoveOutcome(false, 0, false, from, "not_adjacent"); // 拒绝：不能瞬移
        }

        bool revisited = visited.Contains(to);
        int cost = revisited ? move.RevisitCost : move.NewRoomCost;
        meter.TryAdvanceBy(log, cost, revisited ? "revisit" : "advance");
        visited.Add(to);
        return new MoveOutcome(true, cost, revisited, to, revisited ? "revisit" : "advance");
    }

    /// <summary>从起点走到终点的**最短段数**（BFS；用于报告"这张图要走几段"）。</summary>
    public static int ShortestPathLength(ExpeditionMap map, int from, int to)
    {
        var adj = new Dictionary<int, List<int>>();
        foreach (MapEdge e in map.Edges)
        {
            (adj.TryGetValue(e.From, out List<int>? a) ? a : adj[e.From] = new List<int>()).Add(e.To);
            (adj.TryGetValue(e.To, out List<int>? b) ? b : adj[e.To] = new List<int>()).Add(e.From);
        }

        var dist = new Dictionary<int, int> { [from] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            if (cur == to)
            {
                return dist[cur];
            }

            foreach (int next in adj.GetValueOrDefault(cur) ?? new List<int>())
            {
                if (!dist.ContainsKey(next))
                {
                    dist[next] = dist[cur] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        return -1; // 不连通
    }
}
