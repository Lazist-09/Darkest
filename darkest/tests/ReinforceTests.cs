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

/// <summary>增援单按钮两步 Reinforce(A,B,X)（#181 推翻旧双按钮）：X 有人交换/空直入、发起者消耗行动、同回合 ≤1（#176/#181）。</summary>
[TestClass]
public sealed class ReinforceTests
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

    [TestMethod]
    public void Reinforce_XOccupied_SupportSwapsIn_OncePerRound()
    {
        BattleDirector d = NewDirector(out CombatLog log);
        UnitRuntime tank = d.Player.UnitRuntimeAt(1)!;
        tank.Weak = true;

        // A=tank(1) 发起，B=槽6（medic_2），X=1 → B 与 X 上单位【直接互换】（原 X 上的人到 6，途经槽位不动）
        Assert.IsTrue(d.Reinforce(tank.Id, 6, 1), "战斗位虚弱发起 → 支援位换入");
        Assert.AreEqual("medic_2", d.Player.UnitRuntimeAt(1)!.Id.Value, "军医进入战斗位 1");
        Assert.AreEqual("tank", d.Player.UnitRuntimeAt(6)!.Id.Value, "原 1 位坦克到支援位 6（直接互换，非链式推移）");
        Assert.AreEqual("warrior", d.Player.UnitRuntimeAt(2)!.Id.Value, "途经槽位 2 不受影响");
        Assert.IsTrue(d.SwappedThisRound);
        Assert.IsTrue(log.Events.OfType<SwapEvent>().Any(), "增援事件落日志");

        Assert.IsFalse(d.Reinforce(tank.Id, 6, 2), "同回合至多 1 次（护栏）");
        d.StartTurn(new RngProvider(1));
        Assert.IsFalse(d.SwappedThisRound, "下回合重置");
    }

    [TestMethod]
    public void Reinforce_5To3_OriginalOccupantGoesTo5_Slot4Untouched()
    {
        // 用户实测场景（#181）：增援 B=5、X=3 —— 原 3 号位的人应到 5，4 号位不动（修复链式推移 bug）
        BattleDirector d = NewDirector(out _);
        d.Player.UnitRuntimeAt(1)!.Weak = true; // A=发起者（战斗位）
        string slot3Before = d.Player.UnitRuntimeAt(3)!.Id.Value;
        string slot4Before = d.Player.UnitRuntimeAt(4)!.Id.Value;
        string b5 = d.Player.UnitRuntimeAt(5)!.Id.Value;

        Assert.IsTrue(d.Reinforce(d.Player.UnitRuntimeAt(1)!.Id, 5, 3));
        Assert.AreEqual(b5, d.Player.UnitRuntimeAt(3)!.Id.Value, "B（5 位）进入 X=3");
        Assert.AreEqual(slot3Before, d.Player.UnitRuntimeAt(5)!.Id.Value, "原 3 位的人回到 B 的原位 5");
        Assert.AreEqual(slot4Before, d.Player.UnitRuntimeAt(4)!.Id.Value, "4 号位不动（修复前会被推到 5）");
    }

    [TestMethod]
    public void Reinforce_XEmpty_SupportEntersDirectly()
    {
        BattleDirector d = NewDirector(out _);
        d.Player.RemoveUnitAt(2); // 战斗位 2 空
        d.Player.UnitRuntimeAt(1)!.Weak = true;

        Assert.IsTrue(d.Reinforce(d.Player.UnitRuntimeAt(1)!.Id, 6, 2), "X 空 → B 直接进入");
        Assert.AreEqual("medic_2", d.Player.UnitRuntimeAt(2)!.Id.Value, "军医进入空战斗位 2");
        Assert.IsNull(d.Player.UnitRuntimeAt(6), "原支援位 6 空出");
    }

    [TestMethod]
    public void Reinforce_RejectsEmptySupportOrNonCombatX()
    {
        BattleDirector d = NewDirector(out _);
        d.Player.RemoveUnitAt(6);
        d.Player.UnitRuntimeAt(1)!.Weak = true;
        Assert.IsFalse(d.Reinforce(d.Player.UnitRuntimeAt(1)!.Id, 6, 1), "支援位空不可发起");
        Assert.IsFalse(d.Reinforce(d.Player.UnitRuntimeAt(1)!.Id, 5, 7), "X 越界 → 拒绝");
    }

    [TestMethod]
    public void SemiRandom_DecidesReinforce_WeakFrontend_MedicPreferred()
    {
        BattleDirector d = NewDirector(out _);
        UnitRuntime tank = d.Player.UnitRuntimeAt(1)!;
        tank.Weak = true;
        var rng = new RngProvider(7);

        PlayerDecision decision = Policies.DecideForUnit(PolicyKind.SemiRandom, tank, d, rng);
        Assert.IsNull(decision.SkillId, "增援消耗行动：不再放技能");
        Assert.IsTrue(decision.ReinforceB is 5 or 6, $"支援位（5/6）中择一换入（实际 {decision.ReinforceB}）");
        Assert.AreEqual(1, decision.ReinforceX, "目标 = 发起者自己的战斗位");
    }

    [TestMethod]
    public void SemiRandom_NoReinforce_WhenSupportAllWeak()
    {
        BattleDirector d = NewDirector(out _);
        var rng = new RngProvider(7);
        d.Player.UnitRuntimeAt(1)!.Weak = true;
        d.Player.UnitRuntimeAt(5)!.Weak = true;
        d.Player.UnitRuntimeAt(6)!.Weak = true;

        PlayerDecision decision = Policies.DecideForUnit(PolicyKind.SemiRandom, d.Player.UnitRuntimeAt(1)!, d, rng);
        Assert.IsNull(decision.ReinforceB, "支援位无健康者 → 不增援");
    }
}