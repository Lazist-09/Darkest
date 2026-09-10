using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;

namespace Darkest.Tests.MonteCarlo;

/// <summary>单场结果（T-M6-01/03 输出）。RoundLimit = 100 回合强切（软锁审计，不计胜）。</summary>
public enum GameResult { PlayerVictory, EnemyVictory, DrawRetreat, RoundLimit }

/// <summary>单场统计（从内核事件流聚合，与表现零耦合）。</summary>
public sealed record GameOutcome(long Seed, GameResult Result, int Rounds, int CollapseCount, int WeakCount,
    int DeathDoorRolls, bool AnyRetreat, int VirtueCount, int AfflictionCount, int Displacements,
    IReadOnlyDictionary<string, int> SkillUses, IReadOnlyDictionary<int, int> MoraleHistogram);

/// <summary>批量报告（T-M6-03/04）。</summary>
public sealed record SimulationReport(int Runs, double WinRate, double AvgRounds, double MinRounds, double MaxRounds,
    int TotalCollapse, int TotalWeak, int TotalDeathDoorRolls, double AvgDeathDoorRolls,
    int GamesWithRetreat, int TotalVirtue, int TotalAffliction, int TotalDisplacements,
    IReadOnlyDictionary<string, int> SkillUses, IReadOnlyDictionary<int, int> MoraleHistogram,
    IReadOnlyDictionary<string, int> PlayerDamage, int RoundLimitGames, string? LastLogDump);

/// <summary>
/// HeadlessDriver（T-M6-01）：直驱 BattleDirector（无场景树/无窗口/零 Godot）——
/// 显式 seed 每场一个独立 IRngProvider；回合=StartTurn→玩家策略动作→EnemyPhase；
/// 结束=我方全灭/敌方全灭/撤退成功（三态）。支持 tuning 覆盖（overrideSQL Cam 语义由测试注入）。
/// </summary>
public static class HeadlessDriver
{
    private static readonly int MaxRoundsCap = 100;

    private static GameResult SideWinner(BattleDirector director)
        => director.Enemy.OccupiedPositions(false).Count == 0
            ? GameResult.PlayerVictory
            : GameResult.EnemyVictory;

    public static BattleDirector NewDirector(CombatLog log) => DirectorBuilders.Build(log, null, null, null);

    /// <summary>单场模拟：返回结果 + 事件日志（供确定性留档）。支持三类数据覆盖（探针/override 语义）。</summary>
    public static (GameOutcome outcome, CombatLog log) Run(long seed, PolicyKind policy,
        Func<TuningConfig, TuningConfig>? tweak = null,
        Func<UnitsConfig, UnitsConfig>? unitsTweak = null,
        Func<SkillsConfig, SkillsConfig>? skillsTweak = null)
    {
        CombatLog log = new();
        BattleDirector director = DirectorBuilders.Build(log, tweak, unitsTweak, skillsTweak);
        var rng = new RngProvider(seed);
        var skillUses = new Dictionary<string, int>();
        var moraleHistogram = new Dictionary<int, int>();
        GameResult result = GameResult.DrawRetreat;

        int round;
        for (round = 1; round <= 100; round++)
        {
            director.StartTurn(rng); // 回合钩子（增援/支援位+3/虚弱回升）+ 构建行动序列

            // 士气直方图（回合初采样，供「士气触底/分布」统计）
            foreach (UnitRuntime u in director.Player.UnitsInSlotOrder())
            {
                moraleHistogram[u.Morale / 10] = moraleHistogram.GetValueOrDefault(u.Morale / 10) + 1;
            }

            // 每回合偶发撤退尝试（策略随机注入，满足「撤退≥1 整场」自然发生）
            double retreatRoll = rng.NextPercent();
            if (retreatRoll < 6.0 && director.PlayerRetreat(rng))
            {
                result = GameResult.DrawRetreat;
                break;
            }

            // actor 节拍（M6 前置立卡）：行动序列/眩晕/减速生效，我方逐个决策（半随机含换位增援 #176）、敌方经 AI
            director.RunFullRound(rng, unit =>
            {
                PlayerDecision decision = Policies.DecideForUnit(policy, unit, director, rng);
                if (decision.SkillId is not null)
                {
                    skillUses[decision.SkillId] = skillUses.GetValueOrDefault(decision.SkillId) + 1;
                }

                return decision;
            });

            if (director.IsBattleOver)
            {
                result = SideWinner(director);
                break;
            }

            if (round >= MaxRoundsCap)
            {
                result = GameResult.RoundLimit; // 100 回合强切：软锁审计，不计胜
                break;
            }
        }

        // 从事件流聚合 KPI（T-M6-04）：崩溃判定数 = CollapseResultEvent 数（每次判定一个产物）
        var events = log.Events;
        int collapse = events.OfType<CollapseResultEvent>().Count();
        GameOutcome outcome = new(seed, result, round,
            collapse,
            events.OfType<WeakEnterEvent>().Count(),
            events.OfType<DeathDoorEvent>().Count(),
            events.OfType<RetreatEvent>().Any(),
            events.OfType<CollapseResultEvent>().Count(e => e.Kind == "Virtue"),
            events.OfType<CollapseResultEvent>().Count(e => e.Kind == "Affliction"),
            events.OfType<DisplaceEvent>().Count(),
            skillUses, moraleHistogram);
        return (outcome, log);
    }

