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
    public void D3_PassTurn_NoPenaltyAfter272_AndRecordsZeroDelta()
    {
        var log = new CombatLog();
        Darkest.Gameplay.Sim.Director.BattleDirector d = HeadlessDriver.NewDirector(log);

        UnitId actor = d.Player.UnitsInSlotOrder().First().Id;
        int before = d.Player.UnitRuntimeAt(d.Player.UnitAtPosition(actor)!.Value)!.Morale;

        d.PassTurn(actor);

        TurnSkippedEvent skip = log.Events.OfType<TurnSkippedEvent>().Last();
        Assert.AreEqual("passed", skip.Reason);
        Assert.AreEqual(0, skip.MoraleDelta, "#272 ④：待命惩罚【已撤销】（pass_morale_delta = 0），机制保留");

        int after = d.Player.UnitRuntimeAt(d.Player.UnitAtPosition(actor)!.Value)!.Morale;
        Assert.AreEqual(before, after, "#272：待命**不再扣士气**（DD 的 Pass 惩罚依赖其战术价值，我们没有）");

        // 🔴 #269 门禁（P21 ⑦）：冗余必须可对账 —— MoraleEvent.delta == TurnSkippedEvent.MoraleDelta
        //（#272 后两者都为 0；delta=0 时 MoraleLedger 可能不写事件 ⇒ 用 LastOrDefault 取 0 对账）
        int moraleEventDelta = log.Events.OfType<MoraleEvent>().Where(e => e.Source == "pass").Select(e => e.Delta).LastOrDefault();
        Assert.AreEqual(moraleEventDelta, skip.MoraleDelta,
            "P21 ⑦ / #269：MoraleEvent.delta 必须 == TurnSkippedEvent.MoraleDelta（现在两边都是 0 也必须一致）");

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
