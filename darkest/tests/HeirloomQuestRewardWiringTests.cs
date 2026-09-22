using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **传家宝「任务奖励」步骤 ②（接线）的纯机制验收**（策划 `DELIVERY-DESIGNER-HEIRLOOM-STEP2-ANSWER` ✓）
///
/// 策划给的三口径（一手核过的两条 + 一条"数据说不出"）：
///   ① **难度档 ← 队伍的 resolve level**：一手 `generated_resolve_level_difficulties` =
///      `[0,1,2]→1 · [2,3,4]→3 · [4,5,6]→5` ✓
///   ② **任务长度 ← 任务自身的 `length`**（一手：`plot_tutorial_crypts length=1 …` ✓）
///   ③ **每趟 4 种都给**（数据说不出 ⇒ 最直接读法 ⇒ **标 placeholder** + 观察清单 **O11** ⚠️）
/// 🔴 **索引口径 = (ii)**：`amounts[difficulty][length - 1]` ⇒ **4 个数 = length 1,2,3,4** ✓
///    （我第一版写成 `row[length]` = **(i) 形** ✗ ⇒ 会让 length 4 没值，而一手**有** length=4 的任务 ✓ ⇒ 已改 ✓）
/// </summary>
[TestClass]
public sealed class HeirloomQuestRewardWiringTests
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
    public void IndexRule_IsTwo_SoLengthFourHasAValue()
    {
        // 🔴 **这条就是 (ii) 的证明**：若按 (i)（首项=长度0占位）⇒ length 4 会**没有值** ✗
        HeirloomQuestRewardConfig cfg = HeirloomQuestRewardConfig.Parse(ReadData("heirlooms.json"));

        Assert.AreEqual(0, cfg.AmountAt("deed", 5, 1), "length 1 ⇒ **0**（一手：[0,6,9,18] 首项为 0 ✓ 短任务不给 ✓）");
        Assert.AreEqual(6, cfg.AmountAt("deed", 5, 2), "length 2 ⇒ 6 ✓");
        Assert.AreEqual(9, cfg.AmountAt("deed", 5, 3), "length 3 ⇒ 9 ✓");
        Assert.AreEqual(18, cfg.AmountAt("deed", 5, 4), "length 4 ⇒ **18**（末项 ✓ —— (i) 读法在这里会得 0 ✗）");
        Assert.AreEqual(0, cfg.AmountAt("deed", 5, 5), "length 5 越界 ⇒ 0 ✓");

        Console.WriteLine("[传家宝·步骤②] 索引口径 (ii) 已生效：deed 档5 ⇒ length 1/2/3/4 = 0 / 6 / 9 / **18** ✓");
    }

    [TestMethod]
    public void DifficultyBands_MatchThePrimary()
    {
        // 一手三条带（[0,1,2]→1 · [2,3,4]→3 · [4,5,6]→5 ✓）；2 与 4 重叠 ⇒ 取靠后档（已在注释写明 ✓）
        Assert.AreEqual(1, HeirloomQuestRewardConfig.DifficultyForResolveLevel(0), "level 0 ⇒ 档 1 ✓");
        Assert.AreEqual(1, HeirloomQuestRewardConfig.DifficultyForResolveLevel(1), "level 1 ⇒ 档 1 ✓");
        Assert.AreEqual(3, HeirloomQuestRewardConfig.DifficultyForResolveLevel(3), "level 3 ⇒ 档 3 ✓");
        Assert.AreEqual(5, HeirloomQuestRewardConfig.DifficultyForResolveLevel(5), "level 5 ⇒ 档 5 ✓");
        Assert.AreEqual(5, HeirloomQuestRewardConfig.DifficultyForResolveLevel(6), "level 6 ⇒ 档 5 ✓");

        Console.WriteLine("[传家宝·步骤②] 难度带：0/1⇒1 · 3⇒3 · 5/6⇒5 ✓（2 与 4 在一手里重叠 ⇒ 取靠后档，已注明 ✓）");
    }

    [TestMethod]
    public void RewardPerRun_IsPrinted_ForThePlanner()
    {
        // 策划要求「前后读数：传家宝产出量（每趟/每档）」⇒ 本件把**任务奖励读法**下的产出量逐格打印 ✓
        HeirloomQuestRewardConfig cfg = HeirloomQuestRewardConfig.Parse(ReadData("heirlooms.json"));

        Console.WriteLine("[传家宝·步骤②] 任务奖励产出量（每趟 · 4 种都给 · 标 placeholder ✓）：");
        foreach (int tier in new[] { 1, 3, 5 })
        {
            foreach (int len in new[] { 1, 2, 3, 4 })
            {
                var r = cfg.RewardFor(tier, len);
                string detail = string.Join(" ", r.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => $"{kv.Key}={kv.Value}"));
                Console.WriteLine($"    档{tier} 长度{len} ⇒ 合计 **{cfg.RewardTotalFor(tier, len)}** （{detail}）");
            }
        }

        Assert.AreEqual(54, cfg.RewardTotalFor(5, 4), "档5 长度4 ⇒ 合计 **54**（deed 18 + crest 18 + bust 9 + portrait 9 ✓）");
        Assert.AreEqual(0, cfg.RewardTotalFor(5, 1), "档5 长度1 ⇒ 0（短任务不给 ✓）");
        Assert.AreEqual(4, cfg.RewardFor(5, 4).Count, "**每趟 4 种都给**（策划裁的最直接读法 ✓ placeholder ⚠️ O11）");
    }

    public TestContext TestContext { get; set; } = null!;
}
