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
/// ③ 给出**名册当前可用上限**（C1 · **M7②**：单一来源 = **马车曲线** `roster_cap_by_level`；
///    未传曲线时退回旧口径 = 起手 8 + 解锁 + 马车增量，硬上限由调用方给）。
///
/// ⚠️ 口径（如实标注，供策划复核）：`runs` 统计**已结束的出征**（结局 = **完成 / 放弃远征 / 全灭** —— 🔴 `#352` 后**撤退不算结局**）——
///    理由：若只数"完成"，撤退/团灭的玩家会**永远解锁不了任何东西**。若你要改成"只数完成"，改本类一处 ✓
/// </summary>
/// <remarks>🆕 **存档（`Phase 1`）**：改 `partial` —— 快照存取见 `RunProgress.Save.cs`（避免撑大本文件）。</remarks>
public sealed partial class RunProgress
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
    /// 🔴 **名册当前可用上限**（策划 `#403` 起手口径 · **M7② `#423` 收敛**）：
    ///   ① **传了马车曲线**（当前唯一调用口径）⇒ `min(硬上限, 曲线[马车等级])` —— **上限的单一来源** ✓
    ///   ② **不传曲线**（历史调用）⇒ 老逻辑一字不动：`min(硬上限, 起手 + 解锁 + 马车增量)` ✓
    /// ⚠️ `roster_cap_delta:`（增量）**已撤**（M7②：解锁表不再提供上限增量，否则「两处真值」）；
    ///    旧语法 `roster_cap:N`（**绝对值**）仍接受 ⇒ 取最大，以免**改写历史读数**（归档纪律 ✓）。
    /// </summary>
    /// <param name="stagecoachCapByLevel">
    /// 🆕 **M7 第 ② 步（策划 `#423`）**：**上限的单一来源 = 马车曲线**（`economy.json` 的
    ///   `stagecoach.roster_cap_by_level` = 9→12→16→20→24→28，索引 = 马车等级）✓
    ///   🔴 **传了曲线 ⇒ 以曲线为准**（不再相加解锁增量/马车效果 —— 那些是"两处真值"家族 ✓）
    ///   🔴 **不传 ⇒ 老逻辑一字不动**（历史读数不被改写 ✓）
    ///   ✅ **已接线（2026-10-01）**：调用点 = `MainMenuRoot.cs`（菜单路径）与
    ///      `HamletRoot.Recruit.cs` 的 `RecomputeRosterCap`（城池路径：马车升级后重算）✓
    /// </param>
    /// <param name="stagecoachLevel">马车等级（`HeirloomStock.LevelOf("stagecoach")` ✓）；越界会被钳制 ✓</param>
    public int CurrentRosterCap(
        UnlocksConfig cfg,
        int hardCap,
        int heirloomDelta = 0,
        IReadOnlyList<int>? stagecoachCapByLevel = null,
        int stagecoachLevel = 0)
    {
        // 🆕 **M7②：单一来源 = 马车曲线**（给了曲线就只用它 ✓）
        if (stagecoachCapByLevel is { Count: > 0 } curve)
        {
            int idx = Math.Clamp(stagecoachLevel, 0, curve.Count - 1);
            return Math.Min(curve[idx], hardCap);
        }

        int cap = cfg.RosterBaseCap;
        foreach (string s in UnlockedIds(cfg))
        {
            // 🔴 M7②：`roster_cap_delta:`（增量）**已撤** —— 上限单一来源 = 马车曲线 ✓
            if (s.StartsWith("roster_cap:", StringComparison.Ordinal)
                && int.TryParse(s["roster_cap:".Length..], out int n))
            {
                cap = Math.Max(cap, n); // 旧语法：绝对值取最大（历史口径不变 ✓）
            }
        }

        // 遗留：只服务「不传曲线」的历史调用（新调用一律走上面的马车曲线 ✓）
        cap += Math.Max(0, heirloomDelta);
        return Math.Min(cap, hardCap);     // 封顶 ✓
    }

    /// <summary>🟩 取证：一行摘要（解锁进度 / 三个消费点的当前值）。</summary>
    /// <summary>
    /// 🆕 **下一个解锁**（做功能：把"还差多少"变成**可读** —— 与 A4「感受到成长」同族 ✓）。
    /// 取**最近可达**的那条未解锁项（"两项缺口之和最小"；并列时按 `required_runs_finished` 再按 id 保序 ⇒ **确定性** ✓）。
    /// 全部已解锁 ⇒ 返回 null（**如实报"没有下一个"，不编一个** ✓）。
    /// </summary>
    public (UnlockEntry Entry, int RunsRemaining, int BattlesRemaining)? NextUnlock(UnlocksConfig cfg)
    {
        var candidates = new List<(UnlockEntry Entry, int Runs, int Battles)>();
        foreach (UnlockEntry e in cfg.Unlocks)
        {
            int runs = Math.Max(0, e.RequiredRunsFinished - RunsFinished);
            int battles = Math.Max(0, e.RequiredBattlesWon - BattlesWon);
            if (runs == 0 && battles == 0)
            {
                continue; // 已达成 ⇒ 不是"下一个" ✓
            }

            candidates.Add((e, runs, battles));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .OrderBy(c => c.Runs + c.Battles)
            .ThenBy(c => c.Entry.RequiredRunsFinished)
            .ThenBy(c => string.Join(",", c.Entry.Unlocks), StringComparer.Ordinal)
            .First();
        return (best.Entry, best.Runs, best.Battles);
    }

    /// <summary>
    /// 一行读数（菜单用）✓
    /// 🔴 **M7②**：上限的单一来源 = 马车曲线 ⇒ 本读数**必须与 `Roster.CurrentCap` 同口径**
    ///    （传了曲线就按曲线报，**不另算一份** —— 否则读数与真值打架，O-96 家族）✓
    /// </summary>
    public string Audit(UnlocksConfig cfg, int hardCap,
        IReadOnlyList<int>? stagecoachCapByLevel = null, int stagecoachLevel = 0)
    {
        IReadOnlySet<string> ids = UnlockedIds(cfg);
        return $"征途进度：已完成出征 {RunsFinished} 趟（已胜 {BattlesWon} 场）　" +
               $"已解锁 {ids.Count} 项［{string.Join(" ", ids.OrderBy(x => x, StringComparer.Ordinal))}］　" +
               $"名册当前可用上限 {CurrentRosterCap(cfg, hardCap, stagecoachCapByLevel: stagecoachCapByLevel, stagecoachLevel: stagecoachLevel)}（硬上限 {hardCap}）　" +
               $"Curio 可用 {4 + UnlockedCurios(cfg).Count} 种　" +
            // 🆕 **下一个解锁**（进度可见性 · A4 同族）：报「还差多少」，全解锁就如实说没有 ✓
            (NextUnlock(cfg) is { } next
                ? $"下一解锁：{string.Join("/", next.Entry.Unlocks)}（还差 **{next.RunsRemaining} 趟** ／ {next.BattlesRemaining} 胜）"
                : "下一解锁：**全部已解锁** ✓");
    }
}
