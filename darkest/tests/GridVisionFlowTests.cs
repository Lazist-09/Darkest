using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴🔴 **`D-3` 流程级：`vision` 契约接线**（`GridVision` 的**消费点**）。
///
/// 为什么必须有这一批：`DungeonGridVision` 记录（`reveal_on_enter` / `radius` / `scout_bonus`）
/// **在本批之前全仓无消费点**（只有加载期 `radius ≥ 0` 校验）⇒ 典型的「填了但没生效"
/// （红线 21 / `P29` ① 静默默认）⚠️
///
/// 🔴 数据载体：`dungeon_grid.json` **不存在**（仍在派生图阶段）⇒ `vision` 走 **`tuning.dungeon_layer.vision`**
///    （与 `D-1` 的 `backtrack` ／ `D-5` 的 `hunger` 同一位置、同一纪律）✓
///
/// 判据（`dungeon_layer_design.md §F4`）：
///   · 未配置 `vision` ⇒ **行为逐字不变**（不照、不侦察、不写日志）—— opt-in ✓
///   · `reveal_on_enter: true` + `radius: N` ⇒ 落格后**曼哈顿距离 ≤ N** 的可见格 ⇒ `Visited`
///   · 「看见」**不计步、不移动、不写 `Visited` 集合**（否则 `D-1` 回头代价会算错）⚠️
///   · UI 快照（`FromTileWalk`）**必须能区分三态**（展示值 == 消费值，纪律 V）✓
/// </summary>
[TestClass]
public sealed class GridVisionFlowTests
{
    private const int SegmentCost = 30;

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

