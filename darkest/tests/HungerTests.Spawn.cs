// 🔴 从 HungerTests.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3）——只搬家、零行为改动 ✓
//    本文件 = 饥饿生成（Roll / FoodRequired）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

public sealed partial class HungerTests
{
    [TestMethod]
    public void Roll_NoConfig_ReturnsNone_AndWritesNoLog()
    {
        var log = new CombatLog();
        var rng = new FixedRng(0.0); // 若真掷了，percent=0 会必中 ⇒ 用"不中"反证没掷 ✓
        HungerRollResult r = HungerSpawner.Roll(log, rng, null, lightValue: 0);
        Assert.IsFalse(r.Triggered, "未配置 ⇒ 一律 `None`（**哪怕光照为 0**）✓");
        Assert.AreEqual(0, log.Events.Count, "🔴 未配置 ⇒ **连 `RngDraw` 都不写**（零副作用）✓");
        Assert.AreEqual(0UL, rng.DrawCount, "未配置 ⇒ **一次都不掷** ✓");
    }

    [TestMethod]
    public void Roll_EmptyTiers_AlsoReturnsNone()
    {
        var log = new CombatLog();
        var rng = new FixedRng(0.0);
        var empty = new HungerConfig(new List<HungerTier>(),
            FoodPerHero: 1, EatHealPercent: 5.0, StarveHpPercent: 20.0, StarveMorale: 20);
        HungerRollResult r = HungerSpawner.Roll(log, rng, empty, lightValue: 0);
        Assert.IsFalse(r.Triggered, "空档位 ⇒ `None` ✓");
        Assert.AreEqual(0, log.Events.Count, "空档位 ⇒ 不写日志 ✓");
    }

    [TestMethod]
    public void Roll_LightAboveAllTiers_ReturnsNone_WithoutRolling()
    {
        // ⚠️ 这种配置在**真实数据里是非法的**（末档 < 满光照 ⇒ 加载期拒绝，见 `HungerTiers_MustCoverFullLight_ElseRejected`）；
        //    但纯函数层仍须**优雅**处理（不抛、不空掷）—— 即便有人绕过校验直接构造，也不会崩 ✓
        var log = new CombatLog();
        var rng = new FixedRng(0.0);
        var narrow = new HungerConfig(new List<HungerTier> { new(0, 12.5), new(25, 10.0) },
            FoodPerHero: 1, EatHealPercent: 5.0, StarveHpPercent: 20.0, StarveMorale: 20);
        HungerRollResult r = HungerSpawner.Roll(log, rng, narrow, lightValue: 100);
        Assert.IsFalse(r.Triggered, "比所有档都亮（100 > 25）⇒ 不饿 ✓");
        Assert.AreEqual(0, log.Events.Count, "比所有档都亮 ⇒ **不掷不写**（不是'掷了但没中'）✓");
    }

