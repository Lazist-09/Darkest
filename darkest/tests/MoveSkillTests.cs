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
/// F1（#191）池外通用「移动」：技能 id = `move`（不绑 owner_unit）、射程从**单位** `move_distance` 读
/// （坦克 1 / 战医政 2）；候选 = 自身 ±N 被占用战斗位；执行=两点直接互换（不过抗性/无伤害/不死门）。
/// </summary>
[TestClass]
public sealed class MoveSkillTests
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
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), d.Log, rt);

    [TestMethod]
    public void MoveCandidates_UseUnitMoveDistance()
    {
        (BattleDirector d, SkillsConfig skills, _) = World();
        SkillTemplateConfig move = skills.Get("move");

        // 坦克（1 位，move_distance 1）→ 仅相邻战斗位 2
        IReadOnlyList<int> tank = SkillTargetResolver.Resolve(move, UnitId.Of("tank"), d.Player, d.Enemy);
        CollectionAssert.AreEqual(new[] { 2 }, tank.ToArray(), "坦克距离 1 → 仅 2 位");

        // 战士（2 位，move_distance 2）→ 1/3/4（排除自身 2）
        IReadOnlyList<int> warrior = SkillTargetResolver.Resolve(move, UnitId.Of("warrior"), d.Player, d.Enemy);
        CollectionAssert.AreEqual(new[] { 1, 3, 4 }, warrior.ToArray(), "战士距离 2 → 1/3/4");
    }

    [TestMethod]
    public void MoveRange_EmptyNeighbor_NoTarget()
    {
        (BattleDirector d, SkillsConfig skills, _) = World();
        d.Player.RemoveUnitAt(2);
        Assert.AreEqual(0, SkillTargetResolver.Resolve(skills.Get("move"), UnitId.Of("tank"), d.Player, d.Enemy).Count,
            "空位不可选 → NoTarget（#21）");
    }

    [TestMethod]
    public void Move_Execution_DirectSwap_NoDamageNoMoraleNoDeathDoor()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) = World();
        var log = d.Log;
        SkillExecutor ex = Executor(d, skills, rt);
        string slot3Before = d.Player.UnitRuntimeAt(3)!.Id.Value;

        // 战士（2 位，距离 2）移动到 4：直接互换 → 战士@4、原 4（军医）@2、3 位不动
        ex.Execute(skills.Get("move"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(), chosenTargets: new[] { 4 });

        Assert.AreEqual("warrior", d.Player.UnitRuntimeAt(4)!.Id.Value, "自身到目标位 4");
        Assert.AreEqual("medic", d.Player.UnitRuntimeAt(2)!.Id.Value, "原 4 位军医到自身原位 2");
        Assert.AreEqual(slot3Before, d.Player.UnitRuntimeAt(3)!.Id.Value, "途经 3 位不动");
        Assert.IsTrue(log.Events.OfType<DisplaceEvent>().Any(e => e.PassedResist), "移动事件（不过抗性）");
        Assert.AreEqual(0, log.Events.OfType<DamageEvent>().Count(), "移动无伤害");
        Assert.AreEqual(0, log.Events.OfType<MoraleEvent>().Count(), "移动无士气变化");
        Assert.AreEqual(0, log.Events.OfType<DeathDoorEvent>().Count(), "移动不触发死门（#117）");
    }

    [TestMethod]
    public void Move_Execution_ChosenOutsideCandidates_UsesOnlyCandidate()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) = World();
        SkillExecutor ex = Executor(d, skills, rt);
        string slot1Before = d.Player.UnitRuntimeAt(1)!.Id.Value;

        // 坦克唯一候选 2；指定非法 4 → 按唯一候选执行
        ex.Execute(skills.Get("move"), UnitId.Of("tank"), d.Player, d.Enemy,
            new ScriptedRng(0.0), chosenTargets: new[] { 4 });

        Assert.AreEqual("warrior", d.Player.UnitRuntimeAt(1)!.Id.Value, "唯一候选 2 → 2 位战士到 1");
        Assert.AreEqual(slot1Before, d.Player.UnitRuntimeAt(2)!.Id.Value, "坦克到 2");
    }
}