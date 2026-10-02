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

        // 🔴🆕 `O-106`（2026-10-01）：**存档接线 + 启动读档** —— 位置：五个持有者备齐之后、
        //    任何冒烟播种之前（播种族读数必须与档无关）；写回口径 = 城池侧 true ✓
        EnsureSaves();

        // 🆕 2026-10-01 M6u 冒烟播种：`--hamlet-runs-seed=N` 的调用点**必须在这里**（nav 建树之前）——
        //    锁着的建筑只建 🔒 Label、不建按钮（建完再播种就晚了）；走**公开 API** `RunProgress.FinishRun`，不改内核 ✓
        HandleSmokeUnlockSeed(OS.GetCmdlineArgs());

        // 🔴 片② 数据源：名册（等级/特质）+ 技能（战斗技能）+ 单位（6 属性 + 5 抗性，**按原型**）
        _rosterCfgForDetail = rosterCfg;
        _skillsCfg = SkillsConfig.Parse(FileAccess.GetFileAsString(SkillsConfig.ResPath));
        _unitsCfg = UnitsConfig.Parse(FileAccess.GetFileAsString(UnitsConfig.ResPath));
        _campSkills ??= CampSkillsConfig.Parse(FileAccess.GetFileAsString(CampSkillsConfig.ResPath));
        // 🆕 2026-10-01 M5u：怪癖库（`quirks.json`）—— 高级新兵掷签（`StagecoachRecruits.RollQuirk`）
        //    与详情/名册的**分类 + 互斥**显示都真读它（UI 不写死任何 id 与判定）✓
        _quirksCfg = QuirksConfig.Parse(FileAccess.GetFileAsString(QuirksConfig.ResPath));
        // 🆕 2026-10-02 M4u：饰品库（`trinkets.json`）—— 详情 2 格的槽位读数 / 职业要求 / 悬停全文都真读它
        //    （UI 不写死任何 id 与判定；装不上的理由生产点在 `Roster.CanEquipTrinket`）✓
        _trinketsCfg = TrinketsConfig.Parse(FileAccess.GetFileAsString(TrinketsConfig.ResPath));

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

        // 🆕 2026-10-01 M7u：招募行**不再在此预建** ⇒ 改**懒建**（只有真打开驿站弹窗才建，城池主屏零新增节点）✓
        //    🔴 旧写法有两条硬伤：① 4 颗按钮把原型写死成第二份表（`warrior/tank/medic/commissar`，违反 P3 纪律）；
        //       ② 名单数量 ／ 高级概率 ／ 起始等级全无来源（玩家看不到「今日新兵几人」）。
        //    ✅ 新写法 = `HamletRoot.Recruit.cs`：原型池从 `units.json` 派生，数量/概率读马车曲线（`economy.json`）✓

        // 🔴 M8.1 建筑区（= 片① 的"中央建筑区"）+ `next_round`③ 消费点 (a)：**按解锁显示**
        // 🆕 2026-10-01 解冻窗口（用户裁定「铁匠铺加一条解锁」）：+ 铁匠铺的**两条升级树**（weapon / armour）——
        //    🔴 用【树 id】而不是建筑名：`HeirloomStock.LevelOf / NextLevel / CanUpgrade` 的键 = `heirlooms.json` 的 `building` 字段
        //       （= `hero_upgrades.json` 的 `prerequisites.tree_id`·H-1 前置）⇒ 建筑名 `blacksmith` 在这三个口上会**抛**（P23 ④）✓
        //    ⚠️ 原版铁匠铺 = **一栋建筑内两个页签**；我方弹窗一次只显示一条树 ⇒ 暂以**两个 nav 入口**表达（deviation 已登记 O-105）✓
        string[] upgradable = { "tavern", "abbey", "stagecoach", "blacksmith.weapon", "blacksmith.armour" };
        string[] buildingNames = { "酒馆 Tavern", "修道院 Abbey", "驿站 Stage Coach", "铁匠铺·武器", "铁匠铺·护甲" };
        _buildingIds = upgradable;      // 🔴 P3：左列切换用**同一份**清单（不抄第二份）✓
        _buildingLabels = buildingNames;
        UnlocksConfig unlockCfg = UnlocksConfig.Parse(FileAccess.GetFileAsString(UnlocksConfig.ResPath),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            CuriosConfig.Parse(FileAccess.GetFileAsString(CuriosConfig.ResPath)).RealCurios
                .Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            roster.Cap);
        _unlockCfg = unlockCfg;   // 🆕 M7u：存下供 `RecomputeRosterCap` 用（上限单一来源 = 马车曲线）✓
        IReadOnlySet<string> unlockedBuildings = ExpeditionContext.Progress.UnlockedBuildings(unlockCfg);

        // 🔴 M7u 上限接线点 ①（回城装配）：按**马车曲线**写 `Roster.CurrentCap`
        //    （另一个接线点 = 马车升级后的 `UpgradeBuilding`）⇒「改马车第 3 级，只有一处跟着变」✓
        RecomputeRosterCap("回城装配");

        // 🔴 2026-09-21 DD 1:1 还原 #1c：建筑区 = **窄左列竖排 nav**（DD: 宽 128、按钮竖距 68、贴左缘）✓
        VBoxContainer buildingRow = skel?.BuildingNav ?? new VBoxContainer { Name = "BuildingNav" };   // DD 1:1 3-3：骨架优先（编辑器可改），缺失才代码建
        // 🔴 DD 原文 building_navigation.base_size **128×1000**（1920×1080）⇒ 我方相机 720 ⇒ 等比 **128×667**
        //    但**固定最小高 1000 会撑爆 HamletRootCol**（实测 demand 2）⇒ 宽度照 DD 128，高度交 expand-fill 撑满（≥667 自然满足）
        buildingRow.CustomMinimumSize = new Vector2(128, 0);
        buildingRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        buildingRow.AddThemeConstantOverride("separation", 12);   // DD 竖距 68 = 按钮高 56 + 12 ✓
        // 🔴 2026-10-02 M12 修 demand 超相机（实测 2）：**内容最小高 912 > 可分配 874**（1080 − TopBar 39 − StatusBar 87
        //    − BottomBar 56 − 3×8 间距 − HamletMargin 20）⇒ `HamletRootCol` 最小高 1159 撑爆相机。
        //    ✅ 用 Godot 内建 `ScrollContainer` 兜住：**滚动轴的最小尺寸不向上传播**（4.6 实测：内容 896 ⇒ scroll 最小高 **0**）⇒ demand 归零 ✓
        EnsureNavScrollHost(leftCol, buildingRow);
        for (int i = 0; i < upgradable.Length; i++)
        {
            string bId = upgradable[i];
            string label = buildingNames[i];
            bool unlocked = bId == "stagecoach" || unlockedBuildings.Contains(bId);

            if (!unlocked)
            {
                int need = RunsRequiredFor(unlockCfg, $"building:{bId}");
                var locked = new Label
                {
                    Name = $"Locked_{bId}",
                    Text = $"🔒 {label}（第 {need} 趟后解锁）",
                    CustomMinimumSize = new Vector2(180, 32),
                    // 🔴 2026-10-02 M12：Locked 文案自然宽 **214~229** ⇒ 会撑宽左列（Body 753）；`ClipText` 后最小宽 = 180 上限 ✓
                    ClipText = true,
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
                // 🔴 2026-10-02 M12：文案自然宽 **367**（dd_theme 字号）⇒ 撑宽左列到 379；`ClipText` 把最小宽压回 128 ✓
                ClipText = true,
            };
            ub.Pressed += () => OpenBuildingPopup(bId);
            ub.MouseEntered += () => ShowBuildingInfo(bId); // 悬停仍给一行摘要（低成本、不占版面）
            // P1.1：DD index 槽优先（编辑器里可见的位）；缺失则回落直接加到 nav（不崩不静默）
            // 🔴 2026-09-27 修假绿：本行整体曾被写成**注释** ⇒ 建筑入口按钮**从未挂上树**（与上面 BuildingNav 同一类事故）
            // 🔴 DD nav 槽位（骨架 `hamlet_skeleton.tscn` 里已按 DD index 摆好）：铁匠铺 = **index 1**（骨架的 `DDNav1_blacksmith` 就是为它留的）✓
            //    ⚠️ 护甲轴**没有**第二个槽（原版是同一栋里的页签）⇒ 落 -1（直接追加到 nav 末尾，不崩不静默）✓
            int ddIdx = bId switch
            {
                "stage_coach" => 0, "tavern" => 4, "abbey" => 5, "blacksmith.weapon" => 1, _ => -1,
            };
            PanelContainer? ubSlot = ddIdx >= 0 ? buildingRow.GetNodeOrNull<PanelContainer>($"DDNav{ddIdx}_{bId}") : null;
            if (ubSlot is not null)
            {
                // 骨架 §14.0.68：**数据接入 ⇒ 占位让位**（`PurposeLabel` + `NavPlaceholder` 是"未接线"的占位，
                // 留着会和真按钮**重叠**⇒ 正是用户 #319① 说的"文字各种重叠"）✓
                foreach (Node ph in ubSlot.GetChildren().ToArray())
                {
                    ubSlot.RemoveChild(ph);
                    ph.QueueFree();
                }
            }
            else if (ddIdx < 0)
            {
                GD.Print($"[HamletRoot] nav：{bId} 无 DD 槽（骨架未留）⇒ 直接追加到 nav 末尾（如实留痕）✓");
            }
            (ubSlot is not null ? (Node)ubSlot : buildingRow).AddChild(ub);
            _upgradeButtons[bId] = ub; // ⚠️ 明细按钮在弹窗里（`RefreshBuildingPopup` 重建）；这里三栋都登记到**同一个入口**（`PressUpgrade` 两步路径仍成立）✓
            // 🔴 DD 1:1 #1c：**三栋 nav 都保留**（旧"`i==0` 保留 ＋ 其余 `QueueFree`"已被本次 DD 还原覆盖）✓
            //    2026-09-27 修假绿：旧代码把 abbey / stagecoach 的入口按钮**直接销毁** ⇒ 玩家只能进酒馆
            //    🆕 2026-10-01：删掉 `if (i == 0) { _buildingEntry = ub; }`（字段随之下线）——
            //    每颗 nav 按钮的文案/可用性由 `Refresh()` 按**同一份清单**统一重写，"默认入口"这个概念不再需要 ✓
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

        // 🆕 2026-10-02 分片：底栏（资源条 ＋ ☰ 菜单 ＋ EMBARK）已移到 `HamletRoot.Build.BottomBar.cs`
        //    （用户红线：程序文件 ≤600 行；只搬家、零行为改动）✓
        BuildBottomBar(skel, rootCol);

        // 🆕 2026-10-01：分母跟着 `upgradable` 走（此前写死"／3"，加铁匠铺两条树后会是 **5**）✓
        int unlockedNav = upgradable.Count(id => id == "stagecoach" || unlockedBuildings.Contains(id));
        GD.Print($"[HamletRoot] 城池建筑：已解锁 {unlockedNav} ／ {upgradable.Length}" +
                 $"（起手只有 Stage Coach；已完成出征 {ExpeditionContext.Progress.RunsFinished} 趟；铁匠铺 = 2026-10-01 解冻窗口）");

        Refresh();
        GD.Print($"[HamletRoot] 回城就绪：金钱 {economy.Gold}（跨趟持有；减压一次 {economy.StressReliefCost}）" +
                 $"　名册 {roster.Heroes.Count} 人（士气跨趟；最低 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}）");

        // 🔴 M8.0 ⑥ 端到端：回城阶段 ⇒ **花钱（减压）** 然后 **再出发**
        // 🔴 片①（三界面卡 §1.3）冒烟钩子：**全部走真实 Pressed**（红线 26：功能级验收走玩家路径）
        // 🆕 2026-10-02 分片：本族（--hamlet-hero-detail= ／ --hamlet-row= ／ --hamlet-detail-back ／
        //    --hamlet-embark ／ SmokeScript.Step ／ --e2e 阶段 1）已移到 `HamletRoot.Build.Cli.cs`
        //    （用户红线：程序文件 ≤600 行；只搬家、零行为改动）✓
        //    🔴 顺序纪律：调用**必须**留在 Refresh() ＋「回城就绪」读数**之后** ——
        //       --hamlet-embark 的提前 return 由本调用的返回值承担（true ⇒ _Ready() 立即早退）✓
        if (RunHamletCliAndSmokeFlags(economy, roster))
        {
            return; // 已切场景（--hamlet-embark）
        }
    }
}
