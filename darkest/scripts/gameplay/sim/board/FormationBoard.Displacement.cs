// 🔴 从 FormationBoard.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）
//    本文件 = **T-M1-02 交换链 + T-M1-03 向中靠齐**（`TrySwapChain` / `CloseUp` / `CanCloseUpIn` ＋ 槽位搜索私有助手 `FindLeftmostEmptyOnOrAfter`/`FindLeftmostHealthyAfter`） · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_side, _rules, _units, _obstacles
using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;

namespace Darkest.Gameplay.Sim.Board;

public sealed partial class FormationBoard
{
    /// <inheritdoc />
    public DisplaceResult TrySwapChain(UnitId mover, int fromPos, int toPos, int distance)
    {
        if (fromPos < 1 || fromPos > SlotCount)
        {
            return DisplaceResult.Fail(DisplaceFailureReason.InvalidFromSlot);
        }

        if (toPos < 1 || toPos > SlotCount)
        {
            return DisplaceResult.Fail(DisplaceFailureReason.InvalidToSlot); // 边界即硬墙（#78）
        }

        if (fromPos == toPos)
        {
            throw new ArgumentException($"位移 fromPos==toPos=={fromPos} 无意义。");
        }

        if (distance != Math.Abs(toPos - fromPos))
        {
            throw new ArgumentException(
                $"distance({distance}) 必须等于路径长度 |{toPos}-{fromPos}| = {Math.Abs(toPos - fromPos)}。");
        }

        if (UnitAt(fromPos) != mover)
        {
            return DisplaceResult.Fail(DisplaceFailureReason.NotMoverAtFrom);
        }

        // 原子预检：mover 路径上的每个目标格必须非 Empty（#20/#21/#78 泛化——
        // 缩编后队尾空位挡推 / 最外侧被外推均属此列）。交换不产生空槽，故预检即终态校验。
        int dir = Math.Sign(toPos - fromPos);
        for (int step = 1; step <= distance; step++)
        {
            int target = fromPos + dir * step;
            SlotState targetState = GetSlot(target);
            if (targetState == SlotState.Empty)
            {
                return DisplaceResult.Fail(DisplaceFailureReason.TargetSlotEmpty);
            }

            if (targetState == SlotState.Blocked && !_rules.ObstacleSwapsLikeUnit)
            {
                // 规则关闭「障碍当角色交换」时，障碍视为不可穿越（防呆；切片配置恒开启）。
                return DisplaceResult.Fail(DisplaceFailureReason.TargetSlotEmpty);
            }
        }

        // 逐级交换链（formation.md §2）：每步与邻格占据物（角色或障碍）交换一次。
        var steps = new List<SlotChange>(distance);
        int cur = fromPos;
        for (int step = 1; step <= distance; step++)
        {
            int next = cur + dir;
            SlotState nextState = GetSlot(next);
            UnitRuntime? nextUnit = _units[next - 1];
            ObstacleRuntime? nextObstacle = _obstacles[next - 1];

            UnitRuntime moverRuntime = _units[cur - 1]!;
            _units[next - 1] = moverRuntime;
            if (nextUnit is not null)
            {
                _units[cur - 1] = nextUnit;
            }
            else if (_rules.ObstacleSwapsLikeUnit && nextObstacle is not null)
            {
                _units[cur - 1] = null;
                _obstacles[cur - 1] = nextObstacle; // 障碍落到 mover 原位（#22）
            }

            _obstacles[next - 1] = null;

            steps.Add(new SlotChange(
                _side,
                SlotChangeKind.Swap,
                cur,
                next,
                moverRuntime.Id,
                nextUnit?.Id,
                FromSlotState: GetSlot(cur),
                ToSlotState: SlotState.Occupied));

            cur = next;
        }

        return DisplaceResult.Ok(steps);
    }

