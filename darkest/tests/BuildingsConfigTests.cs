using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M6 · 建筑结构对齐**（策划 `#421` + 派发卡的 `code`/`prerequisites` 两条 P 校验）。
/// 一手 E 盘实测：**8 建筑 · 20 树 · 99 等级** · `code` 重复 0 · 悬空前置 0 · **无环** · 资源 5 种 ✓
/// </summary>
[TestClass]
public sealed class BuildingsConfigTests
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

    [TestMethod]
    public void LandedBuildings_MatchTheMeasuredNumbers()
    {
        BuildingsConfig cfg = BuildingsConfig.Parse(ReadData("buildings.json"));

        Assert.AreEqual(8, cfg.Buildings.Count, "建筑 = 8 ✓");
        Assert.AreEqual(20, cfg.TreeCount, "升级树 = 20 ✓");
        Assert.AreEqual(99, cfg.LevelCount, "等级条目 = 99 ✓");

        // 一手实测的 8 个建筑名（不多不少）
        var expected = new[] { "abbey", "blacksmith", "camping_trainer", "guild", "nomad_wagon", "sanitarium", "stage_coach", "tavern" };
        CollectionAssert.AreEquivalent(expected, cfg.Buildings.Select(b => b.Id).ToArray(), "8 个建筑名应与一手一致 ✓");

        // 花费类型都在实测集合内（不发明新资源）
        var used = cfg.Buildings.SelectMany(b => b.Trees).SelectMany(t => t.Levels)
            .SelectMany(l => l.CurrencyCost).Select(c => c.Type).Distinct().OrderBy(x => x).ToArray();
        Assert.IsTrue(used.All(t => BuildingsConfig.KnownCurrencyTypes.Contains(t)));
        Assert.AreEqual(5, used.Length, "实测用到 5 种资源 ✓");

        // 一处可核的样例：abbey 的第一棵树第一档（一手：bust 4 / crest 5）
        BuildingConfig abbey = cfg.Get("abbey");
        BuildingLevelConfig first = abbey.Trees[0].Levels[0];
        Assert.AreEqual("a", first.Code, "第一档 code = a ✓");
        Assert.AreEqual(4, first.CurrencyCost.First(c => c.Type == "bust").Amount, "abbey 首档 bust = 4 ✓");
        Assert.AreEqual(5, first.CurrencyCost.First(c => c.Type == "crest").Amount, "abbey 首档 crest = 5 ✓");
        Assert.AreEqual(0, first.Prerequisites.Count, "首档无前置 ✓");
        Assert.AreEqual(1, abbey.Trees[0].Levels[1].Prerequisites.Count, "第二档有 1 条前置（指回 a）✓");

        Console.WriteLine($"[M6] 落库验收：{cfg.Buildings.Count} 建筑 · {cfg.TreeCount} 树 · {cfg.LevelCount} 等级 · 资源 {used.Length} 种 ✓");
        TestContext.WriteLine("[M6] 8/20/99 + 样例字段通过 ✓");
    }

    [TestMethod]
    public void PChesks_RejectDuplicateCode_DanglingPrerequisite_AndCycle()
    {
        const string ok = """
        { "buildings": [ { "id": "b1", "trees": [ { "id": "t1", "is_instanced": false, "tags": [],
          "levels": [
            { "code": "a", "currency_cost": [ { "type": "gold", "amount": 0 } ], "prerequisites": [], "origin": "synthetic" },
            { "code": "b", "currency_cost": [ { "type": "gold", "amount": 1 } ],
              "prerequisites": [ { "tree_id": "t1", "requirement_code": "a" } ], "origin": "synthetic" } ] } ] } ] }
        """;
        BuildingsConfig cfg = BuildingsConfig.Parse(ok);
        Assert.AreEqual(2, cfg.LevelCount);

        // P① code 树内重复
        var dup = Assert.ThrowsException<InvalidDataException>(
            () => BuildingsConfig.Parse(ok.Replace("\"code\": \"b\"", "\"code\": \"a\"")));
        StringAssert.Contains(dup.Message, "重复");

        // P② 悬空前置
        var dangling = Assert.ThrowsException<InvalidDataException>(
            () => BuildingsConfig.Parse(ok.Replace("\"requirement_code\": \"a\"", "\"requirement_code\": \"zzz\"")));
        StringAssert.Contains(dangling.Message, "不存在");

        // P② 成环（b 依赖 a，a 反过来依赖 b）
        const string cyclic = """
        { "buildings": [ { "id": "b1", "trees": [ { "id": "t1", "is_instanced": false, "tags": [],
          "levels": [
            { "code": "a", "currency_cost": [ { "type": "gold", "amount": 0 } ],
              "prerequisites": [ { "tree_id": "t1", "requirement_code": "b" } ], "origin": "synthetic" },
            { "code": "b", "currency_cost": [ { "type": "gold", "amount": 1 } ],
              "prerequisites": [ { "tree_id": "t1", "requirement_code": "a" } ], "origin": "synthetic" } ] } ] } ] }
        """;
        var cycle = Assert.ThrowsException<InvalidDataException>(() => BuildingsConfig.Parse(cyclic));
        StringAssert.Contains(cycle.Message, "成环");

        // P③ 未知资源类型
        var money = Assert.ThrowsException<InvalidDataException>(
            () => BuildingsConfig.Parse(ok.Replace("\"type\": \"gold\", \"amount\": 1", "\"type\": \"gems\", \"amount\": 1")));
        StringAssert.Contains(money.Message, "花费类型");

        Console.WriteLine($"[M6] 三条 P 校验都拦得住：{dup.Message.Split('：').Last()} / {dangling.Message.Split('：').Last()} / {cycle.Message.Split('：').Last()} / {money.Message.Split('：').Last()} ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
