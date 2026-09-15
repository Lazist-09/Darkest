using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 `#327` 层④：**DD 式示意地图**（用户要求："**大方块=房间、小方块=走廊**，就那么简单"）。
///
/// 布局（确定性、只读）：**x = `MapRoom.Depth`（一列一层）**、**y = 同深度内的序号**（按 Id 升序）✓
/// · **房间** = 大方块（14×14）· **走廊** = 两房之间的小方块（6×6，沿连线铺 1~3 个）✓
/// · 配色：**当前=强调金** · 已揭示=正文色 · 未揭示=压暗 · **终点=危险红**（一眼看出目标在哪）✓
/// · 🔴 数据**全部来自内核只读读数**：`Map.Rooms` / `.Edges` / `.GoalId` / `RevealedRoomIds` / `CurrentRoomId`；
///   本类**不推断任何规则**（谁可达、走了几步都由内核答）✓
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

    /// <summary>当前**可移动**的房间 id（= low.AdjacentUnexplored()）✓</summary>
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

        // 🔴 修重叠：本视图子项是**手工定位**（不是容器）⇒ 信息行与画布必须**错开**，否则相压（实测 1 对重叠）✓
        _info.Position = new Vector2(0, 0);
        _info.Size = new Vector2(240, 18);
        _canvas.Position = new Vector2(0, 18);

        AddChild(_canvas);
    }

    /// <summary>重画（数据全来自内核；`revealed` 是"已揭示"集合）✓</summary>
    public void Refresh(Darkest.Gameplay.Sim.Run.ExpeditionMap map, int currentRoomId, IReadOnlyList<int> revealed, IReadOnlyList<int>? movable = null, int remainingSegments = -1, string? currentType = null)
    {
        // 只在"画的东西会变"时重画（房间/揭示/当前/终点任一变化）——避免每帧重建 ✓
        int key = HashCode.Combine(map.RoomCount, revealed.Count, currentRoomId, map.GoalId, _lastKey == -1 ? 0 : 1);
        key = HashCode.Combine(map.RoomCount, revealed.Count, currentRoomId, map.GoalId);
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

        // 摆位：x = Depth，y = 同深度内序号（按 Id 升序 ⇒ 确定性）
        var pos = new Dictionary<int, (int X, int Y)>();
        foreach (IGrouping<int, Darkest.Gameplay.Sim.Run.MapRoom> col in map.Rooms.GroupBy(r => r.Depth).OrderBy(g => g.Key))
        {
            int lane = 0;
            foreach (Darkest.Gameplay.Sim.Run.MapRoom room in col.OrderBy(r => r.Id))
            {
                pos[room.Id] = (Pad + col.Key * StepX, Pad + lane * StepY);
                lane++;
            }
        }

        var revealedSet = new HashSet<int>(revealed);

        // ① 走廊 = 小方块（沿两房连线铺；DD 的走廊就是这种小方块串）
        foreach (Darkest.Gameplay.Sim.Run.MapEdge e in map.Edges)
        {
            if (!pos.TryGetValue(e.From, out (int X, int Y) a) || !pos.TryGetValue(e.To, out (int X, int Y) b))
            {
                continue;
            }

            // 只在**两端至少一端已揭示**时画（未探索区域不剧透）✓
            if (!revealedSet.Contains(e.From) && !revealedSet.Contains(e.To))
            {
                continue;
            }

            int corrCount = Math.Max(1, (int)(Math.Abs(b.Y - a.Y) > 0 ? Math.Abs(b.Y - a.Y) / StepY : 1));
            for (int i = 1; i <= corrCount; i++)
            {
                float t = i / (float)(corrCount + 1);
                int cx = (int)(a.X + (b.X - a.X) * t) + (RoomSize - CorridorSize) / 2;
                int cy = (int)(a.Y + (b.Y - a.Y) * t) + (RoomSize - CorridorSize) / 2;
                _canvas.AddChild(new ColorRect
                {
                    Name = $"Corridor_{e.From}_{e.To}_{i}",
                    Color = Darkest.Ui.DdTheme.MapEdge,
                    Position = new Vector2(cx, cy),
                    Size = new Vector2(CorridorSize, CorridorSize),
                });
            }
        }

        // ② 房间 = 大方块（未揭示 ⇒ 只画"未知"色，不标类型；DD 里没走过的房间就是这样）
        int maxX = 0;
        int maxY = 0;
        foreach (Darkest.Gameplay.Sim.Run.MapRoom room in map.Rooms)
        {
            (int X, int Y) p = pos[room.Id];
            bool seen = revealedSet.Contains(room.Id);
            bool isCurrent = room.Id == currentRoomId;
            bool isGoal = room.Id == map.GoalId;

            Color color = isCurrent ? Darkest.Ui.DdTheme.Highlight
                : isGoal ? Darkest.Ui.DdTheme.Danger
                : !seen ? Darkest.Ui.DdTheme.MapUnknown
                : Darkest.Ui.DdTheme.MapVisited;

            _canvas.AddChild(new ColorRect
            {
                Name = $"Room_{room.Id}",
                Color = color,
                Position = new Vector2(p.X, p.Y),
                Size = new Vector2(RoomSize, RoomSize),
            });

            // 🔴 主程序 (A)：**格子上叠一个可点热区** —— 相邻未探索房间才可点（点击 ⇒ 走一格）✓
            //    视觉仍是 `ColorRect`（不参与"框必须不透明"判据）；热区是扁平透明 Button ✓
            var hit = new Button
            {
                Name = $"RoomHit_{room.Id}",
                Position = new Vector2(p.X, p.Y),
                Size = new Vector2(RoomSize, RoomSize),
                Flat = true,
                Disabled = !(movable ?? System.Array.Empty<int>()).Contains(room.Id),
                TooltipText = $"房间 {room.Id}（{room.Type}）" + ((movable ?? System.Array.Empty<int>()).Contains(room.Id) ? "　点击 ⇒ 走一格" : "　（不可达）"),
            };
            int rid = room.Id;
            hit.Pressed += () => OnRoomClicked?.Invoke(rid);
            _canvas.AddChild(hit);

            maxX = Math.Max(maxX, p.X + RoomSize);
            maxY = Math.Max(maxY, p.Y + RoomSize);
        }

        // 🔴 相机 1280 口径（规则①）：**地图画布不得用自身尺寸撑父容器** ——
        //    实测它（几百像素宽）把战斗底栏整行撑爆 ⇒ 与右侧 6 号位长条框**重叠** ⚠️
        //    ⇒ 画布尺寸**夹在预算内**（超出部分由画布裁剪），本视图自身最小尺寸设为 0 ✓
        _canvas.CustomMinimumSize = new Vector2(System.Math.Min(maxX + Pad, 260), System.Math.Min(maxY + Pad, 150));
        CustomMinimumSize = new Vector2(0, 0);
        if (_info is not null)
        {
            _info.Text = $"当前：{(string.IsNullOrEmpty(currentType) ? "?" : currentType)}" +
                         (remainingSegments >= 0 ? $"　剩余 {remainingSegments} 段" : string.Empty) +
                         $"　已揭示 {revealedSet.Count}/{map.Rooms.Count}";
        }

        _lastSketch = Sketch(map, currentRoomId, revealedSet, pos);
    }

    /// <summary>
    /// 文字速写（**布局自证**）：每行 = 一个 lane、每列 = 一个 depth。
    /// `■`=当前 `◆`=终点 `□`=已揭示 `·`=未揭示。
    /// ⚠️ **只画房间**：走廊在这里**不画**（它在画面上是两房之间的小方块，共 N 条，见行首计数）——
    ///    图例不得承诺没画的东西（我自己立的规矩：读数与事实必须一致）✓
    /// </summary>
    private static string Sketch(
        Darkest.Gameplay.Sim.Run.ExpeditionMap map, int currentRoomId, HashSet<int> revealed,
        Dictionary<int, (int X, int Y)> pos)
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

        foreach (Darkest.Gameplay.Sim.Run.MapRoom room in map.Rooms)
        {
            (int X, int Y) p = pos[room.Id];
            char ch = room.Id == currentRoomId ? '■' : room.Id == map.GoalId ? '◆' : revealed.Contains(room.Id) ? '□' : '·';
            grid[p.Y / StepY, (p.X / StepX) * 2] = ch;
        }

        var sb = new StringBuilder();
        sb.Append($"房间方块 {map.RoomCount} 个／走廊小方块 {map.Edges.Count} 条（x=深度 · y=同深度内序号）");
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
