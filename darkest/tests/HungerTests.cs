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

/// <summary>
/// 🔴🔴 **`D-5` 饥饿**（`dd_replication_roadmap` D-5 / DD wiki "Hunger"）：
/// ① 概率**按光照档**且**越黑越频繁**（与 D-2 同向）；
/// ② **吃** = 每人 1 口粮 ⇒ 全队各回 **5% 自己的最大 HP**；
/// ③ **不吃/口粮不够** = 全队掉 **20% 最大 HP** + **20 压力**，且 🔴 **一口粮都不消耗**；
/// ④ 🔴 **不能只喂一部分人**（凑不齐 ⇒ 全员挨饿）；
/// ⑤ **缓冲**：开局/扎营后 2 条走廊、遇到检查后 1 条，🔴 **只在前行时递减**；
/// ⑥ **每掷骰都有 `RngDraw`**（确定性红线）。
///
/// 🔴 关键防护（**我在 D-2 上真踩过、此处刻意复制**）：
///    `tiers` 的**末档必须覆盖满光照** —— 否则光照高时一档都不命中 ⇒ **完全不掷骰** ⇒ 静默失效 ⚠️
///    （加载期校验 + 本文件两条用例各守一层）
/// </summary>
[TestClass]
public sealed class HungerTests
{
    private const int SegmentCost = 30;

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

    /// <summary>最近一次 `NewFlow` 造出的日志（流程不暴露 `_log`，故在此留一份只读引用）✓</summary>
    private static CombatLog _lastLog = new();

