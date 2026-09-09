using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Buffs;
using Darkest.Gameplay.Sim.Pipeline;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M4 状态机组合用例（T-M4-09，blueprint §5c）：Afflicted→Normal（折磨结束=回 50）、
/// Virtuous→Afflicted（美德归零消除→回 50→重判=折磨）、Virtuous→Virtuous（重判=美德上限 1 保持）。
/// Normal→Weak→Dead 全链由 CombatResolutionTests 覆盖。
/// </summary>
[TestClass]
public sealed class StateMachineTests
{
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _p;
        private readonly Queue<int> _ints;
        private ulong _d;

        public ScriptedRng(double[] percents, int[] ints)
        {
            _p = new Queue<double>(percents);
            _ints = new Queue<int>(ints);
        }

        public double NextPercent()
        {
            _d++;
            return _p.Count > 0 ? _p.Dequeue() : 0.0;
        }

        public int NextInt(int a, int b)
        {
            _d++;
            return _ints.Count > 0 ? _ints.Dequeue() : a;
        }

        public ulong DrawCount => _d;
    }

    private static string FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    private static (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs, UnitsConfig units) Data()
        => (BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json")))),
            MoraleEventsConfig.Parse(File.ReadAllText(FindDataFile("morale_events.json"))),
            BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json"))),
            UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json"))));

    private static UnitRuntime Warrior(int morale) => new(UnitId.Of("warrior"), FormationSide.Player,
        UnitStatsMapper.From(Data().units.Get("warrior"))) { Morale = morale };

    [TestMethod]
    public void Afflicted_To_Normal_WhenMoraleBackTo50()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs, _) = Data();
        var buffLedger = new BuffLedger(buffs);
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, buffLedger);
        UnitRuntime u = Warrior(0);
        u.CollapseEmber = true; // 先经 Apply 达成停在 0 的余烬态
        buffLedger.Add(u.Id, "affliction_fear", source: null);
        Assert.IsTrue(ledger.IsCollapseEmber(u));

        ledger.Apply(u, 50, "morale_recovery", log); // 士气回 50
        Assert.IsFalse(buffLedger.Has(u.Id, "affliction_fear"), "折磨结束 = 士气回 50（morale §4.0）");
        Assert.IsFalse(ledger.IsCollapseEmber(u), "同一时刻恢复判定资格");
    }

    [TestMethod]
    public void Virtuous_To_Afflicted_VirtueRemoved_RejudgeAsAffliction()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs, _) = Data();
        var buffLedger = new BuffLedger(buffs);
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, buffLedger);
        UnitRuntime u = Warrior(10);
        buffLedger.Add(u.Id, "virtue_brave", source: null);
        ledger.ResetActionTracker();

        // 士气 10 → 归零（模拟美德中再归 0）：roll 40 ≥ 美德率 35 → 重判 = 折磨
        ledger.Apply(u, -10, "mental_hit", log);
        ledger.CheckCollapseTrigger(u, new ScriptedRng(new[] { 40.0 }, new[] { 0 }), log);

        Assert.IsFalse(buffLedger.Has(u.Id, "virtue_brave"), "美德消除（morale §8）");
        Assert.AreEqual(balance.MoraleStart, u.Morale, "消除美德 → 士气回 50 → 重新判定");
        Assert.IsTrue(buffLedger.Buffs(u.Id).Any(b => b.StartsWith("affliction_", StringComparison.Ordinal)),
            "重判结果 = 折磨");
    }

    [TestMethod]
    public void Virtuous_To_Virtuous_RejudgeVirtue_KeepsOne()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs, _) = Data();
        var buffLedger = new BuffLedger(buffs);
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, buffLedger);
        UnitRuntime u = Warrior(0);
        buffLedger.Add(u.Id, "virtue_brave", source: null);
        ledger.ResetActionTracker();

        // 美德中再归 0 重判 roll 20 < 35 → 再得美德：旧已消除 → 挂新（上限 1 保持）
        ledger.CheckCollapseTrigger(u, new ScriptedRng(new[] { 20.0 }, new[] { 0 }), log);

        Assert.AreEqual(1, buffLedger.Buffs(u.Id).Count(b => b.StartsWith("virtue_", StringComparison.Ordinal)),
            "Virtuous→Virtuous：上限 1 不叠层（#94）");
    }
}