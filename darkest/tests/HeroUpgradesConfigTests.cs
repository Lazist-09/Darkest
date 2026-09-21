using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M8（职业）· 升级树解析器/校验器**的用例（**不落数据** ✓）。
///
/// 三件事：
///   ① **能吃进真实形状**：直接读我量出的一手产物 `reports/dd1_hero_upgrades_source.json`
///      ⇒ 总数必须 = **15 职业 / 135 树 / 645 等级**（与卡里的"135 树 · 645 等级"对上 ✓）
///   ② **三条 P 检查都拦得住**：悬空先决 / 自环 / `level_codes` 与实际不一致 ✓
///   ③ **外部树目录**（建筑树如 `blacksmith.weapon`）必须被接受 ⇒ 否则真实数据会被误报悬空 ✓
/// </summary>
[TestClass]
public sealed class HeroUpgradesConfigTests
{
    private static string? Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string c = Path.Combine(dir.FullName, relative);
            if (File.Exists(c))
            {
                return c;
            }

            dir = dir.Parent;
        }

        return null;
    }

    [TestMethod]
    public void ParsesTheMeasuredShape_AndCountsMatchTheCard()
    {
        string? path = Find(Path.Combine("reports", "dd1_hero_upgrades_source.json"));
        if (path is null)
        {
            Assert.Inconclusive("reports/dd1_hero_upgrades_source.json 不在（未跑提取器）⇒ 不假装通过 ✓");
            return;
        }

        HeroUpgradesConfig cfg = HeroUpgradesConfig.Parse(File.ReadAllText(path));

        // 外部树目录：建筑树（一手数据里的先决会指向 blacksmith.weapon 等 ✓）
        string? bPath = Find(Path.Combine("data", "buildings.json"));
        var external = new List<string>();
        if (bPath is not null)
        {
            foreach (BuildingConfig b in BuildingsConfig.Parse(File.ReadAllText(bPath)).Buildings)
            {
                foreach (BuildingTreeConfig t in b.Trees)
                {
                    external.Add(t.Id);
                }
            }
        }

        cfg.Validate(external);   // 🔴 不抛 = 真实数据**全部可解**（不悬空、不重复、不自环 ✓）

        Assert.AreEqual(15, cfg.Heroes.Count, "15 个职业 ✓");
        Assert.AreEqual(135, cfg.TreeCount, "135 条升级树（与卡一致 ✓）");
        Assert.AreEqual(645, cfg.LevelCount, "645 个等级（与卡一致 ✓）");

        Console.WriteLine($"[M8] 真实形状吃进来了：{cfg.Heroes.Count} 职业 / {cfg.TreeCount} 树 / {cfg.LevelCount} 等级 ✓");
        Console.WriteLine($"[M8] 外部树目录（建筑）= {external.Count} 条 ⇒ 无悬空 ✓");
        TestContext.WriteLine("[M8] 解析器可吃真实数据 ✓");
    }

    private const string Good = """
    { "heroes": { "abomination": { "trees": [
        { "id": "abomination.weapon", "is_instanced": true, "tags": ["weapon"],
          "level_codes": ["0","1"],
          "levels": [
            { "code": "0", "currency_cost": [{ "type": "gold", "amount": 750 }],
              "prerequisites": [{ "tree_id": "blacksmith.weapon", "requirement_code": "a" }],
              "prerequisite_resolve_level": 1 },
            { "code": "1", "currency_cost": [{ "type": "gold", "amount": 1750 }],
              "prerequisites": [{ "tree_id": "abomination.weapon", "requirement_code": "0" }] }
          ] } ] } } }
    """;

    [TestMethod]
    public void AcceptsExternalTreeDirectory()
    {
        HeroUpgradesConfig cfg = HeroUpgradesConfig.Parse(Good);
        cfg.Validate(new[] { "blacksmith.weapon" });   // 外部树被接受 ⇒ 不报悬空 ✓
        Assert.AreEqual(1, cfg.Heroes.Count);
        Assert.AreEqual(2, cfg.LevelCount);
        Console.WriteLine("[M8] 外部树目录被正确接受 ✓");
    }

    [TestMethod]
    public void RejectsDanglingPrerequisite()
    {
        HeroUpgradesConfig cfg = HeroUpgradesConfig.Parse(Good);
        var ex = Assert.ThrowsException<InvalidDataException>(() => cfg.Validate());   // 不传外部目录
        StringAssert.Contains(ex.Message, "悬空", "不传外部目录 ⇒ blacksmith.weapon 必须被判悬空 ✓");
        Console.WriteLine($"[M8] 悬空被拦住：{ex.Message.Split('：').Last().Trim().Substring(0, Math.Min(60, ex.Message.Split('：').Last().Trim().Length))} ✓");
    }

    [TestMethod]
    public void RejectsSelfLoopAndCodeMismatch()
    {
        string selfLoop = Good.Replace(
            "{ \"tree_id\": \"abomination.weapon\", \"requirement_code\": \"0\" }",
            "{ \"tree_id\": \"abomination.weapon\", \"requirement_code\": \"1\" }");
        var ex1 = Assert.ThrowsException<InvalidDataException>(
            () => HeroUpgradesConfig.Parse(selfLoop).Validate(new[] { "blacksmith.weapon" }));
        StringAssert.Contains(ex1.Message, "自环", "等级 1 说\"要先有等级 1\" ⇒ 必须报自环 ✓");

        string mismatch = Good.Replace("\"level_codes\": [\"0\",\"1\"]", "\"level_codes\": [\"0\",\"1\",\"2\"]");
        var ex2 = Assert.ThrowsException<InvalidDataException>(
            () => HeroUpgradesConfig.Parse(mismatch).Validate(new[] { "blacksmith.weapon" }));
        StringAssert.Contains(ex2.Message, "不一致", "level_codes 与实际等级不一致 ⇒ 必须报 ✓");

        Console.WriteLine("[M8] 自环 + level_codes 不一致 都被拦住 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
