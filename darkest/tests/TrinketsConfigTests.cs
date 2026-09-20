using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M4 · Trinket 的结构与校验**（契约 `doc/modules/trinkets.md` · 策划 `#437`）：
///   **T1** 字段齐全 · **T2** `id` 唯一 / `rarity` ∈ 声明集合 / `price ≥ 0` / `buffs` ∈ 原语层 · **T5** `price ≤ 1` 不可购买 ✓
///
/// 🔴 本用例**只用内联样例**：因为参考件实测 **488 条 ≠ 契约 T1 的 196 条**（数字冲突已投策划）
///   ⇒ **裁定前不落 `darkest/data/trinkets.json`** ⇒ 校验逻辑先独立可测 ✓
/// </summary>
[TestClass]
public sealed class TrinketsConfigTests
{
    private const string Good = """
    {
      "_note": "内联样例（不是落库数据）",
      "rarities": ["common", "rare", "ancestral"],
      "trinkets": [
        { "id": "a", "buffs": ["B1"], "hero_class_requirements": [], "rarity": "common",
          "price": 150, "limit": 1, "origin_dungeon": "", "origin": "synthetic" },
        { "id": "b", "buffs": ["B2"], "hero_class_requirements": ["crusader"], "rarity": "ancestral",
          "price": 0, "limit": 1, "origin_dungeon": "crypts", "origin": "synthetic" }
      ]
    }
    """;

    [TestMethod]
    public void ParseAndValidate_AcceptsTheContractShape_AndT5HasASingleHome()
    {
        TrinketsConfig cfg = TrinketsConfig.Parse(Good, knownBuffIds: new HashSet<string> { "B1", "B2" });

        Assert.AreEqual(2, cfg.Trinkets.Count);
        Assert.IsTrue(cfg.BuffsCrossChecked, "给了原语层 ⇒ 交叉校验应真的跑了（自证 ✓）");
        Assert.AreEqual(3, cfg.Rarities.Count);

        // T5：price ≤ 1 ⇒ 不可购买（单一落点 ✓）
        Assert.IsTrue(TrinketsConfig.IsPurchasable(cfg.Get("a")), "price 150 ⇒ 可购买 ✓");
        Assert.IsFalse(TrinketsConfig.IsPurchasable(cfg.Get("b")), "price 0 ⇒ **不可购买** ✓");
        Assert.AreEqual(1, cfg.PurchasableCount, "可购买条数 = 1 ✓");

        Console.WriteLine($"[M4] T1/T2 通过 · T5：可购买 {cfg.PurchasableCount}/{cfg.Trinkets.Count} ✓");
    }

    [TestMethod]
    public void WithoutThePrimitiveLayer_TheCrossCheckIsSkipped_AndSaysSo()
    {
        TrinketsConfig cfg = TrinketsConfig.Parse(Good);   // 不给 knownBuffIds = 原语层未就位
        Assert.IsFalse(cfg.BuffsCrossChecked,
            "🔴 原语层未就位 ⇒ 交叉校验**必须自证为「没跑」**（不许静默当成通过 ✓）");
        Console.WriteLine("[M4] 原语层未就位 ⇒ BuffsCrossChecked=false（如实标注，不假装 ✓）");
    }

    [TestMethod]
    public void Validation_RejectsDuplicateId_UnknownRarity_NegativePrice_UnknownPrimitive()
    {
        // T2 ①：id 重复
        var dup = Assert.ThrowsException<InvalidDataException>(() => TrinketsConfig.Parse(Good.Replace("\"id\": \"b\"", "\"id\": \"a\"")));
        StringAssert.Contains(dup.Message, "重复");

        // T2 ③：rarity 不在声明集合
        var rar = Assert.ThrowsException<InvalidDataException>(() => TrinketsConfig.Parse(Good.Replace("\"rarity\": \"ancestral\"", "\"rarity\": \"mythic\"")));
        StringAssert.Contains(rar.Message, "rarity");

        // T2 ④：price 为负
        var neg = Assert.ThrowsException<InvalidDataException>(() => TrinketsConfig.Parse(Good.Replace("\"price\": 0", "\"price\": -5")));
        StringAssert.Contains(neg.Message, "price");

        // T2 ②：buffs 引用了原语层里不存在的 id
        var buf = Assert.ThrowsException<InvalidDataException>(
            () => TrinketsConfig.Parse(Good, knownBuffIds: new HashSet<string> { "B1" }));
        StringAssert.Contains(buf.Message, "原语");

        Console.WriteLine($"[M4] 四条校验都拦得住：{dup.Message.Split('：').Last()} / {rar.Message.Split('：').Last()} / {neg.Message.Split('：').Last()} / {buf.Message.Split('：').Last()} ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
