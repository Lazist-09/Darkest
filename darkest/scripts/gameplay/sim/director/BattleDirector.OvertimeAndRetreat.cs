// 🔴 从 BattleDirector.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）：本文件 = **回合推进与撤退**
//    （敌意图/回合事件/流血/敌行动/整轮/敌方阶段/撤退/投影）—— 只搬家、零行为改动（partial 过渡）
//    弹性窗口/超时增援/波次间隔见 **`BattleDirector.Reinforcement.cs`**（M11 ① 预警面第十九件）✓
using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Buffs;
using Darkest.Gameplay.Sim.Enemy;
using Darkest.Gameplay.Sim.Pipeline;
using Darkest.Gameplay.Sim.Skill;
using Darkest.Gameplay.Sim.Survival;
using Darkest.Gameplay.Sim.Turn;

namespace Darkest.Gameplay.Sim.Director;

public sealed partial class BattleDirector
{
/// <summary>
    /// G4（O-57）：敌方意图预览（供后续「侦察」技能调用）。用**调用方提供的独立 RNG/日志**跑同源决策，
    /// 绝不消耗战斗随机数、不写战斗事件流（确定性红线：#201）。
    /// </summary>
    public SkillChoice? PreviewEnemyIntent(UnitId actor, IRngProvider scratchRng, CombatLog scratchLog)
    {
        UnitRuntime? unit = _enemy.UnitsInSlotOrder().FirstOrDefault(u => u.Id == actor);
        return unit is null ? null : _ai.Choose(unit, _enemy, _player, _buffs, scratchRng, scratchLog);
    }

    /// <summary>G0/O-55：回合开始事件（谁行动、在哪号位、有效速度）。</summary>
    private void EmitTurnStart(UnitId actor)
    {
        UnitRuntime? u = _player.UnitsInSlotOrder().FirstOrDefault(x => x.Id == actor)
                         ?? _enemy.UnitsInSlotOrder().FirstOrDefault(x => x.Id == actor);
        if (u is null)
        {
            return;
        }

        int slot = _player.UnitAtPosition(actor) ?? _enemy.UnitAtPosition(actor) ?? 0;
        TickBleedAtTurnStart(u);
        _log.Append(new TurnStartEvent(actor, slot, u.EffectiveSpeed(_balance.WeakSpeedMult)));
    }

    /// <summary>
    /// D3（#205）流血四规则：**目标回合开始结算**、**不受物防减免**（固定值）、**每回合不暴击**、
    /// 致死走既有死亡/死门链（敌人无死门 → 直接死）。伤害 = tuning.bleed.per_round_damage。
    /// </summary>
    private void TickBleedAtTurnStart(UnitRuntime u)
    {
        if (u.BleedRoundsRemaining <= 0 || u.CurrentHp <= 0)
        {
            return;
        }

        int dmg = _balance.BleedPerRound;
        u.CurrentHp -= dmg;
        u.BleedRoundsRemaining--;
        _log.Append(new DamageEvent(u.Id, dmg, dmg, Crit: false, SegmentIndex: 0, Axis: "bleed",
            Attacker: null, SkillId: "bleed"));

        if (u.CurrentHp > 0)
        {
            return;
        }

        if (u.IsPlayer && _lastRng is { } rng)
        {
            WeakDeathsDoor.EnterWeak(u, _pipeline.Morale, rng, _log, _balance);
        }
        else if (!u.IsPlayer)
        {
            _enemy.RemoveUnitAt(_enemy.UnitAtPosition(u.Id) ?? -1);
            _log.Append(new DeathEvent(u.Id, IsPlayer: false, Cause: "bleed"));
        }
    }

