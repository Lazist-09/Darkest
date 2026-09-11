using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Rng;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Enemy;
using Darkest.Gameplay.Sim.Skill;

namespace Darkest.Gameplay.Sim.Director;

/// <summary>单位视图行（B 区/C 卡；值来自内核只读投影）。Archetype=原型 id（中文名/技能池按此匹配）。</summary>
public sealed record UnitProjection(int Slot, string UnitId, int Hp, int MaxHp, int Morale, bool Weak,
    IReadOnlyList<string> Buffs, bool IsPlayer, string Archetype = "");

/// <summary>技能可用性视图（D 栏：灰显 + tooltip 原因）。</summary>
public sealed record SkillProjection(string SkillId, AvailabilityReason Reason, string Tooltip);

/// <summary>决策支持投影（ui_spec §2 必显 #1/#2/#4/#5/#6/#9 数据源，T-M5-06）。</summary>
public sealed record DecisionSupportProjection(
    int Round,
    IReadOnlyList<string> ActionOrderThisRound,
    IReadOnlyList<string> ActionOrderNextRound, // 预演：以当前速度快照外推，不新增抽取（#163 重掷前可能变动）
    int RetreatRatePercent,
    bool CanRetreat,
    IReadOnlyList<int> OccupiedPlayerSlots,
    IReadOnlyList<int> OccupiedEnemySlots,
    int SupportPoints = 0,        // #211（S0）必显 #10：当前支援点
    int SupportCap = 0,           // 上限
    int SupportRegenPreview = 0); // 恢复预览：min(cur + regen, cap)

/// <summary>
/// BattleProjector：把内核（板/士气/行动序列/技能可用性）投影为 UI 只读视图 / IBattleView 数据源
/// （T-M5-06/09）。零随机、零写状态（行动序列读 StartTurn 固定点缓存；撤退数字 = 导演纯公式）。
/// 位移预览 = 对只读快照跑同一 TrySwapChain（dry-run，与结算同源，T-M5-06 #6 / M-A）。
/// </summary>
public sealed class BattleProjector
{
    private readonly BattleDirector _director;
    private readonly BalanceTable _balance;
    private readonly SkillsConfig _skills;
    private readonly SkillUseResolver _resolver;
    private readonly SkillRuntimeState _runtime;

    public BattleProjector(BattleDirector director, BalanceTable balance, SkillsConfig skills, SkillRuntimeState runtime)
    {
        _director = director ?? throw new ArgumentNullException(nameof(director));
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _resolver = new SkillUseResolver(skills);
    }

    /// <summary>单位列表（我方 1~6 / 敌方 1~4，槽升序；空槽也产出（#9 编号常驻）。</summary>
    public IReadOnlyList<UnitProjection> Units(bool player) => SlotProjection(player ? _director.Player : _director.Enemy, player);

    private IReadOnlyList<UnitProjection> SlotProjection(FormationBoard board, bool isPlayer)
    {
        var list = new List<UnitProjection>();
        for (int slot = 1; slot <= board.SlotCount; slot++)
        {
            UnitRuntime? u = board.UnitRuntimeAt(slot);
            list.Add(u is null
                ? new UnitProjection(slot, "-", 0, 0, 0, false, Array.Empty<string>(), isPlayer)
                : new UnitProjection(slot, u.Id.ToString(), u.CurrentHp, u.MaxHp, u.Morale, u.Weak,
                    Array.Empty<string>(), isPlayer, u.ArchetypeId)); // buff 摘要由 IBattleView 侧经 IBuffLedger 提供
        }

        return list;
    }

    /// <summary>技能可用性（D 栏；携带集由 BattleSetup 冻结）。#211：支援位技能接入 SP 判定。</summary>
    public SkillProjection Skill(string skillId, UnitId caster, FormationBoard ally, FormationBoard target,
        IReadOnlySet<string>? carried)
    {
        bool supportSlot = _director.IsSupportSlotActor(caster);
        SkillTemplateConfig skill = _skills.Get(skillId);
        int cost = skill.SupportPointCost ?? (supportSlot ? _director.SupportCostSkill : 0); // #211：技能声明优先
        Availability av = _resolver.Resolve(new SkillUseContext(skill, caster, ally, target,
            carried, _runtime, IsEnemy: false,
            SupportPoints: _director.SupportPoints,
            SupportCost: cost,
            IsSupportSlotActor: supportSlot));
        return new SkillProjection(skillId, av.Reason, av.Tooltip);
    }

