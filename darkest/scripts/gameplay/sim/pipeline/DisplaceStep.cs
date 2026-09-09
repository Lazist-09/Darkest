using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>
/// 位移步（T-M2-07）：过抗性 = roll &gt;= 目标位移抗性（唯一 &gt;= 例外，combat_math §3）→
/// IFormation.TrySwapChain（永不产生空位）；失败 → 位移不生效、伤害与其它效果照常（本步与
/// 05/06 解耦）；位移不产生伤害 → 不触发死门（#117），本步不触碰 HP/士气/死门。零 Godot 引用。
/// </summary>
public static class DisplaceStep
{
    public static DisplaceResult Resolve(UnitRuntime displaced, FormationBoard board,
        int fromPos, int toPos, int distance, IRngProvider rng, CombatLog log)
    {
        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll));
        bool passedResist = roll >= displaced.Base.DisplaceResist; // §3 / data_schema §2.3

        DisplaceResult chain = DisplaceResult.Fail(DisplaceFailureReason.None);
        if (passedResist)
        {
            chain = board.TrySwapChain(displaced.Id, fromPos, toPos, distance);
        }

        log.Append(new DisplaceEvent(displaced.Id, fromPos, toPos, passedResist, chain.Success, chain.Failure.ToString()));
        return chain;
    }
}