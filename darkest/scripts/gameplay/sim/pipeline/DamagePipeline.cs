using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Survival;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>技能最小夹具（M2 期：data_schema §3.2 所需字段的最小只读形态；skills.json 全量导入归 M3）。</summary>
public sealed record SkillFixture(
    string Id,
    UnitId CasterId,
    FormationSide TargetSide,
    IReadOnlyList<int> TargetSlots,
    int HitMod,
    int CritMod,
    string Axis,
    IReadOnlyList<double> Segments,
    bool IsAoe,
    IReadOnlyList<EffectRequest> Effects,
    SkillDisplace? Displacement,
    IReadOnlyList<MoraleEffectRequest>? ExplicitMoraleEffects = null);

/// <summary>显式士气影响（data_schema §3.2 morale_effects 的最小形态；O-21 威吓箭 −4 属此类）。</summary>
public sealed record MoraleEffectRequest(string Scope, int Delta);

/// <summary>位移效果（data_schema §3.2 displacement 的最小形态；SelfDisplacement=自移不依赖命中，O-12）。</summary>
public sealed record SkillDisplace(int FromPos, int ToPos, int Distance, bool SelfDisplacement);

/// <summary>
/// 结算管线（T-M2-04~10 集成，§2 固定次序）：
///   单目标链：命中 → 暴击 → 伤害(扣HP) → 士气 → 虚弱/死门；
///   技能级：全部目标逐个结算（槽位升序）后，再执行附加效果与位移（O-12 默认固化）；未命中
///   目标不结算伤害/士气/目标层效果/该目标位移，技能自位移照常。全部事件写同一 CombatLog
///   （每骰 RngDraw）。零 Godot 引用。
/// </summary>
public sealed class DamagePipeline
{
    private readonly BalanceTable _balance;
    private readonly MoraleEventsConfig _moraleEvents;
    private readonly CombatLog _log;
    private readonly MoraleLedger _ledger;

    public DamagePipeline(BalanceTable balance, MoraleEventsConfig moraleEvents, CombatLog log,
        Darkest.Core.Contracts.IBuffLedger? buffs = null)
    {
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _moraleEvents = moraleEvents ?? throw new ArgumentNullException(nameof(moraleEvents));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _ledger = new MoraleLedger(balance, moraleEvents, buffs);
    }

    public CombatLog Log => _log;

    public MoraleLedger Morale => _ledger;

    public void InitializeMorale(FormationBoard player)
        => _ledger.Initialize(player.UnitsInSlotOrder());

