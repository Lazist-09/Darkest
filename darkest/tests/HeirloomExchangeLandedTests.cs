using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **策划 ① 「祖产兑换表 · 现在就做（12 条）**」的落库验收 ✓
///
/// 依据：一手 E 盘 `campaign/heirloom_exchange/heirloom_exchange.json`（**一手** ⇒ 出处等级最高 ✓）
/// 读数：**12 条** · 每条 `origin="dd1"` + `placeholder=true` ✓ · 结构校验（类型/数量/唯一）✓
/// 🔴 并把「**损失率**」逐条**打印**（不是判据 ✓）⇒ 因为我实测发现：一手 12 条里**并非都损失 50%**
/// </summary>
[TestClass]
public sealed class HeirloomExchangeLandedTests
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
    public void TwelveRates_FromPrimary_WithOriginAndPlaceholderMarkers()
    {
        HeirloomExchangeConfig cfg = HeirloomExchangeConfig.Parse(ReadData("heirloom_exchange.json"));
        cfg.Validate();   // 结构校验：类型 ∈ 四种 · 数量 ≥ 1 · from ≠ to · (from,to) 唯一 ✓

        Assert.AreEqual(12, cfg.Rates.Count, "**12 条**（策划原话：「12 条 · 一次落完」✓）");
        Assert.AreEqual(12, cfg.Rates.Count(e => e.Origin == "dd1"), "每条都要标 `origin=dd1`（一手 ✓）");
        Assert.AreEqual(12, cfg.Rates.Count(e => e.Placeholder == true), "每条都要标 `placeholder=true`（#422 口径 ✓）");

        Console.WriteLine($"[祖产兑换] **{cfg.Rates.Count} 条** · origin=dd1 {cfg.Rates.Count(e => e.Origin == "dd1")} 条 · "
            + $"placeholder {cfg.Rates.Count(e => e.Placeholder == true)} 条 ✓");
    }

    [TestMethod]
    public void LossRate_IsPrintedPerEntry_NotAsserted()
    {
        // 🔴 **它是读数、不是判据**：我实测发现一手 12 条**并非都损失 50%** ⇒ 断言 50% 会逼数据说谎 ✗
        HeirloomExchangeConfig cfg = HeirloomExchangeConfig.Parse(ReadData("heirloom_exchange.json"));

        Console.WriteLine("[祖产兑换] 逐条隐含损失率（相对价值 portrait:bust:deed:crest = 6:3:3:2 ✓）：");
        foreach ((string pair, double loss) in cfg.LossTable())
        {
            Console.WriteLine($"    {pair,-28} 损失 {loss:0.#}%");
        }

        // 只断言"每一行都算得出来"（数据可解析 ✓）—— 不断言具体损失率 ✓
        Assert.AreEqual(cfg.Rates.Count, cfg.LossTable().Count(), "每一条都能算出损失率（数据可解析 ✓）");
    }

    [TestMethod]
    public void KindsSpellingDiffersFromHeirloomsJson_AndThatIsRecorded()
    {
        // 🔴 如实记录一处**口径不一致**：本表用**单数**（bust），`heirlooms.json` 的 `kinds` 用**复数**（busts）✗
        //    ⇒ 将来接线时必须先统一（否则"兑换 bust"会查不到 "busts" ✓）
        Console.WriteLine("[祖产兑换] 口径提示：本表 = 单数（" + string.Join("/", HeirloomExchangeConfig.SingleKinds)
            + "）；`heirlooms.json` 的 `kinds` = 复数（busts/crests/deeds/portraits）⇒ **接线前要统一** ✓");
        Assert.AreEqual(4, HeirloomExchangeConfig.SingleKinds.Length, "四种传家宝 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
