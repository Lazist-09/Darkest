using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M3 技能执行骨架（T-M3-06）：真实技能数据桥接 DamagePipeline——多段独立实例、固定次序、
/// 未命中无事件、显式 −4 取代派生、支援技能路径、missing_hp 实际倍率、位移殿后。
/// </summary>
[TestClass]
public sealed class SkillExecutionTests
{
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _p;
        private ulong _d;

        public ScriptedRng(params double[] percents)
        {
            _p = new Queue<double>(percents);
        }

        public double NextPercent()
        {
            _d++;
            return _p.Count > 0 ? _p.Dequeue() : 50.0;
        }

        public int NextInt(int a, int b)
        {
            _d++;
            return a;
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

    private static (FormationBoard player, FormationBoard enemy, BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale) World()
    {
        FormationConfig formation = FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json")));
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        return (FormationBoardFactory.CreatePlayerBoard(formation, units),
                FormationBoardFactory.CreateEnemyBoard(formation, units),
                BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json")))),
                SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json"))),
                MoraleEventsConfig.Parse(File.ReadAllText(FindDataFile("morale_events.json"))));
    }

    private static FormationBoard RebuildPlayer(params (int slot, string id)[] placed)
    {
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        var dict = new Dictionary<int, UnitRuntime>();
        foreach ((int slot, string id) in placed)
        {
            dict[slot] = new UnitRuntime(UnitId.Of(id), FormationSide.Player, UnitStatsMapper.From(units.Get(id)));
        }

        return new FormationBoard(FormationSide.Player, new SlotLayout(6, 4, new[] { 5, 6 }), FormationRules.Default(), dict);
    }

    private static SkillExecutor NewExecutor(CombatLog log, SkillRuntimeState runtime,
        BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale)
        => new(skills, balance, morale, log, runtime);

    [TestMethod]
    public void DoubleHit_TwoSegmentInstances_OrderedDraws()
    {
        (_, FormationBoard enemy, BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale) = World();
        // 敌板仅 1 号位占用（单目标 → 双段独立实例）
        var loneEnemy = new FormationBoard(FormationSide.Enemy, new SlotLayout(4, 4, Array.Empty<int>()),
            FormationRules.Default(),
            new Dictionary<int, UnitRuntime>
            {
                [1] = new(UnitId.Of("melee_soldier"), FormationSide.Enemy, UnitStatsMapper.From(
                    UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json"))).Get("melee_soldier"))),
            });
        var log = new CombatLog();
        var executor = NewExecutor(log, new SkillRuntimeState(), balance, skills, morale);
        FormationBoard player = RebuildPlayer((1, "medic"), (2, "tank"));

        executor.Execute(skills.Get("medic_double_hit"), UnitId.Of("medic"), player, loneEnemy, new ScriptedRng(0.0, 100.0, 100.0));

        DamageEvent[] damages = log.Events.OfType<DamageEvent>().ToArray();
        Assert.AreEqual(2, damages.Length, "0.55×2 = 两段独立伤害实例（O-13）");
        Assert.AreEqual(0, damages[0].SegmentIndex);
        Assert.AreEqual(1, damages[1].SegmentIndex);
        Assert.IsTrue(damages.Sum(d => d.Amount) > 0);
        // 抽取连续可审计
        var draws = log.Events.OfType<RngDraw>().Select(d => d.DrawCount).ToArray();
        for (int i = 1; i < draws.Length; i++)
        {
            Assert.AreEqual(draws[i - 1] + 1, draws[i], "抽取序号连续无跳号");
        }
    }

    [TestMethod]
    public void HitMiss_NoDamageNoMorale()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale) = World();
        var log = new CombatLog();
        var executor = NewExecutor(log, new SkillRuntimeState(), balance, skills, morale);

        // warrior_cleave 命中率 90；敌 1、2 两目标均 roll=90 未命中
        executor.Execute(skills.Get("warrior_cleave"), UnitId.Of("warrior"), player, enemy, new ScriptedRng(90.0, 90.0));
        Assert.IsTrue(log.Events.OfType<HitEvent>().All(h => !h.Hit), "两目标均未命中");
        Assert.IsFalse(log.Events.OfType<DamageEvent>().Any());
        Assert.IsFalse(log.Events.OfType<MoraleEvent>().Any());
        Assert.AreEqual(60, enemy.UnitRuntimeAt(1)!.CurrentHp);
        Assert.AreEqual(60, enemy.UnitRuntimeAt(2)!.CurrentHp);
    }

    [TestMethod]
    public void IntimidatingShot_ExplicitMinus4_NoDerivedMinus8()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale) = World();
        var log = new CombatLog();
        var executor = NewExecutor(log, new SkillRuntimeState(), balance, skills, morale);
        executor.Pipeline.Morale.Initialize(player.UnitsInSlotOrder());

