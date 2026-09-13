using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>地图上的一个房间（M7.6）。</summary>
public sealed record MapRoom(int Id, int Depth, string Type, bool IsBranch);

/// <summary>一条走廊（无向，用于连通性判定）。</summary>
public sealed record MapEdge(int From, int To);

/// <summary>生成出来的地图（M7.6 §4.3①：房间 + 走廊；有分叉、可有死路）。</summary>
public sealed record ExpeditionMap(
    IReadOnlyList<MapRoom> Rooms,
    IReadOnlyList<MapEdge> Edges,
    int StartId,
    int GoalId)
{
    public int RoomCount => Rooms.Count;

    /// <summary>分支房（IsBranch）数量。</summary>
    public int BranchCount => Rooms.Count(r => r.IsBranch);

    /// <summary>分叉点数量（度数 ≥ 3 的房间）—— 🔴 **必须 ≥ 1**，否则又退化成线性。</summary>
    public int ForkCount
    {
        get
        {
            var degree = new Dictionary<int, int>();
            foreach (MapEdge e in Edges)
            {
                degree[e.From] = degree.GetValueOrDefault(e.From) + 1;
                degree[e.To] = degree.GetValueOrDefault(e.To) + 1;
            }

            return degree.Values.Count(d => d >= 3);
        }
    }

    /// <summary>从起点到终点是否连通（BFS；🔴 生成后必须为真）。</summary>
    public bool IsConnected()
    {
        var adj = new Dictionary<int, List<int>>();
        foreach (MapEdge e in Edges)
        {
            (adj.TryGetValue(e.From, out List<int>? a) ? a : adj[e.From] = new List<int>()).Add(e.To);
            (adj.TryGetValue(e.To, out List<int>? b) ? b : adj[e.To] = new List<int>()).Add(e.From);
        }

        var seen = new HashSet<int> { StartId };
        var queue = new Queue<int>();
        queue.Enqueue(StartId);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (int next in adj.GetValueOrDefault(cur) ?? new List<int>())
            {
                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen.Count == RoomCount; // 全部房间可达
    }
}

/// <summary>
/// **M7.6 地图生成**（`m8_roadmap §4.3①`）：**房间 + 走廊**（取代"线性 6 节点 + 每步二选一"）。
///
/// 🔴 设计（与 O-76 三原则一致）：
/// · **只做地图生成** —— **不碰光照 / 掉落 / 扎营**（那些已调平，`#273~#278`）；
/// · 房间数 **6~8**（保留节奏感）；**必须有分叉点**、可以有**死路**（§4.3④）；
/// · 🔴 **所有随机必写 `RngDraw`**（红线：唯一随机出口）；**同 seed 必须复现**（确定性）。
///
/// ⚠️ 本片**只生成地图**，**尚未接进远征流程**（那一步会动"选路"⇒ 必须按 O-76 重跑 V10/A1/A2）。
/// </summary>
public static class ExpeditionMapGenerator
{
    /// <summary>生成一张地图（房间 + 走廊）。</summary>
    public static ExpeditionMap Generate(CombatLog log, IRngProvider rng, ExpeditionMapConfig cfg)
    {
        if (log is null || rng is null || cfg is null)
        {
            throw new ArgumentNullException(nameof(cfg));
        }

        MapGenConfig m = cfg.Map;

        // ① 房间数（写 RngDraw）
        int countRoll = rng.NextInt(m.RoomCountMin, m.RoomCountMax + 1);
        log.Append(new RngDraw(rng.DrawCount, countRoll));
        int count = countRoll;

        var rooms = new List<MapRoom>();
        var edges = new List<MapEdge>();

        // ② 主干：一条"房间 → 房间"的走廊（保证连通；分叉由后面的支路提供）
        for (int i = 0; i < count; i++)
        {
            double typeRoll = rng.NextPercent();
            log.Append(new RngDraw(rng.DrawCount, typeRoll));
            double total = m.BattleWeight + m.EventWeight;
            string type = typeRoll < (m.BattleWeight / total * 100.0) ? "battle" : "event";
            rooms.Add(new MapRoom(i, i, type, IsBranch: false));
            if (i > 0)
            {
                edges.Add(new MapEdge(i - 1, i));
            }
        }

        // ③ 分叉：在主干房间上挂**支路房**（分叉点 = 主干房；支路尽头 = 死路）
        int branches = 0;
        for (int depth = 1; depth < count - 1 && branches < m.MaxBranches; depth++)
        {
            double branchRoll = rng.NextPercent();
            log.Append(new RngDraw(rng.DrawCount, branchRoll));
            if (branchRoll >= (m.BranchChance * 100.0))
            {
                continue;
            }

            int branchId = rooms.Count;
            double branchTypeRoll = rng.NextPercent();
            log.Append(new RngDraw(rng.DrawCount, branchTypeRoll));
            string branchType = branchTypeRoll < (m.BattleWeight / (m.BattleWeight + m.EventWeight) * 100.0) ? "battle" : "event";
            rooms.Add(new MapRoom(branchId, depth + 1, branchType, IsBranch: true));
            edges.Add(new MapEdge(depth, branchId)); // 主干房 → 支路房（分叉点度数 ≥ 3）
            branches++;
        }

        return new ExpeditionMap(rooms, edges, StartId: 0, GoalId: count - 1);
    }
}
