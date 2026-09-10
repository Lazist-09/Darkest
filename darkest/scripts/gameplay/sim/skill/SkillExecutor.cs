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

        int[] candidates = SkillTargetResolver.Resolve(skill, caster, player, enemy).ToArray();
        if (candidates.Length == 0)
        {
            return; // NoTarget（防御性；导演层任务应已灰显）
        }

        // 选一（#178/#179）：单体伤害（非 aoe）或 any_ally 单体支援 → 玩家指定（须合法）优先，否则随机固定调用点
        int[] execTargets = candidates;
        bool isSingleton = skill.Damage is not null && !IsAoe(skill)
                           || skill.Damage is null && skill.Target.Scope == SkillTargetScope.AnyAlly;
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
            }

            execTargets = new[] { pick };
        }

        MoraleEffectRequest[] explicitMorale = MapMoraleEffects(skill);

        if (skill.Damage is null)
        {
            ExecuteSupportPath(skill, caster, allyBoard, targetBoard, execTargets, rng, explicitMorale);
        }
        else
        {
            ExecuteDamagePath(skill, caster, player, enemy, execTargets, explicitMorale, rng);
        }

        _runtime.RecordUse(caster, skill); // CD 置位 / per_battle 计数
    }

    // ------------------------------------------------------------------

    private void ExecuteDamagePath(SkillTemplateConfig skill, UnitId caster,
        FormationBoard player, FormationBoard enemy,
        int[] targets, MoraleEffectRequest[] explicitMorale, IRngProvider rng)
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
                explicitMorale.Length > 0 ? explicitMorale : null);
            _pipeline.Execute(fixture, player, enemy, rng);
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
                MapEffects(skill), disp, explicitMorale.Length > 0 ? explicitMorale : null);
            _pipeline.Execute(fixture, player, enemy, rng);
        }
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
                    int healed = Math.Min(target.MaxHp - target.CurrentHp, heal);
                    target.CurrentHp += healed;
                    _log.Append(new HealEvent(target.Id, healed));
                }
            }

            foreach (EffectSpec effect in skill.Effects)
            {
                if (effect.Type is SkillEffectType.StatMod && effect.Stat is not null)
                {
                    UnitRuntime? target = ResolveRuntime(allyBoard, targetBoard, slot);
                    if (target is not null)
                    {
                        ApplyStatMod(target, effect);
                        _log.Append(new EffectEvent(target.Id, "stat_mod", 100.0, true));
                    }
                }

                if (effect.Type is SkillEffectType.Shield && effect.Charges is { } charges)
                {
                    UnitRuntime? target = ResolveRuntime(allyBoard, targetBoard, slot);
                    if (target is not null)
                    {
                        _buffs?.AddCharged(target.Id, "shield", charges); // #156 按次数（铁壁 2 次）
                        _log.Append(new EffectEvent(target.Id, "shield", 100.0, true));
                    }
                }
                // taunt/guard_attach/next_attack_boost：数据已录，钩子执行归 M5 动作层（buff 生命周期在岗）
            }
        }

        foreach (MoraleEffectRequest m in explicitMorale)
        {
            ApplyMoraleToTargets(m, allyBoard, targetBoard, targets, caster);
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
            .Where(e => e.Type is SkillEffectType.Stun or SkillEffectType.Bleed or SkillEffectType.StatMod)
            .Select(e => new EffectRequest(
                e.Type switch
                {
                    SkillEffectType.Stun => "stun",
                    SkillEffectType.Bleed => "bleed",
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