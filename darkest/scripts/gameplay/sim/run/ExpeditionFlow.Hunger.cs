// 🔴 从 ExpeditionFlow.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3 的 8 件计划）
//    本文件 = Hunger · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_hungerBuffer, _log, _session, _tuning

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
}
