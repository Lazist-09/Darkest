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
/// D0（#202）眩晕抗性递增 + D1（#203）连续未命中补偿：
/// 数量型/边界型断言——连续叠加递减、未晕行动后清零、补偿 +4 且**不改面板显示值**。
/// </summary>
[TestClass]
public sealed class DdAdoptionTests
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

    private static (BattleDirector d, BalanceTable balance) World()
    {
        var log = new CombatLog();
        BalanceTable balance = BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json")));
        var d = new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            SkillsConfig.Parse(ReadData("skills.json")),
            balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        return (d, balance);
    }

    [TestMethod]
    public void D1_MissCompensation_Plus4_HiddenFromDisplay()
    {
        (BattleDirector d, BalanceTable balance) = World();
        UnitRuntime warrior = d.Player.UnitRuntimeAt(2)!;
        UnitRuntime mook = d.Enemy.UnitRuntimeAt(1)!; // 闪避 10 → 显示命中 90

        int shown = BattleMath.HitRate(mook.Base.Dodge, 0, balance.HitClampMin, balance.HitClampMax);
        Assert.AreEqual(90, shown, "近战小兵显示命中率 90");

        // 连续 2 次未命中 → 第 3 次补偿 +4
        warrior.ConsecutiveMisses = 2;
        var log3 = new CombatLog();
        bool hit = HitStep.Resolve(warrior, mook, 0, new ScriptedRng(92.0), log3, balance);

        Assert.IsTrue(hit, "补偿 +4 → 掷 92 命中（90+4=94）");
        Assert.AreEqual(90, log3.Events.OfType<HitEvent>().Single().HitRate, "面板/事件显示值仍是 90（补偿隐藏）");
        Assert.AreEqual(0, warrior.ConsecutiveMisses, "命中后计数清零");
    }

    [TestMethod]
    public void D1_FirstMiss_NoCompensation()
    {
        (BattleDirector d, BalanceTable balance) = World();
        UnitRuntime warrior = d.Player.UnitRuntimeAt(2)!;
        UnitRuntime mook = d.Enemy.UnitRuntimeAt(1)!;

        Assert.AreEqual(0, warrior.ConsecutiveMisses, "起手无补偿");
        var log = new CombatLog();
        bool hit = HitStep.Resolve(warrior, mook, 0, new ScriptedRng(92.0), log, balance);
        Assert.IsFalse(hit, "第一次未命中无补偿（92 ≥ 90 仍 miss）");
        Assert.AreEqual(1, warrior.ConsecutiveMisses, "计数 +1");
    }

    [TestMethod]
    public void D0_StunBuildup_Plus50_AndClamp100()
    {
        (BattleDirector d, BalanceTable _) = World();
        UnitRuntime mook = d.Enemy.UnitRuntimeAt(1)!; // 眩晕抗性 30

        var req = new EffectRequest("stun", 100, "stun_resist", null, 0);
        var log = new CombatLog();
        Assert.IsTrue(EffectsStep.Apply(mook, req, new ScriptedRng(0.0), log, BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json")))));
        Assert.AreEqual(50, mook.StunResistBuildup, "成功施加眩晕 → 递增 +50");

        mook.Stunned = false;
        EffectsStep.Apply(mook, req, new ScriptedRng(0.0), log, BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json"))));
        Assert.AreEqual(100, mook.StunResistBuildup, "再叠 +50 → 100（上限）");

        // 实际概率随递增递减：标注 40% 时
        double before = BattleMath.ActualEffectChance(40, 30);
        double after = BattleMath.ActualEffectChance(40, Math.Clamp(mook.Base.StunResist + mook.StunResistBuildup, 0, 100));
        Assert.IsTrue(after < before, $"递增令实际概率下降（{before:P0} → {after:P0}）");
        Assert.AreEqual(0.0, after, 0.01, "抗性 100% → 实际概率 0（自然免疫）");
    }

    [TestMethod]
    public void D0_UnstunnedAction_ClearsBuildup()
    {
        (BattleDirector d, BalanceTable _) = World();
        UnitRuntime mook = d.Enemy.UnitRuntimeAt(1)!;
        mook.StunResistBuildup = 50;
        mook.Stunned = false;
        d.StartTurn(new RngProvider(41)); // 构建本回合行动序列

        UnitId? actor = d.NextActor();
        bool sawMook = false;
        while (actor is not null)
        {
            if (actor is { } a && a == mook.Id)
            {
                sawMook = true;
                break;
            }

            actor = d.NextActor();
        }

        Assert.IsTrue(sawMook, "该单位终于行动（未被晕）");
        Assert.AreEqual(0, mook.StunResistBuildup, "完成一次未被晕的行动 → 递增清零");
    }

    [TestMethod]
    public void D0_StunnedSkip_KeepsBuildup()
    {
        (BattleDirector d, BalanceTable _) = World();
        UnitRuntime mook = d.Enemy.UnitRuntimeAt(1)!;
        mook.StunResistBuildup = 50;
        mook.Stunned = true;
        d.StartTurn(new RngProvider(42)); // 构建本回合行动序列
        int orderSize = d.LastRoundOrder.Count; // 遍历上限

        bool sawMook = false;
        for (int i = 0; i < orderSize; i++)
        {
            UnitId? actor = d.NextActor();
            if (actor is { } a && a == mook.Id)
            {
                sawMook = true;
            }
        }

        Assert.IsFalse(sawMook, "被晕 → 本次不行动（跳过）");
        Assert.AreEqual(50, mook.StunResistBuildup, "跳过不清理递增（DD 同：直到拿到未被晕的回合）");
    }

    [TestMethod]
    public void D2_Mark_Applied_AndBoostsDeclaredSkills()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = SkillsConfig.Parse(ReadData("skills.json"));
        var rt = new Darkest.Gameplay.Sim.Skill.SkillRuntimeState();
        var executor = new Darkest.Gameplay.Sim.Skill.SkillExecutor(skills, balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), d.Log, rt, d.Buffs);

        // 督战（政委）→ 对敌 1 施加 mark
        executor.Execute(skills.Get("commissar_supervise"), UnitId.Of("commissar"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 0.0), chosenTargets: new[] { 1 });
        Assert.IsTrue(d.Buffs.Has(UnitId.Of("melee_soldier"), "mark"), "督战 → 目标带 mark（不可被抵抗，无掷骰）");

        // 致命注射（声明 bonus_vs_marked_percent 25）对带 mark 目标加伤 25%
        d.Enemy.UnitRuntimeAt(1)!.CurrentHp = 24; // 48 → 缺失 50%
        double baselineRaw = MarkedDamageRaw(d, skills, balance, marked: false);
        double markedRaw = MarkedDamageRaw(d, skills, balance, marked: true);
        Assert.AreEqual(1.25, markedRaw / baselineRaw, 0.02, "mark 加伤 ×1.25（乘法阶段）");
    }

    private static double MarkedDamageRaw(BattleDirector d, SkillsConfig skills, BalanceTable balance, bool marked)
    {
        var rt = new Darkest.Gameplay.Sim.Skill.SkillRuntimeState();
        var log = new CombatLog();
        var buf = BuffDefsConfig.Parse(ReadData("buff_defs.json"));
        var ledger = new Darkest.Gameplay.Sim.Buffs.BuffLedger(buf, log);
        if (marked)
        {
            ledger.Add(UnitId.Of("melee_soldier"), "mark", null);
        }

        var executor = new Darkest.Gameplay.Sim.Skill.SkillExecutor(skills, balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), log, rt, ledger);
        executor.Execute(skills.Get("medic_lethal_injection"), UnitId.Of("medic"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 1 });
        DamageEvent dmg = log.Events.OfType<DamageEvent>().First();
        d.Enemy.UnitRuntimeAt(1)!.CurrentHp = 24; // 复位，供下一次对照
        return dmg.Raw;
    }

    [TestMethod]
    public void D2_EnemyAi_RaisesWeight_OnMarkedAlly()
    {
        (BattleDirector d, BalanceTable _) = World();
        var cfg = EnemyAiConfig.Parse(ReadData("enemy_ai.json"));
        var ai = new Darkest.Gameplay.Sim.Enemy.EnemyAi(cfg, SkillsConfig.Parse(ReadData("skills.json")), new Darkest.Gameplay.Sim.Skill.SkillRuntimeState());
        var buf = BuffDefsConfig.Parse(ReadData("buff_defs.json"));
        var ledger = new Darkest.Gameplay.Sim.Buffs.BuffLedger(buf);
        ledger.Add(UnitId.Of("warrior"), "mark", null); // 我 2 位（战士）被标记

        UnitRuntime mook = d.Enemy.UnitRuntimeAt(1)!;
        var log = new CombatLog();
        var rng = new RngProvider(20260909);
        int markedPicked = 0;
        const int runs = 1000;
        for (int i = 0; i < runs; i++)
        {
            Darkest.Gameplay.Sim.Enemy.SkillChoice c = ai.Choose(mook, d.Enemy, d.Player, ledger, rng, log)!;
            if (c.TargetSlots[0] == 2)
            {
                markedPicked++;
            }
        }

        double rate = (double)markedPicked / runs;
        Assert.IsTrue(rate is > 0.33 and < 0.47, $"被标记者权重 ×2 → 2/(2+1+1+1) ≈40%（C 轴后池 4 人；实测 {rate:P1}）");
    }

    [TestMethod]
    public void D3_Bleed_TicksAtTargetTurnStart_NoPhysDef_NoCrit_AndCritExtends()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = SkillsConfig.Parse(ReadData("skills.json"));
        var rt = new Darkest.Gameplay.Sim.Skill.SkillRuntimeState();
        var executor = new Darkest.Gameplay.Sim.Skill.SkillExecutor(skills, balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")), d.Log, rt, d.Buffs);

        // 突刺（追加流血 3×2）命中敌 1；暴击（暴击掷 0）→ 时长 2→4
        executor.Execute(skills.Get("warrior_lunge"), UnitId.Of("warrior"), d.Player, d.Enemy,
            new ScriptedRng(0.0, 0.0, 0.0, 0.0), chosenTargets: new[] { 1 });
        UnitRuntime mook = d.Enemy.UnitRuntimeAt(1)!;
        Assert.AreEqual(4, mook.BleedRoundsRemaining, "暴击施加流血 → 时长 2 → 4（D3）");

        // 目标回合开始结算：固定 3 点、不吃物防、不暴击
        int hpBefore = mook.CurrentHp;
        d.StartTurn(new RngProvider(51));
        while (d.NextActor() is { } a && a != mook.Id)
        {
            // 推进到该单位回合开始
        }

        DamageEvent tick = d.Log.Events.OfType<DamageEvent>().Last(e => e.Axis == "bleed");
        Assert.AreEqual(balance.BleedPerRound, tick.Amount, "流血每回合固定伤害（tuning 值）");
        Assert.IsFalse(tick.Crit, "流血每回合伤害不暴击");
        Assert.AreEqual(hpBefore - balance.BleedPerRound, mook.CurrentHp, "在目标回合开始结算");
        Assert.AreEqual(3, mook.BleedRoundsRemaining, "结算一次后剩余回合 −1");
    }

    [TestMethod]
    public void D4_DeathsDoorRecovery_GrantsPenalty_OnceSpeedAndMods()
    {
        (BattleDirector d, BalanceTable balance) = World();
        UnitRuntime w = d.Player.UnitRuntimeAt(2)!;
        w.Weak = true;
        w.Morale = 60;   // ≥ 初始值 → 触发归队判定
        w.CurrentHp = 1;

        d.StartTurn(new RngProvider(61));

        Assert.IsTrue(d.Buffs.Has(w.Id, "deaths_door_recovery"), "死门存活归队 → 获得后遗症");
        Assert.AreEqual(-1, w.SpeedMod, "速度 −1");
        Assert.AreEqual(10, d.Buffs.PercentMod(w.Id, "taken_damage_mult"), "承受伤害 +10%");
        Assert.AreEqual(-5, d.Buffs.PercentMod(w.Id, "hit_mod"), "命中 −5");

        // 命中 −5 生效：战士显示命中 90 → 后遗症下 85，掷 87 变 miss
        UnitRuntime mook = d.Enemy.UnitRuntimeAt(1)!;
        var log = new CombatLog();
        bool hit = HitStep.Resolve(w, mook, 0, new ScriptedRng(87.0), log, balance, d.Buffs);
        Assert.IsFalse(hit, "命中 −5 → 掷 87 未命中（无后遗症时为 90 命中）");

        // 不叠加：再次进出死门只保留一层
        w.Weak = true;
        w.Morale = 60;
        d.StartTurn(new RngProvider(62));
        Assert.AreEqual(-1, w.SpeedMod, "多次进出死门不叠加（防死亡螺旋）");
    }

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