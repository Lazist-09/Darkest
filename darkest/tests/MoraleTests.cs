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
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M4 士气与生存（T-M4-01~07）：唯一写入口/钳制/事件触发崩溃判定/美德·折磨池/满值/
/// 虚弱回升复算/归队。#157 物理不掉士气由 M2 覆盖。
/// </summary>
[TestClass]
public sealed class MoraleTests
{
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _p;
        private readonly Queue<int> _ints;
        private ulong _d;

        public ScriptedRng(double[]? percents = null, int[]? ints = null)
        {
            _p = new Queue<double>(percents ?? Array.Empty<double>());
            _ints = new Queue<int>(ints ?? Array.Empty<int>());
        }

        public double NextPercent()
        {
            _d++;
            return _p.Count > 0 ? _p.Dequeue() : 0.0;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            _d++;
            return _ints.Count > 0 ? _ints.Dequeue() : minInclusive;
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

    private static (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs) Data()
        => (BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json")))),
            MoraleEventsConfig.Parse(File.ReadAllText(FindDataFile("morale_events.json"))),
            BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json"))));

    private static UnitRuntime Warrior(int morale = 50)
    {
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        UnitRuntime u = new(UnitId.Of("warrior"), FormationSide.Player, UnitStatsMapper.From(units.Get("warrior")));
        u.Morale = morale;
        return u;
    }

    // ------------------------------------------------------------------ T-M4-01

    [TestMethod]
    public void Apply_Clamps_Min0Max100_Start50()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig _) = Data();
        var ledger = new MoraleLedger(balance, events);
        UnitRuntime u = Warrior(50);
        ledger.Apply(u, -1000, "probe", new CombatLog());
        Assert.AreEqual(0, u.Morale);
        Assert.IsTrue(u.CollapseEmber, "停在 0 → 崩溃余烬标记");
        ledger.Apply(u, 1000, "probe", new CombatLog());
        Assert.AreEqual(100, u.Morale);
        Assert.IsFalse(u.CollapseEmber);
    }

