using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;

namespace Darkest.Core.Events;

// M2 结算事件族（T-M2-04~09）：一律不可变 record（值语义，供回放/统计逐事件比对）。
// 每次随机抽取：先追加 RngDraw（含 DrawCount+数值），再追加对应业务事件。

/// <summary>命中判定事件（T-M2-04）。</summary>
public sealed record HitEvent(bool Hit, int HitRate, UnitId? Attacker, UnitId? Target) : BattleEvent;

/// <summary>暴击判定事件（T-M2-05）。</summary>
public sealed record CritEvent(bool Crit, UnitId? Attacker, UnitId? Target) : BattleEvent;

/// <summary>单段伤害事件（T-M2-05；多段=多条，SegmentIndex 从 0 起；Attacker 供 M6 输出统计）。</summary>
public sealed record DamageEvent(UnitId? Target, int Amount, double Raw, bool Crit, int SegmentIndex, string Axis, UnitId? Attacker = null, string? SkillId = null) : BattleEvent;

/// <summary>士气变动事件（T-M2-08；Delta 为净变动，NewValue 为钳制后值）。</summary>
public sealed record MoraleEvent(UnitId Unit, int Delta, string Source, int NewValue) : BattleEvent;

/// <summary>附加效果判定/施加事件（T-M2-06；无概率直挂 ActualChance=100 且无 RngDraw）。</summary>
public sealed record EffectEvent(UnitId? Target, string EffectType, double ActualChance, bool Triggered, UnitId? Source = null) : BattleEvent;

/// <summary>位移判定事件（T-M2-07；唯一 >= 例外）。</summary>
public sealed record DisplaceEvent(UnitId? Mover, int FromPos, int ToPos, bool PassedResist, bool ChainSucceeded, string Failure) : BattleEvent;

/// <summary>死门判定事件（T-M2-09）。</summary>
public sealed record DeathDoorEvent(UnitId? Unit, int SurvivePercent, double Roll, bool Survived) : BattleEvent;

/// <summary>进入虚弱事件（T-M2-09）。</summary>
public sealed record WeakEnterEvent(UnitId? Unit) : BattleEvent;

/// <summary>真死/离场事件（T-M2-09）。</summary>
public sealed record DeathEvent(UnitId? Unit, bool IsPlayer, string Cause = "unknown") : BattleEvent;

/// <summary>崩溃判定调用点（T-M2-09；完整池解析归 M4，本事件仅记录抽取）。</summary>
public sealed record CollapseRollEvent(UnitId? Unit, double Roll) : BattleEvent;

/// <summary>固定值治疗（combat_math §8，不吃攻击力；急救 12 / 群体绷带 5 / 喘息 8、10）。</summary>
public sealed record HealEvent(UnitId? Target, int Amount, UnitId? Source = null, string? SkillId = null) : BattleEvent;

/// <summary>自我伤害固定值（殊死一搏 6 / 舍身 8；可致死走死门，M4 接线）。</summary>
public sealed record SelfDamageEvent(UnitId? Unit, int Amount) : BattleEvent;

/// <summary>崩溃判定产物（T-M4-02/03）：Kind ∈ Virtue/Affliction + 落挂 buff id。</summary>
public sealed record CollapseResultEvent(UnitId? Unit, string Kind, string? BuffId) : BattleEvent;

/// <summary>回合开始事件（M5 导演，供增援/支援位/回升钩子与回放锚点）。</summary>
public sealed record RoundStartEvent(int Round) : BattleEvent;

/// <summary>超时增援事件（M5-03）：Kind=Fill（填了谁/槽位）或 Buff（满编增益）。</summary>
public sealed record ReinforcementEvent(string Kind, UnitId? Unit, int? Slot) : BattleEvent;

/// <summary>撤退结算事件（M5-04；Rate=当回合成功率数字）。</summary>
public sealed record RetreatEvent(bool Success, double Rate) : BattleEvent;

