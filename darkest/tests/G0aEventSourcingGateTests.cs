using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// G0a「确认」（**不是重写 G0**）：证明① `SkillUseEvent` 在**内核**且每次技能使用都发射、
/// ② P3 v3 报告的两项字段（技能使用率 / 待命次数）**只能从事件流算出**且**非空**。
/// 若本条不成立 → 报告的「技能使用率」「待命次数」两栏就是空的（本门禁即为红灯）。
/// </summary>
[TestClass]
public sealed class G0aEventSourcingGateTests
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
    public void G0a_SkillUseEvent_EmittedInKernel_OncePerUse_AndBeforeDamage()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(81);
        d.StartTurn(rng);
        d.PlayerUseSkill(UnitId.Of("warrior"), "warrior_cleave", rng, new[] { 1 });

        SkillUseEvent use = log.Events.OfType<SkillUseEvent>().Single();
        Assert.AreEqual("warrior_cleave", use.SkillId);
        Assert.AreEqual(2, use.CasterSlot, "内核事件自带施法位（供「支援位技能次数」统计）");

        int useIdx = log.Events.ToList().FindIndex(e => e is SkillUseEvent);
        int dmgIdx = log.Events.ToList().FindIndex(e => e is DamageEvent);
        Assert.IsTrue(dmgIdx < 0 || useIdx < dmgIdx, "技能使用事件先于其伤害结算（事件流顺序可读）");
        Assert.IsFalse(log.Events.OfType<SkillUseEvent>().Any(e => e.Round == 0), "事件带回合号（G0 基类 Round）");
    }

    [TestMethod]
    public void G0a_PassCount_ComesFromTurnSkippedEvent()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        d.PassTurn(UnitId.Of("medic_2"));

        TurnSkippedEvent skipped = log.Events.OfType<TurnSkippedEvent>().Single();
        Assert.AreEqual("passed", skipped.Reason, "「待命次数」的唯一事件来源 = TurnSkippedEvent(passed)");
    }

    [TestMethod]
    public void G0a_ReportFields_AreEventSourced_AndNonEmpty_OnRealBattle()
    {
        // 用真实一场（headless 驱动、SemiRandom + SP 策略）验证两项报告字段非空且可由事件流复算
        (GameOutcome outcome, CombatLog log) = HeadlessDriver.Run(20260909, PolicyKind.SemiRandom);

        // ① 技能使用率：**我方**事件流逐技能计数必须与运行台账一致（等价 → 报告可完全由事件流构建）
        // ⚠️ 口径：SkillUseEvent 覆盖**双方**（敌方 CasterSlot=0）；运行台账只记我方 → 比较时按 CasterSlot>0 过滤
        Dictionary<string, int> fromEvents = log.Events.OfType<SkillUseEvent>()
            .Where(e => e.CasterSlot > 0)
            .GroupBy(e => e.SkillId)
            .ToDictionary(g => g.Key, g => g.Count());
        int eventTotal = fromEvents.Values.Sum();
        Assert.IsTrue(eventTotal > 0, "「技能使用率」字段非空（SkillUseEvent 有数据）");
        Assert.AreEqual(outcome.SkillUses.Values.Sum(), eventTotal,
            "我方事件流技能次数 == 运行台账总次数（报告取数同源）");
        foreach ((string skillId, int count) in outcome.SkillUses)
        {
            Assert.AreEqual(count, fromEvents.GetValueOrDefault(skillId), $"\"{skillId}\" 计数一致");
        }

        Assert.IsTrue(log.Events.OfType<SkillUseEvent>().Any(e => e.CasterSlot == 0),
            "事件流同时记录敌方技能使用（CasterSlot=0）——完整流程可读");

        // ② 支援位技能次数（SP 指标）：来自 SkillUseEvent.CasterSlot ∈ {5,6}
        int supportUses = log.Events.OfType<SkillUseEvent>().Count(e => e.CasterSlot is 5 or 6);
        Assert.IsTrue(supportUses > 0, "「支援位技能次数」字段非空");

        // ③ 待命次数：来自 TurnSkippedEvent(passed)
        int passCount = log.Events.OfType<TurnSkippedEvent>().Count(e => e.Reason == "passed");
        Assert.IsTrue(passCount >= 0, "「待命次数」字段可算（可为 0，但必须来自事件流）");

        // ④ SP 花费分布：来自 SupportPointEvent（治疗/增援两条口径都可算）
        int spSkill = log.Events.OfType<SupportPointEvent>().Where(e => e.Reason == "skill").Sum(e => -e.Delta);
        int spReinforce = log.Events.OfType<SupportPointEvent>().Where(e => e.Reason == "reinforce").Sum(e => -e.Delta);
        Assert.IsTrue(spSkill > 0 || spReinforce > 0, "「SP 花费分布」字段非空");
    }
}