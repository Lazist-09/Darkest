using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **阶段 3：把现有【房间 + 连线】图**派生**成瓷砖网格**（架构 `P30` ③ 认可的"内容零返工"路径；
/// 策划 `#338`④ 定的顺序：**先派生网格跑通 ⇒ 再手写真关卡**）✓
///
/// 约定（写死，避免各写一份）：
///   · **房间 = 3×3 格块**（策划 `#338`④：6 站位 + 走位空间）· **走廊 = 1 格宽** ✓
///   · 布局**确定性**：同 `Depth` 的房间按序竖排；`x = Depth × (3+1)`、`y = 序号 × (3+1)` ✓
///   · 连线 ⇒ 从 A 房间中心到 B 房间中心的 **L 形走廊**（先横后竖；确定性）✓
///   · 房间类型 ⇒ 瓷砖字符（`battle`→`!` · `event`→`E` · `camp`→`A` · 其它→`R`）✓
///   · 起点/终点 = 对应房间**中心**；终点中心改写为 `G` ✓
///   · 🔴 **不动任何内容引用**：房间 id 与"格 → 房间"的映射一并返回 ⇒ 内容表/Curio/战斗照旧 ✓
///
/// ⚠️ 这是**派生（占位）布局**，不是关卡设计：走廊可能与房间交叉（L 形直连所致）——
///    本阶段目标是"**让格子行走先跑起来**"，真关卡属阶段 4（策划手写 `tiles`）✓
/// </summary>
public static class DungeonGridDeriver
{
    /// <summary>房间格块边长（策划 `#338`④：建议 3×3）✓</summary>
    public const int RoomSize = 3;

    /// <summary>房间之间的走廊间隔（格）✓</summary>
    public const int RoomGap = 1;

    /// <summary>
    /// 🆕 **走廊段**（策划 `#338`① 的"段"在派生网格里的对应物）：
    /// `From/To` 两端房间 · `Tiles` = 该段的**走廊格序列**（从离开 A 后的第一格 → 进 B 前的最后一格）
    /// ⇒ 有了**预知长度**，"逐格扣 `floor(acc/剩余格数)`" 才能既**守恒**又**不靠猜** ✓
    /// </summary>
    public sealed record CorridorSegment(int From, int To, IReadOnlyList<(int X, int Y)> Tiles)
    {
        public int Length => Tiles.Count;
    }

    /// <summary>派生产物：网格 + **房间中心** + **格 → 房间 id** + **走廊段表**（内容引用零改动的关键）✓</summary>
    public sealed record Derived(
        DungeonGrid Grid,
        (int X, int Y) Start,
        IReadOnlyDictionary<int, (int X, int Y)> RoomCenters,
        IReadOnlyDictionary<(int X, int Y), int> TileRoom,
        IReadOnlyList<CorridorSegment> Segments,
        int TrunkSegments = 0,
        int TruncatedFromTrunkSegments = 0);

    /// <summary>房间类型 ⇒ 瓷砖字符（结构性映射；未登记类型 ⇒ `R`，不静默当成战斗 ✓）</summary>
    public static DungeonTileKind KindForRoomType(string type) => type switch
    {
        "battle" => DungeonTileKind.Battle,
        "event" => DungeonTileKind.Event,
        "camp" => DungeonTileKind.Camp,
        "treasure" => DungeonTileKind.Curio,
        _ => DungeonTileKind.Room,
    };

    /// <summary>
    /// 🔴 **主干段数上限**（策划 `#342`③ 的裁定）：**一张图的【主干段数】应 ≤ 3**
    /// （推论：`enter_value` 100 ÷ 段消耗 30 = **3.33** ⇒ 取 3 ⇒ **主干 ≈ 4 间房 / 3 条连线**）
    /// ⇒ 派生占位图据此**把终点提前到 ≤3 段处**（支路仍保留：**支路要绕路 ⇒ 靠扎营/提亮回填** = 设计意图 ✓）
    /// ⚠️ 这是**派生占位布局**的取舍，**不改任何数值**（`enter_value`/`new_area` 一行未动 ✓）
    /// </summary>
    public const int MaxTrunkSegments = 3;

    public static Derived Derive(ExpeditionMap map) => Derive(map, MaxTrunkSegments);

