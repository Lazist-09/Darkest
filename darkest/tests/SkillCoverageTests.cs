using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M3 覆盖率回归（T-M3-08）：4 角色 × 位置 1~6 可用技能自数 ≥ skill_data §6 表声明数且 ≥2
/// （"换位不废人"）；每角色 ≥1 个 self_slots="all" 兜底；清单可与人读表逐格对账。
/// </summary>
[TestClass]
public sealed class SkillCoverageTests
{
    private static SkillsConfig Skills() => SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json")));

    private static string FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    /// <summary>skill_data §6 表声明数（判据下限；自数多于表值合法，O-16≠用相等）。</summary>
    private static readonly IReadOnlyDictionary<string, int[]> CoverageTable = new Dictionary<string, int[]>
    {
        ["warrior"] = new[] { 5, 5, 4, 3, 2, 2 },
        ["tank"] = new[] { 7, 6, 2, 2, 2, 2 },
        ["medic"] = new[] { 6, 6, 4, 4, 2, 2 },
        ["commissar"] = new[] { 5, 5, 4, 4, 2, 2 },
    };

    private static readonly string[] AllSkills = { "warrior_war_cry", "tank_war_cry", "medic_first_aid", "commissar_battle_inspiration" };

    private static int CountUsable(SkillsConfig skills, string owner, int pos)
    {
        int count = 0;
        foreach (SkillTemplateConfig s in skills.Skills)
        {
            if (s.OwnerUnit == owner && s.SelfSlots.Allows(pos))
            {
                count++;
            }
        }

        return count;
    }

    [TestMethod]
    public void Coverage_EveryArchetypeEveryPosition_MeetsOrExceedsTableAndAtLeast2()
    {
        SkillsConfig skills = Skills();
        var failures = new List<string>();
        foreach ((string owner, int[] expected) in CoverageTable)
        {
            for (int pos = 1; pos <= 6; pos++)
            {
                int actual = CountUsable(skills, owner, pos);
                if (actual < expected[pos - 1] || actual < 2)
                {
                    failures.Add($"{owner} 位{pos}: 自数 {actual} < 表下限 {expected[pos - 1]} 或 <2");
                }
            }

            // 每角色 ≥1 个 "all" 兜底（战吼×2 / 急救 / 战场鼓舞）
            int allCount = skills.Skills.Count(s => s.OwnerUnit == owner && s.SelfSlots.IsAll);
            if (allCount < 1)
            {
                failures.Add($"{owner}: 缺 self_slots=all 兜底");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void Coverage_MatrixMatchesSkillData_TableRowByRow()
    {
        SkillsConfig skills = Skills();
        // 逐格输出当前自数（对账用；并断言不低于表值）
        foreach ((string owner, int[] expected) in CoverageTable)
        {
            var row = new List<string>();
            for (int pos = 1; pos <= 6; pos++)
            {
                int actual = CountUsable(skills, owner, pos);
                row.Add($"{pos}:{actual}(表{expected[pos - 1]})");
                Assert.IsTrue(actual >= expected[pos - 1], $"{owner} 位{pos} 自数 {actual} < 表值 {expected[pos - 1]}");
            }

            TestContext.WriteLine($"{owner}: {string.Join(" ", row)}");
        }
    }

    [TestMethod]
    public void ConstructiveCounter_FirstAidAllTo1_Red()
    {
        SkillsConfig skills = Skills();
        SkillsConfig bad = skills with
        {
            Skills = skills.Skills.Select(s => s.Id == "medic_first_aid"
                ? s with { SelfSlots = new SelfSlots(false, new[] { 1 }) }
                : s).ToList(),
        };
        // 军医位 5/6 将只剩群体绷带（1 个）→ <2 覆盖不足（模拟数据层校验函数在此断言自数）
        int medicSupport = CountUsable(bad, "medic", 5);
        Assert.AreEqual(1, medicSupport, "构造性反例：急救失去 all 后位 5 仅剩群体绷带");
        Assert.IsTrue(medicSupport < 2, "覆盖校验（≥2）应红");
    }

    [TestMethod]
    public void EnemySkills_AllReferencedByUnitsPool()
    {
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        SkillsConfig skills = Skills();
        foreach (string enemyId in new[] { "melee_soldier", "ranged_archer", "caster" })
        {
            var pool = units.Get(enemyId).Skills.ToHashSet();
            var owned = skills.Skills.Where(s => s.OwnerUnit == enemyId).Select(s => s.Id).ToArray();
            Assert.IsTrue(owned.Length > 0 && owned.All(pool.Contains), $"{enemyId} 技能需全部收录于 units.json 技能池（引用完整补强）");
        }
    }

    public TestContext TestContext { get; set; } = null!;
}