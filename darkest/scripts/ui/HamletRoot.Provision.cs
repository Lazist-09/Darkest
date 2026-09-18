using Godot;

namespace Darkest.UI;

/// <summary>DD 1:1 ②：供应屏控制器（开屏 + 骨架优先 + 关闭）—— 与 HamletRoot 同 partial ⇒ 复用 MakePopup/Overlay ✓</summary>
public partial class HamletRoot : Control
{
    /// <summary>开【供应】屏（骨架优先；缺失 ⇒ 回落一行说明，不崩不静默）</summary>
    public void OpenProvision()
    {
        (_, _, VBoxContainer body) = MakePopup("ProvisionPopup", "🛒 【供应】");
        ProvisionSkeleton? skel = ProvisionSkeleton.TryInstantiate();
        if (skel is not null)
        {
            body.AddChild(skel);
            if (skel.Close is Button close)
            {
                PanelContainer? self = body.GetParent()?.GetParent() as PanelContainer;
                close.Pressed += () => { if (self is not null) { self.Visible = false; } CloseTopPopup(); };
            }
        }
        else
        {
            body.AddChild(PopupLine("供应：骨架不可用（回落文本，不静默）"));
        }

        GD.Print($"[HamletRoot] OpenProvision：已开屏（骨架{(skel is not null ? "采用" : "回落")}）");
    }
}
