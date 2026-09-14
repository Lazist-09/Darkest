using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **合并包片 D 验收**（`tasks/merged_content_layer_pack.md` §3 片 D / `O-86`）：
/// `data/unlocks.json` = 解锁阈值表（形态参照真机 `generated_dungeons[{id, required_number_of_quests_finished}]`）
/// + **P27 校验**：① 阈值 `≥ 0` 且**至少给一个**、`unlocks` 非空；② 🔴 **不得两个条目解锁同一 id**（语义二义）。
/// ⚠️ **当前只有形态、没有消费点**（"解锁什么"属内容 ⇒ `O-86` 待策划）—— 用例只锁【形态 + 校验】，不假装有行为 ✓
/// </summary>
[TestClass]
public sealed class UnlocksConfigTests
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
    public void ShippedUnlocks_LoadAndPassP27()
    {
        UnlocksConfig cfg = UnlocksConfig.Parse(ReadData("unlocks.json"));

        Assert.IsTrue(cfg.Unlocks.Count >= 1, "至少一条占位示例（形态要求）");
        foreach (UnlockEntry e in cfg.Unlocks)
        {
            Assert.IsTrue(e.RequiredRunsFinished > 0 || e.RequiredBattlesWon > 0, "至少给一个阈值");
            Assert.IsTrue(e.Unlocks.Count > 0, "unlocks 非空");
        }

        // 🔴 同一条 id 只能被一个条目解锁（P27 ②）
        var all = cfg.Unlocks.SelectMany(e => e.Unlocks).ToArray();
        Assert.AreEqual(all.Length, all.Distinct(StringComparer.Ordinal).Count(),
            "不得有两个条目解锁同一 id");

        // 占位示例必须**显式标为占位**（避免将来被误当成真实内容）
        Assert.IsTrue(cfg.Unlocks.Any(e => e.Unlocks.Contains("__placeholder__")),
            "出厂数据应只有**占位**（内容清单待策划 O-86）");
    }

    [TestMethod]
    public void P27_RejectsDuplicateUnlockTarget()
    {
        const string bad = """
        { "config": { "version": 1 },
          "unlocks": [
            { "id": "a", "required_runs_finished": 1, "required_battles_won": 0, "unlocks": ["same_id"] },
            { "id": "b", "required_runs_finished": 2, "required_battles_won": 0, "unlocks": ["same_id"] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(() => UnlocksConfig.Parse(bad),
            "两个条目解锁同一 id ⇒ 启动即报错（P27 ②：解锁语义二义）");
    }

    [TestMethod]
    public void P27_RejectsNegativeThreshold_NoThreshold_AndEmptyUnlocks()
    {
        const string negative = """
        { "config": { "version": 1 },
          "unlocks": [ { "id": "a", "required_runs_finished": -1, "required_battles_won": 0, "unlocks": ["x"] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(() => UnlocksConfig.Parse(negative), "阈值不得为负（P27 ①）");

        const string noThreshold = """
        { "config": { "version": 1 },
          "unlocks": [ { "id": "a", "required_runs_finished": 0, "required_battles_won": 0, "unlocks": ["x"] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(() => UnlocksConfig.Parse(noThreshold),
            "一个阈值都不给 ⇒ 报错（否则等价于「立即解锁」，语义空洞）");

        const string emptyUnlocks = """
        { "config": { "version": 1 },
          "unlocks": [ { "id": "a", "required_runs_finished": 1, "required_battles_won": 0, "unlocks": [] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(() => UnlocksConfig.Parse(emptyUnlocks),
            "unlocks 为空 ⇒ 报错（无事可解锁）");
    }

    [TestMethod]
    public void EmptyTable_IsAllowed_ForFormOnlyStage()
    {
        // 形态阶段允许"空表"（内容还没定），但数组本身必须存在
        UnlocksConfig cfg = UnlocksConfig.Parse("""{ "config": { "version": 1 }, "unlocks": [] }""");
        Assert.AreEqual(0, cfg.Unlocks.Count);
    }
}
