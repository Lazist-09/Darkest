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
        Assert.AreEqual(2, cfg.Unlocks.Count, "两条阈值（1／3 趟）—— 🔴 M7②/#423：6/10 趟那两条只做「上限增量」，已收敛到马车曲线 ✓");
        CollectionAssert.AreEquivalent(new[] { 1, 3 },
            cfg.Unlocks.Select(e => e.RequiredRunsFinished).ToArray(), "阈值 = 1／3／6／10");
        Assert.AreEqual(8, cfg.RosterBaseCap, "起手名册可用上限 8（硬上限 12 见 C1）");

        // 命名空间三种都必须出现（覆盖 C2 的三个消费点）
        var targets = cfg.Unlocks.SelectMany(e => e.Unlocks).ToArray();
        Assert.IsTrue(targets.Any(t => t.StartsWith("building:", StringComparison.Ordinal)), "有 building: 项");
        Assert.IsTrue(targets.Any(t => t.StartsWith("curio:", StringComparison.Ordinal)), "有 curio: 项");
        // 🔴 策划 `#403`：名册上限改成**增量语义**（`roster_cap_delta:N`，与马车同语法 ✓）
        Assert.IsFalse(targets.Any(t => t.StartsWith("roster_cap_delta:", StringComparison.Ordinal)),   // 🔴 M7②：已撤（单一来源=马车曲线）
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

    /// <summary>
    /// 🔴 **命名空间清单 ⇔ 实际分支**（防 `152_*.md` 那处漂移**复发**）：
    ///   ① `NamespacePrefixes` 里每一支都**真的被 Parse 接受**（否则清单是空的）✓
    ///   ③ 🔴 **反方向**：不在清单里的前缀必须被拒，且**报错文案要列出全部 4 支**
    ///      （文案由 `NamespacePrefixes` 插值 ⇒ 加一支即自动同步）✓
    /// </summary>
    [TestMethod]
    public void NamespacePrefixes_CoverEveryBranch()
    {
        string[] prefixes = UnlocksConfig.NamespacePrefixes.ToArray();
        Assert.AreEqual(4, prefixes.Length,
            "KNOWN: 4 支（building / curio / roster_cap_delta / roster_cap）✓");

        // ① 每一支都被接受（用最小合法负载 —— 值本身由各支自行校验）
        foreach (string p in prefixes)
        {
            string payload = p switch
            {
                "building:" => "building:tavern",
                "curio:" => "curio:cur_sconce",
                "roster_cap_delta:" => "roster_cap_delta:1",
                _ => "roster_cap:9",
            };
            string json = "{ \"config\": { \"version\": 1, \"roster_base_cap\": 8 }, "
                + "\"unlocks\": [ { \"id\": \"u\", \"required_runs_finished\": 1, "
                + "\"required_battles_won\": 0, \"unlocks\": [\"" + payload + "\"] } ] }";
            UnlocksConfig.Parse(json,
                new[] { "tavern", "abbey", "stagecoach" }.ToHashSet(StringComparer.Ordinal),
                new[] { "cur_sconce" }.ToHashSet(StringComparer.Ordinal), 12);
        }

        // ③ 反方向：清单外的前缀 ⇒ 报错，且报错里必须**列出全部 4 支**（插值而来）
        string bad = "{ \"config\": { \"version\": 1, \"roster_base_cap\": 8 }, "
            + "\"unlocks\": [ { \"id\": \"u\", \"required_runs_finished\": 1, "
            + "\"required_battles_won\": 0, \"unlocks\": [\"bogus:x\"] } ] }";
        var ex = Assert.ThrowsException<InvalidDataException>(() => UnlocksConfig.Parse(bad));
        foreach (string p in prefixes)
        {
            StringAssert.Contains(ex.Message, p,
                $"报错文案必须列出 `{p}`（清单由 NamespacePrefixes 插值）✓");
        }
    }
}
