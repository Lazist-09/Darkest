using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// M8.1（`hamlet.md §1` / `m8_roadmap §1`）：**传家宝库存 + 建筑升级**（**跨会话状态**，与 `Economy` 同层）。
///
/// 🔴 三条设计要点（照规格）：
/// · **第三种资源**：传家宝**从光照档掉落**（**与金钱同源**：越暗越多）；
/// · **两轴升级**：**降费**（服务更便宜）与**解锁/增强**（更高阶）—— 每级**消耗固定几种、数量递增**；
/// · 🔴 **升级必须真的改变数字**（验收）：本类提供**生效值**读取口（`EffectiveReliefCost` / `EffectiveMoraleRestore` /
///   `EffectiveRosterCap` / `EffectiveRookieLevel`），供 Hamlet 与经济使用 —— **UI 只读不算**。
///
/// 归属：**跨会话持有者**（组合根注入；`BattleDirector` 不持有它）。变更**必写事件**。
/// </summary>
public sealed class HeirloomStock
{
    private readonly HeirloomConfig _cfg;
    private readonly Dictionary<string, int> _counts;
    private readonly Dictionary<string, int> _levels = new();

    public HeirloomStock(HeirloomConfig config)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
        _counts = config.Kinds.ToDictionary(k => k, _ => 0);
        foreach (UpgradePath p in config.UpgradePaths)
        {
            _levels[p.Building] = 0; // 未升级
        }
    }

    /// <summary>四种传家宝的 kind 名（UI 只读渲染用）。</summary>
    public IReadOnlyList<string> Kinds => _cfg.Kinds;

    /// <summary>某类传家宝的持有数。</summary>
    public int Count(string kind) => _counts.TryGetValue(kind, out int n)
        ? n
        : throw new InvalidOperationException($"未定义的传家宝 \"{kind}\"（P23 ①）。");

    /// <summary>某建筑的当前升级等级（0 = 未升级）。</summary>
    public int LevelOf(string building) => _levels.TryGetValue(building, out int lv)
        ? lv
        : throw new InvalidOperationException($"未知建筑 \"{building}\"（P23 ④）。");

    /// <summary>按当前光照档发放本趟的传家宝（**与金钱同一结算点**）；返回发放总数。</summary>
    public int AwardForTier(CombatLog log, string tierId, string reason = "battle")
    {
        HeirloomDropSpec drop = _cfg.DropFor(tierId);
        int total = 0;
        foreach (string kind in _cfg.Kinds)
        {
            int amount = KindAmount(drop, kind);
            if (amount > 0)
            {
                Add(log, kind, amount, reason);
                total += amount;
            }
        }

        return total;
    }

    /// <summary>增加/减少传家宝（变更必写事件；减少不足即拒绝）。</summary>
    public bool Add(CombatLog log, string kind, int delta, string reason)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        int current = Count(kind);
        if (current + delta < 0)
        {
            return false; // 不足 ⇒ 拒绝（不静默变负）
        }

        _counts[kind] = current + delta;
        log.Append(new HeirloomChangedEvent(kind, delta, _counts[kind], reason));
        return true;
    }

    /// <summary>下一级的消耗（已满级 ⇒ null）。</summary>
    public UpgradeLevel? NextLevel(string building)
    {
        UpgradePath p = _cfg.PathFor(building);
        int next = LevelOf(building) + 1;
        return p.Levels.FirstOrDefault(l => l.Level == next);
    }

    /// <summary>
    /// 🔴 红线 21 (b)：**按钮的可用性必须由内核回答**（UI 只渲染）—— 传家宝够且未满级才可升级。
    /// </summary>
    public bool CanUpgrade(string building)
    {
        UpgradeLevel? next = NextLevel(building);
        return next is not null && next.Cost.All(kv => Count(kv.Key) >= kv.Value);
    }

    /// <summary>**升级**：按曲线扣传家宝并升一级（不足即拒绝且不扣）。</summary>
    public bool TryUpgrade(CombatLog log, string building)
    {
        UpgradeLevel? next = NextLevel(building);
        if (next is null)
        {
            return false; // 已满级
        }

        foreach ((string kind, int need) in next.Cost)
        {
            if (Count(kind) < need)
            {
                return false; // 传家宝不足 ⇒ 拒绝（不部分扣、不静默）
            }
        }

        foreach ((string kind, int need) in next.Cost)
        {
            Add(log, kind, -need, $"upgrade:{building}");
        }

        _levels[building] = next.Level;
        log.Append(new BuildingUpgradedEvent(building, next.Level,
            string.Join(",", next.Cost.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}:{k.Value}"))));
        return true;
    }

    // ---------------------------------------------------------------
    // 生效值（**升级真的改变数字** —— 验收口径；UI 只读不算）
    // ---------------------------------------------------------------

    /// <summary>减压价格（含降费升级）。</summary>
    public int EffectiveReliefCost(int baseCost)
        => Math.Max(0, baseCost + SumEffects("tavern").ReliefCostDelta + SumEffects("abbey").ReliefCostDelta);

    /// <summary>某减压建筑的恢复量（含增强升级）。</summary>
    public int EffectiveMoraleRestore(string building, int baseRestore)
        => Math.Max(0, baseRestore + SumEffects(building).MoraleRestoreDelta);

    /// <summary>名册上限（含 Stage Coach 解锁）。</summary>
    public int EffectiveRosterCap(int baseCap, int hardCap)
        => Math.Min(hardCap, baseCap + SumEffects("stagecoach").RosterCapDelta);

    /// <summary>新兵起始等级（含 Stage Coach 解锁；缺省 1）。</summary>
    public int EffectiveRookieLevel(int baseLevel)
    {
        UpgradeEffect e = SumEffects("stagecoach");
        return e.RookieLevel ?? baseLevel;
    }

    private UpgradeEffect SumEffects(string building)
    {
        int level = LevelOf(building);
        var costDown = 0;
        var restore = 0;
        var cap = 0;
        int? rookie = null;
        foreach (UpgradeLevel lv in _cfg.PathFor(building).Levels.Where(l => l.Level <= level))
        {
            costDown += lv.Effect.ReliefCostDelta;
            restore += lv.Effect.MoraleRestoreDelta;
            cap += lv.Effect.RosterCapDelta;
            rookie = lv.Effect.RookieLevel ?? rookie;
        }

        return new UpgradeEffect(costDown, restore, cap, rookie);
    }

    private static int KindAmount(HeirloomDropSpec d, string kind) => kind switch
    {
        "busts" => d.Busts,
        "crests" => d.Crests,
        "deeds" => d.Deeds,
        "portraits" => d.Portraits,
        _ => 0,
    };
}
