// 🔴 从 ExpeditionFlow.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3 的 8 件计划）
//    本文件 = Traps · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_currentRegion, _log, _pendingTrapGate, _rng, _session, _tileWalker

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
	public TrapDefs? Traps { get; private set; }

	public Func<string, int>? TrapResistFor { get; private set; }

	public bool TrapResistSourceDeclared => TrapResistFor != null;

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

	public void SetRegion(string? region)
	{
		_currentRegion = region;
	}
}
