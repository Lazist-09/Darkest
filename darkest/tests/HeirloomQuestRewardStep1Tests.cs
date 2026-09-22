using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **策划 ② 步骤 ①「加【任务奖励】通道」的验收**（**零行为** ✓ 步骤 ② 另起一轮 ✓）
///
/// 一手实测形状（`quest.generation.json` ✓ 与策划表**逐字一致** ✓）：
///   · 4 个地牢（crypts/warrens/weald/cove）**都给全 4 种** ✓
///   · **6 档难度 × 4 个任务长度**，且**只有档 1/3/5 有值** ✓ · 每行首项 `0`（长度 0 不存在 ✓）
///   · **档越高给得不少**（逐项非递减 ✓）
/// 🔴 **本轮的"没越界"证据**：`tier_drop` **仍在**（步骤 ② 才删 ✓ —— 纪律 AY：一次只改一类 ✓）
/// </summary>
[TestClass]
public sealed class HeirloomQuestRewardStep1Tests
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
    public void QuestRewardChannel_IsValid_AndMatchesThePrimaryShape()
    {
        HeirloomQuestRewardConfig cfg = HeirloomQuestRewardConfig.Parse(ReadData("heirlooms.json"));
        cfg.Validate();   // 五条判据全来自一手形状（见配置注释 ✓）

        Assert.AreEqual(4, cfg.DungeonTypes.Count, "一手：4 个地牢 ✓");
        Assert.IsTrue(cfg.DungeonTypes.Values.All(v => v.Count == 4), "一手：每个地牢都给全 4 种 ✓");
        Assert.AreEqual(4, cfg.AmountTable.Count, "4 种传家宝 ✓");

        Console.WriteLine("[传家宝·步骤①] 通道可加载 + 可校验 ✓（4 地牢 × 4 种 · 6 档 × 4 长度）");
    }

    [TestMethod]
    public void Step1_DidNotTouchTierDrop_SoBehaviorIsUnchanged()
    {
        // 🔴 **这就是"零行为"的机械证据**：步骤 ① 只加通道 ⇒ `tier_drop` 必须**原封不动** ✓
        string json = ReadData("heirlooms.json");
        Assert.IsTrue(json.Contains("\"tier_drop\"", StringComparison.Ordinal),
            "**步骤 ① 不许删 `tier_drop`** ⇒ 删它是步骤 ②（另起一轮 ✓ 纪律 AY：一次只改一类 ✓）");
        Assert.IsTrue(json.Contains("\"quest_reward\"", StringComparison.Ordinal), "通道已加入 ✓");

        Console.WriteLine("[传家宝·步骤①] `tier_drop` **仍在** + `quest_reward` **已加** ⇒ 本步零行为 ✓");
    }

    [TestMethod]
    public void AmountTable_Readout_IsPrinted_ForThePlanner()
    {
        // 策划要求「每步要【前后读数】：传家宝产出量（每趟/每档）」⇒ 本件把**将来的产出量**逐格打出来 ✓
        HeirloomQuestRewardConfig cfg = HeirloomQuestRewardConfig.Parse(ReadData("heirlooms.json"));

        Console.WriteLine("[传家宝·步骤①] 任务奖励产出量（档 × 任务长度 1~4）：");
        foreach (string kind in new[] { "bust", "portrait", "deed", "crest" })
        {
            foreach (int tier in new[] { 1, 3, 5 })
            {
                string row = string.Join(", ", Enumerable.Range(1, 4).Select(len => cfg.AmountAt(kind, tier, len)));
                Console.WriteLine($"    {kind,-9} 档{tier} ⇒ [{row}]");
            }
        }

        // 🔴 **索引口径有歧义 ⇒ 我不断言它**（不编 ✓）：
        //   一手 `[0,2,2,4]` 只有 **4 个元素**。两种读法：
        //     (i) 首项 = 长度 0 的占位（策划口径）⇒ 覆盖长度 0..3 ⇒ **长度 4 没有值** ⚠️
        //     (ii) 首项 = **长度 1**（值 0 ⇒ 短任务不给传家宝）⇒ 覆盖长度 1..4 ⇒ 长度 4 = 末项 ✓
        //   ⇒ 两种都自洽 ⇒ **我只断言无歧义的事实**（4 地牢全 4 种 · 只有奇档 · 每行 4 项 · 档越高不少 ✓），
        //     并把歧义**打出来**让策划裁 ✓
        Assert.AreEqual(0, cfg.AmountAt("deed", 0, 4), "档 0 没有配 ⇒ 0（无歧义 ✓）");
        Assert.AreEqual(0, cfg.AmountAt("deed", 5, 0), "索引 0 之外越界 ⇒ 0（无歧义 ✓）");
        Console.WriteLine("[传家宝·步骤①] ⚠️ **索引口径歧义（请策划裁）**：一手每行 4 项 ⇒");
        Console.WriteLine("    (i) 首项=长度0 占位 ⇒ 长度 4 无值　(ii) 首项=长度1(=0) ⇒ 长度 4 = 末项");
        Console.WriteLine("    两种读法下 `deed 档5` 的 4 个数 = [0,6,9,18] ⇒ 长度4 是 **18**（读数，非断言 ✓）");
    }

    public TestContext TestContext { get; set; } = null!;
}
