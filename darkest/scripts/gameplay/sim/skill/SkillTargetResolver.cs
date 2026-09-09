using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;

namespace Darkest.Gameplay.Sim.Skill;

/// <summary>
/// 技能运行时台账（T-M3-03 判定④数据源）：CD 剩余 / 每场已用次数。
/// 由导演在结算后更新（M3-06 SkillExecutor 消耗）；Resolver 只读。
/// </summary>
public sealed class SkillRuntimeState
{
    private readonly Dictionary<(UnitId, string), int> _cooldownRemaining = new();
    private readonly Dictionary<(UnitId, string), int> _usedPerBattle = new();

    public int CooldownRemaining(UnitId unit, string skillId)
        => _cooldownRemaining.TryGetValue((unit, skillId), out int v) ? v : 0;

    public int UsedPerBattle(UnitId unit, string skillId)
        => _usedPerBattle.TryGetValue((unit, skillId), out int v) ? v : 0;

    /// <summary>结算后调用：置 CD / 累计次数（回合递减由导演回合钩子负责，M4 完善；M3 只读判定）。</summary>
    public void RecordUse(UnitId unit, SkillTemplateConfig skill)
    {
        if (skill.UseLimit.Type == UseLimitType.Cooldown && skill.UseLimit.Value is { } cd)
        {
            _cooldownRemaining[(unit, skill.Id)] = cd;
        }

        if (skill.UseLimit.Type == UseLimitType.PerBattle)
        {
            _usedPerBattle[(unit, skill.Id)] = UsedPerBattle(unit, skill.Id) + 1;
        }
    }
}

/// <summary>
/// 技能目标解析（T-M3-05）：scope 五类 → 候选槽位 → 占用过滤（含障碍）→ 槽号升序的有序目标列表；
/// 障碍计为非空位（#73/#77/#105）但免疫附加状态（调用方按类型跳过）；空 → 空列表（NoTarget 由 Resolver 判定）。
/// 零随机、零写状态。
/// </summary>
public static class SkillTargetResolver
{
    public static IReadOnlyList<int> Resolve(
        SkillTemplateConfig skill,
        UnitId caster,
        FormationBoard player,
        FormationBoard enemy)
    {
        FormationBoard allyBoard = player.UnitAtPosition(caster) is not null ? player : enemy;
        FormationBoard targetBoard = skill.Target.Side == "enemy" ? enemy : player;

        switch (skill.Target.Scope)
        {
            case SkillTargetScope.Slots:
            {
                int max = targetBoard.SlotCount;
                var hits = new List<int>();
                foreach (int pos in skill.Target.Slots ?? System.Array.Empty<int>())
                {
                    if (pos < 1 || pos > max)
                    {
                        continue; // 数据层 P2 已拦；此处防御性跳过（不静默放行进结算）
                    }

                    if (targetBoard.GetSlot(pos) != SlotState.Empty)
                    {
                        hits.Add(pos); // Occupied + Blocked 都算非空（障碍当目标）
                    }
                }

                return hits; // 已按槽升序（输入 slots 升序）
            }

            case SkillTargetScope.Self:
                return allyBoard.UnitAtPosition(caster) is { } selfSlot ? new[] { selfSlot } : System.Array.Empty<int>();

            case SkillTargetScope.AnyAlly:
            case SkillTargetScope.Team:
                return allyBoard.OccupiedPositions(includeObstacle: true).OrderBy(p => p).ToArray();

            case SkillTargetScope.AdjacentAllyAndSelf:
            {
                if (allyBoard.UnitAtPosition(caster) is not { } c)
                {
                    return System.Array.Empty<int>();
                }

                var list = new SortedSet<int> { c };
                if (c - 1 >= 1 && allyBoard.GetSlot(c - 1) != SlotState.Empty)
                {
                    list.Add(c - 1);
                }

                if (c + 1 <= allyBoard.SlotCount && allyBoard.GetSlot(c + 1) != SlotState.Empty)
                {
                    list.Add(c + 1);
                }

                return list.ToArray();
            }

            default:
                return System.Array.Empty<int>();
        }
    }
}