    /// <summary>9 条必显决策支持投影（行动序列/撤退数字/占用列表；无抽取、无写）。</summary>
    public DecisionSupportProjection Support()
    {
        IReadOnlyList<UnitId> order = _director.LastRoundOrder; // 回合固定点已构建（不在此抽取）
        string[] ids = order.Select(u => u.ToString()).ToArray();
        return new DecisionSupportProjection(
            _director.Round,
            ids,
            ids, // 下回合预览：以当前快照外推（速度未重掷 → 同序；#163 重掷后可能变动，UI 标注"预演"）
            (int)Math.Round(_director.CurrentRetreatRate(), MidpointRounding.AwayFromZero),
            _director.CanRetreatThisRound,
            _director.Player.OccupiedPositions(false).ToArray(),
            _director.Enemy.OccupiedPositions(false).ToArray(),
            _director.SupportPoints,
            _director.SupportCap,
            _director.SupportRegenPreview);
    }

    /// <summary>命中率（必显 #4）：100 − 目标闪避 + 技能 hit_mod 钳制 [55,100]（combat_math §1，纯函数）。</summary>
    public int HitRateFor(int targetDodge, int hitMod)
        => Core.Math.BattleMath.HitRate(targetDodge, hitMod, _balance.HitClampMin, _balance.HitClampMax);

    /// <summary>位移预览（必显 #6）：对只读快照跑同一 TrySwapChain（dry-run，与结算同源）。</summary>
    public DisplaceResult DisplacementPreview(UnitId mover, int fromPos, int toPos, int distance, FormationBoard board)
    {
        FormationBoard snapshot = board.CreatePreviewSnapshot();
        return snapshot.TrySwapChain(mover, fromPos, toPos, distance);
    }

    /// <summary>
    /// G4（O-57）敌方意图预览：切片**默认不显示**（enabled=false → 空意图）；后续「侦察」技能可开启。
    /// 预览用独立一次性 RNG/日志，绝不消耗战斗随机数、不写战斗事件流。
    /// </summary>
    public IntentProjection IntentPreview(UnitId actor, IRngProvider battleRng, bool enabled = false)
    {
        _ = battleRng; // 只读：战斗用 RNG 一律不参与预览
        if (!enabled)
        {
            return new IntentProjection(null, Array.Empty<int>(), "disabled");
        }

        if (_director.Enemy.UnitAtPosition(actor) is null)
        {
            return new IntentProjection(null, Array.Empty<int>(), "not_an_enemy");
        }

        SkillChoice? choice = _director.PreviewEnemyIntent(actor, new PreviewRng(), new CombatLog());
        return choice is null
            ? new IntentProjection(null, Array.Empty<int>(), "no_usable_skill")
            : new IntentProjection(choice.SkillId, choice.TargetSlots.ToArray(), "preview");
    }

    /// <summary>预览专用 RNG：固定序列、零副作用（不读也不推进战斗随机数）。</summary>
    private sealed class PreviewRng : IRngProvider
    {
        public double NextPercent() => 0.0;

        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;

        public ulong DrawCount => 0;
    }

    /// <summary>
    /// G3（O-56）单位详情：属性/士气/状态/buff/技能表全量投影（**敌方同样全暴露**，用户明确要求）。
    /// 只读快照，零抽取零写状态。
    /// </summary>
    public UnitDetail Detail(bool player, int slot)
    {
        FormationBoard board = player ? _director.Player : _director.Enemy;
        UnitRuntime? u = board.UnitRuntimeAt(slot);
        if (u is null)
        {
            return new UnitDetail(slot, "-", "", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, Array.Empty<string>());
        }

        string archetype = u.ArchetypeId;
        string[] skills = _skills.Skills.Where(s => s.OwnerUnit == archetype && !s.PoolExternal).Select(s => s.Id).ToArray();
        return new UnitDetail(
            slot, u.Id.Value, archetype,
            u.CurrentHp, u.MaxHp, u.Morale,
            u.Base.Attack, u.EffectivePhysDef, u.Base.Speed,
            u.Base.Resilience, u.Base.StunResist, u.Base.BleedResist,
            u.Base.StatDebuffResist, u.Base.DisplaceResist, u.Base.DeathsDoorResist ?? 0,
            u.Weak, u.Base.MovementRange, skills);
    }

