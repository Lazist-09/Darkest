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
/// 🔴🔴 **`D-3` 三态揭示（`GridVision`）** —— 本批用例是 `D-3` 的**唯一判据**：
///
/// **DD 口径**（`dungeon_layer_design.md §F4`）：
///   · `Unexplored`（暗）= 从没「见过」 ⇒ **完全不知道那格存在或是什么** ✓
///   · `Scouted`（暗 + 亮轮廓）= **拓扑层侦察**揭示 ⇒ 知道「那里有个东西」，但**没看清内容** ✓
///   · `Visited`（浅灰）= **走过** 或 **格视野照到** ⇒ 内容已知 ✓
///   · 🔴 `#380`：**侦察 = 段级（能看多远）**、**视野 = 格级（能看清什么）** ⇒ **两条独立通道，互不加减** ✓
///
/// 🔴 与旧实现的关键差异：旧的 `_revealed` 是 `HashSet`（**二态**：「见过/没见过」），
///    并且 **`TryStep` 里 `_revealed.Add` 与 `_visited.Add` 恒同时置位**
///    ⇒ 「只被侦察到、从没走过」的格**无法表达** ⇒ 这是 `D-3` 要修的真缺陷 ✓
/// </summary>
[TestClass]
public sealed class GridVisionTests
{
    // 7×3：起点 (0,1)，终点 (6,1)；中间地板/门/战斗格；第 0/2 行是墙 ✓
    private static readonly string[] Rows =
    {
        "#######",
        "R.E!..G",
        "#######",
    };

    private static DungeonGrid NewGrid() => DungeonGrid.Parse("test://grid", Rows);

    private static DungeonGridVision Vision(bool revealOnEnter, int radius, int scoutBonus = 0)
        => new(revealOnEnter, radius, scoutBonus);

    // ── ① 三态的存在性与互斥性（内核真值）────────────────────────────────────────