    /// <summary>执行一次技能动作（按 §2 次序），全部事件落 Log。</summary>
    public void Execute(SkillFixture skill, FormationBoard player, FormationBoard enemy, IRngProvider rng)
    {
        if (skill is null)
        {
            throw new ArgumentNullException(nameof(skill));
        }

        FormationBoard targetBoard = skill.TargetSide == FormationSide.Enemy ? enemy : player;
        FormationBoard sourceBoard = skill.TargetSide == FormationSide.Enemy ? player : enemy;
        UnitRuntime? caster = sourceBoard.UnitsInSlotOrder().FirstOrDefault(u => u.Id == skill.CasterId);
        UnitRuntime[] playerTeam = player.UnitsInSlotOrder().ToArray();
        _ledger.ResetActionTracker(); // 动作级崩溃判定去重（T-M4-02）

        bool anyCritThisAction = false;
        int[] orderedSlots = skill.TargetSlots.OrderBy(s => s).ToArray(); // 槽位编号升序（固定枚举序）

        // ① 单目标链（命中→伤害→士气→虚弱/死门）
        foreach (int slot in orderedSlots)
        {
            if (targetBoard.GetSlot(slot) != SlotState.Occupied)
            {
                continue; // Empty 跳过；Blocked（障碍）M2 不结算伤害/状态（扩充归 M4；部分非空只作用非空位）
            }

            UnitRuntime target = targetBoard.UnitRuntimeAt(slot)!;
            if (caster is null)
            {
                continue;
            }

            bool hit = HitStep.Resolve(caster, target, skill.HitMod, rng, _log, _balance);
            if (!hit)
            {
                continue; // 未命中：该目标无伤害/士气/效果/位移（技能自位移殿后照常，§2 O-12 默认）
            }

            DamageOutcome dmg = DamageStep.Deal(caster, target, skill.Axis, skill.Segments, skill.CritMod, rng, _log, _balance);
            anyCritThisAction |= dmg.AnyCrit;

            int moraleBefore = target.Morale;
            // 士气：显式 morale_effects（如威吓箭 targets −4，O-21/#170）取代精神派生 −8/−12/−5（不叠加）；
            // 否则按 damage_axis + 暴击 + aoe 派生（#157）。
            if (skill.ExplicitMoraleEffects is { Count: > 0 } explicitEffects)
            {
                foreach (MoraleEffectRequest eff in explicitEffects)
                {
                    if (eff.Scope == "targets")
                    {
                        _ledger.Apply(target, eff.Delta, "skill_morale_effect", _log); // 数值来源 = 技能数据（skills.json morale_effects，M3 全量接入）
                    }
                }
            }
            else
            {
                _ledger.ApplyIncomingDamageMorale(target, skill.Axis, dmg.AnyCrit, skill.IsAoe, _log);
            }

            // 崩溃判定（事件触发 #67：士气从 >0 跨到 0 → 恰好一次）+ 满值处理（T-M4-05）
            if (moraleBefore > 0 && target.Morale == 0)
            {
                _ledger.CheckCollapseTrigger(target, rng, _log);
            }

            if (target.Morale >= _balance.MoraleMax)
            {
                _ledger.HandleMoraleMax(target, playerTeam, rng, _log);
            }

            if (target.IsPlayer)
            {
                if (target.Weak)
                {
                    if (dmg.TotalDealt > 0)
                    {
                        _ = _ledger.TryApplyWeakHitPenalty(target, _log); // 虚弱 −5（每回合≤1）
                        bool survived = WeakDeathsDoor.Roll(target, afflicted: false, rng, _log, _balance);
                        if (!survived)
                        {
                            KillAt(targetBoard, slot, target, isPlayer: true);
                            _ledger.ApplyTeamOnce(playerTeam, "ally_death", _log); // O-14 合并一次
                        }
                    }
                }
                else if (target.CurrentHp <= 0)
                {
                    WeakDeathsDoor.EnterWeak(target, _ledger, rng, _log, _balance);
                    _ledger.ApplyTeamOnce(playerTeam, "ally_enters_weak", _log);
                }
            }
            else if (target.CurrentHp <= 0)
            {
                KillAt(targetBoard, slot, target, isPlayer: false); // 敌方直接死亡（无虚弱/死门）
                _ledger.ApplyTeamOnce(playerTeam, "kill_enemy", _log);
            }
        }

        // ② 团队性暴击 +5（动作级聚合一次，O-14）
        if (anyCritThisAction)
        {
            _ledger.ApplyTeamOnce(playerTeam, "critical_strike_dealt", _log);
        }

        // ③ 附加效果（只对 occupied 目标；Blocked 障碍免疫状态，GDD §1.1）
        foreach (int slot in orderedSlots)
        {
            if (targetBoard.GetSlot(slot) != SlotState.Occupied)
            {
                continue;
            }

            UnitRuntime target = targetBoard.UnitRuntimeAt(slot)!;
            foreach (EffectRequest effect in skill.Effects)
            {
                EffectsStep.Apply(target, effect, rng, _log, _balance);
            }
        }

        // ④ 位移（全部目标结算后；§2 / O-12）
        if (skill.Displacement is { } displacement)
        {
            if (displacement.SelfDisplacement)
            {
                // 技能自位移（突进/后撤）：不依赖命中照常结算（O-12）
                if (caster is not null && sourceBoard.UnitAtPosition(caster.Id) is { } fromSelf)
                {
                    DisplaceResult chain = sourceBoard.TrySwapChain(caster.Id, fromSelf,
                        displacement.ToPos, displacement.Distance);
                    _log.Append(new DisplaceEvent(caster.Id, fromSelf, displacement.ToPos, true, chain.Success, chain.Failure.ToString()));
                }
            }
            else if (targetBoard.GetSlot(displacement.FromPos) == SlotState.Occupied)
            {
                UnitRuntime displaced = targetBoard.UnitRuntimeAt(displacement.FromPos)!;
                DisplaceStep.Resolve(displaced, targetBoard, displacement.FromPos, displacement.ToPos,
                    displacement.Distance, rng, _log);
            }
        }
    }

    private void KillAt(FormationBoard board, int slot, UnitRuntime unit, bool isPlayer)
    {
        board.RemoveUnitAt(slot);
        board.CloseUp(slot); // 死亡/离场 → 立即向中靠齐（M1 原子）
        _log.Append(new DeathEvent(unit.Id, isPlayer));
    }
}