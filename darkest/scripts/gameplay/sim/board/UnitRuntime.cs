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

    public int MaxHp { get; private set; }

    /// <summary>
    /// 远征层难度递进（#252 / O-70 ① 裁定：**只作用敌 HP**）：按场序放大最大 HP。
    /// 单次伤害不变 → **保 A1 濒死带**（死门/阵亡）；代价是**拉长战斗** → 受"回合 4~6"护栏约束（⑳）。
    /// 增援单位本体不受影响（③ 不波及增援与 SP/资源）。
    /// </summary>
    public void ApplyMaxHpMultiplier(double multiplier)
    {
        if (multiplier <= 0 || Math.Abs(multiplier - 1.0) < 1e-9)
        {
            return;
        }

        MaxHp = Math.Max(1, (int)Math.Round(MaxHp * multiplier));
        CurrentHp = MaxHp;
    }

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

    /// <summary>D1（#203）：连续未命中计数（命中清零）。补偿 = max(0, 计数−1) × 4，仅用于命中判定、不进面板。</summary>
    public int ConsecutiveMisses { get; set; }

    /// <summary>D0（#202）：眩晕抗性递增（每次成功被晕 +50%，完成一次未被晕的行动后清零；抗性上限 100）。</summary>
    public int StunResistBuildup { get; set; }

    /// <summary>
    /// 远征层难度备用轴（#253）：**敌【眩晕抗性】+15pp**（运行时投影、**不改 `units.json` 基准**）。
    /// 选择它的理由（文档原文）：它**不改回合数、不改伤害** ⇒ **同时避开 A1 的回合带与濒死带**；
    /// ⚠️ **不动位移抗性** —— "位置驱动"是本作核心定位，削它等于削核心玩法。
    /// </summary>
    public int StunResistBonus { get; set; }

    /// <summary>按档施加眩晕抗性加成（pp；0 或负数 = 不改）。</summary>
    /// <summary>
    /// M8.0 ①(b)（`#283` 7.6 / `#286`）：**等级成长的运行时投影** —— HP +N/级、攻击 +N/级；
    /// 与 `ApplyMaxHpMultiplier` 同款：**运行时投影、不改基准数据**（P4 同源），由组合根在装配时注入。
    /// 🔴 **不含技能**（7.8：不碰技能表；技能升级留 M8.1）。
    /// </summary>
    public void ApplyLevelGrowth(int level, int hpPerLevel, int attackPerLevel)
    {
        int steps = Math.Max(0, level - 1);
        if (steps == 0)
        {
            return;
        }

        MaxHp = Math.Max(1, MaxHp + (hpPerLevel * steps));
        CurrentHp = MaxHp;
        AttackMod += attackPerLevel * steps;
    }

    public void ApplyStunResistBonus(int pp)
    {
        if (pp != 0)
        {
            StunResistBonus += pp;
        }
    }

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