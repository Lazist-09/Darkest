using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Pipeline;

namespace Darkest.Gameplay.Sim.Skill;

/// <summary>
/// 技能执行骨架（T-M3-06）：把真实技能数据（SkillTemplateConfig，13 字段）桥接到 M2 DamagePipeline：
/// 伤害技能 → 逐段（flat / missing_hp 按目标当前 HP 计算实际倍率）；附加效果（stun/stat_mod/bleed）与
/// 位移经 SkillFixture 由管线按 §2 次序（全部目标结算后执行）完成；显式 morale_effects 走管线钩子
/// （威吓箭 −4 取代派生，O-21）；无伤害技能（治疗/士气/增益）走支援最小路径（固定值治疗/效果/士气/记录）。
/// 动作结束记录 use_limit 消耗（CD 置位 / per_battle 计数）。零 Godot 引用。
/// </summary>
public sealed class SkillExecutor
{
    private readonly SkillsConfig _skills;
    private readonly BalanceTable _balance;
    private readonly MoraleEventsConfig _moraleEvents;
    private readonly CombatLog _log;
    private readonly SkillRuntimeState _runtime;
    private readonly DamagePipeline _pipeline;
    private readonly Darkest.Core.Contracts.IBuffLedger? _buffs;

    public SkillExecutor(SkillsConfig skills, BalanceTable balance, MoraleEventsConfig moraleEvents,
        CombatLog log, SkillRuntimeState runtime, Darkest.Core.Contracts.IBuffLedger? buffs = null)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _moraleEvents = moraleEvents ?? throw new ArgumentNullException(nameof(moraleEvents));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _buffs = buffs;
        _pipeline = new DamagePipeline(balance, moraleEvents, log, buffs);
    }

    public DamagePipeline Pipeline => _pipeline;

    /// <summary>
