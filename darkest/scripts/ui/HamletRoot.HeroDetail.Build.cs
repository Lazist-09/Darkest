using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律（用户 2026-09-21）：一律 Darkest.UI（大写 UI），不得写成 Ui ✓

/// <summary>
/// ① 从 `HamletRoot.HeroDetail.cs` 拆出（用户红线 28；2026-10-02 第十件）✓
/// ② 本文件 = **城池 · 角色详情【懒建整树】**：`HeroDetailPanel`（Sheet 版式 · 左 0.38 ／ 右 0.62）· 骨架优先 (`HeroDetailSkeleton`) ／
///    回落代码建 · 状态条 + 六属性块 · 装备/饰品孔行 · 推荐留框 · 扎营技能列 · `返回城池` 按钮 ✓
/// ③ 🔴 依赖主类私有成员：**写** `_detailPanel`(`HamletRoot.cs:86`) ／ `_detailLeft`(`:87`) ／ `_detailRight`(`:88`) ／ `_detailCampSkills`(`:89`) ／
///    `_detailQuirks`(`:90`) ／ `_detailSkills`(`:91`) ／ `_detailRecommend`(`:92`) ／ `_detailTrinketGrid`(`HamletRoot.HeroDetail.Trinkets.cs:36`)；
///    **读** `_rosterCfgForDetail`(`HamletRoot.cs:52`) ／ `_unitsCfg`(`:54`) ／ 公有面 `ExpeditionContext.Roster`(`:170` 取士气)；
///    **调** `EnsureTrinketSlots`(`HeroDetail.Trinkets.cs:53`) ／ `CloseHeroDetail`(同族门面) ✓
/// ④ **只搬家、零行为改动**：原 `OpenHeroDetail` 内 `if (_detailPanel is null) { … }`（:40-295）**逐字节原样**（含原有不规整缩进）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>
    /// 懒建整棵详情面板（只在首次打开时有活）：方法内**原样保留** `if (_detailPanel is null)` 守卫 ⇒ 可重复调用（幂等）✓
    /// 🔴 调用点 = `OpenHeroDetail`（`HamletRoot.HeroDetail.cs`），必须在 `RefreshHeroDetail` **之前**（与原内序一致）✓
    /// </summary>
    private void BuildHeroDetailPanel(HeroConfig hero, string heroId)
    {
        if (_detailPanel is null)
        {
            // 🔴 2026-09-20 L2/L3 布局整改：**不再满屏** ——
            //    改按 DD `shared/character/character.layout.darkest`：**1395×776 @ (144,132)**（左偏，不居中）✓
            //    数值实测：`.character_pos 144 132` + `characterpanel_bg.png` = 1395×776 ✓
            //    ⚠️ 此前是 `SetAnchorsAndOffsetsPreset(FullRect)` ⇒ 铺满 1920×1080，挡住整个主城 ✓
            var detailRoot = new PanelContainer { Name = "HeroDetailPanel", Visible = false };
            // ⚠️ **必须显式挂 Theme**：本节点是 `HamletRoot`(Node2D) 的子节点，**不在带 Theme 的 `margin` 之下**
            //    ⇒ 否则它用**引擎默认面板样式**（实测 `a=0.6` ⇒ 判据 2 直接抓到"框透明"）✓
            Darkest.UI.DdTheme.Apply(detailRoot);
            AddChild(detailRoot);
            Darkest.UI.UILayoutSpec.Place(detailRoot, Darkest.UI.PopupLayout.Sheet);
            GD.Print($"[UI 布局] HeroDetailPanel：{Darkest.UI.UILayoutSpec.Describe(Darkest.UI.PopupLayout.Sheet)}");
            _detailPanel = detailRoot;

            var dMargin = new MarginContainer { Name = "DetailMargin" };
            dMargin.AddThemeConstantOverride("margin_left", 16);
            dMargin.AddThemeConstantOverride("margin_top", 12);
            dMargin.AddThemeConstantOverride("margin_right", 16);
            dMargin.AddThemeConstantOverride("margin_bottom", 12);
            detailRoot.AddChild(dMargin);

            var dCol = new VBoxContainer { Name = "DetailCol" };
            dCol.AddThemeConstantOverride("separation", 8);
            dMargin.AddChild(dCol);

            var dTitle = new Label
            {
                Name = "DetailTitle",
                Text = "【角色详情】",
                CustomMinimumSize = new Vector2(0, 28),
            };
            dTitle.ThemeTypeVariation = Darkest.UI.DdTheme.TitleVariation; // 🔴 架构裁定②：标题用 Bold
            dCol.AddChild(dTitle);

            // 🔴 用户规则③（§14.0.2）：**二级弹窗必须有显式【退出】按钮**（`Esc` 只作附加出口）✓
        var dExit = new Button { Name = "DetailExit", Text = "✕ 退出", CustomMinimumSize = new Vector2(110, 34) };
        dExit.Pressed += () =>
        {
            GD.Print("[HamletRoot] 角色详情：✕ 退出 ⇒ 回到城池");
            CloseHeroDetail();
        };
        dCol.AddChild(dExit);

        // 🔴 相机 720 口径（规则①）：详情内容实测需 **1169 高**（超相机 449px）⇒ 正文**必须可滚动**：
        //    由 `ScrollContainer` 兜住高度（面板自身最小高不再由长文本决定）✓
        var dScroll = new ScrollContainer
        {
            Name = "DetailScroll",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        dCol.AddChild(dScroll);

        var dBody = new HBoxContainer { Name = "DetailBody", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            dBody.AddThemeConstantOverride("separation", 10);
            dScroll.AddChild(dBody);   // 🔴 正文进滚动容器 ✓

            var dLeftCol = new VBoxContainer { Name = "DetailLeftCol", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        dLeftCol.SizeFlagsStretchRatio = 0.38f;   // 🔴 DD 1:1 #2：左状态块 ≈0-230/600 ⇒ 比例 **38%**（DD panel.hero：HP 红 130,11 / 压力灰 130,40 / 属性列 60,72）✓
            dLeftCol.AddThemeConstantOverride("separation", 6);
            dBody.AddChild(dLeftCol);
            var dRightCol = new VBoxContainer { Name = "DetailRightCol", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        dRightCol.SizeFlagsStretchRatio = 0.62f;   // 🔴 DD 1:1 #2：右装备块 ≈230-600/600 ⇒ 比例 **62%**（DD panel.hero：装备 238,0 / 饰品 453,0）✓
            dRightCol.AddThemeConstantOverride("separation", 6);
            dBody.AddChild(dRightCol);

            _detailLeft = new Label
            {
                Name = "DetailLeft",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(360, 380),
            };
        // DD 1:1 3-2：**英雄面板骨架优先**（`hero_detail_skeleton.tscn`，编辑器里可改）
        //   成功 ⇒ 取骨架的 HeroStatusBars；失败 ⇒ 回落下面的代码建（不崩不静默）✓
        Darkest.UI.HeroDetailSkeleton? hSkel = Darkest.UI.HeroDetailSkeleton.TryInstantiate();
        // 🔴 2026-10-02 修正（M4u 顺手）：**逐块**判骨架，而不是全局一个 `usedSkel` ——
        //    实测 `.tscn` 里只有 `HeroStatusBars`／`HeroStatsGrid` 两块，缺 `HeroEquipmentRow`／`HeroTrinketGrid`；
        //    旧的全局判法会让**缺的那两块连「代码建」也跳过** ⇒ 整块静默消失（Reparent 得 null 也不报）✓
        bool skelBarsUsed = false;
        bool skelStatsUsed = false;
        bool skelEquipUsed = false;
        bool skelTrinketUsed = false;
        if (hSkel is not null)
        {
            // DD 1:1 3-2：**骨架有哪块就用哪块**（编辑器里可改）；Reparent 后再 MoveChild（Godot 4 规则）✓
            if (hSkel.HeroStatusBars is Control skelBars)
            {
                skelBars.Reparent(dLeftCol);
                dLeftCol.MoveChild(skelBars, 0);
                skelBarsUsed = true;
            }

            if (hSkel.HeroStatsGrid is Control skelStats)
            {
                skelStats.Reparent(dLeftCol);
                skelStatsUsed = true;
            }

            if (hSkel.HeroEquipmentRow is Control skelEq)
            {
                skelEq.Reparent(dRightCol);
                dRightCol.MoveChild(skelEq, 0);
                skelEquipUsed = true;
            }

            if (hSkel.HeroTrinketGrid is Control skelTr)
            {
                skelTr.Reparent(dRightCol);
                dRightCol.MoveChild(skelTr, 1);
                if (skelTr is GridContainer skelTrinketGrid)
                {
                    _detailTrinketGrid = skelTrinketGrid;   // 🔴 骨架给了格 ⇒ 刷新走同一份 ✓
                }

                skelTrinketUsed = true;
            }

            hSkel.QueueFree();   // 四块已取走 ⇒ 释放空根，不留多余节点
            GD.Print($"[UI 英雄面板] 骨架 hero_detail_skeleton.tscn 提供：状态条={skelBarsUsed} 属性列={skelStatsUsed} " +
                     $"装备行={skelEquipUsed} 饰品格={skelTrinketUsed}（缺的块走代码建 · 不静默）✓");
        }
        // 🔴 DD 1:1 ②【英雄状态条】照 `shared\hero\hero.layout.darkest` 的 `hero_campaign_status_layout`（次序/间距）
        //    DD：resolve_level_bar_offset 6,4 · stress_bar_offset -14,100 · stress_bar_spacing 10,0（×0.667 ⇒ 间距≈7）
        //    上=决心等级条 ⇒ **用户裁定：映射现有【士气条】（真数据）** · 下=压力条（同源） · HP 条=**接口占位**（TooltipText 标注）✓
        if (!skelBarsUsed)   // 🔴 逐块判：骨架没给状态条 ⇒ 代码建（不静默消失）✓
        {
        var statusBars = new VBoxContainer { Name = "HeroStatusBars" };
        statusBars.AddThemeConstantOverride("separation", 7);
        int moraleNow = ExpeditionContext.Roster?.MoraleOf(heroId) ?? 0;
        var hpBar = new ProgressBar { Name = "HeroHpBar", CustomMinimumSize = new Vector2(0, 12), TooltipText = "生命值（读数接口 · 本屏暂不可用）", MouseFilter = Control.MouseFilterEnum.Ignore };
        hpBar.Modulate = Darkest.UI.DdTheme.Danger;
        statusBars.AddChild(hpBar);
        var moraleBarUi = new ProgressBar { Name = "HeroMoraleBar", MinValue = 0, MaxValue = 100, Value = moraleNow, CustomMinimumSize = new Vector2(0, 10) };
        moraleBarUi.Modulate = Darkest.UI.DdTheme.TextInfo;
        statusBars.AddChild(moraleBarUi);
        dLeftCol.AddChild(statusBars);
        dLeftCol.MoveChild(statusBars, 0);
        GD.Print($"[UI 英雄面板] ✅ DD 状态条就位：士气条={moraleNow}（真数据）／HP 条=占位接口（已标 TooltipText）✓");
        }

        // 🔴 DD 1:1 ②-2【六属性列】照 `shared\hero\hero.layout.darkest` 的 `hero_base_stats_layout`：
        //    .name_offset 0 0 · .value_offset **115 0** · .spacing **200 22** ⇒ 两列（名/值）+ 行距 ⇒ 用 GridContainer 表达 ✓
        //    数据源：`_unitsCfg.Units` 按原型取（与右栏文本同源，不新造数字）✓ 只依赖字段 + heroId 参数（作用域安全）
        if (!skelStatsUsed)   // 🔴 逐块判（同上）✓
        {
        string archeForStats = _rosterCfgForDetail?.Heroes.FirstOrDefault(h => h.Id == heroId)?.Archetype ?? string.Empty;
        UnitConfig? statsUnit = _unitsCfg?.Units.FirstOrDefault(u => u.Id == archeForStats);
        var statsGrid = new GridContainer { Name = "HeroStatsGrid", Columns = 2 };
        statsGrid.AddThemeConstantOverride("h_separation", 200);   // DD spacing 200 ×0.667 ≈ 133（名→值间距）✓
        statsGrid.AddThemeConstantOverride("v_separation", 22);    // DD spacing 22 ×0.667 ≈ 15（行距）✓
        string[][] statPairs = statsUnit is null
            ? new[] { new[] { "攻击", "—" }, new[] { "物防", "—" }, new[] { "速度", "—" }, new[] { "闪避", "—" }, new[] { "暴击", "—" }, new[] { "韧性", "—" } }
            : new[]
            {
                new[] { "攻击", statsUnit.Attack.ToString() },
                new[] { "物防", statsUnit.Prot.ToString() },
                new[] { "速度", statsUnit.Speed.ToString() },
                new[] { "闪避", statsUnit.Dodge.ToString() },
                new[] { "暴击", $"{statsUnit.Crit}%" },
                new[] { "韧性", statsUnit.Resilience.ToString() },
            };
        foreach (string[] row in statPairs)
        {
            statsGrid.AddChild(new Label { Text = row[0], VerticalAlignment = VerticalAlignment.Center });
            statsGrid.AddChild(new Label { Text = row[1], VerticalAlignment = VerticalAlignment.Center });
        }
        dLeftCol.AddChild(statsGrid);
        GD.Print($"[UI 英雄面板] ✅ DD 六属性列就位（原型 {archeForStats} · 有数据={statsUnit is not null} · 间距 200/22 = DD 原值）✓");
        }
            dLeftCol.AddChild(_detailLeft);

        // 🆕 2026-10-01 M5u：**怪癖区**（正/负/疾病分类 + 互斥提示）—— 独立 Label ⇒ 悬停出完整信息（Godot 内建 `TooltipText`）✓
        //    🔴 `MouseFilter` 必须显式给：Godot 的 `Label` 默认不吃鼠标（不给 ⇒ tooltip 不弹）；用 **Pass**（不是 Stop）
        //    ⇒ 悬停可读全文，且滚轮事件继续上抛到外层 `ScrollContainer`（规则①：正文必须可滚）✓
        _detailQuirks = new Label
        {
            Name = "DetailQuirks",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Pass,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        dLeftCol.AddChild(_detailQuirks);

            // 🔴 P4（用户参考图④）：**技能 = 图标 + 悬停 tooltip 讲解**（不再是大段文字行）——
        //    图标 = 立绘留框同款（不透明面板样式 1px 边框 + 色块占位）；讲解走 `TooltipText` ✓
        _detailSkills = new HBoxContainer { Name = "DetailSkillIcons" };
        _detailSkills.AddThemeConstantOverride("separation", 6);
        // 🔴 DD 1:1 ②-3【右栏装备位】照 `shared\hero\hero.layout.darkest` 的 `hero_equipment_layout`：
        //    .weapon_pos **4 0**（左）· .armour_pos **95 0**（右）· icon_offset 29 52 · level_offset 90 12
        //    ⚠️ 装备/护甲属**装备系统**（用户裁定：留接口）⇒ 只做**空框占位 + TooltipText**，MouseFilter=Ignore（不留"点了没用"的控件·红线21）✓
        if (!skelEquipUsed)   // 🔴 逐块判（同上）✓
        {
        var equipRow = new HBoxContainer { Name = "HeroEquipmentRow" };
        equipRow.AddThemeConstantOverride("separation", 91);   // DD 95-4=91 的间距感 ×0.667 ≈ 61 → 取容器可读间距 15（两格自适应）✓
        var wSlot = new PanelContainer { Name = "HeroWeaponSlot", CustomMinimumSize = new Vector2(48, 48), MouseFilter = Control.MouseFilterEnum.Ignore, TooltipText = "武器（装备系统接口 · 暂不可用）" };
        wSlot.AddChild(new ColorRect { Name = "WeaponPlaceholder", Color = Darkest.UI.DdTheme.PlaceholderFill });
        equipRow.AddChild(wSlot);
        var aSlot = new PanelContainer { Name = "HeroArmourSlot", CustomMinimumSize = new Vector2(48, 48), MouseFilter = Control.MouseFilterEnum.Ignore, TooltipText = "护甲（装备系统接口 · 暂不可用）" };
        aSlot.AddChild(new ColorRect { Name = "ArmourPlaceholder", Color = Darkest.UI.DdTheme.PlaceholderFill });
        equipRow.AddChild(aSlot);
        dRightCol.AddChild(equipRow);
        dRightCol.MoveChild(equipRow, 0);   // DD：装备位在右栏**最上**（先于技能/抗性）✓
        GD.Print("[UI 英雄面板] ✅ DD 装备位就位（weapon/armour 空框占位 · TooltipText 已标 · 属装备系统接口）✓");
        }

        // 🔴 DD 1:1 ②-4【饰品 2 列格】照 `shared\hero\hero.layout.darkest` 的 `hero_trinket_grid_layout`：
        //    .number_of_columns **2** · .start_pos 32 52 · .offset **92 160** ⇒ 格距 ×0.667 ≈ 61×107 ✓
        //    🆕 2026-10-02 M4u：**从占位框升级为真读数**（此前是"装备系统接口 · 暂不可用"的空框）——
        //    2 个孔 + 读数行建在 `HamletRoot.HeroDetail.Trinkets.cs`（懒建 + 每次开详情现读，红线 26）✓
        //    🔴 槽位是**孔**（PanelContainer，MouseFilter=Pass），不是按钮 —— 红线 21：不留"点了没用"的控件 ✓
        EnsureTrinketSlots(dRightCol);
        dRightCol.AddChild(_detailSkills);

        // 🔴 P4：**右上角"推荐位置"留框**（用户原话"这个留一个框后面做都可以"）✓
        _detailRecommend = new PanelContainer { Name = "DetailRecommendSlot", CustomMinimumSize = new Vector2(0, 44) };
        dRightCol.AddChild(_detailRecommend);
        var recRow = new HBoxContainer { Name = "DetailRecommendRow" };
        recRow.AddThemeConstantOverride("separation", 6);
        _detailRecommend.AddChild(recRow);
        var recFrame = new PanelContainer { Name = "RecommendPortraitFrame", CustomMinimumSize = new Vector2(36, 36) };
        recFrame.AddChild(new ColorRect { Name = "RecommendPlaceholder", Color = Darkest.UI.DdTheme.PlaceholderFill });   // 🔴 规则②：待填位用半透明占位 ✓
        recRow.AddChild(recFrame);
        // 🔴 2026-09-21：文案从开发者口气「待定」改为**玩家可理解的诚实标注**「开发中·预留」
        //    （框本身是用户明确要保留的预留框 ⇒ 保框、不改结构；红线 21：不留不可解释状态）✓
        recRow.AddChild(new Label { Name = "RecommendText", Text = "推荐位置（开发中·预留）", VerticalAlignment = VerticalAlignment.Center });

        _detailRight = new Label
            {
                Name = "DetailRight",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(360, 300),
            };
            dRightCol.AddChild(_detailRight);

            _detailCampSkills = new Label
            {
                Name = "DetailCampSkills",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(360, 90),
            };
            dRightCol.AddChild(_detailCampSkills);

            var dBackRow = new HBoxContainer { Name = "DetailBackRow" };
            dCol.AddChild(dBackRow);
            var back = new Button
            {
                Name = "DetailBack",
                Text = "返回城池",
                CustomMinimumSize = new Vector2(180, 36),
            };
            back.Pressed += CloseHeroDetail;
            dBackRow.AddChild(back);
        }
    }
}
