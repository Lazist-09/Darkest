using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
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

    /// <summary>
    /// 🔴 **`#305`③ 第一步 / 红线 21**：**夜袭必须真的发生在【生产流程】里** ——
    /// 契约（`m7_expedition.md:143` 与 `EndCamp` 的注释："夜袭判定由调用方接 `RollAmbush`"）
    /// 而此前 `Camp()` **漏了这一步调用**（包装与注释都在 ⇒ **实现漏一步**）。
    /// 验证：① **扎营后确实会触发夜袭** · ② **守夜 ／ 站岗（`ambush_immunity_once`）真的免疫一次**。
    /// </summary>
    [TestMethod]
    public void CampTriggersAmbush_AndWatchSkillImmunizesOnce()
    {
        var log = new CombatLog();
        var rng = new RngProvider(20260909);
        ExpeditionSession s = NewSession();
        int camps = 0, ambushes = 0;
        for (int i = 0; i < 40; i++)
        {
            if (s.StartCamp(log, i, 3))
            {
                camps++;
                if (s.RollAmbush(log, rng))
                {
                    ambushes++;
                }
            }
        }

        Assert.IsTrue(camps > 0, "至少一次扎营成功（柴火足够）");
        Assert.IsTrue(ambushes > 0, "🔴 夜袭必须【真的会触发】（33% × 多次扎营 ⇒ 至少一次）");
        Assert.IsTrue(log.Events.OfType<AmbushTriggeredEvent>().Any(), "触发必须写事件（战报可读）");

        // 守夜 ／ 站岗：授予"免下一次夜袭" ⇒ 本次不触发，且免疫被消费（一次性）
        ExpeditionSession s2 = NewSession();
        Assert.IsTrue(s2.StartCamp(log, 0, 3), "扎营成功");
        Assert.IsTrue(s2.UseCampSkill(log, CampSkillTestKit.Skill("camp_warrior_watch"), default, CampSkillTestKit.Camp),
            "轮流守夜应可施加（点数足够）");
        Assert.IsTrue(s2.AmbushImmune, "守夜后应持有【免下一次夜袭】");
        Assert.IsFalse(s2.RollAmbush(log, rng), "🔴 持有免疫 ⇒ 本次【不触发】夜袭");
        Assert.IsFalse(s2.AmbushImmune, "🔴 免疫是【一次性】：已被消费");

        bool anyAfter = false;
        for (int i = 0; i < 40; i++)
        {
            anyAfter |= s2.RollAmbush(log, new RngProvider(20260909 + i));
        }

        Assert.IsTrue(anyAfter, "免疫用掉后夜袭恢复正常判定");
    }

    /// <summary>
    /// 🔴 **`#307`③：夜袭【真的插一场战斗】且【计入 6 场皆胜】**（契约 `m7_expedition.md:35`）——
    /// 内核提供 `BeginAmbushBattle`（真实战斗：同一难度递进 + 当前光照档）；
    /// 结算走**常规路径** `OnBattleFinished` ⇒ **自然计入胜场**（无需特殊通道）✓
    /// </summary>
    [TestMethod]
    public void AmbushBattle_IsInserted_AndCountsTowardWins()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            ambushChance: 1.0); // 必定触发（专测插入）
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new RngProvider(20260909));

        int winsBefore = flow.Wins, battlesBefore = session.BattlesPlayed;

        Assert.IsTrue(flow.Camp(), "扎营成功");
        Assert.IsTrue(flow.LastCampAmbushed, "ambush_chance=1.0 ⇒ 必定触发夜袭");

        var director = flow.BeginAmbushBattle(log);
        Assert.IsNotNull(director, "🔴 夜袭必须插一场【真实战斗】（不是只给一个标记）");
        Assert.IsTrue(flow.AmbushBattleStarted, "已标记为夜袭战斗（防重复插）");

        // 走真实的完整流程：跑战斗 → session.EndBattle（推进序号）→ flow.OnBattleFinished（计入胜场）
        session.EndBattle(director, session.BattlesPlayed + 1, "PlayerVictory", 5);
        flow.OnBattleFinished("PlayerVictory", rounds: 5, isAmbush: true);
        Assert.AreEqual(winsBefore + 1, flow.Wins, "🔴 夜袭胜场【计入 Wins】（契约：计入 6 场皆胜）");
        Assert.AreEqual(battlesBefore + 1, session.BattlesPlayed, "夜袭也推进一步战斗序号（EndBattle 记账）");
    }

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
}
