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

        // 🔴 M8.0 ③ 端到端冒烟路径（**跑图 → 回城**）：`--hamlet-next` ⇒ 本趟即刻结算并回城
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
        SanitariumConfig saniCfg = SanitariumConfig.Parse(
            Godot.FileAccess.GetFileAsString(SanitariumConfig.ResPath));
        foreach (HeroConfig h in sortie)
        {
            openingMorale.Add(shared.MoraleOf(h.Id));
            diseasePenalties.Add(Sanitarium.TotalPenalty(saniCfg, shared, h.Id)); // 🔴 V14：疾病真的影响战斗
        }

        GD.Print($"[ExpeditionRoot] 名册出征 6 人（按模板槽位原型配人）：" +
                 string.Join("、", sortie.Select((h, i) => $"{h.Name}({h.Archetype} Lv{h.Level} 士气{openingMorale[i]})")));

        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _); // 整备默认 = 推荐配置（2/9/support_crate）
        bag.LockForRun();                // 🔴 出发后局内不可改

        var session = new ExpeditionSession(
            _ => DirectorBridge.BuildFromRes(this, sortie, roster.LevelGrowth, openingMorale, diseasePenalties).Core,
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
}
