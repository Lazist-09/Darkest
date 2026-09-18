using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **ModalDialog 模板**（2026-09-21 架构诊断 Track 3：对齐 DD `shared/modal_dialog`）✓
/// 用途：`ui_spec §14` 的**模态外观** —— 满屏不透明 `PanelContainer` + 标题行（**含 ✕ 关闭**）+ 内容 `VBox`；
/// **改这一处 = 改所有模态的外观**（与 13 个既有模板 + `tooltip.tscn` 同规格）✓
/// 纪律：① 容器化、**零手写坐标** ② **必须显式挂 Theme**（本屏根若为 Node2D 主题链不经过 ⇒ 框会半透明）
///      ③ `Esc`（`ui_cancel`）也必须能关（"能开不能关"是弹窗最常见的坑）—— 由 `OverlayLayer` 统一兜底 ✓
/// ⚠️ **接线状态：未接线**（本轮只建成模板；接线 = 让 `BattleUI.Modals.MakeOpaqueModal` 与
///    `HamletRoot.PopupMenu.MakePopup` **优先用本模板**、缺失回落代码构建）—— 未接线就明说 ✓
/// </summary>
public static class ModalDialogTemplate
{
    /// <summary>模板场景路径（编辑器里改外观）✓</summary>
    public const string ScenePath = "res://scenes/ui/modal_dialog.tscn";

    /// <summary>
    /// 实例化模板并填标题；返回模态根 + 内容容器（供调用方往 `body` 里加行）。
    /// **场景缺失/节点缺失 ⇒ 双双为 null**（调用方回落代码构建，不崩不静默）✓
    /// </summary>
    public static (PanelContainer? Panel, VBoxContainer? Body) TryCreate(string title)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        PanelContainer? panel = packed?.Instantiate<PanelContainer>();
        if (panel is null)
        {
            GD.Print($"[UI 模板] `{ScenePath}` 不可用 ⇒ 模态回落代码构建（不静默）✓");
            return (null, null);
        }

        if (panel.GetNodeOrNull<Label>("DialogCol/DialogTitleRow/DialogTitle") is Label titleLabel)
        {
            titleLabel.Text = title;
        }

        VBoxContainer? body = panel.GetNodeOrNull<VBoxContainer>("DialogCol/DialogBody");
        GD.Print($"[UI 模板] ✅ 模态采用模板 `{ScenePath}`（标题「{title}」，编辑器里可改外观）✓");
        return (panel, body);
    }

    /// <summary>把 ✕ 关闭按钮接到给定动作（模板若改节点名，这里同步改）✓</summary>
    public static void BindClose(PanelContainer panel, System.Action onClose)
    {
        if (panel.GetNodeOrNull<Button>("DialogCol/DialogTitleRow/DialogClose") is Button close)
        {
            close.Pressed += onClose;
        }
    }
}
