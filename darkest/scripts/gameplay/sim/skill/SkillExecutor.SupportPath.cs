// 🔴 从 SkillExecutor.cs 拆出（用户红线：程序文件 <=600 行 · 架构 file_size_split §1.2）
//    本文件 = 无伤害技能的支援最小路径：ExecuteSupportPath（固定值治疗 / 暴击治疗 / 效果挂载 / 显式士气）·
//    ResolveEffectTargets（apply_to 挂载对象）· ApplyMoraleToTargets · 共用小助手 ResolveRuntime / ApplyStatMod
//    · 只搬家、零行为改动 ✓
//    依赖主类私有成员（实测扫描本文件得出）：_buffs, _balance, _log, _pipeline, _moraleEvents

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
                    // F2（#193）自私 proc：33% 拒绝这一次治疗/鼓舞（本次无效；施法者行动照常消耗）
                    if (Darkest.Gameplay.Sim.Buffs.AfflictionProcs.Triggered(_buffs, _balance, target.Id, "affliction_selfish", rng, _log))
                    {
                        _log.Append(new EffectEvent(target.Id, "selfish_refuse", 100.0, true));
                        continue;
                    }

                    // D7（#209）暴击治疗：概率来自 `tuning.heal_crit`（原硬编码：单体 12% / 多目标 5%）→ 治疗量 ×2 + 目标士气（`morale_events`）
                    double critRoll = rng.NextPercent();
                    _log.Append(new RngDraw(rng.DrawCount, critRoll));
                    double critChance = targets.Length > 1 ? _balance.HealCritMultiPercent : _balance.HealCritSinglePercent;
                    bool critHeal = critRoll < critChance;

                    // 🆕 **M2 激活（架构裁 (丙) · 第 1 条）**：`hp_heal_percent` ⇒ 治疗量 ×(1 + pct/100) ✓
                    //    语义（原版）：**施法者**的"治疗量"修正 ⇒ 故读 `caster` 的 buff 修正 ✓
                    //    🔴 无该 buff ⇒ pct = 0 ⇒ **行为与激活前逐位相同** ✓（前后读数见 reports/m2_activation_readings.md）
                    int healPct = _buffs?.PercentModAny(caster, "hp_heal_percent") ?? 0;
                    int baseHeal = critHeal ? heal * 2 : heal;
                    int scaledHeal = HealAmount.Scale(baseHeal, healPct);   // 🆕 算法在 HealAmount（可单测 ✓）
                    if (healPct != 0)
                    {
                        _log.Append(new EffectEvent(caster, "hp_heal_percent", healPct, true, caster));
                    }

                    int healed = Math.Min(target.MaxHp - target.CurrentHp, scaledHeal);
                    target.CurrentHp += healed;
                    _log.Append(new HealEvent(target.Id, healed, caster, skill.Id)); // G0：治疗来源 + 技能
                    if (critHeal)
                    {
                        _pipeline.Morale.Apply(target, _moraleEvents.Get("critical_heal").Delta, "critical_heal", _log);
                        _log.Append(new EffectEvent(target.Id, "critical_heal", 100.0, true, caster));
                    }
                }
            }

            foreach (EffectSpec effect in skill.Effects)
            {
                // 🔴 补欠账（`data_schema.md:259`）：**`apply_to` = effects 的【挂载对象】**
                //    契约：缺省 = `targets`；可选 `self`（盾墙的自身物防 +6）／`ally_targets`（守护挂相邻友方）
                //    实测：此前**处理器完全不读它** ⇒ 5 个 `apply_to=self` 的技能 + 1 个 `apply_to=team`
                //    的挂载语义未生效 ⚠️（数据里实际用到 `self` ×5 ／ **`team` ×1**）
                //    ⚠️ **契约与数据不一致**：数据用了 `team`，而契约只列了 `self`/`ally_targets` ⇒ 已上报待对账
                IReadOnlyList<UnitRuntime> effectTargets = ResolveEffectTargets(effect.ApplyTo, allyBoard, targetBoard, slot, caster);
                foreach (UnitRuntime target in effectTargets)
                {
                    if (effect.Type is SkillEffectType.StatMod && effect.Stat is not null)
                    {
                        ApplyStatMod(target, effect);
                        _log.Append(new EffectEvent(target.Id, "stat_mod", 100.0, true, caster));
                        _log.Append(new StatModEvent(target.Id, effect.Stat ?? "?", effect.Delta ?? 0, effect.DurationRounds ?? 0)); // G0
                    }

                    if (effect.Type is SkillEffectType.Shield && effect.Charges is { } charges)
                    {
                        _buffs?.AddCharged(target.Id, "shield", charges); // #156 按次数（铁壁 2 次）
                        _log.Append(new EffectEvent(target.Id, "shield", 100.0, true));
                    }

                    if (effect.Type is SkillEffectType.Taunt)
                    {
                        // F1（#192）：嘲讽挂到【自己】身上（target.scope=self）；敌方 AI 按 buff holder 识别 → 可插拔
                        _buffs?.Add(target.Id, "taunt", source: null);
                        _log.Append(new EffectEvent(target.Id, "taunt", 100.0, true));
                    }

                    if (effect.Type is SkillEffectType.Stealth)
                    {
                        // C-1（v0.99）：潜行挂到目标身上（原版 `.stealth 1 .duration 2`，playwright:26 挂 performer=自己；
                        // 本实现走 apply_to 通用挂载 —— 缺省=目标列表，`self`=施法者，与 taunt 同构）。
                        // 时长由 buff_defs.json 的 stealth.duration.value 决定（2 回合，placeholder）。
                        _buffs?.Add(target.Id, SkillTargetResolver.StealthFlag, source: caster);
                        _log.Append(new EffectEvent(target.Id, SkillTargetResolver.StealthFlag, 100.0, true, caster));
                    }
                }
                // guard_attach/next_attack_boost：数据已录，钩子执行归后续包
            }
        }

        foreach (MoraleEffectRequest m in explicitMorale)
        {
            ApplyMoraleToTargets(m, allyBoard, targetBoard, targets, caster);
        }
    }

    /// <summary>
    /// **`apply_to` 的挂载对象**（`data_schema.md:259`）：
    /// · 缺省 / `targets` ⇒ 技能解析出的目标（原行为）
    /// · `self` ⇒ **施法者自己**（盾墙自身物防 +6 ／ 殊死一搏的自身 buff）
    /// · `team` ⇒ **我方全体存活单位**（总动员：全队 buff；⚠️ 该取值**不在契约列表里**，已上报对账）
    /// · `ally_targets` ⇒ 与 `targets` 同处理（契约提到但**数据未使用**；待契约澄清后细化"相邻友方"）
    /// </summary>
    private IReadOnlyList<UnitRuntime> ResolveEffectTargets(string? applyTo, FormationBoard allyBoard,
        FormationBoard targetBoard, int slot, UnitId caster)
    {
        switch (applyTo)
        {
            case "self":
            {
                int? pos = allyBoard.UnitAtPosition(caster) ?? targetBoard.UnitAtPosition(caster);
                UnitRuntime? me = pos is { } p
                    ? allyBoard.UnitRuntimeAt(p) ?? targetBoard.UnitRuntimeAt(p)
                    : null;
                return me is null ? Array.Empty<UnitRuntime>() : new[] { me };
            }
            case "team":
                return allyBoard.UnitsInSlotOrder().Where(u => u.CurrentHp > 0).ToArray();
            default:
            {
                UnitRuntime? target = ResolveRuntime(allyBoard, targetBoard, slot);
                return target is null ? Array.Empty<UnitRuntime>() : new[] { target };
            }
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

    private static UnitRuntime? ResolveRuntime(FormationBoard a, FormationBoard b, int slot)
        => a.SlotCount >= slot ? a.UnitRuntimeAt(slot) ?? b.UnitRuntimeAt(slot) : b.UnitRuntimeAt(slot);

    private static void ApplyStatMod(UnitRuntime target, EffectSpec effect)
    {
        int delta = effect.Delta ?? 0;
        switch (effect.Stat)
        {
            case "attack": target.AttackMod += delta; break;
            case "phys_def": target.ProtMod += delta; break;
            case "resilience": target.ResilienceMod += delta; break;
            case "speed": target.SpeedMod += delta; break;
        }
    }
}
