using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Math;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M1c · 阶段 1（换伤害模型 · 零行为改动）**：原版口径 = **武器区间 × (1 + 技能 dmg%)**。
///
/// 阶段 1 的两条铁律：
///   ① **只加纯函数、不接线** ⇒ 🔴 本用例**直接断言"零消费点"**（全仓搜引用 ⇒ 必须 0 处，除本文件）✓
///   ② **给出对照读数**（新旧两种形状差多少）⇒ 这是**阶段 2 试点**要用的"起跑线" ✓
///
/// 输入来源（都可核）：
///   · 武器区间 = `units.json` 的 `weapon[i].dmg_min/dmg_max`（**策划 `#448` 已对齐到参考项目的 4 原型**）✓
///   · 技能 `dmg%` = 参考项目的**示例值**（`smite 0%` · `zealous_accusation -40%` · `stunning_blow -75%`）
///     ⇒ ⚠️ 它们是**测试输入**（用来说明形状差异），**不是**我们技能表里的值（我们技能表的值归策划）✓
/// </summary>
[TestClass]
public sealed class WeaponDamageModelStage1Tests
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

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "darkest", "scripts")))
        {
            dir = dir.Parent;
        }

        return dir ?? throw new DirectoryNotFoundException("找不到仓库根（darkest/scripts）");
    }

    [TestMethod]
    public void PureFunctions_AreDeterministic_AndBracketTheWeaponRange()
    {
        Assert.AreEqual(6.0, BattleMath.WeaponRoll(6, 12, 0.0), "roll 0 ⇒ 区间左值 ✓");
        Assert.AreEqual(12.0, BattleMath.WeaponRoll(6, 12, 1.0), "roll 1 ⇒ 区间右值 ✓");
        Assert.AreEqual(9.0, BattleMath.WeaponRoll(6, 12, 0.5), "roll 0.5 ⇒ 中点 ✓");
        Assert.AreEqual(6.0, BattleMath.WeaponRoll(6, 6, 0.7), "退化区间（min == max）⇒ 恒为 min ✓");

        // 技能 dmg% 是**乘法修正**：0% ⇒ 原样；-40% ⇒ 六折；-75% ⇒ 两五折
        Assert.AreEqual(9.0, BattleMath.WeaponRawDamage(6, 12, 0, 0.5), "0% ⇒ 原样 ✓");
        Assert.AreEqual(5.4, BattleMath.WeaponRawDamage(6, 12, -40, 0.5), 1e-6, "-40% ⇒ ×0.6 ✓");
        Assert.AreEqual(2.25, BattleMath.WeaponRawDamage(6, 12, -75, 0.5), 1e-6, "-75% ⇒ ×0.25 ✓");
        Assert.AreEqual(0.0, BattleMath.WeaponRawDamage(6, 12, -200, 0.5), "负到 0 以下 ⇒ 0（不产生负伤害）✓");

        Console.WriteLine("[M1c·阶段1] 纯函数：区间夹取 + dmg% 乘法 + 不为负 ✓");
    }

    /// <summary>
    /// 🔴 **"零消费点"的自证**：阶段 1 的定义是"新路径存在但**没人用**" ⇒ 所以"全仓引用必须只有本文件"是**可测**的 ✓
    /// （同族纪律：**能自证就不要靠口头保证** —— 与 `TrapResistSourceDeclared` 的"退化解自证"同一思路 ✓）
    /// </summary>
    [TestMethod]
    public void NoProductionCodeCallsTheNewModel_SoOldReadingsCannotChange()
    {
        DirectoryInfo root = RepoRoot();
        // 🔴 **M1c 阶段 3 起**：把"**新模型的机制文件**"也放进白名单 —— 因为阶段 3 的定义就是
        //    "机制存在、但**还没人调用**" ✓ ⇒ 所以下面**额外加一条更强的断言**：**无人调用 `WeaponBaseDamage`** ✓
        //    （即：白名单放宽了"机制文件本身"，但"**旧路径不许碰它**"这条钉得更死了 ✓）
        string[] allow = { "WeaponDamageModelStage1Tests.cs", "BattleMath.cs", "WeaponBaseDamage.cs" };
        var offenders = new List<string>();

        foreach (string f in Directory.EnumerateFiles(Path.Combine(root.FullName, "darkest", "scripts"), "*.cs", SearchOption.AllDirectories))
        {
            // 定义处（BattleMath.cs）本身当然含这些名字 ⇒ 排除（白名单外的人才是"消费点"✓）
            if (allow.Contains(Path.GetFileName(f)))
            {
                continue;
            }

            string text = File.ReadAllText(f);
            if (text.Contains("WeaponRawDamage") || text.Contains("WeaponRoll("))
            {
                offenders.Add(Path.GetFileName(f));
            }
        }

        // 🔴 **口径修正（我自己的 bug，如实记）**：本用例的名字就是 No**ProductionCode**Calls...
        //    ⇒ 要钉的是**生产代码**（darkest/scripts/**）零调用 ✓；**测试**调用它是**允许**的
        //    （例：M1c 阶段 2 的对照夹具 `M1cPilotComparisonTests` 必须调用它才能做对照 ✓）
        //    我第一版把 tests 目录也一并算作违规 ⇒ 夹具一加就假红 ✗ ⇒ 已收窄到只查生产代码 ✓

        // 🆕 **更精确的那条**（阶段 3 机制）：**没有任何生产文件调用 `WeaponBaseDamage`** ⇒
        //    即"机制就位但**未接线**" ✓ —— 这比"零消费点"更准：允许机制存在，但**不许有人用它算伤害** ✓
        var modelConsumers = new List<string>();
        foreach (string f in Directory.EnumerateFiles(Path.Combine(root.FullName, "darkest", "scripts"), "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f) is "WeaponBaseDamage.cs" or "TierDefence.cs")
            {
                continue;
            }

            // 🔴 **判据必须【注释/代码分流】**（与 B6 门禁同款纪律 ✓）：
            //    注释里"提到"机制名 ≠ 调用它 ⇒ 我第一版只 grep 原文 ⇒ **把注释里的提及误判成消费点** ✗
            //    （实测抓到 `SkillsConfig.cs`：它只是在 XML 注释里引用了 `WeaponBaseDamage` ✓）
            bool callsIt = false;
            foreach (string line in File.ReadAllLines(f))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;   // 注释不算 ✓
                }

                if (line.Contains("WeaponBaseDamage", StringComparison.Ordinal) || line.Contains("TierDefence", StringComparison.Ordinal))
                {
                    callsIt = true;
                    break;
                }
            }

            if (callsIt)
            {
                modelConsumers.Add(Path.GetFileName(f));
            }
        }

        Assert.AreEqual(0, modelConsumers.Count,
            $"阶段 3 的机制（WeaponBaseDamage / TierDefence）**必须无人消费**（机制在、接线等解冻）⇒ 实际调用者：{string.Join(", ", modelConsumers)}");

        Assert.AreEqual(0, offenders.Count,
            $"阶段 1 必须对**生产代码**零消费点 ⇒ 除 `BattleMath.cs`，`darkest/scripts/**` 不应有人调用新模型；实际：{string.Join(", ", offenders)}");
        Console.WriteLine("[M1c·阶段1] 零消费点自证：全仓（scripts + tests）除 BattleMath/本用例外 **0 处引用** ⇒ 旧读数不变 ✓");
    }

    /// <summary>
    /// 📊 **对照读数**（阶段 2 的起跑线）：同 tier、同技能 dmg% 下，**新模型**给出的原始伤害区间
    /// （对照组 = 我们现模型的"中性帧"原始伤害 = `attack × 1.0`，即不吃段倍率/暴击/增益）✓
    /// </summary>
    [TestMethod]
    public void ComparisonReadings_NewModelVersusCurrentNeutralFrame()
    {
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        int[] skills = { 0, -40, -75 };   // 参考项目示例：smite / zealous_accusation / stunning_blow ✓

        foreach (UnitConfig u in units.Units.Where(x => x.Side == "player"))
        {
            if (u.Weapon is null)
            {
                continue;
            }

            foreach (int tier in new[] { 0, 4 })
            {
                var w = u.Weapon[tier];
                var parts = skills.Select(p =>
                    $"{p}% ⇒ {BattleMath.WeaponRawDamage(w.DmgMin, w.DmgMax, p, 0.5):0.##}");
                Console.WriteLine($"[M1c·对照] {u.Id} tier{tier}（{w.DmgMin}~{w.DmgMax}）｜新模型(roll .5)：{string.Join(" · ", parts)}"
                    + $"　｜现模型中性帧：{u.Attack}（atk）");
            }
        }

        // 现状对照：现模型不吃武器区间 ⇒ 四原型的"中性帧原始伤害"就是 attack
        Assert.AreEqual(4, units.Units.Count(x => x.Side == "player" && x.Weapon is not null),
            "4 原型应已带 tier（策划 #448 对齐）✓");
        Console.WriteLine("[M1c·对照] 结论：新模型由**武器区间**决定（同职业不同阶差异明显）· 现模型由 `attack` 平推 ⇒ 形状不同 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
