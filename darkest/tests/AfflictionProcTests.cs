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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// F2（#193）折磨 proc 三件套：恐惧（用技能前 33% 拒放，不消耗行动，勇猛豁免）、
/// 自私（被治疗 33% 拒一次，本次治疗 0）、失控（攻击 33% 在合法候选池内换目标；支援位→捆缚）。
/// 概率唯一来源 tuning.affliction_proc_percent，所有抽取写 RngDraw。
/// </summary>
[TestClass]
public sealed class AfflictionProcTests
{
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _p;
        private readonly Queue<int> _ints;
        private ulong _d;

        public ScriptedRng(double[]? percents = null, int[]? ints = null)
        {
            _p = new Queue<double>(percents ?? Array.Empty<double>());
            _ints = new Queue<int>(ints ?? Array.Empty<int>());
        }

        public double NextPercent()
        {
            _d++;
            return _p.Count > 0 ? _p.Dequeue() : 0.0;
        }

        public int NextInt(int a, int b)
        {
            _d++;
            return _ints.Count > 0 ? _ints.Dequeue() : a;
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
    public void Fear_RefusalRate_About33Percent_AndWritesRngDraw()
    {
        (BattleDirector d, _, _) = World();
        d.Buffs.Add(UnitId.Of("warrior"), "affliction_fear", source: null);

        var rng = new RngProvider(20260909);
        int refused = 0;
        const int runs = 2000;
        for (int i = 0; i < runs; i++)
        {
            if (d.TryFearRefusal(UnitId.Of("warrior"), rng))
            {
                refused++;
            }
        }

        double rate = (double)refused / runs;
        Assert.IsTrue(rate is > 0.28 and < 0.38, $"恐惧拒放率 ≈33%（实测 {rate:P1}）");
        Assert.IsTrue(d.Log.Events.OfType<RngDraw>().Count() >= runs, "每次 proc 掷骰必写 RngDraw");
    }

    [TestMethod]
    public void Fear_ImmuneByBrave_NoRoll()
    {
        (BattleDirector d, _, _) = World();
        d.Buffs.Add(UnitId.Of("warrior"), "affliction_fear", source: null);
        d.Buffs.Add(UnitId.Of("warrior"), "virtue_brave", source: null);

        var rng = new RngProvider(1);
        ulong before = rng.DrawCount;
        for (int i = 0; i < 200; i++)
        {
            Assert.IsFalse(d.TryFearRefusal(UnitId.Of("warrior"), rng), "勇猛 immune_fear → 永不拒放");
        }

        Assert.AreEqual(before, rng.DrawCount, "免疫时不掷骰（无意义抽取）");
    }

    [TestMethod]
    public void Selfish_RefusesHeal_ZeroHealing()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) = World();
        FormationBoard target = d.Player;
        target.UnitRuntimeAt(2)!.CurrentHp = 10; // 战士重伤
        d.Buffs.Add(UnitId.Of("warrior"), "affliction_selfish", source: null);

        SkillExecutor ex = Executor(d, skills, rt);
        ex.Execute(skills.Get("medic_first_aid"), UnitId.Of("medic"), d.Player, d.Enemy,
            new ScriptedRng(ints: new[] { 0 }), chosenTargets: new[] { 2 }); // roll 0 < 33 → 拒疗

        Assert.AreEqual(10, target.UnitRuntimeAt(2)!.CurrentHp, "自私拒疗 → 治疗量为 0");
        Assert.AreEqual(0, d.Log.Events.OfType<HealEvent>().Count(), "无治疗事件");
        Assert.IsTrue(d.Log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "selfish_refuse"), "拒疗事件已记录");
    }

    [TestMethod]
    public void Uncontrolled_Retargets_WithinCandidates()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) = World();
        d.Buffs.Add(UnitId.Of("warrior"), "affliction_uncontrolled", source: null);
        IReadOnlyList<int> candidates = SkillTargetResolver.Resolve(skills.Get("warrior_cleave"), UnitId.Of("warrior"), d.Player, d.Enemy);

        SkillExecutor ex = Executor(d, skills, rt);
        ex.Execute(skills.Get("warrior_cleave"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(new[] { 0.0, 100.0 }, ints: new[] { 0, 0 }), chosenTargets: new[] { candidates[0] });

        DamageEvent dmg = d.Log.Events.OfType<DamageEvent>().Single();
        Assert.IsTrue(candidates.Contains(d.Enemy.UnitAtPosition(dmg.Target!.Value) ?? -1),
            "失控换目标仍在合法候选池内");
        Assert.IsTrue(d.Log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "uncontrolled_retarget"), "换目标已记录");
    }

    [TestMethod]
    public void Uncontrolled_SupportSlot_BecomesBound()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) = World();
        // 把 5 位（支援位）单位设为失控；其可用技能（喘息）在支援位可放
        UnitId supportUnit = d.Player.UnitRuntimeAt(5)!.Id;
        d.Buffs.Add(supportUnit, "affliction_uncontrolled", source: null);

        SkillExecutor ex = Executor(d, skills, rt);
        ex.Execute(skills.Get("warrior_catch_breath"), supportUnit, d.Player, d.Enemy, new ScriptedRng());

        Assert.IsTrue(d.Buffs.Has(supportUnit, "bound"), "支援位失控 → 改为捆缚（#48）");
        Assert.AreEqual(0, d.Log.Events.OfType<HealEvent>().Count(), "失控支援位不产生治疗");
    }
}