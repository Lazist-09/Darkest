using System;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴🔴 **走格的光照消耗：总消耗守恒**（策划 `#338`① ＋ 架构 `P30` 附注④；我先前"平移"的口径**是错的**）：
///
/// 我草案里写"1 段 = 1 格 ⇒ 每格 −30 平移"⇒ 🔴 **前提不成立**：
///   旧"段" = 一次**房间→房间**推进（一趟 6~8 段）；新"格" = **每个瓷砖格**（DD 走廊 1~8 格 + 房间成块）
///   ⇒ 照字面"每格 −30" ⇒ **总消耗 ×N ⇒ 静默改难度** ❌（`#327`③ 预警"段→格会让数值漂移"的第二种形态）
///
/// ✅ **规则（写死；逐步实现，不写死任何数字）**：
///   · 进一段走廊：`acc += segmentCost`（`segmentCost` = **现值** `move.new_area` = 30 ⇒ **不触 `#307`** ✓）
///   · 每走一格：扣 `floor(acc / 本段剩余格数)`，**余数结转到下一格**
///   ⇒ **整数、不漂移、总消耗恒 = `segmentCost` × 段数** ✓（与旧语义逐段等价 ✓）
///   ❌ 禁止：`每格 −30`（×N）· `每格 −30/N` 写成**字面量**（造新数字 + 取整漂移）✓
/// </summary>
public static class WalkLightCost
{
    /// <summary>
    /// 走**一格**应扣多少光照。
    /// </summary>
    /// <param name="acc">本段已累计未扣的光照（进段时 = `segmentCost`；调用方持有）</param>
    /// <param name="remainingTilesInSegment">**含本格**的本段剩余格数（≥ 1；最后格 ⇒ 扣完余数）</param>
    /// <returns>`Deduct` = 本格应扣（≥ 0）；`NewAcc` = 结转后余额（最后格必为 0）✓</returns>
    public static (int Deduct, int NewAcc) StepCost(int acc, int remainingTilesInSegment)
    {
        if (remainingTilesInSegment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingTilesInSegment),
                "本段剩余格数必须 ≥ 1（0 会让除法无意义；这是调用方的状态错误，不静默兜底）✓");
        }

        if (acc <= 0)
        {
            return (0, 0); // 本段已扣完 ⇒ 后续格不再扣（不会负扣 ✓）
        }

        if (remainingTilesInSegment == 1)
        {
            return (acc, 0); // 🔴 最后格：**余数一次扣清**（保证总消耗恒等，不漂移）✓
        }

        int deduct = acc / remainingTilesInSegment; // 整数除法 = floor（acc ≥ 0）✓
        return (deduct, acc - deduct);
    }
}
