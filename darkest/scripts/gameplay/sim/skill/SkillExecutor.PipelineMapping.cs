// 🔴 从 SkillExecutor.cs 拆出（用户红线：程序文件 <=600 行 · 架构 file_size_split §1.2）
//    本文件 = 技能数据 → 管线输入的纯映射（全部 static 纯函数 ⇒ 零实例状态、可独立测试）：
//    TargetSide · IsAoe · AxisString · FlatMultipliers · ResolvedMultipliers · MapEffects ·
//    MapDisplacement · MapMoraleEffects · 只搬家、零行为改动 ✓
//    依赖主类私有成员（实测扫描本文件得出）：无（不读任何字段/属性）✓

using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Pipeline;

namespace Darkest.Gameplay.Sim.Skill;

public sealed partial class SkillExecutor
{
    // ------------------------------------------------------------------ 映射辅助

    private static FormationSide TargetSide(SkillTemplateConfig skill)
        => skill.Target.Side == "enemy" ? FormationSide.Enemy : FormationSide.Player;

    private static bool IsAoe(SkillTemplateConfig skill) => skill.Tags.Contains(FuncTag.Aoe);

    private static string AxisString(SkillDamageAxis axis) => axis switch
    {
        SkillDamageAxis.Physical => "physical",
        SkillDamageAxis.Mental => "mental",
        _ => "none",
    };

    private static IReadOnlyList<double> FlatMultipliers(SkillTemplateConfig skill)
        => skill.Damage!.Segments.Select(s => s.Multiplier ?? 1.0).ToArray();

    private static IReadOnlyList<double> ResolvedMultipliers(SkillTemplateConfig skill, FormationBoard board, int slot)
    {
        UnitRuntime? target = board.UnitRuntimeAt(slot);
        double missingRatio = target is null || target.MaxHp <= 0
            ? 0.0
            : (double)(target.MaxHp - target.CurrentHp) / target.MaxHp;
        return skill.Damage!.Segments.Select(s => s.Type == DamageSegmentType.MissingHp
            ? (s.Base ?? 1.0) + missingRatio * (s.Coefficient ?? 0.0)
            : s.Multiplier ?? 1.0).ToArray();
    }

    private static IReadOnlyList<EffectRequest> MapEffects(SkillTemplateConfig skill)
        => skill.Effects
            .Where(e => e.Type is SkillEffectType.Stun or SkillEffectType.Bleed or SkillEffectType.StatMod or SkillEffectType.Mark)
            .Select(e => new EffectRequest(
                e.Type switch
                {
                    SkillEffectType.Stun => "stun",
                    SkillEffectType.Bleed => "bleed",
                    SkillEffectType.Mark => "mark", // D2（#204）
                    _ => "stat_mod",
                },
                e.Probability,
                e.ResistAxis,
                e.Stat,
                e.Delta ?? 0))
            .ToArray();

    private static SkillDisplace? MapDisplacement(SkillTemplateConfig skill, bool selfOnly)
    {
        if (skill.Displacement is not { } d)
        {
            return null;
        }

        return d.Type switch
        {
            DisplacementType.SelfForward or DisplacementType.SelfBackward
                => new SkillDisplace(FromPos: 0, ToPos: d.Type == DisplacementType.SelfForward ? 1 : -1, Distance: d.Count, SelfDisplacement: true),
            DisplacementType.Push when selfOnly => null, // push 由逐目标路径构造
            _ => null,
        };
    }

    private static MoraleEffectRequest[] MapMoraleEffects(SkillTemplateConfig skill)
        => skill.MoraleEffects.Select(m => new MoraleEffectRequest(
            m.Scope switch
            {
                MoraleEffectScope.Targets => "targets",
                MoraleEffectScope.Team => "team",
                MoraleEffectScope.Self => "self",
                _ => "ally_targets",
            },
            m.Delta)).ToArray();
}
