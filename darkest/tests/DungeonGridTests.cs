using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **地牢瓷砖网格**（用户 2026-09-15 裁定 (B)：瓷砖网格自由走）的**规则模型**用例。
/// 本批**只落结构**（网格/四向走/寻路/揭示），**遭遇与视野规则按用户要求"暂留"** ⇒ 用例里也**不含**它们 ✓
/// </summary>
[TestClass]
public sealed class DungeonGridTests
{
    // 5×3 示例：起点 (0,1) ⇒ 终点 (4,1)；中间有门与战斗格；第 0 行是墙 ✓
    private static readonly string[] Rows =
    {
        "#####",
        "R.E!G",
        "#####",
    };

    private static DungeonGrid NewGrid() => DungeonGrid.Parse("test://grid", Rows);

    [TestMethod]
    public void Parse_RejectsRaggedRows_AndMissingOrMultipleGoals()
    {
        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGrid.Parse("t", new[] { "###", "##" }), "行不等长 ⇒ 拒绝（否则网格错位）✓");
        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGrid.Parse("t", new[] { "..#" }), "没有 `G` ⇒ 拒绝 ✓");
        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGrid.Parse("t", new[] { "G.G" }), "多处 `G` ⇒ 拒绝（终点必须唯一）✓");
    }

    [TestMethod]
    public void Parse_MapsCharsToKinds_AndRoundTrips()
    {
        DungeonGrid g = NewGrid();
        Assert.AreEqual(5, g.Width);
        Assert.AreEqual(3, g.Height);
        Assert.AreEqual((4, 1), g.Goal, "终点位置 ✓");
        Assert.AreEqual(DungeonTileKind.Wall, g.TileAt(0, 0), "`#` ⇒ 墙 ✓");
        Assert.AreEqual(DungeonTileKind.Battle, g.TileAt(3, 1), "`!` ⇒ 战斗格 ✓");
        CollectionAssert.AreEqual(Rows, g.ToRows().ToArray(), "字符 ⇔ 枚举**往返一致**（留档可读）✓");
        Assert.AreEqual(DungeonTileKind.Wall, DungeonTileMap.FromChar('☃'), "未登记字符 ⇒ **当墙**（不静默当地板）✓");
    }

    [TestMethod]
    public void Walker_StepsFourWays_ButNeverIntoWallOrOutOfBounds()
    {
        var w = new DungeonWalker(NewGrid(), (0, 1));
        Assert.AreEqual(DungeonTileKind.Room, w.CurrentTile, "起点在房间格 ✓");

        Assert.IsTrue(w.TryStep(1, 0), "向右（地板）应可走 ✓");
        Assert.AreEqual(1, w.StepsTaken, "走一格 = 一步 ✓");

        Assert.IsFalse(w.TryStep(0, -1), "向上是墙 ⇒ 拒绝 ✓");
        Assert.IsFalse(w.TryStep(0, 1), "向下越界 ⇒ 拒绝 ✓");
        Assert.AreEqual((1, 1), w.Position, "被拒的移动**不得**改变状态 ✓");
        Assert.AreEqual(1, w.StepsTaken, "被拒的移动**不得**计步 ✓");
        Assert.IsFalse(w.Revealed.Contains((1, 0)), "被拒的移动**不得**揭示 ✓");
    }

    [TestMethod]
    public void Walker_RevealsOnEnter_AndReachesGoal()
    {
        var w = new DungeonWalker(NewGrid(), (0, 1));
        Assert.IsTrue(w.Revealed.Contains((0, 1)), "起点即揭示 ✓");
        Assert.IsFalse(w.HasReachedGoal, "起点不是终点 ✓");

        IReadOnlyList<(int X, int Y)> path = w.PathTo(4, 1);
        Assert.AreEqual(4, path.Count, "到终点 4 步（含终点、不含起点）✓");
        foreach ((int x, int y) in path)
        {
            Assert.IsTrue(w.TryStep(x - w.Position.X, y - w.Position.Y), $"沿路径走 ({x},{y}) ✓");
        }

        Assert.IsTrue(w.HasReachedGoal, "应到终点 ✓");
        Assert.AreEqual(DungeonTileKind.Goal, w.CurrentTile, "脚下是终点格 ✓");
        Assert.AreEqual(5, w.Revealed.Count, "走过的 5 格全揭示（起点+4 步）✓");
    }

    [TestMethod]
    public void PathTo_IsDeterministic_AndEmptyWhenUnreachable()
    {
        var a = new DungeonWalker(NewGrid(), (0, 1));
        var b = new DungeonWalker(NewGrid(), (0, 1));
        CollectionAssert.AreEqual(a.PathTo(4, 1).ToArray(), b.PathTo(4, 1).ToArray(),
            "四向顺序固定 ⇒ 寻路**确定性**（同输入同路径）✓");

        Assert.AreEqual(0, a.PathTo(0, 0).Count, "目标是墙 ⇒ 空（不抛）✓");
        Assert.AreEqual(0, a.PathTo(99, 99).Count, "越界 ⇒ 空（不抛）✓");

        // 被墙隔开的区域 ⇒ 不可达 ⇒ 空 ✓
        DungeonGrid split = DungeonGrid.Parse("t", new[] { "R#G" });
        Assert.AreEqual(0, new DungeonWalker(split, (0, 0)).PathTo(2, 0).Count, "墙隔开 ⇒ 不可达 ⇒ 空 ✓");
    }
}
