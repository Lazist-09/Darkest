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
            (PanelContainer panel, Label title, VBoxContainer body) = MakePopup("BuildingPopup", "🏛 【建筑】");
            _buildingPopup = panel;
            _buildingPopupTitle = title;

            // 🔴 骨架优先（2026-09-17）：建筑详情的**内容布局**从 `scenes/ui/building_popup.tscn` 取
            //    ⇒ 左列宽 / 店长位高 / 左右间距 / 内容列占比 **在编辑器里直接改** ✓
            //    ⚠️ 场景缺失/类型不符 ⇒ **回落代码构建**（不崩、不静默）
            //    🔴 节点名保持一致：BuildingSplit / BuildingList / ShopkeeperSlot / BuildingContent ✓
            Darkest.UI.BuildingPopupSkeleton? bpSkel = Darkest.UI.BuildingPopupSkeleton.TryInstantiate();
            HBoxContainer split;
            VBoxContainer leftCol;
            VBoxContainer rightCol;
            if (bpSkel is not null)
            {
                body.AddChild(bpSkel);
                split = bpSkel.Split!;
                leftCol = bpSkel.List!;
                rightCol = bpSkel.Content!;
                if (bpSkel.ShopkeeperPlaceholder is ColorRect bpPh)
                {
                    bpPh.Color = Darkest.UI.DdTheme.PlaceholderFill; // 🔴 规则②：半透明占位（α 来自调色板）✓
                }
            }
            else
            {
                split = new HBoxContainer { Name = "BuildingSplit", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
                split.AddThemeConstantOverride("separation", 12);
                body.AddChild(split);

                leftCol = new VBoxContainer { Name = "BuildingList", CustomMinimumSize = new Vector2(230, 0) };
                leftCol.AddThemeConstantOverride("separation", 6);
                split.AddChild(leftCol);

                // 🔴 **店长位留框**（用户原话"为店长位置留一个空间"）：不透明面板样式(1px 边框) + **色块占位** ✓
                var shopFrame = new PanelContainer { Name = "ShopkeeperSlot", CustomMinimumSize = new Vector2(0, 96) };
                leftCol.AddChild(shopFrame);
                // 🔴 规则②：空闲位改**半透明占位**（α 来自调色板 `PlaceholderFill`）✓
                shopFrame.AddChild(new ColorRect { Name = "ShopkeeperPlaceholder", Color = Darkest.UI.DdTheme.PlaceholderFill });

                rightCol = new VBoxContainer { Name = "BuildingContent", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                rightCol.AddThemeConstantOverride("separation", 6);
                split.AddChild(rightCol);
            }

            for (int k = 0; k < _buildingIds.Length; k++)
            {
                string bid = _buildingIds[k];
                // 🔴 用户要求（2026-09-17）：**重复元素抽模板** ⇒ 建筑切换按钮（3 处同构）实例化 `building_nav_button.tscn`
                //    ⚠️ 场景缺失 ⇒ **回落代码构建**（不崩、不静默）；节点名 `BuildingNav_<id>` 保持不变 ✓
                Button nav = Darkest.UI.BuildingNavButtonTemplate.TryCreate(_buildingLabels[k])
                    ?? new Button { Text = _buildingLabels[k], CustomMinimumSize = new Vector2(220, 32) };
                nav.Name = $"BuildingNav_{bid}";
                nav.Pressed += () => { _buildingPopupId = bid; RefreshBuildingPopup(); }; // 🔴 左列切换（只换右侧内容）✓
                leftCol.AddChild(nav);
            }

            _buildingPopupBody = rightCol; // 右侧 = 建筑内容（骨架或回落）✓
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
        string label = building switch
        {
            "tavern" => "酒馆 Tavern",
            "abbey" => "修道院 Abbey",
            "stagecoach" => "驿站 Stage Coach",
            _ => building,
        };

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

        // 🔴 升级 = 弹窗里的**显式动作**（不再是"点建筑就升级"）
        // 🔴 升级 = 弹窗里的**显式动作**（不再是"点建筑就升级"）；
        //    可用性**复用内核的同一入口** `HeirloomStock.CanUpgrade`（红线 21 (b)：由内核回答，UI 不在本地重算）✓
        bool affordable = next is not null && h.CanUpgrade(building);
        // 🔴 DD 1:1 ①【升级树】照 `building.layout.darkest` 的 `.upgrade_trees_offset 0 195`：等级链三态（已达成/下一级/未达成）
        //    数据全部用**已有** `HeirloomStock.LevelOf` 与 `NextLevel().Cost`（不新造数字）✓
        var tree = new HBoxContainer { Name = "UpgradeTree" };
        tree.AddThemeConstantOverride("separation", 6);
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
        _buildingPopupBody.AddChild(tree);
        GD.Print($"[UI 建筑弹窗] OK DD 升级树就位：{building} 当前 Lv{curLv} · 节点 {shownLv + 1} 个（DD upgrade_trees，数据同源）");

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
        _buildingPopupBody.AddChild(upgrade);

        // 🔴 P3 文案精简：去掉"怎么关窗"的提示行（关闭按钮与 Esc 已自明）✓
        GD.Print($"[HamletRoot] 建筑弹窗内容：{label} Lv{level}　下一级 {nextText}　可升级={affordable}");
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

    public void ShowBuildingInfo(string building)
    {
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
            _ => "—",
        };
        UpgradeLevel? next = h.NextLevel(building);
        string nextText = next is null
            ? "已满级"
            : string.Join(" ＋ ", next.Cost.Select(k => $"{k.Key}×{k.Value}")) +
              $"　⇒ Lv{h.LevelOf(building) + 1}";
        _buildingInfo.Text = $"🏛 {building}　功能：{func}　当前等级：Lv{h.LevelOf(building)}　下一级所需：{nextText}";
        GD.Print($"[HamletRoot] 悬停建筑 {building}：{_buildingInfo.Text}");
    }
}
