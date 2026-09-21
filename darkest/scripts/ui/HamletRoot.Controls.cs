using Godot;

namespace Darkest.UI;

/// <summary>阶段2：按键提示屏控制器（DD shared/controls）—— 与 HamletRoot 同 partial ✓
/// 说明：DD 的按键提示从**设置菜单**进入；我域暂**不加 ☰ 菜单项**（避免把设置类屏塞进城镇菜单 ✗），
/// 仅提供 `OpenControls()` + 冒烟旗标 `--hamlet-controls` ⇒ 需要时可再接入口 ✓</summary>
public partial class HamletRoot : Control
{
    /// <summary>开【按键提示】屏（骨架优先；缺失 ⇒ 回落一行说明，不崩不静默）✓</summary>
    public void OpenControls()
    {
        (_, _, VBoxContainer body) = MakePopup("ControlsPopup", "🎮 【按键提示】", Darkest.UI.PopupLayout.Modal);
        ControlsSkeleton? skel = ControlsSkeleton.TryInstantiate();
        if (skel is not null)
        {
            body.AddChild(skel);
        }
        else
        {
            body.AddChild(PopupLine("按键提示：骨架不可用（回落文本，不静默）"));
        }

        GD.Print($"[HamletRoot] OpenControls：已开屏（骨架{(skel is not null ? "采用" : "回落")}）");
    }
}
