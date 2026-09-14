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
            CustomMinimumSize = new Vector2(420, 0), // 名册列固定宽度（否则会被左栏挤扁）
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

            var ub = new Button
            {
                Name = $"Upgrade_{bId}",
                Text = $"🏛 {label}",
                CustomMinimumSize = new Vector2(180, 32),
            };
            ub.Pressed += () => UpgradeBuilding(bId);
            ub.MouseEntered += () => ShowBuildingInfo(bId);
            buildingRow.AddChild(ub);
            _upgradeButtons[bId] = ub;
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
                CustomMinimumSize = new Vector2(230, 32),
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

        var embark = new Button
        {
            Name = "Embark",
            Text = "再出发（远征）· EMBARK",
            CustomMinimumSize = new Vector2(280, 44),
            Modulate = new Color(1.0f, 0.35f, 0.35f),
        };
        embark.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/expedition/Expedition.tscn");
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
            GetTree().CallDeferred("change_scene_to_file", "res://scenes/expedition/Expedition.tscn");
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
    private string? _detailHeroId;

    /// <summary>详情面板是否已打开（供冒烟断言）。</summary>
    public bool DetailOpen => _detailPanel is not null && _detailPanel.Visible;

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
            dTitle.AddThemeFontSizeOverride("font_size", Darkest.Ui.DdTheme.FontTitle);
            dCol.AddChild(dTitle);

            var dBody = new HBoxContainer { Name = "DetailBody", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            dBody.AddThemeConstantOverride("separation", 10);
            dCol.AddChild(dBody);

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

        // ④ 战斗技能 5 个（该原型；悬停 tooltip 的文本直接展开，避免依赖 tooltip 机制）
        right.AppendLine("【战斗技能】");
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

        GD.Print($"[HamletRoot] PressRosterRow({index})：发出真实 Pressed（「{_heroButtons[index].Text}」）");
        _heroButtons[index].EmitSignal(BaseButton.SignalName.Pressed);
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
                var b = new Button
                {
                    Name = $"RosterRow_{id}",
                    Text = $"{abbrev} {h.Name} {dots} ⚔- 🛡-（未实现）{(canRelief ? " · 可减压" : string.Empty)}",
                    CustomMinimumSize = new Vector2(400, 28),
                };
                b.Pressed += () =>
                {
                    SelectHero(id);          // 保留既有"减压按人选"
                    OpenHeroDetail(id);      // 🔴 片②：**点行 ⇒ 打开角色详情**（唯一入口）
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
                btn.Text = next is null ? $"升级·{b}（已满级）" : $"升级·{b}（需 {string.Join("/", next.Cost.Select(k => $"{k.Key}×{k.Value}"))}）";
            }
        }
    }

    /// <summary>
    /// 🔴 M8.1 / 红线 21 (b)：**升级按钮的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// </summary>
    public void PressUpgrade(string building)
    {
        if (!_upgradeButtons.TryGetValue(building, out Button? btn))
        {
            GD.Print($"[HamletRoot] PressUpgrade({building})：找不到按钮（红线 21：按钮没挂上）");
            return;
        }

        GD.Print($"[HamletRoot] PressUpgrade({building})：发出真实 Pressed 信号（按钮「{btn.Text}」，置灰={btn.Disabled}）");
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
