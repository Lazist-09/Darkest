using System;
using System.IO;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **目标 ③④ 减伤侧机制的用例**（`TierDefence`）✓
///
/// 依据：一手/第三方的护甲表是**逐阶**的（`armour[i].{def_pct, prot, hp}` ✓，已在轮 1 改成一手 ✓）
/// 🔴 **零行为**：本件只验证**纯函数**；"有没有人消费它"由 `WeaponDamageModelStage1Tests` 的
///   "**无人消费**"守卫钉住（已把 `TierDefence` 纳入 ✓）
/// </summary>
[TestClass]
public sealed class TierDefenceTests
{
    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string c = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(c))
            {
                return File.ReadAllText(c);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    [TestMethod]
    public void WithFiveTiers_ValuesComeFromTheRequestedTier()
    {
        UnitsConfig shipped = UnitsConfig.Parse(ReadData("units.json"));
        UnitStats warrior = UnitStatsMapper.From(shipped.Get("warrior"));

        Console.WriteLine("[③④·减伤] warrior（一手 hellion）+ 逐阶对照：");
        for (int tier = 0; tier < 5; tier++)
        {
            ArmourTier expect = warrior.ArmourAt(tier)!;
            Assert.AreEqual(expect.Prot, TierDefence.ProtAt(warrior, tier), $"tier{tier} prot 取自该阶 ✓");
            Assert.AreEqual(expect.DefPct, TierDefence.DefAt(warrior, tier), $"tier{tier} def 取自该阶 ✓");
            Assert.AreEqual((expect.Prot, expect.DefPct, expect.Hp), TierDefence.ArmourViewAt(warrior, tier),
                $"tier{tier} 三读数与护甲表一致 ✓");

            double frac = TierDefence.ProtFractionAt(warrior, tier);
            Assert.AreEqual(Math.Clamp(expect.Prot / 100.0, 0.0, 0.85), frac, 1e-9, "比例换算 + 0.85 封顶 ✓");
            Console.WriteLine($"    tier{tier}: prot={expect.Prot} · def(闪避)={expect.DefPct} · hp={expect.Hp} · 减伤比例={frac:0.###}");
        }
    }

    [TestMethod]
    public void TierIsClamped_SoAnOutOfRangeTierCannotCrash()
    {
        UnitsConfig shipped = UnitsConfig.Parse(ReadData("units.json"));
        UnitStats warrior = UnitStatsMapper.From(shipped.Get("warrior"));

        Assert.AreEqual(TierDefence.ProtAt(warrior, 0), TierDefence.ProtAt(warrior, -3), "下越界 ⇒ 钳到第 0 阶 ✓");
        Assert.AreEqual(TierDefence.ProtAt(warrior, 4), TierDefence.ProtAt(warrior, 99), "上越界 ⇒ 钳到第 4 阶 ✓");
        Console.WriteLine("[③④·减伤] 越界钳制：-3 ⇒ tier0 · 99 ⇒ tier4 ✓");
    }

    [TestMethod]
    public void WithoutTiers_ItFallsBackToTheTopLevelValues()
    {
        // 敌人（我们自设，没有 5 阶）⇒ **退回顶层**（不许崩、也不许假装有阶 ✓）
        UnitsConfig shipped = UnitsConfig.Parse(ReadData("units.json"));
        UnitStats enemy = UnitStatsMapper.From(shipped.Get("melee_soldier"));

        Assert.IsNull(enemy.ArmourAt(0), "前提：敌人没有 5 阶 ✓");
        Assert.AreEqual(enemy.Prot, TierDefence.ProtAt(enemy, 2), "无阶 ⇒ prot 退回顶层 ✓");
        Assert.AreEqual(enemy.Dodge, TierDefence.DefAt(enemy, 2), "无阶 ⇒ 闪避退回顶层 ✓");
        Assert.AreEqual((enemy.Prot, enemy.Dodge, enemy.Hp), TierDefence.ArmourViewAt(enemy, 2), "三读数退回顶层 ✓");

        Console.WriteLine($"[③④·减伤] 敌人（无 5 阶）回退：prot={enemy.Prot} · dodge={enemy.Dodge} · hp={enemy.Hp} ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
