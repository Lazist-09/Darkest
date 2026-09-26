using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **A3 的【来源守卫】**：我方 4 个英雄的 `weapon`/`armour` 5 阶，必须与**参考项目**的值逐字段相等 ✓
///
/// WHY 需要这条（而不是只靠落库器跑一次）：
///   `units.json` 是**人手也会改**的文件；一旦有人改了阶数值，落库器不会自动跑 ⇒
///   数据就会**悄悄漂移**回"一手 E 盘"或某个中间版本 ⚠️ ⇒ 本用例把"值 = 参考项目"钉成**机器判据** ✓
///
/// 判据源：本仓**冻结件** `reports/dd1_hero_tables_from_unity_ref.json`
///   （`source` 字段写明它来自参考项目 `Assets/Resources/Data/Heroes/Info/*.bytes` ✓）
///   ⇒ 本用例**不读** `F:\GithubPro\Darkest-Dungeon-Unity`（外部盘不可依赖），
///     但落库器 `tools/dsh/land_ref_hero_tiers.py` 每次跑都会**重新读外部 `.bytes`** 并与该冻结件对账 ⇒ 两头锁住 ✓
///
/// 🔴 **零行为**：只读两个 JSON + 断言 ⇒ 不改任何数值、不接生产路径 ✓
/// </summary>
[TestClass]
public sealed class A3HeroTierSourceTests
{
    /// <summary>我方原型 → 参考项目英雄（与 `units.json` 的 `_align` 注记同一配对）✓</summary>
    private static readonly (string Ours, string Ref)[] Map =
    {
        ("warrior", "Hellion"),
        ("tank", "ManAtArms"),
        ("medic", "PlagueDoctor"),
        ("commissar", "Highwayman"),
    };

    private static readonly (string Ours, string Ref)[] WeaponFields =
    {
        ("atk_pct", "atk"), ("dmg_min", "dmg_min"), ("dmg_max", "dmg_max"),
        ("crit_pct", "crit"), ("spd", "spd"),
    };

    private static readonly (string Ours, string Ref)[] ArmourFields =
    {
        ("def_pct", "def"), ("prot", "prot"), ("hp", "hp"), ("spd", "spd"),
    };

