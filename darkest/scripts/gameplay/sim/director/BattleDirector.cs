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

/// <summary>战斗结果（O-47）：Ongoing 进行中 / Victory 敌方全灭 / Defeat 我方全灭（优先判定）。</summary>

/// <summary>
/// BattleDirector：一场战斗的确定性编排（blueprint §4 B3 / §8.3，T-M5-02/03/04/09）——
/// 回合推进（增援/支援位+3/虚弱回升）、玩家命令（UseSkill/Swap/Retreat）、敌方阶段（EnemyAi 同源）、
/// 失败当回合不可再试（#118）。实机输入 / 录制回放 / headless 策略只差命令来源；
/// 全部事件写同一 CombatLog（回放/镜像基础）。零 Godot 引用。
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

        ApplyReinforcement(rng);

        // 支援位每回合 +3（morale_events support_slot_turn_start，#60）
        foreach (int slot in _player.Layout.SupportSlots)
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

    /// <summary>
    /// 玩家命令：释放技能（SkillExecutor 桥接管线；chosenTargets 供实机单体选一，headless 默认随机选一 #178/#179）。
    /// #211（S0）：**支援位（5/6）技能 −1 SP**；不足 → 拒（返回 false，调用方保持该单位行动，不吞回合）；
    /// **战斗位技能永不因 SP 被拒**（硬提醒①）。
    /// </summary>
    public bool PlayerUseSkill(UnitId actor, string skillId, IRngProvider rng, IReadOnlyList<int>? chosenTargets = null)
    {
        SkillTemplateConfig skill = _skills.Get(skillId);
        // #211（S0）：SP 消耗**按技能声明优先**（`support_point_cost: 0` = 豁免，如【喘息】）；
        // 未声明时沿用"支援位技能扣 cost_skill、战斗位不扣"的默认规则
        int spCost = skill.SupportPointCost ?? (IsSupportSlotActor(actor) ? SupportCostSkill : 0);
        if (spCost > 0 && !TrySpendSupportPoints(spCost, "skill"))
        {
            _log.Append(new SkillRefusedEvent(actor, skillId, "support_points")); // 不足 = 不可用（非失败）
            return false;
        }

        if (skill.Tags.Contains(FuncTag.Output))
        {
            _outputUsersThisRound.Add(actor); // #198：攻击类技能 = tags 含 output（玩家可见的"输出"类）
        }

        _executor.Execute(skill, actor, _player, _enemy, rng, chosenTargets);

        // 🔴 补欠账（`m3_skills_units.md:228/247/312` + `data_schema.md:207`）：
        //    **`self_damage_fixed`（殊死一搏 6 ／ 舍身 8）**：M3 明确"自伤致死死门链【归 M4】"，
        //    但实测**全仓无消费点** ⇒ 这两张牌不会自伤、也不会致死门 ⚠️
        //    契约要求：**固定值、不被护盾吸收、可致死（走既有死门链）** —— 与 `TickBleedAtTurnStart` 同语义
        //    （直接 `CurrentHp -=` ⇒ 不经过 `DamageStep` 的护盾/防御判定 ⇒ 天然"不被护盾吸收"）。
        if (skill.SelfDamageFixed is { } selfDmg && selfDmg > 0)
        {
            ApplyFixedSelfDamage(actor, selfDmg, skillId, rng);
        }

        return true;
    }

    /// <summary>
    /// 固定自伤（`self_damage_fixed`）：**不经护盾、不受防御减免**（直接扣 HP）、
    /// **致死走既有死亡/死门链**（玩家 ⇒ 死门；敌方无死门 ⇒ 直接死）—— 与流血同通道。
    /// </summary>
    private void ApplyFixedSelfDamage(UnitId actor, int dmg, string skillId, IRngProvider rng)
    {
        int? pos = _player.UnitAtPosition(actor);
        UnitRuntime? u = pos is { } p ? _player.UnitRuntimeAt(p) : null;
        if (u is null || u.CurrentHp <= 0)
        {
            return;
        }

        u.CurrentHp -= dmg;
        _log.Append(new DamageEvent(u.Id, dmg, dmg, Crit: false, SegmentIndex: 0, Axis: "self",
            Attacker: u.Id, SkillId: skillId));

        if (u.CurrentHp > 0)
        {
            return;
        }

        if (u.IsPlayer)
        {
            WeakDeathsDoor.EnterWeak(u, _pipeline.Morale, rng, _log, _balance); // 契约：可致死（走死门）
        }
        else
        {
            _enemy.RemoveUnitAt(_enemy.UnitAtPosition(u.Id) ?? -1);
            _log.Append(new DeathEvent(u.Id, IsPlayer: false, Cause: "self_damage"));
        }
    }

    /// <summary>
