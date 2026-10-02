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
using Darkest.Gameplay.Sim.Morale;
using Darkest.Gameplay.Sim.Pipeline;
using Darkest.Gameplay.Sim.Skill;
using Darkest.Gameplay.Sim.Survival;
using Darkest.Gameplay.Sim.Turn;

namespace Darkest.Gameplay.Sim.Director;

/// <summary>战斗结果（O-47）：Ongoing 进行中 / Victory 敌方全灭 / Defeat 我方全灭（优先判定）。</summary>

/// <summary>
/// BattleDirector：一场战斗的确定性编排（blueprint §4 B3 / §8.3，T-M5-02/03/04/09）——
/// 回合推进（增援/支援位+3/虚弱回升）、玩家命令（UseSkill/Swap/Retreat）、敌方阶段（EnemyAi 同源）、
/// 失败当回合不可再试（#118）。实机输入 / 录制回放 / headless 策略只差命令来源；
/// 全部事件写同一 CombatLog（回放/镜像基础）。零 Godot 引用。
/// 分片（M11 ① 预警面第二十一件 · 2026-10-02）：**玩家命令族** ⇒ `BattleDirector.Commands.cs`；**支援点 SP 族** ⇒ `BattleDirector.SupportPoints.cs`（只搬家 · 零行为改动）✓
/// </summary>
public sealed partial class BattleDirector
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
    private int _lastWaveRound; // #196：上一波增援的回合（0 = 尚未触发）
    private bool _endEmitted;   // G0：战斗结束事件幂等
    private IRngProvider? _lastRng; // D3：回合开始钩子（流血致死进死门）需要 RNG
    private int _supportPoints;     // #211（S0）：支援点 SP（战斗级资源，全队共享）
    private int _elasticBonus;  // #198：弹性浮动（0..max_bonus，叠加在 M_base 上）
    private readonly HashSet<UnitId> _outputUsersThisRound = new(); // #198：本回合用过 output 技能的我方单位
    private readonly Queue<bool> _recentNotFull = new();           // #198：最近 K 回合"未全力进攻"标记
    private readonly string[] _enemyRoster; // 满编原型（算满编总 HP）
    private readonly string[] _reinforcePool = { "melee_soldier", "ranged_archer", "caster" }; // O-20 占位轮换
    private readonly TurnSequencer _sequencer;
    private IReadOnlyList<UnitId> _lastRoundOrder = Array.Empty<UnitId>();

    public BattleDirector(FormationConfig formation, UnitsConfig units, SkillsConfig skills,
        BalanceTable balance, MoraleEventsConfig moraleEvents, BuffDefsConfig buffDefs, EnemyAiConfig enemyAi,
        CombatLog log, BuffLedger? sharedBuffs = null)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _moraleEvents = moraleEvents ?? throw new ArgumentNullException(nameof(moraleEvents));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        // 🔴 M7 E3 / #240：**可注入会话级台账** —— `next_battle`（磨刀/加固甲胄）需跨场持久且**单源**；
        // 未注入时行为与从前完全一致（每场自建 → 单场纯、同 seed 可复现）。
        _buffs = sharedBuffs ?? new BuffLedger(buffDefs, log); // G0/O-55：buff 生命周期事件接入事件流
        _shield = new ShieldGuard(_buffs, balance.Tuning.GuardRedirect); // 🔴 护卫参数来自 data（唯一真相）✓
        _runtime = new SkillRuntimeState();
        _pipeline = new DamagePipeline(balance, moraleEvents, log, _buffs, _shield);
        _executor = new SkillExecutor(skills, balance, moraleEvents, log, _runtime, _buffs);
        // #211（S0）：支援点 SP = 战斗级状态（全队共享、不挂单位、不吃驱散）；起手值来自 tuning
        _supportPoints = balance.Tuning.SupportPoints.Start;
        _ai = new EnemyAi(enemyAi, skills, _runtime);
        _player = FormationBoardFactory.CreatePlayerBoard(formation, units);
        _enemy = FormationBoardFactory.CreateEnemyBoard(formation, units);
        _enemyRoster = formation.InitialRoster.Enemy.Select(e => e.Unit).ToArray(); // #196：满编原型（算总 HP）
        _pipeline.InitializeMorale(_player);
        _sequencer = new TurnSequencer(_player, _enemy, balance, log); // G0：跳过行动事件接入
    }

    public int Round => _round;
    public FormationBoard Player => _player;
    public FormationBoard Enemy => _enemy;
    public CombatLog Log => _log;
    public MoraleLedger Morale => _pipeline.Morale;

    /// <summary>buff 台账（只读面；F2 起 UI/测试需按 buff 查询美德/折磨效果）。</summary>
    public Darkest.Core.Contracts.IBuffLedger Buffs => _buffs;

    /// <summary>本回合行动序列（StartTurn 固定点构建；UI 只读，不新增抽取）。</summary>
    public IReadOnlyList<UnitId> LastRoundOrder => _lastRoundOrder;

    /// <summary>回合开始：增援（第 6 回合起）→ 支援位 +3 → 虚弱回升/归队 → 重置当回合撤退禁用。</summary>
    public void StartTurn(IRngProvider rng)
    {
        _round++;
        _log.Round = _round; // G0/O-55：此后 append 的事件自动带回合号
        _lastRng = rng;
        _log.Append(new RoundStartEvent(_round));

        // #211（S0）硬提醒③：regen 必须在【消耗判定之前】，且 cap 在 regen 后立即钳制（否则第 1 回合起手值会算错）
        int spRegen = _balance.Tuning.SupportPoints.RegenPerRound;
        if (spRegen > 0)
        {
            int beforeRegen = _supportPoints;
            _supportPoints = Math.Min(_supportPoints + spRegen, _balance.Tuning.SupportPoints.Cap);
            if (_supportPoints != beforeRegen)
            {
                _log.Append(new SupportPointEvent(_supportPoints - beforeRegen, _supportPoints, "regen"));
            }
        }

        // #198 弹性：先评估"刚结束的回合"是否未全力进攻（存活战斗位中未用 output 技能者 ≥ 阈值），再清空本回合记录
        if (_round > 1)
        {
            EvaluateElasticWindow();
        }

        _outputUsersThisRound.Clear();

        // 🔴 **回合级士气计数器清零**（`morale_events` 的 `once_per_turn_max1`）——
        //    ⚠️ 此前**无人调用** `MoraleLedger.ResetTurnCounters` ⇒ `_weakHitThisTurn` 永不清空
        //       ⇒ "虚弱者受击 −5（每回合≤1）"实际退化成"**整场≤1**"（真缺陷）⚠️
        //    ⇒ 与 `_outputUsersThisRound.Clear()` 同一处收口（都是"回合开始清零"的语义）✓
        _pipeline.Morale.ResetTurnCounters();

        ApplyReinforcement(rng);

        // 支援位每回合 +3（morale_events support_slot_turn_start，#60）
        foreach (int slot in _player.Layout.ExtensionSlots)
        {
            if (_player.GetSlot(slot) == SlotState.Occupied)
            {
                _pipeline.Morale.Apply(_player.UnitRuntimeAt(slot)!, _moraleEvents.Get("support_slot_turn_start").Delta, "support_slot_turn_start", _log);
            }
        }

        // F2（#193）：美德「振奋」回合钩子——每个持有者令全队 +3 士气（O-27 定值，读 tuning）
        int inspired = _balance.Tuning.Collapse.VirtueInspiredMoralePerTurn;
        foreach (UnitRuntime holder in _player.UnitsInSlotOrder())
        {
            if (_buffs.Has(holder.Id, "virtue_inspired"))
            {
                int moraleBefore = _player.UnitsInSlotOrder().Sum(u => u.Morale);
        _pipeline.Morale.ApplyTeamOnce(_player.UnitsInSlotOrder(), "virtue_inspired_round", _log, overrideDelta: inspired);
            }
        }

        // 虚弱回升（T-M4-07/#59）→ 归队（T-M4-06/#164）
        foreach (UnitRuntime u in _player.UnitsInSlotOrder())
        {
            if (u.Weak)
            {
                int regen = _pipeline.Morale.SupportSlotRegen(u, _player);
                _pipeline.Morale.Apply(u, regen, "weak_recovery", _log);
                if (WeakDeathsDoor.TryRecover(u, _balance))
                {
                    // D4（#206）死门后遗症：归队后受伤 +10% / 命中 −5 / 速度 −1，到战斗结束且**不叠加**
                    if (!_buffs.Has(u.Id, "deaths_door_recovery"))
                    {
                        _buffs.Add(u.Id, "deaths_door_recovery", source: null);
                        u.SpeedMod -= 1;
                        _log.Append(new EffectEvent(u.Id, "deaths_door_recovery", 100.0, true, u.Id));
                    }
                }
            }
        }

        _retreatDisabledThisRound = false;
        _swappedThisRound = false;
        _shield.OnTurnStart();
        _lastRoundOrder = _sequencer.BuildRoundOrder(rng); // 每回合固定点重掷（#163）；UI 读缓存
    }

    /// <summary>下一位行动者（M6 前置立卡：行动序列/眩晕/减速生效——排序与眩晕跳过均在内核 TurnSequencer）。</summary>
    public UnitId? NextActor()
    {
        UnitId? actor = _sequencer.NextActor();
        if (actor is not null)
        {
            EmitTurnStart(actor.Value);
        }

        return actor;
    }

    /// <summary>G0/O-55：战斗结束事件（幂等；胜负与原因入日志）。</summary>
    public void EmitBattleEnd(string? reason = null)
    {
        if (_endEmitted)
        {
            return;
        }

        _endEmitted = true;
        string why = reason ?? (Outcome == BattleOutcome.Victory ? "enemy_wiped" : "player_wiped");
        // 显式传入原因时按原因定结局（战斗可能尚未投影为已结束，例如撤退收束路径）
        string outcome = Outcome != BattleOutcome.Ongoing
            ? Outcome.ToString()
            : why switch
            {
                "enemy_wiped" => "Victory",
                "player_wiped" => "Defeat",
                _ => "Ongoing",
            };
        _log.Append(new BattleEndEvent(outcome, _round, why));
    }

    /// <summary>实例 id 是否属于我方原型（含 _2 等实例后缀；用于从事件流统计我方输出）。</summary>
    private bool IsPlayerId(UnitId id)
        => _units.PlayerArchetypes.Any(p =>
            id.Value == p || id.Value.StartsWith(p + "_", StringComparison.Ordinal));

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
