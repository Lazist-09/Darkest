using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>🔴 **地牢瓷砖种类**（用户 2026-09-15 裁定 (B)：瓷砖网格自由走）✓</summary>
public enum DungeonTileKind
{
    Floor,
    Wall,
    Door,
    Room,
    Corridor,
    Curio,
    Battle,
    Event,
    Camp,
    Goal,
}

/// <summary>
/// 🔴 **瓷砖字符 ⇔ 枚举**（结构性映射 ⇒ 属"枚举到字符的映射"，可进白名单；**不是**可调数字 ✓）。
/// 字符表（与草案 §2.1 一致）：`.` 地板 · `#` 墙 · `+` 门 · `R` 房间 · `C` 走廊 ·
/// `?` Curio · `!` 战斗 · `E` 事件 · `A` 营地 · `G` 终点 ✓
/// </summary>
public static class DungeonTileMap
{
    public static DungeonTileKind FromChar(char c) => c switch
    {
        '.' => DungeonTileKind.Floor,
        '+' => DungeonTileKind.Door,
        'R' => DungeonTileKind.Room,
        'C' => DungeonTileKind.Corridor,
        '?' => DungeonTileKind.Curio,
        '!' => DungeonTileKind.Battle,
        'E' => DungeonTileKind.Event,
        'A' => DungeonTileKind.Camp,
        'G' => DungeonTileKind.Goal,
        _ => DungeonTileKind.Wall, // 未登记字符 ⇒ **当墙**（不可通行；不静默当成地板 ✓）
    };

    public static char ToChar(DungeonTileKind kind) => kind switch
    {
        DungeonTileKind.Floor => '.',
        DungeonTileKind.Wall => '#',
        DungeonTileKind.Door => '+',
        DungeonTileKind.Room => 'R',
        DungeonTileKind.Corridor => 'C',
        DungeonTileKind.Curio => '?',
        DungeonTileKind.Battle => '!',
        DungeonTileKind.Event => 'E',
        DungeonTileKind.Camp => 'A',
        DungeonTileKind.Goal => 'G',
        _ => '#',
    };

    /// <summary>可通行？（墙不可走；其余可走 ✓）</summary>
    public static bool IsWalkable(DungeonTileKind kind) => kind != DungeonTileKind.Wall;
}

/// <summary>
/// 🔴 **不可变的瓷砖网格**（用户裁定 (B) 的规则模型；**零 Godot 依赖** ✓）。
/// 本类**不含**遭遇/视野/光照的规则 —— 那些按用户要求"**暂留**"，等契约（草案 §6）✓
/// </summary>
public sealed class DungeonGrid
{
    private readonly DungeonTileKind[] _tiles;

