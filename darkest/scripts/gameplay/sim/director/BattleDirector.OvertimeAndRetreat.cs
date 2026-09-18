// 🔴 从 BattleDirector.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）：本文件 = **回合推进与撤退**（敌意图/回合事件/流血/敌行动/整轮/敌方阶段/撤退/弹性窗口/增援）—— 只搬家、零行为改动（partial 过渡）
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

    /// <summary>
    /// #198 弹性增援（橡胶筋）：窗口 K 回合内每回合都"未全力进攻"（存活战斗位中未使用 output 技能者 ≥ idle_output_slots）
    /// → M += 1（上限 M_base + max_bonus）；任一回合达标 → M 回落 M_base。每次变动写 ReinforcementElasticEvent。
    /// </summary>
    private void EvaluateElasticWindow()
    {
        TuningElasticSpec? e = _balance.Tuning.OvertimeReinforcement.Elastic;
        if (e is null || !e.Enabled)
        {
            return;
        }

        int aliveCombat = 0;
        int idle = 0;
        for (int slot = 1; slot <= _player.Layout.CombatSlots; slot++)
        {
            UnitRuntime? u = _player.UnitRuntimeAt(slot);
            if (u is null)
            {
                continue;
            }

            aliveCombat++;
            if (!_outputUsersThisRound.Contains(u.Id))
            {
                idle++;
            }
        }

        bool notFull = aliveCombat > 0 && idle >= Math.Max(1, e.IdleOutputSlots);
        _recentNotFull.Enqueue(notFull);
        int window = Math.Max(1, e.KRounds);
        while (_recentNotFull.Count > window)
        {
            _recentNotFull.Dequeue();
        }

        int before = CurrentM();
        bool windowSatisfied = _recentNotFull.Count >= window && _recentNotFull.All(x => x);
        _elasticBonus = windowSatisfied
            ? Math.Min(Math.Max(0, e.MaxBonus), _elasticBonus + 1)
            : 0;

        int after = CurrentM();
        if (after != before)
        {
            _log.Append(new ReinforcementElasticEvent(before, after, windowSatisfied ? "not_full_attack" : "reset"));
        }
    }

    /// <summary>当前波次间隔 M = M_base（#196 导出量）+ 弹性浮动（#198）。</summary>
    private int CurrentM() => DeriveWaveInterval(_balance.Tuning.OvertimeReinforcement) + _elasticBonus;

    /// <summary>T-M5-03 超时增援：第 trigger_round 起每回合开始判定；有空位填（不超 4 位上限），满编上增益（O-20 占位数值）。</summary>
    private void ApplyReinforcement(IRngProvider rng)
    {
        _ = rng; // 切片增援不走随机分布（O-20 裁定）
        TuningOvertimeReinforcement o = _balance.Tuning.OvertimeReinforcement;
        if (_round < o.TriggerRound || IsBattleOver)
        {
            return;
        }

        // #196：M 不是常数，改为**导出量** M = ceil(敌方满编总HP ÷ (D × 0.8))，护栏 M ≥ 3；
        // D = 我方每回合对敌方造成的总伤害（事件流实测）。无输出（D≈0）时回落到 tuning 的 M 下限。
        int interval = CurrentM();
        if (_lastWaveRound == 0)
        {
            if (_round != o.TriggerRound)
            {
                return; // 首波严格在 trigger_round
            }
        }
        else if (_round - _lastWaveRound < interval)
        {
            return; // 未到下一波
        }

        int[] empties = Enumerable.Range(1, _enemy.SlotCount)
            .Where(s => _enemy.GetSlot(s) == SlotState.Empty)
            .ToArray();
        if (empties.Length == 0)
        {
            // 满编分支：给在场全体敌人 +攻/+速（O-20 占位数值；每波叠加一次）
            BuffAllEnemies(_enemy);
            _lastWaveRound = _round;
            return;
        }

        // 有空位分支：**一次性补齐当时全部空位**（按原型轮换，默认起手值；不超 4 位上限）
        foreach (int slot in empties)
        {
            string archetype = _reinforcePool[_reinforcementCount % _reinforcePool.Length];
            _reinforcementCount++;
            var unit = new UnitRuntime(UnitId.Of(archetype), FormationSide.Enemy,
                UnitStatsMapper.From(_units.Get(archetype)));
            _enemy.PlaceUnitAt(slot, unit);
            _log.Append(new ReinforcementEvent("Fill", unit.Id, slot));
        }

        _lastWaveRound = _round;
    }

    /// <summary>
    /// #196：增援波次间隔 M = ceil(敌方满编总HP ÷ (D × 0.8))，护栏 ≥ tuning.wave_interval_rounds（默认 3，即 M 下限）。
    /// D = 我方每回合对敌方造成的总伤害（事件流 DamageEvent 实测均值）；D≈0（无输出）时回落 M 下限。
    /// </summary>
    private int DeriveWaveInterval(TuningOvertimeReinforcement o)
    {
        // P16 门禁：m_value 未回填时以 tuning 下限为占位（复测出实测 D 后回填，见 tasks/m6_fix_pack P3 v3）
        int minM = Math.Max(3, o.MValue ?? o.WaveIntervalRounds);
        int rounds = Math.Max(1, _round - 1);
        int dealt = _log.Events.OfType<DamageEvent>()
            .Where(e => e.Attacker is { } a && IsPlayerId(a))
            .Sum(e => e.Amount);
        double d = (double)dealt / rounds;
        if (d <= 0.01)
        {
            return minM;
        }

        int fullHp = 0;
        foreach (string archetype in _enemyRoster)
        {
            fullHp += _units.Get(archetype).Hp;
        }

        if (fullHp <= 0)
        {
            return minM;
        }

        // 🔴 数字外置：`o.SafetyFactor` 已由 `TuningConfig` 校验为 > 0 ⇒ **不再回落 0.8**
        //    （旧写法 `o.SafetyFactor > 0 ? o.SafetyFactor : 0.8` 是"静默默认"，违反纪律且掩盖数据缺键 ⚠️）
        double safety = o.SafetyFactor;
        return Math.Max(minM, (int)Math.Ceiling(fullHp / (d * safety)));
    }

    
}
//    【依赖主类私有状态/方法】(partial 使封装在文件级失效 => 必须声明)：_ai x3 · _balance x10 · _buffs x3 · _elasticBonus x3 · _enemy x19 · _enemyRoster x1 · _executor x2 · _lastRng x1 · _lastWaveRound x4 · _log x14 · _outputUsersThisRound x1 · _pipeline x2 · _player x16 · _recentNotFull x5 · _reinforcementCount x2 · _reinforcePool x2 · _retreatDisabledThisRound x3 · _round x10 · _sequencer x1 · _skills x2 · _units x2
