namespace Darkest.Core.Contracts;

/// <summary>
/// 阵型行为规则标记（data_schema §3.6 `rules` 九键，全部 true）。
/// 行为开关一律读数据（blueprint §7 起手值纪律），禁止在 Board 内另拍一套常量。
/// </summary>
public sealed record FormationRules(
    bool BoundaryAsHardWall = true,
    bool DisplacementOnlyViaSwapChain = true,
    bool ObstacleSwapsLikeUnit = true,
    bool CloseUpOnDeathImmediate = true,
    bool CloseUpIgnoresObstacle = true,
    bool CloseUpEnemySymmetric = true,
    bool SwapPlayerInitiated = true,
    bool SlotThreeState = true,
    bool DeathsAndCloseUpSeparateFromDisplacement = true)
{
    public static FormationRules Default() => new();
}