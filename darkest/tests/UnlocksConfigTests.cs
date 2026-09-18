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
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        UnlocksConfig cfg = UnlocksConfig.Parse(ReadData("unlocks.json"),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curios.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal), 12);

        // 🔴 `#316`③ 的真清单（4 条：第 1／3／6／10 趟）—— 不再是占位
        Assert.AreEqual(4, cfg.Unlocks.Count, "四条阈值（1／3／6／10 趟）");
        CollectionAssert.AreEquivalent(new[] { 1, 3, 6, 10 },
            cfg.Unlocks.Select(e => e.RequiredRunsFinished).ToArray(), "阈值 = 1／3／6／10");
        Assert.AreEqual(8, cfg.RosterBaseCap, "起手名册可用上限 8（硬上限 12 见 C1）");

        // 命名空间三种都必须出现（覆盖 C2 的三个消费点）
        var targets = cfg.Unlocks.SelectMany(e => e.Unlocks).ToArray();
        Assert.IsTrue(targets.Any(t => t.StartsWith("building:", StringComparison.Ordinal)), "有 building: 项");
        Assert.IsTrue(targets.Any(t => t.StartsWith("curio:", StringComparison.Ordinal)), "有 curio: 项");
        // 🔴 策划 `#403`：名册上限改成**增量语义**（`roster_cap_delta:N`，与马车同语法 ✓）
        Assert.IsTrue(targets.Any(t => t.StartsWith("roster_cap_delta:", StringComparison.Ordinal)),
            "有 roster_cap_delta: 项（增量语义 · `#403` ✓）");

        foreach (UnlockEntry e in cfg.Unlocks)
        {
            Assert.IsTrue(e.RequiredRunsFinished > 0 || e.RequiredBattlesWon > 0, "至少给一个阈值");
            Assert.IsTrue(e.Unlocks.Count > 0, "unlocks 非空");
        }

        var all = cfg.Unlocks.SelectMany(e => e.Unlocks).ToArray();
        Assert.AreEqual(all.Length, all.Distinct(StringComparer.Ordinal).Count(), "不得有两个条目解锁同一 id");
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
        // 形态阶段允许"空表"（内容还没定），但数组本身必须存在；`roster_base_cap` 是必需键（数字外置 P29）✓
        UnlocksConfig cfg = UnlocksConfig.Parse(
            """{ "config": { "version": 1, "roster_base_cap": 8 }, "unlocks": [] }""");
        Assert.AreEqual(0, cfg.Unlocks.Count);
    }
}
