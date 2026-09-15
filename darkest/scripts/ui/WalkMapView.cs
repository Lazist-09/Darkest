using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Darkest.Ui;

/// <summary>🔴 行走层【渲染快照】的一格（表现层自己的输入格式，**不含任何内核类型**）✓</summary>
public sealed record SketchCell(int Id, int Depth, int Lane, string Type, bool Revealed, bool IsCurrent, bool IsGoal, bool Movable);

/// <summary>🔴 行走层【渲染快照】的一条连线（两端格子 id）✓</summary>
public sealed record SketchLink(int From, int To);

/// <summary>
/// 🔴 行走层【渲染快照】—— **表现层与内核解耦的接缝**。
/// 用户已裁定 **(B) 瓷砖网格**（`doc/architecture/tasks/dungeon_grid.md`）：迁移 §5 的 **③ 派生网格**
/// 落地时，"旧【房间+连线】图页退役" ⇒ 🔴 **那时只写一个新的 `From…` 适配器，本视图不动** ✓
/// （§3 接缝：规则/内容/光照/HP 结转/同场景战斗全不动，换的只是"行走层的表示"）✓
/// </summary>
public sealed record MapSketch(
    IReadOnlyList<SketchCell> Cells,
    IReadOnlyList<SketchLink> Links,
    int RemainingSegments,
    string CurrentType);

/// <summary>
/// 🔴 `#327` 层④：**DD 式示意地图**（用户要求："**大方块=房间、小方块=走廊**，就那么简单"）。
///
/// 布局（确定性、只读）：**x = 深度（一列一层）**、**y = 同深度内的序号**（按 Id 升序）✓
/// · **房间** = 大方块（14×14）· **走廊** = 两房之间的小方块（5×5，沿连线铺 1~3 个）✓
/// · 配色：**当前=强调金** · 已揭示=正文色 · 未揭示=压暗 · **终点=危险红**（一眼看出目标在哪）✓
/// · 🔴 数据只经 **`MapSketch`**（表现层输入格式）⇒ 内核换表示时不牵连本类 ✓
/// · **本类不推断任何规则**（谁可达、走了几步都由内核答；`Movable` 由宿主从内核读数填）✓
/// ⚠️ 方块一律 `ColorRect`（**不是** `Panel`/`PanelContainer`）⇒ 不参与"框必须不透明"判据，也不会互相算作模态 ✓
/// </summary>
public partial class WalkMapView : PanelContainer
{
    private const int RoomSize = 14;   // 大方块
    private const int CorridorSize = 5; // 小方块
    private const int StepX = 34;      // 列间距（depth 方向）
    private const int StepY = 26;      // 行间距（同深度内）
    private const int Pad = 8;

    private Control _canvas = null!;
    private int _lastKey = -1;
    private string _lastSketch = string.Empty;
    private Label? _info;   // 🔴 (A)：格子视图顶部信息（当前类型/剩余段数/已揭示）✓

    /// <summary>🔴 主程序 (A)：**点相邻房间 ⇒ 移动一格** 的回调 ✓</summary>
    public Action<int>? OnRoomClicked;

    /// <summary>当前**可移动**的格子 id（宿主从内核读数填；空 ⇒ 全部不可点）✓</summary>
    public IReadOnlyList<int> MovableRooms = System.Array.Empty<int>();

    /// <summary>最近一次文字速写（供日志自证；headless 看不到画面 ⇒ 用文字证明布局与标记 ✓）</summary>
    public string LastSketch => _lastSketch;

    public override void _Ready()
    {
        _canvas = new Control { Name = "WalkMapCanvas" };

        // 🔴 主程序 (A)：**把"当前房间类型 + 剩余段数"画在格子上方**（提示不该只活在 log 里）✓
        _info = new Label { Name = "WalkInfo", VerticalAlignment = VerticalAlignment.Center };
        _info.AddThemeFontSizeOverride("font_size", Darkest.Ui.DdTheme.FontSmall);
        _info.AutowrapMode = TextServer.AutowrapMode.Off;
        _info.ClipText = true;
        _info.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _info.CustomMinimumSize = new Vector2(0, 18);
        AddChild(_info);

        // 🔴 本视图子项是**手工定位** ⇒ 信息行与画布必须错开，否则相压（实测 1 对重叠）✓
        _info.Position = new Vector2(0, 0);
        _info.Size = new Vector2(240, 18);
        _canvas.Position = new Vector2(0, 18);

        AddChild(_canvas);
    }

