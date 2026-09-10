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
/// M5 导演集成（T-M5-03/04/09）：超时增援（第 6 回合/上限/满编增益）、撤退（公式数字/士气/当回合禁用/
/// 范围）、同 seed 同命令流两份 CombatLog 逐条一致（镜像基础）。
/// </summary>
[TestClass]
public sealed class DirectorMilestoneTests
{
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _p;
        private ulong _d;

        public ScriptedRng(params double[] percents)
        {
            _p = new Queue<double>(percents);
        }

        public double NextPercent()
        {
            _d++;
            return _p.Count > 0 ? _p.Dequeue() : 0.0;
        }

        public int NextInt(int a, int b)
        {
            _d++;
            return a;
        }

        public ulong DrawCount => _d;
    }

    private static string FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    private static BattleDirector NewDirector(CombatLog log)
        => new(FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json"))),
            UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json"))),
            SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json"))),
            BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json")))),
            MoraleEventsConfig.Parse(File.ReadAllText(FindDataFile("morale_events.json"))),
            BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json"))),
            EnemyAiConfig.Parse(File.ReadAllText(FindDataFile("enemy_ai.json"))),
            log);

    [TestMethod]
    public void Reinforcement_FillsFirstEmptyAtRound6_NotBefore()
    {
        var log = new CombatLog();
        BattleDirector director = NewDirector(log);
        director.Enemy.RemoveUnitAt(4); // 留一个空位（施法者先被杀）

        var rng = new ScriptedRng();
        for (int r = 1; r <= 5; r++)
        {
            director.StartTurn(rng);
        }

        Assert.AreEqual(0, log.Events.OfType<ReinforcementEvent>().Count(), "第 6 回合前无增援");

        director.StartTurn(rng);
        ReinforcementEvent fill = log.Events.OfType<ReinforcementEvent>().Single();
        Assert.AreEqual("Fill", fill.Kind);
        Assert.AreEqual(4, fill.Slot, "填第一个空位（槽位升序）");
        Assert.AreEqual(SlotState.Occupied, director.Enemy.GetSlot(4));
        Assert.AreEqual(4, director.Enemy.OccupiedPositions(false).Count, "不超 4 位上限");
    }

    [TestMethod]
    public void Reinforcement_FullBoard_BuffsAllWithTuningPlaceholders()
    {
        var log = new CombatLog();
        BattleDirector director = NewDirector(log);
        var rng = new ScriptedRng();
        for (int r = 1; r <= 6; r++)
        {
            director.StartTurn(rng);
        }

        ReinforcementEvent[] buffs = log.Events.OfType<ReinforcementEvent>().ToArray();
        Assert.IsTrue(buffs.Length >= 1 && buffs.All(b => b.Kind == "Buff"), "满编 → 无新单位、改上增益（O-20/#71）");
        Assert.AreEqual(4, director.Enemy.OccupiedPositions(false).Count, "满编边界不扩编");
        Assert.IsTrue(director.Enemy.UnitsInSlotOrder().All(u => u.AttackMod == 3 && u.SpeedMod == 3),
            "+攻/+速读取 tuning 占位（buff_attack_delta/buff_speed_delta）");
    }

    [TestMethod]
    public void Retreat_SuccessCostsTeam10_AndDisabledForRound()
    {
        var log = new CombatLog();
        BattleDirector director = NewDirector(log);
        var rng = new ScriptedRng(0.0, 0.0); // 扰动 0 → base；判定 roll 0 < rate → 成功
        int moraleBefore = director.Player.UnitsInSlotOrder().Sum(u => u.Morale);

        bool success = director.PlayerRetreat(rng);
        Assert.IsTrue(success);
        RetreatEvent r = log.Events.OfType<RetreatEvent>().Single();
        Assert.IsTrue(r.Rate is >= 5 and <= 95, "成功率钳制 [5,95]");
        int moraleAfter = director.Player.UnitsInSlotOrder().Sum(u => u.Morale);
        Assert.AreEqual("retreat_success", log.Events.OfType<MoraleEvent>().Last().Source);
        Assert.AreEqual(-10 * 6, moraleAfter - moraleBefore, "成功 → 全队 6 人各 −10（retreat_success）");
        Assert.IsFalse(director.CanRetreatThisRound, "当回合不可再试（#118）");

        // 第二次尝试被拒（无新事件/无新士气扣）
        int moraleBefore2 = director.Player.UnitsInSlotOrder().Sum(u => u.Morale);
        bool second = director.PlayerRetreat(rng);
        Assert.IsFalse(second);
        Assert.AreEqual(moraleBefore2, director.Player.UnitsInSlotOrder().Sum(u => u.Morale));
        Assert.AreEqual(1, log.Events.OfType<RetreatEvent>().Count(), "单回合最多一次撤退判定");
    }

    [TestMethod]
    public void Retreat_FailCostsTeam5_NextTurnRetryable()
    {
        var log = new CombatLog();
        BattleDirector director = NewDirector(log);
        var rng = new ScriptedRng(0.0, 100.0); // 判定 roll=100 ≥ rate → 失败
        int moraleBefore = director.Player.UnitsInSlotOrder().Sum(u => u.Morale);

        Assert.IsFalse(director.PlayerRetreat(rng));
        int moraleAfter = director.Player.UnitsInSlotOrder().Sum(u => u.Morale);
        Assert.AreEqual(-5 * 6, moraleAfter - moraleBefore, "失败 → 全队 6 人各 −5（retreat_fail）");
        Assert.IsFalse(director.CanRetreatThisRound);

        director.StartTurn(new ScriptedRng()); // 下回合
        Assert.IsTrue(director.CanRetreatThisRound, "下回合可再试");
    }

    [TestMethod]
    public void Mirror_SameSeedSameCommands_IdenticalLogs()
    {
        long s = 20260909;

        // 两线完全相同的 3 回合流程（起始/玩家命令/敌方阶段各用独立固定种子 RNG 流）
        var logA = new CombatLog();
        BattleDirector a = NewDirector(logA);
        for (int r = 1; r <= 3; r++)
        {
            a.StartTurn(new RngProvider(s + r));
            a.PlayerUseSkill(UnitId.Of("warrior"), "warrior_cleave", new RngProvider(s + 100 + r));
            a.EnemyPhase(new RngProvider(s + 200 + r));
        }

        var logB = new CombatLog();
        BattleDirector b = NewDirector(logB);
        for (int r = 1; r <= 3; r++)
        {
            b.StartTurn(new RngProvider(s + r));
            b.PlayerUseSkill(UnitId.Of("warrior"), "warrior_cleave", new RngProvider(s + 100 + r));
            b.EnemyPhase(new RngProvider(s + 200 + r));
        }

        Assert.AreEqual(logA.Events.Count, logB.Events.Count, "同 seed 同命令流 → 事件数一致");
        for (int i = 0; i < logA.Events.Count; i++)
        {
            Assert.AreEqual(logA.Events[i], logB.Events[i], $"第 {i} 条事件逐项一致（含增援/敌方 AI/士气，镜像 M-A）");
        }
    }
}