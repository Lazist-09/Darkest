using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Gameplay.Sim.Director;
using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 战斗 · DD 紧凑状态托盘（DD 1:1 还原 ④；2026-09-21）
/// 依据＝直读原游戏 scripts\layout\screen.raid.status_bars.darkest + screen.raid.darkest(overlays)：
///   overlays: hero_start_pos 788 680 · hero_spacing -168 0 · monster_start_pos 1050 680 · monster_spacing 168 0
///   status_bars: y_pos 698 · health_bar_offset 50 0 · health_bar_height 10 · health_bar_widths 100 200 300 400 · stress_offset -1 12
/// ⇒ 680=单位/overlay 层、698=托盘层（分开取）；全部折成屏幕比例，不写像素 ✓
/// ④-1 骨架+接线（094b112）· ④-2 填充（本刀，与卡牌同源 UnitProjection）✓ namespace Darkest.UI（大写 UI）✓
/// </summary>
public partial class BattleUI : Control
{
    private Control? _statusTray;

    private void BuildStatusTray(Control parent)
    {
        if (_statusTray is not null && GodotObject.IsInstanceValid(_statusTray))
        {
            return;
        }

        // DD 1:1 3-1：骨架优先 —— 骨架里已放好 8 槽（编辑器可改）则直接用，缺失才代码建
        if (_bottomBarSkel?.StatusTray is Control skelTray)
        {
            _statusTray = skelTray;
            GD.Print("[UI 战斗] OK 托盘采用骨架 battle_bottombar.tscn/StatusTray（编辑器里可改）");
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

        GD.Print("[UI 战斗] ✅ DD 紧凑状态托盘骨架就位（英雄 41.0%-8.75%x4 / 怪物 54.7%+8.75%x4 · y 64.6% = DD 698/1080）");
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

    /// <summary>④-2 填充：4v4 的 HP/压力条绑进 8 槽（与卡牌同源投影，不新造数字）✓</summary>
    private void FillStatusTray(UnitProjection[] players, UnitProjection[] enemies)
    {
        // DD 1:1 3-1：**惰性建托盘**（此处底栏骨架已采用 ⇒ 能用骨架就用骨架；否则回落到代码建槽）
        if (_statusTray is null || !GodotObject.IsInstanceValid(_statusTray))
        {
            BuildStatusTray(_uiRoot);
        }

        if (_statusTray is null || !GodotObject.IsInstanceValid(_statusTray))
        {
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            FillTraySlot($"HeroTray{i + 1}", i < players.Length ? players[players.Length - 1 - i] : null);
            FillTraySlot($"EnemyTray{i + 1}", i < enemies.Length ? enemies[i] : null);
        }
    }

    private void FillTraySlot(string slotName, UnitProjection? u)
    {
        if (_statusTray is null || !GodotObject.IsInstanceValid(_statusTray))
        {
            return;
        }

        if (_statusTray.GetNodeOrNull<Control>(slotName) is not Control slot)
        {
            return;
        }

        ProgressBar? hp = slot.GetNodeOrNull<ProgressBar>("Hp");
        if (hp is null)
        {
            hp = new ProgressBar { Name = "Hp", MinValue = 0, MaxValue = 1, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            hp.AnchorLeft = 0f;
            hp.AnchorRight = 1f;
            hp.AnchorTop = 0f;
            hp.AnchorBottom = 0.5f;
            slot.AddChild(hp);
        }

        ProgressBar? stress = slot.GetNodeOrNull<ProgressBar>("Stress");
        if (stress is null)
        {
            stress = new ProgressBar { Name = "Stress", MinValue = 0, MaxValue = 100, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            stress.AnchorLeft = 0f;
            stress.AnchorRight = 1f;
            stress.AnchorTop = 0.5f;
            stress.AnchorBottom = 1f;
            stress.Modulate = Darkest.UI.DdTheme.TextInfo;
            slot.AddChild(stress);
        }

        bool empty = u is null || u.UnitId == "-";
        slot.Visible = !empty;
        if (empty || u is null)
        {
            return;
        }

        hp.MaxValue = u.MaxHp > 0 ? u.MaxHp : 1;
        hp.Value = u.Hp;
        hp.Modulate = u.Weak ? Darkest.UI.DdTheme.HpWeak : Darkest.UI.DdTheme.Hp;
        stress.MaxValue = 100;
        stress.Value = u.Morale;
        slot.TooltipText = $"{u.UnitId}　HP {u.Hp}/{u.MaxHp}　士气 {u.Morale}";
    }

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

        return $"status-tray: 槽 {n} 个（DD 4v4 位置 · 比例锚点 · 已绑 HP/压力条）";
    }
}
