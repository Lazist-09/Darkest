using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>单场结束快照（run 曲线用；分母固定为**整编人数**，阵亡者计 0%）。</summary>
public sealed record RunBattleSnapshot(
    int Battle,
    int Rounds,
    string Result,
    int AliveCount,
    int RosterCount,
    double AvgHpPercent,
    double AvgMoralePercent)
{
    /// <summary>是否"我方全灭"（run 失败判定）。</summary>
    public bool PlayerWiped => Result == "EnemyVictory" || AliveCount == 0;
}

/// <summary>一趟 run 的结果：逐场曲线 + 完成口径（v0.68）。</summary>
public sealed record RunOutcome(IReadOnlyList<RunBattleSnapshot> Curve, bool SurvivedAllBattles, bool CompletedIgnoreRetreat)
{
    /// <summary>口径 ①：**未全灭**（撤退也算活下来；仅用于诊断）。</summary>
    public bool CompletedCountingRetreat => SurvivedAllBattles && Curve.Count > 0;

    /// <summary>口径 ②/③（v0.68 起二者应相等）：**打满 run 长度且每场皆胜**——
    /// 撤退 = 该场判负 + run 立即结束（不再计入），故"打满 3 场且未全灭" ≡ "3 场皆胜"。</summary>
    public bool CompletedStrict => CompletedIgnoreRetreat
                                   && Curve.Count >= 3
                                   && Curve.All(s => s.Result == "PlayerVictory");
}

/// <summary>
/// #223/O-63（blueprint §9.12）run 级状态持有者：**跨场保留** HP/士气/虚弱；
/// **每场重置** buff / CD / `per_battle` 次数 / **支援点 SP**（SP 是战斗级资源，blueprint §9.11）。
/// 结构约束：**`BattleDirector` 保持"单场纯"**——本类每场都用工厂新建一个 director（同 seed 可复现单场）。
/// </summary>
public interface IRunSession
{
    /// <summary>当前已完成的场次数。</summary>
    int BattlesPlayed { get; }

    /// <summary>逐场结束曲线。</summary>
    IReadOnlyList<RunBattleSnapshot> Curve { get; }

    /// <summary>开始下一场：新建（纯净的）director 并套用跨场保留状态。</summary>
    BattleDirector BeginBattle(int battleIndex, CombatLog log);

    /// <summary>结算本场：快照保留状态（阵亡者记 0 HP，下一场不复活）。</summary>
    RunBattleSnapshot EndBattle(BattleDirector director, int battleIndex, string result, int rounds);

    /// <summary>是否已达 run 长度（终值随 M7 旋钮；切片 = 3）。</summary>
    bool IsComplete { get; }

    /// <summary>导出两种完成口径的结论。</summary>
    RunOutcome Outcome(bool anyRetreat);
}

/// <summary>默认实现：跨场保留 HP/士气/虚弱；其余每场重置。可被远征层 <c>ExpeditionSession</c> 扩展。</summary>
public class RunSession : IRunSession
{
    private readonly Func<CombatLog, BattleDirector> _buildDirector;
    private readonly int _battles;
    private readonly List<RunBattleSnapshot> _curve = new();
    private readonly Dictionary<string, (int Hp, int Morale, bool Weak)> _retained = new();

    /// <summary>远征层用（E4/E6）：跨场保留状态的读写面（HP/士气/虚弱）。</summary>
    protected Dictionary<string, (int Hp, int Morale, bool Weak)> Retained => _retained;

    /// <summary>远征层用（E6）：死门后遗症跨场保留集（回城时按"到下次恢复"清空）。</summary>
    protected HashSet<string> RetainedRecovery => _retainedRecovery;

    /// <summary>远征层用（E3）：整编最大 HP（扎营的 HP% 结算分母；跨场固定）。</summary>
    protected Dictionary<string, int> RosterMaxHp => _rosterMaxHp;
    private readonly HashSet<string> _retainedRecovery = new(); // #227：deaths_door_recovery 跨场保留
    private readonly List<string> _roster = new();
    private readonly Dictionary<string, int> _rosterMaxHp = new(); // 整编最大 HP 分母（第一场记录，跨场固定）

    public RunSession(Func<CombatLog, BattleDirector> buildDirector, int battles = 3)
    {
        _buildDirector = buildDirector ?? throw new ArgumentNullException(nameof(buildDirector));
        _battles = Math.Max(1, battles);
    }

