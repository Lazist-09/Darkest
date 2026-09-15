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
/// 🔴🔴 **架构 `G4` 的判据**：走格光照**总消耗不得因格变多而暴涨** ——
/// 同一段走廊无论 **3 格**还是 **7 格**，扣的**总量必须都是 `segmentCost`（30）** ✓
/// （策划 `#338`① 的余数结转法：进段 acc=30 → 每格 floor(acc/剩余格数) → 余数结转 → 末格扣清 ✓）
/// </summary>
[TestClass]
public sealed class DungeonWalkLightTests
{
    private const int SegmentCost = 30; // 现值由调用方给（代码里不写死数字 ✓）

    private static (DungeonGrid Grid, Dictionary<(int X, int Y), int> Rooms) Row(string tiles, params int[] roomX)
    {
        var rooms = new Dictionary<(int X, int Y), int>();
        foreach (int x in roomX)
        {
            rooms[(x, 0)] = 100 + x; // 房间 id 用 x 编一个（用例内唯一即可 ✓）
        }

        return (DungeonGrid.Parse("t", new[] { tiles }), rooms);
    }

    private static (int X, int Y)[] PathFrom(int start, int end)
    {
        var list = new List<(int X, int Y)>();
        for (int x = start + 1; x <= end; x++)
        {
            list.Add((x, 0));
        }

        return list.ToArray();
    }

    [TestMethod]
    public void OneCorridor_ThreeTiles_TotalIsExactlySegmentCost()
    {
        (DungeonGrid grid, Dictionary<(int X, int Y), int> rooms) = Row("RCCCG", 0, 4);
        IReadOnlyList<int> plan = DungeonWalkLight.PlanPath(grid, rooms, (0, 0), PathFrom(0, 4), SegmentCost);
        Assert.AreEqual(30, plan.Sum(), "3 格走廊 ⇒ 总计 = 30 ✓");
        Assert.AreEqual(0, plan[^1], "进房间那一步**不扣光**（房间格）✓");
    }

    [TestMethod]
    public void LongerCorridor_SevenTiles_StillExactlySegmentCost_ThisIsG4()
    {
        (DungeonGrid grid, Dictionary<(int X, int Y), int> rooms) = Row("RCCCCCCCG", 0, 8);
        IReadOnlyList<int> plan = DungeonWalkLight.PlanPath(grid, rooms, (0, 0), PathFrom(0, 8), SegmentCost);
        Assert.AreEqual(30, plan.Sum(),
            "🔴 **7 格走廊 ⇒ 总计仍 = 30**（G4：总消耗不得因格变多而暴涨 ⚠️ —— 若有人改成'每格 −30'，这里立刻红）✓");
        Assert.AreEqual(8, plan.Count, "8 步（格 1..8；含进房间那一步 ✓ —— 我第一版写成 7，是**我的期望值**错，不是代码错）✓");
    }

    [TestMethod]
    public void TwoCorridors_TotalIsTwoSegments()
    {
        (DungeonGrid grid, Dictionary<(int X, int Y), int> rooms) = Row("RCCCRCCCG", 0, 4, 8);
        IReadOnlyList<int> plan = DungeonWalkLight.PlanPath(grid, rooms, (0, 0), PathFrom(0, 8), SegmentCost);
        Assert.AreEqual(60, plan.Sum(), "两段走廊 ⇒ 总计 = 2 × 30 ✓（与每段格数无关）✓");
    }

    [TestMethod]
    public void PathNotEndingInRoom_IsRejected()
    {
        (DungeonGrid grid, Dictionary<(int X, int Y), int> rooms) = Row("RCCCG", 0, 4);
        Assert.ThrowsException<ArgumentException>(
            () => DungeonWalkLight.PlanPath(grid, rooms, (0, 0), new[] { (1, 0), (2, 0) }, SegmentCost),
            "路径终点不在房间格 ⇒ **拒绝**（最后一段未结算会漂移；不静默扣个大概）✓");
    }

    [TestMethod]
    public void OnTheDerivedGrid_TotalScalesWithSegments_NotWithTileCount()
    {
        // 🔴 用**真实派生网格**复测 G4：走到终点的总扣光 = 30 × 跨过的段数，而**远小于** 30 × 格数 ✓
        ExpeditionMapConfig cfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));
        ExpeditionMap map = ExpeditionMapGenerator.Generate(new CombatLog(), new RngProvider(20260915), cfg);
        DungeonGridDeriver.Derived d = DungeonGridDeriver.Derive(map);

        var walker = new DungeonWalker(d.Grid, d.Start);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(d.Grid.Goal.X, d.Grid.Goal.Y);
        Assert.IsTrue(path.Count > 0, "派生网格上 start 必能到 goal ✓");

        IReadOnlyList<int> plan = DungeonWalkLight.PlanPath(d.Grid, d.TileRoom, d.Start, path, SegmentCost);

        // 段数 = 路径里"从房间跨进走廊"的次数 ✓
        int segments = 0;
        var cur = d.Start;
        foreach ((int X, int Y) next in path)
        {
            bool fromRoom = d.TileRoom.ContainsKey(cur);
            bool toRoom = d.TileRoom.ContainsKey(next);
            if (fromRoom && !toRoom)
            {
                segments++;
            }

            cur = next;
        }

        Assert.IsTrue(segments >= 1, "至少跨一段走廊 ✓");
        Assert.AreEqual(SegmentCost * segments, plan.Sum(),
            $"🔴 总扣光 = 30 × 段数（{segments}）—— **与格数（{path.Count}）无关** ✓");
        Assert.IsTrue(plan.Sum() < SegmentCost * path.Count / 2,
            $"G4：总扣光（{plan.Sum()}）必须**远小于**'每格 −30'那种暴涨（{SegmentCost * path.Count}）⚠️");
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
}
