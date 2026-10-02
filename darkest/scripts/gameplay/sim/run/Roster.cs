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
/// <remarks>
/// **分片（`partial` · 红线 28 · 按职责切）**：本片 = **核心**（状态 · 构造 · 只读面 · 阵亡消费 · 上限 · 士气）；
/// 其余：**`Roster.Progression.cs`**（成长与招募）· **`Roster.Ailments.cs`**（疾病与特质）·
/// **`Roster.Save.cs`**（存档存取）· **`Roster.Quirks.cs`**（怪癖 · M5u）· **`Roster.Trinkets.cs`**（饰品 · M4u）✓
/// 为什么 `partial`：快照/状态半要**读写私有字段** ⇒ 同类型分片既拿到私有访问权、又**不撑大单个文件** ✓
/// </remarks>
public sealed partial class Roster
{
    private readonly RosterConfig _cfg;
    private readonly Dictionary<string, int> _morale;
    private readonly Dictionary<string, int> _xp = new();   // 🆕 升级通道：累计经验（缺省不接线）✓
    private readonly List<(string HeroId, string Name, int Level, string Cause)> _graveyard = new(); // 🆕 阵亡留档 ✓
    private readonly List<HeroConfig> _heroes;
    private int _recruitSeq;

    /// <summary>🔴 `#283` 7.3：待生效的"**下一趟开局 −N**"（减压副作用；`OpeningMorale` 消费后清空）✓</summary>
    private readonly Dictionary<string, int> _pendingOpeningPenalty = new(StringComparer.Ordinal);

