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

/// <summary>玩家行动决策（actor 节拍回调产物）：技能 或 换位（消耗本次行动，二选一）。</summary>
public sealed record PlayerDecision(string? SkillId, int? SwapSupportPos);

/// <summary>
/// BattleDirector：一场战斗的确定性编排（blueprint §4 B3 / §8.3，T-M5-02/03/04/09）——
/// 回合推进（增援/支援位+3/虚弱回升）、玩家命令（UseSkill/Swap/Retreat）、敌方阶段（EnemyAi 同源）、
/// 失败当回合不可再试（#118）。实机输入 / 录制回放 / headless 策略只差命令来源；
/// 全部事件写同一 CombatLog（回放/镜像基础）。零 Godot 引用。
/// </summary>
public sealed class BattleDirector
{
    private readonly SkillsConfig _skills;
    private readonly UnitsConfig _units;
    private readonly BalanceTable _balance;
    private readonly MoraleEventsConfig _moraleEvents;
    private readonly CombatLog _log;
    private readonly BuffLedger _buffs;
    private readonly ShieldGuard _shield;
    private readonly SkillRuntimeState _runtime;
    private readonly DamagePipeline _pipeline;
    private readonly SkillExecutor _executor;
    private readonly EnemyAi _ai;
    private readonly FormationBoard _player;
    private readonly FormationBoard _enemy;
    private int _round;
    private bool _retreatDisabledThisRound;
    private bool _swappedThisRound;
    private int _reinforcementCount;
    private readonly string[] _reinforcePool = { "melee_soldier", "ranged_archer", "caster" }; // O-20 占位轮换
    private readonly TurnSequencer _sequencer;
    private IReadOnlyList<UnitId> _lastRoundOrder = Array.Empty<UnitId>();

