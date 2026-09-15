using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **Curio 圣坛的 `damage_buff`（`curio.md` §3 #5）** —— 从"阶段二"**转正**后锁行为：
/// 契约：「空手 ⇒ **本趟 +20% 伤害（到扎营）**；用支援包 ⇒ +30%」⇒
/// · **跨场**：每场开场挂到全队（不是只当场）
/// · **到期**：**扎营清**（= `until_next_recovery` 的"下次恢复" ✓ 与死门后遗症同一条链）
/// · **叠加**：取大（+20 与 +30 不叠成 +50 —— 设计检查③"不同道具给不同等级的好结果"）
/// </summary>
[TestClass]
public sealed class CurioBlessingTests
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

    private static (ExpeditionSession Session, ExpeditionFlow Flow, TuningConfig Tuning, CombatLog Log, CurioConfig Altar) NewRun()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new Darkest.Core.Rng.RngProvider(20260909));
        return (session, flow, tuning, log, curios.Get("cur_altar")!);
    }

    [TestMethod]
    public void Altar_BareHands_Grants20_AndInjectsTeamWideInNextBattle()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, CurioConfig altar) = NewRun();

        CurioOutcome outcome = flow.ResolveCurio(altar, itemUsed: null)!;
        Assert.IsFalse(outcome.Deferred, "damage_buff 已转正 ⇒ 不该再是【阶段二·未生效】");
        Assert.AreEqual(20, session.CurioDamageBlessingPct, "空手 ⇒ 本趟 +20%");

        var d = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        foreach (Darkest.Gameplay.Sim.Board.UnitRuntime u in d.Player.UnitsInSlotOrder())
        {
            Assert.IsTrue(d.Buffs.Has(u.Id, "curio_altar_blessing_20"),
                $"{u.Id} 应带圣坛祝福（**全队**，不只是出手的人）");
            Assert.AreEqual(20, d.Buffs.PercentMod(u.Id, "dealt_damage_mult"),
                "🔴 实际量级：`dealt_damage_mult` = +20（`DamageStep` 的 raw 处消费）");
        }
    }

    [TestMethod]
    public void Altar_SupplyPack_Gives30_AndTakesMax_NotStacking()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, CurioConfig altar) = NewRun();

        flow.ResolveCurio(altar, itemUsed: null);              // 先 +20
        CurioOutcome better = flow.ResolveCurio(altar, "supply_pack")!; // 再 +30
        Assert.AreEqual(30, session.CurioDamageBlessingPct, "取大 ⇒ +30（不叠成 +50）");
        Assert.IsTrue(better.Amount > 20, "用对道具更好（设计检查③）");
    }

    [TestMethod]
    public void Altar_BlessingIsClearedByCamping_AndIsAuditable()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, CurioConfig altar) = NewRun();

        flow.ResolveCurio(altar, itemUsed: null);
        Assert.AreEqual(20, session.CurioDamageBlessingPct);

        // 第 1 场：带祝福
        var d1 = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        Assert.IsTrue(d1.Buffs.Has(d1.Player.UnitsInSlotOrder().First().Id, "curio_altar_blessing_20"),
            "第 1 场应带祝福");

        // 扎营 ⇒ **清**（契约：到扎营）
        session.EndBattle(d1, 1, "PlayerVictory", rounds: 5); // 🔴 相位纪律：**必须先结束战斗**才能扎营（新相位规则拦的就是"战斗中扎营"）✓
        Assert.IsTrue(session.StartCamp(log, campIndex: 1, respiteBase: 6), "扎营应可开始");
        session.EndCamp(log);
        Assert.AreEqual(0, session.CurioDamageBlessingPct, "🔴 契约：祝福【到扎营】⇒ 扎营清它");
        Assert.IsTrue(log.Events.OfType<EffectEvent>()
                .Any(e => e.EffectType.StartsWith("camp_cleared_curio_blessing", StringComparison.Ordinal)),
            "扎营清祝福应当**留痕**（可审计）");

        // 第 2 场：**不再有**祝福
        var d2 = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        var u2 = d2.Player.UnitsInSlotOrder().First();
        Assert.IsFalse(d2.Buffs.Has(u2.Id, "curio_altar_blessing_20"), "扎营后下一场不该再有祝福");
        Assert.AreEqual(0, d2.Buffs.PercentMod(u2.Id, "dealt_damage_mult"), "且无伤害加成");
    }
}
