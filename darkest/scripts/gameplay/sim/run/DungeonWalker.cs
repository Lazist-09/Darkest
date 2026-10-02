using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkest.Gameplay.Sim.Run;

// ① 来源：从 `DungeonGrid.cs` 拆出（用户红线：程序文件 ≤600 行 · 架构 `file_size_split.md` §3 ①：能独立命名＋独立持有状态 ⇒ 抽真类型）——
//    本片 = **揭示三态 ＋ 走格者**（`RevealState` ＋ `DungeonWalker`，原 `:190-492` 逐字节）✓
// ② 职责：**走格者单一真值**（位置 ／ 揭示三态 ／ 步数 ／ 四向移动 ／ 视线与路径）；
//    原文件四类型中的「揭示＋移动」两件 —— `DungeonTileKind` ／ `DungeonTileMap` ／ `DungeonGrid` 留主片 ✓
// ③ 依赖关系：**真类型拆分（非 partial）** —— 只经主片**公开 API**（`DungeonGrid.InBounds`／`TileAt`／`Goal` · `DungeonTileMap.IsWalkable`）；
//    **零私有成员穿透** ⇒ 与主片可独立编译、独立测试 ✓
// ④ 只搬家、零行为改动（两类型逐字节原样；using 块按需裁剪：新片不抛 `InvalidDataException` ⇒ 不保留 `System.IO`）✓

/// <summary>
/// 🔴🔴 **揭示三态**（`D-3`，2026-09-20）—— DD 的**雾**其实有三档，不是两档：
/// <list type="bullet">
///   <item><description><see cref="Unexplored"/>（**暗**）= 从没接触过 ⇒ 完全不知道那格存在 ⚠️</description></item>
///   <item><description><see cref="Scouted"/>（**暗 + 亮轮廓**）= **拓扑层侦察**揭示 ⇒ 知道"那边有个东西"，但**没看清内容** ✓</description></item>
///   <item><description><see cref="Visited"/>（**浅灰**）= **走过** 或 **格视野照到** ⇒ 内容已知 ✓</description></item>
/// </list>
///
/// 🔴 **为什么必须把旧 `HashSet` 换掉**：旧 `_revealed` 是**二态**（"见过/没见过"），
/// 且 `TryStep` 里 `_revealed.Add` 与 `_visited.Add` **恒同时置位** ⇒
/// **"只被侦察到、从没走过"的格根本无法表达** —— 这就是 `D-3` 要修的**真缺陷**（不是美学问题）⚠️
///
/// 🔴 `#380` 两条**独立通道**（数值**互不加减**）：
///   · **侦察 = 段级**（"能看多远"）⇒ 落 <see cref="Scouted"/>；
///   · **视野 = 格级**（"能看清什么"）⇒ 落 <see cref="Visited"/>。
///   ⇒ 大到足以覆盖侦察过的格时，它是**升级**（`Scouted → Visited`），**不是**"抵消" ✓
///
/// 🔴 **单向**：`Unexplored → Scouted → Visited`；**永不降级**（已 `Visited` 的格再被侦察仍是 `Visited`——
/// 否则"走过去的记忆"会被侦察抹掉）⚠️
/// </summary>
public enum RevealState
{
    /// <summary>暗（从没接触过）✓</summary>
    Unexplored = 0,

    /// <summary>暗 + 亮轮廓（拓扑层侦察揭示，但**没走过**）✓</summary>
    Scouted = 1,

    /// <summary>浅灰（**走过** 或 **格视野照到**）✓</summary>
    Visited = 2,
}

/// <summary>
/// 🔴 **走格者**（位置 / 揭示三态 / 步数 = **单一真值**；表现层只读 ✓）。
/// 用户裁定 (B)：**上下左右一格一格走**（四向）⇒ 本类只做四向移动 ✓
/// ⚠️ **每格遭遇**（用户明说"后续安排、暂留"）⇒ 本类**不做任何遭遇判定**；
///    将来接入时由契约给出字段，届时在"落格"处触发（`Landing`）✓
/// </summary>
public sealed class DungeonWalker
{
    /// <summary>四向顺序**固定**（上/下/左/右）⇒ 寻路与冒烟**确定性** ✓</summary>
    private static readonly (int Dx, int Dy)[] Directions = { (0, -1), (0, 1), (-1, 0), (1, 0) };

