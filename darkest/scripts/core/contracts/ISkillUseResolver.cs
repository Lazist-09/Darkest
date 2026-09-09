using Darkest.Core.Contracts;

namespace Darkest.Core.Contracts;

/// <summary>技能可用性判定原因（blueprint §9.3 逐字）。</summary>
public enum AvailabilityReason
{
    Ok,
    NotCarried,
    BadStance,
    NoTarget,
    OnCooldown,
    UsesExhausted,
}

/// <summary>可用性判定结果（reason + UI tooltip 文案，ui_spec §4 逐字）。</summary>
public sealed record Availability(AvailabilityReason Reason, string Tooltip)
{
    public static readonly Availability Ok = new(AvailabilityReason.Ok, "");

    /// <summary>tooltip 文案映射（ui_spec §4 / 必显 #3，逐字）。</summary>
    public static string TooltipFor(AvailabilityReason reason) => reason switch
    {
        AvailabilityReason.BadStance => "站位不符",
        AvailabilityReason.NoTarget => "范围内没有目标",
        AvailabilityReason.OnCooldown => "CD 中",
        AvailabilityReason.UsesExhausted => "每场次数用尽",
        AvailabilityReason.NotCarried => "未携带",
        _ => "",
    };
}

/// <summary>技能可用性判定契约（blueprint §9.3；零随机、零写状态，幂等）。</summary>
public interface ISkillUseResolver
{
    /// <summary>判定顺序固定：①携带 ②站位 ③目标部分非空 ④使用限制（skill.md §5）。</summary>
    Availability Resolve(UnitId caster, string skillId, IFormation snapshot);
}