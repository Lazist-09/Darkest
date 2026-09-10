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

    private static Func<SkillsConfig, SkillsConfig> CasterShockCd1 = skills =>
        skills with
        {
            Skills = skills.Skills
                .Select(s => s.Id == "caster_mental_shock"
                    ? s with { UseLimit = new UseLimitSpec(UseLimitType.Cooldown, 1) }
                    : s)
                .ToList(),
        };

    [TestMethod]
    public void Probe_Matrix_PrintOnly()
    {
        const int runs = 150;
        const long seedBase = 20260909;
        string[] names =
        {
            "P0 baseline（当前数据）",
            "P1 enemy HP+20%（60/46/41）",
            "P2 =P1 + 玩家倍率下调（cleave0.9/lunge0.8/doubleHit0.5/charge1.0）",
            "P3 =P2 + 施法者 CD1 精神震荡（强化敌方压榨）",
            "P4 敌方每回合行动×2（enemy_actions_per_round=2）",
            "P5 =P4 + 玩家倍率下调（cleave0.9/lunge0.8/doubleHit0.5/charge1.0）",
            "P6 =P5 + 敌人 HP+20%（60/46/41）",
        };

        var configs = new[]
        {
            (t: (Func<TuningConfig, TuningConfig>?)null, un: (Func<UnitsConfig, UnitsConfig>?)null, sk: (Func<SkillsConfig, SkillsConfig>?)null),
            (t: null, un: (Func<UnitsConfig, UnitsConfig>?)(u => BoostEnemyHp(u, 60, 46, 41)), sk: null),
            (t: null, un: (Func<UnitsConfig, UnitsConfig>?)(u => BoostEnemyHp(u, 60, 46, 41)),
             sk: (Func<SkillsConfig, SkillsConfig>?)(s => NerfPlayerMult(s,
                 ("warrior_cleave", 0.9), ("warrior_lunge", 0.8),
                 ("medic_double_hit", 0.5), ("commissar_charge_order", 1.0)))),
            (t: null, un: (Func<UnitsConfig, UnitsConfig>?)(u => BoostEnemyHp(u, 60, 46, 41)),
             sk: (Func<SkillsConfig, SkillsConfig>?)(s => CasterShockCd1(NerfPlayerMult(s,
                 ("warrior_cleave", 0.9), ("warrior_lunge", 0.8),
                 ("medic_double_hit", 0.5), ("commissar_charge_order", 1.0))))),
            (t: (Func<TuningConfig, TuningConfig>?)(x => x with { EnemyActionsPerRound = 2 }), un: null, sk: null),
            (t: (Func<TuningConfig, TuningConfig>?)(x => x with { EnemyActionsPerRound = 2 }), un: null,
             sk: (Func<SkillsConfig, SkillsConfig>?)(s => NerfPlayerMult(s,
                 ("warrior_cleave", 0.9), ("warrior_lunge", 0.8),
                 ("medic_double_hit", 0.5), ("commissar_charge_order", 1.0)))),
            (t: (Func<TuningConfig, TuningConfig>?)(x => x with { EnemyActionsPerRound = 2 }),
             un: (Func<UnitsConfig, UnitsConfig>?)(u => BoostEnemyHp(u, 60, 46, 41)),
             sk: (Func<SkillsConfig, SkillsConfig>?)(s => NerfPlayerMult(s,
                 ("warrior_cleave", 0.9), ("warrior_lunge", 0.8),
                 ("medic_double_hit", 0.5), ("commissar_charge_order", 1.0)))),
        };

        for (int i = 0; i < configs.Length; i++)
        {
            SimulationReport r = HeadlessDriver.RunMany(runs, PolicyKind.SemiRandom, seedBase,
                configs[i].t, configs[i].un, configs[i].sk);
            string line = $"[Probe {names[i]}] runs={r.Runs} win={r.WinRate:P0} avgRounds={r.AvgRounds:F2} " +
                          $"max={r.MaxRounds} retreat={r.GamesWithRetreat} weak={r.TotalWeak}";
            Console.WriteLine(line);
            TestContext.WriteLine(line);
        }
    }

    public TestContext TestContext { get; set; } = null!;
}