    public static Derived Derive(ExpeditionMap map, int maxTrunkSegments)
    {
        if (map is null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        if (map.Rooms.Count == 0)
        {
            throw new InvalidOperationException("派生网格：地图没有房间（生成器应保证 ≥1）⇒ 拒绝派生。");
        }

        int stride = RoomSize + RoomGap;

        // ① 房间落位（确定性）：按 Depth 分列，列内按原顺序竖排 ✓
        var centers = new Dictionary<int, (int X, int Y)>();
        var order = new List<int>();
        foreach (IGrouping<int, MapRoom> depth in map.Rooms.GroupBy(r => r.Depth).OrderBy(g => g.Key))
        {
            int i = 0;
            foreach (MapRoom room in depth)
            {
                centers[room.Id] = ((depth.Key * stride) + 1, (i * stride) + 1); // 中心 = 块内 (1,1) ✓
                order.Add(room.Id);
                i++;
            }
        }

        int width = (map.Rooms.Max(r => r.Depth) + 1) * stride;
        int height = map.Rooms.GroupBy(r => r.Depth).Max(g => g.Count()) * stride;
        var tiles = new DungeonTileKind[width * height];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = DungeonTileKind.Wall; // 先全墙，再挖房间与走廊 ✓
        }

        var tileRoom = new Dictionary<(int X, int Y), int>();
        var segments = new List<CorridorSegment>();

        // ② 挖房间：每个房间一个 RoomSize×RoomSize 格块；记录"格 → 房间"映射 ✓
        foreach (MapRoom room in map.Rooms)
        {
            (int cx, int cy) = centers[room.Id];
            DungeonTileKind kind = KindForRoomType(room.Type);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        continue;
                    }

                    tiles[(y * width) + x] = kind;
                    tileRoom[(x, y)] = room.Id;
                }
            }
        }

        // ③ 挖走廊：每条边 ⇒ A 中心 → B 中心的 **L 形**（先横后竖）1 格宽 ✓
        foreach (MapEdge e in map.Edges)
        {
            if (!centers.TryGetValue(e.From, out (int X, int Y) a)
                || !centers.TryGetValue(e.To, out (int X, int Y) b))
            {
                continue; // 悬空边（生成器不该产生；此处**不静默造房间** ✓）
            }

            var segTiles = new List<(int X, int Y)>();
            for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
            {
                Carve(x, a.Y, segTiles);
            }

            for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
            {
                Carve(b.X, y, segTiles);
            }

            segments.Add(new CorridorSegment(e.From, e.To, segTiles));
        }

        // ④ 起点/终点：房间中心；终点中心改写为 `G` ✓
        if (!centers.TryGetValue(map.StartId, out (int X, int Y) start))
        {
            throw new InvalidOperationException($"派生网格：地图起点房间 {map.StartId} 不在房间表里 ⇒ 拒绝派生。");
        }

        // 🔴 策划 #342③：**把终点沿主干提前到 ≤ `maxTrunkSegments` 段处**（否则"一罐光"走不到终点 ⚠️）
        int truncatedFrom = 0; // 记录"原主干段数"（> 上限时非 0）⇒ 供调用方打印 ✓
        int goalRoomId = map.GoalId;
        int trunk = HopsOnMap(map, map.StartId, map.GoalId);
        if (trunk > maxTrunkSegments)
        {
            goalRoomId = RoomAtHops(map, map.StartId, maxTrunkSegments) ?? map.GoalId;
            trunk = HopsOnMap(map, map.StartId, goalRoomId);
            truncatedFrom = HopsOnMap(map, map.StartId, map.GoalId); // 🔴 内核**不打日志**（零 Godot）⇒ 交给场景层打印 ✓
        }

        if (!centers.TryGetValue(goalRoomId, out (int X, int Y) goal))
        {
            throw new InvalidOperationException($"派生网格：终点房间 {goalRoomId} 不在房间表里 ⇒ 拒绝派生。");
        }

        tiles[(goal.Y * width) + goal.X] = DungeonTileKind.Goal;

        // ⑤ 组行并走**同一套**解析（形状/字符/可达性校验一并复用 ✓）
        var rows = new List<string>(height);
        for (int y = 0; y < height; y++)
        {
            var chars = new char[width];
            for (int x = 0; x < width; x++)
            {
                chars[x] = DungeonTileMap.ToChar(tiles[(y * width) + x]);
            }

            rows.Add(new string(chars));
        }

        DungeonGrid grid = DungeonGrid.Parse("derived://dungeon_grid", rows);
        if (grid.Goal != goal)
        {
            throw new InvalidOperationException("派生网格：终点与 `G` 格不一致（派生逻辑错误）⇒ 拒绝。");
        }

        return new Derived(grid, start, centers, tileRoom, segments, trunk, truncatedFrom);

        void Carve(int x, int y, List<(int X, int Y)> segTiles)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            (int X, int Y) pos = (x, y);
            if (tileRoom.ContainsKey(pos))
            {
                return; // 房间格**不被走廊覆盖**（保住房间类型与内容映射 ✓）
            }

            tiles[(y * width) + x] = DungeonTileKind.Corridor;
            segTiles.Add((x, y)); // 🆕 记进本段（走廊格序列）✓
        }
    }

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
