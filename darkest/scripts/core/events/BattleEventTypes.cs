using Darkest.Core.Contracts;

namespace Darkest.Core.Events;

// M2 结算事件族（T-M2-04~09）：一律不可变 record（值语义，供回放/统计逐事件比对）。
// 每次随机抽取：先追加 RngDraw（含 DrawCount+数值），再追加对应业务事件。

/// <summary>命中判定事件（T-M2-04）。</summary>
public sealed record HitEvent(bool Hit, int HitRate, UnitId? Attacker, UnitId? Target) : BattleEvent;

/// <summary>暴击判定事件（T-M2-05）。</summary>
public sealed record CritEvent(bool Crit, UnitId? Attacker, UnitId? Target) : BattleEvent;

/// <summary>单段伤害事件（T-M2-05；多段=多条，SegmentIndex 从 0 起）。</summary>
public sealed record DamageEvent(UnitId? Target, int Amount, double Raw, bool Crit, int SegmentIndex, string Axis) : BattleEvent;

/// <summary>士气变动事件（T-M2-08；Delta 为净变动，NewValue 为钳制后值）。</summary>
public sealed record MoraleEvent(UnitId Unit, int Delta, string Source, int NewValue) : BattleEvent;

/// <summary>附加效果判定/施加事件（T-M2-06；无概率直挂 ActualChance=100 且无 RngDraw）。</summary>
public sealed record EffectEvent(UnitId? Target, string EffectType, double ActualChance, bool Triggered) : BattleEvent;

/// <summary>位移判定事件（T-M2-07；唯一 >= 例外）。</summary>
public sealed record DisplaceEvent(UnitId? Mover, int FromPos, int ToPos, bool PassedResist, bool ChainSucceeded, string Failure) : BattleEvent;

/// <summary>死门判定事件（T-M2-09）。</summary>
public sealed record DeathDoorEvent(UnitId? Unit, int SurvivePercent, double Roll, bool Survived) : BattleEvent;

/// <summary>进入虚弱事件（T-M2-09）。</summary>
public sealed record WeakEnterEvent(UnitId? Unit) : BattleEvent;

/// <summary>真死/离场事件（T-M2-09）。</summary>
public sealed record DeathEvent(UnitId? Unit, bool IsPlayer) : BattleEvent;

/// <summary>崩溃判定调用点（T-M2-09；完整池解析归 M4，本事件仅记录抽取）。</summary>
public sealed record CollapseRollEvent(UnitId? Unit, double Roll) : BattleEvent;

/// <summary>固定值治疗（combat_math §8，不吃攻击力；急救 12 / 群体绷带 5 / 喘息 8、10）。</summary>
public sealed record HealEvent(UnitId? Target, int Amount) : BattleEvent;

/// <summary>自我伤害固定值（殊死一搏 6 / 舍身 8；可致死走死门，M4 接线）。</summary>
public sealed record SelfDamageEvent(UnitId? Unit, int Amount) : BattleEvent;