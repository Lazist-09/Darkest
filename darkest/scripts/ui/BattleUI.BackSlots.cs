using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `BattleUI.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **战斗 · 后排长条框与占位族**（`RefreshBackSlots`（同框 5/6 号位）· `FillBackSlots`（立绘留框＋空位如实“（空）”）· `PlaceholderCombatTexture`／`WithPlaceholderAlpha`（占位贴图与调色板 α））✓
/// ③ 🔴 依赖主类私有成员：`_view`（`Units(true)` 投影）· `_slotLeft`（同框容器）；外部走 `SlotRowTemplate`／`DdTheme`／`HeroArt`✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class BattleUI : Control
{
    /// <summary>
    /// 🔴 P5（用户参考图②）：**左右长条框 = 5／6 号位（后排）** —— 数据来自内核投影（`UnitProjection.Slot`）；
    /// **空位如实显示"（空）"**（红线 21：不留不可解释的空）✓
    /// </summary>
    private void RefreshBackSlots()
    {
        FillBackSlots(_slotLeft, new[] { 5, 6 });   // 🔴 5/6 号位同框（用户要求）✓
    }

    /// <summary>填一个长条框：标题 + 立绘留框(色块占位) + 名字；空位 ⇒ 如实"（空）"✓</summary>
    private void FillBackSlots(PanelContainer? box, int[] slots)
    {
        if (box is null || !GodotObject.IsInstanceValid(box))
        {
            return;
        }

        foreach (Node old in box.GetChildren().ToArray())
        {
            box.RemoveChild(old);
            old.QueueFree();
        }

        var col = new VBoxContainer { Name = "BackSlotsCol" };
        col.AddThemeConstantOverride("separation", 6);
        box.AddChild(col);

        var players = _view.Units(true).ToArray();

        foreach (int slot in slots)
        {
            // 5/6 号位行（2 处同构）实例化模板 scenes/ui/slot_row.tscn（用户 2026-09-17 复用规则）
            Darkest.UI.SlotRowTemplate? slotRow = Darkest.UI.SlotRowTemplate.TryCreate(slot);
            VBoxContainer row = slotRow ?? new VBoxContainer { Name = $"BackSlot{slot}Row" };
            if (slotRow is null)
            {
                row.AddThemeConstantOverride("separation", 2);
            }

            col.AddChild(row);

            var title = new Label { Name = $"BackSlot{slot}Title", Text = $"{slot} 号位" };
            title.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
            row.AddChild(title);

            var u = players.FirstOrDefault(x => x.Slot == slot);
            if (u is null || u.UnitId == "-")
            {
                row.AddChild(new Label { Name = $"BackSlot{slot}Empty", Text = "（空）" });
                continue;
            }

        string display = NameOf(u.Archetype.Length > 0 ? u.Archetype : u.UnitId);
        var frame = new PanelContainer { Name = $"BackSlot{slot}Frame", CustomMinimumSize = new Vector2(26, 26) };
        row.AddChild(frame);
        frame.AddChild(new ColorRect
        {
            Name = "Placeholder",
            Color = WithPlaceholderAlpha(Darkest.UI.DdTheme.ArchetypeColor(u.Archetype.Length > 0 ? u.Archetype : u.UnitId, isPlayer: true)),   // 🔴 规则②：α 取调色板
        });
        var nameLabel = new Label { Name = $"BackSlot{slot}Name", Text = display, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        nameLabel.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontSmall);
        row.AddChild(nameLabel);
        box.TooltipText = $"{slot} 号位：{display}　HP {u.Hp}/{u.MaxHp}　士气 {u.Morale}";
        }
    }

    /// <summary>🔴 策划 `#348`③：战斗单帧占位 —— 统一走共享入口 `HeroArt.CombatTexture()`（**只从 PlaceholderRoot 读**）✓
    /// ⚠️ V6 纪律：只证明"接口能装下 + UI 能显示"，**不证明**"动画能播"（需 Spine，本阶段裁掉）✓</summary>
    private static Texture2D? PlaceholderCombatTexture() => Darkest.UI.HeroArt.CombatTexture();
    /// <summary>🔴 用户规则②：**保留色相、只把 α 换成调色板里的占位透明度**（空闲位半透明 ⇒ 一眼看出"待填"）✓</summary>
    private static Color WithPlaceholderAlpha(Color hue)
    {
        hue.A = Darkest.UI.DdTheme.PlaceholderFill.A;
        return hue;
    }

}
