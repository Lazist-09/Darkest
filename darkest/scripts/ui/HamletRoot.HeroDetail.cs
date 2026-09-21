using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律（用户 2026-09-21）：一律 Darkest.UI（大写 UI），不得写成 Ui ✓

/// <summary>
/// ① 从 `HamletRoot.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **城池 · 角色详情族**（打开 / 关闭 / 详情面板构建与刷新）✓
/// ③ 🔴 依赖主类 `HamletRoot` 的私有成员/状态：`_detailPanel` · `_detailHeroId` · `_rosterCfgForDetail` ·
///    `_detailLeft` · `_detailRight` · `_detailSkills` · `_detailRecommend` · `_selectedHero`（其余走 `ExpeditionContext`）✓
/// ④ **只搬家、零行为改动**（读数对照见提交信息）✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>🔴 打开某英雄的**角色详情**（片②）—— 数据全部真读（红线 26：断言"显示的是被点的人"）。</summary>
    public void OpenHeroDetail(string heroId)
    {
        RosterConfig? rosterCfg = _rosterCfgForDetail;
        Roster? roster = ExpeditionContext.Roster;
        if (rosterCfg is null || roster is null)
        {
            GD.Print("[HamletRoot] 角色详情：名册未加载");
            return;
        }

        HeroConfig? hero = rosterCfg.Heroes.FirstOrDefault(h => h.Id == heroId);
        if (hero is null)
        {
            GD.Print($"[HamletRoot] 角色详情：找不到英雄 {heroId}");
            return;
        }

        _detailHeroId = heroId;

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
        bool usedSkel = hSkel is not null;
        if (hSkel is not null)
        {
            // DD 1:1 3-2：四块**全部取骨架**（编辑器里可改）；Reparent 后再 MoveChild（Godot 4 规则）
            if (hSkel.HeroStatusBars is Control skelBars)
            {
                skelBars.Reparent(dLeftCol);
                dLeftCol.MoveChild(skelBars, 0);
            }

            if (hSkel.HeroStatsGrid is Control skelStats)
            {
                skelStats.Reparent(dLeftCol);
            }

            if (hSkel.HeroEquipmentRow is Control skelEq)
            {
                skelEq.Reparent(dRightCol);
                dRightCol.MoveChild(skelEq, 0);
            }

            if (hSkel.HeroTrinketGrid is Control skelTr)
            {
                skelTr.Reparent(dRightCol);
                dRightCol.MoveChild(skelTr, 1);
            }

            hSkel.QueueFree();   // 四块已取走 ⇒ 释放空根，不留多余节点
        }
        // 🔴 DD 1:1 ②【英雄状态条】照 `shared\hero\hero.layout.darkest` 的 `hero_campaign_status_layout`（次序/间距）
        //    DD：resolve_level_bar_offset 6,4 · stress_bar_offset -14,100 · stress_bar_spacing 10,0（×0.667 ⇒ 间距≈7）
        //    上=决心等级条 ⇒ **用户裁定：映射现有【士气条】（真数据）** · 下=压力条（同源） · HP 条=**接口占位**（TooltipText 标注）✓
        if (!usedSkel)
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
        if (!usedSkel)
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
                new[] { "物防", statsUnit.PhysDef.ToString() },
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

            // 🔴 P4（用户参考图④）：**技能 = 图标 + 悬停 tooltip 讲解**（不再是大段文字行）——
        //    图标 = 立绘留框同款（不透明面板样式 1px 边框 + 色块占位）；讲解走 `TooltipText` ✓
        _detailSkills = new HBoxContainer { Name = "DetailSkillIcons" };
        _detailSkills.AddThemeConstantOverride("separation", 6);
        // 🔴 DD 1:1 ②-3【右栏装备位】照 `shared\hero\hero.layout.darkest` 的 `hero_equipment_layout`：
        //    .weapon_pos **4 0**（左）· .armour_pos **95 0**（右）· icon_offset 29 52 · level_offset 90 12
        //    ⚠️ 装备/护甲属**装备系统**（用户裁定：留接口）⇒ 只做**空框占位 + TooltipText**，MouseFilter=Ignore（不留"点了没用"的控件·红线21）✓
        if (!usedSkel)
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
        //    .number_of_columns **2** · .start_pos 32 52 · .offset **92 160** ⇒ 格距 ×0.667 ≈ 61×107 ⇒ 用 GridContainer 表达 ✓
        //    ⚠️ 饰品同属**装备系统**（用户裁定：留接口）⇒ 两个**空框占位 + TooltipText**，MouseFilter=Ignore（不留"点了没用"的控件·红线21）✓
        if (!usedSkel)
        {
        var trinketGrid = new GridContainer { Name = "HeroTrinketGrid", Columns = 2 };
        trinketGrid.AddThemeConstantOverride("h_separation", 92);    // DD offset 92 ×0.667 ≈ 61 ✓
        trinketGrid.AddThemeConstantOverride("v_separation", 160);   // DD offset 160 ×0.667 ≈ 107 ✓
        for (int t = 0; t < 2; t++)   // DD：2 列 = 2 个饰品位（一行）✓
        {
            var slot = new PanelContainer { Name = $"HeroTrinketSlot{t + 1}", CustomMinimumSize = new Vector2(44, 44), MouseFilter = Control.MouseFilterEnum.Ignore, TooltipText = $"饰品位 {t + 1}（装备系统接口 · 暂不可用）" };
            slot.AddChild(new ColorRect { Name = $"TrinketPlaceholder{t + 1}", Color = Darkest.UI.DdTheme.PlaceholderFill });
            trinketGrid.AddChild(slot);
        }
        dRightCol.AddChild(trinketGrid);
        dRightCol.MoveChild(trinketGrid, 1);   // DD：饰品格紧随装备位（装备 0 → 饰品 1）✓
        GD.Print("[UI 英雄面板] ✅ DD 饰品 2 列格就位（2 位 · 间距 92/160 = DD 原值 · 占位接口）✓");
        }
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

        // ① 左栏：立绘占位 + 名字/原型/等级/士气（数值 + 条）
        int morale = roster.MoraleOf(heroId);
        string moraleBar = new string('█', Math.Clamp(morale / 10, 0, 10)).PadRight(10, '·');
        var left = new System.Text.StringBuilder();
        left.AppendLine($"【立绘位】（占位块）　原型：{hero.Archetype}");
        left.AppendLine($"名字：{hero.Name}");
        left.AppendLine($"原型：{hero.Archetype}　等级：Lv{hero.Level}");
        left.AppendLine($"士气：{morale} / 100　{moraleBar}");
        left.AppendLine();

        // 🔴 ② 特质：**已固化 / 已清除 标出**（与 `Roster` 状态一致 —— 卡 §2.3 的验收项）
        left.AppendLine("【特质】");
        IReadOnlyList<HeroTraitConfig> currentTraits = roster.TraitsOf(heroId);
        foreach (HeroTraitConfig t in currentTraits)
        {
            bool locked = roster.IsTraitLocked(heroId, t.Id);
            string sign = t.DamagePct != 0 ? $"伤害{t.DamagePct:+#;-#;0}%" : $"士气伤害{t.MoraleDamagePct:+#;-#;0}%";
            left.AppendLine($"　· {t.Id}（{sign}）{(locked ? "🔒 已固化" : string.Empty)}");
        }

        IReadOnlyCollection<string> diseases = roster.DiseasesOf(heroId);
        left.AppendLine();
        left.AppendLine($"【疾病】{(diseases.Count == 0 ? "无" : string.Join("、", diseases))}");
        _detailLeft!.Text = left.ToString();

        // ③ 属性 6 项常显 + 5 项抗性折叠一行（**读按原型的单位数据**，不是写死）
        UnitConfig? unit = _unitsCfg?.Units.FirstOrDefault(u => u.Id == hero.Archetype);
        var right = new System.Text.StringBuilder();
        if (unit is null)
        {
            right.AppendLine($"【属性】🔴 找不到原型单位数据（{hero.Archetype}）");
        }
        else
        {
            right.AppendLine("【属性】");
            right.AppendLine($"　攻击 {unit.Attack}　物防 {unit.PhysDef}　速度 {unit.Speed}　" +
                             $"闪避 {unit.Dodge}　暴击 {unit.Crit}　韧性 {unit.Resilience}");
            right.AppendLine($"　（5 项抗性折叠）眩晕 {unit.StunResist}　流血 {unit.BleedResist}　" +
                             $"减益 {unit.StatDebuffResist}　位移 {unit.DisplaceResist}　死门 {unit.DeathsDoorResist}");
        }
        right.AppendLine();

        // 🔴 P4（用户参考图④）：**战斗技能改成【图标 + 悬停讲解】** —— 图标行在这里填充，
        //    文字区**只留一行提示**（不再逐条列大段说明 ⇒ "简洁、文字不要太多"）✓
        if (_detailSkills is not null)
        {
            foreach (Node c in _detailSkills.GetChildren().ToArray())
            {
                _detailSkills.RemoveChild(c);
                c.QueueFree();
            }

            if (_skillsCfg is not null)
            {
                foreach (SkillTemplateConfig s in _skillsCfg.Skills.Where(s => s.OwnerUnit == hero.Archetype).Take(5))
                {
                    // 图标 = 立绘留框同款（1px 边框 + 色块占位）；**讲解走 TooltipText**（悬停即可读全）✓
                    var slot = new PanelContainer
                    {
                        Name = $"SkillIcon_{s.Id}",
                        CustomMinimumSize = new Vector2(40, 40),
                        TooltipText = $"{s.Name}（{s.Id}）\n命中修正 {s.HitMod:+#;-#;0}　效果 {s.Effects.Count} 条",
                    };
                    slot.AddChild(new ColorRect
                    {
                        Name = "SkillIconPlaceholder",
                        Color = WithPlaceholderAlpha(Darkest.UI.DdTheme.ArchetypeColor(hero.Archetype, isPlayer: true)),   // 🔴 规则②：α 取调色板
                    });
                    _detailSkills.AddChild(slot);
                }
            }
        }

        // ④ 战斗技能 5 个（该原型；悬停 tooltip 的文本直接展开，避免依赖 tooltip 机制）
        right.AppendLine("【战斗技能】（见上方图标；悬停读讲解）");
        if (_skillsCfg is not null)
        {
            foreach (SkillTemplateConfig s in _skillsCfg.Skills.Where(s => s.OwnerUnit == hero.Archetype).Take(5))
            {
                string dmg = s.Damage is null ? "非伤害" : "有伤害";
                right.AppendLine($"　· {s.Name}（{s.Id}）{dmg}　命中修正 {s.HitMod}　暴击修正 {s.CritMod}");
            }
        }

        right.AppendLine();
        right.AppendLine("【装备 2 格】🔴 空 —— 未实现（M8.3）");
        _detailRight!.Text = right.ToString();

        // 🔴 ⑤ 扎营技能：**只列该原型的** + **只列已接线的**（`ConsumedEffectNames`，红线 21）
        var campLines = new List<string> { "【扎营技能】（只列该原型 + 只列已接线）" };
        if (_campSkills is not null)
        {
            CampSkillConfig[] usable = _campSkills.Skills
                .Where(s => s.OwnerUnit == hero.Archetype)
                .ToArray();
            foreach (CampSkillConfig s in usable)
            {
                bool wired = CampSkillsConfig.ConsumedEffectNames.Contains(s.Effect);
                campLines.Add(wired
                    ? $"　· {s.Name}（{s.Cost} 点）✓ 已接线"
                    : $"　· {s.Name}（{s.Cost} 点）🔴 阶段二·未接线（不出现于扎营面板）");
            }
        }

        _detailCampSkills!.Text = string.Join("\n", campLines);

        _detailPanel.Visible = true;
        GD.Print($"[HamletRoot] 角色详情打开：{hero.Name}（{heroId}）原型 {hero.Archetype} Lv{hero.Level} 士气 {morale}" +
                 $"　特质 {currentTraits.Count} 条　疾病 {diseases.Count} 项");
    }

    /// <summary>🔴 关闭详情 ⇒ **回到城池**（红线 18：不是孤岛）。</summary>
    public void CloseHeroDetail()
    {
        if (_detailPanel is not null)
        {
            _detailPanel.Visible = false;
        }

        _detailHeroId = null;
        GD.Print("[HamletRoot] 角色详情关闭 ⇒ 回到城池");
    }
}