    /// <summary>
    /// 🔴 **旧签名 = 适配器**（零行为变化）：把内核"房间+连线"图翻成 `MapSketch` 后交给新入口 ✓
    /// （(B) ③ 落地后，本签名与其调用点一并退役 —— 那时写 `FromDungeonGrid(...)` 即可）✓
    /// </summary>
    public void Refresh(
        Darkest.Gameplay.Sim.Run.ExpeditionMap map,
        int currentRoomId,
        IReadOnlyList<int> revealed,
        IReadOnlyList<int>? movable = null,
        int remainingSegments = -1,
        string? currentType = null)
        => Refresh(FromExpeditionMap(map, currentRoomId, revealed, movable ?? System.Array.Empty<int>(), remainingSegments, currentType));

    /// <summary>🔴 适配器：内核"房间+连线"图 ⇒ 表现层快照（**唯一读内核类型的地方**）✓</summary>
    public static MapSketch FromExpeditionMap(
        Darkest.Gameplay.Sim.Run.ExpeditionMap map,
        int currentRoomId,
        IReadOnlyList<int> revealed,
        IReadOnlyList<int> movable,
        int remainingSegments,
        string? currentType)
    {
        var revealedSet = new HashSet<int>(revealed);
        var cells = new List<SketchCell>();
        foreach (IGrouping<int, Darkest.Gameplay.Sim.Run.MapRoom> col in map.Rooms.GroupBy(r => r.Depth).OrderBy(g => g.Key))
        {
            int lane = 0;
            foreach (Darkest.Gameplay.Sim.Run.MapRoom room in col.OrderBy(r => r.Id))
            {
                cells.Add(new SketchCell(
                    room.Id,
                    room.Depth,
                    lane,
                    room.Type ?? string.Empty,
                    revealedSet.Contains(room.Id),
                    room.Id == currentRoomId,
                    room.Id == map.GoalId,
                    movable.Contains(room.Id)));
                lane++;
            }
        }

        var links = map.Edges.Select(e => new SketchLink(e.From, e.To)).ToList();
        return new MapSketch(cells, links, remainingSegments, currentType ?? string.Empty);
    }

    /// <summary>重画（**只吃 `MapSketch`** ⇒ 与内核表示解耦）✓</summary>
    public void Refresh(MapSketch sketch)
    {
        int currentId = sketch.Cells.FirstOrDefault(c => c.IsCurrent)?.Id ?? -1;
        int goalId = sketch.Cells.FirstOrDefault(c => c.IsGoal)?.Id ?? -1;

        // 只在"画的东西会变"时重画（格子/连线/揭示/当前/终点任一变化）——避免每帧重建 ✓
        int key = HashCode.Combine(sketch.Cells.Count, sketch.Links.Count, sketch.Cells.Count(c => c.Revealed), currentId, goalId);
        if (key == _lastKey && _canvas.GetChildCount() > 0)
        {
            return;
        }

        _lastKey = key;

        foreach (Node child in _canvas.GetChildren())
        {
            _canvas.RemoveChild(child);
            child.QueueFree();
        }

        // 摆位：x = 深度，y = 同深度内序号（由适配器给定 ⇒ 确定性）
        var pos = new Dictionary<int, (int X, int Y)>();
        foreach (SketchCell c in sketch.Cells)
        {
            pos[c.Id] = (Pad + c.Depth * StepX, Pad + c.Lane * StepY);
        }

        // ① 走廊 = 小方块（沿两端连线铺；DD 的走廊就是这种小方块串）
        foreach (SketchLink link in sketch.Links)
        {
            if (!pos.TryGetValue(link.From, out (int X, int Y) a) || !pos.TryGetValue(link.To, out (int X, int Y) b))
            {
                continue;
            }

            // 只在**两端至少一端已揭示**时画（未探索区域不剧透）✓
            bool fromSeen = sketch.Cells.FirstOrDefault(c => c.Id == link.From)?.Revealed ?? false;
            bool toSeen = sketch.Cells.FirstOrDefault(c => c.Id == link.To)?.Revealed ?? false;
            if (!fromSeen && !toSeen)
            {
                continue;
            }

            int corrCount = Math.Max(1, Math.Abs(b.Y - a.Y) > 0 ? Math.Abs(b.Y - a.Y) / StepY : 1);
            for (int i = 1; i <= corrCount; i++)
            {
                float t = i / (float)(corrCount + 1);
                int cx = (int)(a.X + (b.X - a.X) * t) + (RoomSize - CorridorSize) / 2;
                int cy = (int)(a.Y + (b.Y - a.Y) * t) + (RoomSize - CorridorSize) / 2;
                _canvas.AddChild(new ColorRect
                {
                    Name = $"Corridor_{link.From}_{link.To}_{i}",
                    Color = Darkest.Ui.DdTheme.MapEdge,
                    Position = new Vector2(cx, cy),
                    Size = new Vector2(CorridorSize, CorridorSize),
                });
            }
        }

        // ② 房间 = 大方块（未揭示 ⇒ 只画"未知"色；DD 里没走过的房间就是这样）
        int maxX = 0;
        int maxY = 0;
        foreach (SketchCell c in sketch.Cells)
        {
            (int X, int Y) p = pos[c.Id];
            Color color = c.IsCurrent ? Darkest.Ui.DdTheme.Highlight
                : c.IsGoal ? Darkest.Ui.DdTheme.Danger
                : !c.Revealed ? Darkest.Ui.DdTheme.MapUnknown
                : Darkest.Ui.DdTheme.MapVisited;

            _canvas.AddChild(new ColorRect
            {
                Name = $"Room_{c.Id}",
                Color = color,
                Position = new Vector2(p.X, p.Y),
                Size = new Vector2(RoomSize, RoomSize),
            });

            // 🔴 主程序 (A)：格子上叠**可点热区**（相邻未探索才可点 ⇒ 点击走一格）✓
            //    视觉仍是 `ColorRect`（不参与"框必须不透明"判据）；热区是扁平透明 Button ✓
            var hit = new Button
            {
                Name = $"RoomHit_{c.Id}",
                Position = new Vector2(p.X, p.Y),
                Size = new Vector2(RoomSize, RoomSize),
                Flat = true,
                Disabled = !c.Movable,
                TooltipText = $"房间 {c.Id}（{(string.IsNullOrEmpty(c.Type) ? "?" : c.Type)}）" + (c.Movable ? "　点击 ⇒ 走一格" : "　（不可达）"),
            };
            int rid = c.Id;
            hit.Pressed += () => OnRoomClicked?.Invoke(rid);
            _canvas.AddChild(hit);

            maxX = Math.Max(maxX, p.X + RoomSize);
            maxY = Math.Max(maxY, p.Y + RoomSize);
        }

        // 🔴 相机 1280/720 口径（规则①）：**画布不得用自身尺寸撑父容器**（超出部分由画布裁剪）✓
        _canvas.CustomMinimumSize = new Vector2(System.Math.Min(maxX + Pad, 260), System.Math.Min(maxY + Pad, 150));
        CustomMinimumSize = new Vector2(0, 0);

        if (_info is not null)
        {
            _info.Text = $"当前：{(string.IsNullOrEmpty(sketch.CurrentType) ? "?" : sketch.CurrentType)}" +
                         (sketch.RemainingSegments >= 0 ? $"　剩余 {sketch.RemainingSegments} 段" : string.Empty) +
                         $"　已揭示 {sketch.Cells.Count(x => x.Revealed)}/{sketch.Cells.Count}";
        }

        _lastSketch = Sketch(sketch, pos);
    }

