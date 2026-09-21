using System;
using System.Collections.Generic;
using System.IO;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴🔴 **`D-7` 探索层 act-out**（`dd_replication_roadmap` D-7 / `dungeon_layer_design.md §F5` / DD ⑩）：
/// **带折磨**的英雄在**探索层**会**拒绝**做某些事 —— 本刀落两条：
/// ① **拒绝摸奇物**：`curio.md §1.2 ⑤` 原文 = 带折磨者会「**自动空手碰**」某类 Curio（**绝不用道具**）
///    ⇒ 语义裁定 = **拒绝用道具、被迫空手**（空手掷骰照常走、照常写 `RngDraw`）；
/// ② **拒绝进食**：`§F5 ③` —— 与 F3c 饥饿联动，掷中 ⇒ **强制挨饿**（推翻玩家的"吃"选择）。
///
/// <para>🔴🔴 **折磨判据 = 士气 &lt; 50**（用户 2026-09-20 裁定）—— 该阈值**不是近似，而是模型自身的边界**：</para>
/// <para>· `MoraleLedger.GrantAffliction` 只在 **士气 == 0** 时挂折磨（`CheckCollapseTrigger` / `TriggerCollapseForHpZero`）；</para>
/// <para>· `MoraleLedger` 第 127~134 行：**只有士气回到 `morale.start`（= 50）才解除**折磨与捆缚；</para>
/// <para>· 美德走 **士气 == 100**（`HandleMoraleMax`）且**立即把士气拉回 50** ⇒ 美德与折磨**永不并存**
///   （士气是从 100 往下连续变化的，必然经过 50 ⇒ 到 50 时折磨已解除）✓</para>
/// <para>⇒ **「士气 &lt; 50」区间与「带折磨」在模型上等价**，且**天然跨趟**（士气本就跨趟累积，`#245`/`#287`）✓</para>
///
/// <para>🔴 **纪律**（与 D-5 同族同式）：**随机必写 `RngDraw`**；**未配置 ⇒ 一点副作用都没有**（不掷、不写日志）；
/// **拒绝必写文案事件**（红线 21：绝不静默失败）✓</para>
/// </summary>
[TestClass]
public sealed class ExplorationActOutTests
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

    /// <summary>把随机源钉成"每次必中"或"每次必不中"（与 `HungerTests` 同款夹具）✓</summary>
    private sealed class FixedRng(double percent, int intValue = 0) : IRngProvider
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

    /// <summary>造一份最小 act-out 配置（阈值 50 / 两条拒绝概率）✓</summary>
    private static TuningExplorationActOut Cfg(double curioRefuse, double eatRefuse, int threshold = 50) => new(
        MoraleAfflictionThreshold: threshold,
        CurioRefusePercent: curioRefuse,
        EatRefusePercent: eatRefuse);

    // ══════════════════════ ① 折磨判据（阈值 50）══════════════════════

    [TestMethod]
    public void IsAfflicted_MoraleBelowThreshold_IsTrue()
    {
        TuningExplorationActOut cfg = Cfg(50, 50); // threshold = 50
        var rng = new FixedRng(0.0);
        var log = new CombatLog();

        Assert.IsTrue(ExplorationActOut.IsAfflicted(cfg, 0, rng, log),
            "士气 0（崩溃点，折磨**必然挂上**）⇒ 带折磨 ✓");
        Assert.IsTrue(ExplorationActOut.IsAfflicted(cfg, 49, rng, log), "士气 49 < 50 ⇒ 带折磨 ✓");
        Assert.IsTrue(ExplorationActOut.IsAfflicted(cfg, 1, rng, log), "士气 1 < 50 ⇒ 带折磨 ✓");
    }

    [TestMethod]
    public void IsAfflicted_AtOrAboveThreshold_IsFalse()
    {
        TuningExplorationActOut cfg = Cfg(50, 50);
        var rng = new FixedRng(0.0);
        var log = new CombatLog();

        Assert.IsFalse(ExplorationActOut.IsAfflicted(cfg, 50, rng, log),
            "🔴 士气恰好 50 = `morale.start` ⇒ **正是折磨的解除点**（`MoraleLedger` 第 127 行）⇒ 不带折磨 ✓");
        Assert.IsFalse(ExplorationActOut.IsAfflicted(cfg, 51, rng, log), "士气 51 > 50 ⇒ 不带折磨 ✓");
        Assert.IsFalse(ExplorationActOut.IsAfflicted(cfg, 100, rng, log), "士气 100（美德点）⇒ 不带折磨 ✓");
    }

    [TestMethod]
    public void IsAfflicted_IsPureQuery_NoDrawNoLog()
    {
        // 🔴 判据是**纯查询**：掷骰发生在【拒绝判定】那一步，不在"是否带折磨"这一步 ✓
        var rng = new FixedRng(0.0);
        var log = new CombatLog();
        _ = ExplorationActOut.IsAfflicted(Cfg(50, 50), 30, rng, log);
        Assert.AreEqual(0UL, rng.DrawCount, "🔴 「是否带折磨」**不掷骰**（阈值判定是确定的）✓");
        Assert.AreEqual(0, log.Events.Count, "🔴 「是否带折磨」**不写日志** ✓");
    }

    [TestMethod]
    public void IsAfflicted_NoConfig_IsFalse()
    {
        var rng = new FixedRng(0.0);
        var log = new CombatLog();
        Assert.IsFalse(ExplorationActOut.IsAfflicted(null, 0, rng, log),
            "🔴 未配置 `exploration` 段 ⇒ **一律不带折磨**（opt-in：既有行为逐字不变）✓");
        Assert.AreEqual(0UL, rng.DrawCount, "未配置 ⇒ 不掷 ✓");
        Assert.AreEqual(0, log.Events.Count, "未配置 ⇒ 不写日志 ✓");
    }

    // ══════════════════════ ② 拒绝掷骰（必写 RngDraw）══════════════════════

    [TestMethod]
    public void RollCurioRefuse_NoConfig_ReturnsNone_AndWritesNoLog()
    {
        var log = new CombatLog();
        var rng = new FixedRng(0.0); // 若真掷了，0.0 必中 ⇒ 用"不中"反证没掷 ✓
        ActOutRollResult r = ExplorationActOut.RollCurioRefuse(log, rng, null, morale: 0);
        Assert.IsFalse(r.Refused, "未配置 ⇒ 一律不拒绝（**哪怕士气为 0**）✓");
        Assert.AreEqual(0, log.Events.Count, "🔴 未配置 ⇒ **连 `RngDraw` 都不写**（零副作用）✓");
        Assert.AreEqual(0UL, rng.DrawCount, "未配置 ⇒ **一次都不掷** ✓");
    }

    [TestMethod]
    public void RollCurioRefuse_NotAfflicted_DoesNotRoll()
    {
        // 🔴🔴 **关键**：不带折磨 ⇒ **不掷骰**（不是"掷了但没中"）——
        //    否则每次摸 Curio 都多消耗一个随机数，会**污染整条随机流**（D-4 踩过同源坑）⚠️
        var log = new CombatLog();
        var rng = new FixedRng(0.0);
        ActOutRollResult r = ExplorationActOut.RollCurioRefuse(log, rng, Cfg(50, 50), morale: 80);
        Assert.IsFalse(r.Refused, "士气 80 ⇒ 不带折磨 ⇒ 不拒绝 ✓");
        Assert.AreEqual(0UL, rng.DrawCount, "🔴 不带折磨 ⇒ **不掷骰**（随机流零污染）✓");
        Assert.AreEqual(0, log.Events.Count, "🔴 不带折磨 ⇒ **不写日志** ✓");
    }

    [TestMethod]
    public void RollCurioRefuse_AfflictedEveryDraw_WritesRngDraw()
    {
        // 掷了但没中 ⇒ 1 条 RngDraw（并写"检查过"的痕迹）✓
        var logMiss = new CombatLog();
        ActOutRollResult miss = ExplorationActOut.RollCurioRefuse(logMiss, new FixedRng(99.0), Cfg(33, 33), morale: 0);
        Assert.IsFalse(miss.Refused, "99.0 ≥ 33 ⇒ 没拒绝 ✓");
        Assert.AreEqual(1, logMiss.Events.Count, "🔴 掷了就必写 `RngDraw`（哪怕没中）✓");
        Assert.IsInstanceOfType(logMiss.Events[0], typeof(RngDraw), "第一条必须是 `RngDraw`（确定性红线）✓");

        // 中了 ⇒ 同样只有**一次**抽取（拒绝判定无子掷）✓
        var logHit = new CombatLog();
        ActOutRollResult hit = ExplorationActOut.RollCurioRefuse(logHit, new FixedRng(0.0), Cfg(33, 33), morale: 0);
        Assert.IsTrue(hit.Refused, "0.0 < 33 ⇒ 拒绝 ✓");
        Assert.AreEqual(33.0, hit.PercentUsed, 1e-9, "展示值 == 配置值（纪律 V）✓");
    }

    [TestMethod]
    public void RollEatRefuse_NoConfig_ReturnsNone_AndWritesNoLog()
    {
        var log = new CombatLog();
        var rng = new FixedRng(0.0);
        ActOutRollResult r = ExplorationActOut.RollEatRefuse(log, rng, null, morale: 0);
        Assert.IsFalse(r.Refused, "未配置 ⇒ 不拒绝（**哪怕士气为 0**）✓");
        Assert.AreEqual(0, log.Events.Count, "🔴 未配置 ⇒ 不写日志 ✓");
        Assert.AreEqual(0UL, rng.DrawCount, "未配置 ⇒ 不掷 ✓");
    }

    [TestMethod]
    public void RollEatRefuse_NotAfflicted_DoesNotRoll()
    {
        var log = new CombatLog();
        var rng = new FixedRng(0.0);
        ActOutRollResult r = ExplorationActOut.RollEatRefuse(log, rng, Cfg(50, 50), morale: 50);
        Assert.IsFalse(r.Refused, "士气 50 ⇒ 恰恰在解除点 ⇒ 不拒绝 ✓");
        Assert.AreEqual(0UL, rng.DrawCount, "🔴 不带折磨 ⇒ 不掷骰 ✓");
        Assert.AreEqual(0, log.Events.Count, "🔴 不带折磨 ⇒ 不写日志 ✓");
    }

    [TestMethod]
    public void RollEatRefuse_AfflictedEveryDraw_WritesRngDraw()
    {
        var logMiss = new CombatLog();
        ActOutRollResult miss = ExplorationActOut.RollEatRefuse(logMiss, new FixedRng(99.0), Cfg(33, 25), morale: 10);
        Assert.IsFalse(miss.Refused, "99.0 ≥ 25 ⇒ 没拒绝 ✓");
        Assert.AreEqual(1, logMiss.Events.Count, "🔴 掷了就必写 `RngDraw` ✓");
        Assert.IsInstanceOfType(logMiss.Events[0], typeof(RngDraw));

        var logHit = new CombatLog();
        ActOutRollResult hit = ExplorationActOut.RollEatRefuse(logHit, new FixedRng(0.0), Cfg(33, 25), morale: 10);
        Assert.IsTrue(hit.Refused, "0.0 < 25 ⇒ 拒绝 ✓");
        Assert.AreEqual(25.0, hit.PercentUsed, 1e-9, "展示值 == 配置值 ✓");
    }

    [TestMethod]
    public void RefusePercentAtOrAbove100_IsAlwaysRefused_Below0_Never()
    {
        // 边界：100 ⇒ `NextPercent()` 是 [0,100) ⇒ 恒 < 100 ⇒ **必拒** ✓
        ActOutRollResult always = ExplorationActOut.RollCurioRefuse(new CombatLog(), new FixedRng(99.999), Cfg(100, 50), morale: 0);
        Assert.IsTrue(always.Refused, "100% 配置 + roll=99.999 ⇒ 必拒 ✓");

        // 0 ⇒ 恒不拒（但**仍掷**：配置存在 ⇒ 走完整路径，与"未配置"是两回事）✓
        var logZero = new CombatLog();
        ActOutRollResult never = ExplorationActOut.RollCurioRefuse(logZero, new FixedRng(0.0), Cfg(0, 50), morale: 0);
        Assert.IsFalse(never.Refused, "0% 配置 ⇒ 恒不拒 ✓");
        Assert.AreEqual(1, logZero.Events.Count, "🔴 配置存在（哪怕 0）⇒ **仍然掷、仍然留痕**（不是静默跳过）✓");
    }

    // ══════════════════════ ③ 文案事件（不静默）══════════════════════

    [TestMethod]
    public void Refused_TextIsNonEmpty_AndDistinctPerKind()
    {
        TuningExplorationActOut cfg = Cfg(100, 100);
        ActOutRollResult curio = ExplorationActOut.RollCurioRefuse(new CombatLog(), new FixedRng(0.0), cfg, morale: 0);
        ActOutRollResult eat = ExplorationActOut.RollEatRefuse(new CombatLog(), new FixedRng(0.0), cfg, morale: 0);

        Assert.IsTrue(curio.Refused && eat.Refused, "两条都拒绝 ✓");
        Assert.IsFalse(string.IsNullOrWhiteSpace(curio.Text), "🔴 拒绝**必须有文案**（红线 21：不静默）✓");
        Assert.IsFalse(string.IsNullOrWhiteSpace(eat.Text), "🔴 拒绝**必须有文案** ✓");
        Assert.AreNotEqual(curio.Text, eat.Text, "🔴 两种拒绝的文案**必须不同**（否则玩家分不清发生了什么）✓");
    }

    // ══════════════════════ ④ 配置加载（fail-fast）══════════════════════

    /// <summary>
    /// 🔴 **格式契约**（与 `HungerTests` / `RevisitTests` **逐字同款**）：
    /// 既有用例用**文本级替换**把 `dungeon_layer` 插在 `retreat_formula` **之前**
    /// ⇒ 依赖 ① `retreat_formula` 这个**物理位置** ② 文件**紧凑格式**（短对象单行）✓
    /// </summary>
    private static string TuningJson(string dungeonLayerJson)
        => ReadData("tuning.json").Replace("\"retreat_formula\": {",
            $"\"dungeon_layer\": {dungeonLayerJson},\n  \"retreat_formula\": {{");

    [TestMethod]
    public void Configured_Exploration_IsParsed()
    {
        TuningConfig t = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.IsNotNull(t.DungeonLayer, "🔴 `tuning.json` 已配 `dungeon_layer` ✓");
        Assert.IsNotNull(t.DungeonLayer!.Exploration, "🔴 D-7 探索层 act-out 读得到 ✓");
        Assert.AreEqual(50, t.DungeonLayer.Exploration!.MoraleAfflictionThreshold,
            "🔴 折磨阈值 = `morale.start` = 50（模型自身的解除点，见类注释）✓");
        Assert.IsTrue(t.DungeonLayer.Exploration.CurioRefusePercent is > 0 and <= 100,
            "🔴 拒绝摸奇物概率 ∈ (0,100] —— 0 等于「这段配了但永远不生效」（静默失效家族）⚠️ —— " +
            "要关闭请**整段删掉** `exploration` ✓");
        Assert.IsTrue(t.DungeonLayer.Exploration.EatRefusePercent is > 0 and <= 100,
            "🔴 拒绝进食概率 ∈ (0,100]（同上）✓");
    }

    [TestMethod]
    public void ThresholdOutOfRange_ElseRejected()
    {
        string bad = TuningJson("""{ "exploration": { "morale_affliction_threshold": 150, "curio_refuse_percent": 33, "eat_refuse_percent": 25 } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(bad),
            "阈值越界 [0,100] ⇒ 加载期 fail-fast（红线 21）✓");
    }

    [TestMethod]
    public void CurioRefusePercentOutOfRange_ElseRejected()
    {
        string bad = TuningJson("""{ "exploration": { "morale_affliction_threshold": 50, "curio_refuse_percent": 101, "eat_refuse_percent": 25 } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(bad),
            "> 100 ⇒ 加载期 fail-fast ✓");
    }

    [TestMethod]
    public void RefusePercentZero_ElseRejected()
    {
        // 🔴 0 是"配了但永远不生效"的静默失效家族 ⇒ 要关就删整段（与 D-6 的 reward_gold>0 同族纪律）✓
        string bad = TuningJson("""{ "exploration": { "morale_affliction_threshold": 50, "curio_refuse_percent": 0, "eat_refuse_percent": 25 } }""");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(bad),
            "0 ⇒ 加载期 fail-fast（要关闭请整段删掉 `exploration`）✓");
    }

    [TestMethod]
    public void NotConfigured_IsNull_NotSilentlyDefaulted()
    {
        // 未配置 ⇒ `Exploration` 为 null（**opt-in**：既有行为逐字不变）✓
        string json = ReadData("tuning.json")
            .Replace("\"dungeon_layer\": {", "\"dungeon_layer_removed\": {");
        TuningConfig t = TuningConfig.Parse(json);
        Assert.IsNull(t.DungeonLayer, "整段移除 ⇒ `DungeonLayer` = null ✓");
    }

    // ══════════════════════ ⑤ 阈值来源一致性（`#325` D6：一份真值）══════════════════════

    [TestMethod]
    public void Threshold_EqualsMoraleStart_SoItTracksTheRealUnlockBoundary()
    {
        // 🔴 这不是"我挑了个好看的数" —— 它**必须**等于 `morale.start`，
        //    因为那正是 `MoraleLedger` 解除折磨的唯一判据（第 127 行 `newValue == _balance.MoraleStart`）✓
        TuningConfig t = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(t.Morale.Start, t.DungeonLayer!.Exploration!.MoraleAfflictionThreshold,
            "🔴 折磨阈值 == `tuning.morale.start` —— 两者**必须同值**（否则要么「该解除还被判折磨」、要么「该判折磨却已解除」）✓");
    }
}
