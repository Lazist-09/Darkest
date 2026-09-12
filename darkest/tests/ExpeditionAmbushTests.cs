using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// E5（夜袭）：扎营后 **33%** 概率触发（`tuning.expedition.ambush_chance`），
/// **每次判定必写 `RngDraw`**（确定性红线）、触发写 `AmbushTriggeredEvent`、同 seed 同结果；
/// 夜袭产生的战斗**计入"6 场皆胜"**（由 run 流程按 `AmbushCount` 计入，见 E5 验收）。
/// </summary>
[TestClass]
public sealed class ExpeditionAmbushTests
{
    private static ExpeditionSession NewSession(double chance = 0.33)
        => new(log => HeadlessDriver.NewDirector(log), targetBattles: 6, firewood: 2, food: 12, ambushChance: chance);

    [TestMethod]
    public void Ambush_Probability_About33Percent_AndWritesRngDraw()
    {
        ExpeditionSession s = NewSession();
        var log = new CombatLog();
        var rng = new RngProvider(20260909);

        int triggered = 0;
        const int runs = 3000;
        for (int i = 0; i < runs; i++)
        {
            if (s.RollAmbush(log, rng))
            {
                triggered++;
            }
        }

        double rate = (double)triggered / runs;
        Assert.IsTrue(rate is > 0.28 and < 0.38, $"夜袭概率 ≈33%（实测 {rate:P1}）");
        Assert.IsTrue(log.Events.OfType<RngDraw>().Count() >= runs, "每次夜袭判定必写 RngDraw（确定性红线）");
        Assert.AreEqual(triggered, log.Events.OfType<AmbushTriggeredEvent>().Count(), "触发次数 == AmbushTriggeredEvent 条数");
        Assert.AreEqual(triggered, s.AmbushCount, "夜袭次数计入会话（用于'计入 6 场皆胜'）");
    }

    [TestMethod]
    public void Ambush_Deterministic_SameSeedSameOutcome()
    {
        static List<bool> Run()
        {
            ExpeditionSession s = NewSession();
            var log = new CombatLog();
            var rng = new RngProvider(7777);
            var seq = new List<bool>();
            for (int i = 0; i < 200; i++)
            {
                seq.Add(s.RollAmbush(log, rng));
            }

            return seq;
        }

        CollectionAssert.AreEqual(Run(), Run(), "同 seed → 同夜袭序列（纯计数 + 固定抽取点）");
    }

    [TestMethod]
    public void Ambush_ChanceZero_NeverTriggers_ButStillRolls()
    {
        ExpeditionSession s = NewSession(chance: 0.0);
        var log = new CombatLog();
        var rng = new RngProvider(1);
        for (int i = 0; i < 100; i++)
        {
            Assert.IsFalse(s.RollAmbush(log, rng));
        }

        Assert.AreEqual(0, s.AmbushCount);
        Assert.IsTrue(log.Events.OfType<RngDraw>().Count() >= 100, "即使概率为 0 也照常走固定调用点（不改变抽取序列）");
    }
}
