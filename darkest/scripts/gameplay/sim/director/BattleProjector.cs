using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;

namespace Darkest.Gameplay.Sim.Director;

/// <summary>单位视图行（B 区/C 卡；值来自内核只读投影）。</summary>
public sealed record UnitProjection(int Slot, string UnitId, int Hp, int MaxHp, int Morale, bool Weak,
    IReadOnlyList<string> Buffs, bool IsPlayer);

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
    IReadOnlyList<int> OccupiedEnemySlots);

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
                    Array.Empty<string>(), isPlayer)); // buff 摘要由 IBattleView 侧经 IBuffLedger 提供（M5 UI 薄层）
        }

        return list;
    }

    /// <summary>技能可用性（D 栏；携带集由 BattleSetup 冻结）。</summary>
    public SkillProjection Skill(string skillId, UnitId caster, FormationBoard ally, FormationBoard target,
        IReadOnlySet<string>? carried)
    {
        Availability av = _resolver.Resolve(new SkillUseContext(_skills.Get(skillId), caster, ally, target,
            carried, _runtime, IsEnemy: false));
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
            _director.Enemy.OccupiedPositions(false).ToArray());
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
}