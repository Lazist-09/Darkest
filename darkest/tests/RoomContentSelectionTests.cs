using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **片 C 验收**（`tasks/merged_content_layer_pack.md` §3 片 C）：**Curio 由【内容表】决定**（引用，不搬家）——
/// · 候选池 = `roomType` 行 ∪（支路房）`branch` 行；按**组内权重**抽取
/// · 🔴 **写 `RngDraw`**（随机留痕：可审计、可复现）
/// · 池为空 ⇒ **返回 `null`**（调用方走回退，**不静默造一个**）
/// · `expedition_nodes.json` **原样保留**（B5）· 未接线 kind 不进白名单（B6，沿用 CurioResolver/CuriosConfig）✓
/// </summary>
[TestClass]
public sealed class RoomContentSelectionTests
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

    private static (ExpeditionFlow Flow, CombatLog Log) NewFlow(int seed)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log,
            new Darkest.Core.Rng.RngProvider(seed));
        return (flow, log);
    }

    [TestMethod]
    public void ShippedTable_EventRoom_PicksOnlyFromTheEventPool_AndWritesRngDraw()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        RoomContentsConfig contents = RoomContentsConfig.Parse(ReadData("room_contents.json"), curios);
        string[] pool = contents.ForType("event").SelectMany(e => e.CurioPool ?? Array.Empty<string>()).ToArray();

        (ExpeditionFlow flow, CombatLog log) = NewFlow(20260909);
        for (int i = 0; i < 30; i++)
        {
            string? picked = flow.PickCurioForRoom(contents, "event", isBranch: false);
            Assert.IsNotNull(picked, "event 房应当总能取到内容（出厂表给了池）");
            Assert.IsTrue(pool.Contains(picked), $"抽到的 \"{picked}\" 必须在 event 行的池里");
        }

        Assert.AreEqual(30, log.Events.OfType<RngDraw>().Count(),
            "🔴 每次抽取都要写 RngDraw（随机留痕：可审计、可复现）");
    }

    [TestMethod]
    public void BranchRoom_MergesTheBranchPool()
    {
        // 用**合成表**做判别（出厂表里 branch 的池是 event 的子集 ⇒ 无法区分"是否并入"）
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        const string synthetic = """
        { "config": { "version": 1 },
          "rooms": {
            "event":  [ { "weight": 1, "encounter": null, "curio_pool": ["cur_sconce"] } ],
            "branch": [ { "weight": 1, "encounter": null, "curio_pool": ["cur_altar"] } ] } }
        """;
        RoomContentsConfig contents = RoomContentsConfig.Parse(synthetic, curios);

        (ExpeditionFlow flow, CombatLog _) = NewFlow(7);
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < 60; i++)
        {
            string? gained = flow.PickCurioForRoom(contents, "event", isBranch: true);
            if (gained is not null)
            {
                seen.Add(gained);
            }
        }

        Assert.IsTrue(seen.Contains("cur_altar"),
            "🔴 支路房必须并入 `branch` 行的池（否则支路覆盖键形同虚设）");

        (ExpeditionFlow flow2, CombatLog _) = NewFlow(7);
        for (int i = 0; i < 30; i++)
        {
            Assert.AreEqual("cur_sconce", flow2.PickCurioForRoom(contents, "event", isBranch: false),
                "非支路房**不得**拿到 branch 专属内容");
        }
    }

    [TestMethod]
    public void EmptyOrUnknownType_ReturnsNull_SoCallerFallsBackExplicitly()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        const string empty = """
        { "config": { "version": 1 },
          "rooms": { "battle": [ { "weight": 1, "encounter": null, "curio_pool": [] } ] } }
        """;
        RoomContentsConfig contents = RoomContentsConfig.Parse(empty, curios);

        (ExpeditionFlow flow, CombatLog log) = NewFlow(1);
        Assert.IsNull(flow.PickCurioForRoom(contents, "battle", isBranch: false),
            "池为空 ⇒ 返回 null（调用方走回退，不静默造一个）");
        Assert.IsNull(flow.PickCurioForRoom(contents, "branch", isBranch: false),
            "表里没有该类型 ⇒ 也是 null（不抛、不造）");
        Assert.AreEqual(0, log.Events.OfType<RngDraw>().Count(),
            "没抽到任何东西时**不写 RngDraw**（没有随机发生）");
    }

    [TestMethod]
    public void SameSeed_SamePick_Determinism()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        RoomContentsConfig contents = RoomContentsConfig.Parse(ReadData("room_contents.json"), curios);

        (ExpeditionFlow a, CombatLog _) = NewFlow(99);
        (ExpeditionFlow b, CombatLog _) = NewFlow(99);
        for (int i = 0; i < 10; i++)
        {
            Assert.AreEqual(a.PickCurioForRoom(contents, "event", false), b.PickCurioForRoom(contents, "event", false),
                "同 seed ⇒ 同序列（确定性）");
        }
    }
}
