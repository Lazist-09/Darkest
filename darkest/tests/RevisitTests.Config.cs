using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// ① 从 `RevisitTests.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **D-2 重访威胁 · 配置校验**（`Configured_*`／`RevisitTiers_*`／`RevisitWeights_*`／`RevisitLightCost_*`／`NotConfigured_*` 十个用例：解析 ／ 暗→亮排序 ／ 空数组给指引 ／ 全光照覆盖 ／ 百分比与光照区间 ／ 权重单侧允许 ／ 光代价非正 ／ 未配置无影响）✓
/// ③ 🔴 依赖主类私有成员：`ReadData`／`TuningJson`；外部走 `TuningConfig`／`ReadData`／`TuningJson`✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public sealed partial class RevisitTests
{
    // ────────────────────────────── D-2：配置校验 ──────────────────────────────

    private static string TuningJson(string dungeonLayerJson)
        => ReadData("tuning.json").Replace("\"retreat_formula\": {",
            $"\"dungeon_layer\": {dungeonLayerJson},\n  \"retreat_formula\": {{");

    [TestMethod]
    public void Configured_Revisit_IsParsed()
    {
        TuningConfig t = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.IsNotNull(t.DungeonLayer, "🔴 `tuning.json` 已配 `dungeon_layer` ⇒ 解析出来不能是 null ✓");
        Assert.AreEqual(15, t.DungeonLayer!.RevisitLightCost, "D-1 回头代价读得到 ✓");
        Assert.IsNotNull(t.DungeonLayer.Revisit, "D-2 回头威胁读得到 ✓");
        Assert.AreEqual(4, t.DungeonLayer.Revisit!.Tiers.Count, "四档（0 / 25 / 50 / 100）✓");
        Assert.AreEqual(0, t.DungeonLayer.Revisit.Tiers[0].MaxLight, "🔴 首档必须是**最暗**（暗 → 亮）✓");
        Assert.AreEqual(100, t.DungeonLayer.Revisit.Tiers[^1].MaxLight,
            "🔴 末档必须覆盖满光照（= `light.enter_value` = 100）—— 否则亮着走永远不掷 ✓");
        // 越黑越频繁（D-2 判据②）：percent 必须随 max_light 递减 ✓
        double last = double.MaxValue;
        foreach (RevisitThreatTier tier in t.DungeonLayer.Revisit.Tiers)
        {
            Assert.IsTrue(tier.Percent <= last,
                $"🔴 percent 必须随光照变亮而**不增**（{tier.MaxLight} 档 = {tier.Percent}）—— 这是「越黑越频繁」的数据保证 ✓");
            last = tier.Percent;
        }
    }

    [TestMethod]
    public void RevisitTiers_MustBeDarkToLight_ElseRejected()
    {
        string bad = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 50, "percent": 2.5 }, { "max_light": 0, "percent": 5.0 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(bad), "🔴 档位乱序（亮 → 暗）⇒ 必须拒绝 —— `RevisitSpawner` **不排序**，乱序会静默错 ⚠️");

        string dup = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 0, "percent": 5.0 }, { "max_light": 0, "percent": 2.5 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(dup), "重复 `max_light` ⇒ 非严格递增 ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void RevisitTiers_EmptyArray_Rejected_WithGuidance()
    {
        string empty = TuningJson("""{ "revisit": { "tiers": [] } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(empty), "空数组 ⇒ 拒绝（要关就整段删掉）✓");
        Assert.IsTrue(ex.Message.Contains("整段删掉"), $"报错必须给出**怎么改**（不是只说'非法'）✓：{ex.Message}");
    }

    [TestMethod]
    public void RevisitTiers_MustCoverFullLight_ElseRejected()
    {
        // 🔴 这是我**在初版真实数据里真踩过的坑**：末档只到 50 ⇒ 光照 51..100 一档都不命中 ⇒ **完全不掷骰**，
        //    于是"亮着走一趟"永远平安 ⇒ D-2 在最常见的情形下静默失效 ⚠️
        string shortTop = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 0, "percent": 5 }, { "max_light": 50, "percent": 2.5 } ] } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(shortTop), "末档 < 满光照（100）⇒ 必须拒绝 ✓");
        Assert.IsTrue(ex.Message.Contains("静默失效"), $"报错要说清后果 ✓：{ex.Message}");
    }

    [TestMethod]
    public void RevisitTiers_PercentAndLightRange_Enforced()
    {
        string zeroPct = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 100, "percent": 0 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(zeroPct), "percent = 0 ⇒ 拒绝（要关就删档）✓");

        string overPct = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 100, "percent": 101 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(overPct), "percent > 100 ⇒ 拒绝 ✓");

        string badLight = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 120, "percent": 5 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(badLight), "max_light > 100 ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void RevisitWeights_ZeroZero_Rejected_ButSingleSideAllowed()
    {
        string both0 = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 100, "percent": 5 } ], "battle_weight": 0, "trap_weight": 0 } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(both0), "🔴 权重同时为 0 ⇒ 拒绝（否则判定触发后无种类可选 ⇒ 白掷一次）✓");
        Assert.IsTrue(ex.Message.Contains("白掷"), $"报错应说明后果 ✓：{ex.Message}");

        string oneSide = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 100, "percent": 5 } ], "battle_weight": 0, "trap_weight": 2 } }""");
        TuningConfig ok = TuningConfig.Parse(oneSide);
        Assert.AreEqual(0, ok.DungeonLayer!.Revisit!.BattleWeight, "单侧为 0 ⇒ **合法**（种类已定）✓");
    }

    [TestMethod]
    public void RevisitLightCost_NonPositive_Rejected()
    {
        string zero = TuningJson("""{ "revisit_light_cost": 0 }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(zero), "🔴 `revisit_light_cost = 0` ⇒ 拒绝（否则被当'未配置'静默忽略，是**数据写错**）✓");
        Assert.IsTrue(ex.Message.Contains("删掉该键"), $"报错应给出正确改法 ✓：{ex.Message}");

        string negative = TuningJson("""{ "revisit_light_cost": -5 }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(negative), "负数 ⇒ 同样拒绝 ✓");
    }

    [TestMethod]
    public void NotConfigured_DungeonLayerIsNull_SoNoImpact()
    {
        string stripped = ReadData("tuning.json")
            .Replace("\"dungeon_layer\": {", "\"dungeon_layer_removed\": {");
        TuningConfig t = TuningConfig.Parse(stripped);
        Assert.IsNull(t.DungeonLayer, "🔴 未配 `dungeon_layer` ⇒ 解析为 null（**可选段**，不给数据加必需负担）✓");
    }
}
