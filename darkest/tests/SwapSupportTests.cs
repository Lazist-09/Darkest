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

/// <summary>换位/增援命令与基础智能策略（拍板 #176：#41a 发起者消耗行动、支援位优先军医/政委、同回合 1 次）。</summary>
[TestClass]
public sealed class SwapSupportTests
{
    private static string FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    private static BattleDirector NewDirector(CombatLog log)
        => new(FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json"))),
            UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json"))),
            SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json"))),
            BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json")))),
            MoraleEventsConfig.Parse(File.ReadAllText(FindDataFile("morale_events.json"))),
            BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json"))),
            EnemyAiConfig.Parse(File.ReadAllText(FindDataFile("enemy_ai.json"))),
            log);

    [TestMethod]
    public void PlayerSwap_WeakFrontend_ExchangesWithSupport_OncePerRound()
    {
        var log = new CombatLog();
        BattleDirector d = NewDirector(log);
        UnitRuntime tank = d.Player.UnitRuntimeAt(1)!;
        tank.Weak = true;

        Assert.IsTrue(d.PlayerSwap(tank.Id, 6), "战斗位虚弱者发起 → 与支援位 6 交换");
        Assert.AreEqual("tank", d.Player.UnitRuntimeAt(6)!.Id.Value, "虚弱者沿交换链到支援位 6");
        // 交换链语义（M1）：路径上各单位整体前移一位（1=原2位 战士、2=原3位 政委…5=原6位 军医）
        Assert.AreEqual("warrior", d.Player.UnitRuntimeAt(1)!.Id.Value, "原 2 位战士前移填 1");
        Assert.IsFalse(d.Player.UnitRuntimeAt(1)!.Weak, "换位后战斗位 1 是健康者");
        Assert.AreEqual("medic_2", d.Player.UnitRuntimeAt(5)!.Id.Value, "原 6 位军医（实例 medic_2）前移到 5");
        Assert.IsTrue(d.SwappedThisRound);
        Assert.IsTrue(log.Events.OfType<SwapEvent>().Any(), "换位事件落日志");

        Assert.IsFalse(d.PlayerSwap(tank.Id, 1), "同回合至多 1 次换位（护栏）");
        d.StartTurn(new RngProvider(1));
        Assert.IsFalse(d.SwappedThisRound, "下回合重置");
    }

    [TestMethod]
    public void PlayerSwap_RejectsWhenSupportSlotEmpty()
    {
        var log = new CombatLog();
        BattleDirector d = NewDirector(log);
        d.Player.RemoveUnitAt(6); // 支援位空
        d.Player.UnitRuntimeAt(1)!.Weak = true;
        Assert.IsFalse(d.PlayerSwap(d.Player.UnitRuntimeAt(1)!.Id, 6), "空支援位不可换");
    }

    [TestMethod]
    public void SemiRandom_DecidesSwap_WeakFrontend_MedicPreferred()
    {
        var log = new CombatLog();
        BattleDirector d = NewDirector(log);
        UnitRuntime tank = d.Player.UnitRuntimeAt(1)!;
        tank.Weak = true;
        var rng = new RngProvider(7);

        PlayerDecision decision = Policies.DecideForUnit(PolicyKind.SemiRandom, tank, d, rng);
        Assert.IsNull(decision.SkillId, "换位消耗行动：不再放技能");
        Assert.AreEqual(6, decision.SwapSupportPos, "优先军医（支援位 6）");
    }

    [TestMethod]
    public void SemiRandom_NoSwap_WhenSupportIsWeakOrFrontendHealthy()
    {
        var log = new CombatLog();
        BattleDirector d = NewDirector(log);
        var rng = new RngProvider(7);

        // 战斗位无虚弱 → 正常技能决策
        PlayerDecision healthy = Policies.DecideForUnit(PolicyKind.SemiRandom, d.Player.UnitRuntimeAt(1)!, d, rng);
        Assert.IsNull(healthy.SwapSupportPos, "无虚弱者不换位");

        // 支援位全部虚弱 → 不换（换虚弱无意义）
        d.Player.UnitRuntimeAt(1)!.Weak = true;
        d.Player.UnitRuntimeAt(5)!.Weak = true;
        d.Player.UnitRuntimeAt(6)!.Weak = true;
        PlayerDecision weakSupport = Policies.DecideForUnit(PolicyKind.SemiRandom, d.Player.UnitRuntimeAt(1)!, d, rng);
        Assert.IsNull(weakSupport.SwapSupportPos, "支援位无健康者 → 不换");
    }
}