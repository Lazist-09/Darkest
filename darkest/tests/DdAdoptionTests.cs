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
}