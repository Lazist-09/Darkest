using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>一步的类型。</summary>
public enum FlowStepKind
{
    Battle,
    Event,
    Done,
}

/// <summary>当前待处理的步骤（供 UI 渲染与转发）。</summary>
public sealed record FlowStep(int Index, FlowStepKind Kind, string NodeId, IReadOnlyList<PathOption> Options);

/// <summary>
/// M7.5 **远征流程控制器**（内核，零 Godot）：把"选路 → 侦察 → 光照前进 →（战斗 | 事件）→ 掉落 → 扎营 → 下一步"
/// 串成一个可驱动的状态机，**供 Godot 场景层往返调用**：
/// · 战斗步骤：UI 切到战斗场景，打完把 `OnBattleFinished` 回灌（结果 + 回合数）；
/// · 事件步骤：UI 直接调 `ResolveEvent(0/1)`（**二选一，无跳过**）；
/// · 扎营：`Camp()`（柴火不足 ⇒ 内核拒绝且不扣）。
/// 🔴 状态与判定全在内核；数据由外部注入；**本类不持 Godot 引用**。
/// </summary>
public sealed class ExpeditionFlow
{
    private readonly ExpeditionSession _session;
    private readonly LightMeter _meter;
    private readonly Inventory _bag;
    private readonly Scouting _scout;
    private readonly ExpeditionNodesConfig _nodes;
    private readonly TuningConfig _tuning;
    private readonly CombatLog _log;
    private readonly IRngProvider _rng;
    private readonly Economy? _economy; // M8.0 ②（#283 硬要求①）：跨趟金钱（组合根持有注入；本类只是调用方）
    private readonly HeirloomStock? _heirlooms;      // M8.1：传家宝库存（与金钱同源的第三种资源）
    private readonly HeirloomConfig? _heirloomConfig; // M8.1：掉落曲线

    // 🔴 流程闭环（`#307`⑤）：**返程复用的只读面** —— 战斗结束切回远征场景时，
    //    远征场景要用【同一趟】的 meter/bag/nodes 重建 UI，**而不是新建一趟**。
    //    （`Meter` / `Bag` / `Session` 已有同名只读属性 ⇒ 这里只补 `Nodes` / `Tuning`）
    public ExpeditionNodesConfig Nodes => _nodes;
    public TuningConfig Tuning => _tuning;

    // ---------------------------------------------------------------
    // M7.6 片 (i)：**拓扑模式**（地图驱动）—— 与旧"线性 6 节点 + 每步二选一"并存但**互斥**
    //   · 未注入 `mapCfg` ⇒ 走旧线性路径（**保留**：A1 判定闸 / 旧 e2e 依赖）
    //   · 注入 `mapCfg`  ⇒ 走拓扑路径（StepTo(roomId) + 相邻未探索房间）
    // ---------------------------------------------------------------
    private ExpeditionMapConfig? _mapCfg;
    private ExpeditionMap? _map;
    private HashSet<int>? _visitedRooms;
    private int _currentRoomId = -1;

    /// <summary>是否拓扑模式（地图驱动）。</summary>
    public bool IsTopologyMode => _map is not null;

    /// <summary>当前地图（拓扑模式；供 UI **画出**房间+走廊 &#8212; `m7_roadmap §4.3①`）。</summary>
    public ExpeditionMap? Map => _map;

    /// <summary>某个房间是否已探索过（供 UI 区分"已探索 / 可走"）。</summary>
    public bool HasVisited(int roomId) => _visitedRooms?.Contains(roomId) ?? false;

    /// <summary>当前房间（拓扑模式）。</summary>
    public int CurrentRoomId => _currentRoomId;

    /// <summary>🔴 M7.6：**开启拓扑模式** —— 生成地图（**所有随机写 `RngDraw`**）并落在起点。</summary>
    public ExpeditionMap BeginTopology(ExpeditionMapConfig mapCfg)
    {
        _mapCfg = mapCfg ?? throw new ArgumentNullException(nameof(mapCfg));
        _map = ExpeditionMapGenerator.Generate(_log, _rng, mapCfg);
        _visitedRooms = new HashSet<int> { _map.StartId };
        _currentRoomId = _map.StartId;
        return _map;
    }

