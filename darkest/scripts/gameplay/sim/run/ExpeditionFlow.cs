using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;   // 🆕 路线(c)：恢复件的类体用到它（HEAD 原先没有）✓
using Darkest.Gameplay.Sim.Survival;   // 🆕 事故回填：LightMeter 现居此命名空间（别人把它从 sim/run 搬来）✓

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

public sealed partial class ExpeditionFlow
{
	private readonly ExpeditionSession _session;

	private readonly LightMeter _meter;

	private readonly Inventory _bag;

	private readonly Scouting _scout;

	private readonly ExpeditionNodesConfig _nodes;

	private readonly TuningConfig _tuning;

	private readonly CombatLog _log;

	private readonly IRngProvider _rng;

	private readonly Economy? _economy;

	private readonly HeirloomStock? _heirlooms;

	private readonly HeirloomConfig? _heirloomConfig;

	private ExpeditionMapConfig? _mapCfg;

	private ExpeditionMap? _map;

	private HashSet<int>? _visitedRooms;

	private readonly HashSet<int> _resolvedRooms = new HashSet<int>();

	private int _currentRoomId = -1;

	private DungeonWalker? _tileWalker;

	private Dictionary<(int X, int Y), int>? _tileRoom;

	private Dictionary<(int X, int Y), (int Segment, int Index, int Length)>? _tileSegAt;

	private int _tileSegmentCost;

	private int? _backtrackCost;

	private int _tileAcc;

	private int _tileSegmentId = -1;

	private int _hungerBuffer;

	private TrapGate _pendingTrapGate = TrapGate.Consumed;

	private readonly HashSet<(int X, int Y)> _claimedSecrets = new HashSet<(int, int)>();

	private string? _currentRegion;

	private IReadOnlyList<PathStep>? _path;

	private readonly Queue<InventoryItem> _pendingLoot = new Queue<InventoryItem>();

	private int _lootSeq;

	private bool _runCounted;

	public ExpeditionNodesConfig Nodes => _nodes;

	public TuningConfig Tuning => _tuning;

	public ExpeditionOutcome Outcome { get; private set; } = ExpeditionOutcome.InProgress;

	public bool IsTopologyMode => (object)_map != null;

	public ExpeditionMap? Map => _map;

	public IReadOnlyList<int> RevealedRoomIds => (_visitedRooms == null) ? Array.Empty<int>() : _visitedRooms.ToArray();

	public int CurrentRoomId => _currentRoomId;

	private int EffectiveGoalRoomId
	{
		get
		{
			int result;
			if ((object)_map != null)
			{
				DungeonGridDeriver.Derived tileWalk = TileWalk;
				result = (((object)tileWalk != null && tileWalk.TileRoom.TryGetValue(tileWalk.Grid.Goal, out var value)) ? value : _map.GoalId);
			}
			else
			{
				result = -1;
			}
			return result;
		}
	}

	public bool ReachedGoal => (object)_map != null && _currentRoomId == EffectiveGoalRoomId;

	public bool TileWalkEnabled => _tileWalker != null;

	public (int X, int Y) TilePosition => _tileWalker?.Position ?? (-1, -1);

	public DungeonTileKind TileHere => _tileWalker?.CurrentTile ?? DungeonTileKind.Wall;

	public int TileStepsTaken => _tileWalker?.StepsTaken ?? 0;

	public DungeonGridDeriver.Derived? TileWalk { get; private set; }

	public TrapDefs? Traps { get; private set; }

	public Func<string, int>? TrapResistFor { get; private set; }

	public bool TrapResistSourceDeclared => TrapResistFor != null;

	public int TrapTriggeredCount { get; private set; }

	public int TrapDisarmedCount { get; private set; }

	public (string Region, TrapGate Gate, string Outcome)? LastTrap { get; private set; }

	public int SecretRevealedCount => _claimedSecrets.Count;

	public int SecretGoldGranted { get; private set; }

	public ((int X, int Y) Pos, int Gold)? LastSecret { get; private set; }

	public DungeonGridVision? TileVision { get; private set; }

	public int ScoutedTileCount => _tileWalker?.ScoutedCount ?? 0;

