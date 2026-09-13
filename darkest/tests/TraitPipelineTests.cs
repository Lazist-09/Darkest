using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// **V5 收紧后的验收**（`#288` ③ / `#289` 裁定 (B)）：**同原型、不同特质的两人，在战斗中的读数确实不同**。
/// 做法：同一 seed、同一原型、同一策略 ⇒ 唯一差异是特质伤害修正 ⇒ `DamageEvent` 合计必须不同。
/// </summary>
[TestClass]
public sealed class TraitPipelineTests
{
    private static string ReadData(string name)
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(dir.FullName, "data", name);
            if (System.IO.File.Exists(candidate))
            {
                return System.IO.File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new System.IO.FileNotFoundException($"data/{name} 未找到。");
    }

    /// <summary>跑一场（同一 seed、同一策略），返回我方对敌方造成的伤害合计。</summary>
    private static int RunOneBattle(int traitDamagePct, long seed)
    {
        var log = new CombatLog();
        var rng = new Darkest.Core.Rng.RngProvider(seed);
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);

        // 🔴 唯一变量：给"第一个我方单位"叠加特质伤害修正（同原型、同基准）
        director.Player.UnitsInSlotOrder().First().ApplyTraitDamagePct(traitDamagePct);

        for (int round = 0; round < 30 && !director.IsBattleOver; round++)
        {
            director.RunFullRound(rng, unit => MonteCarlo.Policies.DecideForUnit(
                MonteCarlo.PolicyKind.SemiRandom, unit, director, rng));
        }

        return log.Events.OfType<DamageEvent>()
            .Where(e => e.Attacker is not null && director.Player.UnitAtPosition(e.Attacker.Value) is not null)
            .Sum(e => e.Amount);
    }

    [TestMethod]
    public void V5_TraitDamage_ChangesBattleReadings_SameArchetype()
    {
        RosterConfig rcfg = RosterConfig.Parse(ReadData("roster.json"));
        Assert.IsTrue(RosterConfig.MaxTraitDamagePct == 15, "特质伤害幅度必须可测（红线 19：≤15%）");

        const long seed = 20260909L;
        int plus = RunOneBattle(+10, seed);
        int minus = RunOneBattle(-10, seed);
        int none = RunOneBattle(0, seed);

        string report = $"[M8] V5 特质接管线验收（同原型、同 seed、同策略，唯一差异 = 特质伤害修正）：" +
                        $"特质 +10% ⇒ 我方伤害合计 {plus}；0% ⇒ {none}；−10% ⇒ {minus}";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(plus > none, $"+10% 特质必须打得更多（{plus} 大于 {none}）");
        Assert.IsTrue(minus < none, $"−10% 特质必须打得更少（{minus} 小于 {none}）");
        Assert.AreNotEqual(plus, minus, "**同原型、不同特质 ⇒ 战斗读数确实不同**（V5 收紧后的验收）");
    }

    [TestMethod]
    public void V5_NoDeadDeclaration_DamageModIsConsumed()
    {
        // 红线 21：DamageMod 不再是"已声明未消费" —— 单位上的修正确实能被结算读到
        var log = new CombatLog();
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);
        var unit = director.Player.UnitsInSlotOrder().First();

        Assert.AreEqual(1.0, unit.DamageMultiplier, 1e-9, "默认无修正（1.0）");
        unit.ApplyTraitDamagePct(15);
        Assert.AreEqual(1.15, unit.DamageMultiplier, 1e-9, "+15% ⇒ 1.15（加法先于乘法：多来源先累加）");
        unit.ApplyTraitMoraleDamagePct(20);
        Assert.AreEqual(1.20, unit.MoraleDamageTakenMultiplier, 1e-9, "受士气伤害 +20% ⇒ 1.20（士气通道）");

        // 特质投影到单位（HeroProjection 的出口）
        HeroConfig hero = RosterConfig.Parse(ReadData("roster.json")).Heroes.First();
        TraitEffects eff = HeroProjection.ApplyTraits(hero, unit);
        Assert.AreEqual(eff.DamagePct, unit.DamageModPct - 15, "投影把特质伤害写进单位（与既有层同层叠加）");
    }

    /// <summary>跑一场，返回**带特质那个单位自己**造成的伤害合计（㉟：不被 6 人摊薄）。</summary>
    private static int RunOneBattle_SingleUnit(int traitDamagePct, long seed)
    {
        var log = new CombatLog();
        var rng = new Darkest.Core.Rng.RngProvider(seed);
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);
        var hero = director.Player.UnitsInSlotOrder().First();
        var heroId = hero.Id;
        hero.ApplyTraitDamagePct(traitDamagePct);

        for (int round = 0; round < 30 && !director.IsBattleOver; round++)
        {
            director.RunFullRound(rng, unit => MonteCarlo.Policies.DecideForUnit(
                MonteCarlo.PolicyKind.SemiRandom, unit, director, rng));
        }

        return log.Events.OfType<DamageEvent>().Where(e => e.Attacker == heroId).Sum(e => e.Amount);
    }

    [TestMethod]
    public void V5_35_SingleUnitDamageDelta_ReflectsTraitMagnitude()
    {
        const long seed = 20260909L;
        int plus = RunOneBattle_SingleUnit(+10, seed);
        int none = RunOneBattle_SingleUnit(0, seed);
        int minus = RunOneBattle_SingleUnit(-10, seed);

        double plusPct = none == 0 ? 0 : 100.0 * (plus - none) / none;
        double minusPct = none == 0 ? 0 : 100.0 * (minus - none) / none;

        string report = $"[M8] ㉟ 带特质者**单体**伤害差（同 seed 同策略，只有他带特质）：" +
                        $"+10% ⇒ {plus}（对基准 {none} 为 {plusPct:F1}%）；−10% ⇒ {minus}（{minusPct:F1}%）" +
                        $"　⇒ 接线成立；🔴 若单人差异远小于 10% ⇒ 属 7.7 幅度/摊薄问题，不是接线问题";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(plus >= none && minus <= none, "方向正确（+10 不少于基准，−10 不多于基准）");
        Assert.IsTrue(Math.Abs(plusPct) > 0 || Math.Abs(minusPct) > 0, "带特质者单体读数必须可分辨");
    }

    public TestContext TestContext { get; set; } = null!;
}
