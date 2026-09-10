using System;
using Darkest.Core.Contracts;

namespace Darkest.Gameplay.Sim.Board;

/// <summary>
/// 战斗单位运行时实体（M1 为最小 Id/Side/Weak；M2 扩展为结算管线属性宿主）：
/// 基础属性只读（模板），HP/状态/临时修正为运行时可变；生效口统一在此计算
/// （"加法先于乘法"，blueprint §9.6 FinalStats 语义，M4 buff 修改器接入同一批口）。
/// 零 `using Godot`。
/// </summary>
public sealed class UnitRuntime
{
    public UnitRuntime(UnitId id, FormationSide side, UnitStats baseStats, bool weak = false, string? archetypeId = null)
    {
        Id = id;
        Side = side;
        Base = baseStats ?? throw new ArgumentNullException(nameof(baseStats));
        Weak = weak;
        ArchetypeId = archetypeId ?? id.Value; // 兼容旧构造：id 即原型
        MaxHp = baseStats.Hp;
        CurrentHp = baseStats.Hp;
    }

    /// <summary>实例身份 id（编成内唯一；同原型多实例为 `原型_2` 等——修正同名冲突）。</summary>
    public UnitId Id { get; }

    /// <summary>原型 id（技能池 owner_unit / units.json / AI 表 / 中文名均按此匹配；与实例 Id 分离）。</summary>
    public string ArchetypeId { get; }

    /// <summary>本单位所属阵营。</summary>
    public FormationSide Side { get; }

    public bool IsPlayer => Side == FormationSide.Player;

    /// <summary>基础属性模板（只读）。</summary>
    public UnitStats Base { get; }

    /// <summary>虚弱标记：靠齐时编号不得减小；伤害 −50%、速度 −30%（tuning weak）。</summary>
    public bool Weak { get; set; }

    public int MaxHp { get; }

    /// <summary>当前 HP（扣血/击杀/虚弱锁 1 语义见 T-M2-05/09）。</summary>
    public int CurrentHp { get; set; }

    // ------------------------------------------------------------------
    // M2-06 临时修正与状态（属性减益固定值 / 眩晕 / 流血；buff 修改器 M4 接入）
    // ------------------------------------------------------------------

    public int AttackMod { get; set; }
    public int PhysDefMod { get; set; }
    public int ResilienceMod { get; set; }
    public int SpeedMod { get; set; }

    /// <summary>眩晕标记：跳过 1 次行动随即结束（GDD §2.5 / tuning stun）。</summary>
    public bool Stunned { get; set; }

    /// <summary>流血剩余回合（每回合结束 3 点，切片无施加者，链路预置）。</summary>
    public int BleedRoundsRemaining { get; set; }

    /// <summary>士气值（0~100；起手 balance.MoraleStart，唯一写入口 = MoraleLedger）。</summary>
    public int Morale { get; set; }

    /// <summary>崩溃余烬（morale §4.0）：士气停在 0 的标记，期间不再触发崩溃判定（事件触发，#67）。</summary>
    public bool CollapseEmber { get; set; }

    // ------------------------------------------------------------------
    // 生效口（加法先于乘法；M4 由 IBuffLedger.FinalStats 汇总后仍走这些口）
    // ------------------------------------------------------------------

    public int EffectiveAttack => Math.Max(1, Base.Attack + AttackMod);
    public int EffectivePhysDef => Math.Max(0, Base.PhysDef + PhysDefMod);
    public int EffectiveResilience => Math.Max(0, Base.Resilience + ResilienceMod);

    /// <summary>生效速度 = (基础+速度修正) × 虚弱因子（weak.speed_mult；加法先于乘法）。</summary>
    public double EffectiveSpeed(double weakSpeedMult = 1.0)
    {
        double flat = Base.Speed + SpeedMod;
        return Weak ? flat * weakSpeedMult : flat;
    }

    public override string ToString()
        => $"{Id}@({Side}){(Weak ? ":W" : "")} hp={CurrentHp}/{MaxHp}";
}