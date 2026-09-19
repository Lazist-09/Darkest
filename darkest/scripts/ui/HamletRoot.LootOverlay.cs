using Godot;

namespace Darkest.UI;

/// <summary>DD 1:1 战利品弹层控制器（开屏 + 骨架优先 + 关闭）—— 与 HamletRoot 同 partial ✓</summary>
public partial class HamletRoot : Control
{
    /// <summary>开【战利品】弹层（骨架优先；缺失 ⇒ 回落一行说明）✓
    /// 说明：这是**远征/战斗**的弹层（DD overlay.loot），故**不加城池菜单入口**（避免把战斗弹层塞进城镇 UI）✓
    /// 数据未接入 ⇒ 格子/描述/按钮全为**色块占位**（不换不删），仅 ✕ 关闭可用 ✓</summary>
    public void OpenLootOverlay()
    {
        (_, _, VBoxContainer body) = MakePopup("LootOverlayPopup", "🎁 【战利品】");
        LootOverlaySkeleton? skel = LootOverlaySkeleton.TryInstantiate();
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
            body.AddChild(PopupLine("战利品：骨架不可用（回落文本，不静默）"));
        }

        GD.Print($"[HamletRoot] OpenLootOverlay：已开屏（骨架{(skel is not null ? "采用" : "回落")}）");
    }
}
