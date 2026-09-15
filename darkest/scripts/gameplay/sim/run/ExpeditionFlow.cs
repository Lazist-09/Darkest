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

    /// <summary>🆕 表现层读数：**已揭示房间集合**（= `HasVisited` 的集合形态；省得逐间问）✓ 顺序无意义 ✓</summary>
    public IReadOnlyList<int> RevealedRoomIds => _visitedRooms is null ? Array.Empty<int>() : _visitedRooms.ToArray();

    /// <summary>🆕 表现层读数：**朝目标走的下一间**（走廊推进用；-1 = 不可达/已到）—— 口径见 `MapTraversal.FirstStepToward` ✓</summary>
    public int NextRoomToward(int roomId) => _map is null ? -1 : MapTraversal.FirstStepToward(_map, _currentRoomId, roomId);

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

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // 🆕 **走格**（用户裁 (B)：格内自由走）＋**逐格光照扣除**（策划 `#338`① 的守恒口径）
    //    🔴 **opt-in**：不开启时，现有"按房间推进"的路径**一行都不受影响** ✓
    //    🔴 未登记遭遇/视野/陷阱 ⇒ 此处**一律不做**（用户"规则暂留"；契约 P30 ⑤⑥ 也不许填默认值）✓
    // ══════════════════════════════════════════════════════════════════════════════════════════

    private DungeonWalker? _tileWalker;
    private Dictionary<(int X, int Y), int>? _tileRoom;
    private Dictionary<(int X, int Y), (int Segment, int Index, int Length)>? _tileSegAt;
    private int _tileSegmentCost;
    private int _tileAcc;
    private int _tileSegmentId = -1;

    /// <summary>是否已开启走格（表现层据此决定用"格子主画面"还是旧的房间视图）✓</summary>
    public bool TileWalkEnabled => _tileWalker is not null;

    public (int X, int Y) TilePosition => _tileWalker?.Position ?? (-1, -1);

    public DungeonTileKind TileHere => _tileWalker?.CurrentTile ?? DungeonTileKind.Wall;

    public int TileStepsTaken => _tileWalker?.StepsTaken ?? 0;

    /// <summary>本趟派生出的瓷砖网格（`null` = 未开启走格）✓</summary>
    public DungeonGridDeriver.Derived? TileWalk { get; private set; }

    /// <summary>
    /// 🔴 **开启走格**（幂等）：从当前拓扑图**派生**网格，队伍落在**起点房间中心** ✓
    /// `segmentCost` 由调用方给（**现值** = `tuning.expedition`/`map.move.new_area` = 30 ⇒ 本类不写死）✓
    /// </summary>
    public void EnableTileWalk(int segmentCost)
    {
        if (_map is null)
        {
            throw new InvalidOperationException("未开启拓扑模式（先 `BeginTopology`）⇒ 不能开启走格 ✓");
        }

        if (_tileWalker is not null)
        {
            return; // 幂等 ✓
        }

        DungeonGridDeriver.Derived d = DungeonGridDeriver.Derive(_map);
        TileWalk = d;
        _tileRoom = new Dictionary<(int X, int Y), int>(d.TileRoom);
        _tileSegAt = new Dictionary<(int X, int Y), (int Segment, int Index, int Length)>();
        for (int si = 0; si < d.Segments.Count; si++)
        {
            DungeonGridDeriver.CorridorSegment seg = d.Segments[si];
            for (int i = 0; i < seg.Tiles.Count; i++)
            {
                _tileSegAt[seg.Tiles[i]] = (si, i, seg.Tiles.Count); // 🔴 预知长度 ⇒ 逐格扣才算得准 ✓
            }
        }

        _tileWalker = new DungeonWalker(d.Grid, d.Start);
        _tileSegmentCost = segmentCost;
        _tileAcc = 0;
        _tileSegmentId = -1;
        _currentRoomId = _map.StartId; // 走格把"当前房间"也带上（`ReachedGoal`/内容判定继续有效）✓
    }

    /// <summary>
    /// 🔴 **走一格**（四向）：撞墙/越界 ⇒ `false` 且**状态零变化**（不计步、不扣光、不揭示）✓
    /// 走廊格 ⇒ 按**本段剩余格数**逐格扣光（余数结转、末格扣清 ⇒ 总扣除恒 = 段数 × `segmentCost`）✓
    /// 房间格 ⇒ **不扣光**，并把"当前房间"跟到该格所属房间 ✓
    /// </summary>
    public bool TryStepTile(int dx, int dy)
    {
        if (_tileWalker is null || _tileRoom is null || _tileSegAt is null)
        {
            return false;
        }

        if (!_tileWalker.TryStep(dx, dy))
        {
            return false; // 被拒 ⇒ 什么都不变 ✓
        }

        (int X, int Y) pos = _tileWalker.Position;
        if (_tileSegAt.TryGetValue(pos, out (int Segment, int Index, int Length) at))
        {
            if (at.Segment != _tileSegmentId)
            {
                _tileSegmentId = at.Segment;
                _tileAcc = _tileSegmentCost; // 🔴 进段：acc = 段消耗 ✓
            }

            int remaining = at.Length - at.Index; // **含本格** ✓
            (int deduct, int newAcc) = WalkLightCost.StepCost(_tileAcc, remaining);
            _tileAcc = newAcc;
            if (deduct > 0)
            {
                _meter.TryAdvanceBy(_log, -deduct, "walk"); // 负值 = 前进消耗 ✓
            }
        }
        else if (_tileRoom.TryGetValue(pos, out int roomId))
        {
            _currentRoomId = roomId; // 进房间 ⇒ 房间状态跟上（不扣光 ✓）
        }

        return true;
    }

    /// <summary>
    /// 🔴 策划 `#335`① **命名口径**：`HasReachedGoal`（与既有 `ReachedGoal` 同义，按裁定命名；两者并存只为不破坏既有调用）✓
    /// </summary>
    public bool HasReachedGoal => ReachedGoal;

    /// <summary>
    /// 🔴🔴 策划 `#335`① **只读读数**：**从【当前所在】到 `GoalId` 还剩几段**（表现层"到终点距离"用）。
    ///
    /// 语义**写死**（否则又是"读数不回答被问的问题"）：
    ///   · **已到终点 ⇒ 0**（不是 -1、不是 null）✓
    ///   · **未开始（在入口）⇒ = 全程段数** ✓
    ///   · 只在**拓扑模式**下有 `GoalId`；线性模式（历史路径）用**等价口径**：剩余场数 = `NBattles − StepsDone`（注释写明，避免误读）✓
    /// 📌 为什么不让表现层自己算：`ShortestPathLength` 是**通用 BFS 工具**，让 UI 决定"to = GoalId" = **UI 在算业务规则** ❌（`blueprint §9.17`）
    /// </summary>
    public int RemainingSegmentsToGoal
    {
        get
        {
            if (_map is null)
            {
                // 线性模式（历史路径）：没有图、没有 GoalId ⇒ 用"剩余场数"作**等价口径**并在注释里写清 ✓
                return Math.Max(0, _tuning.Expedition.NBattles - StepsDone);
            }

            if (_currentRoomId == _map.GoalId)
            {
                return 0; // 🔴 已到终点 ⇒ 0 ✓
            }

            int d = MapTraversal.ShortestPathLength(_map, _currentRoomId, _map.GoalId);
            return d < 0 ? 0 : d;
        }
    }

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
        _session.EnterPhase(FlowPhase.Walking); // 🔴 打完 ⇒ 回【走图】相位（可再扎营/选路；内核单一真值）✓
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

        // 🔴 营地士气加成**不得漏进名册**（否则"营地加士气"会变成免费减压 —— 契约 `#310` ② 禁止）
        //    ⇒ 战后把本趟台账的加成从 `Retained` 扣回 ✓
        _session.StripCampBonusesFromRetained();
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

    /// <summary>
    /// 🔴 **片 C：由【内容表】决定该房间放哪个 Curio**（取代原先的临时确定性映射 `roomId % N`）。
    ///
    /// 读法（`tasks/merged_content_layer_pack.md` §4）：
    /// · `roomType` 行的 `curio_pool` 组成候选池；**支路房**再并入 `branch` 行的池（支路专用覆盖键）✓
    /// · 抽取按**组内权重**（行 `weight`）⇒ 🔴 **写 `RngDraw`**（随机必须留痕：可审计、可复现）✓
    /// · 池为空 ⇒ 返回 `null`（**该房间没有内容** ⇒ 调用方走既有回退，不静默造一个）✓
    /// 🔴 主 = 内容表；`branch_battle_weight` 只是**过渡覆盖项**（默认 0），不得与主混（契约 §3 尾注）。
    /// </summary>
    public string? PickCurioForRoom(Darkest.Data.RoomContentsConfig contents, string roomType, bool isBranch,
        IReadOnlySet<string>? allowedCurios = null)
    {
        // 🔴 `O-86` / C2 消费点 (b) 的**内部门禁**（修一个真缺陷）：
        //    生产调用点（UI）一度**没传** `allowedCurios` ⇒ 未解锁的 Curio 也能被抽到 ⚠️
        //    ⇒ 于是一旦组合根注入了 `Unlocks` + `Progress`，**这里自动按"当前可用 Curio"过滤**，
        //      调用方无需记得传参（少一个"必须记得"的接口 = 少一个漏接的机会）✓
        if (allowedCurios is null && Unlocks is not null && Progress is not null)
        {
            allowedCurios = Progress.AvailableCurios(Unlocks);
        }

        // 候选 = 该类型的行 ∪（支路房）branch 行；权重取【行 weight】（组内权重）
        var candidates = new List<(int Weight, string CurioId)>();
        void Collect(IReadOnlyList<Darkest.Data.RoomContentEntry> rows)
        {
            foreach (Darkest.Data.RoomContentEntry row in rows)
            {
                foreach (string id in row.CurioPool ?? System.Array.Empty<string>())
                {
                    // 🔴 `$pool:<name>`：**校验期已允许，但解析【未实现】** ⇒ 这里**显式跳过并留痕**
                    //    （不静默当成一个 curio id —— 那会在抽取时给出不存在的东西，红线 21）✓
                    if (id.StartsWith("$pool:", StringComparison.Ordinal))
                    {
                        _log.Append(new Darkest.Core.Events.EffectEvent(default,
                            $"curio_pool_unresolved:{id}", 0.0, Triggered: false));
                        continue;
                    }

                    // 🔴 消费点 (b)：**只从【已解锁】的 Curio 里抽**（`O-86` 起手 4 种 → 解锁后 6 种）——
                    //    这是**内核级**拦截（C2：不能只在 UI 上"锁着"）✓ `allowedCurios == null` ⇒ 不限制（测试/旧路径）
                    if (allowedCurios is not null && !allowedCurios.Contains(id))
                    {
                        continue;
                    }

                    candidates.Add((row.Weight, id));
                }
            }
        }

        Collect(contents.ForType(roomType));
        if (isBranch)
        {
            Collect(contents.ForType("branch"));
        }

        if (candidates.Count == 0)
        {
            return null; // 内容表没给这个房间任何内容 ⇒ 交回调用方（不静默造）
        }

        int total = candidates.Sum(c => c.Weight);
        int roll = _rng.NextInt(0, total); // 组内加权抽取
        _log.Append(new RngDraw(_rng.DrawCount, roll)); // 🔴 随机留痕

        int acc = 0;
        foreach ((int weight, string curioId) in candidates)
        {
            acc += weight;
            if (roll < acc)
            {
                _log.Append(new EventNodeResolvedEvent(curioId, "room_content",
                    $"picked:type={roomType}:branch={isBranch}:roll={roll}"));
                return curioId;
            }
        }

        return candidates[^1].CurioId; // 理论到不了（roll < total）；兜底也**不静默**：上面已写留痕
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
    public CurioOutcome? ResolveCurio(Darkest.Data.CurioConfig curio, string? itemUsed,
        Roster? roster = null, Darkest.Data.SanitariumConfig? diseases = null)
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

        ApplyCurioEffect(outcome.Kind, outcome.Amount, roster, diseases);
        if (outcome.ExtraKind is { } extra && outcome.ExtraAmount != 0)
        {
            ApplyCurioEffect(extra, outcome.ExtraAmount, roster, diseases);
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
    private void ApplyCurioEffect(string kind, int amount, Roster? roster = null,
        Darkest.Data.SanitariumConfig? diseases = null)
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
            case "support_pack":
                // 🔴 策划 #329/#332：补给箱（空手）给**支援包** ⇒ 与战利品**同一条收取路径**
                //    （包满 ⇒ 进"待处理"队列，**不静默丢弃**）✓ 让"用支援包"那条已接好的链路真正可达 ✓
                for (int i = 0; i < Math.Max(1, amount); i++)
                {
                    Collect(new InventoryItem(ItemKind.SupportPack, $"curio_supply_{StepsDone}_{_lootSeq++}"));
                }

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
            case "disease_one":
                // 🔴 骸骨堆（`curio.md` §3 #6）：**一人患病** —— 走既有 `Roster.Infect`（与回城患病同一通道）
                //    ⚠️ 受害者是**随机**的 ⇒ **必须写 `RngDraw`**（红线：随机留痕）；
                //    疾病种类取目录第一条（**确定性**，已记档：契约只写"一人患病"，未指定病种）
                if (roster is null || diseases is null || diseases.Diseases.Count == 0)
                {
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        "curio_disease_no_catalog", 0.0, Triggered: false)); // 不静默：缺目录就留痕
                    break;
                }

                string[] candidates = roster.Heroes.Select(h => h.Id).ToArray();
                if (candidates.Length == 0)
                {
                    break;
                }

                int pick = _rng.NextInt(0, candidates.Length);
                _log.Append(new RngDraw(_rng.DrawCount, pick)); // 🔴 随机留痕
                string victim = candidates[pick];
                string diseaseId = diseases.Diseases[0].Id;
                bool infected = roster.Infect(_log, victim, diseaseId, "curio");
                _log.Append(new Darkest.Core.Events.EffectEvent(default,
                    $"curio_disease:{victim}:{diseaseId}:{infected}", 100.0, true));
                break;
            case "trait_positive":
                // 🔴 书堆（`curio.md` §3 #4）：25% ⇒ **随机正面特质**
                //    · 目录 = **名册里出现过的特质**（不新增数据文件 ✓）；正/负判定沿用既有口径
                //      （与 `FindLockablePositiveTrait` 同一判据：`DamagePct > 0 || MoraleDamagePct < 0`）
                //    · **两处随机**（谁 + 哪个特质）⇒ **各写一条 `RngDraw`**（红线：随机留痕）
                if (roster is null)
                {
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        "curio_trait_no_roster", 0.0, Triggered: false));
                    break;
                }

                Darkest.Data.HeroTraitConfig[] pool = roster.Heroes
                    .SelectMany(h => h.Traits)
                    .Where(t => t.DamagePct > 0 || t.MoraleDamagePct < 0)
                    .GroupBy(t => t.Id)
                    .Select(g => g.First())
                    .ToArray();
                if (pool.Length == 0)
                {
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        "curio_trait_no_catalog", 0.0, Triggered: false));
                    break;
                }

                // 🔴 只在**合法对**（该英雄**还没有**的特质）里选 —— 否则会选到已有的 ⇒ `AddTrait` no-op
                //    ⇒ **分支静默无效果**（我实测踩到：seed=17 时没人涨特质）⚠️
                var pairs = new List<(string Hero, Darkest.Data.HeroTraitConfig Trait)>();
                foreach (Darkest.Data.HeroConfig h in roster.Heroes)
                {
                    var owned = roster.TraitsOf(h.Id).Select(t => t.Id).ToHashSet();
                    foreach (Darkest.Data.HeroTraitConfig t in pool.Where(t => !owned.Contains(t.Id)))
                    {
                        pairs.Add((h.Id, t));
                    }
                }

                if (pairs.Count == 0)
                {
                    _log.Append(new Darkest.Core.Events.EffectEvent(default,
                        "curio_trait_all_owned", 0.0, Triggered: false)); // 全都有 ⇒ 留痕（不静默）
                    break;
                }

                int pickPair = _rng.NextInt(0, pairs.Count);
                _log.Append(new RngDraw(_rng.DrawCount, pickPair)); // 🔴 随机留痕
                (string who, Darkest.Data.HeroTraitConfig what) = pairs[pickPair];
                bool added = roster.AddTrait(_log, who, what, "curio");
                _log.Append(new Darkest.Core.Events.EffectEvent(default,
                    $"curio_trait:{who}:{what.Id}:{added}", 100.0, true));
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
        _session.EnterPhase(FlowPhase.Walking); // 🔴 收营 ⇒ 回【走图】相位 ✓
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

    /// <summary>
    /// 🔴 **`O-83` 的生产接线**：战后把本场结束血量落进跨趟台账（由 `BattleRoot.EndGame` 在**回灌结果/切场景之前**调用）。
    /// 用流程自己的 `_log` 留痕 ⇒ 调用方不必再传日志 ✓
    /// </summary>
    public void CaptureBattleEndHp(Darkest.Gameplay.Sim.Director.BattleDirector director)
        => _session.CaptureBattleEndHp(director, _log);

    /// <summary>回城结算（士气完全不恢复由内核 #245 保证）。</summary>
    public int ReturnToTown(string outcome)
    {
        IsFinished = true;

        // 🔴 `next_round` ③：**记"一趟结束"到跨趟进度** —— 这里是**所有路径的唯一咽喉**
        //    （UI 有 `FinishRunToTown` ／线性 e2e 直接调本方法 ⇒ 只挂在 UI 上会**漏计**：我实测踩到，
        //      e2e 跑完一趟仍显示"已完成出征 0 趟" ⚠️）⇒ 挂在流程层，并按 `IsFinished` 防重复计数 ✓
        if (!_runCounted)
        {
            _runCounted = true;
            Progress?.FinishRun(_log, outcome, Wins);
        }

        return _session.ReturnToTown(_log, outcome);
    }

    private bool _runCounted;

    /// <summary>
    /// 跨趟进度（解锁阈值表的输入）。**由组合根注入**（内核层不接触 UI 层持有者）✓
    /// `null` ⇒ 不记（测试/旧路径）—— 不静默：`ReturnToTown` 里的判断是显式的 ✓
    /// </summary>
    public RunProgress? Progress { get; init; }

    /// <summary>
    /// 🔴 **解锁阈值表**（`O-86`）：与 `Progress` 一起由组合根注入 ⇒ 供 `PickCurioForRoom` 内部做
    /// "只从【当前可用】Curio 里抽"的门禁（C2 消费点 (b)）✓ 内核层不读文件（组合根喂字符串解析出的对象）✓
    /// </summary>
    public Darkest.Data.UnlocksConfig? Unlocks { get; init; }

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
