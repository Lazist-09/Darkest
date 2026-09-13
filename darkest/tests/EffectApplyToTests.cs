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
/// 🔴 **补欠账：`apply_to`（effects 的【挂载对象】）** —— 契约 `data_schema.md:259`：
/// 缺省 = `targets` ／ `self`（盾墙的自身物防 +6）／ `ally_targets`（守护挂相邻友方）。
/// 实测此前：**处理器完全不读它** ⇒ 5 个 `apply_to=self` + 1 个 `apply_to=team` 的挂载语义未生效。
///
/// ⚠️ **同时登记一处契约/数据不一致**：数据里用了 **`team`**，而契约只列了 `self`/`ally_targets`
/// ⇒ 本用例按【数据语义】锁行为，并把不一致如实写在这里（待对账）。
/// </summary>
[TestClass]
public sealed class EffectApplyToTests
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
    public void DataContract_ApplyToValues_UsedBySixEffects()
    {
        SkillsConfig cfg = SkillsConfig.Parse(ReadData("skills.json"));
        (string Id, string ApplyTo)[] withApplyTo = cfg.Skills
            .SelectMany(s => s.Effects.Where(e => e.ApplyTo is not null)
                .Select(e => (Id: s.Id, ApplyTo: e.ApplyTo!)))
            .ToArray();

        Assert.AreEqual(6, withApplyTo.Length, "共 6 处 effect 声明了 apply_to");
        Assert.AreEqual(5, withApplyTo.Count(x => x.ApplyTo == "self"), "`self` ×5（盾墙系列等）");
        Assert.AreEqual(1, withApplyTo.Count(x => x.ApplyTo == "team"),
            "`team` ×1（总动员）—— ⚠️ 该取值**不在契约列表**里（`data_schema.md:259` 只列 self/ally_targets）⇒ 待对账");
    }

    [TestMethod]
    public void ApplyToSelf_ShieldLandsOnCaster_NotOnTarget()
    {
        var log = new CombatLog();
        BattleDirector d = MonteCarlo.HeadlessDriver.NewDirector(log);
        UnitRuntime tank = d.Player.UnitsInSlotOrder().First(u => u.ArchetypeId == "tank");

        // 铁壁（tank_iron_wall）的 effect 是 shield(charges=2, apply_to=self)
        Assert.IsTrue(d.PlayerUseSkill(tank.Id, "tank_iron_wall", new RngProvider(20260909)),
            "铁壁应可用");

        EffectEvent[] shields = log.Events.OfType<EffectEvent>()
            .Where(e => e.EffectType == "shield").ToArray();
        Assert.IsTrue(shields.Length >= 1, "护盾 effect 应被施加并写事件");
        Assert.IsTrue(shields.All(e => e.Target == tank.Id),
            $"🔴 `apply_to=self` ⇒ 护盾必须挂在【施法者自己】身上；实际：{string.Join(",", shields.Select(e => e.Target?.ToString() ?? "null"))}");
    }

    [TestMethod]
    public void ApplyToTeam_EffectLandsOnWholePlayerSide()
    {
        var log = new CombatLog();
        BattleDirector d = MonteCarlo.HeadlessDriver.NewDirector(log);
        UnitRuntime commissar = d.Player.UnitsInSlotOrder().First(u => u.ArchetypeId == "commissar");
        int alive = d.Player.UnitsInSlotOrder().Count(u => u.CurrentHp > 0);

        Assert.IsTrue(d.PlayerUseSkill(commissar.Id, "commissar_mobilize", new RngProvider(20240909)),
            "总动员应可用");

        // `apply_to=team` ⇒ effects 挂到【我方全体】⇒ 事件里应有多个（≥2）受挂单位
        EffectEvent[] statMods = log.Events.OfType<EffectEvent>()
            .Where(e => e.EffectType == "stat_mod").ToArray();
        int distinct = statMods.Select(e => e.Target).Distinct().Count();
        Assert.IsTrue(distinct >= 2,
            $"🔴 `apply_to=team` ⇒ 应挂到全队（存活 {alive} 人）；实测受挂单位 {distinct} 个");
    }
}
