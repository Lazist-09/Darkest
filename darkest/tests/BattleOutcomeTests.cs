using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// P0/O-47 胜负判定与流程收束：BattleOutcome 三分投影、**我方优先判负**（同瞬间双灭 → 判负）、
/// 胜负后立即收束（headless RunFullRound 不再产生任何行动事件）、IsBattleOver 别名兼容。
/// </summary>
[TestClass]
public sealed class BattleOutcomeTests
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
        return new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            SkillsConfig.Parse(ReadData("skills.json")),
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
    }

    private static void Wipe(BattleDirector d, bool player)
    {
        var board = player ? d.Player : d.Enemy;
        for (int slot = 1; slot <= board.SlotCount; slot++)
        {
            board.RemoveUnitAt(slot);
        }
    }

    [TestMethod]
    public void Outcome_InitiallyOngoing()
    {
        BattleDirector d = NewDirector(out _);
        Assert.AreEqual(BattleOutcome.Ongoing, d.Outcome);
        Assert.IsFalse(d.IsBattleOver);
    }

    [TestMethod]
    public void Outcome_EnemyWiped_IsVictory()
    {
        BattleDirector d = NewDirector(out _);
        Wipe(d, player: false);
        Assert.AreEqual(BattleOutcome.Victory, d.Outcome);
        Assert.IsTrue(d.IsBattleOver, "IsBattleOver 为 Outcome 别名");
    }

    [TestMethod]
    public void Outcome_PlayerWiped_IsDefeat_NotVictory()
    {
        BattleDirector d = NewDirector(out _);
        Wipe(d, player: true);
        Assert.AreEqual(BattleOutcome.Defeat, d.Outcome, "我方全灭必须判负（P0 修复硬编码『胜利』）");
        Assert.IsTrue(d.IsBattleOver);
    }

    [TestMethod]
    public void Outcome_BothWiped_PlayerPriority_Defeat()
    {
        BattleDirector d = NewDirector(out _);
        Wipe(d, player: false);
        Wipe(d, player: true);
        Assert.AreEqual(BattleOutcome.Defeat, d.Outcome, "同瞬间双灭 → 我方优先判负（硬核受苦定位）");
    }

    [TestMethod]
    public void RunFullRound_AfterVictory_NoFurtherActions()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        Wipe(d, player: false); // 敌方全灭

        int before = log.Events.Count;
        d.RunFullRound(new RngProvider(1), _ => PlayerDecision.Skill("warrior_cleave"));

        IEnumerable<BattleEvent> added = log.Events.Skip(before);
        Assert.IsFalse(added.OfType<DamageEvent>().Any(), "胜负已定后不得产生伤害");
        Assert.IsFalse(added.OfType<DisplaceEvent>().Any(), "胜负已定后不得产生位移");
        Assert.IsFalse(added.OfType<SwapEvent>().Any(), "胜负已定后不得产生增援/移动");
        Assert.IsFalse(added.OfType<HitEvent>().Any(), "胜负已定后不得产生命中判定");
    }

    [TestMethod]
    public void RunFullRound_AfterDefeat_NoFurtherActions()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        Wipe(d, player: true); // 我方全灭

        int before = log.Events.Count;
        d.RunFullRound(new RngProvider(1), _ => PlayerDecision.None);

        // P0/O-47 + G0/O-55：不得再有行动事件；仅允许补记一条战斗结束事件（幂等）
        Assert.IsTrue(log.Events.Skip(before).All(e => e is BattleEndEvent),
            $"我方全灭后不得再有行动事件（新增 {log.Events.Count - before} 条，只允许 BattleEndEvent）");
    }

    [TestMethod]
    public void Retreat_Disabled_WhenBattleOver()
    {
        BattleDirector d = NewDirector(out _);
        Wipe(d, player: false);
        Assert.IsFalse(d.CanRetreatThisRound, "胜负已定 → 撤退不可点");
    }
}