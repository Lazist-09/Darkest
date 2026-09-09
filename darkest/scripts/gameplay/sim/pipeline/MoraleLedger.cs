using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>
/// 士气台账（T-M2-08，blueprint §9.4 成员由 M4 补齐）：唯一写入口 Apply（钳制 [0,100]）；
/// 受击派生士气按 damage_axis + 暴击 + aoe 标签自动落行，数值唯一来自 morale_events.json
/// （经 MoraleEventsConfig.Get，禁止硬编码）；虚弱者挨打 −5 每回合最多 1 次；
/// 团队性事件经 ApplyTeamOnce（"实例各自结算、同动作合并一次" O-14）。
/// </summary>
public sealed class MoraleLedger
{
    private readonly BalanceTable _balance;
    private readonly MoraleEventsConfig _events;
    private readonly HashSet<UnitId> _weakHitThisTurn = new();

    public MoraleLedger(BalanceTable balance, MoraleEventsConfig events)
    {
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <summary>起手士气（tuning morale.start，默认 50）。</summary>
    public void Initialize(IEnumerable<UnitRuntime> units)
    {
        if (units is null)
        {
            throw new ArgumentNullException(nameof(units));
        }

        foreach (UnitRuntime u in units)
        {
            u.Morale = _balance.MoraleStart;
        }
    }

    public void ResetTurnCounters() => _weakHitThisTurn.Clear();

    public int Get(UnitRuntime unit) => unit.Morale;

    /// <summary>唯一写入口：clamp 至 [min,max]，写 MoraleEvent。</summary>
    public void Apply(UnitRuntime unit, int delta, string sourceId, CombatLog log)
    {
        if (unit is null)
        {
            throw new ArgumentNullException(nameof(unit));
        }

        int newValue = Math.Clamp(unit.Morale + delta, _balance.MoraleMin, _balance.MoraleMax);
        unit.Morale = newValue;
        log.Append(new MoraleEvent(unit.Id, delta, sourceId, newValue));
    }

    /// <summary>
    /// 受击派生（#157）：物理不掉；精神 = AOE −5(per_target) / 精神暴击 −12 / 普通 −8
    /// （组合行默认：AOE 折扣优先，暴击不改变 −5，§6 待确认）。
    /// </summary>
    public void ApplyIncomingDamageMorale(UnitRuntime victim, string axis, bool crit, bool isAoe, CombatLog log)
    {
        if (axis != "mental")
        {
            return;
        }

        string id = isAoe ? "mental_aoe_hit" : crit ? "mental_crit_hit" : "mental_hit";
        Apply(victim, _events.Get(id).Delta, id, log);
    }

    /// <summary>虚弱者受任意伤害 −5（once_per_turn_max1）：返回当次是否实际扣减。</summary>
    public bool TryApplyWeakHitPenalty(UnitRuntime victim, CombatLog log)
    {
        if (!_weakHitThisTurn.Add(victim.Id))
        {
            return false;
        }

        Apply(victim, _events.Get("weak_hit_any_damage").Delta, "weak_hit_any_damage", log);
        return true;
    }

    /// <summary>团队事件一次（暴击+5/击杀+10/队友虚弱−8/死亡−15 等，O-14：同动作内合并一次逐成员施加）。</summary>
    public void ApplyTeamOnce(IEnumerable<UnitRuntime> team, string sourceId, CombatLog log)
    {
        foreach (UnitRuntime unit in team)
        {
            Apply(unit, _events.Get(sourceId).Delta, sourceId, log);
        }
    }
}