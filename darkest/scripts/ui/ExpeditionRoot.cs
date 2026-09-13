using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 远征场景层**骨架**（M7.5 接线）：持有面板与内核对象的**引用**，把玩家操作转发给内核，再刷新面板。
///
/// 🔴 分工红线（与 `ExpeditionListPanel` 一致）：
/// · **所有状态与判定在内核**（`ExpeditionSession` / `LightMeter` / `Inventory` / `Scouting`）；
///   本类**不做规则判断、不算数字、不掷骰**；
/// · 面板文本**一律**来自 `ExpeditionProjector.RenderList(...)`（数字来自事件流）；
/// · 数据（tuning / nodes）由外部注入（与 `BattleRoot` 同源加载），本类不自己读文件。
///
/// 已接线的交互：**事件二选一（无跳过）** · 扎营入口（柴火不足则按钮禁用） · 回城结算面板 · 面板刷新。
/// 未接线（下一件事）：地图/选路界面切换、背包格子拖放、光照条可视化控件、与战斗场景的往返。
/// </summary>
public partial class ExpeditionRoot : Node
{
    private ExpeditionListPanel _panel = null!;
    private Button _choiceA = null!;
    private Button _choiceB = null!;
    private Button _campButton = null!;
    private readonly List<Button> _buttons = new();

    /// <summary>内核对象（由外部注入；本类只转发，不构造、不改写）。</summary>
    public ExpeditionSession? Session { get; private set; }

    public LightMeter? Meter { get; private set; }

    public Inventory? Bag { get; private set; }

    public TuningConfig? Tuning { get; private set; }

    public ExpeditionNodesConfig? Nodes { get; private set; }

    public CombatLog Log { get; } = new();

    /// <summary>面板最后渲染的行（供自检/冒烟）。</summary>
    public IReadOnlyList<string> LastLines { get; private set; } = Array.Empty<string>();

    /// <summary>M7.5：远征流程状态机（内核；场景层只驱动）。</summary>
    private ExpeditionFlow? _flow;

    /// <summary>最小版选路：交替选（真实玩家的选路来自 `PathChoicePanel`）。</summary>
    private int _nextOption;

    private LightBarPanel? _lightBar;
    private ScoutMarkPanel? _scoutMark;
    private PathChoicePanel? _pathPanel;

