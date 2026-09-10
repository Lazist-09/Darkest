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
        ArchetypeAiConfig? ai = _config.For(enemyUnit.Id.Value);
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

            if (!IsSkillUsable(enemyUnit, rule.SkillId, slot, enemy, player))
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

        IReadOnlyList<int> targets = ResolveTargets(_skills.Get(chosen.SkillId), enemyUnit, enemy, player, buffs);
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

    private bool IsSkillUsable(UnitRuntime unit, string skillId, int slot, FormationBoard enemy, FormationBoard player)
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
            IReadOnlyList<int> targets = SkillTargetResolver.Resolve(skill, unit.Id, enemy, player);
            if (targets.Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>目标槽（槽升序）；taunt 时把坦克位置首（"改敌方 AI 优先攻击嘲盳者"，buff.md taunt）。</summary>
    private IReadOnlyList<int> ResolveTargets(SkillTemplateConfig skill, UnitRuntime unit,
        FormationBoard enemy, FormationBoard player, IBuffLedger? buffs)
    {
        if (skill.Target.Scope != SkillTargetScope.Slots)
        {
            return Array.Empty<int>();
        }

        List<int> targets = SkillTargetResolver.Resolve(skill, unit.Id, enemy, player).ToList();
        if (targets.Count > 1 && buffs is not null && buffs.Has(unit.Id, "taunt"))
        {
            int? taunterSlot = player.UnitAtPosition(new UnitId("tank"));
            if (taunterSlot is { } ts && targets.Contains(ts))
            {
                targets.Remove(ts);
                targets.Insert(0, ts);
            }
        }

        return targets;
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