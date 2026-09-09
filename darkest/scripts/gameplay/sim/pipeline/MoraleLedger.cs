using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Buffs;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>
/// 士气台账（T-M2-08 + T-M4-01~05/07）：唯一写入口 Apply（钳制 [0,100]）+ 崩溃判定（事件触发，
/// 美德/折磨池 + 33% 钩子定义；T-M4-02/03）+ 满值处理（T-M4-05）+ 虚弱回升（T-M4-07）。
/// 数值唯一来自 morale_events.json；buff 落挂经 IBuffLedger（可空注入）。
/// </summary>
public sealed class MoraleLedger
{
    private readonly BalanceTable _balance;
    private readonly MoraleEventsConfig _events;
    private readonly IBuffLedger? _buffs;
    private readonly HashSet<UnitId> _weakHitThisTurn = new();
    private readonly HashSet<UnitId> _collapsedThisAction = new();
    private bool _moraleFullTeamBonusDone;

    public MoraleLedger(BalanceTable balance, MoraleEventsConfig events, IBuffLedger? buffs = null)
    {
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _buffs = buffs;
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

    /// <summary>动作级判定去重（一次伤害事件内崩溃判定最多 1 次，T-M4-02）。</summary>
    public void ResetActionTracker() => _collapsedThisAction.Clear();

    public int Get(UnitRuntime unit) => unit.Morale;

    /// <summary>唯一写入口：clamp 至 [min,max]，写 MoraleEvent，返回实际净变动。</summary>
    public int Apply(UnitRuntime unit, int delta, string sourceId, CombatLog log)
    {
        if (unit is null)
        {
            throw new ArgumentNullException(nameof(unit));
        }

        int newValue = Math.Clamp(unit.Morale + delta, _balance.MoraleMin, _balance.MoraleMax);
        int actual = newValue - unit.Morale;
        unit.Morale = newValue;
        unit.CollapseEmber = newValue == 0; // morale §4.0 余烬标记；>0 清除
        log.Append(new MoraleEvent(unit.Id, delta, sourceId, newValue));
        return actual;
    }

    /// <summary>受击派生（#157）：物理不掉；精神 = AOE −5(per_target) / 精神暴击 −12 / 普通 −8。</summary>
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

    /// <summary>团队事件一次（O-14：同动作内合并一次逐成员施加）。</summary>
    public void ApplyTeamOnce(IEnumerable<UnitRuntime> team, string sourceId, CombatLog log)
    {
        foreach (UnitRuntime unit in team)
        {
            Apply(unit, _events.Get(sourceId).Delta, sourceId, log);
        }
    }

    // ------------------------------------------------------------------
    // T-M4-02/03 崩溃判定（事件触发 #67；判定后士气留在 0）
    // ------------------------------------------------------------------

    public bool IsCollapseEmber(UnitRuntime unit) => unit.CollapseEmber;

    /// <summary>士气从 >0 跨到 0（普通打击路径）：恰好触发 1 次判定（调用方保证 before&gt;0）。</summary>
    public void CheckCollapseTrigger(UnitRuntime unit, IRngProvider rng, CombatLog log)
    {
        if (unit.Morale != 0 || !_collapsedThisAction.Add(unit.Id))
        {
            return;
        }

        RollCollapse(unit, rng, log);
    }

    /// <summary>HP 归零路径：即使士气已在 0（余烬）也判定一次，与跨 0 判定去重（同动作只 1 次）。</summary>
    public void TriggerCollapseForHpZero(UnitRuntime unit, IRngProvider rng, CombatLog log)
    {
        if (!_collapsedThisAction.Add(unit.Id))
        {
            return;
        }

        RollCollapse(unit, rng, log);
    }

    /// <summary>崩溃掷骰：美德率 = base% + 韧性 ÷ divisor（#56/#68）；roll &lt; 美德率 → 美德，否则折磨。</summary>
    public void RollCollapse(UnitRuntime unit, IRngProvider rng, CombatLog log)
    {
        int virtueRate = _balance.Tuning.VirtueRate.BasePercent
                         + unit.EffectiveResilience / _balance.Tuning.VirtueRate.ResilienceDivisor;

        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll));
        log.Append(new CollapseRollEvent(unit.Id, roll));

