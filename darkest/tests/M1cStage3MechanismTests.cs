using System;
using System.Collections.Generic;
using System.IO;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Pipeline;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M1c 阶段 3 的机制用例**：`武器区间 × (1 + dmg%)` ✓
///
/// 策划给的**两个试点值**（一手 `crusader.info.darkest` ✓）：`smite **0%**` · `zealous_accusation **−40%**` ✓
///   —— 一个 0%（无修正）· 一个负修正 ⇒ **同时验证"区间原样"与"修正生效"** ✓（他的原话 ✓）
/// 🔴 **零行为**：本件的机制**没有生产调用方**（两个前置见 `WeaponBaseDamage` 的注释 ✓）
/// </summary>
[TestClass]
public sealed class M1cStage3MechanismTests
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
    public void PilotValues_ProduceTheWeaponRangeNumbers()
    {
        // 用**已入库**的 warrior 武器 5 阶（一手：tier0 6-12 ⇒ 中点 9 · tier4 10-19 ⇒ 中点 14.5 ✓）
        UnitConfig warrior = UnitsConfig.Parse(ReadData("units.json")).Get("warrior");
        IReadOnlyList<WeaponTier> tiers = warrior.Weapon!;

        // `smite` = **0%** ⇒ 区间原样 ✓
        Assert.AreEqual(9, WeaponBaseDamage.Damage(tiers, 0, 1.0, 0, 0.5), "tier0 中点 9 ×(1+0%) = 9 ✓");
        Assert.AreEqual(15, WeaponBaseDamage.Damage(tiers, 4, 1.0, 0, 0.5), "tier4 中点 14.5 ⇒ 15（四舍五入）✓");

        // `zealous_accusation` = **−40%** ⇒ 修正生效 ✓
        Assert.AreEqual(5, WeaponBaseDamage.Damage(tiers, 0, 1.0, -40, 0.5), "tier0 9 ×0.6 = 5.4 ⇒ 5 ✓");
        Assert.AreEqual(9, WeaponBaseDamage.Damage(tiers, 4, 1.0, -40, 0.5), "tier4 14.5 ×0.6 = 8.7 ⇒ 9 ✓");

        Console.WriteLine("[M1c·阶段3] 试点读数（warrior）："
            + "tier0 9→**9**(0%) / **5**(−40%) · tier4 14.5→**15**(0%) / **9**(−40%) ✓");
    }

    [TestMethod]
    public void SegmentMultipliers_StillApply_SoTheShapeIsTheSameAxis()
    {
        // 段倍率仍然乘在区间上（与旧模型**同一根轴** ⇒ 可逐项对照 ✓）
        UnitConfig warrior = UnitsConfig.Parse(ReadData("units.json")).Get("warrior");
        IReadOnlyList<WeaponTier> tiers = warrior.Weapon!;

        double one = WeaponBaseDamage.Raw(tiers, 0, 1.0, 0, 0.5);
        double two = WeaponBaseDamage.Raw(tiers, 0, 2.0, 0, 0.5);
        Assert.AreEqual(one * 2, two, 1e-9, "Σ段倍率 2.0 ⇒ 基础值翻倍 ✓");

        Console.WriteLine($"[M1c·阶段3] Σ倍率仍是同一根轴：×1 ⇒ {one:0.##} · ×2 ⇒ {two:0.##} ✓");
    }

    [TestMethod]
    public void EverySkillNowDeclaresDmgPct_AndTheReferencelessOnesAreExplicitlyZero()
    {
        // 🔴 **可控性（不是"零行为"）的证据**：`skills.json` 的 44 条**全部**有了 `dmg_pct` ✓
        //    ⇒ 策划 `#475` 裁定：**参考项目答不出来的 30 条一律取 0**（= 不做修正 = 最保守）✓
        //    🔴 **关键**：那 30 条**必须能被分辨出来** —— 它们标了 `value_source: none` + `origin: ours` ✓
        //       （否则将来审计会说"44 条都有值 ⇒ 我们完全对齐了"，而实际只有 14 条有出处 ⚠️）
        SkillsConfig shipped = SkillsConfig.Parse(ReadData("skills.json"));
        int total = 0, withPct = 0, fromRef = 0, fromOurs = 0;
        foreach (SkillTemplateConfig s in shipped.Skills)
        {
            total++;
            if (s.DmgPct is not null)
            {
                withPct++;
            }
        }

        // 🔴 **两个维度现在都是【被声明的契约字段】**（`SkillsConfig`）——
        //    不声明的话死数据门禁会报"疑似死数据"（实测 30 处）⇒ 声明 = 把"这个键是有意加的"写进契约 ✓
        foreach (SkillTemplateConfig s in shipped.Skills)
        {
            if (s.ValueSource == "none" && s.Origin == "ours")
            {
                fromOurs++;
                Assert.AreEqual(0, s.DmgPct,
                    $"{s.Id}：`value_source: none` 的**必须恰好是 0**（不做修正）✓");
                Assert.IsNull(s.DmgPctSource,
                    $"{s.Id}：`value_source: none` 的不该同时声称有参考出处（两维必须一致）✓");
            }
            else
            {
                fromRef++;
                Assert.IsNotNull(s.DmgPctSource,
                    $"{s.Id}：有出处的那些**必须点名参考技能**（`_dmg_pct_source`）✓");
            }
        }

        Assert.AreEqual(44, total, "技能总数 44 ✓");
        Assert.AreEqual(44, withPct, $"策划 `#475` 后应为 **44/44 全有 `dmg_pct`**（实测 {withPct}）✓");
        Assert.AreEqual(14, fromRef, $"**有参考出处**的应为 **14** 条（实测 {fromRef}）✓");
        Assert.AreEqual(30, fromOurs, $"**我们自加、显式取 0** 的应为 **30** 条（实测 {fromOurs}）✓");
        Console.WriteLine($"[M1c·阶段3] `dmg_pct` **{withPct}/{total}** 全覆盖：**有出处 {fromRef}**（`ref:` 点名）"
            + $" + **自加取 0 {fromOurs}**（`value_source:none`/`origin:ours`）⇒ 两者**可分辨** ✓ "
            + "⚠️ **伤害路径仍未读它** ⇒ 本件是**可控性**证明，不是行为证明 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
