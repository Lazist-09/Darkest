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
/// 任一环节中止（**放弃远征 / 全灭**）→ 提前回城（未完成）—— 🔴 **撤退不在其中**（`#352`：撤退是【一场】的选择）；**夜袭产生的战斗计入 6 场皆胜**。
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
        // 🔴 M7.5 补欠账（`#305`③ / 红线 21）：**守夜 ／ 站岗（`ambush_immunity_once`）的"免下次夜袭"在此消费** ——
        //    此前该 effect **全仓无消费点** ⇒ 玩家花 3 点买了一无所获。免疫**一次性**（消费后清除）。
        if (AmbushImmune)
        {
            AmbushImmune = false;
            log.Append(new RngDraw(rng.DrawCount, -1)); // 🔴 审计：本次【未掷骰】（免疫生效），用 -1 标明
            return false;
        }

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
    /// 全队士气变化（Curio/事件共用）：只影响**存活**单位；钳制到 [0,100]。
    /// </summary>
    public void ApplyTeamMorale(CombatLog log, int delta, string reason)
    {
        if (delta == 0)
        {
            return;
        }

        foreach (string id in Retained.Keys.ToArray())
        {
            (int hp, int morale, bool weak) = Retained[id];
            if (hp <= 0)
            {
                continue; // 阵亡者不受影响
            }

            Retained[id] = (hp, Math.Clamp(morale + delta, 0, 100), weak);
        }

        log.Append(new EffectEvent(default, $"team_morale:{reason}:{delta}", 100.0, true));
    }

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
        EnterPhase(FlowPhase.Battle); // 🔴 进战斗 ⇒ Battle 相位（此后扎营/选路/Curio 一律不可用）✓
        BattleDirector director = BeginBattle(battleIndex, log);
        director.BattleIndex = battleIndex; // 🔴 `#376`②：把"本趟第几场"注入导演（它仍是单场纯，只多一个只读字段）✓
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

        // 🔴 片③(a)：**我方士气伤害** —— `our_morale_damage_pct` 接进 `MoraleLedger`
        //    （只对"受击派生"的士气损失生效；事件/流程的士气变化不受光照影响）
        if (lightEffect is not null && lightEffect.OurMoraleDamagePct != 0)
        {
            director.Morale.OurMoraleDamagePct = (int)Math.Round(lightEffect.OurMoraleDamagePct);
        }

        // #253 备用轴：**敌眩晕抗性 +Npp**（不改回合数、不改伤害 ⇒ 同时避开 A1 两个带；位移抗性不动）
        if (tier is not null && tier.StunResistPp != 0)
        {
            foreach (UnitRuntime u in director.Enemy.UnitsInSlotOrder())
            {
                u.ApplyStunResistBonus(tier.StunResistPp);
            }
        }

        // 🔴 `O-83`（**承上**）：把【上一场结束时保留的 HP】应用到本场开局 —— 契约 `blueprint §9`
        //    「战后【不自动恢复】：**HP 与士气跨战斗完全保留**；场间无恢复」；
        //    而我此前只把 HP 记进 `Retained`、**从未用于下一场** ⇒ 每场都满血开局 = 红线 21 家族缺口 ⚠️
        //    ⇒ 顺序：**先承上（结转血量）再加成（营地 +HP% 在结转值之上加）** ✓
        ApplyRetainedHpToBattle(director, log);

        // 🔴 跨场 buff（`next_battle` 类）：**每场开场注入** —— 磨刀/加固甲胄 挂在上一趟扎营的目标身上 ⇒ 本场生效 ✓
        //    （注入后由 `ExpeditionFlow.OnBattleFinished` → `ConsumeRunBuffsAfterBattle` 消耗 ⇒ 只生效一场）
        InjectRunBuffs(director, log);

        // 🔴 本趟营地加成（士气/HP）：**战斗开场施加**（`until_run_end` 台账；战后由流程扣回，防漏进名册）
        ApplyCampBonusesToBattle(director);

        // 🔴 Curio 圣坛祝福（到扎营）：**每场开场挂到全队**（`damage_buff` 的消费点在 `DamageStep` 的 raw）
        if (_curioDamageBlessingPct > 0)
        {
            string buffId = $"curio_altar_blessing_{_curioDamageBlessingPct}";
            foreach (UnitRuntime u in director.Player.UnitsInSlotOrder())
            {
                director.Buffs.Add(u.Id, buffId, source: null);
            }

            log.Append(new EffectEvent(default, $"curio_blessing_injected:{_curioDamageBlessingPct}", 100.0, true));
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
        => Retained.Count == 0 ? MoraleStart : (int)Math.Round(Retained.Values.Average(v => v.Morale));

    // ------------------------------------------------------------------
    // E3 · 扎营流程（最小版内核）：许可（柴火 1）→ 食物档位 → Respite 分配 → 结束
    // 说明：`next_battle` 类效果（磨刀/加固甲胄）由 camp_skills 声明为 **grant_buff**，
    //       其生效与到期**归 buff 台账**，本会话**不另存**"下一场加成"（单源）。
    // ------------------------------------------------------------------

    /// <summary>当前存活人数（口粮缩放 / Respite / 事件士气的作用面）。</summary>
    public int Survivors => Retained.Values.Count(v => v.Hp > 0);

    /// <summary>是否可扎营（**柴火 ≥ 1**；不足 → 灰显不可选）。</summary>

    /// <summary>
    /// 能否扎营：**资源条件 + 相位条件**（原先只看 `Firewood` ⇒ 那等于"内核允许战斗中扎营" ⚠️）
    /// ⇒ 只允许在【走图】或【已在扎营】时扎营 ✓
    /// </summary>
    public bool CanCamp => Firewood > 0 && Phase is FlowPhase.Walking or FlowPhase.Camp;

    // ---- 🔴 派生谓词（**给表现层读的**；实现 = 读相位 + 各自资源条件）--------------------------------
    /// <summary>可显示扎营面板（走图中且柴火 > 0，或已在扎营中）✓</summary>
    public bool CanShowCampUi => CanCamp;

    /// <summary>可显示【选路】面板（只有走图时才有"下一步选哪"）✓</summary>
    public bool CanShowPathChoice => Phase is FlowPhase.Walking;

    /// <summary>可显示【Curio】面板（Curio 是走图途中遇到的）✓</summary>
    public bool CanShowCurioUi => Phase is FlowPhase.Walking;

    /// <summary>剩余 Respite 点数（扎营期间有效）。</summary>
    public int RespiteLeft { get; private set; }

    /// <summary>开始扎营：**扣 1 份柴火**（不足 → 拒绝且不扣）+ `CampStartedEvent`；
    /// Respite 池 = `respite_base + 存活人数`（满编 12）。</summary>
    public bool StartCamp(CombatLog log, int campIndex, int respiteBase)
    {
        if (!CanCamp)
        {
            return false; // 🔴 相位条件：战斗中/已结算时**不许**扎营（原先只看柴火 ⇒ 等于内核允许战斗中扎营）⚠️
        }

        EnterPhase(FlowPhase.Camp);

        if (!TrySpend(log, "firewood", 1, "camp"))
        {
            EnterPhase(FlowPhase.Walking); // 扣不到柴火 ⇒ 相位**不留下**（避免"扎营了但没扣"这种半态）✓
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

        (double hpPct, int moraleDelta) = ExpeditionCampMath.FoodEffect(camp.FoodEffects, tier); // 🔴 效果来自 data ✓
        ApplyToSurvivors(hpPct, moraleDelta);
        log.Append(new CampFoodChosenEvent(tier, need, Survivors));
        return tier;
    }

    /// <summary>使用扎营技能：**点数不足 → 不可选（返回 false，不扣）**；成功写 `CampSkillUsedEvent`。</summary>
    public bool UseCampSkill(CombatLog log, Darkest.Data.CampSkillConfig skill, UnitId target,
        Darkest.Data.TuningCamp camp)
    {
        // 🔴 数字外置（P29 / 消掉"两处真值"）：数字一律来自 data ⇒ 调用方不必记得传 ✓
        string skillId = skill.Id;
        int cost = skill.Cost;
        string effect = skill.Effect;
        if (cost < 1 || cost > RespiteLeft)
        {
            return false; // 点数不足 → 灰显（E3 验收）
        }

        RespiteLeft -= cost;
        log.Append(new CampSkillUsedEvent(skillId, target, RespiteLeft));

        // 🔴 M7.5 补欠账（`#305`③ / 红线 21）：**扎营技能的【效果】以前只扣点数、不生效** ——
        //    `camp_skills.json` 的 `effect` 字段（如 `ambush_immunity_once` = 轮流守夜 ／ 站岗）
        //    **此前全仓没有消费点** ⇒ 玩家花 3 点买了一无所获（红线 21 最坏形态：**误导玩家**）。
        //    本片先接**语义已存在**的那一项：`ambush_immunity_once` ⇒ **免疫【下一次】夜袭**。
        if (effect == "ambush_immunity_once")
        {
            AmbushImmune = true;
        }

        // 🔴 `m7_expedition.md:160`（三类型之二）：**`next_battle` 类 buff** ——
        //    磨刀（`next_battle_sharpen` 伤害 +25%）／加固甲胄（`next_battle_armor` 物防 +4）
        //    ⇒ 挂成【跨场 buff，剩余 1 场】：下一场开场注入、该场结束即消耗 ✓
        //    （两个 buff 的消费点 `dealt_damage_mult` ／ `phys_def` 早在 `ConsumedEffectNames` 里 ✓）
        if (effect == "grant_buff:next_battle_sharpen")
        {
            GrantRunBuff(target, "next_battle_sharpen", remainingBattles: 1);
        }

        if (effect == "grant_buff:next_battle_armor")
        {
            GrantRunBuff(target, "next_battle_armor", remainingBattles: 1);
        }

        // 🔴 `m7_expedition.md:160`（三类型之二 `battles:N`）：**打气**（士气伤害 −15%）——
        //    契约：**跨场 4 场**、**扎营【不清】它**（`ConsumeRunBuffsAfterBattle` 每场 −1，扎营不碰 ✓）
        if (effect == "morale_damage_minus_15_for_4_battles")
        {
            GrantRunBuff(target, "pep_talk", remainingBattles: camp.PepTalkBattles);
        }

        // 🔴 `#310` ②/①（**本趟台账 `until_run_end`**）：士气类与 HP 类
        if (effect == "morale_plus_8")
        {
            GrantCampMorale(target, effectNumberOrThrow(skill.EffectNumber, "morale_plus_8"));            // 笑谈（单体 +8）
        }

        if (effect == "morale_plus_5_team")
        {
            GrantCampMoraleTeam(effectNumberOrThrow(skill.EffectNumber, "morale_plus_5_team"));                // 埋锅造饭（全队 +5）
        }

        if (effect == "morale_plus_8_team")
        {
            GrantCampMoraleTeam(effectNumberOrThrow(skill.EffectNumber, "morale_plus_8_team"));                // 动员（全队 +8）
        }

        if (effect == "heal_15_percent_and_clear_bleed")
        {
            // ⚠️ 「清流血」部分：流血是**战斗内**状态 ⇒ 营地"清"没有落点（契约 `#310`：归【冗余·阶段二】）
            GrantCampHpPercent(target, effectNumberOrThrow(skill.EffectNumber, "heal_15_percent_and_clear_bleed"));        // 包扎：HP +15% 部分按裁定落地 ✓
        }

        if (effect == "heal_5_percent")
        {
            GrantCampHpPercent(target, effectNumberOrThrow(skill.EffectNumber, "heal_5_percent"));         // 照料
        }

        return true;
    }

    /// <summary>是否持有"免下一次夜袭"（由扎营技能 `ambush_immunity_once` 授予；**在 `RollAmbush` 里消费**）。</summary>
    /// <summary>🆕 空台账默认士气（`tuning.morale.start`，`init` 注入；未注入 ⇒ 契约常量 `RookieMorale`）✓</summary>
    public int MoraleStart { get; init; } = Darkest.Data.RosterConfig.RookieMorale;

    /// <summary>🆕 效果数字取值（**data 驱动**；缺失 ⇒ 立刻报错，**不静默取默认值**）✓</summary>
    private static int effectNumberOrThrow(int? value, string effect) => value
        ?? throw new InvalidOperationException(
            $"camp_skills.json: 效果 \"{effect}\" 需要 effect_number（P29 数字外置）—— 缺失即报错，不静默取默认值。");

    public bool AmbushImmune { get; private set; }

    // ------------------------------------------------------------------
    // 🔴 跨场 buff（`m7_expedition.md:160` 的三类型之二：`next_battle`）
    //    契约：`next_battle`（磨刀/加固甲胄）= **仅下一场**；`battles:N` = 跨场计数（每场 −1，扎营不清）
    //    落点：**每场开场把未过期的 buff 注入该场台账**（不是新状态层；台账仍是 buff 的唯一真相）
    // ------------------------------------------------------------------

    private readonly List<(int Slot, string BuffId, int RemainingBattles)> _runBuffs = new();
    private readonly Dictionary<string, int> _heroSlots = new(StringComparer.Ordinal);

    /// <summary>
    /// 🔴 **绑定出征编成**（`英雄 id → 阵型槽位`）—— 由组合根在建会话时调用。
    /// 原因（实测）：**营地侧用英雄 id**（`hero_warrior_1`），**战斗侧玩家单位用原型 id**（`warrior` ／ `warrior_2` …）
    /// ⇒ 两套 id 体系 ⇒ 跨场 buff 必须**按槽位**挂载，注入时用 `Player.UnitRuntimeAt(slot)` 换成本场单位 id ✓
    /// （槽位来自 `FormationConfig` 的 `initial_roster.player[].slot`，与 `FormationSortie` 的顺序一致）
    /// </summary>
    public void BindSortie(IReadOnlyDictionary<string, int> heroSlotById)
    {
        _heroSlots.Clear();
        foreach ((string hero, int slot) in heroSlotById)
        {
            _heroSlots[hero] = slot;
        }
    }

    /// <summary>本趟挂着的跨场 buff（供测试/日志：`(槽位, buffId, 剩余场数)`）。</summary>
    public IReadOnlyList<(int Slot, string BuffId, int RemainingBattles)> RunBuffs => _runBuffs;

    /// <summary>给某英雄挂一个【跨场】buff（`next_battle` ⇒ `remainingBattles: 1`）；按槽位记账。</summary>
    public void GrantRunBuff(UnitId unit, string buffId, int remainingBattles)
    {
        if (remainingBattles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingBattles), "跨场 buff 的剩余场数必须 ≥ 1。");
        }

        int slot = _heroSlots.TryGetValue(unit.Value, out int s) ? s : 0;
        _runBuffs.RemoveAll(b => b.Slot == slot && b.BuffId == buffId); // refresh 语义
        _runBuffs.Add((slot, buffId, remainingBattles));
    }

    /// <summary>
    /// 🔴 **把跨场 buff 注入本场**（每场开场调用；只对"本场在场的单位"注入）。
    /// `next_battle` 类的注入后**在本场结束时消耗**（见 <see cref="ConsumeRunBuffsAfterBattle"/>）。
    /// </summary>
    public void InjectRunBuffs(BattleDirector director, CombatLog log)
    {
        if (_runBuffs.Count == 0)
        {
            return;
        }

        foreach ((int slot, string buffId, int _) in _runBuffs.ToArray())
        {
            UnitRuntime? u = slot > 0 ? director.Player.UnitRuntimeAt(slot) : null;
            if (u is null)
            {
                // 🔴 **已知阻塞（不静默）**：未能把"英雄"映射到本场单位（槽位未绑定 / 该位无人）。
                UnmappedRunBuffs++;
                log.Append(new EffectEvent(default, $"run_buff_unmapped:{buffId}", 0.0, Triggered: false));
                continue;
            }

            director.Buffs.Add(u.Id, buffId, source: null);
            log.Append(new EffectEvent(u.Id, $"run_buff:{buffId}", 100.0, true));
        }
    }

    /// <summary>未能注入的跨场 buff 次数（**供测试/日志断言**：`> 0` 说明 hero→战斗单位映射仍缺）。</summary>
    public int UnmappedRunBuffs { get; private set; }

    // ------------------------------------------------------------------
    // 🔴 Curio 圣坛（`curio.md` §3 #5）：**本趟 +N% 伤害，到扎营**
    //    · 跨场：每场开场挂 `curio_altar_blessing_{pct}` 到**全队**
    //    · 到期：**扎营清**（= `until_next_recovery` 的语义 ✓ 与死门后遗症同一个"下次恢复"）
    //    · 叠加：**取大（refresh）** —— 契约设计检查③："不同道具给不同等级的好结果"（空手 +20 ／ 支援包 +30）
    // ------------------------------------------------------------------

    private int _curioDamageBlessingPct;

    /// <summary>本趟的 Curio 伤害祝福（0 = 无）。</summary>
    public int CurioDamageBlessingPct => _curioDamageBlessingPct;

    /// <summary>授予 Curio 伤害祝福（**取大**；不叠加成 +50）。</summary>
    public void GrantCurioDamageBlessing(int percent)
    {
        if (percent > _curioDamageBlessingPct)
        {
            _curioDamageBlessingPct = percent;
        }
    }

    // ------------------------------------------------------------------
    // 🔴 扎营技能的第二批（`#310` ②/① 裁定：**写【本趟台账】`until_run_end`，不写回名册**）：
    //    · 士气类：笑谈 +8（单体）／埋锅造饭 全队 +5／动员 全队 +8  ⇒ **下一场起手士气 +N**
    //    · HP 类：包扎 +15% ／照料 +5%                              ⇒ **本趟剩余场次的开局 HP +N%**
    //    🔴 关键纪律：**这两个台账不得漏进名册** —— 战斗开场施加后，在 `EndBattle` 之后要**扣回**
    //       （否则"营地加士气"会变成【免费减压】，与 M8.0 的减压冲突 —— 契约明文禁止）
    // ------------------------------------------------------------------

    private readonly Dictionary<string, int> _campMoraleBonus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _campHpBonusPct = new(StringComparer.Ordinal);

    /// <summary>本趟的营地士气加成（英雄 → +N；供测试/日志）。</summary>
    public IReadOnlyDictionary<string, int> CampMoraleBonus => _campMoraleBonus;

    /// <summary>本趟的营地 HP 加成（英雄 → +N% MaxHp；供测试/日志）。</summary>
    public IReadOnlyDictionary<string, int> CampHpBonusPct => _campHpBonusPct;

    /// <summary>单体 +N 士气（**本趟台账**；如「笑谈」）。</summary>
    public void GrantCampMorale(UnitId hero, int delta)
    {
        _campMoraleBonus[hero.Value] = _campMoraleBonus.GetValueOrDefault(hero.Value) + delta;
    }

    /// <summary>全队 +N 士气（**本趟台账**；如「埋锅造饭」「动员」）。</summary>
    public void GrantCampMoraleTeam(int delta)
    {
        foreach (Darkest.Data.HeroConfig h in _allHeroesForCamp)
        {
            GrantCampMorale(new UnitId(h.Id), delta);
        }
    }

    /// <summary>单体开局 HP +N%（**本趟台账**；如「包扎 +15%」「照料 +5%」）。</summary>
    public void GrantCampHpPercent(UnitId hero, int percent)
    {
        _campHpBonusPct[hero.Value] = Math.Max(_campHpBonusPct.GetValueOrDefault(hero.Value), percent);
    }

    /// <summary>营地技能的作用对象全集（名册英雄；由组合根注入 —— 名字册与战斗 id 是两套体系）。</summary>
    private IReadOnlyList<Darkest.Data.HeroConfig> _allHeroesForCamp = Array.Empty<Darkest.Data.HeroConfig>();

    /// <summary>注入名册英雄（供"全队"类营地技能枚举目标）。</summary>
    public void BindCampHeroes(IReadOnlyList<Darkest.Data.HeroConfig> heroes) => _allHeroesForCamp = heroes;

    /// <summary>
    /// 把本趟营地加成施加到本场（**战斗开场**调用；加成不进 `Retained` 的持久值）。
    /// 🔴 **必须按【槽位】映射**：本台账按**英雄 id** 记账，而战斗单位的 `Id` 是**原型 id**（两套体系）
    /// —— 我第一版直接比 `u.Id.Value` ⇒ **永不命中、加成静默失效** ⚠️（用例当场抓到）
    /// </summary>
    public void ApplyCampBonusesToBattle(BattleDirector director)
    {
        if (_campMoraleBonus.Count == 0 && _campHpBonusPct.Count == 0)
        {
            return;
        }

        foreach ((string heroId, int morale) in _campMoraleBonus)
        {
            UnitRuntime? u = UnitAtHeroSlot(director, heroId);
            if (u is not null && morale != 0)
            {
                u.Morale = Math.Clamp(u.Morale + morale, 0, 100);
                // 🔴 `Retained` 的键是**战斗单位 id**（不是英雄 id）⇒ 记下来供战后【扣回】用
                _heroBattleIds[heroId] = u.Id.Value;
                log_note($"[camp] 本趟士气加成 +{morale} ⇒ 英雄 {heroId}（槽位 {_heroSlots[heroId]}）");
            }
        }

        foreach ((string heroId, int pct) in _campHpBonusPct)
        {
            UnitRuntime? u = UnitAtHeroSlot(director, heroId);
            if (u is not null && pct > 0)
            {
                int bonus = Math.Max(1, (int)Math.Round(u.MaxHp * (pct / 100.0)));
                u.CurrentHp = Math.Min(u.MaxHp, u.CurrentHp + bonus);
                log_note($"[camp] 本趟开局 HP +{pct}% ⇒ 英雄 {heroId}（+{bonus}）");
            }
        }
    }

    /// <summary>
    /// 🔴 **`O-83`：把上一场保留的 HP 应用到本场开局**（"承上"）。
    ///
    /// 契约（`blueprint` 明文）：「战后【不自动恢复】：**HP 与士气跨战斗完全保留**；场间无恢复（切片无扎营/回城/战后回血）」。
    /// ⚠️ 我此前只把 HP 记进 `Retained`、**从未用于下一场** ⇒ **每场都满血开局** = 红线 21 家族缺口（架构在 `blueprint` 里也标了这个问号）。
    ///
    /// 🔴 **必须按【槽位】映射**（两套 id 体系的唯一正确接法，照 `ApplyCampBonusesToBattle`）：
    /// `Retained` 的键是**上一场的战斗单位 id**（原型 id 体系），英雄用英雄 id 记账 ⇒
    /// 先经 `_heroSlots` 找到本场单位，再用 `_heroBattleIds`（此刻还是**上一场的** id）查 `Retained`。
    /// 因此本方法**必须在 `ApplyCampBonusesToBattle` 之前调用**（后者会覆写 `_heroBattleIds`）。
    ///
    /// ⚠️ 结转值为 0（阵亡却仍被编入）⇒ **不静默补满**：照实写 0 并留痕 `retained_hp_zero_but_deployed`（那是流程 bug 的信号）✓
    /// </summary>
    public void ApplyRetainedHpToBattle(BattleDirector director, CombatLog log)
    {
        foreach ((string heroId, int slot) in _heroSlots)
        {
            UnitRuntime? u = UnitAtHeroSlot(director, heroId);
            if (u is null)
            {
                continue;
            }

            // 上一场的战斗单位 id（本方法在 `ApplyCampBonusesToBattle` 之前 ⇒ 此刻仍是上一场的）✓
            // 🔴🔴 **两种键空间都要认**（`O-83` 真缺陷，靠"绑了阵型读数仍逐字不变"追出来的）：
            //    · **场景路径**：`CaptureBattleEndHp`（我加的）按【战斗单位 id】写 `Retained` ⇒ 这里用 `prevBattleId` 命中 ✓
            //    · **内核路径**（探针 / `RunSession.EndBattle`）：按【`_roster` 里的 id】写 `Retained`
            //      ⇒ 用战斗单位 id 查**必然落空** ⇒ 于是"承上"在这条路上**静默跳过**（连留痕都没有）⚠️
            //    ⇒ 只认一种键 = 一半路径永远不生效（两套 id 体系的老陷阱的又一形态）；这里显式认两种 ✓
            (int Hp, int Morale, bool Weak) prev = default;
            bool found = _heroBattleIds.TryGetValue(heroId, out string? prevBattleId)
                         && Retained.TryGetValue(prevBattleId, out prev);
            if (!found)
            {
                found = Retained.TryGetValue(heroId, out prev);
            }
            if (found)
            {
                // 🔴 **开局 HP = 结转值**（无条件设置 + **必留痕**）——
                //    我第一版写成"只在 carried < u.CurrentHp 时才压低"，那是**静默分支**：
                //    实测（驱动复用棋盘 ⇒ 新单位起始值恰好等于结转值）⇒ 条件不成立 ⇒ **既没设值也没留痕**
                //    ⇒ 读数和用例都抓不到（红线 21：不许有"看起来接了、其实什么都没发生"的分支）⚠️
                int carried = Math.Min(prev.Hp, u.MaxHp); // `int.MaxValue` = 全满（开局/恢复后的哨兵值）✓
                u.CurrentHp = Math.Clamp(carried, 0, u.MaxHp);
                log.Append(new EffectEvent(u.Id, $"retained_hp_carried:{u.CurrentHp}/{u.MaxHp}", u.CurrentHp, true));
                if (u.CurrentHp == 0)
                {
                    log.Append(new EffectEvent(u.Id, "retained_hp_zero_but_deployed", 0.0, true));
                }
            }

            _heroBattleIds[heroId] = u.Id.Value; // 记下【本场】id，供战后扣回/下一场查找 ✓
        }
    }

    /// <summary>
    /// 🔴 **`O-83` 的前一半：把【本场结束时的血量】落进跨场台账**（战后、切场景之前调用）。
    ///
    /// ⚠️ 我自查发现的**惰性风险**：`Retained` 原先只被"士气/恢复/回城/扎营"写过，**没有任何一处写战后血量**
    /// ⇒ 若只加"承上"（`ApplyRetainedHpToBattle`）而不加本方法，它读到的**永远是满血哨兵值** ⇒ **改动是惰性的**
    /// （红线 25「动作 ≠ 意义」：看起来接了、其实什么都没发生）⚠️
    ///
    /// 🔴 键 = **本场战斗单位 id**（`u.Id.Value`，与 `_heroBattleIds` 同口径）⇒ 下一场开局才查得到 ✓
    /// ⚠️ 只动 **HP**；士气/虚弱沿用台账现值（本方法**不越权**改它们）✓
    /// </summary>
    public void CaptureBattleEndHp(BattleDirector director, CombatLog log)
    {
        int captured = 0;
        foreach (UnitRuntime u in director.Player.UnitsInSlotOrder())
        {
            string id = u.Id.Value;
            int morale = Retained.TryGetValue(id, out (int Hp, int Morale, bool Weak) cur) ? cur.Morale : MoraleStart; // 🆕 数字外置：缺台账 ⇒ 用 `MoraleStart`（= `tuning.morale.start`，未注入退回契约常量 `RookieMorale`）✓
            bool weak = Retained.TryGetValue(id, out (int Hp, int Morale, bool Weak) cur2) && cur2.Weak;
            int hp = Math.Clamp(u.CurrentHp, 0, u.MaxHp);
            if (Retained.TryGetValue(id, out (int Hp, int Morale, bool Weak) before) && before.Hp == hp)
            {
                continue; // 无变化 ⇒ 不留痕（避免事件流噪声）
            }

            Retained[id] = (hp, morale, weak);
            captured++;
            log.Append(new EffectEvent(u.Id, $"battle_end_hp_captured:{hp}/{u.MaxHp}", hp, true));
        }

        if (captured > 0)
        {
            log.Append(new EffectEvent(default, $"battle_end_hp_captured_count:{captured}", captured, true));
        }
    }

    /// <summary>英雄 id → 本场单位（**按槽位**；两套 id 体系的唯一正确接法）。</summary>
    private UnitRuntime? UnitAtHeroSlot(BattleDirector director, string heroId)
        => _heroSlots.TryGetValue(heroId, out int slot) && slot > 0
            ? director.Player.UnitRuntimeAt(slot)
            : null;

    private void log_note(string message)
    {
        // 内核层不碰 Godot；这里只把说明写进事件流（可审计）
        _campNotes.Add(message);
    }

    /// <summary>本趟营地加成的施加记录（供测试/审计：证明"真的施加了"，不是只记账）。</summary>
    private readonly List<string> _campNotes = new();

    /// <summary>本趟营地加成的施加记录（供测试/审计）。</summary>
    public IReadOnlyList<string> CampNotes => _campNotes;

    /// <summary>
    /// 🔴 **把营地加成从跨场台账里扣回**（每场 `EndBattle` **之后**调用）——
    /// 否则营地士气会经 `Retained` 漏进名册 ⇒ 变成【免费减压】（契约 `#310` ② 明文禁止）⚠️
    /// </summary>
    public void StripCampBonusesFromRetained()
    {
        foreach ((string heroId, int back) in _campMoraleBonus)
        {
            // 🔴 必须用【战斗单位 id】查 `Retained`（两套 id 体系；我第一版用英雄 id ⇒ 查不到 ⇒ 扣回静默失效）
            if (!_heroBattleIds.TryGetValue(heroId, out string? battleId))
            {
                continue;
            }

            if (!Retained.TryGetValue(battleId, out (int Hp, int Morale, bool Weak) cur))
            {
                continue;
            }

            Retained[battleId] = (cur.Hp, Math.Clamp(cur.Morale - back, 0, 100), cur.Weak);
        }
    }

    /// <summary>英雄 id → **本场战斗单位 id**（在 `ApplyCampBonusesToBattle` 时记录；供战后扣回）。</summary>
    private readonly Dictionary<string, string> _heroBattleIds = new(StringComparer.Ordinal);
    /// <summary>一场结束后：跨场 buff 的剩余场数 −1（到 0 清除）。`next_battle` ⇒ 1 ⇒ 紧接着就被清 ✓</summary>
    public void ConsumeRunBuffsAfterBattle()
    {
        for (int i = _runBuffs.Count - 1; i >= 0; i--)
        {
            (int slot, string buffId, int left) = _runBuffs[i];
            if (left <= 1)
            {
                _runBuffs.RemoveAt(i); // 用完即清（`next_battle` 的语义）
            }
            else
            {
                _runBuffs[i] = (slot, buffId, left - 1);
            }
        }
    }

    /// <summary>结束扎营（夜袭判定由调用方接 `RollAmbush`；E5）。</summary>
    public void EndCamp(CombatLog log)
    {
        EnterPhase(FlowPhase.Walking); // 🔴 收营 ⇒ 回【走图】相位（内核收口 ⇒ 不依赖调用方记得）✓
        // 🔴 补欠账（契约 `m7_expedition.md:160` ① / `O-67`）：
        //    **`until_next_recovery` = 到下次恢复（**扎营**/回城）** ⇒ **扎营必须清【死门后遗症】**。
        //    实测此前：只有**回城**清（`ReturnToTown`）⇒ 扎营后下一场**仍带着后遗症** ⚠️（红线 21 家族）。
        //    清的是跨场保留集合（`RetainedRecovery`）⇒ 下一场开场就不会再把该 buff 挂上去 ✓（单源）
        int cleared = RetainedRecovery.Count;
        RetainedRecovery.Clear();

        // 🔴 同属"到下次恢复"：**Curio 圣坛祝福也扎营清**（契约"本趟 +N% 伤害（到扎营）"✓）
        if (_curioDamageBlessingPct > 0)
        {
            log.Append(new EffectEvent(default, $"camp_cleared_curio_blessing:{_curioDamageBlessingPct}", 100.0, true));
            _curioDamageBlessingPct = 0;
        }

        log.Append(new CampEndedEvent(0));
        if (cleared > 0)
        {
            log.Append(new EffectEvent(default, $"camp_cleared_until_next_recovery:{cleared}", 100.0, true));
        }
    }

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
