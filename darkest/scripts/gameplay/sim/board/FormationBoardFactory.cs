using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Board;

/// <summary>
/// 编成装载（T-M1-01/04）：把 <see cref="FormationConfig"/>（data_schema §3.6）映射为
/// 两侧 <see cref="FormationBoard"/>。board 构造自配置快照，配置在战斗期只读（blueprint §7）。
/// 引用完整性（units.json 原型 id）校验待 M2/M3 数据齐备后接入（data_schema §4.2 P1/P2）。
/// </summary>
public static class FormationBoardFactory
{
    public static FormationBoard CreatePlayerBoard(FormationConfig cfg) => Create(cfg, FormationSide.Player);

    public static FormationBoard CreateEnemyBoard(FormationConfig cfg) => Create(cfg, FormationSide.Enemy);

    public static FormationBoard Create(FormationConfig cfg, FormationSide side)
    {
        if (cfg is null)
        {
            throw new ArgumentNullException(nameof(cfg));
        }

        SlotLayoutConfig layoutCfg = side == FormationSide.Player ? cfg.Player : cfg.Enemy;
        if (layoutCfg is null)
        {
            throw new InvalidOperationException($"formation.json 缺少 {side} 布局（应已由 FormationConfig.Validate 拦截）。");
        }

        IReadOnlyList<RosterEntryConfig> roster =
            side == FormationSide.Player ? cfg.InitialRoster.Player : cfg.InitialRoster.Enemy;

        var layout = new SlotLayout(layoutCfg.SlotCount, layoutCfg.CombatSlots, layoutCfg.SupportSlots);
        var units = new Dictionary<int, UnitRuntime>();
        foreach (RosterEntryConfig entry in roster)
        {
            units[entry.Slot] = new UnitRuntime(UnitId.Of(entry.Unit), side, weak: false);
        }

        var obstacles = new Dictionary<int, ObstacleRuntime>();
        foreach (ObstacleConfig o in cfg.Obstacles)
        {
            if ((o.Side == "player") == (side == FormationSide.Player))
            {
                obstacles[o.Slot] = new ObstacleRuntime(o.Hp);
            }
        }

        FormationRules rules = MapRules(cfg.Rules);
        return new FormationBoard(side, layout, rules, units, obstacles);
    }

    private static FormationRules MapRules(FormationRulesConfig r)
    {
        return new FormationRules(
            BoundaryAsHardWall: r.BoundaryAsHardWall,
            DisplacementOnlyViaSwapChain: r.DisplacementOnlyViaSwapChain,
            ObstacleSwapsLikeUnit: r.ObstacleSwapsLikeUnit,
            CloseUpOnDeathImmediate: r.CloseUpOnDeathImmediate,
            CloseUpIgnoresObstacle: r.CloseUpIgnoresObstacle,
            CloseUpEnemySymmetric: r.CloseUpEnemySymmetric,
            SwapPlayerInitiated: r.SwapPlayerInitiated,
            SlotThreeState: r.SlotThreeState,
            DeathsAndCloseUpSeparateFromDisplacement: r.DeathsAndCloseUpSeparateFromDisplacement);
    }
}