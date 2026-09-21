// 🔴 从 ExpeditionFlow.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3 的 8 件计划）
//    本文件 = TileWalk · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_backtrackCost, _currentRegion, _currentRoomId, _hungerBuffer, _log, _map, _meter, _pendingTrapGate, _rng, _session, _tileAcc, _tileRoom, _tileSegAt, _tileSegmentCost, _tileSegmentId, _tileWalker, _tuning

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;   // 🆕 路线(c)：恢复件的类体用到它（HEAD 原先没有）✓
using Darkest.Gameplay.Sim.Survival;   // 🆕 事故回填：LightMeter 现居此命名空间（别人把它从 sim/run 搬来）✓

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionFlow
{
	public bool TileWalkEnabled => _tileWalker != null;

	public DungeonTileKind TileHere => _tileWalker?.CurrentTile ?? DungeonTileKind.Wall;

	public int TileStepsTaken => _tileWalker?.StepsTaken ?? 0;

	public DungeonGridDeriver.Derived? TileWalk { get; private set; }

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
}
