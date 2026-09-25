using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🆕 **M1b 的补齐（清单 P6）**：按阶取属性的**入口纪律** —— 越界/缺阶要**报错**、旧字段要**零行为** ✓
///
/// 🔴 **口径声明（纪律 AU：先定义再数 · 这是本条最容易被数错的地方）**：
///   · P6 说的「**阶数 ∈ 1~5**」= **`weapon` / `armour` 数组里有几阶** ⇒ 原版 `weapon_0..4` = **整 5 阶** ✓
///   · 🔴 **不是**「这个单位现在第几阶」—— 那个概念叫 `O-101`（由 `Roster`/`Hero` 持有
///     `weaponTier`/`armourTier`），**本阶段代码里【不存在】** ⇒ 若把它当成 P6 的对象，
///     就会去测一个**还没有的东西**（`WeaponAt` 现在收的是 **0~4 的索引**）⚠️
///   ⇒ 故本件四条用例分别钉：**① 阶数越界报错 · ② 缺阶报错 · ③ 整段缺席合法 · ④ 旧字段零行为** ✓
/// </summary>
[TestClass]
public sealed class M1bTierValidationTests
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

    private static string Units() => ReadData("units.json");

    /// <summary>把 units.json 里**第一个** `"weapon": [` 的整段替换成给定文本（用 JSON 文本级构造反例 ✓）。</summary>
    private static string ReplaceFirstArray(string json, string key, string newArrayBody)
    {
        int start = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"data/units.json 里应当有 {key} 段 ⇒ 否则本用例的前提不成立（如实失败）✓");
        int open = json.IndexOf('[', start);
        int close = json.IndexOf(']', open);
        Assert.IsTrue(open > 0 && close > open, $"{key} 应当是数组 ✓");
        return string.Concat(json.AsSpan(0, open + 1), newArrayBody, json.AsSpan(close));
    }

    // ── ① 阶数越界 ⇒ 报错 ────────────────────────────────────────────

    [TestMethod]
    public void P6_1_TierCountMustBeExactlyFive_OutOfRangeThrows()
    {
        string raw = Units();

        // 现状：一手数据是**整 5 阶** ⇒ 先钉住"现在是对的"（否则后面的反例没有对照 ✓）
        UnitsConfig ok = UnitsConfig.Parse(raw);
        UnitConfig first = ok.Units.First(u => u.Weapon is not null);
        Assert.AreEqual(5, first.Weapon!.Count, $"\"{first.Id}\" 的 weapon 是**整 5 阶** ✓");

        // 反例 A：**只有 4 阶**（少一阶 ⇒ 后面按阶取会取到空位）
        string four = ReplaceFirstArray(raw, "weapon",
            "{\"atk_pct\":0,\"dmg_min\":1,\"dmg_max\":2,\"crit_pct\":0,\"spd\":0},"
            + "{\"atk_pct\":0,\"dmg_min\":1,\"dmg_max\":2,\"crit_pct\":0,\"spd\":0},"
            + "{\"atk_pct\":0,\"dmg_min\":1,\"dmg_max\":2,\"crit_pct\":0,\"spd\":0},"
            + "{\"atk_pct\":0,\"dmg_min\":1,\"dmg_max\":2,\"crit_pct\":0,\"spd\":0}");
        InvalidDataException a = Assert.ThrowsException<InvalidDataException>(
            () => UnitsConfig.Parse(four), "4 阶 ⇒ 报错（P6 ①）✓");
        StringAssert.Contains(a.Message, "4 阶", "错误信息要带上**实际阶数**（可检索 ✓）");

        // 反例 B：**6 阶**（多一阶 ⇒ 超出原版口径）
        string six = ReplaceFirstArray(raw, "weapon",
            string.Join(",",
                Enumerable.Range(0, 6).Select(_ =>
                    "{\"atk_pct\":0,\"dmg_min\":1,\"dmg_max\":2,\"crit_pct\":0,\"spd\":0}")));
        InvalidDataException b = Assert.ThrowsException<InvalidDataException>(
            () => UnitsConfig.Parse(six), "6 阶 ⇒ 报错（P6 ①）✓");
        StringAssert.Contains(b.Message, "6 阶", "错误信息要带上**实际阶数** ✓");

        Console.WriteLine($"[M1b] ① 阶数越界：整 5 阶 ✓ · 4 阶 ⇒ 报错（\"{Trim(a.Message)}\"）· " +
                          $"6 阶 ⇒ 报错（\"{Trim(b.Message)}\"）✓");
    }

    // ── ② 缺阶 ⇒ 报错（**不许变成 NRE**）──────────────────────────────

    [TestMethod]
    public void P6_2_MissingTierThrowsCleanly_NotNullReference()
    {
        // 反例：数组**仍在、但有一格是 `null`**（"给了壳、里面缺阶"）✓
        string holed = ReplaceFirstArray(Units(), "weapon", "null");
        // 🔴 反例构造后必须**仍是合法 JSON**（否则测的是语法错、不是缺阶 ✓）
        Assert.IsTrue(holed.Contains("\"weapon\": [null]", StringComparison.Ordinal),
            "反例形状 = `\"weapon\": [null]`（1 阶且为 null）✓");

        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => UnitsConfig.Parse(holed),
            "缺阶 ⇒ 必须是 **InvalidDataException**（**不是** NullReferenceException —— 不许把数据问题伪装成代码崩了 ✓）");

        StringAssert.Contains(ex.Message, "null", "错误信息要点名 null ✓");
        StringAssert.Contains(ex.Message, "缺阶", "错误信息要用**数据的语言**说（缺阶）✓");

        Console.WriteLine($"[M1b] ② 缺阶：`[null]` ⇒ `InvalidDataException`（\"{Trim(ex.Message)}\"）" +
                          "—— **不是 NRE** ✓");
    }

    // ── ③ 整段缺席 ⇒ 合法（阶段 1 允许不配）────────────────────────────

    [TestMethod]
    public void P6_3_WholeArrayAbsent_IsLegal_AndYieldsNullTiers()
    {
        // units.json 里**确实存在**没有 weapon 的单位（否则本条的前提不成立 ⇒ 如实失败 ✓）
        UnitsConfig cfg = UnitsConfig.Parse(Units());
        UnitConfig? bare = cfg.Units.FirstOrDefault(u => u.Weapon is null);
        Assert.IsNotNull(bare, "data/units.json 里应当有**没配 weapon** 的单位（否则前提不成立）✓");

        Assert.IsTrue(cfg.Units.Any(u => u.Weapon is not null), "同时也有配了的 ✓");
        Assert.AreEqual(0, bare!.Weapon?.Count ?? 0, "缺席 ⇒ 读出来是 null（**不是空数组**）✓");

        Console.WriteLine($"[M1b] ③ 整段缺席合法：\"{bare.Id}\" 无 weapon ⇒ `Weapon == null` ✓" +
                          $"（配了的共 {cfg.Units.Count(u => u.Weapon is not null)} 个）✓");
    }

    // ── ④ 旧字段零行为 ───────────────────────────────────────────────

    /// <summary>
    /// 🔴 **对照用例**（P6 ③）：`WeaponTiers`/`ArmourTiers` 为 **null** 时，**旧字段逐位不变** ✓
    /// 🔴 **并且要说清这条证明的边界**（架构 `#462` 的判据）：
    ///    「**换源不影响现有行为，但新数据的正确性未被任何用例验证**」——
    ///    本用例只能证明**"不配阶 ⇒ 旧读数一个都不变"**，**不能**证明"配了阶之后取出来的值是对的"，
    ///    也不能证明"伤害路径已经按阶变"（那是 M1c 阶段 3 = P7 的事）⚠️
    /// </summary>
    [TestMethod]
    public void P6_4_OldFieldsUnchanged_WhenTiersAreNull()
    {
        UnitsConfig cfg = UnitsConfig.Parse(Units());
        UnitConfig src = cfg.Units.First(u => u.Weapon is not null);

        // 造一个**只有旧字段**的模板：把两套阶都拿掉 ⇒ 其余字段逐位保留 ✓
        UnitStats withTiers = UnitStatsMapper.From(src);
        UnitStats without = UnitStatsMapper.From(src with { Weapon = null, Armour = null });

        Assert.IsNotNull(withTiers.WeaponTiers, "透传后 template 上**有**阶 ✓");
        Assert.IsNull(without.WeaponTiers, "不配阶 ⇒ template 上是 null ✓");
        Assert.IsNull(without.ArmourTiers, "护甲同理 ✓");

        // 🔴 "零行为" = **旧字段逐位相同**（不是"大概一样"⇒ 逐字段断言）
        Assert.AreEqual(withTiers.Hp, without.Hp, "Hp 不变 ✓");
        Assert.AreEqual(withTiers.Attack, without.Attack, "Attack 不变 ✓");
        Assert.AreEqual(withTiers.Prot, without.Prot, "Prot 不变 ✓");
        Assert.AreEqual(withTiers.Speed, without.Speed, "Speed 不变 ✓");
        Assert.AreEqual(withTiers.Dodge, without.Dodge, "Dodge 不变 ✓");
        Assert.AreEqual(withTiers.Crit, without.Crit, "Crit 不变 ✓");
        Assert.AreEqual(withTiers.Resilience, without.Resilience, "Resilience 不变 ✓");
        Assert.AreEqual(withTiers.StunResist, without.StunResist, "StunResist 不变 ✓");
        Assert.AreEqual(withTiers.BleedResist, without.BleedResist, "BleedResist 不变 ✓");
        Assert.AreEqual(withTiers.StatDebuffResist, without.StatDebuffResist, "StatDebuffResist 不变 ✓");
        Assert.AreEqual(withTiers.DisplaceResist, without.DisplaceResist, "DisplaceResist 不变 ✓");
        Assert.AreEqual(withTiers.DeathsDoorResist, without.DeathsDoorResist, "DeathsDoorResist 不变 ✓");
        Assert.AreEqual(withTiers.PoisonResist, without.PoisonResist, "PoisonResist 不变 ✓");
        Assert.AreEqual(withTiers.DiseaseResist, without.DiseaseResist, "DiseaseResist 不变 ✓");
        Assert.AreEqual(withTiers.TrapResist, without.TrapResist, "TrapResist 不变 ✓");
        Assert.AreEqual(withTiers.MoveDistance, without.MoveDistance, "MoveDistance 不变 ✓");
        Assert.AreEqual(withTiers.ProtFraction, without.ProtFraction, "ProtFraction（派生）也不变 ✓");

        // 🔴 `WeaponAt` 在"没配阶"时**如实返回 null**（不假装第 0 阶 ⇒ 决策 O-101 明令不许默认 0 阶 ✓）
        Assert.IsNull(without.WeaponAt(0), "没配阶 ⇒ `WeaponAt(0)` = **null**（**不许假装第 0 阶** ✓ `O-101`）");
        Assert.IsNull(without.ArmourAt(0), "护甲同理 ✓");
        Assert.AreEqual(withTiers.WeaponTiers![0], withTiers.WeaponAt(0), "配了阶 ⇒ 取第 0 阶 ✓");
        Assert.AreEqual(5, withTiers.WeaponTiers!.Count, "一手 5 阶 ✓");

        Console.WriteLine("[M1b] ④ 旧字段零行为：配阶/不配阶 ⇒ **17 个旧字段逐位相同** ✓；" +
                          "不配阶时 `WeaponAt(0)`/`ArmourAt(0)` = **null**（不假装 0 阶 ✓ O-101）；" +
                          "⚠️ 边界：**只证明「不配阶 ⇒ 旧读数不变」**，不证明「配了阶之后取值正确」（那是 P7）✓");
    }

    private static string Trim(string s) => s.Length <= 60 ? s : s[..60] + "…";

    public TestContext TestContext { get; set; } = null!;
}