        // 远程射手(敌2) 威吓箭（单体 #178/#179）：候选 我1/我2 → 分别指定目标验证显式 −4（无 mental_hit）
        executor.Execute(skills.Get("ranged_intimidating_shot"), UnitId.Of("ranged_archer"), player, enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 1 });
        MoraleEvent[] moraleEvents = log.Events.OfType<MoraleEvent>().ToArray();
        Assert.AreEqual(1, moraleEvents.Length, "单体选一 → 单条显式士气事件");
        Assert.AreEqual(-4, moraleEvents[0].Delta, "显式 −4（O-21/#170）");
        Assert.IsFalse(log.Events.OfType<MoraleEvent>().Any(m => m.Source == "mental_hit"), "不叠加精神派生 −8");
        Assert.AreEqual(46, player.UnitRuntimeAt(1)!.Morale);
        Assert.AreEqual(50, player.UnitRuntimeAt(2)!.Morale, "未选中目标不受影响");

        // 第二次：选中 2 号位（独立执行，仅断言士气落点）
        executor.Execute(skills.Get("ranged_intimidating_shot"), UnitId.Of("ranged_archer"), player, enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 2 });
        MoraleEvent[] second = log.Events.OfType<MoraleEvent>().Where(m => m.Source == "skill_morale_effect").ToArray();
        Assert.AreEqual(46, player.UnitRuntimeAt(2)!.Morale, "选中 2 号位 −4");
        Assert.IsTrue(second.Length >= 2, "两次执行各一条显式 −4");
    }

    [TestMethod]
    public void WarCry_TeamPlus5_SupportPath_NoHitDraw()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale) = World();
        var log = new CombatLog();
        var runtime = new SkillRuntimeState();
        var executor = NewExecutor(log, runtime, balance, skills, morale);
        executor.Pipeline.Morale.Initialize(player.UnitsInSlotOrder());

        executor.Execute(skills.Get("warrior_war_cry"), UnitId.Of("warrior"), player, enemy, new ScriptedRng());
        Assert.IsFalse(log.Events.OfType<HitEvent>().Any(), "支援技能不走命中");
        MoraleEvent[] moraleEvents = log.Events.OfType<MoraleEvent>().ToArray();
        Assert.AreEqual(6, moraleEvents.Length, "团队 +5 逐成员（6 人满编，含支援位）");
        Assert.IsTrue(moraleEvents.All(m => m.Delta == 5));
        Assert.AreEqual(55, player.UnitRuntimeAt(1)!.Morale);
        Assert.AreEqual(2, runtime.CooldownRemaining(UnitId.Of("warrior"), "warrior_war_cry"), "战吼 CD 2 置位");
    }

    [TestMethod]
    public void FirstAid_HealFixed12_NotAttackScaled()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale) = World();
        var log = new CombatLog();
        var executor = NewExecutor(log, new SkillRuntimeState(), balance, skills, morale);
        FormationBoard rebuilt = RebuildPlayer((1, "medic"), (2, "warrior"));
        rebuilt.UnitRuntimeAt(2)!.CurrentHp = 1; // 战士濒死

        executor.Execute(skills.Get("medic_first_aid"), UnitId.Of("medic"), rebuilt, enemy, new ScriptedRng(), chosenTargets: new[] { 2 });
        HealEvent heal = log.Events.OfType<HealEvent>().First(e => e.Target == UnitId.Of("warrior"));
        Assert.AreEqual(12, heal.Amount, "急救固定值 12（不吃攻击力，combat_math §8）");
        Assert.AreEqual(13, rebuilt.UnitRuntimeAt(2)!.CurrentHp);
    }

    [TestMethod]
    public void MissingHp_LethalInjection_ScalesWithLostHp()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale) = World();
        var log = new CombatLog();
        var executor = NewExecutor(log, new SkillRuntimeState(), balance, skills, morale);
        FormationBoard rebuilt = RebuildPlayer((1, "medic"), (2, "tank"));

        enemy.UnitRuntimeAt(1)!.CurrentHp = 25; // 近战小兵 60 → 已失血 58.3%
        // 单体（#178/#179）：先指定敌 1（失血 58.3%）→ 倍率 1.0+0.583×0.4=1.233 → 11
        executor.Execute(skills.Get("medic_lethal_injection"), UnitId.Of("medic"), rebuilt, enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 1 });
        DamageEvent[] damages = log.Events.OfType<DamageEvent>().ToArray();
        Assert.AreEqual(1, damages.Length, "单体选一 → 单条伤害");
        Assert.AreEqual(11, damages[0].Amount, "失血目标 11（1.233 倍）");
        Assert.IsTrue(damages[0].Raw > 11 * 1.0 * (1 - 8 / 38.0), "失血目标伤害高于基础 1.0 情形");

        // 再指定满血敌 2 → 倍率 1.0 → 9（逐目标按实际 HP 计算，#171 系数 0.4）
        executor.Execute(skills.Get("medic_lethal_injection"), UnitId.Of("medic"), rebuilt, enemy,
            new ScriptedRng(0.0, 100.0), chosenTargets: new[] { 2 });
        Assert.AreEqual(9, log.Events.OfType<DamageEvent>().Last().Amount, "满血目标倍率 1.0 → 9");
    }

    [TestMethod]
    public void Lunge_PushAfterDamage_OrderAndSwap()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance, SkillsConfig skills, MoraleEventsConfig morale) = World();
        var log = new CombatLog();
        var executor = NewExecutor(log, new SkillRuntimeState(), balance, skills, morale);
        FormationBoard rebuilt = RebuildPlayer((2, "warrior"), (1, "tank"));

        // 战士突刺（self_slots [2,3]，候选 敌1/2，推1，单体 #178/#179）：指定敌 1，[命中0 暴击100 位移55]（55 ≥ melee 抗性 55）
        executor.Execute(skills.Get("warrior_lunge"), UnitId.Of("warrior"), rebuilt, enemy,
            new ScriptedRng(0.0, 100.0, 55.0), chosenTargets: new[] { 1 });
        Assert.IsTrue(log.Events.OfType<DamageEvent>().Any(), "位移失败与否不影响伤害");
        DisplaceEvent[] displaces = log.Events.OfType<DisplaceEvent>().ToArray();
        Assert.AreEqual(1, displaces.Length, "单体选一 → 单目标位移判定");
        Assert.IsTrue(displaces.All(d => d.PassedResist && d.ChainSucceeded), "roll==抗性 → 位移成功（>=）");
        int dmgIdx = log.Events.ToList().FindIndex(e => e is DamageEvent);
        int dispIdx = log.Events.ToList().FindIndex(e => e is DisplaceEvent);
        Assert.IsTrue(dmgIdx >= 0 && dispIdx > dmgIdx, "位移在本目标伤害结算后执行（§2/B5b）");
    }
}