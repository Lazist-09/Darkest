using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **跨趟进度 + 解锁评估**（`O-86` / `next_round` ③ / `#316`③）—— **内核层（零 Godot）** ✓
///
/// 职责：
/// ① 记【已完成出征数】（`runs`）—— **这是解锁阈值表的输入**（此前**不存在**任何跨趟计数器 ⚠️）；
/// ② 由 `UnlocksConfig` 评估出**已解锁 id 集合**，并按命名空间分类：
///    `building:<id>`（城池建筑可见性）／`curio:<id>`（Curio 池种类）／`roster_cap:<N>`（名册可用上限）；
/// ③ 给出**名册当前可用上限**（C1：**硬上限 12** 不变；解锁只抬高【当前可用上限】，起手 8）。
///
/// ⚠️ 口径（如实标注，供策划复核）：`runs` 统计**已结束的出征**（结局 = **完成 / 放弃远征 / 全灭** —— 🔴 `#352` 后**撤退不算结局**）——
///    理由：若只数"完成"，撤退/团灭的玩家会**永远解锁不了任何东西**。若你要改成"只数完成"，改本类一处 ✓
/// </summary>
public sealed class RunProgress
{
    /// <summary>已完成的出征数（跨趟持有；由组合根在"一趟结束"时 `FinishRun`）。</summary>
    public int RunsFinished { get; private set; }

    /// <summary>已打赢的场数（跨趟累计；供阈值表里 `required_battles_won` 一类条目用）。</summary>
    public int BattlesWon { get; private set; }

    /// <summary>记"一趟结束"（不论结局；见类注释的口径说明）。</summary>
    public void FinishRun(CombatLog log, string outcome, int battlesWon = 0)
    {
        RunsFinished++;
        BattlesWon += Math.Max(0, battlesWon);
        log.Append(new EffectEvent(default,
            $"run_finished:{outcome}:runs={RunsFinished}:wins={BattlesWon}", 100.0, true));
    }

    /// <summary>阈值已满足的条目所解锁的 id 全集（**引用**：只吐 id，不拷贝定义）。</summary>
    public IReadOnlySet<string> UnlockedIds(UnlocksConfig cfg)
        => cfg.Unlocks
            .Where(e => RunsFinished >= e.RequiredRunsFinished && BattlesWon >= e.RequiredBattlesWon)
            .SelectMany(e => e.Unlocks)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>已解锁的 Curio id（`curio:` 前缀去壳）。</summary>
    public IReadOnlySet<string> UnlockedCurios(UnlocksConfig cfg)
        => UnlockedIds(cfg).Where(s => s.StartsWith("curio:", StringComparison.Ordinal))
            .Select(s => s["curio:".Length..]).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 🔴 **当前可用的 Curio**（C3 起手态 = **基础 4 种**）：`config.base_curios` **∪** 已解锁的。
    /// ⚠️ 我最初只传"已解锁集合"给内核过滤 ⇒ 起手会变成 **0 种**（与 C3 的"起手 4 种"矛盾）——
    ///    所以**基础集合必须来自数据**（`base_curios`），由本方法合并 ✓
    /// </summary>
    public IReadOnlySet<string> AvailableCurios(UnlocksConfig cfg)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in cfg.Config?.BaseCurios ?? Array.Empty<string>())
        {
            set.Add(id);
        }

        foreach (string id in UnlockedCurios(cfg))
        {
            set.Add(id);
        }

        return set;
    }

    /// <summary>已解锁的建筑 id（`building:` 前缀去壳）。</summary>
    public IReadOnlySet<string> UnlockedBuildings(UnlocksConfig cfg)
        => UnlockedIds(cfg).Where(s => s.StartsWith("building:", StringComparison.Ordinal))
            .Select(s => s["building:".Length..]).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 🔴 **名册当前可用上限**（C1）：起手 = `cfg.RosterBaseCap`（8）；
    /// 已解锁的 `roster_cap:N` 里取**最大**；并**不得超硬上限**（`hardCap`，= `roster.cap` 12）。
    /// </summary>
    public int CurrentRosterCap(UnlocksConfig cfg, int hardCap)
    {
        int cap = cfg.RosterBaseCap;
        foreach (string s in UnlockedIds(cfg))
        {
            if (s.StartsWith("roster_cap:", StringComparison.Ordinal)
                && int.TryParse(s["roster_cap:".Length..], out int n))
            {
                cap = Math.Max(cap, n);
            }
        }

        return Math.Min(cap, hardCap);
    }

    /// <summary>🟩 取证：一行摘要（解锁进度 / 三个消费点的当前值）。</summary>
    public string Audit(UnlocksConfig cfg, int hardCap)
    {
        IReadOnlySet<string> ids = UnlockedIds(cfg);
        return $"征途进度：已完成出征 {RunsFinished} 趟（已胜 {BattlesWon} 场）　" +
               $"已解锁 {ids.Count} 项［{string.Join(" ", ids.OrderBy(x => x, StringComparer.Ordinal))}］　" +
               $"名册当前可用上限 {CurrentRosterCap(cfg, hardCap)}（硬上限 {hardCap}）　" +
               $"Curio 可用 {4 + UnlockedCurios(cfg).Count} 种";
    }
}
