using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **主菜单骨架**（用户 2026-09-17：「修改目前所有 UI ⇒ 能在编辑器里直接干预」）✓
///
/// 定位：`scenes/ui/main_menu.tscn` = **整屏骨架**（`MenuMargin` → `MenuCol` → `TitlePanel` / `OptionsPanel` / `StatusPanel`）
/// ⇒ 在编辑器里可**拖拽/改边距/改间距/改尺寸**；宿主接线后**用它当骨架**，代码只往里填数据 ✓
///
/// 🔴 `[Tool]` ⇒ 编辑器里可见结构；⚠️ 编辑器逻辑必须 `Engine.IsEditorHint()` 守卫（运行时绝不走编辑器分支）✓
/// ⚠️ 节点名保持：`MenuMargin` / `MenuCol` / `TitlePanel` / `TitleLabel` / `OptionsPanel` / `OptionsCol` / `StatusPanel` / `StatusLabel`
///    （宿主与验收读数按名取；改名会断线）✓
/// ⚠️ **接线状态：未接线**（本轮只建骨架；接线要同时改 `MainMenuRoot` 的建树与刷新路径，单独一轮做）✓
/// </summary>
[Tool]
public partial class MainMenuSkeleton : Control
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/main_menu.tscn";

    /// <summary>编辑器预览用标题（运行时被真实标题覆盖）✓</summary>
    [Export]
    public string PreviewTitle { get; set; } = "Darkest（骨架预览）";

    public VBoxContainer? MenuCol => GetNodeOrNull<VBoxContainer>("MenuMargin/MenuCol");

    public Label? TitleLabel => GetNodeOrNull<Label>("MenuMargin/MenuCol/TitlePanel/TitleRow/TitleLabel");

    public VBoxContainer? OptionsCol => GetNodeOrNull<VBoxContainer>("MenuMargin/MenuCol/OptionsPanel/OptionsCol");

    public Label? StatusLabel => GetNodeOrNull<Label>("MenuMargin/MenuCol/StatusPanel/StatusLabel");

    /// <summary>实例化骨架；场景缺失/类型不符 ⇒ 返回 null（宿主回落代码构建，不崩不静默）✓</summary>
    public static MainMenuSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        MainMenuSkeleton? menu = packed?.Instantiate<MainMenuSkeleton>();
        if (menu is null)
        {
            GD.Print($"[UI 骨架] `{ScenePath}` 不可用 ⇒ 主菜单回落代码构建（不静默）✓");
            return null;
        }

        GD.Print($"[UI 骨架] ✅ 主菜单采用骨架 `{ScenePath}`（**编辑器里可编辑**）✓");
        return menu;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint() && TitleLabel is not null)
        {
            TitleLabel.Text = PreviewTitle;
        }
    }
}
