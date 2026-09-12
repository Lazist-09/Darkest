using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M7.5 D2 / `#268`（架构裁定）：**支援包（`support_pack`）的 SP 结算**。
/// · `BattleDirector` 是 SP 的**唯一写入口**（远征层只当库存）
/// · **+2 后必须钳 cap**（P19 ⑧）
/// · **每次变动必写 `SupportPointEvent(reason:"item")`**（UI 与 ⑲ 归因的唯一来源）
/// · **不消耗行动**（"用库存换资源"，不是技能）
/// </summary>
[TestClass]
public sealed class SupportPackSpTests
{
    private static Darkest.Gameplay.Sim.Director.BattleDirector NewBattle()
        => HeadlessDriver.NewDirector(new CombatLog());

    [TestMethod]
    public void TryUseSupportPack_AddsTwoSp_AndWritesItemEvent()
    {
        var log = new CombatLog();
        Darkest.Gameplay.Sim.Director.BattleDirector d = HeadlessDriver.NewDirector(log);

        // 先花到低位（保证 +2 不被 cap 吞）
        while (d.SupportPoints >= 2 && d.TrySpendSupportPoints(2, "skill"))
        {
        }

        int before = d.SupportPoints;
        Assert.IsTrue(d.TryUseSupportPackForSp(), "支援包可用");
        Assert.AreEqual(Math.Min(before + 2, d.SupportCap), d.SupportPoints, "**+2**（超过 cap 时钳制）");

        SupportPointEvent e = log.Events.OfType<SupportPointEvent>().Last();
        Assert.AreEqual("item", e.Reason, "reason 必须是 item（⑲ 按此归因）");
        Assert.AreEqual(d.SupportPoints - before, e.Delta);
        Assert.AreEqual(d.SupportPoints, e.NewValue);
    }

    [TestMethod]
    public void TryUseSupportPack_ClampsToCap_NeverExceeds()
    {
        Darkest.Gameplay.Sim.Director.BattleDirector d = NewBattle();
        int cap = d.SupportCap;

        for (int i = 0; i < 5; i++)
        {
            d.TryUseSupportPackForSp(); // 连用 5 次（+10）
        }

        Assert.AreEqual(cap, d.SupportPoints, $"🔴 连续使用后仍 = cap {cap}（P19 ⑧：必须钳制，不得越界）");
        Assert.IsTrue(d.SupportPoints <= cap);
    }

    [TestMethod]
    public void TryUseSupportPack_ZeroOrNegativeAmount_Rejected()
    {
        Darkest.Gameplay.Sim.Director.BattleDirector d = NewBattle();
        Assert.IsFalse(d.TryUseSupportPackForSp(0), "0 → 拒绝");
        Assert.IsFalse(d.TryUseSupportPackForSp(-2), "负数 → 拒绝");
    }

    [TestMethod]
    public void TryUseSupportPack_DoesNotConsumeAction_OnlySp()
    {
        var log = new CombatLog();
        Darkest.Gameplay.Sim.Director.BattleDirector d = HeadlessDriver.NewDirector(log);
        int eventsBefore = log.Events.Count;

        d.TryUseSupportPackForSp();

        // 🔴 不消耗行动：不产生 TurnSkipped / SkillUse 之类"行动"事件，只产生 SP 事件
        Assert.IsFalse(log.Events.Skip(eventsBefore).OfType<TurnSkippedEvent>().Any(), "不产生待命事件");
        Assert.IsFalse(log.Events.Skip(eventsBefore).OfType<SkillUseEvent>().Any(), "不产生技能使用事件");
        Assert.IsTrue(log.Events.Skip(eventsBefore).OfType<SupportPointEvent>().Any(), "只写 SP 事件");
    }
}