    [TestMethod]
    public void Uniqueness_ApplyIsTheOnlyWriter_PipelineMenal_Minus8()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig _) = Data();
        var ledger = new MoraleLedger(balance, events);
        UnitRuntime u = Warrior(50);
        ledger.ApplyIncomingDamageMorale(u, "mental", crit: false, isAoe: false, new CombatLog());
        Assert.AreEqual(42, u.Morale, "精神 −8");

        UnitRuntime v = Warrior(50);
        ledger.ApplyIncomingDamageMorale(v, "mental", crit: true, isAoe: false, new CombatLog());
        Assert.AreEqual(38, v.Morale, "精神暴击 −12");

        UnitRuntime w = Warrior(50);
        ledger.ApplyIncomingDamageMorale(w, "physical", crit: false, isAoe: false, new CombatLog());
        Assert.AreEqual(50, w.Morale, "物理不掉士气（#157）");
    }

    // ------------------------------------------------------------------ T-M4-02/03 崩溃判定

    [TestMethod]
    public void CollapseTrigger_CrossZero_ExactlyOnce_GrantsAffliction()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs) = Data();
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, new BuffLedger(buffs));
        UnitRuntime u = Warrior(10);
        ledger.ResetActionTracker();

        ledger.Apply(u, -8, "mental_hit", log); // 10 → 2 未跨 0
        Assert.AreEqual(0, log.Events.OfType<CollapseResultEvent>().Count(), "未跨 0 不判定");
        ledger.Apply(u, -2, "mental_hit", log); // 2 → 0 跨 0
        ledger.CheckCollapseTrigger(u, new ScriptedRng(percents: new[] { 90.0 }, ints: new[] { 0 }), log);

        CollapseResultEvent[] results = log.Events.OfType<CollapseResultEvent>().ToArray();
        Assert.AreEqual(1, results.Length, "恰好触发 1 次判定");
        Assert.AreEqual("Affliction", results[0].Kind);
        Assert.AreEqual(0, u.Morale, "判定后士气留在 0");
        Assert.IsTrue(u.CollapseEmber);
    }

    [TestMethod]
    public void Ember_NoRepeatCollapse_OnFurtherHits()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs) = Data();
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, new BuffLedger(buffs));
        UnitRuntime u = Warrior(0); // 已在 0（余烬）
        ledger.ResetActionTracker();
        ledger.Apply(u, -5, "weak_hit_any_damage", log);
        // 已在 0 → 无跨 0 → 不触发判定（HP 归零路径除外，由 EnterWeak 单独调用）
        Assert.AreEqual(0, log.Events.OfType<CollapseResultEvent>().Count());
    }

    [TestMethod]
    public void HpZeroTrigger_Dedup_WithCrossZero()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs) = Data();
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, new BuffLedger(buffs));
        UnitRuntime u = Warrior(10);
        ledger.ResetActionTracker();
        ledger.Apply(u, -8, "mental_hit", log); // 10→2
        ledger.Apply(u, -2, "mental_hit", log); // 2→0
        ledger.CheckCollapseTrigger(u, new ScriptedRng(ints: new[] { 0 }), log); // 跨 0 → 判定 1
        ledger.TriggerCollapseForHpZero(u, new ScriptedRng(), log); // HP 归零路径 → 去重跳过

        Assert.AreEqual(1, log.Events.OfType<CollapseResultEvent>().Count(), "同一伤害事件两条路径只判定一次");
    }

    [TestMethod]
    public void VirtueRate_Boundary_RollBelowVirtue_AboveAffliction()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs) = Data();
        var buffLedger = new BuffLedger(buffs);
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, buffLedger);

        // 韧性 50 → 美德率 = 10 + 50/2 = 35%；roll<35 美德（池 idx0 → virtue_brave），roll=35 折磨
        UnitRuntime a = Warrior(0);
        ledger.ResetActionTracker();
        ledger.TriggerCollapseForHpZero(a, new ScriptedRng(percents: new[] { 34.5 }, ints: new[] { 0 }), log);
        Assert.AreEqual("Virtue", log.Events.OfType<CollapseResultEvent>().Last().Kind);
        Assert.IsTrue(buffLedger.Has(a.Id, "virtue_brave"));

        UnitRuntime b = Warrior(0);
        ledger.ResetActionTracker();
        ledger.TriggerCollapseForHpZero(b, new ScriptedRng(percents: new[] { 35.0 }, ints: new[] { 0 }), log);
        Assert.AreEqual("Affliction", log.Events.OfType<CollapseResultEvent>().Last().Kind);
        Assert.IsTrue(buffLedger.Buffs(b.Id).Any(id => id.StartsWith("affliction_", StringComparison.Ordinal)),
            "折磨池三选一（tuning collapse.affliction_pool）");
    }

    [TestMethod]
    public void RollCollapse_VirtueGranted_WhenRollBelowRate_BraveOnly()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs) = Data();
        var buffLedger = new BuffLedger(buffs);
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, buffLedger);
        UnitRuntime u = Warrior(0); // 韧性 50 → 美德率 35%
        ledger.ResetActionTracker();

        ledger.TriggerCollapseForHpZero(u, new ScriptedRng(percents: new[] { 20.0 }, ints: new[] { 0 }), log); // 20<35 → 美德；池随机 idx 0 → virtue_brave
        CollapseResultEvent r = log.Events.OfType<CollapseResultEvent>().Single();
        Assert.AreEqual("Virtue", r.Kind);
        Assert.AreEqual("virtue_brave", buffLedger.Buffs(u.Id).Single(), "切片美德池仅 [勇猛]（O-27）");
        Assert.AreEqual(25, buffLedger.PercentMod(u.Id, "dealt_damage_mult"), "勇猛造成伤害 +25%");
        Assert.IsTrue(buffLedger.HasStateFlag(u.Id, "immune_fear"), "勇猛免疫恐惧");
    }

    // ------------------------------------------------------------------ T-M4-05 满值

    [TestMethod]
    public void MoralMax_GrantsVirtue_TeamOnce10_SelfBackTo50()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig buffs) = Data();
        var buffLedger = new BuffLedger(buffs);
        var log = new CombatLog();
        var ledger = new MoraleLedger(balance, events, buffLedger);
        UnitRuntime[] team = { Warrior(50), UnitRuntimeAlias("tank") };
        ledger.ResetActionTracker();

        UnitRuntime u = team[0];
        ledger.HandleMoraleMax(u, team, new ScriptedRng(ints: new[] { 0 }), log);

        Assert.AreEqual("virtue_brave", buffLedger.Buffs(u.Id).Single(), "冲满挂 1 美德（随机抽取 #55）");
        Assert.AreEqual(50, u.Morale, "自身回 50");
        MoraleEvent[] teamEvents = log.Events.OfType<MoraleEvent>().Where(m => m.Source == "morale_full_100").ToArray();
        Assert.AreEqual(2, teamEvents.Length, "全队各 +10（一次性）");

        // 再次冲满：保留旧美德、全队不再 +10
        u.Morale = 100;
        ledger.HandleMoraleMax(u, team, new ScriptedRng(ints: new[] { 0 }), log);
        Assert.AreEqual(1, buffLedger.Buffs(u.Id).Count, "美德不替换不叠加（#94/#100）");
        Assert.AreEqual(2, log.Events.OfType<MoraleEvent>().Count(m => m.Source == "morale_full_100"), "全队 +10 只结算一次（once_per_battle）");
    }

    // ------------------------------------------------------------------ T-M4-07 虚弱回升 / 归队

    [TestMethod]
    public void RecoveryAmount_Samples()
    {
        (BalanceTable balance, _, _) = Data();
        Assert.AreEqual(15, MoraleLedger.RecoveryAmount(100, 0, balance), "韧性100+0健康支援位 → 10+0+5=15");
        Assert.AreEqual(23, MoraleLedger.RecoveryAmount(60, 2, balance), "韧性60+2 → 10+10+3=23");
        Assert.AreEqual(25, MoraleLedger.RecoveryAmount(100, 3, balance), "10+15+5=30 → 钳 25");
        Assert.AreEqual(10 + 100 / 20, MoraleLedger.RecoveryAmount(100, 0, balance));
    }

    [TestMethod]
    public void Recovery_ExcludesWeakSupportAllies()
    {
        (BalanceTable balance, MoraleEventsConfig events, BuffDefsConfig _) = Data();
        // 支援位 5/6 均虚弱 → 健康支援位 0 → 仅 10 + 韧性÷20（不含 +5×2）
        UnitRuntime target = Warrior(0); target.Weak = true;
        UnitRuntime s5 = Warrior(0); s5.Weak = true;
        UnitRuntime s6 = UnitRuntimeAlias("medic"); s6.Weak = true;
        FormationBoard player = new(FormationSide.Player, new SlotLayout(6, 4, new[] { 5, 6 }), FormationRules.Default(),
            new Dictionary<int, UnitRuntime> { [2] = target, [5] = s5, [6] = s6 });

        var ledger = new MoraleLedger(balance, events);
        Assert.AreEqual(10 + 50 / 20, ledger.SupportSlotRegen(target, player),
            "虚弱者互不提供加成（morale §9 / #59）：不含 +5×2");
    }

    [TestMethod]
    public void TryRecover_Hp10Percent_OnMoraleAtStart()
    {
        (BalanceTable balance, _, _) = Data();
        UnitRuntime u = Warrior(0);
        u.Weak = true;
        u.Morale = 49;
        Assert.IsFalse(WeakDeathsDoor.TryRecover(u, balance), "士气 <50 不归队");
        u.Morale = 50;
        Assert.IsTrue(WeakDeathsDoor.TryRecover(u, balance), "#164 士气回初始值归队");
        Assert.IsFalse(u.Weak);
        Assert.AreEqual(4, u.CurrentHp, "HP = 最大血量 10%（40×0.1=4）");
    }

    private static UnitRuntime UnitRuntimeAlias(string id)
    {
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        UnitRuntime u = new(UnitId.Of(id), FormationSide.Player, UnitStatsMapper.From(units.Get(id)));
        u.Morale = 50;
        return u;
    }
}