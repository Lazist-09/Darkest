using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🆕 **M7 第 ③ 步（策划 `#423`）**：今日新兵的**数量曲线 + 高级新兵概率 + 确定性** ✓
///   数据：`num_recruits_by_level` = 2→3→4→5→6→7 · `upgraded_recruit_chances_pct` = 18.75/12.5/6.25% ✓
///   🔴 本件**只测纯函数**（招募屏接线在 UI 域 ⇒ 未接线前它零行为 ✓）
/// </summary>
[TestClass]
public sealed class StagecoachRecruitsTests
{
    [TestMethod]
    public void CountFollowsTheRuledCurve_TwoToSeven()
    {
        EconomyConfig eco = EconomyConfig.Parse(ReadData("economy.json"));
        StagecoachConfig coach = eco.Coach;

        // 策划 #423：2 → 3 → 4 → 5 → 6 → 7（索引 = 马车等级）✓
        Assert.AreEqual(2, StagecoachRecruits.CountAt(coach, 0), "未升级 ⇒ 2 个 ✓");
        Assert.AreEqual(7, StagecoachRecruits.CountAt(coach, 5), "满级 ⇒ 7 个 ✓");
        Assert.AreEqual(7, StagecoachRecruits.CountAt(coach, 99), "越界 ⇒ 钳到末档 ✓");
        Assert.AreEqual(2, StagecoachRecruits.CountAt(coach, -3), "负值 ⇒ 钳到首档 ✓");
        Console.WriteLine($"[M7③] 数量曲线：{new[] { 2, 3, 4, 5, 6, 7 }.Length} 档 · Lv0=2 · Lv5=7 ✓");
    }

    [TestMethod]
    public void UpgradedChanceFollowsTheRuledNumbers()
    {
        StagecoachConfig coach = EconomyConfig.Parse(ReadData("economy.json")).Coach;

        // 策划 #423：18.75 / 12.5 / 6.25 % ✓（数值一字未改，照抄数据 ✓）
        Assert.AreEqual(18.75, StagecoachRecruits.UpgradedChancePctAt(coach, 0), 1e-9, "a 档 18.75% ✓");
        Assert.AreEqual(12.5, StagecoachRecruits.UpgradedChancePctAt(coach, 1), 1e-9, "b 档 12.5% ✓");
        Assert.AreEqual(6.25, StagecoachRecruits.UpgradedChancePctAt(coach, 2), 1e-9, "c 档 6.25% ✓");
        Assert.AreEqual(6.25, StagecoachRecruits.UpgradedChancePctAt(coach, 5), 1e-9, "越界 ⇒ 末档 ✓");
        Console.WriteLine("[M7③] 高级概率：18.75 / 12.5 / 6.25 %（照抄 #423）✓");
    }

    [TestMethod]
    public void Roll_IsDeterministic_ForTheSameSeed_AndWritesEveryDraw()
    {
        StagecoachConfig coach = EconomyConfig.Parse(ReadData("economy.json")).Coach;

        var logA = new CombatLog();
        var logB = new CombatLog();
        IReadOnlyList<StagecoachRecruits.Offering> a =
            StagecoachRecruits.Roll(coach, 3, new RngProvider(20260922), logA);
        IReadOnlyList<StagecoachRecruits.Offering> b =
            StagecoachRecruits.Roll(coach, 3, new RngProvider(20260922), logB);

        Assert.AreEqual(5, a.Count, "Lv3 ⇒ 5 个（曲线 2→3→4→5→6→7 ✓）");
        CollectionAssert.AreEqual(a.Select(x => x.Upgraded).ToArray(), b.Select(x => x.Upgraded).ToArray(),
            "🔴 同种子 ⇒ 同结果（确定性 ✓）");

        int draws = logA.Events.Count(e => e is StagecoachRecruitRolledEvent);
        Assert.AreEqual(5, draws, "🔴 每一次判定都必须留痕（可审计 ✓）");
        foreach (StagecoachRecruitRolledEvent e in logA.Events.OfType<StagecoachRecruitRolledEvent>())
        {
            Assert.AreEqual(3, e.StagecoachLevel, "事件要带【马车等级 3】（不是我误写的数量 5）✓");
            Assert.IsTrue(e.DrawPercent >= 0 && e.DrawPercent <= 100, "掷骰是百分比 ✓");
        }

        Console.WriteLine($"[M7③] Lv3 ⇒ {a.Count} 个 · 高级 {a.Count(x => x.Upgraded)} 个 · 掷骰留痕 {draws} 条 ✓");
        Console.WriteLine($"[M7③] 高级新兵起始等级 = 基础{coach.RookieLevel} + 1 = {StagecoachRecruits.StartingLevel(coach, new StagecoachRecruits.Offering(true))} ✓");
        TestContext.WriteLine("[M7③] 确定性 + 留痕 ✓");
    }

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

    public TestContext TestContext { get; set; } = null!;
}