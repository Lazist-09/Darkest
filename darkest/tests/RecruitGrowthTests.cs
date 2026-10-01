using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **招募/马车的"养成"面**（做功能，不是校验）—— 两个真功能：
///   ① 🔴 **马车升级 ⇒ 新兵起始等级真的更高**（实测过的缺陷：`EffectiveRookieLevel` **只被 UI 打印**、
///      **没被消费** ⇒ "有新兵起始等级"是**死声明** ⚠️ ⇒ 本轮接上 `Recruit(..., rookieLevel)` ✓）
///   ② 🔴 **阵亡 ⇒ 空位 ⇒ 可以招募补人**（策划 `#400` 裁定 (a)+ 的闭环：**A6 不成死锁** ✓）
/// </summary>
[TestClass]
public sealed class RecruitGrowthTests
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

    /// <summary>① 马车升级 ⇒ **新兵起点更高**（把"只有展示"的效果变成"真的消费" ✓）</summary>
    [TestMethod]
    public void StagecoachUpgrade_RaisesTheRecruitsStartingLevel()
    {
        RosterConfig cfg = RosterConfig.Parse(ReadData("roster.json"));
        HeirloomConfig heirCfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        var log = new CombatLog();

        var roster = new Roster(cfg);
        var stock = new HeirloomStock(heirCfg);
        foreach (string k in heirCfg.Kinds)
        {
            stock.Add(log, k, 40, "recruit_probe");
        }

        // 🔴 `Coach` 在 **`EconomyConfig`** 上（不是 `RosterConfig`）⇒ 从 economy.json 取 ✓
        StagecoachConfig coach = EconomyConfig.Parse(ReadData("economy.json")).Coach;
        int beforeLevel = stock.EffectiveRookieLevel(coach.RookieLevel);
        HeroConfig? rookieBefore = roster.Recruit(log, coach, "warrior", "甲", rookieLevel: beforeLevel);

        // 🔴 数据事实（我第一版用例写错、被断言当场抓到 ⚠️）：马车 **Lv3 才是 `rookie_level: 2`**
        //    ⇒ 所以必须**连升 3 级**才看得到"新兵起点"变化 ✓
        //    ⚠️ **M7②（策划 `#423`）**：名册上限**不在本用例读**（单一来源 = 马车曲线 ⇒ `RosterCapGrowthTests` 专测）✓
        Assert.IsTrue(stock.TryUpgrade(log, "stagecoach"), "马车 Lv1 ✓");
        Assert.IsTrue(stock.TryUpgrade(log, "stagecoach"), "马车 Lv2 ✓");
        Assert.IsTrue(stock.TryUpgrade(log, "stagecoach"), "马车 Lv3（**新兵起始等级**）✓");
        int afterLevel = stock.EffectiveRookieLevel(coach.RookieLevel);
        HeroConfig? rookieAfter = roster.Recruit(log, coach, "warrior", "乙", rookieLevel: afterLevel);

        Assert.IsNotNull(rookieBefore);
        Assert.IsNotNull(rookieAfter);
        Assert.AreEqual(beforeLevel, rookieBefore!.Level, "升级前的招募 ⇒ 起点 = 基础值 ✓");
        Assert.AreEqual(afterLevel, rookieAfter!.Level, "升级后的招募 ⇒ 起点 = **生效值**（马车效果真的被消费 ✓）");
        Assert.IsTrue(afterLevel > beforeLevel, "马车升级必须**真的抬高**新兵起点 ✓");

        string line = $"[招募·马车] 新兵起始等级：升级前 {beforeLevel} → 升级后 **{afterLevel}**" +
                      $"（`coach.rookie_level` + 马车升级增量 ⇒ **真的落到新兵身上** ✓）";
        Console.WriteLine(line);
        TestContext.WriteLine(line);
    }

    /// <summary>② **阵亡 ⇒ 空位 ⇒ 招募补人**（(a)+ 的闭环 · A6 不成死锁 ✓）</summary>
    [TestMethod]
    public void ADeath_FreesASlot_SoRecruitingCanRefill()
    {
        RosterConfig cfg = RosterConfig.Parse(ReadData("roster.json"));
        HeirloomConfig heirCfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        var log = new CombatLog();
        var roster = new Roster(cfg);

        // 先把名册填到**当前可用上限**（模拟"满员"⇒ 招募应被拒 ✓）
        roster.CurrentCap = roster.Heroes.Count;
        // 🔴 `Coach` 在 **`EconomyConfig`** 上（不是 `RosterConfig`）⇒ 从 economy.json 取 ✓
        StagecoachConfig coach = EconomyConfig.Parse(ReadData("economy.json")).Coach;
        Assert.IsNull(roster.Recruit(log, coach, "warrior", "满员测试"), "满员时招募必须被拒（不悄悄顶替 ✓）");

        int before = roster.Heroes.Count;
        string victim = roster.Heroes[0].Id;
        log.Append(new DeathEvent(UnitId.Of(victim), true, "probe_death"));
        int removed = roster.ConsumePlayerDeaths(log);
        Assert.AreEqual(1, removed, "阵亡应被消费 ✓");

        HeroConfig? refill = roster.Recruit(log, coach, "warrior", "补位者");
        Assert.IsNotNull(refill, "🔴 **阵亡腾出的名额必须能招募补上**（(a)+ 的闭环 · A6 ✓）");

        string line = $"[招募·闭环] 满员 {before} ⇒ 阵亡 1 名 ⇒ **招募补位成功**（现 {roster.Heroes.Count}）" +
                      $"　HireEvent={log.Events.OfType<HeroRecruitedEvent>().Count()}　留档 {roster.Graveyard.Count} 名 ✓";
        Console.WriteLine(line);
        TestContext.WriteLine(line);
        Console.WriteLine($"[招募·闭环] {RosterComposition.Describe(roster)}");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
