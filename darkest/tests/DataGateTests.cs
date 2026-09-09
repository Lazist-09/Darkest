using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M3 数据门禁（T-M3-02）：skills.json 43 条导入、抽样逐字段对照（data_schema §3.2/§5 口径）、
/// 扰动反例 fail-fast。
/// </summary>
[TestClass]
public sealed class DataGateTests
{
    private static SkillsConfig LoadSkills()
        => SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json")));

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
    public void SkillsJson_ImportsAll43_NoException()
    {
        SkillsConfig cfg = LoadSkills();
        Assert.AreEqual(43, cfg.Skills.Count, "O-16：技能数 = 43（36 我方 + 7 敌方）");
    }

    [TestMethod]
    public void Sampling_WarriorCleave_Fieldwise()
    {
        SkillTemplateConfig cleave = LoadSkills().Get("warrior_cleave");
        Assert.AreEqual("劈砍", cleave.Name);
        Assert.AreEqual("warrior", cleave.OwnerUnit);
        Assert.IsFalse(cleave.SelfSlots.IsAll);
        CollectionAssert.AreEqual(new[] { 1, 2 }, cleave.SelfSlots.Slots.ToArray());
        Assert.AreEqual(SkillTargetScope.Slots, cleave.Target.Scope);
        Assert.AreEqual("enemy", cleave.Target.Side);
        CollectionAssert.AreEqual(new[] { 1, 2 }, cleave.Target.Slots!.ToArray());
        Assert.AreEqual(1.0, cleave.Damage!.Segments[0].Multiplier);
        Assert.AreEqual(DamageSegmentType.Flat, cleave.Damage.Segments[0].Type);
        Assert.AreEqual(0, cleave.HitMod);
        Assert.AreEqual(0, cleave.CritMod);
        Assert.AreEqual(UseLimitType.None, cleave.UseLimit.Type);
        Assert.AreEqual(SkillRangeAxis.Melee, cleave.RangeAxis);
        Assert.AreEqual(SkillDamageAxis.Physical, cleave.DamageAxis);
        CollectionAssert.AreEqual(new[] { FuncTag.Output }, cleave.Tags.ToArray());
    }

    [TestMethod]
    public void Sampling_TankIronWall_ShieldChargesAndCooldown()
    {
        SkillTemplateConfig ironWall = LoadSkills().Get("tank_iron_wall");
        EffectSpec shield = ironWall.Effects.Single();
        Assert.AreEqual(SkillEffectType.Shield, shield.Type);
        Assert.AreEqual(2, shield.Charges, "#156 护盾按次数 = 2");
        Assert.AreEqual("self", shield.ApplyTo);
        Assert.AreEqual(UseLimitType.Cooldown, ironWall.UseLimit.Type);
        Assert.AreEqual(4, ironWall.UseLimit.Value, "CD 4");
    }

    [TestMethod]
    public void Sampling_CasterFearWhisper_And_IntimidatingShot_O21()
    {
        SkillsConfig cfg = LoadSkills();
        SkillTemplateConfig whisper = cfg.Get("caster_fear_whisper");
        Assert.AreEqual(SkillDamageAxis.Mental, whisper.DamageAxis);
        Assert.AreEqual(UseLimitType.Cooldown, whisper.UseLimit.Type);
        Assert.AreEqual(1, whisper.UseLimit.Value, "恐惧低语 CD 1");
        EffectSpec debuff = whisper.Effects.Single();
        Assert.AreEqual(SkillEffectType.StatMod, debuff.Type);
        Assert.AreEqual("resilience", debuff.Stat);
        Assert.AreEqual(-10, debuff.Delta, "韧性 −10 / 2 回合（O-23 走技能标注值）");

        SkillTemplateConfig intimidating = cfg.Get("ranged_intimidating_shot");
        Assert.AreEqual(SkillDamageAxis.Mental, intimidating.DamageAxis);
        MoraleEffectSpec morale = intimidating.MoraleEffects.Single();
        Assert.AreEqual(MoraleEffectScope.Targets, morale.Scope);
        Assert.AreEqual(-4, morale.Delta, "#165/#170：显式 −4");
        Assert.AreEqual(2, intimidating.UseLimit.Value, "威吓箭 CD 2");
    }

    [TestMethod]
    public void Sampling_Segments_DoubleHit_MissingHp()
    {
        SkillsConfig cfg = LoadSkills();
        SkillTemplateConfig doubleHit = cfg.Get("medic_double_hit");
        Assert.AreEqual(2, doubleHit.Damage!.Segments.Count, "0.55×2 = 两段各 0.55");
        Assert.IsTrue(doubleHit.Damage.Segments.All(s => s.Type == DamageSegmentType.Flat && s.Multiplier == 0.55));

        SkillTemplateConfig lethal = cfg.Get("medic_lethal_injection");
        DamageSegment seg = lethal.Damage!.Segments.Single();
        Assert.AreEqual(DamageSegmentType.MissingHp, seg.Type);
        Assert.AreEqual(1.0, seg.Base);
        Assert.AreEqual(0.8, seg.Coefficient, "致命注射 coefficient 0.8");

        Assert.AreEqual(0.9, cfg.Get("commissar_execution_order").Damage!.Segments.Single().Coefficient, "处决令 0.9");
        Assert.AreEqual(0.5, cfg.Get("commissar_burst_fire").Damage!.Segments[0].Multiplier, "连射 0.5×2");
    }

    [TestMethod]
    public void CounterDeletion_42_Throws()
    {
        SkillsConfig cfg = LoadSkills();
        // 删除 1 条 → 42 → 计数断言红（O-16）
        SkillsConfig deleted = cfg with { Skills = cfg.Skills.Take(42).ToList() };
        Assert.ThrowsException<InvalidDataException>(() => SkillsConfig.Parse(SkillsConfig.Serialize(deleted)));
    }

    [TestMethod]
    public void CounterBadSelfSlot_7_Throws()
    {
        SkillsConfig cfg = LoadSkills();
        SkillsConfig bad = cfg with
        {
            Skills = cfg.Skills.Select((s, i) => i == 0 ? s with { SelfSlots = new SelfSlots(false, new[] { 7 }) } : s).ToList(),
        };
        Assert.ThrowsException<InvalidDataException>(() => SkillsConfig.Parse(SkillsConfig.Serialize(bad)));
    }

    [TestMethod]
    public void CounterEffectPairing_Broken_Throws_O24()
    {
        SkillsConfig cfg = LoadSkills();
        // probability 无 resist_axis → O-24 成对规则报错
        SkillsConfig bad = cfg with
        {
            Skills = cfg.Skills.Select((s, i) => i == 0
                ? s with { Effects = new[] { new EffectSpec(SkillEffectType.Stun, Probability: 35, null, null, null, null, null, null) } }
                : s).ToList(),
        };
        Assert.ThrowsException<InvalidDataException>(() => SkillsConfig.Parse(SkillsConfig.Serialize(bad)));
    }

    [TestMethod]
    public void Roundtrip_ReimportsWithoutMutation()
    {
        SkillsConfig cfg = LoadSkills();
        SkillsConfig again = SkillsConfig.Parse(SkillsConfig.Serialize(cfg));
        Assert.AreEqual(cfg.Skills.Count, again.Skills.Count);
        CollectionAssert.AreEqual(
            cfg.Skills.Select(s => s.Id).ToArray(),
            again.Skills.Select(s => s.Id).ToArray(),
            "roundtrip 后 43 条技能 id 序列一致（逐条强相等见抽样对照）");
    }
}