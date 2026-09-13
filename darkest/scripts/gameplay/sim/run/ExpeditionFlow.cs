using System;
using System.Collections.Generic;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>一步的类型。</summary>
public enum FlowStepKind
{
    Battle,
    Event,
    Done,
}

/// <summary>当前待处理的步骤（供 UI 渲染与转发）。</summary>
public sealed record FlowStep(int Index, FlowStepKind Kind, string NodeId, IReadOnlyList<PathOption> Options);

/// <summary>
/// M7.5 **远征流程控制器**（内核，零 Godot）：把"选路 → 侦察 → 光照前进 →（战斗 | 事件）→ 掉落 → 扎营 → 下一步"
/// 串成一个可驱动的状态机，**供 Godot 场景层往返调用**：
/// · 战斗步骤：UI 切到战斗场景，打完把 `OnBattleFinished` 回灌（结果 + 回合数）；
/// · 事件步骤：UI 直接调 `ResolveEvent(0/1)`（**二选一，无跳过**）；
/// · 扎营：`Camp()`（柴火不足 ⇒ 内核拒绝且不扣）。
/// 🔴 状态与判定全在内核；数据由外部注入；**本类不持 Godot 引用**。
/// </summary>
public sealed class ExpeditionFlow
{
    private readonly ExpeditionSession _session;
    private readonly LightMeter _meter;
    private readonly Inventory _bag;
    private readonly Scouting _scout;
    private readonly ExpeditionNodesConfig _nodes;
    private readonly TuningConfig _tuning;
    private readonly CombatLog _log;
    private readonly IRngProvider _rng;

    private IReadOnlyList<PathStep>? _path;

    public ExpeditionFlow(ExpeditionSession session, LightMeter meter, Inventory bag, Scouting scout,
        ExpeditionNodesConfig nodes, TuningConfig tuning, CombatLog log, IRngProvider rng)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _meter = meter ?? throw new ArgumentNullException(nameof(meter));
        _bag = bag ?? throw new ArgumentNullException(nameof(bag));
        _scout = scout ?? throw new ArgumentNullException(nameof(scout));
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _meter.EmitStart(_log);
    }

    /// <summary>当前步骤（null = 尚未开始或已结束）。</summary>
    public FlowStep? Current { get; private set; }

    /// <summary>已完成步数。</summary>
    public int StepsDone { get; private set; }

    /// <summary>上一次侦察结果（供 UI 必显 13）。</summary>
    public ScoutOutcome? LastScout { get; private set; }

    /// <summary>本趟是否已结束（走完 6 步 / 撤退 / 全灭）。</summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// 推进到下一步：选路（`optionIndex` 0/1）→ 侦察判定 → 光照 −15 → 判定类型。
    /// 走到路径尽头 ⇒ `Done`。
    /// </summary>
    public FlowStep Advance(int optionIndex)
    {
        if (IsFinished)
        {
            return new FlowStep(StepsDone, FlowStepKind.Done, string.Empty, Array.Empty<PathOption>());
        }

        _path ??= ExpeditionPathPlanner.GeneratePath(_log, _rng, _tuning.Expedition.NBattles, _nodes);
        if (StepsDone >= _path.Count)
        {
            IsFinished = true;
            Current = new FlowStep(StepsDone, FlowStepKind.Done, string.Empty, Array.Empty<PathOption>());
            return Current;
        }

        PathStep step = _path[StepsDone];
        PathOption chosen = ExpeditionPathPlanner.ChoosePath(_log, step, optionIndex);

        // 侦察：只揭示【下一个】节点类型（失败 ⇒ null）；每次判定必写 RngDraw
        LastScout = _scout.Roll(_log, _rng, _meter.Value, chosen.NodeType);

        _meter.TryAdvanceNode(_log); // 前进一个节点 −15

        Current = new FlowStep(StepsDone, chosen.NodeType == "battle" ? FlowStepKind.Battle : FlowStepKind.Event,
            chosen.NodeId, step.Options);
        return Current;
    }

    /// <summary>结算事件步骤（二选一；内核保证越界即拒）。</summary>
    public void ResolveEvent(int optionIndex)
    {
        if (Current is not { Kind: FlowStepKind.Event } step)
        {
            throw new InvalidOperationException("当前步骤不是事件节点（流程层不应调用）。");
        }

        _session.ResolveEventNode(_log, _nodes.Get(step.NodeId), optionIndex);
        StepsDone++;
    }

    /// <summary>
    /// 战斗结束回灌：失败/撤退 ⇒ 本趟结束；胜利 ⇒ **按档给份数掉落**（#270：不掷骰）并推进。
    /// </summary>
    public void OnBattleFinished(string result, int rounds)
    {
        _ = rounds;
        if (Current is not { Kind: FlowStepKind.Battle } step)
        {
            throw new InvalidOperationException("当前步骤不是战斗节点（流程层不应调用）。");
        }

        if (result != "PlayerVictory")
        {
            IsFinished = true;
            StepsDone++;
            return;
        }

        // 🔴 收益端（#270 裁定①）：按当前光照档**确定给份数**，不引入抽取
        int grant = _tuning.Light!.Loot[LightMeter.TierId(_meter.Tier)];
        if (grant > 0)
        {
            _session.Gain(_log, "food", grant, "loot");
        }

        _ = step;
        StepsDone++;
    }

    /// <summary>夜袭判定（扎营后调用；触发则插一场额外战斗，计入完成）。</summary>
    public bool RollAmbush() => _session.RollAmbush(_log, _rng);

    /// <summary>扎营（柴火不足 ⇒ 拒绝；成功则光照回满）。</summary>
    public bool Camp()
    {
        if (!_session.StartCamp(_log, StepsDone, _tuning.Camp!.RespiteBase))
        {
            return false;
        }

        _meter.OnCamp(_log);
        string best = _session.CanAffordFood(_tuning.Camp, "feast") ? "feast"
            : _session.CanAffordFood(_tuning.Camp, "full") ? "full"
            : _session.CanAffordFood(_tuning.Camp, "half") ? "half" : "starve";
        _session.ChooseFood(_log, _tuning.Camp, best);
        _session.EndCamp(_log);
        return true;
    }

    /// <summary>回城结算（士气完全不恢复由内核 #245 保证）。</summary>
    public int ReturnToTown(string outcome)
    {
        IsFinished = true;
        return _session.ReturnToTown(_log, outcome);
    }

    /// <summary>本趟是否走完 6 步且未撤退/未团灭（#270 裁定② 的完成口径）。</summary>
    public bool Completed => !IsFinished ? StepsDone >= _tuning.Expedition.NBattles : StepsDone >= _tuning.Expedition.NBattles;

    /// <summary>背包（供 UI 渲染格子）。</summary>
    public Inventory Bag => _bag;

    /// <summary>光照计（供 UI 渲染光照条）。</summary>
    public LightMeter Meter => _meter;

    /// <summary>会话（供投影取 HP/士气/资源）。</summary>
    public ExpeditionSession Session => _session;
}
