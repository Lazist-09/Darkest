using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.2（`m8_roadmap §2` + **P24**）：疾病 = **长线损耗**（每趟结束按概率获得、惩罚可测）；
/// Sanitarium = 清除出口（**消耗金钱 + 传家宝**；不足即拒绝且不扣）；随机必写 `RngDraw`。
/// </summary>
[TestClass]
public sealed class SanitariumTests
{
    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    private static SanitariumConfig Cfg() => SanitariumConfig.Parse(ReadData("sanitarium.json"));

    private static EconomyConfig Econ() => EconomyConfig.Parse(ReadData("economy.json"));

    private static HeirloomConfig Heirlooms() => HeirloomConfig.Parse(ReadData("heirlooms.json"));

    private static Roster NewRoster() => new(RosterConfig.Parse(ReadData("roster.json")));

    [TestMethod]
    public void P24_DiseasesMeasurable_AndThreeServicesSpendHeirlooms()
    {
        SanitariumConfig c = Cfg();

        Assert.IsTrue(c.Diseases.Count >= 3, "至少 3 种疾病（P24 ①）");
        Assert.IsTrue(c.Diseases.All(d => !d.Penalty.IsNone), "疾病惩罚不得三项全 0（可测；P24 ①）");
        Assert.IsTrue(c.Diseases.All(d => d.ContractChancePerRun is > 0 and <= 1), "患病概率 ∈ (0,1]（P24 ①）");

        foreach (string name in SanitariumConfig.RequiredServices)
        {
            SanitariumService s = c.Service(name);
            Assert.IsTrue(s.Gold > 0, $"{name} 要花钱（P24 ②）");
            Assert.IsTrue(s.Heirlooms.Count > 0 && s.Heirlooms.Values.All(v => v > 0),
                $"🔴 {name} **必须消耗传家宝**（P24 ③：它是 M8.1 传家宝的稳定出口）");
        }
    }

    [TestMethod]
    public void P24_BadSanitariumData_ThrowsOnLoad()
    {
        string raw = ReadData("sanitarium.json");

        Assert.ThrowsException<InvalidDataException>(
            () => SanitariumConfig.Parse(raw.Replace("\"hp_delta\": -4, \"attack_delta\": -1, \"morale_delta\": 0", "\"hp_delta\": 0, \"attack_delta\": 0, \"morale_delta\": 0", StringComparison.Ordinal)),
            "惩罚三项全 0 ⇒ 报错（P24 ①）");

        Assert.ThrowsException<InvalidDataException>(
            () => SanitariumConfig.Parse(raw.Replace("\"heirlooms\": { \"busts\": 2 }", "\"heirlooms\": {}", StringComparison.Ordinal)),
            "服务不消耗传家宝 ⇒ 报错（P24 ③）");
    }

    [TestMethod]
    public void Contract_WritesRngDraw_AndPenaltyIsReadable()
    {
        SanitariumConfig cfg = Cfg();
        Roster roster = NewRoster();
        var log = new CombatLog();
        var rng = new RngProvider(20260909);
        string hero = roster.Heroes[0].Id;

        // 200 趟的患病判定（只对一位英雄、一种病观察）
        int infected = 0;
        for (int i = 0; i < 200; i++)
        {
            infected += Sanitarium.RollContract(log, rng, cfg, roster, new[] { hero })
                .Count(x => x.DiseaseId == cfg.Diseases[0].Id);
        }

        int rolls = log.Events.OfType<RngDraw>().Count();
        Assert.AreEqual(200 * cfg.Diseases.Count, rolls,
            $"每人每病各掷一次且**必写 RngDraw**（200 趟 × {cfg.Diseases.Count} 种）");
        Assert.IsTrue(infected > 0, "200 趟里应至少患病一次（概率 > 0）");

        DiseasePenalty p = Sanitarium.TotalPenalty(cfg, roster, hero);
        Assert.IsTrue(!p.IsNone, $"患病后惩罚可读（HP{p.HpDelta} 攻击{p.AttackDelta} 士气{p.MoraleDelta}）");
    }

    [TestMethod]
    public void Cure_SpendsGoldAndHeirlooms_RefusedWhenPoor_NoPartialSpend()
    {
        SanitariumConfig cfg = Cfg();
        EconomyConfig ecfg = Econ();
        Roster roster = NewRoster();
        var log = new CombatLog();
        string hero = roster.Heroes[0].Id;
        roster.Infect(log, hero, "disease_rot", "test");

        // ① 钱不够 ⇒ 拒绝且不扣
        var poorEcon = new Economy(ecfg, gold: 0);
        var richHeirlooms = new HeirloomStock(Heirlooms());
        richHeirlooms.Add(log, "busts", 10, "test");
        CureOutcome refused = Sanitarium.CureDisease(log, cfg, poorEcon, richHeirlooms, roster, hero, "disease_rot");
        Assert.IsFalse(refused.Paid, "钱不够 ⇒ 拒绝");
        Assert.AreEqual(10, richHeirlooms.Count("busts"), "🔴 拒绝时**不得部分扣**传家宝");
        Assert.AreEqual(1, roster.DiseasesOf(hero).Count, "仍患病");

        // ② 传家宝不够 ⇒ 拒绝且不扣钱
        var richEcon = new Economy(ecfg, gold: 100);
        var poorHeirlooms = new HeirloomStock(Heirlooms());
        CureOutcome refused2 = Sanitarium.CureDisease(log, cfg, richEcon, poorHeirlooms, roster, hero, "disease_rot");
        Assert.IsFalse(refused2.Paid, "传家宝不够 ⇒ 拒绝");
        Assert.AreEqual(100, richEcon.Gold, "🔴 拒绝时不得扣钱（避免部分扣费）");

        // ③ 两者都够 ⇒ 成交
        CureOutcome paid = Sanitarium.CureDisease(log, cfg, richEcon, richHeirlooms, roster, hero, "disease_rot");
        Assert.IsTrue(paid.Paid, "两者都够 ⇒ 成交");
        Assert.AreEqual(100 - cfg.Service("cure_disease").Gold, richEcon.Gold, "扣钱");
        Assert.AreEqual(10 - cfg.Service("cure_disease").Heirlooms["busts"], richHeirlooms.Count("busts"), "扣传家宝");
        Assert.AreEqual(0, roster.DiseasesOf(hero).Count, "病已清除");
        Assert.IsTrue(log.Events.OfType<HeroCuredEvent>().Any(), "治愈必写事件（数字来自事件流）");
    }
}
