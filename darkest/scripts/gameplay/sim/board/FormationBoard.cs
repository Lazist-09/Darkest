using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;

namespace Darkest.Gameplay.Sim.Board;

/// <summary>
/// FormationBoard：一侧阵型的确定性内核（我方 6 槽 / 敌方 4 槽，blueprint §5a）。
/// 槽位三态（Occupied/Empty/Blocked）、逐级交换链（T-M1-02）、向中靠齐（T-M1-03）、
/// 预置障碍（T-M1-04）、位移预览 dry-run（T-M1-05）。零 Godot 引用。
/// 行为开关全部读 <see cref="FormationRules"/>（data_schema §3.6 rules，禁止代码另拍常量）。
/// </summary>
public sealed class FormationBoard : IFormation
{
    private readonly FormationSide _side;
    private readonly FormationRules _rules;
    private readonly UnitRuntime?[] _units;           // index = pos - 1
    private readonly ObstacleRuntime?[] _obstacles;   // index = pos - 1
    private readonly HashSet<int> _supportSlots;

    public FormationBoard(
        FormationSide side,
        SlotLayout layout,
        FormationRules rules,
        IReadOnlyDictionary<int, UnitRuntime>? initialUnits = null,
        IReadOnlyDictionary<int, ObstacleRuntime>? initialObstacles = null)
    {
        _side = side;
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _units = new UnitRuntime?[layout.SlotCount];
        _obstacles = new ObstacleRuntime?[layout.SlotCount];
        _supportSlots = new HashSet<int>(layout.SupportSlots);

        if (initialUnits is not null)
        {
            foreach ((int slot, UnitRuntime unit) in initialUnits)
            {
                SetUnit(slot, unit);   // 校验 + 与障碍互斥
            }
        }

        if (initialObstacles is not null)
        {
            foreach ((int slot, ObstacleRuntime obstacle) in initialObstacles)
            {
                SetObstacle(slot, obstacle);
            }
        }
    }

    /// <summary>本板阵营。</summary>
    public FormationSide Side => _side;

    /// <summary>槽位布局（只读，战斗期不变）。</summary>
    public SlotLayout Layout { get; }

    /// <summary>行为规则标记（起手值来自 data/formation.json rules）。</summary>
    public FormationRules Rules => _rules;

    /// <summary>本侧槽数。</summary>
    public int SlotCount => Layout.SlotCount;

    /// <inheritdoc />
    public SlotState GetSlot(int pos)
    {
        ValidateSlot(pos);
        int i = pos - 1;
        if (_units[i] is not null)
        {
            return SlotState.Occupied;
        }

        return _obstacles[i] is not null ? SlotState.Blocked : SlotState.Empty;
    }

    /// <inheritdoc />
    public UnitId? UnitAt(int pos)
    {
        ValidateSlot(pos);
        return _units[pos - 1]?.Id;
    }

    /// <summary>槽类型（战斗位 / 支援位，供 M3 可用性判定与 UI；“位置不是职业”#139）。</summary>
    public SlotKind SlotKindAt(int pos)
    {
        ValidateSlot(pos);
        return _supportSlots.Contains(pos) ? SlotKind.Support : SlotKind.Combat;
    }

    /// <summary>
    /// 非空槽位列表：includeObstacle=true 含 Blocked（障碍计入「范围内非空」，GDD §1.1）；
    /// false 只含 Occupied。
    /// </summary>
    public IReadOnlyList<int> OccupiedPositions(bool includeObstacle = true)
    {
        var list = new List<int>();
        for (int pos = 1; pos <= SlotCount; pos++)
        {
            SlotState state = GetSlot(pos);
            if (state == SlotState.Occupied || (includeObstacle && state == SlotState.Blocked))
            {
                list.Add(pos);
            }
        }

        return list;
    }

    /// <summary>本侧槽位升序的在列单位列表（固定枚举序：我方 1~6 / 敌方 1~4；供行动序列固定抽取序）。</summary>
    public IReadOnlyList<UnitRuntime> UnitsInSlotOrder()
    {
        var list = new List<UnitRuntime>();
        for (int pos = 1; pos <= SlotCount; pos++)
        {
            if (_units[pos - 1] is { } unit)
            {
                list.Add(unit);
            }
        }

        return list;
    }

