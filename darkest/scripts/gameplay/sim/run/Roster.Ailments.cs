using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

// ① 来源：从 `Roster.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **疾病与特质**（跨趟长期状态半；原 `:167-343` 逐字节；M11 ① 预警面第十八件·第三片）✓
// ② 职责：疾病（`DiseasesOf`／`Infect`／`Cure` —— M8.2，由 Sanitarium 在扣费后调用）＋ 可变特质（`TraitsOf`／
//    `IsTraitLocked`／`AddTrait`／`FindRemovableNegativeTrait`／`FindLockablePositiveTrait`／`RemoveTrait`／
//    `LockTrait`／`TraitEffectsOf` —— V15：清除不得低于 `MinTraitsPerHero`，固化后不可清除）✓
// ③ 🔴 依赖主类私有成员/状态（实测扫描本片）：`_heroes` x1（`TraitsOf` 首次物化取初始特质）；根片公开口 `MoraleOf` x2
//    （存在性校验 · 与 `QuirksOf`／`TrinketsOf` 同一口径）；自有状态 `_diseases` x5 ／ `_traits` x7 ／ `_lockedTraits` x3
//    （本片声明 —— 与主片同层同型，`Roster.Save.cs` 直接读写）✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 5 条；`Godot` 零引用 —— 构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
public sealed partial class Roster
{
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

}
