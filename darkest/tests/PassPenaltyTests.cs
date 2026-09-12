using System;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M7.5 D3（待命惩罚 · `m7_5_dungeon_layer.md` §D3 / `#266`）：
/// **待命 → 受士气伤害 −5**（走既有 `MoraleLedger` 通道）；`TurnSkippedEvent` 携带 `MoraleDelta`（㉗ 的统计来源）；
/// 数值走 `tuning.expedition.pass_morale_delta`（**禁止硬编码**）。
/// </summary>
[TestClass]
public sealed class PassPenaltyTests
{
    [TestMethod]
    public void D3_PassTurn_CostsFiveMorale_AndRecordsDeltaOnEvent()
    {
        var log = new CombatLog();
        Darkest.Gameplay.Sim.Director.BattleDirector d = HeadlessDriver.NewDirector(log);

        UnitId actor = d.Player.UnitsInSlotOrder().First().Id;
        int before = d.Player.UnitRuntimeAt(d.Player.UnitAtPosition(actor)!.Value)!.Morale;

        d.PassTurn(actor);

        TurnSkippedEvent skip = log.Events.OfType<TurnSkippedEvent>().Last();
        Assert.AreEqual("passed", skip.Reason);
        Assert.AreEqual(-5, skip.MoraleDelta, "待命 −5 士气（㉗ 从本字段统计）");

        int after = d.Player.UnitRuntimeAt(d.Player.UnitAtPosition(actor)!.Value)!.Morale;
        Assert.AreEqual(Math.Max(0, before - 5), after, "士气**实际**被扣（走既有 MoraleLedger 通道）");

        Assert.IsTrue(log.Events.OfType<MoraleEvent>().Any(e => e.Source == "pass"), "士气变更写 MoraleEvent（唯一来源）");
    }

    [TestMethod]
    public void D3_PassTurn_DoesNotSpendSupportPoints()
    {
        var log = new CombatLog();
        Darkest.Gameplay.Sim.Director.BattleDirector d = HeadlessDriver.NewDirector(log);
        int sp = d.SupportPoints;

        d.PassTurn(d.Player.UnitsInSlotOrder().First().Id);

        Assert.AreEqual(sp, d.SupportPoints, "待命**不消耗 SP**（S5.2 原语义不变）");
    }

    [TestMethod]
    public void D3_PassMoraleDelta_ComesFromTuning_NotHardcoded()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        string? json = null;
        while (dir is not null && json is null)
        {
            string candidate = System.IO.Path.Combine(dir.FullName, "data", "tuning.json");
            if (System.IO.File.Exists(candidate))
            {
                json = System.IO.File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("\"pass_morale_delta\"", StringComparison.Ordinal),
            "待命士气伤害必须走 tuning（禁止硬编码；P16/P19 同款纪律）");
    }
}
