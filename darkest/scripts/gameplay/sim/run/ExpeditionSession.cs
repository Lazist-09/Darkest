using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;

namespace Darkest.Gameplay.Sim.Run;

// E1 的 PathStep / PathOption 定义在 ExpeditionPathPlanner.cs（选路归规划器；会话只持状态）

/// <summary>
/// M7 远征层会话（E0：#240 / O-66）——**由 `RunSession` 扩**：
/// N = 6 场战斗（`tuning.expedition.n_battles`）；每场后可选【扎营】；**6 场皆胜 → 完成**；
/// 任一环节中止（撤退 / 全灭）→ 提前回城（未完成）；**夜袭产生的战斗计入 6 场皆胜**。
/// 资源：柴火（扎营许可）/ 口粮（吃饭）——**不足必须拒绝且不扣**（P20 ②）。
/// 约束：`BattleDirector` **仍保持单场纯**（每场工厂新建 → 同 seed 可复现单场）。
/// </summary>
public interface IExpeditionSession : IRunSession
{
    /// <summary>本趟目标场数 N。</summary>
    int TargetBattles { get; }

    /// <summary>柴火（扎营许可）。</summary>
    int Firewood { get; }

    /// <summary>口粮（吃饭）。</summary>
    int Food { get; }

    /// <summary>夜袭次数（计入完成）。</summary>
    int AmbushCount { get; }

    /// <summary>资源支付：不足 → **拒绝且不扣**（返回 false）。</summary>
    bool TrySpend(CombatLog log, string kind, int amount, string reason);

    /// <summary>资源增加（事件节点）。</summary>
    void Gain(CombatLog log, string kind, int amount, string reason);

    /// <summary>夜袭判定（33%）：写 RngDraw；触发则本场为额外战斗。</summary>
    bool RollAmbush(CombatLog log, Core.Rng.IRngProvider rng);

    /// <summary>是否已完成（N 场皆胜）。</summary>
    bool IsExpeditionComplete { get; }
}

/// <summary>默认实现：跨场状态沿用 <see cref="RunSession"/>（HP/士气/虚弱/死门后遗症），另持资源与夜袭计数。</summary>
public sealed class ExpeditionSession : RunSession, IExpeditionSession
{
    private readonly double _ambushChance;

    public ExpeditionSession(Func<CombatLog, BattleDirector> buildDirector, int targetBattles, int firewood, int food,
        double ambushChance)
        : base(buildDirector, targetBattles)
    {
        TargetBattles = Math.Max(1, targetBattles);
        Firewood = firewood;
        Food = food;
        _ambushChance = Math.Clamp(ambushChance, 0.0, 1.0);
    }

    public int TargetBattles { get; }

    public int Firewood { get; private set; }

    public int Food { get; private set; }

    public int AmbushCount { get; private set; }

    public bool IsExpeditionComplete
    {
        get
        {
            RunOutcome o = Outcome(anyRetreat: false);
            return o.Curve.Count >= TargetBattles && o.Curve.All(s => s.Result == "PlayerVictory");
        }
    }

    public bool TrySpend(CombatLog log, string kind, int amount, string reason)
    {
        int current = kind == "firewood" ? Firewood : Food;
        if (amount <= 0 || current < amount)
        {
            // 🔴 P20 ②：支付不足必须**拒绝且不扣**
            log.Append(new ResourceChangedEvent(kind, 0, current, "rejected"));
            return false;
        }

        if (kind == "firewood")
        {
            Firewood -= amount;
        }
        else
        {
            Food -= amount;
        }

        log.Append(new ResourceChangedEvent(kind, -amount, kind == "firewood" ? Firewood : Food, reason));
        return true;
    }

    public void Gain(CombatLog log, string kind, int amount, string reason)
    {
        if (amount == 0)
        {
            return;
        }

        if (kind == "firewood")
        {
            Firewood += amount;
        }
        else
        {
            Food += amount;
        }

        log.Append(new ResourceChangedEvent(kind, amount, kind == "firewood" ? Firewood : Food, reason));
    }

