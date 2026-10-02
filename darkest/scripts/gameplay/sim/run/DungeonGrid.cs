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

    /// <summary>🔴 陷阱（策划 `#338`③）：**本次只留枚举位**，行为/触发规则**暂留**（与"每格遭遇"同类）——
    /// 当前**可通行且无任何效果**；表现层/流程**不得**据此假设"有陷阱效果" ✓</summary>
    Trap,

    /// <summary>
    /// 🔴🔴 **`D-6` 隐藏房（`Secret`，2026-09-20）** —— DD wiki 「Dungeon Map」④ 的第六类格内容。
    ///
    /// <para>**DD 口径**：「隐藏房」在**地图上完全不显示**（不是"暗"，是**不画**）——
    /// 只有**侦察成功**才能把它变成一张**可进的 rewards 房**。这是给侦察的**非信息类回报**：
    /// 三态揭示（`D-3`）给的是"信息"，`D-4` 陷阱给的是"避免损失"，
    /// 而隐藏房给的是**实打实的收益**（战利品 / 资源）⇒ 侦察从此有"值钱"的理由 ✓</para>
    ///
    /// <para>🔴 **与 `Trap` 的关键差别**：`Trap` 可通行但**不可见**（未侦察照样触发）；
    /// `Secret` 在**被揭示之前**对玩家**根本不存在**（地图不画、也不该能点进去）⚠️</para>
    ///
    /// <para>🔴 **可通行**（`IsWalkable` 只看墙 ⇒ 本枚举位自动可走）：揭示后它就是一间**房**，
    /// 玩家要能走进去拿东西 —— 若不可走，"揭示后成 rewards 房"就永远进不去（静默失效）⚠️</para>
    /// </summary>
    Secret,
}

/// <summary>
/// 🔴 **瓷砖字符 ⇔ 枚举**（结构性映射 ⇒ 属"枚举到字符的映射"，可进白名单；**不是**可调数字 ✓）。
/// 字符表（与草案 §2.1 一致）：`.` 地板 · `#` 墙 · `+` 门 · `R` 房间 · `C` 走廊 ·
/// `?` Curio · `!` 战斗 · `E` 事件 · `A` 营地 · `G` 终点 · `^` 陷阱 · `*` 隐藏房 ✓
/// 🔴 **登记纪律**（`dungeon_layer_design.md §F3d`）：每加一个字符，**必须同时**登记
///   ① 本类的 `FromChar`/`ToChar` 往返 ② `DungeonGridConfig.Parse` ② 的字符集校验
///   —— 后者用 `ToChar(FromChar(c)) != c` 判定 ⇒ 漏登记**会被自动拦**（既有护栏，别绕过）✓
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
        '^' => DungeonTileKind.Trap, // 🔴 陷阱（规则暂留）✓
        '*' => DungeonTileKind.Secret, // 🔴🔴 `D-6` 隐藏房（地图不显示、靠侦察揭示）✓
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
        DungeonTileKind.Trap => '^',
        DungeonTileKind.Secret => '*',
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
