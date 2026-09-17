using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **建筑详情弹窗【内容布局】骨架**（用户 2026-09-17：「能在编辑器里直接干预」+「重复元素抽模板」）✓
///
/// 定位：`scenes/ui/building_popup.tscn` —— 弹窗**外框**仍由 `HamletRoot.MakePopup` 工厂出（标题 + ✕ 关闭），
/// 本骨架只负责**内容布局**那一块（改这里 = 改建筑详情弹窗的内部排布）：
/// ```
/// BuildingPopupBody (VBox)
/// └ BuildingSplit (HBox)
///   ├ BuildingList (VBox · 220 宽)
///   │ └ ShopkeeperSlot (PanelContainer · 96 高) → ShopkeeperPlaceholder (ColorRect)
///   └ BuildingContent (VBox · ExpandFill)
/// ```
/// ⇒ **左列宽 220 / 店长位高 96 / 左右间距 12** 等全部可在编辑器里改 ✓
///
/// ✅ **接线状态：已接线**（`HamletRoot.OpenBuildingPopup` 采用之；2026-09-21 验证：`[UI 骨架] ✅ 建筑详情采用骨架`）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见结构；编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// ⚠️ 节点名保持：`BuildingSplit` / `BuildingList` / `ShopkeeperSlot` / `ShopkeeperPlaceholder` / `BuildingContent` ✓
/// </summary>
[Tool]
public partial class BuildingPopupSkeleton : VBoxContainer
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/building_popup.tscn";

    public HBoxContainer? Split => GetNodeOrNull<HBoxContainer>("BuildingSplit");

    public VBoxContainer? List => GetNodeOrNull<VBoxContainer>("BuildingSplit/BuildingList");

    public PanelContainer? ShopkeeperSlot => GetNodeOrNull<PanelContainer>("BuildingSplit/BuildingList/ShopkeeperSlot");

    public ColorRect? ShopkeeperPlaceholder =>
        GetNodeOrNull<ColorRect>("BuildingSplit/BuildingList/ShopkeeperSlot/ShopkeeperPlaceholder");

    public VBoxContainer? Content => GetNodeOrNull<VBoxContainer>("BuildingSplit/BuildingContent");

    /// <summary>实例化骨架；场景缺失/类型不符 ⇒ null（宿主回落代码构建，不崩不静默）✓</summary>
    public static BuildingPopupSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        BuildingPopupSkeleton? skel = packed?.Instantiate<BuildingPopupSkeleton>();
        if (skel is null)
        {
            GD.Print($"[UI 骨架] `{ScenePath}` 不可用 ⇒ 建筑详情弹窗回落代码构建（不静默）✓");
            return null;
        }

        GD.Print($"[UI 骨架] ✅ 建筑详情采用骨架 `{ScenePath}`（**编辑器里可编辑**）✓");
        return skel;
    }

    public override void _Ready()
    {
        _ = Engine.IsEditorHint();
    }
}