    /// <summary>
    /// 🔴🔴 `D-3`：**揭示三态真值**（取代旧的 `_revealed` HashSet）。
    /// **不存在的键 = `Unexplored`**（不预填全图 ⇒ 大图也不白花内存；`StateAt` 统一兜底）✓
    /// </summary>
    private readonly Dictionary<(int X, int Y), RevealState> _state = new();

    /// <summary>
    /// 🔴 `D-1`（2026-09-20）：**已【落过】的格**（不含"只被揭示但没走过"的格）。
    /// 与揭示态的区别：`Visited` 态含"**视野照到**"（没站过），本集合只认"**亲自站过**"——
    /// **回头代价必须按后者判定**（否则"看一眼"就算走过了，代价会莫名其妙地省掉）⚠️
    /// </summary>
    private readonly HashSet<(int X, int Y)> _visited = new();


    public DungeonWalker(DungeonGrid grid, (int X, int Y) start)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        if (!grid.InBounds(start.X, start.Y) || !DungeonTileMap.IsWalkable(grid.TileAt(start.X, start.Y)))
        {
            throw new InvalidOperationException(
                $"起点 ({start.X},{start.Y}) 越界或不可通行（{grid.TileAt(start.X, start.Y)}）⇒ 拒绝开局。");
        }

        if (!DungeonTileMap.IsWalkable(grid.TileAt(start.X, start.Y)))
        {
            throw new InvalidOperationException("起点不可通行 ⇒ 拒绝开局。");
        }

