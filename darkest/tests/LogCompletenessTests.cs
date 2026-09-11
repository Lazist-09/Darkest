using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// G0（O-55）事件字典完备性：日志必须能**完整描述流程**——谁用了什么技能、谁给谁上了 buff、
/// 谁在第几回合行动、战斗如何结束。数量型断言（不是"有没有"，而是"有几条"）。
/// </summary>
[TestClass]
public sealed class LogCompletenessTests
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

    private static BattleDirector NewDirector(out CombatLog log)
    {
        log = new CombatLog();
        var d = new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            SkillsConfig.Parse(ReadData("skills.json")),
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        return d;
    }

    [TestMethod]
    public void EveryEventCarriesRound_AfterFirstStartTurn()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(7);
        d.RunFullRound(rng, _ => PlayerDecision.Skill("warrior_cleave", null)); // 内含一次 StartTurn → 回合 1

        Assert.AreEqual(1, d.Round, "只跑了 1 个回合");
        RoundStartEvent start = log.Events.OfType<RoundStartEvent>().First();
        Assert.IsTrue(log.Events.Where(e => e.Sequence >= start.Sequence).All(e => e.Round == 1),
            "G0：回合号盖章到每个事件（首回合起）");
        Assert.IsTrue(log.Events.OfType<RoundStartEvent>().Any(e => e.Round == 1));
    }

    [TestMethod]
    public void SkillUse_Recorded_PerUse_WithCasterSlotAndTargets()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(8);
        d.StartTurn(rng);
        d.PlayerUseSkill(UnitId.Of("warrior"), "warrior_cleave", rng, new[] { 1 });

        SkillUseEvent use = log.Events.OfType<SkillUseEvent>().Single();
        Assert.AreEqual("warrior_cleave", use.SkillId, "谁用了什么技能");
        Assert.AreEqual(UnitId.Of("warrior"), use.Actor);
        Assert.AreEqual(2, use.CasterSlot, "战士在 2 号位");
        CollectionAssert.AreEqual(new[] { 1, 2 }, use.TargetSlots.ToArray(), "候选池目标位");
    }

    [TestMethod]
    public void BuffLifecycle_AppliedAndRemoved_AreLogged()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        d.Buffs.Add(UnitId.Of("tank"), "shield", source: UnitId.Of("tank"));
        Assert.AreEqual(1, log.Events.OfType<BuffAppliedEvent>().Count(e => e.BuffId == "shield"), "buff 施加留痕（恰好 1 条）");

        d.Buffs.Remove(UnitId.Of("tank"), "shield");
        BuffRemovedEvent removed = log.Events.OfType<BuffRemovedEvent>().Single(e => e.BuffId == "shield");
        Assert.AreEqual("consumed", removed.Reason, "buff 移除留痕并带原因（默认消耗）");
    }

    [TestMethod]
    public void TurnStart_CountMatchesActorsActed()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(9);
        d.StartTurn(rng);
        d.RunFullRound(rng, _ => PlayerDecision.Skill("warrior_cleave", null));

        int turns = log.Events.OfType<TurnStartEvent>().Count();
        int enemyDecisions = log.Events.OfType<EnemyDecisionEvent>().Count();
        int playerSkillUses = log.Events.OfType<SkillUseEvent>().Count(e => e.Actor.Value is "warrior" or "tank" or "medic" or "commissar" or "warrior_2" or "medic_2");
        Assert.IsTrue(turns >= 1, $"回合开始事件 ≥1（实际 {turns}）");
        Assert.AreEqual(turns, playerSkillUses + enemyDecisions,
            $"每个行动者恰好一条行动记录（TurnStart={turns}，我方技能={playerSkillUses}，敌方决策={enemyDecisions}）");
    }

    [TestMethod]
    public void BattleEnd_Event_EmittedOnce_WithOutcomeAndReason()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(10);
        d.Enemy.RemoveUnitAt(1);
        d.Enemy.RemoveUnitAt(2);
        d.Enemy.RemoveUnitAt(3);
        d.Enemy.RemoveUnitAt(4); // 敌方全灭 → 胜负已定

        d.RunFullRound(rng, _ => PlayerDecision.Skill("warrior_cleave", null));
        d.RunFullRound(rng, _ => PlayerDecision.Skill("warrior_cleave", null)); // 第二次不得重复记

        BattleEndEvent end = log.Events.OfType<BattleEndEvent>().Single();
        Assert.AreEqual("Victory", end.Outcome, "结局入日志");
        Assert.AreEqual("enemy_wiped", end.Reason, "原因入日志");
    }
}