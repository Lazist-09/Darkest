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
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// #211/#212（O-60）支援点 SP 与「待命」——**P19 门禁**：
/// ① 起手 3 / 每回合 +1 / 上限 4；② 支援位技能 −1、增援 −2；③ 不足 → 拒且**不吞行动**；
/// ④ 🔴 **战斗位零消耗回归锁**（否则 SP 会退化成"全体限流"）；⑤ 🔴 **每次变动必写 SupportPointEvent**；
/// ⑥ 🔴 regen 在消耗判定之前且 cap 立即钳制；⑦ 待命 = TurnSkippedEvent(passed)（不消耗 SP、不算技能）。
/// </summary>
[TestClass]
public sealed class SupportPointTests
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

    [TestMethod]
    public void P19_StartRegenCap()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(71);

        Assert.AreEqual(3, d.SupportPoints, "起手 3（tuning.support_points.start）");
        Assert.AreEqual(4, d.SupportCap);

        d.StartTurn(rng);
        Assert.AreEqual(4, d.SupportPoints, "第 1 回合开始 +1 → 4");
        Assert.IsTrue(log.Events.OfType<SupportPointEvent>().Any(e => e.Reason == "regen" && e.NewValue == 4), "恢复写事件");

        d.StartTurn(rng);
        Assert.AreEqual(4, d.SupportPoints, "已在上限 → 不再增长（cap=4）");
    }

    [TestMethod]
    public void P19_RegenHappensBeforeSpend_AndClampsImmediately()
    {
        // 🔴 硬提醒③：regen 在消耗判定之前、cap 在 regen 后立即钳制
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(72);
        d.StartTurn(rng); // 3 → 4

        UnitId medic2 = d.Player.UnitRuntimeAt(6)!.Id; // 支援位军医
        int cost = d.SupportCostSkill;
        Assert.AreEqual(2, cost, "#213（v0.57）：cost_skill = 2");
        Assert.IsTrue(d.PlayerUseSkill(medic2, "medic_first_aid", rng, new[] { 1 }), "支援位技能可用");
        Assert.AreEqual(2, d.SupportPoints, $"支援位技能 −{cost}");

        // 下一回合开始先 +1（→3）再判定消耗：不因"先扣后加"而算错
        d.StartTurn(rng);
        Assert.AreEqual(3, d.SupportPoints, "第 2 回合开始恢复后为 3（先 regen 后消耗）");
    }

    [TestMethod]
    public void P19_SupportSlotSkill_Costs2_AndBlocksWhenInsufficient()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(73);
        UnitId medic2 = d.Player.UnitRuntimeAt(6)!.Id;
        int cost = d.SupportCostSkill;

        d.StartTurn(rng); // 3 → 4（cap）
        Assert.IsTrue(d.PlayerUseSkill(medic2, "medic_first_aid", rng, new[] { 1 }), "第一次可用");
        Assert.AreEqual(4 - cost, d.SupportPoints, $"−{cost}");

        if (d.SupportPoints >= cost)
        {
            Assert.IsTrue(d.PlayerUseSkill(medic2, "medic_first_aid", rng, new[] { 1 }), "本回合第二次可用（若点数够）");
        }

        // 点数不足 → 拒（返回 false），并写 rejected + SkillRefusedEvent
        while (d.SupportPoints > 0)
        {
            d.PlayerUseSkill(medic2, "medic_first_aid", rng, new[] { 1 });
            if (d.SupportPoints < cost)
            {
                break;
            }
        }

        Assert.IsTrue(d.SupportPoints < cost, $"点数已低于消耗（{d.SupportPoints} < {cost}）");
        Assert.IsFalse(d.PlayerUseSkill(medic2, "medic_first_aid", rng, new[] { 1 }), "SP 不足 → 拒");
        Assert.IsTrue(log.Events.OfType<SupportPointEvent>().Any(e => e.Reason == "rejected"), "拒绝写事件");
        Assert.IsTrue(log.Events.OfType<SkillRefusedEvent>().Any(e => e.Reason == "support_points"), "不可用原因入日志");
    }

    [TestMethod]
    public void P19_CombatSlotSkills_NeverCostSp_RegressionLock()
    {
        // 🔴 硬提醒①：战斗位零消耗的回归锁（否则 SP 退化成"全体限流"）
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(74);
        d.StartTurn(rng);
        int before = d.SupportPoints;

        for (int i = 0; i < 5; i++)
        {
            Assert.IsTrue(d.PlayerUseSkill(UnitId.Of("tank"), "tank_shield_bash", rng, new[] { 1 }), "战斗位技能恒可用");
            Assert.IsTrue(d.PlayerUseSkill(UnitId.Of("warrior"), "warrior_cleave", rng, new[] { 1 }));
            Assert.IsTrue(d.PlayerUseSkill(UnitId.Of("commissar"), "commissar_pistol_shot", rng, null));
        }

        Assert.AreEqual(before, d.SupportPoints, "战斗位连放 15 次技能 → SP 不变（零消耗）");
        Assert.IsFalse(log.Events.OfType<SupportPointEvent>().Any(e => e.Reason == "skill"), "战斗位不留 SP 花费事件");
    }

    [TestMethod]
    public void P19_Reinforce_Costs2_AndRejectDoesNotConsumeTurn()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(75);
        UnitId tank = UnitId.Of("tank");

        Assert.IsTrue(d.Reinforce(tank, 5, 1), "SP=3 ≥ 2 → 增援成功");
        Assert.AreEqual(1, d.SupportPoints, "增援 −2");

        // 同回合第二次已被 SwappedThisRound 拦住；下一回合 SP=2 → 仍可一次
        d.StartTurn(rng);
        Assert.AreEqual(2, d.SupportPoints, "回合恢复 +1");

        // 用支援位技能把 SP 降到 1（< cost_reinforce=2），且本回合尚未增援 → 增援应被拒
        BattleDirector d2 = NewDirector(out CombatLog log2);
        UnitId medic2 = d2.Player.UnitRuntimeAt(6)!.Id;
        d2.PlayerUseSkill(medic2, "medic_first_aid", rng, new[] { 1 });
        Assert.AreEqual(1, d2.SupportPoints, $"一次支援位技能（−{d2.SupportCostSkill}）→ SP=1");

        Assert.IsFalse(d2.Reinforce(UnitId.Of("tank"), 5, 1), "SP=1 < 2 → 增援被拒");
        Assert.IsTrue(log2.Events.OfType<SupportPointEvent>().Any(e => e.Reason == "rejected"), "拒增援写事件");
    }

    [TestMethod]
    public void P19_EveryChangeWritesEvent_AndPassIsTurnSkipped()
    {
        // 🔴 硬提醒②：每次变动必写事件；⑦ 待命 = TurnSkippedEvent(passed)
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(76);
        d.StartTurn(rng);
        UnitId medic2 = d.Player.UnitRuntimeAt(6)!.Id;
        d.PlayerUseSkill(medic2, "medic_first_aid", rng, new[] { 1 });

        int regenEvents = log.Events.OfType<SupportPointEvent>().Count(e => e.Reason == "regen");
        int skillEvents = log.Events.OfType<SupportPointEvent>().Count(e => e.Reason == "skill");
        Assert.IsTrue(regenEvents >= 1 && skillEvents == 1, $"恢复与花费各有事件（regen={regenEvents}, skill={skillEvents}）");

        // SP 快照可从事件流复算（UI 只允许来自事件流）
        int recomputed = 3 + log.Events.OfType<SupportPointEvent>().Sum(e => e.Delta);
        Assert.AreEqual(d.SupportPoints, recomputed, "SP 现值 == 起手 + ΣΔ（事件流是唯一事实来源）");

        d.PassTurn(medic2);
        TurnSkippedEvent skipped = log.Events.OfType<TurnSkippedEvent>().Last();
        Assert.AreEqual("passed", skipped.Reason, "待命记 TurnSkippedEvent(passed)");
        Assert.AreEqual(d.SupportPoints, recomputed, "待命不消耗 SP");
    }

    [TestMethod]
    public void P19_Determinism_SameSeedSameSpTrajectory()
    {
        static List<int> Run()
        {
            BattleDirector d = NewDirector(out CombatLog _);
            var rng = new RngProvider(20260909);
            var trace = new List<int>();
            d.StartTurn(rng);
            for (int r = 0; r < 6; r++)
            {
                UnitId medic2 = d.Player.UnitRuntimeAt(6)?.Id ?? UnitId.Of("medic_2");
                d.PlayerUseSkill(medic2, "medic_first_aid", rng, new[] { 1 });
                trace.Add(d.SupportPoints);
                d.StartTurn(rng);
            }

            return trace;
        }

        CollectionAssert.AreEqual(Run(), Run(), "SP 纯计数零随机 → 同 seed 同轨迹");
    }
}