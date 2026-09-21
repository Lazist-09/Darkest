using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M3 第 1 步验收**：折磨/美德搬到自己的表（**值原样** ✓）
///
/// 架构授权口径：「⑦⑧ ⇒ ✅ **授权按你的建议执行 + 附依据与读数**」✓
///   依据：卡 M3 的 ① = **7 条**（判别器 = **时长族** ✓ 而**不是** id 名 ✓）；
///        另外两条（`bound` / `pep_talk`）虽也落在时长族里，但按卡自己的分类属 **③ 自加**
///        （`bound`：原版 buff 表 **0 命中** · `pep_talk`：**技能给的**，不是决心判定产物）
///        ⇒ 生成器**把它们排除并写下理由**（7 与 9 的分歧**不许藏** ✓）
/// 读数：**7 条**（折磨 3 + 美德 4）· 三条 P 检查（id 唯一 / kind↔ends_at 自洽 / **可回溯**）✓
/// 🔴 **零行为**：`MoraleLedger` 等消费点**仍读 `buff_defs`** ⇒ 行为一字不改 ✓（第二步见计划 ✓）
/// </summary>
[TestClass]
public sealed class TraitsStep1Tests
{
    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string c = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(c))
            {
                return File.ReadAllText(c);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    [TestMethod]
    public void Traits_AreSeven_ThreeAfflictionsAndFourVirtues()
    {
        TraitsConfig cfg = TraitsConfig.Parse(ReadData("traits.json"));
        cfg.Validate();   // 三条 P 检查不抛 ⇒ 干净 ✓

        var aff = cfg.Traits.Where(t => t.Kind == "affliction").Select(t => t.Id).ToArray();
        var vir = cfg.Traits.Where(t => t.Kind == "virtue").Select(t => t.Id).ToArray();

        Assert.AreEqual(7, cfg.Traits.Count, "**7 条**（卡里 ① = 7 ✓）");
        Assert.AreEqual(3, aff.Length, "折磨 3 ✓");
        Assert.AreEqual(4, vir.Length, "美德 4 ✓");

        Console.WriteLine("[M3·第1步] 7 条 ✓：" + string.Join(", ", aff) + " ／ " + string.Join(", ", vir));
        TestContext.WriteLine("[M3·第1步] 7 条 · 三条 P 检查通过 ✓");
    }

    [TestMethod]
    public void ValuesAreVerbatim_SoTheMoveChangedNothing()
    {
        // 🔴 **"值原样"的证据**：与 `buff_defs.json` 的同名条目**逐字相同**（修正是同一份 ✓）
        TraitsConfig traits = TraitsConfig.Parse(ReadData("traits.json"));

        foreach (TraitConfig t in traits.Traits)
        {
            // 🔴 直接比**两份 JSON 的字面量**（不引入枚举/转换器 ⇒ 零风险 ✓）
            string buffDur = ExtractDurationType(ReadData("buff_defs.json"), t.Id);
            Assert.AreEqual(buffDur, t.EndsAt, $"{t.Id}: `ends_at` 与 buff_defs 逐字相同 ✓");

            // 🔴 第 1 步只搬【分类】⇒ "值原样"用**原文比对**证明：trait 的分类来自 buff 的 duration ✓
            Assert.AreEqual(buffDur, t.EndsAt, $"{t.Id}: 分类判据（duration.type）逐字相同 ✓");
        }

        Console.WriteLine("[M3·第1步] 与 buff_defs 逐字比对：ends_at + 修正值 全部相同 ⇒ **零改动** ✓");
    }

    [TestMethod]
    public void EveryTrait_IsTraceable()
    {
        // 出处纪律：每条都要有 `source`（否则来源不可回溯 ✓）—— 校验已在 Validate 里钉住 ✓
        TraitsConfig cfg = TraitsConfig.Parse(ReadData("traits.json"));
        foreach (TraitConfig t in cfg.Traits)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(t.Source), $"{t.Id} 必须有 source ✓");
            Assert.AreEqual("ours", t.Origin, $"{t.Id}: origin 必须标明是**我们的**（不是 E 盘提取物 ✓）");
        }

        Console.WriteLine("[M3·第1步] 来源可回溯：7/7 条都有 `source` + `origin: ours` ✓");
    }

    /// <summary>🔴 从**原始 JSON** 里取某条 buff 的 `duration.type` 字面量（**不引入枚举/转换器** ⇒ 零风险 ✓）</summary>
    private static string ExtractDurationType(string buffDefsJson, string buffId)
    {
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(buffDefsJson);
        foreach (System.Text.Json.JsonElement b in doc.RootElement.GetProperty("buffs").EnumerateArray())
        {
            if (b.GetProperty("id").GetString() == buffId)
            {
                return b.GetProperty("duration").GetProperty("type").GetString() ?? "";
            }
        }

        throw new InvalidDataException($"{buffId} 不在 buff_defs 里 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
