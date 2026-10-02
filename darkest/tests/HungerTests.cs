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
public sealed partial class HungerTests
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

    // ────────────────────────────── D-5：数值口径（纯函数）──────────────────────────────

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
}
