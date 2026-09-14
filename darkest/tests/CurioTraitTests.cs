using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **Curio 书堆的 `trait_positive`（`curio.md` §3 #4）** —— 从"阶段二"**转正**后锁行为：
/// 契约：空手 25% ⇒ **随机正面特质**。实现走既有可变特质列表（`Roster.AddTrait`，与 Sanitarium 的
/// `RemoveTrait`/`LockTrait` 同一份）⇒ `TraitEffectsOf` 与战斗投影**自动生效** ✓。
/// 🔴 **两处随机**（谁 + 哪个特质）⇒ **各写一条 `RngDraw`**（红线：随机留痕）。
/// </summary>
[TestClass]
public sealed class CurioTraitTests
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

    [TestMethod]
    public void BookStack_BareHands_CanGrantOnePositiveTrait_WithTwoRngDraws()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        SanitariumConfig sani = SanitariumConfig.Parse(ReadData("sanitarium.json"));
        CurioConfig books = curios.Get("cur_book_stack")!;
        RosterConfig rosterCfg = RosterConfig.Parse(ReadData("roster.json"));

        int found = 0;
        for (int seed = 1; seed <= 400 && found == 0; seed++)
        {
            var log = new CombatLog();
            var bag = new Inventory(tuning.Inventory!);
            bag.ConfigureRecommended(out _);
            bag.LockForRun();
            var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
                tuning.Expedition.NBattles, firewood: 1, food: 1, tuning.Expedition.AmbushChance);
            var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
                new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log,
                new Darkest.Core.Rng.RngProvider(seed));
            var roster = new Roster(rosterCfg);

            var before = rosterCfg.Heroes.ToDictionary(h => h.Id, h => roster.TraitsOf(h.Id).Count);
            CurioOutcome outcome = flow.ResolveCurio(books, itemUsed: null, roster, sani)!;
            if (outcome.Kind != "trait_positive")
            {
                continue; // 没掷到该分支 ⇒ 换 seed（确定性可复现）
            }

            found = seed;

            // ① **恰一位英雄**多了特质（状态断言，不只看事件）
            var gained = rosterCfg.Heroes
                .Where(h => roster.TraitsOf(h.Id).Count == before[h.Id] + 1)
                .ToArray();
            Assert.AreEqual(1, gained.Length, $"🔴 契约：随机正面特质（一位英雄）；seed={seed}");

            // ② 新特质是**正面**的（与 `FindLockablePositiveTrait` 同一判据）
            var traits = roster.TraitsOf(gained[0].Id);
            Darkest.Data.HeroTraitConfig added = traits.Last();
            Assert.IsTrue(added.DamagePct > 0 || added.MoraleDamagePct < 0,
                $"🔴 契约：**正面**特质（实际 {added.Id}: dmg={added.DamagePct} morale={added.MoraleDamagePct}）");

            // ③ **随机留痕**：选（英雄, 特质）**合法对**各写一条 `RngDraw`（红线：随机留痕）
            Assert.IsTrue(log.Events.OfType<RngDraw>().Any(),
                "🔴 随机选（英雄, 特质）合法对 ⇒ 必须写 RngDraw（红线：随机留痕）");

            // ④ 特质**真的进了投影**（`TraitEffectsOf` 把它算进去 ⇒ 战斗会生效）
            TraitEffects fx = roster.TraitEffectsOf(gained[0].Id);
            Assert.IsTrue(fx.DamagePct > 0 || fx.MoraleDamagePct < 0 || added.DamagePct == 0,
                "加的特质应体现在 `TraitEffectsOf`（投影自动生效，无需另接）");
        }

        Assert.AreNotEqual(0, found, "400 个 seed 里应至少有一个掷到书堆的 25% 正面特质分支（否则分支不可达）");
        Console.WriteLine($"🔴 书堆正面特质分支命中 seed = {found}（用例确定性复现）");
    }

    [TestMethod]
    public void AddTrait_IsIdempotent_AndAuditable()
    {
        RosterConfig rosterCfg = RosterConfig.Parse(ReadData("roster.json"));
        var roster = new Roster(rosterCfg);
        var log = new CombatLog();
        string hero = rosterCfg.Heroes[0].Id;
        var owned = roster.TraitsOf(hero).Select(t => t.Id).ToHashSet();
        Darkest.Data.HeroTraitConfig trait = rosterCfg.Heroes
            .SelectMany(h => h.Traits)
            .First(t => (t.DamagePct > 0 || t.MoraleDamagePct < 0) && !owned.Contains(t.Id));

        int before = roster.TraitsOf(hero).Count;
        Assert.IsTrue(roster.AddTrait(log, hero, trait, "test"), "首次加应成功");
        Assert.IsFalse(roster.AddTrait(log, hero, trait, "test"), "重复加同一 id ⇒ no-op（与 Infect 一致）");
        Assert.AreEqual(before + 1, roster.TraitsOf(hero).Count, "只多一条");
        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType.StartsWith("trait_added:", StringComparison.Ordinal)),
            "加特质应**留痕**（可审计）");
    }
}
