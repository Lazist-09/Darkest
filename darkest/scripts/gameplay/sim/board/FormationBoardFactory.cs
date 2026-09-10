using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Board;

/// <summary>
/// 编成装载（T-M1-01/04 → M2 属性接入）：把 <see cref="FormationConfig"/>（data_schema §3.6）
/// 与 <see cref="UnitsConfig"/>（§3.1 属性表）映射为两侧 <see cref="FormationBoard"/>。
/// board 构造自配置快照，配置在战斗期只读（blueprint §7）。引用完整性（units.json 原型 id）
/// 校验随数据齐备接入（data_schema §4.2 P1/P2；原型缺失由 UnitsConfig.Get fail-fast）。
/// </summary>
public static class FormationBoardFactory
{
    public static FormationBoard CreatePlayerBoard(FormationConfig cfg, UnitsConfig units)
        => Create(cfg, units, FormationSide.Player);

    public static FormationBoard CreateEnemyBoard(FormationConfig cfg, UnitsConfig units)
        => Create(cfg, units, FormationSide.Enemy);

    public static FormationBoard Create(FormationConfig cfg, UnitsConfig units, FormationSide side)
    {
        if (cfg is null)
        {
            throw new ArgumentNullException(nameof(cfg));
        }

        if (units is null)
        {
            throw new ArgumentNullException(nameof(units));
        }

        SlotLayoutConfig layoutCfg = side == FormationSide.Player ? cfg.Player : cfg.Enemy;
        if (layoutCfg is null)
        {
            throw new InvalidOperationException($"formation.json 缺少 {side} 布局（应已由 FormationConfig.Validate 拦截）。");
        }

        IReadOnlyList<RosterEntryConfig> roster =
            side == FormationSide.Player ? cfg.InitialRoster.Player : cfg.InitialRoster.Enemy;

        var layout = new SlotLayout(layoutCfg.SlotCount, layoutCfg.CombatSlots, layoutCfg.SupportSlots);
        var unitsBySlot = new Dictionary<int, UnitRuntime>();
        var archetypeCount = new Dictionary<string, int>();
        foreach (RosterEntryConfig entry in roster)
        {
            // 实例 id 唯一化（同原型多实例：原型 / 原型_2 / 原型_3…）；ArchetypeId 保留原型供技能池/AI/中文名匹配
            int seen = archetypeCount.TryGetValue(entry.Unit, out int c) ? c + 1 : 1;
            archetypeCount[entry.Unit] = seen;
            string instanceId = seen == 1 ? entry.Unit : $"{entry.Unit}_{seen}";
            UnitRuntime unit = new(UnitId.Of(instanceId), side, UnitStatsMapper.From(units.Get(entry.Unit)),
                weak: false, archetypeId: entry.Unit);
            unitsBySlot[entry.Slot] = unit;
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
        return new FormationBoard(side, layout, rules, unitsBySlot, obstacles);
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