    /// <inheritdoc />
    public IReadOnlyList<SlotChange> CloseUp(int fromPos)
    {
        ValidateSlot(fromPos);
        var steps = new List<SlotChange>();

        if (GetSlot(fromPos) != SlotState.Empty)
        {
            throw new ArgumentException(
                $"CloseUp({fromPos}) 要求该槽为 Empty（由死亡/离场制造）；当前为 {GetSlot(fromPos)}。");
        }

        // 反复把【最靠左的空槽】用【其右侧最近的健康角色】经相邻交换链补上
        // （formation.md §3：虚弱者不参与前移、编号不减小；障碍不阻挡、被交换推后；全虚弱留空）。
        // 防呆：每次外循环必有健康角色左移、空位右移（严格推进），理论上限 = 槽数×槽数；
        // 超出即抛异常，杜绝任何"不收敛 → 无限步 → 内存上涨"的可能（主程序防呆纪律）。
        int guard = SlotCount * SlotCount + 8;
        while (true)
        {
            if (--guard < 0)
            {
                throw new InvalidOperationException(
                    $"CloseUp({fromPos}) 未在 {SlotCount * SlotCount + 8} 次内收敛——板状态异常（槽数 {SlotCount}）。");
            }

            int empty = FindLeftmostEmptyOnOrAfter(fromPos);
            int source = FindLeftmostHealthyAfter(empty);

            if (empty < 0 || source < 0)
            {
                break; // 已收敛：无空槽 或 空槽右侧无健康角色（全虚弱 / 队尾）
            }

            // 相邻交换链：source 槽的健康角色逐格左移直到落入 empty。
            // 数组下标 = 槽位号 - 1（曾以槽位号直作下标导致整体偏移 +1 的死循环——2026-09-09 冒烟钉死）。
            for (int pos = source - 1; pos >= empty; pos--)
            {
                int moverPos = pos + 1;
                UnitRuntime mover = _units[moverPos - 1]!; // 当前持健康角色的槽
                UnitRuntime? leftUnit = _units[pos - 1];
                ObstacleRuntime? leftObstacle = _obstacles[pos - 1];
                bool targetEmpty = leftUnit is null && leftObstacle is null;

                if (targetEmpty)
                {
                    // 末步：移入空槽（Move）
                    _units[pos - 1] = mover;
                    _units[moverPos - 1] = null;
                    steps.Add(new SlotChange(
                        _side, SlotChangeKind.Move, moverPos, pos,
                        mover.Id, null,
                        FromSlotState: SlotState.Empty,
                        ToSlotState: SlotState.Occupied));
                }
                else
                {
                    // 交换：健康角色与左侧占据物（角色/障碍/虚弱者）互换
                    _units[pos - 1] = mover;
                    if (leftUnit is not null)
                    {
                        _units[moverPos - 1] = leftUnit;
                    }
                    else
                    {
                        _units[moverPos - 1] = null;
                        _obstacles[moverPos - 1] = leftObstacle; // 障碍被交换推后（#114）
                    }

                    _obstacles[pos - 1] = null;
                    steps.Add(new SlotChange(
                        _side, SlotChangeKind.Swap, moverPos, pos,
                        mover.Id, leftUnit?.Id,
                        FromSlotState: GetSlot(moverPos),
                        ToSlotState: SlotState.Occupied));
                }
            }
        }

        return steps;
    }

    /// <inheritdoc />
    public bool CanCloseUpIn(int dir)
    {
        if (dir is not (-1) and not 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dir), "dir 只允许 -1（向中）或 +1（向队尾）。");
        }

        for (int pos = 1; pos <= SlotCount; pos++)
        {
            if (GetSlot(pos) != SlotState.Empty)
            {
                continue;
            }

            for (int q = dir == -1 ? pos + 1 : 1; q <= SlotCount; q++)
            {
                if (dir == 1 && q >= pos)
                {
                    break;
                }

                UnitRuntime? u = _units[q - 1];
                if (u is not null && !u.Weak)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private int FindLeftmostEmptyOnOrAfter(int fromPos)
    {
        for (int pos = fromPos; pos <= SlotCount; pos++)
        {
            if (GetSlot(pos) == SlotState.Empty)
            {
                return pos;
            }
        }

        return -1;
    }

    private int FindLeftmostHealthyAfter(int empty)
    {
        if (empty < 1)
        {
            return -1;
        }

        for (int pos = empty + 1; pos <= SlotCount; pos++)
        {
            UnitRuntime? u = _units[pos - 1];
            if (u is not null && !u.Weak)
            {
                return pos;
            }
        }

        return -1;
    }
}