	public int RevealedTileCount => _tileWalker?.Revealed.Count ?? 0;

	public int VisitedTileCount => _tileWalker?.Visited.Count ?? 0;

	public string? CurrentRegion => _currentRegion;

	public HungerRollResult LastHunger { get; private set; } = HungerRollResult.None;

	public int HungerCount { get; private set; }

	public int HungerBuffer => _hungerBuffer;

	public bool HasPendingHunger => LastHunger.Triggered && _session.HungerCanApply;

	public int HungerFoodNeeded
	{
		get
		{
			HungerConfig hungerConfig = _tuning.DungeonLayer?.Hunger;
			return ((object)hungerConfig != null) ? HungerSpawner.FoodRequired(hungerConfig, _session.Survivors) : 0;
		}
	}

	public bool CanEatForHunger
	{
		get
		{
			HungerConfig hungerConfig = _tuning.DungeonLayer?.Hunger;
			return (object)hungerConfig != null && _session.CanEatForHunger(hungerConfig);
		}
	}

	public RevisitRollResult LastRevisitThreat { get; private set; } = RevisitRollResult.None;

	public int RevisitThreatCount { get; private set; }

	public int TileBacktrackCount { get; private set; }

	public bool HasReachedGoal => ReachedGoal;

	public int RemainingSegmentsToGoal
	{
		get
		{
			if ((object)_map == null)
			{
				return Math.Max(0, _tuning.Expedition.NBattles - StepsDone);
			}
			int effectiveGoalRoomId = EffectiveGoalRoomId;
			if (_currentRoomId == effectiveGoalRoomId)
			{
				return 0;
			}
			int num = MapTraversal.ShortestPathLength(_map, _currentRoomId, effectiveGoalRoomId);
			return (num >= 0) ? num : 0;
		}
	}

	public string? CurrentRoomType => _map?.Rooms.First((MapRoom r) => r.Id == _currentRoomId).Type;

	public FlowStep? Current { get; private set; }

	public int StepsDone { get; private set; }

	public ScoutOutcome? LastScout { get; private set; }

	public bool IsFinished { get; private set; }

	public IReadOnlyCollection<InventoryItem> PendingLoot => _pendingLoot;

	public bool AmbushBattleStarted { get; private set; }

	public RunProgress? Progress { get; init; }

	public UnlocksConfig? Unlocks { get; init; }

	public int LootFirewood { get; private set; }

	public int Wins { get; private set; }

	public bool Completed => (IsTopologyMode ? ReachedGoal : (StepsDone >= _tuning.Expedition.NBattles)) && Wins >= _tuning.Expedition.BattleGoal;

	public Inventory Bag => _bag;

	public LightMeter Meter => _meter;

	public ExpeditionSession Session => _session;








	public bool IsRoomResolved(int roomId)
	{
		return _resolvedRooms.Contains(roomId);
	}

	public void Abandon(string reason)
	{
		if (!IsFinished)
		{
			Outcome = ExpeditionOutcome.Abandoned;
			IsFinished = true;
			_log.Append(new ExpeditionAbandoned(reason, StepsDone, Wins, _meter.Value));
		}
	}

	public bool HasVisited(int roomId)
	{
		return _visitedRooms?.Contains(roomId) ?? false;
	}

	public int NextRoomToward(int roomId)
	{
		return ((object)_map == null) ? (-1) : MapTraversal.FirstStepToward(_map, _currentRoomId, roomId);
	}

	public ExpeditionMap BeginTopology(ExpeditionMapConfig mapCfg)
	{
		_mapCfg = mapCfg ?? throw new ArgumentNullException("mapCfg");
		_map = ExpeditionMapGenerator.Generate(_log, _rng, mapCfg);
		if (!_map.IsConnected())
		{
			throw new InvalidOperationException($"\ud83d\udd34 拓扑地图生成后**不连通**（房间 {_map.RoomCount} 间，起点 {_map.StartId} ⇒ 终点 {_map.GoalId}）" + "⇒ 玩家将永远走不到终点。这是生成器缺陷，不是数据问题（`IsConnected` 保证全房间可达）。");
		}
		_visitedRooms = new HashSet<int> { _map.StartId };
		_currentRoomId = _map.StartId;
		return _map;
	}

