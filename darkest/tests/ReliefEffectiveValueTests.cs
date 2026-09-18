using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **纪律 V**（策划 `#404`）· **A13** · **A14** —— 由那次 `Effective*` 消费面**普查**挖出的真缺陷：
///   此前 **UI 展示**走 `EffectiveReliefCost`（升级后 2）· **实际收费**走 `b.Cost`（仍是 3）❗
///   ⇒ 📌 策划的定性：**"前两处是少了个功能（玩家不知道），这两处是多了一个谎（玩家看得见）"** ⚠️
///
/// 本用例钉住两件事（**都按策划给的判据**）：
///   · **A13**：**"升级后实际收费/恢复真的变了"** —— 🔴 **以【实际余额变化】为准**，不是以 UI 文案为准 ✓
///   · **A14**：**"展示值 == 消费值"** —— **同一个来源**（事件里记的 cost/restore 必须 == `Effective*`）✓
/// </summary>
[TestClass]
public sealed class ReliefEffectiveValueTests
{
    private const int Seed = 20260918;
    private const int Gold = 50;

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

    private static HeirloomStock NewStock(out CombatLog log, out HeirloomConfig heirCfg)
    {
        log = new CombatLog();
        heirCfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        var stock = new HeirloomStock(heirCfg);
        foreach (string k in heirCfg.Kinds)
        {
            stock.Add(log, k, 40, "relief_probe");
        }

        return stock;
    }

    [TestMethod]
    public void A13_ActualGoldSpentFollowsTheEffectiveCost()
    {
        EconomyConfig cfg = EconomyConfig.Parse(ReadData("economy.json"));
        HeirloomStock stock = NewStock(out CombatLog log, out _);
        var economy = new Economy(cfg, gold: Gold);
        int baseCost = cfg.StressReliefCost;

        // ① 未升级：**实际扣的**应等于生效值（= 基础价）✓
        int effectiveBefore = stock.EffectiveReliefCost(baseCost); // 🔴 **升级前**的生效价 ✓
        int before1 = economy.Gold;
        StressRelief.Apply(cfg, economy, new RngProvider(Seed), log, "tavern", "hero_tank_1", 40, stock);
        int spent1 = before1 - economy.Gold;

        // ② 升 tavern 一级（`cost_down`）⇒ **实际扣的必须变少** ✓（A13：以余额变化为准 ✓）
        Assert.IsTrue(stock.TryUpgrade(log, "tavern"), "酒馆应能升一级（降费轴）✓");
        int effectiveAfter = stock.EffectiveReliefCost(baseCost); // 🔴 **升级后**的生效价 ✓
        int before2 = economy.Gold;
        StressRelief.Apply(cfg, economy, new RngProvider(Seed + 1), log, "tavern", "hero_tank_1", 40, stock);
        int spent2 = before2 - economy.Gold;

        // 🔴 时机很关键：**生效值必须在【升级前】取** —— 我第一版在升级后才取 ⇒ 拿到的已是"升级后"的值 ⚠️
        Assert.AreEqual(effectiveBefore, spent1, "A14：**展示值 == 消费值**（未升级 ✓）");
        Assert.AreEqual(effectiveAfter, spent2, "A14：**展示值 == 消费值**（升级后 ✓）");
        Assert.IsTrue(spent2 < spent1, "A13：**升级后实际扣得更少**（以实际余额变化为准 ✓）");

        Console.WriteLine($"[A13·收费] 实际余额变化：升级前扣 **{spent1}** ⇒ 升级后扣 **{spent2}**（生效价 {stock.EffectiveReliefCost(baseCost)} ✓）");
        TestContext.WriteLine($"[A13] 扣款 {spent1} → {spent2}（以前是 UI 写 2、实际扣 3 ❗）✓");
    }

    [TestMethod]
    public void A13_A14_RestoreAlsoFollowsTheEffectiveValue_AndEventsMatch()
    {
        EconomyConfig cfg = EconomyConfig.Parse(ReadData("economy.json"));
        HeirloomStock stock = NewStock(out CombatLog log, out _);
        var economy = new Economy(cfg, gold: Gold);
        int baseRestore = cfg.Building("abbey").MoraleRestore;

        int beforeMorale = 10;
        StressReliefOutcome o1 = StressRelief.Apply(cfg, economy, new RngProvider(Seed), log, "abbey", "hero_medic_1", beforeMorale, stock);
        int gain1 = o1.NewMorale - beforeMorale;

        Assert.IsTrue(stock.TryUpgrade(log, "abbey"), "修道院应能升一级（增强轴）✓");
        int before2 = 10;
        StressReliefOutcome o2 = StressRelief.Apply(cfg, economy, new RngProvider(Seed + 1), log, "abbey", "hero_medic_1", before2, stock);
        int gain2 = o2.NewMorale - before2;

        Assert.IsTrue(gain2 > gain1, "A13：**升级后实际恢复更多**（以士气变化为准 ✓）");
        Assert.AreEqual(stock.EffectiveMoraleRestore("abbey", baseRestore), gain2, "A14：恢复量与生效值**同源** ✓");

        // 🔴 A14：**事件里记的数字**必须与生效口一致（"展示值 == 消费值" ✓）
        var ev = log.Events.OfType<StressReliefEvent>().Last();
        Assert.AreEqual(stock.EffectiveMoraleRestore("abbey", baseRestore), ev.MoraleRestored,
            "A14：`StressReliefEvent.MoraleRestored` 必须 == 生效值（同源 ✓）");
        Assert.AreEqual(stock.EffectiveReliefCost(cfg.StressReliefCost), ev.Cost,
            "A14：`StressReliefEvent.Cost` 必须 == 生效价（同源 ✓）");

        Console.WriteLine($"[A13·恢复] 实际士气增量：升级前 +{gain1} ⇒ 升级后 +{gain2}（生效值 {stock.EffectiveMoraleRestore("abbey", baseRestore)} ✓）");
        Console.WriteLine($"[A14·同源] 事件记账：cost={ev.Cost}（生效 {stock.EffectiveReliefCost(cfg.StressReliefCost)}）· 恢复={ev.MoraleRestored}（生效 {stock.EffectiveMoraleRestore("abbey", baseRestore)}）✓");
        TestContext.WriteLine($"[A13/A14] 恢复 +{gain1} → +{gain2}；事件与生效值同源 ✓");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
