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
/// M4 护盾/护卫（T-M4-08/#156/#159）：被挡完全无效、精神穿透、AOE 整攻击挡掉、
/// 两次动作分别消耗、护卫重定向（按保护者防御、每回合≤1、虚弱护者走死门）。#136 被守护者 +3 士气由 M4 归档。
/// </summary>
[TestClass]
public sealed class ShieldGuardTests
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
            return _p.Count > 0 ? _p.Dequeue() : 0.0;
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

    private static (BalanceTable balance, MoraleEventsConfig morale, SkillsConfig skills, UnitsConfig units) Data()
        => (BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json")))),
            MoraleEventsConfig.Parse(File.ReadAllText(FindDataFile("morale_events.json"))),
            SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json"))),
            UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json"))));

    private static UnitRuntime U(string id, FormationSide side)
        => new(UnitId.Of(id), side, UnitStatsMapper.From(Data().units.Get(id)));

    private static FormationBoard Board(FormationSide side, params (int slot, UnitRuntime unit)[] placed)
        => new(side, side == FormationSide.Player ? new SlotLayout(6, 4, new[] { 5, 6 }) : new SlotLayout(4, 4, Array.Empty<int>()),
            FormationRules.Default(), placed.ToDictionary(p => p.slot, p => p.unit));

    [TestMethod]
    public void Shield_BlocksPhysical_FullyInvalid()
    {
        (BalanceTable balance, MoraleEventsConfig morale, _, _) = Data();
        BuffDefsConfig buffDefs = BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json")));
        var buffs = new BuffLedger(buffDefs);
        var shield = new ShieldGuard(buffs);
        var log = new CombatLog();
        var pipeline = new DamagePipeline(balance, morale, log, buffs, shield);

        FormationBoard player = Board(FormationSide.Player, (1, U("tank", FormationSide.Player)), (2, U("warrior", FormationSide.Player)));
        FormationBoard enemy = Board(FormationSide.Enemy, (1, U("melee_soldier", FormationSide.Enemy)));
        UnitRuntime tank = player.UnitRuntimeAt(1)!;
        buffs.AddCharged(tank.Id, "shield", 2);
        pipeline.InitializeMorale(player);

        // 敌方 melee 打坦克（物理 9）：被挡完全无效
        pipeline.Execute(new SkillFixture("probe", UnitId.Of("melee_soldier"), FormationSide.Player,
            new[] { 1 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null), player, enemy, new ScriptedRng(0.0, 100.0));

        Assert.AreEqual(55, tank.CurrentHp, "被挡不扣 HP");
        Assert.AreEqual(1, buffs.Charges(tank.Id, "shield"), "次数 2→1");
        Assert.IsFalse(log.Events.OfType<DamageEvent>().Any(), "被挡无伤害事件");
        Assert.IsFalse(log.Events.OfType<MoraleEvent>().Any(), "被挡士气不变");
        Assert.IsFalse(log.Events.OfType<DeathDoorEvent>().Any(), "被挡不触发死门");
    }

    [TestMethod]
    public void Shield_MentalPenetrates_NoConsume()
    {
        (BalanceTable balance, MoraleEventsConfig morale, _, _) = Data();
        BuffDefsConfig buffDefs = BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json")));
        var buffs = new BuffLedger(buffDefs);
        var shield = new ShieldGuard(buffs);
        var log = new CombatLog();
        var pipeline = new DamagePipeline(balance, morale, log, buffs, shield);
        pipeline.InitializeMorale(playerBoard());

        FormationBoard enemy = Board(FormationSide.Enemy, (1, U("melee_soldier", FormationSide.Enemy)), (4, U("caster", FormationSide.Enemy)));
        FormationBoard player = playerBoard();
        UnitRuntime tank = player.UnitRuntimeAt(1)!;
        buffs.AddCharged(tank.Id, "shield", 2);

        // 精神攻击穿透护盾（伤害照常 + 士气 -8）
        pipeline.Execute(new SkillFixture("probe", UnitId.Of("caster"), FormationSide.Player,
            new[] { 1 }, HitMod: 0, CritMod: 0, Axis: "mental", Segments: new[] { 0.8 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null), player, enemy, new ScriptedRng(0.0, 100.0));

        Assert.AreEqual(2, buffs.Charges(tank.Id, "shield"), "精神不消耗次数（#156）");
        Assert.AreEqual(48, tank.CurrentHp, "精神伤害照常（坦克韧 55 → 减免 22% → 7）");
        Assert.IsTrue(log.Events.OfType<DamageEvent>().Any());
    }

    [TestMethod]
    public void Shield_TwoActions_ConsumesTwice_ThenExpires()
    {
        (BalanceTable balance, MoraleEventsConfig morale, _, _) = Data();
        BuffDefsConfig buffDefs = BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json")));
        var buffs = new BuffLedger(buffDefs);
        var shield = new ShieldGuard(buffs);
        var log = new CombatLog();
        var pipeline = new DamagePipeline(balance, morale, log, buffs, shield);

        FormationBoard player = playerBoard();
        FormationBoard enemy = Board(FormationSide.Enemy, (1, U("melee_soldier", FormationSide.Enemy)));
        UnitRuntime tank = player.UnitRuntimeAt(1)!;
        buffs.AddCharged(tank.Id, "shield", 2);
        var skill = new SkillFixture("probe", UnitId.Of("melee_soldier"), FormationSide.Player,
            new[] { 1 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null);

        pipeline.Execute(skill, player, enemy, new ScriptedRng(0.0, 100.0)); // 挡 1（2→1）
        pipeline.Execute(skill, player, enemy, new ScriptedRng(0.0, 100.0)); // 挡 2（1→0 移除）
        Assert.AreEqual(0, buffs.Charges(tank.Id, "shield"));
        Assert.IsFalse(buffs.Has(tank.Id, "shield"));
        Assert.AreEqual(55, tank.CurrentHp);

        pipeline.Execute(skill, player, enemy, new ScriptedRng(0.0, 100.0)); // 无盾：伤害照常
        Assert.AreEqual(46, tank.CurrentHp, "无盾受击 55−9=46");
    }

    [TestMethod]
    public void Guard_RedirectsPhysical_ToAdjacentProtector_ByProtectorDef()
    {
        (BalanceTable balance, MoraleEventsConfig morale, _, _) = Data();
        BuffDefsConfig buffDefs = BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json")));
        var buffs = new BuffLedger(buffDefs);
        var shield = new ShieldGuard(buffs);
        var log = new CombatLog();
        var pipeline = new DamagePipeline(balance, morale, log, buffs, shield);

        // 我方：1=tank(持 guard_attach, def12), 2=warrior(def8 def)
        FormationBoard player = Board(FormationSide.Player, (1, U("tank", FormationSide.Player)), (2, U("warrior", FormationSide.Player)));
        FormationBoard enemy = Board(FormationSide.Enemy, (1, U("melee_soldier", FormationSide.Enemy)));
        UnitRuntime tank = player.UnitRuntimeAt(1)!;
        UnitRuntime warrior = player.UnitRuntimeAt(2)!;
        buffs.Add(tank.Id, "guard_attach", source: null);
        pipeline.InitializeMorale(player);

        // melee 打 2 号位（邻 1 号坦克）→ 重定向：伤害按坦克 def（12）算 → 9? 战士 def8 → 若直伤 9；坦克 def12 减免 12/42 → 12×1.0×0.7143=8.57→9（同 9？战士 def8 也是 9）。取 tank.PhysDef 12 → 9 vs warrior 8 → 9 相同（都 round 到 9）。改用精神? 护卫只物理。换验证：重定向后坦克 HP 减、战士 HP 不减。
        pipeline.Execute(new SkillFixture("probe", UnitId.Of("melee_soldier"), FormationSide.Player,
            new[] { 2 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null), player, enemy, new ScriptedRng(0.0, 100.0));

        Assert.AreEqual(55 - 9, tank.CurrentHp, "坦克替挡按坦克防御受伤（9）");
        Assert.AreEqual(40, warrior.CurrentHp, "战士未被打");
        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "guard_redirect"));
    }

    [TestMethod]
    public void Guard_MaxOneRedirectPerTurn_SecondPassesThrough()
    {
        (BalanceTable balance, MoraleEventsConfig morale, _, _) = Data();
        BuffDefsConfig buffDefs = BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json")));
        var buffs = new BuffLedger(buffDefs);
        var shield = new ShieldGuard(buffs);
        var log = new CombatLog();
        var pipeline = new DamagePipeline(balance, morale, log, buffs, shield);

        FormationBoard player = Board(FormationSide.Player, (1, U("tank", FormationSide.Player)), (2, U("warrior", FormationSide.Player)));
        FormationBoard enemy = Board(FormationSide.Enemy, (1, U("melee_soldier", FormationSide.Enemy)));
        buffs.Add(player.UnitRuntimeAt(1)!.Id, "guard_attach", source: null);
        pipeline.InitializeMorale(player);
        var skill = new SkillFixture("probe", UnitId.Of("melee_soldier"), FormationSide.Player,
            new[] { 2 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null);

        pipeline.Execute(skill, player, enemy, new ScriptedRng(0.0, 100.0)); // 第 1 次：重定向坦克
        UnitRuntime tank1 = player.UnitRuntimeAt(1)!;
        Assert.AreEqual(46, tank1.CurrentHp);
        Assert.AreEqual(40, player.UnitRuntimeAt(2)!.CurrentHp);

        pipeline.Execute(skill, player, enemy, new ScriptedRng(0.0, 100.0)); // 同回合第 2 次：不再重定向
        Assert.AreEqual(46, tank1.CurrentHp, "每回合最多重定向 1 次（#159）：第二次直打战士");
        Assert.AreEqual(31, player.UnitRuntimeAt(2)!.CurrentHp, "战士 40−9");
    }

    private static FormationBoard playerBoard()
        => Board(FormationSide.Player, (1, U("tank", FormationSide.Player)), (2, U("warrior", FormationSide.Player)));
}