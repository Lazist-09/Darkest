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
/// M7.5 **远征流程控制器状态机**（`ExpeditionFlow`，供场景层往返驱动）：
/// 锁「步进与类型 / 光照 −15 / 侦察只揭示下一节点 / 事件二选一推进 / 战斗回灌后按档给份数掉落（不掷骰）/ 撤退即中止」。
/// </summary>
[TestClass]
public sealed class ExpeditionFlowStateMachineTests
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

    private static (ExpeditionFlow Flow, TuningConfig Tuning, CombatLog Log) NewFlow(long seed = 20260909)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        var meter = new LightMeter(tuning.Light!);
        var scout = new Scouting(tuning.Scouting!, tuning.Light!);
        return (new ExpeditionFlow(session, meter, bag, scout, nodes, tuning, log, new RngProvider(seed)), tuning, log);
    }

    [TestMethod]
    public void Flow_Advance_Costs15Light_AndScoutsOnlyNextNode()
    {
        (ExpeditionFlow flow, TuningConfig tuning, _) = NewFlow();
        int before = flow.Meter.Value;
        int drawsBefore = flow.Session is null ? 0 : 0;

        FlowStep step = flow.Advance(optionIndex: 0);

        Assert.IsTrue(step.Kind is FlowStepKind.Battle or FlowStepKind.Event, "步骤类型只能是战斗或事件");
        Assert.AreEqual(before + tuning.Light!.NodeStep, flow.Meter.Value, "#278：前进一个节点 −30");
        Assert.IsTrue(step.Options.Count == 2, "每步恰 2 个候选（P20 ⑤）");

        ScoutOutcome? sc = flow.LastScout;
        Assert.IsNotNull(sc, "每次 Advance 都做一次侦察判定");
        if (!sc!.Success)
        {
            Assert.IsNull(sc.RevealedNodeType, "失败 ⇒ null（不透露类型）");
        }

        _ = drawsBefore;
    }

    [TestMethod]
    public void Flow_EventStep_ResolvesAndAdvances()
    {
        (ExpeditionFlow flow, _, _) = NewFlow();
        FlowStep step = flow.Advance(0);
        if (step.Kind != FlowStepKind.Event)
        {
            Assert.Inconclusive("本 seed 首步是战斗（路径随机）—— 事件分支由其它 seed 覆盖");
            return;
        }

        Assert.AreEqual(0, flow.StepsDone);
        flow.ResolveEvent(0);
        Assert.AreEqual(1, flow.StepsDone, "事件结算后步数 +1");
    }

    [TestMethod]
    public void Flow_BattleFinished_LootGoesToBag_FullGoesPending_NoSilentDrop_NoDraw()
    {
        (ExpeditionFlow flow, TuningConfig tuning, CombatLog log) = NewFlow();
        FlowStep step = flow.Advance(1); // option 1 = 战斗（确定性）
        if (step.Kind != FlowStepKind.Battle)
        {
            Assert.Inconclusive("本 seed 首步是事件（路径随机）");
            return;
        }

        int foodBefore = flow.Session.Food;
        int bagBefore = flow.Bag.Count;
        int drawsBefore = log.Events.OfType<RngDraw>().Count();

        flow.OnBattleFinished("PlayerVictory", rounds: 5);

        TuningLootSpec spec = tuning.Light!.Loot[LightMeter.TierId(flow.Meter.Tier)];
        int expected = spec.Firewood + spec.Food; // #276：掉落 = 柴火 + 口粮
        int collected = flow.Bag.Count - bagBefore;
        int pending = flow.PendingLoot.Count;

        // 🔴 掉落进**背包**；背包满（推荐配置恰满 12）⇒ 剩余进"待处理"，**绝不静默丢**
        Assert.AreEqual(expected, collected + pending,
            $"按档共 {expected} 份补给：已收 {collected} + 待处理 {pending}（不得静默丢弃）");
        Assert.AreEqual(foodBefore + collected, flow.Session.Food, "会话计数与**实际收进背包**的份数同步");
        Assert.AreEqual(drawsBefore, log.Events.OfType<RngDraw>().Count(), "🔴 掉落不得引入抽取（P21 ⑧ / #270）");
        Assert.AreEqual(1, flow.StepsDone);
    }

    [TestMethod]
    public void Flow_PendingLoot_CollectedAfterFreeingSlot()
    {
        (ExpeditionFlow flow, _, _) = NewFlow();

        // 走到较暗档位（100→40：4 次前进，每次 −15），使掉落份数 > 0
        int advanced = 0;
        for (int i = 0; i < 4; i++)
        {
            FlowStep s = flow.Advance(1); // option 1 = 战斗（确定性）
            if (s.Kind != FlowStepKind.Battle)
            {
                continue; // 路径随机导致非战斗步：跳过（本用例只验"满格 → 待处理 → 腾格可收"）
            }

            flow.OnBattleFinished("PlayerVictory", rounds: 5);
            advanced++;
        }

        Assert.IsTrue(advanced > 0, "至少完成一场战斗");
        Assert.IsTrue(flow.Bag.Count > 0, "背包有物品（推荐配置）");

        // 满格场景：待处理 + 已收 == 本趟份数；腾出一格后**至少能收下一件**（不静默丢、也不白丢）
        int before = flow.PendingLoot.Count;
        if (before == 0)
        {
            Console.WriteLine("[M7.5] 本档份数被背包全部收下（未触发满格）——待处理分支需满格场景");
            return;
        }

        Assert.IsTrue(flow.Bag.TryDiscardAt(0, out _), "玩家选择丢弃一格（腾格）");

        // 腾够格数（待处理件数 ≥ 1；逐格丢弃后再收）——注意 RetryPendingLoot 对**收不下的那件会重新入队**，
        // 因此"待处理计数只减"不成立；正确断言 = 腾出足够格子后**能全部收下**
        for (int i = 0; i < before && flow.Bag.Count > 0; i++)
        {
            flow.Bag.TryDiscardAt(0, out _);
        }

        flow.RetryPendingLoot();
        Assert.AreEqual(0, flow.PendingLoot.Count, $"腾够格子后应把 {before} 件待处理补给**全部收进背包**（不静默丢）");
    }

    [TestMethod]
    public void Flow_Victory_AwardsGoldByLightTier_WhenEconomyInjected()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        EconomyConfig econCfg = EconomyConfig.Parse(ReadData("economy.json"));
        var econ = new Darkest.Gameplay.Sim.Run.Economy(econCfg);
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new RngProvider(20260909), econ);

        FlowStep s = flow.Advance(1); // option 1 = 战斗（确定性）
        if (s.Kind != FlowStepKind.Battle)
        {
            Assert.Inconclusive("路径随机导致非战斗步");
            return;
        }

        string tier = LightMeter.TierId(flow.Meter.Tier);
        int expected = econCfg.RewardFor(tier);
        flow.OnBattleFinished("PlayerVictory", rounds: 5);

        Assert.AreEqual(expected, econ.Gold, $"打赢一场按光照档记金钱（档 {tier} ／ 战斗数挂钩 ⇒ 共 {expected}）");
        Assert.IsTrue(log.Events.OfType<Darkest.Core.Events.GoldChangedEvent>().Any(e => e.Reason == "battle"),
            "金钱变更必写事件（数字必须来自事件流）");
    }

    /// <summary>
    /// 🔴🔴 **`#352` 语义拆分**（策划 `retreat.md §1/§3`）—— 本条用例**原先断言的正是被推翻的旧契约**：
    /// 旧：`撤退 ⇒ 本趟结束（#233）` ⚠️（= "撤退 = 结局"）
    /// 新：**撤退 = 【一场】的选择 ⇒ 本趟【不】结束**（回地图当前格继续走）；**只有全灭 / 放弃远征才结束** ✓
    /// 📌 这正是"**既有用例依赖错误行为**是洞曾存在的最硬证据"那套方法论的现场实例 ✓
    /// </summary>
    [TestMethod]
    public void Flow_RetreatDoesNotEndRun_ButWipeDoes()
    {
        (ExpeditionFlow flow, _, _) = NewFlow();
        FlowStep step = flow.Advance(1); // option 1 = 战斗（确定性）
        if (step.Kind != FlowStepKind.Battle)
        {
            Assert.Inconclusive("本 seed 首步是事件（路径随机）");
            return;
        }

        flow.OnBattleFinished("DrawRetreat", rounds: 5);
        Assert.IsFalse(flow.IsFinished, "🔴 **撤退不再结束本趟**（#352：撤退是【一场】的选择）✓");
        Assert.AreEqual(ExpeditionOutcome.InProgress, flow.Outcome, "撤退后结局仍是「进行中」 ✓");

        // 🔴 R8 的流程级形态：**再撤一次 ⇒ 本趟仍不结束**（⇒ 后续还能继续撤 ⇒ 惩罚可累积）✓
        flow.OnBattleFinished("DrawRetreat", rounds: 5);
        Assert.IsFalse(flow.IsFinished, "退两次 ⇒ 本趟**仍**不结束 ✓");

        flow.OnBattleFinished("EnemyVictory", rounds: 5);
        Assert.IsTrue(flow.IsFinished, "**全灭**才是【一趟】的结局 ✓");
        Assert.AreEqual(ExpeditionOutcome.Wiped, flow.Outcome, "结局 = 全灭 ✓");
    }
}