        Position = start;
        _state[start] = RevealState.Visited; // 进格即**看清**（脚下那格当然"内容已知"）✓
        _visited.Add(start);                 // 🔴 D-1：起点算"已站过"（首次走出再回来就应算重走）✓
    }

    public DungeonGrid Grid { get; }

    public (int X, int Y) Position { get; private set; }

    public int StepsTaken { get; private set; }

    /// <summary>
    /// 🔴 `D-3`：该格的**揭示态**（`vision` 为空 ⇒ 退化成"走过/没走过"的二态旧行为）✓
    ///
    /// 🔴 **`vision` 是显式参数，不是内部状态** —— 因为"能看多远"是**关卡/装备**的属性，
    ///    不是走格者的属性（本类构造点在测试里遍布 ⇒ 塞进构造会牵动十几个调用点，
    ///    且会让"同一张图不同光照下视野不同"这类设计**无处安放**）✓
    ///
    /// 🔴 **只读，零副作用**：视野只**回答**"看不看得见"，**不改** `Visited` 集合、不计步、不扣光 ⚠️
    /// （`D-1` 的回头代价按**站过**判定 ⇒ 若"看见"写进集合，第一次走到那儿就会被当成回头多扣光）✓
    /// </summary>
    public RevealState StateAt((int X, int Y) pos, DungeonGridVision? vision = null)
    {
        if (IsWallOrOutOfBounds(pos))
        {
            return RevealState.Unexplored; // 🔴 墙/越界**永不揭示**（DD：迷宫只画可走格；也别让玩家"透视")✓
        }

        bool inVision = vision is { RevealOnEnter: true, Radius: > 0 } && InVisionRange(pos, vision.Radius);

        if (_state.TryGetValue(pos, out RevealState s))
        {
            // 🔴 已 `Visited` ⇒ 最高态，视野**不能**改变它（也不许降级）✓
            if (s == RevealState.Visited)
            {
                return RevealState.Visited;
            }

            // 🔴 已 `Scouted` + 现在**在视野内** ⇒ **升为 `Visited`**（看清了）——
            //    这是"两条通道交汇"的正确语义：侦察说"那边有东西"，视野说"我看清了" ⇒ 取高者 ✓
            //    ⚠️ 只升不降：视野离开后**仍是** `Scouted`（"记忆"保留，不退回未知）✓
            return inVision ? RevealState.Visited : s;
        }

        return inVision
            ? RevealState.Visited // 🔴 格级视野：**看清了**（不是"轮廓"）✓
            : RevealState.Unexplored;
    }

    /// <summary>已揭示格（表现层画"雾"用；顺序无意义）✓</summary>
    public IReadOnlyCollection<(int X, int Y)> Revealed => _state.Keys;

    /// <summary>🔴 `D-1`：**已站过**的格（"见过" ≠ "走过"；回头代价按本集合判定）✓</summary>
    public IReadOnlyCollection<(int X, int Y)> Visited => _visited;

    /// <summary>🔴 `D-1`：该格**是否已经站过**（回头代价的判定口）✓</summary>
    public bool HasVisited((int X, int Y) pos) => _visited.Contains(pos);

    /// <summary>🔴 `D-3` 读数：**只被侦察到、还没走过**的格数（验收断言 / 留档用）✓</summary>
    public int ScoutedCount => _state.Count(kv => kv.Value == RevealState.Scouted);

    /// <summary>🔴 `D-3` 读数：**已看清内容**（走过 或 视野照到）的格数 —— 只统计已记录的，不含"视野临时可见"✓</summary>
    public int VisitedTileCount => _visited.Count;

    /// <summary>
    /// 🔴 `D-3`：**拓扑层侦察揭示**（段级）—— 把一批格标成 <see cref="Scouted"/>（**暗 + 亮轮廓**）✓
    ///
    /// 🔴 **单向升级**：已 `Visited` 的格**保持不变**（侦察**不得**抹掉"走过去的记忆"）⚠️
    /// 🔴 **不写 `Visited` 集合**：侦察 ≠ 站过 ⇒ `D-1` 回头代价不受影响（这是最容易搞错的接线）⚠️
    /// 🔴 墙/越界**静默跳过**（侦察照到墙没有意义，不是错误）✓
    /// </summary>
    public void RevealScouted(IEnumerable<(int X, int Y)> tiles)
    {
        if (tiles is null)
        {
            return;
        }

        foreach ((int X, int Y) pos in tiles)
        {
            if (IsWallOrOutOfBounds(pos))
            {
                continue;
            }

            if (_state.TryGetValue(pos, out RevealState s) && s >= RevealState.Scouted)
            {
                continue; // 已是 Scouted/Visited ⇒ 不动（**永不降级**）✓
            }

            _state[pos] = RevealState.Scouted;
        }
    }

    /// <summary>🔴 `D-3`：曼哈顿距离 ≤ `radius`、且**中间没有墙挡住**（DD：视野不穿墙）✓</summary>
    private bool InVisionRange((int X, int Y) pos, int radius)
    {
        int dist = Math.Abs(pos.X - Position.X) + Math.Abs(pos.Y - Position.Y);
        if (dist > radius)
        {
            return false;
        }

        return HasLineOfSight(pos);
    }

    /// <summary>
    /// 🔴 `D-3`：**视线判定**（DD 口径：视野**不穿墙**）。
    /// 用"四向步进的 L 形"两段代替真正的 Bresenham —— 因为我们本来就是**四向网格**，
    /// 且这样**确定**（同输入同结果）、**便宜**、与 `PathTo` 的取向一致 ✓
    /// ⚠️ 判据：沿着"先 x 后 y"的格子路径，任一中间格是墙 ⇒ **看不见** ✓
    /// </summary>
    private bool HasLineOfSight((int X, int Y) pos)
    {
        int x = Position.X;
        int y = Position.Y;
        int sx = Math.Sign(pos.X - x);
        int sy = Math.Sign(pos.Y - y);

        while (x != pos.X)
        {
            x += sx;
            if ((x != pos.X || y != pos.Y) && !DungeonTileMap.IsWalkable(Grid.TileAt(x, y)))
            {
                return false;
            }
        }

        while (y != pos.Y)
        {
            y += sy;
            if ((x != pos.X || y != pos.Y) && !DungeonTileMap.IsWalkable(Grid.TileAt(x, y)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>墙/越界 ⇒ 永不揭示（单一判定口，避免各处重复写同一条件）✓</summary>
    private bool IsWallOrOutOfBounds((int X, int Y) pos)
        => !Grid.InBounds(pos.X, pos.Y) || !DungeonTileMap.IsWalkable(Grid.TileAt(pos.X, pos.Y));

    public bool HasReachedGoal => Position == Grid.Goal;

    /// <summary>落格内容（表现层/流程据此切模式：战斗格 ⇒ 起战斗；Curio 格 ⇒ 开面板）✓</summary>
    public DungeonTileKind CurrentTile => Grid.TileAt(Position.X, Position.Y);

    /// <summary>
    /// 🔴 四向走一格：墙/越界 ⇒ **返回 false 且什么都不会变**（不扣步、不揭示、不触发）✓
    ///
    /// `D-1`（2026-09-20）：🔴 **走之前先判"是不是重走"** —— 走完再判会把"刚站的这一格"也算进去 ⚠️。
    /// 判定结果由 `out bool wasRevisit` 交给调用方（**代价在调用方结算**：本类只管位置，不管光照）✓
    /// </summary>
    public bool TryStep(int dx, int dy) => TryStep(dx, dy, out _);

    /// <summary>🔴 `D-1` 带"是否重走"输出的走法（语义同上；代价仍由调用方结算）✓</summary>
    public bool TryStep(int dx, int dy, out bool wasRevisit)
    {
        wasRevisit = false;
        int nx = Position.X + dx;
        int ny = Position.Y + dy;
        if (!Grid.InBounds(nx, ny) || !DungeonTileMap.IsWalkable(Grid.TileAt(nx, ny)))
        {
            return false;
        }

        wasRevisit = _visited.Contains((nx, ny)); // 🔴 必须在移动**之前**判 ✓
        Position = (nx, ny);
        StepsTaken++;
        // 🔴 `D-3`：走到 ⇒ **看清**（`Visited`）——**单向升级**：即使先前是 `Scouted` 也升上来；
        //    已经 `Visited` 的格再走一次仍是 `Visited`（不降级）✓
        _state[Position] = RevealState.Visited;
        _visited.Add(Position);
        return true;
    }

    /// <summary>
    /// 🔴 **到目标格的路径**（表现层"点远处 ⇒ 自动走"用）：BFS，四向顺序固定 ⇒ **确定性** ✓
    /// 返回**不含起点、含终点**的格序列；不可达 ⇒ 空 ✓
    /// </summary>
    public IReadOnlyList<(int X, int Y)> PathTo(int gx, int gy)
    {
        if (!Grid.InBounds(gx, gy) || !DungeonTileMap.IsWalkable(Grid.TileAt(gx, gy)))
        {
            return Array.Empty<(int X, int Y)>();
        }

        var prev = new Dictionary<(int X, int Y), (int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(Position);
        prev[Position] = Position;
        while (queue.Count > 0)
        {
            (int X, int Y) cur = queue.Dequeue();
            if (cur == (gx, gy))
            {
                break;
            }

            foreach ((int dx, int dy) in Directions)
            {
                var next = (cur.X + dx, cur.Y + dy);
                if (!Grid.InBounds(next.Item1, next.Item2)
                    || !DungeonTileMap.IsWalkable(Grid.TileAt(next.Item1, next.Item2))
                    || prev.ContainsKey(next))
                {
                    continue;
                }

                prev[next] = cur;
                queue.Enqueue(next);
            }
        }

        var target = (X: gx, Y: gy);
        if (!prev.ContainsKey(target))
        {
            return Array.Empty<(int X, int Y)>();
        }

        var path = new List<(int X, int Y)>();
        for (var step = target; step != Position; step = prev[step])
        {
            path.Add(step);
        }

        path.Reverse();
        return path;
    }
}
