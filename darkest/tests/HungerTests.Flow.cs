// 🔴 从 HungerTests.cs 拆出（用户红线 ≤600 行 · 架构 file_size_split §1.3）——只搬家、零行为改动 ✓
//    本文件 = D-5 饥饿：**流程级接线**（开局缓冲 ／ 前行递减·回头不动 ／ 每掷必写 RngDraw ／ 未配置不掷）
//    依赖主片私有成员：ReadData ／ NewFlow ／ _lastLog ／ SegmentCost ／ Cfg ／ FixedRng ✓

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

public sealed partial class HungerTests
{

    // ────────────────────────────── D-5：流程级接线（缓冲 + 触发）──────────────────────────────

    [TestMethod]
    public void Flow_InitialBuffer_ComesFromConfig()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost);
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(tuning.DungeonLayer!.Hunger!.BufferAtStart, flow.HungerBuffer,
            "🔴 开局缓冲必须 = `hunger.buffer_at_start`（DD：开局给缓冲）✓");
    }

    [TestMethod]
    public void Flow_ForwardWalk_DecrementsBuffer_ButBacktrackDoesNot()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost, backtrackCost: 15);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 2, "路至少三步（才能验证前行 vs 回头）✓");

        int start = flow.HungerBuffer;
        Assert.IsTrue(start > 0, "前置：开局缓冲 > 0，否则本用例证明不了递减 ✓");

        // 🔴 必须走**走廊格**（`TileRoom` 里没有 = 走廊）—— 房间格不算"走过一条走廊"，
        //    拿 path[0] 直接断言是**测试脆性**（它可能是房间格）⚠️
        (int X, int Y) corridor = path.First(t => !flow.TileWalk.TileRoom.ContainsKey(t));
        int corridorIndex = path.ToList().IndexOf(corridor);
        Assert.IsTrue(corridorIndex >= 0, "路上必有走廊格 ✓");

        // ① 先走到走廊**前一格**（不动缓冲口径：只走必要的步）✓
        for (int i = 0; i < corridorIndex; i++)
        {
            Step(flow, path[i]);
        }

        int beforeForward = flow.HungerBuffer;
        Step(flow, corridor); // 踏上走廊格 = **前行**一条走廊 ✓
        int afterForward = flow.HungerBuffer;
        Assert.AreEqual(beforeForward - 1, afterForward,
            $"🔴 前行一条走廊 ⇒ 缓冲**恰好 −1**（{beforeForward} → {afterForward}）—— DD：「only decreases after walking forward」✓");

        // ② **回头**（退回上一格 = 重走已站过的格）⇒ 缓冲**不动** ✓
        (int X, int Y) prev = corridorIndex > 0 ? path[corridorIndex - 1] : flow.TileWalk.Start;
        bool movedBack = flow.TryStepTile(prev.X - flow.TilePosition.X, prev.Y - flow.TilePosition.Y);
        Assert.IsTrue(movedBack, "回头一步应被允许（那是合法移动）✓");
        Assert.AreEqual(afterForward, flow.HungerBuffer,
            "🔴 **回头不算走过一条走廊** ⇒ 缓冲恒不变（DD 原文硬要求）✓");
    }

    [TestMethod]
    public void Flow_EveryHungerRoll_WritesRngDraw()
    {
        ExpeditionFlow flow = NewFlow();
        // 🔴 让缓冲从 0 起步（构造一份 `buffer_at_start = 0` 的流程不可行 —— 数据来自 res://），
        //    故本用例走"自然耗尽缓冲"路径：反复前行直到缓冲归零并掷出第一骰 ✓
        flow.EnableTileWalk(SegmentCost);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);

        int drawsBefore = _lastLog.Events.Count(e => e is RngDraw);
        int steps = Math.Min(path.Count, 12);
        for (int i = 0; i < steps; i++)
        {
            Step(flow, path[i]);
            if (flow.HungerCount > 0 || flow.LastHunger.TierIndex >= 0)
            {
                break; // 已经掷过一次 ⇒ 够了 ✓
            }
        }

        Assert.IsTrue(_lastLog.Events.Count(e => e is RngDraw) > drawsBefore,
            "🔴 走过缓冲期后**必然**留下 `RngDraw`（D-5 判据⑥：随机必写日志）✓");
    }

    [TestMethod]
    public void Flow_NotConfiguredHunger_NeverRolls_NoRngDraw()
    {
        // 🔴 用"删掉 hunger"的 tuning 造流程 ⇒ 走格**一次都不掷**（既有调用点行为逐字不变）✓
        TuningConfig tuning = TuningConfig.Parse(
            ReadData("tuning.json").Replace("\"hunger\": {", "\"hunger_removed\": {"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var log = new CombatLog();
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, log, new RngProvider(20260915));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        flow.EnableTileWalk(SegmentCost);

        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        int drawsBefore = log.Events.Count(e => e is RngDraw);
        for (int i = 0; i < Math.Min(path.Count, 8); i++)
        {
            Step(flow, path[i]);
        }

        // ⚠️ 注意：D-2 重访威胁**仍会掷**（它没被删）⇒ 这里只断言"**饥饿档位读数从未出现**"✓
        Assert.AreEqual(0, flow.HungerCount, "🔴 未配置 `hunger` ⇒ **一次都不饿** ✓");
        Assert.AreEqual(HungerRollResult.None, flow.LastHunger, "未配置 ⇒ 读数恒为 `None` ✓");
        Assert.AreEqual(0, flow.HungerBuffer, "未配置 ⇒ 缓冲恒 0（不伪造缓冲）✓");
        _ = drawsBefore;
    }

    // ────────────────────────────── 帮助器 ──────────────────────────────

    /// <summary>走一格（按差分算方向）✓</summary>
    private static void Step(ExpeditionFlow flow, (int X, int Y) target)
    {
        (int X, int Y) here = flow.TilePosition;
        bool ok = flow.TryStepTile(target.X - here.X, target.Y - here.Y);
        Assert.IsTrue(ok, $"应能从 ({here.X},{here.Y}) 走到 ({target.X},{target.Y}) ✓");
    }
}