    /// <summary>敌方单位行动一次（actor 节拍内调用；AI 同源）。
    /// 目标选择按 O-39 架构裁定：候选池槽序【首个非空】+ taunt 优先（EnemyAi 已重排），不含 RNG。</summary>
    public void EnemyAct(UnitId actor, IRngProvider rng)
    {
        UnitRuntime? u = _enemy.UnitsInSlotOrder().FirstOrDefault(x => x.Id == actor);
        if (u is null)
        {
            return;
        }

        SkillChoice? choice = _ai.Choose(u, _enemy, _player, _buffs, rng, _log);
        if (choice is not null)
        {
            // P1：单体 → 传规则所选单槽；AOE → 传 null（执行器按全池命中，不受选一影响）
            SkillTemplateConfig skill = _skills.Get(choice.SkillId);
            bool aoe = skill.Tags.Contains(FuncTag.Aoe);
            int[]? chosen = aoe || choice.TargetSlots.Count == 0 ? null : new[] { choice.TargetSlots[0] };
            _executor.Execute(skill, actor, _player, _enemy, rng, chosen);
        }
    }

    /// <summary>
    /// 完整一回合（策划拍板 M6 前置）：StartTurn 后按行动序列逐个 NextActor——
    /// 我方单位由 choosePlayerSkill 回调用例决策，敌方经 EnemyAi；眩晕单位被 NextActor 跳过
    /// （清除标记），减速经行动序列排序生效。队列耗尽或胜负分定即回合结束。
    /// enemy_actions_per_round（默认 1）为「策划保留否决」探针键：&gt;1 时走旧 EnemyPhase 路径。
    /// </summary>
    public void RunFullRound(IRngProvider rng, System.Func<UnitRuntime, PlayerDecision> decide)
    {
        if (IsBattleOver)
        {
            EmitBattleEnd(); // G0/O-55：已分出胜负时补记结束事件（幂等）
            return; // P0/O-47：胜负已定 → 不开启回合、不产生任何战斗事件
        }

        StartTurn(rng);

        // 🔴 2026-09-20：**`enemy_actions_per_round > 1` 的分支此前从未生效**（真缺陷）——
        //    本方法文档写着"&gt;1 时走旧 EnemyPhase 路径"，但代码里**没有任何一处**读这个键 ⇒
        //    `EnemyPhase` 成了死函数，而"每回合敌方多动"这个策划保留开关**形同虚设** ⚠️
        //    ⇒ 这里补上：>1 ⇒ 走 `EnemyPhase`（bulk 敌方阶段），我方仍按行动序列逐个决策 ✓
        //    ⚠️ 纪律：**只是把已声明的分支接上**，不改默认行为（`enemy_actions_per_round` 现值 = 1 ⇒ 逐字不变）✓
        if (_balance.Tuning.EnemyActionsPerRound > 1)
        {
            RunBulkEnemyRound(rng, decide);
            return;
        }

        while (!IsBattleOver)
        {
            UnitId? actor = _sequencer.NextActor();
            if (actor is null)
            {
                break;
            }

            EmitTurnStart(actor.Value);

            UnitRuntime? playerUnit = _player.UnitsInSlotOrder().FirstOrDefault(x => x.Id == actor);
            if (playerUnit is not null)
            {
                PlayerDecision decision = decide(playerUnit);
                if (decision.ReinforceB is { } bSlot && decision.ReinforceX is { } xSlot)
                {
                    if (!Reinforce(actor.Value, bSlot, xSlot))
                    {
                        // #211（S0）：SP 不足 → 被拒不吞行动（本回合视为待命；实机保持该单位行动）
                        PassTurn(actor.Value);
                    }
                }
                else if (decision.SkillId is not null)
                {
                    if (TryFearRefusal(actor.Value, rng))
                    {
                        continue; // 恐惧拒放：不消耗行动（本回合跳过；实机为"技能灰掉须重选"）
                    }

                    int[]? chosen = decision.SkillTargetSlot is { } ts ? new[] { ts } : null;
                    if (!PlayerUseSkill(actor.Value, decision.SkillId, rng, chosen))
                    {
                        PassTurn(actor.Value); // 支援点不足 → 不消耗行动（S5.2 待命出口）
                    }
                }
                else
                {
                    PassTurn(actor.Value); // S5.2：显式待命（不消耗 SP、不算技能）
                }
            }
            else if (_enemy.UnitsInSlotOrder().Any(x => x.Id == actor))
            {
                EnemyAct(actor.Value, rng);
            }
        }

        if (IsBattleOver)
        {
            EmitBattleEnd(); // G0/O-55：整回合跑完即结算胜负入日志
        }
    }

