namespace Darkest.Core.Contracts;

/// <summary>
/// 一步槽位变更的种类（位移/靠齐的动画逐格单元，ui_spec §6/§7）。
/// </summary>
public enum SlotChangeKind
{
    /// <summary>双槽交换：FromSlot 与 ToSlot 的占据物互换（含单位↔单位、单位↔障碍）。</summary>
    Swap,

    /// <summary>单向往移动：UnitA 从 FromSlot 移入 ToSlot（ToSlot 原为 Empty；仅靠齐使用）。</summary>
    Move,
}

/// <summary>
/// 一步槽位变更记录：位移=逐级交换链每步一次 Swap；靠齐=相邻 Swap 与末步 Move 的组合。
/// 字段语义（供 M5 逐格滑动与 dry-run 一致性比对）：
/// <list type="bullet">
/// <item>Side：本侧。</item>
/// <item>FromSlot / ToSlot：参与槽位。</item>
/// <item>Kind=Swap：UnitA(在 FromSlot) 与 UnitB(在 ToSlot) 互换；UnitB=null 表示障碍被交换（其槽位以 From/ToSlotState=Blocked 表达）。</item>
/// <item>Kind=Move：UnitA 从 FromSlot 移入 ToSlot（ToSlot 原 Empty）。</item>
/// <item>FromSlotState / ToSlotState：步骤完成后两个槽位的【结果态】。</item>
/// </list>
/// </summary>
public sealed record SlotChange(
    FormationSide Side,
    SlotChangeKind Kind,
    int FromSlot,
    int ToSlot,
    UnitId? UnitA,
    UnitId? UnitB,
    SlotState FromSlotState,
    SlotState ToSlotState);