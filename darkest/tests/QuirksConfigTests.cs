using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M5 · Quirk 结构与校验**（派发卡 M5 + 一手 E 盘实测）：
///   库 **170 条** · **互斥必须可断言**（引用完整性 + **对称性**）· 分类分布可测 ✓
///
/// 数据来源：E 盘**一手** `shared/quirk/quirk_library.json`（**不是**第三方参考件 —— M4 的教训 ✓）
/// </summary>
[TestClass]
public sealed class QuirksConfigTests
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

    /// <summary>🔴 对**真实落库数据**验收（数字若变即红 ⇒ 这就是 M5 的验收 ✓）</summary>
    [TestMethod]
    public void LandedLibrary_MatchesTheMeasuredNumbers_AndIncompatibilityIsAssertable()
    {
        QuirksConfig cfg = QuirksConfig.Parse(ReadData("quirks.json"));

        Assert.AreEqual(170, cfg.Quirks.Count, "库应为 170 条（契约定义）✓");
        Assert.AreEqual(68, cfg.Quirks.Count(q => q.IsPositive), "正面 = 68 ✓");
        Assert.AreEqual(102, cfg.Quirks.Count(q => !q.IsPositive), "负面 = 102 ✓");
        Assert.AreEqual(23, cfg.Quirks.Count(q => q.IsDisease), "疾病 = 23 ✓");
        Assert.AreEqual(108, cfg.Quirks.Count(q => q.Classification == "mental"), "mental = 108 ✓");
        Assert.AreEqual(56, cfg.Quirks.Count(q => q.Classification == "physical"), "physical = 56 ✓");
        Assert.AreEqual(6, cfg.Quirks.Count(q => q.Classification == ""), "分类为空的 = 6（如实保留）✓");

        Assert.AreEqual(130, cfg.IncompatibleEdgeCount, "互斥边总数 = 130 ✓");
        Assert.AreEqual(95, cfg.Quirks.Count(q => q.IncompatibleQuirks.Count > 0), "声明互斥的条目 = 95 ✓");

        // 互斥可断言（一手实测的样例：tough ↔ fragile 双向 ✓）
        Assert.IsTrue(cfg.AreIncompatible("tough", "fragile"), "tough → fragile ✓");
        Assert.IsTrue(cfg.AreIncompatible("fragile", "tough"), "对称：fragile → tough ✓");
        Assert.IsFalse(cfg.AreIncompatible("tough", "tough"), "自反不算互斥（校验也已禁）✓");

        Console.WriteLine($"[M5] 落库验收：{cfg.Quirks.Count} 条 · 正 {cfg.Quirks.Count(q => q.IsPositive)} / 负 "
            + $"{cfg.Quirks.Count(q => !q.IsPositive)} · 疾病 {cfg.Quirks.Count(q => q.IsDisease)} · 互斥边 {cfg.IncompatibleEdgeCount} ✓");
        TestContext.WriteLine("[M5] 170 条 + 互斥可断言 ✓");
    }

    [TestMethod]
    public void Validation_RejectsDanglingSelfAndAsymmetricIncompatibility()
    {
        const string baseJson = """
        { "quirks": [
          { "id": "a", "buffs": [], "is_positive": true, "is_disease": false, "classification": "physical",
            "incompatible_quirks": ["b"], "curio_tag": "", "curio_tag_chance": 0, "keep_loot": false,
            "random_chance": 1, "can_be_replaced_by_new_quirk": true, "can_modify_in_activity": false,
            "show_explicit_buff_description": true, "show_flavor_description": false,
            "show_explicit_curio_tag_description": false, "origin": "synthetic" },
          { "id": "b", "buffs": [], "is_positive": false, "is_disease": false, "classification": "mental",
            "incompatible_quirks": ["a"], "curio_tag": "", "curio_tag_chance": 0, "keep_loot": false,
            "random_chance": 1, "can_be_replaced_by_new_quirk": true, "can_modify_in_activity": false,
            "show_explicit_buff_description": true, "show_flavor_description": false,
            "show_explicit_curio_tag_description": false, "origin": "synthetic" } ] }
        """;

        // 基准：对称 + 引用完整 ⇒ 通过
        QuirksConfig ok = QuirksConfig.Parse(baseJson);
        Assert.AreEqual(2, ok.IncompatibleEdgeCount);

        // ① 悬空引用
        var dangling = Assert.ThrowsException<InvalidDataException>(
            () => QuirksConfig.Parse(baseJson.Replace("\"incompatible_quirks\": [\"b\"]", "\"incompatible_quirks\": [\"zzz\"]")));
        StringAssert.Contains(dangling.Message, "不存在");

        // ② 自反
        var self = Assert.ThrowsException<InvalidDataException>(
            () => QuirksConfig.Parse(baseJson.Replace("\"incompatible_quirks\": [\"b\"]", "\"incompatible_quirks\": [\"a\"]")));
        StringAssert.Contains(self.Message, "自己");

        // ③ 非对称（b 不再指回 a）
        var asym = Assert.ThrowsException<InvalidDataException>(
            () => QuirksConfig.Parse(baseJson.Replace("\"incompatible_quirks\": [\"a\"]", "\"incompatible_quirks\": []")));
        StringAssert.Contains(asym.Message, "不对称");

        Console.WriteLine($"[M5] 三条互斥校验都拦得住：{dangling.Message.Split('：').Last()} / {self.Message.Split('：').Last()} / {asym.Message.Split('：').Last()} ✓");
    }

    /// <summary>
    /// 🔴 **M5u · 高级新兵带怪癖的掷签**（`StagecoachRecruits.RollQuirk` · M7③）——
    /// **一条用例守三条不变式**（不拆成三条，控用例数 ✓）：
    ///   ① 同种子 ⇒ 同结果（可复现；随机出口 = `RngProvider`）
    ///   ② **疾病不进池**（疾病走 `Roster.Infect` ／ 疗养院，不走招募）
    ///   ③ **与现持互斥的候选不进池**（判据 = `QuirksConfig.AreIncompatible`，单一落点）✓
    /// </summary>
    [TestMethod]
    public void RollQuirk_IsDeterministic_AndNeverPicksADiseaseOrAnIncompatibleOne()
    {
        QuirksConfig cfg = QuirksConfig.Parse(ReadData("quirks.json"));
        int pool = cfg.Quirks.Count(q => !q.IsDisease && q.RandomChance > 0);

        // ① 同种子 ⇒ 同结果；且 170 条里 141 条可进池 ⇒ **空手而归就说明池被抽干了**（口径坏了）✓
        string? first = StagecoachRecruits.RollQuirk(cfg, Array.Empty<string>(), new RngProvider(20261001));
        string? again = StagecoachRecruits.RollQuirk(cfg, Array.Empty<string>(), new RngProvider(20261001));
        Assert.AreEqual(first, again, "同种子必须同结果（否则掷签不可复现）✓");
        Assert.IsNotNull(first, $"候选池 = {pool} 条（> 0）⇒ 不该抽不出 ✓");

        // ② + ③ 现持 `tough`（与 `fragile` 双向互斥，已由上面那条用例断言）⇒ 反复掷，两条都不许出现 ✓
        var rng = new RngProvider(7);
        for (int i = 0; i < 100; i++)
        {
            string? id = StagecoachRecruits.RollQuirk(cfg, new[] { "tough" }, rng);
            Assert.IsNotNull(id, "池非空 ⇒ 每次都必须掷出结果 ✓");
            Assert.IsFalse(cfg.Get(id!).IsDisease, $"掷出了疾病 `{id}` —— 疾病不进招募池 ✓");
            Assert.IsFalse(cfg.AreIncompatible(id!, "tough"), $"掷出了与现持互斥的 `{id}` ✓");
        }

        Console.WriteLine($"[M5u] 候选池 = {pool} / {cfg.Quirks.Count} 条 · 100 次掷签无疾病、无互斥 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
