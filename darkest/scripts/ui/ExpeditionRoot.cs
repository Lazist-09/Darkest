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
        bool smokeDriven = Darkest.Gameplay.Scene.SmokeScript.Enabled;
        if (smokeDriven || System.Array.Exists(args, a => a == "--topology" || a == "--topology-auto"))
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
            //    ⚠️ 若已切到战斗场景 ⇒ **必须终止本次 `_Ready`**（否则后面的钩子会在"已离开地图"的状态下继续跑）
            if (System.Array.Exists(args, a => a == "--camp"))
            {
                GD.Print("[拓扑UI] --camp ⇒ 真实点击扎营按钮");
                PressCampAndMaybeRouteToBattle();
            }

            // 🔴 冒烟：`--camp-skill=N` ⇒ **真实点击第 N 个扎营技能**（验红线 18：玩家点得到）
            string? campSkill = System.Array.Find(args, a => a.StartsWith("--camp-skill=", StringComparison.Ordinal));
            if (campSkill is not null && int.TryParse(campSkill["--camp-skill=".Length..], out int skillIdx))
            {
                PressCampSkill(skillIdx);
            }

            // 🔴 冒烟：`--finish-camp` ⇒ **真实点击【结束扎营】**（阶段三：夜袭判定）
            if (System.Array.Exists(args, a => a == "--finish-camp"))
            {
                PressFinishCamp();
                if (_flow?.LastCampAmbushed == true)
                {
                    return; // 已切到夜袭战斗
                }
            }

            // 🔴 冒烟：`--revisit` ⇒ **再进一次远征场景**（真场景切换）⇒ 验【返程守卫】：
            //    应打印"返程：复用同一趟"且**地图保持同一张**（而不是重开一趟 + 新地图）
            if (System.Array.Exists(args, a => a == "--revisit"))
            {
                GD.Print("[拓扑UI] --revisit ⇒ 再进一次远征场景（验返程守卫：同一趟 + 同一张地图）");
                GetTree().CallDeferred("change_scene_to_file", "res://scenes/expedition/Expedition.tscn");
                return;
            }

            // 🔴 冒烟：`--return-town` ⇒ **真实点击"回城"**（验"地图 → Hamlet"这一段闭环）
            if (System.Array.Exists(args, a => a == "--return-town"))
            {
                GD.Print("[拓扑UI] --return-town ⇒ 真实点击回城按钮");
                PressReturnToTown();
                return;
            }

            // 🔴 冒烟：`--run-full` ⇒ **跑完整趟**（玩家路径：逐间点击走动 → 遇战斗房真打 → 完成后回城）
            //    每次 `_Ready`（含"从战斗返回"）推进一格 ⇒ 自然串成完整一趟 ✓
            if (System.Array.Exists(args, a => a == "--run-full"))
            {
                RunFullSmokeStep();
                return;
            }

            // 🔴 跨场景步进冒烟：消费本场景的一步（`ui_three_screens.md` §3 / `#310`⑦）
            Darkest.Gameplay.Scene.SmokeScript.Step(this);

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

        // 🔴 跨场 buff 的【hero → 战斗单位】映射：营地用英雄 id、战斗用原型 id（两套体系）⇒
        //    按【阵型槽位】挂载，注入时用 `Player.UnitRuntimeAt(slot)` 换成本场单位 ✓
        var heroSlots = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<RosterEntryConfig> playerSlots = template.InitialRoster.Player;
        for (int i = 0; i < sortie.Count && i < playerSlots.Count; i++)
        {
            heroSlots[sortie[i].Id] = playerSlots[i].Slot;
        }

        session.BindSortie(heroSlots);
        _heroSlots.Clear();
        foreach ((string hero, int slot) in heroSlots)
        {
            _heroSlots[hero] = slot;
        }

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

        // 🔴 扎营技能：**加载即校验**（红线 21 防线，照 `BuffDefsConfig.ConsumedEffectNames`）——
        //    每个 effect 名必须登记为【已接线】或【阶段二没落点】之一，否则**启动即报错**。
        //    实测审计：12 个技能里只有 `ambush_immunity_once`（守夜 ／ 站岗）真的会生效；
        //    其余登记为阶段二（需【跨战斗待生效效果层】）⇒ **在它落地前，这些技能不得上 UI**。
        CampSkillsConfig campSkills = CampSkillsConfig.Parse(
            Godot.FileAccess.GetFileAsString(CampSkillsConfig.ResPath));
        _campSkills = campSkills;   // 🔴 供扎营技能面板（只列已接线的 effect）
        _rosterCfg = RosterConfig.Parse(
            Godot.FileAccess.GetFileAsString(RosterConfig.ResPath));

        // 🔴 Curio 数据（`#313`）：拓扑模式下**事件房 = Curio 房**（空手 ／ 用道具 ／ 走开）
        _curiosCfg = Darkest.Data.CuriosConfig.Parse(
            Godot.FileAccess.GetFileAsString(Darkest.Data.CuriosConfig.ResPath));
        GD.Print($"[拓扑UI] Curio：{_curiosCfg.RealCurios.Count} 个（三按钮：空手 ／ 用道具 ／ 走开；" +
                 "阶段二道具选项**不列出**，红线 21）");
        GD.Print($"[ExpeditionRoot] 扎营技能：已接线 {campSkills.ConsumedCount} ／ 阶段二（未落点）{campSkills.DeferredCount}" +
                 $"（共 {campSkills.Skills.Count}）—— 阶段二项在【跨战斗待生效效果层】落地前不上 UI（红线 21）");

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
    private Button? _returnTown;
    private bool _routedToBattle; // 冒烟：本轮是否已切到战斗场景（供 `--run-full` 判断"该停了"）
    private readonly List<Node> _mapGraph = new(); // 拓扑图元素（线 + 房间方块；每次刷新重建）
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
            // 🔴 拆开：**阶段一→二**（开始扎营）⇒ 弹技能面板 ⇒ 玩家点技能 ⇒ 【结束扎营】⇒ 阶段三（夜袭判定）
            //    （此前 `Camp()` 一调用到底 ⇒ 6 个已接线的扎营技能【玩家碰不到】，红线 18/21）
            bool ok = _flow!.BeginCamp();
            GD.Print($"[拓扑UI] 扎营（开始）：{(ok ? "成功（光照回满，进入 Respite 分配）" : "拒绝（柴火不足）")}");
            if (ok)
            {
                BuildCampSkillPanel();
                return;
            }

            RefreshMapView();
        };

        // 🔴 `#307`⑤ 流程闭环最后一段：**【完成本趟（回城）】** —— 把玩家从地图带回 Hamlet，
        //    走与线性模式**完全相同**的收尾路径（回灌结果 + 名册士气写回 + `End()` + 切场景）。
        _returnTown = new Button
        {
            Name = "ReturnToTown",
            Text = "回城（完成本趟）",
            Position = new Vector2(470, 566),
            Size = new Vector2(280, 36),
        };
        _returnTown.Pressed += () =>
        {
            string outcome = _flow!.Completed ? "completed" : "retreat";
            GD.Print($"[拓扑UI] 回城：完成口径={_flow.Completed}（到达终点 {_flow.ReachedGoal} ／ 胜 {_flow.Wins} ≥ 3）" +
                     $"　outcome={outcome}");
            FinishRunToTown(outcome);
        };
        AddChild(_returnTown);
        AddChild(_campInTopology);

        RefreshMapView();
        GD.Print($"[拓扑UI] 地图视图就绪：当前房间 {_flow.CurrentRoomId}　可点房间 {_mapButtons.Count} 个（红线 18：玩家可点）");
    }

    /// <summary>
    /// 🔴 **把地图【画出来】**（`m7_roadmap §4.3①`：房间 + 走廊）——
    /// 此前只有"文字按钮列表"（可用但**玩家看不见拓扑**）⇒ 这里补**图形化**：
    /// · 每个房间一个方块（**主干在下排 ／ 支路在上排**，按 `Depth` 排开）
    /// · 每条走廊一条线（`Line2D`）
    /// · **当前房间**用「▶」标出；**可走房间**高亮可点；已探索房间变暗
    /// ⚠️ 纯 UI（零数值、零内核改动）；headless 无法验"好不好看"⇒ 需要人眼或截图。
    /// </summary>
    private void DrawMapGraph()
    {
        if (_flow?.Map is null)
        {
            return;
        }

        ExpeditionMap map = _flow.Map;

        foreach (Node n in _mapGraph)
        {
            n.QueueFree();
        }

        _mapGraph.Clear();

        // 走廊（先画线，房间方块盖在上面）
        foreach (MapEdge e in map.Edges)
        {
            MapRoom a = map.Rooms.First(r => r.Id == e.From);
            MapRoom b = map.Rooms.First(r => r.Id == e.To);
            var line = new Line2D
            {
                Name = $"Edge_{e.From}_{e.To}",
                Width = 2f,
                DefaultColor = new Color(0.55f, 0.55f, 0.6f),
                Points = new[] { RoomScreenPos(a), RoomScreenPos(b) },
            };
            AddChild(line);
            _mapGraph.Add(line);
        }

        // 房间方块：已探索 ⇒ 变暗；当前 ⇒ 加「▶」；可走 ⇒ 亮
        var reachable = _flow.AdjacentUnexplored().Select(r => r.Id).ToHashSet();
        foreach (MapRoom room in map.Rooms)
        {
            bool current = room.Id == _flow.CurrentRoomId;
            bool canGo = reachable.Contains(room.Id);
            bool explored = !canGo && !current && _flow.HasVisited(room.Id);

            var box = new Button
            {
                Name = $"RoomBox_{room.Id}",
                Text = $"{(current ? "▶" : string.Empty)}{room.Id}\n{room.Type}",
                Position = RoomScreenPos(room) - new Vector2(40, 16),
                Size = new Vector2(80, 32),
                Disabled = !canGo,
                Modulate = explored ? new Color(0.55f, 0.55f, 0.55f) : Colors.White,
            };
            AddChild(box);
            _mapGraph.Add(box);
        }
    }

    /// <summary>房间在屏幕上的位置（主干下排 ／ 支路上排，按 `Depth` 排开）。</summary>
    private static Vector2 RoomScreenPos(MapRoom room)
        => new(60 + (room.Depth * 80), room.IsBranch ? 600 : 660);

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
            string roomType = room.Type;
            b.Pressed += () =>
            {
                MoveOutcome o = _flow.StepTo(target);
                GD.Print($"[拓扑UI] 走 → 房间 {target}：{(o.Moved ? "成功" : "被拒")}　代价 {o.Cost}　重走={o.Revisited}" +
                         $"　段数 {_flow.StepsDone}　光照 {Meter.Value}");

                // 🔴 `#307`⑤：**走进房间要触发【该房间的内容】** —— 此前地图视图只移动、不进入内容
                //    ⇒ 一趟里永远不会真的打战斗 ⇒ 完成口径（打赢 ≥3）永远达不成。
                //    战斗房：走【与夜袭完全相同】的往返（切 `Battle.tscn` 真打；`isAmbush=false` 因为它是节点步骤）
                if (o.Moved && roomType == "battle" && !_flow.ReachedGoal)
                {
                    GD.Print("[拓扑UI] 进入【战斗房】⇒ 切到 Battle.tscn 真打（结算按节点步骤计入）");
                    _routedToBattle = true;
                    ExpeditionContext.PendingAmbush = false;
                    ExpeditionContext.Bind(_flow, Log);
                    GetTree().ChangeSceneToFile("res://scenes/battle/Battle.tscn");
                    return;
                }

                // 🔴 `#313`（Curio）：**事件房 ⇒ Curio（可交互物体）面板**（空手 ／ 用道具 ／ 走开）
                //    · 线性模式仍走既有事件面板（两条路径分开，互不影响）
                //    · 映射：`roomId % curios.Count`（确定性，不掷骰；与事件映射同法）
                if (o.Moved && roomType == "event" && _curiosCfg is not null)
                {
                    GD.Print($"[拓扑UI] 进入【Curio 房】⇒ 房间 {target} 映射到物件（roomId % {_curiosCfg.RealCurios.Count}）");
                    SetPendingCurio(target);

                    // 🔴 冒烟（`#310`⑦ 的"场景内步骤"补丁）：`--curio-bare` / `--curio-leave`
                    //    ⇒ 面板建好后**同一帧内真实点击**（步进器只在"进场景"时消费一步，覆盖不到场景内下一步）
                    string[] curioArgs = OS.GetCmdlineArgs();
                    if (System.Array.Exists(curioArgs, a => a == "--curio-bare"))
                    {
                        PressCurioBare();
                    }
                    else if (System.Array.Exists(curioArgs, a => a == "--curio-leave"))
                    {
                        PressCurioLeave();
                    }

                    return;
                }

                // 兜底：没有 Curio 数据时退回既有事件面板（如实报，不静默）
                if (o.Moved && roomType == "event")
                {
                    string? nodeId = _flow.EventNodeIdForRoom(target);
                    if (nodeId is not null)
                    {
                        GD.Print($"[拓扑UI] 无 Curio 数据 ⇒ 退回事件节点 {nodeId}（选项 A/B）");
                        SetPendingEvent(nodeId);
                        RefreshPanel();
                        return;
                    }

                    GD.Print("[拓扑UI] 进入【事件房】但映射不到事件节点（如实报：房间↔节点映射缺失）");
                }

                RefreshMapView();
            };
            AddChild(b);
            _mapButtons.Add(b);
        }

        DrawMapGraph(); // 🔴 图形化：房间 + 走廊（玩家能看见拓扑）
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

    // ------------------------------------------------------------------
    // 🔴 扎营技能面板（红线 18/21：**已接线的技能必须让玩家点得到**；阶段二的不出现）
    // ------------------------------------------------------------------

    private Label? _campSkillStatus;
    private Button? _finishCamp;
    private readonly List<Button> _campSkillButtons = new();
    private CampSkillsConfig? _campSkills;                       // 扎营技能数据（装配时读入）
    private RosterConfig? _rosterCfg;                            // 名册配置（角色专属判定用）
    private readonly Dictionary<string, int> _heroSlots = new(StringComparer.Ordinal); // 英雄 id → 阵型槽位

    /// <summary>可选扎营技能数（供冒烟断言）。</summary>
    public int CampSkillOptionCount => _campSkillButtons.Count;

    // ------------------------------------------------------------------
    // 🔴 Curio（可交互物体）面板 —— `doc/modules/curio.md` / `#313`
    //    三按钮：**空手 ／ 用道具（只列已实现）／ 走开**；结果有**描述文本**（V6）；
    //    阶段二分支**显式标注**且**不施加效果**（红线 21）。
    // ------------------------------------------------------------------

    private Darkest.Data.CuriosConfig? _curiosCfg;
    private Darkest.Data.CurioConfig? _pendingCurio;
    private Label? _curioText;
    private readonly List<Button> _curioButtons = new();

    /// <summary>当前 Curio 面板的可选项数（供冒烟断言）。</summary>
    public int CurioOptionCount => _curioButtons.Count;

    /// <summary>当前待交互的 Curio id（供冒烟断言）。</summary>
    public string? PendingCurioId => _pendingCurio?.Id;

    /// <summary>🔴 按房间确定性映射到 Curio（`roomId % N`，**不掷骰** —— 与事件映射同法）。</summary>
    public void SetPendingCurio(int roomId)
    {
        if (_curiosCfg is null)
        {
            return;
        }

        IReadOnlyList<Darkest.Data.CurioConfig> list = _curiosCfg.RealCurios;
        if (list.Count == 0)
        {
            return;
        }

        _pendingCurio = list[roomId % list.Count];
        BuildCurioPanel();
    }

    /// <summary>建/刷新 Curio 面板（显示物件 + 三按钮）。</summary>
    public void BuildCurioPanel()
    {
        if (_pendingCurio is null || _flow is null)
        {
            return;
        }

        if (_curioText is null)
        {
            _curioText = new Label
            {
                Name = "CurioText",
                Position = new Vector2(24, 596),
                Size = new Vector2(1200, 60),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            AddChild(_curioText);
        }

        foreach (Button b in _curioButtons)
        {
            b.QueueFree();
        }

        _curioButtons.Clear();

        _curioText.Text = $"【{_pendingCurio.Name}】（{_pendingCurio.CurioType}）　" +
                          "空手有风险；用对道具可得**确定**的好结果，用错道具也是**确定**的坏结果。";

        AddCurioButton("空手", () => ResolveCurioRoute(null));

        // 🔴 V7 / 红线 21：**只列【该 Curio 定义了】且【kind 已实现】的道具**（阶段二的不列出）
        Darkest.Data.CurioItemResultConfig[] implemented = (_pendingCurio.ItemResults ?? Array.Empty<Darkest.Data.CurioItemResultConfig>())
            .Where(r => !Darkest.Data.CuriosConfig.DeferredKinds.Contains(r.Kind))
            .ToArray();
        foreach (Darkest.Data.CurioItemResultConfig r in implemented)
        {
            string item = r.Item;
            AddCurioButton($"用道具：{item}", () => ResolveCurioRoute(item));
        }

        AddCurioButton("走开（不碰）", () =>
        {
            _flow.LeaveCurio(_pendingCurio!);
            ReportCurioOutcome();
        });

        int deferred = (_pendingCurio.ItemResults?.Count ?? 0) - implemented.Length;
        GD.Print($"[Curio] {_pendingCurio.Id}（{_pendingCurio.Name}）：可选项 {_curioButtons.Count} 个" +
                 (deferred > 0 ? $"；另有 {deferred} 个【阶段二·未接线】道具选项未列出（红线 21）" : string.Empty));
    }

    private void AddCurioButton(string text, Action onPressed)
    {
        var b = new Button
        {
            Name = $"Curio_{_curioButtons.Count}",
            Text = text,
            Position = new Vector2(24 + (_curioButtons.Count * 200), 560),
            Size = new Vector2(190, 28),
        };
        b.Pressed += onPressed;
        AddChild(b);
        _curioButtons.Add(b);
    }

    private void ResolveCurioRoute(string? itemUsed)
    {
        Darkest.Gameplay.Sim.Run.CurioOutcome? outcome = _flow!.ResolveCurio(_pendingCurio!, itemUsed);
        if (outcome is null)
        {
            GD.Print($"[Curio] 道具 {itemUsed} 对该物件没有定义 ⇒ 拒绝（V7：UI 本不该列它）");
            return;
        }

        ReportCurioOutcome();
    }

    private void ReportCurioOutcome()
    {
        bool deferred = _flow!.LastCurioDeferred;
        string text = _flow.LastCurioText ?? "（无描述）";
        _curioText!.Text = (deferred ? "🔴【阶段二·未接线 ⇒ 不生效】" : "⇒ ") + text;
        GD.Print($"[Curio] 结果：{(deferred ? "（未接线）" : string.Empty)}{text}" +
                 $"　光照 {Meter?.Value}");

        // 交互完 ⇒ 收起按钮（房间已处理），刷新地图让玩家继续走
        foreach (Button b in _curioButtons)
        {
            b.QueueFree();
        }

        _curioButtons.Clear();
        _pendingCurio = null;
        RefreshMapView();
    }

    /// <summary>🔴 供冒烟：**真实点击【空手】**。</summary>
    public bool PressCurioBare() => PressCurioButton(0);

    /// <summary>🔴 供冒烟：**真实点击第 i 个选项**（0 = 空手，其后为"用道具"，最后为"走开"）。</summary>
    public bool PressCurioButton(int index)
    {
        if (index < 0 || index >= _curioButtons.Count)
        {
            GD.Print($"[Curio] PressCurioButton({index})：没有这个选项（当前 {_curioButtons.Count} 个）");
            return false;
        }

        GD.Print($"[Curio] PressCurioButton({index})：发出真实 Pressed（「{_curioButtons[index].Text}」）");
        _curioButtons[index].EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>🔴 供冒烟：**真实点击最后一个选项（走开）**。</summary>
    public bool PressCurioLeave() => PressCurioButton(_curioButtons.Count - 1);

    /// <summary>
    /// **建扎营技能面板**：只列【已接线】的 effect（`CampSkillsConfig.ConsumedEffectNames`）——
    /// 🔴 阶段二的技能**不出现**（红线 21：未接线的不得让玩家点）。
    /// 角色专属：`owner_unit` 与出征名册里某英雄的原型一致才列（契约：战士的技能不能由军医放）。
    /// </summary>
    public void BuildCampSkillPanel()
    {
        if (_flow is null || Session is null || _campSkills is null)
        {
            return;
        }

        if (_campSkillStatus is null)
        {
            _campSkillStatus = new Label
            {
                Name = "CampSkillStatus",
                Position = new Vector2(24, 606),
                Size = new Vector2(1250, 26),
            };
            AddChild(_campSkillStatus);

            _finishCamp = new Button
            {
                Name = "FinishCamp",
                Text = "结束扎营（阶段三：夜袭判定）",
                Position = new Vector2(24, 636),
                Size = new Vector2(360, 34),
            };
            _finishCamp.Pressed += () =>
            {
                _flow.FinishCamp();
                GD.Print($"[拓扑UI] 结束扎营：夜袭触发={_flow.LastCampAmbushed}");
                if (_flow.LastCampAmbushed)
                {
                    GD.Print("[拓扑UI] 夜袭已触发 ⇒ 插入一场额外战斗（切 Battle.tscn，真打）");
                    ExpeditionContext.PendingAmbush = true;
                    ExpeditionContext.Bind(_flow, Log);
                    GetTree().ChangeSceneToFile("res://scenes/battle/Battle.tscn");
                    return;
                }

                ClearCampSkillPanel();
                RefreshMapView();
            };
            AddChild(_finishCamp);
        }

        foreach (Button b in _campSkillButtons)
        {
            b.QueueFree();
        }

        _campSkillButtons.Clear();

        // 出征名册（按槽位）⇒ 原型 → 英雄（角色专属判定用）
        var heroByArchetype = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string id, int slot) in _heroSlots)
        {
            // 槽位对应的英雄原型：从名册配置读（与出征同源）
            string? archetype = _rosterCfg?.Heroes.FirstOrDefault(h => h.Id == id)?.Archetype;
            if (archetype is not null && slot > 0 && !heroByArchetype.ContainsKey(archetype))
            {
                heroByArchetype[archetype] = id;
            }
        }

        var usable = _campSkills.Skills
            .Where(s => CampSkillsConfig.ConsumedEffectNames.Contains(s.Effect)) // 🔴 只列已接线
            .Where(s => heroByArchetype.ContainsKey(s.OwnerUnit))                // 角色专属：该原型在队里
            .ToArray();

        _campSkillStatus.Text = $"【扎营】Respite {Session.RespiteLeft} 点　可用技能 {usable.Length} 个" +
                                $"（只列已接线；阶段二的不出现）";
        GD.Print($"[拓扑UI] 扎营技能面板：可用 {usable.Length} 个（英雄槽位映射 {_heroSlots.Count} 个；" +
                 $"名册 {_rosterCfg?.Heroes.Count ?? 0} 人；技能数据 {_campSkills.Skills.Count} 条）");

        for (int i = 0; i < usable.Length; i++)
        {
            CampSkillConfig skill = usable[i];
            bool afford = Session.RespiteLeft >= skill.Cost;
            var b = new Button
            {
                Name = $"CampSkill_{skill.Id}",
                Text = $"{skill.Name}（{skill.Cost} 点）",
                Position = new Vector2(24 + (i * 200), 570),
                Size = new Vector2(190, 30),
                Disabled = !afford,
            };
            string effect = skill.Effect;
            string target = heroByArchetype[skill.OwnerUnit];
            b.Pressed += () =>
            {
                bool used = Session.UseCampSkill(Log, skill.Id, skill.Cost, Darkest.Core.Contracts.UnitId.Of(target), effect);
                GD.Print($"[拓扑UI] 扎营技能 {skill.Name}：{(used ? "已使用" : "拒绝")}　剩余 Respite {Session.RespiteLeft}");
                BuildCampSkillPanel(); // 刷新（点数/可用性变化）
            };
            AddChild(b);
            _campSkillButtons.Add(b);
        }
    }

    /// <summary>收起扎营面板（结束扎营后）。</summary>
    public void ClearCampSkillPanel()
    {
        foreach (Button b in _campSkillButtons)
        {
            b.QueueFree();
        }

        _campSkillButtons.Clear();
    }

    /// <summary>🔴 供冒烟：**真实点击第 i 个扎营技能**。</summary>
    public bool PressCampSkill(int index)
    {
        if (index < 0 || index >= _campSkillButtons.Count)
        {
            GD.Print($"[拓扑UI] PressCampSkill({index})：没有这个技能（当前 {_campSkillButtons.Count} 个）");
            return false;
        }

        GD.Print($"[拓扑UI] PressCampSkill({index})：发出真实 Pressed（按钮「{_campSkillButtons[index].Text}」）");
        _campSkillButtons[index].EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>🔴 供冒烟：**真实点击【结束扎营】**。</summary>
    public void PressFinishCamp()
    {
        if (_finishCamp is null)
        {
            GD.Print("[拓扑UI] PressFinishCamp：没有结束扎营按钮（未在扎营中）");
            return;
        }

        GD.Print("[拓扑UI] PressFinishCamp：发出真实 Pressed");
        _finishCamp.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>
    /// 🔴 冒烟：**真实点击"扎营"**并返回**是否已路由到战斗场景**（夜袭触发时）——
    /// 供 `_Ready` 决定"是否终止本次钩子链"（否则 `--return-town` 等钩子会在已离开地图时误触发）。
    /// </summary>
    public bool PressCampAndMaybeRouteToBattle()
    {
        if (_campInTopology is null || _flow is null)
        {
            GD.Print("[拓扑UI] PressCampAndMaybeRouteToBattle：没有扎营按钮（非拓扑模式）");
            return false;
        }

        GD.Print("[拓扑UI] PressCamp：发出真实 Pressed（扎营）");
        _campInTopology.EmitSignal(BaseButton.SignalName.Pressed);
        return _flow.LastCampAmbushed; // 触发过夜袭 ⇒ 上面已切到 Battle.tscn
    }

    /// <summary>
    /// 🔴 **本趟收尾（回城）** —— 线性模式与拓扑模式**共用同一条收尾路径**：
    /// ① `ReturnToTown(outcome)`（结果入事件流）② **名册士气写回**（`#245`：回城不恢复 ⇒ 士气必须跨趟留存）
    /// ③ `ExpeditionContext.End()`（清空"一趟"引用；金钱/名册/传家宝**不随 End 清空**）④ 切到 Hamlet。
    /// </summary>
    public void FinishRunToTown(string outcome)
    {
        if (_flow is null)
        {
            return;
        }

        _flow.ReturnToTown(outcome);
        ExpeditionContext.Roster?.ApplyReturnFromRun(Log, Session!.Roster().Select(r => (r.Id, r.Morale)));
        ExpeditionContext.End();
        GD.Print($"[拓扑UI] 回城：本趟结束（outcome={outcome}，共走 {_flow.StepsDone} 段 ／ 胜 {_flow.Wins}）" +
                 $"⇒ 切到 Hamlet（再出发可从主菜单/回城界面）");
        GetTree().CallDeferred("change_scene_to_file", "res://scenes/hamlet/Hamlet.tscn");
    }

    /// <summary>🔴 供冒烟：**真实点击"回城（完成本趟）"**。</summary>
    public void PressReturnToTown()
    {
        if (_returnTown is null)
        {
            GD.Print("[拓扑UI] PressReturnToTown：没有回城按钮（非拓扑模式）");
            return;
        }

        GD.Print("[拓扑UI] PressReturnToTown：发出真实 Pressed（回城）");
        _returnTown.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>
    /// 🔴 冒烟：**完整趟的"一步"**（`--run-full`）——每次 `_Ready`（含从战斗/夜袭返回后）推进一格：
    /// ① 若已【完成】（到达终点 ＋ 打赢 ≥3）⇒ **真实点击"回城"**（闭环终点）
    /// ② 否则在分叉点取第一间未探索房 ⇒ **真实点击**走进去（战斗房会切 `Battle.tscn` ⇒ 由战斗场景接管）
    /// </summary>
    public void RunFullSmokeStep()
    {
        if (_flow is null)
        {
            return;
        }

        // 循环推进：**非战斗房不切场景** ⇒ 必须在本轮内继续走，否则钩子链会停在原地（我踩过一次）
        int guard = 0;
        while (guard++ < 40)
        {
            if (_flow.ReachedGoal && _flow.Completed)
            {
                GD.Print($"[拓扑UI] --run-full：**完成口径达成**（到达终点 ／ 胜 {_flow.Wins} ≥ 3 ／ 段 {_flow.StepsDone}）⇒ 真实点击回城");
                PressReturnToTown();
                return;
            }

            IReadOnlyList<MapRoom> options = _flow.AdjacentUnexplored();
            if (options.Count == 0)
            {
                GD.Print($"[拓扑UI] --run-full：无路可走（到达终点 {_flow.ReachedGoal} ／ 胜 {_flow.Wins} ／ 完成 {_flow.Completed}）" +
                         $"⇒ 如实回城（outcome=retreat）");
                PressReturnToTown();
                return;
            }

            MapRoom next = options[0];
            GD.Print($"[拓扑UI] --run-full：推进一格（段 {_flow.StepsDone} ／ 胜 {_flow.Wins} ／ 到达终点 {_flow.ReachedGoal}）" +
                     $"⇒ 目标 房间 {next.Id}（{next.Type}）");
            PressMapRoom(0);

            if (_routedToBattle)
            {
                _routedToBattle = false;
                return; // 已切到战斗场景 ⇒ 等它返回后再继续（下次 `_Ready` 会重新进这里）
            }

            // 事件房：走进后是**待选事件** ⇒ 冒烟自动选 A（真实点击路径：`ChooseEventOption`）
            if (_pendingEventNodeId is not null)
            {
                GD.Print($"[拓扑UI] --run-full：事件房待选（节点 {_pendingEventNodeId}）⇒ 自动选 A（真实路径 ChooseEventOption(0)）");
                ChooseEventOption(0);
            }
        }

        GD.Print("[拓扑UI] --run-full：单轮推进达上限（40 格）⇒ 停下（防死循环）");
    }

    /// <summary>🔴 供冒烟：**真实点击"扎营"**（发真实 `Pressed` ⇒ 走玩家路径）。</summary>
    public void PressCamp()
    {        if (_campInTopology is null)
        {
            GD.Print("[拓扑UI] PressCamp：没有扎营按钮（非拓扑模式）");
            return;
        }

        GD.Print("[拓扑UI] PressCamp：发出真实 Pressed（扎营）");
        _campInTopology.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