        if (roll < virtueRate)
        {
            GrantVirtue(unit, rng, log);
        }
        else
        {
            GrantAffliction(unit, rng, log);
        }
    }

    private void GrantVirtue(UnitRuntime unit, IRngProvider rng, CombatLog log)
    {
        IReadOnlyList<string> pool = _balance.Tuning.Collapse.VirtuePool;
        if (pool.Count == 0)
        {
            log.Append(new CollapseResultEvent(unit.Id, "Virtue", null));
            return;
        }

        string buffId = pool[rng.NextInt(0, pool.Count)];
        // 美德不叠层（#94/#100）：已有美德 → 保留旧
        if (_buffs is null)
        {
            log.Append(new CollapseResultEvent(unit.Id, "Virtue", buffId));
            return;
        }

        if (!_buffs.AnyOfKind(unit.Id, "virtue_"))
        {
            _buffs.Add(unit.Id, buffId, source: null);
        }

        log.Append(new CollapseResultEvent(unit.Id, "Virtue", buffId));
    }

    private void GrantAffliction(UnitRuntime unit, IRngProvider rng, CombatLog log)
    {
        IReadOnlyList<string> pool = _balance.Tuning.Collapse.AfflictionPool;
        if (pool.Count == 0)
        {
            log.Append(new CollapseResultEvent(unit.Id, "Affliction", null));
            return;
        }

        string buffId = pool[rng.NextInt(0, pool.Count)];
        if (_buffs is not null)
        {
            // 折磨自然不并存：先清旧折磨再挂新（morale §4.0）
            foreach (string old in _buffs.Buffs(unit.Id).Where(b => b.StartsWith("affliction_", StringComparison.Ordinal)).ToArray())
            {
                _buffs.Remove(unit.Id, old);
            }

            _buffs.Add(unit.Id, buffId, source: null);
        }

        log.Append(new CollapseResultEvent(unit.Id, "Affliction", buffId));
    }

    // ------------------------------------------------------------------
    // T-M4-05 满值处理（#55/#151/#100）
    // ------------------------------------------------------------------

    /// <summary>士气冲满 100：随机美德挂自身（保留旧）+ 全队 +10（一次性）+ 自身回 50。</summary>
    public void HandleMoraleMax(UnitRuntime unit, UnitRuntime[] team, IRngProvider rng, CombatLog log)
    {
        GrantVirtue(unit, rng, log);
        if (!_moraleFullTeamBonusDone)
        {
            _moraleFullTeamBonusDone = true;
            ApplyTeamOnce(team, "morale_full_100", log); // once_per_battle（morale_events）
        }

        unit.Morale = _balance.MoraleStart; // 自身回 50（morale §7）
        unit.CollapseEmber = false;
    }

    // ------------------------------------------------------------------
    // T-M4-07 虚弱回升（#59）
    // ------------------------------------------------------------------

    /// <summary>回合回升 = 10 + 健康支援位队友×5 + 韧性÷20，上限 25；只来自【健康】支援位（虚弱互不提供）。</summary>
    public int SupportSlotRegen(UnitRuntime unit, FormationBoard player)
    {
        int healthySupport = 0;
        foreach (int slot in player.Layout.SupportSlots)
        {
            UnitRuntime? ally = player.UnitRuntimeAt(slot);
            if (ally is not null && !ally.Weak && ally.Id != unit.Id)
            {
                healthySupport++;
            }
        }

        return RecoveryAmount(unit.EffectiveResilience, healthySupport, _balance);
    }

    /// <summary>纯公式：10 + healthy×5 + 韧性÷20，钳 [0, cap]。供测试复算。</summary>
    public static int RecoveryAmount(int resilience, int healthySupportAllies, BalanceTable balance)
    {
        TuningWeakRecovery w = balance.Tuning.WeakRecovery;
        int raw = w.Base + healthySupportAllies * w.PerHealthySupportAlly + resilience / w.ResilienceDivisor;
        return Math.Clamp(raw, 0, w.Cap);
    }
}