    public int BattlesPlayed => _curve.Count;

    public IReadOnlyList<RunBattleSnapshot> Curve => _curve;

    public bool IsComplete => _curve.Count >= _battles;

    public BattleDirector BeginBattle(int battleIndex, CombatLog log)
    {
        BattleDirector director = _buildDirector(log); // 每场新建 → buff/CD/per_battle/SP 全部重置（战斗级）
        if (_roster.Count == 0)
        {
            // 整编名册在**第一场**记录（含后来阵亡者），作为曲线分母与跨场携带的键集
            foreach (UnitRuntime u in director.Player.UnitsInSlotOrder())
            {
                _roster.Add(u.Id.Value);
                _rosterMaxHp[u.Id.Value] = u.MaxHp;
            }
        }

        foreach (string id in _roster)
        {
            if (!_retained.TryGetValue(id, out (int Hp, int Morale, bool Weak) c))
            {
                continue;
            }

            UnitRuntime? u = director.Player.UnitsInSlotOrder().FirstOrDefault(x => x.Id.Value == id);
            if (u is null)
            {
                continue;
            }

            if (c.Hp <= 0)
            {
                director.Player.RemoveUnitAt(director.Player.UnitAtPosition(u.Id) ?? -1); // 阵亡者不复活
                continue;
            }

            u.CurrentHp = Math.Min(c.Hp, u.MaxHp);
            u.Morale = c.Morale;
            u.Weak = c.Weak; // O-63 建议：虚弱保留（属损耗累积）

            // #227：死门后遗症跨场保留（它是"活下来付出的代价"，跨场继续生效）
            if (_retainedRecovery.Contains(id))
            {
                director.Buffs.Add(u.Id, "deaths_door_recovery", source: null);
                u.SpeedMod -= 1;
            }
        }

        return director;
    }

    /// <summary>
    /// 🔴 **相位**（架构 §9.17.0）：规则状态的**单一真值**，由流程推进时设置 ✓
    /// 表现层**只读**下面的派生谓词，**绝不自己推断相位**（否则会出现"两个面板同时该显示 / 都不该显示"）⚠️
    /// </summary>
    public FlowPhase Phase { get; private set; } = FlowPhase.Walking;

    /// <summary>推进相位（由流程在正确的时机调用；内核唯一的相位写入口）✓</summary>
    public void EnterPhase(FlowPhase next) => Phase = next;
    public RunBattleSnapshot EndBattle(BattleDirector director, int battleIndex, string result, int rounds)
    {
        List<UnitRuntime> alive = director.Player.UnitsInSlotOrder().ToList();
        int aliveCount = alive.Count(u => u.CurrentHp > 0);
        int maxHpSum = Math.Max(1, _rosterMaxHp.Values.Sum()); // 整编分母（跨场固定；阵亡者计 0）
        double hpPct = 100.0 * alive.Sum(u => Math.Max(0, u.CurrentHp)) / maxHpSum;
        double moralePct = _roster.Count == 0 ? 0 : alive.Sum(u => u.Morale) / (double)(_roster.Count * 100) * 100.0;

        var snapshot = new RunBattleSnapshot(battleIndex, rounds, result, aliveCount,
            Math.Max(_roster.Count, alive.Count), hpPct, moralePct);
        _curve.Add(snapshot);

        _retained.Clear();
        _retainedRecovery.Clear();
        foreach (string id in _roster)
        {
            UnitRuntime? u = director.Player.UnitsInSlotOrder().FirstOrDefault(x => x.Id.Value == id);
            _retained[id] = u is null ? (0, 0, false) : (u.CurrentHp, u.Morale, u.Weak);

            // #227：本场结束时仍持有死门后遗症 → 记入跨场保留（阵亡者不记）
            if (u is not null && director.Buffs.Has(u.Id, "deaths_door_recovery"))
            {
                _retainedRecovery.Add(id);
            }
        }

        EnterPhase(FlowPhase.Walking); // 🔴 战斗结束 ⇒ 回到【走图】相位（内核侧统一设置 ⇒ 探针与场景同源）✓
        return snapshot;
    }

    public RunOutcome Outcome(bool anyRetreat)
    {
        bool survived = _curve.Count > 0 && _curve.All(s => !s.PlayerWiped);
        bool strict = survived && !anyRetreat && _curve.Count >= _battles;
        return new RunOutcome(_curve, survived, strict);
    }
}
