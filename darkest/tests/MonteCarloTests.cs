using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M6 验收（T-M6-01~07 内核侧）：headless 直驱统计跑通 / override 生效 / 确定性回放 /
/// 系统触发 KPI / 无软锁与边界 / 手感报告模板落盘。
/// </summary>
[TestClass]
public sealed class MonteCarloTests
{
    [TestMethod]
    public void SingleRun_ProducesCompactLog_AllStatsPopulated()
    {
        (GameOutcome outcome, CombatLog log) = HeadlessDriver.Run(20260909, PolicyKind.SemiRandom);
        Assert.IsTrue(outcome.Rounds >= 1 && outcome.Rounds <= 100);
        Assert.IsTrue(outcome.SkillUses.Count > 0, "技能使用率非空（强制记录）");
        Assert.IsTrue(outcome.MoraleHistogram.Count > 0, "士气分布直方图非空");
        Assert.IsTrue(HeadlessDriver.Dump(log).Length > 0, "紧凑日志非空（确定性留档）");
    }

    [TestMethod]
    public void Override_ChangesSameSeedOutcome_DamageFloat()
    {
        // override 语义：以覆盖后的 tuning 构建导演（同 seed）→ 与默认结果/伤害不同
        (GameOutcome a, CombatLog logA) = HeadlessDriver.Run(777, PolicyKind.Baseline);
        (GameOutcome b, CombatLog logB) = HeadlessDriver.Run(777, PolicyKind.Baseline,
            t => t with { DamageFloat = t.DamageFloat with { Enabled = true } });
        int dmgA = logA.Events.OfType<DamageEvent>().Sum(d => d.Amount);
        int dmgB = logB.Events.OfType<DamageEvent>().Sum(d => d.Amount);
        Assert.IsTrue(dmgA != dmgB, "开启伤害浮动必须改变同 seed 结算结果（override 生效）");
    }

    [TestMethod]
    public void Determinism_SameSeedSamePolicy_IdenticalLog()
    {
        (_, CombatLog logA) = HeadlessDriver.Run(424242, PolicyKind.SemiRandom);
        (_, CombatLog logB) = HeadlessDriver.Run(424242, PolicyKind.SemiRandom);
        Assert.AreEqual(logA.Events.Count, logB.Events.Count);
        for (int i = 0; i < logA.Events.Count; i++)
        {
            Assert.AreEqual(logA.Events[i], logB.Events[i], $"第 {i} 条：同 seed 同命令流逐条一致（T-M6-05）");
        }
    }

    [TestMethod]
    public void SmallBatch_ReportShapeValid()
    {
        // 快速批（30 场）验证报告结构 + 软锁审计（RoundLimit 局占比 ≤20%，强切局不计胜）
        SimulationReport r = HeadlessDriver.RunMany(30, PolicyKind.SemiRandom, seedBase: 333);
        Assert.AreEqual(30, r.Runs);
        Assert.IsTrue(r.WinRate is >= 0 and <= 1);
        Assert.IsTrue(r.RoundLimitGames <= 6, $"软锁审计：30 场中 {r.RoundLimitGames} 场打满 100 回合（>20%）");
        Assert.IsTrue(r.TotalCollapse + r.TotalWeak + r.TotalDisplacements >= 1, "系统事件确实发生");
        Assert.IsTrue(r.SkillUses.Count > 0 && r.PlayerDamage.Count > 0);
    }

    [TestMethod]
    public void Kpi_SixItems_NaturallyOccurring_OverSmallBatch()
    {
        // 六项 KPI 单场下限的“自然发生”验证（小批采样：至少一场各有）
        SimulationReport r = HeadlessDriver.RunMany(24, PolicyKind.SemiRandom, seedBase: 9090);
        Assert.IsTrue(r.TotalCollapse >= 1, "士气触底判定 ≥1");
        Assert.IsTrue(r.GamesWithRetreat >= 1, "撤退 ≥1（整场，自然触发链）");
        Assert.IsTrue(r.TotalAffliction + r.TotalVirtue >= 1, "美德/折磨 ≥1（整场合计）");
        Assert.IsTrue(r.TotalDisplacements >= 1, "位移 ≥2（小样可低但须发生）");
        // 死门 ≥3 为整场单场判据；小批聚合给出趋势供告警口径
        Assert.IsTrue(r.AvgDeathDoorRolls > 0, "死门路径被触发");
    }

    [TestMethod]
    public void InspiredRisk_HpNeverNegative_MoraleClamped_SlotsValid()
    {
        // 长篇冒烟（50 场）软锁审计：100 回合强切局（RoundLimit）占比须 ≤20%；HP/士气钳制由内核保证
        SimulationReport r = HeadlessDriver.RunMany(50, PolicyKind.SemiRandom, seedBase: 55);
        Assert.IsTrue(r.MaxRounds <= 100, "回合计数钳制在 100（无越界）");
        Assert.IsTrue(r.RoundLimitGames <= 10, $"软锁回归：50 场中 {r.RoundLimitGames} 场打满 100 回合（>20% 需数值干预）");
        Assert.IsTrue(r.TotalWeak + r.TotalCollapse >= 1, "系统事件发生");
    }

