// 🔴 从 ExpeditionFlow.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3 的 8 件计划）
//    本文件 = Topology · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_currentRoomId, _log, _map, _mapCfg, _meter, _nodes, _path, _resolvedRooms, _rng, _tuning, _visitedRooms

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
	public bool IsTopologyMode => (object)_map != null;

	public ExpeditionMap? Map => _map;

	public IReadOnlyList<int> RevealedRoomIds => (_visitedRooms == null) ? Array.Empty<int>() : _visitedRooms.ToArray();

	public int CurrentRoomId => _currentRoomId;

	public bool IsRoomResolved(int roomId)
	{
		return _resolvedRooms.Contains(roomId);
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
}