/// 增援（#181 单按钮两步，推翻旧双按钮 PlayerSwap）：发起者 A（战斗位，消耗本次行动）指定
/// 支援位角色 B（5/6 占用者）与目标战斗位 X（1~4）——X 有人 → B 与 X 上单位【直接互换】；
/// X 空 → B 直接进入并（若需）前移。同回合至多 1 次。
/// </summary>
    public bool Reinforce(UnitId a, int bSlot, int x)
    {
        if (_swappedThisRound)
        {
            return false;
        }

        int aPos = _player.UnitAtPosition(a) ?? -1;
        if (aPos is < 1 or > 4) // 发起者须在战斗位
        {
            return false;
        }

        UnitRuntime? b = _player.UnitRuntimeAt(bSlot);
        if (b is null || !_player.Layout.SupportSlots.Contains(bSlot))
        {
            return false;
        }

        if (x is < 1 or > 4 || _player.GetSlot(x) == SlotState.Empty && aPos == x)
        {
            return false;
        }

        // #211（S0）：增援 = 调动一个人上场 → 比放技能重（−2）；不足 → 被拒，且**不得吞掉发起者行动**
        if (!TrySpendSupportPoints(_balance.Tuning.SupportPoints.CostReinforce, "reinforce"))
        {
            return false;
        }

        if (_player.GetSlot(x) == SlotState.Occupied)
        {
            // X 有人：B 与 X 上单位【直接互换】（#41a「X 有人则交换」——原 X 上的人到 B 的原位，途经槽位不动）
            if (!_player.SwapSlots(bSlot, x))
            {
                return false;
            }
        }
        else
        {
            // X 空：B 直接进入（原支援位槽位随之空出）
            _player.RemoveUnitAt(bSlot);
            _player.PlaceUnitAt(x, b);
        }

        _swappedThisRound = true;
        // G0/O-55：SwapEvent 补被调动者（B）与种类（#181 增援）
        _log.Append(new SwapEvent(a, bSlot, x, b.Id, "reinforce"));
        return true;
    }

    /// <summary>F2（#193）恐惧 proc：使用技能前 33% 拒放（技能灰掉、**不消耗行动**、须重选）；勇猛 immune_fear 豁免。</summary>
    public bool TryFearRefusal(UnitId actor, IRngProvider rng)
    {
        bool refused = Darkest.Gameplay.Sim.Buffs.AfflictionProcs.Triggered(_buffs, _balance, actor, "affliction_fear", rng, _log);
        if (refused)
        {
            _log.Append(new SkillRefusedEvent(actor, "-", "affliction_fear")); // G0/O-55
        }

        return refused;
    }

    /// <summary>本回合是否已换位（策略护栏；StartTurn 重置）。</summary>
    public bool SwappedThisRound => _swappedThisRound;

    // ------------------------------------------------------------------
    // #211（S0）支援点 SP：战斗级资源（全队共享；不挂单位、不吃驱散、不随死亡改变）
    // ------------------------------------------------------------------

    /// <summary>当前支援点（投影只读；UI 不得自行扣点/缓存）。</summary>
    public int SupportPoints => _supportPoints;

    /// <summary>支援点上限。</summary>
    public int SupportCap => _balance.Tuning.SupportPoints.Cap;

    /// <summary>下回合开始的恢复预览（UI 必显 #10：含本回合恢复预览）。</summary>
    public int SupportRegenPreview
        => Math.Min(_supportPoints + _balance.Tuning.SupportPoints.RegenPerRound, _balance.Tuning.SupportPoints.Cap);

    /// <summary>支援位技能消耗。</summary>
    public int SupportCostSkill => _balance.Tuning.SupportPoints.CostSkill;

    /// <summary>增援消耗。</summary>
    public int SupportCostReinforce => _balance.Tuning.SupportPoints.CostReinforce;

    /// <summary>
    /// 扣点（#211）：不足 → 返回 false 并记 `rejected`（调用方负责"被拒不吞行动"）。
    /// 硬提醒②：**每次变动必须写 SupportPointEvent**（UI 数字与统计的唯一来源）。
    /// </summary>
    public bool TrySpendSupportPoints(int cost, string reason)
    {
        if (_supportPoints < cost)
        {
            _log.Append(new SupportPointEvent(0, _supportPoints, "rejected"));
            return false;
        }

        _supportPoints -= cost;
        _log.Append(new SupportPointEvent(-cost, _supportPoints, reason));
        return true;
    }

    /// <summary>该单位是否位于支援位（5/6）——只有支援位技能与增援消耗 SP。</summary>
    public bool IsSupportSlotActor(UnitId actor)
        => _player.UnitAtPosition(actor) is { } pos && _player.Layout.SupportSlots.Contains(pos);

    /// <summary>
    /// 🔴 M7.5 D2 / `#268`（架构裁定）：**支援包（`support_pack`，消耗品）的 SP 结算入口**。
    /// · 语义 = **「用库存换资源」**，**不是技能** ⇒ **不消耗行动**（支援位已受 SP 成本 + Pass −5 士气双约束）；
    /// · 🔴 **SP 是战斗级资源（`blueprint` §9.11），只有本处一个写入口** —— 远征层只当**库存**扣物品，
    ///      加值一律由本方法结算（否则出现两套 SP 台账：本项目已多次栽在"双源"上）；
    /// · 🔴 **加到 cap 后必须钳制**（P19 ⑧；cap 来自 `tuning.support_points.cap`）；
    /// · **每次变动必写 `SupportPointEvent(reason:"item")`**（UI 数字与 ⑲ 归因的唯一来源）。
    /// </summary>
    public bool TryUseSupportPackForSp(int amount = 2, string reason = "item")
    {
        if (amount <= 0)
        {
            return false;
        }

        int before = _supportPoints;
        _supportPoints = Math.Min(_supportPoints + amount, SupportCap); // 🔴 钳 cap（P19 ⑧）
        _log.Append(new SupportPointEvent(_supportPoints - before, _supportPoints, reason));
        return true;
    }

    /// <summary>显式「待命」（S5.2）：放弃本次行动，不消耗 SP、不结算任何效果、不进技能栏。</summary>
    /// <remarks>
    /// 🔴 M7.5 D3（`m7_5_dungeon_layer.md` §D3 / `#266`）：**待命【受士气伤害】** —— "最后手段"必须有代价。
    /// · 数值走 `tuning.expedition.pass_morale_delta`（起手 **−5**；**禁止硬编码**）；
    /// · 士气变更**走既有 `MoraleLedger` 通道**（士气状态的唯一来源）；
    /// · `TurnSkippedEvent(Reason:"passed", MoraleDelta:…)` 只**同时携带**该增量（㉗ 的统计来源），
    ///   不构成第二套状态；若架构要求只留一处，删事件字段即可（已写进窗口备案）。
    /// · 阈值与处置：**待命占支援位行动回合 ≤ 25%**；越界 ⇒ **调 SP / 补"不耗 SP 的事"，不加罚**。
    /// </remarks>
    public void PassTurn(UnitId actor)
    {
        int position = _player.UnitAtPosition(actor) ?? -1;
        UnitRuntime? unit = position >= 0 ? _player.UnitRuntimeAt(position) : null;
        int delta = unit is null ? 0 : -Math.Abs(_balance.Tuning.Expedition.PassMoraleDelta);

        if (unit is not null && delta != 0)
        {
            Morale.Apply(unit, delta, "pass", _log); // 士气唯一来源（写 MoraleEvent）
        }

        _log.Append(new TurnSkippedEvent(actor, "passed", delta));
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
