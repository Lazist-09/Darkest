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
using Darkest.Gameplay.Sim.Pipeline;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// F0（#189）强制目标点击：候选池非空一律需点选确认；点击后各 scope 的**生效数量**正确——
/// 单体=1、AOE=整池、team=全队、self=自身；候选池数量与 scope 对应（数量型断言）。
/// </summary>
[TestClass]
public sealed class TargetClickFlowTests
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

    private static (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt, CombatLog log) World()
    {
        var log = new CombatLog();
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
            log);
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        return (d, skills, rt, log);
    }

    private static SkillExecutor Executor(BattleDirector d, SkillsConfig skills, SkillRuntimeState rt, CombatLog log)
        => new(skills, BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), log, rt);

    [TestMethod]
    public void Candidates_ByScope_CountsMatch()
    {
        (BattleDirector d, SkillsConfig skills, _, _) = World();

        // self（铁壁）：候选 = 自身所在位，恰 1 个
        IReadOnlyList<int> self = SkillTargetResolver.Resolve(skills.Get("tank_iron_wall"), UnitId.Of("tank"), d.Player, d.Enemy);
        Assert.AreEqual(1, self.Count, "self 候选恰 1（自己的卡）");

        // team（战吼）：候选 = 我方全部非空位 = 6
        IReadOnlyList<int> team = SkillTargetResolver.Resolve(skills.Get("warrior_war_cry"), UnitId.Of("warrior"), d.Player, d.Enemy);
        Assert.AreEqual(6, team.Count, "team 候选 = 我方全部非空位（6）");

        // AOE（横扫）：候选 = 范围内非空位（敌 1/2/3）= 3
        IReadOnlyList<int> aoe = SkillTargetResolver.Resolve(skills.Get("warrior_sweep"), UnitId.Of("warrior"), d.Player, d.Enemy);
        Assert.AreEqual(3, aoe.Count, "AOE 候选 = 范围内全部非空位（3）");

        // 单体（劈砍）：候选 = 范围内非空位（敌 1/2）= 2
        IReadOnlyList<int> single = SkillTargetResolver.Resolve(skills.Get("warrior_cleave"), UnitId.Of("warrior"), d.Player, d.Enemy);
        Assert.AreEqual(2, single.Count, "单体候选 = 范围内非空位（2）");
    }

    [TestMethod]
    public void Aoe_ClickAnyCard_HitsWholePool()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt, CombatLog log) = World();
        SkillExecutor ex = Executor(d, skills, rt, log);

        // 横扫候选 3 个：点其中一张（槽 2）确认 → 命中整池（3 条伤害事件）
        ex.Execute(skills.Get("warrior_sweep"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 100.0, 0.0, 100.0, 0.0, 100.0), chosenTargets: new[] { 2 });

        Assert.AreEqual(3, log.Events.OfType<DamageEvent>().Count(), "AOE 点任意候选卡 → 命中整池（3 个目标）");
    }

    [TestMethod]
    public void Team_ClickAnyAllyCard_WholeTeamEffected()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt, CombatLog log) = World();
        SkillExecutor ex = Executor(d, skills, rt, log);

        // 战吼（team）：点一张友方卡确认 → 全队 6 人各 +5 士气
        ex.Execute(skills.Get("warrior_war_cry"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(), chosenTargets: new[] { 4 });

        Assert.AreEqual(6, log.Events.OfType<MoraleEvent>().Count(), "team 点任意友方卡 → 全队 6 人生效");
        Assert.IsTrue(d.Player.UnitsInSlotOrder().All(u => u.Morale == 55), "全队士气 50→55");
    }

    [TestMethod]
    public void Self_ClickOwnCard_OnlySelfEffected()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt, CombatLog log) = World();
        SkillExecutor ex = Executor(d, skills, rt, log);

        // 铁壁（self）：点自己的卡（槽 1）→ 只有自己获得护盾（charges 2）
        ex.Execute(skills.Get("tank_iron_wall"), UnitId.Of("tank"), d.Player, d.Enemy,
            new ScriptedRng(), chosenTargets: new[] { 1 });

        Assert.AreEqual(1, log.Events.OfType<EffectEvent>().Count(e => e.EffectType == "shield"), "self 仅自己获得护盾（恰 1 条）");
    }

    [TestMethod]
    public void EmptyPool_SkillNotUsable()
    {
        (BattleDirector d, SkillsConfig skills, _, _) = World();
        d.Enemy.RemoveUnitAt(1);
        d.Enemy.RemoveUnitAt(2);
        d.Enemy.RemoveUnitAt(3);

        // 横扫范围仅敌 1/2/3 → 全空 → 候选池 0（UI 应灰显，不进选目标）
        Assert.AreEqual(0, SkillTargetResolver.Resolve(skills.Get("warrior_sweep"), UnitId.Of("warrior"), d.Player, d.Enemy).Count,
            "候选池为空 → 不可点（灰显）");
    }
}