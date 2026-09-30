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
    /// 🔴 **面板复用注册表**（2026-09-20 L2/L3 布局整改）✓
    /// 修的是：`QuestSelect` / `Provision` / `HeirloomExchange` / `LootOverlay` **每次开屏都新建**一个满屏面板
    /// ⇒ 四个屏互相重叠堆积、且 `Esc` 不认它们（此前 `CloseTopPopup` 只硬编码认 2 个面板）✓
    /// ⚠️ 这是**最小实现**：只做"同名复用"，**不是**新的面板管理器架构（`UIRoot` 外壳本轮仍不接管）✓
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<string, (PanelContainer Panel, Label Title, VBoxContainer Body)> _popups
        = new(System.StringComparer.Ordinal);

    /// <summary>
    /// 🔴 **二级窗口（弹窗）工厂**（用户 2026-09-14：「弹窗要能打开、也要能关闭」）：
    /// 按 `UILayoutSpec` 档位定位的 `PanelContainer`（**不再一律满屏**）+ 标题行（**含 `✕ 关闭` 按钮**）+ 内容 `VBox`。
    /// ⚠️ 三条纪律：① **必须显式挂 Theme**（本屏根是 `Node2D`，主题链不经过它 ⇒ 否则框是引擎默认 `a=0.6`）
    ///            ② **`Esc`（`ui_cancel`）也必须能关**（"能开不能关"是弹窗最常见的坑）✓
    ///            ③ **同名复用**（不再每次新建 ⇒ 不堆叠）✓
    /// </summary>
    private (PanelContainer Panel, Label Title, VBoxContainer Body) MakePopup(
        string name, string titleText, PopupLayout layout = PopupLayout.Modal)
    {
        // 🔴 ① 同名复用：命中 ⇒ 清空 body 后直接返回（不再新建，杜绝堆叠）✓
        if (_popups.TryGetValue(name, out (PanelContainer Panel, Label Title, VBoxContainer Body) existed))
        {
            foreach (Node child in existed.Body.GetChildren().ToArray())
            {
                existed.Body.RemoveChild(child);
                child.QueueFree();
            }

            existed.Title.Text = titleText;
            Darkest.UI.UILayoutSpec.Place(existed.Panel, layout);
            OpenInOverlay(existed.Panel, name);
            return existed;
        }

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
            tplTitle.ThemeTypeVariation = Darkest.UI.DdTheme.TitleVariation;   // 与代码构建路径一致（标题 Bold）✓
            PanelContainer tplLocal = tplPanel;
            Darkest.UI.ModalDialogTemplate.BindClose(tplPanel, () =>
            {
                // 🔴 关自己 + **出栈**（此前只 `Visible = false` ⇒ 栈里还留着，遮罩不消失）
                ClosePopup(tplLocal, name);
                GD.Print($"[HamletRoot] {name} 关闭（模板 ✕）✓");
            });
            (_overlay?.ModalHost ?? tplPanel.GetParent()!).AddChild(tplPanel);

            // 🔴 尺寸/位置按档位（不再一律满屏）✓
            Darkest.UI.UILayoutSpec.Place(tplPanel, layout);
            OpenInOverlay(tplPanel, name);
            _popups[name] = (tplPanel, tplTitle, tplBody);
            return (tplPanel, tplTitle, tplBody);
        }

        var panel = new PanelContainer { Name = name, Visible = false };
        Darkest.UI.DdTheme.Apply(panel);
        (_overlay?.ModalHost ?? panel.GetParent()!).AddChild(panel);

        // 🔴 尺寸/位置按档位（此前是 `SetAnchorsAndOffsetsPreset(FullRect)` ⇒ 一律铺满 1920×1080）✓
        Darkest.UI.UILayoutSpec.Place(panel, layout);
        GD.Print($"[UI 布局] `{name}`：{Darkest.UI.UILayoutSpec.Describe(layout)}");

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
            // 🔴 关自己 + **出栈**（此前只 `Visible = false` ⇒ 栈里还留着，遮罩不消失）
            ClosePopup(panel, name);
            _buildingPopupId = null;
            GD.Print($"[HamletRoot] {name} 关闭（✕ 按钮）⇒ 回到城池");
        };
        head.AddChild(close);

        col.AddChild(new HSeparator());

        var body = new VBoxContainer { Name = $"{name}Body", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        col.AddChild(body);

        _popups[name] = (panel, title, body);
        OpenInOverlay(panel, name);
        return (panel, title, body);
    }

    /// <summary>
    /// 🔴 **开屏 = 挂进 Overlay + 压栈**（2026-09-20）✓
    /// 修的是：此前 `MakePopup` 只是 `AddChild(panel)`，**绕过了** `OverlayLayer.OpenModal`
    /// ⇒ `_modals` 恒空 ⇒ `Esc` 兜底失效、遮罩不出现、面板互相堆叠 ✓
    /// </summary>
    private void OpenInOverlay(PanelContainer panel, string name)
    {
        if (_overlay is not null)
        {
            // 🔴 已在栈里 ⇒ 移到栈顶（不重复入栈）；不在 ⇒ 入栈
            _overlay.OpenModal(panel);
        }
        else
        {
            // Overlay 不可用 ⇒ 回落：至少保证可见（不崩不静默，与既有纪律一致）
            panel.Visible = true;
            GD.Print($"[HamletRoot] `{name}`：Overlay 不可用 ⇒ 直接显示（未入栈，Esc 可能关不掉）");
        }
    }

    /// <summary>
    /// 🔴 **关屏 = 隐藏 + 出栈**（2026-09-20）✓
    /// 修的是：此前 ✕ 只 `Visible = false` ⇒ 模态栈里还留着它 ⇒ 遮罩不消失、`Esc` 会关到"隐形"的层 ✓
    /// </summary>
    private void ClosePopup(PanelContainer panel, string name)
    {
        panel.Visible = false;
        if (_overlay is not null)
        {
            _overlay.CloseModal(panel);
        }

        GD.Print($"[HamletRoot] `{name}` 已关闭并出栈（栈深 {_overlay?.ModalDepth ?? 0}）✓");
    }

    /// <summary>🔴 `Esc`（`ui_cancel`，引擎内置动作）关最上层弹窗 —— 与 `✕ 关闭` 等价的第二条出口 ✓</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") && CloseTopPopup())
        {
            GetViewport().SetInputAsHandled(); // 只吃这一下（不阻塞其它输入路径）
        }
    }

    /// <summary>
    /// 关掉最上层的弹窗；返回是否真的关了一个。
    /// 🔴 **优先委托模态栈**（2026-09-20）：栈里有 ⇒ 关栈顶（**任意**面板，不再只认 2 个）✓
    ///    此前是硬编码 if 链，只认 `_buildingPopup` 与 `_detailPanel` ⇒ QuestSelect/Provision/传家宝/战利品 **Esc 关不掉** ✓
    /// </summary>
    public bool CloseTopPopup()
    {
        // ① 栈优先：Overlay 可用且栈非空 ⇒ 关栈顶（含所有后来接进来的面板）
        if (_overlay is not null && _overlay.ModalDepth > 0)
        {
            return _overlay.CloseTopModal();
        }

        // ② 回落：Overlay 不可用（骨架缺失）⇒ 保留旧行为，不至于"能开不能关"
        if (_buildingPopup is { Visible: true })
        {
            ClosePopup(_buildingPopup, "BuildingPopup");
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
            (PanelContainer panel, Label title, VBoxContainer body) = MakePopup("HamletMenu", "☰ 【城池菜单】", Darkest.UI.PopupLayout.Modal);
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
            ClosePopup(_hamletMenu!, "HamletMenu");   // 🔴 关菜单 + 出栈（此前只 Visible=false ⇒ 遮罩不消失）
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
                ClosePopup(_hamletMenu!, "HamletMenu");
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
                ClosePopup(_hamletMenu!, "HamletMenu");
                GD.Print($"[城池菜单] 库存：本趟背包 {bag.Slots.Count}/{bag.SlotCap}（详情面板在远征层；此处先只报读数）");
            };
            _hamletMenuBody.AddChild(bBag);

        var bProvision = new Button { Name = "Menu_Provision", Text = "🛒 供应", CustomMinimumSize = new Vector2(220, 32) };   // DD 1:1 ②：供应屏入口
        bProvision.Pressed += () => { ClosePopup(_hamletMenu!, "HamletMenu"); OpenProvision(); };
        _hamletMenuBody.AddChild(bProvision);

        var bQuest = new Button { Name = "Menu_QuestSelect", Text = "📜 任务选择", CustomMinimumSize = new Vector2(220, 32) };   // DD 1:1 ②：任务选择入口
        bQuest.Pressed += () => { ClosePopup(_hamletMenu!, "HamletMenu"); OpenQuestSelect(); };
        _hamletMenuBody.AddChild(bQuest);

        var bExchange = new Button { Name = "Menu_HeirloomExchange", Text = "💎 传家宝兑换", CustomMinimumSize = new Vector2(220, 32) };   // DD 1:1 P5：传家宝兑换入口
        bExchange.Pressed += () => { ClosePopup(_hamletMenu!, "HamletMenu"); OpenHeirloomExchange(); };
        _hamletMenuBody.AddChild(bExchange);
        }
        else
        {
            GD.Print("[城池菜单] 库存：**本趟无背包**（`ExpeditionContext.Flow` 为空）⇒ 不显示该项（红线 21：不假装可用）✓");
        }

        // 🔴 2026-09-27 修假绿：减压 / 招募已改由**建筑弹窗自己挂**（见 `BuildingPopup.MountServiceRow`）⇒ 这里不再挂 ✓
        //    `sanitarium` 是**服务**而非**可升级建筑**（内核裁定：`HeirloomStock.LevelOf` 对它抛"未知建筑"）
        //    ⇒ 它没有建筑弹窗可挂 ⇒ 疗养三键与状态行挂到**城池菜单**（玩家唯一够得着的地方）✓
        //    ⚠️ 旧代码挂在 `_buildingPopupBody` 上，而菜单打开时该字段**必为 null** ⇒ 三行永远游离在树外（点不到）
        foreach (Control? extra in new Control?[] { _saniRow, _saniStatus })
        {
            if (extra is not null && extra.GetParent() is null)
            {
                _hamletMenuBody?.AddChild(extra);
            }
        }

        _hamletMenu.Visible = true;
        GD.Print($"[城池菜单] 打开：建筑{(heroPick is null ? "" : " ／ 角色详情")}{(bag is null ? "" : " ／ 库存")}" +
                 $"（只列**当前可用**项）✓");
    }
}
