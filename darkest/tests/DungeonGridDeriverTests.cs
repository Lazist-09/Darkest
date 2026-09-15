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
/// 🔴 **阶段 3 派生桥**（架构 `P30` ③ 认可的"内容零返工"路径）：现有【房间+连线】图 ⇒ 瓷砖网格。
/// 判据：
///   · **可达性**：派生网格里 `start` 必须能走到 `goal`（否则玩家一开局就卡死 ⚠️）
///   · **确定性**：同一张图派生两次 ⇒ 逐行完全相同（否则冒烟/回放不可复现）
///   · **房间映射**：每间房中心是**该房间类型**的瓷砖；`TileRoom` 覆盖整块（内容引用不变的关键）
/// </summary>
[TestClass]
public sealed class DungeonGridDeriverTests
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

    private static ExpeditionMap NewMap(long seed)
    {
        ExpeditionMapConfig cfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));
        return ExpeditionMapGenerator.Generate(new CombatLog(), new RngProvider(seed), cfg);
    }

    [TestMethod]
    public void Derived_StartCanReachGoal_AndEveryRoomCenterIsWalkable()
    {
        ExpeditionMap map = NewMap(20260915);
        DungeonGridDeriver.Derived d = DungeonGridDeriver.Derive(map);

        var walker = new DungeonWalker(d.Grid, d.Start);
        Assert.AreEqual(map.StartId == map.GoalId ? 0 : 1, d.Start == d.Grid.Goal ? 0 : 1,
            "起点与终点是不同房间（生成器保证）✓");
        Assert.IsTrue(walker.PathTo(d.Grid.Goal.X, d.Grid.Goal.Y).Count > 0,
            "🔴 派生后 `start` 必须能走到 `goal`（否则一开局就卡死）✓");
        Assert.AreEqual(DungeonTileKind.Goal, d.Grid.TileAt(d.Grid.Goal.X, d.Grid.Goal.Y), "终点格是 `G` ✓");

        foreach (MapRoom room in map.Rooms)
        {
            (int cx, int cy) = d.RoomCenters[room.Id];
            Assert.IsTrue(DungeonTileMap.IsWalkable(d.Grid.TileAt(cx, cy)),
                $"房间 {room.Id} 的中心 ({cx},{cy}) 必须可通行（它是落脚点）✓");
        }
    }

    [TestMethod]
    public void Derived_IsDeterministic_AndShapeFollowsDepthAndCount()
    {
        ExpeditionMap map = NewMap(4242);
        DungeonGridDeriver.Derived a = DungeonGridDeriver.Derive(map);
        DungeonGridDeriver.Derived b = DungeonGridDeriver.Derive(map);
        CollectionAssert.AreEqual(a.Grid.ToRows().ToArray(), b.Grid.ToRows().ToArray(),
            "同一张图派生两次 ⇒ **逐行相同**（确定性）✓");

        int stride = DungeonGridDeriver.RoomSize + DungeonGridDeriver.RoomGap;
        int maxDepth = map.Rooms.Max(r => r.Depth);
        int maxPerDepth = map.Rooms.GroupBy(r => r.Depth).Max(g => g.Count());
        Assert.AreEqual((maxDepth + 1) * stride, a.Grid.Width, "宽度 = (最大深度+1) × (房间边长+间隔) ✓");
        Assert.AreEqual(maxPerDepth * stride, a.Grid.Height, "高度 = 同深度最多房间数 × (房间边长+间隔) ✓");
    }

    [TestMethod]
    public void RoomType_MapsToTileKind_AndTileRoomCoversTheWholeBlock()
    {
        ExpeditionMap map = NewMap(7);
        DungeonGridDeriver.Derived d = DungeonGridDeriver.Derive(map);

        foreach (MapRoom room in map.Rooms)
        {
            (int cx, int cy) = d.RoomCenters[room.Id];
            DungeonTileKind expected = room.Id == map.GoalId
                ? DungeonTileKind.Goal // 终点房间中心被改写为 `G` ✓
                : DungeonGridDeriver.KindForRoomType(room.Type);
            Assert.AreEqual(expected, d.Grid.TileAt(cx, cy),
                $"房间 {room.Id}（类型 {room.Type}）中心应为 {expected} ✓");

            // 3×3 块内每一格都归属该房间 ⇒ **内容引用零改动**的关键 ✓
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (!d.Grid.InBounds(x, y))
                    {
                        continue;
                    }

                    Assert.AreEqual(room.Id, d.TileRoom[(x, y)], $"({x},{y}) 应归属房间 {room.Id} ✓");
                }
            }
        }
    }

    [TestMethod]
    public void UnknownRoomType_FallsBackToPlainRoom_NotToBattle()
    {
        // 结构性映射：未登记类型 ⇒ `R`（**不静默当成战斗** ⚠️）✓
        Assert.AreEqual(DungeonTileKind.Room, DungeonGridDeriver.KindForRoomType("mystery"));
        Assert.AreEqual(DungeonTileKind.Battle, DungeonGridDeriver.KindForRoomType("battle"));
        Assert.AreEqual(DungeonTileKind.Event, DungeonGridDeriver.KindForRoomType("event"));
        Assert.AreEqual(DungeonTileKind.Camp, DungeonGridDeriver.KindForRoomType("camp"));
        Assert.AreEqual(DungeonTileKind.Curio, DungeonGridDeriver.KindForRoomType("treasure"));
    }
}
