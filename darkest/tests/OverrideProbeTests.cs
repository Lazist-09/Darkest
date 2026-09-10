using System;
using System.Linq;
using Darkest.Data;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// Override 组合探针（M6 数值建议验证，不改任何 data/*.json）：经 HeadlessDriver 的
/// units/skills/tuning 覆盖通道试算胜率/节奏，找带内组合供策划拍板（README §6-5）。
/// 本组用例只打印报告、不做断言（非判据；判据闸为 M6Acceptance_WinRateBand_And_Rhythm）。
/// </summary>
[TestClass]
public sealed class OverrideProbeTests
{
    private static UnitsConfig BoostEnemyHp(UnitsConfig units, int melee, int archer, int caster)
        => units with
        {
            Units = units.Units
                .Select(u => u.Id switch
                {
                    "melee_soldier" => u with { Hp = melee },
                    "ranged_archer" => u with { Hp = archer },
                    "caster" => u with { Hp = caster },
                    _ => u,
                })
                .ToList(),
        };

    private static SkillsConfig NerfPlayerMult(SkillsConfig skills, params (string id, double mult)[] entries)
        => skills with
        {
            Skills = skills.Skills
                .Select(s => entries.FirstOrDefault(e => e.id == s.Id) is var hit && hit.id is not null && s.Damage is not null
                    ? s with { Damage = new DamageSpec(s.Damage.Segments.Select(x => x with { Multiplier = hit.mult }).ToList()) }
                    : s)
                .ToList(),
        };

    /// <summary>把指定单体技能的段倍率设为给定值（探针用；双段技能会同时改两段，避免用于它）。</summary>
    private static Func<SkillsConfig, SkillsConfig> SetMult(string id, double mult) => skills =>
        skills with
        {
            Skills = skills.Skills
                .Select(s => s.Id == id && s.Damage is not null
                    ? s with { Damage = new DamageSpec(s.Damage.Segments.Select(x => x with { Multiplier = mult }).ToList()) }
                    : s)
                .ToList(),
        };

    private static Func<SkillsConfig, SkillsConfig> SetCoefficient(string id, double coef) => skills =>
        skills with
        {
            Skills = skills.Skills
                .Select(s => s.Id == id && s.Damage is not null
                    ? s with { Damage = new DamageSpec(s.Damage.Segments.Select(x => x with { Coefficient = coef }).ToList()) }
                    : s)
                .ToList(),
        };

    private static Func<SkillsConfig, SkillsConfig> Chain(params Func<SkillsConfig, SkillsConfig>[] fs)
        => s => fs.Aggregate(s, (acc, f) => f(acc));

    [TestMethod]
    public void Probe_Matrix_v045_PrintOnly()
    {
        const int runs = 150;
        const long seedBase = 20260909;

        Func<UnitsConfig, UnitsConfig> hp58 = u => BoostEnemyHp(u, 58, 44, 41);
        Func<SkillsConfig, SkillsConfig> cleave1 = SetMult("warrior_cleave", 1.0);
        Func<SkillsConfig, SkillsConfig> lethal5 = SetCoefficient("medic_lethal_injection", 0.5);
        Func<SkillsConfig, SkillsConfig> sweep7 = SetMult("warrior_sweep", 0.7);
        Func<SkillsConfig, SkillsConfig> exec6 = SetCoefficient("commissar_execution_order", 0.6);

        // (名称, tuning, units, skills, 策略)
        (string name, Func<TuningConfig, TuningConfig>? t, Func<UnitsConfig, UnitsConfig>? un,
         Func<SkillsConfig, SkillsConfig>? sk, PolicyKind policy)[] cases =
        {
            ("V0 v0.45 基线（当前数据）", null, null, null, PolicyKind.SemiRandom),
            ("V6 V0 数据 + Baseline 集火（玩家上限对照）", null, null, null, PolicyKind.Baseline),
            ("V1 HP 58/44/41（预授权轻降）", null, hp58, null, PolicyKind.SemiRandom),
            ("V2 =V1 + cleave 1.0", null, hp58, cleave1, PolicyKind.SemiRandom),
            ("V3 =V1 + lethal 0.5", null, hp58, lethal5, PolicyKind.SemiRandom),
            ("V4 =V1 + cleave 1.0 + lethal 0.5", null, hp58, Chain(cleave1, lethal5), PolicyKind.SemiRandom),
            ("V5 =V4 + sweep 0.7 + 处决 0.6", null, hp58, Chain(cleave1, lethal5, sweep7, exec6), PolicyKind.SemiRandom),
            ("V7 =V4 + Baseline 集火", null, hp58, Chain(cleave1, lethal5), PolicyKind.Baseline),
        };

        foreach ((string name, var t, var un, var sk, PolicyKind policy) in cases)
        {
            SimulationReport r = HeadlessDriver.RunMany(runs, policy, seedBase, t, un, sk);
            string line = $"[Probe {name}] win={r.WinRate:P0} avgRounds={r.AvgRounds:F2} " +
                          $"max={r.MaxRounds} coll={r.TotalCollapse} weak={r.TotalWeak} dd={r.TotalDeathDoorRolls} " +
                          $"retreat={r.GamesWithRetreat} disp={r.TotalDisplacements} roundLimit={r.RoundLimitGames}";
            Console.WriteLine(line);
            TestContext.WriteLine(line);
        }
    }

    public TestContext TestContext { get; set; } = null!;
}