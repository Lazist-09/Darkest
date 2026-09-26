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
    public void SkillField_DefaultsToNull_SoTheOldModelStaysDefault()
    {
        // 🔴 **默认零行为**的证据：已入库的 skills.json **只填了有依据的那些**（其余为 null）
        //    而**伤害路径尚未读 `dmg_pct`** ⇒ 无论填没填都走旧模型 ✓
        SkillsConfig shipped = SkillsConfig.Parse(ReadData("skills.json"));
        int withPct = 0, total = 0;
        foreach (SkillTemplateConfig s in shipped.Skills)
        {
            total++;
            if (s.DmgPct is not null)
            {
                withPct++;
            }
        }

        // 🔴 **A2 更新（2026-09-26）**：判据已改为【数值一律采用本地参考项目】⇒ 现在 **14 填 / 30 未填** ✓
        //    14 = ①【明确】6 条（clean/war_cry 等，`ref:` 注记）
        //       + ②【候选】但库里已有值、且**自己的注记点名了参考技能**、实测相等 ⇒ **保留** 6 条
        //       + ③同上但实测**不等** ⇒ **纠正** 2 条（`warrior_lunge` -50→**-55** · `commissar_burst_fire` -50→**-60**）
        //    🔴 旧的"非明确一律撤出"口径**已废**：它会把 ② 的 6 条**对的数**也撤掉
        //       （`_dmg_pct_source` 里的 `dd1:<skill>` 就是【策划 §43 表】早已做过的映射声明 ⇒ 不是"我推的"）
        //    ⚠️ 剩 30 条**无可落之值**：映射本身待**策划逐行裁定**（逐行请求见 `reports/ref_skill_dmg_source.md`）
        //    ⚠️ 而**伤害路径仍未读 `dmg_pct`** ⇒ 所以这 14 条是**零行为占位** ✓（阶段 3 切换读它才会生效 ✓）
        Assert.AreEqual(14, withPct, $"A2 后应为 **14** 条已填（{total - 14} 条未填 ✓；候选 30 条待策划逐行裁定 ✓）");
        Assert.AreEqual(total - 14, total - withPct, "其余仍未填 ⇒ 走旧模型 ✓");
        Console.WriteLine($"[M1c·阶段3] `dmg_pct` 已就位（{withPct}/{total}）但**伤害路径未读** ⇒ **零行为** ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
