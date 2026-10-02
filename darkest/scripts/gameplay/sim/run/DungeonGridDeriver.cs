// 🔴 **从自身拆出两片**（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **派生主流程**（数据契约与结构映射 原 :24-72 ＋ `Derive` 四重载与局部函数 `Carve` 原 :74-259）⇒
//    走廊格散布（陷阱 `^` / 隐藏房 `*`）⇒ `DungeonGridDeriver.Scatter.cs`；图上跳数 ⇒ `DungeonGridDeriver.Hops.cs`（M11 ① 预警面第二十四件 · 2026-10-02 · 只搬家 · 零行为改动）✓

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
public static partial class DungeonGridDeriver
{
// ---------------------------------------------------------------------------
// 数据契约与结构映射（`RoomSize` / `RoomGap` / `CorridorSegment` / `Derived` / `KindForRoomType` / `MaxTrunkSegments`）
// ---------------------------------------------------------------------------
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
        int TruncatedFromTrunkSegments = 0,

        /// <summary>🔴 `D-4`：本次撒下的**陷阱格数**（0 = 未开启撒陷阱，或概率全落空）✓</summary>
        int TrapTiles = 0,

        /// <summary>🔴 `D-6`：本次撒下的**隐藏房格数**（0 = 未开启撒隐藏房，或概率全落空）✓</summary>
        int SecretTiles = 0);

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

// ---------------------------------------------------------------------------
// `Derive` 四重载：房间落位 ⇒ 挖房间 ⇒ L 形走廊 ⇒ 起终点（沿主干提前 ≤ `maxTrunkSegments` 段）⇒ 撒布（opt-in）
// ---------------------------------------------------------------------------
    public static Derived Derive(ExpeditionMap map) => Derive(map, MaxTrunkSegments);

    public static Derived Derive(ExpeditionMap map, int maxTrunkSegments)
        => Derive(map, maxTrunkSegments, null, 0);

    /// <summary>🔴 `D-4` 兼容重载（只撒陷阱，不撒隐藏房）—— 既有调用点行为**逐字不变** ✓</summary>
    public static Derived Derive(ExpeditionMap map, int maxTrunkSegments, Darkest.Core.Rng.IRngProvider? rng,
        double trapChancePercent, Darkest.Core.Events.CombatLog? log = null)
        => Derive(map, maxTrunkSegments, rng, trapChancePercent, 0, log);

    /// <summary>
    /// 🔴🔴 `D-4`（2026-09-20）：**派生时撒陷阱格**（opt-in）。
    ///
    /// <para>⚠️ **为什么必须有这一步**：`DungeonTileKind.Trap` 此前**全仓无人产出** ——
    /// 派生器只挖"房间（`R`/`!`/`E`/`A`/`?`）"与"走廊（`C`）" ⇒ 图上一个 `^` 都没有
    /// ⇒ `TrapResolver` 再完备也**永远触发不了**（典型的"库写完了但没人接线"，红线 21 家族）⚠️</para>
    ///
    /// <para>**散布口径**（写死，避免各处各写一份）：</para>
    /// <para>· **只在走廊格上撒**（DD：陷阱在走廊里 —— 「Traps can sometimes be encountered along corridors」）；
    ///   房间格**永不**放陷阱（房间有自己的遭遇物）✓</para>
    /// <para>· 每个**走廊段**最多撒 1 个，且**跳过段的首尾格**（首格刚从房间出来、尾格马上进房间 ⇒
    ///   那两格放陷阱会让"进出房间"变成惩罚，与 DD 手感不符）✓</para>
    /// <para>· 概率由 `trapChancePercent` 给（**未给 / ≤0 ⇒ 一格都不撒** ⇒ 既有调用点行为逐字不变）✓</para>
    /// <para>· 🔴 **每次掷骰必写 `RngDraw`**（红线）；`rng` 为 null 且有概率 ⇒ **抛错**
    ///   （不许"想撒但不给随机源"这种静默半生效）⚠️</para>
    /// <para>🔴🔴 `D-6`（2026-09-20）：**隐藏房 `*` 与陷阱共用这一趟走廊扫描** ——
    ///   同一格要么是陷阱、要么是隐藏房、要么是普通走廊（**互斥**，`DungeonTileKind` 是单值）⇒
    ///   若两次各扫一趟，同一格会被掷两次（且第二次会**覆盖**第一次的结果）⇒
    ///   "陷阱密度"会悄悄变成"隐藏房概率的函数"（不可解释）⚠️
    ///   ⇒ 一次遍历、每格**至多一次**掷骰、按 `OrderBy(Y).ThenBy(X)` 确定性推进 ✓</para>
    /// </summary>
    /// <param name="trapChancePercent">走廊格的陷阱概率（%）；≤0 ⇒ 关闭（零随机不留痕）✓</param>
    /// <param name="secretChancePercent">🔴 `D-6` 走廊格的隐藏房概率（%）；≤0 ⇒ 关闭 ✓</param>
    public static Derived Derive(ExpeditionMap map, int maxTrunkSegments, Darkest.Core.Rng.IRngProvider? rng,
        double trapChancePercent, double secretChancePercent, Darkest.Core.Events.CombatLog? log = null)
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

        // 🔴🔴 `D-4` / `D-6`：**撒陷阱格 + 隐藏房格**（opt-in；仅走廊、一次遍历、每格至多掷一次）——
        //    必须在"挖完走廊、定了终点"之后（终点格是房间中心，本来就不会被选中）✓
        (int trapCount, int secretCount) = ScatterCorridorContent(
            segments, tiles, width, rng, trapChancePercent, secretChancePercent, log);
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

        // ⚠️ 这个标识**不写 `//`**（如 `derived://...`）：三扫工具的注释切分会把字符串内的 `//`
        //    当成注释起点 ⇒ 该行后半段被当代码扫 ⇒ **假数字**（2026-09-20 踩过）⚠️
        DungeonGrid grid = DungeonGrid.Parse("derived-dungeon-grid", rows);
        if (grid.Goal != goal)
        {
            throw new InvalidOperationException("派生网格：终点与 `G` 格不一致（派生逻辑错误）⇒ 拒绝。");
        }

        return new Derived(grid, start, centers, tileRoom, segments, trunk, truncatedFrom, trapCount, secretCount);

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
}
