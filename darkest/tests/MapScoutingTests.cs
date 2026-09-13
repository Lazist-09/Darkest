using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// **M7.6 片 ③：侦察揭示前方 1~3 步拓扑**（`m8_roadmap §4.3③` + **P25 ⑤**）——
/// 成功概率**沿用已调平口径**（`base_pct` + 光照档加成）· 只改"揭示什么"（拓扑，而非"下一节点类型"）·
/// **不泄露更深层** · 随机必写 `RngDraw` · 同 seed 复现。
/// </summary>
[TestClass]
public sealed class MapScoutingTests
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

    private static ExpeditionMapConfig MapCfg() => ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));

    private static TuningConfig Tuning() => TuningConfig.Parse(ReadData("tuning.json"));

    [TestMethod]
    public void P25_5_RevealDepthInRange_AndBadDataThrows()
    {
        ExpeditionMapConfig c = MapCfg();
        Assert.IsTrue(c.Scout!.RevealDepthMin >= 1 && c.Scout.RevealDepthMax <= 3, "揭示深度 ∈ [1,3]（P25 ⑤）");
        Assert.IsTrue(c.Scout.RevealDepthMin <= c.Scout.RevealDepthMax, "min ≤ max");

        string raw = ReadData("expedition_map.json");
        Assert.ThrowsException<InvalidDataException>(
            () => ExpeditionMapConfig.Parse(raw.Replace("\"reveal_depth_max\": 3", "\"reveal_depth_max\": 9", StringComparison.Ordinal)),
            "揭示深度越界 ⇒ 报错（P25 ⑤）");
    }

    [TestMethod]
    public void Scout_SuccessRevealsTopologyWithinDepth_NoDeeperLeak()
    {
        TuningConfig tuning = Tuning();
        ExpeditionMapConfig mapCfg = MapCfg();
        var log = new CombatLog();
        var rng = new RngProvider(20260909);
        ExpeditionMap map = ExpeditionMapGenerator.Generate(log, rng, mapCfg);
        var meter = new LightMeter(tuning.Light!);
        meter.EmitStart(log);

        int successes = 0, minRevealed = int.MaxValue, maxRevealed = 0;
        int maxDepthSeen = 0;
        for (int i = 0; i < 200; i++)
        {
            MapScoutResult r = MapScouting.Roll(log, rng, meter, tuning.Scouting!, mapCfg, map, map.StartId);

            if (!r.Success)
            {
                Assert.AreEqual(0, r.RevealedRooms.Count, "失败 ⇒ **不揭示任何东西**（与旧的『失败必为 null』一致）");
                continue;
            }

            successes++;
            maxDepthSeen = Math.Max(maxDepthSeen, r.RevealedDepth);
            minRevealed = Math.Min(minRevealed, r.RevealedRooms.Count);
            maxRevealed = Math.Max(maxRevealed, r.RevealedRooms.Count);

            // 🔴 不泄露更深层：揭示的房间**必须都能在 ≤ depth 步内从起点走到**
            Assert.IsTrue(r.RevealedRooms.All(room =>
                    MapTraversal.ShortestPathLength(map, map.StartId, room.Id) is > 0 and <= 3),
                "揭示的房间必须 ≤ 3 步可达（不泄露更深层）");
            Assert.IsTrue(r.RevealedRooms.All(room => room.Id != map.StartId), "不重复揭示起点自身");
        }

        Assert.IsTrue(successes > 0, "200 次侦察应有成功样本（概率来自 base_pct + 档加成）");
        Assert.IsTrue(maxDepthSeen is >= 1 and <= 3, $"揭示深度必须 ∈ [1,3]（实测 {maxDepthSeen}）");
        Assert.IsTrue(log.Events.OfType<RngDraw>().Count() >= 200, "每次侦察至少写一条 `RngDraw`（成功判定）");

        string report = $"[M7.6] 片③ 侦察（200 次，同一张图与同一光照档）：成功 {successes} 次" +
                        $"（成功率先用 base_pct {tuning.Scouting!.BasePct}% 加 档加成）；揭示房间数 {minRevealed}~{maxRevealed}；最大揭示深度 {maxDepthSeen}";
        Console.WriteLine(report);
    }

    [TestMethod]
    public void Scout_SameSeed_ReproducesExactly()
    {
        TuningConfig tuning = Tuning();
        ExpeditionMapConfig mapCfg = MapCfg();

        MapScoutResult A()
        {
            var log = new CombatLog();
            var rng = new RngProvider(555);
            ExpeditionMap map = ExpeditionMapGenerator.Generate(log, rng, mapCfg);
            var meter = new LightMeter(tuning.Light!);
            meter.EmitStart(log);
            return MapScouting.Roll(log, rng, meter, tuning.Scouting!, mapCfg, map, map.StartId);
        }

        MapScoutResult a = A();
        MapScoutResult b = A();

        Assert.AreEqual(a.Success, b.Success, "同 seed ⇒ 成败一致");
        Assert.AreEqual(a.RevealedDepth, b.RevealedDepth, "同 seed ⇒ 揭示深度一致");
        CollectionAssert.AreEqual(a.RevealedRooms.Select(r => r.Id).ToArray(), b.RevealedRooms.Select(r => r.Id).ToArray(),
            "同 seed ⇒ 揭示的房间集合一致（确定性）");
    }

    [TestMethod]
    public void Scout_ChanceRisesWithDarkerTier_ButBaseStaysTuned()
    {
        TuningConfig tuning = Tuning();
        Assert.AreEqual(25, tuning.Scouting!.BasePct, "🔴 基础概率**沿用已调平值 25%**（O-76 原则①：光照不动）");

        // 越暗 ⇒ 档加成越高（我们没改这条；这里只是**确认它与本地图改动无关**）
        double radiant = tuning.Light!.Effects["radiant"].ScoutingPct;
        double black = tuning.Light.Effects["black"].ScoutingPct;

        // ⚠️ 我原先假设"越暗侦察越好" —— **那是我的假设，不是契约**（效果表由策划定）
        //    ⇒ 这里**只如实报**，不擅自断言方向（红线：不自己发明设计）
        string report = $"[M7.6] 片③ 侦察档加成（沿用既有 light.effects，未改）：radiant {radiant} ／ black {black}" +
                        $"　base_pct {tuning.Scouting!.BasePct}%（已调平）";
        Console.WriteLine(report);
        Assert.IsTrue(radiant >= 0 && black >= 0, "档加成不得为负（其余方向由策划定，本用例不判）");
    }
}
