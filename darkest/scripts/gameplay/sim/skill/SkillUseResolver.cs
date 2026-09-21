using System.Collections.Generic;
using System.Linq;
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
    bool IsEnemy,
    int SupportPoints = int.MaxValue,   // #211（S0）：战斗级 SP（默认不可耗尽 → 旧调用不受影响）
    int SupportCost = 0,                // 支援位技能消耗（战斗位恒 0）
    bool IsSupportSlotActor = false,    // 只有支援位技能会因 SP 被拒
    IBuffLedger? Buffs = null);         // C-1（v0.99）：潜行门禁数据源（null = 不做潜行过滤，旧调用不受影响）

/// <summary>
/// 技能可用性判定（T-M3-03 / skill.md §5，顺序固定不可调换）：
/// ① 携带 ② 站位 ③ 目标部分非空（全空灰显，障碍计非空）④ 使用限制（CD / 每场次数）。
/// 敌方无"携带"概念，跳过 ①；零随机、零写状态（CD/次数只读台账），幂等。
/// C-1（v0.99）：③ 的候选池在带 `Buffs` 时已剔除潜行目标 ⇒ 全潜行自然落为 `NoTarget`；
/// 若原始槽位里**存在**目标、只因潜行被剔除，则改报 `TargetStealthed` 以便 UI 给出准确文案。
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
        //    C-1：带 Buffs 时潜行目标已被剔除；若剔除后才变空 ⇒ 区分文案（目标处于潜行 vs 范围内没有目标）
        IReadOnlyList<int> targets = SkillTargetResolver.Resolve(
            skill, ctx.Caster, ctx.AllyBoard, ctx.TargetBoard, ctx.Buffs);
        if (targets.Count == 0)
        {
            bool stealthedOnly = ctx.Buffs is not null
                && SkillTargetResolver.Resolve(skill, ctx.Caster, ctx.AllyBoard, ctx.TargetBoard).Count > 0;
            AvailabilityReason reason = stealthedOnly
                ? AvailabilityReason.TargetStealthed
                : AvailabilityReason.NoTarget;
            return new Availability(reason, Availability.TooltipFor(reason));
        }

        // ③.5 D5（#207）前置条件：自身/目标血量阈值、自身虚弱、自身死门 → 不满足则灰显
        if (skill.Requires is { } req)
        {
            UnitRuntime caster = ctx.AllyBoard.UnitRuntimeAt(slot)!;
            if (req.SelfHpBelowPercent is { } selfPct
                && (caster.MaxHp <= 0 || 100.0 * caster.CurrentHp / caster.MaxHp >= selfPct))
            {
                return new Availability(AvailabilityReason.RequiresUnmet, $"需要自身 HP 低于 {selfPct}%");
            }

            if (req.SelfWeak == true && !caster.Weak)
            {
                return new Availability(AvailabilityReason.RequiresUnmet, "需要自身处于虚弱");
            }

            if (req.SelfDeathsDoor == true && !caster.Weak)
            {
                return new Availability(AvailabilityReason.RequiresUnmet, "需要自身处于死门");
            }

            if (req.TargetHpBelowPercent is { } targetPct)
            {
                bool ok = targets.Any(s =>
                {
                    UnitRuntime? t = ctx.TargetBoard.UnitRuntimeAt(s) ?? ctx.AllyBoard.UnitRuntimeAt(s);
                    return t is not null && t.MaxHp > 0 && 100.0 * t.CurrentHp / t.MaxHp < targetPct;
                });
                if (!ok)
                {
                    return new Availability(AvailabilityReason.RequiresUnmet, $"需要目标 HP 低于 {targetPct}%");
                }
            }
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

        // ⑤ #211（S0）支援点：**只有支援位技能**会因 SP 不足被拒（战斗位永不因 SP 被拒；"不足"是"不可用"而非"失败"）
        if (ctx.IsSupportSlotActor && ctx.SupportCost > 0 && ctx.SupportPoints < ctx.SupportCost)
        {
            return new Availability(AvailabilityReason.SupportPointsNotEnough,
                $"支援点不足（当前 {ctx.SupportPoints} / 需要 {ctx.SupportCost}）");
        }

        return Availability.Ok;
    }
}