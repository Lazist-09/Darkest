using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 战斗 · DD 紧凑状态托盘（DD 1:1 还原 ④-1；2026-09-21）
/// 依据＝直读原游戏 `scripts\layout\screen.raid.status_bars.darkest` + `screen.raid.darkest`(overlays)：
///   overlays: hero_start_pos 788 680 · hero_spacing -168 0 · monster_start_pos 1050 680 · monster_spacing 168 0
///   status_bars: y_pos 698 · health_bar_offset 50 0 · health_bar_height 10 · health_bar_widths 100 200 300 400 · char_x_offset -50
/// ⇒ 两处 y 不是同一个数（680=单位/overlay 层；698=托盘层）⇒ 本文件用托盘层 698 ✓
/// ⇒ 全部折成屏幕比例（1920×1080），不写像素坐标 ✓
/// ⚠️ 本刀（④-1）只建 8 个空槽骨架并已接入 Build()；填充见 ④-2 ✓ 命名：namespace Darkest.UI（大写 UI）✓
/// </summary>
public partial class BattleUI : Control
{
    private Control? _statusTray;

    /// <summary>建 DD 紧凑状态托盘（英雄 4 + 怪物 4 空槽，比例锚点）✓</summary>
    private void BuildStatusTray(Control parent)
    {
        if (_statusTray is not null && GodotObject.IsInstanceValid(_statusTray))
        {
            return;
        }

        var tray = new Control { Name = "StatusTray", MouseFilter = Control.MouseFilterEnum.Ignore };
        tray.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        parent.AddChild(tray);
        _statusTray = tray;

        float[] heroX = { 0.410f, 0.323f, 0.235f, 0.148f };
        float[] enemyX = { 0.547f, 0.634f, 0.722f, 0.809f };
        const float trayY = 0.646f;
        const float barH = 0.0093f;
        const float barW = 0.104f;

        for (int i = 0; i < 4; i++)
        {
            AddTraySlot(tray, $"HeroTray{i + 1}", heroX[i], trayY, barW, barH);
            AddTraySlot(tray, $"EnemyTray{i + 1}", enemyX[i], trayY, barW, barH);
        }

        GD.Print("[UI 战斗] ✅ DD 紧凑状态托盘骨架就位（英雄 41.0%-8.75%x4 / 怪物 54.7%+8.75%x4 · y 64.6% = DD 698/1080 · 条 10.4%x0.93% = DD 200/10）仅骨架，填充见 ④-2");
    }

    private static void AddTraySlot(Control parent, string name, float x, float y, float w, float h)
    {
        var slot = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
        slot.AnchorLeft = x;
        slot.AnchorRight = x + w;
        slot.AnchorTop = y;
        slot.AnchorBottom = y + h;
        parent.AddChild(slot);
    }

    /// <summary>自检读数（槽数；未建 ⇒ 未建）✓</summary>
    public string DescribeStatusTray()
    {
        if (_statusTray is null || !GodotObject.IsInstanceValid(_statusTray))
        {
            return "status-tray: 未建";
        }

        int n = 0;
        foreach (Node child in _statusTray.GetChildren())
        {
            if (child is Control)
            {
                n++;
            }
        }

        return $"status-tray: 槽 {n} 个（DD 4v4 位置 · 比例锚点）";
    }
}