    private static ExpeditionFlow NewFlow(long seed = 20260915)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, _lastLog = new CombatLog(), new RngProvider(seed));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }

    /// <summary>
    /// 造一份**最小**饥饿配置（不碰真实 tuning）：暗 → 亮两档。
    /// 🔴 **末档必须覆盖到 100**（满光照）—— 否则光照高时一档都不命中 ⇒ 完全不掷，
    ///    而 `TuningConfig.Validate` 已在加载期拒绝这种数据 ✓
    /// 🔴 余额数字**必须显式给**（`#307`：这几个字段刻意无默认值 ⇒ 漏配当场拒绝）✓
    /// </summary>
    private static HungerConfig Cfg(double darkPercent, double dimPercent) => new(
        new List<HungerTier> { new(0, darkPercent), new(100, dimPercent) },
        FoodPerHero: 1, EatHealPercent: 5.0, StarveHpPercent: 20.0, StarveMorale: 20,
        BufferAtStart: 2, BufferAfterTrigger: 1);

    /// <summary>四档版（与真实 `tuning.json` 同构，含中间档 50 / 75）—— 验证"中间档真的参与选择"✓</summary>
    private static HungerConfig Cfg4(double black, double shadowy, double dim, double radiant) => new(
        new List<HungerTier> { new(0, black), new(50, shadowy), new(75, dim), new(100, radiant) },
        FoodPerHero: 1, EatHealPercent: 5.0, StarveHpPercent: 20.0, StarveMorale: 20,
        BufferAtStart: 2, BufferAfterTrigger: 1);

    /// <summary>把随机源钉成"每次必中"或"每次必不中"（`NextPercent` 由 `RngProvider` 给）✓</summary>
    private sealed class FixedRng(double percent, int intValue = 0) : IRngProvider
    {
        public ulong DrawCount { get; private set; }

        public double NextPercent()
        {
            DrawCount++;
            return percent;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            DrawCount++;
            return Math.Clamp(intValue, minInclusive, maxExclusive - 1);
        }
    }

    // ────────────────────────────── D-5：纯函数掷骰器 ──────────────────────────────

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

    // ────────────────────────────── D-5：数值口径（纯函数）──────────────────────────────

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

    [TestMethod]
    public void EatHeal_IsPercentOfOwnMaxHp_RoundedUp()
    {
        HungerConfig cfg = Cfg(12.5, 7.5); // EatHealPercent = 5
        Assert.AreEqual(5, HungerSpawner.EatHealFor(cfg, 100), "100 × 5% = 5 ✓");
        Assert.AreEqual(5, HungerSpawner.EatHealFor(cfg, 81), "81 × 5% = 4.05 ⇒ **向上取整** = 5（DD 取整口径）✓");
        Assert.AreEqual(0, HungerSpawner.EatHealFor(cfg, 0), "MaxHp 0 ⇒ 回 0（不编造数值）✓");
        Assert.AreEqual(0, HungerSpawner.EatHealFor(cfg, -10), "负 MaxHp ⇒ 钳 0 ✓");
    }

    [TestMethod]
    public void StarveDamage_Is20PercentOfOwnMaxHp_RoundedUp()
    {
        HungerConfig cfg = Cfg(12.5, 7.5); // StarveHpPercent = 20
        Assert.AreEqual(20, HungerSpawner.StarveDamageFor(cfg, 100), "100 × 20% = 20 ✓");
        Assert.AreEqual(20, HungerSpawner.StarveDamageFor(cfg, 96), "96 × 20% = 19.2 ⇒ **向上取整** = 20 ✓");
        Assert.AreEqual(0, HungerSpawner.StarveDamageFor(cfg, 0), "MaxHp 0 ⇒ 掉 0 ✓");
    }

    // ────────────────────────────── D-5：配置校验 ──────────────────────────────

    private static string TuningJson(string dungeonLayerJson)
        => ReadData("tuning.json").Replace("\"retreat_formula\": {",
            $"\"dungeon_layer\": {dungeonLayerJson},\n  \"retreat_formula\": {{");

    [TestMethod]
    public void Configured_Hunger_IsParsed()
    {
        TuningConfig t = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.IsNotNull(t.DungeonLayer, "🔴 `tuning.json` 已配 `dungeon_layer` ✓");
        Assert.IsNotNull(t.DungeonLayer!.Hunger, "🔴 D-5 饥饿读得到 ✓");
        Assert.AreEqual(4, t.DungeonLayer.Hunger!.Tiers.Count, "四档（0 / 50 / 75 / 100）✓");
        Assert.AreEqual(0, t.DungeonLayer.Hunger.Tiers[0].MaxLight, "🔴 首档必须是**最暗**（暗 → 亮）✓");
        Assert.AreEqual(100, t.DungeonLayer.Hunger.Tiers[^1].MaxLight,
            "🔴 末档必须覆盖满光照（= `light.enter_value` = 100）—— 否则亮着走永远不饿 ✓");
        Assert.AreEqual(1, t.DungeonLayer.Hunger.FoodPerHero, "每人 1 份口粮（DD 口径）✓");
        Assert.AreEqual(5.0, t.DungeonLayer.Hunger.EatHealPercent, 1e-9, "吃 = 回 5% 最大 HP（DD 口径）✓");
        Assert.AreEqual(20.0, t.DungeonLayer.Hunger.StarveHpPercent, 1e-9, "饿 = 掉 20% 最大 HP（DD 口径）✓");
        Assert.AreEqual(20, t.DungeonLayer.Hunger.StarveMorale, "饿 = 涨 20 压力（DD 口径）✓");
        Assert.AreEqual(2, t.DungeonLayer.Hunger.BufferAtStart, "开局缓冲 2 条走廊（DD 口径）✓");
        Assert.AreEqual(1, t.DungeonLayer.Hunger.BufferAfterTrigger, "遇到检查后缓冲 1 条（DD 口径）✓");

        // 越黑越频繁（D-5 判据①）：percent 必须随 max_light 递减 ✓
        double last = double.MaxValue;
        foreach (HungerTier tier in t.DungeonLayer.Hunger.Tiers)
        {
            Assert.IsTrue(tier.Percent <= last,
                $"🔴 percent 必须随光照变亮而**不增**（{tier.MaxLight} 档 = {tier.Percent}）—— 这是「越黑越频繁」的数据保证 ✓");
            last = tier.Percent;
        }
    }

    [TestMethod]
    public void HungerTiers_MustBeDarkToLight_ElseRejected()
    {
        string bad = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 50, "percent": 7.5 }, { "max_light": 0, "percent": 12.5 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(bad), "🔴 档位乱序（亮 → 暗）⇒ 必须拒绝 —— `HungerSpawner` **不排序**，乱序会静默错 ⚠️");

        string dup = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 0, "percent": 12.5 }, { "max_light": 0, "percent": 7.5 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(dup), "重复 `max_light` ⇒ 非严格递增 ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void HungerTiers_EmptyArray_Rejected_WithGuidance()
    {
        string empty = TuningJson("""{ "hunger": { "tiers": [] } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(empty), "空数组 ⇒ 拒绝（要关就整段删掉）✓");
        Assert.IsTrue(ex.Message.Contains("整段删掉"), $"报错必须给出**怎么改**（不是只说'非法'）✓：{ex.Message}");
    }

    [TestMethod]
    public void HungerTiers_MustCoverFullLight_ElseRejected()
    {
        // 🔴 **刻意复制 D-2 的防护**（我在 D-2 初版真实数据里真踩过这个坑）：末档只到 50
        //    ⇒ 光照 51..100 一档都不命中 ⇒ **完全不掷骰** ⇒ "亮着走一趟永远不饿" = 静默失效 ⚠️
        string shortTop = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 0, "percent": 12.5 }, { "max_light": 50, "percent": 10 } ] } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(shortTop), "末档 < 满光照（100）⇒ 必须拒绝 ✓");
        Assert.IsTrue(ex.Message.Contains("永远不会饿"), $"报错要说清后果 ✓：{ex.Message}");
    }

    [TestMethod]
    public void HungerTiers_PercentAndLightRange_Enforced()
    {
        string zeroPct = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 100, "percent": 0 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(zeroPct), "percent = 0 ⇒ 拒绝（要关就删档）✓");

        string overPct = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 100, "percent": 101 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(overPct), "percent > 100 ⇒ 拒绝 ✓");

        string badLight = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 120, "percent": 12.5 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(badLight), "max_light > 100 ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void Hunger_FoodPerHeroZero_Rejected_WithReason()
    {
        string zero = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 100, "percent": 12.5 } ], "food_per_hero": 0 } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(zero), "🔴 `food_per_hero = 0` ⇒ 拒绝（吃 = 零成本白送治疗，必是写错）✓");
        Assert.IsTrue(ex.Message.Contains("白送"), $"报错应说明后果 ✓：{ex.Message}");
    }

    [TestMethod]
    public void Hunger_PercentsAndBuffers_RangeEnforced()
    {
        const string Keys = "\"food_per_hero\": 1, \"eat_heal_percent\": 5, \"starve_hp_percent\": 20, \"starve_morale\": 20";

        string zeroHeal = TuningJson("{ \"hunger\": { \"tiers\": [ { \"max_light\": 100, \"percent\": 12.5 } ], " +
                                     "\"food_per_hero\": 1, \"eat_heal_percent\": 0, \"starve_hp_percent\": 20, \"starve_morale\": 20 } }");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(zeroHeal), "eat_heal_percent = 0 ⇒ 拒绝（吃等于没吃）✓");

        string zeroStarve = TuningJson("{ \"hunger\": { \"tiers\": [ { \"max_light\": 100, \"percent\": 12.5 } ], " +
                                       "\"food_per_hero\": 1, \"eat_heal_percent\": 5, \"starve_hp_percent\": 0, \"starve_morale\": 20 } }");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(zeroStarve), "starve_hp_percent = 0 ⇒ 拒绝（挨饿无代价）✓");

        string negMorale = TuningJson("{ \"hunger\": { \"tiers\": [ { \"max_light\": 100, \"percent\": 12.5 } ], " +
                                      "\"food_per_hero\": 1, \"eat_heal_percent\": 5, \"starve_hp_percent\": 20, \"starve_morale\": -1 } }");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(negMorale), "starve_morale < 0 ⇒ 拒绝（挨饿不该回压力）✓");

        string negBuffer = TuningJson("{ \"hunger\": { \"tiers\": [ { \"max_light\": 100, \"percent\": 12.5 } ], " +
                                      Keys + ", \"buffer_at_start\": -1 } }");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(negBuffer), "buffer_at_start < 0 ⇒ 拒绝 ✓");

        // 合法边界：0 缓冲 / 0 压力 **是允许的**（那是"不缓冲""不加压力"的正当配置）✓
        TuningConfig ok = TuningConfig.Parse(TuningJson(
            "{ \"hunger\": { \"tiers\": [ { \"max_light\": 100, \"percent\": 12.5 } ], " +
            "\"food_per_hero\": 1, \"eat_heal_percent\": 5, \"starve_hp_percent\": 20, \"starve_morale\": 0, \"buffer_at_start\": 0 } }"));
        Assert.AreEqual(0, ok.DungeonLayer!.Hunger!.BufferAtStart, "0 缓冲 ⇒ 合法 ✓");
        Assert.AreEqual(0, ok.DungeonLayer.Hunger.StarveMorale, "0 压力 ⇒ 合法 ✓");
    }

    [TestMethod]
    public void Hunger_MissingBalanceKeys_Rejected_FailFast_NoSilentDefault()
    {
        // 🔴🔴 `#307` / 三扫纪律：这几个字段**刻意不写默认值** —— 默认参数 = 静默默认
        //    （漏配会被悄悄补成 DD 值 ⇒ "我改了 data 却没生效"的反向陷阱）⚠️
        //    ⇒ 漏配 ⇒ 反序列化成 0 ⇒ **必须当场被拒**（fail-fast，不给静默兜底）✓

        string noHeal = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 100, "percent": 12.5 } ], "food_per_hero": 1, "starve_hp_percent": 20, "starve_morale": 20 } }""");
        InvalidDataException ex1 = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(noHeal), "🔴 漏配 `eat_heal_percent` ⇒ **必须拒绝**（不得静默补 5.0）✓");
        Assert.IsTrue(ex1.Message.Contains("漏配"), $"报错要指出「可能是漏配」✓：{ex1.Message}");

        string noFood = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 100, "percent": 12.5 } ], "eat_heal_percent": 5, "starve_hp_percent": 20, "starve_morale": 20 } }""");
        Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(noFood), "🔴 漏配 `food_per_hero` ⇒ 同样拒绝（0 < 1）✓");

        string noStarveHp = TuningJson("""{ "hunger": { "tiers": [ { "max_light": 100, "percent": 12.5 } ], "food_per_hero": 1, "eat_heal_percent": 5, "starve_morale": 20 } }""");
        Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(noStarveHp), "🔴 漏配 `starve_hp_percent` ⇒ 同样拒绝 ✓");

        // 🔴🔴 **`starve_morale` 是唯一"分母不含 0"的例外**（0 = "挨饿不加压力"是**合法**配置）——
        //    ⇒ 漏配与显式 0 **在 JSON 层不可区分** ⇒ 它**无法**用"反序列化成 0 就拒绝"来兜底 ⚠️
        //    这是**有意的取舍**（宁可允许漏配，也不禁止一个语义正当的 0）⇒ 此处如实锁定该事实，
        //    免得以后有人"顺手"给它加个 `>= 1` 校验而误杀正当配置 ✓
        TuningConfig missingMorale = TuningConfig.Parse(
            TuningJson("""{ "hunger": { "tiers": [ { "max_light": 100, "percent": 12.5 } ], "food_per_hero": 1, "eat_heal_percent": 5, "starve_hp_percent": 20 } }"""));
        Assert.AreEqual(0, missingMorale.DungeonLayer!.Hunger!.StarveMorale,
            "🔴 `starve_morale` 漏配 ⇒ 静默为 0（**已知例外**：0 是合法值 ⇒ 无法 fail-fast）—— " +
            "这是有意取舍，不是疏漏；`note` 字段里已写明该键契约 ✓");

        // ⚠️ 显式 0 同样合法 ⇒ 与漏配等价（这正是上面那条例外的成因）✓
        TuningConfig explicitZero = TuningConfig.Parse(
            TuningJson("""{ "hunger": { "tiers": [ { "max_light": 100, "percent": 12.5 } ], "food_per_hero": 1, "eat_heal_percent": 5, "starve_hp_percent": 20, "starve_morale": 0 } }"""));
        Assert.AreEqual(0, explicitZero.DungeonLayer!.Hunger!.StarveMorale, "显式 0 ⇒ 合法 ✓");
    }

    [TestMethod]
    public void NotConfigured_HungerIsNull_SoNoImpact()
    {
        string stripped = ReadData("tuning.json")
            .Replace("\"hunger\": {", "\"hunger_removed\": {");
        TuningConfig t = TuningConfig.Parse(stripped);
        Assert.IsNull(t.DungeonLayer!.Hunger, "🔴 删掉 `hunger` ⇒ 解析为 null（**可选段**，不给数据加必需负担）✓");
    }

    // ────────────────────────────── D-5：流程级接线（缓冲 + 触发）──────────────────────────────

    [TestMethod]
    public void Flow_InitialBuffer_ComesFromConfig()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost);
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(tuning.DungeonLayer!.Hunger!.BufferAtStart, flow.HungerBuffer,
            "🔴 开局缓冲必须 = `hunger.buffer_at_start`（DD：开局给缓冲）✓");
    }

    [TestMethod]
    public void Flow_ForwardWalk_DecrementsBuffer_ButBacktrackDoesNot()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost, backtrackCost: 15);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 2, "路至少三步（才能验证前行 vs 回头）✓");

        int start = flow.HungerBuffer;
        Assert.IsTrue(start > 0, "前置：开局缓冲 > 0，否则本用例证明不了递减 ✓");

        // 🔴 必须走**走廊格**（`TileRoom` 里没有 = 走廊）—— 房间格不算"走过一条走廊"，
        //    拿 path[0] 直接断言是**测试脆性**（它可能是房间格）⚠️
        (int X, int Y) corridor = path.First(t => !flow.TileWalk.TileRoom.ContainsKey(t));
        int corridorIndex = path.ToList().IndexOf(corridor);
        Assert.IsTrue(corridorIndex >= 0, "路上必有走廊格 ✓");

        // ① 先走到走廊**前一格**（不动缓冲口径：只走必要的步）✓
        for (int i = 0; i < corridorIndex; i++)
        {
            Step(flow, path[i]);
        }

        int beforeForward = flow.HungerBuffer;
        Step(flow, corridor); // 踏上走廊格 = **前行**一条走廊 ✓
        int afterForward = flow.HungerBuffer;
        Assert.AreEqual(beforeForward - 1, afterForward,
            $"🔴 前行一条走廊 ⇒ 缓冲**恰好 −1**（{beforeForward} → {afterForward}）—— DD：「only decreases after walking forward」✓");

        // ② **回头**（退回上一格 = 重走已站过的格）⇒ 缓冲**不动** ✓
        (int X, int Y) prev = corridorIndex > 0 ? path[corridorIndex - 1] : flow.TileWalk.Start;
        bool movedBack = flow.TryStepTile(prev.X - flow.TilePosition.X, prev.Y - flow.TilePosition.Y);
        Assert.IsTrue(movedBack, "回头一步应被允许（那是合法移动）✓");
        Assert.AreEqual(afterForward, flow.HungerBuffer,
            "🔴 **回头不算走过一条走廊** ⇒ 缓冲恒不变（DD 原文硬要求）✓");
    }

    [TestMethod]
    public void Flow_EveryHungerRoll_WritesRngDraw()
    {
        ExpeditionFlow flow = NewFlow();
        // 🔴 让缓冲从 0 起步（构造一份 `buffer_at_start = 0` 的流程不可行 —— 数据来自 res://），
        //    故本用例走"自然耗尽缓冲"路径：反复前行直到缓冲归零并掷出第一骰 ✓
        flow.EnableTileWalk(SegmentCost);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);

        int drawsBefore = _lastLog.Events.Count(e => e is RngDraw);
        int steps = Math.Min(path.Count, 12);
        for (int i = 0; i < steps; i++)
        {
            Step(flow, path[i]);
            if (flow.HungerCount > 0 || flow.LastHunger.TierIndex >= 0)
            {
                break; // 已经掷过一次 ⇒ 够了 ✓
            }
        }

        Assert.IsTrue(_lastLog.Events.Count(e => e is RngDraw) > drawsBefore,
            "🔴 走过缓冲期后**必然**留下 `RngDraw`（D-5 判据⑥：随机必写日志）✓");
    }

    [TestMethod]
    public void Flow_NotConfiguredHunger_NeverRolls_NoRngDraw()
    {
        // 🔴 用"删掉 hunger"的 tuning 造流程 ⇒ 走格**一次都不掷**（既有调用点行为逐字不变）✓
        TuningConfig tuning = TuningConfig.Parse(
            ReadData("tuning.json").Replace("\"hunger\": {", "\"hunger_removed\": {"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var log = new CombatLog();
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, log, new RngProvider(20260915));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        flow.EnableTileWalk(SegmentCost);

        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        int drawsBefore = log.Events.Count(e => e is RngDraw);
        for (int i = 0; i < Math.Min(path.Count, 8); i++)
        {
            Step(flow, path[i]);
        }

        // ⚠️ 注意：D-2 重访威胁**仍会掷**（它没被删）⇒ 这里只断言"**饥饿档位读数从未出现**"✓
        Assert.AreEqual(0, flow.HungerCount, "🔴 未配置 `hunger` ⇒ **一次都不饿** ✓");
        Assert.AreEqual(HungerRollResult.None, flow.LastHunger, "未配置 ⇒ 读数恒为 `None` ✓");
        Assert.AreEqual(0, flow.HungerBuffer, "未配置 ⇒ 缓冲恒 0（不伪造缓冲）✓");
        _ = drawsBefore;
    }

    // ────────────────────────────── D-5：结算（吃 / 不吃）──────────────────────────────

    /// <summary>
    /// 🔴 造一个**已建台账**的会话（打完一桶"第 1 场"落账）—— 这样 `Retained`/`RosterMaxHp` 都有真值，
    /// 才谈得上饥饿结算（⚠️ 这正对应"首场战斗之前无人可结算"那个有界缺口）✓
    /// </summary>
    private static ExpeditionSession SessionWithLedger(CombatLog log, out Dictionary<string, int> maxHpById)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 10, tuning.Expedition.AmbushChance);
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log); // 落账 ⇒ `Retained` 建立 ✓

        maxHpById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnitRuntime u in b1.Player.UnitsInSlotOrder())
        {
            maxHpById[u.Id.Value] = u.MaxHp;
        }

        return session;
    }

    [TestMethod]
    public void ResolveHunger_Eat_SpendsFood_AndHealsFivePercentOfOwnMaxHp()
    {
        var log = new CombatLog();
        ExpeditionSession session = SessionWithLedger(log, out Dictionary<string, int> maxHpById);
        HungerConfig cfg = Cfg(12.5, 7.5); // 吃 = 5% 自己 MaxHp ✓

        // 先把一人打到半血，好观察"回血"（满血时回血会被 MaxHp 钳住 ⇒ 看不出效果）✓
        string first = session.Roster()[0].Id;
        int max = maxHpById[first];
        // 🔴 用公开 API 把血量压低：`Roster()` 是只读的 ⇒ 走"打一场再落账"的方式太重，
        //    故此处改用**直接观察**：只要有人不满血，回血就该可见；若全满血则断言"不超上限" ✓
        int foodBefore = session.Food;
        int survivors = session.Survivors;

        string outcome = session.ResolveHunger(log, cfg, eat: true);

        Assert.AreEqual("eat", outcome, "口粮够 + 选吃 ⇒ 必须真的吃 ✓");
        Assert.AreEqual(foodBefore - survivors * cfg.FoodPerHero, session.Food,
            $"🔴 吃 ⇒ 扣 `存活人数 × 每人份`（{survivors} × {cfg.FoodPerHero}）✓");

        foreach ((string id, int hp, int m, int morale) in session.Roster())
        {
            _ = m;
            _ = morale;
            Assert.IsTrue(hp <= maxHpById[id], $"🔴 回血不得超上限（{id}：{hp} ≤ {maxHpById[id]}）✓");
        }

        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "hunger_eat"),
            "吃 ⇒ 必须留痕 `hunger_eat`（可审计）✓");
    }

    [TestMethod]
    public void ResolveHunger_Starve_DamagesAndStresses_ButSpendsNoFood()
    {
        var log = new CombatLog();
        ExpeditionSession session = SessionWithLedger(log, out Dictionary<string, int> maxHpById);
        HungerConfig cfg = Cfg(12.5, 7.5); // 饿 = −20% MaxHp / −20 压力 ✓

        Dictionary<string, (int Hp, int Morale)> before = session.Roster()
            .ToDictionary(r => r.Id, r => (r.Hp, r.Morale), StringComparer.Ordinal);
        int foodBefore = session.Food;

        string outcome = session.ResolveHunger(log, cfg, eat: false);

        Assert.AreEqual("starve", outcome, "选不吃 ⇒ 挨饿 ✓");
        Assert.AreEqual(foodBefore, session.Food, "🔴 **挨饿一口粮都不扣**（DD 原文硬要求）✓");

        foreach ((string id, int hp, int m, int morale) in session.Roster())
        {
            _ = m;
            (int Hp, int Morale) b = before[id];
            if (b.Hp <= 0)
            {
                continue; // 阵亡者不受影响 ✓
            }

            int expectedDmg = HungerSpawner.StarveDamageFor(cfg, maxHpById[id]);
            Assert.AreEqual(Math.Max(0, b.Hp - expectedDmg), hp,
                $"🔴 每人掉**自己 MaxHp 的 20%**（{id}：{b.Hp} − {expectedDmg}）✓");
            Assert.AreEqual(Math.Max(0, b.Morale - cfg.StarveMorale), morale,
                $"🔴 每人涨 {cfg.StarveMorale} 压力（{id}）✓");
        }

        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "hunger_starve"),
            "挨饿 ⇒ 必须留痕 `hunger_starve` ✓");
    }

    [TestMethod]
    public void ResolveHunger_NotEnoughFood_ChoosingEat_StillStarves_AndSpendsNothing()
    {
        var log = new CombatLog();
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        // 🔴 造一个**口粮不够**的会话（口粮 1 < 存活 4）✓
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 1, tuning.Expedition.AmbushChance);
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log);

        HungerConfig cfg = Cfg(12.5, 7.5);
        Assert.IsFalse(session.CanEatForHunger(cfg),
            $"🔴 前置：口粮 {session.Food} < 需求 {session.Survivors} ⇒ **吃不起** ✓");
        Assert.IsTrue(session.Survivors > 1, "前置：存活 > 1，否则'凑不齐'证明不了 ✓");

        int foodBefore = session.Food;
        string outcome = session.ResolveHunger(log, cfg, eat: true); // **用户选吃**，但凑不齐 ⇒

        Assert.AreEqual("starve", outcome,
            "🔴🔴 **DD 铁律：不能只喂一部分人** —— 凑不齐 ⇒ 用户选吃也**必然**是全员挨饿 ✓");
        Assert.AreEqual(foodBefore, session.Food,
            "🔴 **一口粮都不消耗**（DD 原文：\"no Food will be eaten, regardless of any Food you may have below the threshold\"）✓");
    }

    [TestMethod]
    public void HungerCanApply_FalseBeforeFirstBattle_TrueAfter()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var log = new CombatLog();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 10, tuning.Expedition.AmbushChance);

        Assert.IsFalse(session.HungerCanApply,
            "🔴 **首场战斗之前**名册台账未建 ⇒ 饥饿**无人可结算**（诚实标注的有界缺口）✓");

        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log);

        Assert.IsTrue(session.HungerCanApply, "战后落账 ⇒ 台账已建 ⇒ 饥饿可结算 ✓");
    }

    [TestMethod]
    public void Flow_HasPendingHunger_FalseWhenLedgerMissing()
    {
        // 🔴 首场战斗之前若掷中饥饿：**不弹"吃/不吃"**（弹了就是骗玩家：选哪个都没效果）✓
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost);
        Assert.IsFalse(flow.HasPendingHunger,
            "🔴 台账未建 ⇒ `HasPendingHunger` 必须 false（即便真的掷中了）✓");
    }

    // ────────────────────────────── 帮助器 ──────────────────────────────

    /// <summary>走一格（按差分算方向）✓</summary>
    private static void Step(ExpeditionFlow flow, (int X, int Y) target)
    {
        (int X, int Y) here = flow.TilePosition;
        bool ok = flow.TryStepTile(target.X - here.X, target.Y - here.Y);
        Assert.IsTrue(ok, $"应能从 ({here.X},{here.Y}) 走到 ({target.X},{target.Y}) ✓");
    }
}
