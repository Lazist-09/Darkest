using System;
using System.IO;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// E3 数值契约门禁：四档食物效果/需求、口粮按存活人数缩放、Respite = 基准 + 存活人数（满编 12 / 死 2 人 10）。
/// 纯函数、零抽取（不含 RngDraw 要求）。
/// </summary>
[TestClass]
public sealed class ExpeditionCampMathTests
{
    private static TuningCamp Camp()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", "tuning.json");
            if (File.Exists(candidate))
            {
                return TuningConfig.Parse(File.ReadAllText(candidate)).Camp;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("data/tuning.json 未找到。");
    }

    [TestMethod]
    public void FoodTiers_Monotonic_AndBaseValues()
    {
        TuningCamp c = Camp();
        Assert.AreEqual(0, c.FoodTiers.Starve);
        Assert.AreEqual(3, c.FoodTiers.Half);
        Assert.AreEqual(6, c.FoodTiers.Full);
        Assert.AreEqual(12, c.FoodTiers.Feast, "四档起手值 0/3/6/12（P20 ③）");
    }

    [TestMethod]
    public void FoodRequired_ScalesWithSurvivors()
    {
        TuningCamp c = Camp();
        Assert.AreEqual(0, ExpeditionCampMath.FoodRequired(c.FoodTiers, "starve", 6));
        Assert.AreEqual(3, ExpeditionCampMath.FoodRequired(c.FoodTiers, "half", 6));
        Assert.AreEqual(6, ExpeditionCampMath.FoodRequired(c.FoodTiers, "full", 6));
        Assert.AreEqual(12, ExpeditionCampMath.FoodRequired(c.FoodTiers, "feast", 6));

        // 减员 → 需求等比下降（每减员 −1/4 精神：4 人 = 满编的 2/3 → 向上取整）
        Assert.AreEqual(8, ExpeditionCampMath.FoodRequired(c.FoodTiers, "feast", 4), "12 × 4/6 = 8");
        Assert.AreEqual(2, ExpeditionCampMath.FoodRequired(c.FoodTiers, "half", 4), "3 × 4/6 = 2");
        Assert.AreEqual(1, ExpeditionCampMath.FoodRequired(c.FoodTiers, "half", 1), "向上取整为 1");
    }

    [TestMethod]
    public void FoodEffects_MatchSpec()
    {
        Assert.AreEqual((-0.20, -15), ExpeditionCampMath.FoodEffect("starve"), "Starve：全队 −20% HP、−15 士气");
        Assert.AreEqual((0.0, 0), ExpeditionCampMath.FoodEffect("half"), "Half：无效果");
        Assert.AreEqual((0.10, 0), ExpeditionCampMath.FoodEffect("full"), "Full：全队 +10% HP");
        Assert.AreEqual((0.25, 10), ExpeditionCampMath.FoodEffect("feast"), "Feast：全队 +25% HP、+10 士气");
    }

    [TestMethod]
    public void Respite_Pool_IsBasePlusSurvivors()
    {
        TuningCamp c = Camp();
        Assert.AreEqual(12, ExpeditionCampMath.RespitePool(c.RespiteBase, survivors: 6), "满编 6 人 → 12");
        Assert.AreEqual(10, ExpeditionCampMath.RespitePool(c.RespiteBase, survivors: 4), "死 2 人 → 10（减员惩罚）");
        Assert.AreEqual(6, ExpeditionCampMath.RespitePool(c.RespiteBase, survivors: 0));
    }

    [TestMethod]
    public void UnknownTier_Throws()
    {
        TuningCamp c = Camp();
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ExpeditionCampMath.FoodRequired(c.FoodTiers, "brunch", 6));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ExpeditionCampMath.FoodEffect("brunch"));
    }
}
