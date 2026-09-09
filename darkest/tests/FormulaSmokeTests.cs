using Darkest.Core.Math;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M0 冒烟单测（T-M0-05）：复算 combat_math §7.1 任意 2 行样例。
/// 其余 6 行与钳制/减免极值/多段用例归 M2（blueprint §10）。
/// </summary>
[TestClass]
public sealed class FormulaSmokeTests
{
    /// <summary>
    /// 战士 → 近战小兵 = 12 × 1.0 × (1 − 8/38) = 9（物理，最简路径；combat_math §7.1 行 1）。
    /// </summary>
    [TestMethod]
    public void Warrior_To_MeleeMook_Physical_Is_9()
    {
        int damage = BattleMath.PhysicalHit(attack: 12, skillMultiplier: 1.0, defense: 8);
        Assert.AreEqual(9, damage);
    }

    /// <summary>
    /// 施法者 → 战士(精神) = 12 × 0.8 × (1 − 20%) = 7.68 → 8（combat_math §7.1 行 8）。
    /// 7.68 进位断言 round() 四舍五入而非截断（截断得 7），钉死 §2.3 口径。
    /// 韧性 50 ⇒ 精神减免 50/250 = 20%（§2.2/#158）。
    /// </summary>
    [TestMethod]
    public void Caster_To_Warrior_Spirit_Is_8_RoundsNotTruncates()
    {
        int damage = BattleMath.SpiritHit(attack: 12, skillMultiplier: 0.8, resilience: 50);
        Assert.AreEqual(8, damage);
    }

    /// <summary>任何来源伤害最低 1 点（combat_math §2.3）—— 防呆冒烟。</summary>
    [TestMethod]
    public void Damage_Never_Below_One()
    {
        Assert.AreEqual(1, BattleMath.PhysicalHit(attack: 1, skillMultiplier: 0.1, defense: 999));
        Assert.AreEqual(1, BattleMath.SpiritHit(attack: 1, skillMultiplier: 0.1, resilience: 1000));
    }
}
