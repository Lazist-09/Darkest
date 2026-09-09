using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Turn;

/// <summary>
/// TurnSequencer：行动序列（blueprint §9.2 / T-M2-03）。
/// 每回合 BuildRoundOrder 对全部在列单位按【固定枚举序】（我方槽 1~6 后敌方槽 1~4）
/// 逐次抽取速度浮动骰（blueprint §8.1，杜绝字典/哈希序漂移）；实际速度 = 生效速度 × 浮动因子，
/// 排序降序；同速 → 编号小者先动（#80），跨阵营同速 → 我方先手（M2 §6 组合行默认）。
/// 眩晕（Stunned）出列时跳过并随即清除（GDD §2.5）。零 `using Godot`。
/// </summary>
public sealed class TurnSequencer : ITurnSequencer
{
    private readonly FormationBoard _player;
    private readonly FormationBoard _enemy;
    private readonly BalanceTable _balance;
    private readonly double _weakSpeedMult;
    private Queue<UnitId> _order = new();

    public TurnSequencer(FormationBoard player, FormationBoard enemy, BalanceTable balance)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _weakSpeedMult = balance.WeakSpeedMult;
    }

    /// <inheritdoc />
    public IReadOnlyList<UnitId> BuildRoundOrder(IRngProvider rng)
    {
        if (rng is null)
        {
            throw new ArgumentNullException(nameof(rng));
        }

        var entries = new List<(UnitId Id, bool Player, double Speed)>();
        foreach ((FormationBoard board, bool player) in new[] { (_player, true), (_enemy, false) })
        {
            foreach (UnitRuntime unit in board.UnitsInSlotOrder())
            {
                double effective = unit.EffectiveSpeed(_weakSpeedMult);
                double factor = 1.0;
                if (_balance.SpeedFloatEnabled)
                {
                    double unitRoll = rng.NextPercent() / 100.0; // [0,1)
                    factor = BattleMath.SpeedFloatMultiplier(unitRoll, _balance.SpeedFloatPercent);
                }

                // 固定精度（保留 3 位小数）比较，避免 double 漂移（T-M2-03 要点 2）
                double actual = Math.Round(effective * factor, 3, MidpointRounding.AwayFromZero);
                entries.Add((unit.Id, player, actual));
            }
        }

        // 降序；同速：编号小者先动（#80）；跨阵营同速 → 我方先手（M2 §6 组合行默认）。
        IReadOnlyList<UnitId> order = entries
            .OrderByDescending(e => e.Speed)
            .ThenBy(e => e.Player ? 0 : 1)
            .ThenBy(e => SlotOf(e.Id))
            .Select(e => e.Id)
            .ToArray();

        _order = new Queue<UnitId>(order);
        return order;
    }

    /// <inheritdoc />
    public UnitId? NextActor()
    {
        while (_order.Count > 0)
        {
            UnitId id = _order.Dequeue();
            UnitRuntime? unit = FindUnit(id);
            if (unit is null)
            {
                continue; // 已离场
            }

            if (unit.Stunned)
            {
                unit.Stunned = false; // 跳过本次行动，状态随即结束（GDD §2.5）
                continue;
            }

            return id;
        }

        return null;
    }

    /// <inheritdoc />
    public void OnRemoved(UnitId who)
    {
        // 仅从待出列队列剔除；已出列/未构建部分由 NextActor 的空引用检查兜底。
        var kept = _order.Where(id => id != who).ToArray();
        _order = new Queue<UnitId>(kept);
    }

    private UnitRuntime? FindUnit(UnitId id) => FindIn(_player, id) ?? FindIn(_enemy, id);

    private static UnitRuntime? FindIn(FormationBoard board, UnitId id)
        => board.UnitsInSlotOrder().FirstOrDefault(u => u.Id == id);

    private int SlotOf(UnitId id)
        => _player.UnitAtPosition(id) ?? _enemy.UnitAtPosition(id) ?? int.MaxValue;
}