    /// <summary>
    /// G3（O-56）目标预估：对候选池中某个目标位算出【命中率 + 预估伤害 + 段数】。
    /// **零抽取、零写状态**（不调用 rng、不 Append 事件）——否则预览会偷走随机数、破坏确定性（#201 同源约束）。
    /// 预估假设「不暴击 + 无伤害浮动」，与结算在同等假设下逐点一致（由 ProjectionDetailTests 对照验证）。
    /// </summary>
    public TargetEstimate Estimate(string skillId, UnitId caster, int targetSlot, bool targetIsPlayer)
    {
        SkillTemplateConfig skill = _skills.Get(skillId);
        FormationBoard ally = _director.Player.UnitAtPosition(caster) is not null ? _director.Player : _director.Enemy;
        FormationBoard targetBoard = targetIsPlayer ? _director.Player : _director.Enemy;
        UnitRuntime? attacker = ally.UnitsInSlotOrder().FirstOrDefault(u => u.Id == caster);
        UnitRuntime? victim = targetBoard.UnitRuntimeAt(targetSlot);
        if (attacker is null || victim is null)
        {
            return new TargetEstimate(0, 0, 0);
        }

        int hit = HitRateFor(victim.Base.Dodge, skill.HitMod);
        if (skill.Damage is null)
        {
            return new TargetEstimate(hit, 0, 0); // 支援/控制类：无伤害段
        }

        int total = 0;
        foreach (DamageSegment seg in skill.Damage.Segments)
        {
            double mult = seg.Type == DamageSegmentType.MissingHp
                ? (seg.Base ?? 1.0) + (seg.Coefficient ?? 0.0) * (victim.MaxHp > 0 ? (double)(victim.MaxHp - victim.CurrentHp) / victim.MaxHp : 0.0)
                : seg.Multiplier ?? 1.0;

            double raw;
            if (skill.DamageAxis == SkillDamageAxis.Mental)
            {
                double mitig = Core.Math.BattleMath.MentalMitigation(
                    victim.EffectiveResilience, _balance.MentalReductionDivisor, _balance.MentalReductionCapPercent);
                raw = attacker.EffectiveAttack * mult * (1.0 - mitig);
            }
            else
            {
                double mitig = Core.Math.BattleMath.PhysicalMitigation(victim.EffectivePhysDef);
                raw = attacker.EffectiveAttack * mult * (1.0 - mitig);
            }

            total += Core.Math.BattleMath.ApplyDamageRounding(raw, _balance.DamageFloor);
        }

        return new TargetEstimate(hit, total, skill.Damage.Segments.Count);
    }
}

/// <summary>G3：某候选目标位的只读预估（命中率 / 预估伤害 / 段数）。</summary>
public sealed record TargetEstimate(int HitRatePercent, int EstimatedDamage, int Segments);

/// <summary>G4：敌方意图投影（切片默认 disabled；侦察技能将来可读）。</summary>
public sealed record IntentProjection(string? SkillId, int[] TargetSlots, string Status);

/// <summary>G3：单位详情投影（敌方全暴露；Undead 属性 + 技能表）。</summary>
public sealed record UnitDetail(
    int Slot,
    string UnitId,
    string Archetype,
    int Hp,
    int MaxHp,
    int Morale,
    int Attack,
    int PhysDef,
    int Speed,
    int Resilience,
    int StunResist,
    int BleedResist,
    int StatDebuffResist,
    int DisplaceResist,
    int DeathsDoorResist,
    bool Weak,
    int MoveDistance,
    IReadOnlyList<string> SkillIds);