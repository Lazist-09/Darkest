using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// P20 门禁（M7 远征层 ①②③⑥）——**加载级 fail-fast**：坏数据必须启动报错；
/// 另含 E2 执行层断言（资源不足 → **拒绝且不扣** + `ResourceChangedEvent(rejected)`）。
/// 做法：对真实 tuning.json 做定点替换制造坏数据 → 断言 `TuningConfig.Parse` 抛错。
/// </summary>
[TestClass]
public sealed class P20ExpeditionGateTests
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

    private static string Bad(string from, string to)
    {
        string json = ReadData("tuning.json");
        Assert.IsTrue(json.Contains(from, StringComparison.Ordinal), $"基准数据应包含 {from}（测试自检）");
        return json.Replace(from, to, StringComparison.Ordinal);
    }

    [TestMethod]
    public void P20_CurrentTuning_PassesLoadValidation()
    {
        TuningConfig t = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(6, t.Expedition.NBattles);
        Assert.AreEqual(0.33, t.Expedition.AmbushChance, 1e-9);
        Assert.AreEqual(2, t.Resources.Firewood);
        Assert.AreEqual(12, t.Resources.Food);
        Assert.AreEqual(6, t.Camp.RespiteBase);
    }

    [TestMethod]
    public void P20_BadNBattles_Throws()
        => Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(Bad("\"n_battles\": 6", "\"n_battles\": 0")),
            "expedition.n_battles 必须 ≥ 1（P20 ①）");

    [TestMethod]
    public void P20_BadAmbushChance_Throws()
        => Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(Bad("\"ambush_chance\": 0.33", "\"ambush_chance\": 1.5")),
            "ambush_chance 必须 ∈ [0,1]（P20 ①）");

    [TestMethod]
    public void P20_RetreatPenalty_Inverted_Throws()
        => Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(Bad("\"retreat_penalty\": { \"no_death\": 12, \"with_death\": 15 }", "\"retreat_penalty\": { \"no_death\": 15, \"with_death\": 12 }")),
            "with_death 必须 ≥ no_death（P20 ①）");

    [TestMethod]
    public void P20_RetreatReconcile_Mismatch_Throws()
        => Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(Bad("\"success_morale\": -12", "\"success_morale\": -30")),
            "tuning.retreat 与 retreat_penalty 必须对账（P20 ⑥）");

    [TestMethod]
    public void P20_FoodTiers_NonMonotonic_Throws()
        => Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(Bad("\"food_tiers\": { \"starve\": 0, \"half\": 3, \"full\": 6, \"feast\": 12 }", "\"food_tiers\": { \"starve\": 0, \"half\": 3, \"full\": 2, \"feast\": 12 }")),
            "四档必须单调 0 < half < full < feast（P20 ③）");

    [TestMethod]
    public void P20_BadRespiteBase_Throws()
        => Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(Bad("\"respite_base\": 6", "\"respite_base\": 0")),
            "respite_base 必须 ≥ 1（P20 ③）");

    [TestMethod]
    public void E2_InsufficientResource_RejectedWithoutSpending()
    {
        ExpeditionSession s = new(log => HeadlessDriver.NewDirector(log), targetBattles: 6,
            firewood: 0, food: 3, ambushChance: 0.33);
        var log = new CombatLog();

        Assert.IsFalse(s.TrySpend(log, "food", 12, "camp_food"), "口粮 3 < 12 → 拒绝");
        Assert.AreEqual(3, s.Food, "**拒绝且不扣**（P20 ② 执行层断言）");
        ResourceChangedEvent e = log.Events.OfType<ResourceChangedEvent>().Single();
        Assert.AreEqual(0, e.Delta, "拒绝事件 Delta = 0");
        Assert.AreEqual("rejected", e.Reason);
        Assert.AreEqual(3, e.NewValue);

        Assert.IsTrue(s.TrySpend(log, "food", 3, "camp_food"), "足够 → 成功");
        Assert.AreEqual(0, s.Food);
    }

    [TestMethod]
    public void E2_Gain_WritesEventWithNewValue()
    {
        ExpeditionSession s = new(log => HeadlessDriver.NewDirector(log), targetBattles: 6,
            firewood: 2, food: 12, ambushChance: 0.33);
        var log = new CombatLog();
        s.Gain(log, "food", 4, "event");
        ResourceChangedEvent e = log.Events.OfType<ResourceChangedEvent>().Single();
        Assert.AreEqual(4, e.Delta);
        Assert.AreEqual(16, e.NewValue);
        Assert.AreEqual(16, s.Food);
    }
}
