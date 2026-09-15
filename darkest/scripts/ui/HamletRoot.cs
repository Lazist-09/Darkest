using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M8.0 ③（`#283`）：**回城场景 Hamlet**（局外养成层的入口）。
///
/// 🔴 职责边界（`blueprint §9.15`）：本类**只渲染 + 转发** —— 金钱来自**跨趟持有者**
/// （`ExpeditionContext.Gold`，与经济数据同源），减压/招募按钮在 ④/⑤ 落地前**置灰**
/// （**不假装可用**）。
/// 🔴 红线 18：Hamlet 必须**从启动场景可达**（`BattleRoot` 的按钮 / `--hamlet` CLI）。
/// </summary>
public partial class HamletRoot : Node2D
{
    private Label _status = null!;
    private Label _hint = null!;
    private Label _upgradeStatus = null!;
    private Label _saniStatus = null!;
    // 🔴 片①（三界面卡 §1）：DD 式排布的新增元素
    private Label _banner = null!;
    private Label _rosterCount = null!;
    private Label _resourceBar = null!;

    /// <summary>🔴 P2：建筑**唯一入口**按钮（三栋共用；明细在二级窗口里切换）✓</summary>
    private Button? _buildingEntry;
    private Button? _menuButton;                 // 🔴 P2：底部"☰ 菜单"入口 ✓
    private PanelContainer? _hamletMenu;         // 🔴 P2：城池二级菜单（弹窗）✓
    private VBoxContainer? _hamletMenuBody;
    private string[] _buildingIds = System.Array.Empty<string>();     // 🔴 P3：左列切换用的同一份清单 ✓
    private string[] _buildingLabels = System.Array.Empty<string>();
    private Label _buildingInfo = null!;
    private Label _rosterTitle = null!;
    private VBoxContainer _rosterList = null!; // 🔴 §14：名册竖列的容器（行由 Refresh 填，不再写坐标）
    private Button _embark = null!;
    // 🔴 片②：详情面板的数据源（装配时读入）
    private RosterConfig? _rosterCfgForDetail;
    private SkillsConfig? _skillsCfg;
    private UnitsConfig? _unitsCfg;
    private CampSkillsConfig? _campSkills;
    private SanitariumConfig? _saniCfg;
    private readonly Dictionary<string, Button> _saniButtons = new(); // M8.2：三项服务按钮（用于置灰）
    private EconomyConfig _cfg = null!;
    private string? _selectedHero;                       // ② 选人权：玩家选中的被减压者
    private readonly List<Button> _heroButtons = new();  // 动态重建（士气 < 50 的人）
    private readonly Dictionary<string, Button> _upgradeButtons = new(); // M8.1：三栋升级按钮（用于置灰）
    // 🔴 **二级窗口（弹窗）**：建筑详情 —— 用户 2026-09-14 要求「弹窗要能打开也能关闭」「建筑详细使用走二级窗口」
    private PanelContainer? _buildingPopup;
    private Label? _buildingPopupTitle;
    private VBoxContainer? _buildingPopupBody;
    private string? _buildingPopupId;
    private readonly Darkest.Core.Events.CombatLog _log = new();
    private readonly Darkest.Core.Rng.RngProvider _rng = new(20260909);

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
        var margin = new MarginContainer { Name = "HamletMargin" };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);
        Darkest.Ui.DdTheme.Apply(margin);

        var rootCol = new VBoxContainer { Name = "HamletRootCol" };
        rootCol.AddThemeConstantOverride("separation", 8);
        margin.AddChild(rootCol);

        // ---- 顶栏：横幅（左） + 名册计数（右）----
        var topPanel = new PanelContainer { Name = "TopBar" };
        rootCol.AddChild(topPanel);
        var topRow = new HBoxContainer { Name = "TopRow" };
        topRow.AddThemeConstantOverride("separation", 12);
        topPanel.AddChild(topRow);

        _banner = new Label
        {
            Name = "HamletBanner",
            Text = "未命名庄园 · 回城",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _banner.AddThemeFontSizeOverride("font_size", Darkest.Ui.DdTheme.FontTitle);
        topRow.AddChild(_banner);

        _rosterCount = new Label
        {
            Name = "HamletRosterCount",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        topRow.AddChild(_rosterCount);

        // ---- 状态栏（第二行，独占一条，避免与别的文字压在一起）----
        var statusPanel = new PanelContainer { Name = "StatusBar" };
        rootCol.AddChild(statusPanel);
        // 🔴 相机 1280 口径：状态行**不设最小宽**、允许收缩到 0（内容裁切显示）
        _status = new Label
        {
            Name = "HamletStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 56), // §14.2 ④：给最小高度（防塌陷）
        };
        statusPanel.AddChild(_status);

        // ---- 主体：左栏（操作） ／ 右栏（名册）----
        var body = new HBoxContainer { Name = "Body", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        rootCol.AddChild(body);

        var leftPanel = new PanelContainer { Name = "LeftColumn", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddChild(leftPanel);
        var leftCol = new VBoxContainer { Name = "LeftCol" };
        leftCol.AddThemeConstantOverride("separation", 6);
        leftPanel.AddChild(leftCol);

        var rightPanel = new PanelContainer
        {
            Name = "RightColumn",
            CustomMinimumSize = new Vector2(300, 0), // 🔴 相机 1280 口径：420 → 300（320 时实测 1297，仍超 17px）
        };
        body.AddChild(rightPanel);
        var rightCol = new VBoxContainer { Name = "RightCol" };
        rightCol.AddThemeConstantOverride("separation", 6);
        rightPanel.AddChild(rightCol);

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
        leftCol.AddChild(reliefRow);
        var tavern = new Button { Name = "ReliefTavern", Text = "减压·酒馆（快而不稳）", CustomMinimumSize = new Vector2(220, 34) };
        tavern.Pressed += () => DoRelief("tavern");
        reliefRow.AddChild(tavern);
        var abbey = new Button { Name = "ReliefAbbey", Text = "减压·修道院（慢而稳）", CustomMinimumSize = new Vector2(220, 34) };
        abbey.Pressed += () => DoRelief("abbey");
        reliefRow.AddChild(abbey);

        // 招募（Stage Coach）：按原型选
        var recruitRow = new HBoxContainer { Name = "RecruitRow" };
        recruitRow.AddThemeConstantOverride("separation", 6);
        leftCol.AddChild(recruitRow);
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

        var buildingRow = new HBoxContainer { Name = "BuildingRow" };
        buildingRow.AddThemeConstantOverride("separation", 6);
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
                locked.AddThemeColorOverride("font_color", Darkest.Ui.DdTheme.Disabled);
                buildingRow.AddChild(locked);
                continue;
            }

            // 🔴 DD 式 P2（用户参考图①）：**建筑只留【一个入口】**（用户原话"只有一个按钮没有其他"）——
            //    三栋的明细与切换一并在**二级窗口**里（`BuildingPopup` 左列切换）⇒ 主城本体干净 ✓
            var ub = new Button
            {
                Name = $"BuildingEntry_{bId}",
                Text = buildingEntryText, // 入口文案 = 三栋摘要（见下，一次性算好）
                CustomMinimumSize = new Vector2(176, 30),   // 🔴 收窄（原 220）
            };
            ub.Pressed += () => OpenBuildingPopup(bId);
            ub.MouseEntered += () => ShowBuildingInfo(bId); // 悬停仍给一行摘要（低成本、不占版面）
            buildingRow.AddChild(ub);
            _upgradeButtons[bId] = ub; // ⚠️ 明细按钮在弹窗里（`RefreshBuildingPopup` 重建）；这里三栋都登记到**同一个入口**（`PressUpgrade` 两步路径仍成立）✓
            if (i == 0)
            {
                _buildingEntry = ub; // 只保留第一个作为入口；其余栋不再各建按钮（DD 式"只有一个按钮"）✓
            }
            else
            {
                buildingRow.RemoveChild(ub);
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
        leftCol.AddChild(_buildingInfo);

        _upgradeStatus = new Label
        {
            Name = "UpgradeStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 40),
        };
        leftCol.AddChild(_upgradeStatus);

        // M8.2 Sanitarium 三项服务
        var saniRow = new HBoxContainer { Name = "SaniRow" };
        saniRow.AddThemeConstantOverride("separation", 6);
        leftCol.AddChild(saniRow);
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
        leftCol.AddChild(_saniStatus);

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
        var bottomPanel = new PanelContainer { Name = "BottomBar" };
        rootCol.AddChild(bottomPanel);
        var bottomRow = new HBoxContainer { Name = "BottomRow" };
        bottomRow.AddThemeConstantOverride("separation", 12);
        bottomPanel.AddChild(bottomRow);
        _resourceBar = new Label
        {
            Name = "HamletResourceBar",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        bottomRow.AddChild(_resourceBar);

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
            Modulate = Darkest.Ui.DdTheme.Danger, // 🔴 `§14.4`：颜色不得在节点上硬写 ⇒ 走语义色（Embark = 危险红：出发是要付代价的）
        };
        embark.Pressed += () =>
        {
            Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon(); // 🔴 片 4①：再出发 ⇒ 宿主进地牢 ✓
            GetTree().ChangeSceneToFile(Darkest.Ui.MainMenuRoot.BattleScene);
        };
        bottomRow.AddChild(embark);
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
                GetTree().CallDeferred("change_scene_to_file", Darkest.Ui.MainMenuRoot.BattleScene);
        }
    }

    /// <summary>
    /// **减压**（M8.0 ④）：花钱 → 恢复士气（**唯一**士气出口）→ 副作用掷骰（Tavern 更不稳 / Abbey 更稳）。
    /// 🔴 只转发：钱与士气都在**跨趟持有者**里；UI 不自己算账。
    /// </summary>
    public void DoRelief(string buildingId)
    {
        Economy? economy = ExpeditionContext.Gold;
        Roster? roster = ExpeditionContext.Roster;
        if (economy is null || roster is null)
        {
            return;
        }

        // ② 选人权：**优先用玩家指定的人**（未指定时退化为最低者，便于冒烟）
        string heroId = _selectedHero ?? roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).First().Id;
        StressReliefOutcome o = StressRelief.Apply(_cfg, economy, _rng, _log, buildingId, heroId, roster.MoraleOf(heroId));
        if (o.Paid)
        {
            roster.ApplyRelief(_log, heroId, _cfg.Building(buildingId).MoraleRestore, buildingId);
        }

        GD.Print($"[HamletRoot] 减压·{buildingId}：{(o.Paid ? "成交" : "拒绝（钱不够）")}" +
                 $"　{heroId} 士气 {o.NewMorale}　副作用 {(o.PenaltyTriggered ? $"触发（下趟 −{o.NextRunPenalty}）" : "未触发")}" +
                 $"　剩余金钱 {economy.Gold}");
        Refresh();
    }

    /// <summary>**招募**（M8.0 ⑤）：免费；新兵 Lv1 / 士气 50；满员即拒绝（不悄悄顶替）。</summary>
    public void Recruit()
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            return;
        }

        // 挑一个"人最少的原型"，名字用序号（最小实现；将来由玩家选）
        string archetype = roster.Heroes
            .GroupBy(h => h.Archetype)
            .OrderBy(g => g.Count())
            .First().Key;
        HeroConfig? rookie = roster.Recruit(_log, _cfg.Coach, archetype, $"新兵{roster.Heroes.Count + 1}");

        GD.Print(rookie is null
            ? $"[HamletRoot] 招募：**名册已满**（{roster.Heroes.Count}/{_cfg.Coach.MaxRoster}）—— 拒绝（不悄悄顶替）"
            : $"[HamletRoot] 招募：{rookie.Name}（{rookie.Archetype} Lv{rookie.Level} 士气{rookie.Morale}）**免费**" +
              $"　名册 {roster.Heroes.Count}/{_cfg.Coach.MaxRoster}");
        Refresh();
    }

    /// <summary>**选中某位英雄**（② 选人权：减压必须由玩家指定对象，不是"自动挑最低的"）。</summary>
    public void SelectHero(string heroId)
    {
        _selectedHero = heroId;
        Roster? roster = ExpeditionContext.Roster;
        int morale = roster?.MoraleOf(heroId) ?? 0;
        GD.Print($"[HamletRoot] 已选中 {heroId}（当前士气 {morale}）⇒ 再点酒馆/修道院减压");
        Refresh();
    }

    /// <summary>**招募指定原型**（⑤：玩家决定招哪种人；免费 / Lv1 / morale 50 / 满员拒绝）。</summary>
    public void RecruitArchetype(string archetype)
    {
        Roster? roster = ExpeditionContext.Roster;
        if (roster is null)
        {
            return;
        }

        HeroConfig? rookie = roster.Recruit(_log, _cfg.Coach, archetype, $"新兵{roster.Heroes.Count + 1}");
        GD.Print(rookie is null
            ? $"[HamletRoot] 招募·{archetype}：**名册已满**（{roster.Heroes.Count}/{roster.Cap}）—— 拒绝（不悄悄顶替）"
            : $"[HamletRoot] 招募·{archetype}：{rookie.Name}（Lv{rookie.Level} 士气{rookie.Morale}）**免费**" +
              $"　名册 {roster.Heroes.Count}/{roster.Cap}");
        Refresh();
    }

    /// <summary>**升级建筑**（M8.1）：按曲线扣传家宝；不足即拒绝（不部分扣）；升级后**生效值真的改变**。</summary>
    public void UpgradeBuilding(string building)
    {
        HeirloomStock? h = ExpeditionContext.Heirlooms;
        if (h is null)
        {
            return;
        }

        UpgradeLevel? next = h.NextLevel(building);
        bool ok = h.TryUpgrade(_log, building);
        GD.Print(ok
            ? $"[HamletRoot] 升级·{building} ⇒ Lv{h.LevelOf(building)}（花 {string.Join("/", next!.Cost.Select(k => $"{k.Key}×{k.Value}"))}）" +
              $"　生效：减压价 {h.EffectiveReliefCost(_cfg.StressReliefCost)}　名册上限 {h.EffectiveRosterCap(baseCap: ExpeditionContext.Roster?.Heroes.Count ?? 0, hardCap: 12)}"
            : $"[HamletRoot] 升级·{building}：**传家宝不足或已满级** ⇒ 拒绝（不部分扣）");
        Refresh();
    }

    // ------------------------------------------------------------------
    // 🔴 片② 角色详情（`tasks/ui_three_screens.md` §2）—— **城池右侧名册点行 ⇒ 打开**（唯一入口）
    //    实现取舍：**用 Hamlet 内的覆盖面板**（不新建场景）⇒ 无需场景路由；
    //    "返回城池"= 关闭面板（红线 18：不是孤岛）。数据全部真读既有持有者。
    // ------------------------------------------------------------------

    private PanelContainer? _detailPanel; // 🔴 §14：详情面板 = 满屏不透明 PanelContainer（不再是 Node2D 浮层）
    private Label? _detailLeft;
    private Label? _detailRight;
    private Label? _detailCampSkills;
    private HBoxContainer? _detailSkills;          // 🔴 P4：技能图标行（图标 + tooltip 讲解）✓
    private PanelContainer? _detailRecommend;      // 🔴 P4：右上"推荐位置"留框 ✓
    private string? _detailHeroId;

    /// <summary>详情面板是否已打开（供冒烟断言）。</summary>
    public bool DetailOpen => _detailPanel is not null && _detailPanel.Visible;

    /// <summary>🔴 供冒烟/自检：**建筑详情弹窗（二级窗口）是否打开** —— 判据"能开也能关"的可断言读数 ✓</summary>
    public bool BuildingPopupOpen => _buildingPopup is not null && _buildingPopup.Visible;

    /// <summary>当前详情显示的是谁（供冒烟断言"显示的是被点的那个人"）。</summary>
    public string? DetailHeroId => _detailHeroId;

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
            // 🔴 `ui_spec §14`（`#319`）：详情面板**不再用绝对坐标 + 也不再是透明浮层** ——
            //    改【**满屏 `PanelContainer`（不透明）+ 容器树**】：Margin → VBox（标题行 ／ 左右两栏 ／ 返回行）
            //    左栏 `VBox`（立绘/属性/特质/疾病）· 右栏 `VBox`（技能/抗性/扎营技能/装备）✓
            var detailRoot = new PanelContainer { Name = "HeroDetailPanel" };
            detailRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            // ⚠️ **必须显式挂 Theme**：本节点是 `HamletRoot`(Node2D) 的子节点，**不在带 Theme 的 `margin` 之下**
            //    ⇒ 否则它用**引擎默认面板样式**（实测 `a=0.6` ⇒ 判据 2 直接抓到"框透明"）✓
            Darkest.Ui.DdTheme.Apply(detailRoot);
            AddChild(detailRoot);
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
            dTitle.ThemeTypeVariation = Darkest.Ui.DdTheme.TitleVariation; // 🔴 架构裁定②：标题用 Bold
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
            dLeftCol.AddThemeConstantOverride("separation", 6);
            dBody.AddChild(dLeftCol);
            var dRightCol = new VBoxContainer { Name = "DetailRightCol", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            dRightCol.AddThemeConstantOverride("separation", 6);
            dBody.AddChild(dRightCol);

            _detailLeft = new Label
            {
                Name = "DetailLeft",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(360, 380),
            };
            dLeftCol.AddChild(_detailLeft);

            // 🔴 P4（用户参考图④）：**技能 = 图标 + 悬停 tooltip 讲解**（不再是大段文字行）——
        //    图标 = 立绘留框同款（不透明面板样式 1px 边框 + 色块占位）；讲解走 `TooltipText` ✓
        _detailSkills = new HBoxContainer { Name = "DetailSkillIcons" };
        _detailSkills.AddThemeConstantOverride("separation", 6);
        dRightCol.AddChild(_detailSkills);

        // 🔴 P4：**右上角"推荐位置"留框**（用户原话"这个留一个框后面做都可以"）✓
        _detailRecommend = new PanelContainer { Name = "DetailRecommendSlot", CustomMinimumSize = new Vector2(0, 44) };
        dRightCol.AddChild(_detailRecommend);
        var recRow = new HBoxContainer { Name = "DetailRecommendRow" };
        recRow.AddThemeConstantOverride("separation", 6);
        _detailRecommend.AddChild(recRow);
        recRow.AddChild(new PanelContainer { Name = "RecommendPortraitFrame", CustomMinimumSize = new Vector2(36, 36) });
        recRow.AddChild(new Label { Name = "RecommendText", Text = "推荐位置（待定）", VerticalAlignment = VerticalAlignment.Center });

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
                        Color = Darkest.Ui.DdTheme.ArchetypeColor(hero.Archetype, isPlayer: true),
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

    /// <summary>🔴 片① ③：**悬停/点击某栋建筑 ⇒ 显示名称 + 功能 + 当前等级 + 下一级所需传家宝** ——
    /// **真读 `HeirloomStock`（`LevelOf` / `NextLevel().Cost`）**，不是写死文本（卡 §1.3 的验收要求）。
    /// </summary>
    /// <summary>
    /// 🔴 `next_round` ③ 辅助：某个解锁目标（如 `building:tavern`）需要**第几趟** ——
    /// 用于给"锁着的建筑"写出**可解释的**解锁条件（红线 21：不留不可解释的禁用）✓
    /// </summary>
    internal static int RunsRequiredFor(UnlocksConfig cfg, string target)
    {
        foreach (UnlockEntry e in cfg.Unlocks)
        {
            if (e.Unlocks.Contains(target))
            {
                return e.RequiredRunsFinished > 0 ? e.RequiredRunsFinished : 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// 🔴 **二级窗口（弹窗）工厂**（用户 2026-09-14：「弹窗要能打开、也要能关闭」）：
    /// 满屏不透明 `PanelContainer`（判据据此把它认成**模态** ⇒ 只审它内部）+ 标题行（**含 `✕ 关闭` 按钮**）+ 内容 `VBox`。
    /// ⚠️ 两条纪律：① **必须显式挂 Theme**（本屏根是 `Node2D`，主题链不经过它 ⇒ 否则框是引擎默认 `a=0.6`）
    ///            ② **`Esc`（`ui_cancel`）也必须能关**（"能开不能关"是弹窗最常见的坑）✓
    /// </summary>
    private (PanelContainer Panel, Label Title, VBoxContainer Body) MakePopup(string name, string titleText)
    {
        var panel = new PanelContainer { Name = name, Visible = false };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Darkest.Ui.DdTheme.Apply(panel);
        AddChild(panel);

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
        title.ThemeTypeVariation = Darkest.Ui.DdTheme.TitleVariation; // 🔴 架构裁定②：标题用 Bold（不再逐处写字号）
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
        }
        else
        {
            GD.Print("[城池菜单] 库存：**本趟无背包**（`ExpeditionContext.Flow` 为空）⇒ 不显示该项（红线 21：不假装可用）✓");
        }

        _hamletMenu.Visible = true;
        GD.Print($"[城池菜单] 打开：建筑{(heroPick is null ? "" : " ／ 角色详情")}{(bag is null ? "" : " ／ 库存")}" +
                 $"（只列**当前可用**项）✓");
    }

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

            // 🔴 P3（用户参考图③）：**左列切换建筑** + **店长位留框**　｜　**右侧 = 建筑内容** ✓
            var split = new HBoxContainer { Name = "BuildingSplit", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            split.AddThemeConstantOverride("separation", 12);
            body.AddChild(split);

            var leftCol = new VBoxContainer { Name = "BuildingList", CustomMinimumSize = new Vector2(230, 0) };
            leftCol.AddThemeConstantOverride("separation", 6);
            split.AddChild(leftCol);

            // 🔴 **店长位留框**（用户原话"为店长位置留一个空间"）：不透明面板样式(1px 边框) + **色块占位** ⇒ 以后只换里面那格 ✓
            var shopFrame = new PanelContainer { Name = "ShopkeeperSlot", CustomMinimumSize = new Vector2(0, 96) };
            leftCol.AddChild(shopFrame);
            shopFrame.AddChild(new ColorRect { Name = "ShopkeeperPlaceholder", Color = Darkest.Ui.DdTheme.PanelBgRaised });

            for (int k = 0; k < _buildingIds.Length; k++)
            {
                string bid = _buildingIds[k];
                var nav = new Button
                {
                    Name = $"BuildingNav_{bid}",
                    Text = _buildingLabels[k],
                    CustomMinimumSize = new Vector2(220, 32),
                };
                nav.Pressed += () => { _buildingPopupId = bid; RefreshBuildingPopup(); }; // 🔴 左列切换（只换右侧内容）✓
                leftCol.AddChild(nav);
            }

            var rightCol = new VBoxContainer { Name = "BuildingContent", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            rightCol.AddThemeConstantOverride("separation", 6);
            split.AddChild(rightCol);
            _buildingPopupBody = rightCol; // 右侧 = 建筑内容（原 `body` 的职责）✓
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

    private static Label PopupLine(string text) => new()
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

    /// <summary>🔴 片① ⑥（冒烟）：**真实点击 Embark（再出发）** ⇒ 切 `Expedition.tscn`（红线 18）。</summary>
    public void PressEmbark()
    {
        GD.Print("[HamletRoot] PressEmbark：发出真实 Pressed（再出发 · EMBARK）");
        _embark.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>🔴 片① ④（冒烟）：**真实点击右列第 i 行名册**。</summary>
    public bool PressRosterRow(int index)
    {
        if (index < 0 || index >= _heroButtons.Count)
        {
            GD.Print($"[HamletRoot] PressRosterRow({index})：没有这一行（当前 {_heroButtons.Count} 行）");
            return false;
        }

        GD.Print($"[HamletRoot] PressRosterRow({index})：发出真实 Pressed（「{(_heroButtons[index].Text is { Length: > 0 } t ? t : _heroButtons[index].TooltipText)}」）");
        _heroButtons[index].EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>DD 名册行：**等级** —— 来自 `roster.json`（取不到 ⇒ 0，不编数字）✓</summary>
    private int LevelOfHero(string heroId)
        => _rosterCfgForDetail?.Heroes.FirstOrDefault(x => x.Id == heroId)?.Level ?? 0;

    /// <summary>DD 名册行：**防御等级** —— 来自 `units.json` 的该原型 `dodge`（取不到 ⇒ "?"，不编数字）✓</summary>
    private string DodgeOfHero(HeroConfig h)
    {
        int? dodge = _unitsCfg?.Units.FirstOrDefault(u => u.Id == h.Archetype)?.Dodge;
        return dodge?.ToString() ?? "?";
    }

    /// <summary>🔴 冒烟：**右键头像**（真实走 `GuiInput` 处理器 ⇒ 与玩家同一条路径）⇒ 打开角色详情 ✓</summary>
    public bool PressPortraitRightClick(int index)
    {
        if (index < 0 || index >= _heroButtons.Count)
        {
            GD.Print($"[HamletRoot] 右键头像({index})：没有这一行（当前 {_heroButtons.Count} 行）");
            return false;
        }

        Control? frame = _heroButtons[index].GetNodeOrNull<Control>("RosterRowBody/PortraitFrame");
        if (frame is null)
        {
            GD.Print("[HamletRoot] 右键头像：**找不到头像框**（红线 21：控件没挂上 ⇒ 如实报，不静默）");
            return false;
        }

        var ev = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
        GD.Print($"[HamletRoot] 右键头像({index})：发出真实 `GuiInput`（右键按下）⇒ 打开角色详情");
        frame.EmitSignal(Control.SignalName.GuiInput, ev);
        return true;
    }

    /// <summary>供冒烟：名册竖列的行数（应等于名册人数）。</summary>
    public int RosterRowCount => _heroButtons.Count;

    /// <summary>
    /// 🔴 M8.2 / V16：**Sanitarium 服务的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// </summary>
    public void PressService(string serviceName)
    {
        if (!_saniButtons.TryGetValue(serviceName, out Button? btn))
        {
            GD.Print($"[HamletRoot] PressService({serviceName})：找不到按钮（红线 21）");
            return;
        }

        GD.Print($"[HamletRoot] PressService({serviceName})：发出真实 Pressed 信号（按钮「{btn.Text}」，置灰={btn.Disabled}）");
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>**执行一项 Sanitarium 服务**：挑对象（优先玩家选中的、否则找有病/可改的人）→ 调用内核 → 打印结果。</summary>
    public void DoService(string serviceName)
    {
        Roster? roster = ExpeditionContext.Roster;
        Economy? economy = ExpeditionContext.Gold;
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        if (roster is null || economy is null || heirlooms is null || _saniCfg is null)
        {
            return;
        }

        // 选对象：优先玩家选中的人；否则按服务挑一个"有事可做"的（有病 / 有负面特质 / 有正面特质）
        string? hero = _selectedHero;
        hero ??= serviceName switch
        {
            "cure_disease" => roster.Heroes.FirstOrDefault(h => roster.DiseasesOf(h.Id).Count > 0)?.Id,
            "remove_negative_trait" => roster.Heroes.FirstOrDefault(h => roster.FindRemovableNegativeTrait(h.Id) is not null)?.Id,
            _ => roster.Heroes.FirstOrDefault(h => roster.FindLockablePositiveTrait(h.Id) is not null)?.Id,
        };

        if (hero is null)
        {
            GD.Print($"[HamletRoot] Sanitarium·{serviceName}：**没有可用对象**（拒绝对空做事）");
            Refresh();
            return;
        }

        CureOutcome o = serviceName switch
        {
            "cure_disease" => CureFirstDisease(roster, economy, heirlooms, hero),
            "remove_negative_trait" => Sanitarium.RemoveNegativeTrait(_log, _saniCfg, economy, heirlooms, roster, hero),
            _ => Sanitarium.LockPositiveTrait(_log, _saniCfg, economy, heirlooms, roster, hero),
        };

        GD.Print($"[HamletRoot] Sanitarium·{serviceName}：{(o.Paid ? "成交" : "拒绝（钱/传家宝不足，或无事可做）")}" +
                 $"　对象 {hero}　花 金钱{o.GoldSpent} 加 传家宝[{o.HeirloomSpent}]");
        Refresh();
    }

    private CureOutcome CureFirstDisease(Roster roster, Economy economy, HeirloomStock heirlooms, string heroId)
    {
        foreach (string d in roster.DiseasesOf(heroId).ToArray())
        {
            return Sanitarium.CureDisease(_log, _saniCfg!, economy, heirlooms, roster, heroId, d);
        }

        return new CureOutcome(false, 0, string.Empty);
    }

    /// <summary>刷新（只读跨趟状态，不自己算账）。</summary>
    public void Refresh()
    {
        int gold = ExpeditionContext.Gold?.Gold ?? 0;
        int cost = ExpeditionContext.Gold?.StressReliefCost ?? 0;
        int affordable = cost <= 0 ? 0 : gold / cost;
        Roster? roster = ExpeditionContext.Roster;
        string moraleLine = roster is null
            ? "名册：未加载"
            : "名册士气：" + string.Join("、", roster.Heroes
                .OrderByDescending(h => roster.MoraleOf(h.Id))
                .Select(h => $"{h.Name}{roster.MoraleOf(h.Id)}"));
        _status.Text =
            $"【Hamlet 回城】金钱 {gold}　一次减压 {cost} ⇒ 现在能减 {affordable} 次\n" +
            moraleLine + "\n" +
            "④ 减压已可用（Tavern 快而不稳 ／ Abbey 慢而稳，**同价同效**）；⑤ 招募随后落地。";

        // ② 选人权：刷新"可减压者"按钮（名册里**士气低于基准 50** 的人）
        foreach (Button b in _heroButtons)
        {
            b.QueueFree();
        }

        _heroButtons.Clear();
        if (roster is not null)
        {
            // 🔴 片① ④：**右侧名册竖列**（照 DD）—— 每行 = 缩写头像 + 名字 + **士气点阵** + 装备位占位
            //    ⚠️ 红线 21：装备位显式标「未实现」；「可减压」标记保留（士气 < 基准者才可减压）
            int row = 0;
            foreach (HeroConfig h in roster.Heroes)
            {
                string id = h.Id;
                int morale = roster.MoraleOf(id);
                string dots = new string('●', Math.Clamp(morale / 10, 0, 10)).PadRight(10, '○');
                string abbrev = h.Name.Length > 0 ? h.Name[..1] : "?";
                bool canRelief = morale < RosterConfig.RookieMorale;
                int lv = LevelOfHero(id);
                string dodge = DodgeOfHero(h);
                // 🔴 DD 式紧凑行（用户参考图①）：**立绘留框（色块占位） + 等级 + 压力点阵 + 防御** ✓
                //    框 = `PanelContainer`（主题不透明面板样式 ⇒ 自带 1px 边框）⇒ 以后放立绘只换里面那格 ✓
                //    文字走**子 Label**（按钮自身 `Text` 置空，避免与子控件叠字）✓
                var b = new Button
                {
                    Name = $"RosterRow_{id}",
                    CustomMinimumSize = new Vector2(232, 32),   // 🔴 相机 1280 口径收窄（原 300）
                    TooltipText = $"{h.Name}　Lv{lv}　士气 {morale}　防御 {dodge}{(canRelief ? "　·可减压" : string.Empty)}",
                };
                var rowBody = new HBoxContainer { Name = "RosterRowBody" };
                rowBody.AddThemeConstantOverride("separation", 6);
                rowBody.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                b.AddChild(rowBody);
                var frame = new PanelContainer { Name = "PortraitFrame", CustomMinimumSize = new Vector2(26, 26) };
                rowBody.AddChild(frame);
                frame.AddChild(new ColorRect
                {
                    Name = "PortraitPlaceholder", // 🔴 色块占位（`§14.4` 原型色 ⇒ 不硬写字面量）✓
                    Color = Darkest.Ui.DdTheme.ArchetypeColor(h.Archetype, isPlayer: true),
                });

                // 🔴 **用户要求（2026-09-15）：角色详情 = 【右键头像】点开**（左键点行仍是"选中"，供减压用）✓
                frame.MouseFilter = Control.MouseFilterEnum.Stop; // 头像要自己收鼠标事件（否则被按钮吃掉）
                frame.TooltipText = "右键 ⇒ 打开角色详情";
                frame.GuiInput += (InputEvent ev) =>
                {
                    if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
                    {
                        GD.Print($"[HamletRoot] **右键头像** ⇒ 打开角色详情：{id}");
                        OpenHeroDetail(id);
                    }
                };
                var info = new Label
                {
                    Name = "RosterInfo",
                    Text = $"Lv{lv}　{dots}　防{dodge}{(canRelief ? "　·可减压" : string.Empty)}",
                    VerticalAlignment = VerticalAlignment.Center,
                    // 🔴 相机 1280 口径（规则①）：行内文本**可收缩 + 裁切**（否则长文本把整行撑宽 ⇒ 实测长文本下 4 处越界）✓
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    ClipText = true,
                    TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                    CustomMinimumSize = new Vector2(0, 0),
                };
                info.AddThemeFontSizeOverride("font_size", Darkest.Ui.DdTheme.FontSmall);
                rowBody.AddChild(info);
                b.Pressed += () =>
                {
                    SelectHero(id);          // 左键 = **选中**（减压按人选）
                    GD.Print($"[HamletRoot] 左键选中 {id}（角色详情请**右键头像**打开 —— 用户 2026-09-15 要求）✓");
                };
                _rosterList.AddChild(b); // 🔴 §14：填进名册容器（容器自动堆叠 ⇒ 不可能重叠）✓
                _heroButtons.Add(b);
                row++;
            }
        }

        _hint.Text = roster is null
            ? "减压：名册未加载"
            : _selectedHero is null
                ? "减压：请先在右侧名册点一位【可减压】的人，再点酒馆/修道院（同价同效、风险不同）"
                : $"减压对象：{_selectedHero}（士气 {roster.MoraleOf(_selectedHero)}）⇒ 请点酒馆或修道院";

        // 🔴 片①：**名册计数 / 资源条 / 建筑信息默认行**（都真读跨趟持有者，不写死）
        _rosterCount.Text = roster is null
            ? "名册 -/-"
            : $"名册 {roster.Heroes.Count} / {((roster.CurrentCap > 0) ? roster.CurrentCap : roster.Cap)}" +
              $"（上限 {roster.Cap}）";
        HeirloomStock? resHeirlooms = ExpeditionContext.Heirlooms;
        Economy? resGold = ExpeditionContext.Gold;
        _resourceBar.Text = resGold is null || resHeirlooms is null
            ? "资源：未加载"
            : $"💰 金钱 {resGold.Gold}　｜　传家宝：" +
              string.Join("　", resHeirlooms.Kinds.Select(k => $"{k} {resHeirlooms.Count(k)}"));
        if (string.IsNullOrEmpty(_buildingInfo.Text))
        {
            _buildingInfo.Text = "建筑：悬停/点击某一栋 ⇒ 显示名称 + 功能 + 当前等级 + 下一级所需传家宝";
        }

        // 🔴 M8.2 / V15：Sanitarium 三服务的**成本显示 + 可用性置灰**（红线 21 (b)：由内核回答）
        HeirloomStock? saniHeirlooms = ExpeditionContext.Heirlooms;
        if (_saniCfg is not null && saniHeirlooms is not null && ExpeditionContext.Gold is not null)
        {
            int saniAffordable = 0;
            foreach ((string s, Button btn) in _saniButtons)
            {
                SanitariumService svc = _saniCfg.Service(s);
                string svcCost = $"{svc.Gold}金＋{string.Join("/", svc.Heirlooms.Select(k => $"{k.Key}×{k.Value}"))}";
                bool can = Sanitarium.CanAfford(_saniCfg, s, ExpeditionContext.Gold, saniHeirlooms);
                btn.Disabled = !can;
                btn.Text = $"Sanitarium·{s}（{svcCost}）";
                saniAffordable += can ? 1 : 0;
            }

            int sick = roster?.Heroes.Count(h => roster.DiseasesOf(h.Id).Count > 0) ?? 0;
            _saniStatus.Text = $"Sanitarium：可支付 {saniAffordable}/3 项服务（不足即置灰）　患病英雄 {sick} 人" +
                               $"　负面特质可除 {roster?.Heroes.Count(h => roster.FindRemovableNegativeTrait(h.Id) is not null) ?? 0} 人";
        }

        // 🔴 M8.1：传家宝库存 + 三栋建筑的等级与**生效值**（升级真的改变数字）
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        if (heirlooms is not null)
        {
            string stock = string.Join(" ／ ", heirlooms.Kinds.Select(k => $"{k}×{heirlooms.Count(k)}"));
            string levels = string.Join(" ／ ", new[] { "tavern", "abbey", "stagecoach" }
                .Select(b => $"{b} Lv{heirlooms.LevelOf(b)}"));
            _upgradeStatus.Text =
                $"传家宝：{stock}\n建筑：{levels}　⇒ 减压价 {heirlooms.EffectiveReliefCost(_cfg.StressReliefCost)}" +
                $"　恢复量 {heirlooms.EffectiveMoraleRestore("tavern", _cfg.StressRelief!.Buildings[0].MoraleRestore)}" +
                $"　新兵起始等级 {heirlooms.EffectiveRookieLevel(_cfg.Coach.RookieLevel)}";

            // 🔴 红线 21 (b)：**按钮可用性由内核回答**（传家宝不足或已满级 ⇒ 置灰；不假装可用）
            foreach ((string b, Button btn) in _upgradeButtons)
            {
                bool can = heirlooms.CanUpgrade(b);
                btn.Disabled = !can;
                UpgradeLevel? next = heirlooms.NextLevel(b);
                if (ReferenceEquals(btn, _buildingEntry)) { continue; } // 🔴 入口按钮文案由摘要统一写（不在按栋循环里覆盖）

                // 🔴 按钮文案带上【当前等级】（玩家一眼看得到），详细使用仍走点击后的二级窗口 ✓
                btn.Text = next is null
                    ? $"🏛 {b}　Lv{heirlooms.LevelOf(b)}（已满级）"
                    : $"🏛 {b}　Lv{heirlooms.LevelOf(b)} ⇒ Lv{heirlooms.LevelOf(b) + 1}（需 {string.Join("/", next.Cost.Select(k => $"{k.Key}×{k.Value}"))}）";
            }
        }
    }

    /// <summary>
    /// 🔴 M8.1 / 红线 21 (b)：**升级按钮的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// </summary>
    /// 🔴 M8.1 / 红线 21 (b)：**升级的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// 🔴 用户 2026-09-14 改版后（建筑详细使用走**二级窗口**），本方法 = **完整玩家两步路径**：
    ///    ① 真实按下【建筑按钮】⇒ 打开建筑详情弹窗　② 再真实按下【弹窗里的升级按钮】⇒ 真正升级 ✓
    ///    ⚠️ 不能只按第一步（那只开弹窗、不升级）——否则 e2e 那句"升级后减压价变了"的证据会**静默失真**（红线 25）✓
    /// </summary>
    public void PressUpgrade(string building)
    {
        if (!_upgradeButtons.TryGetValue(building, out Button? btn))
        {
            GD.Print($"[HamletRoot] PressUpgrade({building})：找不到按钮（红线 21：按钮没挂上）");
            return;
        }

        GD.Print($"[HamletRoot] PressUpgrade({building})：① 发出真实 Pressed（按钮「{btn.Text}」，置灰={btn.Disabled}）⇒ 开二级窗口");
        btn.EmitSignal(BaseButton.SignalName.Pressed);

        Button? up = _buildingPopupBody?.GetNodeOrNull<Button>("PopupUpgrade");
        if (up is null)
        {
            GD.Print($"[HamletRoot] PressUpgrade({building})：弹窗里没有升级按钮（没打开？）⇒ 未升级");
            return;
        }

        if (up.Disabled)
        {
            GD.Print($"[HamletRoot] PressUpgrade({building})：② 升级按钮**置灰**（传家宝不足/已满级）⇒ 不改等级（红线 21）");
            return;
        }

        GD.Print($"[HamletRoot] PressUpgrade({building})：② 发出真实 Pressed（弹窗按钮「{up.Text}」）⇒ 升级");
        up.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
