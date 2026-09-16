using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **行走地图瓷砖渲染骨架**（用户 2026-09-17：「E —— 行走地图改用引擎内置 `TileMapLayer`」）✓
///
/// 定位：`scenes/ui/walk_map_layer.tscn` = **双层瓷砖渲染**（几何与手绘现状**逐像素一致**，见 skill `§14.0.16`）：
/// ```
/// WalkMapLayer (Control · **clip_contents = true** ⇒ 保留"永不超出框")
/// ├ WalkCorridorLayer (TileMapLayer · TileSize **5×5** = 走廊)
/// └ WalkRoomLayer     (TileMapLayer · TileSize **14×14** = 房间，4 态着色)
/// ```
/// 🔴 **为什么两层**：`TileSet` 只有一个 `TileSize`，而房间 14px、走廊 5px ⇒ 单层会把走廊画粗（= 视觉变更）✓
/// 🔴 **瓦片纹理不引入任何美术文件**：运行时用**引擎内置**生成图集（`Image.CreateEmpty` → `FillRect` → `ImageTexture` → `TileSetAtlasSource`）✓
///    · 房间 4 格：**未知 / 已访 / 当前 / 终点**（颜色取自 `DdTheme`，不写死字面量，红线 19）✓
///    · 走廊 1 格：`DdTheme.MapEdge` ✓
/// ⚠️ **接线状态：未接线**（`WalkMapView.Refresh` 仍手绘 `ColorRect`；接线单独一轮，见 skill `§14.0.16`）✓
/// ⚠️ 节点名保持：`WalkCorridorLayer` / `WalkRoomLayer` ✓
/// </summary>
[Tool]
public partial class WalkMapSkeleton : Control
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/walk_map_layer.tscn";

    /// <summary>房间瓦片边长（= 手绘现状 `RoomSize`）✓</summary>
    public const int RoomPx = 14;

    /// <summary>走廊瓦片边长（= 手绘现状 `CorridorSize`）✓</summary>
    public const int CorridorPx = 5;

    /// <summary>房间四态在图集里的列号（与 `WalkMapView` 的着色分支一一对应）✓</summary>
    public const int TileUnknown = 0;

    public const int TileVisited = 1;

    public const int TileCurrent = 2;

    public const int TileGoal = 3;

    public TileMapLayer? RoomLayer => GetNodeOrNull<TileMapLayer>("WalkRoomLayer");

    public TileMapLayer? CorridorLayer => GetNodeOrNull<TileMapLayer>("WalkCorridorLayer");

    /// <summary>房间图集的源 id（宿主 `SetCell` 用）✓</summary>
    public int RoomSourceId => _roomSourceId;

    /// <summary>走廊图集的源 id（宿主 `SetCell` 用）✓</summary>
    public int CorridorSourceId => _corridorSourceId;

    private int _roomSourceId;
    private int _corridorSourceId;

    /// <summary>实例化骨架；场景缺失/类型不符 ⇒ null（宿主回落手绘，不崩不静默）✓</summary>
    public static WalkMapSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        WalkMapSkeleton? skel = packed?.Instantiate<WalkMapSkeleton>();
        if (skel is null)
        {
            GD.Print($"[UI 骨架] `{ScenePath}` 不可用 ⇒ 行走地图回落手绘（不静默）✓");
            return null;
        }

        skel.EnsureTileSets();
        GD.Print($"[UI 骨架] ✅ 行走地图采用**双层瓷砖**骨架 `{ScenePath}`（房间 {RoomPx}px ／ 走廊 {CorridorPx}px）✓");
        return skel;
    }

    /// <summary>用**引擎内置**造两个图集（**不读任何美术文件**）✓</summary>
    public void EnsureTileSets()
    {
        // ① 房间：14×14 × 4 格（横排）—— 未知 / 已访 / 当前 / 终点
        if (RoomLayer is TileMapLayer room && (room.TileSet is null || room.TileSet.GetSourceCount() == 0))
        {
            var img = Image.CreateEmpty(RoomPx * 4, RoomPx, false, Image.Format.Rgba8);
            Color[] colors =
            {
                Opaque(Darkest.Ui.DdTheme.MapUnknown),
                Opaque(Darkest.Ui.DdTheme.MapVisited),
                Opaque(Darkest.Ui.DdTheme.Highlight),
                Opaque(Darkest.Ui.DdTheme.Danger),
            };
            for (int i = 0; i < colors.Length; i++)
            {
                img.FillRect(new Rect2I(i * RoomPx, 0, RoomPx, RoomPx), colors[i]);
            }

            var src = new TileSetAtlasSource
            {
                Texture = ImageTexture.CreateFromImage(img),
                TextureRegionSize = new Vector2I(RoomPx, RoomPx),
            };
            for (int i = 0; i < colors.Length; i++)
            {
                src.CreateTile(new Vector2I(i, 0));
            }

            var tileSet = new TileSet { TileSize = new Vector2I(RoomPx, RoomPx) };
            _roomSourceId = tileSet.AddSource(src);
            room.TileSet = tileSet;
        }

        // ② 走廊：5×5 × 1 格（MapEdge）
        if (CorridorLayer is TileMapLayer corridor && (corridor.TileSet is null || corridor.TileSet.GetSourceCount() == 0))
        {
            var img = Image.CreateEmpty(CorridorPx, CorridorPx, false, Image.Format.Rgba8);
            img.FillRect(new Rect2I(0, 0, CorridorPx, CorridorPx), Opaque(Darkest.Ui.DdTheme.MapEdge));
            var src = new TileSetAtlasSource
            {
                Texture = ImageTexture.CreateFromImage(img),
                TextureRegionSize = new Vector2I(CorridorPx, CorridorPx),
            };
            src.CreateTile(new Vector2I(0, 0));
            var tileSet = new TileSet { TileSize = new Vector2I(CorridorPx, CorridorPx) };
            _corridorSourceId = tileSet.AddSource(src);
            corridor.TileSet = tileSet;
        }
    }

    /// <summary>清空两层（宿主每次重画前调用）✓</summary>
    public void ClearTiles()
    {
        RoomLayer?.Clear();
        CorridorLayer?.Clear();
    }

    /// <summary>🔴 瓦片是**实体**（不适用"空闲占位半透明"规则）⇒ α 统一拉到 1 ✓</summary>
    private static Color Opaque(Color c)
    {
        c.A = 1f;
        return c;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            EnsureTileSets();
        }
    }
}
