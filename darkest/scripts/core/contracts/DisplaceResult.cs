using System;
using System.Collections.Generic;

namespace Darkest.Core.Contracts;

/// <summary>TrySwapChain 失败原因（供 UI「位移失败」反馈，ui_spec §7）。</summary>
public enum DisplaceFailureReason
{
    None,
    InvalidFromSlot,
    InvalidToSlot,

    /// <summary>from 槽位不是 mover（请求与板不符）。</summary>
    NotMoverAtFrom,

    /// <summary>任一步目标格为 Empty → 整条链失败不动（#20/#21 泛化，含缩编后队尾空位挡推，blueprint §9.1）。</summary>
    TargetSlotEmpty,
}

/// <summary>
/// 位移结算结果（blueprint §9.1）：成功返回整条步骤序列（顺序即动画播放顺序）；
/// 失败返回原因且板与调用前逐槽一致（原子）。
/// </summary>
public sealed record DisplaceResult(bool Success, IReadOnlyList<SlotChange> Steps, DisplaceFailureReason Failure)
{
    public static DisplaceResult Ok(IReadOnlyList<SlotChange> steps) => new(true, steps, DisplaceFailureReason.None);

    public static DisplaceResult Fail(DisplaceFailureReason reason) => new(false, Array.Empty<SlotChange>(), reason);
}