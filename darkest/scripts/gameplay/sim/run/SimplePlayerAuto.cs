using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// 🔴 **冒烟用的小型"玩家自动"策略**（`#308`⑧：让夜袭战斗能被**真的打完**，从而走完玩家路径做功能级验收）。
///
/// ⚠️ 定位说明（**不冒充正式 AI**）：
/// · 它与测试里的 `MonteCarlo.Policies` **同源规则**（可用技能 + 集火最残敌人），但**只保留输出主线**
///   （不实现支援位回 SP、增援换位等）—— **目的只是让冒烟能打完一场**，**不用于任何数值读数**。
/// · 走内核件（`SkillsConfig` / `SkillUseResolver` / `SkillTargetResolver`）⇒ **零 Godot 引用**，可单测。
/// </summary>
public sealed class SimplePlayerAuto
{
    private readonly SkillsConfig _skills;
    private readonly SkillRuntimeState _runtime = new();
    private readonly SkillUseResolver _resolver;

    public SimplePlayerAuto(SkillsConfig skills)
    {
        _skills = skills;
        _resolver = new SkillUseResolver(skills);
    }

    /// <summary>该单位本回合的决策：**能打到"最残敌人"的输出技能优先**，否则任一输出，再否则待命。</summary>
    public PlayerDecision Decide(UnitRuntime unit, BattleDirector director)
    {
        string[] owned = _skills.Skills
            .Where(s => s.OwnerUnit == unit.ArchetypeId || s.PoolExternal)
            .Select(s => s.Id)
            .ToArray();

        var usable = new List<string>();
        var ownedSet = new HashSet<string>(owned);
        foreach (string id in owned)
        {
            SkillTemplateConfig skill = _skills.Get(id);
            Availability av = _resolver.Resolve(new SkillUseContext(skill, unit.Id,
                director.Player, director.Enemy, ownedSet, _runtime, IsEnemy: false));
            if (av.Reason == AvailabilityReason.Ok)
            {
                usable.Add(id);
            }
        }

        if (usable.Count == 0)
        {
            return PlayerDecision.None;
        }

        UnitRuntime? weakest = director.Enemy.UnitsInSlotOrder()
            .OrderBy(u => u.CurrentHp)
            .ThenBy(u => director.Enemy.UnitAtPosition(u.Id) ?? 9)
            .FirstOrDefault();
        int? weakestSlot = weakest is null ? null : director.Enemy.UnitAtPosition(weakest.Id);

        // ① 输出：优先"能覆盖最残敌人"的单体；AOE 直接放
        foreach (string id in usable)
        {
            SkillTemplateConfig s = _skills.Get(id);
            if (s.Damage is null)
            {
                continue;
            }

            if (s.Tags.Contains(FuncTag.Aoe))
            {
                return PlayerDecision.Skill(id, null);
            }

            IReadOnlyList<int> cand = SkillTargetResolver.Resolve(s, unit.Id, director.Player, director.Enemy);
            if (weakestSlot is { } ws && cand.Contains(ws))
            {
                return PlayerDecision.Skill(id, ws); // 集火
            }
        }

        // ② 退一步：任一输出（打第一个合法目标）
        string? anyOutput = usable.FirstOrDefault(id => _skills.Get(id).Damage is not null);
        if (anyOutput is not null)
        {
            IReadOnlyList<int> cand = SkillTargetResolver.Resolve(_skills.Get(anyOutput), unit.Id,
                director.Player, director.Enemy);
            return PlayerDecision.Skill(anyOutput, cand.Select(p => (int?)p).FirstOrDefault());
        }

        // ③ 没有输出 ⇒ 用第一个可用技能（多为支援/移动），否则待命
        return PlayerDecision.Skill(usable[0], null);
    }
}