    /// <summary>拓扑模式：**当前位置的相邻未探索房间**（供 UI 渲染"选路"，取代旧的"每步二选一"）。</summary>
    public IReadOnlyList<MapRoom> AdjacentUnexplored()
    {
        if (_map is null || _visitedRooms is null)
        {
            return Array.Empty<MapRoom>();
        }

        return _map.Edges
            .Where(e => (e.From == _currentRoomId && !_visitedRooms.Contains(e.To))
                        || (e.To == _currentRoomId && !_visitedRooms.Contains(e.From)))
            .Select(e => e.From == _currentRoomId ? e.To : e.From)
            .Distinct()
            .Select(id => _map.Rooms.First(r => r.Id == id))
            .ToArray();
    }

    /// <summary>
    /// 🔴 M7.6：**走到某个相邻房间**（取代 `Advance(optionIndex)`）—— 按"新区域 −30 ／ 重走 −10"计价，
    /// 推进光照；段数 +1（**只有新房间才算进度**）。
    /// </summary>
    public MoveOutcome StepTo(int roomId)
    {
        if (_map is null || _mapCfg?.Move is null || _visitedRooms is null)
        {
            throw new InvalidOperationException("未开启拓扑模式（先调用 BeginTopology）。");
        }

        bool wasVisited = _visitedRooms.Contains(roomId);
        MoveOutcome outcome = MapTraversal.Step(_log, _map, _mapCfg.Move, _meter, _currentRoomId, roomId, _visitedRooms);
        if (outcome.Moved)
        {
            _currentRoomId = roomId;
            if (!wasVisited)
            {
                StepsDone++; // 只有**首次进入**才算推进了一步（回头不算进度）
            }
        }

        return outcome;
    }

    /// <summary>拓扑模式：是否已走到终点（主干末房）—— 完成口径的另一半是 `Wins ≥ battle_goal`。</summary>
    public bool ReachedGoal => _map is not null && _currentRoomId == _map.GoalId;

    /// <summary>拓扑模式下当前房间的类型（battle / event ⇒ 决定进战斗还是进事件）。</summary>
    public string? CurrentRoomType => _map?.Rooms.First(r => r.Id == _currentRoomId).Type;

    private IReadOnlyList<PathStep>? _path;

    public ExpeditionFlow(ExpeditionSession session, LightMeter meter, Inventory bag, Scouting scout,
        ExpeditionNodesConfig nodes, TuningConfig tuning, CombatLog log, IRngProvider rng,
        Economy? economy = null, HeirloomStock? heirlooms = null, HeirloomConfig? heirloomConfig = null)
    {
        _economy = economy;
        _heirlooms = heirlooms;
        _heirloomConfig = heirloomConfig;
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _meter = meter ?? throw new ArgumentNullException(nameof(meter));
        _bag = bag ?? throw new ArgumentNullException(nameof(bag));
        _scout = scout ?? throw new ArgumentNullException(nameof(scout));
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _meter.EmitStart(_log);
    }

    /// <summary>
    /// **预览下一步的两个候选**（供选路界面在玩家点击**之前**渲染；**不消耗**流程状态、不掷骰）。
    /// 玩家点选后调用 <see cref="Advance"/>（携带所选下标）。
    /// </summary>
    public IReadOnlyList<PathOption> PreviewOptions()
    {
        _path ??= ExpeditionPathPlanner.GeneratePath(_log, _rng, _tuning.Expedition.NBattles, _nodes);
        return StepsDone < _path.Count ? _path[StepsDone].Options : Array.Empty<PathOption>();
    }

    /// <summary>当前步骤（null = 尚未开始或已结束）。</summary>
    public FlowStep? Current { get; private set; }

    /// <summary>已完成步数。</summary>
    public int StepsDone { get; private set; }

    /// <summary>上一次侦察结果（供 UI 必显 13）。</summary>
    public ScoutOutcome? LastScout { get; private set; }

