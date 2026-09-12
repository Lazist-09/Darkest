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
/// M7.5 D1（侦察，`#259`）：基础 25% + 光照加成；**只揭示下一节点类型**；
/// **`success == false` ⇒ `revealedNodeType == null`**；每次判定**必写 `RngDraw`**；
/// 事件流可复算（V5 / ㉕ 只能从 `ScoutResultEvent` 统计）。
/// </summary>
[TestClass]
public sealed class ScoutingTests
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

    private static (TuningScouting Scouting, TuningLight Light) Cfg()
    {
        TuningConfig t = TuningConfig.Parse(ReadData("tuning.json"));
        return (t.Scouting!, t.Light!);
    }

    [TestMethod]
    public void D1_Chance_IsBasePlusLightBonus()
    {
        (TuningScouting cfg, TuningLight light) = Cfg();
        var s = new Scouting(cfg, light);
        Assert.AreEqual(40.0, s.ChanceFor(100), 1e-9, "Radiant：25% + 15% = 40%");
        Assert.AreEqual(32.5, s.ChanceFor(70), 1e-9, "Dim：25% + 7.5% = 32.5%");
        Assert.AreEqual(25.0, s.ChanceFor(40), 1e-9, "Shadowy：25% + 0 = 25%");
        Assert.AreEqual(25.0, s.ChanceFor(0), 1e-9, "Black：25% + 0 = 25%（侦察不是越高档越差）");
    }

    [TestMethod]
    public void D1_RevealsOnlyNextNodeType_AndWritesRngDraw()
    {
        (TuningScouting cfg, TuningLight light) = Cfg();
        var s = new Scouting(cfg, light);
        var log = new CombatLog();

        // ScriptedRng(roll=0) → 必成功；揭示值必须**原样等于传入的下一节点类型**（不得外扩）
        ScoutOutcome ok = s.Roll(log, new ScriptedRng(0.0), lightValue: 100, nextNodeType: "battle");
        Assert.IsTrue(ok.Success);
        Assert.AreEqual("battle", ok.RevealedNodeType, "只揭示【下一个】节点类型");
        Assert.IsTrue(log.Events.OfType<RngDraw>().Any(), "判定必写 RngDraw（确定性红线）");

        ScoutResultEvent e = log.Events.OfType<ScoutResultEvent>().Single();
        Assert.AreEqual(0.0, e.Roll);
        Assert.IsTrue(e.Success);
        Assert.AreEqual("battle", e.RevealedNodeType);
    }

    [TestMethod]
    public void D1_Failure_RevealsNull()
    {
        (TuningScouting cfg, TuningLight light) = Cfg();
        var s = new Scouting(cfg, light);
        var log = new CombatLog();

        // roll=99 → 必失败：**revealedNodeType 必须为 null**（策划 v1.02 硬要求）
        ScoutOutcome fail = s.Roll(log, new ScriptedRng(99.0), lightValue: 100, nextNodeType: "event");
        Assert.IsFalse(fail.Success);
        Assert.IsNull(fail.RevealedNodeType, "失败 ⇒ null（不得「失败却也带类型」）");

        ScoutResultEvent e = log.Events.OfType<ScoutResultEvent>().Single();
        Assert.IsFalse(e.Success);
        Assert.IsNull(e.RevealedNodeType);
    }

    [TestMethod]
    public void D1_Deterministic_SameSeedSameOutcome()
    {
        (TuningScouting cfg, TuningLight light) = Cfg();
        var s = new Scouting(cfg, light);

        ScoutOutcome a = s.Roll(new CombatLog(), new RngProvider(1234), 60, "battle");
        ScoutOutcome b = s.Roll(new CombatLog(), new RngProvider(1234), 60, "battle");
        Assert.AreEqual(a.Success, b.Success);
        Assert.AreEqual(a.Roll, b.Roll, 1e-9);
        Assert.AreEqual(a.RevealedNodeType, b.RevealedNodeType);
    }

    [TestMethod]
    public void D1_SuccessRate_MatchesChance_OverManyRolls()
    {
        (TuningScouting cfg, TuningLight light) = Cfg();
        var s = new Scouting(cfg, light);
        var log = new CombatLog();
        var rng = new RngProvider(20260909);

        int success = 0;
        const int runs = 4000;
        for (int i = 0; i < runs; i++)
        {
            if (s.Roll(log, rng, lightValue: 100, nextNodeType: "battle").Success)
            {
                success++;
            }
        }

        double rate = (double)success / runs;
        Assert.IsTrue(rate is > 0.35 and < 0.45, $"Radiant 侦察率 ≈40%（实测 {rate:P1}）");
        Assert.AreEqual(runs, log.Events.OfType<ScoutResultEvent>().Count(), "每次判定恰一条 ScoutResultEvent（V5/㉕ 数据面）");
    }
}
