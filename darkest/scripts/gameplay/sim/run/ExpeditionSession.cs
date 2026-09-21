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
public sealed partial class ExpeditionSession : RunSession, IExpeditionSession
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
    // 🔴🔴 D-7 · 探索层 act-out（DD ⑩ / `dungeon_layer_design.md §F5`）
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 `D-7`：**队内最低士气**（只算**存活者**）；无人可算（`Retained` 空）⇒ `null`。
    ///
    /// <para>⚠️ **为什么用"最低"而不是"平均"**：折磨是**个体**状态（DD ⑩ 是"**某个**英雄被恐惧攫住"），
    /// 只要**任何一名**队员带折磨，那次 Curio / 那顿饭就可能被拒 ⇒ 判据必须取**最低者**，不是平均
    /// （平均会把"一个崩溃 + 五个满状态"抹平成 42 ⇒ 判不出个体折磨）✓</para>
    ///
    /// <para>🔴 **为什么不在这里判"是否带折磨"**：阈值是**数据**（`tuning.dungeon_layer.exploration`）
    /// ⇒ 会话**只给读数**，判定归 `ExplorationActOut.IsAfflicted`（纯函数、可单测、口径唯一）✓</para>
    ///
    /// <para>⚠️ **已知的有界缺口（与 `D-5`/`D-4` 同源，诚实标注）**：`Retained` 只在 `EndBattle` 落账 ⇒
    /// **首战之前**返回 `null` ⇒ 门禁**不生效**（不掷、不写）。窗口 = "开局到首战之间"，
    /// 与 `HungerCanApply` / `TrapCanApply` **同因同果** ✓</para>
    /// </summary>
    public int? LowestSurvivorMorale()
    {
        int? min = null;
        foreach ((int hp, int morale, bool _) in Retained.Values)
        {
            if (hp <= 0)
            {
                continue; // 阵亡者不参与（与 `ResolveHunger` / `ResolveTrapByResist` 同口径）✓
            }

            if (min is null || morale < min.Value)
            {
                min = morale;
            }
        }

        return min;
    }

    /// <summary>🔴 `D-7`：**探索层门禁能否真的生效**（= 名册台账已建立，与 `HungerCanApply` 同因同果）✓</summary>
    public bool ActOutCanApply => LowestSurvivorMorale() is not null;

    // ------------------------------------------------------------------
    // E3 · 扎营流程（最小版内核）：许可（柴火 1）→ 食物档位 → Respite 分配 → 结束
    // 说明：`next_battle` 类效果（磨刀/加固甲胄）由 camp_skills 声明为 **grant_buff**，
    //       其生效与到期**归 buff 台账**，本会话**不另存**"下一场加成"（单源）。
    // ------------------------------------------------------------------

    /// <summary>当前存活人数（口粮缩放 / Respite / 事件士气的作用面）。</summary>
    public int Survivors => Retained.Values.Count(v => v.Hp > 0);

    // ------------------------------------------------------------------
    // 🔴🔴 D-5 · 饥饿（DD wiki "Hunger"）—— 触发后的"吃 / 不吃"结算
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 `D-5`：**吃得起这一顿吗**（每人 <see cref="HungerConfig.FoodPerHero"/> 口粮）。
    /// **DD 铁律：不能只喂一部分人** —— 凑不齐 ⇒ **只能挨饿，且一口粮都不消耗** ✓
    /// </summary>
    public bool CanEatForHunger(HungerConfig config) => Food >= HungerSpawner.FoodRequired(config, Survivors);

    /// <summary>
    /// 🔴🔴 `D-5` **结算一次饥饿**（触发之后**必须**二选一，不能拖/跳）。
    ///
    /// <para>· **吃**（`eat: true` 且**口粮够**）：扣 `Survivors × FoodPerHero` 口粮，全队回
    ///   `EatHealPercent`% 最大 HP（**每人按自己 MaxHp 算** —— DD 原文 "heal every hero for 5% of their maximum HP"）✓</para>
    /// <para>· **不吃**（或口粮不够）：全队掉 `StarveHpPercent`% 最大 HP + 涨 `StarveMorale` 压力，
    ///   🔴 **口粮一点不扣**（DD 原文："the entire party is forced to starve, and **no Food will be eaten**,
    ///   regardless of any Food you may have below the threshold"）✓</para>
    /// <para>· **阵亡者**（`Hp ≤ 0`）既不吃也不挨饿（不再二次伤害）✓</para>
    /// </summary>
    /// <returns>实际发生的是"吃了"还是"挨饿"（口粮不够时即使用户选吃也**必然**是挨饿）✓</returns>
    public string ResolveHunger(CombatLog log, HungerConfig config, bool eat,
        TuningExplorationActOut? actOut = null)
    {
        // 🔴🔴 `D-7`（2026-09-20）：**探索层 act-out —— 拒绝进食**（DD ⑩ / `dungeon_layer_design.md §F5 ③`）。
        //
        //   带**折磨**的队员会**拒绝进食** ⇒ 掷中即**强制挨饿**（推翻玩家选的"吃"）。
        //
        //   🔴 **为什么挂在入口（而不是别处）**：
        //     · 它是"**这次吃饭的结果**"，与 `CanEatForHunger` 同层 ⇒ 必须在这里改写 `eat`，
        //       这样下游（扣口粮 / 回血 / 掉血）**逐字不用改** ✓
        //     · 掷中时 **`ate` 必为 `false`** ⇒ `TrySpend` 不会被调用 ⇒ 口粮**一点不扣**
        //       （与"口粮不够 ⇒ 一口都不消耗"的既有口径**自动一致**）✓
        //
        //   🔴 **配置从参数进**（`actOut`）—— 与 `config`（`HungerConfig`）**同一手法**：
        //      会话不持 `TuningConfig`（那是流程层的），故按需传入；`null` ⇒ 门禁关闭 ✓
        //   🔴 **未配置 ⇒ 一次都不判**（`RollEatRefuse` 内部直接返回 `None`，不掷不写）✓
        bool eatRefused = false;
        if (actOut is not null && LowestSurvivorMorale() is { } lowestMorale)
        {            // 🔴 复用**已绑定**的随机流（`BindTrapRng`）—— 与 `ResolveTrapByResist` 同一条流（可复现）；
            //    未绑定 ⇒ **明确抛**（不静默跳过整个门禁，那是"机制静默失效"家族）⚠️
            IRngProvider actOutRng = _trapRng ?? throw new InvalidOperationException(
                "`ResolveHunger` 判 D-7 拒绝进食需要 `IRngProvider` ⇒ 本会话未注入（`BindTrapRng`）✓");
            ActOutRollResult refused = ExplorationActOut.RollEatRefuse(log, actOutRng, actOut, lowestMorale);
            if (refused.Refused)
            {
                eatRefused = true;
                ActOutEatRefuseCount++;
                log.Append(new EffectEvent(default, "act_out_eat_refuse", 100.0, Triggered: true));
                if (eat)
                {
                    eat = false; // 🔴 **强制挨饿**：推翻玩家的"吃" ✓
                }
            }
        }

        _ = eatRefused; // 留档：它已通过 `act_out_eat_refuse` 事件留痕（不静默）✓

        bool canEat = CanEatForHunger(config);
        bool ate = eat && canEat;

        if (ate)
        {
            int need = HungerSpawner.FoodRequired(config, Survivors);
            TrySpend(log, "food", need, "hunger");
        }

        foreach (string id in Retained.Keys.ToArray())
        {
            (int hp, int morale, bool weak) = Retained[id];
            if (hp <= 0)
            {
                continue; // 阵亡者不受饥饿影响 ✓
            }

            // 🔴 **血量上限的口径 = 全仓唯一真值**（`#325` D6「两份真值」禁令）：
            //    `RosterMaxHp` 由 `RunSession.BeginBattle` 在**第一场**从 `director.Player` 的
            //    `UnitRuntime.MaxHp` 记录 —— 那正是**等级/特质/疾病投影之后**的真值 ✓
            //    🔴 **绝不可**另造第二份公式（那是 `#325` D6 明令禁止的），故与 `ApplyToSurvivors`
            //    （扎营 HP% 的同族结算）**逐字同式**：取不到就回落到"当前 HP"（钳制下限 1）✓
            int max = RosterMaxHp.TryGetValue(id, out int m) && m > 0 ? m : Math.Max(1, hp);

            if (ate)
            {
                int heal = HungerSpawner.EatHealFor(config, max);
                Retained[id] = (Math.Min(max, hp + heal), morale, weak);
            }
            else
            {
                int dmg = HungerSpawner.StarveDamageFor(config, max);
                int newHp = Math.Max(0, hp - dmg);
                Retained[id] = (newHp, Math.Clamp(morale - config.StarveMorale, 0, 100), weak);
            }
        }

        log.Append(new EffectEvent(default, ate ? "hunger_eat" : "hunger_starve", 100.0, true));
        return ate ? "eat" : "starve";
    }

    /// <summary>
    /// 🔴 `D-5`：**饥饿能否真的生效**（= 名册台账已建立）。
    ///
    /// <para>⚠️ **已知的有界缺口（诚实标注，不粉饰）**：`Retained` 只在 `EndBattle`（战后）落账 ⇒
    /// **第一场战斗之前**走走廊若掷中饥饿，此时没有任何人可结算 ⇒ 本方法返回 `false`，
    /// 表现层**不应弹"吃/不吃"**（弹了就是骗玩家：选哪个都没效果）✓</para>
    /// <para>· 实际暴露窗口极窄：`hunger.buffer_at_start = 2` 已保护**开局两条走廊**，而首场战斗
    ///   通常就在起点房间或紧邻处触发 ⇒ 真正的暴露 = "开局 2 条走廊之后、首战之前"这一段 ✓</para>
    /// <para>🔴 **为什么不顺手补上**：唯一真值 `RosterMaxHp` 由 `BeginBattle` 从**投影后的** `UnitRuntime.MaxHp`
    ///   记录；在首战之前要拿到它就得**自己跑一遍投影** ⇒ 那是 `#325` D6 明令禁止的「**两份真值**」⚠️
    ///   ⇒ 宁可留一个**有界且有痕**的缺口，也不制造第二份血量公式 ✓</para>
    /// </summary>
    public bool HungerCanApply => Retained.Count > 0;

    // ------------------------------------------------------------------
    // 🔴🔴 D-4（2026-09-20）· 陷阱结算（DD wiki `Trap`）—— 与 `ResolveHunger` **同族同式**
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴🔴 `D-4` **结算一次陷阱**（踏中 / 拆除 / 已消费 三条路径由 `TrapResolver` 判，本方法只管**记账**）。
    ///
    /// <para>**DD 原文口径**（`darkestdungeon.fandom.com/wiki/Trap`）：</para>
    /// <para>· **踏中** ⇒ **随机一名**存活队员掉 `hp_percent`% **其**最大 HP 的血 + 压力 **恒定** `stress_damage` ✓</para>
    /// <para>· **拆除成功** ⇒ **完全无害**（"renders it harmless"）+ **回** `disarm_stress_heal` 压力 ✓</para>
    /// <para>· **已消费** ⇒ 什么都不做（**不掷、不写、不改任何数**）✓</para>
    ///
    /// <para>🔴 **为什么"随机一人"也要写 `RngDraw`**：DD 原文的伤害是打在"**a random party member**"身上 ⇒
    /// 选人本身是一次**真随机**（且**展示值必须 == 消费值**，纪律 V）⇒ 必写日志 ⚠️</para>
    /// <para>🔴 **血量上限的唯一真值** = `RosterMaxHp`（由 `BeginBattle` 从**投影后**的 `UnitRuntime.MaxHp` 记录）
    /// —— 与 `ResolveHunger` / `ApplyToSurvivors` **逐字同式**，**绝不另造第二份公式**（`#325` D6 禁令）✓</para>
    /// </summary>
    /// <returns>实际发生的路径（`triggered` / `disarmed` / `avoided` / `consumed` / `triggered_no_roster`）✓</returns>
    /// <param name="resistFor">🔴 **按【英雄 id】查陷阱抗性**的口（`null` ⇒ 全体按 0 算的退化解）。
    /// 选人**在本方法内**做（因为"谁被踩中"要等掷骰完才知道）⇒ 故传入的是**查询口**而不是某个值 ✓</param>
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
    public int ActOutEatRefuseCount { get; private set; }


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

}
