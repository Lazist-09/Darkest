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
            string? skillId = kind == PolicyKind.SemiRandom
                ? PickSemiRandom(unit, director, rng)
                : PickBaseline(unit, director);
            if (skillId is not null)
            {
                actions.Add(new PlayerAction(unit.Id, skillId));
            }
        }

        return actions;
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