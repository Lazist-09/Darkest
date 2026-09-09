using System.Collections.Generic;

namespace Darkest.Core.Contracts;

/// <summary>
/// 阵型只读契约（blueprint §9.1；data_schema §2.1 枚举对齐）。
/// 实现 = FormationBoard（一侧一张板：我方 6 槽 / 敌方 4 槽）。
/// </summary>
public interface IFormation
{
    /// <summary>本板阵营。</summary>
    FormationSide Side { get; }

    /// <summary>槽位三态查询。</summary>
    SlotState GetSlot(int pos);

    /// <summary>槽位上的单位（Blocked 槽与 Empty 槽返回 null）。</summary>
    UnitId? UnitAt(int pos);

    /// <summary>
    /// 非空槽位列表：includeObstacle=true 时含 Blocked（障碍计入「范围内非空」，GDD §1.1）；
    /// false 时只含 Occupied（单位）。
    /// </summary>
    IReadOnlyList<int> OccupiedPositions(bool includeObstacle = true);

    /// <summary>
    /// 逐级交换链（formation.md §2 / #20/#21/#22/#78）：
    /// mover 从 fromPos 朝 toPos 每步与邻格占据物（角色或障碍）交换一次，共 distance 步；
    /// 任一步目标格为 Empty → 整条链失败、板不动；永不产生空位、不触发靠齐；
    /// 撞障碍 = 照常交换（#22）。位移 ≠ 靠齐（rules 键）。
    /// </summary>
    DisplaceResult TrySwapChain(UnitId mover, int fromPos, int toPos, int distance);

    /// <summary>
    /// 向中靠齐（formation.md §3 / #24/#114/#115）：死亡/离场瞬间调用一次，单次调用原子
    /// 返回整条变更序列；虚弱者不参与前移（编号不减小）、障碍不阻挡、全虚弱留空不补。
    /// </summary>
    IReadOnlyList<SlotChange> CloseUp(int fromPos);

    /// <summary>
    /// 边界/硬墙查询（供位移预览 dry-run）：dir ∈ {+1, -1}，返回该方向是否仍存在可移动的一步。
    /// </summary>
    bool CanCloseUpIn(int dir);
}