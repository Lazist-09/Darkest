using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.2 **V15**（`#293`）：**清除有代价且有出口** —— 三服务**全耗金钱 + 传家宝** · **不部分扣** ·
/// 🔴 **特质可变且不得降到下限** · **清除后投影立即反映**（否则又是"写了但没接上" = 红线 21）。
/// </summary>
[TestClass]
public sealed class TraitMutationTests
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

    private static SanitariumConfig Sani() => SanitariumConfig.Parse(ReadData("sanitarium.json"));

    private static EconomyConfig Econ() => EconomyConfig.Parse(ReadData("economy.json"));

    private static HeirloomConfig Heirlooms() => HeirloomConfig.Parse(ReadData("heirlooms.json"));

    private static Roster NewRoster() => new(RosterConfig.Parse(ReadData("roster.json")));

    private static (Economy Econ, HeirloomStock H, CombatLog Log) Rich(SanitariumConfig cfg)
    {
        var log = new CombatLog();
        var econ = new Economy(Econ(), gold: 100);
        var h = new HeirloomStock(Heirlooms());
        foreach (string k in h.Kinds)
        {
            h.Add(log, k, 20, "test");
        }

        return (econ, h, log);
    }

    [TestMethod]
    public void V15_RemoveNegativeTrait_SpendsBoth_AndShowsUpInProjection()
    {
        SanitariumConfig cfg = Sani();
        Roster roster = NewRoster();
        (Economy econ, HeirloomStock h, CombatLog log) = Rich(cfg);
        string hero = roster.Heroes.First(hh => hh.Traits.Any(t => t.DamagePct < 0)).Id;

        TraitEffects before = roster.TraitEffectsOf(hero);
        int goldBefore = econ.Gold;

        CureOutcome o = Sanitarium.RemoveNegativeTrait(log, cfg, econ, h, roster, hero);

        Assert.IsTrue(o.Paid, "钱与传家宝都够 ⇒ 成交");
        Assert.AreEqual(goldBefore - o.GoldSpent, econ.Gold, "扣钱");
        Assert.IsTrue(log.Events.OfType<TraitRemovedEvent>().Any(), "必写事件");
        Assert.AreNotEqual(before.DamagePct, roster.TraitEffectsOf(hero).DamagePct,
            "🔴 V15：**清除后投影立即反映**（特质效果变了）");
    }

    [TestMethod]
    public void V15_TraitCount_NeverDropsBelowFloor()
    {
        SanitariumConfig cfg = Sani();
        Roster roster = NewRoster();
        (Economy econ, HeirloomStock h, CombatLog log) = Rich(cfg);
        string hero = roster.Heroes.First(hh => hh.Traits.Any(t => t.DamagePct < 0)).Id;

        int guard = 0;
        while (Sanitarium.RemoveNegativeTrait(log, cfg, econ, h, roster, hero).Paid && guard++ < 10)
        {
            // 一直清到清不动为止
        }

        Assert.IsTrue(roster.TraitsOf(hero).Count >= SanitariumConfig.MinTraitsPerHero,
            $"🔴 特质数不得低于下限 {SanitariumConfig.MinTraitsPerHero}（实际 {roster.TraitsOf(hero).Count}）");
        Assert.IsFalse(Sanitarium.RemoveNegativeTrait(log, cfg, econ, h, roster, hero).Paid,
            "到下限后必须拒绝（不得把英雄清成「无特质」）");
    }

    [TestMethod]
    public void V15_LockPositiveTrait_MakesItPermanent()
    {
        SanitariumConfig cfg = Sani();
        Roster roster = NewRoster();
        (Economy econ, HeirloomStock h, CombatLog log) = Rich(cfg);
        string hero = roster.Heroes.First(hh => hh.Traits.Any(t => t.DamagePct > 0)).Id;
        string positive = roster.TraitsOf(hero).First(t => t.DamagePct > 0).Id;

        Assert.IsTrue(Sanitarium.LockPositiveTrait(log, cfg, econ, h, roster, hero).Paid, "锁正面特质成交");
        Assert.IsTrue(roster.IsTraitLocked(hero, positive), "已固化");
        Assert.IsTrue(log.Events.OfType<TraitLockedEvent>().Any(), "必写事件");

        // 固化后不再可被"锁"（重复锁无效）；且它不会被"除负面"挑走（它是正面）
        Assert.IsFalse(Sanitarium.LockPositiveTrait(log, cfg, econ, h, roster, hero).Paid,
            "同一个已被固化的正面特质不会重复成交");
        Assert.IsFalse(roster.FindRemovableNegativeTrait(hero)?.Id == positive,
            "正面特质不会被「除负面」挑走");
    }

    [TestMethod]
    public void V15_RefusedWhenPoor_NoPartialSpend()
    {
        SanitariumConfig cfg = Sani();
        Roster roster = NewRoster();
        string hero = roster.Heroes.First(hh => hh.Traits.Any(t => t.DamagePct < 0)).Id;

        var log = new CombatLog();
        var brokeEcon = new Economy(Econ(), gold: 0);
        var richH = new HeirloomStock(Heirlooms());
        richH.Add(log, "portraits", 20, "test");

        CureOutcome refused = Sanitarium.RemoveNegativeTrait(log, cfg, brokeEcon, richH, roster, hero);
        Assert.IsFalse(refused.Paid, "钱不够 ⇒ 拒绝");
        Assert.AreEqual(20, richH.Count("portraits"), "🔴 拒绝时不得部分扣传家宝");
        Assert.AreEqual(100, new Economy(Econ(), gold: 100).Gold, "（对照：富有的情形不受影响）");
    }
}
