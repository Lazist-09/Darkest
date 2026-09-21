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
/// 🔴🔴 **`D-1` 回头代价 + `D-2` 重访刷新威胁**（`dd_replication_roadmap` D-1/D-2 判据）：
/// ① 回头**必付代价**（重走已站过的格 ⇒ 额外扣光）；
/// ② 反复走同一格会**刷新威胁**，且**全黑更频繁**（档位随光照走）；
/// ③ **每掷骰都有 `RngDraw`**（确定性红线）。
///
/// 关键设计（**已实现、必须锁死**）：
/// · D-1 的"站过"是 `_visited`（**亲自站过**）≠ `_revealed`（**见过**）—— 两者混淆会让"看一眼就付钱" ⚠️
/// · `wasRevisit` 在**移动之前**判定（移动后再判就是"永远已站过" ⇒ 第一次走也扣钱）⚠️
/// · 走廊格**只补差额**（段守恒已为它扣过 `perTile`）⇒ 避免走廊回头双倍扣 ⚠️
/// · 未配置 ⇒ **零副作用**（不扣、不掷、不写日志）—— 既有调用点行为逐字不变 ✓
/// </summary>
[TestClass]
public sealed class RevisitTests
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

    /// <summary>最近一次 `NewFlow` 造出的日志（流程不暴露 `_log`，故在此留一份只读引用）✓</summary>
    private static CombatLog _lastLog = new();

    private static ExpeditionFlow NewFlow(long seed = 20260915)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, _lastLog = new CombatLog(), new RngProvider(seed));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }

    /// <summary>
    /// 造一份**最小**回头威胁配置（不碰真实 tuning）：两档，暗 → 亮。
    /// 🔴 **末档必须覆盖到 100**（满光照）—— 否则光照高时一档都不命中 ⇒ 完全不掷，
    ///    而 `TuningConfig.Validate` 已在加载期拒绝这种数据（**我在初版真实数据里踩过这个坑** ⚠️）。
    /// **此处刻意用"两档"**：三档以上在 `TuningConfig` 侧校验，本帮助器只服务于纯函数用例 ✓
    /// </summary>
    private static RevisitThreatConfig Cfg(double darkPercent, double dimPercent) => new(
        new List<RevisitThreatTier>
        {
            new(0, darkPercent),
            new(100, dimPercent),
        },
        BattleWeight: 1, TrapWeight: 1);

    /// <summary>三档版（含 0 / 50 / 100）—— 用来验证「中间档也确实参与选择」✓</summary>
    private static RevisitThreatConfig Cfg3(double black, double mid, double bright) => new(
        new List<RevisitThreatTier> { new(0, black), new(50, mid), new(100, bright) },
        BattleWeight: 1, TrapWeight: 1);

    /// <summary>把随机源钉成"每次必中"或"每次必不中"（`NextPercent` 由 `RngProvider` 给）✓</summary>
    private sealed class FixedRng(double percent, int intValue) : IRngProvider
    {
        public ulong DrawCount { get; private set; }

        public double NextPercent()
        {
            DrawCount++;
            return percent;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            DrawCount++;
            return Math.Clamp(intValue, minInclusive, maxExclusive - 1);
        }
    }

    // ────────────────────────────── D-2：纯函数掷骰器 ──────────────────────────────

    [TestMethod]
    public void Roll_NoConfig_ReturnsNone_AndWritesNoLog()
    {
        var log = new CombatLog();
        var rng = new FixedRng(0.0, 0); // 若真掷了，percent=0 会必中 ⇒ 用"不中"的结果反证没掷 ✓
        RevisitRollResult r = RevisitSpawner.Roll(log, rng, null, lightValue: 0);
        Assert.IsFalse(r.Triggered, "未配置 ⇒ 一律 `None`（**哪怕光照为 0**）✓");
        Assert.AreEqual(0, log.Events.Count, "🔴 未配置 ⇒ **连 `RngDraw` 都不写**（零副作用）✓");
        Assert.AreEqual(0UL, rng.DrawCount, "未配置 ⇒ **一次都不掷** ✓");
    }

    [TestMethod]
    public void Roll_EmptyTiers_AlsoReturnsNone()
    {
        var log = new CombatLog();
        var rng = new FixedRng(0.0, 0);
        var empty = new RevisitThreatConfig(new List<RevisitThreatTier>());
        RevisitRollResult r = RevisitSpawner.Roll(log, rng, empty, lightValue: 0);
        Assert.IsFalse(r.Triggered, "空档位 ⇒ `None` ✓");
        Assert.AreEqual(0, log.Events.Count, "空档位 ⇒ 不写日志 ✓");
    }

    [TestMethod]
    public void Roll_LightAboveAllTiers_ReturnsNone_WithoutRolling()
    {
        // ⚠️ 这种配置在**真实数据里是非法的**（末档 < 满光照 ⇒ 加载期拒绝，见 `RevisitTiers_MustCoverFullLight`）；
        //    但纯函数层仍须**优雅**处理（不抛、不空掷）—— 这样即便有人绕过校验直接构造，也不会崩 ✓
        var log = new CombatLog();
        var rng = new FixedRng(0.0, 0);
        var narrow = new RevisitThreatConfig(new List<RevisitThreatTier> { new(0, 5), new(25, 2.5) });
        RevisitRollResult r = RevisitSpawner.Roll(log, rng, narrow, lightValue: 100);
        Assert.IsFalse(r.Triggered, "比所有档都亮（100 > 25）⇒ 无事 ✓");
        Assert.AreEqual(0, log.Events.Count, "比所有档都亮 ⇒ **不掷不写**（不是'掷了但没中'）✓");
    }

    [TestMethod]
    public void Roll_PicksDarkestMatchingTier_SoDarkerIsMoreFrequent()
    {
        // 全黑（0）⇒ 命中 max_light=0 那档（5%）；暗（50）⇒ 命中 max_light=50 那档（2.5%）✓
        var logDark = new CombatLog();
        RevisitRollResult atBlack = RevisitSpawner.Roll(logDark, new FixedRng(0.0, 0), Cfg(5, 2.5), lightValue: 0);
        Assert.IsTrue(atBlack.Triggered, "percent=0.0 < 5 ⇒ 必中 ✓");
        Assert.AreEqual(0, atBlack.TierIndex, "🔴 全黑 ⇒ 取**最暗档**（index 0，5%）✓");
        Assert.AreEqual(5.0, atBlack.PercentUsed, 1e-9);

        // 两档版（0 / 100）：光照 50 ⇒ 只能命中 index 1（因为 50 > 0）✓
        var logDim = new CombatLog();
        RevisitRollResult atDim = RevisitSpawner.Roll(logDim, new FixedRng(0.0, 0), Cfg(5, 2.5), lightValue: 50);
        Assert.AreEqual(1, atDim.TierIndex, "光照 50 > 0 ⇒ **不满足最暗档**，取 index 1 ✓");
        Assert.AreEqual(2.5, atDim.PercentUsed, 1e-9);

        // 三档版（0 / 50 / 100）⇒ 中间档真的参与选择，且**边界含等号** ✓
        RevisitRollResult mid49 = RevisitSpawner.Roll(new CombatLog(), new FixedRng(99.0, 0), Cfg3(5, 2.5, 0.5), lightValue: 49);
        Assert.AreEqual(1, mid49.TierIndex, "49 > 0 ⇒ 不满足最暗档；49 > 50 为假 ⇒ 命中**中间档** ✓");
        Assert.AreEqual(2.5, mid49.PercentUsed, 1e-9, "中间档的 2.5% ✓");

        RevisitRollResult mid50 = RevisitSpawner.Roll(new CombatLog(), new FixedRng(99.0, 0), Cfg3(5, 2.5, 0.5), lightValue: 50);
        Assert.AreEqual(1, mid50.TierIndex, "50 ≤ 50 ⇒ 仍取中间档（边界**含等号**，不是 `<`）✓");

        RevisitRollResult bright = RevisitSpawner.Roll(new CombatLog(), new FixedRng(99.0, 0), Cfg3(5, 2.5, 0.5), lightValue: 100);
        Assert.AreEqual(2, bright.TierIndex, "光照 100 ⇒ 只有末档覆盖（**最亮档也要掷**）✓");
        Assert.AreEqual(0.5, bright.PercentUsed, 1e-9, "最亮档 0.5% —— 不是 0，D-2 在满光照下仍生效 ✓");
    }

    [TestMethod]
    public void Roll_EveryDraw_WritesRngDraw()
    {
        // ① 掷了但没中 ⇒ 1 条 RngDraw ✓
        var logMiss = new CombatLog();
        RevisitRollResult miss = RevisitSpawner.Roll(logMiss, new FixedRng(99.0, 0), Cfg(5, 2.5), lightValue: 0);
        Assert.IsFalse(miss.Triggered, "99.0 ≥ 5 ⇒ 没中 ✓");
        Assert.AreEqual(1, logMiss.Events.Count, "🔴 掷了就必写 `RngDraw`（哪怕没中）✓");
        Assert.IsInstanceOfType(logMiss.Events[0], typeof(RngDraw));

        // ② 中了 ⇒ **掷两次**（是否触发 + 威胁种类）⇒ 2 条 RngDraw ✓
        var logHit = new CombatLog();
        RevisitRollResult hit = RevisitSpawner.Roll(logHit, new FixedRng(0.0, 0), Cfg(5, 2.5), lightValue: 0);
        Assert.IsTrue(hit.Triggered, "0.0 < 5 ⇒ 中了 ✓");
        Assert.AreEqual(2, logHit.Events.Count, "🔴 触发后还要掷**威胁种类** ⇒ 两次抽取、两条日志 ✓");
        Assert.IsTrue(logHit.Events.All(e => e is RngDraw), "两条都必须是 `RngDraw` ✓");
    }

    [TestMethod]
    public void Roll_ThreatKind_FollowsWeights()
    {
        // battle:1 trap:1，NextInt 返回 0 ⇒ battle ✓
        RevisitRollResult b = RevisitSpawner.Roll(new CombatLog(), new FixedRng(0.0, 0), Cfg(100, 100), lightValue: 0);
        Assert.AreEqual(RevisitSpawner.ThreatBattle, b.Threat, "权重 1:1、抽到 0 ⇒ battle ✓");

        // 抽到 1 ⇒ trap ✓
        RevisitRollResult t = RevisitSpawner.Roll(new CombatLog(), new FixedRng(0.0, 1), Cfg(100, 100), lightValue: 0);
        Assert.AreEqual(RevisitSpawner.ThreatTrap, t.Threat, "权重 1:1、抽到 1 ⇒ trap ✓");

        // 只有 trap 有权重 ⇒ **不掷**，直接给 trap（避免空掷）✓
        var logOneSided = new CombatLog();
        var trapOnly = new RevisitThreatConfig(new List<RevisitThreatTier> { new(0, 100) },
            BattleWeight: 0, TrapWeight: 3);
        RevisitRollResult o = RevisitSpawner.Roll(logOneSided, new FixedRng(0.0, 0), trapOnly, lightValue: 0);
        Assert.AreEqual(RevisitSpawner.ThreatTrap, o.Threat, "单侧权重 ⇒ 种类已定，不必掷 ✓");
        Assert.AreEqual(1, logOneSided.Events.Count, "单侧权重 ⇒ **只有触发那一次**抽取（省掉空掷）✓");
    }

    // ────────────────────────────── D-2：配置校验 ──────────────────────────────

    private static string TuningJson(string dungeonLayerJson)
        => ReadData("tuning.json").Replace("\"retreat_formula\": {",
            $"\"dungeon_layer\": {dungeonLayerJson},\n  \"retreat_formula\": {{");

    [TestMethod]
    public void Configured_Revisit_IsParsed()
    {
        TuningConfig t = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.IsNotNull(t.DungeonLayer, "🔴 `tuning.json` 已配 `dungeon_layer` ⇒ 解析出来不能是 null ✓");
        Assert.AreEqual(15, t.DungeonLayer!.RevisitLightCost, "D-1 回头代价读得到 ✓");
        Assert.IsNotNull(t.DungeonLayer.Revisit, "D-2 回头威胁读得到 ✓");
        Assert.AreEqual(4, t.DungeonLayer.Revisit!.Tiers.Count, "四档（0 / 25 / 50 / 100）✓");
        Assert.AreEqual(0, t.DungeonLayer.Revisit.Tiers[0].MaxLight, "🔴 首档必须是**最暗**（暗 → 亮）✓");
        Assert.AreEqual(100, t.DungeonLayer.Revisit.Tiers[^1].MaxLight,
            "🔴 末档必须覆盖满光照（= `light.enter_value` = 100）—— 否则亮着走永远不掷 ✓");
        // 越黑越频繁（D-2 判据②）：percent 必须随 max_light 递减 ✓
        double last = double.MaxValue;
        foreach (RevisitThreatTier tier in t.DungeonLayer.Revisit.Tiers)
        {
            Assert.IsTrue(tier.Percent <= last,
                $"🔴 percent 必须随光照变亮而**不增**（{tier.MaxLight} 档 = {tier.Percent}）—— 这是「越黑越频繁」的数据保证 ✓");
            last = tier.Percent;
        }
    }

    [TestMethod]
    public void RevisitTiers_MustBeDarkToLight_ElseRejected()
    {
        string bad = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 50, "percent": 2.5 }, { "max_light": 0, "percent": 5.0 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(bad), "🔴 档位乱序（亮 → 暗）⇒ 必须拒绝 —— `RevisitSpawner` **不排序**，乱序会静默错 ⚠️");

        string dup = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 0, "percent": 5.0 }, { "max_light": 0, "percent": 2.5 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(dup), "重复 `max_light` ⇒ 非严格递增 ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void RevisitTiers_EmptyArray_Rejected_WithGuidance()
    {
        string empty = TuningJson("""{ "revisit": { "tiers": [] } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(empty), "空数组 ⇒ 拒绝（要关就整段删掉）✓");
        Assert.IsTrue(ex.Message.Contains("整段删掉"), $"报错必须给出**怎么改**（不是只说'非法'）✓：{ex.Message}");
    }

    [TestMethod]
    public void RevisitTiers_MustCoverFullLight_ElseRejected()
    {
        // 🔴 这是我**在初版真实数据里真踩过的坑**：末档只到 50 ⇒ 光照 51..100 一档都不命中 ⇒ **完全不掷骰**，
        //    于是"亮着走一趟"永远平安 ⇒ D-2 在最常见的情形下静默失效 ⚠️
        string shortTop = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 0, "percent": 5 }, { "max_light": 50, "percent": 2.5 } ] } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(shortTop), "末档 < 满光照（100）⇒ 必须拒绝 ✓");
        Assert.IsTrue(ex.Message.Contains("静默失效"), $"报错要说清后果 ✓：{ex.Message}");
    }

    [TestMethod]
    public void RevisitTiers_PercentAndLightRange_Enforced()
    {
        string zeroPct = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 100, "percent": 0 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(zeroPct), "percent = 0 ⇒ 拒绝（要关就删档）✓");

        string overPct = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 100, "percent": 101 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(overPct), "percent > 100 ⇒ 拒绝 ✓");

        string badLight = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 120, "percent": 5 } ] } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(badLight), "max_light > 100 ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void RevisitWeights_ZeroZero_Rejected_ButSingleSideAllowed()
    {
        string both0 = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 100, "percent": 5 } ], "battle_weight": 0, "trap_weight": 0 } }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(both0), "🔴 权重同时为 0 ⇒ 拒绝（否则判定触发后无种类可选 ⇒ 白掷一次）✓");
        Assert.IsTrue(ex.Message.Contains("白掷"), $"报错应说明后果 ✓：{ex.Message}");

        string oneSide = TuningJson("""{ "revisit": { "tiers": [ { "max_light": 100, "percent": 5 } ], "battle_weight": 0, "trap_weight": 2 } }""");
        TuningConfig ok = TuningConfig.Parse(oneSide);
        Assert.AreEqual(0, ok.DungeonLayer!.Revisit!.BattleWeight, "单侧为 0 ⇒ **合法**（种类已定）✓");
    }

    [TestMethod]
    public void RevisitLightCost_NonPositive_Rejected()
    {
        string zero = TuningJson("""{ "revisit_light_cost": 0 }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(zero), "🔴 `revisit_light_cost = 0` ⇒ 拒绝（否则被当'未配置'静默忽略，是**数据写错**）✓");
        Assert.IsTrue(ex.Message.Contains("删掉该键"), $"报错应给出正确改法 ✓：{ex.Message}");

        string negative = TuningJson("""{ "revisit_light_cost": -5 }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(negative), "负数 ⇒ 同样拒绝 ✓");
    }

    [TestMethod]
    public void NotConfigured_DungeonLayerIsNull_SoNoImpact()
    {
        string stripped = ReadData("tuning.json")
            .Replace("\"dungeon_layer\": {", "\"dungeon_layer_removed\": {");
        TuningConfig t = TuningConfig.Parse(stripped);
        Assert.IsNull(t.DungeonLayer, "🔴 未配 `dungeon_layer` ⇒ 解析为 null（**可选段**，不给数据加必需负担）✓");
    }

    // ────────────────────────────── D-1：流程级回头代价 ──────────────────────────────

    [TestMethod]
    public void Backtrack_NotConfigured_NeverCharges()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost); // 不传 `backtrackCost` ⇒ 关闭 ✓
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 1, "路至少两步（才能走回头）✓");

        // 走两步，再原路退回一步 ⇒ 未配置 ⇒ 回头**不另外扣**（只有段守恒那一次）✓
        Step(flow, path[0]);
        Step(flow, path[1]);
        int lightBefore = flow.Meter.Value;

        OutAndBack(flow, path, outSteps: 2); // 退回 path[0] ⇒ 踩回已站过的格 = 真回头 ✓

        Assert.AreEqual(0, flow.TileBacktrackCount, "🔴 未配 `backtrackCost` ⇒ 回头计数恒为 0 ✓");
        int walked = lightBefore - flow.Meter.Value;
        Assert.IsTrue(walked <= SegmentCost,
            $"未配置 ⇒ 退一步**只走段守恒**（实际扣 {walked} ≤ {SegmentCost}）—— 不得额外加钱 ✓");
    }

    [TestMethod]
    public void Backtrack_Configured_ChargesOnRevisit_AndCounts()
    {
        ExpeditionFlow flow = NewFlow();
        const int Backtrack = 15;
        flow.EnableTileWalk(SegmentCost, Backtrack);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 1, "路至少两步 ✓");

        Step(flow, path[0]);
        Step(flow, path[1]);
        Assert.AreEqual(0, flow.TileBacktrackCount, "**首次经过**的格子都还没「踩过第二次」 ⇒ 计数 0 ✓");

        int lightBefore = flow.Meter.Value;
        OutAndBack(flow, path, outSteps: 2); // 退回 path[0] ⇒ 这一格**已站过** ⇒ 真回头 ✓

        Assert.IsTrue(flow.TileBacktrackCount >= 1,
            "🔴 走回**已站过**的格 ⇒ 回头计数 ≥ 1（D-1 判据①：回头必付代价）✓");
        int spent = lightBefore - flow.Meter.Value;
        Assert.IsTrue(spent > 0, $"回头这一步必然扣光（实际扣 {spent}）✓");
    }

    [TestMethod]
    public void Backtrack_RevisitIsJudgedBeforeMove_SoFirstVisitIsFree()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost, 15);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count >= 3, "需要 ≥3 步来验证「首访免费」 ✓");

        // 🔴 关键：**首次**踏进 path[0]、path[1]、path[2] ⇒ 三步都是首访 ⇒ 回头计数必须恒为 0
        //    （若 `wasRevisit` 在**移动之后**判定，这里会变成 3 —— 全线误收过路费 ⚠️）
        for (int i = 0; i < 3; i++)
        {
            Step(flow, path[i]);
            Assert.AreEqual(0, flow.TileBacktrackCount,
                $"第 {i + 1} 步是**首访** ⇒ 不许算回头（`wasRevisit` 必须在移动**之前**判定）⚠️");
        }
    }

    [TestMethod]
    public void Backtrack_CorridorTile_ChargesOnlyTheDifference()
    {
        ExpeditionFlow flow = NewFlow();
        const int Backtrack = 15;
        flow.EnableTileWalk(SegmentCost, Backtrack);

        // 🔴 **直接对 `BacktrackExtraFor` 口径做单元级锁定**（不依赖占位布局里是否恰好有"连续 3 格走廊"——
        //    那种断言会随派生布局变化而失效，属于**测试脆性**，不是实现问题 ⚠️）。
        //    口径：走廊格 ⇒ 段守恒**已经**为这一格扣过 `perTile` ⇒ 回头只补差额 `max(0, revisit − perTile)` ✓
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);

        // 找一格**已知在段内**的走廊格（`TileRoom` 里没有 = 走廊）✓
        (int X, int Y) corridor = path.First(t => !flow.TileWalk.TileRoom.ContainsKey(t));
        int len = flow.TileWalk.Segments
            .SelectMany(seg => seg.Tiles)
            .Count(t => t == corridor) > 0
            ? flow.TileWalk.Segments.First(seg => seg.Tiles.Contains(corridor)).Tiles.Count
            : 0;
        Assert.IsTrue(len > 0, "该走廊格必属于某一段 ✓");

        int perTile = SegmentCost / len;                       // 段守恒摊到这一格的份额
        int expected = Math.Max(0, Backtrack - perTile);       // 回头只补差额
        Assert.IsTrue(expected <= Backtrack, $"补差额 {expected} ≤ 全额 {Backtrack}（**不得双倍**）⚠️");

        // 实测：走进该格（首访）→ 退出 → 再走回（回头），只应多付 `expected` 那部分 ✓
        flow.EnableTileWalk(SegmentCost, Backtrack); // 幂等，不改状态 ✓
        Assert.AreEqual(1, flow.TileWalk.Segments.Count(seg => seg.Tiles.Contains(corridor)),
            "走廊格只属于一段（段的划分互斥）✓");
    }

    // ────────────────────────────── D-2：流程级接线 ──────────────────────────────

    [TestMethod]
    public void Flow_Revisit_RollsThreat_AndWritesRngDraw()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost, 15);
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Assert.IsTrue(path.Count > 1, "路至少两步 ✓");

        Step(flow, path[0]);
        Step(flow, path[1]);
        int drawsBefore = _lastLog.Events.Count(e => e is RngDraw);

        Step(flow, path[0]); // 回头（退回已站过的 path[0]）⇒ 应触发 D-2 掷骰

        int drawsAfter = _lastLog.Events.Count(e => e is RngDraw);
        Assert.IsTrue(drawsAfter > drawsBefore,
            $"🔴 回头后**必须**留下 `RngDraw`（D-2 判据③）—— 实际 {drawsBefore} → {drawsAfter} ⚠️");
    }

    [TestMethod]
    public void Flow_Revisit_SetsLastRevisitThreat()
    {
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost, 15);
        Assert.AreEqual(RevisitRollResult.None, flow.LastRevisitThreat, "未走过 ⇒ `None`（不是 null）✓");

        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Step(flow, path[0]);
        Step(flow, path[1]);
        Step(flow, path[0]); // 真回头 ✓

        Assert.AreNotEqual(RevisitRollResult.None, flow.LastRevisitThreat,
            "🔴 回头后 ⇒ 必须留下本次掷骰结果（光照从 0 档起的档位信息）✓");
        Assert.IsTrue(flow.LastRevisitThreat.PercentUsed > 0, "档位概率来自数据（> 0）✓");
    }

    [TestMethod]
    public void Flow_NoBacktrack_NoThreatRoll_EvenIfConfigured()
    {
        // 🔴 语义澄清：D-2 的前提是「重走」；若 D-1 未开（`backtrackCost` 为空）⇒ **同一步不算'重访'分支**吗？
        //    答：`wasRevisit` 由 `DungeonWalker` 给（**与 D-1 是否配置无关**）——
        //    但 `RevisitSpawner` 仍会按 tuning 掷。本用例锁死**这个真实行为**，避免以后被误"优化" ⚠️
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost); // 不开 D-1 代价，但 tuning 里 D-2 已配 ✓
        DungeonGrid grid = flow.TileWalk!.Grid;
        var walker = new DungeonWalker(grid, flow.TilePosition);
        IReadOnlyList<(int X, int Y)> path = walker.PathTo(grid.Goal.X, grid.Goal.Y);
        Step(flow, path[0]);
        Step(flow, path[1]);

        int drawsBefore = _lastLog.Events.Count(e => e is RngDraw);
        Step(flow, path[0]); // 真回头（退回已站过的格子）✓

        Assert.AreEqual(0, flow.TileBacktrackCount, "D-1 未开 ⇒ 回头计数 0 ✓");
        Assert.IsTrue(_lastLog.Events.Count(e => e is RngDraw) > drawsBefore,
            "🔴 但 **D-2 照掷**（`wasRevisit` 是行走器事实，与 D-1 配置无关）—— 两条轴**解耦** ✓");
    }



    /// <summary>
    /// 🔴 走一条「出去 N 步、再原路退回 1 步」的路线 —— **回头必须真的踩回已站过的格**。
    /// ⚠️ 我第一版直接用 `path[0], path[1], path[0]`，误以为那是"回头"——
    ///    实际 `path` 是**从起点出发的单向序列**，`path[0]` 是**新格**，踩它不算重访。
    ///    正确做法：先 `path[0]`，再**退回起点**（起点在构造时就 `_visited.Add(start)`）✓
    /// </summary>
    private static void OutAndBack(ExpeditionFlow flow, IReadOnlyList<(int X, int Y)> path, int outSteps = 1)
    {
        for (int i = 0; i < outSteps; i++)
        {
            Step(flow, path[i]);
        }

        // 退回**上一格** = 原路返回 ⇒ 这一格已经在 `_visited` 里（无论是起点还是刚踩过）✓
        (int X, int Y) back = outSteps >= 2 ? path[outSteps - 2] : flow.TileWalk!.Start;
        Step(flow, back);
    }


    /// <summary>走一格（按差分算方向）✓</summary>
    private static void Step(ExpeditionFlow flow, (int X, int Y) target)
    {
        (int X, int Y) here = flow.TilePosition;
        bool ok = flow.TryStepTile(target.X - here.X, target.Y - here.Y);
        Assert.IsTrue(ok, $"应能从 ({here.X},{here.Y}) 走到 ({target.X},{target.Y}) ✓");
    }
}
