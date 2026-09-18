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

            // 🔴 空态 + 接口声明（不伪造内容）：本屏布局按 DD 还原，但**数据接口未由内核提供** ⇒ 明确写出待接口 ✓
            //   需要内核提供：① 队伍位（4 人）② 商店商品列表（名称/价格/库存）③ 任务信息 ④ 侦察数值 ⑤ 售回信息
            if (skel.QuestInfo is Label qi) { qi.Text = "任务信息：待内核接口（ProvisionQuestInfo）"; }
            if (skel.ScoutingStat is Label ss) { ss.Text = "侦察数值：待内核接口（ProvisionScouting）"; }
            if (skel.SellBackInfo is Label sb) { sb.Text = "售回信息：待内核接口（ProvisionSellBack）"; }
            if (skel.PartyGrid is GridContainer pg)
            {
                for (int slot = 1; slot <= 4; slot++)
                {
                    var cell = new PanelContainer { Name = $"PartySlot{slot}", CustomMinimumSize = new Vector2(60, 80), TooltipText = $"队伍位 {slot}：待内核接口（ProvisionParty）" };
                    cell.AddChild(new ColorRect { Name = "CellFill", Color = Darkest.UI.DdTheme.PlaceholderFill });
                    pg.AddChild(cell);
                }
            }

            if (skel.StoreGrid is GridContainer sg)
            {
                var empty = new PanelContainer { Name = "StoreEmptyState", CustomMinimumSize = new Vector2(200, 60) };
                empty.AddChild(PopupLine("商店：内核未提供商品接口 ⇒ 空态（不伪造商品）"));
                sg.AddChild(empty);
            }

            GD.Print("[UI-TRACE] provision-empty-state");
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