    /// <summary>构造流程；`vision` 由调用方**显式注入**（未给 ⇒ 关闭 ⇒ 既有行为不变）✓</summary>
    private static ExpeditionFlow NewFlow(int? visionRadius = null, bool revealOnEnter = false, int scoutBonus = 0)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        tuning = tuning with
        {
            DungeonLayer = (tuning.DungeonLayer ?? new TuningDungeonLayer()) with
            {
                // 🔴 `visionRadius` 显式给了 ⇒ 用给的；**没给 ⇒ 显式置 null**（关掉）——
                //    绝不能"不给就沿用 data 里的值"：那样用例会随出货数据漂移（`D-2` 踩过的坑）⚠️
                Vision = visionRadius is { } r ? new TuningGridVision(revealOnEnter, r, scoutBonus) : null,
            },
        };
        return BuildFlow(tuning);
    }

    /// <summary>🔴 `D-3`：**显式**构造"未配置 vision"（不依赖 data 现状；出货数据里它是开着的）✓</summary>
    private static ExpeditionFlow NewFlowWithVisionExplicitlyNull() => NewFlow();

    private static ExpeditionFlow BuildFlow(TuningConfig tuning)
    {
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, new CombatLog(), new RngProvider(20260915));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }

    // ── ① opt-in：未配置 ⇒ 行为逐字不变 ────────────────────────────────────────

    [TestMethod]
    public void VisionNotConfigured_IsInert_AndStateAtFallsBackToWalkOnly()
    {
        // 🔴 `vision` **未注入**（用户/测试选择关掉）⇒ 不接线、退化成二态 ✓
        //    ⚠️ 注意：**出货数据里 `tuning.dungeon_layer.vision` 是开着的**（radius 2）——
        //       它是"本次交付的默认"，不是"硬编码默认值"（改 tuning 即改行为）✓
        //       故本用例必须**显式**构造"未配置"，而不是依赖 data 里的现状（否则一改数据用例就红）⚠️
        ExpeditionFlow flow = NewFlowWithVisionExplicitlyNull();
        flow.EnableTileWalk(SegmentCost);

        Assert.IsNull(flow.TileVision, "未配置 `vision` ⇒ 不接线（**不是**默认开一个半径）⚠️");
        Assert.AreEqual(RevealState.Visited, flow.TileStateAt(flow.TilePosition),
            "脚下永远是 Visited ✓");
        Assert.AreEqual(1, flow.RevealedTileCount, "未配置 ⇒ 只有「走到的格」被揭示（旧行为逐字不变）✓");
    }

    // ── ② 格视野真的接线了 ────────────────────────────────────────────────────

    [TestMethod]
    public void VisionEnabled_MarksNeighboursAsVisited_WithoutSteppingOrSpendingLight()
    {
        ExpeditionFlow flow = NewFlow(visionRadius: 1, revealOnEnter: true);
        flow.EnableTileWalk(SegmentCost);

        int lightBefore = flow.Meter.Value;
        (int X, int Y) pos = flow.TilePosition;
        int steps = flow.TileStepsTaken;
        int visitedCollection = flow.VisitedTileCount; // 🔴 「站过」集合（`D-1` 判定口）

        Assert.IsNotNull(flow.TileVision, "已配置 ⇒ 接线 ✓");
        Assert.AreEqual(1, flow.TileVision!.Radius, "半径照配置来（**不放大**）✓");

        // 起点是 3×3 房间中心 ⇒ 四邻居都是房间格 ⇒ radius 1 应把它们都照到 ✓
        var neighbours = new[] { (pos.X, pos.Y - 1), (pos.X, pos.Y + 1), (pos.X - 1, pos.Y), (pos.X + 1, pos.Y) }
            .Where(p => flow.TileWalk!.Grid.InBounds(p.Item1, p.Item2)
                        && flow.TileWalk.Grid.TileAt(p.Item1, p.Item2) != DungeonTileKind.Wall)
            .ToArray();
        Assert.IsTrue(neighbours.Length > 0, "起点应有至少一个可走邻居（否则本用例无意义）✓");

        foreach ((int nx, int ny) in neighbours)
        {
            Assert.AreEqual(RevealState.Visited, flow.TileStateAt((nx, ny)),
                $"视野半径 1 ⇒ 邻居 ({nx},{ny}) 应被照到 ⇒ Visited ✓");
            Assert.IsFalse(flow.VisitedTileAt((nx, ny)),
                $"🔴 **「照到」不得写进 `Visited` 集合**（否则 `D-1` 第一次走到那里会被当成「回头」多扣光）⚠️");
        }

        Assert.AreEqual(steps, flow.TileStepsTaken, "🔴 看见 ≠ 走过：**不计步** ✓");
        Assert.AreEqual(pos, flow.TilePosition, "🔴 看见 ≠ 移动：**队伍没动** ✓");
        Assert.AreEqual(lightBefore, flow.Meter.Value, "🔴 看见 ≠ 消耗：**不扣光** ✓");
        Assert.AreEqual(visitedCollection, flow.VisitedTileCount, "🔴 `Visited` 集合**一格未增** ✓");
    }

    [TestMethod]
    public void VisionEnabled_DoesNotChangeTheLightEconomy_OfTheWholeWalk()
    {
        // 🔴 回归：接上视野之后，「走完全程扣光」必须与不接视野时**一模一样**（视野不参与光照）⚠️
        int TotalLightSpent(bool vision)
        {
            ExpeditionFlow flow = vision ? NewFlow(visionRadius: 2, revealOnEnter: true) : NewFlow();
            flow.EnableTileWalk(SegmentCost);
            int before = flow.Meter.Value;
            DungeonGrid grid = flow.TileWalk!.Grid;
            var walker = new DungeonWalker(grid, flow.TilePosition);
            foreach ((int X, int Y) next in walker.PathTo(grid.Goal.X, grid.Goal.Y))
            {
                flow.TryStepTile(next.X - flow.TilePosition.X, next.Y - flow.TilePosition.Y);
            }

            return before - flow.Meter.Value;
        }

        Assert.AreEqual(TotalLightSpent(false), TotalLightSpent(true),
            "🔴 **视野不得改动光照经济**（`#380`：两通道独立 ⇒ 视野既不便宜也不加价）⚠️");
    }

    // ── ③ 侦察（段级）接线 ────────────────────────────────────────────────────

    [TestMethod]
    public void ScoutedTiles_AreDistinctFromVisited_InTheFlowReadouts()
    {
        ExpeditionFlow flow = NewFlow(visionRadius: 0, revealOnEnter: true);
        flow.EnableTileWalk(SegmentCost);

        // 挑一格「远处、从没走过」的可走格 ⇒ 手动按侦察口径揭示 ✓
        (int X, int Y) far = FirstFarWalkableTile(flow, minDistance: 3);
        Assert.AreEqual(RevealState.Unexplored, flow.TileStateAt(far), "侦察**之前** ⇒ 未知 ✓");

        flow.RevealScoutedTiles(new[] { far });

        Assert.AreEqual(RevealState.Scouted, flow.TileStateAt(far), "侦察**之后** ⇒ **Scouted**（不是 Visited）✓");
        Assert.IsFalse(flow.VisitedTileAt(far), "🔴 `D-1`：**侦察过 ≠ 站过**（回头代价判定口不变）⚠️");
        Assert.AreNotEqual(RevealState.Unexplored, flow.TileStateAt(far), "但属于「已揭示」（画雾时不该盖住它）✓");

        Assert.AreEqual(1, flow.ScoutedTileCount, "侦察计数 = 1（**只读面可自证**，纪律 V）✓");

        // 走过去 ⇒ 升级为 Visited（单向）✓
        var walker = new DungeonWalker(flow.TileWalk!.Grid, flow.TilePosition);
        foreach ((int X, int Y) next in walker.PathTo(far.Item1, far.Item2))
        {
            Assert.IsTrue(flow.TryStepTile(next.X - flow.TilePosition.X, next.Y - flow.TilePosition.Y),
                $"沿路径走到 ({next.X},{next.Y}) ✓");
        }

        Assert.AreEqual(RevealState.Visited, flow.TileStateAt(far), "走到 ⇒ 升为 Visited ✓");
        Assert.IsTrue(flow.VisitedTileAt(far), "`D-1`：现在才「站过」 ✓");
        Assert.AreEqual(0, flow.ScoutedTileCount, "升级后**不再**算 Scouted（三态互斥）✓");
    }

    [TestMethod]
    public void RevealScoutedTiles_NotEnabled_IsIgnored_NotSilentlyApplied()
    {
        // 🔴 未开走格 ⇒ 没有 `_tileWalker` ⇒ 侦察揭示**无处可落** ⇒ 必须**明确忽略**（不抛、不静默半生效）✓
        ExpeditionFlow flow = NewFlow();
        Assert.IsFalse(flow.TileWalkEnabled, "未开走格 ✓");

        flow.RevealScoutedTiles(new[] { (0, 0) }); // 不崩 ✓
        Assert.AreEqual(0, flow.ScoutedTileCount, "未开走格 ⇒ 侦察揭示**不生效**（且有读数可自证）✓");
        Assert.AreEqual(0, flow.RevealedTileCount, "也**不**会凭空多出揭示格 ✓");
    }

    // ── ④ 展示值 == 消费值（纪律 V）────────────────────────────────────────────
    //
    //   🔴 `D-3` 的 **UI 侧（`WalkMapView` 三态配色 / 文字速写字形）不在此工程验** ——
    //      `Darkest.Tests.csproj` 是**零 Godot 内核工程**（只直编 `core`/`gameplay/sim`/`data`），
    //      `Darkest.UI` 引用 Godot ⇒ 编不进来（这是**刻意的**，CI 无 Godot 也能跑）✓
    //      ⇒ UI 侧由 `tools/dsh/smoke.ps1` 的 `--tile-walk` 冒烟验（画面/文字速写落地看得到）✓
    //      本处只验**内核向表现层提供的三态数据是否完整可读**（这才是"能不能画"的前提）✓

    [TestMethod]
    public void CoreExposesAllThreeStates_SoTheUiCanActuallyDrawThem()
    {
        ExpeditionFlow flow = NewFlow(visionRadius: 1, revealOnEnter: true);
        flow.EnableTileWalk(SegmentCost);

        (int X, int Y) far = FirstFarWalkableTile(flow, minDistance: 3);
        flow.RevealScoutedTiles(new[] { far });

        // 🔴 内核必须**同时**提供三种状态的格（否则 UI 想画三态也无从下手）✓
        DungeonGrid g = flow.TileWalk!.Grid;
        var states = new HashSet<RevealState>();
        for (int y = 0; y < g.Height; y++)
        {
            for (int x = 0; x < g.Width; x++)
            {
                if (g.TileAt(x, y) == DungeonTileKind.Wall)
                {
                    continue;
                }

                states.Add(flow.TileStateAt((x, y)));
            }
        }

        Assert.IsTrue(states.Contains(RevealState.Unexplored), "有未知格 ✓");
        Assert.IsTrue(states.Contains(RevealState.Scouted), "有「只有轮廓」的格 ✓");
        Assert.IsTrue(states.Contains(RevealState.Visited), "有「已看清」的格 ✓");

        // 🔴 三态读数的**可加性自证**：已揭示 = 站过 + 只被侦察到（不多不少）✓
        Assert.AreEqual(flow.VisitedTileCount + flow.ScoutedTileCount, flow.RevealedTileCount,
            "`已揭示` 必须**恰好** = `站过` + `只侦察过`（展示值与消费值同源，纪律 V）✓");
    }

    // ── ⑤ 与跳/墙的交互：视野不改变"能不能走" ───────────────────────────────

    [TestMethod]
    public void VisionDoesNotChangeWhatsWalkable()
    {
        // 🔴 视野是**只读**的 ⇒ 开启前后"墙仍然走不进去"必须完全一致 ✓
        ExpeditionFlow flow = NewFlow(visionRadius: 3, revealOnEnter: true);
        flow.EnableTileWalk(SegmentCost);

        DungeonGrid g = flow.TileWalk!.Grid;
        (int px, int py) = flow.TilePosition;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) != 1)
                {
                    continue;
                }

                (int X, int Y) t = (px + dx, py + dy);
                bool walkable = g.InBounds(t.X, t.Y) && g.TileAt(t.X, t.Y) != DungeonTileKind.Wall;
                if (walkable)
                {
                    continue;
                }

                Assert.AreEqual(RevealState.Unexplored, flow.TileStateAt(t),
                    $"🔴 墙 ({t.X},{t.Y}) 即使「在视野半径内」也**不得**被揭示（视野不穿墙）⚠️");
            }
        }
    }

    // ── 工具 ──────────────────────────────────────────────────────────────────

    /// <summary>找一格「距离当前位置 ≥ `minDistance`、从没走过、非墙」的格（确定性：按 y 再 x 扫）✓</summary>
    private static (int X, int Y) FirstFarWalkableTile(ExpeditionFlow flow, int minDistance)
    {
        DungeonGrid g = flow.TileWalk!.Grid;
        (int px, int py) = flow.TilePosition;
        for (int y = 0; y < g.Height; y++)
        {
            for (int x = 0; x < g.Width; x++)
            {
                if (g.TileAt(x, y) == DungeonTileKind.Wall)
                {
                    continue;
                }

                if (flow.VisitedTileAt((x, y)))
                {
                    continue;
                }

                if (Math.Abs(px - x) + Math.Abs(py - y) >= minDistance)
                {
                    return (x, y);
                }
            }
        }

        throw new InvalidOperationException("找不到足够远的格（布局变了？）⇒ 用例失效。");
    }
}