	public IReadOnlyList<MapRoom> AdjacentUnexplored()
	{
		if ((object)_map == null || _visitedRooms == null)
		{
			return Array.Empty<MapRoom>();
		}
		return (from id in (from e in _map.Edges
				where (e.From == _currentRoomId && !_visitedRooms.Contains(e.To)) || (e.To == _currentRoomId && !_visitedRooms.Contains(e.From))
				select (e.From == _currentRoomId) ? e.To : e.From).Distinct()
			select _map.Rooms.First((MapRoom r) => r.Id == id)).ToArray();
	}

	public MoveOutcome StepTo(int roomId)
	{
		if ((object)_map == null || (object)_mapCfg?.Move == null || _visitedRooms == null)
		{
			throw new InvalidOperationException("未开启拓扑模式（先调用 BeginTopology）。");
		}
		bool flag = _visitedRooms.Contains(roomId);
		MoveOutcome moveOutcome = MapTraversal.Step(_log, _map, _mapCfg.Move, _meter, _currentRoomId, roomId, _visitedRooms);
		if (moveOutcome.Moved)
		{
			_currentRoomId = roomId;
			if (!flag)
			{
				StepsDone++;
			}
		}
		return moveOutcome;
	}

	public void EnableTileWalk(int segmentCost, int? backtrackCost = null)
	{
		if ((object)_map == null)
		{
			throw new InvalidOperationException("未开启拓扑模式（先 `BeginTopology`）⇒ 不能开启走格 ✓");
		}
		if (_tileWalker != null)
		{
			return;
		}
		double valueOrDefault = (_tuning.DungeonLayer?.Traps?.CorridorChancePercent).GetValueOrDefault();
		double valueOrDefault2 = (_tuning.DungeonLayer?.Secrets?.CorridorChancePercent).GetValueOrDefault();
		DungeonGridDeriver.Derived derived = (TileWalk = ((valueOrDefault > 0.0 || valueOrDefault2 > 0.0) ? DungeonGridDeriver.Derive(_map, 3, _rng, valueOrDefault, valueOrDefault2, _log) : DungeonGridDeriver.Derive(_map)));
		DungeonGridDeriver.Derived derived3 = derived;
		_tileRoom = new Dictionary<(int, int), int>(derived3.TileRoom);
		_tileSegAt = new Dictionary<(int, int), (int, int, int)>();
		for (int i = 0; i < derived3.Segments.Count; i++)
		{
			DungeonGridDeriver.CorridorSegment corridorSegment = derived3.Segments[i];
			for (int j = 0; j < corridorSegment.Tiles.Count; j++)
			{
				_tileSegAt[corridorSegment.Tiles[j]] = (i, j, corridorSegment.Tiles.Count);
			}
		}
		_tileWalker = new DungeonWalker(derived3.Grid, derived3.Start);
		_tileSegmentCost = segmentCost;
		_backtrackCost = ((backtrackCost.HasValue && backtrackCost.GetValueOrDefault() > 0) ? backtrackCost : ((int?)null));
		_tileAcc = 0;
		_tileSegmentId = -1;
		TuningGridVision tuningGridVision = _tuning.DungeonLayer?.Vision;
		TileVision = (((object)tuningGridVision != null) ? new DungeonGridVision(tuningGridVision.RevealOnEnter, tuningGridVision.Radius, tuningGridVision.ScoutBonus) : null);
		_hungerBuffer = (_tuning.DungeonLayer?.Hunger?.BufferAtStart).GetValueOrDefault();
		_currentRoomId = _map.StartId;
	}

	public void BindTraps(TrapDefs? defs, Func<string, int>? resistFor = null)
	{
		Traps = defs;
		TrapResistFor = resistFor;
	}

