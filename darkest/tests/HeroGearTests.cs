using System;
using System.Collections.Generic;
using System.IO;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// `H-1 英雄装备阶`的守卫（**精简：只守"错了但不会报错"的静默失效** ✓）
///
/// 三条各守一个家族：
///   ① **配了不生效** —— 前置齐备 + 金币够 ⇒ 阶**必须真的 +1**（否则整个 H-1 是死的，编译过、测试绿、肉眼看不见）
///   ② **静默拒绝** —— 铁匠铺等级不足 ⇒ 必须**拒绝且不扣钱**，且**带人话理由**（不返回 null 让 UI 自己猜）
///   ③ **静默封顶** —— 满阶后再升 ⇒ 必须拒绝（不是"钳住假装成功"）
/// </summary>
[TestClass]
public sealed class HeroGearTests
{
    private static HeroUpgradesConfig Cfg => HeroUpgradesConfig.Parse(ReadData("hero_upgrades.json"));

    private static HeroConfig Hero(string id, string archetype, int level = 6)
        => new(id, id, archetype, level, Array.Empty<HeroTraitConfig>());

    private static Economy NewEconomy(int gold) => new(EconomyConfig.Parse(ReadData("economy.json")), gold);

    [TestMethod]
    public void 前置齐备且金币够时_阶必须真的加一()
    {
        // 铁匠铺拉满（Lv4 = code d）⇒ 买 code0..code3 全通；决心等级 6 ≥ 5 ✓
        var state = new HeroGearState();
        HeroConfig hero = Hero("hero_warrior_1", "warrior");
        var log = new Darkest.Core.Events.CombatLog();

        Assert.AreEqual(0, state.ArmourTierOf(hero.Id), "起手必须是第 0 阶 ✓");

        int after = state.TryUpgrade(log, Cfg, NewEconomy(999999), hero, GearAxis.Armour, buildingLevel: 4);

        Assert.AreEqual(1, after, "🔴 前置齐备 + 金币够 ⇒ 阶**必须 +1**（否则 H-1 是死的：数据配了、逻辑写了、但没人真的升得了）");
        Assert.AreEqual(1, state.ArmourTierOf(hero.Id), "状态必须真的落进去（不只是返回值对）");
        Assert.AreEqual(0, state.WeaponTierOf(hero.Id), "护甲升级**不得**连带武器阶（两条树互相独立）");
    }

    [TestMethod]
    public void 铁匠铺等级不足时_必须拒绝且不扣钱_并给出理由()
    {
        var state = new HeroGearState();
        HeroConfig hero = Hero("hero_warrior_1", "warrior");
        Economy econ = NewEconomy(999999);
        int goldBefore = econ.Gold;

        // 铁匠铺 Lv0 ⇒ 连 code a（等级 1）都不满足 ⇒ 必须拒绝
        string? why = HeroGear.Why(Cfg, "warrior", GearAxis.Armour, 0, buildingLevel: 0, resolveLevel: 6, gold: 999999);
        Assert.IsNotNull(why, "🔴 前置不足必须给出**人话理由**（返回 null = UI 只能自己猜 ⇒ 静默拒绝家族）");
        Assert.IsTrue(why!.Contains("铁匠铺"), $"理由必须说清是哪一条前置不满足（实际：{why}）");

        int after = state.TryUpgrade(new Darkest.Core.Events.CombatLog(), Cfg, econ, hero, GearAxis.Armour, buildingLevel: 0);

        Assert.AreEqual(0, after, "被拒 ⇒ 阶不变");
        Assert.AreEqual(goldBefore, econ.Gold, "🔴 被拒**不得扣钱**（先扣后判 = 玩家白花一笔，且日志里看不出）");
    }

    [TestMethod]
    public void 满阶后再升_必须拒绝而不是静默钳制()
    {
        var state = new HeroGearState();
        HeroConfig hero = Hero("hero_medic_1", "medic", level: 6);
        Economy econ = NewEconomy(999999);
        var log = new Darkest.Core.Events.CombatLog();

        for (int i = 0; i < HeroGear.MaxTier; i++)
        {
            state.TryUpgrade(log, Cfg, econ, hero, GearAxis.Weapon, buildingLevel: 4);
        }

        Assert.AreEqual(HeroGear.MaxTier, state.WeaponTierOf(hero.Id), $"连升 {HeroGear.MaxTier} 次 ⇒ 应到第 {HeroGear.MaxTier} 阶");

        int after = state.TryUpgrade(log, Cfg, econ, hero, GearAxis.Weapon, buildingLevel: 4);
        Assert.AreEqual(HeroGear.MaxTier, after, "🔴 满阶后再升必须**拒绝**（钳住假装成功 = 玩家以为升了、读数却没变 ⇒ 静默失效）");
    }

    [TestMethod]
    public void 护甲阶必须真的改变减伤读数_且敌人不受影响()
    {
        // 🔴 守"接线但没接上"（红线 21）：`ApplyGearTier` 之后 `EffectiveProt` 必须按阶取，而不是仍是顶层值
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        UnitConfig warrior = units.Get("warrior");
        Assert.IsNotNull(warrior.Armour, "warrior 必须有 5 阶护甲表（否则本条测不到东西）");

        var unit = new UnitRuntime(Darkest.Core.Contracts.UnitId.Of("warrior"),
            Darkest.Core.Contracts.FormationSide.Player, UnitStatsMapper.From(warrior));

        int before = unit.EffectiveProt;
        unit.ApplyGearTier(0); // 第 0 阶
        int tier0 = unit.EffectiveProt;

        Assert.AreEqual(TierDefence.ProtAt(unit.Base, 0), tier0,
            "🔴 接上阶之后 `EffectiveProt` 必须 == `TierDefence.ProtAt(第0阶)`（否则装备阶是**写了没接上**）");
        Assert.AreNotEqual(before, tier0,
            "⚠️ 本条同时记录了 H-1 的**行为变更**：顶层 prot 与第 0 阶不一致（已实测登记，策划裁定「直接接线」）");

        // 敌人（无 5 阶）⇒ 必须退回顶层 ⇒ 逐字不变
        UnitConfig enemy = units.Get("melee_soldier");
        var foe = new UnitRuntime(Darkest.Core.Contracts.UnitId.Of("melee_soldier"),
            Darkest.Core.Contracts.FormationSide.Enemy, UnitStatsMapper.From(enemy));
        int foeBefore = foe.EffectiveProt;
        foe.ApplyGearTier(0);
        Assert.AreEqual(foeBefore, foe.EffectiveProt, "🔴 敌人没有 5 阶 ⇒ 必须**退回顶层**（否则会误伤全部敌人读数）");
    }

    // ------------------------------------------------------------------
    private static string ReadData(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string c = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(c))
            {
                return File.ReadAllText(c);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }
}
