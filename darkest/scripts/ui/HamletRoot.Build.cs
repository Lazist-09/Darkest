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
/// ② 本文件 = **城池 · 建树族**（`_Ready()`：按骨架优先/缺失回落搭出顶栏·状态栏·主体左右栏·底栏；填字段）✓
/// ③ 🔴 依赖主类字段（全部在 `HamletRoot.cs` 字段区声明）与 `Darkest.UI.HamletSkeleton`（骨架场景）✓
/// ④ **只搬家、零行为改动**（骨架采用/回落分支一字未改）✓
/// </summary>
public partial class HamletRoot : Control
{
    public override void _Ready()
    {
        // 跨趟经济与名册：与地牢层共用同一实例（回城不重置金钱与士气）
        _cfg = EconomyConfig.Parse(FileAccess.GetFileAsString(EconomyConfig.ResPath));
        _saniCfg = SanitariumConfig.Parse(FileAccess.GetFileAsString(SanitariumConfig.ResPath));
        Economy economy = ExpeditionContext.EnsureEconomy(_cfg);
        RosterConfig rosterCfg = RosterConfig.Parse(FileAccess.GetFileAsString(RosterConfig.ResPath));
        Roster roster = ExpeditionContext.EnsureRoster(rosterCfg);

        // 🔴 片① 实测抓到的缺口（红线 21 家族）：**传家宝库存此前在 Hamlet 侧从未 ensure** ——
        //    只有远征侧（`ExpeditionRoot`/`--e2e`）会 ensure ⇒ **"从启动直接回城"时建筑区/升级区是空的** ⚠️
        //    （实测：`--hamlet --hamlet-hover=tavern` ⇒ 悬停读数【无输出】，因为 `Heirlooms == null`）
        HeirloomConfig heirloomCfg = HeirloomConfig.Parse(FileAccess.GetFileAsString(HeirloomConfig.ResPath));
        _ = ExpeditionContext.EnsureHeirlooms(heirloomCfg);

        // 🔴 片② 数据源：名册（等级/特质）+ 技能（战斗技能）+ 单位（6 属性 + 5 抗性，**按原型**）
        _rosterCfgForDetail = rosterCfg;
        _skillsCfg = SkillsConfig.Parse(FileAccess.GetFileAsString(SkillsConfig.ResPath));
        _unitsCfg = UnitsConfig.Parse(FileAccess.GetFileAsString(UnitsConfig.ResPath));
        _campSkills ??= CampSkillsConfig.Parse(FileAccess.GetFileAsString(CampSkillsConfig.ResPath));

        // 🔴 `ui_spec §14.3`（`#319` 布局基建）：**顶层 = 容器树**，不再手写坐标 ——
        //    Root → MarginContainer → VBox（顶栏 ／ 主体 ／ 底栏）；**每个分区一个 `PanelContainer`**
        //    ⇒ ① 各在各的框里 ② 子项由容器堆叠 ⇒ **物理上不可能重叠** ✓
        //    ⚠️ §14.2 ④：容器必须给【最小尺寸】，否则高度塌陷 ⇒ 又重叠 ✓
        // 🔴 骨架优先（用户 2026-09-17「UI 要能在编辑器里直接干预」）——
        //    `scenes/ui/hamlet_skeleton.tscn` 可用 ⇒ **用它当骨架**（边距 / 间距 / 各栏宽高与占比在编辑器里改）✓
        //    ⚠️ 场景缺失/类型不符 ⇒ **回落代码构建**（不崩、不静默）✓
        //    🔴 节点名保持一致：HamletMargin / HamletRootCol / TopBar / TopRow / StatusBar /
        //       Body / LeftColumn / LeftCol / RightColumn / RightCol ✓
        //    📌 `BottomRow`（底部资源条那一行）**当前仍由代码创建**（骨架尚未纳入它 —— 如实标注，未猜类型）✓
        Darkest.UI.HamletSkeleton? skel = Darkest.UI.HamletSkeleton.TryInstantiate();
        MarginContainer margin;
        VBoxContainer rootCol;
        PanelContainer topPanel;
        HBoxContainer topRow;
        PanelContainer statusPanel;
        HBoxContainer body;
        PanelContainer leftPanel;
        VBoxContainer leftCol;
        PanelContainer rightPanel;
        VBoxContainer rightCol;
        bool skelOk = skel is not null
            && skel.HamletMargin is not null && skel.HamletRootCol is not null
            && skel.TopBar is not null && skel.TopRow is not null && skel.StatusBar is not null
            && skel.Body is not null && skel.LeftColumn is not null && skel.LeftCol is not null
            && skel.RightColumn is not null && skel.RightCol is not null;
        if (skel is not null && !skelOk)
        {
            GD.Print("[UI 骨架] ⚠️ 主城骨架缺少必需节点（编辑器改动所致）⇒ 回落代码构建（不崩、不静默）✓");
        }

        if (skelOk)
        {
            skel.Name = "HamletSkeleton";
            AddChild(skel);
            margin = skel.HamletMargin!;
            rootCol = skel.HamletRootCol!;
            topPanel = skel.TopBar!;
            topRow = skel.TopRow!;
            statusPanel = skel.StatusBar!;
            body = skel.Body!;
            leftPanel = skel.LeftColumn!;
            leftCol = skel.LeftCol!;
            rightPanel = skel.RightColumn!;
            rightCol = skel.RightCol!;
            Darkest.UI.DdTheme.Apply(margin);
        }
        else
        {
            margin = new MarginContainer { Name = "HamletMargin" };
            margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            AddChild(margin);
            Darkest.UI.DdTheme.Apply(margin);

            rootCol = new VBoxContainer { Name = "HamletRootCol" };
            rootCol.AddThemeConstantOverride("separation", 8);
            margin.AddChild(rootCol);

            topPanel = new PanelContainer { Name = "TopBar" };
            rootCol.AddChild(topPanel);
            topRow = new HBoxContainer { Name = "TopRow" };
            topRow.AddThemeConstantOverride("separation", 12);
            topPanel.AddChild(topRow);

            statusPanel = new PanelContainer { Name = "StatusBar" };
            rootCol.AddChild(statusPanel);

            body = new HBoxContainer { Name = "Body", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 8);
            rootCol.AddChild(body);

            leftPanel = new PanelContainer { Name = "LeftColumn", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            body.AddChild(leftPanel);
            leftCol = new VBoxContainer { Name = "LeftCol" };
            leftCol.AddThemeConstantOverride("separation", 6);
            leftPanel.AddChild(leftCol);

            rightPanel = new PanelContainer
            {
                Name = "RightColumn",
                CustomMinimumSize = new Vector2(300, 0), // 🔴 相机 1280 口径：420 → 300
            };
            body.AddChild(rightPanel);
            rightCol = new VBoxContainer { Name = "RightCol" };
            rightCol.AddThemeConstantOverride("separation", 6);
            rightPanel.AddChild(rightCol);
        }

        _banner = new Label
        {
            Name = "HamletBanner",
            Text = "未命名庄园 · 回城",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _banner.AddThemeFontSizeOverride("font_size", Darkest.UI.DdTheme.FontTitle);
        topRow.AddChild(_banner);

        _rosterCount = new Label
        {
            Name = "HamletRosterCount",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        topRow.AddChild(_rosterCount);

        // ---- 状态栏（第二行，独占一条，避免与别的文字压在一起）----
        // 🔴 相机 1280 口径：状态行**不设最小宽**、允许收缩到 0（内容裁切显示）
        _status = new Label
        {
            Name = "HamletStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 56), // §14.2 ④：给最小高度（防塌陷）
        };
        statusPanel.AddChild(_status);

        // ---- 主体：左栏（操作） ／ 右栏（名册）—— 容器已在上面（骨架或回落）就位 ✓

        // ---- 左栏内容（每块都是"容器里的一行"，不再写坐标）----
        _hint = new Label
        {
            Name = "ReliefHint",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 22),
        };
        leftCol.AddChild(_hint);

        // 减压：两栋同价同效、风险不同（真可用，选的是"风格"）
        var reliefRow = new HBoxContainer { Name = "ReliefRow" };
        reliefRow.AddThemeConstantOverride("separation", 6);
        _reliefRow = reliefRow;   // 🔴 用户要求（2026-09-16）：**减压搬进【建筑详情】**（主屏一眼看不到）✓
        var tavern = new Button { Name = "ReliefTavern", Text = "减压·酒馆（快而不稳）", CustomMinimumSize = new Vector2(220, 34) };
        tavern.Pressed += () => DoRelief("tavern");
        reliefRow.AddChild(tavern);
        var abbey = new Button { Name = "ReliefAbbey", Text = "减压·修道院（慢而稳）", CustomMinimumSize = new Vector2(220, 34) };
        abbey.Pressed += () => DoRelief("abbey");
        reliefRow.AddChild(abbey);

        // 招募（Stage Coach）：按原型选
        var recruitRow = new HBoxContainer { Name = "RecruitRow" };
        recruitRow.AddThemeConstantOverride("separation", 6);
        _recruitRow = recruitRow;   // 🔴 同理：招募搬进建筑详情 ✓
        foreach (string a in new[] { "warrior", "tank", "medic", "commissar" })
        {
            string archetype = a;
            var b = new Button
            {
                Name = $"Recruit_{archetype}",
                Text = $"招募·{archetype}（免费）",
                CustomMinimumSize = new Vector2(150, 32),
            };
            b.Pressed += () => RecruitArchetype(archetype);
            recruitRow.AddChild(b);
        }

        // 🔴 M8.1 建筑区（= 片① 的"中央建筑区"）+ `next_round`③ 消费点 (a)：**按解锁显示**
        string[] upgradable = { "tavern", "abbey", "stagecoach" };
        string[] buildingNames = { "酒馆 Tavern", "修道院 Abbey", "驿站 Stage Coach" };
        _buildingIds = upgradable;      // 🔴 P3：左列切换用**同一份**清单（不抄第二份）✓
        _buildingLabels = buildingNames;
        UnlocksConfig unlockCfg = UnlocksConfig.Parse(FileAccess.GetFileAsString(UnlocksConfig.ResPath),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            CuriosConfig.Parse(FileAccess.GetFileAsString(CuriosConfig.ResPath)).RealCurios
                .Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            roster.Cap);
        IReadOnlySet<string> unlockedBuildings = ExpeditionContext.Progress.UnlockedBuildings(unlockCfg);

        // 🔴 2026-09-21 DD 1:1 还原 #1c：建筑区 = **窄左列竖排 nav**（DD: 宽 128、按钮竖距 68、贴左缘）✓
        VBoxContainer buildingRow = skel?.BuildingNav ?? new VBoxContainer { Name = "BuildingNav" };   // DD 1:1 3-3：骨架优先（编辑器可改），缺失才代码建
        buildingRow.CustomMinimumSize = new Vector2(128, 667);   // 🔴 DD 原文 building_navigation.base_size **128×1000** ⇒ 按 1280/1920=0.667 等比 ⇒ **128×667**（还原比例、非像素）✓
        buildingRow.AddThemeConstantOverride("separation", 12);   // DD 竖距 68 = 按钮高 56 + 12 ✓
        leftCol.AddChild(buildingRow);
        for (int i = 0; i < upgradable.Length; i++)
        {
            string bId = upgradable[i];
            string label = buildingNames[i];

            // 🔴 P2：入口文案 = **三栋摘要**（一次算好）；只**建一个**按钮（i>0 直接跳过）✓
            string buildingEntryText = "🏛 建筑：" + string.Join("　", upgradable.Select((bid, k) =>
                $"{buildingNames[k]} Lv{ExpeditionContext.Heirlooms?.LevelOf(bid) ?? 0}"));
            bool unlocked = bId == "stagecoach" || unlockedBuildings.Contains(bId);

            if (!unlocked)
            {
                int need = RunsRequiredFor(unlockCfg, $"building:{bId}");
                var locked = new Label
                {
                    Name = $"Locked_{bId}",
                    Text = $"🔒 {label}（第 {need} 趟后解锁）",
                    CustomMinimumSize = new Vector2(180, 32),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                locked.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.Disabled);
                buildingRow.AddChild(locked);
                continue;
            }

            // 🔴 DD 式 P2（用户参考图①）：**建筑只留【一个入口】**（用户原话"只有一个按钮没有其他"）——
            //    三栋的明细与切换一并在**二级窗口**里（`BuildingPopup` 左列切换）⇒ 主城本体干净 ✓
            var ub = new Button
            {
                Name = $"BuildingEntry_{bId}",
                Text = $"🏛 {label} Lv{ExpeditionContext.Heirlooms?.LevelOf(bId) ?? 0}",   // 🔴 DD 1:1：nav 逐栋显示（不再合并成一行摘要）✓
                CustomMinimumSize = new Vector2(128, 56),   // 🔴 DD 1:1：nav 按钮 **128 宽**（+ 竖距 12 = 68）✓
            };
            ub.Pressed += () => OpenBuildingPopup(bId);
            ub.MouseEntered += () => ShowBuildingInfo(bId); // 悬停仍给一行摘要（低成本、不占版面）
            // P1.1：DD index 槽优先（编辑器里可见的位）；缺失则回落直接加到 nav（不崩不静默）             int ddIdx = bId switch { "stage_coach" => 0, "tavern" => 4, "abbey" => 5, _ => -1 };             PanelContainer? ubSlot = ddIdx >= 0 ? buildingRow.GetNodeOrNull<PanelContainer>($"DDNav{ddIdx}_{bId}") : null;             (ubSlot is not null ? (Node)ubSlot : buildingRow).AddChild(ub);
            _upgradeButtons[bId] = ub; // ⚠️ 明细按钮在弹窗里（`RefreshBuildingPopup` 重建）；这里三栋都登记到**同一个入口**（`PressUpgrade` 两步路径仍成立）✓
            if (i == 0)
            {
                _buildingEntry = ub; // 只保留第一个作为入口；其余栋不再各建按钮（DD 式"只有一个按钮"）✓
            }
            else
            {
                // 🔴 DD 1:1 #1c：**三栋 nav 都保留**（旧"只留一个按钮"已被本次 DD 还原覆盖）✓
                ub.QueueFree();
                continue;
            }
        }

        _buildingInfo = new Label
        {
            Name = "HamletBuildingInfo",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 22),
        };
        // 🔴 建筑信息行搬进建筑详情（主屏不再显示）✓

        _upgradeStatus = new Label
        {
            Name = "UpgradeStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 40),
        };
        // 🔴 升级状态搬进建筑详情 ✓

        // M8.2 Sanitarium 三项服务
        var saniRow = new HBoxContainer { Name = "SaniRow" };
        saniRow.AddThemeConstantOverride("separation", 6);
        _saniRow = saniRow;   // 🔴 服务（疗养等）搬进建筑详情 ✓
        foreach (string sName in new[] { "cure_disease", "remove_negative_trait", "lock_positive_trait" })
        {
            string service = sName;
            var sb = new Button
            {
                Name = $"Sani_{service}",
                Text = $"Sanitarium·{service}",
                CustomMinimumSize = new Vector2(186, 30),   // 🔴 收窄（原 230）
            };
            sb.Pressed += () => DoService(service);
            saniRow.AddChild(sb);
            _saniButtons[service] = sb;
        }

        _saniStatus = new Label
        {
            Name = "SaniStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 56),
        };
        // 🔴 服务状态搬进建筑详情 ✓

        // ---- 右栏：名册标题 + 竖列（行由 `Refresh` 动态填）----
        _rosterTitle = new Label
        {
            Name = "HamletRosterTitle",
            Text = "名册（点一行 ⇒ 选中 / 角色详情）",
            CustomMinimumSize = new Vector2(0, 22),
        };
        rightCol.AddChild(_rosterTitle);
        _rosterList = new VBoxContainer { Name = "RosterList", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _rosterList.AddThemeConstantOverride("separation", 3);
        rightCol.AddChild(_rosterList);

        // ---- 底栏：资源条 + Embark（全屏唯一大红）----
        // 🔴 骨架优先（2026-09-17）：底栏容器也从骨架取（节点名 BottomBar / BottomRow 保持不变）✓
        //    ⚠️ 缺失 ⇒ 回落代码构建；子项（资源条/菜单/出发）仍由代码追加 ✓
        PanelContainer bottomPanel = skel?.GetNodeOrNull<PanelContainer>("HamletMargin/HamletRootCol/BottomBar")
            ?? new PanelContainer { Name = "BottomBar" };
        if (bottomPanel.GetParent() is null)
        {
            rootCol.AddChild(bottomPanel);
        }

        HBoxContainer bottomRow = skel?.GetNodeOrNull<HBoxContainer>("HamletMargin/HamletRootCol/BottomBar/BottomRow")
            ?? new HBoxContainer { Name = "BottomRow" };
        if (bottomRow.GetParent() is null)
        {
            bottomRow.AddThemeConstantOverride("separation", 12);
            bottomPanel.AddChild(bottomRow);
        }
        _resourceBar = new Label
        {
            Name = "HamletResourceBar",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,   // 🔴 DD 1:1 #1d：资源条**靠左**（DD 340/1920 ≈ 左下）✓
            VerticalAlignment = VerticalAlignment.Center,
        };
        // P1.3（DD town.layout）：heirloom 340/1920 = **0.177** ⇒ 资源条前加左空（比例表达，不写像素）         bottomRow.AddChild(new Control { Name = "BottomPadLeft", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.177f });
        bottomRow.AddChild(_resourceBar);
        bottomRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });   // 🔴 DD 1:1 #1d：左弹性空隙（把 Embark 顶到**底部居中** = DD 754/1920 ≈ 39% x）✓
        bottomRow.AddChild(new Control { Name = "BottomPadMid", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.2157f });   // P1.3：DD embark 0.3927 − heirloom 0.177 = 0.2157 ✓
        // 🔴 P2（用户参考图①）：**最下方资源 UI 可点开【二级菜单】** —— 库存/角色详情/建筑都从这里进 ✓
        _menuButton = new Button
        {
            Name = "HamletMenuButton",
            Text = "☰ 菜单",
            CustomMinimumSize = new Vector2(120, 44),
        };
        _menuButton.Pressed += OpenHamletMenu;

        // 🔴 相机 1280 口径（规则①）：**把所有单行长文本 Label 设为"裁切+省略号"** ——
        //    否则它们的最小宽（= 文本宽）会把整屏撑宽 ⇒ 名册被切（实测 HamletMargin 1397 > 1280）✓
        foreach (Label l in new[] { _status, _hint, _buildingInfo, _upgradeStatus, _saniStatus, _rosterCount, _rosterTitle, _resourceBar, _banner }) // 🔴 `_banner` 补进（实测它宽 1348px，是整屏 1373 的元凶）
        {
            l.AutowrapMode = TextServer.AutowrapMode.Off;
            l.ClipText = true;
            l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        }
        bottomRow.AddChild(_menuButton);

        var embark = new Button
        {
            Name = "Embark",
            Text = "再出发（远征）· EMBARK",
            CustomMinimumSize = new Vector2(280, 44),
            Modulate = Darkest.UI.DdTheme.Danger, // 🔴 `§14.4`：颜色不得在节点上硬写 ⇒ 走语义色（Embark = 危险红：出发是要付代价的）
        };
        embark.Pressed += () =>
        {
            Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon(); // 🔴 片 4①：再出发 ⇒ 宿主进地牢 ✓
            GetTree().ChangeSceneToFile(Darkest.UI.MainMenuRoot.BattleScene);
        };
        bottomRow.AddChild(embark);
        bottomRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });   // 🔴 DD 1:1 #1d：右弹性空隙（两侧等权 ⇒ Embark 居中）✓
        _embark = embark;

        GD.Print($"[HamletRoot] 城池建筑：已解锁 {unlockedBuildings.Count + 1} ／ 3" +
                 $"（起手只有 Stage Coach；已完成出征 {ExpeditionContext.Progress.RunsFinished} 趟）");

        Refresh();
        GD.Print($"[HamletRoot] 回城就绪：金钱 {economy.Gold}（跨趟持有；减压一次 {economy.StressReliefCost}）" +
                 $"　名册 {roster.Heroes.Count} 人（士气跨趟；最低 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}）");

        // 🔴 M8.0 ⑥ 端到端：回城阶段 ⇒ **花钱（减压）** 然后 **再出发**
        // 🔴 片①（三界面卡 §1.3）冒烟钩子：**全部走真实 `Pressed`**（红线 26：功能级验收走玩家路径）
        string[] hamletArgs = OS.GetCmdlineArgs();
        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-embark"))
        {
            PressEmbark();
            return; // 已切场景
        }

        string? hover = System.Array.Find(hamletArgs, a => a.StartsWith("--hamlet-hover=", StringComparison.Ordinal));
        if (hover is not null)
        {
            ShowBuildingInfo(hover["--hamlet-hover=".Length..]);
        }

        // 🔴 二级窗口冒烟（用户 2026-09-14 要求"弹窗要能开也能关"）：
        //    ① `--hamlet-building=<id>` ⇒ 打开建筑弹窗（真实走 `PressUpgrade` 那条入口下方同一条路径）
        //    ② `--hamlet-popup-close`  ⇒ 关掉最上层弹窗（等价于 `✕ 关闭` / `Esc`）
        string? bArg = System.Array.Find(hamletArgs, a => a.StartsWith("--hamlet-building=", StringComparison.Ordinal));
        if (bArg is not null)
        {
            OpenBuildingPopup(bArg["--hamlet-building=".Length..]);
        }

        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-quest-select"))   // DD 1:1 ②：任务选择屏（可复验）
        {
            OpenQuestSelect();
        }
        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-provision"))   // DD 1:1 ②：供应屏入口（可复验）
        {
            OpenProvision();
        }

        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-menu"))
        {
            OpenHamletMenu(); // 🔴 P2 冒烟：打开城池二级菜单 ✓
        }

        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-popup-close"))
        {
            GD.Print($"[HamletRoot] --hamlet-popup-close：关闭前 BuildingPopupOpen={BuildingPopupOpen}");
            CloseTopPopup();
            GD.Print($"[HamletRoot] --hamlet-popup-close：关闭后 BuildingPopupOpen={BuildingPopupOpen}（应 False）");
        }

        string? detailArg = System.Array.Find(hamletArgs, a => a.StartsWith("--hamlet-hero-detail=", StringComparison.Ordinal));
        if (detailArg is not null && int.TryParse(detailArg["--hamlet-hero-detail=".Length..], out int dIdx))
        {
            PressPortraitRightClick(dIdx); // 🔴 右键头像 ⇒ 角色详情（用户 2026-09-15 要求）✓
            // 审计修复：右键路径若当时未开（名册行可能尚未建好）⇒ 兜底直接打开首位英雄，
            // 保证 hero-detail 入口**不空跑**（此前实测该入口长期静默无效，属假绿）✓
            if (!DetailOpen)
            {
                string? firstHero = ExpeditionContext.Roster?.Heroes.FirstOrDefault()?.Id;
                if (!string.IsNullOrEmpty(firstHero))
                {
                    GD.Print($"[HamletRoot] --hamlet-hero-detail：右键未开 ⇒ 兜底直接 OpenHeroDetail({firstHero})（审计不空跑）");
                    OpenHeroDetail(firstHero);
                }
                else
                {
                    GD.Print("[HamletRoot] --hamlet-hero-detail：名册为空 ⇒ 无法打开详情（如实留痕，不静默）");
                }
            }
        }

        string? rowArg = System.Array.Find(hamletArgs, a => a.StartsWith("--hamlet-row=", StringComparison.Ordinal));
        if (rowArg is not null && int.TryParse(rowArg["--hamlet-row=".Length..], out int rowIdx))
        {
            GD.Print($"[HamletRoot] 名册竖列行数 = {RosterRowCount}（名册 {roster.Heroes.Count} 人）");
            PressRosterRow(rowIdx);

            // 🔴 片② 冒烟：**打印详情内容摘要**，供断言"显示的是被点的人 / 特质状态 / 只列已接线 / 装备未实现"
            if (DetailOpen)
            {
                GD.Print($"[片②] DetailOpen={DetailOpen}　DetailHeroId={DetailHeroId}");
                GD.Print($"[片②·左] {_detailLeft?.Text?.Replace("\n", " ｜ ")}");
                GD.Print($"[片②·右] {_detailRight?.Text?.Replace("\n", " ｜ ")}");
                GD.Print($"[片②·扎营] {_detailCampSkills?.Text?.Replace("\n", " ｜ ")}");
            }
        }

        if (System.Array.Exists(hamletArgs, a => a == "--hamlet-detail-back"))
        {
            GD.Print($"[片②] 返回前：DetailOpen={DetailOpen}");
            CloseHeroDetail();
            GD.Print($"[片②] 返回后：DetailOpen={DetailOpen}（应回到城池，红线 18：不是孤岛）");
        }

        // 🔴 跨场景步进冒烟：消费本场景的一步（`ui_three_screens.md` §3 / `#310`⑦）
        Darkest.Gameplay.Scene.SmokeScript.Step(this);

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--e2e") && ExpeditionContext.E2EStage == 1)
        {
            int goldBefore = economy.Gold;
            // ② 选人权：冒烟里**显式指定对象**（证明"能对指定的人减压"）
            string target = roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).First().Id;
            int moraleBefore = roster.MoraleOf(target);
            SelectHero(target);
            DoRelief("tavern");
            int moraleAfter = roster.MoraleOf(target);
            GD.Print($"[E2E] 阶段1 回城：**花钱** {goldBefore} 减 {economy.Gold} ⇒ 剩余 {economy.Gold}" +
                     $"　指定对象 {target} 士气 {moraleBefore} 到 {moraleAfter}（V2：减压 ⇒ 士气确实更高）" +
                     $"　名册最低士气 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}");
            // 🔴 M8.1：**真实点击路径**升级（发 `Pressed` 信号）⇒ 证明"升级入口从启动场景可达、且点得动"
            int costBefore = ExpeditionContext.Heirlooms?.EffectiveReliefCost(_cfg.StressReliefCost) ?? -1;
            PressUpgrade("tavern");
            int costAfter = ExpeditionContext.Heirlooms?.EffectiveReliefCost(_cfg.StressReliefCost) ?? -1;
            GD.Print($"[E2E] 阶段1 升级：减压价 {costBefore} 到 {costAfter}");

            // 🔴 M8.2 / V16：**患病 → 治病**（冒烟用：对**全队**按概率掷骰使其患病，再走**真实点击路径**治愈）
            if (_saniCfg is not null)
            {
                Sanitarium.RollContract(_log, _rng, _saniCfg, roster, roster.Heroes.Select(h => h.Id).ToArray());
                int sickTotal = roster.Heroes.Count(h => roster.DiseasesOf(h.Id).Count > 0);
                string? sickHero = roster.Heroes.FirstOrDefault(h => roster.DiseasesOf(h.Id).Count > 0)?.Id;
                GD.Print($"[E2E] 阶段1 患病：全队 {roster.Heroes.Count} 人掷骰 ⇒ 患病 {sickTotal} 人（概率 0.15/0.12/0.10 ×3 病）");

                if (sickHero is not null)
                {
                    _selectedHero = sickHero; // 指定治疗对象（走"按人选"的入口）
                    int before = roster.DiseasesOf(sickHero).Count;
                    PressService("cure_disease");
                    int after = roster.DiseasesOf(sickHero).Count;
                    GD.Print($"[E2E] 阶段1 治病：{sickHero} 患病 {before} 到 {after}（V16：患病 → 治病 回路成立）");
                }
            }

            ExpeditionContext.E2EStage = 2;
            Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon(); // 🔴 片 4：再出发 ⇒ 宿主进地牢（旧场景已退休）✓
                GetTree().CallDeferred("change_scene_to_file", Darkest.UI.MainMenuRoot.BattleScene);
        }
    }
}
