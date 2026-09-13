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
/// M8.2 **V14**（`#293`，🔴🔴 最关键）：**疾病必须真的影响战斗** ——
/// 惩罚要**投影到单位**，而不是只记在 `Roster` 里（那会再次落入"写了但没接上" = 红线 21）。
/// </summary>
[TestClass]
public sealed class DiseaseProjectionTests
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

    [TestMethod]
    public void V14_DiseasePenalty_ProjectsOntoUnit_NotJustRoster()
    {
        SanitariumConfig cfg = Sani();
        DiseasePenalty p = cfg.Disease("disease_rot").Penalty; // HP-4 / 攻击-1

        var director = MonteCarlo.HeadlessDriver.NewDirector(new CombatLog());
        var unit = director.Player.UnitsInSlotOrder().First();
        int hpBefore = unit.MaxHp, atkBefore = unit.EffectiveAttack;

        HeroProjection.ApplyDisease(p, unit);

        Assert.AreEqual(hpBefore + p.HpDelta, unit.MaxHp, $"🔴 V14：疾病 HP 惩罚必须落到单位（{hpBefore} 到 {unit.MaxHp}）");
        Assert.AreEqual(atkBefore + p.AttackDelta, unit.EffectiveAttack, "🔴 V14：疾病攻击惩罚必须落到单位");
        Assert.IsTrue(unit.CurrentHp <= unit.MaxHp, "当前 HP 不得超过新的上限");
    }

    [TestMethod]
    public void V14_DiseaseChangesBattleReadings_SameArchetype()
    {
        SanitariumConfig cfg = Sani();
        DiseasePenalty rot = cfg.Disease("disease_rot").Penalty;
        DiseasePenalty creep = cfg.Disease("disease_creep").Penalty; // 攻击-2

        int Healthy(long seed) => RunWith(seed, null);
        int Sick(long seed, DiseasePenalty p) => RunWith(seed, p);

        const long seed = 20260909L;
        int healthy = Healthy(seed);
        int rotDamage = Sick(seed, rot);
        int creepDamage = Sick(seed, creep);

        string report = $"[M8.2] V14 疾病影响战斗（同原型 / 同 seed / 同策略，唯一差异 = 是否患病的投影）：" +
                        $"健康 ⇒ 我方伤害 {healthy}；患腐疮（HP−4/攻击−1）⇒ {rotDamage}；患蚁行（攻击−2）⇒ {creepDamage}";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(rotDamage <= healthy, "患病不得比健康更强");
        Assert.IsTrue(creepDamage <= healthy, "攻击−2 不得比健康更强");
        Assert.IsTrue(rotDamage != healthy || creepDamage != healthy, "🔴 V14：患病必须改变战斗读数（否则只是账本数字）");
    }

    [TestMethod]
    public void V14_TotalPenalty_StacksAcrossDiseases()
    {
        SanitariumConfig cfg = Sani();
        Roster roster = new(RosterConfig.Parse(ReadData("roster.json")));
        var log = new CombatLog();
        string hero = roster.Heroes[0].Id;

        Assert.IsTrue(Sanitarium.TotalPenalty(cfg, roster, hero).IsNone, "未患病 ⇒ 无惩罚");

        roster.Infect(log, hero, "disease_rot", "test");
        DiseasePenalty one = Sanitarium.TotalPenalty(cfg, roster, hero);
        roster.Infect(log, hero, "disease_creep", "test");
        DiseasePenalty two = Sanitarium.TotalPenalty(cfg, roster, hero);

        Assert.AreEqual(cfg.Disease("disease_rot").Penalty.AttackDelta + cfg.Disease("disease_creep").Penalty.AttackDelta,
            two.AttackDelta, "多种疾病的攻击惩罚**累加**");
        Assert.AreEqual(cfg.Disease("disease_rot").Penalty.HpDelta + cfg.Disease("disease_creep").Penalty.HpDelta,
            two.HpDelta, "多种疾病的 HP 惩罚**累加**");
        Assert.IsTrue(two.HpDelta <= one.HpDelta, "叠加后 HP 惩罚不减轻");
    }

    private static int RunWith(long seed, DiseasePenalty? penalty)
    {
        var log = new CombatLog();
        var rng = new RngProvider(seed);
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);
        var hero = director.Player.UnitsInSlotOrder().First();
        var heroId = hero.Id;
        if (penalty is not null)
        {
            HeroProjection.ApplyDisease(penalty, hero);
        }

        for (int round = 0; round < 30 && !director.IsBattleOver; round++)
        {
            director.RunFullRound(rng, unit => MonteCarlo.Policies.DecideForUnit(
                MonteCarlo.PolicyKind.SemiRandom, unit, director, rng));
        }

        return log.Events.OfType<DamageEvent>().Where(e => e.Attacker == heroId).Sum(e => e.Amount);
    }

    public TestContext TestContext { get; set; } = null!;
}
