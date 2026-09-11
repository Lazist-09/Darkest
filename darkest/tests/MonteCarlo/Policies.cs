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

    /// <summary>单单位决策（P2/O-42 规则化启发式；Baseline 保持独立对照）。rng 保留以兼容 actor 节拍签名。</summary>
    public static PlayerDecision DecideForUnit(PolicyKind kind, UnitRuntime unit, BattleDirector director, IRngProvider rng)
    {
        _ = rng; // P2：SemiRandom 不再权重随机，改为可解释规则
        if (kind == PolicyKind.Baseline)
        {
            string? skillId = PickBaseline(unit, director);
            return PlayerDecision.Skill(skillId ?? "", LowestHpTargetSlot(skillId, unit, director));
        }

        return DecideRuleBased(unit, director);
    }

    /// <summary>
    /// P2 + S5（#211/#212）启发式：**SP 花费必须有目的，禁止无条件花**。
    /// ①保命（HP%&lt;30% 且 SP≥1）→ ②救崩溃（战斗位虚弱 且 SP≥2 → 增援 −2）→ ③续航（HP%&lt;60% 且 SP≥2，
    /// 治后保留 ≥1）→ ④士气（不消耗 SP）→ ⑤攒点/待命（支援位无可用时不花）。
    /// **战斗位技能永不消耗 SP**，因此战斗位仍按"输出集火 → 移动"照常行动。
    /// </summary>
    private static PlayerDecision DecideRuleBased(UnitRuntime unit, BattleDirector director)
    {
        bool supportSlot = director.IsSupportSlotActor(unit.Id);
        int sp = director.SupportPoints;
        int costSkill = director.SupportCostSkill;

        // ② 救崩溃：战斗位有虚弱者且支援位有健康者且 SP ≥ cost_reinforce → 花 2 点换下（保留 #176 意图 + SP 门槛）
        if (!supportSlot && sp >= director.SupportCostReinforce && unit.Weak
            && SupportHealthyAlly(director) is { } healthy)
        {
            return PlayerDecision.Reinforce(healthy, director.Player.UnitAtPosition(unit.Id) ?? 1);
        }

        List<string> usable = UsableSkills(unit, director);
        if (supportSlot)
        {
            // 支援位：只做"值得花 SP"的事；否则攒点/待命
            string? healSkill = usable.FirstOrDefault(id => C.Skills.Get(id).HealFixed is not null);
            if (sp >= costSkill && healSkill is not null)
            {
                // ① 保命：HP% < 30% → 必花 1 点
                int? critical = LowestHpPctAlly(director, threshold: 0.30);
                if (critical is { } crit)
                {
                    return PlayerDecision.Skill(healSkill, crit);
                }

                // ③ 续航：HP% < 60% 且 SP ≥ cost + 1（治完保留 ≥1 点）
                int? wounded = LowestHpPctAlly(director, threshold: 0.60);
                if (wounded is { } w && sp >= costSkill + 1)
                {
                    return PlayerDecision.Skill(healSkill, w);
                }
            }

            // ④ 士气：不消耗 SP 的支援（鼓舞类）→ 直接做
            string? morale = usable.FirstOrDefault(id => C.Skills.Get(id).MoraleEffects.Count > 0 && C.Skills.Get(id).HealFixed is null);
            if (morale is not null)
            {
                return PlayerDecision.Skill(morale, null);
            }

            return PlayerDecision.None; // ⑤ 攒点 → 待命（不花 SP）
        }

        if (usable.Count == 0)
        {
            return PlayerDecision.None;
        }

        // 战斗位：技能零 SP 消耗 → 治疗/输出照常
        string? heal = usable.FirstOrDefault(id => C.Skills.Get(id).HealFixed is not null);
        if (heal is not null && LowestHpPctAlly(director) is { } woundedAlly)
        {
            return PlayerDecision.Skill(heal, woundedAlly);
        }

        List<string> nonHeal = usable.Where(id => C.Skills.Get(id).HealFixed is null).ToList();

        // ① 输出：优先"能打到全场最低 HP 敌人"的单体（集火收残）；否则 AOE（≥2 目标时全池）；目标平局取槽号小
        List<string> outputs = nonHeal.Where(id => C.Skills.Get(id).Damage is not null).ToList();
        if (outputs.Count > 0)
        {
            UnitRuntime? weakest = director.Enemy.UnitsInSlotOrder()
                .OrderBy(u => u.CurrentHp)
                .ThenBy(u => director.Enemy.UnitAtPosition(u.Id) ?? 9)
                .FirstOrDefault();
            int? weakestSlot = weakest is null ? null : director.Enemy.UnitAtPosition(weakest.Id);

            foreach (string id in outputs)
            {
                SkillTemplateConfig s = C.Skills.Get(id);
                if (s.Tags.Contains(FuncTag.Aoe))
                {
                    continue;
                }

                IReadOnlyList<int> cand = SkillTargetResolver.Resolve(s, unit.Id, director.Player, director.Enemy);
                if (weakestSlot is { } ws && cand.Contains(ws))
                {
                    return PlayerDecision.Skill(id, ws); // 集火
                }
            }

            string? aoe = outputs.FirstOrDefault(id => C.Skills.Get(id).Tags.Contains(FuncTag.Aoe));
            if (aoe is not null)
            {
                return PlayerDecision.Skill(aoe, null); // AOE 全池（无需指定）
            }

            string any = outputs[0];
            IReadOnlyList<int> cands = SkillTargetResolver.Resolve(C.Skills.Get(any), unit.Id, director.Player, director.Enemy);
            int? fallbackTarget = cands.OrderBy(p => director.Enemy.UnitRuntimeAt(p)?.CurrentHp ?? int.MaxValue)
                .ThenBy(p => p).Select(p => (int?)p).FirstOrDefault();
            return PlayerDecision.Skill(any, fallbackTarget);
        }

        // ② 移动条件化：无输出可用时才考虑，且落点须"可用技能数更多"
        string moveId = "move"; // F1（#191）：池外通用移动（所有我方单位共用；距离从单位读）
        if (usable.Contains(moveId) && BestMovePos(unit, director, usable) is { } mv)
        {
            return PlayerDecision.Skill(moveId, mv);
        }

        // ⑤ 控制/位移：对己方威胁最高（攻击力最高）的敌人
        string? ctrl = nonHeal.FirstOrDefault(id => C.Skills.Get(id).Tags.Contains(FuncTag.Control));
        if (ctrl is not null)
        {
            IReadOnlyList<int> cand = SkillTargetResolver.Resolve(C.Skills.Get(ctrl), unit.Id, director.Player, director.Enemy);
            int? tough = cand.OrderBy(p => -(director.Enemy.UnitRuntimeAt(p)?.Base.Attack ?? 0))
                .ThenBy(p => p).Select(p => (int?)p).FirstOrDefault();
            return PlayerDecision.Skill(ctrl, tough);
        }

        return PlayerDecision.Skill(nonHeal.FirstOrDefault() ?? usable[0], null);
    }

    /// <summary>S5（#211）②：支援位中有健康者（HP% ≥ 60%）→ 返回其槽位，供"救崩溃"增援换下虚弱战斗位。</summary>
    private static int? SupportHealthyAlly(BattleDirector director)
    {
        foreach (int slot in director.Player.Layout.SupportSlots)
        {
            UnitRuntime? u = director.Player.UnitRuntimeAt(slot);
            if (u is not null && !u.Weak && u.MaxHp > 0 && (double)u.CurrentHp / u.MaxHp >= 0.6)
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>③ 最低 HP% 友方（含自身）；无真伤员（低于 threshold）→ null。</summary>
    private static int? LowestHpPctAlly(BattleDirector director, double threshold = 0.6)
    {
        int? best = null;
        double bestPct = threshold;
        foreach (UnitRuntime ally in director.Player.UnitsInSlotOrder())
        {
            double pct = ally.MaxHp > 0 ? (double)ally.CurrentHp / ally.MaxHp : 1.0;
            int? slot = director.Player.UnitAtPosition(ally.Id);
            if (slot is null || pct >= bestPct)
            {
                continue;
            }

            bestPct = pct;
            best = slot;
        }

        return best;
    }

    /// <summary>② 移动条件化：候选落点中"可用技能数"最大且严格优于当前位；无收益 → null。</summary>
    private static int? BestMovePos(UnitRuntime unit, BattleDirector director, List<string> usable)
    {
        int cur = director.Player.UnitAtPosition(unit.Id) ?? -1;
        if (cur < 1)
        {
            return null;
        }

        int CountAt(int pos) => usable.Count(id =>
            !C.Skills.Get(id).PoolExternal && C.Skills.Get(id).SelfSlots.Allows(pos));

        int bestCount = CountAt(cur);
        int? bestPos = null;
        foreach (int pos in SkillTargetResolver.Resolve(C.Skills.Get("move"), unit.Id, director.Player, director.Enemy))
        {
            int c = CountAt(pos);
            if (c > bestCount)
            {
                bestCount = c;
                bestPos = pos;
            }
        }

        return bestPos;
    }

    /// <summary>增援（#176/#181 + P2④）：战斗位【虚弱 或 HP% &lt; 30%】→ 换入健康支援位（优先军医）到其槽位。</summary>
    private static (int bSlot, int xSlot)? ReinforceNeeded(UnitRuntime unit, BattleDirector director)
    {
        if (director.SwappedThisRound)
        {
            return null;
        }

        int aPos = director.Player.UnitAtPosition(unit.Id) ?? -1;
        if (aPos is < 1 or > 4)
        {
            return null; // 发起者须在战斗位
        }

        bool critical = unit.MaxHp > 0 && (double)unit.CurrentHp / unit.MaxHp < 0.30;
        if (!unit.Weak && !critical)
        {
            return null;
        }

        int priority(string archetype) => archetype switch { "medic" => 0, "commissar" => 1, _ => 2 };
        int? best = null;
        foreach (int slot in director.Player.Layout.SupportSlots)
        {
            UnitRuntime? ally = director.Player.UnitRuntimeAt(slot);
            if (ally is null || ally.Weak || ally.Id == unit.Id)
            {
                continue;
            }

            if (best is null || priority(ally.ArchetypeId) < priority(director.Player.UnitRuntimeAt(best.Value)!.ArchetypeId))
            {
                best = slot;
            }
        }

        return best is { } bSlot ? (bSlot, aPos) : null;
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

        IReadOnlyList<int> candidates = SkillTargetResolver.Resolve(s, unit.Id, director.Player, director.Enemy); // 参数序：player 板在前
        UnitRuntime? lowest = director.Enemy.UnitsInSlotOrder().OrderBy(u => u.CurrentHp).FirstOrDefault();
        if (lowest is null)
        {
            return null;
        }

        int? lowestSlot = director.Enemy.UnitAtPosition(lowest.Id);
        return lowestSlot is { } ls && candidates.Contains(ls) ? ls : null;
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

                IReadOnlyList<int> targets = SkillTargetResolver.Resolve(s, unit.Id, director.Player, director.Enemy); // 参数序：player 板在前
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
        // F1（#191）：池内技能（owner 匹配）+ 池外通用技能（owner 不绑，人人可用）
        string[] owned = C.Skills.Skills
            .Where(s => (s.OwnerUnit == unit.ArchetypeId || s.PoolExternal))
            .Select(s => s.Id).ToArray();
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