    /// <summary>
    /// 🔴 **`enemy_actions_per_round > 1` 的 bulk 回合**（2026-09-20 接线）：
    /// 我方按行动序列逐个决策（与 `RunFullRound` 同一条路），敌方**整队跑完 `EnemyPhase`**
    /// （每个敌人各行动 `enemy_actions_per_round` 次）⇒ 这就是本方法文档里承诺的"旧 `EnemyPhase` 路径" ✓
    /// ⚠️ 只被 `EnemyActionsPerRound > 1` 触发；现值 = 1 ⇒ **默认行为逐字不变** ✓
    /// </summary>
    private void RunBulkEnemyRound(IRngProvider rng, System.Func<UnitRuntime, PlayerDecision> decide)
    {
        while (!IsBattleOver)
        {
            UnitId? actor = _sequencer.NextActor();
            if (actor is null)
            {
                break;
            }

            EmitTurnStart(actor.Value);

            UnitRuntime? playerUnit = _player.UnitsInSlotOrder().FirstOrDefault(x => x.Id == actor);
            if (playerUnit is not null)
            {
                PlayerDecision decision = decide(playerUnit);
                if (decision.ReinforceB is { } bSlot && decision.ReinforceX is { } xSlot)
                {
                    if (!Reinforce(actor.Value, bSlot, xSlot))
                    {
                        PassTurn(actor.Value);
                    }
                }
                else if (decision.SkillId is not null)
                {
                    if (TryFearRefusal(actor.Value, rng))
                    {
                        continue;
                    }

                    int[]? chosen = decision.SkillTargetSlot is { } ts ? new[] { ts } : null;
                    if (!PlayerUseSkill(actor.Value, decision.SkillId, rng, chosen))
                    {
                        PassTurn(actor.Value);
                    }
                }
                else
                {
                    PassTurn(actor.Value);
                }
            }
            else if (_enemy.UnitsInSlotOrder().Any(x => x.Id == actor))
            {
                // 🔴 bulk 口径：**一个敌方节拍 = 整队各动 `EnemyActionsPerRound` 次**（`EnemyPhase` 语义）✓
                EnemyPhase(rng);
            }
        }

        if (IsBattleOver)
        {
            EmitBattleEnd();
        }
    }

    /// <summary>敌方阶段（探针/旧入口保留：bulk 模式，供 enemy_actions_per_round&gt;1 调试与既有测试）。</summary>
    public void EnemyPhase(IRngProvider rng)
    {
        int actions = Math.Max(1, _balance.Tuning.EnemyActionsPerRound);
        for (int k = 0; k < actions; k++)
        {
            foreach (UnitRuntime enemyUnit in _enemy.UnitsInSlotOrder().ToArray())
            {
                SkillChoice? choice = _ai.Choose(enemyUnit, _enemy, _player, _buffs, rng, _log);
                if (choice is not null)
                {
                    _executor.Execute(_skills.Get(choice.SkillId), enemyUnit.Id, _player, _enemy, rng);
                }
            }
        }
    }

    /// <summary>撤退成功率数字（展示口径，T-M5-04 要点 3/5）：基础率 = f(存活平均速度)，无扰动、无抽取（M-C）。</summary>
    public double CurrentRetreatRate()
    {
        TuningRetreatFormula f = _balance.RetreatFormula;
        return Darkest.Core.Math.BattleMath.RetreatBaseRate(AvgSpeed(_player) - AvgSpeed(_enemy),
            f.BasePercent, f.PerSpeedDiffPercent, f.ClampMin, f.ClampMax);
    }

