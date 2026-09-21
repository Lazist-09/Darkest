using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **`next_round` ③ 解锁：内核消费点验收**（`#316`③ / `O-86` / C1·C2·C3）。
///
/// 本用例锁三件事：
/// ① **阈值 → 解锁**（起手 0 解锁；第 1 趟 ⇒ Tavern；第 3 趟 ⇒ Abbey + 书堆/圣坛；第 6/10 趟 ⇒ 上限 10/12）
/// ② **C1：硬上限 vs 当前可用上限**（`roster.Cap` 恒 12；`CurrentRosterCap` 起手 8、逐级抬高、**不超 12**）
/// ③ **C2：内核也要拦**（未解锁的 Curio **抽不到**；未到上限的招募按【当前可用上限】判满员）
/// </summary>
[TestClass]
public sealed class UnlockConsumptionTests
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

    private static (UnlocksConfig Unlocks, CuriosConfig Curios) Load()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        UnlocksConfig unlocks = UnlocksConfig.Parse(ReadData("unlocks.json"),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curios.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            12);
        return (unlocks, curios);
    }

    [TestMethod]
    public void UnlockCurve_MatchesTheContractList()
    {
        (UnlocksConfig unlocks, _) = Load();
        var p = new RunProgress();

        // 起手：什么都没解锁
        Assert.AreEqual(0, p.UnlockedIds(unlocks).Count, "起手 0 解锁");
        Assert.AreEqual(8, p.CurrentRosterCap(unlocks, 12), "C1：起手可用上限 8");
        Assert.AreEqual(0, p.UnlockedCurios(unlocks).Count, "起手 Curio 可用 = 基础 4 种（解锁 0 种）");

        p.FinishRun(new CombatLog(), "completed", 3);           // 第 1 趟
        Assert.IsTrue(p.UnlockedBuildings(unlocks).Contains("tavern"), "第 1 趟 ⇒ Tavern");
        Assert.AreEqual(8, p.CurrentRosterCap(unlocks, 12), "上限未变");

        p.FinishRun(new CombatLog(), "retreat", 1);             // 第 2 趟（撤退也算"已结束"，口径见 RunProgress 注释）
        p.FinishRun(new CombatLog(), "completed", 3);           // 第 3 趟
        Assert.IsTrue(p.UnlockedBuildings(unlocks).Contains("abbey"), "第 3 趟 ⇒ Abbey");
        Assert.IsTrue(p.UnlockedCurios(unlocks).Contains("cur_book_stack"), "第 3 趟 ⇒ 书堆");
        Assert.IsTrue(p.UnlockedCurios(unlocks).Contains("cur_altar"), "第 3 趟 ⇒ 圣坛");

        while (p.RunsFinished < 6)
        {
            p.FinishRun(new CombatLog(), "completed", 3);
        }

        Assert.AreEqual(10, p.CurrentRosterCap(unlocks, 12), "第 6 趟 ⇒ 可用上限 10");

        while (p.RunsFinished < 10)
        {
            p.FinishRun(new CombatLog(), "completed", 3);
        }

        Assert.AreEqual(12, p.CurrentRosterCap(unlocks, 12), "第 10 趟 ⇒ 可用上限 12（= 硬上限）");

        p.FinishRun(new CombatLog(), "completed", 3);           // 第 11 趟
        Assert.AreEqual(12, p.CurrentRosterCap(unlocks, 12), "不得超硬上限（C1）");
    }

    [TestMethod]
    public void C2_KernelGatesUnlockedCurios_NotOnlyTheUi()
    {
        (UnlocksConfig unlocks, CuriosConfig curios) = Load();
        RoomContentsConfig contents = RoomContentsConfig.Parse(ReadData("room_contents.json"), curios);
        var p = new RunProgress();

        // 🔴 **C3 起手态**：可用 Curio = **基础 4 种**（书堆/圣坛**不在**其中）
        IReadOnlySet<string> available = p.AvailableCurios(unlocks);
        Assert.AreEqual(4, available.Count, "起手可用 4 种（C3）");
        Assert.IsTrue(available.Contains("cur_supply_crate"), "基础 4 种里有补给箱");
        Assert.IsFalse(available.Contains("cur_book_stack"), "书堆**未**解锁（第 3 趟才有）");
        Assert.IsFalse(available.Contains("cur_altar"), "圣坛**未**解锁（第 3 趟才有）");

        // 抽 200 次：**只可能在基础 4 种里**（未解锁的书堆/圣坛一次都不该出现 ⇒ 内核级拦截）
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l), tuning.Expedition.NBattles,
            firewood: 1, food: 1, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, new CombatLog(),
            new Darkest.Core.Rng.RngProvider(20260909));

        for (int i = 0; i < 200; i++)
        {
            string? picked = flow.PickCurioForRoom(contents, "event", isBranch: false, allowedCurios: available);
            Assert.IsNotNull(picked, "基础 4 种可用 ⇒ 应当抽得到（不是 null）");
            Assert.IsTrue(available.Contains(picked), $"抽到的 \"{picked}\" 必须在【当前可用】集合里");
        }

        // 把可用集合收窄成空集 ⇒ 必须返回 null（内核**不偷偷给一个**）
        for (int i = 0; i < 20; i++)
        {
            Assert.IsNull(flow.PickCurioForRoom(contents, "event", isBranch: false,
                allowedCurios: new HashSet<string>(StringComparer.Ordinal)), "空可用集 ⇒ null");
        }
    }

    [TestMethod]
    public void C1_RosterFullCheck_UsesCurrentCap_NotHardCap()
    {
        RosterConfig cfg = RosterConfig.Parse(ReadData("roster.json"));
        var roster = new Roster(cfg);
        Assert.AreEqual(12, roster.Cap, "硬上限 = 12（P22① 不变）");

        // 起手：可用上限 8 ⇒ 即使名册只有 8 人，"再招一个"必须被拒（按【当前可用上限】判，C1）
        roster.CurrentCap = 8;
        Assert.AreEqual(8, roster.Heroes.Count, "出厂名册 8 人（C3 起手态）");

        var coach = EconomyConfig.Parse(ReadData("economy.json")).Coach;
        HeroConfig? hired = roster.Recruit(new CombatLog(), coach, "warrior", "新兵");
        Assert.IsNull(hired, "🔴 C1：满员判定按【当前可用上限 8】⇒ 必须拒绝（不是按硬上限 12）");

        // 抬高到 10 ⇒ 才允许招
        roster.CurrentCap = 10;
        HeroConfig? hired2 = roster.Recruit(new CombatLog(), coach, "warrior", "新兵");
        Assert.IsNotNull(hired2, "可用上限抬到 10 ⇒ 可招");
        Assert.AreEqual(9, roster.Heroes.Count);
    }
}
