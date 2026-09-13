using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 名册（M8.0，`blueprint §9.15`）：**跨会话状态** —— 英雄个体（等级/特质）与
/// 🔴 **士气（`#287` = `#245` 的落地）** 都由它持有。
///
/// 🔴 为什么士气必须在这里：`#245` 定的是「**回城士气完全不恢复**」——
/// 那句话的**全部含义**就是"**士气跨趟累积**"；而实现里士气一直是**单趟状态**（回城即随趟结束）
/// ⇒ 与 `#245` **直接矛盾** ⇒ 本类补上这块欠账：
/// · **出征**：`OpeningMorale(...)` 给本趟开局士气（**不重置**）；
/// · **归来**：`ApplyReturnFromRun(...)` 把本趟结果写回（**回城不解算、不重置** —— 只写回，不修正）；
/// · **减压**：`ApplyRelief(...)` 是**唯一**的"提高士气"出口（M8.0 ④）。
/// 所有变更**必写 `HeroMoraleChangedEvent`**。
/// </summary>
public sealed class Roster
{
    private readonly RosterConfig _cfg;
    private readonly Dictionary<string, int> _morale;

    public Roster(RosterConfig config)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
        _morale = config.Heroes.ToDictionary(h => h.Id, h => Math.Clamp(h.Morale, 0, 100));
    }

    /// <summary>名册上限与英雄清单（只读）。</summary>
    public IReadOnlyList<HeroConfig> Heroes => _cfg.Heroes;

    /// <summary>某英雄当前士气（跨趟）。</summary>
    public int MoraleOf(string heroId)
    {
        if (!_morale.TryGetValue(heroId, out int m))
        {
            throw new InvalidOperationException($"名册里没有英雄 \"{heroId}\"（M8.0 / #287）。");
        }

        return m;
    }

    /// <summary>设置士气（夹在 [0,100]；变更才写事件）。</summary>
    public void SetMorale(CombatLog log, string heroId, int value, string reason)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        int current = MoraleOf(heroId);
        int next = Math.Clamp(value, 0, 100);
        if (next == current)
        {
            return;
        }

        _morale[heroId] = next;
        log.Append(new HeroMoraleChangedEvent(heroId, next - current, next, reason));
    }

    /// <summary>
    /// **归来写回**（`#245`：回城**不解算、不重置**）—— 只把本趟结束时的士气原样写回名册。
    /// </summary>
    public void ApplyReturnFromRun(CombatLog log, IEnumerable<(string Id, int Morale)> runResult)
    {
        foreach ((string id, int morale) in runResult)
        {
            if (_morale.ContainsKey(id))
            {
                SetMorale(log, id, morale, "run_return");
            }
        }
    }

    /// <summary>**减压**（M8.0 ④ 的唯一士气出口）；返回恢复后的士气。</summary>
    public int ApplyRelief(CombatLog log, string heroId, int restore, string building)
    {
        SetMorale(log, heroId, MoraleOf(heroId) + restore, $"relief:{building}");
        return MoraleOf(heroId);
    }

    /// <summary>下一趟的**开局士气**（`#287` V2 的可观测入口）：**不重置**，原样取出。</summary>
    public IReadOnlyDictionary<string, int> OpeningMorale(IEnumerable<string> heroIds)
        => heroIds.ToDictionary(id => id, MoraleOf);
}
