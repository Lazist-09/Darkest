// 🔴 从 ExpeditionSession.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3）——只搬家、零行为改动 ✓
//    本文件 = 陷阱（ResolveTrapByResist / TrapCanApply / 陷阱 RNG）

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Morale;

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionSession
{
    public string ResolveTrapByResist(CombatLog log, TrapDefs defs, TrapDef trap, TrapGate gate,
        Func<string, int>? resistFor)
    {
        // 🔴 已消费的格 ⇒ **完全不碰**（连掷骰都不掷；`TrapResolver` 已短路，此处再兜一层）✓
        if (gate == TrapGate.Consumed)
        {
            return "consumed";
        }

        IRngProvider rng = _trapRng ?? throw new InvalidOperationException(
            "`ResolveTrap` 需要 `IRngProvider` ⇒ 本会话未注入（`BindTrapRng`）✓");

        // 🔴 踏中 / 拆除是**按人**判的（DD：每人的 Trap Resist 不同）⇒
        //    ⚠️ 但"踩中的是谁"要掷完才知道 —— 这里采用 DD 的**简化口径**：
        //    `Hidden`（未侦察）⇒ 全队无人可预知 ⇒ 用**队伍平均抗性**掷闪避；
        //    `Disarmable`（已侦察）⇒ 由**事先指定的拆弹者**掷（本刀无 UI 选人 ⇒ 取抗性**最高者**，
        //      DD 的实战选择就是让 Grave Robber 去拆 ⇒ "最高者"是该选择的**确定性等价**）✓
        //    🔴 两者都是**明确写下的口径**，不是"随便挑一个数" ✓
        string[] alive = Retained.Where(kv => kv.Value.Hp > 0)
            .Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (alive.Length == 0)
        {
            // 🔴 名册台账未建 / 全员阵亡 ⇒ **无人可结算** ⇒ 留痕（不静默；与 D-5 的 `hunger_no_roster_pending` 同款）⚠️
            log.Append(new EffectEvent(default, "trap_no_roster_pending", 100.0, true));
            return "triggered_no_roster";
        }

        int resist = gate == TrapGate.Disarmable
            ? alive.Max(id => resistFor?.Invoke(id) ?? 0)          // 拆弹者 = 抗性最高者（确定性）✓
            : (int)Math.Round(alive.Average(id => resistFor?.Invoke(id) ?? 0)); // 未侦察 ⇒ 队伍平均 ✓

        TrapOutcome outcome = TrapResolver.Resolve(log, rng, defs, trap, gate, resist);

        if (!outcome.Triggered)
        {
            // 拆除成功 ⇒ **完全无害** + 回压（DD：「renders it harmless and heals 8 stress」）✓
            if (outcome.Disarmed)
            {
                ApplyTeamMorale(log, defs.DisarmStressHeal, "trap_disarmed");
                log.Append(new EffectEvent(default, $"trap_disarmed:{trap.Id}", defs.DisarmStressHeal, true));
            }

            return outcome.Disarmed ? "disarmed" : "avoided";
        }

        // 踏中 ⇒ **随机一名**存活队员吃伤害（DD 原文：伤害打在 "**a random party member**" 身上）✓
        // 🔴 选人**必写 `RngDraw`**（纪律 V：展示值 == 消费值）——
        //    ⚠️ **单人也掷**：否则"队伍只剩 1 人"时随机流会与多人时**不同步**（回放/复现会漂移）✓
        int pick = rng.NextInt(0, alive.Length);
        log.Append(new RngDraw(rng.DrawCount, pick));
        string victimId = alive[pick];

        (int hp, int morale, bool weak) = Retained[victimId];
        int max = RosterMaxHp.TryGetValue(victimId, out int m) && m > 0 ? m : Math.Max(1, hp);
        int dmg = TrapResolver.DamageFor(trap, max);
        int newHp = Math.Max(0, hp - dmg);
        Retained[victimId] = (newHp, Math.Clamp(morale - defs.StressDamage, 0, 100), weak);

        log.Append(new EffectEvent(default, $"trap_triggered:{trap.Id}:{victimId}:{dmg}", dmg, true));
        return "triggered";
    }

    /// <summary>
    /// 🔴 `D-4`：**陷阱能否结算**（= 名册台账已建）。
    /// 与 <see cref="HungerCanApply"/> **同因同果**：`Retained` 只在**战后**落账 ⇒
    /// **首场战斗之前**踏中陷阱时无人可结算 ⇒ 返回 `false`（表现层**不该**播"某人受伤"的反馈）✓
    /// </summary>
    public bool TrapCanApply => Retained.Count > 0;

    /// <summary>🔴 `D-4`：本次会话的**陷阱掷骰源**（构造注入；与主流程共用同一条随机流 ⇒ 可复现）✓</summary>
    private IRngProvider? _trapRng;

    /// <summary>🔴 `D-4`：注入陷阱掷骰源（组合根在构造后立即调；不给 ⇒ `ResolveTrap` 抛错）✓</summary>
    public void BindTrapRng(IRngProvider rng) => _trapRng = rng ?? throw new ArgumentNullException(nameof(rng));

    /// <summary>
    /// 🔴 `D-7`：本趟**累计**"被折磨拒绝进食"的次数（供冒烟/日志自证 —— 与 `ExpeditionFlow.ActOutCurioRefuseCount` 成对）✓
    /// </summary>
}
