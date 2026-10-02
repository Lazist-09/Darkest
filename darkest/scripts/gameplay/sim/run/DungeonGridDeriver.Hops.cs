// ① 来源：从 `DungeonGridDeriver.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **图上跳数查询**（`HopsOnMap` 原 :352-383 ＋ `RoomAtHops` 原 :386-413；M11 ① 预警面第二十四件·第二片）✓
// ② 职责：给主片「把终点沿主干提前到 ≤ `maxTrunkSegments` 段」提供 BFS 读数（跳数 ／ 恰好 N 跳的房间，多个取 id 最小 ⇒ 确定性）✓
// ③ 🔴 依赖（实测扫描本片）：仅 `ExpeditionMap`／`MapEdge`（`ExpeditionMap.cs`）＋ `System.Linq`（`OrderBy`／`First`）；BFS 容器全用完全限定名 ⇒ **零跨片依赖** ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 1 条；构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
using System.Linq;

namespace Darkest.Gameplay.Sim.Run;

public static partial class DungeonGridDeriver
{
    /// <summary>图上两房间的**跳数**（= 段数；不可达 ⇒ `int.MaxValue`）✓</summary>
    private static int HopsOnMap(ExpeditionMap map, int from, int to)
    {
        if (from == to)
        {
            return 0;
        }

        var seen = new System.Collections.Generic.HashSet<int> { from };
        var q = new System.Collections.Generic.Queue<(int Id, int D)>();
        q.Enqueue((from, 0));
        while (q.Count > 0)
        {
            (int id, int d) = q.Dequeue();
            foreach (MapEdge e in map.Edges)
            {
                int next = e.From == id ? e.To : e.To == id ? e.From : -1;
                if (next < 0 || !seen.Add(next))
                {
                    continue;
                }

                if (next == to)
                {
                    return d + 1;
                }

                q.Enqueue((next, d + 1));
            }
        }

        return int.MaxValue;
    }

    /// <summary>恰好 `hops` 跳能到的房间（多个取 id 最小 ⇒ **确定性** ✓；没有 ⇒ null）✓</summary>
    private static int? RoomAtHops(ExpeditionMap map, int from, int hops)
    {
        var seen = new System.Collections.Generic.HashSet<int> { from };
        var frontier = new System.Collections.Generic.List<int> { from };
        for (int step = 0; step < hops; step++)
        {
            var next = new System.Collections.Generic.List<int>();
            foreach (int id in frontier)
            {
                foreach (MapEdge e in map.Edges)
                {
                    int n = e.From == id ? e.To : e.To == id ? e.From : -1;
                    if (n >= 0 && seen.Add(n))
                    {
                        next.Add(n);
                    }
                }
            }

            if (next.Count == 0)
            {
                return null;
            }

            frontier = next;
        }

        return frontier.OrderBy(x => x).First();
    }
}