    public bool RollAmbush(CombatLog log, Core.Rng.IRngProvider rng)    {
        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll)); // 确定性红线：夜袭判定必写
        bool triggered = roll < _ambushChance * 100.0;
        if (triggered)
        {
            AmbushCount++;
            log.Append(new AmbushTriggeredEvent(AmbushCount));
        }

        return triggered;
    }

    // ------------------------------------------------------------------
    // E4 · 事件节点结算（二选一强制；效果只改资源与士气，无战斗）
    // ------------------------------------------------------------------

    /// <summary>
    /// 结算事件节点：应用所选选项的资源/士气效果，并写 `EventNodeResolvedEvent`。
    /// **必须二选一**（节点选项数 ≠ 2 或越界 → 抛错，P20 ⑤）。
    /// </summary>
    public string ResolveEventNode(CombatLog log, Darkest.Data.ExpeditionNodeConfig node, int optionIndex)
    {
        if (node.Options is null || node.Options.Count != 2)
        {
            throw new InvalidOperationException($"事件节点 \"{node.Id}\" 必须恰 2 个选项（P20 ⑤ 二选一强制）。");
        }

        if (optionIndex is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(optionIndex), "事件节点必须二选一（不允许跳过，E4）。");
        }

        Darkest.Data.NodeOptionConfig opt = node.Options[optionIndex];
        if (opt.Effect.Resource is { } kind && opt.Effect.Delta != 0)
        {
            Gain(log, kind, opt.Effect.Delta, "event");
        }

        if (opt.Effect.Morale != 0)
        {
            foreach (string id in Retained.Keys.ToArray())
            {
                (int hp, int morale, bool weak) = Retained[id];
                if (hp <= 0)
                {
                    continue; // 阵亡者不受事件士气影响
                }

                Retained[id] = (hp, Math.Clamp(morale + opt.Effect.Morale, 0, 100), weak);
            }
        }

        string effect = $"resource:{opt.Effect.Resource ?? "-"}:{opt.Effect.Delta};morale:{opt.Effect.Morale}";
        log.Append(new EventNodeResolvedEvent(node.Id, opt.Choice, effect));
        return effect;
    }

    // ------------------------------------------------------------------
    // E6 · 回城结算（HP 全恢复；完成/全灭 → 士气回基准 50；**撤退不恢复**；清虚弱与死门后遗症；恢复满编）
    // ------------------------------------------------------------------

    /// <summary>
    /// 回城：HP 完全恢复（记 `int.MaxValue`，下场 `BeginBattle` 按 MaxHp 钳制）；
    /// **完成 / 全灭 → 士气回到基准 50**，**撤退 → 保留撤退结算后的士气（不二次扣、也不回 50）**；
    /// 清【虚弱】与【死门后遗症】（后者按 `until_next_recovery`）；阵容恢复满编（阵亡者归队，不做招募）。
    /// </summary>
    public int ReturnToTown(CombatLog log, string outcome)
    {
        bool retreat = outcome == "retreat";
        int moraleBefore = AverageRetainedMorale();
        int penaltyApplied = 0;

        foreach (string id in Retained.Keys.ToArray())
        {
            (int hp, int morale, bool weak) = Retained[id];
            bool alive = hp > 0;
            // 撤退：存活者保留撤退结算后的士气（惩罚已在撤退那一刻结算，此处不二次扣）
            int newMorale = retreat && alive ? morale : 50;
            Retained[id] = (int.MaxValue, newMorale, false); // HP 全恢复 + 清虚弱
            _ = weak;
            _ = penaltyApplied;
        }

        RetainedRecovery.Clear(); // 清【死门后遗症】（"到下次恢复"）
        int moraleAfter = AverageRetainedMorale();
        log.Append(new TownReturnEvent(outcome, moraleBefore, moraleAfter, PenaltyApplied: retreat));
        return moraleAfter;
    }

    private int AverageRetainedMorale()
        => Retained.Count == 0 ? 50 : (int)Math.Round(Retained.Values.Average(v => v.Morale));

    // ------------------------------------------------------------------
    // E3 · 扎营流程（最小版内核）：许可（柴火 1）→ 食物档位 → Respite 分配 → 结束
    // 说明：`next_battle` 类效果（磨刀/加固甲胄）由 camp_skills 声明为 **grant_buff**，
    //       其生效与到期**归 buff 台账**，本会话**不另存**"下一场加成"（单源）。
    // ------------------------------------------------------------------

    /// <summary>当前存活人数（口粮缩放 / Respite / 事件士气的作用面）。</summary>
    public int Survivors => Retained.Values.Count(v => v.Hp > 0);

    /// <summary>是否可扎营（**柴火 ≥ 1**；不足 → 灰显不可选）。</summary>
    public bool CanCamp => Firewood > 0;

    /// <summary>剩余 Respite 点数（扎营期间有效）。</summary>
    public int RespiteLeft { get; private set; }

    /// <summary>开始扎营：**扣 1 份柴火**（不足 → 拒绝且不扣）+ `CampStartedEvent`；
    /// Respite 池 = `respite_base + 存活人数`（满编 12）。</summary>
    public bool StartCamp(CombatLog log, int campIndex, int respiteBase)
    {
        if (!TrySpend(log, "firewood", 1, "camp"))
        {
            return false; // 无柴火 → 不可扎营（E3 验收）
        }

        RespiteLeft = ExpeditionCampMath.RespitePool(respiteBase, Survivors);
        log.Append(new CampStartedEvent(campIndex));
        return true;
    }

    /// <summary>
    /// 选择食物档位：口粮需求按**存活人数**缩放（`ExpeditionCampMath.FoodRequired`）；
    /// 口粮不足 → **退化为 starve（挨饿）且不扣**，并如实写入所选档位（E3 验收：Starve 有意义）。
    /// </summary>
    public string ChooseFood(CombatLog log, Darkest.Data.TuningCamp camp, string tier)
    {
        int need = ExpeditionCampMath.FoodRequired(camp.FoodTiers, tier, Survivors);
        string applied = tier;
        int spent = need;
        if (need > 0 && !TrySpend(log, "food", need, "camp_food"))
        {
            applied = "starve";
            spent = 0;
        }

        (double hpPct, int moraleDelta) = ExpeditionCampMath.FoodEffect(applied);
        ApplyToSurvivors(hpPct, moraleDelta);
        log.Append(new CampFoodChosenEvent(applied, spent, Survivors));
        return applied;
    }

    /// <summary>使用扎营技能：**点数不足 → 不可选（返回 false，不扣）**；成功写 `CampSkillUsedEvent`。</summary>
    public bool UseCampSkill(CombatLog log, string skillId, int cost, UnitId target)
    {
        if (cost < 1 || cost > RespiteLeft)
        {
            return false; // 点数不足 → 灰显（E3 验收）
        }

        RespiteLeft -= cost;
        log.Append(new CampSkillUsedEvent(skillId, target, RespiteLeft));
        return true;
    }

    /// <summary>结束扎营（夜袭判定由调用方接 `RollAmbush`；E5）。</summary>
    public void EndCamp(CombatLog log)
        => log.Append(new CampEndedEvent(0));

    /// <summary>把 HP%（按整编 MaxHp）与士气增量施加到**存活者**（阵亡者不参与）。</summary>
    private void ApplyToSurvivors(double hpPercent, int moraleDelta)
    {
        foreach (string id in Retained.Keys.ToArray())
        {
            (int hp, int morale, bool weak) = Retained[id];
            if (hp <= 0)
            {
                continue;
            }

            int max = RosterMaxHp.TryGetValue(id, out int m) && m > 0 ? m : Math.Max(1, hp);
            int delta = (int)Math.Round(max * hpPercent);
            int newHp = Math.Clamp(hp + delta, 1, max);
            Retained[id] = (newHp, Math.Clamp(morale + moraleDelta, 0, 100), weak);
        }
    }
}
