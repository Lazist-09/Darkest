using System.IO;
using Darkest.Core.Math;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M2 公式复算（T-M2-01/T-M2-10）：combat_math §7.1 全 8 样例逐字 + 边界
/// （钳制 55/100、四舍五入、下限 1、减免极值、精神减免 cap 40%）。
/// </summary>
[TestClass]
public sealed class FormulaTests
{
    private static BalanceTable Table()
        => BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json"))));

    private static string FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContextBaseDir());
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到（应从 darkest/ 运行测试）。");
    }

    private static string AppContextBaseDir() => System.AppContext.BaseDirectory;

    // ------------------------------------------------------------------
    // combat_math §7.1 八样例（判据字符串与文档逐字一致）
    // ------------------------------------------------------------------

    [TestMethod]
    public void Warrior_To_MeleeMook_Is_9()
        => Assert.AreEqual(9, BattleMath.PhysicalHit(attack: 12, skillMultiplier: 1.0, defense: 8));

    [TestMethod]
    public void Medic_To_MeleeMook_Is_9()
        => Assert.AreEqual(9, BattleMath.PhysicalHit(attack: 11, skillMultiplier: 1.0, defense: 8));

    [TestMethod]
    public void Commissar_To_RangedArcher_Is_9()
        => Assert.AreEqual(9, BattleMath.PhysicalHit(attack: 11, skillMultiplier: 0.95, defense: 4));

    [TestMethod]
    public void Tank_To_MeleeMook_Is_6()
        => Assert.AreEqual(6, BattleMath.PhysicalHit(attack: 8, skillMultiplier: 0.9, defense: 8));

    [TestMethod]
    public void MeleeMook_To_Warrior_Is_9()
        => Assert.AreEqual(9, BattleMath.PhysicalHit(attack: 12, skillMultiplier: 1.0, defense: 8));

    [TestMethod]
    public void MeleeMook_To_Tank_Is_9()
        => Assert.AreEqual(9, BattleMath.PhysicalHit(attack: 12, skillMultiplier: 1.0, defense: 12));

    [TestMethod]
    public void RangedArcher_To_Medic_Is_10()
        => Assert.AreEqual(10, BattleMath.PhysicalHit(attack: 13, skillMultiplier: 0.9, defense: 4));

    [TestMethod]
    public void Caster_To_Warrior_Spirit_Is_8()
        => Assert.AreEqual(8, BattleMath.SpiritHit(attack: 12, skillMultiplier: 0.8, resilience: 50));

    // ------------------------------------------------------------------
    // 边界（T-M2-01 完成判据）
    // ------------------------------------------------------------------

    [TestMethod]
    public void HitRate_Clamps_And_Baseline()
    {
        Assert.AreEqual(90, BattleMath.HitRate(dodge: 10, hitMod: 0), "§7.2 命中 90% 基线");
        Assert.AreEqual(55, BattleMath.HitRate(dodge: 100, hitMod: -60), "负修正触发下限 55");
        Assert.AreEqual(100, BattleMath.HitRate(dodge: 0, hitMod: 120), "正修正触发上限 100（不做必中只钳顶）");
    }

    [TestMethod]
    public void ApplyDamageRounding_RoundsAwayFromZero_AndFloors()
    {
        Assert.AreEqual(1, BattleMath.ApplyDamageRounding(0.5), "round(0.5)=1（AwayFromZero）");
        Assert.AreEqual(1, BattleMath.ApplyDamageRounding(1.49));
        Assert.AreEqual(2, BattleMath.ApplyDamageRounding(1.5), "round(1.5)=2");
        Assert.AreEqual(1, BattleMath.ApplyDamageRounding(-3.2), "任何来源伤害最低 1 点");
        Assert.AreEqual(8, BattleMath.ApplyDamageRounding(7.68), "7.68 四舍五入 → 8（与精神样例同口径）");
    }

    [TestMethod]
    public void MentalMitigation_Caps_At_40_Percent()
    {
        Assert.AreEqual(0.20, BattleMath.MentalMitigation(50), 1e-12, "韧性 50 → 20%（#158 锚点）");
        Assert.AreEqual(0.40, BattleMath.MentalMitigation(1000), 1e-12, "减免 cap 40%（韧性再高不再加）");
        Assert.AreEqual(0.12, BattleMath.MentalMitigation(30), 1e-12, "韧性 30 → 12%");
    }

    [TestMethod]
    public void PhysicalMitigation_Converges_NeverNegative()
    {
        Assert.AreEqual(8 / 38.0, BattleMath.PhysicalMitigation(8), 1e-12);
        double highDef = BattleMath.PhysicalMitigation(10_000);
        Assert.IsTrue(highDef < 1.0 && highDef > 0.99, "高防收敛向 1 但永不为 1/负");
    }

    [TestMethod]
    public void ActualEffectChance_UsesMultiplication()
    {
        Assert.AreEqual(28.0, BattleMath.ActualEffectChance(labeledPercent: 40, resistPercent: 30), 1e-12,
            "40%×(1−30%)=28%（反例：非 40−30=10）");
        Assert.AreEqual(0.0, BattleMath.ActualEffectChance(100, 100), "抗性 100 → 0");
        Assert.AreEqual(70.0, BattleMath.ActualEffectChance(100, 30), "标注 100% → 实际 = 100−抗性");
    }

    [TestMethod]
    public void DeathDoorSurvivePercent_AfflictionPenalty()
    {
        Assert.AreEqual(70, BattleMath.DeathDoorSurvivePercent(resistPercent: 70, afflicted: false));
        Assert.AreEqual(60, BattleMath.DeathDoorSurvivePercent(resistPercent: 70, afflicted: true), "#123 折磨 −10%");
    }

    [TestMethod]
    public void RetreatFormula_BaseAndFinal_Clamps()
    {
        // O-11/#169：基础 = 50% + 速度差×4%，钳制 [15,85]；最终 = 基础 ±10，钳制 [5,95]
        Assert.AreEqual(50.0, BattleMath.RetreatBaseRate(0), 1e-9, "速度差 0 → 50%");
        Assert.AreEqual(70.0, BattleMath.RetreatBaseRate(5), 1e-9, "速度差每 1 点 ±4%（+5 → 70%）");
        Assert.AreEqual(40.0, BattleMath.RetreatBaseRate(-2.5), 1e-9, "对方更快 → 下降");
        Assert.AreEqual(15.0, BattleMath.RetreatBaseRate(-10), 1e-9, "基础下钳 15%");
        Assert.AreEqual(85.0, BattleMath.RetreatBaseRate(20), 1e-9, "基础上钳 85%");

        Assert.AreEqual(40.0, BattleMath.RetreatFinalRate(50, 0.0), 1e-9, "roll=0 → −10");
        Assert.AreEqual(60.0, BattleMath.RetreatFinalRate(50, 1.0), 1e-9, "roll=1 → +10");
        Assert.AreEqual(95.0, BattleMath.RetreatFinalRate(90, 1.0), 1e-9, "90+10 → 钳 95");
        Assert.AreEqual(5.0, BattleMath.RetreatFinalRate(0, 0.0), 1e-9, "0−10 → 最终下钳 5%");
        Assert.AreEqual(95.0, BattleMath.RetreatFinalRate(100, 0.5), 1e-9, "最终上钳 95%");
    }
}