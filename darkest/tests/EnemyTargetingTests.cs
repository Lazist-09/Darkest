using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Buffs;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Enemy;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// P1/O-46 敌人池内目标选择（#185/#186/#187）：① taunt 加权抽取（写 RngDraw）→
/// ② 原型固定偏好（近战 lowest_hp / 射手 backmost / 施法者 lowest_morale，平局槽号小）→ ③ 槽号兜底；
/// AOE 全池不受选一影响；唯一候选不掷骰。
/// </summary>
[TestClass]
public sealed class EnemyTargetingTests
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

    private static BattleDirector NewDirector(out SkillsConfig skills, out BuffDefsConfig buffs,
        out EnemyAiConfig aiCfg, out CombatLog log)
    {
        skills = SkillsConfig.Parse(ReadData("skills.json"));
        buffs = BuffDefsConfig.Parse(ReadData("buff_defs.json"));
        aiCfg = EnemyAiConfig.Parse(ReadData("enemy_ai.json"));
        log = new CombatLog();
        return new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            skills,
            BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))),
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            buffs,
            aiCfg,
            log);
    }

    private static EnemyAi NewAi(EnemyAiConfig cfg, SkillsConfig skills)
        => new(cfg, skills, new SkillRuntimeState());

    [TestMethod]
    public void Melee_LowestHp_PicksWoundedSlot()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out _, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime melee = d.Enemy.UnitRuntimeAt(1)!;
        d.Player.UnitRuntimeAt(1)!.CurrentHp = 55; // 满血
        d.Player.UnitRuntimeAt(2)!.CurrentHp = 12; // 残血
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());

        SkillChoice? c = NewAi(cfg, skills).Choose(melee, d.Enemy, d.Player, null, new RngProvider(1), log);
        Assert.AreEqual("melee_heavy_slash", c!.SkillId);
        // v0.68：原型偏好已取消 → **池内均匀随机**（近战池 = {1,2}）
        var rng = new RngProvider(20260909);
        var counts = new Dictionary<int, int>();
        const int runs = 1000;
        for (int i = 0; i < runs; i++)
        {
            SkillChoice choice = NewAi(cfg, skills).Choose(d.Enemy.UnitRuntimeAt(1)!, d.Enemy, d.Player, null, rng, log)!;
            counts[choice.TargetSlots[0]] = counts.GetValueOrDefault(choice.TargetSlots[0]) + 1;
        }

        Assert.IsTrue(counts.ContainsKey(1) && counts.ContainsKey(2), "池内两个候选都被选到（不是固定槽）");
        foreach (int slot in new[] { 1, 2 })
        {
            double rate = (double)counts[slot] / runs;
            Assert.IsTrue(rate is > 0.40 and < 0.60, $"{slot} 位占比 ≈50%（池内随机；实测 {rate:P1}）");
        }
    }

    [TestMethod]
    public void Archer_RandomInPool_NotFixedSlot()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out _, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime archer = d.Enemy.UnitRuntimeAt(3)!;
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());

        // v0.68：射手偏好取消 → 精准射击池 = [1..6]，目标应分散（不再恒为 6 位）
        var rng = new RngProvider(7);
        var seen = new HashSet<int>();
        for (int i = 0; i < 200; i++)
        {
            SkillChoice c = NewAi(cfg, skills).Choose(archer, d.Enemy, d.Player, null, rng, log)!;
            Assert.AreEqual("ranged_precise_shot", c.SkillId);
            seen.Add(c.TargetSlots[0]);
        }

        Assert.IsTrue(seen.Count >= 3, $"池内随机 → 命中多个不同槽位（实际 {seen.Count} 个：{string.Join(",", seen.OrderBy(x => x))}）");
    }

    [TestMethod]
    public void Caster_RandomInPool_NotLowestMorale()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out _, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime caster = d.Enemy.UnitRuntimeAt(4)!;
        d.Player.RemoveUnitAt(2); // 让 AOE 规则（需 1/2 位 ≥2）不可用 → 落到恐惧低语
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        d.Player.UnitRuntimeAt(1)!.Morale = 70;
        d.Player.UnitRuntimeAt(4)!.Morale = 30;

        // v0.68：施法者偏好取消 → 恐惧低语池覆盖 5/6，目标应分散（不再恒打最低士气）
        var rng = new RngProvider(3);
        var seen = new HashSet<int>();
        for (int i = 0; i < 200; i++)
        {
            SkillChoice c = NewAi(cfg, skills).Choose(caster, d.Enemy, d.Player, null, rng, log)!;
            Assert.AreEqual("caster_fear_whisper", c.SkillId, "恐惧低语（AOE 规则不可用时）");
            seen.Add(c.TargetSlots[0]);
        }

        Assert.IsTrue(seen.Count >= 3, $"池内随机 → 命中多个不同槽位（实际 {seen.Count} 个：{string.Join(",", seen.OrderBy(x => x))}）");
    }

    [TestMethod]
    public void Caster_Aoe_HitsFullPool_NotSingleton()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out _, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime caster = d.Enemy.UnitRuntimeAt(4)!;
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());

        SkillChoice? c = NewAi(cfg, skills).Choose(caster, d.Enemy, d.Player, null, new RngProvider(1), log);
        Assert.AreEqual("caster_mental_shock", c!.SkillId);
        Assert.AreEqual(4, c.TargetSlots.Count, "AOE 不受选一影响：命中池内全部非空位（= 1,2,5,6）");
    }

    [TestMethod]
    public void Taunt_WeightedDraw_About75Percent_AndWritesRngDraw()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out BuffDefsConfig buffs, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime melee = d.Enemy.UnitRuntimeAt(1)!;
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        var ledger = new BuffLedger(buffs);
        ledger.Add(UnitId.Of("tank"), "taunt", source: null); // 嘲讽挂在我方嘲讽者身上；C 轴后近战池 = {1,2,5,6}
        // 权重：嘲讽者 3，其余候选各 1 → 3/6 = 50%

        EnemyAi ai = NewAi(cfg, skills);
        var rng = new RngProvider(20260909);
        int tankPicked = 0;
        const int runs = 1000;
        for (int i = 0; i < runs; i++)
        {
            SkillChoice c = ai.Choose(melee, d.Enemy, d.Player, ledger, rng, log)!;
            if (c.TargetSlots[0] == 1)
            {
                tankPicked++;
            }
        }

        double rate = (double)tankPicked / runs;
        Assert.IsTrue(rate is > 0.70 and < 0.80, $"taunt 加权 = 3/(3+1) = 75%（重劈回 [1,2] → 近战池 2 人；实测 {rate:P1}）");
        Assert.IsTrue(log.Events.OfType<RngDraw>().Count() >= runs, "taunt 加权抽取必写 RngDraw（确定性红线）");
    }

    [TestMethod]
    public void Taunt_UniqueCandidate_NoDraw()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out BuffDefsConfig buffs, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime melee = d.Enemy.UnitRuntimeAt(1)!;
        d.Player.RemoveUnitAt(2); // C 轴后近战池 = {1,2,5,6} → 清掉 2/5/6 才能造出「唯一候选」
        d.Player.RemoveUnitAt(5);
        d.Player.RemoveUnitAt(6);
        var ledger = new BuffLedger(buffs);
        ledger.Add(UnitId.Of("tank"), "taunt", source: null);

        var rng = new RngProvider(5);
        ulong before = rng.DrawCount;
        SkillChoice c = NewAi(cfg, skills).Choose(melee, d.Enemy, d.Player, ledger, rng, log)!;
        Assert.AreEqual(1, c.TargetSlots[0],
            $"唯一候选 100% 打它（skill={c.SkillId} targets=[{string.Join(",", c.TargetSlots)}] draws={rng.DrawCount - before}）");
        Assert.AreEqual(before, rng.DrawCount, "唯一候选不掷骰（不写 RngDraw）");
    }

    [TestMethod]
    public void Taunt_TaunterOutsidePool_Ignored_FallsBackToPreference()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out BuffDefsConfig buffs, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime melee = d.Enemy.UnitRuntimeAt(1)!;
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        d.Player.SwapSlots(1, 4); // 坦克移到 4（近战技能池 [1,2] 之外）
        d.Player.UnitRuntimeAt(2)!.CurrentHp = 10; // 我 2 位残血
        var ledger = new BuffLedger(buffs);
        ledger.Add(UnitId.Of("tank"), "taunt", source: null); // 嘲讽者在 4 位（池 [1,2] 之外）

        SkillChoice c = NewAi(cfg, skills).Choose(melee, d.Enemy, d.Player, ledger, new RngProvider(3), log)!;
        CollectionAssert.AreEqual(new[] { 2 }, c.TargetSlots.ToArray(), "嘲讽者在池外 → 忽略 taunt，走 lowest_hp 偏好");
    }

    [TestMethod]
    public void Determinism_SameSeed_SameTargetAndDrawCount()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out BuffDefsConfig buffs, out EnemyAiConfig cfg, out CombatLog logA);
        UnitRuntime melee = d.Enemy.UnitRuntimeAt(1)!;
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        var ledger = new BuffLedger(buffs);
        ledger.Add(melee.Id, "taunt", source: null);
        EnemyAi ai = NewAi(cfg, skills);

        var rngA = new RngProvider(777);
        var logB = new CombatLog();
        var rngB = new RngProvider(777);
        SkillChoice a = ai.Choose(melee, d.Enemy, d.Player, ledger, rngA, logA)!;
        SkillChoice b = ai.Choose(melee, d.Enemy, d.Player, ledger, rngB, logB)!;

        CollectionAssert.AreEqual(a.TargetSlots.ToArray(), b.TargetSlots.ToArray(), "同 seed → 同目标");
        Assert.AreEqual(rngA.DrawCount, rngB.DrawCount, "同 seed → 同 DrawCount");
    }

    [TestMethod]
    public void Data_P13_FearWhisperSlots_And_Preferences()
    {
        SkillsConfig skills = SkillsConfig.Parse(ReadData("skills.json"));
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 },
            skills.Get("caster_fear_whisper").Target.Slots!.ToArray(), "C 轴（#223）：恐惧低语覆盖 [1..6]（只有重劈回 [1,2]）");

        EnemyAiConfig cfg = EnemyAiConfig.Parse(ReadData("enemy_ai.json"));
        Assert.AreEqual(3, cfg.TauntWeight, "taunt_weight 起手 3");
        // v0.68：三原型偏好统一为**池内随机**（原 lowest_hp / backmost / lowest_morale 已作废）
        Assert.AreEqual("random", cfg.For("melee_soldier")!.TargetPreference);
        Assert.AreEqual("random", cfg.For("ranged_archer")!.TargetPreference);
        Assert.AreEqual("random", cfg.For("caster")!.TargetPreference);
    }
}