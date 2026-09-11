using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// G3（O-56）详情框数据面 + G4（O-57）意图预留：
/// ① 预估与结算同源（同假设下逐点一致）；② 预估/详情/意图预览零抽取零写状态（确定性红线）；
/// ③ 敌方属性全暴露；④ 意图默认关闭、按需开启且不消耗战斗随机数。
/// </summary>
[TestClass]
public sealed class ProjectionIntentTests
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

    private static (BattleDirector d, BattleProjector p, SkillsConfig skills, SkillRuntimeState rt) World()
    {
        var log = new CombatLog();
        var skills = SkillsConfig.Parse(ReadData("skills.json"));
        var rt = new SkillRuntimeState();
        BalanceTable balance = BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json")));
        var d = new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            skills,
            balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        var p = new BattleProjector(d, balance, skills, rt);
        return (d, p, skills, rt);
    }

    private static SkillExecutor Executor(BattleDirector d, SkillsConfig skills, SkillRuntimeState rt)
        => new(skills, BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), d.Log, rt, d.Buffs);

    [TestMethod]
    public void Estimate_MatchesExecution_WhenNoCritNoFloat()
    {
        (BattleDirector d, BattleProjector p, SkillsConfig skills, SkillRuntimeState rt) = World();
        TargetEstimate est = p.Estimate("warrior_cleave", UnitId.Of("warrior"), 1, targetIsPlayer: false);

        // 同假设结算：不暴击（暴击掷 100）→ 实伤应与预估一致
        Executor(d, skills, rt).Execute(skills.Get("warrior_cleave"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 1 });
        DamageEvent dmg = d.Log.Events.OfType<DamageEvent>().Single();

        Assert.AreEqual(est.EstimatedDamage, dmg.Amount, "预估伤害 == 结算实伤（无暴击/无浮动）");
        Assert.AreEqual(1, est.Segments, "劈砍 1 段");
    }

    [TestMethod]
    public void EstimateAndDetail_HaveNoSideEffects()
    {
        (BattleDirector d, BattleProjector p, _, _) = World();
        var rng = new RngProvider(31);
        ulong drawsBefore = rng.DrawCount;
        int eventsBefore = d.Log.Count;

        for (int i = 0; i < 20; i++)
        {
            p.Estimate("warrior_cleave", UnitId.Of("warrior"), 1, targetIsPlayer: false);
            p.Detail(false, 1);
            p.Detail(true, 1);
            p.IntentPreview(UnitId.Of("melee_soldier"), rng);
        }

        Assert.AreEqual(drawsBefore, rng.DrawCount, "预估/详情/意图预览不得抽取战斗随机数（确定性红线）");
        Assert.AreEqual(eventsBefore, d.Log.Count, "预估/详情/意图预览不得写战斗事件流");
    }

    [TestMethod]
    public void EnemyDetail_FullyExposed()
    {
        (BattleDirector _, BattleProjector p, _, _) = World();
        UnitDetail enemy = p.Detail(player: false, 1);

        Assert.AreEqual("melee_soldier", enemy.Archetype, "敌方原型暴露");
        Assert.AreEqual(32, enemy.MaxHp, "敌方精确 HP 暴露（v0.68：近战小兵 48→32）");
        Assert.IsTrue(enemy.PhysDef > 0 && enemy.Speed > 0, "物防/速度暴露");
        Assert.IsTrue(enemy.StunResist >= 0 && enemy.BleedResist >= 0 && enemy.DisplaceResist >= 0, "四类抗性暴露");
        Assert.AreEqual(2, enemy.SkillIds.Count, "敌方技能表暴露（近战小兵 2 条）");
    }

    [TestMethod]
    public void IntentPreview_DisabledDefault_EnabledOnDemand_NoBattleRngCost()
    {
        (BattleDirector _, BattleProjector p, _, _) = World();
        var rng = new RngProvider(32);
        ulong before = rng.DrawCount;

        IntentProjection off = p.IntentPreview(UnitId.Of("melee_soldier"), rng);
        Assert.IsNull(off.SkillId, "G4：切片默认不显示敌方意图");

        IntentProjection on = p.IntentPreview(UnitId.Of("melee_soldier"), rng, enabled: true);
        Assert.IsNotNull(on.SkillId, "开启后产出意图（后续侦察技能可读）");
        Assert.IsTrue(on.TargetSlots.Length >= 1, "意图含目标位");
        Assert.AreEqual(before, rng.DrawCount, "意图预览不消耗战斗随机数");
    }
}