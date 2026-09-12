using System;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// v0.92 / #250：**难度递进三档** —— P20 ⑭ 加载级校验（覆盖 1~6 无缝隙无重叠、单调不减、全走 tuning）、
/// 档位查询、**UI 必显 ⑧（当前档位 + 后续预告）**、以及 🔴 **O-70 ① 未裁定前“不施加”回归锁**。
/// </summary>
[TestClass]
public sealed class DifficultyTierTests
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

    private static TuningConfig Tuning() => TuningConfig.Parse(ReadData("tuning.json"));

    private static string Bad(string from, string to)
    {
        string json = ReadData("tuning.json");
        Assert.IsTrue(json.Contains(from, StringComparison.Ordinal), $"基准数据应包含 {from}（测试自检）");
        return json.Replace(from, to, StringComparison.Ordinal);
    }

    [TestMethod]
    public void P20_14_DifficultyTiers_CoverAllBattles_Monotonic_FromTuning()
    {
        TuningExpedition exp = Tuning().Expedition;
        Assert.AreEqual(3, exp.DifficultyTiers!.Count, "三档（#250 起手值）");

        int expected = 1;
        double last = 0;
        foreach (TuningDifficultyTier t in exp.DifficultyTiers.OrderBy(x => x.BattleFrom))
        {
            Assert.AreEqual(expected, t.BattleFrom, "从第 1 场起、无缝隙无重叠（P20 ⑭）");
            Assert.IsTrue(t.Multiplier >= last, "乘数单调不减（P20 ⑭）");
            last = t.Multiplier;
            expected = t.BattleTo + 1;
        }

        Assert.AreEqual(exp.NBattles + 1, expected, $"恰好覆盖第 1~{exp.NBattles} 场（P20 ⑭）");
        Assert.AreEqual(1.0, exp.DifficultyTiers[0].Multiplier, 1e-9, "第 1~2 场 ×1.0");
        Assert.AreEqual(1.1, exp.DifficultyTiers[1].Multiplier, 1e-9, "第 3~4 场 ×1.1");
        Assert.AreEqual(1.25, exp.DifficultyTiers[2].Multiplier, 1e-9, "第 5~6 场 ×1.25");
    }

    [TestMethod]
    public void P20_14_GapOrOverlapOrNonMonotonic_ThrowsOnLoad()
    {
        // 缝隙（1~2 / 4~6 缺第 3 场）
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(Bad(
            "{ \"battle_from\": 3, \"battle_to\": 4, \"multiplier\": 1.1",
            "{ \"battle_from\": 4, \"battle_to\": 4, \"multiplier\": 1.1")),
            "有缝隙 → 启动报错（P20 ⑭）");

        // 重叠（3~4 与 3~6）
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(Bad(
            "{ \"battle_from\": 5, \"battle_to\": 6, \"multiplier\": 1.25",
            "{ \"battle_from\": 4, \"battle_to\": 6, \"multiplier\": 1.25")),
            "有重叠 → 启动报错（P20 ⑭）");

        // 乘数下降（1.1 → 1.05）
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(Bad(
            "\"multiplier\": 1.25", "\"multiplier\": 1.05")),
            "乘数下降 → 启动报错（P20 ⑭）");
    }

    [TestMethod]
    public void UI_8_CurrentTier_And_NextTierPreview()
    {
        TuningExpedition exp = Tuning().Expedition;
        Assert.AreEqual(1.0, ExpeditionProjector.MultiplierFor(exp.DifficultyTiers, 1), 1e-9);
        Assert.AreEqual(1.0, ExpeditionProjector.MultiplierFor(exp.DifficultyTiers, 2), 1e-9);
        Assert.AreEqual(1.1, ExpeditionProjector.MultiplierFor(exp.DifficultyTiers, 3), 1e-9);
        Assert.AreEqual(1.25, ExpeditionProjector.MultiplierFor(exp.DifficultyTiers, 6), 1e-9);

        Assert.AreEqual(1.1, ExpeditionProjector.NextMultiplierFor(exp.DifficultyTiers, 1)!.Value, 1e-9, "必显 ⑧：后续预告");
        Assert.AreEqual(1.25, ExpeditionProjector.NextMultiplierFor(exp.DifficultyTiers, 3)!.Value, 1e-9);
        Assert.IsNull(ExpeditionProjector.NextMultiplierFor(exp.DifficultyTiers, 6), "末档无后续");

        ExpeditionSession s = new(log => HeadlessDriver.NewDirector(log), 6, 2, 12, 0.33);
        var log = new Darkest.Core.Events.CombatLog();
        TuningCamp camp = Tuning().Camp;
        ExpeditionViewState v = ExpeditionProjector.Project(log, 2, 12, s, camp, 6, exp.DifficultyTiers);
        var lines = ExpeditionProjector.RenderList(v, s, 6, false, camp);
        Assert.IsTrue(lines.Any(l => l.StartsWith("⑧", StringComparison.Ordinal) && l.Contains("待 O-70 ① 裁定")),
            "必显 ⑧ 行必须写明『施加对象待裁定 → 当前不施加』");
    }

    [TestMethod]
    public void O70_1_NotRuled_Multiplier_IsNotAppliedToEnemyStats()
    {
        // 🔴 回归锁：O-70 ① 未裁定前，乘数**不得**作用到敌 HP/攻击（units.json 是单场基准值）
        // 若将来施加，本用例必须同步改写为"第 5~6 场的敌 HP > 第 1~2 场"
        Darkest.Gameplay.Sim.Director.BattleDirector d = HeadlessDriver.NewDirector(new Darkest.Core.Events.CombatLog());
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        Assert.AreEqual(units.Get("melee_soldier").Hp, d.Enemy.UnitRuntimeAt(1)!.MaxHp,
            "敌 HP == units.json 基准值（**乘数尚未施加**）");
        Assert.AreEqual(0, d.Enemy.UnitRuntimeAt(1)!.AttackMod, "无攻击修正（**乘数尚未施加**）");
        Assert.IsTrue(Tuning().Expedition.DifficultyTiers!.All(t => t.Target is null),
            "target=null = 施加对象**待架构 O-70 ① 裁定**（实现方不得自行选定）");
    }
}