    /// <summary>本趟是否已结束（走完 6 步 / 撤退 / 全灭）。</summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// 推进到下一步：选路（`optionIndex` 0/1）→ 侦察判定 → 光照 −15 → 判定类型。
    /// 走到路径尽头 ⇒ `Done`。
    /// </summary>
    public FlowStep Advance(int optionIndex)
    {
        if (IsFinished)
        {
            return new FlowStep(StepsDone, FlowStepKind.Done, string.Empty, Array.Empty<PathOption>());
        }

        _path ??= ExpeditionPathPlanner.GeneratePath(_log, _rng, _tuning.Expedition.NBattles, _nodes);
        if (StepsDone >= _path.Count)
        {
            IsFinished = true;
            Current = new FlowStep(StepsDone, FlowStepKind.Done, string.Empty, Array.Empty<PathOption>());
            return Current;
        }

        PathStep step = _path[StepsDone];
        PathOption chosen = ExpeditionPathPlanner.ChoosePath(_log, step, optionIndex);

        // 侦察：只揭示【下一个】节点类型（失败 ⇒ null）；每次判定必写 RngDraw
        LastScout = _scout.Roll(_log, _rng, _meter.Value, chosen.NodeType);

        _meter.TryAdvanceNode(_log); // 前进一个节点 −15

        Current = new FlowStep(StepsDone, chosen.NodeType == "battle" ? FlowStepKind.Battle : FlowStepKind.Event,
            chosen.NodeId, step.Options);
        return Current;
    }

    /// <summary>结算事件步骤（二选一；内核保证越界即拒）。</summary>
    public void ResolveEvent(int optionIndex)
    {
        if (Current is not { Kind: FlowStepKind.Event } step)
        {
            throw new InvalidOperationException("当前步骤不是事件节点（流程层不应调用）。");
        }

        _session.ResolveEventNode(_log, _nodes.Get(step.NodeId), optionIndex);
        StepsDone++;
    }

    /// <summary>
    /// 战斗结束回灌：失败/撤退 ⇒ 本趟结束；胜利 ⇒ **按档给份数掉落**（#270：不掷骰）并推进。
    /// 🔴 `#307`③：**夜袭战斗**（`isAmbush: true`）是"扎营后插进来的额外战斗"，**不是节点步骤** ⇒
    ///    守卫对它放行（契约 `m7_expedition.md:35`：夜袭战斗**计入 6 场皆胜**）。
    /// </summary>
    public void OnBattleFinished(string result, int rounds, bool isAmbush = false)
    {
        _ = rounds;
        // 🔴 M7.6：**拓扑模式下没有"线性步骤"**（推进靠 `StepTo(房间)`）⇒ 守卫必须模式感知，
        //    否则从【战斗房】返回时会抛「当前步骤不是战斗节点」（实测：链路直接中断）。
        if (!isAmbush && !IsTopologyMode && Current is not { Kind: FlowStepKind.Battle })
        {
            throw new InvalidOperationException("当前步骤不是战斗节点（流程层不应调用）。");
        }

        if (result != "PlayerVictory")
        {
            IsFinished = true;
            StepsDone++;
            return;
        }

        // 🔴 收益端（#270 裁定①）：按当前光照档**确定给份数**，不引入抽取；
        // 🔴 且按 D2/P21 ⑬：补给**进背包**（`Inventory.TryPickup` 流程）—— **满则进"待处理"、绝不静默丢**。
        TuningLootSpec spec = _tuning.Light!.Loot[LightMeter.TierId(_meter.Tier)]; // #276：类型 + 份数（柴火优先）
        for (int i = 0; i < spec.Firewood; i++)
        {
            TryCollectLoot(ItemKind.Firewood); // #276：柴火优先 —— 摸黑搏到的补给要能变成【多一次扎营】
        }

        for (int i = 0; i < spec.Food; i++)
        {
            TryCollectLoot(ItemKind.Food);
        }

        LootFirewood += spec.Firewood; // ㉓ 第三列（与扎营次数配对，看出"摸黑换来的续航"）

        // 🔴 M8.0 ②（#283 硬要求①）：**跨趟回报** —— 每打赢一场按【当前光照档】记金钱（越暗越多）；
        //    注入 `Economy` 才生效（未注入 = 该项目尚未接入，不静默造一份平行账）。
        _economy?.AwardBattle(_log, LightMeter.TierId(_meter.Tier), "battle");

        // 🔴 M8.1：**传家宝与金钱同源**（同一结算点、同一光照档）—— 未注入则不记（不静默造平行账）
        _heirlooms?.AwardForTier(_log, LightMeter.TierId(_meter.Tier), "battle");

        Wins++; // #273：完成需要「打赢 ≥ battle_goal 场」；🔴 夜袭战斗同样计入（契约 m7_expedition.md:35）
        StepsDone++;

        // 🔴 跨场 buff 计时（`m7_expedition.md:160`）：一场结束 ⇒ 剩余场数 −1（到 0 清）。
        //    `next_battle`（磨刀/加固甲胄）注入后剩余 1 ⇒ 本场结束即消耗掉 ⇒ **只生效一场** ✓
        //    `battles:N`（训话/打气）⇒ 每场 −1，**扎营不清**（此处不涉及扎营，天然满足"扎营不清"✓）
        _session.ConsumeRunBuffsAfterBattle();
    }

