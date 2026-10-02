using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Pipeline;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// ① 从 `DdAdoptionTests.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **可用性 / AOE 士气 / 暴击联动**（`D5` requires 灰显与理由 ／ `D6` AOE 多暴击全队士气只结算一次 ／ `D7` 暴击震慑与暴击治疗）✓
/// ③ 只搬家、零行为改动 ✓（唯一非逐字节动作：类声明加 `partial`）
/// </summary>
public sealed partial class DdAdoptionTests
{
    [TestMethod]
    public void D7_CritHit_ShocksSelfMinus10_AndAllies50PercentMinus5()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = SkillsConfig.Parse(ReadData("skills.json"));
        var rt = new Darkest.Gameplay.Sim.Skill.SkillRuntimeState();
        var executor = new Darkest.Gameplay.Sim.Skill.SkillExecutor(skills, balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), new CombatLog(), rt, d.Buffs);

        // 敌方近战小兵暴击打我方 1 位（坦克）：暴击掷 0、队友震慑掷 0（全部触发）
        var execLog = new CombatLog();
        var executor2 = new Darkest.Gameplay.Sim.Skill.SkillExecutor(skills, balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), execLog, rt, d.Buffs);
        executor2.Execute(skills.Get("melee_heavy_slash"), UnitId.Of("melee_soldier"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0), chosenTargets: new[] { 1 });

        MoraleEvent[] shocks = execLog.Events.OfType<MoraleEvent>()
            .Where(e => e.Source is "physical_crit_hit_self" or "physical_crit_hit_ally").ToArray();
        Assert.AreEqual(1, shocks.Count(e => e.Source == "physical_crit_hit_self"), "被暴击者自身恰 −10 一次");
        Assert.AreEqual(-10, shocks.Single(e => e.Source == "physical_crit_hit_self").Delta);
        Assert.IsTrue(shocks.Any(e => e.Source == "physical_crit_hit_ally" && e.Delta == -5), "队友 50% 触发 → −5");
        Assert.IsTrue(execLog.Events.OfType<RngDraw>().Count() >= 5, "队友震慑判定写 RngDraw（确定性红线）");
        Assert.IsTrue(execLog.Events.OfType<EffectEvent>().Any(e => e.EffectType == "crit_shock"), "震慑事件（UI 三态用）");
    }

    [TestMethod]
    public void D7_CritHeal_DoublesHealAndPlus4Morale_FixedChance()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = SkillsConfig.Parse(ReadData("skills.json"));
        var rt = new Darkest.Gameplay.Sim.Skill.SkillRuntimeState();

        // 单体治疗、暴击治疗判定掷 0（<12%）→ 治疗 ×2 + 目标 +4
        UnitRuntime warrior = d.Player.UnitRuntimeAt(2)!;
        warrior.CurrentHp = warrior.MaxHp - 30;
        int hpBefore = warrior.CurrentHp;
        int moraleBefore = warrior.Morale;
        var log = new CombatLog();
        var executor = new Darkest.Gameplay.Sim.Skill.SkillExecutor(skills, balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), log, rt, d.Buffs);
        executor.Execute(skills.Get("medic_first_aid"), UnitId.Of("medic"), d.Player, d.Enemy,
            new ScriptedRng(0.0), chosenTargets: new[] { 2 });

        HealEvent heal = log.Events.OfType<HealEvent>().Single();
        Assert.AreEqual(24, heal.Amount, "暴击治疗 → 12 × 2（数值撤回）");
        Assert.AreEqual(hpBefore + 24, warrior.CurrentHp);
        Assert.AreEqual(moraleBefore + 4, warrior.Morale, "被治疗者 +4 士气");
        Assert.IsTrue(log.Events.OfType<MoraleEvent>().Any(e => e.Source == "critical_heal"));
    }

    [TestMethod]
    public void D5_Requires_GatesAvailability_WithReason()
    {
        (BattleDirector d, BalanceTable _) = World();
        var skills = SkillsConfig.Parse(ReadData("skills.json"));
        var resolver = new Darkest.Gameplay.Sim.Skill.SkillUseResolver(skills);
        var carried = new HashSet<string> { "warrior_last_stand" };
        var rt = new Darkest.Gameplay.Sim.Skill.SkillRuntimeState();
        var ctx = new Darkest.Gameplay.Sim.Skill.SkillUseContext(
            skills.Get("warrior_last_stand"), UnitId.Of("warrior"), d.Player, d.Enemy, carried, rt, IsEnemy: false);

        // 满血 → 前置不满足（需自身 HP < 50%）→ 灰显 + 原因
        Availability unmet = resolver.Resolve(ctx);
        Assert.AreEqual(AvailabilityReason.RequiresUnmet, unmet.Reason, "不满足 requires → 灰显");
        Assert.IsTrue(unmet.Tooltip.Contains("50"), $"tooltip 说明阈值（实际 {unmet.Tooltip}）");

        // 压到 45% → 可点
        d.Player.UnitRuntimeAt(2)!.CurrentHp = d.Player.UnitRuntimeAt(2)!.MaxHp * 45 / 100;
        Assert.AreEqual(AvailabilityReason.Ok, resolver.Resolve(ctx).Reason, "满足 requires → 可点");
    }

    [TestMethod]
    public void D6_AoeMultiCrit_TeamMoraleAppliedOnce()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = SkillsConfig.Parse(ReadData("skills.json"));
        var rt = new Darkest.Gameplay.Sim.Skill.SkillRuntimeState();
        var executor = new Darkest.Gameplay.Sim.Skill.SkillExecutor(skills, balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), d.Log, rt, d.Buffs);

        // 横扫（AOE，命中敌 1/2）双目标全暴击 → 全队 +5 只结算一次（6 人 × 1 条，而不是 12 条）
        executor.Execute(skills.Get("warrior_sweep"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 0.0, 0.0, 0.0, 0.0, 0.0), chosenTargets: new[] { 1, 2 });

        int crit = d.Log.Events.OfType<CritEvent>().Count(e => e.Crit);
        int teamMorale = d.Log.Events.OfType<MoraleEvent>().Count(e => e.Source == "critical_strike_dealt");
        Assert.IsTrue(crit >= 2, $"AOE 双目标都暴击（实际 {crit}）");
        Assert.AreEqual(6, teamMorale, "全队 6 人各 +5 一次（不是按目标数 ×N 重复结算）");
    }
}