    [TestMethod]
    public void Roll_PicksDarkestMatchingTier_SoDarkerIsMoreFrequent()
    {
        // 全黑（0）⇒ 命中 max_light=0 那档（12.5%）；暗（50）⇒ 只满足 index 1 ✓
        HungerRollResult atBlack = HungerSpawner.Roll(new CombatLog(), new FixedRng(0.0), Cfg(12.5, 7.5), lightValue: 0);
        Assert.IsTrue(atBlack.Triggered, "percent=0.0 < 12.5 ⇒ 必中 ✓");
        Assert.AreEqual(0, atBlack.TierIndex, "🔴 全黑 ⇒ 取**最暗档**（index 0，12.5%）✓");
        Assert.AreEqual(12.5, atBlack.PercentUsed, 1e-9);

        HungerRollResult atDim = HungerSpawner.Roll(new CombatLog(), new FixedRng(0.0), Cfg(12.5, 7.5), lightValue: 50);
        Assert.AreEqual(1, atDim.TierIndex, "光照 50 > 0 ⇒ **不满足最暗档**，取 index 1 ✓");
        Assert.AreEqual(7.5, atDim.PercentUsed, 1e-9);

        // 四档（0 / 50 / 75 / 100）⇒ 中间档真的参与选择，且**边界含等号** ✓
        HungerRollResult mid49 = HungerSpawner.Roll(new CombatLog(), new FixedRng(99.0), Cfg4(12.5, 10, 7.5, 7.5), lightValue: 49);
        Assert.AreEqual(1, mid49.TierIndex, "49 > 0 ⇒ 不满足最暗档；49 ≤ 50 ⇒ 命中**shadowy 档** ✓");
        Assert.AreEqual(10.0, mid49.PercentUsed, 1e-9, "shadowy 档 = 10% ✓");

        HungerRollResult mid50 = HungerSpawner.Roll(new CombatLog(), new FixedRng(99.0), Cfg4(12.5, 10, 7.5, 7.5), lightValue: 50);
        Assert.AreEqual(1, mid50.TierIndex, "50 ≤ 50 ⇒ 仍取 shadowy（边界**含等号**，不是 `<`）✓");

        HungerRollResult mid75 = HungerSpawner.Roll(new CombatLog(), new FixedRng(99.0), Cfg4(12.5, 10, 7.5, 7.5), lightValue: 75);
        Assert.AreEqual(2, mid75.TierIndex, "75 ≤ 75 ⇒ dim 档 ✓");
        Assert.AreEqual(7.5, mid75.PercentUsed, 1e-9);

        HungerRollResult bright = HungerSpawner.Roll(new CombatLog(), new FixedRng(99.0), Cfg4(12.5, 10, 7.5, 7.5), lightValue: 100);
        Assert.AreEqual(3, bright.TierIndex, "光照 100 ⇒ 只有末档覆盖（**最亮档也要掷**）✓");
        Assert.AreEqual(7.5, bright.PercentUsed, 1e-9, "满光照仍有 7.5% —— D-5 在满光照下仍生效 ✓");
    }

    [TestMethod]
    public void Roll_EveryDraw_WritesRngDraw()
    {
        // 掷了但没中 ⇒ 1 条 RngDraw ✓
        var logMiss = new CombatLog();
        HungerRollResult miss = HungerSpawner.Roll(logMiss, new FixedRng(99.0), Cfg(12.5, 7.5), lightValue: 0);
        Assert.IsFalse(miss.Triggered, "99.0 ≥ 12.5 ⇒ 没中 ✓");
        Assert.AreEqual(1, logMiss.Events.Count, "🔴 掷了就必写 `RngDraw`（哪怕没中）✓");
        Assert.IsInstanceOfType(logMiss.Events[0], typeof(RngDraw));

        // 中了 ⇒ 同样只有**一次**抽取（饥饿没有"种类"子掷）✓
        var logHit = new CombatLog();
        HungerRollResult hit = HungerSpawner.Roll(logHit, new FixedRng(0.0), Cfg(12.5, 7.5), lightValue: 0);
        Assert.IsTrue(hit.Triggered, "0.0 < 12.5 ⇒ 中了 ✓");
        Assert.AreEqual(1, logHit.Events.Count, "🔴 饥饿只掷一次（无子掷）⇒ 一条 `RngDraw` ✓");
    }

    [TestMethod]
    public void FoodRequired_IsSurvivorsTimesPerHero()
    {
        HungerConfig cfg = Cfg(12.5, 7.5);
        Assert.AreEqual(4, HungerSpawner.FoodRequired(cfg, 4), "4 人 × 1 份 = 4 ✓");
        Assert.AreEqual(0, HungerSpawner.FoodRequired(cfg, 0), "无人 ⇒ 0 ✓");
        Assert.AreEqual(0, HungerSpawner.FoodRequired(cfg, -3), "负人数 ⇒ 钳到 0（不产生负数需求）✓");

        var twoPer = new HungerConfig(new List<HungerTier> { new(100, 5.0) },
            FoodPerHero: 2, EatHealPercent: 5.0, StarveHpPercent: 20.0, StarveMorale: 20);
        Assert.AreEqual(6, HungerSpawner.FoodRequired(twoPer, 3), "每人口粮可配（3 人 × 2 = 6）✓");
    }
}
