using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;

namespace Darkest.Gameplay.Sim.Enemy;

/// <summary>敌方 AI 决策产物：技能 + 目标槽（槽升序；taunt 优先时坦克位置首）。</summary>
public sealed record SkillChoice(string SkillId, IReadOnlyList<int> TargetSlots);

/// <summary>
/// EnemyAi：固定优先级决策（T-M5-02，enemy.md §5.4 / data_schema §3.5 直译）。
/// 规则表是配置不是逻辑：按数组顺序取第一个"条件满足且技能可用"的规则；无条件规则=默认。
/// 引擎层先过滤技能可用（self_slots/CD/目标非空）；AI 表只决定同类可选时优先谁。
/// 15% 次优先随机 = 数据开关 default off（O-19/#112）；#96 mark_weight 预留不参与。
/// </summary>
public sealed class EnemyAi
{
    private readonly EnemyAiConfig _config;
    private readonly SkillsConfig _skills;
    private readonly SkillRuntimeState _runtime;

    public EnemyAi(EnemyAiConfig config, SkillsConfig skills, SkillRuntimeState runtime)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <summary>决策：返回本回合行动（null = 全部技能不可用 → 空过）。</summary>
    public SkillChoice? Choose(UnitRuntime enemyUnit, FormationBoard enemy, FormationBoard player,
        IBuffLedger? buffs, IRngProvider rng, CombatLog log)
    {
        ArchetypeAiConfig? ai = _config.For(enemyUnit.ArchetypeId);
        if (ai is null || ai.Rules.Count == 0)
        {
            return null;
        }

        int slot = enemy.UnitAtPosition(enemyUnit.Id) ?? -1;
        if (slot < 0)
        {
            return null;
        }

        // 可用规则候选（条件满足 + 技能可用）
        var usable = new List<AiRuleSpec>();
        foreach (AiRuleSpec rule in ai.Rules)
        {
            if (!_skills.ContainsId(rule.SkillId)) // helper below
            {
                continue;
            }

            if (!IsSkillUsable(enemyUnit, rule.SkillId, slot, enemy, player, buffs))
            {
                continue;
            }

            if (!WhenMatches(rule, enemyUnit, slot, enemy, player))
            {
                continue;
            }

            usable.Add(rule);
        }

        if (usable.Count == 0)
        {
            return null; // 全部不可用 → 空过（位置驱动预期张力）
        }

        AiRuleSpec chosen = usable[0];
        // O-19/#112：15% 次优先开关（default off）；开启时抽取走 IRngProvider 并写 CombatLog
        if (ai.Random.Enabled && usable.Count > 1)
        {
            double roll = rng.NextPercent();
            log.Append(new RngDraw(rng.DrawCount, roll));
            if (roll < ai.Random.FallbackProbability * 100.0)
            {
                chosen = usable[1];
            }
        }

        IReadOnlyList<int> targets = ResolveTargets(_skills.Get(chosen.SkillId), enemyUnit, enemy, player, buffs, rng, log);
        log.Append(new EnemyDecisionEvent(enemyUnit.Id, chosen.SkillId, 0, "table_pick", targets.ToArray())); // G0/O-55
        return new SkillChoice(chosen.SkillId, targets);
    }

