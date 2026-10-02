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
///
/// <para>**本类按职责拆为 4 片**（用户红线：程序文件 <=600 行 · 架构 file_size_split §1.2）：
/// 本片 = 入口与骨架（Execute 分流 · 命中即解除潜行 · 池外移动）；另三片 partial =
/// SkillExecutor.DamagePath.cs（伤害路径）· SkillExecutor.SupportPath.cs（支援路径）·
/// SkillExecutor.PipelineMapping.cs（技能数据 → 管线输入的纯映射）✓</para>
public sealed partial class SkillExecutor
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

}
