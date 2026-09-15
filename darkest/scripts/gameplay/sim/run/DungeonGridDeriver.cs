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

    /// <summary>派生产物：网格 + **房间中心** + **格 → 房间 id**（内容引用零改动的关键）✓</summary>
    public sealed record Derived(
        DungeonGrid Grid,
        (int X, int Y) Start,
        IReadOnlyDictionary<int, (int X, int Y)> RoomCenters,
        IReadOnlyDictionary<(int X, int Y), int> TileRoom);

    /// <summary>房间类型 ⇒ 瓷砖字符（结构性映射；未登记类型 ⇒ `R`，不静默当成战斗 ✓）</summary>
    public static DungeonTileKind KindForRoomType(string type) => type switch
    {
        "battle" => DungeonTileKind.Battle,
        "event" => DungeonTileKind.Event,
        "camp" => DungeonTileKind.Camp,
        "treasure" => DungeonTileKind.Curio,
        _ => DungeonTileKind.Room,
    };

    public static Derived Derive(ExpeditionMap map)
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

            for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
            {
                Carve(x, a.Y);
            }

            for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
            {
                Carve(b.X, y);
            }
        }

        // ④ 起点/终点：房间中心；终点中心改写为 `G` ✓
        if (!centers.TryGetValue(map.StartId, out (int X, int Y) start))
        {
            throw new InvalidOperationException($"派生网格：地图起点房间 {map.StartId} 不在房间表里 ⇒ 拒绝派生。");
        }

        if (!centers.TryGetValue(map.GoalId, out (int X, int Y) goal))
        {
            throw new InvalidOperationException($"派生网格：地图终点房间 {map.GoalId} 不在房间表里 ⇒ 拒绝派生。");
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

        return new Derived(grid, start, centers, tileRoom);

        void Carve(int x, int y)
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
        }
    }
}