	private void ResolveLandingTrap(string? region)
	{
		if ((object)Traps == null || _tileWalker == null || string.IsNullOrWhiteSpace(region) || _tileWalker.CurrentTile != DungeonTileKind.Trap)
		{
			return;
		}
		TrapGate pendingTrapGate = _pendingTrapGate;
		_pendingTrapGate = TrapGate.Consumed;
		if (pendingTrapGate == TrapGate.Consumed)
		{
			return;
		}
		TrapDef trapDef = TrapResolver.Pick(_log, _rng, Traps, region);
		if ((object)trapDef != null)
		{
			string text = _session.ResolveTrapByResist(_log, Traps, trapDef, pendingTrapGate, TrapResistFor);
			LastTrap = (region, pendingTrapGate, text);
			if (text == "triggered")
			{
				TrapTriggeredCount++;
			}
			else if (text == "disarmed")
			{
				TrapDisarmedCount++;
			}
		}
	}

	public int RevealSecrets(IEnumerable<(int X, int Y)> within)
	{
		TuningSecretScatter tuningSecretScatter = _tuning.DungeonLayer?.Secrets;
		if ((object)tuningSecretScatter != null && _tileWalker != null)
		{
			DungeonGridDeriver.Derived tw = TileWalk;
			if ((object)tw != null)
			{
				if (within == null)
				{
					return 0;
				}
				int maxRewardsPerRun = tuningSecretScatter.MaxRewardsPerRun;
				if (maxRewardsPerRun > 0 && _claimedSecrets.Count >= maxRewardsPerRun)
				{
					_log.Append(new EffectEvent(null, $"secret_quota_reached:{maxRewardsPerRun}", 0.0, Triggered: false));
					return 0;
				}
				int num = 0;
				foreach (var item in from p in within
					where tw.Grid.TileAt(p.X, p.Y) == DungeonTileKind.Secret
					orderby p.Y, p.X
					select p)
				{
					if (_claimedSecrets.Add(item))
					{
						_tileWalker.RevealScouted(new(int, int)[1] { item });
						int rewardGold = tuningSecretScatter.RewardGold;
						Economy economy = _economy;
						if (economy != null)
						{
							rewardGold = economy.AwardContent(_log, tuningSecretScatter.RewardGold, "secret");
						}
						else
						{
							rewardGold = 0;
							_log.Append(new EffectEvent(null, "secret_no_economy_gold_uncredited", 0.0, Triggered: false));
						}
						SecretGoldGranted += rewardGold;
						LastSecret = (item, rewardGold);
						num++;
						_log.Append(new EffectEvent(null, $"secret_revealed:{item.X},{item.Y}:gold{rewardGold}", 100.0, Triggered: true));
						if (maxRewardsPerRun > 0 && _claimedSecrets.Count >= maxRewardsPerRun)
						{
							break;
						}
					}
				}
				return num;
			}
		}
		return 0;
	}

	public int RevealSecretsWithinRooms(IEnumerable<int> roomIds)
	{
		DungeonGridDeriver.Derived tileWalk = TileWalk;
		if ((object)tileWalk == null)
		{
			return 0;
		}
		HashSet<int> set = new HashSet<int>(roomIds ?? Array.Empty<int>());
		if (set.Count == 0)
		{
			return 0;
		}
		return RevealSecrets(from kv in tileWalk.TileRoom
			where set.Contains(kv.Value)
			select kv.Key);
	}

	public RevealState TileStateAt((int X, int Y) pos)
	{
		return _tileWalker?.StateAt(pos, TileVision) ?? RevealState.Unexplored;
	}

	public bool VisitedTileAt((int X, int Y) pos)
	{
		return _tileWalker?.HasVisited(pos) ?? false;
	}

	public void RevealScoutedTiles(IEnumerable<(int X, int Y)> tiles)
	{
		if (_tileWalker != null && TileWalkEnabled)
		{
			_tileWalker.RevealScouted(tiles);
		}
	}

	public void RevealScoutedRooms(IEnumerable<int> roomIds)
	{
		if (_tileWalker == null)
		{
			return;
		}
		DungeonGridDeriver.Derived tileWalk = TileWalk;
		if ((object)tileWalk == null)
		{
			return;
		}
		HashSet<int> set = new HashSet<int>(roomIds ?? Array.Empty<int>());
		if (set.Count != 0)
		{
			RevealScoutedTiles(from kv in tileWalk.TileRoom
				where set.Contains(kv.Value)
				select kv.Key);
		}
	}

