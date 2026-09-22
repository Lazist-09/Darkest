using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M9 · 4v4 的【验收夹具】**（策划 `DELIVERY-DESIGNER-FOUR-MEANINGS-TO-LEAD-20260922` ② ✓）
///
/// 🔴 **纪律 BG（策划新立）**：**「这 4 个人，是【开局名单】、【编队上限】还是【测试夹具】？」** ——
///   三者混谈 ⇒ "点名"会被当成"改规则" ⚠️（**这次正是这样发生的** ✓）
///   ⇒ 架构裁定的**三种 4 人**：
///     ① **开局名单**（规则 · 不改）= 一手 `first_hero_classes: [plague_doctor, vestal]`（**2 人** ✓）
///     ② **编队上限**（规则 · 已裁 O-95 (c)）= **4 人** ✓
///     ③ 🆕 **验收夹具编队**（**不是规则** ✓）= **warrior + tank + medic + commissar** ✓ ← 本件钉住它
///
/// 🎖️ **为什么夹具用 4 原型**（架构给的三条理由 ✓）：① 已有 5 阶数据且已对齐一手 ✓
///   ② 已有我们的 4 职业语义（`origin:ours`）⇒ 夹具不该混入"未对齐数据"的噪音 ✓ ③ 可复现（同 seed + 同 4 人 ⇒ 读数可比 ✓）
///
/// 🔴 **零行为**：本件只**钉住夹具定义**（一条常量 + 对 `units.json` 的存在性断言 ✓）⇒ 不改任何规则 ✓
/// </summary>
[TestClass]
public sealed class M9FourVFourFixtureTests
{
    /// <summary>🆕 **验收夹具编队**（架构裁 ✓ —— 是**夹具**，不是规则 ✓）</summary>
    public static readonly string[] FixtureLineup = { "warrior", "tank", "medic", "commissar" };

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
    public void FixtureLineup_IsTheFourArchetypes_AndAllOfThemExist()
    {
        Assert.AreEqual(4, FixtureLineup.Length, "夹具 = **4 人**（对应编队上限 4 ✓）");

        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        foreach (string id in FixtureLineup)
        {
            _ = units.Get(id);   // 不存在会抛 ⇒ 夹具不许引用不存在的单位 ✓
        }

        Console.WriteLine($"[M9·4v4 夹具] **{string.Join(" + ", FixtureLineup)}**（架构裁：**夹具**，不是规则 ✓）");
        Console.WriteLine("[M9·4v4] 另两种 4 人（纪律 BG）：开局名单 = 一手 `[plague_doctor, vestal]`（2 人 · 不改）· 编队上限 = 4（O-95 (c) ✓）");
    }

    [TestMethod]
    public void FixtureUnits_AlreadyCarryTheFiveTierTables_SoReadingsAreComparable()
    {
        // 架构给的理由 ①：夹具单位**已有 5 阶数据且已对齐一手** ⇒ 跑 4v4 读数时不混入"未对齐数据"的噪音 ✓
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        foreach (string id in FixtureLineup)
        {
            UnitConfig u = units.Get(id);
            Assert.IsNotNull(u.Weapon, $"{id} 应有 5 阶武器表 ✓");
            Assert.IsNotNull(u.Armour, $"{id} 应有 5 阶护甲表 ✓");
            Assert.AreEqual(5, u.Weapon!.Count, $"{id} 武器 5 阶 ✓");
            Assert.AreEqual(5, u.Armour!.Count, $"{id} 护甲 5 阶 ✓");
        }

        Console.WriteLine("[M9·4v4 夹具] 4 人都有 5 阶武器/护甲表（已对齐一手 ✓）⇒ 读数可比 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
