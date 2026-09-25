// 🔴 从 ExpeditionFlow.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3 的 8 件计划）
//    本文件 = BattleReturn · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_currentRoomId, _economy, _heirlooms, _log, _map, _meter, _nodes, _resolvedRooms, _session, _tuning

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
	public bool ReachedGoal => (object)_map != null && _currentRoomId == EffectiveGoalRoomId;

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

		// 🆕 **步骤 ②（接线）**：传家宝改走【任务奖励】通道（`#470` 顺序 (A)：**先接线，后删旧源**）✓
		//    · **难度档** ← `_partyAverageLevel`（⚠️ 代理量，一手要的是队伍 resolve level ⇒ placeholder · O11）
		//    · **长度** ← **这趟【已走过】的段数** ⇒ 本场打完才算走过 ⇒ `StepsDone + 1`
		//      （`StepsDone` 的语义实测 = **已完成段数**：`Advance` 里用 `_path[StepsDone]` 取下一段 ✓
		//        而第 65 行的 `StepsDone++` 在**发放之后** ⇒ 此处手动 +1，口径写死在一处 ✓）
		//    · 第 4 个实参只有 `_reward` **缺失时**才被用（P4 前的回落桥 ⇒ 与 `AwardForTier` 同款）✓
		_heirlooms?.AwardForRun(_log, StepsDone + 1, _partyAverageLevel, LightMeter.TierId(_meter.Tier));
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
}
