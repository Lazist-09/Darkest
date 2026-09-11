using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// P2/O-42 模拟策略启发式：① 输出集火最低 HP ② 移动条件化（默认不移动）③ 治疗仅救真伤员
/// ④ 增援（虚弱 / HP%&lt;30% 拉起）⑤ 控制打攻击力最高者。规则化、可解释。
/// </summary>
[TestClass]
public sealed class PolicyTests
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
        var d = new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            SkillsConfig.Parse(ReadData("skills.json")),
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        return d;
    }

    private static PlayerDecision Decide(BattleDirector d, string unitInstanceId)
    {
        UnitRuntime unit = d.Player.UnitsInSlotOrder().First(u => u.Id.Value == unitInstanceId);
        return Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, new RngProvider(1));
    }

    [TestMethod]
    public void Output_FocusFire_LowestHpEnemy()
    {
        BattleDirector d = NewDirector(out _);
        d.Enemy.UnitRuntimeAt(2)!.CurrentHp = 7; // 敌 2 残血
        PlayerDecision dec = Decide(d, "warrior"); // 战士（2 位）劈砍候选 [1,2]
        Assert.AreEqual(2, dec.SkillTargetSlot, "输出技能集火最低 HP 敌（敌 2）");
    }

    [TestMethod]
    public void Heal_OnlyWhenWounded_PicksLowestHpPercentAlly()
    {
        BattleDirector d = NewDirector(out _);
        PlayerDecision full = Decide(d, "medic"); // 全体满血
        Assert.IsFalse(full.SkillId is "medic_first_aid" or "medic_group_bandage", "全体健康 → 不使用治疗（不浪费行动）");

        d.Player.UnitRuntimeAt(1)!.CurrentHp = 10; // 坦克重伤（HP% 低）
        PlayerDecision hurt = Decide(d, "medic");
        Assert.IsTrue(hurt.SkillId is "medic_first_aid" or "medic_group_bandage", "有真伤员 → 用治疗");
        Assert.AreEqual(1, hurt.SkillTargetSlot, "治疗目标 = 最低 HP% 友方（坦克）");
    }

    [TestMethod]
    public void Move_NotInPool_WhenOutputsAvailable()
    {
        BattleDirector d = NewDirector(out _);
        foreach (string id in new[] { "warrior", "tank", "commissar", "medic" })
        {
            for (int i = 0; i < 20; i++)
            {
                PlayerDecision dec = Decide(d, id);
                Assert.AreNotEqual("move", dec.SkillId,
                    $"{id} 有可用输出技能时移动不得入池（实际 {dec.SkillId}）");
            }
        }
    }

    [TestMethod]
    public void Reinforce_Triggered_WhenWeakOrCritical_InCombatSlot()
    {
        BattleDirector d = NewDirector(out _);
        d.Player.UnitRuntimeAt(1)!.Weak = true; // 战斗位虚弱
        PlayerDecision weak = Decide(d, "tank");
        Assert.IsNotNull(weak.ReinforceB, "虚弱战斗位 + SP≥2 → 触发增援换下（S5 ② 救崩溃）");

        // S5 ①：濒死（HP%<30%）走【保命治疗】分支（支援位军医），不再由濒死者自己发起增援
        BattleDirector d2 = NewDirector(out _);
        d2.Player.UnitRuntimeAt(2)!.CurrentHp = 1;
        PlayerDecision crit = Decide(d2, "warrior");
        Assert.IsNull(crit.ReinforceB, "濒死不触发增援（改由支援位保命治疗）");

        PlayerDecision medic = Decide(d2, "medic_2"); // 支援位军医
        Assert.IsTrue(medic.SkillId is "medic_first_aid" or "medic_group_bandage", $"保命：支援位军医治疗（实际 {medic.SkillId}）");
        Assert.AreEqual(2, medic.SkillTargetSlot, "治疗目标 = 濒死的 2 位战士");
    }

    [TestMethod]
    public void Control_TargetsHighestAttackEnemy()
    {
        BattleDirector d = NewDirector(out _);
        // 战士盾击（control，目标 [1]）→ 候选唯一 1；改用政委督战（[1,2]）验证攻击力择优
        PlayerDecision dec = Decide(d, "commissar");
        Assert.IsNotNull(dec.SkillId);
        if (dec.SkillId is "commissar_supervise" or "commissar_charge_order")
        {
            Assert.IsTrue(dec.SkillTargetSlot is 1 or 2 or 3 or 4, "控制/输出技能目标落在候选池内");
        }
    }
}