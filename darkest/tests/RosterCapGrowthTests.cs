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
/// 背景：`EffectiveRosterCap` 此前**只被 UI 打印**（= 有展示、无消费 ⚠️）⇒ 本轮按 `#403`
///   **(b) 相加 + 封顶** 把它接进 `CurrentRosterCap`：`min(硬上限, 起手 8 + 解锁增量 + 马车增量)` ✓
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
            rosterHardCap: 12);
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
        int baseCap = unlocks.RosterBaseCap; // 起手（= 8）

        // ① 起手：**只有解锁**（0 趟）⇒ 上限 = 起手值 ✓
        var progress = new RunProgress();
        int capStart = progress.CurrentRosterCap(unlocks, cfg.RosterCap);
        Assert.AreEqual(baseCap, capStart, "0 趟时上限 = 起手值 ✓");

        // ② 跑够 6 趟 ⇒ 解锁 `roster_cap_delta:2` ⇒ 上限 = 起手 + 2 ✓（#403 的**增量**语义 ✓）
        for (int i = 0; i < 6; i++)
        {
            progress.FinishRun(log, "completed", battlesWon: 3);
        }

        int capUnlocked = progress.CurrentRosterCap(unlocks, cfg.RosterCap);
        Assert.AreEqual(baseCap + 2, capUnlocked, "6 趟后应 +2（`roster_cap_delta:2` ⇒ **增量** ✓）");

        // ③ **填满**到解锁后的上限 ⇒ 招募**应被拒**（"满员即拒" ✓）
        roster.CurrentCap = capUnlocked;
        while (roster.Heroes.Count < capUnlocked)
        {
            Assert.IsNotNull(roster.Recruit(log, coach, "warrior", $"填{roster.Heroes.Count + 1}"));
        }

        Assert.AreEqual(capUnlocked, roster.Heroes.Count, "应正好填满 ✓");
        Assert.IsNull(roster.Recruit(log, coach, "warrior", "满员测试"), "满员 ⇒ 招募**必须被拒**（不悄悄顶替 ✓）");

        // ④ **马车 Lv1（上限 +2）⇒ 上限应真的变大 ⇒ 招募变可用** = **A12 的判据** ✓
        int deltaBefore = stock.EffectiveRosterCap(baseCap, cfg.RosterCap) - baseCap;
        Assert.IsTrue(stock.TryUpgrade(log, "stagecoach"), "马车应能升 Lv1（+2 上限）✓");
        int deltaAfter = stock.EffectiveRosterCap(baseCap, cfg.RosterCap) - baseCap;
        int capAfter = progress.CurrentRosterCap(unlocks, cfg.RosterCap, heirloomDelta: deltaAfter);

        Assert.IsTrue(deltaAfter > deltaBefore, "马车升级必须**真的抬高**上限生效值 ✓");
        Assert.IsTrue(capAfter > capUnlocked || capAfter == cfg.RosterCap, "上限应增（或已到硬上限）✓");

        roster.CurrentCap = capAfter;
        HeroConfig? rookie = roster.Recruit(log, coach, "warrior", "马车补位",
            rookieLevel: stock.EffectiveRookieLevel(coach.RookieLevel));
        Assert.IsNotNull(rookie, "🔴 **A12**：升级后上限变大 ⇒ **招募从「满员即拒」变「可招募」** ✓");

        Console.WriteLine($"[A12] 名册上限：起手 {capStart} ⇒ 6 趟解锁后 **{capUnlocked}** ⇒ 马车 Lv1 后 **{capAfter}**（硬上限 {cfg.RosterCap}）");
        Console.WriteLine($"[A12] 招募：满员（{capUnlocked}）时被拒 ✓ ⇒ 升级后**成功招到** {rookie!.Name}（Lv{rookie.Level}）✓");
        TestContext.WriteLine($"[A12] 上限 {capStart} → {capUnlocked} → {capAfter}；招募由拒变可用 ✓");
        Console.WriteLine($"[A12] {RosterComposition.Describe(roster)}");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
