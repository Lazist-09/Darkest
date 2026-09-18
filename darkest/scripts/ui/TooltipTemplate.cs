using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **Tooltip 模板**（2026-09-21 架构诊断 Track 3：对齐 DD `shared/tooltip`）✓
/// 用途：`ui_spec §11.4⑤` 的**悬停层外观** —— **改这一处 = 改所有悬停说明**（与 13 个既有模板同规格）✓
/// 纪律：容器化 + 零手写坐标 + 显式挂 Theme（本屏根若是 Node2D 主题链不经过 ⇒ 框会半透明）✓
/// ⚠️ **接线状态：已接线**（`OverlayLayer.ShowTooltip` 优先实例化本模板；缺失 ⇒ 回落代码构建，不崩不静默）✓
/// </summary>
public static class TooltipTemplate
{
    /// <summary>模板场景路径（编辑器里改外观）✓</summary>
    public const string ScenePath = "res://scenes/ui/tooltip.tscn";

    /// <summary>实例化模板并写入文本；场景缺失/类型不符 ⇒ null（调用方回落）✓</summary>
    public static Control? TryCreate(string text)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        Control? box = packed?.Instantiate<Control>();
        if (box is null)
        {
            return null;
        }

        SetText(box, text);
        GD.Print($"[UI 模板] ✅ 悬停说明采用模板 `{ScenePath}`（编辑器里可改外观）✓");
        return box;
    }

    /// <summary>把文本写进模板内的 `TooltipText`（模板若改节点名，这里同步改）✓</summary>
    public static void SetText(Control box, string text)
    {
        if (box.GetNodeOrNull<Label>("TooltipText") is Label label)
        {
            label.Text = text;
        }
    }
}
