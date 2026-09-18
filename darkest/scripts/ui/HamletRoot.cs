using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;

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

    // 🔴 用户要求（2026-09-16）：以下三块**搬进【建筑详情】**（主屏不再一眼可见）✓
    private HBoxContainer? _reliefRow;
    private HBoxContainer? _recruitRow;
    private HBoxContainer? _saniRow;


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
                locked.AddThemeColorOverride("font_color", Darkest.UI.DdTheme.Disabled);
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
            Modulate = Darkest.UI.DdTheme.Danger, // 🔴 `§14.4`：颜色不得在节点上硬写 ⇒ 走语义色（Embark = 危险红：出发是要付代价的）
        };
        embark.Pressed += () =>
        {
            Darkest.Gameplay.Scene.ExpeditionContext.RequestDungeon(); // 🔴 片 4①：再出发 ⇒ 宿主进地牢 ✓
            GetTree().ChangeSceneToFile(Darkest.UI.MainMenuRoot.BattleScene);
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
                GetTree().CallDeferred("change_scene_to_file", Darkest.UI.MainMenuRoot.BattleScene);
        }
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
        Darkest.UI.DdTheme.Apply(panel);
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
