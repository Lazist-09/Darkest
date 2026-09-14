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
/// 🔴 **真缺陷回归防线**（`C2` 消费点 (b) "内核也要拦"）：我实现过 `RunProgress.AvailableCurios`，
/// 但**生产调用点一度没把它传给 `PickCurioForRoom`** ⇒ **未解锁的 Curio 在生产里照样抽得到** ⚠️
/// （"有 API 无调用" = 红线 21 家族；由死代码扫描抓到 ✓）
///
/// 修法：`PickCurioForRoom` 内部**自动**按注入的 `Unlocks` + `Progress` 过滤（调用方不必记得传参）✓
/// 本用例锁：**不传 allowedCurios** 时，注入了解锁状态 ⇒ 未解锁的 Curio 也必须抽不到 ✓
/// </summary>
[TestClass]
public sealed class CurioUnlockGateWiringTests
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
    public void WithoutInjection_NoGating_AllPoolsUsable()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        RoomContentsConfig contents = RoomContentsConfig.Parse(ReadData("room_contents.json"), curios);
        ExpeditionFlow flow = RoomContentSelectionTests_Flow(seed: 5, progress: null, unlocks: null);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < 200; i++)
        {
            string? picked = flow.PickCurioForRoom(contents, "event", isBranch: false);
            if (picked is not null)
            {
                seen.Add(picked);
            }
        }

        Assert.IsTrue(seen.Contains("cur_book_stack") || seen.Contains("cur_altar"),
            "未注入解锁状态 ⇒ 不做门禁（保持旧行为：全部池可用）✓（这是「未接线」的显式状态，不是静默）");
    }

    [TestMethod]
    public void WithInjection_LockedCuriosAreNeverPicked_EvenWithoutPassingTheSet()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        RoomContentsConfig contents = RoomContentsConfig.Parse(ReadData("room_contents.json"), curios);
        UnlocksConfig unlocks = UnlocksConfig.Parse(ReadData("unlocks.json"),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curios.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal), 12);

        var progress = new RunProgress(); // 起手：0 趟 ⇒ 书堆/圣坛**未解锁**
        ExpeditionFlow flow = RoomContentSelectionTests_Flow(seed: 5, progress, unlocks);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < 300; i++)
        {
            string? picked = flow.PickCurioForRoom(contents, "event", isBranch: false); // 🔴 故意**不传** allowedCurios
            if (picked is not null)
            {
                seen.Add(picked);
            }
        }

        Assert.IsTrue(seen.Count > 0, "起手可用 4 种 ⇒ 仍应抽得到");
        Assert.IsFalse(seen.Contains("cur_book_stack"),
            "🔴 注入解锁状态后，**未解锁的**书堆**抽不到**（修复前这里会因为调用方没传参而漏过）");
        Assert.IsFalse(seen.Contains("cur_altar"), "🔴 同理：圣坛未解锁");
        Assert.IsTrue(seen.All(id => progress.AvailableCurios(unlocks).Contains(id)),
            "抽到的一切都必须在【当前可用】集合里 ✓");
    }

    private static ExpeditionFlow RoomContentSelectionTests_Flow(int seed, RunProgress? progress, UnlocksConfig? unlocks)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        return new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, new CombatLog(),
            new Darkest.Core.Rng.RngProvider(seed))
        {
            Progress = progress,
            Unlocks = unlocks,
        };
    }
}
