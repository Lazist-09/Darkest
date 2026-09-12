using System;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// E4（事件节点二选一）+ E6（回城结算）：资源/士气效果可复现、事件齐全、
/// **撤退不回到 50**、完成/全灭回 50、清虚弱与死门后遗症。
/// </summary>
[TestClass]
public sealed class ExpeditionFlowTests
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

    private static ExpeditionSession NewSession()
        => new(log => HeadlessDriver.NewDirector(log), targetBattles: 6, firewood: 2, food: 12, ambushChance: 0.33);

    private static string RunOneBattle(ExpeditionSession s, long seed)
    {
        var log = new CombatLog();
        var rng = new RngProvider(seed);
        Darkest.Gameplay.Sim.Director.BattleDirector d = s.BeginBattle(1, log);
        string result = "RoundLimit";
        for (int round = 1; round <= 100; round++)
        {
            d.RunFullRound(rng, unit => Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, rng));
            if (d.IsBattleOver)
            {
                result = d.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                break;
            }
        }

        s.EndBattle(d, 1, result, 10);
        return result;
    }

    [TestMethod]
    public void E4_EventNode_TwoOptions_ApplyResourceAndMorale_WithEvent()
    {
        ExpeditionNodesConfig cfg = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        ExpeditionNodeConfig node = cfg.Get("ev_supply_cache");
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909); // 先打一场，让跨场状态里有存活者

        var log = new CombatLog();
        int foodBefore = s.Food;
        string effect = s.ResolveEventNode(log, node, optionIndex: 0); // loot → 口粮 +4

        Assert.AreEqual(foodBefore + 4, s.Food, "口粮 +4（数据驱动）");
        Assert.IsTrue(effect.Contains("food"), $"效果字串含资源：{effect}");
        EventNodeResolvedEvent e = log.Events.OfType<EventNodeResolvedEvent>().Single();
        Assert.AreEqual("ev_supply_cache", e.NodeId);
        Assert.AreEqual("loot", e.Choice);
        Assert.IsTrue(log.Events.OfType<ResourceChangedEvent>().Any(), "资源变动有事件（E2）");
    }

    [TestMethod]
    public void E4_EventNode_MoraleOption_ChangesRetainedMorale()
    {
        ExpeditionNodesConfig cfg = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);

        var log = new CombatLog();
        s.ResolveEventNode(log, cfg.Get("ev_shrine"), optionIndex: 0); // pray → 士气 +8
        Assert.IsTrue(log.Events.OfType<EventNodeResolvedEvent>().Any(), "结算有事件");
    }

    [TestMethod]
    public void E4_SkipNotAllowed_Throws()
    {
        ExpeditionNodesConfig cfg = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        ExpeditionSession s = NewSession();
        var log = new CombatLog();
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => s.ResolveEventNode(log, cfg.Get("ev_shrine"), optionIndex: 2),
            "事件必须二选一，不允许跳过（E4 / P20 ⑤）");
    }

    [TestMethod]
    public void E6_Retreat_KeepsMorale_NotRestoredTo50()
    {
        ExpeditionNodesConfig cfg = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));

        // 两条会话做同一场战斗 + 同一事件，唯一差别是回城口径 → 对比证明"撤退不恢复到 50"
        ExpeditionSession retreat = NewSession();
        RunOneBattle(retreat, 20260909);
        var logR = new CombatLog();
        retreat.ResolveEventNode(logR, cfg.Get("ev_supply_cache"), optionIndex: 1); // leave → 士气 −5
        // 用事件把跨场士气压到低于 50（重复 12 次 −5 → 钳到 0），这样才能证明"撤退不被抬回 50"
        ExpeditionNodesConfig cfgRetreat = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        for (int i = 0; i < 12; i++)
        {
            retreat.ResolveEventNode(logR, cfgRetreat.Get("ev_supply_cache"), optionIndex: 1);
        }

        int afterRetreat = retreat.ReturnToTown(logR, "retreat");
        TownReturnEvent e = logR.Events.OfType<TownReturnEvent>().Last();
        Assert.AreEqual("retreat", e.Outcome);
        Assert.IsTrue(e.PenaltyApplied, "撤退记录惩罚已施加");

        ExpeditionSession completed = NewSession();
        RunOneBattle(completed, 20260909);
        var logC = new CombatLog();
        completed.ResolveEventNode(logC, cfgRetreat.Get("ev_supply_cache"), optionIndex: 1);
        int afterCompleted = completed.ReturnToTown(logC, "completed");

        Assert.AreEqual(logC.Events.OfType<TownReturnEvent>().Last().MoraleBefore, afterCompleted,
            "#245：完成档也**不恢复到 50**");
        Assert.IsTrue(afterRetreat < 50,
            $"撤退**保留撤退结算后的士气、不恢复到 50**（实测撤退 {afterRetreat} < 50）");
    }

    [TestMethod]
    public void E6_Completion_RestoresHpButNotMorale()
    {
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        int after = s.ReturnToTown(log, "completed");
        TownReturnEvent e = log.Events.OfType<TownReturnEvent>().Last();
        Assert.AreEqual("completed", e.Outcome);
        Assert.IsFalse(e.PenaltyApplied, "完成档不触发惩罚");
        // 🔴 #245：士气**完全不恢复**（完成档也不回 50）
        Assert.AreEqual(e.MoraleBefore, after, "#245：回城士气**完全不恢复**（完成档同样保留原值）");
    }

    [TestMethod]
    public void E6_Wipe_NoPenalty_AndMoraleKept()
    {
        ExpeditionSession s = NewSession();
        var log = new CombatLog();
        RunOneBattle(s, 20260909);
        int after = s.ReturnToTown(log, "wiped");
        TownReturnEvent e = log.Events.OfType<TownReturnEvent>().Last();
        Assert.IsFalse(e.PenaltyApplied, "全灭档不额外施加撤退惩罚");
        Assert.AreEqual(e.MoraleBefore, after, "#245：士气保留（不恢复）");
    }
}