    private DungeonGrid(int width, int height, DungeonTileKind[] tiles, (int X, int Y) goal)
    {
        Width = width;
        Height = height;
        Goal = goal;
        _tiles = tiles;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>终点格（`G`；解析期**必须恰有一处** ✓）</summary>
    public (int X, int Y) Goal { get; }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public DungeonTileKind TileAt(int x, int y)
        => InBounds(x, y) ? _tiles[(y * Width) + x] : DungeonTileKind.Wall; // 越界 ⇒ 当墙（不可走；不抛 ✓）

    /// <summary>
    /// 🔴 解析（**加载期校验，缺一不可**）：
    /// ① 行数 = 高、每行等长（否则网格错位 ⇒ 一律拒绝）
    /// ② 恰有一处 `G`（终点唯一；0 处或多处 ⇒ 拒绝）
    /// ③ `start` 由调用方给出且必须可通行（起点不能是墙）
    /// ⚠️ **遭遇/视野数值不在此校验** —— 它们是"暂留项"，契约未定前**不许**有人填默认值 ✓
    /// </summary>
    public static DungeonGrid Parse(string resPath, IReadOnlyList<string> rows)
    {
        if (rows is null || rows.Count == 0)
        {
            throw new InvalidDataException($"{resPath}: 瓷砖图为空。");
        }

        int width = rows[0].Length;
        if (width == 0)
        {
            throw new InvalidDataException($"{resPath}: 首行为空。");
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Length != width)
            {
                throw new InvalidDataException(
                    $"{resPath}: 第 {i + 1} 行长度 {rows[i].Length} ≠ 首行 {width}（瓷砖图必须等长，否则网格错位）。");
            }
        }

        int height = rows.Count;
        var tiles = new DungeonTileKind[width * height];
        var goals = new List<(int X, int Y)>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                DungeonTileKind kind = DungeonTileMap.FromChar(rows[y][x]);
                tiles[(y * width) + x] = kind;
                if (kind == DungeonTileKind.Goal)
                {
                    goals.Add((x, y));
                }
            }
        }

        if (goals.Count != 1)
        {
            throw new InvalidDataException($"{resPath}: 终点 `G` 必须恰有 1 处（实测 {goals.Count} 处）⇒ 拒绝加载。");
        }

        return new DungeonGrid(width, height, tiles, goals[0]);
    }

    /// <summary>调试/留档：把网格还原成字符行 ✓</summary>
    public IReadOnlyList<string> ToRows()
    {
        var rows = new List<string>(Height);
        for (int y = 0; y < Height; y++)
        {
            var chars = new char[Width];
            for (int x = 0; x < Width; x++)
            {
                chars[x] = DungeonTileMap.ToChar(TileAt(x, y));
            }

            rows.Add(new string(chars));
        }

        return rows;
    }
}

/// <summary>
/// 🔴 **走格者**（位置 / 已揭示 / 步数 = **单一真值**；表现层只读 ✓）。
/// 用户裁定 (B)：**上下左右一格一格走**（四向）⇒ 本类只做四向移动 ✓
/// ⚠️ **每格遭遇**（用户明说"后续安排、暂留"）⇒ 本类**不做任何遭遇判定**；
///    将来接入时由契约给出字段，届时在"落格"处触发（`Landing`）✓
/// </summary>
public sealed class DungeonWalker
{
    /// <summary>四向顺序**固定**（上/下/左/右）⇒ 寻路与冒烟**确定性** ✓</summary>
    private static readonly (int Dx, int Dy)[] Directions = { (0, -1), (0, 1), (-1, 0), (1, 0) };

    private readonly HashSet<(int X, int Y)> _revealed = new();

    public DungeonWalker(DungeonGrid grid, (int X, int Y) start)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        if (!grid.InBounds(start.X, start.Y) || !DungeonTileMap.IsWalkable(grid.TileAt(start.X, start.Y)))
        {
            throw new InvalidOperationException(
                $"起点 ({start.X},{start.Y}) 越界或不可通行（{grid.TileAt(start.X, start.Y)}）⇒ 拒绝开局。");
        }

        Position = start;
        _revealed.Add(start); // 进格即揭示（视野规则**暂留** ⇒ 这里只记"走过/见过" ✓）
    }

    public DungeonGrid Grid { get; }

    public (int X, int Y) Position { get; private set; }

    public int StepsTaken { get; private set; }

    /// <summary>已揭示格（表现层画"雾"用；顺序无意义）✓</summary>
    public IReadOnlyCollection<(int X, int Y)> Revealed => _revealed;

    public bool HasReachedGoal => Position == Grid.Goal;

    /// <summary>落格内容（表现层/流程据此切模式：战斗格 ⇒ 起战斗；Curio 格 ⇒ 开面板）✓</summary>
    public DungeonTileKind CurrentTile => Grid.TileAt(Position.X, Position.Y);

    /// <summary>
    /// 🔴 四向走一格：墙/越界 ⇒ **返回 false 且什么都不会变**（不扣步、不揭示、不触发）✓
    /// </summary>
    public bool TryStep(int dx, int dy)
    {
        int nx = Position.X + dx;
        int ny = Position.Y + dy;
        if (!Grid.InBounds(nx, ny) || !DungeonTileMap.IsWalkable(Grid.TileAt(nx, ny)))
        {
            return false;
        }

        Position = (nx, ny);
        StepsTaken++;
        _revealed.Add(Position);
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
