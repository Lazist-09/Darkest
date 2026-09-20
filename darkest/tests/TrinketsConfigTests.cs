using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M4 · Trinket 的结构与校验**（契约 `doc/modules/trinkets.md` · 策划 `#437` / `#452`）：
///   **T1** 196 条 · 字段齐全　**T2** `id` 唯一 / `rarity` ∈ rarity 表 / `price ≥ 0` / `buffs` ∈ 原语层
///   **T5** 🔴 **不可购买 = `award_category != "universal"`（26 条）** —— ⚠️ **不是** `price ≤ 1`（15 条）
///
/// 数据来源：E 盘**一手**（`base.entries.trinkets.json` · 排除 `rarity == kickstarter`）⇒ 落 `darkest/data/trinkets.json` ✓
/// 本用例对**真实落库数据**断言 ⇒ 数字不符即红（**这就是 M4 的验收**）✓
/// </summary>
[TestClass]
public sealed class TrinketsConfigTests
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

    /// <summary>🔴 **T1 + T2 + T5 对着真实落库数据一次性验收**（数字全来自策划 `#452` 的定义）✓</summary>
    [TestMethod]
    public void LandedTable_MatchesTheRuledNumbers_AndT5UsesAwardCategoryNotPrice()
    {
        TrinketsConfig cfg = TrinketsConfig.Parse(ReadData("trinkets.json"));

        Assert.AreEqual(196, cfg.Trinkets.Count, "T1：应为 196 条（一手 E 盘 + 排除 kickstarter）✓");
        Assert.AreEqual(14, cfg.Rarities.Count, "rarity 表应为 14 条（一手）✓");
        Assert.AreEqual(13, cfg.Trinkets.Select(t => t.Rarity).Distinct().Count(),
            "条目实际用到 13 种 rarity（kickstarter 已被排除）✓");

        // T5：🔴 判据是 award_category（非 universal = 26 条不可购买）；price ≤ 1 是 15 条（不是判据）
        Assert.AreEqual(26, cfg.NonPurchasableCount, "T5：非 universal 应为 26 条（不可购买）✓");
        Assert.AreEqual(170, cfg.PurchasableCount, "可购买 = 196 − 26 = 170 ✓");
        int priceLe1 = cfg.Trinkets.Count(t => t.Price <= 1);
        Assert.AreEqual(15, priceLe1, "price ≤ 1 是 15 条（策划实测）—— 若拿它当购买判据就会漏 11 条 battle ✓");

        Assert.AreEqual(196, cfg.Trinkets.Select(t => t.Id).Distinct().Count(), "T2：id 唯一 ✓");
        Assert.IsNotNull(cfg.Get("crow_wingfeather"), "参考件漏掉的 crow 系条目必须在（一手 E 盘有）✓");

        Console.WriteLine($"[M4] 落库验收：196 条 · 13/14 rarity · 可购买 {cfg.PurchasableCount} / 不可购买 {cfg.NonPurchasableCount}"
            + $" · price≤1 {priceLe1}（**不是判据**）✓");
        TestContext.WriteLine("[M4] T1/T2/T5 对着真实数据通过 ✓");
    }

    [TestMethod]
    public void WithoutThePrimitiveLayer_TheCrossCheckIsSkipped_AndSaysSo()
    {
        TrinketsConfig cfg = TrinketsConfig.Parse(ReadData("trinkets.json"));
        Assert.IsFalse(cfg.BuffsCrossChecked,
            "🔴 原语层（M2）未就位 ⇒ 交叉校验**必须自证为没跑**（不许静默当通过 ✓）");
        Console.WriteLine("[M4] 原语层未就位 ⇒ BuffsCrossChecked=false（如实标注 ✓）");
    }

    [TestMethod]
    public void Validation_RejectsDuplicateId_UnknownRarity_NegativePrice_UnknownPrimitive()
    {
        const string sample = """
        {
          "rarities": [ { "id": "common", "award_category": "universal" }, { "id": "rare", "award_category": "battle" } ],
          "trinkets": [
            { "id": "a", "buffs": ["B1"], "hero_class_requirements": [], "rarity": "common",
              "price": 150, "limit": 1, "origin_dungeon": "", "origin": "synthetic" },
            { "id": "b", "buffs": ["B2"], "hero_class_requirements": [], "rarity": "rare",
              "price": 0, "limit": 1, "origin_dungeon": "", "origin": "synthetic" }
          ]
        }
        """;

        TrinketsConfig cfg = TrinketsConfig.Parse(sample, knownBuffIds: new HashSet<string> { "B1", "B2" });
        Assert.IsTrue(cfg.IsPurchasable(cfg.Get("a")), "award_category=universal ⇒ 可购买 ✓");
        Assert.IsFalse(cfg.IsPurchasable(cfg.Get("b")), "award_category=battle ⇒ 不可购买（且与 price 无关）✓");

        var dup = Assert.ThrowsException<InvalidDataException>(() => TrinketsConfig.Parse(sample.Replace("\"id\": \"b\"", "\"id\": \"a\"")));
        StringAssert.Contains(dup.Message, "重复");

        var rar = Assert.ThrowsException<InvalidDataException>(() => TrinketsConfig.Parse(sample.Replace("\"rarity\": \"rare\"", "\"rarity\": \"mythic\"")));
        StringAssert.Contains(rar.Message, "rarity");

        var neg = Assert.ThrowsException<InvalidDataException>(() => TrinketsConfig.Parse(sample.Replace("\"price\": 0", "\"price\": -5")));
        StringAssert.Contains(neg.Message, "price");

        var buf = Assert.ThrowsException<InvalidDataException>(() => TrinketsConfig.Parse(sample, knownBuffIds: new HashSet<string> { "B1" }));
        StringAssert.Contains(buf.Message, "原语");

        Console.WriteLine("[M4] 四条校验都拦得住 + T5 判据是 award_category（不是 price）✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
