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
        CollectionAssert.AreEqual(new[] { 2 }, c.TargetSlots.ToArray(), "近战 lowest_hp → 打残血的我 2 位");
    }

    [TestMethod]
    public void Archer_Backmost_PicksHighestSlot()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out _, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime archer = d.Enemy.UnitRuntimeAt(3)!;
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());

        SkillChoice? c = NewAi(cfg, skills).Choose(archer, d.Enemy, d.Player, null, new RngProvider(1), log);
        Assert.AreEqual("ranged_precise_shot", c!.SkillId);
        CollectionAssert.AreEqual(new[] { 6 }, c.TargetSlots.ToArray(), "射手 backmost → 打最深的我方单位（C 轴后 = 6 位）");
    }

    [TestMethod]
    public void Caster_LowestMorale_PicksLowestMoraleSlot()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out _, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime caster = d.Enemy.UnitRuntimeAt(4)!;
        d.Player.RemoveUnitAt(2); // 让 AOE 规则（需 1/2 位 ≥2）不可用 → 落到恐惧低语
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        d.Player.UnitRuntimeAt(1)!.Morale = 70;
        d.Player.UnitRuntimeAt(4)!.Morale = 30;

        SkillChoice? c = NewAi(cfg, skills).Choose(caster, d.Enemy, d.Player, null, new RngProvider(1), log);
        Assert.AreEqual("caster_fear_whisper", c!.SkillId, "恐惧低语目标位已扩为 [1,2,3,4]（#186）");
        CollectionAssert.AreEqual(new[] { 4 }, c.TargetSlots.ToArray(), "施法者 lowest_morale → 打我 4 位");
    }

    [TestMethod]
    public void Caster_Aoe_HitsFullPool_NotSingleton()
    {
        BattleDirector d = NewDirector(out SkillsConfig skills, out _, out EnemyAiConfig cfg, out CombatLog log);
        UnitRuntime caster = d.Enemy.UnitRuntimeAt(4)!;
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());

        SkillChoice? c = NewAi(cfg, skills).Choose(caster, d.Enemy, d.Player, null, new RngProvider(1), log);
        Assert.AreEqual("caster_mental_shock", c!.SkillId);
        Assert.AreEqual(4, c.TargetSlots.Count, "AOE 不受选一影响：命中池内全部非空位（C 轴后池 = 1,2,5,6）");
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
        Assert.IsTrue(rate is > 0.45 and < 0.56, $"taunt 加权 = 3/(3+1+1+1) ≈50%（C 轴后池 4 人；实测 {rate:P1}）");
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
            skills.Get("caster_fear_whisper").Target.Slots!.ToArray(), "C 轴（v0.62）：敌方攻击范围覆盖 5/6 → [1..6]");

        EnemyAiConfig cfg = EnemyAiConfig.Parse(ReadData("enemy_ai.json"));
        Assert.AreEqual(3, cfg.TauntWeight, "taunt_weight 起手 3");
        Assert.AreEqual("lowest_hp", cfg.For("melee_soldier")!.TargetPreference);
        Assert.AreEqual("backmost", cfg.For("ranged_archer")!.TargetPreference);
        Assert.AreEqual("lowest_morale", cfg.For("caster")!.TargetPreference);
    }
}