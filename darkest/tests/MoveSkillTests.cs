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
/// 池外「移动」（#180，O-41）：候选=自身±N 被占用战斗位；执行=与目标位【直接互换】
/// （原目标位的人到自身原位，途经槽位不动）；不过抗性、无伤害/士气 → 不触发死门（#117）；
/// 空位不可选 → NoTarget（#21）。距离：坦克 1 / 战士·军医·政委 2（character §7.1b）。
/// </summary>
[TestClass]
public sealed class MoveSkillTests
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
        return (d, skills, rt);
    }

    [TestMethod]
    public void MoveRange_Candidates_SelfPlusMinusN_OccupiedCombatOnly()
    {
        (BattleDirector d, SkillsConfig skills, _) = World();
        SkillTemplateConfig tankMove = skills.Get("tank_move"); // distance 1

        // 坦克在 1：候选仅 2（±1 内被占用战斗位；自身排除）
        IReadOnlyList<int> c1 = SkillTargetResolver.Resolve(tankMove, UnitId.Of("tank"), d.Player, d.Enemy);
        CollectionAssert.AreEqual(new[] { 2 }, c1.ToArray(), "距离 1 → 仅相邻战斗位 2");

        // 战士（距离 2）在 2：候选 = 1..4 中的被占用位（±2 内，排除自身）
        SkillTemplateConfig warriorMove = skills.Get("warrior_move");
        IReadOnlyList<int> c2 = SkillTargetResolver.Resolve(warriorMove, UnitId.Of("warrior"), d.Player, d.Enemy);
        CollectionAssert.AreEqual(new[] { 1, 3, 4 }, c2.ToArray(), "距离 2 → 1/3/4（2 为自身排除）");
    }

    [TestMethod]
    public void MoveRange_EmptyNeighbor_NoTarget()
    {
        (BattleDirector d, SkillsConfig skills, _) = World();
        d.Player.RemoveUnitAt(2); // 坦克唯一相邻位空出
        IReadOnlyList<int> c = SkillTargetResolver.Resolve(skills.Get("tank_move"), UnitId.Of("tank"), d.Player, d.Enemy);
        Assert.AreEqual(0, c.Count, "空位不可选 → NoTarget（#21）");
    }

    [TestMethod]
    public void Move_Execution_DirectSwap_NoDamageNoMoraleNoDeathDoor_MiddleUntouched()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) = World();
        var log = d.Log;
        var executor = new SkillExecutor(skills, BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), log, rt);
        string slot2Before = d.Player.UnitRuntimeAt(2)!.Id.Value;
        string slot3Before = d.Player.UnitRuntimeAt(3)!.Id.Value;

        // 战士（2 位，距离 2）移动到 4：直接互换 → 战士@4、原4（军医）@2、3 位不动
        executor.Execute(skills.Get("warrior_move"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(), chosenTargets: new[] { 4 });

        Assert.AreEqual("warrior", d.Player.UnitRuntimeAt(4)!.Id.Value, "自身到目标位 4");
        Assert.AreEqual("medic", d.Player.UnitRuntimeAt(2)!.Id.Value, "原 4 位军医到自身原位 2（直接互换）");
        Assert.AreEqual(slot3Before, d.Player.UnitRuntimeAt(3)!.Id.Value, "途经 3 位不动");
        Assert.IsTrue(log.Events.OfType<DisplaceEvent>().Any(e => e.PassedResist), "移动事件（不过抗性）计入位移 KPI");
        Assert.IsFalse(log.Events.OfType<DamageEvent>().Any(), "移动无伤害");
        Assert.IsFalse(log.Events.OfType<MoraleEvent>().Any(), "移动无士气变化");
        Assert.IsFalse(log.Events.OfType<DeathDoorEvent>().Any(), "移动不触发死门（#117）");
    }

    [TestMethod]
    public void Move_Execution_ChosenOutsideCandidates_IgnoredOrFallback()
    {
        (BattleDirector d, SkillsConfig skills, SkillRuntimeState rt) = World();
        var executor = new SkillExecutor(skills, BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), d.Log, rt);
        string slot1Before = d.Player.UnitRuntimeAt(1)!.Id.Value;

        // 坦克（1 位，距离 1）候选唯一（仅 2）→ 不进入选一分支，指定非法目标 4 被忽略（按唯一候选执行）
        executor.Execute(skills.Get("tank_move"), UnitId.Of("tank"), d.Player, d.Enemy,
            new ScriptedRng(0.0), chosenTargets: new[] { 4 });

        Assert.AreEqual("warrior", d.Player.UnitRuntimeAt(1)!.Id.Value, "唯一候选 2 → 2 位战士到 1");
        Assert.AreEqual(slot1Before, d.Player.UnitRuntimeAt(2)!.Id.Value, "坦克到 2");
    }
}