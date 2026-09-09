using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Buffs;

/// <summary>
/// BuffLedger：buff 台账实现（blueprint §9.6 / T-M4-03/04/08）。
/// 实例 = (buffId, 剩余回合?, 次数?)；叠层：refresh=刷新时长（护盾刷新为新满次数 O-25 建议）、
/// stack=有上限计数、none=上限 1（美德保留旧，#94）。驱散 1 次 1 个（Remove）。
/// </summary>
public sealed class BuffLedger : IBuffLedger
{
    private readonly BuffDefsConfig _defs;
    private readonly Dictionary<UnitId, List<BuffInstance>> _byUnit = new();

    private sealed record BuffInstance(string BuffId, int RemainingRounds, int Charges)
    {
        public BuffInstance WithRounds(int r) => this with { RemainingRounds = r };
        public BuffInstance WithCharges(int c) => this with { Charges = c };
    }

    public BuffLedger(BuffDefsConfig defs)
    {
        _defs = defs ?? throw new ArgumentNullException(nameof(defs));
    }

    public void Add(UnitId u, string buffId, UnitId? source)
    {
        BuffDefConfig def = _defs.Get(buffId);
        if (!_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            list = new List<BuffInstance>();
            _byUnit[u] = list;
        }

        int rounds = def.Duration.Value ?? 0;
        int charges = def.Duration.Type == BuffDurationType.Charges ? rounds : 0;

        switch (def.Stack.Rule)
        {
            case BuffStackRule.Refresh:
                list.RemoveAll(b => b.BuffId == buffId);
                list.Add(new BuffInstance(buffId, rounds, charges));
                break;
            case BuffStackRule.Stack:
                BuffInstance? existing = list.FirstOrDefault(b => b.BuffId == buffId);
                int max = def.Stack.Max ?? int.MaxValue;
                if (existing is null)
                {
                    list.Add(new BuffInstance(buffId, rounds, charges));
                }
                else if (list.Count(b => b.BuffId == buffId) < max)
                {
                    list.Add(new BuffInstance(buffId, rounds, charges));
                }

                break;
            case BuffStackRule.None:
                if (list.Any(b => b.BuffId == buffId))
                {
                    return; // 上限 1：保留旧（美德 #94/#100）
                }

                list.Add(new BuffInstance(buffId, rounds, charges));
                break;
        }
    }

    public void AddCharged(UnitId u, string buffId, int charges)
    {
        if (!_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            list = new List<BuffInstance>();
            _byUnit[u] = list;
        }

        list.RemoveAll(b => b.BuffId == buffId);
        list.Add(new BuffInstance(buffId, RemainingRounds: 0, charges));
    }

    public void Remove(UnitId u, string buffId)
    {
        if (_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            list.RemoveAll(b => b.BuffId == buffId);
        }
    }

    public bool Has(UnitId u, string buffId)
        => _byUnit.TryGetValue(u, out List<BuffInstance>? list) && list.Any(b => b.BuffId == buffId);

    public IReadOnlyList<string> Buffs(UnitId u)
        => _byUnit.TryGetValue(u, out List<BuffInstance>? list) ? list.Select(b => b.BuffId).ToArray() : Array.Empty<string>();

    public int Charges(UnitId u, string buffId)
    {
        if (_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            BuffInstance? b = list.FirstOrDefault(x => x.BuffId == buffId);
            if (b is not null)
            {
                return b.Charges;
            }
        }

        return 0;
    }

    public void ConsumeCharge(UnitId u, string buffId)
    {
        if (!_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            return;
        }

        int idx = list.FindIndex(b => b.BuffId == buffId);
        if (idx < 0)
        {
            return;
        }

        int left = list[idx].Charges - 1;
        if (left <= 0)
        {
            list.RemoveAt(idx);
        }
        else
        {
            list[idx] = list[idx].WithCharges(left);
        }
    }

    public int StatModTotal(UnitId u, string stat)
    {
        if (!_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            return 0;
        }

        int total = 0;
        foreach (BuffInstance b in list)
        {
            BuffDefConfig def = _defs.Get(b.BuffId);
            foreach (BuffModifierSpec m in def.Modifiers ?? Array.Empty<BuffModifierSpec>())
            {
                if (m.Kind == BuffModifierKind.StatMod && m.Stat == stat)
                {
                    total += m.Delta ?? 0;
                }
            }
        }

        return total;
    }

    public bool HasStateFlag(UnitId u, string flag)
    {
        if (!_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            return false;
        }

        return list.Any(b =>
        {
            BuffDefConfig def = _defs.Get(b.BuffId);
            return def.Modifiers?.Any(m => m.Kind == BuffModifierKind.StateFlag && m.Effect == flag) == true;
        });
    }

    public void ClearStateFlag(UnitId u, string flag)
    {
        // 状态类 buff（action_skip 等）标记清除 = 移除整个 buff（眩晕=跳过 1 次随即结束）
        if (!_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            return;
        }

        foreach (BuffInstance b in list.ToArray())
        {
            BuffDefConfig def = _defs.Get(b.BuffId);
            if (def.Modifiers?.Any(m => m.Kind == BuffModifierKind.StateFlag && m.Effect == flag) == true)
            {
                list.Remove(b);
            }
        }
    }

    public int PercentMod(UnitId u, string effect)
    {
        if (!_byUnit.TryGetValue(u, out List<BuffInstance>? list))
        {
            return 0;
        }

        int total = 0;
        foreach (BuffInstance b in list)
        {
            BuffDefConfig def = _defs.Get(b.BuffId);
            foreach (BuffModifierSpec m in def.Modifiers ?? Array.Empty<BuffModifierSpec>())
            {
                if (m.Kind == BuffModifierKind.DamageMod && m.Effect == effect)
                {
                    total += m.Percent ?? 0;
                }
            }
        }

        return total;
    }

    public bool AnyOfKind(UnitId u, string kindPrefix)
        => _byUnit.TryGetValue(u, out List<BuffInstance>? list) && list.Any(b => b.BuffId.StartsWith(kindPrefix, StringComparison.Ordinal));

    public void TickRounds()
    {
        foreach (List<BuffInstance> list in _byUnit.Values)
        {
            // 仅 Rounds 型递减；Charges/并无时限型不动
            foreach (BuffInstance b in list.ToArray())
            {
                if (_defs.Get(b.BuffId).Duration.Type == BuffDurationType.Rounds && b.RemainingRounds > 0)
                {
                    int left = b.RemainingRounds - 1;
                    if (left <= 0)
                    {
                        list.Remove(b);
                    }
                    else
                    {
                        list[list.IndexOf(b)] = b.WithRounds(left);
                    }
                }
            }
        }
    }
}