	public bool TryStepTile(int dx, int dy)
	{
		if (_tileWalker == null || _tileRoom == null || _tileSegAt == null)
		{
			return false;
		}
		(int, int) pos = (_tileWalker.Position.X + dx, _tileWalker.Position.Y + dy);
		_pendingTrapGate = TrapResolver.GateFor(TileStateAt(pos));
		int value = _meter.Value;
		if (!_tileWalker.TryStep(dx, dy, out var wasRevisit))
		{
			_pendingTrapGate = TrapGate.Consumed;
			return false;
		}
		(int, int) position = _tileWalker.Position;
		bool segmentBilled = false;
		int value3;
		if (_tileSegAt.TryGetValue(position, out (int, int, int) value2))
		{
			if (value2.Item1 != _tileSegmentId)
			{
				_tileSegmentId = value2.Item1;
				_tileAcc = _tileSegmentCost;
			}
			int remainingTilesInSegment = value2.Item3 - value2.Item2;
			(int Deduct, int NewAcc) tuple = WalkLightCost.StepCost(_tileAcc, remainingTilesInSegment);
			int item = tuple.Deduct;
			int item2 = tuple.NewAcc;
			_tileAcc = item2;
			if (item > 0)
			{
				_meter.TryAdvanceBy(_log, -item, "walk");
				segmentBilled = true;
			}
		}
		else if (_tileRoom.TryGetValue(position, out value3))
		{
			_currentRoomId = value3;
		}
		if (wasRevisit)
		{
			int? backtrackCost = _backtrackCost;
			if (backtrackCost.HasValue)
			{
				int valueOrDefault = backtrackCost.GetValueOrDefault();
				if (true)
				{
					int num = BacktrackExtraFor(valueOrDefault, position, segmentBilled);
					if (num > 0)
					{
						_meter.TryAdvanceBy(_log, -num, "backtrack");
						TileBacktrackCount++;
					}
				}
			}
		}
		if (wasRevisit)
		{
			LastRevisitThreat = RevisitSpawner.Roll(_log, _rng, _tuning.DungeonLayer?.Revisit, _meter.Value);
			if (LastRevisitThreat.Triggered)
			{
				RevisitThreatCount++;
			}
		}
		if (!wasRevisit && _tileSegAt.TryGetValue(position, out (int, int, int) _))
		{
			HungerConfig hungerConfig = _tuning.DungeonLayer?.Hunger;
			if ((object)hungerConfig != null)
			{
				if (_hungerBuffer > 0)
				{
					_hungerBuffer--;
					LastHunger = HungerRollResult.None;
				}
				else
				{
					LastHunger = HungerSpawner.Roll(_log, _rng, hungerConfig, _meter.Value);
					_hungerBuffer = hungerConfig.BufferAfterTrigger;
					if (LastHunger.Triggered)
					{
						HungerCount++;
						if (!_session.HungerCanApply)
						{
							_log.Append(new EffectEvent(null, "hunger_no_roster_pending", 100.0, Triggered: true));
						}
					}
				}
			}
		}
		ResolveLandingTrap(_currentRegion);
		return true;
	}

	public void SetRegion(string? region)
	{
		_currentRegion = region;
	}

	public string ResolveHunger(bool eat)
	{
		HungerConfig config = _tuning.DungeonLayer?.Hunger ?? throw new InvalidOperationException("未配置 `tuning.dungeon_layer.hunger` ⇒ 不存在可结算的饥饿事件 ✓");
		if (!_session.HungerCanApply)
		{
			throw new InvalidOperationException("名册台账未建立（首场战斗之前）⇒ 饥饿**无人可结算**；请先判 `HasPendingHunger`（它会返回 false）✓");
		}
		string result = _session.ResolveHunger(_log, config, eat, _tuning.DungeonLayer?.Exploration);
		LastHunger = HungerRollResult.None;
		return result;
	}

