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
    /// 回城（**#245 定案**）：**HP 完全恢复**（记 `int.MaxValue`，下场按 MaxHp 钳制）；
    /// 🔴 **士气完全不恢复**（完成/撤退/全灭**一律保留** —— 这是"跨趟累积"的唯一体现，也是玩家能感知的那一层）；
    /// 清【虚弱】与【死门后遗症】（后者按 `until_next_recovery`）；阵容恢复满编（不做招募）。
    /// </summary>
    public int ReturnToTown(CombatLog log, string outcome)
    {
        bool retreat = outcome == "retreat";
        int moraleBefore = AverageRetainedMorale();

        foreach (string id in Retained.Keys.ToArray())
        {
            (int hp, int morale, bool weak) = Retained[id];
            _ = weak;
            // 🔴 #245：士气**完全不恢复**（不再有"完成档回 50"）
            Retained[id] = (int.MaxValue, morale, false); // HP 全恢复 + 清虚弱
        }

        RetainedRecovery.Clear(); // 清【死门后遗症】（"到下次恢复"）
        int moraleAfter = AverageRetainedMorale();
        log.Append(new TownReturnEvent(outcome, moraleBefore, moraleAfter, PenaltyApplied: retreat));
        return moraleAfter;
    }

    /// <summary>
    /// **开始第 `battleIndex` 场（#252 / O-70 ①）**：先按会话流程建导演，再按 `tuning.expedition.difficulty_tiers`
    /// 的档位乘数**只放大敌 HP**（`target: "enemy_hp"`；单次伤害不变 → 保 A1 濒死带）。
    /// 🔴 增援单位在战斗内按基准生成，**不受乘数影响**（③ 不波及增援与 SP/资源）。
    /// </summary>
    public BattleDirector BeginExpeditionBattle(int battleIndex, CombatLog log,
        IReadOnlyList<Darkest.Data.TuningDifficultyTier>? tiers,
        Darkest.Data.TuningLightEffect? lightEffect = null)
    {
        BattleDirector director = BeginBattle(battleIndex, log);
        Darkest.Data.TuningDifficultyTier? tier = tiers?.FirstOrDefault(
            t => battleIndex >= t.BattleFrom && battleIndex <= t.BattleTo);
        if (tier is not null && tier.Target == "enemy_hp" && Math.Abs(tier.Multiplier - 1.0) > 1e-9)
        {
            foreach (UnitRuntime u in director.Enemy.UnitsInSlotOrder())
            {
                u.ApplyMaxHpMultiplier(tier.Multiplier);
            }
        }

        // 🔴 M7.5 补欠账（`#302` (i1)）：**光照档位的战斗效果必须真的接进战斗** ——
        //    此前 `light.effects` 的六个战斗字段**只有 UI 在读**（"有展示、无消费" = 欺骗玩家；红线 21 最坏形态）。
        //    本片先接**已有消费点**的那一环：`enemy_dmg_pct` → **敌方单位的伤害修正**
        //    （走 `UnitRuntime.DamageModPct`，`DamageStep` 已在消费 ⇒ **不新建通道**）。
        //    ⚠️ 其余五项（`enemy_acc` / `enemy_crit_pct` / `our_crit_pct` / `our_ambush_pct` / `our_morale_damage_pct`）
        //    **待各自通道接线**（红线 21：未接线不假装已生效）。
        if (lightEffect is not null && lightEffect.EnemyDmgPct != 0)
        {
            int pct = (int)Math.Round(lightEffect.EnemyDmgPct);
            foreach (UnitRuntime u in director.Enemy.UnitsInSlotOrder())
            {
                u.ApplyTraitDamagePct(pct); // 敌方输出修正（与特质/buff 同层）
            }
        }

        // 🔴 片②（`#303`）：**暴击率** —— 敌 `enemy_crit_pct` ／ 我 `our_crit_pct`（同一 `CritModPct` 层）
        if (lightEffect is not null && lightEffect.EnemyCritPct != 0)
        {
            int pct = (int)Math.Round(lightEffect.EnemyCritPct);
            foreach (UnitRuntime u in director.Enemy.UnitsInSlotOrder())
            {
                u.ApplyCritModPct(pct);
            }
        }

        if (lightEffect is not null && lightEffect.OurCritPct != 0)
        {
            int pct = (int)Math.Round(lightEffect.OurCritPct);
            foreach (UnitRuntime u in director.Player.UnitsInSlotOrder())
            {
                u.ApplyCritModPct(pct);
            }
        }

        // 🔴 片②：**命中率** —— 敌 `enemy_acc`（接在 `HitStep` 的命中判定层，与 `hit_mod` 同层）
        if (lightEffect is not null && lightEffect.EnemyAcc != 0)
        {
            int acc = (int)Math.Round(lightEffect.EnemyAcc);
            foreach (UnitRuntime u in director.Enemy.UnitsInSlotOrder())
            {
                u.ApplyAccModPct(acc);
            }
        }

        // #253 备用轴：**敌眩晕抗性 +Npp**（不改回合数、不改伤害 ⇒ 同时避开 A1 两个带；位移抗性不动）
        if (tier is not null && tier.StunResistPp != 0)
        {
            foreach (UnitRuntime u in director.Enemy.UnitsInSlotOrder())
            {
                u.ApplyStunResistBonus(tier.StunResistPp);
            }
        }

        return director;
    }

    /// <summary>UI 用（E2/E3 必显 ③）：6 人名册的 HP/MaxHp/士气（阵亡者 HP 0）。</summary>
    public IReadOnlyList<(string Id, int Hp, int MaxHp, int Morale)> Roster()
    {
        var list = new List<(string, int, int, int)>();
        foreach ((string id, (int hp, int morale, bool weak)) in Retained)
        {
            int max = RosterMaxHp.TryGetValue(id, out int m) && m > 0 ? m : Math.Max(1, hp);
            list.Add((id, hp > 0 ? Math.Min(hp, max) : 0, max, morale));
        }

        list.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        return list;
    }

    /// <summary>
    /// **跨趟携带**（#245 的必然结果）：上一趟结束（回城）后开新一趟时——
    /// **HP 完全恢复**（记 `int.MaxValue`，下场按 MaxHp 钳制）、**士气保留**、虚弱与死门后遗症清除、阵容满编。
    /// 这是"**跨趟累积**"的唯一载体；主循环（回城 → 再出发）必须调用它。
    /// </summary>
    public void CarryOverFrom(ExpeditionSession previous)
    {
        foreach ((string id, int hp, int max, int morale) in previous.Roster())
        {
            _ = hp;
            _ = max;
            Retained[id] = (int.MaxValue, morale, false); // 回城：HP 全满、士气不回（#245）
            RetainedRecovery.Remove(id);
        }
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

    /// <summary>该档位是否**可支付**（口粮足够）——不足时 UI **灰显 + tooltip「口粮不足」**（v0.86 规格）。</summary>
    public bool CanAffordFood(Darkest.Data.TuningCamp camp, string tier)
        => ExpeditionCampMath.FoodRequired(camp.FoodTiers, tier, Survivors) <= Food;

    /// <summary>
    /// 选择食物档位（v0.86 规格修订）：**口粮不足的档位不可选** —— 抛 `InvalidOperationException`，
    /// **不施加任何效果、不扣口粮**；🔴 **禁止"自动退化为 starve 且不扣"**（那是免费午餐，曾导致灵敏度非单调）。
    /// 【Starve】需**主动选择**：需求 0（永远可选），照常受罚（−20% HP / −15 士气），不是"免费躲罚"。
    /// </summary>
    public string ChooseFood(CombatLog log, Darkest.Data.TuningCamp camp, string tier)
    {
        int need = ExpeditionCampMath.FoodRequired(camp.FoodTiers, tier, Survivors);
        if (need > Food)
        {
            throw new InvalidOperationException(
                $"口粮不足：「{tier}」需要 {need} 份、现有 {Food} 份 → **该档位不可选（灰显）**；" +
                "不得自动退化为 starve（v0.86 / E3 规格）。");
        }

        if (need > 0)
        {
            TrySpend(log, "food", need, "camp_food");
        }

        (double hpPct, int moraleDelta) = ExpeditionCampMath.FoodEffect(tier);
        ApplyToSurvivors(hpPct, moraleDelta);
        log.Append(new CampFoodChosenEvent(tier, need, Survivors));
        return tier;
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
