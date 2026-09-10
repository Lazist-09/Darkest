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
/// 实例身份修复回归（试玩日志实测 bug）：同原型多实例（2/5 位 warrior、4/6 位 medic）
/// 曾共用原型 id → NextActor 弹出 6 位 medic 时，技能/换位/目标解析都 FirstOrDefault 落到 4 位，
/// 表现为「同一角色连续行动两次」与「选目标点不动（候选池按错误 caster 计算）」。
/// 修复：实例 Id 唯一化（warrior_2 / medic_2…），原型匹配改用 ArchetypeId。
/// </summary>
[TestClass]
public sealed class UnitIdentityTests
{
    private static string FindDataFile(string name)
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
            FormationConfig.Parse(FindDataFile("formation.json")),
            UnitsConfig.Parse(FindDataFile("units.json")),
            SkillsConfig.Parse(FindDataFile("skills.json")),
            BalanceTable.FromTuning(TuningConfig.Parse(FindDataFile("tuning.json"))),
            MoraleEventsConfig.Parse(FindDataFile("morale_events.json")),
            BuffDefsConfig.Parse(FindDataFile("buff_defs.json")),
            EnemyAiConfig.Parse(FindDataFile("enemy_ai.json")),
            log);
    }

    [TestMethod]
    public void DuplicateArchetypes_GetUniqueInstanceIds_ArchetypePreserved()
    {
        BattleDirector d = NewDirector(out _);

        // 我方编成 1=tank 2=warrior 3=commissar 4=medic 5=warrior 6=medic
        Assert.AreEqual("warrior", d.Player.UnitRuntimeAt(2)!.Id.Value);
        Assert.AreEqual("warrior_2", d.Player.UnitRuntimeAt(5)!.Id.Value, "5 位战士实例 id 唯一化");
        Assert.AreEqual("medic", d.Player.UnitRuntimeAt(4)!.Id.Value);
        Assert.AreEqual("medic_2", d.Player.UnitRuntimeAt(6)!.Id.Value, "6 位军医实例 id 唯一化");
        Assert.AreEqual("warrior", d.Player.UnitRuntimeAt(5)!.ArchetypeId, "ArchetypeId 保留原型（技能池/AI/中文名匹配）");
        Assert.AreEqual("medic", d.Player.UnitRuntimeAt(6)!.ArchetypeId);

        // 敌方 2 个近战小兵同样唯一化
        Assert.AreEqual("melee_soldier", d.Enemy.UnitRuntimeAt(1)!.Id.Value);
        Assert.AreEqual("melee_soldier_2", d.Enemy.UnitRuntimeAt(2)!.Id.Value);
        Assert.AreEqual("melee_soldier", d.Enemy.UnitRuntimeAt(2)!.ArchetypeId);
    }

    [TestMethod]
    public void UseSkill_TargetsTheExactInstance_NotFirstMatch()
    {
        BattleDirector d = NewDirector(out _);
        var rng = new RngProvider(11);

        // 5 位 warrior_2 受伤 → 用「喘息」（self，回 8）→ 只有 5 位回血，2 位不受影响
        d.Player.UnitRuntimeAt(5)!.CurrentHp = 20;
        int hp2Before = d.Player.UnitRuntimeAt(2)!.CurrentHp;
        d.PlayerUseSkill(UnitId.Of("warrior_2"), "warrior_catch_breath", rng, chosenTargets: null);

        Assert.AreEqual(28, d.Player.UnitRuntimeAt(5)!.CurrentHp, "自疗作用于 5 位实例（修复前会落到 2 位）");
        Assert.AreEqual(hp2Before, d.Player.UnitRuntimeAt(2)!.CurrentHp, "2 位实例不受影响");
    }

    [TestMethod]
    public void NextActor_OrderCoversBothInstances_OnceEach()
    {
        BattleDirector d = NewDirector(out _);
        d.StartTurn(new RngProvider(20260909));

        var seen = new List<string>();
        UnitId? actor;
        while ((actor = d.NextActor()) is { } id)
        {
            seen.Add(id.Value);
        }

        Assert.AreEqual(10, seen.Count, "回合队列 = 我方 6 + 敌方 4（含两实例）");
        Assert.AreEqual(1, seen.Count(s => s == "warrior"), "warrior 实例恰好一次");
        Assert.AreEqual(1, seen.Count(s => s == "warrior_2"), "warrior_2 实例恰好一次");
        Assert.AreEqual(1, seen.Count(s => s == "medic"), "medic 实例恰好一次");
        Assert.AreEqual(1, seen.Count(s => s == "medic_2"), "medic_2 实例恰好一次");
    }
}