	private int BacktrackExtraFor(int revisitCost, (int X, int Y) pos, bool segmentBilled)
	{
		if (!segmentBilled)
		{
			return revisitCost;
		}
		int item = _tileSegAt[pos].Length;
		int num = ((item > 0) ? (_tileSegmentCost / item) : 0);
		return Math.Max(0, revisitCost - num);
	}

	public int? ForecastLightTo(int goalRoomId)
	{
		if (_tileWalker != null)
		{
			DungeonGridDeriver.Derived tileWalk = TileWalk;
			if ((object)tileWalk != null && _tileRoom != null && (object)_map != null)
			{
				if (goalRoomId == _currentRoomId)
				{
					return 0;
				}
				(int, int) position = _tileWalker.Position;
				(int, int)? tuple = null;
				int num = int.MaxValue;
				foreach (KeyValuePair<(int, int), int> item in tileWalk.TileRoom)
				{
					if (item.Value == goalRoomId)
					{
						int num2 = Math.Abs(item.Key.Item1 - position.Item1) + Math.Abs(item.Key.Item2 - position.Item2);
						if (num2 < num)
						{
							num = num2;
							tuple = item.Key;
						}
					}
				}
				if (tuple.HasValue)
				{
					(int, int) valueOrDefault = tuple.GetValueOrDefault();
					if (true)
					{
						DungeonWalker dungeonWalker = new DungeonWalker(tileWalk.Grid, position);
						IReadOnlyList<(int, int)> readOnlyList = dungeonWalker.PathTo(valueOrDefault.Item1, valueOrDefault.Item2);
						if (readOnlyList.Count == 0)
						{
							return null;
						}
						try
						{
							IReadOnlyList<int> source = DungeonWalkLight.PlanPath(tileWalk.Grid, _tileRoom, position, readOnlyList, _tileSegmentCost);
							return source.Sum();
						}
						catch (ArgumentException)
						{
							return null;
						}
					}
				}
				return null;
			}
		}
		return null;
	}

	public ExpeditionFlow(ExpeditionSession session, LightMeter meter, Inventory bag, Scouting scout, ExpeditionNodesConfig nodes, TuningConfig tuning, CombatLog log, IRngProvider rng, Economy? economy = null, HeirloomStock? heirlooms = null, HeirloomConfig? heirloomConfig = null)
	{
		_economy = economy;
		_heirlooms = heirlooms;
		_heirloomConfig = heirloomConfig;
		_session = session ?? throw new ArgumentNullException("session");
		_meter = meter ?? throw new ArgumentNullException("meter");
		_bag = bag ?? throw new ArgumentNullException("bag");
		_scout = scout ?? throw new ArgumentNullException("scout");
		_nodes = nodes ?? throw new ArgumentNullException("nodes");
		_tuning = tuning ?? throw new ArgumentNullException("tuning");
		_log = log ?? throw new ArgumentNullException("log");
		_rng = rng ?? throw new ArgumentNullException("rng");
		_meter.EmitStart(_log);
	}

	public IReadOnlyList<PathOption> PreviewOptions()
	{
		if (_path == null)
		{
			_path = ExpeditionPathPlanner.GeneratePath(_log, _rng, _tuning.Expedition.NBattles, _nodes);
		}
		IReadOnlyList<PathOption> result;
		if (StepsDone >= _path.Count)
		{
			IReadOnlyList<PathOption> readOnlyList = Array.Empty<PathOption>();
			result = readOnlyList;
		}
		else
		{
			result = _path[StepsDone].Options;
		}
		return result;
	}

	public FlowStep Advance(int optionIndex)
	{
		if (IsFinished)
		{
			return new FlowStep(StepsDone, FlowStepKind.Done, string.Empty, Array.Empty<PathOption>());
		}
		if (_path == null)
		{
			_path = ExpeditionPathPlanner.GeneratePath(_log, _rng, _tuning.Expedition.NBattles, _nodes);
		}
		if (StepsDone >= _path.Count)
		{
			IsFinished = true;
			Current = new FlowStep(StepsDone, FlowStepKind.Done, string.Empty, Array.Empty<PathOption>());
			return Current;
		}
		PathStep pathStep = _path[StepsDone];
		PathOption pathOption = ExpeditionPathPlanner.ChoosePath(_log, pathStep, optionIndex);
		LastScout = _scout.Roll(_log, _rng, _meter.Value, pathOption.NodeType);
		_meter.TryAdvanceNode(_log);
		Current = new FlowStep(StepsDone, (!(pathOption.NodeType == "battle")) ? FlowStepKind.Event : FlowStepKind.Battle, pathOption.NodeId, pathStep.Options);
		return Current;
	}