    private readonly Queue<InventoryItem> _pendingLoot = new();
    private int _lootSeq;

    /// <summary>包满而暂未收下的补给（**待玩家在主面板/背包界面选择丢弃后收取**；不是丢弃）。</summary>
    public IReadOnlyCollection<InventoryItem> PendingLoot => _pendingLoot;

    /// <summary>背包界面在"选择丢弃"流程里调用：把一件补给收进背包。</summary>
    public bool TryCollectLoot(InventoryItem item) => Collect(item);

    /// <summary>收取一件补给（按类型进背包并同步会话计数）。</summary>
    public bool TryCollectLoot(ItemKind kind) => Collect(new InventoryItem(kind, $"loot_{StepsDone}_{_lootSeq++}"));

    /// <summary>把"待处理"补给再尝试收一次（玩家腾出格子后调用）。</summary>
    public bool RetryPendingLoot()
    {
        while (_pendingLoot.Count > 0)
        {
            InventoryItem next = _pendingLoot.Peek();
            if (!Collect(next))
            {
                return false;
            }

            _pendingLoot.Dequeue();
        }

        return true;
    }

    private bool Collect(InventoryItem item)
    {
        if (!_bag.TryAdd(item, out string reason))
        {
            if (reason == "full_choose_discard")
            {
                _pendingLoot.Enqueue(item); // 🔴 不静默丢：交由玩家选择丢弃哪一格
            }

            return false;
        }

        // 与远征会话的资源计数保持同步（背包是物品来源；会话计数用于资源收支读数）
        if (item.Kind == ItemKind.Firewood)
        {
            _session.Gain(_log, "firewood", 1, "loot");
        }
        else if (item.Kind == ItemKind.Food)
        {
            _session.Gain(_log, "food", 1, "loot");
        }

        return true;
    }

    /// <summary>夜袭判定（扎营后调用；触发则插一场额外战斗，计入完成）。</summary>
    public bool RollAmbush() => _session.RollAmbush(_log, _rng);

    /// <summary>
    /// 🔴 **Curio 结算**（`doc/modules/curio.md` / `#313`）—— 流程层负责它持有的两种 kind：
    /// `light`（`LightMeter`）与 `scout`（`Scouting`）；其余走会话（资源/士气）。
    /// · `itemUsed is null` ⇒ **空手**（内核掷骰 + 写 `RngDraw`）
    /// · 否则 ⇒ **道具直查**（不掷骰）；数据没定义该道具 ⇒ 返回 `null`（调用方应拒绝，V7）
    /// · 命中**阶段二 kind** ⇒ **不施加效果**，以 `LastCurioDeferred = true` 显式告知（红线 21，不静默）
    /// · **走开**走 <see cref="LeaveCurio"/>（零变化）
    /// </summary>
    public CurioOutcome? ResolveCurio(Darkest.Data.CurioConfig curio, string? itemUsed)
    {
        CurioOutcome? outcome = itemUsed is null
            ? CurioResolver.ResolveBare(curio, _rng, _log)
            : CurioResolver.ResolveItem(curio, itemUsed);
        if (outcome is null)
        {
            LastCurioDeferred = false;
            LastCurioText = null;
            return null; // 该道具对此 Curio 未定义 ⇒ 拒绝（V7：UI 不该列它）
        }

        LastCurioDeferred = outcome.Deferred;
        LastCurioText = outcome.Text;
        if (outcome.Deferred)
        {
            // 🔴 阶段二：**显式不生效**（写可审计事件 + UI 标注"未接线"）
            _log.Append(new Darkest.Core.Events.EffectEvent(default,
                $"curio_deferred:{curio.Id}:{outcome.Kind}", 0.0, Triggered: false));
            _log.Append(new EventNodeResolvedEvent(curio.Id, outcome.Route, $"deferred:{outcome.Kind}"));
            return outcome;
        }

        ApplyCurioEffect(outcome.Kind, outcome.Amount);
        if (outcome.ExtraKind is { } extra && outcome.ExtraAmount != 0)
        {
            ApplyCurioEffect(extra, outcome.ExtraAmount);
        }

        _log.Append(new EventNodeResolvedEvent(curio.Id, outcome.Route,
            $"{outcome.Kind}:{outcome.Amount}" +
            (outcome.ItemUsed is null ? string.Empty : $";item:{outcome.ItemUsed}")));
        return outcome;
    }