    /// <summary>批量（含并行分片：Des Epoch 每场独立 seed；T-M6-01 要点 6）。</summary>
    public static SimulationReport RunMany(int runs, PolicyKind policy, long seedBase = 20260909,
        Func<TuningConfig, TuningConfig>? tweak = null,
        Func<UnitsConfig, UnitsConfig>? unitsTweak = null,
        Func<SkillsConfig, SkillsConfig>? skillsTweak = null)
    {
        var outcomes = new List<GameOutcome>();
        CombatLog? lastLog = null;
        var playerDamage = new Dictionary<string, int>();
        for (int i = 0; i < runs; i++)
        {
            (GameOutcome o, CombatLog log) = Run(seedBase + i, policy, tweak, unitsTweak, skillsTweak);
            outcomes.Add(o);
            foreach (DamageEvent d in log.Events.OfType<DamageEvent>().Where(d => d.Attacker is not null))
            {
                playerDamage[d.Attacker!.Value.ToString()] = playerDamage.GetValueOrDefault(d.Attacker!.Value.ToString()) + d.Amount;
            }

            lastLog = log;
        }

        // 胜 = 敌方全灭（PlayerVictory）或 撤退成功（DrawRetreat 口径按玩家撤离胜出）；RoundLimit 不计胜
        double win = outcomes.Count(o => o.Result == GameResult.PlayerVictory || o.Result == GameResult.DrawRetreat);
        double avg = outcomes.Average(o => o.Rounds);
        var report = new SimulationReport(
            runs,
            win / runs,
            avg,
            outcomes.Min(o => o.Rounds),
            outcomes.Max(o => o.Rounds),
            outcomes.Sum(o => o.CollapseCount),
            outcomes.Sum(o => o.WeakCount),
            outcomes.Sum(o => o.DeathDoorRolls),
            outcomes.Average(o => o.DeathDoorRolls),
            outcomes.Count(o => o.AnyRetreat),
            outcomes.Sum(o => o.VirtueCount),
            outcomes.Sum(o => o.AfflictionCount),
            outcomes.Sum(o => o.Displacements),
            MergeSkills(outcomes),
            MergeHistogram(outcomes),
            playerDamage,
            outcomes.Count(o => o.Result == GameResult.RoundLimit),
            lastLog is null ? null : Dump(lastLog));
        return report;
    }

    private static Dictionary<string, int> MergeSkills(List<GameOutcome> os)
    {
        var m = new Dictionary<string, int>();
        foreach (var o in os)
        {
            foreach ((string k, int v) in o.SkillUses)
            {
                m[k] = m.GetValueOrDefault(k) + v;
            }
        }

        return m;
    }

    private static Dictionary<int, int> MergeHistogram(List<GameOutcome> os)
    {
        var m = new Dictionary<int, int>();
        foreach (var o in os)
        {
            foreach ((int k, int v) in o.MoraleHistogram)
            {
                m[k] = m.GetValueOrDefault(k) + v;
            }
        }

        return m;
    }

    /// <summary>紧凑日志（统计字段/确定性留档）。</summary>
    public static string Dump(CombatLog log)
        => string.Join("|", log.Events.Select(e => e is RngDraw r ? $"D{r.DrawCount}:{r.Value.ToString("F2")}" : e.ToString()));

    private static class DirectorBuilders
    {
        public static BattleDirector Build(CombatLog log, Func<TuningConfig, TuningConfig>? tweak,
            Func<UnitsConfig, UnitsConfig>? unitsTweak, Func<SkillsConfig, SkillsConfig>? skillsTweak)
        {
            TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
            if (tweak is not null)
            {
                tuning = tweak(tuning);
            }

            UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
            if (unitsTweak is not null)
            {
                units = unitsTweak(units);
            }

            SkillsConfig skills = SkillsConfig.Parse(ReadData("skills.json"));
            if (skillsTweak is not null)
            {
                skills = skillsTweak(skills);
            }

            return new BattleDirector(
                FormationConfig.Parse(ReadData("formation.json")),
                units,
                skills,
                BalanceTable.FromTuning(tuning),
                MoraleEventsConfig.Parse(ReadData("morale_events.json")),
                BuffDefsConfig.Parse(ReadData("buff_defs.json")),
                EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
                log);
        }

        private static string ReadData(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, "data", name);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                dir = dir.Parent;
            }

            throw new FileNotFoundException($"data/{name} 未找到。");
        }
    }
}