    /// <summary>
    /// 文字速写（**布局自证**）：每行 = 一个 lane、每列 = 一个 depth。
    /// `■`=当前 `◆`=终点 `□`=已揭示 `·`=未揭示。
    /// ⚠️ **只画房间**：走廊在这里**不画**（它在画面上是两房之间的小方块，共 N 条，见行首计数）——
    ///    图例不得承诺没画的东西（我自己立的规矩：读数与事实必须一致）✓
    /// </summary>
    private static string Sketch(MapSketch sketch, Dictionary<int, (int X, int Y)> pos)
    {
        int lanes = pos.Values.Select(v => v.Y).DefaultIfEmpty(0).Max() / StepY + 1;
        int depths = pos.Values.Select(v => v.X).DefaultIfEmpty(0).Max() / StepX + 1;
        var grid = new char[lanes, depths * 2];
        for (int y = 0; y < lanes; y++)
        {
            for (int x = 0; x < depths * 2; x++)
            {
                grid[y, x] = ' ';
            }
        }

        foreach (SketchCell c in sketch.Cells)
        {
            (int X, int Y) p = pos[c.Id];
            char ch = c.IsCurrent ? '■' : c.IsGoal ? '◆' : c.Revealed ? '□' : '·';
            grid[p.Y / StepY, (p.X / StepX) * 2] = ch;
        }

        var sb = new StringBuilder();
        sb.Append($"房间方块 {sketch.Cells.Count} 个／走廊小方块 {sketch.Links.Count} 条（x=深度 · y=同深度内序号）");
        sb.Append("　图例：■当前 ◆终点 □已揭示 ·未揭示（**只画房间**；走廊在画面上是两房之间的小方块）");
        for (int y = 0; y < lanes; y++)
        {
            sb.Append('\n');
            for (int x = 0; x < depths * 2; x++)
            {
                sb.Append(grid[y, x]);
            }
        }

        return sb.ToString();
    }
}
