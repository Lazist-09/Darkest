// 🔴 从 SkillExecutor.cs 拆出（用户红线：程序文件 <=600 行 · 架构 file_size_split §1.2）
//    本文件 = 伤害路径：ExecuteDamagePath（动作级 / 逐目标两臂）· ApplyObstacleDamage（障碍受击结算）·
//    FallbackObstacleDamage（障碍回落扣减量）· 只搬家、零行为改动 ✓
//    依赖主类私有成员（实测扫描本文件得出）：_pipeline, _log
//      ＋ 映射辅助 IsAoe / TargetSide / AxisString / FlatMultipliers / ResolvedMultipliers /
//        MapEffects / MapDisplacement（在 SkillExecutor.PipelineMapping.cs）✓

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

public sealed partial class SkillExecutor
{
    // ------------------------------------------------------------------

    private void ExecuteDamagePath(SkillTemplateConfig skill, UnitId caster,
        FormationBoard player, FormationBoard enemy,
        int[] targets, MoraleEffectRequest[] explicitMorale, IRngProvider rng, int logMark)
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
                explicitMorale.Length > 0 ? explicitMorale : null, skill.BonusVsMarkedPercent);
            _pipeline.Execute(fixture, player, enemy, rng);
            ApplyObstacleDamage(logMark, skill, targetBoard, targets, rng); // 🔴 障碍受击（动作级一次）✓
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
                MapEffects(skill), disp, explicitMorale.Length > 0 ? explicitMorale : null, skill.BonusVsMarkedPercent);
            _pipeline.Execute(fixture, player, enemy, rng);
            ApplyObstacleDamage(logMark, skill, targetBoard, new[] { slot }, rng); // 🔴 障碍受击（逐目标）✓
        }
    }

    /// <summary>
    /// 🔴 **障碍受击结算**（2026-09-20 接线）：让"挡路的木箱/石堆"**真的能被打掉**。
    /// <para>**为什么必须补这一刀**：`DamagePipeline` 遇 `SlotState.Blocked` 直接 `continue`
    /// （"障碍 M2 不结算伤害/状态"）⇒ 实测**全仓无任何障碍扣血调用点** ⇒
    /// `FormationBoard.TryGetObstacleHp` / `RemoveObstacle` **只有测试在调**，
    /// 实机里障碍 = **纯无敌墙**（与 `ObstacleRuntime` 注释"只有血量的占位角色"矛盾）⚠️</para>
    /// <para>**纪律 V（展示值 == 消费值）**：本方法**不重掷骰** —— 它只**读本次区间新产生的
    /// `HitEvent`**（已经写进日志的那次判定），据其 `Hit` 决定打没打中 ⇒ **数字完全可从事件流复算** ✓</para>
    /// <para>**伤害量来源**：优先用本次实际 `DamageEvent.Amount`（已经过防御/护盾/暴击的**结算值**）；
    /// 若该目标无 `DamageEvent`（例：纯位移/纯效果技能打在障碍上）⇒ 回落到**技能自身最小段伤害**
    /// 作为确定性的扣减量（不掷骰 ⇒ 仍可复算）✓</para>
    /// <para>**不做的**：障碍**不参与士气/虚弱/死门**、**免疫 debuff**（GDD §1.1）⇒ 本方法只碰 HP ✓</para>
    /// </summary>
    /// <param name="logMark2">本次结算开始前的事件下标（只读这之后的新事件）✓</param>
    private void ApplyObstacleDamage(int logMark2, SkillTemplateConfig skill,
        FormationBoard targetBoard, int[] targets, IRngProvider rng)
    {
        foreach (int slot in targets)
        {
            if (targetBoard.GetSlot(slot) != SlotState.Blocked)
            {
                continue; // 只处理障碍槽（非障碍由管线自理）✓
            }

            // ① 读本次区间里**针对该槽**的命中判定（不重掷 ⇒ 纪律 V）——
            //    `HitEvent.Target` 对障碍槽为 null（障碍无 UnitId）⇒ 只能按"本次是否有命中"整体取用 ✓
            bool? hit = null;
            int dealt = 0;
            for (int i = logMark2; i < _log.Events.Count; i++)
            {
                if (_log.Events[i] is HitEvent h && h.Target is null)
                {
                    hit = h.Hit; // 碰撞在"有碰撞无单位"的槽上 = 障碍（唯一契约：障碍槽内无 UnitRuntime）✓
                }

                if (_log.Events[i] is DamageEvent d && d.Target is null && d.Amount > 0)
                {
                    dealt += d.Amount;
                }
            }

            if (hit is false)
            {
                _log.Append(new EffectEvent(null, $"obstacle_miss@{slot}", 100.0, false)); // 没打中：障碍不掉血 ✓
                continue;
            }

            bool hasRoll = hit is not null;
            int amount = dealt > 0 ? dealt : FallbackObstacleDamage(skill);
            if (amount <= 0 && !hasRoll)
            {
                continue; // 无命中判定、又无伤害段 ⇒ 不是攻击（例：纯支援/移动）⇒ 不碰障碍 ✓
            }

            bool stillThere = targetBoard.DamageObstacle(slot, amount);
            _log.Append(new EffectEvent(null, $"obstacle_damage@{slot}", 100.0, true, null));
            _log.Append(new EffectEvent(null,
                stillThere ? $"obstacle_stand@{slot}" : $"obstacle_destroyed@{slot}", 100.0, true, null));
        }
    }

    /// <summary>障碍受击的**回落扣减量**：技能伤害段的最小 `multiplier`（不掷骰、不读来源属性 ⇒ 可复算）✓</summary>
    private static int FallbackObstacleDamage(SkillTemplateConfig skill)
    {
        if (skill.Damage is null)
        {
            return 0;
        }

        int best = int.MaxValue;
        foreach (DamageSegment seg in skill.Damage.Segments)
        {
            if (seg.Multiplier is { } m && m > 0 && m < best)
            {
                best = (int)Math.Round(m);
            }
        }

        return best == int.MaxValue ? 0 : Math.Max(1, best);
    }
}