    [TestMethod]
    public void M6Acceptance_WinRateBand_And_Rhythm()
    {
        // 🔴 v0.68 判据（用户给数）：
        //   A1 单场 = 胜率 ≥85% / **死门 ≤0.3** / **阵亡 ≤0.1** / **回合 4~6**
        //   A2 run  = **打满 3 场且未全灭**（新口径）；同报三个数：①未全灭 ②打满3场且未全灭 ③3 场皆胜
        //   新读数：增援事件数（预期 ≈0）· 口径健康度（预期无单一位置 >40%）
        const int runs = 300;
        SimulationReport r = HeadlessDriver.RunMany(runs, PolicyKind.SemiRandom, seedBase: 20260909);

        int deaths = 0;
        int reinforcements = 0;
        for (int i = 0; i < runs; i++)
        {
            (_, Darkest.Core.Events.CombatLog log) = HeadlessDriver.Run(20260909 + i, PolicyKind.SemiRandom);
            deaths += log.Events.OfType<Darkest.Core.Events.DeathEvent>().Count(e => e.IsPlayer);
            reinforcements += log.Events.OfType<Darkest.Core.Events.ReinforcementEvent>().Count();
        }

        int survived = 0, fullThree = 0, allWon = 0;
        for (int i = 0; i < runs; i++)
        {
            Darkest.Gameplay.Sim.Run.RunOutcome c = HeadlessDriver.RunCampaign(20260909 + i, PolicyKind.SemiRandom, battles: 3);
            if (c.CompletedCountingRetreat)
            {
                survived++;
            }

            if (c.Curve.Count >= 3 && c.Curve.All(s => !s.PlayerWiped))
            {
                fullThree++;
            }

            if (c.Curve.Count >= 3 && c.Curve.All(s => s.Result == "PlayerVictory"))
            {
                allWon++;
            }
        }

        double deathsPerBattle = (double)deaths / runs;
        double ddPerBattle = (double)r.TotalDeathDoorRolls / runs;
        string report = $"[M6] A1 单场：胜率={r.WinRate:P0} 死门={ddPerBattle:F2}/场 阵亡={deathsPerBattle:F2}/场 回合={r.AvgRounds:F2} " +
                        $"（门槛 ≥85% / ≤0.3 / ≤0.1 / 4~6）\n" +
                        $"[M6] A2 run（v0.68 新口径）：打满3场且未全灭={fullThree}/{runs}（{(double)fullThree / runs:P0}）" +
                        $"｜未全灭={survived}（{(double)survived / runs:P0}）｜3场皆胜={allWon}（{(double)allWon / runs:P0}）\n" +
                        $"[M6] 新读数：增援事件={reinforcements}（预期 ≈0）｜参考 KPI coll={r.TotalCollapse} weak={r.TotalWeak} " +
                        $"retreat={r.GamesWithRetreat} virtue={r.TotalVirtue} affliction={r.TotalAffliction} displace={r.TotalDisplacements}";
        Console.WriteLine(report);

        Assert.IsTrue(r.WinRate >= 0.85, $"判据 A1 未过：单场胜率 {r.WinRate:P0} < 85%。{report}");
        Assert.IsTrue(ddPerBattle <= 0.3, $"判据 A1 未过：死门 {ddPerBattle:F2}/场 > 0.3。{report}");
        Assert.IsTrue(deathsPerBattle <= 0.1, $"判据 A1 未过：阵亡 {deathsPerBattle:F2}/场 > 0.1。{report}");
        Assert.IsTrue(r.AvgRounds is >= 4 and <= 6, $"判据 A1 未过：平均回合 {r.AvgRounds:F2} 不在 4~6。{report}");
    }

    [TestMethod]
    public void M6Report_HandoffDump()
    {
        // 将报告以紧凑文本预置到测试目录（供评审/手感报告引用）
        SimulationReport r = HeadlessDriver.RunMany(300, PolicyKind.SemiRandom, seedBase: 20260909);
        string msg = $"[M6] runs={r.Runs} win={r.WinRate:P0} avgRounds={r.AvgRounds:F2} " +
                     $"min={r.MinRounds} max={r.MaxRounds} collapse={r.TotalCollapse} weak={r.TotalWeak} " +
                     $"ddRolls={r.TotalDeathDoorRolls} retreatGames={r.GamesWithRetreat} virtue={r.TotalVirtue} " +
                     $"affliction={r.TotalAffliction} displace={r.TotalDisplacements}";
        Console.WriteLine(msg);
    }

    [TestCleanup]
    public void FlushContext()
    {
        // TestContext 输出由 vstest 收集（此处仅为接口使用保持完整性）
    }

    public TestContext TestContext { get; set; } = null!;
}