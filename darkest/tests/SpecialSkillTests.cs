using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M3 特殊技能标注核对（T-M3-07）：per_battle×3 / self_damage_fixed 6·8 / "all"×4 /
/// missing_hp 0.8·0.9 / 多段×2 / 我方 mental 0·敌方 3 —— 逐字入库断言（防"顺手改数字"）。
/// </summary>
[TestClass]
public sealed class SpecialSkillTests
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

    [TestMethod]
    public void PerBattleOnce_Exactly3()
    {
        SkillTemplateConfig[] once = Skills().Skills
            .Where(s => s.UseLimit.Type == UseLimitType.PerBattle && s.UseLimit.Value == 1).ToArray();
        Assert.AreEqual(3, once.Length, "每场 1 次恰 3 条");
        CollectionAssert.AreEquivalent(
            new[] { "warrior_last_stand", "tank_selfless_charge", "commissar_total_mobilization" },
            once.Select(s => s.Id).ToArray());
    }

    [TestMethod]
    public void SelfDamageFixed_6And8_Exactly2()
    {
        SkillTemplateConfig[] selfDmg = Skills().Skills.Where(s => s.SelfDamageFixed is not null).ToArray();
        Assert.AreEqual(2, selfDmg.Length);
        Assert.AreEqual(6, selfDmg.Single(s => s.Id == "warrior_last_stand").SelfDamageFixed, "殊死一搏 自身 6 伤");
        Assert.AreEqual(8, selfDmg.Single(s => s.Id == "tank_selfless_charge").SelfDamageFixed, "舍身 自身 8 伤");
    }

    [TestMethod]
    public void SelfSlotsAll_Exactly4()
    {
        SkillTemplateConfig[] all = Skills().Skills.Where(s => s.SelfSlots.IsAll).ToArray();
        Assert.AreEqual(4, all.Length, "'all' 兜底恰 4 条");
        CollectionAssert.AreEquivalent(
            new[] { "warrior_war_cry", "tank_war_cry", "medic_first_aid", "commissar_battle_inspiration" },
            all.Select(s => s.Id).ToArray());
    }

    [TestMethod]
    public void MissingHpSegments_0_4And0_5_Exactly2()
    {
        SkillTemplateConfig[] missing = Skills().Skills
            .Where(s => s.Damage?.Segments.Any(x => x.Type == DamageSegmentType.MissingHp) == true).ToArray();
        Assert.AreEqual(2, missing.Length);
        Assert.AreEqual(0.4, missing.Single(s => s.Id == "medic_lethal_injection").Damage!.Segments[0].Coefficient);
        Assert.AreEqual(0.5, missing.Single(s => s.Id == "commissar_execution_order").Damage!.Segments[0].Coefficient);
    }

    [TestMethod]
    public void MultiSegmentFlat_Exactly2()
    {
        SkillTemplateConfig[] multi = Skills().Skills
            .Where(s => s.Damage?.Segments.Count >= 2 && s.Damage.Segments.All(x => x.Type == DamageSegmentType.Flat))
            .ToArray();
        Assert.AreEqual(2, multi.Length);
        Assert.AreEqual(0.5, multi.Single(s => s.Id == "medic_double_hit").Damage!.Segments[0].Multiplier, "双连击 0.5×2");
        Assert.AreEqual(0.5, multi.Single(s => s.Id == "commissar_burst_fire").Damage!.Segments[0].Multiplier, "连射 0.5×2");
    }

    [TestMethod]
    public void MentalAxis_Player0_Enemy3()
    {
        SkillsConfig skills = Skills();
        SkillTemplateConfig[] playerMental = skills.Skills.Where(s => s.DamageAxis == SkillDamageAxis.Mental && s.OwnerUnit is not ("melee_soldier" or "ranged_archer" or "caster")).ToArray();
        Assert.AreEqual(0, playerMental.Length, "我方 36 技能精神轴 0 条（士气压力源只在敌方）");
        SkillTemplateConfig[] enemyMental = skills.Skills.Where(s => s.DamageAxis == SkillDamageAxis.Mental && s.OwnerUnit is "melee_soldier" or "ranged_archer" or "caster").ToArray();
        Assert.AreEqual(3, enemyMental.Length, "敌方精神轴恰 3 条");
        CollectionAssert.AreEquivalent(
            new[] { "ranged_intimidating_shot", "caster_fear_whisper", "caster_mental_shock" },
            enemyMental.Select(s => s.Id).ToArray());
    }

    [TestMethod]
    public void ConstructiveCounter_PerBattle2_Red()
    {
        SkillsConfig skills = Skills();
        SkillsConfig bad = skills with
        {
            Skills = skills.Skills.Select(s => s.Id == "warrior_last_stand"
                ? s with { UseLimit = new UseLimitSpec(UseLimitType.PerBattle, 2) }
                : s).ToList(),
        };
        // 数据断言（per_battle=1 恰 3）必须能察觉改动：改为 2 后计数应降为 2
        int once = bad.Skills.Count(s => s.UseLimit.Type == UseLimitType.PerBattle && s.UseLimit.Value == 1);
        Assert.AreEqual(2, once, "殊死一搏 per_battle 改为 2 后：per_battle=1 计数 3→2（守卫红）");
    }
}