    public Roster(RosterConfig config)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
        _heroes = config.Heroes.ToList();
        _morale = config.Heroes.ToDictionary(h => h.Id, h => Math.Clamp(h.Morale, 0, 100));
    }

    /// <summary>名册上限与英雄清单（只读）。</summary>
    public IReadOnlyList<HeroConfig> Heroes => _heroes;

    /// <summary>🆕 **升级通道是否接线**（`roster.json` 有 `experience` ⇒ true）——
    /// 🔴 未接线时 `AwardExperience*` **显式不生效**（返回 false、不写事件）⇒ **不假装** ✓</summary>
    public bool ExperienceWired => _cfg.Experience is not null;

    /// <summary>🆕 **阵亡留档**（Graveyard 列表 · 策划 `#400` (a)+ / A11）——只读口，UI/读数**只读它** ✓</summary>
    public IReadOnlyList<(string HeroId, string Name, int Level, string Cause)> Graveyard => _graveyard;

    /// <summary>
    /// 🆕 **消费本趟的阵亡**（`DeathEvent(IsPlayer: true)`）：**移出名册**（⇒ **名额自然释放** ✓）
    /// ＋ **进 Graveyard 留档** ＋ **写 `HeroDiedEvent`**（A11：可读 + 可追溯 ✓）。
    /// 纪律：**只认事件流**（不另记账 ✓）· **幂等**（同一趟重复调用不会重复移除 ✓）· 不抛异常打断主流程 ✓
    /// </summary>
    public int ConsumePlayerDeaths(CombatLog runLog, string cause = "battle_death")
    {
        int removed = 0;
        // 🔴 必须先**物化**：本方法会**往同一个 log 追加** `HeroDiedEvent` ⇒ 边遍历边改会抛
        //    `InvalidOperationException: Collection was modified`（实测被用例当场抓到 ✓）
        foreach (DeathEvent death in runLog.Events.OfType<DeathEvent>().Where(d => d.IsPlayer).ToList())
        {
            string heroId = death.Unit?.ToString() ?? string.Empty;
            int idx = _heroes.FindIndex(h => h.Id == heroId);
            if (idx < 0)
            {
                continue; // 已移除 / 不是名册成员（如敌人或槽位 id）⇒ 跳过（幂等 ✓）
            }

            HeroConfig gone = _heroes[idx];
            _heroes.RemoveAt(idx);
            _xp.Remove(gone.Id);
            _morale.Remove(gone.Id);
            _quirks.Remove(gone.Id);   // 🆕 M5u：阵亡者的怪癖一并清掉（不留孤儿条目 ⇒ 存档不涨）✓
            _trinkets.Remove(gone.Id); // 🆕 M4u：阵亡者的饰品一并清掉（同一条纪律：不留孤儿条目 ⇒ 存档不涨）✓
            _graveyard.Add((gone.Id, gone.Name, gone.Level, string.IsNullOrEmpty(death.Cause) ? cause : death.Cause));
            runLog.Append(new HeroDiedEvent(gone.Id, gone.Name, gone.Level, cause, _heroes.Count));
            removed++;
        }

        return removed;
    }

    /// <summary>名册**硬上限**（= `roster.json` 的 `roster_cap`；实际可用值由马车曲线决定 ⇒ `CurrentCap`）。</summary>
    public int Cap => _cfg.RosterCap;

    /// <summary>
    /// 🔴 **当前可用上限**（C1 / `O-86` / **M7② `#423`**）：**硬上限 = `Cap`**；
    /// 本值 = **马车曲线** `roster_cap_by_level[马车等级]`（由组合根按 `RunProgress.CurrentRosterCap` 写入）✓
    /// 🔴 **招募的「满员即拒绝」必须按【本值】判**（P22⑥ / C1），而不是按硬上限 ✓
    /// </summary>
    public int CurrentCap { get; set; }

    private int EffectiveCap => CurrentCap > 0 ? Math.Min(CurrentCap, Cap) : Cap;

    /// <summary>
    /// 🔴 **能不能再招一个人**（红线 21 (b)：可用性由**内核**回答，UI 只渲染）——
    /// 判据 = 当前人数 &lt; 当前可用上限（`EffectiveCap`）✓
    /// </summary>
    public bool CanRecruit => _heroes.Count < EffectiveCap;

    private static string ResPathOf(RosterConfig? cfg) => RosterConfig.ResPath;

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
    /// <remarks>
    /// 🔴 `#283` 7.3（2026-09-20 接线）：减压副作用的"**下一趟开局 −N**"在这里**真正生效** ——
    /// 此前 `StressRelief.NextRunOpeningMorale` **无生产调用点** ⇒ 副作用**只在 UI 上显示、从不实际扣** ⚠️
    /// （真缺陷：玩家看到"下趟 −8"却毫无代价）⇒ 本方法改为：**待到罚者先扣、再产出开局值** ✓
    /// ⚠️ **读到即清**（`_pendingOpeningPenalty`）⇒ 同一笔副作用**只影响一趟** ✓
    /// </remarks>
    public IReadOnlyDictionary<string, int> OpeningMorale(IEnumerable<string> heroIds)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in heroIds)
        {
            int morale = MoraleOf(id);
            if (_pendingOpeningPenalty.TryGetValue(id, out int penalty) && penalty > 0)
            {
                morale = StressRelief.NextRunOpeningMorale(morale, penalty);
            }

            result[id] = morale;
        }

        _pendingOpeningPenalty.Clear(); // 🔴 读到即清：副作用只影响这一趟 ✓
        return result;
    }

    /// <summary>
    /// 🔴 `#283` 7.3：**登记"下一趟开局 −N"的副作用**（减压掷骰触发时由 Hamlet 侧调用）——
    /// 供 `OpeningMorale` 在下一趟开趟时消费 ✓
    /// 📌 为什么单独存一层而不是直接改士气：`#245` 要求"回城**完全不恢复**（也不额外惩罚）"，
    ///    惩罚的时机是**下一趟开局**，不是回城 ⇒ 必须**延后**到那时才施加 ✓
    /// </summary>
    public void ScheduleOpeningPenalty(string heroId, int penalty)
    {
        if (penalty <= 0 || !_morale.ContainsKey(heroId))
        {
            return; // 无惩罚 / 不在册 ⇒ 什么都不登记（不假装）✓
        }

        _pendingOpeningPenalty[heroId] = Math.Max(
            _pendingOpeningPenalty.GetValueOrDefault(heroId), penalty); // 取最重者（不叠加，避免滚雪球）✓
    }

    /// <summary>待生效的"下一趟开局 −N"（只读读数；headless 冒烟断言用）✓</summary>
    public IReadOnlyDictionary<string, int> PendingOpeningPenalties => _pendingOpeningPenalty;
}
