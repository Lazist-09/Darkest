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



	public (int X, int Y) TilePosition => _tileWalker?.Position ?? (-1, -1);







	public int TrapTriggeredCount { get; private set; }

	public int TrapDisarmedCount { get; private set; }

	public (string Region, TrapGate Gate, string Outcome)? LastTrap { get; private set; }


	public int SecretGoldGranted { get; private set; }

	public ((int X, int Y) Pos, int Gold)? LastSecret { get; private set; }

	public DungeonGridVision? TileVision { get; private set; }




	public string? CurrentRegion => _currentRegion;

	public HungerRollResult LastHunger { get; private set; } = HungerRollResult.None;

	public int HungerCount { get; private set; }





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
}
