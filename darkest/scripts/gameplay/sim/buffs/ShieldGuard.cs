using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Buffs;

/// <summary>
/// 护盾 / 护卫（T-M4-08，blueprint §9.9 语义的领域实现；接口雏形与 UnitRuntime 类型有摩擦，
/// 以具体类落地并注明，M5 装配时对齐蓝图接口）：
/// 护盾 = 按次数挡物理（#156），被挡这次完全无效；精神穿透；AOE 算 1 次攻击（整动作挡掉）；
/// 护卫 = 物理伤害重定向到相邻持 guard_attach 的保护者，每回合 ≤1 次，重定向后按保护者防御
/// （由管线以保护者为受害重算），只挡物理（O-22 可推翻）。零 Godot 引用。
/// </summary>
public sealed class ShieldGuard
{
    private readonly IBuffLedger _buffs;
    private bool _attackBlockedThisAction;
    private int _redirectsThisTurn;

    public ShieldGuard(IBuffLedger buffs)
    {
        _buffs = buffs ?? throw new System.ArgumentNullException(nameof(buffs));
    }

    /// <summary>动作开始：重置"本次攻击已挡"（AOE/多目标整攻击完全无效 #156）。</summary>
    public void OnActionStart() => _attackBlockedThisAction = false;

    /// <summary>回合开始：护卫重定向计数重置（每回合 ≤1，#159）。</summary>
    public void OnTurnStart() => _redirectsThisTurn = 0;

    /// <summary>
    /// 护盾拦截：物理攻击且目标有护盾且本动作未挡过 → 消耗 1 次并整攻击挡掉（完全无效）。
    /// 精神穿透不消耗（#156/#157）。
    /// </summary>
    public bool TryBlockShield(UnitRuntime target, string axis)
    {
        if (axis != "physical" || _attackBlockedThisAction || !_buffs.Has(target.Id, "shield"))
        {
            return false;
        }

        _buffs.ConsumeCharge(target.Id, "shield");
        _attackBlockedThisAction = true;
        return true;
    }

    /// <summary>
    /// 护卫重定向目标查询：受害者邻格（±1）持 guard_attach 的友方保护者，每回合 ≤1 次、只物理。
    /// 返回保护者 UnitId（无 → null）。
    /// </summary>
    public UnitId? FindProtector(UnitRuntime victim, FormationBoard teamBoard, int victimSlot, string axis)
    {
        if (axis != "physical" || _redirectsThisTurn >= 1)
        {
            return null;
        }

        foreach (UnitRuntime ally in teamBoard.UnitsInSlotOrder())
        {
            if (!_buffs.Has(ally.Id, "guard_attach") || ally.Id == victim.Id)
            {
                continue;
            }

            int? allySlot = teamBoard.UnitAtPosition(ally.Id);
            if (allySlot is { } s && (s == victimSlot - 1 || s == victimSlot + 1))
            {
                _redirectsThisTurn++;
                return ally.Id;
            }
        }

        return null;
    }
}