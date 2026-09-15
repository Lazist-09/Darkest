using System;
using System.Collections.Generic;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴🔴 **走格光照：沿一条【格路径】逐格扣光**（把 `WalkLightCost` 的守恒规则接到**实际行走**上）：
///
/// 段（segment）的定义（策划 `#338`①＋架构 `P30` 附注④）：
///   · **一段 = 两个房间之间那段走廊**（房间格**不扣光**；走廊格扣）✓
///   · 进段 `acc = segmentCost`；每格扣 `floor(acc / 本段剩余格数)`；**余数结转**；**末格扣清** ✓
///   ⇒ 因此**总扣除恰好 = `segmentCost` × 走过的段数** —— 与"一段有几格"**无关** ✓
///   （这正是架构 `G4`：**总消耗不得因格变多而暴涨** ⚠️）
///
/// ⚠️ 本类**不持有状态**（纯函数）⇒ 表现层/流程可先用它算"这一趟要走掉多少光"，再接实际扣除 ✓
/// </summary>
public static class DungeonWalkLight
{
    /// <summary>
    /// 沿 `path`（**不含起点、含终点**；与 `DungeonWalker.PathTo` 同口径）逐格给出扣量。
    /// 🔴 要求路径**终点落在房间格**上（否则最后一段未走完 ⇒ 会留下未结算的余数，属调用方状态问题）✓
    /// </summary>
    public static IReadOnlyList<int> PlanPath(
        DungeonGrid grid,
        IReadOnlyDictionary<(int X, int Y), int> tileRoom,
        (int X, int Y) from,
        IReadOnlyList<(int X, int Y)> path,
        int segmentCost)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        if (path is null || path.Count == 0)
        {
            return Array.Empty<int>();
        }

        if (!IsRoom(tileRoom, path[^1]))
        {
            throw new ArgumentException(
                "路径终点不在房间格上 ⇒ 最后一段未结算（余数会漂移）。请让路径在进房间处结束 ✓",
                nameof(path));
        }

        var deductions = new List<int>(path.Count);
        var cur = from;
        int acc = 0;
        int remaining = 0;

        foreach ((int X, int Y) next in path)
        {
            bool fromRoom = IsRoom(tileRoom, cur);
            bool toRoom = IsRoom(tileRoom, next);
            if (fromRoom && !toRoom)
            {
                // 出房间 ⇒ **进段**：本段格数 = 从下一格起、连续走廊格数（到下一个房间格为止）✓
                acc = segmentCost;
                remaining = CountCorridorRun(tileRoom, path, deductions.Count);
            }

            if (!toRoom)
            {
                if (remaining <= 0)
                {
                    throw new InvalidOperationException(
                        "段内剩余格数 ≤ 0（口径错误：进段时应已算出本段格数）⇒ 拒绝继续（不静默扣 0）");
                }

                (int deduct, int newAcc) = WalkLightCost.StepCost(acc, remaining);
                deductions.Add(deduct);
                acc = newAcc;
                remaining--;
                if (remaining == 0 && acc != 0)
                {
                    throw new InvalidOperationException(
                        $"本段走完但余额 {acc} ≠ 0（守恒被破坏）⇒ 拒绝继续（这是口径 bug，不是数据问题）✓");
                }
            }
            else
            {
                deductions.Add(0); // 房间格：不扣光 ✓
            }

            cur = next;
        }

        return deductions;
    }

    /// <summary>`from` 起（含）连续走廊格数，直到遇到房间格或路径结束 ✓</summary>
    private static int CountCorridorRun(
        IReadOnlyDictionary<(int X, int Y), int> tileRoom,
        IReadOnlyList<(int X, int Y)> path,
        int startIndex)
    {
        int n = 0;
        for (int i = startIndex; i < path.Count && !IsRoom(tileRoom, path[i]); i++)
        {
            n++;
        }

        return n;
    }

    private static bool IsRoom(IReadOnlyDictionary<(int X, int Y), int> tileRoom, (int X, int Y) pos)
        => tileRoom.ContainsKey(pos);
}