    /// <summary>🔴 **走开**（V4）：零变化、不掷骰、不阻塞 —— 只写一条"未交互"事件 + 文本。</summary>
    public CurioOutcome LeaveCurio(Darkest.Data.CurioConfig curio)
    {
        CurioOutcome outcome = CurioResolver.Leave(curio);
        LastCurioDeferred = false;
        LastCurioText = outcome.Text;
        _log.Append(new EventNodeResolvedEvent(curio.Id, "leave", "none:0"));
        return outcome;
    }

    /// <summary>施加一种 Curio 效果（`light`/`scout` 走流程层持有者；其余走会话）。</summary>
    private void ApplyCurioEffect(string kind, int amount)
    {
        switch (kind)
        {
            case "none":
                break;
            case "food":
            case "firewood":
            case "gold":
                _session.Gain(_log, kind, amount, "curio");
                break;
            case "morale_team":
                _session.ApplyTeamMorale(_log, amount, "curio");
                break;
            case "light":
                _meter.TryAdvanceBy(_log, amount, "curio"); // 🔴 流程层持有光照计
                break;
            case "scout":
                LastScout = _scout.Roll(_log, _rng, _meter.Value, "curio");
                break;
            case "damage_buff":
                // 🔴 圣坛（`curio.md` §3 #5）：**本趟 +N% 伤害，到扎营** —— 跨场祝福（取大）+ 扎营清 ✓
                _session.GrantCurioDamageBlessing(amount);
                _log.Append(new Darkest.Core.Events.EffectEvent(default,
                    $"curio_damage_blessing:{amount}", 100.0, true));
                break;
            default:
                // 已登记的阶段二 kind 不会走到这里（上面已提前返回）；走到这里说明数据用了**未登记**kind
                // ⇒ 加载期就该炸（`CuriosConfig.Parse`）⇒ 这里也不静默：
                throw new InvalidOperationException($"Curio 效果 kind \"{kind}\" 没有消费通道（红线 21）。");
        }
    }

    /// <summary>最近一次 Curio 的**描述文本**（V6；供 UI 显示）。</summary>
    public string? LastCurioText { get; private set; }

    /// <summary>最近一次 Curio 是否命中**阶段二（未接线）**分支（UI 必须据此标注，红线 21）。</summary>
    public bool LastCurioDeferred { get; private set; }

    /// <summary>扎营（柴火不足 ⇒ 拒绝；成功则光照回满）。**最小版：一调用到底**（供测试/旧路径）。</summary>
    public bool Camp()
    {
        if (!BeginCamp())
        {
            return false;
        }

        return FinishCamp();
    }

    /// <summary>
    /// 🔴 **拆开扎营（阶段一→二）**：`StartCamp`（扣柴火 + 给 Respite 点数）+ 选口粮 + 光照回满。
    /// 拆开的理由（红线 18/21）：**扎营技能必须让玩家【点得到】** —— 原 `Camp()` 是一调用到底的，
    /// UI 没有插"选技能"的位置 ⇒ 6 个已接线的扎营技能玩家永远碰不到 ⚠️
    /// 返回 false ⇒ 柴火不足（拒绝、不扣）。
    /// </summary>
    public bool BeginCamp()
    {
        if (!_session.StartCamp(_log, StepsDone, _tuning.Camp!.RespiteBase))
        {
            return false;
        }

        _meter.OnCamp(_log);
        string best = _session.CanAffordFood(_tuning.Camp, "feast") ? "feast"
            : _session.CanAffordFood(_tuning.Camp, "full") ? "full"
            : _session.CanAffordFood(_tuning.Camp, "half") ? "half" : "starve";
        _session.ChooseFood(_log, _tuning.Camp, best);
        return true;
    }

