using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.0 ①（`#283` 7.2/7.4/7.6/7.7/7.8 + **P22 ①②③**）：名册 12 = 出征 6 + 替补 6；
/// 等级 ∈ [1,6] 且**不升技能**；每人 2~3 条**小幅**特质且**正负都有**；招募免费 ⇒ 新兵 level = 1 存在。
/// </summary>
[TestClass]
public sealed class RosterTests
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

    private static RosterConfig Roster() => RosterConfig.Parse(ReadData("roster.json"));

    [TestMethod]
    public void P22_1_RosterCapTwelve_GreaterThanSortie()
    {
        RosterConfig r = Roster();
        Assert.AreEqual(12, r.RosterCap, "名册上限 12（#283 7.4）");
        Assert.IsTrue(r.RosterCap > RosterConfig.SortieSize, "必须 > 出征 6 ⇒ 轮换休息成为策略");
        Assert.IsTrue(r.Heroes.Count >= RosterConfig.SortieSize && r.Heroes.Count <= r.RosterCap,
            $"heroes 数量 ∈ [6, 12]（实际 {r.Heroes.Count}）");
        Assert.IsTrue(r.Heroes.Count < r.RosterCap, "🔴 起步必须**未满员** ⇒ 留出招募空间（M8.0 ⑤，否则招募是死内容）");
        Assert.AreEqual(r.Heroes.Count, r.Heroes.Select(h => h.Id).Distinct().Count(), "id 不重复");
    }

    [TestMethod]
    public void P22_2_LevelsInRange_AndNoSkillUpgradeField()
    {
        RosterConfig r = Roster();
        Assert.AreEqual(1, r.LevelMin);
        Assert.AreEqual(6, r.LevelMax, "等级 1~6（#283 7.6）");
        Assert.IsTrue(r.Heroes.All(h => h.Level is >= 1 and <= 6), "所有英雄等级在 [1,6]");
        Assert.IsTrue(r.Heroes.Any(h => h.Level == 1), "存在 1 级新兵（招募免费 ⇒ 新兵 level=1，#283 7.8）");
        Assert.IsTrue(r.LevelGrowth.HpPerLevel == 2 && r.LevelGrowth.AttackPerLevel == 1,
            "等级只给属性小幅度：HP+2 / 攻击+1（不升技能）");

        // 原始文本不得含技能/升级字段（P22 ② 的静态底线）
        string raw = ReadData("roster.json");
        foreach (string bad in new[] { "skill", "upgrade" })
        {
            Assert.IsFalse(raw.Contains($"\"{bad}", StringComparison.OrdinalIgnoreCase),
                $"名册不得含 {bad} 字段（#283 7.8：不碰技能表）");
        }
    }

    [TestMethod]
    public void P22_3_TraitsTwoToThree_SmallAndBothSigns()
    {
        RosterConfig r = Roster();
        foreach (HeroConfig h in r.Heroes)
        {
            Assert.IsTrue(h.Traits.Count is >= 2 and <= 3, $"{h.Name} 特质 2~3 条");
            Assert.IsTrue(h.Traits.All(t => Math.Abs(t.DamagePct) <= 15 && Math.Abs(t.MoraleDamagePct) <= 20),
                $"{h.Name} 特质必须小幅");
            bool pos = h.Traits.Any(t => t.DamagePct > 0 || t.MoraleDamagePct < 0);
            bool neg = h.Traits.Any(t => t.DamagePct < 0 || t.MoraleDamagePct > 0);
            Assert.IsTrue(pos && neg, $"{h.Name} 特质必须正负都有（#283 7.7）");
        }
    }

    [TestMethod]
    public void P22_BadRoster_ThrowsOnLoad()
    {
        string raw = ReadData("roster.json");

        // ① 名册上限被调小（≤ 出征 6）
        Assert.ThrowsException<InvalidDataException>(
            () => RosterConfig.Parse(raw.Replace("\"roster_cap\": 12", "\"roster_cap\": 6", StringComparison.Ordinal)),
            "roster_cap ≤ 6 → 启动报错（P22 ①）");

        // ② 出现技能升级字段
        Assert.ThrowsException<InvalidDataException>(
            () => RosterConfig.Parse(raw.Replace("\"traits\"", "\"skill_upgrade\": [], \"traits\"", StringComparison.Ordinal)),
            "出现技能字段 → 启动报错（P22 ② / #283 7.8）");

        // ③ 特质幅度超「小幅」
        Assert.ThrowsException<InvalidDataException>(
            () => RosterConfig.Parse(raw.Replace("\"damage_pct\": 5", "\"damage_pct\": 40", StringComparison.Ordinal)),
            "特质幅度过大 → 启动报错（P22 ③）");
    }

    [TestMethod]
    public void P22_FourArchetypes_CoveredByRoster()
    {
        RosterConfig r = Roster();
        foreach (string archetype in new[] { "warrior", "tank", "medic", "commissar" })
        {
            Assert.IsTrue(r.ByArchetype(archetype).Count >= 2,
                $"原型 {archetype} 至少 2 名（个体化后需可轮换）");
        }
    }
}
