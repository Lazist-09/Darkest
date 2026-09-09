using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Skill;

/// <summary>
/// 可用性判定上下文（T-M3-03）：具体类按 bluePrint §9.3 接口形状扩展的最小必要上下文
/// （接口基线 ISkillUseResolver.Resolve(caster, skillId, snapshot) 视野过窄，M5 装配时对齐）。
/// </summary>
public sealed record SkillUseContext(
    SkillTemplateConfig Skill,
    UnitId Caster,
    FormationBoard AllyBoard,
    FormationBoard TargetBoard,
    IReadOnlySet<string>? Carried,
    SkillRuntimeState Runtime,
    bool IsEnemy);

/// <summary>
/// 技能可用性判定（T-M3-03 / skill.md §5，顺序固定不可调换）：
/// ① 携带 ② 站位 ③ 目标部分非空（全空灰显，障碍计非空）④ 使用限制（CD / 每场次数）。
/// 敌方无"携带"概念，跳过 ①；零随机、零写状态（CD/次数只读台账），幂等。
/// </summary>
public sealed class SkillUseResolver
{
    private readonly SkillsConfig _skills;

    public SkillUseResolver(SkillsConfig skills)
    {
        _skills = skills ?? throw new System.ArgumentNullException(nameof(skills));
    }

    public Availability Resolve(SkillUseContext ctx)
    {
        SkillTemplateConfig skill = ctx.Skill;

        // ① 战前携带 5（O-29：战中不可换；敌方跳过）
        if (!ctx.IsEnemy && (ctx.Carried is null || !ctx.Carried.Contains(skill.Id)))
        {
            return new Availability(AvailabilityReason.NotCarried, Availability.TooltipFor(AvailabilityReason.NotCarried));
        }

        // ② 自身站位要求（"all" = 任意位置）
        int slot = ctx.AllyBoard.UnitAtPosition(ctx.Caster) ?? -1;
        if (slot < 0 || !skill.SelfSlots.Allows(slot))
        {
            return new Availability(AvailabilityReason.BadStance, Availability.TooltipFor(AvailabilityReason.BadStance));
        }

        // ③ 目标范围内"部分非空"（全空 → 灰显；障碍计入非空 #73/#77/#105）
        IReadOnlyList<int> targets = SkillTargetResolver.Resolve(skill, ctx.Caster, ctx.AllyBoard, ctx.TargetBoard);
        if (targets.Count == 0)
        {
            return new Availability(AvailabilityReason.NoTarget, Availability.TooltipFor(AvailabilityReason.NoTarget));
        }

        // ④ 使用限制（只读运行台账）
        if (ctx.Runtime.CooldownRemaining(ctx.Caster, skill.Id) > 0)
        {
            return new Availability(AvailabilityReason.OnCooldown, Availability.TooltipFor(AvailabilityReason.OnCooldown));
        }

        if (skill.UseLimit.Type == UseLimitType.PerBattle
            && ctx.Runtime.UsedPerBattle(ctx.Caster, skill.Id) >= skill.UseLimit.Value.GetValueOrDefault())
        {
            return new Availability(AvailabilityReason.UsesExhausted, Availability.TooltipFor(AvailabilityReason.UsesExhausted));
        }

        return Availability.Ok;
    }
}