using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 行走层【渲染快照】的一格（表现层自己的输入格式，**不含任何内核类型**）✓
///
/// 🔴🔴 `D-3`（2026-09-20）：**`Revealed` 是二态，画不出 DD 的三态雾** ⇒ 新增 <see cref="State"/>。
///   · `State` = `Unexplored`（暗）/ `Scouted`（暗 + 亮轮廓）/ `Visited`（浅灰）—— **三态真值** ✓
///   · `Revealed` = `State != Unexplored` 的**便捷读法**（保留：它已有 6 个消费点，且语义没变）✓
///   ⚠️ 用 `RevealState`（内核**枚举**）而不是另一套 UI 枚举 —— 这是**纯值类型**、零 Godot、零行为，
///      在表现层复制一份只会有两套真值（纪律 `#325` D6）✓
/// </summary>
public sealed record SketchCell(
    int Id,
    int Depth,
    int Lane,
    string Type,
    bool Revealed,
    bool IsCurrent,
    bool IsGoal,
    bool Movable,
    Darkest.Gameplay.Sim.Run.RevealState State = Darkest.Gameplay.Sim.Run.RevealState.Unexplored);

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
    string CurrentType,
    (int X, int Y) CurrentTile = default);

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
    private Darkest.UI.WalkMapSkeleton? _walkSkel;   // 🔴 E（2026-09-17）：瓷砖渲染骨架（双层：房间 14px／走廊 5px）✓
    private int _lastKey = -1;
    private string _lastSketch = string.Empty;
    private bool _dragging;   // 🔴 拖拽平移状态 ✓
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
        _info.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
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

        // 🔴 E（2026-09-17）：**引擎内置瓷砖渲染**（骨架优先；缺失 ⇒ 回落手绘 ColorRect）✓
        _walkSkel = Darkest.UI.WalkMapSkeleton.TryInstantiate();
        if (_walkSkel is not null)
        {
            _canvas.AddChild(_walkSkel);
        }

        // 🔴 用户要求（2026-09-16）：**地图要像 DD 那样在框里拖拽 / 缩放，且绝不超出框** ✓
        //    · `ClipContents` ⇒ 画布超出部分被**裁掉**（不会压到别的框）✓
        //    · 滚轮缩放（0.5~3×）· 左键拖拽平移 ✓
        ClipContents = true;
        _canvas.MouseFilter = Control.MouseFilterEnum.Stop;
        _canvas.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton mb)
            {
                if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp)
                {
                    ZoomCanvas(1.15f);
                }
                else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown)
                {
                    ZoomCanvas(1f / 1.15f);
                }
                else if (mb.ButtonIndex == MouseButton.Left)
                {
                    _dragging = mb.Pressed;
                }
            }
            else if (e is InputEventMouseMotion mm && _dragging)
            {
                _canvas.Position += mm.Relative;
            }
        };
    }

    /// <summary>框内缩放（0.5×~3×）—— 与 `ClipContents` 配合 ⇒ **永不超出框** ✓</summary>
    private void ZoomCanvas(float factor)
    {
        float s = Math.Clamp(_canvas.Scale.X * factor, 0.5f, 3f);
        _canvas.Scale = new Vector2(s, s);
        GD.Print($"[UI 地图·框内缩放] 缩放 = {s:0.00}×（超出部分由框裁剪）✓");
    }

    /// <summary>重画（**只吃 `MapSketch`** ⇒ 与内核表示解耦）✓</summary>
    public void Refresh(MapSketch sketch)
    {
        int currentId = sketch.Cells.FirstOrDefault(c => c.IsCurrent)?.Id ?? -1;
        int goalId = sketch.Cells.FirstOrDefault(c => c.IsGoal)?.Id ?? -1;

        // 只在"画的东西会变"时重画（格子/连线/**三态分布**/当前/终点任一变化）——避免每帧重建 ✓
        // 🔴 `D-3`：key 必须把**三态**算进去（只数 `Revealed` 的话，"Scouted 升级成 Visited" 不会触发重绘 ⚠️）
        int key = HashCode.Combine(
            sketch.Cells.Count,
            sketch.Links.Count,
            sketch.Cells.Count(c => c.Revealed),
            sketch.Cells.Count(c => c.State == Darkest.Gameplay.Sim.Run.RevealState.Scouted),
            currentId,
            goalId);
        if (key == _lastKey && _canvas.GetChildCount() > 0)
        {
            return;
        }

        _lastKey = key;

        foreach (Node child in _canvas.GetChildren())
        {
            if (child == _walkSkel)
            {
                continue;   // 🔴 骨架是渲染层 ⇒ 不删（只清瓦片）✓
            }

            _canvas.RemoveChild(child);
            child.QueueFree();
        }

        _walkSkel?.ClearTiles();

        // 摆位：x = 深度，y = 同深度内序号（由适配器给定 ⇒ 确定性）
        Dictionary<int, (int X, int Y)> pos = Layout(sketch);

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
                if (_walkSkel?.CorridorLayer is TileMapLayer corrLayer)
                {
                    const int cp = Darkest.UI.WalkMapSkeleton.CorridorPx;
                    corrLayer.SetCell(new Vector2I(cx / cp, cy / cp), _walkSkel.CorridorSourceId, new Vector2I(0, 0));   // 🔴 引擎瓷砖 ✓
                }
                else
                {
                    _canvas.AddChild(new ColorRect
                    {
                        Name = $"Corridor_{link.From}_{link.To}_{i}",
                        Color = Darkest.UI.DdTheme.MapEdge,
                        Position = new Vector2(cx, cy),
                        Size = new Vector2(CorridorSize, CorridorSize),
                    });
                }
            }
        }

        // ② 房间 = 大方块（未揭示 ⇒ 只画"未知"色；DD 里没走过的房间就是这样）
        int maxX = 0;
        int maxY = 0;
        foreach (SketchCell c in sketch.Cells)
        {
            (int X, int Y) p = pos[c.Id];
            // 🔴 `D-3`：**三态三配色** —— Scouted 用"暗 + 亮轮廓"（`MapScouted`：比未知亮、比已知暗）
            Color color = c.IsCurrent ? Darkest.UI.DdTheme.Highlight
                : c.IsGoal ? Darkest.UI.DdTheme.Danger
                : c.State == Darkest.Gameplay.Sim.Run.RevealState.Unexplored ? Darkest.UI.DdTheme.MapUnknown
                : c.State == Darkest.Gameplay.Sim.Run.RevealState.Scouted ? Darkest.UI.DdTheme.MapScouted
                : Darkest.UI.DdTheme.MapVisited;

            if (_walkSkel?.RoomLayer is TileMapLayer roomLayer)
            {
                const int rp = Darkest.UI.WalkMapSkeleton.RoomPx;
                int tile = c.IsCurrent ? Darkest.UI.WalkMapSkeleton.TileCurrent
                    : c.IsGoal ? Darkest.UI.WalkMapSkeleton.TileGoal
                    : c.State == Darkest.Gameplay.Sim.Run.RevealState.Unexplored ? Darkest.UI.WalkMapSkeleton.TileUnknown
                    : c.State == Darkest.Gameplay.Sim.Run.RevealState.Scouted ? Darkest.UI.WalkMapSkeleton.TileScouted
                    : Darkest.UI.WalkMapSkeleton.TileVisited;
                roomLayer.SetCell(new Vector2I(p.X / rp, p.Y / rp), _walkSkel.RoomSourceId, new Vector2I(tile, 0));   // 🔴 五态瓦片（未知/侦察/已访/当前/终点）✓
            }
            else
            {
                _canvas.AddChild(new ColorRect
                {
                    Name = $"Room_{c.Id}",
                    Color = color,
                    Position = new Vector2(p.X, p.Y),
                    Size = new Vector2(RoomSize, RoomSize),
                });
            }

            // 🔴 主程序 (A)：格子上叠**可点热区**（相邻未探索才可点 ⇒ 点击走一格）✓
            //    视觉仍是 `ColorRect`（不参与"框必须不透明"判据）；热区是扁平透明 Button ✓
            var hit = new Button
            {
                Name = $"RoomHit_{c.Id}",
                Position = new Vector2(p.X, p.Y),
                Size = new Vector2(RoomSize, RoomSize),
                Flat = true,
                Disabled = !c.Movable,
                TooltipText = $"房间 {c.Id}（{(string.IsNullOrEmpty(c.Type) ? "?" : c.Type)}）" +
                              // 🔴 `D-3`：三态**必须**在提示里说清 —— 玩家要能分辨"知道有东西" vs "进去过" ✓
                              (c.State == Darkest.Gameplay.Sim.Run.RevealState.Scouted ? "　（已侦察：只知轮廓）"
                                  : c.State == Darkest.Gameplay.Sim.Run.RevealState.Visited ? "　（已看清）"
                                  : "　（未知）") +
                              (c.Movable ? "　点击 ⇒ 走一格" : "　（不可达）"),
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
            // 🔴 `D-3`：读数**必须**把三态分开报（否则"已揭示 5/9"盖住了"其中 3 格只是轮廓" ⚠️）
            int scouted = sketch.Cells.Count(x => x.State == Darkest.Gameplay.Sim.Run.RevealState.Scouted);
            int visited = sketch.Cells.Count(x => x.State == Darkest.Gameplay.Sim.Run.RevealState.Visited);
            int explored = sketch.Cells.Count(x => x.State != Darkest.Gameplay.Sim.Run.RevealState.Unexplored);
            _info.Text = $"当前：{(string.IsNullOrEmpty(sketch.CurrentType) ? "?" : sketch.CurrentType)}" +
                         (sketch.RemainingSegments >= 0 ? $"　剩余 {sketch.RemainingSegments} 段" : string.Empty) +
                         $"　已揭示 {explored}/{sketch.Cells.Count}" +
                         (scouted > 0 ? $"（轮廓 {scouted} · 已看 {visited}）" : string.Empty);
        }

        _lastSketch = Sketch(sketch, pos);
    }
}