    /// <summary>撤退（T-M5-04/O-11）：成功率数字 = 基础率 + ±10% 扰动再钳制；失败当回合不可再试（#118）。</summary>
    public bool PlayerRetreat(IRngProvider rng)
    {
        if (_retreatDisabledThisRound)
        {
            return false; // 本回合已试过（指令被拒语义由导演状态机承载）
        }

        _retreatDisabledThisRound = true;

        TuningRetreatFormula f = _balance.RetreatFormula;
        double baseRate = CurrentRetreatRate();
        double perturbRoll = rng.NextPercent() / 100.0;
        _log.Append(new RngDraw(rng.DrawCount, perturbRoll * 100.0));
        double rate = Darkest.Core.Math.BattleMath.RetreatFinalRate(baseRate, perturbRoll,
            f.RandomRange, f.RandClampMin, f.RandClampMax);

        double verdictRoll = rng.NextPercent();
        _log.Append(new RngDraw(rng.DrawCount, verdictRoll));
        bool success = verdictRoll < rate;

        _log.Append(new RetreatEvent(success, rate));
        int moraleBefore = _player.UnitsInSlotOrder().Sum(u => u.Morale);
        _pipeline.Morale.ApplyTeamOnce(_player.UnitsInSlotOrder(),
            success ? "retreat_success" : "retreat_fail", _log); // 🔴 现值：成功 **−12** ／ 失败 −5（各全队）—— ⚠️ 原注释写"−10 / −5"是**过期值**（`#357`② 同族：名字/注释与实现不符 ⇒ 已按 tuning 改 ✓）
        // 🔴 `#376`②：**场级结算事件**（架构 `data_schema §3.11` v1.68 登记）
        int moraleAfter = _player.UnitsInSlotOrder().Sum(u => u.Morale);
        bool playerDown = _player.UnitsInSlotOrder().Any(u => u.CurrentHp <= 0); // "有无阵亡"口径：本场有我方倒下（含死门）✓
        _log.Append(new RetreatResolved(success, BattleIndex, moraleAfter - moraleBefore, playerDown));

        return success;
    }

    /// <summary>🔴 本趟第几场（1 起）—— 由**流程**在起战斗时注入 ⇒ 导演仍是单场纯 ✓（`#376`② 的 `battleIndex` 字段）</summary>
    public int BattleIndex { get; set; } = 1;

    /// <summary>撤退可点状态投影（UI 用）：失败当回合 disabled。</summary>
    public bool CanRetreatThisRound => !_retreatDisabledThisRound && !IsBattleOver;

    /// <summary>战斗结果投影（O-47）：我方优先判负（硬核受苦定位："活下来才算赢"；同瞬间双灭 → 判负）。</summary>
    public BattleOutcome Outcome
    {
        get
        {
            if (_player.OccupiedPositions(false).Count == 0)
            {
                return BattleOutcome.Defeat; // ★ 我方优先判负
            }

            return _enemy.OccupiedPositions(false).Count == 0 ? BattleOutcome.Victory : BattleOutcome.Ongoing;
        }
    }

    /// <summary>胜负已定（= Outcome != Ongoing 的别名；既有调用语义不变）。</summary>
    public bool IsBattleOver => Outcome != BattleOutcome.Ongoing;

    private double AvgSpeed(FormationBoard board)
    {
        UnitRuntime[] alive = board.UnitsInSlotOrder().ToArray();
        if (alive.Length == 0)
        {
            return 0.0;
        }

        return alive.Average(u => u.EffectiveSpeed(_balance.WeakSpeedMult));
    }

    
}
//    【依赖主类私有状态/方法】(partial 使封装在文件级失效 => 必须声明)：_ai x3 · _balance x10 · _buffs x3 · _elasticBonus x3 · _enemy x19 · _enemyRoster x1 · _executor x2 · _lastRng x1 · _lastWaveRound x4 · _log x14 · _outputUsersThisRound x1 · _pipeline x2 · _player x16 · _recentNotFull x5 · _reinforcementCount x2 · _reinforcePool x2 · _retreatDisabledThisRound x3 · _round x10 · _sequencer x1 · _skills x2 · _units x2
