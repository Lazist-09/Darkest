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
/// ② **标记 / 流血 / 死门归队**（`D2` 标记加伤 ／ `D3` 流血每回合结算 ／ `D4` 死门归队后遗症）＋ 夹具 `MarkedDamageRaw`✓
/// ③ 只搬家、零行为改动 ✓（唯一非逐字节动作：类声明加 `partial`）
/// </summary>
public sealed partial class DdAdoptionTests
{
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
        Assert.IsTrue(rate is > 0.60 and < 0.75, $"被标记者权重 ×2 → 2/(2+1) ≈67%（重劈池 {{1,2}}；实测 {rate:P1}）");
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
}
