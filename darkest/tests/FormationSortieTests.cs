using System.IO;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M8.0 ①(c)（`#286`）：**阵型模板化** —— `formation.json` 只给槽位/敌方；**我方出征 6 人由名册提供**。
/// 本用例锁：① 套用名册后**槽位顺序 = 名册顺序**；② 人数不符即报错（不给模糊实现）；
/// ③ 等级投影（(b)）作用到我方单位（HP/攻击按等级上升）。
/// </summary>
[TestClass]
public sealed class FormationSortieTests
{
    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
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

    [TestMethod]
    public void C_PlayerSortie_ComesFromRoster_BySlotOrder()
    {
        FormationConfig template = FormationConfig.Parse(ReadData("formation.json"));
        RosterConfig roster = RosterConfig.Parse(ReadData("roster.json"));

        HeroConfig[] sortie = roster.Heroes.Take(RosterConfig.SortieSize).ToArray();
        FormationConfig slotted = FormationSortie.WithPlayerSortie(template, sortie.Select(h => h.Archetype).ToArray());

        // 槽位顺序 = 名册顺序（阵型模板不再决定"谁出征"）
        for (int i = 0; i < sortie.Length; i++)
        {
            Assert.AreEqual(sortie[i].Archetype, slotted.InitialRoster.Player[i].Unit,
                $"第 {i + 1} 槽应为名册第 {i + 1} 名（{sortie[i].Name} / {sortie[i].Archetype}）");
        }

        // 敌方编成仍来自模板（未被名册影响）
        Assert.AreEqual(template.InitialRoster.Enemy.Count, slotted.InitialRoster.Enemy.Count);
        Assert.AreEqual(template.InitialRoster.Enemy[0].Unit, slotted.InitialRoster.Enemy[0].Unit, "敌方编成不受名册影响");
    }

    [TestMethod]
    public void C_SortieSizeMismatch_Throws()
    {
        FormationConfig template = FormationConfig.Parse(ReadData("formation.json"));
        Assert.ThrowsException<InvalidDataException>(
            () => FormationSortie.WithPlayerSortie(template, new[] { "warrior", "tank" }),
            "出征人数与槽位数不符 ⇒ 报错（红线 19：不给模糊实现）");
    }

    [TestMethod]
    public void C_LevelProjection_AppliedToPlayerUnits()
    {
        RosterConfig roster = RosterConfig.Parse(ReadData("roster.json"));
        HeroConfig veteran = roster.Heroes.OrderByDescending(h => h.Level).First(); // 最高级
        HeroConfig rookie = roster.Heroes.OrderBy(h => h.Level).First();            // 1 级
        Assert.IsTrue(veteran.Level > rookie.Level, "样本必须一老一新");

        var director = MonteCarlo.HeadlessDriver.NewDirector(new Darkest.Core.Events.CombatLog());
        var units = director.Player.UnitsInSlotOrder().ToArray();
        Assert.IsTrue(units.Length >= 2, "我方至少 2 个单位");

        int rookHp = units[0].MaxHp;
        HeroProjection.ApplyLevel(rookie, units[0], roster.LevelGrowth);
        Assert.AreEqual(rookHp, units[0].MaxHp, "1 级英雄 = no-op（不改基准）");

        // 🔴 断言取**自身前置值**（不同原型基准不同，不能拿别的单位当基准 —— 我自己踩过一次）
        int vetHpBefore = units[1].MaxHp, vetAtkBefore = units[1].EffectiveAttack;
        HeroProjection.ApplyLevel(veteran, units[1], roster.LevelGrowth);
        int steps = veteran.Level - 1;
        Assert.AreEqual(vetHpBefore + (roster.LevelGrowth.HpPerLevel * steps), units[1].MaxHp,
            $"HP = 基准 + 每级 ×(等级−1)（{veteran.Name} Lv{veteran.Level}）");
        Assert.AreEqual(vetAtkBefore + (roster.LevelGrowth.AttackPerLevel * steps), units[1].EffectiveAttack,
            $"攻击 = 基准 + 每级 ×(等级−1)（{veteran.Name} Lv{veteran.Level}）");
    }
}
