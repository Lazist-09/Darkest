using Godot;

namespace Darkest.UI;

/// <summary>DD 1:1 P5：传家宝兑换屏控制器（开屏 + 骨架优先 + 关闭）—— 与 HamletRoot 同 partial ✓</summary>
public partial class HamletRoot : Control
{
    /// <summary>开【传家宝兑换】屏（骨架优先；缺失 ⇒ 回落一行说明，不崩不静默）</summary>
    public void OpenHeirloomExchange()
    {
        (PanelContainer self, _, VBoxContainer body) = MakePopup("HeirloomExchangePopup", "💎 【传家宝兑换】", Darkest.UI.PopupLayout.Heirloom);
        HeirloomExchangeSkeleton? skel = HeirloomExchangeSkeleton.TryInstantiate();
        if (skel is not null)
        {
            body.AddChild(skel);
            if (skel.Close is Button close)
            {
                // 🔴 2026-09-20：`self` 由 `MakePopup` 直接返回（此前 `body.GetParent()?.GetParent() as PanelContainer`
                //    在代码回落路径下取到的是 MarginContainer ⇒ cast 失败 ⇒ **关不掉**）✓
                close.Pressed += () => ClosePopup(self, "HeirloomExchangePopup");
            }
        }
        else
        {
            body.AddChild(PopupLine("传家宝兑换：骨架不可用（回落文本，不静默）"));
        }

        GD.Print($"[HamletRoot] OpenHeirloomExchange：已开屏（骨架{(skel is not null ? "采用" : "回落")}）");
    }
}
