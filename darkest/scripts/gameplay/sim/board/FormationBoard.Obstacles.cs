// 🔴 从 FormationBoard.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）
//    本文件 = **T-M1-04 障碍操作**（`TryGetObstacleHp` 读口 / `RemoveObstacle` 写口 / `DamageObstacle` 受击） · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_obstacles
using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;

namespace Darkest.Gameplay.Sim.Board;

public sealed partial class FormationBoard
{
    // ------------------------------------------------------------------
    // T-M1-04 障碍操作（T-M1-05 预览半边随预览留在主文件）
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

    /// <summary>
    /// 🔴 **障碍受击**（2026-09-20 接线）：扣血 → 若归零则**自动移除**，返回**本槽是否还有障碍**。
    /// <para>· `hp == null`（不可摧毁）⇒ **吸收但不掉血、不移除** ⇒ 返回 true（#73/#105 口径）✓</para>
    /// <para>· 这是 `TryGetObstacleHp`（读）+ `RemoveObstacle`（写）的**唯一生产调用点** ——
    ///    此前两者只有测试在调 ⇒「**会挡路的木箱/石堆**」在实机里**打不掉**（`DamagePipeline` 遇
    ///    `Blocked` 直接 `continue` ⇒ 障碍是纯无敌墙）⚠️</para>
    /// <para>· ⚠️ **移除后的"靠齐"不在此处**：GDD §1.1 明文"立即靠齐由调用方负责任务编排" ⇒
    ///    本方法只做"没了就算没了"，靠齐交由 <see cref="CloseUpAfterRemoval"/>（若需）✓</para>
    /// </summary>
    /// <returns>true = 该槽仍被障碍占据（含不可摧毁）；false = 槽已空（本次被摧毁移除）。</returns>
    public bool DamageObstacle(int pos, int amount)
    {
        ValidateSlot(pos);
        if (!TryGetObstacleHp(pos, out int? hp))
        {
            return false; // 非障碍槽：读口判存在（调用方通常也先读一次拿血条）✓
        }

        if (hp is null)
        {
            return true; // 不可摧毁：absorb，无血条 ⇒ 不动 ✓
        }

        int after = hp.Value - Math.Max(0, amount);
        if (after > 0)
        {
            _obstacles[pos - 1] = _obstacles[pos - 1]!.TakeDamage(amount);
            return true;
        }

        // 归零 ⇒ 走既有移除口（**同一语义只此一处**，不写第二份）——靠齐仍由调用方编排 ✓
        RemoveObstacle(pos);
        return false;
    }
}
