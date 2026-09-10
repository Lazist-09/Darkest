using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M5 决策支持投影（T-M5-06/09 内核侧）：9 条必显数据同源（行动序列/撤退数字/命中率）、
/// 位移预览 = 结算同源（dry-run 与真实执行序列一致、无副作用）、技能可用性（tooltip 逐字）。
/// </summary>
[TestClass]
public sealed class BattleProjectorTests
{
    private static string FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    private static (BattleDirector director, BattleProjector projector) World()
    {
        BattleDirector director = new(FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json"))),
            UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json"))),
            SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json"))),
            BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json")))),
            MoraleEventsConfig.Parse(File.ReadAllText(FindDataFile("morale_events.json"))),
            BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json"))),
            EnemyAiConfig.Parse(File.ReadAllText(FindDataFile("enemy_ai.json"))),
            new CombatLog());
        var runtime = new SkillRuntimeState();
        var projector = new BattleProjector(director, BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json")))),
            SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json"))), runtime);
        return (director, projector);
    }

    [TestMethod]
    public void Support_ActionsSequenceAndRetreatNumber_FromKernelSource()
    {
        (BattleDirector director, BattleProjector projector) = World();
        director.StartTurn(new RngProvider(20260909));

        DecisionSupportProjection s = projector.Support();
        Assert.AreEqual(1, s.Round);
        Assert.IsTrue(s.ActionOrderThisRound.Count >= 4, "行动序列 = 全部在列单位（本回合）");
        Assert.AreEqual(s.ActionOrderThisRound.Count, s.ActionOrderNextRound.Count, "下回合预览同长度（外推，无新增抽取）");
        Assert.AreEqual((int)Math.Round(director.CurrentRetreatRate(), MidpointRounding.AwayFromZero), s.RetreatRatePercent,
            "撤退数字 == 导演公式在当回合速度快照下的输出（M-C）");
        Assert.IsTrue(s.RetreatRatePercent is >= 15 and <= 85, "基础率钳制 [15,85]");
        Assert.IsTrue(s.CanRetreat);
    }

    [TestMethod]
    public void HitRate_MatchesFormula()
    {
        (_, BattleProjector projector) = World();
        Assert.AreEqual(90, projector.HitRateFor(targetDodge: 10, hitMod: 0), "命中率 90% 基线（必显 #4）");
        Assert.AreEqual(55, projector.HitRateFor(targetDodge: 100, hitMod: -60), "下限 55");
        Assert.AreEqual(100, projector.HitRateFor(targetDodge: 0, hitMod: 120), "上限 100");
    }

    [TestMethod]
    public void DisplacementPreview_MatchesRealExecution_NoSideEffect()
    {
        (_, BattleProjector projector) = World();
        FormationBoard enemy = new(FormationSide.Enemy, new SlotLayout(4, 4, Array.Empty<int>()), FormationRules.Default(),
            new Dictionary<int, UnitRuntime>
            {
                [1] = new(UnitId.Of("m1"), FormationSide.Enemy, MakeStats()),
                [2] = new(UnitId.Of("m2"), FormationSide.Enemy, MakeStats()),
                [3] = new(UnitId.Of("m3"), FormationSide.Enemy, MakeStats()),
                [4] = new(UnitId.Of("m4"), FormationSide.Enemy, MakeStats()),
            });

        // dry-run 与真实结算在等价板上逐条一致（同函数）
        DisplaceResult preview = projector.DisplacementPreview(UnitId.Of("m4"), 4, 1, 3, enemy);
        Assert.IsTrue(preview.Success);
        string before = Snapshot(enemy);
        DisplaceResult real = enemy.TrySwapChain(UnitId.Of("m4"), 4, 1, 3);
        Assert.AreEqual(preview.Steps.Count, real.Steps.Count);
        for (int i = 0; i < preview.Steps.Count; i++)
        {
            Assert.AreEqual(preview.Steps[i], real.Steps[i], $"第 {i} 步：预览与结算同源逐条一致（M-A）");
        }

        // dry-run 无副作用：对另一等价板预览后不变
        FormationBoard probe = new(FormationSide.Enemy, new SlotLayout(4, 4, Array.Empty<int>()), FormationRules.Default(),
            new Dictionary<int, UnitRuntime>
            {
                [1] = new(UnitId.Of("m1"), FormationSide.Enemy, MakeStats()),
                [2] = new(UnitId.Of("m2"), FormationSide.Enemy, MakeStats()),
                [3] = new(UnitId.Of("m3"), FormationSide.Enemy, MakeStats()),
                [4] = new(UnitId.Of("m4"), FormationSide.Enemy, MakeStats()),
            });
        string probeBefore = Snapshot(probe);
        _ = projector.DisplacementPreview(UnitId.Of("m4"), 4, 1, 3, probe);
        Assert.AreEqual(probeBefore, Snapshot(probe), "预览不得污染真实板");
    }

    [TestMethod]
    public void SkillAvailability_TooltipMatchesUiSpec()
    {
        (_, BattleProjector projector) = World();
        // 战士在支援位 5 用劈砍（self_slots [1,2]）→ BadStance + "站位不符"
        FormationBoard player = new(FormationSide.Player, new SlotLayout(6, 4, new[] { 5, 6 }), FormationRules.Default(),
            new Dictionary<int, UnitRuntime>
            {
                [5] = new(UnitId.Of("warrior"), FormationSide.Player, MakeStats()),
            });
        SkillProjection p = projector.Skill("warrior_cleave", UnitId.Of("warrior"), player, player, new HashSet<string> { "warrior_cleave" });
        Assert.AreEqual(AvailabilityReason.BadStance, p.Reason);
        Assert.AreEqual("站位不符", p.Tooltip);
    }

    private static UnitStats MakeStats()
        => new(Hp: 50, Attack: 12, PhysDef: 8, Speed: 8, Dodge: 10, Crit: 5, Resilience: 55,
            StunResist: 30, BleedResist: 30, StatDebuffResist: 25, DisplaceResist: 55, DeathsDoorResist: null);

    private static string Snapshot(IFormation board)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        return string.Join("|", Enumerable.Range(1, count)
            .Select(p => $"{p}:{board.GetSlot(p)}:{(board.UnitAt(p)?.ToString() ?? "-")}"));
    }
}