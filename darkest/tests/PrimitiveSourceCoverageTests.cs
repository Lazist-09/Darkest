using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **A1 · 两个来源的覆盖差集（实测）**：**本地参考项目** vs **一手 E 盘**。
///
/// WHY 需要这个用例：用户指令（2026-09-25）规定"**数值采用本地参考项目的来源**" ⇒
///   采用参考项目**就必须知道它比一手少了什么**（少了却不说 = 静默丢功能，纪律不许 ✓）。
///   本用例把差集**钉死成集合**：上游任一侧变了 ⇒ 红 ⇒ 必须有人重新判读 ✓
///
/// 两侧输入：
///   · 参考项目 = `darkest/data/buff_primitives.json`（1801 条 / 25 个 `stat_type` / 23 个 `rule_type`）✓
///   · 一手 E 盘 = `reports/dd1_buff_primitives_primary.json`（2020 条 / 27 / 27 —— R4 提取器产物）✓
///
/// 🔴 **判据源已换**：本用例的前身（`BuffPrimitiveTranslationTests`）把**一手**当判据源、
///   且旧注释写着"一手对账发现漏 8 多 1" ⇒ 那是**旧优先级**下的结论；现在判据源是参考项目，
///   一手退化为**覆盖性交叉校验**（两份都只是"谁更全"，不再是"谁对"）✓
/// </summary>
[TestClass]
public sealed class PrimitiveSourceCoverageTests
{
    private static string? FindRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string c = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(c))
            {
                return c;
            }

            dir = dir.Parent;
        }

        return null;
    }

    [TestMethod]
    public void ReferenceProject_MissesThreeStatTypesAndFourRuleTypes_ThatThePrimaryHas()
    {
        string? primaryPath = FindRepoFile("reports", "dd1_buff_primitives_primary.json");
        if (primaryPath is null)
        {
            Assert.Inconclusive("reports/dd1_buff_primitives_primary.json 不在（未跑 R4 提取器）⇒ 不假装通过 ✓");
            return;
        }

        BuffPrimitivesConfig reference = BuffPrimitivesConfig.Parse(
            File.ReadAllText(FindRepoFile("data", "buff_primitives.json")!));

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(primaryPath));
        var primaryStat = Keys(doc.RootElement, "reference_stat_types");
        var primaryRule = Keys(doc.RootElement, "reference_rule_types");
        var referenceRule = reference.Primitives.Select(p => p.RuleType).ToHashSet(StringComparer.Ordinal);

        string[] statOnlyPrimary = primaryStat.Except(reference.StatTypes, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] statOnlyReference = reference.StatTypes.Except(primaryStat, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] ruleOnlyPrimary = primaryRule.Except(referenceRule, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] ruleOnlyReference = referenceRule.Except(primaryRule, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(
            new[] { "activity_side_effect_chance", "crit_received_chance", "ignore_stealth" }, statOnlyPrimary,
            "参考项目少了这 3 个 stat_type（一手有）⇒ 采用参考项目 = 这 3 条原语**没有定义**，须策划裁 ✓");
        CollectionAssert.AreEqual(
            new[] { "hp_heal_amount" }, statOnlyReference,
            "参考项目多的这 1 个 stat_type（一手没有）⇒ 是参考项目的补充 ✓");
        CollectionAssert.AreEqual(
            new[] { "attacking_monster_type", "is_actor_status", "is_guarded", "monster_type_count_min" },
            ruleOnlyPrimary,
            "参考项目少了这 4 个 rule_type（一手有）⇒ 依赖它们的 buff 在参考项目里换了写法 ✓");
        Assert.AreEqual(0, ruleOnlyReference.Length,
            "参考项目没有「一手不存在」的 rule_type ✓（实测 0）");

        Console.WriteLine($"[A1·覆盖] stat_type 一手 {primaryStat.Count} / 参考 {reference.StatTypes.Count}"
            + $" ⇒ 一手独有 {statOnlyPrimary.Length}（{string.Join(", ", statOnlyPrimary)}）· "
            + $"参考独有 {statOnlyReference.Length}（{string.Join(", ", statOnlyReference)}）✓");
        Console.WriteLine($"[A1·覆盖] rule_type 一手 {primaryRule.Count} / 参考 {referenceRule.Count}"
            + $" ⇒ 一手独有 {ruleOnlyPrimary.Length}（{string.Join(", ", ruleOnlyPrimary)}）· "
            + $"参考独有 {ruleOnlyReference.Length} ✓");
        Console.WriteLine("[A1·覆盖] 结论：采用参考项目会丢 3 个 stat_type + 4 个 rule_type 的表达力"
            + "（那 3 个原语的 buff 在参考项目里由 JsonBuffs 的其他写法承担，或干脆没有）⇒ 已登记待策划裁 ✓");
    }

    private static HashSet<string> Keys(JsonElement root, string property)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty(property, out JsonElement e))
        {
            foreach (JsonProperty kv in e.EnumerateObject())
            {
                set.Add(kv.Name);
            }
        }

        return set;
    }
}
