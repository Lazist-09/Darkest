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
}