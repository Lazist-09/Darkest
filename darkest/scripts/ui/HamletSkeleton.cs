using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **主城（城池）骨架**（用户 2026-09-17：「修改目前所有 UI ⇒ 能在编辑器里直接干预」）✓
///
/// 定位：`scenes/ui/hamlet_skeleton.tscn` = **整屏顶层骨架**，节点名与 `HamletRoot` 代码里的**逐字一致**：
/// ```
/// HamletMargin (MarginContainer)
/// └ HamletRootCol (VBoxContainer)
///   ├ TopBar (PanelContainer) → TopRow (HBoxContainer)
///   ├ StatusBar (PanelContainer)
///   └ Body (HBoxContainer · ExpandFill)
///     ├ LeftColumn (PanelContainer · ExpandFill) → LeftCol (VBox)
///     └ RightColumn (PanelContainer · 300 宽) → RightCol (VBox)
/// ```
/// ⇒ 在编辑器里可改**边距 / 列间距 / 各栏宽高与占比**（改一处即影响主城）✓
///
/// ⚠️ **接线状态：未接线**（本轮只建骨架；接线要同时改 `HamletRoot` 的建树段，单独一轮做）✓
/// ⚠️ `BottomRow`（底部资源条那一行）**本轮不在骨架里**（我还没核它的类型 ⇒ 不猜，留给接线那一轮一并做）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见结构；编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class HamletSkeleton : Control
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/hamlet_skeleton.tscn";

    public MarginContainer? HamletMargin => GetNodeOrNull<MarginContainer>("HamletMargin");

    public VBoxContainer? HamletRootCol => GetNodeOrNull<VBoxContainer>("HamletMargin/HamletRootCol");

    public PanelContainer? TopBar => GetNodeOrNull<PanelContainer>("HamletMargin/HamletRootCol/TopBar");

    public HBoxContainer? TopRow => GetNodeOrNull<HBoxContainer>("HamletMargin/HamletRootCol/TopBar/TopRow");

    public PanelContainer? StatusBar => GetNodeOrNull<PanelContainer>("HamletMargin/HamletRootCol/StatusBar");

    public HBoxContainer? Body => GetNodeOrNull<HBoxContainer>("HamletMargin/HamletRootCol/Body");

    public PanelContainer? LeftColumn => GetNodeOrNull<PanelContainer>("HamletMargin/HamletRootCol/Body/LeftColumn");

    public VBoxContainer? LeftCol => GetNodeOrNull<VBoxContainer>("HamletMargin/HamletRootCol/Body/LeftColumn/LeftCol");

    public PanelContainer? RightColumn => GetNodeOrNull<PanelContainer>("HamletMargin/HamletRootCol/Body/RightColumn");

    public VBoxContainer? RightCol => GetNodeOrNull<VBoxContainer>("HamletMargin/HamletRootCol/Body/RightColumn/RightCol");

    /// <summary>实例化骨架；场景缺失/类型不符 ⇒ 返回 null（宿主回落代码构建，不崩不静默）✓</summary>
    public static HamletSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        HamletSkeleton? skel = packed?.Instantiate<HamletSkeleton>();
        if (skel is null)
        {
            GD.Print($"[UI 骨架] `{ScenePath}` 不可用 ⇒ 主城回落代码构建（不静默）✓");
            return null;
        }

        return skel;
    }

    public override void _Ready()
    {
        // 骨架目前没有需要预览的文案（各栏内容仍由代码填）⇒ 编辑器里看结构/尺寸即可 ✓
        _ = Engine.IsEditorHint();
    }
}