/// 执行一次技能动作（导演/目标选择者调用）：
/// #178/#179「target 范围 = 候选池」——单体伤害技能从候选（占用目标槽）【选一】执行：
///   实机由玩家指定（chosenTargets，须落在候选内）；headless/模拟走固定调用点随机选一（NextInt + RngDraw）；
/// AOE（tags aoe，仅横扫/精神震荡，P11）与无伤害技能照旧全范围。
/// </summary>
    public void Execute(SkillTemplateConfig skill, UnitId caster, FormationBoard player, FormationBoard enemy,
        IRngProvider rng, IReadOnlyList<int>? chosenTargets = null)
    {
        if (skill is null)
        {
            throw new ArgumentNullException(nameof(skill));
        }

        FormationBoard targetBoard = skill.Target.Side == "enemy" ? enemy : player;
        FormationBoard allyBoard = player.UnitAtPosition(caster) is not null ? player : enemy;

        int[] candidates = SkillTargetResolver.Resolve(skill, caster, player, enemy, _buffs).ToArray();
        if (candidates.Length == 0)
        {
            _log.Append(new SkillRefusedEvent(caster, skill.Id, "no_target")); // G0/O-55
            return; // NoTarget（防御性；导演层任务应已灰显）
        }

        // C-1 / C-1a（2026-09-20）De-Stealth：带 `ignore_stealth` 的技能【**命中**即解除】目标潜行
        //   （原版 `.on_hit true` ⇒ 命中才解除；`playwright.effects.darkest:5` "gunfire shattered"）。
        //   🔴 精确化：**不再"选中即解除"** —— 改为在管线结算之后**读 `HitEvent`**，
        //      只对 `Hit == true` 的目标解除；未命中（Miss）⇒ 潜行**保留**（原版口径）。
        //      若技能未走伤害管线（无 `Damage`，如纯支援/移动）⇒ 无命中概念 ⇒ **不解除**（保守，不错杀）✓
        bool deStealth = _buffs is not null && skill.Tags.Contains(FuncTag.IgnoreStealth);
        int logMark = _log.Events.Count; // 记录结算前的位置，之后只读**本次结算**新增的 HitEvent ✓

        // G0/O-55：技能使用事件（谁用了什么技能、打了哪些目标位）——「技能使用率」KPI 的唯一前提
        _log.Append(new SkillUseEvent(caster, player.UnitAtPosition(caster) ?? 0, skill.Id, candidates.ToArray()));

        // 选一（#178/#179）：单体伤害（非 aoe）、any_ally 单体支援、或 move_range 移动 → 玩家指定/随机固定调用点
        int[] execTargets = candidates;
        bool isSingleton = skill.Damage is not null && !IsAoe(skill)
                           || skill.Damage is null && skill.Target.Scope is SkillTargetScope.AnyAlly or SkillTargetScope.MoveRange;
        if (isSingleton && candidates.Length > 1)
        {
            int pick = -1;
            if (chosenTargets is not null)
            {
                foreach (int t in chosenTargets)
                {
                    if (Array.IndexOf(candidates, t) >= 0)
                    {
                        pick = t;
                        break;
                    }
                }
            }

            if (pick < 0)
            {
                pick = candidates[rng.NextInt(0, candidates.Length)];
                _log.Append(new RngDraw(rng.DrawCount, pick));
                _log.Append(new EffectEvent(caster, "no_policy_fallback", 100.0, true)); // P2：无策略目标时的兜底标注（便于识别测量口径缺陷）
            }

            execTargets = new[] { pick };
        }

        MoraleEffectRequest[] explicitMorale = MapMoraleEffects(skill);

        // F2（#193）失控 proc：支援位 → 改「捆缚」（无法行动）；战斗位 → 33% 在合法候选池内随机换目标
        if (_buffs is not null && _buffs.Has(caster, "affliction_uncontrolled"))
        {
            int casterPos = allyBoard.UnitAtPosition(caster) ?? -1;
            if (allyBoard.Layout.ExtensionSlots.Contains(casterPos))
            {
                _buffs.Add(caster, "bound", source: null);
                _log.Append(new EffectEvent(caster, "uncontrolled_bound", 100.0, true));
                return; // 支援位失控 = 捆缚（#48）
            }

            if (Darkest.Gameplay.Sim.Buffs.AfflictionProcs.Triggered(_buffs, _balance, caster, "affliction_uncontrolled", rng, _log))
            {
                int wild = candidates[rng.NextInt(0, candidates.Length)];
                _log.Append(new RngDraw(rng.DrawCount, wild));
                _log.Append(new EffectEvent(caster, "uncontrolled_retarget", 100.0, true));
                execTargets = new[] { wild }; // 仍在合法候选池内（受站位/技能目标位约束）
            }
        }

        if (skill.Damage is null && skill.Target.Scope == SkillTargetScope.MoveRange)
        {
            ExecuteMovePath(skill, caster, allyBoard, execTargets, rng); // 池外移动（#180）
        }
        else if (skill.Damage is null)
        {
            ExecuteSupportPath(skill, caster, allyBoard, targetBoard, execTargets, rng, explicitMorale);
        }
        else
        {
            ExecuteDamagePath(skill, caster, player, enemy, execTargets, explicitMorale, rng, logMark);
        }

        // 🔴 C-1a 精确化：结算之后才解除潜行 —— **只认真的命中**（`HitEvent.Hit == true`）✓
        if (deStealth)
        {
            ApplyDeStealthOnHits(logMark, caster);
        }

        _runtime.RecordUse(caster, skill); // CD 置位 / per_battle 计数
    }

    /// <summary>
    /// 🔴 C-1a（2026-09-20）**命中即解除**：扫本次结算新产生的 `HitEvent`，对**命中**的目标清掉潜行。
    /// <para>· 未命中（`Hit == false`）⇒ **保留潜行**（原版 `.on_hit` 语义）✓</para>
    /// <para>· 同一目标多次命中（多段/AOE）⇒ 只清一次、只写一条 `unstealth` 事件（去重）✓</para>
    /// <para>· 「没走伤害管线」⇒ 本次区间内无 `HitEvent` ⇒ 什么都不做（纯支援/移动技能不会误解除）✓</para>
    /// </summary>
    private void ApplyDeStealthOnHits(int fromIndex, UnitId caster)
    {
        if (_buffs is null)
        {
            return;
        }

        var alreadyCleared = new HashSet<UnitId>();
        for (int i = fromIndex; i < _log.Events.Count; i++)
        {
            if (_log.Events[i] is not HitEvent hit || !hit.Hit || hit.Target is not { } victim)
            {
                continue;
            }

            if (!alreadyCleared.Add(victim))
            {
                continue; // 该目标本次已处理过 ✓
            }

            if (_buffs.HasStateFlag(victim, SkillTargetResolver.StealthFlag))
            {
                _buffs.ClearStateFlag(victim, SkillTargetResolver.StealthFlag);
                _log.Append(new EffectEvent(victim, "unstealth", 100.0, true, caster));
            }
        }
    }

    /// <summary>池外「移动」（#180）：与目标位【直接互换】（原目标位的人到自身原位，途经槽位不动）、
    /// 不过抗性、无伤害/士气/死门（#117）；SwapEvent/DisplaceEvent 计入位移 KPI（O-41）。</summary>
    private void ExecuteMovePath(SkillTemplateConfig skill, UnitId caster, FormationBoard allyBoard,
        int[] execTargets, IRngProvider rng)
    {
        _ = rng; // 移动不过抗性、无随机（固定调用点无骰）
        if (execTargets.Length == 0 || allyBoard.GetSlot(execTargets[0]) == SlotState.Empty)
        {
            return; // NoTarget（空位不可选，#21）
        }

        int from = allyBoard.UnitAtPosition(caster) ?? -1;
        int to = execTargets[0];
        if (from < 1 || from == to)
        {
            return;
        }

        bool ok = allyBoard.SwapSlots(from, to); // 两点直接互换（非逐级推移）
        UnitRuntime? other = allyBoard.UnitRuntimeAt(to);
        _log.Append(new SwapEvent(caster, from, to, other?.Id, "move")); // G0：移动事件 + 被换者（O-41 位移 KPI）
        _log.Append(new DisplaceEvent(caster, from, to, PassedResist: true, ok, ok ? "" : "move_swap_failed"));
    }

    // ------------------------------------------------------------------

    private void ExecuteDamagePath(SkillTemplateConfig skill, UnitId caster,
        FormationBoard player, FormationBoard enemy,
        int[] targets, MoraleEffectRequest[] explicitMorale, IRngProvider rng, int logMark)
    {
        bool hasMissingHp = skill.Damage!.Segments.Any(s => s.Type == DamageSegmentType.MissingHp);
        bool hasPush = skill.Displacement is { Type: DisplacementType.Push };
        bool perTarget = hasMissingHp || hasPush || skill.Displacement is { Type: DisplacementType.Pull };
        FormationBoard targetBoard = skill.Target.Side == "enemy" ? enemy : player;

        if (!perTarget)
        {
            // 动作级一次（多目标 AOE 共用同一段倍率；O-14 团队聚合由管线单次调用合并）
            var fixture = new SkillFixture(
                skill.Id, caster, TargetSide(skill), targets.ToArray(),
                skill.HitMod, skill.CritMod, AxisString(skill.DamageAxis),
                FlatMultipliers(skill), IsAoe(skill),
                MapEffects(skill), MapDisplacement(skill, selfOnly: true),
                explicitMorale.Length > 0 ? explicitMorale : null, skill.BonusVsMarkedPercent);
            _pipeline.Execute(fixture, player, enemy, rng);
            ApplyObstacleDamage(logMark, skill, targetBoard, targets, rng); // 🔴 障碍受击（动作级一次）✓
            return;
        }

        // 逐目标（missing_hp 实际倍率 / push 位移槽位依赖目标）
        foreach (int slot in targets)
        {
            if (targetBoard.GetSlot(slot) == SlotState.Empty)
            {
                continue;
            }

            SkillDisplace? disp = skill.Displacement is { Type: DisplacementType.Push } d
                ? new SkillDisplace(slot, slot + 1, d.Count, SelfDisplacement: false)
                : null;

            var fixture = new SkillFixture(
                skill.Id, caster, TargetSide(skill), new[] { slot },
                skill.HitMod, skill.CritMod, AxisString(skill.DamageAxis),
                ResolvedMultipliers(skill, targetBoard, slot), IsAoe(skill),
                MapEffects(skill), disp, explicitMorale.Length > 0 ? explicitMorale : null, skill.BonusVsMarkedPercent);
            _pipeline.Execute(fixture, player, enemy, rng);
            ApplyObstacleDamage(logMark, skill, targetBoard, new[] { slot }, rng); // 🔴 障碍受击（逐目标）✓
        }
    }

    /// <summary>
    /// 🔴 **障碍受击结算**（2026-09-20 接线）：让"挡路的木箱/石堆"**真的能被打掉**。
    /// <para>**为什么必须补这一刀**：`DamagePipeline` 遇 `SlotState.Blocked` 直接 `continue`
    /// （"障碍 M2 不结算伤害/状态"）⇒ 实测**全仓无任何障碍扣血调用点** ⇒
    /// `FormationBoard.TryGetObstacleHp` / `RemoveObstacle` **只有测试在调**，
    /// 实机里障碍 = **纯无敌墙**（与 `ObstacleRuntime` 注释"只有血量的占位角色"矛盾）⚠️</para>
    /// <para>**纪律 V（展示值 == 消费值）**：本方法**不重掷骰** —— 它只**读本次区间新产生的
    /// `HitEvent`**（已经写进日志的那次判定），据其 `Hit` 决定打没打中 ⇒ **数字完全可从事件流复算** ✓</para>
    /// <para>**伤害量来源**：优先用本次实际 `DamageEvent.Amount`（已经过防御/护盾/暴击的**结算值**）；
    /// 若该目标无 `DamageEvent`（例：纯位移/纯效果技能打在障碍上）⇒ 回落到**技能自身最小段伤害**
    /// 作为确定性的扣减量（不掷骰 ⇒ 仍可复算）✓</para>
    /// <para>**不做的**：障碍**不参与士气/虚弱/死门**、**免疫 debuff**（GDD §1.1）⇒ 本方法只碰 HP ✓</para>
    /// </summary>
    /// <param name="logMark2">本次结算开始前的事件下标（只读这之后的新事件）✓</param>
    private void ApplyObstacleDamage(int logMark2, SkillTemplateConfig skill,
        FormationBoard targetBoard, int[] targets, IRngProvider rng)
    {
        foreach (int slot in targets)
        {
            if (targetBoard.GetSlot(slot) != SlotState.Blocked)
            {
                continue; // 只处理障碍槽（非障碍由管线自理）✓
            }

            // ① 读本次区间里**针对该槽**的命中判定（不重掷 ⇒ 纪律 V）——
            //    `HitEvent.Target` 对障碍槽为 null（障碍无 UnitId）⇒ 只能按"本次是否有命中"整体取用 ✓
            bool? hit = null;
            int dealt = 0;
            for (int i = logMark2; i < _log.Events.Count; i++)
            {
                if (_log.Events[i] is HitEvent h && h.Target is null)
                {
                    hit = h.Hit; // 碰撞在"有碰撞无单位"的槽上 = 障碍（唯一契约：障碍槽内无 UnitRuntime）✓
                }

                if (_log.Events[i] is DamageEvent d && d.Target is null && d.Amount > 0)
                {
                    dealt += d.Amount;
                }
            }

            if (hit is false)
            {
                _log.Append(new EffectEvent(null, $"obstacle_miss@{slot}", 100.0, false)); // 没打中：障碍不掉血 ✓
                continue;
            }

            bool hasRoll = hit is not null;
            int amount = dealt > 0 ? dealt : FallbackObstacleDamage(skill);
            if (amount <= 0 && !hasRoll)
            {
                continue; // 无命中判定、又无伤害段 ⇒ 不是攻击（例：纯支援/移动）⇒ 不碰障碍 ✓
            }

            bool stillThere = targetBoard.DamageObstacle(slot, amount);
            _log.Append(new EffectEvent(null, $"obstacle_damage@{slot}", 100.0, true, null));
            _log.Append(new EffectEvent(null,
                stillThere ? $"obstacle_stand@{slot}" : $"obstacle_destroyed@{slot}", 100.0, true, null));
        }
    }

    /// <summary>障碍受击的**回落扣减量**：技能伤害段的最小 `multiplier`（不掷骰、不读来源属性 ⇒ 可复算）✓</summary>
    private static int FallbackObstacleDamage(SkillTemplateConfig skill)
    {
        if (skill.Damage is null)
        {
            return 0;
        }

        int best = int.MaxValue;
        foreach (DamageSegment seg in skill.Damage.Segments)
        {
            if (seg.Multiplier is { } m && m > 0 && m < best)
            {
                best = (int)Math.Round(m);
            }
        }

        return best == int.MaxValue ? 0 : Math.Max(1, best);
    }

    private void ExecuteSupportPath(SkillTemplateConfig skill, UnitId caster, FormationBoard allyBoard,
        FormationBoard targetBoard, int[] targets, IRngProvider rng, MoraleEffectRequest[] explicitMorale)
    {
        // 支援技能最小路径：固定值治疗 / 效果 / 士气（不吃命中）。零抽取除非效果概率型。
        foreach (int slot in targets)
        {
            if (skill.HealFixed is { } heal)
            {
                UnitRuntime? target = ResolveRuntime(allyBoard, targetBoard, slot);
                if (target is not null)
                {
                    // F2（#193）自私 proc：33% 拒绝这一次治疗/鼓舞（本次无效；施法者行动照常消耗）
                    if (Darkest.Gameplay.Sim.Buffs.AfflictionProcs.Triggered(_buffs, _balance, target.Id, "affliction_selfish", rng, _log))
                    {
                        _log.Append(new EffectEvent(target.Id, "selfish_refuse", 100.0, true));
                        continue;
                    }

                    // D7（#209）暴击治疗：概率来自 `tuning.heal_crit`（原硬编码：单体 12% / 多目标 5%）→ 治疗量 ×2 + 目标士气（`morale_events`）
                    double critRoll = rng.NextPercent();
                    _log.Append(new RngDraw(rng.DrawCount, critRoll));
                    double critChance = targets.Length > 1 ? _balance.HealCritMultiPercent : _balance.HealCritSinglePercent;
                    bool critHeal = critRoll < critChance;

                    // 🆕 **M2 激活（架构裁 (丙) · 第 1 条）**：`hp_heal_percent` ⇒ 治疗量 ×(1 + pct/100) ✓
                    //    语义（原版）：**施法者**的"治疗量"修正 ⇒ 故读 `caster` 的 buff 修正 ✓
                    //    🔴 无该 buff ⇒ pct = 0 ⇒ **行为与激活前逐位相同** ✓（前后读数见 reports/m2_activation_readings.md）
                    int healPct = _buffs?.PercentModAny(caster, "hp_heal_percent") ?? 0;
                    int baseHeal = critHeal ? heal * 2 : heal;
                    int scaledHeal = HealAmount.Scale(baseHeal, healPct);   // 🆕 算法在 HealAmount（可单测 ✓）
                    if (healPct != 0)
                    {
                        _log.Append(new EffectEvent(caster, "hp_heal_percent", healPct, true, caster));
                    }

                    int healed = Math.Min(target.MaxHp - target.CurrentHp, scaledHeal);
                    target.CurrentHp += healed;
                    _log.Append(new HealEvent(target.Id, healed, caster, skill.Id)); // G0：治疗来源 + 技能
                    if (critHeal)
                    {
                        _pipeline.Morale.Apply(target, _moraleEvents.Get("critical_heal").Delta, "critical_heal", _log);
                        _log.Append(new EffectEvent(target.Id, "critical_heal", 100.0, true, caster));
                    }
                }
            }

            foreach (EffectSpec effect in skill.Effects)
            {
                // 🔴 补欠账（`data_schema.md:259`）：**`apply_to` = effects 的【挂载对象】**
                //    契约：缺省 = `targets`；可选 `self`（盾墙的自身物防 +6）／`ally_targets`（守护挂相邻友方）
                //    实测：此前**处理器完全不读它** ⇒ 5 个 `apply_to=self` 的技能 + 1 个 `apply_to=team`
                //    的挂载语义未生效 ⚠️（数据里实际用到 `self` ×5 ／ **`team` ×1**）
                //    ⚠️ **契约与数据不一致**：数据用了 `team`，而契约只列了 `self`/`ally_targets` ⇒ 已上报待对账
                IReadOnlyList<UnitRuntime> effectTargets = ResolveEffectTargets(effect.ApplyTo, allyBoard, targetBoard, slot, caster);
                foreach (UnitRuntime target in effectTargets)
                {
                    if (effect.Type is SkillEffectType.StatMod && effect.Stat is not null)
                    {
                        ApplyStatMod(target, effect);
                        _log.Append(new EffectEvent(target.Id, "stat_mod", 100.0, true, caster));
                        _log.Append(new StatModEvent(target.Id, effect.Stat ?? "?", effect.Delta ?? 0, effect.DurationRounds ?? 0)); // G0
                    }

                    if (effect.Type is SkillEffectType.Shield && effect.Charges is { } charges)
                    {
                        _buffs?.AddCharged(target.Id, "shield", charges); // #156 按次数（铁壁 2 次）
                        _log.Append(new EffectEvent(target.Id, "shield", 100.0, true));
                    }

                    if (effect.Type is SkillEffectType.Taunt)
                    {
                        // F1（#192）：嘲讽挂到【自己】身上（target.scope=self）；敌方 AI 按 buff holder 识别 → 可插拔
                        _buffs?.Add(target.Id, "taunt", source: null);
                        _log.Append(new EffectEvent(target.Id, "taunt", 100.0, true));
                    }

                    if (effect.Type is SkillEffectType.Stealth)
                    {
                        // C-1（v0.99）：潜行挂到目标身上（原版 `.stealth 1 .duration 2`，playwright:26 挂 performer=自己；
                        // 本实现走 apply_to 通用挂载 —— 缺省=目标列表，`self`=施法者，与 taunt 同构）。
                        // 时长由 buff_defs.json 的 stealth.duration.value 决定（2 回合，placeholder）。
                        _buffs?.Add(target.Id, SkillTargetResolver.StealthFlag, source: caster);
                        _log.Append(new EffectEvent(target.Id, SkillTargetResolver.StealthFlag, 100.0, true, caster));
                    }
                }
                // guard_attach/next_attack_boost：数据已录，钩子执行归后续包
            }
        }

        foreach (MoraleEffectRequest m in explicitMorale)
        {
            ApplyMoraleToTargets(m, allyBoard, targetBoard, targets, caster);
        }
    }

    /// <summary>
    /// **`apply_to` 的挂载对象**（`data_schema.md:259`）：
    /// · 缺省 / `targets` ⇒ 技能解析出的目标（原行为）
    /// · `self` ⇒ **施法者自己**（盾墙自身物防 +6 ／ 殊死一搏的自身 buff）
    /// · `team` ⇒ **我方全体存活单位**（总动员：全队 buff；⚠️ 该取值**不在契约列表里**，已上报对账）
    /// · `ally_targets` ⇒ 与 `targets` 同处理（契约提到但**数据未使用**；待契约澄清后细化"相邻友方"）
    /// </summary>
    private IReadOnlyList<UnitRuntime> ResolveEffectTargets(string? applyTo, FormationBoard allyBoard,
        FormationBoard targetBoard, int slot, UnitId caster)
    {
        switch (applyTo)
        {
            case "self":
            {
                int? pos = allyBoard.UnitAtPosition(caster) ?? targetBoard.UnitAtPosition(caster);
                UnitRuntime? me = pos is { } p
                    ? allyBoard.UnitRuntimeAt(p) ?? targetBoard.UnitRuntimeAt(p)
                    : null;
                return me is null ? Array.Empty<UnitRuntime>() : new[] { me };
            }
            case "team":
                return allyBoard.UnitsInSlotOrder().Where(u => u.CurrentHp > 0).ToArray();
            default:
            {
                UnitRuntime? target = ResolveRuntime(allyBoard, targetBoard, slot);
                return target is null ? Array.Empty<UnitRuntime>() : new[] { target };
            }
        }
    }

    private void ApplyMoraleToTargets(MoraleEffectRequest m, FormationBoard allyBoard, FormationBoard targetBoard,
        int[] targets, UnitId caster)
    {
        _ = m; _ = allyBoard; _ = targetBoard; _ = targets; _ = caster;
        // M3 骨架：显式 morale_effects 的 targets 作用域在伤害路径由管线处理；支援路径的 team/自
        // 我/目标作用域按 scope 最小实现（这里以 targets 列表为目标逐员 Apply）。
        foreach (int slot in targets)
        {
            UnitRuntime? target = ResolveRuntime(allyBoard, targetBoard, slot)
                ?? allyBoard.UnitRuntimeAt(slot)
                ?? targetBoard.UnitRuntimeAt(slot);
            if (target is not null)
            {
                _pipeline.Morale.Apply(target, m.Delta, "skill_morale_effect", _log);
            }
        }
    }

    // ------------------------------------------------------------------ 映射辅助

    private static FormationSide TargetSide(SkillTemplateConfig skill)
        => skill.Target.Side == "enemy" ? FormationSide.Enemy : FormationSide.Player;

    private static bool IsAoe(SkillTemplateConfig skill) => skill.Tags.Contains(FuncTag.Aoe);

    private static string AxisString(SkillDamageAxis axis) => axis switch
    {
        SkillDamageAxis.Physical => "physical",
        SkillDamageAxis.Mental => "mental",
        _ => "none",
    };

    private static IReadOnlyList<double> FlatMultipliers(SkillTemplateConfig skill)
        => skill.Damage!.Segments.Select(s => s.Multiplier ?? 1.0).ToArray();

    private static IReadOnlyList<double> ResolvedMultipliers(SkillTemplateConfig skill, FormationBoard board, int slot)
    {
        UnitRuntime? target = board.UnitRuntimeAt(slot);
        double missingRatio = target is null || target.MaxHp <= 0
            ? 0.0
            : (double)(target.MaxHp - target.CurrentHp) / target.MaxHp;
        return skill.Damage!.Segments.Select(s => s.Type == DamageSegmentType.MissingHp
            ? (s.Base ?? 1.0) + missingRatio * (s.Coefficient ?? 0.0)
            : s.Multiplier ?? 1.0).ToArray();
    }

    private static IReadOnlyList<EffectRequest> MapEffects(SkillTemplateConfig skill)
        => skill.Effects
            .Where(e => e.Type is SkillEffectType.Stun or SkillEffectType.Bleed or SkillEffectType.StatMod or SkillEffectType.Mark)
            .Select(e => new EffectRequest(
                e.Type switch
                {
                    SkillEffectType.Stun => "stun",
                    SkillEffectType.Bleed => "bleed",
                    SkillEffectType.Mark => "mark", // D2（#204）
                    _ => "stat_mod",
                },
                e.Probability,
                e.ResistAxis,
                e.Stat,
                e.Delta ?? 0))
            .ToArray();

    private static SkillDisplace? MapDisplacement(SkillTemplateConfig skill, bool selfOnly)
    {
        if (skill.Displacement is not { } d)
        {
            return null;
        }

        return d.Type switch
        {
            DisplacementType.SelfForward or DisplacementType.SelfBackward
                => new SkillDisplace(FromPos: 0, ToPos: d.Type == DisplacementType.SelfForward ? 1 : -1, Distance: d.Count, SelfDisplacement: true),
            DisplacementType.Push when selfOnly => null, // push 由逐目标路径构造
            _ => null,
        };
    }

    private static MoraleEffectRequest[] MapMoraleEffects(SkillTemplateConfig skill)
        => skill.MoraleEffects.Select(m => new MoraleEffectRequest(
            m.Scope switch
            {
                MoraleEffectScope.Targets => "targets",
                MoraleEffectScope.Team => "team",
                MoraleEffectScope.Self => "self",
                _ => "ally_targets",
            },
            m.Delta)).ToArray();

    private static UnitRuntime? ResolveRuntime(FormationBoard a, FormationBoard b, int slot)
        => a.SlotCount >= slot ? a.UnitRuntimeAt(slot) ?? b.UnitRuntimeAt(slot) : b.UnitRuntimeAt(slot);

    private static void ApplyStatMod(UnitRuntime target, EffectSpec effect)
    {
        int delta = effect.Delta ?? 0;
        switch (effect.Stat)
        {
            case "attack": target.AttackMod += delta; break;
            case "phys_def": target.PhysDefMod += delta; break;
            case "resilience": target.ResilienceMod += delta; break;
            case "speed": target.SpeedMod += delta; break;
        }
    }
}