    private static string FindUpwards(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"找不到 {string.Join('/', parts)}。");
    }

    private static UnitsConfig Units() => UnitsConfig.Parse(File.ReadAllText(FindUpwards("data", "units.json")));

    private static JsonElement FrozenHero(string hero)
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(
            FindUpwards("reports", "dd1_hero_tables_from_unity_ref.json")));
        return doc.RootElement.GetProperty("heroes").GetProperty(hero);
    }

    private static JsonElement FrozenRoot()
    {
        JsonDocument doc = JsonDocument.Parse(File.ReadAllText(
            FindUpwards("reports", "dd1_hero_tables_from_unity_ref.json")));
        return doc.RootElement;
    }

    [TestMethod]
    public void AllFourHeroesTiersEqualTheReferenceProject()
    {
        UnitsConfig units = Units();
        var rows = new List<string> { "[A3·来源守卫] 我方 4 英雄 × 5 阶 × 9 字段 = 180 个字段 ⇒ 逐个与参考项目对账" };
        int compared = 0;
        var fractions = new List<string>();

        foreach ((string ours, string refName) in Map)
        {
            UnitConfig u = units.Get(ours);
            JsonElement r = FrozenHero(refName);

            Assert.IsNotNull(u.Weapon, $"{ours}.weapon 必须存在（5 阶）✓");
            Assert.IsNotNull(u.Armour, $"{ours}.armour 必须存在（5 阶）✓");
            Assert.AreEqual(5, u.Weapon!.Count, $"{ours}.weapon 必须是 5 阶 ✓");
            Assert.AreEqual(5, u.Armour!.Count, $"{ours}.armour 必须是 5 阶 ✓");

            for (int i = 0; i < 5; i++)
            {
                foreach ((string mine, string theirs) in WeaponFields)
                {
                    double expected = r.GetProperty("weapon")[i].GetProperty(theirs).GetDouble();
                    double actual = mine switch
                    {
                        "atk_pct" => u.Weapon[i].AtkPct,
                        "dmg_min" => u.Weapon[i].DmgMin,
                        "dmg_max" => u.Weapon[i].DmgMax,
                        "crit_pct" => u.Weapon[i].CritPct,
                        _ => u.Weapon[i].Spd,
                    };
                    Assert.AreEqual(expected, actual, 1e-9,
                        $"{ours}.weapon[{i}].{mine} 必须等于参考 `{refName}.weapon[{i}].{theirs}` = {expected} ✓");
                    compared++;
                    if (Math.Abs(expected - Math.Round(expected)) > 1e-9)
                    {
                        fractions.Add($"{ours}.weapon[{i}].{mine}={expected}");
                    }
                }

                foreach ((string mine, string theirs) in ArmourFields)
                {
                    double expected = r.GetProperty("armour")[i].GetProperty(theirs).GetDouble();
                    double actual = mine switch
                    {
                        "def_pct" => u.Armour[i].DefPct,
                        "prot" => u.Armour[i].Prot,
                        "hp" => u.Armour[i].Hp,
                        _ => u.Armour[i].Spd,
                    };
                    Assert.AreEqual(expected, actual, 1e-9,
                        $"{ours}.armour[{i}].{mine} 必须等于参考 `{refName}.armour[{i}].{theirs}` = {expected} ✓");
                    compared++;
                }
            }

            rows.Add($"[A3·来源守卫] {ours,-10} ← {refName,-13} 45 个字段全等 ✓");
        }

        // 🔴 反向断言：**必须真的出现非整数** —— 否则这条用例可能只是在"整数世界"里自证
        //    （参考项目 `weapon.crit` 有 2.5 / 3.75 / 4.25 这类值；我方 4 英雄里共 13 个）✓
        Assert.AreEqual(13, fractions.Count,
            $"参考项目里我方 4 英雄的非整数字段应恰为 13 个（实测 {fractions.Count}）⇒ 若为 0，说明值退回了整数版本 ✓");
        Assert.IsTrue(fractions.All(f => f.Contains("crit_pct")),
            "参考项目里**只有 `weapon.crit` 一列是非整数**（`atk`/`prot`/`spd` 恒 0，其余 8 列全整数）✓");
        rows.Add($"[A3·来源守卫] 非整数字段 **{fractions.Count}** 个，全在 `weapon[*].crit_pct`：{string.Join(" · ", fractions)}");
        rows.Add($"[A3·来源守卫] 共对账 **{compared}** 个字段 ⇒ **全部等于参考项目** ✓");

        foreach (string row in rows)
        {
            Console.WriteLine(row);
        }
    }

    /// <summary>`CritPct` 必须能装小数 —— 这是 A3 放宽类型（`int` → `double`）的**编译期**守卫 ✓</summary>
    [TestMethod]
    public void CritPctHoldsFractions()
    {
        UnitConfig warrior = Units().Get("warrior");
        double t0 = warrior.Weapon![0].CritPct;
        Assert.AreEqual(2.5, t0, 1e-9, "warrior.weapon[0].crit_pct 必须是 2.5（`Hellion.bytes:16` = `.crit 2.5%`）✓");
        Console.WriteLine($"[A3·类型守卫] `WeaponTier.CritPct` 装得下小数（warrior tier0 = {t0}）✓ 零行为：该字段无任何读取点 ✓");
    }

    /// <summary>出处守卫（纪律 AT/AZ）：4 条 `_align` 必须指向**参考项目**，不能残留"一手 E 盘为准" ✓</summary>
    [TestMethod]
    public void AlignNotesPointAtTheReferenceProject()
    {
        string raw = File.ReadAllText(FindUpwards("data", "units.json"));
        JsonDocument doc = JsonDocument.Parse(raw);
        int checked_ = 0;
        foreach ((string ours, string refName) in Map)
        {
            JsonElement unit = doc.RootElement.GetProperty("units").EnumerateArray()
                .First(x => x.GetProperty("id").GetString() == ours);
            string note = unit.GetProperty("_align").GetString() ?? "";
            Assert.IsTrue(note.Contains("Darkest-Dungeon-Unity"),
                $"{ours}._align 必须点名参考项目路径（出处可核）✓ 实测：{note}");
            Assert.IsTrue(note.Contains(refName + ".bytes"),
                $"{ours}._align 必须点名具体来源文件 `{refName}.bytes` ✓ 实测：{note}");
            Assert.IsFalse(note.Contains("一手 E 盘"),
                $"{ours}._align 不得残留「一手 E 盘」口径（该口径已按用户指令作废）✓ 实测：{note}");
            checked_++;
        }

        JsonElement root = FrozenRoot();
        string src = root.GetProperty("source").GetString() ?? "";
        Assert.IsTrue(src.Contains("Heroes") && src.Contains("Info"),
            $"冻结件的 `source` 必须指向参考项目的 Heroes/Info ⇒ 实测：{src}");
        Assert.IsTrue(src.Contains("Darkest-Dungeon-Unity"),
            $"冻结件的 `source` 必须点名参考项目路径 ⇒ 实测：{src}");
        Console.WriteLine($"[A3·出处守卫] {checked_} 条 `_align` 全部指向参考项目 ✓ · 冻结件 source = {src} ✓");
    }
}
