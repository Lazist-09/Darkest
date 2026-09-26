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
/// **M7.6 片 ①：地图生成**（`m8_roadmap §4.3①` + **P25**）——
/// 房间 + 走廊 · 主干 6~8 · **必须连通** · **必须有分叉点** · 所有随机**必写 `RngDraw`** · **同 seed 复现**。
/// </summary>
[TestClass]
public sealed class ExpeditionMapTests
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

    private static ExpeditionMapConfig Cfg() => ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));

    [TestMethod]
    public void P25_ConfigValid_AndBadDataThrows()
    {
        ExpeditionMapConfig c = Cfg();
        Assert.IsTrue(c.Map.RoomCountMin >= 6 && c.Map.RoomCountMax <= 8, "房间数区间 ∈ [6,8]（P25 ①）");
        Assert.IsTrue(c.Map.MaxBranches >= 1, "至少允许一条分叉（P25 ③）");

        string raw = ReadData("expedition_map.json");
        Assert.ThrowsException<InvalidDataException>(
            () => ExpeditionMapConfig.Parse(raw.Replace("\"room_count_max\": 8", "\"room_count_max\": 12", StringComparison.Ordinal)),
            "房间数越界 ⇒ 报错（P25 ①）");
        Assert.ThrowsException<InvalidDataException>(
            () => ExpeditionMapConfig.Parse(raw.Replace("\"event_weight\": 40", "\"event_weight\": 0", StringComparison.Ordinal)),
            "某类房间权重为 0 ⇒ 报错（P25 ②）");
        Assert.ThrowsException<InvalidDataException>(
            () => ExpeditionMapConfig.Parse(raw.Replace("\"max_branches\": 2", "\"max_branches\": 0", StringComparison.Ordinal)),
            "不许分叉 ⇒ 报错（P25 ③：会退化成线性）");
    }

    /// <summary>
    /// 🔴 **报错文案里的 `branch_special_kind` 清单 = 校验用的那份**
    /// （防 `157_*.md` 那处"可钉未钉"）：
    ///   一个非法的 kind ⇒ 报错必须**列出 `SpecialBranchKinds` 的全部内容** ✓
    ///   若将来有人只改常量、不改文案 ⇒ 本用例红 ✓
    /// </summary>
    [TestMethod]
    public void P25_UnknownSpecialBranchKind_MessageListsAllKinds()
    {
        string raw = ReadData("expedition_map.json");
        // 🔴 `branch_special_kind` 只在 `branch_special_weight > 0` 时才校验（`ExpeditionMapConfig.cs:125`）
        //    ⇒ 而**出货数据该权重为 0** ⇒ 必须**同时**把它改成 >0，否则那条分支走不到（实测踩过）✓
        string bad = raw
            .Replace("\"branch_special_weight\": 0", "\"branch_special_weight\": 10", StringComparison.Ordinal)
            .Replace("\"branch_special_kind\": \"free_light\"",
                     "\"branch_special_kind\": \"bogus_kind\"", StringComparison.Ordinal);
        var ex = Assert.ThrowsException<InvalidDataException>(() => ExpeditionMapConfig.Parse(bad));

        // 🔴 断言**全角分隔**形式（插值产物）：手写若用半角 ` / ` ⇒ 不同形 ⇒ 能红 ✓
        //    （教训 `155_*.md`/`156_*.md`：断言与手写同形时会假过）✓
        string expected = string.Join(" ／ ", ExpeditionMapConfig.SpecialBranchKinds);
        StringAssert.Contains(ex.Message, expected,
            $"报错须含全角分隔的 `{expected}`（插值产物；防手写漂移）✓");
    }

    [TestMethod]
    public void Map_SpineSixToEight_Connected_AndRandomIsAudited()
    {
        ExpeditionMapConfig cfg = Cfg();
        var log = new CombatLog();
        var rng = new RngProvider(20260909);

        int forks = 0, spineMin = int.MaxValue, spineMax = 0, totalMin = int.MaxValue, totalMax = 0;
        const int n = 200;
        for (int i = 0; i < n; i++)
        {
            ExpeditionMap map = ExpeditionMapGenerator.Generate(log, rng, cfg);

            int spine = map.Rooms.Count(r => !r.IsBranch);
            Assert.IsTrue(spine is >= 6 and <= 8, $"主干房间数必须在 [6,8]（实测 {spine}；§4.3①）");
            Assert.IsTrue(map.IsConnected(), "🔴 生成后必须**全图连通**（起点可达每个房间）");
            Assert.AreEqual(0, map.StartId, "起点 = 0");
            Assert.AreEqual(spine - 1, map.GoalId, "终点 = 主干末房");

            forks += map.ForkCount >= 1 ? 1 : 0;
            spineMin = Math.Min(spineMin, spine);
            spineMax = Math.Max(spineMax, spine);
            totalMin = Math.Min(totalMin, map.RoomCount);
            totalMax = Math.Max(totalMax, map.RoomCount);
        }

        Assert.IsTrue(log.Events.OfType<RngDraw>().Count() > 0, "🔴 所有随机**必写 RngDraw**（红线）");
        Assert.IsTrue(forks >= (int)(n * 0.9), $"绝大多数地图必须有**分叉点**（实测 {forks}/{n}）");

        string report = $"[M7.6] 地图生成（{n} 张，同一条 RNG 流）：主干 {spineMin}~{spineMax} 间，含支路后总房间 {totalMin}~{totalMax} 间；" +
                        $"有分叉点的地图 {forks}/{n}（{100.0 * forks / n:F0}%）；`RngDraw` 共 {log.Events.OfType<RngDraw>().Count()} 条（可审计）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);
    }

    [TestMethod]
    public void Map_SameSeed_ReproducesExactly()
    {
        ExpeditionMapConfig cfg = Cfg();

        ExpeditionMap A()
        {
            var log = new CombatLog();
            return ExpeditionMapGenerator.Generate(log, new RngProvider(777), cfg);
        }

        ExpeditionMap a = A();
        ExpeditionMap b = A();

        Assert.AreEqual(a.RoomCount, b.RoomCount, "同 seed ⇒ 房间数一致（确定性）");
        Assert.AreEqual(a.Rooms.Count(x => x.Type == "battle"), b.Rooms.Count(x => x.Type == "battle"), "同 seed ⇒ 房间类型分布一致");
        CollectionAssert.AreEqual(
            a.Edges.Select(e => $"{e.From}>{e.To}").ToArray(),
            b.Edges.Select(e => $"{e.From}>{e.To}").ToArray(),
            "同 seed ⇒ 走廊完全一致");
    }

    [TestMethod]
    public void Map_HasBranchRooms_DeadEndsAreAllowed()
    {
        ExpeditionMapConfig cfg = Cfg();
        var log = new CombatLog();
        var rng = new RngProvider(4242);

        int mapsWithBranchRooms = 0;
        const int n = 100;
        for (int i = 0; i < n; i++)
        {
            ExpeditionMap map = ExpeditionMapGenerator.Generate(log, rng, cfg);
            if (map.BranchCount >= 1)
            {
                mapsWithBranchRooms++;

                // 支路房是**死路**（只连一条走廊）—— §4.3④「死路可能」
                foreach (MapRoom br in map.Rooms.Where(r => r.IsBranch))
                {
                    int deg = map.Edges.Count(e => e.From == br.Id || e.To == br.Id);
                    Assert.AreEqual(1, deg, $"支路房 {br.Id} 应是死路（度数 1，实测 {deg}）");
                }
            }
        }

        Assert.IsTrue(mapsWithBranchRooms > 0, "应有地图带支路房（死路）");
    }

    public TestContext TestContext { get; set; } = null!;
}
