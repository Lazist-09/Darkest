using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Sim.Save;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 `Roster` 的**存档存取**（`Phase 1`）—— `Roster` 的 partial 另一半。
///
/// <para>**为什么在这里而不是独立静态类**：快照要读写 `_heroes`/`_morale`/`_xp` 等**私有字段**，
/// 静态类拿不到 ⇒ 用 partial 拿到私有访问权，同时**不撑大 `Roster.cs`**（已 469 行）✓</para>
///
/// <para>**分工**：本文件只做「**状态 ⇄ 快照**」的搬运；
/// **序列化**归 `SaveSerializer`、**迁移**归 `SaveMigrator`、**落盘**归 scene 层 `SaveFileGateway` ✓</para>
/// </summary>
public sealed partial class Roster
{
    /// <summary>
    /// 🔴 **存**：把当前名册状态搬进快照（**纯读取，不改任何状态**）。
    /// </summary>
    public RosterSnapshot CaptureSnapshot()
    {
        // 🔴 `Heroes` 存**完整 HeroConfig**（含招募新兵 —— 新兵不在 roster.json 里）✓
        IReadOnlyList<HeroConfig> heroes = _heroes.ToList();

        var diseases = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach ((string heroId, HashSet<string> set) in _diseases)
        {
            diseases[heroId] = set.ToList();
        }

        var quirks = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach ((string heroId, HashSet<string> set) in _quirks)
        {
            quirks[heroId] = set.ToList();
        }

        // 🆕 M4u（v4）：饰品**保序**搬运（List ⇒ 槽位稳定 ⇒ 存档可 diff；字典键序在 JSON 里不保证）✓
        var trinkets = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach ((string heroId, List<string> list) in _trinkets)
        {
            trinkets[heroId] = list.ToList();
        }

        var traits = new Dictionary<string, IReadOnlyList<HeroTraitConfig>>(StringComparer.Ordinal);
        foreach ((string heroId, List<HeroTraitConfig> list) in _traits)
        {
            traits[heroId] = list.ToList();
        }

        IReadOnlyList<GraveyardSnapshot> graveyard =
            _graveyard.Select(g => new GraveyardSnapshot(g.HeroId, g.Name, g.Level, g.Cause)).ToList();

        return new RosterSnapshot(
            heroes,
            new Dictionary<string, int>(_morale, StringComparer.Ordinal),
            new Dictionary<string, int>(_xp, StringComparer.Ordinal),
            diseases,
            quirks,
            trinkets,
            traits,
            _lockedTraits.ToList(),
            graveyard,
            _recruitSeq,
            CurrentCap,
            new Dictionary<string, int>(_pendingOpeningPenalty, StringComparer.Ordinal));
    }

    /// <summary>
    /// 🔴 **取**：用快照**覆盖**当前名册状态（**只搬状态，不写事件** —— 恢复不是游戏事件）。
    /// <para>🔴 **先清后填**：所有容器都是 `readonly` 字段 ⇒ 只能 `Clear()` 再 `Add`，
    /// 不能整体替换引用（否则恢复后读到的还是旧容器）✓</para>
    /// </summary>
    public void RestoreFrom(RosterSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        _heroes.Clear();
        foreach (HeroConfig hero in snapshot.Heroes)
        {
            _heroes.Add(hero);
        }

        // 🔴 士气**必须每个英雄都有**：`MoraleOf` 缺键会抛 ⇒ 快照里没有的（异常档）按新兵基准补，
        //    这不是"静默兜底"而是"**让对象回到合法态**"（否则恢复后第一次读士气就崩）✓
        _morale.Clear();
        foreach (HeroConfig hero in _heroes)
        {
            _morale[hero.Id] = snapshot.Morale.TryGetValue(hero.Id, out int m) ? m : hero.Morale;
        }

        Replace(_xp, snapshot.Xp);
        Replace(_pendingOpeningPenalty, snapshot.PendingOpeningPenalty);

        _diseases.Clear();
        foreach ((string heroId, IReadOnlyList<string> ids) in snapshot.Diseases)
        {
            _diseases[heroId] = new HashSet<string>(ids, StringComparer.Ordinal);
        }

        _quirks.Clear();
        foreach ((string heroId, IReadOnlyList<string> ids) in snapshot.Quirks)
        {
            _quirks[heroId] = new HashSet<string>(ids, StringComparer.Ordinal);
        }

        // 🆕 M4u（v4）：饰品按【列表】恢复（保序 = 槽位 1/2 与存档时一致）✓
        _trinkets.Clear();
        foreach ((string heroId, IReadOnlyList<string> ids) in snapshot.Trinkets)
        {
            _trinkets[heroId] = new List<string>(ids);
        }

        _traits.Clear();
        foreach ((string heroId, IReadOnlyList<HeroTraitConfig> list) in snapshot.Traits)
        {
            _traits[heroId] = list.ToList();
        }

        _lockedTraits.Clear();
        foreach (string key in snapshot.LockedTraits)
        {
            _lockedTraits.Add(key);
        }

        _graveyard.Clear();
        foreach (GraveyardSnapshot g in snapshot.Graveyard)
        {
            _graveyard.Add((g.HeroId, g.Name, g.Level, g.Cause));
        }

        // 🔴 招募序号**必须恢复**：新兵 id 形如 `hero_x_r{N}` ⇒ 不恢复就会**撞 id** ✓
        _recruitSeq = snapshot.RecruitSeq;
        CurrentCap = snapshot.CurrentCap;
    }

    /// <summary>把 `source` 的键值整体搬进 `target`（先清后填，保持容器引用不变）✓</summary>
    private static void Replace(Dictionary<string, int> target, IReadOnlyDictionary<string, int> source)
    {
        target.Clear();
        foreach ((string key, int value) in source)
        {
            target[key] = value;
        }
    }
}
