using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **行走地图瓷砖渲染骨架**（用户 2026-09-17：「E —— 行走地图改用引擎内置 `TileMapLayer`」）✓
///
/// 定位：`scenes/ui/walk_map_layer.tscn` = **引擎内置瓷砖渲染层**
/// ```
/// WalkMapLayer (Control · **clip_contents = true** ⇒ 保留"永不超出框")  ← TileMapLayer 是 Node2D，裁切必须靠这层 Control ✓
/// └ WalkTileLayer (TileMapLayer)
/// ```
/// ⇒ 图集/瓦片尺寸/图层属性**在编辑器里可改**（`TileMapLayer` 有编辑器内建瓦片绘制器）✓
///
/// 🔴 **瓦片纹理不引入任何美术文件**：运行时用**引擎内置**生成一张 16×16 双色图集
///    （`Image.CreateEmpty` + `Fill` ⇒ `ImageTexture.CreateFromImage` ⇒ `TileSetAtlasSource`）✓
///    · 瓦片 0 = **房间**（房格色）· 瓦片 1 = **走廊**（走廊色）✓
/// ⚠️ 颜色取自 `DdTheme`（不写死字面量，红线 19/§14.4）✓
/// ⚠️ **接线状态：未接线**（`WalkMapView.Refresh` 仍手绘 `ColorRect`；接线单独一轮做，见 skill `§14.0.15`）✓
/// ⚠️ 节点名保持：`WalkTileLayer` ✓
/// </summary>
[Tool]
public partial class WalkMapSkeleton : Control
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/walk_map_layer.tscn";

    /// <summary>瓦片边长（像素）—— 与图集一致 ✓</summary>
    public const int TilePx = 16;

    /// <summary>房间瓦片在图集里的列号 ✓</summary>
    public const int RoomTile = 0;

    /// <summary>走廊瓦片在图集里的列号 ✓</summary>
    public const int CorridorTile = 1;

    public TileMapLayer? Layer => GetNodeOrNull<TileMapLayer>("WalkTileLayer");

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

        skel.EnsureTileSet();
        GD.Print($"[UI 骨架] ✅ 行走地图采用瓷砖骨架 `{ScenePath}`（**编辑器里可编辑图集**）✓");
        return skel;
    }

    /// <summary>
    /// 🔴 用**引擎内置**造图集（**不读任何美术文件**）—— 双色 32×16（两格 16×16）✓
    /// ⚠️ 编辑器里只用于预览；运行时由宿主调用后再 `SetCell` ✓
    /// </summary>
    public void EnsureTileSet()
    {
        TileMapLayer? layer = Layer;
        if (layer is null)
        {
            return;
        }

        if (layer.TileSet is not null && layer.TileSet.GetSourceCount() > 0)
        {
            return;
        }

        var img = Image.CreateEmpty(TilePx * 2, TilePx, false, Image.Format.Rgba8);
        Color room = Darkest.Ui.DdTheme.PlaceholderFill;
        Color corridor = Darkest.Ui.DdTheme.PanelBgRaised;
        room.A = 1f;        // 瓦片本体不透明（"空位半透明"规则针对**空闲占位**，瓷砖是实体）✓
        corridor.A = 1f;
        img.FillRect(new Rect2I(0, 0, TilePx, TilePx), room);
        img.FillRect(new Rect2I(TilePx, 0, TilePx, TilePx), corridor);

        var src = new TileSetAtlasSource
        {
            Texture = ImageTexture.CreateFromImage(img),
            TextureRegionSize = new Vector2I(TilePx, TilePx),
        };
        src.CreateTile(new Vector2I(RoomTile, 0));
        src.CreateTile(new Vector2I(CorridorTile, 0));

        var tileSet = new TileSet { TileSize = new Vector2I(TilePx, TilePx) };
        int id = tileSet.AddSource(src);
        layer.TileSet = tileSet;
        layer.SetMeta("tileset_source_id", id);
    }

    /// <summary>取图集源 id（宿主 `SetCell` 用）✓</summary>
    public int SourceId => Layer is not null && Layer.HasMeta("tileset_source_id")
        ? (int)Layer.GetMeta("tileset_source_id")
        : 0;

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            EnsureTileSet();
        }
    }
}
