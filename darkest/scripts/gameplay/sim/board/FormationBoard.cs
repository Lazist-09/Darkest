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
public sealed partial class FormationBoard : IFormation
{
    private readonly FormationSide _side;
    private readonly FormationRules _rules;
    private readonly UnitRuntime?[] _units;           // index = pos - 1
    private readonly ObstacleRuntime?[] _obstacles;   // index = pos - 1
    private readonly HashSet<int> _extensionSlots;

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
        _extensionSlots = new HashSet<int>(layout.ExtensionSlots);

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
        return _extensionSlots.Contains(pos) ? SlotKind.Extension : SlotKind.Combat;
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

    /// <summary>
    /// 两点【直接互换】（#41a 增援「X 有人则交换」/ #180 移动「与目标位交换」）：
    /// 只交换 a、b 两槽单位，途经槽位不受影响（区别于 TrySwapChain 的逐级推移语义）。
    /// 任一槽为空/越界/相同 → false（空位直入由调用方走 Remove+Place）。
    /// </summary>
    public bool SwapSlots(int a, int b)
    {
        if (a == b || a < 1 || a > SlotCount || b < 1 || b > SlotCount)
        {
            return false;
        }

        UnitRuntime? ua = _units[a - 1];
        UnitRuntime? ub = _units[b - 1];
        if (ua is null || ub is null)
        {
            return false;
        }

        _units[a - 1] = ub;
        _units[b - 1] = ua;
        return true;
    }

    // ------------------------------------------------------------------
    // T-M1-05 预览（只读快照 / dry-run）
    // ------------------------------------------------------------------
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

}