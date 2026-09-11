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
/// #198 弹性增援（橡胶筋）：K=2 回合窗口内"未全力进攻"（存活战斗位中未使用 output 技能者 ≥3）
/// → M+1；任一回合达标 → M 回落 M_base；浮区 M ∈ [M_base, M_base+3]；**首波固定第 6 回合不受弹性影响**。
/// </summary>
[TestClass]
public sealed class ElasticReinforcementTests
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
        return new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            SkillsConfig.Parse(ReadData("skills.json")),
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
    }

    /// <summary>本回合让我方 4 个战斗位各"使用一个 output 技能"（只标记用途，是否命中不影响弹性判定）。</summary>
    private static void FullAttack(BattleDirector d, IRngProvider rng)
    {
        d.PlayerUseSkill(UnitId.Of("tank"), "tank_shield_bash", rng);
        d.PlayerUseSkill(UnitId.Of("warrior"), "warrior_cleave", rng);
        d.PlayerUseSkill(UnitId.Of("commissar"), "commissar_pistol_shot", rng);
        d.PlayerUseSkill(UnitId.Of("medic"), "medic_scalpel", rng);
    }

    [TestMethod]
    public void NotFullAttack_WindowSatisfied_RaisesM_ByOne()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(11);

        d.StartTurn(rng); // 回合 1
        d.StartTurn(rng); // 回合 2：评估回合 1（无人使用 output → 未全力）
        d.StartTurn(rng); // 回合 3：窗口 K=2 满足 → M+1

        ReinforcementElasticEvent[] ev = log.Events.OfType<ReinforcementElasticEvent>().ToArray();
        Assert.IsTrue(ev.Length >= 1, "未全力进攻应产生弹性事件");
        Assert.AreEqual("not_full_attack", ev.Last().Reason);
        Assert.AreEqual(ev.Last().MFrom + 1, ev.Last().MTo, "M 每次 +1");
    }

    [TestMethod]
    public void FullAttack_ResetsM_ToBase()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(12);

        d.StartTurn(rng); // 回合 1
        d.StartTurn(rng); // 回合 2（窗口未满，暂不变动）
        d.StartTurn(rng); // 回合 3：窗口 K=2 满足 → M+1
        int raised = log.Events.OfType<ReinforcementElasticEvent>().Last().MTo;

        FullAttack(d, rng); // 回合 3 的行动：全力进攻
        d.StartTurn(rng);   // 回合 4 评估回合 3 → 达标 → 回落

        ReinforcementElasticEvent last = log.Events.OfType<ReinforcementElasticEvent>().Last();
        Assert.AreEqual("reset", last.Reason, "达标 → 弹性项清零（M 回落 M_base；M_base 随实测 D 滚动，故不比对绝对值）");

        // 回落后再连续 2 回合不全力 → 弹性项重新累积（再次出现 not_full_attack）
        int attacksBefore = log.Events.OfType<ReinforcementElasticEvent>().Count(e => e.Reason == "not_full_attack");
        d.StartTurn(rng); // 5
        d.StartTurn(rng); // 6 → 窗口满足
        int attacksAfter = log.Events.OfType<ReinforcementElasticEvent>().Count(e => e.Reason == "not_full_attack");
        Assert.IsTrue(attacksAfter > attacksBefore, "回落后弹性项可重新累积（再次 +1）");
    }

    [TestMethod]
    public void ElasticBand_CappedAtBase_Plus3()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(13);

        for (int r = 1; r <= 10; r++)
        {
            d.StartTurn(rng); // 全程不全力 → M 持续 +1，但不得超 M_base+3
        }

        ReinforcementElasticEvent[] ev = log.Events.OfType<ReinforcementElasticEvent>().ToArray();
        int baseM = ev[0].MFrom;
        Assert.IsTrue(ev.All(e => e.MTo <= baseM + 3), $"浮区上限 M_base+3（base={baseM}，最大 {ev.Max(e => e.MTo)}）");
        Assert.IsTrue(ev.Count(e => e.Reason == "not_full_attack") <= 3, "最多 +3 次");
    }

    [TestMethod]
    public void FirstWave_StillAtTriggerRound_NotAffectedByElastic()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        var rng = new RngProvider(14);
        d.Enemy.RemoveUnitAt(4); // 留空位以便首波填充

        for (int r = 1; r <= 7; r++)
        {
            d.StartTurn(rng); // 全程不全力（弹性会把后续 M 拉长，但首波固定在 trigger_round=7）
        }

        ReinforcementEvent fill = log.Events.OfType<ReinforcementEvent>().First(e => e.Kind == "Fill");
        Assert.AreEqual(4, fill.Slot, "首波仍在第 6 回合触发（不受弹性影响）");
    }
}