    [TestMethod]
    public void RevealState_HasThreeDistinctValues_AndUnexploredIsTheDefault()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));

        Assert.AreEqual(RevealState.Visited, w.StateAt((0, 1), Vision(true, 0)),
            "起点：亲自站过 ⇒ **Visited**（不是「见过」那种弱态）✓");
        Assert.AreEqual(RevealState.Unexplored, w.StateAt((6, 1), Vision(true, 0)),
            "从没接触过 ⇒ **Unexplored** ✓");
    }

    [TestMethod]
    public void Scouted_IsDistinctFrom_UnexploredAndVisited()
    {
        // 🔴 `D-3` 的**核心判据**：三态必须**互相可分辨**
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));
        DungeonGridVision vis = Vision(revealOnEnter: true, radius: 0);

        // 只侦察（不走过去）⇒ Scouted ✓
        w.RevealScouted(new[] { (3, 1), (4, 1) });

        Assert.AreEqual(RevealState.Scouted, w.StateAt((3, 1), vis), "只被侦察到 ⇒ **Scouted** ✓");
        Assert.AreEqual(RevealState.Scouted, w.StateAt((4, 1), vis), "同上 ✓");
        Assert.AreEqual(RevealState.Unexplored, w.StateAt((5, 1), vis), "既没侦察也没走 ⇒ 仍是 **Unexplored** ✓");
        Assert.AreEqual(RevealState.Visited, w.StateAt((0, 1), vis), "站过的仍是 **Visited** ✓");

        // 🔴 三态**互相可分辨**（这才是 `D-3` 要的：旧实现里前两个都只会是「未见」 ⚠️）
        Assert.AreNotEqual(w.StateAt((3, 1), vis), w.StateAt((5, 1), vis), "Scouted ≠ Unexplored ✓");
        Assert.AreNotEqual(w.StateAt((3, 1), vis), w.StateAt((0, 1), vis), "Scouted ≠ Visited ✓");
    }

    [TestMethod]
    public void WalkingOverAScoutedTile_UpgradesItToVisited_NeverDowngrades()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));
        w.RevealScouted(new[] { (1, 1), (2, 1) });
        Assert.AreEqual(RevealState.Scouted, w.StateAt((1, 1), Vision(true, 0)), "先侦察 ⇒ Scouted ✓");

        Assert.IsTrue(w.TryStep(1, 0), "走到 (1,1) ✓");
        Assert.AreEqual(RevealState.Visited, w.StateAt((1, 1), Vision(true, 0)),
            "**走到 ⇒ 升为 Visited**（三态是**单向**的：Unexplored → Scouted → Visited）✓");

        // 🔴 反向**绝不允许**：Visited 之后再被侦察 ⇒ 仍是 Visited（不许降级）⚠️
        w.RevealScouted(new[] { (1, 1) });
        Assert.AreEqual(RevealState.Visited, w.StateAt((1, 1), Vision(true, 0)),
            "已 Visited 的格**不得**被侦察降级（否则「走过去的记忆」会被抹掉）⚠️");
    }

    // ── ② 格视野（`vision.reveal_on_enter` + `radius`）──────────────────────────

    [TestMethod]
    public void VisionRadius_RevealsOnlyWithinManhattanDistance_AndDoesNotWalk()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));
        DungeonGridVision vis = Vision(revealOnEnter: true, radius: 2);

        // 🔴 radius=2 ⇒ 曼哈顿距离 ≤ 2 的**可见格**被照到 ⇒ Visited（**看清了**）
        //    ⚠️ 本行是 1 维走廊（y 固定）⇒ 只有 x ∈ [0,2] 可达 ✓
        Assert.AreEqual(RevealState.Visited, w.StateAt((2, 1), vis), "距离 2 ⇒ 视野内 ⇒ Visited ✓");
        Assert.AreEqual(RevealState.Unexplored, w.StateAt((3, 1), vis), "距离 3 > radius 2 ⇒ 仍 Unexplored ✓");

        Assert.AreEqual(0, w.StepsTaken, "🔴 **视野不得计步**（「看见」 ≠ 「走过」）⚠️");
        Assert.AreEqual((0, 1), w.Position, "🔴 **视野不得移动队伍** ⚠️");
        Assert.AreEqual(1, w.Visited.Count, "🔴 **视野不得写进 `Visited` 集合**（否则回头代价会算错）⚠️");
    }

    [TestMethod]
    public void VisionOff_RevealOnEnter_KeepsEverythingUnexploredUntilSteppedOn()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));
        DungeonGridVision off = Vision(revealOnEnter: false, radius: 3);

        Assert.AreEqual(RevealState.Unexplored, w.StateAt((1, 1), off),
            "`reveal_on_enter: false` ⇒ **格视野关闭** ⇒ 只有走到的格才是 Visited ✓");
        Assert.AreEqual(RevealState.Visited, w.StateAt((0, 1), off), "但「站过的格」永远是 Visited（与视野无关）✓");
    }

    [TestMethod]
    public void VisionRadius_Zero_RevealsOnlyTheTileItself()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));
        DungeonGridVision vis = Vision(revealOnEnter: true, radius: 0);

        Assert.AreEqual(RevealState.Visited, w.StateAt((0, 1), vis), "脚下 ✓");
        Assert.AreEqual(RevealState.Unexplored, w.StateAt((1, 1), vis), "radius=0 ⇒ 邻居**不**被照到 ✓");
    }

    [TestMethod]
    public void VisionNeverRevealsThroughWalls()
    {
        // 起点 R 与 (2,1) 之间隔着墙 ⇒ **看不见**（DD：视野不穿墙）✓
        DungeonGrid g = DungeonGrid.Parse("t", new[] { "R#..G" });
        var w = new DungeonWalker(g, (0, 0));
        DungeonGridVision vis = Vision(revealOnEnter: true, radius: 4);

        Assert.AreEqual(RevealState.Unexplored, w.StateAt((2, 0), vis),
            "🔴 隔墙不能看见（否则玩家会「透视」迷宫）⚠️");
    }

    // ── ③ 侦察（段级）与视野（格级）**互不加减**（`#380`）──────────────────────

    [TestMethod]
    public void ScoutingAndVision_AreIndependentChannels_NeitherAddsToTheOther()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));

        // 侦察揭示很远（(5,1)），视野很短（radius 1）⇒ 两者**同时成立**、**互不抵消** ✓
        w.RevealScouted(new[] { (5, 1) });
        DungeonGridVision vis = Vision(revealOnEnter: true, radius: 1);

        Assert.AreEqual(RevealState.Scouted, w.StateAt((5, 1), vis),
            "🔴 **侦察的段级揭示不受视野 radius 限制**（`#380`：两条独立通道）✓");
        Assert.AreEqual(RevealState.Visited, w.StateAt((1, 1), vis),
            "🔴 **视野的格级揭示与侦察无关**（没侦察也照得到）✓");
        Assert.AreEqual(RevealState.Visited, w.StateAt((5, 1), Vision(revealOnEnter: true, radius: 5)),
            "半径够大时视野能覆盖侦察过的格 ⇒ 升为 Visited（**单向升级**，不是「抵消」）✓");
    }

    [TestMethod]
    public void ScoutBonus_DoesNotSilentlyExtendVisionRadius()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));

        // 🔴 `scout_bonus` 是**侦察**的加成（段级），**绝不允许**被当成视野半径用 ⚠️
        DungeonGridVision vis = Vision(revealOnEnter: true, radius: 1, scoutBonus: 5);
        Assert.AreEqual(RevealState.Unexplored, w.StateAt((3, 1), vis),
            "`scout_bonus` **不得**偷偷加进视野半径（否则就是「两份真值混用」）⚠️");
    }

    // ── ④ 展示值 == 消费值（纪律 V）────────────────────────────────────────────

    [TestMethod]
    public void RevealedCollection_ContainsExactlyScoutedAndVisited_AndNothingElse()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));
        w.RevealScouted(new[] { (3, 1), (4, 1) });

        // 集合口径 = 「至少 Scouted 过」（表现层画「雾」的**唯一**数据源）✓
        CollectionAssert.AreEquivalent(
            new[] { (0, 1), (3, 1), (4, 1) },
            w.Revealed.ToArray(),
            "`Revealed` = **Scouted ∪ Visited**（不含 Unexplored）✓");

        Assert.IsTrue(w.StateAt((3, 1)) >= RevealState.Scouted, "Scouted 满足「至少 Scouted」 ✓");
        Assert.IsFalse(w.StateAt((5, 1)) >= RevealState.Scouted, "Unexplored **不**满足 ✓");
        Assert.IsTrue(w.StateAt((0, 1)) >= RevealState.Visited, "Visited 满足「至少 Visited」 ✓");
        Assert.IsFalse(w.StateAt((3, 1)) >= RevealState.Visited, "Scouted **不**满足「至少 Visited」 ✓");
    }

    [TestMethod]
    public void ScoutHint_IsRecordedForTheAnswer_NotJustSilentlyApplied()
    {
        // 🔴 纪律 V（展示值 == 消费值）：侦察揭示必须**同时**能从只读面读到——
        //    "消费了但读不出来" = 无法自证（DD 复刻里最典型的静默失效）⚠️
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));

        Assert.AreEqual(0, w.ScoutedCount, "没侦察过 ⇒ 计数为 0 ✓");
        w.RevealScouted(new[] { (3, 1), (4, 1) });
        Assert.AreEqual(2, w.ScoutedCount, "侦察 2 格 ⇒ 计数 2（**只读面可自证**）✓");

        // 升级为 Visited 之后，它**不再**算 Scouted（三态是**互斥**的，不是叠加）✓
        Assert.IsTrue(w.TryStep(1, 0) && w.TryStep(1, 0) && w.TryStep(1, 0), "走到 (3,1) ✓");
        Assert.AreEqual(1, w.ScoutedCount, "升级掉 1 格 ⇒ 计数变 1（**互斥**）✓");
    }

    // ── ⑤ `D-1` 回归：回头代价仍按「站过」判，**不被侦察污染** ⚠️ ────────────────

    [TestMethod]
    public void RevisitDetection_StillUsesVisitedOnly_ScoutedTilesAreNotRevisits()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));
        w.RevealScouted(new[] { (1, 1) });

        Assert.IsFalse(w.HasVisited((1, 1)), "🔴 **侦察过 ≠ 站过**（`D-1` 回头代价的判定口）⚠️");

        Assert.IsTrue(w.TryStep(1, 0, out bool wasRevisit), "走到 (1,1) ✓");
        Assert.IsFalse(wasRevisit, "🔴 第一次走到「只被侦察过」的格 ⇒ **不算回头**（否则第一次探索就被多扣光）⚠️");
        Assert.IsTrue(w.HasVisited((1, 1)), "走过之后 ⇒ 站过 ✓");

        Assert.IsTrue(w.TryStep(-1, 0), "退回 (0,1) ✓");
        Assert.IsTrue(w.TryStep(1, 0, out bool second), "再走回 (1,1) ✓");
        Assert.IsTrue(second, "🔴 这次**是**回头（已站过）⇒ `D-1` 仍生效 ✓");
    }

    // ── ⑥ 确定性 & 越界安全 ───────────────────────────────────────────────────

    [TestMethod]
    public void StateAt_IsSafeOutOfBounds_AndDeterministic()
    {
        DungeonGrid g = NewGrid();
        var w = new DungeonWalker(g, (0, 1));
        DungeonGridVision vis = Vision(true, 1);

        Assert.AreEqual(RevealState.Unexplored, w.StateAt((-5, -5), vis), "越界 ⇒ Unexplored（不抛）✓");
        Assert.AreEqual(RevealState.Unexplored, w.StateAt((99, 99), vis), "越界 ⇒ Unexplored（不抛）✓");
        Assert.AreEqual(RevealState.Unexplored, w.StateAt((0, 0), vis), "墙 ⇒ 永不揭示（DD：墙不画）✓");

        Assert.AreEqual(w.StateAt((1, 1), vis), w.StateAt((1, 1), vis), "同输入同结果 ⇒ **确定性** ✓");
    }

    // ── ⑦ 配置侧：`vision` 契约的加载期校验（fail-fast）────────────────────────

    [TestMethod]
    public void Vision_NegativeRadius_RejectedAtLoadTime()
    {
        string json = """
        {
          "width": 5, "height": 1,
          "tiles": [ "R..!G" ],
          "start": { "x": 0, "y": 0 },
          "goal": { "x": 4, "y": 0 },
          "vision": { "reveal_on_enter": true, "radius": -1 }
        }
        """;

        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(json), "`radius < 0` ⇒ 加载期拒绝（不静默取绝对值）✓");
    }

    [TestMethod]
    public void Vision_NegativeScoutBonus_RejectedAtLoadTime()
    {
        string json = """
        {
          "width": 5, "height": 1,
          "tiles": [ "R..!G" ],
          "start": { "x": 0, "y": 0 },
          "goal": { "x": 4, "y": 0 },
          "vision": { "reveal_on_enter": true, "radius": 1, "scout_bonus": -3 }
        }
        """;

        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(json),
            "`scout_bonus < 0` ⇒ 拒绝（负加成会让侦察**永远失败**，是静默失效）⚠️");
    }

    [TestMethod]
    public void Vision_RadiusTooLarge_RejectedAtLoadTime()
    {
        // 🔴 上界校验：半径 ≥ 图的最长边 ⇒ 开局**整张图全亮** ⇒ 探索层被架空（静默失去玩法）⚠️
        string json = """
        {
          "width": 5, "height": 1,
          "tiles": [ "R..!G" ],
          "start": { "x": 0, "y": 0 },
          "goal": { "x": 4, "y": 0 },
          "vision": { "reveal_on_enter": true, "radius": 99 }
        }
        """;

        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(json),
            "`radius` ≥ 图边长 ⇒ 开局全亮 ⇒ 加载期拒绝（探索层不许被静默架空）⚠️");
    }
}
