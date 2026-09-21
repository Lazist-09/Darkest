using System;
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
///
/// **C-1（v0.99）潜行门禁**（权威规格：`darkestdungeon.wiki.gg/wiki/Stealth_(Darkest_Dungeon)`；
/// 一手实据：`buff_defs.json` stealth 条 source）：
/// · 默认（`buffs == null`）**不做任何潜行过滤** —— 兼容全部历史调用点与确定性基线；
/// · 传入台账后：① 潜行者**不可被直接指定**（候选池剔除掉潜行者本身）；② 含潜行者的多目标技能
///   **仅当至少存在 1 个非潜行可选目标时才放行**，否则整个技能不可用（wiki 原文：must be targeting
///   at least one non-Stealthed target ／ Grapeshot 对全体潜行的 Swine Gorers 不可用）；
///   ③ 带 `ignore_stealth` 标签的技能**无视**①②（原版 `.ignore_stealth true`；De-Stealth 在 `SkillExecutor`）；
///   ④ **障碍位不受影响**（障碍无 buff，天然非潜行；#73/#77/#105）；
///   ⑤ 纯 `self` / `any_ally`（队友侧）范围**不做潜行过滤**（wiki 只规定敌方单体不可指定；
///      我方自选/队友选择不受限 —— 见 dd_reference 已登记的对账项）。
/// </summary>
public static class SkillTargetResolver
{
    /// <summary>潜行状态标记名（`buff_defs.json` 的 `state_flag.effect`）。</summary>
    public const string StealthFlag = "stealth";

    public static IReadOnlyList<int> Resolve(
        SkillTemplateConfig skill,
        UnitId caster,
        FormationBoard player,
        FormationBoard enemy,
        IBuffLedger? buffs = null)
    {
        FormationBoard allyBoard = player.UnitAtPosition(caster) is not null ? player : enemy;
        FormationBoard targetBoard = skill.Target.Side == "enemy" ? enemy : player;
        // C-1：带 ignore_stealth 标签 ⇒ 无视 ①②（④ 的移除时机由 SkillExecutor 负责）
        bool ignoresStealth = skill.Tags.Contains(FuncTag.IgnoreStealth);

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
                        hits.Add(pos); // Occupied + Blocked 都算非空（障碍当目标 #71 行；障碍无 buff ⇒ 不受潜行影响）
                    }
                }

                // C-1 ①②：多目标技能在"至少要打到一个非潜行者"的前提下**可穿过**潜行（wiki 明文）；
                //          命中潜行者**不解除**潜行（wiki: AOE will not de-stealth）
                if (buffs is null || ignoresStealth)
                {
                    return hits;
                }

                bool anyVisible = hits.Any(p => !IsStealthed(buffs, targetBoard, p));
                return anyVisible ? hits : System.Array.Empty<int>();
            }

            case SkillTargetScope.Self:
                return allyBoard.UnitAtPosition(caster) is { } selfSlot ? new[] { selfSlot } : System.Array.Empty<int>();

            case SkillTargetScope.AnyAlly:
            case SkillTargetScope.Team:
                // C-1 ⑤：队友侧**不做**潜行过滤（wiki 只限制"不可被指定"的敌方单体；我方自选/队友
                // 选择不受限）。台障位一并保留（includeObstacle，原行为）。
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

            case SkillTargetScope.MoveRange:
            {
                // 池外移动（#180/O-41）：候选 = 施法者侧【战斗位】中自身 ±1..N 且【被占用】的槽位
                // （障碍位可选中=推动障碍 #22；空位不可选 → NoTarget #21；不含自身与支援位；升序）
                int? fromSlot = allyBoard.UnitAtPosition(caster);
                if (fromSlot is not { } from)
                {
                    return System.Array.Empty<int>();
                }

                // F1（#191）：移动射程从【单位】读（units.move_distance），技能不再自带 distance
                int n = allyBoard.UnitRuntimeAt(from) is { } mover
                    ? mover.Base.MovementRange
                    : skill.Target.Distance ?? 0;
                var moves = new List<int>();
                for (int pos = 1; pos <= allyBoard.SlotCount; pos++)
                {
                    if (allyBoard.SlotKindAt(pos) != SlotKind.Combat || pos == from)
                    {
                        continue;
                    }

                    int dist = Math.Abs(pos - from);
                    if (dist >= 1 && dist <= n && allyBoard.GetSlot(pos) != SlotState.Empty)
                    {
                        moves.Add(pos);
                    }
                }

                return moves; // 槽升序
            }

            default:
                return System.Array.Empty<int>();
        }
    }

    /// <summary>
    /// C-1：槽位上的单位是否处于潜行（`state_flag: stealth`）。
    /// 空位 / 障碍位（无单位运行时）⇒ false —— **障碍必须继续可被选中**（#73/#77/#105 不能被误伤）。
    /// </summary>
    private static bool IsStealthed(IBuffLedger? buffs, FormationBoard board, int pos)
        => buffs is not null
           && board.UnitRuntimeAt(pos) is { } u
           && buffs.HasStateFlag(u.Id, StealthFlag);
}