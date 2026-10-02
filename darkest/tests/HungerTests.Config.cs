// 🔴 从 HungerTests.cs 拆出（用户红线 ≤600 行 · 架构 file_size_split §1.3）——只搬家、零行为改动 ✓
//    本文件 = D-5 饥饿：**配置校验**（TuningJson 变异 ＋ 解析 ／ 档序 ／ 满光照覆盖 ／ 漏配 fail-fast 共 8 条）
//    依赖主片私有成员：ReadData（读 data/tuning.json）✓

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
}
