using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **补欠账：`self_damage_fixed`（殊死一搏 6 ／ 舍身 8）**
/// 契约出处：`m3_skills_units.md:228/247`（自我伤害固定值、**可致死（走死门）**）·
/// `m3_skills_units.md:312`（🔴「`self_damage` 致死死门链**归 M4**」= 明确移交承诺）·
/// `data_schema.md:207`（**不被护盾吸收、可致死**）。
/// 实测此前：**全仓无消费点** ⇒ 这两张牌不会自伤 ⇒ 本用例锁住接线后的行为。
/// </summary>
[TestClass]
public sealed class SelfDamageTests
{
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

    [TestMethod]
    public void DataContract_TwoSkillsCarryFixedSelfDamage()
    {
        SkillsConfig cfg = SkillsConfig.Parse(ReadData("skills.json"));
        var carriers = cfg.Skills.Where(s => s.SelfDamageFixed is > 0).ToArray();

        Assert.AreEqual(2, carriers.Length, "恰 2 条（殊死一搏 / 舍身 —— `m3_skills_units.md:255`）");
        Assert.AreEqual(6, carriers.Single(s => s.Id == "warrior_last_stand").SelfDamageFixed, "殊死一搏 6");
        Assert.AreEqual(8, carriers.Single(s => s.Id == "tank_selfless_charge").SelfDamageFixed, "舍身 8");
    }

    [TestMethod]
    public void LastStand_DamagesCaster_FixedValue_AndWritesSelfAxisEvent()
    {
        var log = new CombatLog();
        BattleDirector d = MonteCarlo.HeadlessDriver.NewDirector(log);
        UnitRuntime warrior = d.Player.UnitsInSlotOrder().First(u => u.ArchetypeId == "warrior");
        int hp0 = warrior.CurrentHp;

        Assert.IsTrue(d.PlayerUseSkill(warrior.Id, "warrior_last_stand", new RngProvider(20260909)),
            "殊死一搏应可用（SP 足够 / 目标合法）");
        Assert.AreEqual(hp0 - 6, warrior.CurrentHp, "🔴 自伤固定 6（不吃攻击力、不经物防减免）");

        DamageEvent[] selfEvents = log.Events.OfType<DamageEvent>().Where(e => e.Axis == "self").ToArray();
        Assert.AreEqual(1, selfEvents.Length, "自伤写一条 DamageEvent（axis=self，可审计）");
        Assert.AreEqual(6, selfEvents[0].Amount);
        Assert.AreEqual(warrior.Id, selfEvents[0].Attacker, "攻击者 = 自己（契约：self_damage）");
    }

    [TestMethod]
    public void SelfDamage_CanBeLethal_GoesThroughDeathsDoor()
    {
        var log = new CombatLog();
        BattleDirector d = MonteCarlo.HeadlessDriver.NewDirector(log);
        UnitRuntime warrior = d.Player.UnitsInSlotOrder().First(u => u.ArchetypeId == "warrior");

        // 把 HP 压到"自伤可直接致死"的水平（> 0 ⇒ 仍能行动）⇒ 契约：可致死，且走死门
        warrior.CurrentHp = 6;

        Assert.IsTrue(d.PlayerUseSkill(warrior.Id, "warrior_last_stand", new RngProvider(7)),
            "HP 仍 > 0 ⇒ 技能可用");
        Assert.IsTrue(warrior.Weak || log.Events.OfType<DeathDoorEvent>().Any(),
            "🔴 自伤致死 ⇒ **走既有死亡/死门链**（`m3_skills_units.md:312`：归 M4 的那条链）");
    }
}
