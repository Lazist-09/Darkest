using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;

namespace Darkest.Tests.MonteCarlo;

/// <summary>玩家策略产物：本回合我方动作序列（经 director 执行）。</summary>
public sealed record PlayerAction(UnitId Actor, string SkillId);

/// <summary>策略种类（T-M6-02）。</summary>
public enum PolicyKind { SemiRandom, Baseline }

/// <summary>
/// 玩家策略（T-M6-02 / blueprint §9.10）：半随机（合法技能+合法目标内按标签权重随机）与
/// 基线 AI（优先输出技能且目标覆盖最低血敌方槽）。策略随机走注入 IRngProvider 固定时机，
/// 保证整场命令流可复现；敌方策略复用 EnemyAi（同源）。携带集 = 原型全池（9 选 5 简化）。
/// </summary>
public static class Policies
{
    private sealed class Cache
    {
        public readonly SkillsConfig Skills;
        public readonly SkillRuntimeState Runtime = new();
        public readonly SkillUseResolver Resolver;

        public Cache()
        {
            Skills = SkillsConfig.Parse(ReadData("skills.json"));
            Resolver = new SkillUseResolver(Skills);
        }
    }

    private static readonly Cache C = new();

    public static IReadOnlyList<PlayerAction> Choose(PolicyKind kind, BattleDirector director, IRngProvider rng)
    {
        var actions = new List<PlayerAction>();
        foreach (UnitRuntime unit in director.Player.UnitsInSlotOrder())
        {
            string? skillId = DecideForUnit(kind, unit, director, rng).SkillId;
            if (skillId is not null)
            {
                actions.Add(new PlayerAction(unit.Id, skillId));
            }
        }

        return actions;
    }

    /// <summary>单单位决策（导演 actor 节拍用）：SemiRandom 含基础智能换位；Baseline 对单体系指定最低血目标。</summary>
    public static PlayerDecision DecideForUnit(PolicyKind kind, UnitRuntime unit, BattleDirector director, IRngProvider rng)
    {
        if (kind == PolicyKind.SemiRandom)
        {
            int? swapPos = SwapSupportNeeded(unit, director);
            if (swapPos is { } sp)
            {
                return new PlayerDecision(null, sp);
            }
        }

        if (kind == PolicyKind.Baseline)
        {
            string? skillId = PickBaseline(unit, director);
            int? target = LowestHpTargetSlot(skillId, unit, director);
            return new PlayerDecision(skillId, null, target);
        }

        return new PlayerDecision(PickSemiRandom(unit, director, rng), null);
    }

    /// <summary>基线：若该技能是单体伤害（非 aoe）且候选含最低血敌方槽 → 指定之（#178 玩家集火）。</summary>
    private static int? LowestHpTargetSlot(string? skillId, UnitRuntime unit, BattleDirector director)
    {
        if (skillId is null)
        {
            return null;
        }

        SkillTemplateConfig s = C.Skills.Get(skillId);
        if (s.Damage is null || s.Tags.Contains(FuncTag.Aoe))
        {
            return null;
        }

        IReadOnlyList<int> candidates = SkillTargetResolver.Resolve(s, unit.Id, director.Enemy, director.Player);
        UnitRuntime? lowest = director.Enemy.UnitsInSlotOrder().OrderBy(u => u.CurrentHp).FirstOrDefault();
        if (lowest is null)
        {
            return null;
        }

        int? lowestSlot = director.Enemy.UnitAtPosition(lowest.Id);
        return lowestSlot is { } ls && candidates.Contains(ls) ? ls : null;
    }

    /// <summary>
    /// 基础智能换位（拍板）：战斗位虚弱者发起 → 换入健康支援位（优先军医/政委；同回合 ≤1 次）。
    /// 返回支援位槽位（无可换 → null）。
    /// </summary>
    private static int? SwapSupportNeeded(UnitRuntime unit, BattleDirector director)
    {
        if (director.SwappedThisRound || !unit.Weak)
        {
            return null;
        }

        int pos = director.Player.UnitAtPosition(unit.Id) ?? -1;
        if (pos is < 1 or > 4)
        {
            return null; // 发起者须在战斗位
        }

        // 支援位占用者，优先军医/政委（#41a「换位由战斗位角色发起，支援位选人优先…」；编成 5=warrior 6=medic）
        int priority(string id) => id switch { "medic" => 0, "commissar" => 1, _ => 2 };
        int? best = null;
        foreach (int slot in director.Player.Layout.SupportSlots)
        {
            UnitRuntime? ally = director.Player.UnitRuntimeAt(slot);
            if (ally is null || ally.Weak)
            {
                continue;
            }

            if (best is null || priority(director.Player.UnitRuntimeAt(slot)!.Id.Value)
                < priority(director.Player.UnitRuntimeAt(best.Value)!.Id.Value))
            {
                best = slot;
            }
        }

        return best;
    }

    private static string? PickSemiRandom(UnitRuntime unit, BattleDirector director, IRngProvider rng)
    {
        List<string> usable = UsableSkills(unit, director);
        if (usable.Count == 0)
        {
            return null;
        }

        // 标签权重：output ×3 / displacement ×2 / 其余 ×1（避免只集火单调）
        var pool = new List<string>();
        foreach (string id in usable)
        {
            SkillTemplateConfig s = C.Skills.Get(id);
            int weight = s.Tags.Contains(FuncTag.Output) ? 3
                : s.Tags.Contains(FuncTag.Displacement) ? 2 : 1;
            for (int i = 0; i < weight; i++)
            {
                pool.Add(id);
            }
        }

        return pool[rng.NextInt(0, pool.Count)];
    }

    private static string? PickBaseline(UnitRuntime unit, BattleDirector director)
    {
        List<string> usable = UsableSkills(unit, director);
        UnitRuntime? lowest = director.Enemy.UnitsInSlotOrder().OrderBy(u => u.CurrentHp).FirstOrDefault();
        if (lowest is not null)
        {
            int lowestSlot = director.Enemy.UnitAtPosition(lowest.Id) ?? -1;
            string? covering = usable.FirstOrDefault(id =>
            {
                SkillTemplateConfig s = C.Skills.Get(id);
                if (!s.Tags.Contains(FuncTag.Output) || s.Damage is null)
                {
                    return false;
                }

                IReadOnlyList<int> targets = SkillTargetResolver.Resolve(s, unit.Id, director.Enemy, director.Player);
                return targets.Contains(lowestSlot);
            });
            if (covering is not null)
            {
                return covering;
            }
        }

        return usable.FirstOrDefault(id => C.Skills.Get(id).Tags.Contains(FuncTag.Output)) ?? usable.FirstOrDefault();
    }

    private static List<string> UsableSkills(UnitRuntime unit, BattleDirector director)
    {
        var usable = new List<string>();
        string[] owned = C.Skills.Skills.Where(s => s.OwnerUnit == unit.Id.Value).Select(s => s.Id).ToArray();
        for (int i = 0; i < owned.Length; i++)
        {
            string skillId = owned[i];
            SkillTemplateConfig skill = C.Skills.Get(skillId);
            Availability av = C.Resolver.Resolve(new SkillUseContext(skill, unit.Id,
                director.Player, director.Enemy, new HashSet<string>(owned), C.Runtime, IsEnemy: false));
            if (av.Reason == AvailabilityReason.Ok)
            {
                usable.Add(skillId);
            }
        }

        return usable;
    }

    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }
}