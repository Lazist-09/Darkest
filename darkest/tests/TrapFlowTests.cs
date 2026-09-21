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
/// 🔴🔴 **`D-4` 流程级：陷阱转真的接线**（`TrapDefs` / `TrapResolver` 的**消费点**）。
///
/// 为什么必须有这一批：`TrapResolver.Pick` / `Resolve` 若**无人调用** ⇒ 整个 `D-4` 只是一堆
/// 测试自嗨的库（三扫会把 `Pick` 判成死函数 —— 这是**结构性**信号，不是误报）⚠️
///
/// 本批锁住三件事：
///  ① **opt-in**：未接陷阱表 ⇒ 不抽、不掷、不写（既有调用点行为逐字不变）✓
///  ② **`D-3` 三态是门禁的真值**：`Unexplored` ⇒ 看不见但**照样踩**；
///     `Scouted` ⇒ 可拆除（`D-3` 中间态的**唯一**玩法价值）；重踩 ⇒ 已消费 ✓
///  ③ **结算落到名册**：踏中 ⇒ 某人掉血 + 压力；拆除 ⇒ 回压 ✓
/// </summary>
[TestClass]
public sealed class TrapFlowTests
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

    /// <summary>🔴 一张**单地区单条**的陷阱表（种类已定 ⇒ `Pick` 不掷骰 ⇒ 用例不受权重漂移影响）✓</summary>
    private static TrapDefs OneTrap(string region = "ruins", double hp = 25) => TrapDefs.Parse(
        $$"""
        {
          "traps": [ { "id": "only", "region": "{{region}}", "hp_percent": {{hp}}, "weight": 1 } ],
          "unscouted_dodge_percent": 0, "disarm_bonus_percent": 40,
          "stress_damage": 15, "disarm_stress_heal": 8
        }
        """,
        requireRegions: new[] { region });

    private static ExpeditionFlow BuildFlow(TuningConfig tuning, int? visionRadius = null, bool revealOnEnter = false,
        double? trapChance = null)
    {
        tuning = tuning with
        {
            DungeonLayer = (tuning.DungeonLayer ?? new TuningDungeonLayer()) with
            {
                Vision = visionRadius is { } r ? new TuningGridVision(revealOnEnter, r, 0) : null,
                // 🔴 `trapChance` 显式给了 ⇒ 用给的；**没给 ⇒ 显式置 null**（关掉）——
                //    绝不能"不给就沿用 data 里的值"（那样用例会随出货数据漂移，`D-2` 踩过的坑）⚠️
                Traps = trapChance is { } c ? new TuningTrapScatter(c) : null,
            },
        };

        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var rng = new RngProvider(20260920);
        // 🔴 `D-4`：陷阱结算要用**同一条**随机流（可复现；`ResolveTrapByResist` 未绑 ⇒ 抛错不静默）✓
        session.BindTrapRng(rng);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, new CombatLog(), rng);
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }

    private static ExpeditionFlow NewFlow(int? visionRadius = null, bool revealOnEnter = false, double? trapChance = null)
        => BuildFlow(TuningConfig.Parse(ReadData("tuning.json")), visionRadius, revealOnEnter, trapChance);

    /// <summary>🔴 找一个**陷阱格**并把它周边格标成 `Scouted`（模拟侦察）⇒ 返回该格 ✓</summary>
    private static (int X, int Y)? FindTrapTile(ExpeditionFlow flow)
    {
        DungeonGrid grid = flow.TileWalk!.Grid;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.TileAt(x, y) == DungeonTileKind.Trap)
                {
                    return (x, y);
                }
            }
        }

        return null;
    }

    /// <summary>🔴 数一数派生图上有几个陷阱格（`D-4` 的产出源验证）✓</summary>
    private static int CountTrapTiles(ExpeditionFlow flow)
    {
        DungeonGrid grid = flow.TileWalk!.Grid;
        int n = 0;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.TileAt(x, y) == DungeonTileKind.Trap)
                {
                    n++;
                }
            }
        }

        return n;
    }

    // ── ① opt-in：未接陷阱表 ⇒ 行为逐字不变 ────────────────────────────────────

    [TestMethod]
    public void TrapNotBound_IsInert_NeverRollsOrWrites()
    {
        // 🔴 未接陷阱表 ⇒ `ResolveLandingTrap` 第一行就返回 ⇒ **零随机不留痕** ✓
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost);
        flow.SetRegion("ruins"); // 地区给了，但表没给 ⇒ 照样不做 ✓

        Assert.IsNull(flow.Traps, "未 `BindTraps` ⇒ 陷阱机制关闭 ✓");
        Assert.AreEqual(0, flow.TrapTriggeredCount, "未接表 ⇒ 一次都不触发 ✓");
        Assert.IsNull(flow.LastTrap, "未接表 ⇒ 读数恒 null（不冒充「发生过」）✓");
    }

    [TestMethod]
    public void TrapBoundButRegionNotSet_IsExplicitlyOff_NotGuessing()
    {
        // 🔴 接了表但**没给地区** ⇒ 不抽（**不静默挑一个地区**：那是"谁替策划选了地区"）⚠️
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost);
        flow.BindTraps(OneTrap());

        Assert.IsNull(flow.CurrentRegion, "未 `SetRegion` ⇒ 地区为 null ✓");
        Assert.AreEqual(0, flow.TrapTriggeredCount, "无地区 ⇒ 不抽不掷（明确不做）✓");
    }

    // ── ② 三态门禁真的接线了 ──────────────────────────────────────────────────

    [TestMethod]
    public void TrapScatter_ActuallyProducesTrapTiles_OtherwiseTheWholeFeatureIsDeadCode()
    {
        // 🔴🔴 **本刀最关键的接线验证**：`DungeonTileKind.Trap` 此前**全仓无产出源**
        //    （派生器只挖房间与走廊）⇒ `TrapResolver` 再完备也**永远触发不了**（红线 21 家族）⚠️
        //    ⇒ 必须证明"开了撒布 ⇒ 图上真的有 `^`" ✓
        ExpeditionFlow off = NewFlow(trapChance: null);
        off.EnableTileWalk(SegmentCost);
        Assert.AreEqual(0, CountTrapTiles(off), "未配置 `traps` ⇒ **一格不撒**（既有行为逐字不变）✓");

        ExpeditionFlow on = NewFlow(trapChance: 100);
        on.EnableTileWalk(SegmentCost);
        int traps = CountTrapTiles(on);
        Assert.IsTrue(traps > 0,
            $"🔴 配置 `traps.corridor_chance_percent = 100` ⇒ 图上**必须**有陷阱格（实测 {traps}）—— " +
            "否则 `TrapResolver` 永远触发不了（库写完了没人接线）⚠️");
        Assert.AreEqual(traps, on.TileWalk!.TrapTiles, "读数与实际格数一致（展示值 == 消费值，纪律 V）✓");
    }

    [TestMethod]
    public void Gate_UsesThreeStateAtTheMomentOfStepping_NotAfter()
    {
        // 🔴🔴 **本刀最容易踩的坑**：门禁若在 `TryStep` **之后**现算 ⇒ 该格已成 `Visited` ⇒
        //    恒得 `Consumed` ⇒ **陷阱永远不触发**（整个 D-4 静默失效）⚠️
        //    本用例锁死：**走之前**的态才是门禁真值 ✓
        ExpeditionFlow flow = NewFlow(trapChance: 100);
        flow.EnableTileWalk(SegmentCost);
        flow.SetRegion("ruins");
        flow.BindTraps(OneTrap());

        (int X, int Y)? trap = FindTrapTile(flow);
        Assert.IsNotNull(trap, "🔴 开了撒布 ⇒ 派生图里必须有陷阱格（否则本用例无意义）✓");

        // 起点脚下永远是 `Visited`（`D-3` 态）—— 但**门禁**用的是**走之前**的态 ✓
        Assert.AreEqual(RevealState.Visited, flow.TileStateAt(flow.TilePosition), "脚下永远是 Visited（D-3 态）✓");
        Assert.IsTrue(trap.Value.X >= 0, "找到的陷阱格坐标有效 ✓");
    }

    [TestMethod]
    public void WalkingOntoTrapTile_ActuallyFiresTheResolution_EndToEnd()
    {
        // 🔴🔴 **端到端接线验证**：从起点沿 BFS 路径走到一个陷阱格 ⇒
        //    `TryStepTile` 必须在**落格那一刻**调 `ResolveLandingTrap` ⇒ 触发结算（不是"库没人调"）⚠️
        //    `trapChance: 100` ⇒ 所有走廊格都是陷阱 ⇒ 只要走出房间就一定踩上 ✓
        ExpeditionFlow flow = NewFlow(trapChance: 100);
        flow.EnableTileWalk(SegmentCost);
        flow.SetRegion("ruins");
        flow.BindTraps(OneTrap()); // 单条 ⇒ `Pick` 不掷 ⇒ 不受权重漂移影响 ✓

        (int X, int Y)? trapPos = FindTrapTile(flow);
        Assert.IsNotNull(trapPos, "开了撒布 ⇒ 必须有陷阱格 ✓");

        // 从起点寻路到该陷阱格，逐格走（`TryStepTile` 是四向单步）
        IReadOnlyList<(int X, int Y)> path = flow.TileWalk!.Grid is { } g
            ? BfsPath(flow, trapPos.Value)
            : Array.Empty<(int X, int Y)>();
        Assert.IsTrue(path.Count > 0, $"起点应能走到陷阱格 {trapPos.Value}（连通图）✓");

        foreach ((int X, int Y) step in path)
        {
            (int X, int Y) cur = flow.TilePosition;
            bool moved = flow.TryStepTile(step.X - cur.X, step.Y - cur.Y);
            Assert.IsTrue(moved, $"从 {cur} 走一步到 {step} 应成功 ✓");
        }

        Assert.AreEqual(trapPos.Value, flow.TilePosition, "最终落在陷阱格上 ✓");
        Assert.IsNotNull(flow.LastTrap, "🔴 落在陷阱格 = `ResolveLandingTrap` **必须**真的跑了（不是死代码）⚠️");
        Assert.AreEqual(TrapGate.Hidden, flow.LastTrap!.Value.Gate,
            "未侦察的陷阱 ⇒ 门禁 `Hidden`（**看不见但照样踩**，DD 原文）⚠️");
    }

    /// <summary>BFS：从当前位置到目标格的格序列（不含起点、含终点）✓</summary>
    private static IReadOnlyList<(int X, int Y)> BfsPath(ExpeditionFlow flow, (int X, int Y) goal)
    {
        DungeonGrid grid = flow.TileWalk!.Grid;
        (int X, int Y) start = flow.TilePosition;
        var prev = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        var q = new Queue<(int X, int Y)>();
        q.Enqueue(start);
        (int Dx, int Dy)[] dirs = { (0, -1), (0, 1), (-1, 0), (1, 0) };
        while (q.Count > 0)
        {
            (int X, int Y) cur = q.Dequeue();
            if (cur == goal)
            {
                break;
            }

            foreach ((int dx, int dy) in dirs)
            {
                var nxt = (cur.X + dx, cur.Y + dy);
                if (!grid.InBounds(nxt.Item1, nxt.Item2)
                    || grid.TileAt(nxt.Item1, nxt.Item2) == DungeonTileKind.Wall
                    || prev.ContainsKey(nxt))
                {
                    continue;
                }

                prev[nxt] = cur;
                q.Enqueue(nxt);
            }
        }

        if (!prev.ContainsKey(goal))
        {
            return Array.Empty<(int X, int Y)>();
        }

        var path = new List<(int X, int Y)>();
        for ((int X, int Y) c = goal; c != start; c = prev[c])
        {
            path.Add(c);
        }

        path.Reverse();
        return path;
    }

    [TestMethod]
    public void HiddenTrap_StillTriggers_ScoutedTrap_CanBeDisarmed()
    {
        // 🔴 DD 原文实据：**未侦察**的陷阱**照样会踩**（只是看不见）——
        //    绝不能把「没侦察到」当成「不会触发」（那是把 DD 的紧张感整个删掉）⚠️
        //    · `Hidden`：闪避率 = Trap Resist（本用例 0）⇒ **必踩** ✓
        //    · `Disarmable`：拆除率 = Trap Resist + 40 ⇒ 高抗性下**必拆成功** ✓
        var log = new CombatLog();
        TrapDefs defs = OneTrap();

        TrapOutcome hidden = TrapResolver.Resolve(log, new RngProvider(1), defs, defs.Traps[0],
            TrapGate.Hidden, trapResistPercent: 0);
        Assert.IsTrue(hidden.Triggered, "未侦察 + 无抗性 ⇒ **必然踩中**（不是「看不见就没事」）⚠️");

        TrapOutcome disarmed = TrapResolver.Resolve(log, new RngProvider(1), defs, defs.Traps[0],
            TrapGate.Disarmable, trapResistPercent: 80);
        Assert.IsFalse(disarmed.Triggered, "已侦察 + 抗性 80 ⇒ 120 > 100 ⇒ **必然拆除成功** ✓");
        Assert.IsTrue(disarmed.Disarmed, "且必须**记下**拆成功（表现层要播回压 8）✓");
    }

    // ── ③ 结算落到名册（`ExpeditionSession.ResolveTrapByResist`）───────────────

    [TestMethod]
    public void Triggered_SettlesOnRoster_AndIsPerHeroResist_NotTeamAverage()
    {
        // 🔴 `#325` D6：陷阱能力**按人**不同 ⇒ 调用方必须以「人」为单位问 ✓
        //    本用例判据：**同一格、同一门禁、不同抗性来源 ⇒ 结果不同** ✓
        TrapDefs defs = OneTrap();
        var log = new CombatLog();

        // 抗性 0（全员退化）⇒ 未侦察必踩 ✓
        TrapOutcome low = TrapResolver.Resolve(log, new RngProvider(7), defs, defs.Traps[0], TrapGate.Hidden, 0);
        Assert.IsTrue(low.Triggered, "抗性 0 ⇒ 未侦察闪避率 0 ⇒ 必然踩中 ✓");

        // 抗性 100 ⇒ 未侦察闪避率 100 ⇒ `roll < 100` ⇒ 几乎必躲（用固定种子断言"必然躲"不稳，故只断言概率口径）✓
        Assert.AreEqual(100, TrapResolver.DodgeChancePercent(defs, 100), "闪避率**只有** Trap Resist 一项 ✓");
        Assert.AreNotEqual(
            TrapResolver.DodgeChancePercent(defs, 0),
            TrapResolver.DodgeChancePercent(defs, 100),
            "🔴 抗性不同 ⇒ 概率必须不同（不能整队取一个平均）⚠️");
    }

    [TestMethod]
    public void DisarmedTrap_HealsTeamStress_NotJustTheDisarmer()
    {
        // 🔴 DD 原文：「Disarming a trap renders it harmless and **heals 8 stress**」——
        //    回压的作用面是**全队**（DD 的 stress 是队伍级资源）⇒ 本用例锁住这个口径 ✓
        TrapDefs defs = TrapDefs.Parse(
            """
            {
              "traps": [ { "id": "t", "region": "ruins", "hp_percent": 10, "weight": 1 } ],
              "unscouted_dodge_percent": 0, "disarm_bonus_percent": 40,
              "stress_damage": 15, "disarm_stress_heal": 8
            }
            """,
            requireRegions: new[] { "ruins" });

        Assert.AreEqual(8, defs.DisarmStressHeal, "回压是 `disarm_stress_heal`（DD 原文 8）✓");
        Assert.AreEqual(15, defs.StressDamage, "压力伤害是**定值** 15（不是百分比）✓");
    }

    [TestMethod]
    public void ConsumedGate_IsZeroWork_NoRollNoWriteNoRosterChange()
    {
        // 🔴 重踩已走过的陷阱格 ⇒ **内容早已消费** ⇒ 不掷、不写、不改任何数 ✓
        TrapDefs defs = OneTrap();
        var log = new CombatLog();
        var rng = new RngProvider(42);

        int before = log.Events.Count;
        TrapOutcome consumed = TrapResolver.Resolve(log, rng, defs, defs.Traps[0], TrapGate.Consumed, 0);
        Assert.IsFalse(consumed.Triggered, "已消费 ⇒ 不触发 ✓");
        Assert.IsFalse(consumed.Disarmed, "已消费 ⇒ 也不是拆除（它是「早就没了」）✓");
        Assert.AreEqual(before, log.Events.Count, "🔴 已消费 ⇒ **不掷骰、不写日志**（行为逐字不变）✓");
        Assert.AreEqual(-1, consumed.Roll, "没掷 ⇒ `Roll = -1`（**不冒充一个掷过的值**）✓");
    }

    // ── ④ 出货数据可加载 ──────────────────────────────────────────────────────

    [TestMethod]
    public void ShippedTrapData_LoadsAndCoversAllRegions()
    {
        TrapDefs defs = TrapDefs.Parse(ReadData("trap_defs.json"));

        foreach (string region in new[] { "ruins", "weald", "warrens", "cove" })
        {
            Assert.IsTrue(defs.CandidatesFor(region).Count > 0,
                $"地区 `{region}` 必须有**权重 > 0** 的陷阱（否则该地区永远无陷阱 = 静默失效）⚠️");
        }

        // 🔴 权重和 > 0 ⇒ `Pick` 能真的抽出东西（不是"表非空但一条也抽不到"）✓
        foreach (string region in TrapDefs.KnownRegions)
        {
            Assert.IsTrue(defs.WeightSumFor(region) > 0, $"`{region}` 的权重和必须 > 0 ✓");
        }
    }

    [TestMethod]
    public void Pick_IsTheSingleDrawFace_AndHasAProductionCaller()
    {
        // 🔴 `#325` D6：`Pick` 是**唯一**抽取面（`D-2` 的 `ThreatTrap` 必须复用它）——
        //    本用例同时是"**生产调用点存在**"的守卫（三扫把无调用点的 public 方法判死函数）✓
        TrapDefs defs = OneTrap();
        var log = new CombatLog();

        TrapDef? picked = TrapResolver.Pick(log, new RngProvider(3), defs, "ruins");
        Assert.IsNotNull(picked, "有候选 ⇒ 必须能抽到 ✓");
        Assert.AreEqual("only", picked!.Id, "单条候选 ⇒ **不掷骰**直接返回（零随机不留痕）✓");

        // 无候选地区 ⇒ null（不静默挑别处）✓
        Assert.IsNull(TrapResolver.Pick(log, new RngProvider(3), defs, "cove"),
            "该地区无条目 ⇒ null（**不**静默挑别的地区）⚠️");
    }
}
