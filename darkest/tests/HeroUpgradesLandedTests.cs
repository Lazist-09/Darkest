using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M8 落库验收**（架构授权"按你的建议执行" ⇒ **每题附依据 + 读数** ✓）
///
/// 本件**读【已入库】的 `data/hero_upgrades.json`**（不是我量测的中间产物 ✓）⇒ 这才是"落库了"的证据 ✓
///   · **依据**：数据来自 **E 盘一手** `upgrades/heroes/*.upgrades.json`（`tools/dsh/extract_dd1_hero_upgrades.py` ✓）
///   · **读数**：**15 职业 / 135 树 / 645 等级** + 三道 P 检查（悬空 / 自环 / `level_codes` 不一致 ✓）
///   · 🔴 **零行为**：本数据**尚无消费方**（职业升级 UI/逻辑属后续 ✓）⇒ 落库只让"能加载 + 可校验" ✓
/// </summary>
[TestClass]
public sealed class HeroUpgradesLandedTests
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
    public void LandedFile_ParsesValidates_AndCountsMatchTheMeasuredExtraction()
    {
        HeroUpgradesConfig cfg = HeroUpgradesConfig.Parse(ReadData("hero_upgrades.json"));

        // 外部树目录 = 建筑树（真实数据里的先决会指向 blacksmith.weapon 等 ✓）
        var external = new List<string>();
        BuildingsConfig buildings = BuildingsConfig.Parse(ReadData("buildings.json"));
        foreach (BuildingConfig b in buildings.Buildings)
        {
            foreach (BuildingTreeConfig t in b.Trees)
            {
                external.Add(t.Id);
            }
        }

        cfg.Validate(external);   // 🔴 不抛 ⇒ 已入库数据**全部可解**（悬空 0 / 自环 0 / codes 一致 ✓）

        Assert.AreEqual(15, cfg.Heroes.Count, "**15 职业**（与卡片一致 ✓）");
        Assert.AreEqual(135, cfg.TreeCount, "**135 条升级树** ✓");
        Assert.AreEqual(645, cfg.LevelCount, "**645 个等级** ✓");

        string first = cfg.Heroes.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
        Console.WriteLine($"[M8·落库] **15 职业 / 135 树 / 645 等级** ✓（外部树目录 {external.Count} 条 ⇒ 无悬空 ✓）");
        Console.WriteLine($"[M8·落库] 例：{first} 有 {cfg.Heroes[first].Trees.Count} 条树 ✓");
        TestContext.WriteLine("[M8·落库] 已入库文件可加载 + 可校验 ✓");
    }

    [TestMethod]
    public void EveryLevel_CarriesItsOriginMarker()
    {
        // 依据可核对：**每级都标 origin**（我的纪律：来源必须可回溯 ✓ 架构 §33/#448 的口径 ✓）
        HeroUpgradesConfig cfg = HeroUpgradesConfig.Parse(ReadData("hero_upgrades.json"));
        int total = 0, marked = 0;
        foreach (HeroUpgradeClassConfig cls in cfg.Heroes.Values)
        {
            foreach (HeroUpgradeTreeConfig tree in cls.Trees)
            {
                foreach (HeroUpgradeLevelConfig lv in tree.Levels)
                {
                    total++;
                    if (!string.IsNullOrEmpty(lv.Origin))
                    {
                        marked++;
                    }
                }
            }
        }

        Assert.AreEqual(total, marked, "**每一级都要带 `origin`**（否则来源不可核对 ✓）");
        Assert.AreEqual(645, total, "总数仍是 645 ✓");
        Console.WriteLine($"[M8·落库] 来源标记：{marked}/{total} 级都带 `origin` ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
