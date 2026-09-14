using System;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Math;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Pipeline;

/// <summary>
/// 命中步（T-M2-04）：命中率 = 100 − 目标闪避 + 技能命中修正，钳制 [55,100]（tuning hit_clamp）；
/// `roll &lt; 命中率` 算成功（阈值本身不命中）。零 Godot 引用。
/// </summary>
public static class HitStep
{
    public static bool Resolve(UnitRuntime attacker, UnitRuntime target, int hitMod,
        IRngProvider rng, CombatLog log, BalanceTable balance,
        Darkest.Core.Contracts.IBuffLedger? buffs = null)
    {
        int shown = BattleMath.HitRate(target.Base.Dodge, hitMod, balance.HitClampMin, balance.HitClampMax);
        // D1（#203）：连续未命中补偿 = max(0, 连续未命中−1) × N，**隐藏**（不改面板显示值）
        // 🔴 数字外置（P29）：N 来自 `tuning.consecutive_miss.hit_bonus_per_miss`（原硬编码 4）✓
        int hidden = Math.Max(0, attacker.ConsecutiveMisses - 1) * balance.ConsecutiveMissHitBonusPerMiss;
        // D4（#206）：死门后遗症 命中 −5（约定：effect=hit_mod 的 percent 视为"点"）
        int aftereffect = buffs?.PercentMod(attacker.Id, "hit_mod") ?? 0;
        // 🔴 M7.5 补欠账（`#302` (i1) 片②）：**命中率修正**（`light.effects.enemy_acc`）
        //    与 `hit_mod`（死门后遗症）**同层**：都是"单位状态"百分点 ⇒ 一起 clamp（不改 BattleMath 签名）
        int rate = Math.Clamp(shown + hidden + aftereffect + attacker.AccModPct, 0, 100);
        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll));
        bool hit = roll < rate;
        attacker.ConsecutiveMisses = hit ? 0 : attacker.ConsecutiveMisses + 1;
        log.Append(new HitEvent(hit, shown, attacker.Id, target.Id)); // 事件记显示值（补偿对玩家隐藏）
        return hit;
    }
}