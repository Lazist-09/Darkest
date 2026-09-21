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
            _graveyard.Add((gone.Id, gone.Name, gone.Level, string.IsNullOrEmpty(death.Cause) ? cause : death.Cause));
            runLog.Append(new HeroDiedEvent(gone.Id, gone.Name, gone.Level, cause, _heroes.Count));
            removed++;
        }

        return removed;
    }

    /// <summary>🆕 某英雄的累计经验（读数口）✓</summary>
    /// <summary>🆕 某英雄**当前这一级的累计经验**（攒够 `level_costs[级-1]` 就升 ✓）</summary>
    public int ExperienceOf(string heroId) => _xp.GetValueOrDefault(heroId);

    /// <summary>🆕 某英雄的当前**等级**（可直接被 `HeroProjection.ApplyLevel` 投影 ✓）</summary>
    public int LevelOf(string heroId) => _heroes.First(h => h.Id == heroId).Level;

    /// <summary>
    /// 🆕 **战斗结算 ⇒ 给经验 ⇒ 可能升级**（契约：`hamlet.md` §7.2/§7.6「战斗给经验 ⇒ 等级成长」）✓
    /// 纪律：**数字全部来自 `_cfg.Experience`**（缺省 ⇒ 显式不生效）· **跨过阈值 ⇒ 写 `HeroLevelUpEvent`** ✓
    /// </summary>
    public bool AwardExperienceForBattle(CombatLog log, bool win, string reason = "battle")
    {
        RosterExperience? exp = _cfg.Experience;
        if (exp is null)
        {
            return false; // 🔴 未接线：**不假装**（不写事件、不改等级）✓
        }

        int amount = win ? exp.XpPerWin : exp.XpPerLoss;
        if (amount <= 0)
        {
            return false;
        }

        foreach (HeroConfig h in _heroes.ToList())
        {
            int total = _xp.GetValueOrDefault(h.Id) + amount;
            _xp[h.Id] = total;
            log.Append(new HeroExperienceGainedEvent(h.Id, amount, total, reason));

            // 🔴 **语义 = 每级固定经验**（策划 `#399` 裁定：`1→2: 2 · 2→3: 3 · 3→4: 4 · 4→5: 5 · 5→6: 6`
            //    递增 1 ⇒ **升到 Lv6 累计 20 场胜利**）✓ —— 实现 = **攒够当前级费用 ⇒ 扣掉、升 1 级** ✓
            int level = h.Level;
            while (level < _cfg.LevelMax)
            {
                int? cost = exp.CostToNextLevel(level, _cfg.LevelMin, _cfg.LevelMax);
                if (cost is null || _xp[h.Id] < cost.Value)
                {
                    break;
                }

                _xp[h.Id] -= cost.Value;
                level++;
                ReplaceLevel(h.Id, level);
                log.Append(new HeroLevelUpEvent(h.Id, level - 1, level));
            }
        }

        return true;
    }

    private void ReplaceLevel(string heroId, int level)
    {
        int i = _heroes.FindIndex(h => h.Id == heroId);
        if (i >= 0)
        {
            _heroes[i] = _heroes[i] with { Level = level };
        }
    }

    /// <summary>名册上限（M8.0 ⑤：出征 6 + 替补 6 = 12）。</summary>
    public int Cap => _cfg.RosterCap;

    /// <summary>
    /// 🔴 **当前可用上限**（C1 / `O-86`）：**硬上限仍是 `Cap`（= 12，P22① 不变）**；
    /// 本值 = 起手 8 + 解锁抬高的 `roster_cap:N`（由组合根按 `RunProgress` 设置）。
    /// 🔴 **招募的"满员即拒绝"必须按【本值】判**（P22⑥ / C1），而不是按硬上限 ✓
    /// </summary>
    public int CurrentCap { get; set; }

    private int EffectiveCap => CurrentCap > 0 ? Math.Min(CurrentCap, Cap) : Cap;

    // ---------------------------------------------------------------
    // M8.2（`m8_roadmap §2`）：**疾病**（跨趟状态，与士气同层）—— 长线损耗，由 Sanitarium 清除
    // ---------------------------------------------------------------

    private readonly Dictionary<string, HashSet<string>> _diseases = new();

    /// <summary>某英雄当前所患疾病（id 集合）。</summary>
    public IReadOnlyCollection<string> DiseasesOf(string heroId)
    {
        _ = MoraleOf(heroId); // 顺带校验英雄存在
        return _diseases.TryGetValue(heroId, out HashSet<string>? set) ? set : Array.Empty<string>();
    }

    /// <summary>患一种疾病（重复患病 = no-op）；变更写事件。</summary>
    public bool Infect(CombatLog log, string heroId, string diseaseId, string reason = "run_end")
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        _ = MoraleOf(heroId);
        if (!_diseases.TryGetValue(heroId, out HashSet<string>? set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            _diseases[heroId] = set;
        }

        if (!set.Add(diseaseId))
        {
            return false; // 已患 ⇒ 不重复
        }

        log.Append(new HeroDiseasedEvent(heroId, diseaseId, reason));
        return true;
    }

    /// <summary>治愈（由 Sanitarium 在扣费后调用）；未患即 no-op。</summary>
    public bool Cure(CombatLog log, string heroId, string diseaseId, int goldCost, string heirloomCost)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (!_diseases.TryGetValue(heroId, out HashSet<string>? set) || !set.Remove(diseaseId))
        {
            return false;
        }

        log.Append(new HeroCuredEvent(heroId, diseaseId, goldCost, heirloomCost));
        return true;
    }

    // ---------------------------------------------------------------
    // M8.2 / V15（`m8_roadmap §2`）：**特质可变** —— 负面可"除"、正面可"锁"
    // 🔴 规则：清除后**不得低于 `SanitariumConfig.MinTraitsPerHero` 条**（与 7.7「每人 2~3 条」一致）
    // ---------------------------------------------------------------

    private readonly Dictionary<string, List<HeroTraitConfig>> _traits = new();
    private readonly HashSet<string> _lockedTraits = new(); // "heroId|traitId"

    /// <summary>某英雄**当前**特质（可变；初始来自 `roster.json`）。</summary>
    public IReadOnlyList<HeroTraitConfig> TraitsOf(string heroId)
    {
        if (!_traits.TryGetValue(heroId, out List<HeroTraitConfig>? list))
        {
            HeroConfig hero = _heroes.First(h => h.Id == heroId);
            list = hero.Traits.ToList();
            _traits[heroId] = list;
        }

        return list;
    }

    /// <summary>该特质是否已固化（固化后不可清除）。</summary>
    public bool IsTraitLocked(string heroId, string traitId) => _lockedTraits.Contains($"{heroId}|{traitId}");

    /// <summary>
    /// 🔴 **运行时加一个特质**（`curio.md` §3 #4 书堆：25% ⇒ **随机正面特质**）。
    /// 走的是**既有的可变特质列表**（`_traits`，与 `RemoveTrait`/`LockTrait` 同一份）
    /// ⇒ `TraitsOf` / `TraitEffectsOf` / 战斗投影**自动生效**（无需新管道）✓。
    /// 重复加同一 id = **no-op**（与 `Infect` 的"重复患病 = no-op"一致）；变更**留痕**。
    /// </summary>
    public bool AddTrait(CombatLog log, string heroId, HeroTraitConfig trait, string reason)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (trait is null)
        {
            throw new ArgumentNullException(nameof(trait));
        }

        List<HeroTraitConfig> list = _traits.TryGetValue(heroId, out List<HeroTraitConfig>? l)
            ? l
            : TraitsOf(heroId).ToList();
        if (list.Any(t => t.Id == trait.Id))
        {
            return false; // 已有 ⇒ no-op（不重复加）
        }

        list.Add(trait);
        _traits[heroId] = list;
        log.Append(new Darkest.Core.Events.EffectEvent(default,
            $"trait_added:{heroId}:{trait.Id}:{reason}", 100.0, true));
        return true;
    }

    /// <summary>负面特质（伤害↓ 或 受士气伤害↑），未固化者优先。</summary>
    public HeroTraitConfig? FindRemovableNegativeTrait(string heroId)
        => TraitsOf(heroId).FirstOrDefault(t => (t.DamagePct < 0 || t.MoraleDamagePct > 0) && !IsTraitLocked(heroId, t.Id));

    /// <summary>正面特质（伤害↑ 或 受士气伤害↓），未固化者。</summary>
    public HeroTraitConfig? FindLockablePositiveTrait(string heroId)
        => TraitsOf(heroId).FirstOrDefault(t => (t.DamagePct > 0 || t.MoraleDamagePct < 0) && !IsTraitLocked(heroId, t.Id));

    /// <summary>清除一个负面特质（**不得使特质数低于下限**）；由 Sanitarium 在扣费后调用。</summary>
    public bool RemoveTrait(CombatLog log, string heroId, string traitId, int goldCost, string heirloomCost)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        List<HeroTraitConfig> list = _traits.TryGetValue(heroId, out List<HeroTraitConfig>? l)
            ? l
            : TraitsOf(heroId).ToList();

        if (list.Count <= SanitariumConfig.MinTraitsPerHero)
        {
            return false; // 🔴 不得降到下限以下（V15）
        }

        int idx = list.FindIndex(t => t.Id == traitId);
        if (idx < 0)
        {
            return false;
        }

        list.RemoveAt(idx);
        log.Append(new TraitRemovedEvent(heroId, traitId, goldCost, heirloomCost));
        return true;
    }

    /// <summary>固化一个正面特质（固化后不可清除）；由 Sanitarium 在扣费后调用。</summary>
    public bool LockTrait(CombatLog log, string heroId, string traitId, int goldCost, string heirloomCost)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (!TraitsOf(heroId).Any(t => t.Id == traitId) || !_lockedTraits.Add($"{heroId}|{traitId}"))
        {
            return false;
        }

        log.Append(new TraitLockedEvent(heroId, traitId, goldCost, heirloomCost));
        return true;
    }

    /// <summary>某英雄**当前**特质的合计效果（供投影；**清除/固化后立即反映**）。</summary>
    public TraitEffects TraitEffectsOf(string heroId)
    {
        int dmg = 0, morale = 0;
        foreach (HeroTraitConfig t in TraitsOf(heroId))
        {
            dmg += t.DamagePct;
            morale += t.MoraleDamagePct;
        }

        return new TraitEffects(dmg, morale);
    }

    /// <summary>
    /// **招募**（M8.0 ⑤ / `#283` 硬要求③）：**免费**；新兵 `level == 1`、`morale == 50`（**不比老的强**）；
    /// 特质从既有英雄的特质池里取**一正一负**（与 7.7「小幅、正负都有」一致）。
    /// 满员即拒绝（返回 null；**不静默顶替**）。
    /// </summary>
    public HeroConfig? Recruit(CombatLog log, StagecoachConfig coach, string archetype, string name,
        int? rookieLevel = null)   // 🆕 马车升级的"新兵起始等级"（`EffectiveRookieLevel` ⇒ **真正应用** ✓）
    {
        if (log is null || coach is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (_heroes.Count >= EffectiveCap)
        {
            return null; // 名册已满（P22 ⑥ / cap = 12）—— 拒绝，不悄悄顶替
        }

        List<HeroTraitConfig> pool = _heroes.SelectMany(h => h.Traits).ToList();
        HeroTraitConfig? pos = pool.FirstOrDefault(t => t.DamagePct > 0 || t.MoraleDamagePct < 0);
        HeroTraitConfig? neg = pool.FirstOrDefault(t => t.DamagePct < 0 || t.MoraleDamagePct > 0);
        if (pos is null || neg is null)
        {
            throw new InvalidOperationException($"{RosterConfig.ResPath}: 特质池里没有可用的正/负特质（P22 ③）。");
        }

        var rookie = new HeroConfig(
            $"hero_{archetype}_r{++_recruitSeq}",
            name,
            archetype,
            // 🔴 应用"新兵起始等级"：**马车升级真的改变新兵起点**（此前只被 UI 打印、**没被消费** ⚠️ ⇒ 已接）✓
            rookieLevel ?? coach.RookieLevel,
            new[] { pos, neg },
            coach.RookieMorale);

        _heroes.Add(rookie);
        _morale[rookie.Id] = rookie.Morale;
        log.Append(new HeroRecruitedEvent(rookie.Id, rookie.Name, rookie.Archetype, rookie.Level, rookie.Morale, coach.RecruitCost));
        return rookie;
    }

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
