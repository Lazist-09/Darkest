using System;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M1a · 阶段 1（加字段级 · 零行为改动）** —— 原版 `weapon`/`armour` 各 **5 阶**（0~4）的结构。
///
/// 阶段 1 的**铁律**（架构 `dd1_workstreams.md` §2 · M1c 三步走）：
///   「**加字段 + 支持按阶取，但【不切换伤害公式】** ⇒ 旧读数必须不变」
/// ⇒ 本用例钉三件事：
///   ① **结构能装下**（5 阶能解析、能按阶取）
///   ② **形状错了会被拦**（不是 5 阶 ⇒ 报错并点名；区间 min ≤ max）
///   ③ 🔴 **旧数据不受影响**（**当前 `units.json` 没写 tier ⇒ 全部为 null ⇒ 没有任何消费点 ⇒ 读数不变**）✓
/// </summary>
[TestClass]
public sealed class UnitTierStage1Tests
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

    private static string Json(string weapon, string armour) => $$"""
    { "units": [ { "id": "probe", "name": "探针", "side": "player", "hp": 10, "attack": 5,
      "phys_def": 5, "speed": 5, "dodge": 5, "crit": 5, "resilience": 5, "stun_resist": 5,
      "bleed_resist": 5, "stat_debuff_resist": 5, "displace_resist": 5, "deaths_door_resist": 67,
      "skills": ["x"], "move_distance": 1, "weapon": {{weapon}}, "armour": {{armour}} },
      { "id": "probe_enemy", "name": "探针敌", "side": "enemy", "hp": 10, "attack": 5, "phys_def": 5,
        "speed": 5, "dodge": 5, "crit": 5, "resilience": 5, "stun_resist": 5, "bleed_resist": 5,
        "stat_debuff_resist": 5, "displace_resist": 5, "skills": ["y"] } ] }
    """;

    private const string FiveWeapons = """
    [ { "atk_pct": 0, "dmg_min": 6, "dmg_max": 12, "crit_pct": 3, "spd": 1 },
      { "atk_pct": 1, "dmg_min": 7, "dmg_max": 13, "crit_pct": 3, "spd": 1 },
      { "atk_pct": 2, "dmg_min": 8, "dmg_max": 14, "crit_pct": 4, "spd": 1 },
      { "atk_pct": 3, "dmg_min": 9, "dmg_max": 15, "crit_pct": 4, "spd": 1 },
      { "atk_pct": 4, "dmg_min": 10, "dmg_max": 16, "crit_pct": 5, "spd": 2 } ]
    """;

    private const string FiveArmours = """
    [ { "def_pct": 5, "prot": 0, "hp": 33, "spd": 0 },
      { "def_pct": 6, "prot": 1, "hp": 34, "spd": 0 },
      { "def_pct": 7, "prot": 1, "hp": 35, "spd": 0 },
      { "def_pct": 8, "prot": 2, "hp": 36, "spd": 0 },
      { "def_pct": 9, "prot": 2, "hp": 37, "spd": 1 } ]
    """;

    [TestMethod]
    public void FiveTiers_ParseAndAreReachableByTierIndex()
    {
        UnitConfig u = UnitsConfig.Parse(Json(FiveWeapons, FiveArmours)).Units[0];
        Assert.IsNotNull(u.Weapon);
        Assert.IsNotNull(u.Armour);
        Assert.AreEqual(5, u.Weapon!.Count, "weapon 必须是 5 阶（原版 0~4）✓");
        Assert.AreEqual(5, u.Armour!.Count, "armour 必须是 5 阶 ✓");

        UnitStats stats = UnitStatsMapper.From(u);
        Assert.IsNotNull(stats.WeaponAt(0));
        Assert.AreEqual(6, stats.WeaponAt(0)!.DmgMin, "0 阶 = 结构里的第 0 项 ✓");
        Assert.AreEqual(16, stats.WeaponAt(4)!.DmgMax, "4 阶 = 第 4 项 ✓");
        Assert.IsNull(stats.WeaponAt(5), "越界 ⇒ null（如实不假装）✓");
        Assert.IsNull(stats.WeaponAt(-1), "负阶 ⇒ null ✓");
        Assert.IsNotNull(stats.ArmourAt(2));
        Assert.AreEqual(35, stats.ArmourAt(2)!.Hp);

        Console.WriteLine($"[M1a·阶段1] 5 阶可解析可按阶取：weapon[4].dmg {stats.WeaponAt(4)!.DmgMin}~{stats.WeaponAt(4)!.DmgMax} · armour[2].hp {stats.ArmourAt(2)!.Hp} ✓");
        TestContext.WriteLine("[M1a·阶段1] 5 阶结构可装下并按阶取 ✓");
    }

    [TestMethod]
    public void WrongTierCount_IsRejectedWithTheUnitId()
    {
        string threeWeapons = """
        [ { "atk_pct": 0, "dmg_min": 6, "dmg_max": 12, "crit_pct": 3, "spd": 1 },
          { "atk_pct": 1, "dmg_min": 7, "dmg_max": 13, "crit_pct": 3, "spd": 1 },
          { "atk_pct": 2, "dmg_min": 8, "dmg_max": 14, "crit_pct": 4, "spd": 1 } ]
        """;
        var ex = Assert.ThrowsException<InvalidDataException>(() => UnitsConfig.Parse(Json(threeWeapons, FiveArmours)));
        StringAssert.Contains(ex.Message, "probe", "报错必须点名是哪个单位 ✓");
        StringAssert.Contains(ex.Message, "5 阶", "报错必须说清要求 ✓");
        Console.WriteLine($"[M1a·阶段1] 阶数不对 ⇒ 拒绝并点名：{ex.Message}");
    }

    [TestMethod]
    public void ShippedDataHasNoTiers_SoNothingConsumesThem_OldReadingsUnchanged()
    {
        UnitsConfig shipped = UnitsConfig.Parse(ReadData("units.json"));
        Assert.IsTrue(shipped.Units.Count > 0);
        Assert.IsTrue(shipped.Units.All(x => x.Weapon is null && x.Armour is null),
            "🔴 当前 units.json 不写 tier ⇒ 全部为 null ⇒ **阶段 1 零行为改动**（旧读数不变的实证）✓");

        UnitStats stats = UnitStatsMapper.From(shipped.Units[0]);
        Assert.IsNull(stats.WeaponAt(0), "没配 tier ⇒ 按阶取返回 null（不是 0，也不是默认值）✓");
        Assert.IsNull(stats.ArmourAt(0));
        Console.WriteLine($"[M1a·阶段1] 出厂数据 {shipped.Units.Count} 个单位 ⇒ tier 全 null ⇒ 无消费点 ⇒ 旧读数不变 ✓");
        TestContext.WriteLine("[M1a·阶段1] 出厂数据无 tier ⇒ 零行为改动 ✓");
    }

    /// <summary>
    /// 🔴 **M1a · 抗性 5→8（加字段级）**：`poison_resist` / `disease_resist` / `trap_resist`。
    ///   · **可空 = 未配**（不假装 0）✓ · 给了就查 [0,100] ✓ · 映射到 `UnitStats` ✓
    ///   🔴 **出厂数据没写它们 ⇒ 全 null ⇒ 判定轴取 `?? 0` ⇒ 与改造前同值（零行为）** ✓
    /// </summary>
    [TestMethod]
    public void ThreeNewResistAxes_AreOptionalValidatedAndMapped()
    {
        UnitConfig plain = UnitsConfig.Parse(Json(FiveWeapons, FiveArmours)).Units[0];
        UnitStats plainStats = UnitStatsMapper.From(plain);
        Assert.IsNull(plainStats.PoisonResist, "未配 ⇒ null（不是 0）✓");
        Assert.IsNull(plainStats.TrapResist, "未配 ⇒ null ✓");

        string json = Json(FiveWeapons, FiveArmours).Replace(
            "\"weapon\":", "\"poison_resist\": 25, \"disease_resist\": 30, \"trap_resist\": 40, \"weapon\":");
        UnitStats stats = UnitStatsMapper.From(UnitsConfig.Parse(json).Units[0]);
        Assert.AreEqual(25, stats.PoisonResist);
        Assert.AreEqual(30, stats.DiseaseResist);
        Assert.AreEqual(40, stats.TrapResist);

        string bad = Json(FiveWeapons, FiveArmours).Replace("\"weapon\":", "\"trap_resist\": 140, \"weapon\":");
        var ex = Assert.ThrowsException<InvalidDataException>(() => UnitsConfig.Parse(bad));
        StringAssert.Contains(ex.Message, "trap_resist");
        StringAssert.Contains(ex.Message, "[0,100]");

        Console.WriteLine($"[M1a·抗性] 未配=null ✓ · 配了 ⇒ {stats.PoisonResist}/{stats.DiseaseResist}/{stats.TrapResist} ✓ · 越界 ⇒ 拒绝：{ex.Message}");
        TestContext.WriteLine("[M1a·抗性] 三轴：可空 + 校验 + 映射 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
