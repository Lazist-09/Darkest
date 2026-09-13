using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>一次治疗后返回：是否成交、花的钱与传家宝（未成交时全 0）。</summary>
public sealed record CureOutcome(bool Paid, int GoldSpent, string HeirloomSpent);

/// <summary>
/// M8.2（`m8_roadmap §2`）：**疾病获取 + Sanitarium 治病**。
///
/// 🔴 设计（照规格）：
/// · **疾病 = 长线损耗**：**每趟结束按概率获得**（`contract_chance_per_run`），效果为**属性惩罚**；
/// · **Sanitarium** = 清除出口：**消耗金钱 + 传家宝**（传家宝是 M8.1 的长线资源 ⇒ 有稳定去路）；
/// · 随机必写 `RngDraw`（红线）；结算必写事件（数字来自事件流）；**钱/传家宝不足即拒绝且不扣**。
/// </summary>
public static class Sanitarium
{
    /// <summary>
    /// **本趟结束的患病判定**（每位英雄 × 每种疾病各掷一次；`roll &lt; chance × 100` 即患病）。
    /// 返回本趟新增的患者（`(heroId, diseaseId)`）。
    /// </summary>
    public static IReadOnlyList<(string HeroId, string DiseaseId)> RollContract(
        CombatLog log, IRngProvider rng, SanitariumConfig cfg, Roster roster, IEnumerable<string> heroIds)
    {
        if (log is null || rng is null || cfg is null || roster is null)
        {
            throw new ArgumentNullException(nameof(cfg));
        }

        var infected = new List<(string, string)>();
        foreach (string heroId in heroIds)
        {
            foreach (DiseaseDef d in cfg.Diseases)
            {
                double roll = rng.NextPercent();
                log.Append(new RngDraw(rng.DrawCount, roll)); // 🔴 随机必写
                if (roll < (d.ContractChancePerRun * 100.0) && roster.Infect(log, heroId, d.Id, "run_end"))
                {
                    infected.Add((heroId, d.Id));
                }
            }
        }

        return infected;
    }

    /// <summary>
    /// **治病**（Sanitarium）：先校验钱与传家宝都够（**不足即拒绝且不扣**），再扣费并治愈。
    /// </summary>
    public static CureOutcome CureDisease(CombatLog log, SanitariumConfig cfg, Economy economy,
        HeirloomStock heirlooms, Roster roster, string heroId, string diseaseId)
    {
        if (log is null || cfg is null || economy is null || heirlooms is null || roster is null)
        {
            throw new ArgumentNullException(nameof(cfg));
        }

        if (!roster.DiseasesOf(heroId).Contains(diseaseId, StringComparer.Ordinal))
        {
            return new CureOutcome(false, 0, string.Empty); // 没这个病 ⇒ 不收费
        }

        SanitariumService s = cfg.Service("cure_disease");

        // 🔴 先整体校验（避免"扣了钱才发现传家宝不够"的部分扣费）
        if (economy.Gold < s.Gold)
        {
            return new CureOutcome(false, 0, string.Empty);
        }

        foreach ((string kind, int need) in s.Heirlooms)
        {
            if (heirlooms.Count(kind) < need)
            {
                return new CureOutcome(false, 0, string.Empty);
            }
        }

        economy.TrySpend(log, s.Gold, $"sanitarium:cure_disease:{diseaseId}");
        foreach ((string kind, int need) in s.Heirlooms)
        {
            heirlooms.Add(log, kind, -need, $"sanitarium:cure_disease:{diseaseId}");
        }

        string cost = string.Join(",", s.Heirlooms.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}:{k.Value}"));
        roster.Cure(log, heroId, diseaseId, s.Gold, cost);
        return new CureOutcome(true, s.Gold, cost);
    }

    /// <summary>疾病惩罚合计（供投影读取：疾病是**长线损耗**，装配时作用到单位）。</summary>
    public static DiseasePenalty TotalPenalty(SanitariumConfig cfg, Roster roster, string heroId)
    {
        int hp = 0, atk = 0, morale = 0;
        foreach (string id in roster.DiseasesOf(heroId))
        {
            DiseasePenalty p = cfg.Disease(id).Penalty;
            hp += p.HpDelta;
            atk += p.AttackDelta;
            morale += p.MoraleDelta;
        }

        return new DiseasePenalty(hp, atk, morale);
    }

    /// <summary>**除负面特质**（V15）：先整体校验（钱与传家宝都够 + 不会把特质降到下限）再扣费。</summary>
    public static CureOutcome RemoveNegativeTrait(CombatLog log, SanitariumConfig cfg, Economy economy,
        HeirloomStock heirlooms, Roster roster, string heroId)
    {
        HeroTraitConfig? trait = roster.FindRemovableNegativeTrait(heroId);
        if (trait is null || roster.TraitsOf(heroId).Count <= SanitariumConfig.MinTraitsPerHero)
        {
            return new CureOutcome(false, 0, string.Empty);
        }

        SanitariumService s = cfg.Service("remove_negative_trait");
        if (!CanPay(economy, heirlooms, s))
        {
            return new CureOutcome(false, 0, string.Empty);
        }

        Pay(log, economy, heirlooms, s, "sanitarium:remove_negative_trait");
        string cost = string.Join(",", s.Heirlooms.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}:{k.Value}"));
        bool ok = roster.RemoveTrait(log, heroId, trait.Id, s.Gold, cost);
        return ok ? new CureOutcome(true, s.Gold, cost) : new CureOutcome(false, 0, string.Empty);
    }

    /// <summary>**锁正面特质**（V15）：先整体校验再扣费。</summary>
    public static CureOutcome LockPositiveTrait(CombatLog log, SanitariumConfig cfg, Economy economy,
        HeirloomStock heirlooms, Roster roster, string heroId)
    {
        HeroTraitConfig? trait = roster.FindLockablePositiveTrait(heroId);
        if (trait is null)
        {
            return new CureOutcome(false, 0, string.Empty);
        }

        SanitariumService s = cfg.Service("lock_positive_trait");
        if (!CanPay(economy, heirlooms, s))
        {
            return new CureOutcome(false, 0, string.Empty);
        }

        Pay(log, economy, heirlooms, s, "sanitarium:lock_positive_trait");
        string cost = string.Join(",", s.Heirlooms.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}:{k.Value}"));
        bool ok = roster.LockTrait(log, heroId, trait.Id, s.Gold, cost);
        return ok ? new CureOutcome(true, s.Gold, cost) : new CureOutcome(false, 0, string.Empty);
    }

    private static bool CanPay(Economy economy, HeirloomStock heirlooms, SanitariumService s)
        => economy.Gold >= s.Gold && s.Heirlooms.All(kv => heirlooms.Count(kv.Key) >= kv.Value);

    private static void Pay(CombatLog log, Economy economy, HeirloomStock heirlooms, SanitariumService s, string reason)
    {
        economy.TrySpend(log, s.Gold, reason);
        foreach ((string kind, int need) in s.Heirlooms)
        {
            heirlooms.Add(log, kind, -need, reason);
        }
    }
}
