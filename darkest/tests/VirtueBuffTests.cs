using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// F2（#193）士气 buff 效果（半包 A）：`modifiers` 通用消费——勇猛伤害 +25%、专注暴击 +15pp、
/// 坚韧死门 +20pp、突进 +20% 一次性；美德池 4 个；振奋每回合全队 +3（O-27 定值）。
/// </summary>
[TestClass]
public sealed class VirtueBuffTests
{
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _p;
        private ulong _d;

        public ScriptedRng(params double[] percents) => _p = new Queue<double>(percents);

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

    private static (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) World()
    {
        var skills = SkillsConfig.Parse(ReadData("skills.json"));
        var rt = new SkillRuntimeState();
        var d = new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            skills,
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            new CombatLog());
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        return (d, skills, rt);
    }

    private static SkillExecutor Executor(BattleDirector d, SkillsConfig skills, SkillRuntimeState rt)
        => new(skills, BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), d.Log, rt, d.Buffs);

    [TestMethod]
    public void VirtueBrave_DealtDamage_MultipliedBy1_25()
    {
        (BattleDirector plain, SkillsConfig s1, SkillRuntimeState r1) = World();
        Executor(plain, s1, r1).Execute(s1.Get("warrior_cleave"), UnitId.Of("warrior"), plain.Player, plain.Enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 1 });
        double baseline = plain.Log.Events.OfType<DamageEvent>().Single().Raw;

        (BattleDirector d, SkillsConfig s2, SkillRuntimeState r2) = World();
        d.Buffs.Add(UnitId.Of("warrior"), "virtue_brave", source: null);
        Executor(d, s2, r2).Execute(s2.Get("warrior_cleave"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 1 });
        double withVirtue = d.Log.Events.OfType<DamageEvent>().Single().Raw;

        Assert.AreEqual(1.25, withVirtue / baseline, 0.01, "勇猛 dealt_damage_mult +25%（乘法阶段）");
    }

    [TestMethod]
    public void VirtueFocused_CritBonus_Plus15pp()
    {
        // 战士基础暴击 5；roll=15：无专注不暴击、有专注（5+15=20）暴击
        (BattleDirector plain, SkillsConfig s1, SkillRuntimeState r1) = World();
        Executor(plain, s1, r1).Execute(s1.Get("warrior_cleave"), UnitId.Of("warrior"), plain.Player, plain.Enemy,
            new ScriptedRng(0.0, 15.0), chosenTargets: new[] { 1 });
        Assert.IsFalse(plain.Log.Events.OfType<CritEvent>().Single().Crit, "基础暴击 5 → roll 15 不暴击");

        (BattleDirector d, SkillsConfig s2, SkillRuntimeState r2) = World();
        d.Buffs.Add(UnitId.Of("warrior"), "virtue_focused", source: null);
        Executor(d, s2, r2).Execute(s2.Get("warrior_cleave"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 15.0), chosenTargets: new[] { 1 });
        Assert.IsTrue(d.Log.Events.OfType<CritEvent>().Single().Crit, "专注 crit_bonus +15pp → roll 15 暴击");
    }

    [TestMethod]
    public void VirtueResolute_DeathDoorResist_Plus20pp()
    {
        (BattleDirector plain, _, _) = World();
        UnitRuntime w = plain.Player.UnitRuntimeAt(2)!; // 战士死门 70
        w.Weak = true;
        Assert.IsFalse(WeakDeathsDoor.Roll(w, afflicted: false, new ScriptedRng(85.0), plain.Log,
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json")))), "基础 70 → roll 85 失败");

        (BattleDirector d, _, _) = World();
        UnitRuntime w2 = d.Player.UnitRuntimeAt(2)!;
        w2.Weak = true;
        d.Buffs.Add(w2.Id, "virtue_resolute", source: null);
        Assert.IsTrue(WeakDeathsDoor.Roll(w2, afflicted: false, new ScriptedRng(85.0), d.Log,
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))), d.Buffs), "坚韧 +20pp → 90 → roll 85 存活");
    }

    [TestMethod]
    public void NextAttackBoost_AppliesOnceThenConsumed()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) = World();
        d.Buffs.Add(UnitId.Of("warrior"), "next_attack_boost", source: null);
        SkillExecutor ex = Executor(d, skills, rt);

        ex.Execute(skills.Get("warrior_cleave"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 1 });
        Assert.IsFalse(d.Buffs.Has(UnitId.Of("warrior"), "next_attack_boost"), "突进增伤为一次性（命中后移除）");
    }

    [TestMethod]
    public void VirtueInspired_TeamPlus3_EachRound_AndPoolHas4()
    {
        (BattleDirector d, _, _) = World();
        d.Buffs.Add(UnitId.Of("tank"), "virtue_inspired", source: null);
        d.StartTurn(new RngProvider(1));

        Assert.AreEqual(53, d.Player.UnitRuntimeAt(1)!.Morale, "振奋：坦克（战斗位）50→53");
        Assert.AreEqual(56, d.Player.UnitRuntimeAt(5)!.Morale, "支援位另计 +3（50+3振奋+3支援）");

        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(4, tuning.Collapse.VirtuePool.Count, "F2：美德池 4 个全量（O-27）");
        Assert.AreEqual(3, tuning.Collapse.VirtueInspiredMoralePerTurn, "振奋 +3/回合（本包定值）");
    }
}