	public void ResolveEvent(int optionIndex)
	{
		FlowStep current = Current;
		if ((object)current == null || current.Kind != FlowStepKind.Event)
		{
			throw new InvalidOperationException("当前步骤不是事件节点（流程层不应调用）。");
		}
		_session.ResolveEventNode(_log, _nodes.Get(current.NodeId), optionIndex);
		StepsDone++;
	}

	public void OnBattleFinished(string result, int rounds, bool isAmbush = false)
	{
		_session.EnterPhase(FlowPhase.Walking);
		if (!isAmbush && !IsTopologyMode)
		{
			FlowStep current = Current;
			if ((object)current == null || current.Kind != FlowStepKind.Battle)
			{
				throw new InvalidOperationException("当前步骤不是战斗节点（流程层不应调用）。");
			}
		}
		if (result != "PlayerVictory")
		{
			if (result == "DrawRetreat")
			{
				if (_currentRoomId >= 0)
				{
					_resolvedRooms.Add(_currentRoomId);
				}
				if (!IsTopologyMode)
				{
					StepsDone++;
				}
			}
			else
			{
				Outcome = ExpeditionOutcome.Wiped;
				IsFinished = true;
				StepsDone++;
			}
			return;
		}
		TuningLootSpec tuningLootSpec = _tuning.Light.Loot[LightMeter.TierId(_meter.Tier)];
		for (int i = 0; i < tuningLootSpec.Firewood; i++)
		{
			TryCollectLoot(ItemKind.Firewood);
		}
		for (int j = 0; j < tuningLootSpec.Food; j++)
		{
			TryCollectLoot(ItemKind.Food);
		}
		LootFirewood += tuningLootSpec.Firewood;
		_economy?.AwardBattle(_log, LightMeter.TierId(_meter.Tier));
		_heirlooms?.AwardForTier(_log, LightMeter.TierId(_meter.Tier));
		Wins++;
		StepsDone++;
		_session.ConsumeRunBuffsAfterBattle();
		_session.StripCampBonusesFromRetained();
	}

	public BattleDirector BeginAmbushBattle(CombatLog log)
	{
		if (!LastCampAmbushed)
		{
			throw new InvalidOperationException("未触发夜袭：先扎营（Camp）且 LastCampAmbushed 为真。");
		}
		AmbushBattleStarted = true;
		int battleIndex = _session.BattlesPlayed + 1;
		return _session.BeginExpeditionBattle(battleIndex, log, _tuning.Expedition.DifficultyTiers, _meter.Effect);
	}

	public string? EventNodeIdForRoom(int roomId)
	{
		ExpeditionNodeConfig[] array = _nodes.Nodes.Where((ExpeditionNodeConfig n) => n.Type == "event").ToArray();
		return (array.Length == 0) ? null : array[roomId % array.Length].Id;
	}

	public void CaptureBattleEndHp(BattleDirector director)
	{
		_session.CaptureBattleEndHp(director, _log);
	}

	public int ReturnToTown(string outcome)
	{
		IsFinished = true;
		if (1 == 0)
		{
		}
		ExpeditionOutcome outcome2 = ((outcome == "completed") ? ExpeditionOutcome.Completed : ((!(outcome == "abandoned")) ? Outcome : ExpeditionOutcome.Abandoned));
		if (1 == 0)
		{
		}
		Outcome = outcome2;
		if (!_runCounted)
		{
			_runCounted = true;
			Progress?.FinishRun(_log, outcome, Wins);
		}
		return _session.ReturnToTown(_log, outcome);
	}













}