    /// <summary>首个持有指定单位的槽位号（无 → null；供行动序列破平局取编号）。</summary>
    public int? UnitAtPosition(UnitId id)
    {
        for (int pos = 1; pos <= SlotCount; pos++)
        {
            if (_units[pos - 1] is { } u && u.Id == id)
            {
                return pos;
            }
        }

        return null;
    }

    /// <summary>槽位上的单位运行时实例（Blocked/Empty → null；供结算管线取属性）。</summary>
    public UnitRuntime? UnitRuntimeAt(int pos)
    {
        ValidateSlot(pos);
        return _units[pos - 1];
    }

    /// <summary>移除单位（死亡/离场；槽位变 Empty）。返回是否确有空单。</summary>
    public bool RemoveUnitAt(int pos)
    {
        ValidateSlot(pos);
        if (_units[pos - 1] is null)
        {
            return false;
        }

        _units[pos - 1] = null;
        return true;
    }

    /// <summary>装配期置位（增援/初始编成用；与障碍互斥校验）。</summary>
    public void PlaceUnitAt(int pos, UnitRuntime unit)
    {
        SetUnit(pos, unit);
    }

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

    // ------------------------------------------------------------------
    // T-M1-04 障碍操作 + T-M1-05 预览（只读快照 / dry-run）
    // ------------------------------------------------------------------

    /// <summary>障碍血量（null=不可摧毁占位）；非障碍槽返回 null 且 hasObstacle=false。</summary>
    public bool TryGetObstacleHp(int pos, out int? hp)
    {
        ValidateSlot(pos);
        ObstacleRuntime? o = _obstacles[pos - 1];
        if (o is null)
        {
            hp = null;
            return false;
        }

        hp = o.Hp;
        return true;
    }

    /// <summary>移除障碍（血量归零 → 移除 → 立即靠齐由调用方负责任务编排，GDD §1.1 表）。</summary>
    public bool RemoveObstacle(int pos)
    {
        ValidateSlot(pos);
        if (_obstacles[pos - 1] is null)
        {
            return false;
        }

        _obstacles[pos - 1] = null;
        return true;
    }

    /// <summary>只读快照 = 板数据的深拷贝（单位引用共享但槽数组独立），供预览 dry-run，永不污染真实板。</summary>
    public FormationBoard CreatePreviewSnapshot()
    {
        var snapshot = new FormationBoard(_side, Layout, _rules);
        Array.Copy(_units, snapshot._units, _units.Length);
        Array.Copy(_obstacles, snapshot._obstacles, _obstacles.Length);
        return snapshot;
    }

    /// <summary>
    /// 位移预览 dry-run（T-M1-05 / blueprint §5d 注）：对【当前板的只读快照】执行与真实结算
    /// 同一个 <see cref="TrySwapChain"/>；输出与确认后结算逐事件一致，且不改动真实板。
    /// </summary>
    public DisplaceResult DryRunSwapChain(UnitId mover, int fromPos, int toPos, int distance)
    {
        FormationBoard snapshot = CreatePreviewSnapshot();
        return snapshot.TrySwapChain(mover, fromPos, toPos, distance);
    }

    // ------------------------------------------------------------------
    // 内部
    // ------------------------------------------------------------------

    private void SetUnit(int pos, UnitRuntime unit)
    {
        ValidateSlot(pos);
        if (unit is null)
        {
            throw new ArgumentNullException(nameof(unit));
        }

        if (_obstacles[pos - 1] is not null)
        {
            throw new InvalidOperationException($"槽 {pos} 已被障碍占据，不能放置单位。");
        }

        _units[pos - 1] = unit;
    }

    private void SetObstacle(int pos, ObstacleRuntime obstacle)
    {
        ValidateSlot(pos);
        if (obstacle is null)
        {
            throw new ArgumentNullException(nameof(obstacle));
        }

        if (_units[pos - 1] is not null)
        {
            throw new InvalidOperationException($"槽 {pos} 已被角色占据，不能放置障碍。");
        }

        _obstacles[pos - 1] = obstacle;
    }

    private void ValidateSlot(int pos)
    {
        if (pos < 1 || pos > SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pos),
                $"槽位 {pos} 越界 [1, {SlotCount}]（阵营 {_side}）。");
        }
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