    /// <summary>
    /// 🔴 **结束扎营（阶段二→三）**：`EndCamp` ＋【阶段三：夜袭判定】（契约 `m7_expedition.md:143`）。
    /// 触发夜袭 ⇒ `LastCampAmbushed = true` ⇒ 调用方插一场额外战斗（计入胜场）。
    /// </summary>
    public bool FinishCamp()
    {
        _session.EndCamp(_log);
        LastCampAmbushed = RollAmbush();
        return true;
    }

    /// <summary>上一次扎营后是否触发夜袭（`#305`：触发 ⇒ 调用方插一场额外战斗，计入胜场）。</summary>
    public bool LastCampAmbushed { get; private set; }

    /// <summary>
    /// 🔴 **`#307`③：夜袭【真的插一场战斗】**（契约 `m7_expedition.md:35`：**夜袭产生的战斗计入 6 场皆胜**）——
    /// 返回一个**真实战斗**（走与常规战斗完全相同的构建路径：同一难度递进 + **当前光照档**），
    /// 其 `battleIndex` 取 `BattlesPlayed + 1` ⇒ 结算后**自然计入胜场/掉落/传家宝**（无需特殊通道）。
    /// ⚠️ 调用方负责跑回合并调 `OnBattleFinished`（与常规战斗一致）。
    /// </summary>
    public Darkest.Gameplay.Sim.Director.BattleDirector BeginAmbushBattle(Darkest.Core.Events.CombatLog log)
    {
        if (!LastCampAmbushed)
        {
            throw new InvalidOperationException("未触发夜袭：先扎营（Camp）且 LastCampAmbushed 为真。");
        }

        AmbushBattleStarted = true;
        int idx = _session.BattlesPlayed + 1;
        return _session.BeginExpeditionBattle(idx, log, _tuning.Expedition.DifficultyTiers, _meter.Effect);
    }

    /// <summary>本趟是否已经为夜袭插过战斗（防重复插）。</summary>
    public bool AmbushBattleStarted { get; private set; }

    /// <summary>
    /// 🔴 **房间 → 事件节点**的映射（`#307`⑤ 剩余：拓扑模式下"走进事件房要有内容"）。
    ///
    /// ⚠️ **本节是最小确定性映射**（`roomId % 事件节点数`）—— **不掷骰**（因此不需要 `RngDraw`），
    ///    目的是"让事件房有内容可玩"；**正式的"房间 ↔ 节点"配额/权重设计属内容层**（后续再定）。
    /// 返回 null ⇒ 该房间没有事件内容（战斗房 / 特殊房由各自逻辑处理）。
    /// </summary>
    public string? EventNodeIdForRoom(int roomId)
    {
        ExpeditionNodeConfig[] events = _nodes.Nodes.Where(n => n.Type == "event").ToArray();
        return events.Length == 0 ? null : events[roomId % events.Length].Id;
    }

    /// <summary>回城结算（士气完全不恢复由内核 #245 保证）。</summary>
    public int ReturnToTown(string outcome)
    {
        IsFinished = true;
        return _session.ReturnToTown(_log, outcome);
    }

    /// <summary>本趟**掉落的柴火份数**（㉓ 第三列：与扎营次数配对，看"摸黑换来的续航"）。</summary>
    public int LootFirewood { get; private set; }

    /// <summary>本趟已打赢的战斗数（敌方全灭计一场）。</summary>
    public int Wins { get; private set; }

    /// <summary>
    /// **完成口径（#273 + M7.6）**：**走完到终点** **且** **打赢 ≥ `battle_goal` 场**。
    /// 🔴 拓扑模式下"走完"= **走到主干终点**（`ReachedGoal`）；线性模式下 = 走完 `n_battles` 步。
    /// 🔴 **数值不变（3）**；**不得按房间数比例**（否则判据随拓扑漂移）。
    /// </summary>
    public bool Completed => (IsTopologyMode ? ReachedGoal : StepsDone >= _tuning.Expedition.NBattles)
                             && Wins >= _tuning.Expedition.BattleGoal;

    /// <summary>背包（供 UI 渲染格子）。</summary>
    public Inventory Bag => _bag;

    /// <summary>光照计（供 UI 渲染光照条）。</summary>
    public LightMeter Meter => _meter;

    /// <summary>会话（供投影取 HP/士气/资源）。</summary>
    public ExpeditionSession Session => _session;
}
