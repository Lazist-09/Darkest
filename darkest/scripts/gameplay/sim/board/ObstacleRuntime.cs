using System;

namespace Darkest.Gameplay.Sim.Board;

/// <summary>
/// 障碍占位实体（GDD §1.1 本质：「不会行动、只有血量的占位角色」）。
/// M1 只建模（三态 Blocked + 可被交换推动 + 可移除），伤害/血量结算归 M2/M4；
/// 障碍不参与士气/虚弱/死门，debuff 免疫（GDD §1.1）。
/// </summary>
/// <param name="Hp">剩余血量；null = 不可被摧毁占位（data_schema §3.6 缺省）。</param>
public sealed record ObstacleRuntime(int? Hp)
{
    public bool IsDestroyable => Hp.HasValue;

    /// <summary>
    /// 🔴 受伤（2026-09-20 接线）：**返回新实例**（record 不可变 ⇒ 不就地改，避免快照/dry-run 被污染）。
    /// <para>· **不可摧毁**（`Hp == null`）⇒ 原样返回（`hp: null` 表示"免疫"，#73/#105 口径）✓</para>
    /// <para>· 扣减后 **下限钳到 0**（不为负）；**是否移除由调用方判**（`Hp &lt;= 0` ⇒ `RemoveObstacle`）——
    ///    GDD §1.1 明确"**移除 → 立即靠齐由调用方负责任务编排**"✓</para>
    /// </summary>
    public ObstacleRuntime TakeDamage(int amount)
    {
        if (Hp is not { } hp)
        {
            return this; // 不可摧毁占位：吸收但不掉血 ✓
        }

        return this with { Hp = Math.Max(0, hp - Math.Max(0, amount)) };
    }
}