    private bool WhenMatches(AiRuleSpec rule, UnitRuntime unit, int slot, FormationBoard enemy, FormationBoard player)
    {
        AiWhenSpec w = rule.When;
        if (w is null)
        {
            return true;
        }

        if (w.SelfSlotIn is { } slots && !slots.Contains(slot))
        {
            return false;
        }

        if (w.TargetSlotsOccupiedMin is { } occ)
        {
            FormationBoard board = occ.Side == "player" ? player : enemy;
            int occupied = occ.Slots.Count(pos => board.GetSlot(pos) != SlotState.Empty);
            if (occupied < occ.Min)
            {
                return false;
            }
        }

        if (w.PlayerMoraleAllAtLeast is { } minMorale)
        {
            if (player.UnitsInSlotOrder().Any(u => u.Morale < minMorale))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsSkillUsable(UnitRuntime unit, string skillId, int slot, FormationBoard enemy, FormationBoard player,
        IBuffLedger? buffs)
    {
        SkillTemplateConfig skill = _skills.Get(skillId);
        if (!skill.SelfSlots.Allows(slot))
        {
            return false;
        }

        if (_runtime.CooldownRemaining(unit.Id, skillId) > 0)
        {
            return false;
        }

        if (skill.UseLimit.Type == UseLimitType.PerBattle
            && _runtime.UsedPerBattle(unit.Id, skillId) >= skill.UseLimit.Value.GetValueOrDefault())
        {
            return false;
        }

        if (skill.Target.Scope == SkillTargetScope.Slots)
        {
            // C-1：传台账 ⇒ 潜行目标已从候选中剔除（全潜行 ⇒ 空 ⇒ 本技能不可用）
            IReadOnlyList<int> targets = SkillTargetResolver.Resolve(skill, unit.Id, player, enemy, buffs); // 参数序修正（同 ResolveTargets）
            if (targets.Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 池内目标选择（P1/O-46 ← #185/#186/#187）三层：
    ///   AOE → 池内全部（不受选一影响）；池 ≤1 → 直接返回（**不掷骰**）；
    ///   ① taunt 生效且嘲讽者在池内 → 加权抽取（嘲讽者 taunt_weight、其余各 1），**写 RngDraw**；
    ///   ② 否则按原型固定偏好 target_preference（lowest_hp / backmost / lowest_morale，平局取槽号小）；
    ///   ③ 兜底槽号最小（由 ThenBy(槽号) 保证）。
    /// </summary>
    private IReadOnlyList<int> ResolveTargets(SkillTemplateConfig skill, UnitRuntime unit,
        FormationBoard enemy, FormationBoard player, IBuffLedger? buffs, IRngProvider rng, CombatLog log)
    {
        if (skill.Target.Scope != SkillTargetScope.Slots)
        {
            return Array.Empty<int>();
        }

        List<int> pool = SkillTargetResolver.Resolve(skill, unit.Id, player, enemy, buffs).ToList(); // 参数序：player 板在前（caster 在敌方侧自动识别）；C-1：潜行目标已剔除
        if (pool.Count <= 1 || skill.Tags.Contains(FuncTag.Aoe))
        {
            return pool; // AOE 全池；唯一/空池无抽取（确定性基线保护）
        }

        // ① taunt：按"我方带 taunt buff 者"识别（#192，可插拔——不再猜 tank 原型）；多个嘲讽者各 ×taunt_weight
        var tauntSlots = new List<int>();
        if (buffs is not null)
        {
            foreach (UnitRuntime ally in player.UnitsInSlotOrder())
            {
                if (buffs.Has(ally.Id, "taunt") && player.UnitAtPosition(ally.Id) is { } s && pool.Contains(s))
                {
                    tauntSlots.Add(s);
                }
            }
        }

        if (tauntSlots.Count > 0)
        {
            int tauntW = Math.Max(1, _config.TauntWeight);
            var others = pool.Where(p => !tauntSlots.Contains(p)).ToList();
            int total = tauntSlots.Count * tauntW + others.Count;
            int roll = rng.NextInt(0, total);
            log.Append(new RngDraw(rng.DrawCount, roll)); // 确定性红线：抽取必写日志
            int acc = 0;
            foreach (int s in tauntSlots)
            {
                acc += tauntW;
                if (roll < acc)
                {
                    return new[] { s };
                }
            }

            foreach (int p in others)
            {
                acc += 1;
                if (roll < acc)
                {
                    return new[] { p };
                }
            }
        }

        // ①.5 D2（#204）Mark：我方被标记者权重 ×2（复用 #187 权重制；嘲讽优先、两者互不干扰）
        var markedSlots = new List<int>();
        if (buffs is not null)
        {
            foreach (UnitRuntime ally in player.UnitsInSlotOrder())
            {
                if (buffs.Has(ally.Id, "mark") && player.UnitAtPosition(ally.Id) is { } ms && pool.Contains(ms))
                {
                    markedSlots.Add(ms);
                }
            }
        }

        if (markedSlots.Count > 0)
        {
            const int markWeight = 2;
            var unmarked = pool.Where(p => !markedSlots.Contains(p)).ToList();
            int totalM = markedSlots.Count * markWeight + unmarked.Count;
            int rollM = rng.NextInt(0, totalM);
            log.Append(new RngDraw(rng.DrawCount, rollM)); // 确定性红线：抽取必写日志
            int accM = 0;
            foreach (int s in markedSlots)
            {
                accM += markWeight;
                if (rollM < accM)
                {
                    return new[] { s };
                }
            }

            foreach (int p in unmarked)
            {
                accM += 1;
                if (rollM < accM)
                {
                    return new[] { p };
                }
            }
        }

        // ② 目标偏好（v0.68）：**池内均匀随机**（写 RngDraw；池内唯一候选已在上面直接返回、不掷骰）
        //    taunt（①）与 mark（①.5）为**显式优先级规则**，仍在其之前生效；其余一律等概率。
        string pref = _config.For(unit.ArchetypeId)?.TargetPreference ?? "random";
        if (pref == "random")
        {
            int idx = rng.NextInt(0, pool.Count);
            log.Append(new RngDraw(rng.DrawCount, idx)); // 确定性红线：抽取必写日志
            return new[] { pool[idx] };
        }

        // 兼容保留：若数据仍声明具体偏好（非 random），按原语义执行（③ 兜底：槽号小者优先）
        int picked = pref switch
        {
            "backmost" => pool.Max(),
            "lowest_morale" => pool.OrderBy(p => player.UnitRuntimeAt(p)?.Morale ?? int.MaxValue).ThenBy(p => p).First(),
            _ => pool.OrderBy(p => player.UnitRuntimeAt(p)?.CurrentHp ?? int.MaxValue).ThenBy(p => p).First(),
        };
        return new[] { picked };
    }
}

internal static class SkillsConfigExt
{
    /// <summary>技能 id 存在性（引用完整性；数据层校验外再防御）。</summary>
    public static bool ContainsId(this SkillsConfig skills, string id)
    {
        foreach (SkillTemplateConfig s in skills.Skills)
        {
            if (s.Id == id)
            {
                return true;
            }
        }

        return false;
    }
}