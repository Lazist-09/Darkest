using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **城池 · 弹窗工厂与菜单族**（`MakePopup` 模态工厂 · `Esc` 关闭 · `CloseTopPopup` · 二级菜单 `OpenHamletMenu`）✓
/// ③ 🔴 依赖主类私有成员/状态：`_hamletMenu` · `_hamletMenuBody` · `_detailPanel` · `_buildingPopup` · `_menuButton` ·
///    `_buildingEntry` · `_buildingIds` · `_buildingLabels`（弹窗一律**不透明**、必带 ✕、`Esc` 也能关）✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 🔴 **二级窗口（弹窗）工厂**（用户 2026-09-14：「弹窗要能打开、也要能关闭」）：
    /// 满屏不透明 `PanelContainer`（判据据此把它认成**模态** ⇒ 只审它内部）+ 标题行（**含 `✕ 关闭` 按钮**）+ 内容 `VBox`。
    /// ⚠️ 两条纪律：① **必须显式挂 Theme**（本屏根是 `Node2D`，主题链不经过它 ⇒ 否则框是引擎默认 `a=0.6`）
    ///            ② **`Esc`（`ui_cancel`）也必须能关**（"能开不能关"是弹窗最常见的坑）✓
    /// </summary>
    private (PanelContainer Panel, Label Title, VBoxContainer Body) MakePopup(string name, string titleText)
    {
        // 🔴 Track 3（DD `fe_flow/overlays`）：**模态统一住 Overlay 层**（缺失则回落旧父容器，不崩不静默）✓
        _overlay ??= Darkest.UI.OverlayLayer.TryInstantiate();
        if (_overlay is not null && _overlay.GetParent() is null)
        {
            AddChild(_overlay);
        }
        // 🔴 Track 3：**模态优先用模板** `scenes/ui/modal_dialog.tscn`（外观在编辑器可改）；
        //    模板缺失/节点缺失 ⇒ **回落下面原有的代码构建**（不崩不静默）✓ 返回契约不变（Panel/Title/Body）✓
        (PanelContainer? tplPanel, VBoxContainer? tplBody) = Darkest.UI.ModalDialogTemplate.TryCreate(titleText);
        if (tplPanel is not null && tplBody is not null &&
            tplPanel.GetNodeOrNull<Label>("DialogCol/DialogTitleRow/DialogTitle") is Label tplTitle)
        {
            tplPanel.Name = name;
            tplPanel.Visible = false;
            tplPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            tplTitle.ThemeTypeVariation = Darkest.UI.DdTheme.TitleVariation;   // 与代码构建路径一致（标题 Bold）✓
            PanelContainer tplLocal = tplPanel;
            Darkest.UI.ModalDialogTemplate.BindClose(tplPanel, () =>
            {
                tplLocal.Visible = false;
                GD.Print($"[HamletRoot] {name} 关闭（模板 ✕）✓");
            });
            (_overlay?.ModalHost ?? tplPanel.GetParent()!).AddChild(tplPanel);
            return (tplPanel, tplTitle, tplBody);
        }

        var panel = new PanelContainer { Name = name, Visible = false };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Darkest.UI.DdTheme.Apply(panel);
        (_overlay?.ModalHost ?? panel.GetParent()!).AddChild(panel);

        var margin = new MarginContainer();
        foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 16);
        }

        panel.AddChild(margin);

        var col = new VBoxContainer { Name = $"{name}Col" };
        col.AddThemeConstantOverride("separation", 8);
        margin.AddChild(col);

        var head = new HBoxContainer { Name = $"{name}Head" };
        head.AddThemeConstantOverride("separation", 10);
        col.AddChild(head);

        var title = new Label
        {
            Text = titleText,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.ThemeTypeVariation = Darkest.UI.DdTheme.TitleVariation; // 🔴 架构裁定②：标题用 Bold（不再逐处写字号）
        head.AddChild(title);

        // 🔴 关闭按钮（弹窗的"出口"必须显式可见 —— 红线 21：不留不可解释的状态）
        var close = new Button { Name = $"{name}Close", Text = "✕ 关闭", CustomMinimumSize = new Vector2(110, 34) };
        close.Pressed += () =>
        {
            panel.Visible = false;
            _buildingPopupId = null;
            GD.Print($"[HamletRoot] {name} 关闭（✕ 按钮）⇒ 回到城池");
        };
        head.AddChild(close);

        col.AddChild(new HSeparator());

        var body = new VBoxContainer { Name = $"{name}Body", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        col.AddChild(body);
        return (panel, title, body);
    }

    /// <summary>🔴 `Esc`（`ui_cancel`，引擎内置动作）关最上层弹窗 —— 与 `✕ 关闭` 等价的第二条出口 ✓</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") && CloseTopPopup())
        {
            GetViewport().SetInputAsHandled(); // 只吃这一下（不阻塞其它输入路径）
        }
    }

    /// <summary>关掉最上层的弹窗（建筑弹窗 → 角色详情）；返回是否真的关了一个。</summary>
    public bool CloseTopPopup()
    {
        if (_buildingPopup is { Visible: true })
        {
            _buildingPopup.Visible = false;
            _buildingPopupId = null;
            GD.Print("[HamletRoot] 建筑弹窗关闭（Esc）⇒ 回到城池");
            return true;
        }

        if (_detailPanel is { Visible: true })
        {
            CloseHeroDetail();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 🔴 P2：**城池二级菜单**（用户参考图①"最下方资源 UI 可点开二级菜单；库存/装备/角色详情/建筑都是二级菜单"）✓
    /// 纪律（红线 21）：**不可用的项直接不显示**（不留"点了没反应"的禁用），并在日志里**说明为什么少了一项** ✓
    /// </summary>
    public void OpenHamletMenu()
    {
        GD.Print("[UI-TRACE] hamlet-menu-open");   // ASCII 留痕（供 ui_sweep 断言：避免 PS5.1 读中文的编码坑）
        if (_hamletMenu is null)
        {
            (PanelContainer panel, Label title, VBoxContainer body) = MakePopup("HamletMenu", "☰ 【城池菜单】");
            _hamletMenu = panel;
            _hamletMenuBody = body;
        }

        foreach (Node child in _hamletMenuBody!.GetChildren().ToArray())
        {
            _hamletMenuBody.RemoveChild(child);
            child.QueueFree();
        }

        // ① 建筑（始终可用）
        var bBuilding = new Button { Name = "Menu_Building", Text = "🏛 建筑", CustomMinimumSize = new Vector2(320, 34) };
        bBuilding.Pressed += () =>
        {
            _hamletMenu!.Visible = false;
            OpenBuildingPopup(_buildingIds.Length > 0 ? _buildingIds[0] : "tavern");
        };
        _hamletMenuBody.AddChild(bBuilding);

        // ② 角色详情（有名册才可用；用"当前选中"或士气最低者作默认）
        Roster? rosterNow = ExpeditionContext.Roster;
        string? heroPick = _selectedHero ?? rosterNow?.Heroes.OrderBy(h => rosterNow.MoraleOf(h.Id)).FirstOrDefault()?.Id;
        if (heroPick is not null)
        {
            var bHero = new Button { Name = "Menu_HeroDetail", Text = "👤 角色详情", CustomMinimumSize = new Vector2(320, 34) };
            string pick = heroPick;
            bHero.Pressed += () =>
            {
                _hamletMenu!.Visible = false;
                OpenHeroDetail(pick);
            };
            _hamletMenuBody.AddChild(bHero);
        }

        // ③ 库存（**只在本趟有背包时**才出现 —— 否则不显示，也不假装可用）✓
        Darkest.Gameplay.Sim.Run.Inventory? bag = ExpeditionContext.Flow?.Bag;
        if (bag is not null)
        {
            var bBag = new Button { Name = "Menu_Inventory", Text = "🎒 库存", CustomMinimumSize = new Vector2(320, 34) };
            bBag.Pressed += () =>
            {
                _hamletMenu!.Visible = false;
                GD.Print($"[城池菜单] 库存：本趟背包 {bag.Slots.Count}/{bag.SlotCap}（详情面板在远征层；此处先只报读数）");
            };
            _hamletMenuBody.AddChild(bBag);

        var bProvision = new Button { Name = "Menu_Provision", Text = "🛒 供应", CustomMinimumSize = new Vector2(220, 32) };   // DD 1:1 ②：供应屏入口
        bProvision.Pressed += () => OpenProvision();
        _hamletMenuBody.AddChild(bProvision);

        var bQuest = new Button { Name = "Menu_QuestSelect", Text = "📜 任务选择", CustomMinimumSize = new Vector2(220, 32) };   // DD 1:1 ②：任务选择入口
        bQuest.Pressed += () => OpenQuestSelect();
        _hamletMenuBody.AddChild(bQuest);
        }
        else
        {
            GD.Print("[城池菜单] 库存：**本趟无背包**（`ExpeditionContext.Flow` 为空）⇒ 不显示该项（红线 21：不假装可用）✓");
        }

        // 🔴 用户要求：**主屏的减压/招募/服务/状态文字 ⇒ 一律进【建筑详情】**（只搬一次）✓
        if (_buildingPopupBody is not null)
        {
            foreach (Control? extra in new Control?[] { _reliefRow, _recruitRow, _saniRow, _buildingInfo, _upgradeStatus, _saniStatus })
            {
                if (extra is not null && extra.GetParent() is null)
                {
                    _buildingPopupBody.AddChild(extra);
                }
            }
        }

        _hamletMenu.Visible = true;
        GD.Print($"[城池菜单] 打开：建筑{(heroPick is null ? "" : " ／ 角色详情")}{(bag is null ? "" : " ／ 库存")}" +
                 $"（只列**当前可用**项）✓");
    }
}