    public override void _Ready()
    {
        _panel = new ExpeditionListPanel { Name = "ExpeditionListPanel" };
        AddChild(_panel);

        // 场景里已挂好的三个面板（节点树见 scenes/expedition/Expedition.tscn）
        _lightBar = GetNodeOrNull<LightBarPanel>("LightBarPanel");
        _scoutMark = GetNodeOrNull<ScoutMarkPanel>("ScoutMarkPanel");
        _pathPanel = GetNodeOrNull<PathChoicePanel>("PathChoicePanel");

        // 事件二选一（**不允许跳过** ⇒ 只有两个选项按钮）
        _choiceA = MakeButton("选项 A", new Vector2(24, 660), () => ChooseEventOption(0));
        _choiceB = MakeButton("选项 B", new Vector2(280, 660), () => ChooseEventOption(1));

        // 扎营入口（柴火不足 ⇒ 禁用 = 灰显）
        _campButton = MakeButton("扎营（1 柴火）", new Vector2(536, 660), Camp);

        _panel.ShowPanel();

        NewExpedition(); // 与 BattleRoot 同款：_Ready 即装配（数据经 DirectorBridge 读 res://data）
        ShowPathChoice(); // 首步：把两个候选交给选路界面（玩家点选后才推进）

        // 🔴 M7.6 片 (iii)：**UI 地图视图** —— `--topology` ⇒ 生成地图并**把选路交给玩家点**（红线 18：玩家要碰得到）
        //    `--topology-auto` ⇒ 仍自动走一遍（供冒烟/读数，不改变玩家路径）
        string[] args = OS.GetCmdlineArgs();
        if (System.Array.Exists(args, a => a == "--topology" || a == "--topology-auto"))
        {
            ExpeditionMapConfig mapCfg = ExpeditionMapConfig.Parse(
                Godot.FileAccess.GetFileAsString(ExpeditionMapConfig.ResPath));
            // 🔴 返程守卫：**已在拓扑模式 ⇒ 复用同一张地图**（不重掷）
            if (!_flow!.IsTopologyMode)
            {
                ExpeditionMap map = _flow.BeginTopology(mapCfg);
                GD.Print($"[拓扑] 地图生成：主干 {map.Rooms.Count(r => !r.IsBranch)} 间 ／ 支路 {map.BranchCount} 条 ／ " +
                         $"分叉点 {map.ForkCount} 个 ／ 连通 {map.IsConnected()}");
            }
            else
            {
                GD.Print($"[拓扑] 返程：**复用同一张地图**（当前房间 {_flow.CurrentRoomId} ／ 已走 {_flow.StepsDone} 段）");
            }

            // 🔴 自动走（仅冒烟/读数用）
            if (System.Array.Exists(args, a => a == "--topology-auto"))
            {
                var path = new List<string>();
                int guard = 0;
                while (!_flow.ReachedGoal && guard++ < 40)
                {
                    IReadOnlyList<MapRoom> options = _flow.AdjacentUnexplored();
                    if (options.Count == 0)
                    {
                        break;
                    }

                    MapRoom next = options[0];
                    MoveOutcome o = _flow.StepTo(next.Id);
                    path.Add($"{next.Type}({o.Cost})");
                }

                GD.Print($"[拓扑] 自动走图：{string.Join(" → ", path)}　共 {_flow.StepsDone} 段　" +
                         $"到达终点 {_flow.ReachedGoal}　结束光照 {Meter!.Value}（起点 100）　最终档 {LightMeter.TierId(Meter.Tier)}");
                return;
            }

            // 🔴 玩家可点：建地图视图并按当前位置刷出"相邻可选房间"
            BuildMapView();

            // 🔴 冒烟：`--click-map=N` ⇒ **连发 N 次真实 `Pressed`** 走图（红线 18/21(b)：验玩家点击路径）
            string? clickMap = System.Array.Find(args, a => a.StartsWith("--click-map=", StringComparison.Ordinal));
            if (clickMap is not null && int.TryParse(clickMap["--click-map=".Length..], out int clicks))
            {
                for (int i = 0; i < clicks && MapOptionCount > 0; i++)
                {
                    PressMapRoom(0);
                }

                GD.Print($"[拓扑UI] 点击冒烟结束：共发 {clicks} 次真实 Pressed　⇒ 已走 {_flow.StepsDone} 段　" +
                         $"当前房间 {_flow.CurrentRoomId}　到达终点 {_flow.ReachedGoal}　剩余可点 {MapOptionCount}");
            }

            // 🔴 冒烟：`--camp` ⇒ **真实点击"扎营"**（验"扎营 → 夜袭判定 → 若触发则切战斗场景"的往返）
            if (System.Array.Exists(args, a => a == "--camp"))
            {
                GD.Print("[拓扑UI] --camp ⇒ 真实点击扎营按钮");
                PressCamp();
            }

            // 🔴 冒烟：`--revisit` ⇒ **再进一次远征场景**（真场景切换）⇒ 验【返程守卫】：
            //    应打印"返程：复用同一趟"且**地图保持同一张**（而不是重开一趟 + 新地图）
            if (System.Array.Exists(args, a => a == "--revisit"))
            {
                GD.Print("[拓扑UI] --revisit ⇒ 再进一次远征场景（验返程守卫：同一趟 + 同一张地图）");
                GetTree().CallDeferred("change_scene_to_file", "res://scenes/expedition/Expedition.tscn");
                return;
            }

            return; // 拓扑模式的推进由玩家点选驱动（不再走旧线性 `ShowPathChoice`）
        }

        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--hamlet-next") && _flow is not null)
        {
            GD.Print("[ExpeditionRoot] --hamlet-next ⇒ 本趟结算并回城（冒烟路径：启动 → 跑图 → 回城）");
            _flow.ReturnToTown("completed");
            ExpeditionContext.End();
            GetTree().CallDeferred("change_scene_to_file", "res://scenes/hamlet/Hamlet.tscn");
        }

        // 🔴 M8.0 ⑥ 端到端（**一次运行跑完整回路**）：`--e2e`
        if (System.Array.Exists(OS.GetCmdlineArgs(), a => a == "--e2e") && _flow is not null)
        {
            if (ExpeditionContext.E2EStage == 0)
            {
                // 阶段 0：**打四场（模拟胜利，冒烟专用；到 black 档 ⇒ 传家宝够升一级）⇒ 结算回城**
                for (int i = 0; i < 4; i++)
                {
                    _flow.Advance(1);
                    _flow.OnBattleFinished("PlayerVictory", rounds: 5);
                }

                HeirloomStock? hs = ExpeditionContext.Heirlooms;
                GD.Print($"[E2E] 阶段0 跑图：四场胜利 ⇒ 金钱 {ExpeditionContext.Gold?.Gold ?? 0}（战斗数 乘 光照档）" +
                         $"　传家宝 {(hs is null ? "未接入" : string.Join("/", hs.Kinds.Select(k => $"{k}×{hs.Count(k)}")))}");
                _flow.ReturnToTown("completed");
                ExpeditionContext.Roster?.ApplyReturnFromRun(Log, Session!.Roster().Select(r => (r.Id, r.Morale)));
                ExpeditionContext.End();
                ExpeditionContext.E2EStage = 1;
                GetTree().CallDeferred("change_scene_to_file", "res://scenes/hamlet/Hamlet.tscn");
            }
            else
            {
                GD.Print($"[E2E] 阶段2 ✅ **再出发成功**（回到地牢层）⇒ 完整回路成立："
                         + "启动 → 跑图 → 回城 → 花钱 → 再出发");
            }
        }
    }

