using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **A12**（策划 `#403`）：**"马车升级 ⇒ 名册上限真的变大（可招募位数变多）"必须可测** ——
///   判据（他给的）：**升级后 `CurrentCap` 增 ⇒ 招募从"满员即拒"变"可招募"** ✓
///
/// 🆕 **M7②（`#423`）· 上限的单一来源 = 马车曲线**（`economy.json` 的
///   `stagecoach.roster_cap_by_level`，索引 = 马车等级）：
///   · 传曲线 ⇒ `min(硬上限, 曲线[马车等级])`，**解锁表不再提供任何上限增量**（`roster_cap_delta:` 已撤）✓
///   · 不传曲线 ⇒ 老口径一字不动（历史读数不被改写 ✓）
///   ⇒ 本用例验收的正是**新口径**：唯一可变项 = 马车等级 ✓
/// </summary>
[TestClass]
public sealed class RosterCapGrowthTests
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
    public void StagecoachUpgrade_RaisesTheCap_SoRecruitingBecomesPossible()
    {
        RosterConfig cfg = RosterConfig.Parse(ReadData("roster.json"));
        // 🔴 `UnlocksConfig.Parse` **必须**拿到建筑/Curio 目录才能校验（P27 ④）——我第一版忘传 ⇒ 用例当场红 ✓
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        UnlocksConfig unlocks = UnlocksConfig.Parse(ReadData("unlocks.json"),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curios.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            rosterHardCap: cfg.RosterCap);   // 🔴 硬上限**读数据**（`roster.json` 的 roster_cap）✓
        HeirloomConfig heirCfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        EconomyConfig ecoCfg = EconomyConfig.Parse(ReadData("economy.json"));

        var log = new CombatLog();
        var roster = new Roster(cfg);
        var stock = new HeirloomStock(heirCfg);
        foreach (string k in heirCfg.Kinds)
        {
            stock.Add(log, k, 40, "cap_probe");
        }

        StagecoachConfig coach = ecoCfg.Coach;
        // 🔴 **上限的唯一来源**：马车曲线（`economy.json` 的 stagecoach.roster_cap_by_level）✓
        IReadOnlyList<int> curve = coach.RosterCapByLevel
            ?? throw new InvalidDataException("economy.json 缺 stagecoach.roster_cap_by_level（= M7② 上限的单一来源）✓");
        Assert.IsTrue(curve.Count >= 2, "马车曲线至少两档（Lv0 / Lv1）—— 否则「升马车抬上限」无从谈起 ✓");

        // ① 马车 Lv0 ⇒ 上限 = 曲线[0]（**不是** unlocks.RosterBaseCap；后者只管起手建筑/Curio）✓
        var progress = new RunProgress();
        int capStart = progress.CurrentRosterCap(unlocks, cfg.RosterCap,
            stagecoachCapByLevel: curve, stagecoachLevel: 0);
        Assert.AreEqual(curve[0], capStart, "🔴 马车 Lv0 ⇒ 上限 = 曲线[0] ✓");
        Assert.IsTrue(capStart < cfg.RosterCap, "曲线起手值必须**低于**硬上限（否则升级无处可抬）✓");

        // ② 跑够 6 趟（含 1／3 趟两条解锁阈值）⇒ 🔴 **解锁不抬高上限**（单一来源 = 马车曲线）⇒ 读数不变 ✓
        for (int i = 0; i < 6; i++)
        {
            progress.FinishRun(log, "completed", battlesWon: 3);
        }

        int capUnlocked = progress.CurrentRosterCap(unlocks, cfg.RosterCap,
            stagecoachCapByLevel: curve, stagecoachLevel: 0);
        Assert.AreEqual(curve[0], capUnlocked,
            "🔴 M7②/#423：单一来源 = 马车曲线 ⇒ 解锁/趟数**不改变**上限（原口径的「起手 +2」已撤）✓");

        // ③ **填满**到当前上限 ⇒ 招募**应被拒**（"满员即拒" ✓）
        roster.CurrentCap = capUnlocked;
        while (roster.Heroes.Count < capUnlocked)
        {
            Assert.IsNotNull(roster.Recruit(log, coach, "warrior", $"填{roster.Heroes.Count + 1}"));
        }

        Assert.AreEqual(capUnlocked, roster.Heroes.Count, "应正好填满 ✓");
        Assert.IsFalse(roster.CanRecruit, "满员 ⇒ 内核 `Roster.CanRecruit` 必须为 False（红线 21 (b)：UI 只渲染）✓");
        Assert.IsNull(roster.Recruit(log, coach, "warrior", "满员测试"), "满员 ⇒ 招募**必须被拒**（不悄悄顶替 ✓）");

        // ④ **马车 Lv1 ⇒ 上限 = 曲线[1]（真的变大）⇒ 招募变可用** = **A12 的判据** ✓
        Assert.IsTrue(stock.TryUpgrade(log, "stagecoach"), "马车应能升 Lv1（曲线下一档）✓");
        int capAfter = progress.CurrentRosterCap(unlocks, cfg.RosterCap,
            stagecoachCapByLevel: curve, stagecoachLevel: stock.LevelOf("stagecoach"));
        Assert.AreEqual(curve[1], capAfter, "🔴 马车 Lv1 ⇒ 上限 = 曲线[1]（与等级一一对应）✓");
        Assert.IsTrue(capAfter > capUnlocked, "马车升级必须**真的抬高**上限（曲线 Lv0 ⇒ Lv1）✓");

        roster.CurrentCap = capAfter;
        Assert.IsTrue(roster.CanRecruit, "🔴 **A12**：升级后上限变大 ⇒ 内核判定「可招募」✓");
        HeroConfig? rookie = roster.Recruit(log, coach, "warrior", "马车补位",
            rookieLevel: stock.EffectiveRookieLevel(coach.RookieLevel));
        Assert.IsNotNull(rookie, "🔴 **A12**：升级后上限变大 ⇒ **招募从「满员即拒」变「可招募」** ✓");

        Console.WriteLine($"[A12] 名册上限（唯一来源 = 马车曲线 {string.Join("/", curve)}）：Lv0 {capStart}" +
                          $" ⇒ 6 趟解锁后仍 **{capUnlocked}**（解锁不加增量 ✓）⇒ Lv1 **{capAfter}**（硬上限 {cfg.RosterCap}）");
        Console.WriteLine($"[A12] 招募：满员（{capUnlocked}）时被拒 ✓ ⇒ 升级后**成功招到** {rookie!.Name}（Lv{rookie.Level}）✓");
        TestContext.WriteLine($"[A12] 上限 {capStart} → {capUnlocked} → {capAfter}；招募由拒变可用 ✓");
        Console.WriteLine($"[A12] {RosterComposition.Describe(roster)}");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
