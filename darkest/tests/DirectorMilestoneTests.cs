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
    public void Reinforcement_FillsFirstEmptyAtTriggerRound_NotBefore()
    {
        var log = new CombatLog();
        BattleDirector director = NewDirector(log);
        director.Enemy.RemoveUnitAt(4); // 留一个空位（施法者先被杀）

        var rng = new ScriptedRng();
        for (int r = 1; r <= 6; r++)
        {
            director.StartTurn(rng);
        }

        Assert.AreEqual(0, log.Events.OfType<ReinforcementEvent>().Count(), "第 7 回合前无增援（v0.68 trigger_round=7）");

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
        for (int r = 1; r <= 7; r++)
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
    public void Retreat_SuccessCostsTeam12_AndDisabledForRound()
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
        Assert.AreEqual(-12 * 6, moraleAfter - moraleBefore, "成功 → 存活者（6 人）各 −12（M7 #240 两档；原 −10 已推翻）");
        Assert.IsFalse(director.CanRetreatThisRound, "当回合不可再试（#118）");

        // 第二次尝试被拒（无新事件/无新士气扣）
        int moraleBefore2 = director.Player.UnitsInSlotOrder().Sum(u => u.Morale);
        bool second = director.PlayerRetreat(rng);
        Assert.IsFalse(second);
        Assert.AreEqual(moraleBefore2, director.Player.UnitsInSlotOrder().Sum(u => u.Morale));
        Assert.AreEqual(1, log.Events.OfType<RetreatEvent>().Count(), "单回合最多一次撤退判定");
    }

    /// <summary>
    /// 🔴 **`#357` R8：一趟退 2 场 ⇒ 士气恰好扣 2 次**（不是 1 次、也不是 3 次）✓
    ///
    /// 📌 口径说明（避免误读）：**士气惩罚由【战斗层】在每次撤退时结算**（`PlayerRetreat` ⇒ `retreat_*` 事件），
    ///    而"跨场累积"是**跑图/名册层**把战后士气带回下一场的结果 ⇒ 所以本用例证的是：
    ///    ① **没有"一趟只结算一次"的全局闸**（两场各结算一次 ✓）② 每次恰好 **一次**（不重复 ✓）
    ///    ⇒ 两条合起来 = "**可累积、且不重不漏**" ✓
    /// </summary>
    [TestMethod]
    public void Retreat_TwoSeparateBattles_EachSettlesExactlyOnce()
    {
        var log = new CombatLog();
        BattleDirector b1 = NewDirector(log);
        var rng = new ScriptedRng(0.0, 0.0); // 判定 roll 0 < rate ⇒ 成功
        int before1 = b1.Player.UnitsInSlotOrder().Sum(u => u.Morale);
        Assert.IsTrue(b1.PlayerRetreat(rng), "第 1 场撤退成功 ✓");
        int delta1 = b1.Player.UnitsInSlotOrder().Sum(u => u.Morale) - before1;

        // 第 2 场（另一场战斗；名册层会把战后士气带进来 ⇒ 这里只验证"每场各结算一次"）
        BattleDirector b2 = NewDirector(log);
        int before2 = b2.Player.UnitsInSlotOrder().Sum(u => u.Morale);
        Assert.IsTrue(b2.PlayerRetreat(rng), "第 2 场撤退成功 ✓");
        int delta2 = b2.Player.UnitsInSlotOrder().Sum(u => u.Morale) - before2;

        Assert.AreEqual(-12 * 6, delta1, "第 1 场：存活 6 人各 −12 ✓");
        Assert.AreEqual(-12 * 6, delta2, "第 2 场：**再扣一次**（不是因为「本趟已撤过」就免掉）✓");
        Assert.AreEqual(2, log.Events.OfType<RetreatEvent>().Count(), "两场 ⇒ 共 2 次撤退判定（不重不漏）✓");
        // ⚠️ `ApplyTeamOnce` **每单位发一条** `MoraleEvent` ⇒ 6 人 × 2 场 = **12 条**（我第一版写 2，是我漏看"按单位发"✓）
        Assert.AreEqual(12, log.Events.OfType<MoraleEvent>().Count(m => m.Source == "retreat_success"),
            "两场 ⇒ 12 条单位级士气事件（= 6 人 × 2 场 ⇒ **每场都结算了**）✓");
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