    /// <summary>
    /// 装配一趟新远征（与 `BattleRoot.NewGame` 同源）：**数据走 `DirectorBridge`**（不另开 res:// 读取路径），
    /// 复用其 tuning 与节点表；内核对象在此构造后注入（本类仍不参与规则）。
    /// </summary>
    public void NewExpedition()
    {
        // 🔴 流程闭环（`#307`⑤）：**从战斗返回时【不要重开一整趟】** ——
        //    此前 `NewExpedition` 无条件新建 session/flow ⇒ 从 `Battle.tscn` 切回来会
        //    **把已走段数 / 已赢场数 / 光照 / 背包全部清零**（破坏契约「一趟 = N 场」）。
        //    现在：若 `ExpeditionContext.Flow` 仍在（= 本趟未结束）⇒ **复用同一趟**，只重建 UI 引用。
        if (ExpeditionContext.Flow is not null)
        {
            _flow = ExpeditionContext.Flow;
            Session = _flow.Session;
            Meter = _flow.Meter;
            GD.Print($"[ExpeditionRoot] 返程：**复用同一趟**（已走 {_flow.StepsDone} 段 ／ 胜 {_flow.Wins} ／ " +
                     $"光照 {_flow.Meter.Value} ／ 模式 {( _flow.IsTopologyMode ? "拓扑" : "线性")}）⇒ 不重开（修复「回来进度清零」）");
            return;
        }

        DirectorBridge.DirectorHandle handle = DirectorBridge.BuildFromRes(this);
        TuningConfig tuning = handle.Tuning;

        // 🔴 M8.0 ①(c)（#286）：**出征 6 人由名册提供**（阵型模板只给槽位/敌方）——
        //    组合根在此读名册、选出快照、连 (b) 等级投影一起传给桥；BattleDirector 仍单场纯。
        RosterConfig roster = RosterConfig.Parse(
            Godot.FileAccess.GetFileAsString("res://data/roster.json"));
        FormationConfig template = FormationConfig.Parse(
            Godot.FileAccess.GetFileAsString("res://data/formation.json"));
        IReadOnlyList<HeroConfig> sortie = FormationSortie.SelectForTemplate(template, roster);
        Roster shared = ExpeditionContext.EnsureRoster(roster); // 🔴 跨趟名册（复用同一实例 ⇒ 士气不被重置）
        var openingMorale = new List<int>();
        var diseasePenalties = new List<DiseasePenalty>();
        var traitEffects = new List<TraitEffects>();
        SanitariumConfig saniCfg = SanitariumConfig.Parse(
            Godot.FileAccess.GetFileAsString(SanitariumConfig.ResPath));
        foreach (HeroConfig h in sortie)
        {
            openingMorale.Add(shared.MoraleOf(h.Id));
            diseasePenalties.Add(Sanitarium.TotalPenalty(saniCfg, shared, h.Id)); // 🔴 V14：疾病真的影响战斗
            traitEffects.Add(shared.TraitEffectsOf(h.Id)); // 🔴 V15：用**当前**特质（清除/固化后立即生效）
        }

        GD.Print($"[ExpeditionRoot] 名册出征 6 人（按模板槽位原型配人）：" +
                 string.Join("、", sortie.Select((h, i) => $"{h.Name}({h.Archetype} Lv{h.Level} 士气{openingMorale[i]})")));

        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _); // 整备默认 = 推荐配置（2/9/support_crate）
        bag.LockForRun();                // 🔴 出发后局内不可改

