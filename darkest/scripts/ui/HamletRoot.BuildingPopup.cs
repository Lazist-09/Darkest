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
/// ② 本文件 = **城池 · 建筑详情弹窗族**（打开 / 内容刷新 / 内容行工厂 `PopupLine` / 信息行 `ShowBuildingInfo`）✓
/// ③ 🔴 依赖主类私有成员/状态：`_buildingPopup` · `_buildingPopupTitle` · `_buildingPopupBody` · `_buildingPopupId` ·
///    `_buildingInfo` · `_buildingIds` · `_buildingLabels` · `_upgradeButtons` ✓
/// ④ **只搬家、零行为改动**（读数对照见提交信息）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>🔴 打开**建筑详情弹窗**（二级窗口）：功能 ／ 当前等级 ／ 下一级所需 ／ **升级按钮** ／ 关闭 ✓
    /// ⚠️ 以前"点一下就升级"（无确认）；现在点建筑 = **打开详情**，升级是弹窗里的**显式动作** ✓</summary>
    public void OpenBuildingPopup(string building)
    {
        HeirloomStock? h = ExpeditionContext.Heirlooms;
        if (h is null)
        {
            GD.Print("[HamletRoot] 建筑弹窗：传家宝库存未加载 ⇒ 不开（如实拒绝，不静默）");
            return;
        }

        if (_buildingPopup is null)
        {
            (PanelContainer panel, Label title, VBoxContainer body) = MakePopup("BuildingPopup", "🏛 【建筑】", Darkest.UI.PopupLayout.Building);
            _buildingPopup = panel;
            _buildingPopupTitle = title;

            // 🔴 骨架优先：建筑详情的**内容布局**从 `scenes/ui/building_popup.tscn` 取
            //    ⇒ 662x764 局部像素空间、三处 DD 锚点各承一类内容，**全部可在编辑器里改** ✓
            //    ⚠️ 场景缺失/类型不符 ⇒ **回落代码构建**（不崩、不静默）
            //    🔴 DD 分工：`BpBodyAnchor`=正文 ／ `BpUpgradeAnchor`=升级按钮 ／ `BpTreesAnchor`=升级树
            //       （节点名贯通 DD 的 body_base_pos 596,102 / upgrade_base_pos 172,259 / upgrade_trees_offset→172,454）✓
            Darkest.UI.BuildingPopupSkeleton? bpSkel = Darkest.UI.BuildingPopupSkeleton.TryInstantiate();
            VBoxContainer bodyHost;
            if (bpSkel is not null)
            {
                body.AddChild(bpSkel);
                bodyHost = TakeAnchor<VBoxContainer>(bpSkel.BodyAnchor, "BpRuntimeBody");
                _buildingPopupUpgrade = TakeAnchor<VBoxContainer>(bpSkel.UpgradeAnchor, "BpRuntimeUpgrade");
                _buildingPopupTrees = TakeAnchor<HBoxContainer>(bpSkel.TreesAnchor, "BpRuntimeTrees");
                if (_buildingPopupTrees is not null)
                {
                    _buildingPopupTrees.AddThemeConstantOverride("separation", 6);
                }
                GD.Print("[UI 建筑弹窗] 采用 662x764 骨架三锚点（正文/升级按钮/升级树）✓");
            }
            else
            {
                // 回落：三区退化为同一竖列（DD 布局全部丢失 ⇒ 如实留痕，由 syslog 可见）
                GD.Print("[UI 建筑弹窗] 骨架不可用 ⇒ 回落单列布局（三区合一，非 DD 布局）");
                bodyHost = new VBoxContainer { Name = "BuildingFallback" };
                bodyHost.AddThemeConstantOverride("separation", 6);
                body.AddChild(bodyHost);
                _buildingPopupUpgrade = bodyHost;
                _buildingPopupTrees = new HBoxContainer { Name = "BpRuntimeTrees" };
                _buildingPopupTrees.AddThemeConstantOverride("separation", 6);
                bodyHost.AddChild(_buildingPopupTrees);
            }

            _buildingPopupBody = bodyHost; // 正文区（骨架锚点 or 回落容器）✓
        }

        _buildingPopupId = building;
        RefreshBuildingPopup();
        _buildingPopup.Visible = true;
        GD.Print($"[HamletRoot] 建筑弹窗打开：{building}（可关：✕ 按钮 ／ Esc）");
    }

    /// <summary>填充建筑弹窗内容（**真读 `HeirloomStock`**，不写死）；升级按钮的可用性由内核持有者回答 ✓</summary>
    private void RefreshBuildingPopup()
    {
        if (_buildingPopupBody is null || _buildingPopupTitle is null || _buildingPopupId is null)
        {
            return;
        }

        HeirloomStock? h = ExpeditionContext.Heirlooms;
        if (h is null)
        {
            return;
        }

        string building = _buildingPopupId;
        // 🔴 建筑名取自**同一份清单**（`_buildingIds` / `_buildingLabels`），此处不抄第二份（P3 纪律）✓
        int idx = Array.IndexOf(_buildingIds, building);
        string label = idx >= 0 ? _buildingLabels[idx] : building;

        // 🔴 2026-09-27 修假绿：清空气前先把**复用的服务行**摘下来（只 RemoveChild、不 QueueFree）✓
        //    否则下面这条清空循环会把它们 `QueueFree` ⇒ 下次刷新再挂上就是**已销毁的野节点**（崩/静默）
        foreach (Control? row in new Control?[] { _reliefRow, _recruitRow, _saniRow })
        {
            if (row is not null && row.GetParent() is not null)
            {
                row.GetParent().RemoveChild(row);
            }
        }

        // 先**摘除**旧内容（`RemoveChild` 立即生效 ⇒ 不会与新建内容同帧并存、判据也不会误报重叠）✓
        foreach (Node child in _buildingPopupBody.GetChildren().ToArray())
        {
            _buildingPopupBody.RemoveChild(child);
            child.QueueFree();
        }

        _buildingPopupTitle.Text = $"🏛 【{label}】";

        string func = building switch
        {
            "tavern" => "减压 · 酒馆（快而不稳）",
            "abbey" => "减压 · 修道院（慢而稳）",
            "stagecoach" => "招募新兵（免费 / Lv1 / 士气 50）",
            // 🆕 2026-10-01 解冻窗口：铁匠铺两条升级树（H-1 前置 = 本树等级被 `HeroGear` 读作装备升级门槛）✓
            "blacksmith.weapon" => "装备升级 · 武器轴（H-1 前置树 blacksmith.weapon）",
            "blacksmith.armour" => "装备升级 · 护甲轴（H-1 前置树 blacksmith.armour）",
            _ => "—",
        };
        UpgradeLevel? next = h.NextLevel(building);
        int level = h.LevelOf(building);
        string nextText = next is null
            ? "（已满级）"
            : string.Join(" ＋ ", next.Cost.Select(k => $"{k.Key}×{k.Value}")) + $"　⇒ Lv{level + 1}";

        _buildingPopupBody.AddChild(PopupLine($"功能：{func}"));
        _buildingPopupBody.AddChild(PopupLine($"当前等级：Lv{level}"));
        _buildingPopupBody.AddChild(PopupLine($"下一级所需：{nextText}"));

        // 🔴 2026-09-27 修假绿：**服务行改由建筑弹窗自己挂**（旧逻辑挂在「城池菜单」里，
        //    而菜单打开时 `_buildingPopupBody` 必为 null ⇒ 三行永远游离在树外，玩家点不到）✓
        MountServiceRow(building);

        // 🔴 升级 = 弹窗里的**显式动作**（不再是"点建筑就升级"）
        // 🔴 升级 = 弹窗里的**显式动作**（不再是"点建筑就升级"）；
        //    可用性**复用内核的同一入口** `HeirloomStock.CanUpgrade`（红线 21 (b)：由内核回答，UI 不在本地重算）✓
        bool affordable = next is not null && h.CanUpgrade(building);
        // 🔴 DD 1:1 ①【升级树】照 `building.layout.darkest` 的 `.upgrade_trees_offset 0 195`：等级链三态（已达成/下一级/未达成）
        //    数据全部用**已有** `HeirloomStock.LevelOf` 与 `NextLevel().Cost`（不新造数字）✓
        HBoxContainer tree = _buildingPopupTrees ?? new HBoxContainer { Name = "UpgradeTree" };   // DD 1:1：骨架锚点优先，缺失才代码建
        foreach (Node old in tree.GetChildren()) { old.Free(); }   // 换一栋 ⇒ 清空重填（复用同一节点，不重复挂载）
        int curLv = h.LevelOf(building);
        int shownLv = curLv + 2;   // 展示 0..当前+2（保守：不虚构更高上限）
        for (int lv = 0; lv <= shownLv; lv++)
        {
            string tip = lv <= curLv
                ? $"Lv{lv}：已达成"
                : (lv == curLv + 1 ? $"Lv{lv}：下一级（所需 {nextText}）" : $"Lv{lv}：尚未可达（先升到 Lv{lv - 1}）");
            var node = new PanelContainer { Name = $"UpgradeNode{lv}", CustomMinimumSize = new Vector2(26, 26), TooltipText = tip };
            node.Modulate = lv <= curLv
                ? Darkest.UI.DdTheme.Highlight
                : (lv == curLv + 1 ? Darkest.UI.DdTheme.Danger : Darkest.UI.DdTheme.Disabled);
            node.AddChild(new ColorRect { Name = "NodeFill", Color = Darkest.UI.DdTheme.PlaceholderFill });
            tree.AddChild(node);
        }
        if (tree.GetParent() is null) { _buildingPopupBody.AddChild(tree); }
        GD.Print($"[UI 建筑弹窗] OK DD 升级树就位：{building} 当前 Lv{curLv} · 节点 {shownLv + 1} 个（DD upgrade_trees，数据同源）");

        // 🔴 2026-09-27 修假绿：升级按钮**每次刷新都新建一个并 AddChild**，从不清理 ⇒ 升一次级就多一颗重复按钮
        //    ⚠️ 回落布局下 `_buildingPopupUpgrade` 就是正文容器本身（内部还挂着升级树）⇒ **必须排除**，不能误清
        if (_buildingPopupUpgrade is not null && _buildingPopupUpgrade != _buildingPopupBody)
        {
            foreach (Node old in _buildingPopupUpgrade.GetChildren().ToArray())
            {
                _buildingPopupUpgrade.RemoveChild(old);
                old.QueueFree();
            }
        }

        var upgrade = new Button
        {
            Name = "PopupUpgrade",
            Text = next is null ? "已满级" : $"升级到 Lv{level + 1}",
            CustomMinimumSize = new Vector2(220, 40),
            Disabled = !affordable,
            TooltipText = next is null
                ? "已满级"
                : affordable ? "消耗上列传家宝升级" : "传家宝不足（灰色 = 不可用，悬停看原因 —— 红线 21）",
        };
        upgrade.Pressed += () =>
        {
            UpgradeBuilding(building);
            RefreshBuildingPopup(); // 等级/花费随之刷新（弹窗不关，玩家能连续看）
            Refresh();
        };
        _buildingPopupUpgrade?.AddChild(upgrade);   // DD 布局：升级按钮落 `BpUpgradeAnchor`（非正文列）✓

        // 🔴 P3 文案精简：去掉"怎么关窗"的提示行（关闭按钮与 Esc 已自明）✓
        GD.Print($"[HamletRoot] 建筑弹窗内容：{label} Lv{level}　下一级 {nextText}　可升级={affordable}");
    }

    /// <summary>
    /// 🔴 2026-09-27 修假绿：把**本栋建筑对应的服务行**挂进弹窗正文区（每栋只挂自己相关的那一行）✓
    /// ⚠️ 行是**复用节点**（`Build` 里只建一次）⇒ 只 `AddChild / Reparent`，**绝不重复 new**（否则 `Pressed` 会重复接线）
    /// ⚠️ `sanitarium` **不在此挂载** —— 内核裁定它是**服务**不是**可升级建筑**
    ///    （`RunStartSnapshot.cs:43`：`HeirloomStock.LevelOf("sanitarium")` 会**抛"未知建筑"**打断整条进地牢流程）
    ///    ⇒ 疗养三键改挂「城池菜单」，见 `HamletRoot.PopupMenu.cs` ✓
    /// </summary>
    private void MountServiceRow(string building)
    {
        if (_buildingPopupBody is null) { return; }

        Control? row = building switch
        {
            "tavern" => _reliefRow,
            "abbey" => _reliefRow,
            "stagecoach" => _recruitRow,
            _ => null,
        };
        if (row is null) { return; }

        // 减压：两栋同价同效、风险不同 ⇒ 只显示**本栋**那颗按钮（隐藏另一颗，避免"点开 A 建筑却触发 B 服务"的歧义）
        Button? tavernBtn = row.GetNodeOrNull<Button>("ReliefTavern");
        Button? abbeyBtn = row.GetNodeOrNull<Button>("ReliefAbbey");
        if (tavernBtn is not null) { tavernBtn.Visible = building == "tavern"; }
        if (abbeyBtn is not null) { abbeyBtn.Visible = building == "abbey"; }

        if (row.GetParent() is null) { _buildingPopupBody.AddChild(row); }
        else if (row.GetParent() != _buildingPopupBody) { row.Reparent(_buildingPopupBody); }
    }

    /// <summary>
    /// 🔴 UI 编辑器化 B（用户 2026-09-17）：**弹窗内容行改为实例化模板场景** `scenes/ui/popup_line.tscn`
    /// ⇒ 换行方式/裁切/字号/颜色**在编辑器里直接改**（改那一行 = 改所有弹窗行）✓
    /// ⚠️ 场景缺失/类型不符 ⇒ **回落代码构建**（不崩、不静默）✓
    /// </summary>
    private static Label PopupLine(string text)
        => Darkest.UI.PopupLineTemplate.TryCreate(text) ?? new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 20),
        };

    /// <summary>
    /// 🔴 接管 DD 骨架锚点：锚点容器内的**占位**（`PurposeLabel` + `BlockPlaceholder`）在**数据接入时**让位给真实内容，
    ///    返回宿主容器承载动态行。
    ///    ⚠️ 硬规矩 §14.0.68 原文是「数据未接入 ⇒ 占位不换不删」——本方法只在**真接数据**的锚点上调用 ✓
    ///    ⚠️ 锚点缺失 ⇒ **打留痕并返回游离容器**（红线 21：如实上报，不静默丢弃）
    /// </summary>
    private static T TakeAnchor<T>(PanelContainer? anchor, string name) where T : Control, new()
    {
        T host = new() { Name = name };
        if (anchor is null)
        {
            GD.Print($"[UI 骨架] 建筑弹窗缺锚点 `{name}` ⇒ 宿主容器游离（内容可能不可见，红线 21 如实上报）");
            return host;
        }

        foreach (Node old in anchor.GetChildren().ToArray())
        {
            anchor.RemoveChild(old);
            old.QueueFree();
        }

        anchor.AddChild(host);
        return host;
    }

    public void ShowBuildingInfo(string building)
    {
        GD.Print("[UI-TRACE] hamlet-hover");   // ASCII 留痕（供 ui_sweep 断言：避免 PS5.1 读中文的编码坑）
        HeirloomStock? h = ExpeditionContext.Heirlooms;
        if (h is null)
        {
            _buildingInfo.Text = "建筑：传家宝库存未加载";
            return;
        }

        string func = building switch
        {
            "tavern" => "减压·酒馆（快而不稳）",
            "abbey" => "减压·修道院（慢而稳）",
            "stagecoach" => "招募新兵（免费 / Lv1 / 士气 50）",
            // 🆕 2026-10-01 解冻窗口：铁匠铺两条升级树（与 RefreshBuildingPopup 同一份口径）✓
            "blacksmith.weapon" => "装备升级 · 武器轴（H-1 前置树 blacksmith.weapon）",
            "blacksmith.armour" => "装备升级 · 护甲轴（H-1 前置树 blacksmith.armour）",
            _ => "—",
        };
        UpgradeLevel? next = h.NextLevel(building);
        string nextText = next is null
            ? "已满级"
            : string.Join(" ＋ ", next.Cost.Select(k => $"{k.Key}×{k.Value}")) +
              $"　⇒ Lv{h.LevelOf(building) + 1}";
        // 🔴 建筑名取自**同一份清单**（`_buildingIds` / `_buildingLabels`），此处不抄第二份（P3 纪律）✓
        int infoIdx = Array.IndexOf(_buildingIds, building);
        string infoLabel = infoIdx >= 0 ? _buildingLabels[infoIdx] : building;
        _buildingInfo.Text = $"🏛 {infoLabel}　功能：{func}　当前等级：Lv{h.LevelOf(building)}　下一级所需：{nextText}";
        GD.Print($"[HamletRoot] 悬停建筑 {building}：{_buildingInfo.Text}");
    }
}