    public BattleDirector(FormationConfig formation, UnitsConfig units, SkillsConfig skills,
        BalanceTable balance, MoraleEventsConfig moraleEvents, BuffDefsConfig buffDefs, EnemyAiConfig enemyAi,
        CombatLog log)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _moraleEvents = moraleEvents ?? throw new ArgumentNullException(nameof(moraleEvents));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _buffs = new BuffLedger(buffDefs);
        _shield = new ShieldGuard(_buffs);
        _runtime = new SkillRuntimeState();
        _pipeline = new DamagePipeline(balance, moraleEvents, log, _buffs, _shield);
        _executor = new SkillExecutor(skills, balance, moraleEvents, log, _runtime, _buffs);
        _ai = new EnemyAi(enemyAi, skills, _runtime);
        _player = FormationBoardFactory.CreatePlayerBoard(formation, units);
        _enemy = FormationBoardFactory.CreateEnemyBoard(formation, units);
        _pipeline.InitializeMorale(_player);
        _sequencer = new TurnSequencer(_player, _enemy, balance);
    }

    public int Round => _round;
    public FormationBoard Player => _player;
    public FormationBoard Enemy => _enemy;
    public CombatLog Log => _log;
    public MoraleLedger Morale => _pipeline.Morale;

    /// <summary>本回合行动序列（StartTurn 固定点构建；UI 只读，不新增抽取）。</summary>
    public IReadOnlyList<UnitId> LastRoundOrder => _lastRoundOrder;

    /// <summary>回合开始：增援（第 6 回合起）→ 支援位 +3 → 虚弱回升/归队 → 重置当回合撤退禁用。</summary>
    public void StartTurn(IRngProvider rng)
    {
        _round++;
        _log.Append(new RoundStartEvent(_round));

        ApplyReinforcement(rng);

        // 支援位每回合 +3（morale_events support_slot_turn_start，#60）
        foreach (int slot in _player.Layout.SupportSlots)
        {
            if (_player.GetSlot(slot) == SlotState.Occupied)
            {
                _pipeline.Morale.Apply(_player.UnitRuntimeAt(slot)!, _moraleEvents.Get("support_slot_turn_start").Delta, "support_slot_turn_start", _log);
            }
        }

        // 虚弱回升（T-M4-07/#59）→ 归队（T-M4-06/#164）
        foreach (UnitRuntime u in _player.UnitsInSlotOrder())
        {
            if (u.Weak)
            {
                int regen = _pipeline.Morale.SupportSlotRegen(u, _player);
                _pipeline.Morale.Apply(u, regen, "weak_recovery", _log);
                _ = WeakDeathsDoor.TryRecover(u, _balance);
            }
        }

        _retreatDisabledThisRound = false;
        _swappedThisRound = false;
        _shield.OnTurnStart();
        _lastRoundOrder = _sequencer.BuildRoundOrder(rng); // 每回合固定点重掷（#163）；UI 读缓存
    }

    /// <summary>玩家命令：释放技能（SkillExecutor 桥接管线；可用性防御性由执行侧短路）。</summary>
    public void PlayerUseSkill(UnitId actor, string skillId, IRngProvider rng)
        => _executor.Execute(_skills.Get(skillId), actor, _player, _enemy, rng);

    /// <summary>
    /// 换位/增援（#41a/CHANGELOG §1.4.1）：战斗位角色发起 → 与指定支援位角色交换（交换链结算）。
    /// 消耗发起者本次行动（actor 节拍内换位后不再放技能）。同回合至多 1 次（SwappedThisRound 护栏，
    /// 防换位空转抽行动；#41a 无冷却但行动即代价）。
    /// </summary>
    public bool PlayerSwap(UnitId actor, int supportPos)
    {
        if (_swappedThisRound)
        {
            return false;
        }

        UnitRuntime? actorUnit = _player.UnitsInSlotOrder().FirstOrDefault(x => x.Id == actor);
        int actorPos = _player.UnitAtPosition(actor) ?? -1;
        if (actorUnit is null || actorPos < 1 || actorPos > _player.SlotCount
            || _player.GetSlot(supportPos) != SlotState.Occupied)
        {
            return false;
        }

        // 交换链：发起者向目标位逐位交换（途经单位后移），目标必须是支援位占用者
        DisplaceResult result = _player.TrySwapChain(actor, actorPos, supportPos, Math.Abs(supportPos - actorPos));
        if (!result.Success)
        {
            return false;
        }

        _swappedThisRound = true;
        _log.Append(new SwapEvent(actor, actorPos, supportPos));
        return true;
    }

    /// <summary>本回合是否已换位（策略护栏；StartTurn 重置）。</summary>
    public bool SwappedThisRound => _swappedThisRound;

    /// <summary>下一位行动者（M6 前置立卡：行动序列/眩晕/减速生效——排序与眩晕跳过均在内核 TurnSequencer）。</summary>
    public UnitId? NextActor() => _sequencer.NextActor();

    /// <summary>敌方单位行动一次（actor 节拍内调用；AI 同源）。</summary>
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
            _executor.Execute(_skills.Get(choice.SkillId), actor, _player, _enemy, rng);
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
        StartTurn(rng);
        while (!IsBattleOver)
        {
            UnitId? actor = _sequencer.NextActor();
            if (actor is null)
            {
                break;
            }

            UnitRuntime? playerUnit = _player.UnitsInSlotOrder().FirstOrDefault(x => x.Id == actor);
            if (playerUnit is not null)
            {
                PlayerDecision decision = decide(playerUnit);
                if (decision.SwapSupportPos is { } swapPos)
                {
                    PlayerSwap(actor.Value, swapPos); // 换位消耗本次行动（不再放技能）
                }
                else if (decision.SkillId is not null)
                {
                    PlayerUseSkill(actor.Value, decision.SkillId, rng);
                }
            }
            else if (_enemy.UnitsInSlotOrder().Any(x => x.Id == actor))
            {
                EnemyAct(actor.Value, rng);
            }
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
        _pipeline.Morale.ApplyTeamOnce(_player.UnitsInSlotOrder(),
            success ? "retreat_success" : "retreat_fail", _log); // −10 / −5 全队（#126/#43）
        return success;
    }

    /// <summary>撤退可点状态投影（UI 用）：失败当回合 disabled。</summary>
    public bool CanRetreatThisRound => !_retreatDisabledThisRound && !IsBattleOver;

    public bool IsBattleOver
        => _enemy.OccupiedPositions(false).Count == 0 || _player.OccupiedPositions(false).Count == 0;

    private double AvgSpeed(FormationBoard board)
    {
        UnitRuntime[] alive = board.UnitsInSlotOrder().ToArray();
        if (alive.Length == 0)
        {
            return 0.0;
        }

        return alive.Average(u => u.EffectiveSpeed(_balance.WeakSpeedMult));
    }

    /// <summary>T-M5-03 超时增援：第 trigger_round 起每回合开始判定；有空位填（不超 4 位上限），满编上增益（O-20 占位数值）。</summary>
    private void ApplyReinforcement(IRngProvider rng)
    {
        _ = rng; // 切片增援不走随机分布（O-20 裁定）
        TuningOvertimeReinforcement o = _balance.Tuning.OvertimeReinforcement;
        if (_round < o.TriggerRound || IsBattleOver)
        {
            return;
        }

        int emptySlot = Enumerable.Range(1, _enemy.SlotCount).FirstOrDefault(s => _enemy.GetSlot(s) == SlotState.Empty, 0);
        if (emptySlot == 0)
        {
            // 满编分支：不给新单位，改为给在场敌人上 +攻/+速（O-20 占位数值）——"有空位才填、无空位才上增益"
            BuffAllEnemies(_enemy);
            return;
        }

        // 有空位分支：按原型轮换填入（默认起手值，无随机分布；不超 4 位上限）
        string archetype = _reinforcePool[_reinforcementCount % _reinforcePool.Length];
        _reinforcementCount++;
        var unit = new UnitRuntime(UnitId.Of(archetype), FormationSide.Enemy,
            UnitStatsMapper.From(_units.Get(archetype)));
        _enemy.PlaceUnitAt(emptySlot, unit);
        _log.Append(new ReinforcementEvent("Fill", unit.Id, emptySlot));
    }

    private void BuffAllEnemies(FormationBoard board)
    {
        TuningOvertimeReinforcement o = _balance.Tuning.OvertimeReinforcement;
        foreach (UnitRuntime u in board.UnitsInSlotOrder())
        {
            u.AttackMod += o.BuffAttackDelta;
            u.SpeedMod += o.BuffSpeedDelta;
            _log.Append(new ReinforcementEvent("Buff", u.Id, null));
        }
    }
}