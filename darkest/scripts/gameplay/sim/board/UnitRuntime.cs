using System;
using Darkest.Core.Contracts;
using Darkest.Gameplay.Sim.Morale;

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
    // 🔴 战斗内修正叠加在**我们自加**的 `Prot` 上（原版无此拆分）⇒ 待裁：`reports/def_merge_three_plans.md` ✓
    public int ProtMod { get; set; }
    public int ResilienceMod { get; set; }
    public int SpeedMod { get; set; }

    // ------------------------------------------------------------------
    // 🆕 H-1（2026-09-27）：**装备阶 → 护甲读数**（"当前阶"问题的减伤半 ✓）
    // ------------------------------------------------------------------

    /// <summary>
    /// 🔴 **护甲阶覆盖的 `prot`**（未接线 = `null` ⇒ 读数仍是顶层的 `Base.Prot` ⇒ **零行为** ✓）。
    /// 由 <see cref="ApplyGearTier"/> 写入（组合根在装配时按 **`HeroGearState`** 的护甲阶调一次 ——
    /// `DirectorBridge` 把阶数组传参进投影 ⇒ 2026-09-30 架构裁定① 已**换源**，旧读点 `HeroConfig.ArmourTier` 不存在 ✓）
    /// </summary>
    public int? GearProtOverride { get; private set; }

    /// <summary>🔴 **护甲阶覆盖的 `dodge`**（同 <see cref="GearProtOverride"/>；`null` ⇒ 顶层 `Base.Dodge`）。</summary>
    public int? GearDodgeOverride { get; private set; }

    /// <summary>
    /// **按护甲阶投影减伤读数**（运行时投影、不改基准数据 —— 与 `ApplyLevelGrowth` 同法 ✓）。
    ///
    /// 🔴 **无 5 阶的单位（敌人）⇒ `TierDefence` 退回顶层** ⇒ **敌人读数逐字不变** ✓
    /// 🔴 **玩家单位 ⇒ 真按阶取**（⚠️ 这是**行为变更**：顶层 `prot` 8/12/4/5 vs 护甲第 0 阶 **全 0**，
    ///    差异已实测登记在 `reports/top_level_vs_tier0_consistency.md` §1，由策划裁定"直接接线"✓）
    /// </summary>
    public void ApplyGearTier(int armourTier)
    {
        GearProtOverride = TierDefence.ProtAt(Base, armourTier);
        GearDodgeOverride = TierDefence.DefAt(Base, armourTier);
    }

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

    /// <summary>
    /// M8.0（`#287` = `#245` 的落地）：**本趟开局士气**由名册（跨会话）投影进来 ——
    /// 与 `ApplyLevelGrowth` / `ApplyMaxHpMultiplier` 同款（运行时投影、不改基准数据）。
    /// </summary>
    public void ApplyOpeningMorale(int morale) => Morale = Math.Clamp(morale, 0, 100);

    /// <summary>
    /// M8.0 ③（`#289` 裁定 (B)）：**特质伤害修正** —— 与既有修正**同层叠加**（在 `DamageStep` 的 `raw` 里
    /// 与 `buffDamageMult` 同层相乘），**不是新通道**。幅度由 P22 ③ 定（伤害 ≤15%）。
    /// </summary>
    public int DamageModPct { get; private set; }

    /// <summary>聚合后的**攻击方伤害倍率**（1.0 = 无修正）。</summary>
    public double DamageMultiplier => 1.0 + (DamageModPct / 100.0);

    /// <summary>叠加一份伤害修正（加法先于乘法：多来源先累加再乘）。</summary>
    public void ApplyTraitDamagePct(int pct) => DamageModPct += pct;

    /// <summary>
    /// 🔴 M7.5 补欠账（`#302` (i1) 片②）：**暴击率修正（百分点的加法层）** ——
    /// 供 `light.effects` 的 `enemy_crit_pct`（敌）/ `our_crit_pct`（我）接线；
    /// 与 `crit_bonus`（buff）**同层**（`DamageStep` 里一起 clamp 到 [0,100]）。
    /// </summary>
    public int CritModPct { get; private set; }

    /// <summary>叠加一份暴击率修正（单位：百分点）。</summary>
    public void ApplyCritModPct(int pct) => CritModPct += pct;

    /// <summary>
    /// 🔴 M7.5 补欠账（`#302` (i1) 片②）：**命中率修正（百分点）** —— 供 `light.effects.enemy_acc` 接线；
    /// 与 `hit_mod`（死门后遗症 buff）**同层**（都在命中判定处一起 clamp）。
    /// </summary>
    public int AccModPct { get; private set; }

    /// <summary>叠加一份命中率修正（单位：百分点）。</summary>
    public void ApplyAccModPct(int pct) => AccModPct += pct;

    /// <summary>
    /// 🔴 **取整余数结转**（`㉟` 对称性修正）：伤害是整数、而下限（damageFloor）会吃掉小数 ⇒
    /// 若不结转，**减伤侧会被下限吞掉**（实测 −10% 只落到 −0.6%），而增伤侧被取整抬高（+21.6%）⇒ **两侧不对称**。
    /// 把每次被取整丢掉的部分留到下一次命中，使**长期均值与百分比成正比** ⇒ 两侧收敛。
    /// </summary>
    public double DamageCarry { get; set; }

    /// <summary>
    /// M8.0 ③：**受士气伤害修正**（特质士气类）—— 供士气通道读取（正值 = 更脆）。
    /// </summary>
    public int MoraleDamageTakenPct { get; private set; }

    /// <summary>受士气伤害倍率（1.0 = 无修正）。</summary>
    public double MoraleDamageTakenMultiplier => 1.0 + (MoraleDamageTakenPct / 100.0);

    public void ApplyTraitMoraleDamagePct(int pct) => MoraleDamageTakenPct += pct;

    /// <summary>
    /// M8.2（`m8_roadmap §2` + **V14**）：**疾病惩罚的运行时投影** —— 与 `ApplyLevelGrowth` / `ApplyOpeningMorale`
    /// / 特质同款（**运行时投影、不改基准数据**）。
    /// 🔴 **V14 的要点**：疾病**只记在 `Roster` 而不投影 = "写了但没接上"（红线 21）** ⇒ 必须落到单位上。
    /// </summary>
    public void ApplyDiseasePenalty(int hpDelta, int attackDelta, int moraleDelta)
    {
        if (hpDelta != 0)
        {
            MaxHp = Math.Max(1, MaxHp + hpDelta);
            CurrentHp = Math.Min(CurrentHp, MaxHp);
        }

        if (attackDelta != 0)
        {
            AttackMod += attackDelta;
        }

        if (moraleDelta != 0)
        {
            Morale = Math.Clamp(Morale + moraleDelta, 0, 100);
        }
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

    /// <summary>生效减伤（🆕 H-1：**装备阶覆盖优先**；未接线 = `null` ⇒ 与旧口径逐字一致 ✓）。</summary>
    public int EffectiveProt => Math.Max(0, (GearProtOverride ?? Base.Prot) + ProtMod);

    /// <summary>
    /// 🆕 **生效闪避**（H-1）：`HitStep` / `BattleProjector` **必须走这里**（而不是 `Base.Dodge`），
    /// 否则"护甲阶影响闪避"就是**写了但没接上**（红线 21）✓
    /// </summary>
    public int EffectiveDodge => GearDodgeOverride ?? Base.Dodge;
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
