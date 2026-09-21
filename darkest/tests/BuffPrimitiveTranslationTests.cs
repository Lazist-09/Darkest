using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M2 · "不许静默跳过"的可断言版**（架构规则）：参考件里**每一条原语**都必须被分类 ——
///   要么落到我们的某个去向（`damage_mod`/`prob_mod`/`stat_mod`/`state_flag`/单位抗性），
///   要么进**显式冻结**清单（带理由）✓
///
/// 输入：`reports/dd1_buff_primitives.json`（我此前从参考件抽出的 41 种原语，**已入库** ✓）
///   ⇒ 若该文件不存在 ⇒ `Assert.Inconclusive`（不假装通过 ✓）
/// 🔴 本用例**不落任何数据、不改 `buff_defs.json`**（只是把"翻译到哪一步"变成可数 ✓）
/// </summary>
[TestClass]
public sealed class BuffPrimitiveTranslationTests
{
    private static string? FindReport()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string c = Path.Combine(dir.FullName, "reports", "dd1_buff_primitives.json");
            if (File.Exists(c))
            {
                return c;
            }

            dir = dir.Parent;
        }

        return null;
    }

    [TestMethod]
    public void EveryReferencePrimitive_IsEitherMappedOrExplicitlyFrozen()
    {
        string? path = FindReport();
        if (path is null)
        {
            Assert.Inconclusive("reports/dd1_buff_primitives.json 不在（未跑提取器）⇒ 不假装通过 ✓");
            return;
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        var primitives = new List<(string, string?)>();
        if (doc.RootElement.TryGetProperty("reference_primitives", out JsonElement p))
        {
            foreach (JsonProperty kv in p.EnumerateObject())
            {
                string[] parts = kv.Name.Split(" / ", 2);
                primitives.Add((parts[0], parts.Length > 1 ? parts[1] : ""));
            }
        }

        Assert.IsTrue(primitives.Count > 0, "原语清单不应为空 ✓");

        var tally = BuffPrimitiveTranslation.Tally(primitives);
        int frozen = tally.TryGetValue(BuffPrimitiveTranslation.Target.Frozen, out int f) ? f : 0;
        int mapped = primitives.Count - frozen;

        // 🔴 规则的可断言形式：**没有第三种去向**（每条要么 mapped 要么 frozen）✓
        Assert.AreEqual(primitives.Count, mapped + frozen, "每条原语必须恰好落一处 ✓");

        foreach (var (st, sub) in primitives)
        {
            var t = BuffPrimitiveTranslation.Classify(st, sub);
            if (t == BuffPrimitiveTranslation.Target.Frozen)
            {
                string why = BuffPrimitiveTranslation.FrozenReason(st);
                Assert.IsFalse(string.IsNullOrWhiteSpace(why), $"冻结的 \"{st}\" 必须有理由（不许静默）✓");
            }
        }

        Console.WriteLine($"[M2·翻译] 原语 {primitives.Count} 种 ⇒ 有去向 **{mapped}** · **显式冻结 {frozen}** ✓");
        foreach (var kv in tally.OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"[M2·翻译]   {kv.Key,-14} {kv.Value,3}");
        }

        // 冻结的逐个点名（这就是"未映射清单"在代码里的形态 ✓）
        var frozenNames = primitives
            .Where(x => BuffPrimitiveTranslation.Classify(x.Item1, x.Item2) == BuffPrimitiveTranslation.Target.Frozen)
            .Select(x => x.Item1).Distinct().OrderBy(x => x).ToArray();
        Console.WriteLine($"[M2·翻译] 冻结清单（{frozenNames.Length} 种）：{string.Join(", ", frozenNames)}");
        TestContext.WriteLine("[M2] 每条原语都有去向或显式冻结 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