/// <summary>换位/增援事件（#41a：战斗位角色发起，消耗其本次行动；交换链结算）。</summary>
public sealed record SwapEvent(UnitId? Actor, int FromPos, int ToPos, UnitId? MovedUnit = null, string Kind = "swap") : BattleEvent;

/// <summary>#198 弹性增援间隔变动（M：MFrom → MTo；Reason = not_full_attack / reset）。</summary>
public sealed record ReinforcementElasticEvent(int MFrom, int MTo, string Reason) : BattleEvent;

// ---------------------------------------------------------------------------
// G0（O-55）事件字典补全：谁用了什么技能 / buff 生命周期 / 回合 / 胜负 / AI 决策
// ---------------------------------------------------------------------------

/// <summary>技能使用事件（G0）：统计「技能使用率」KPI 的前提。</summary>
public sealed record SkillUseEvent(UnitId Actor, int CasterSlot, string SkillId, int[] TargetSlots) : BattleEvent
{
    // int[] 默认按引用比较 → 逐条日志比对（T-M6-05）会误判；按值比较
    public bool Equals(SkillUseEvent? other)
        => other is not null && Actor == other.Actor && CasterSlot == other.CasterSlot
           && SkillId == other.SkillId && TargetSlots.SequenceEqual(other.TargetSlots);

    public override int GetHashCode() => HashCode.Combine(Actor, CasterSlot, SkillId, TargetSlots.Length);
}

/// <summary>技能被拒（G0）：恐惧拒放 / 无可选目标 / CD 中 / 每战已用尽。</summary>
public sealed record SkillRefusedEvent(UnitId Actor, string SkillId, string Reason) : BattleEvent;

/// <summary>buff 施加（G0）。</summary>
public sealed record BuffAppliedEvent(UnitId? Source, UnitId Target, string BuffId, int DurationRounds, int Stacks) : BattleEvent;

/// <summary>buff 移除（G0）：expired / dispelled / consumed / morale_reset / death。</summary>
public sealed record BuffRemovedEvent(UnitId Target, string BuffId, string Reason) : BattleEvent;

/// <summary>回合开始（G0）：谁先动、有效速度。</summary>
public sealed record TurnStartEvent(UnitId Actor, int Slot, double EffectiveSpeed) : BattleEvent;

/// <summary>跳过行动（G0）：stunned / bound / no_usable_skill。</summary>
public sealed record TurnSkippedEvent(UnitId Actor, string Reason) : BattleEvent;

/// <summary>战斗结束（G0）：胜负与原因。</summary>
public sealed record BattleEndEvent(string Outcome, int Round, string Reason) : BattleEvent;

/// <summary>敌方 AI 决策（G0）：用了哪条规则、打了谁。</summary>
public sealed record EnemyDecisionEvent(UnitId Actor, string SkillId, int RuleIndex, string RuleCondition, int[] TargetSlots) : BattleEvent
{
    public bool Equals(EnemyDecisionEvent? other)
        => other is not null && Actor == other.Actor && SkillId == other.SkillId
           && RuleIndex == other.RuleIndex && RuleCondition == other.RuleCondition
           && TargetSlots.SequenceEqual(other.TargetSlots);

    public override int GetHashCode() => HashCode.Combine(Actor, SkillId, RuleIndex, RuleCondition, TargetSlots.Length);
}

/// <summary>属性增减（G0）：绕过 buff 台账的 AttackMod/ResilienceMod/SpeedMod 也在此留痕。</summary>
public sealed record StatModEvent(UnitId Target, string Stat, int Delta, int DurationRounds) : BattleEvent;

/// <summary>士气余烬标记进出（G0，级 2）：enter = 士气触底 0，exit = 回升脱离。</summary>
public sealed record MoraleEmberEvent(UnitId Unit, string Kind) : BattleEvent;

/// <summary>靠齐（G0，级 2）：死亡后队列收拢的每个位移。</summary>
public sealed record CloseUpEvent(IReadOnlyList<(UnitId Unit, int From, int To)> Moves) : BattleEvent;