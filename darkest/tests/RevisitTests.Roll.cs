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
/// ① 从 `RevisitTests.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② **D-2 重访威胁 · 纯函数掷骰器**（`Roll_*` 六个用例：未配置零副作用 ／ 空档位 ／ 光高于全档不掷 ／ 最暗档更频繁 ／ 每掷都写 `RngDraw` ／ 威胁种类按权重）✓
/// ③ 🔴 依赖主类私有成员：`Cfg3`／`Cfg`／`FixedRng`；外部走 `RevisitSpawner`／`CombatLog`／`IRngProvider`✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public sealed partial class RevisitTests
{
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
}
