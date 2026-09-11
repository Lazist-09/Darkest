using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// F3（#194）敌方超时增援波次：首波第 6 回合、此后每 3 回合一波；**每波一次性补齐当时全部空位**；
/// 满编则全体 +攻/+速（每波叠加）。数量型断言：一波补几个、间隔几回合。
/// </summary>
[TestClass]
public sealed class OvertimeReinforcementTests
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

        // 本组只测 #194/#196 波次机制 → 关闭 #198 弹性（弹性单测见 ElasticReinforcementTests）
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        tuning = tuning with
        {
            OvertimeReinforcement = tuning.OvertimeReinforcement with
            {
                Elastic = new TuningElasticSpec(false, 2, 3, 3),
                MValue = 3, // 本组只验波次机制 → 固定 M=3（校准值 10 见 P16 门禁用例）
            },
        };

        return new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            SkillsConfig.Parse(ReadData("skills.json")),
            BalanceTable.FromTuning(tuning),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
    }

    [TestMethod]
    public void Wave_FillsAllEmptySlotsAtOnce_NotOnePerRound()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        d.Enemy.RemoveUnitAt(3);
        d.Enemy.RemoveUnitAt(4); // 2 个空位

        var rng = new RngProvider(1);
        for (int r = 1; r <= 7; r++)
        {
            d.StartTurn(rng);
        }

        ReinforcementEvent[] fills = log.Events.OfType<ReinforcementEvent>().Where(e => e.Kind == "Fill").ToArray();
        Assert.AreEqual(2, fills.Length, "第 7 回合【一波补齐 2 个空位】（不是两回合各补 1 个；v0.68 trigger_round=7）");
        Assert.AreEqual(4, d.Enemy.OccupiedPositions(false).Count, "补位后满编 4");
    }

    [TestMethod]
    public void Wave_Gating_OnlyEvery3Rounds()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        d.Enemy.RemoveUnitAt(4);
        var rng = new RngProvider(2);

        for (int r = 1; r <= 6; r++)
        {
            d.StartTurn(rng);
        }

        Assert.AreEqual(0, log.Events.OfType<ReinforcementEvent>().Count(), "第 7 回合前不触发（v0.68：trigger_round=7）");

        d.StartTurn(rng); // 7
        Assert.AreEqual(1, log.Events.OfType<ReinforcementEvent>().Count(), "第 7 回合触发首波");

        d.Enemy.RemoveUnitAt(4); // 再空出一个
        d.StartTurn(rng); // 8
        d.StartTurn(rng); // 9
        Assert.AreEqual(1, log.Events.OfType<ReinforcementEvent>().Count(), "第 8/9 回合不触发（间隔 3）");

        d.StartTurn(rng); // 10
        Assert.AreEqual(2, log.Events.OfType<ReinforcementEvent>().Count(), "第 10 回合触发第二波（M=3）");
    }

    [TestMethod]
    public void FullBoard_BuffsAll_EachWaveStacks()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(3);

        for (int r = 1; r <= 7; r++)
        {
            d.StartTurn(rng);
        }

        Assert.AreEqual(4, log.Events.OfType<ReinforcementEvent>().Count(e => e.Kind == "Buff"), "满编 → 全体 4 人各上增益");
        Assert.IsTrue(d.Enemy.UnitsInSlotOrder().All(u => u.AttackMod == 3 && u.SpeedMod == 3), "第一波 +攻/+速 = 3");

        for (int r = 8; r <= 10; r++)
        {
            d.StartTurn(rng);
        }

        Assert.IsTrue(d.Enemy.UnitsInSlotOrder().All(u => u.AttackMod == 6 && u.SpeedMod == 6), "第二波再叠一次（3→6）");
    }
}