        var session = new ExpeditionSession(
            _ => DirectorBridge.BuildFromRes(this, sortie, roster.LevelGrowth, openingMorale, diseasePenalties, traitEffects).Core,
            tuning.Expedition.NBattles,
            firewood: bag.CountOf(ItemKind.Firewood),
            food: bag.CountOf(ItemKind.Food),
            ambushChance: tuning.Expedition.AmbushChance);

        var meter = new LightMeter(tuning.Light!);
        Initialize(session, meter, bag, tuning, handle.Nodes);

        // 🔴 M8.0 ②：**经济必须在地牢层就被确保存在**（否则本趟胜利无处记账 ⇒ 金钱永远是 0）
        EconomyConfig econCfg = EconomyConfig.Parse(Godot.FileAccess.GetFileAsString(EconomyConfig.ResPath));
        Economy economy = ExpeditionContext.EnsureEconomy(econCfg);
        HeirloomConfig heirloomCfg = HeirloomConfig.Parse(Godot.FileAccess.GetFileAsString(HeirloomConfig.ResPath));
        HeirloomStock heirlooms = ExpeditionContext.EnsureHeirlooms(heirloomCfg);
        _flow = new ExpeditionFlow(session, meter, bag, new Scouting(tuning.Scouting!, tuning.Light!),
            handle.Nodes, tuning, Log, new Darkest.Core.Rng.RngProvider(20260909), economy, heirlooms, heirloomCfg);
        ExpeditionContext.Bind(_flow, Log);
        GD.Print($"[ExpeditionRoot] 远征就绪：{tuning.Expedition.NBattles} 场；光照 {meter.Value}；" +
                 $"背包 {bag.Count}/{bag.SlotCap}（支援箱 {bag.CarriesSupportCrate}）");
    }

    /// <summary>推进到下一步（战斗 ⇒ 切到战斗场景；事件 ⇒ 显示二选一；走完 ⇒ 回城结算）。</summary>
    public void AdvanceNextStep()
    {
        if (_flow is null)
        {
            return;
        }

        FlowStep step = _flow.Advance(optionIndex: _nextOption);
        _nextOption = _nextOption == 0 ? 1 : 0; // 最小版：交替选择（真实玩家选路由 UI 决定）

        switch (step.Kind)
        {
            case FlowStepKind.Battle:
                ExpeditionContext.Bind(_flow, Log);
                GetTree().ChangeSceneToFile("res://scenes/battle/Battle.tscn");
                return;
            case FlowStepKind.Event:
                SetPendingEvent(step.NodeId);
                RefreshPanel();
                return;
            default:
                _flow.ReturnToTown("completed");
                // 🔴 #287（= #245 的落地）：**归来写回**名册士气（回城不解算不重置）⇒ 士气跨趟累积
                ExpeditionContext.Roster?.ApplyReturnFromRun(
                    Log, Session!.Roster().Select(r => (r.Id, r.Morale)));
                ExpeditionContext.End();
                RefreshPanel();
                // 🔴 M8.0 ③：本趟结束 ⇒ **回城**（金钱与名册士气都留在跨趟持有者里，不随 End 清空）
                GetTree().CallDeferred("change_scene_to_file", "res://scenes/hamlet/Hamlet.tscn");
                return;
        }
    }

    /// <summary>注入内核对象（数据由调用方按与 `BattleRoot` 同源的方式加载）。</summary>
    public void Initialize(ExpeditionSession session, LightMeter meter, Inventory bag,
        TuningConfig tuning, ExpeditionNodesConfig nodes)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Meter = meter ?? throw new ArgumentNullException(nameof(meter));
        Bag = bag ?? throw new ArgumentNullException(nameof(bag));
        Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));

        Meter.EmitStart(Log); // 光照起点（reason = start；㉑ 曲线需要）
        RefreshPanel();
    }

    /// <summary>刷新面板：**行文本只来自内核投影**（UI 不自己算数）。</summary>
    public void RefreshPanel()
    {
        if (Session is null || Tuning is null)
        {
            return;
        }

        LastLines = ExpeditionProjector.RenderList(
            ExpeditionProjector.Project(Log, Bag!.CountOf(ItemKind.Firewood), Bag.CountOf(ItemKind.Food),
                Session, Tuning.Camp!, Tuning.Expedition.NBattles, Tuning.Expedition.DifficultyTiers),
            Session, Tuning.Expedition.NBattles, ambushTriggered: false, Tuning.Camp!);

        _panel.Refresh(LastLines);
        _campButton.Disabled = !Session.CanCamp; // 灰显依据来自内核（不是 UI 自算）

        // 必显 11 光照条（数值 + 档位 + 该档给敌人什么）与 必显 13 侦察标记（两态可区分）
        _lightBar?.Refresh(Meter!);
        _scoutMark?.Refresh(_flow?.LastScout);
    }

    /// <summary>把当前步的两个候选交给选路界面（**玩家点选后才推进**）。</summary>
    public void ShowPathChoice()
    {
        if (_flow is null || _pathPanel is null)
        {
            return;
        }

        IReadOnlyList<PathOption> options = _flow.PreviewOptions();
        if (options.Count == 0)
        {
            _flow.ReturnToTown("completed");
            ExpeditionContext.End();
            RefreshPanel();
            return;
        }

        _pathPanel.Refresh(new PathStep(_flow.StepsDone, options), AdvanceWith, Nodes);
        RefreshPanel();
    }

    /// <summary>按玩家所选下标推进（选路界面的回调）。</summary>
    public void AdvanceWith(int optionIndex)
    {
        if (_flow is null)
        {
            return;
        }

        _pathPanel?.HidePanel();
        FlowStep step = _flow.Advance(optionIndex);
        switch (step.Kind)
        {
            case FlowStepKind.Battle:
                ExpeditionContext.Bind(_flow, Log);
                GetTree().ChangeSceneToFile("res://scenes/battle/Battle.tscn");
                return;
            case FlowStepKind.Event:
                SetPendingEvent(step.NodeId);
                RefreshPanel();
                return;
            default:
                _flow.ReturnToTown("completed");
                // 🔴 #287（= #245 的落地）：**归来写回**名册士气（回城不解算不重置）⇒ 士气跨趟累积
                ExpeditionContext.Roster?.ApplyReturnFromRun(
                    Log, Session!.Roster().Select(r => (r.Id, r.Morale)));
                ExpeditionContext.End();
                RefreshPanel();
                // 🔴 M8.0 ③：本趟结束 ⇒ **回城**（金钱与名册士气都留在跨趟持有者里，不随 End 清空）
                GetTree().CallDeferred("change_scene_to_file", "res://scenes/hamlet/Hamlet.tscn");
                return;
        }
    }

    /// <summary>事件二选一（**无跳过**；越界由内核拒绝并抛错）。</summary>
    public void ChooseEventOption(int index)
    {
        if (Session is null || Nodes is null || _pendingEventNodeId is null)
        {
            return;
        }

        Session.ResolveEventNode(Log, Nodes.Get(_pendingEventNodeId), index);
        _pendingEventNodeId = null;
        RefreshPanel();
    }

    private string? _pendingEventNodeId;

    /// <summary>设置当前待决策的事件节点（由流程层设置；本类不选择节点）。</summary>
    public void SetPendingEvent(string nodeId)
    {
        _pendingEventNodeId = nodeId;
        ExpeditionNodeConfig node = Nodes!.Get(nodeId);
        _choiceA.Text = node.Options[0].Label;
        _choiceB.Text = node.Options[1].Label;
        RefreshPanel();
    }

    /// <summary>扎营入口（转发给内核；柴火不足时内核拒绝且不扣）。</summary>
    public void Camp()
    {
        if (Session is null || Tuning is null || Meter is null)
        {
            return;
        }

        int campIndex = Session.BattlesPlayed + 1;
        if (!Session.StartCamp(Log, campIndex, Tuning.Camp!.RespiteBase))
        {
            RefreshPanel();
            return;
        }

        Meter.OnCamp(Log); // D0.2：扎营回满 100
        RefreshPanel();
    }

    /// <summary>回城结算（转发；士气完全不恢复由内核保证 #245）。</summary>
    public void ReturnToTown(string outcome)
    {
        Session?.ReturnToTown(Log, outcome);
        RefreshPanel();
    }

    private Button MakeButton(string text, Vector2 position, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = new Vector2(240, 40),
        };
        button.Pressed += onPressed;
        AddChild(button);
        _buttons.Add(button);
        return button;
    }

    // ------------------------------------------------------------------
    // 🔴 M7.6 片 (iii)：**地图视图**（红线 18：玩家必须「碰得到」选路）
    // ------------------------------------------------------------------

    private Label? _mapStatus;
    private Label? _mapOptionsTitle;
    private Button? _campInTopology;
    private readonly List<Button> _mapButtons = new();

    /// <summary>当前会话（供地图视图显示夜袭累计）。</summary>
    private ExpeditionSession? TopologySession => _flow?.Session;

    /// <summary>
    /// **建地图视图**：显示当前位置 ／ 已走段数 ／ 光照与档位 ／ 完成状态；并为**每个相邻未探索房间**
    /// 建一个**真实按钮**（点击 ⇒ `flow.StepTo(roomId)` ⇒ 刷新）⇒ 这就是"分叉点选路"的**玩家入口** ✓
    /// </summary>
    public void BuildMapView()
    {
        if (_flow is null || Meter is null)
        {
            return;
        }

        _mapStatus = new Label
        {
            Name = "MapStatus",
            Position = new Vector2(24, 470),
            Size = new Vector2(1250, 40),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_mapStatus);

        _mapOptionsTitle = new Label
        {
            Name = "MapOptionsTitle",
            Position = new Vector2(24, 512),
            Size = new Vector2(1250, 24),
        };
        AddChild(_mapOptionsTitle);

        _campInTopology = new Button
        {
            Name = "CampInTopology",
            Text = "扎营（回满光照；耗 1 柴火；后有夜袭判定）",
            Position = new Vector2(24, 566),
            Size = new Vector2(420, 36),
        };
        _campInTopology.Pressed += () =>
        {
            bool ok = _flow!.Camp();
            GD.Print($"[拓扑UI] 扎营：{(ok ? "成功（光照回满）" : "拒绝（柴火不足）")}　夜袭触发={_flow.LastCampAmbushed}");

            // 🔴 `#307`③：**夜袭真的插一场战斗**（契约：计入 6 场皆胜）——
            //    走【本项目既有的战斗往返】：置"夜袭"标记 ⇒ 切到 `Battle.tscn` 由玩家**真打** ⇒
            //    `BattleRoot` 结算时 `OnBattleFinished(..., isAmbush: true)` 计入胜场 ⇒ 【继续（回远征）】回到地图 ✓
            //    ⚠️ 不在 UI 里另建一套战斗驱动（本项目的真实战斗入口是 `BattleRoot`）。
            if (ok && _flow.LastCampAmbushed)
            {
                GD.Print("[拓扑UI] 夜袭已触发 ⇒ **插入一场额外战斗**（切到 Battle.tscn，真打；结算计入胜场）");
                ExpeditionContext.PendingAmbush = true;
                ExpeditionContext.Bind(_flow, Log);
                GetTree().ChangeSceneToFile("res://scenes/battle/Battle.tscn");
                return;
            }

            RefreshMapView();
        };
        AddChild(_campInTopology);

        RefreshMapView();
        GD.Print($"[拓扑UI] 地图视图就绪：当前房间 {_flow.CurrentRoomId}　可点房间 {_mapButtons.Count} 个（红线 18：玩家可点）");
    }

    /// <summary>刷新地图视图（当前状态 + 相邻可选房间按钮）。</summary>
    public void RefreshMapView()
    {
        if (_flow is null || Meter is null || _mapStatus is null)
        {
            return;
        }

        foreach (Button b in _mapButtons)
        {
            b.QueueFree();
        }

        _mapButtons.Clear();

        IReadOnlyList<MapRoom> options = _flow.AdjacentUnexplored();
        _mapStatus.Text = $"【地图】当前房间 {_flow.CurrentRoomId} ／ 已走 {_flow.StepsDone} 段 ／ " +
                          $"光照 {Meter.Value}（{LightMeter.TierId(Meter.Tier)}） ／ 到达终点 {_flow.ReachedGoal} ／ " +
                          $"完成 {_flow.Completed} ／ 夜袭累计 {TopologySession?.AmbushCount ?? 0}";
        if (_mapOptionsTitle is not null)
        {
            _mapOptionsTitle.Text = options.Count == 0
                ? "无可走房间（终点已到，或相邻房间都已探索过）"
                : "可选房间（点一下就走；新区域 −30 ／ 重走 −10）：";
        }

        for (int i = 0; i < options.Count; i++)
        {
            MapRoom room = options[i];
            var b = new Button
            {
                Name = $"MapRoom_{room.Id}",
                Text = $"房间 {room.Id}（{room.Type}{(room.IsBranch ? "·支路" : string.Empty)}）",
                Position = new Vector2(24 + (i * 250), 536),
                Size = new Vector2(240, 26),
            };
            int target = room.Id;
            b.Pressed += () =>
            {
                MoveOutcome o = _flow.StepTo(target);
                GD.Print($"[拓扑UI] 走 → 房间 {target}：{(o.Moved ? "成功" : "被拒")}　代价 {o.Cost}　重走={o.Revisited}" +
                         $"　段数 {_flow.StepsDone}　光照 {Meter.Value}");
                RefreshMapView();
            };
            AddChild(b);
            _mapButtons.Add(b);
        }
    }

    /// <summary>🔴 供冒烟/测试：**点一下第 i 个可选房间**（发真实 `Pressed` ⇒ 走玩家路径）。</summary>
    public bool PressMapRoom(int index)
    {
        if (index < 0 || index >= _mapButtons.Count)
        {
            GD.Print($"[拓扑UI] PressMapRoom({index})：没有这个可选房间（当前 {_mapButtons.Count} 个）");
            return false;
        }

        GD.Print($"[拓扑UI] PressMapRoom({index})：发出真实 Pressed（按钮「{_mapButtons[index].Text}」）");
        _mapButtons[index].EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>供冒烟：当前可选房间数（0 ⇒ 选路已走完）。</summary>
    public int MapOptionCount => _mapButtons.Count;

    /// <summary>🔴 供冒烟：**真实点击"扎营"**（发真实 `Pressed` ⇒ 走玩家路径）。</summary>
    public void PressCamp()
    {
        if (_campInTopology is null)
        {
            GD.Print("[拓扑UI] PressCamp：没有扎营按钮（非拓扑模式）");
            return;
        }

        GD.Print("[拓扑UI] PressCamp：发出真实 Pressed（扎营）");
        _campInTopology.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
