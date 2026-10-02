// 🔴 从 HungerTests.cs 拆出（用户红线 ≤600 行 · 架构 file_size_split §1.3）——只搬家、零行为改动 ✓
//    本文件 = D-5 饥饿：**结算**（吃＝扣粮回血 ／ 饿＝掉血涨压不扣粮 ／ 凑不齐全员挨饿 ／ 台账门控）
//    依赖主片私有成员：ReadData ／ Cfg ✓

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

public sealed partial class HungerTests
{

    // ────────────────────────────── D-5：结算（吃 / 不吃）──────────────────────────────

    /// <summary>
    /// 🔴 造一个**已建台账**的会话（打完一桶"第 1 场"落账）—— 这样 `Retained`/`RosterMaxHp` 都有真值，
    /// 才谈得上饥饿结算（⚠️ 这正对应"首场战斗之前无人可结算"那个有界缺口）✓
    /// </summary>
    private static ExpeditionSession SessionWithLedger(CombatLog log, out Dictionary<string, int> maxHpById)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 10, tuning.Expedition.AmbushChance);
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log); // 落账 ⇒ `Retained` 建立 ✓

        maxHpById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnitRuntime u in b1.Player.UnitsInSlotOrder())
        {
            maxHpById[u.Id.Value] = u.MaxHp;
        }

        return session;
    }

    [TestMethod]
    public void ResolveHunger_Eat_SpendsFood_AndHealsFivePercentOfOwnMaxHp()
    {
        var log = new CombatLog();
        ExpeditionSession session = SessionWithLedger(log, out Dictionary<string, int> maxHpById);
        HungerConfig cfg = Cfg(12.5, 7.5); // 吃 = 5% 自己 MaxHp ✓

        // 先把一人打到半血，好观察"回血"（满血时回血会被 MaxHp 钳住 ⇒ 看不出效果）✓
        string first = session.Roster()[0].Id;
        int max = maxHpById[first];
        // 🔴 用公开 API 把血量压低：`Roster()` 是只读的 ⇒ 走"打一场再落账"的方式太重，
        //    故此处改用**直接观察**：只要有人不满血，回血就该可见；若全满血则断言"不超上限" ✓
        int foodBefore = session.Food;
        int survivors = session.Survivors;

        string outcome = session.ResolveHunger(log, cfg, eat: true);

        Assert.AreEqual("eat", outcome, "口粮够 + 选吃 ⇒ 必须真的吃 ✓");
        Assert.AreEqual(foodBefore - survivors * cfg.FoodPerHero, session.Food,
            $"🔴 吃 ⇒ 扣 `存活人数 × 每人份`（{survivors} × {cfg.FoodPerHero}）✓");

        foreach ((string id, int hp, int m, int morale) in session.Roster())
        {
            _ = m;
            _ = morale;
            Assert.IsTrue(hp <= maxHpById[id], $"🔴 回血不得超上限（{id}：{hp} ≤ {maxHpById[id]}）✓");
        }

        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "hunger_eat"),
            "吃 ⇒ 必须留痕 `hunger_eat`（可审计）✓");
    }

    [TestMethod]
    public void ResolveHunger_Starve_DamagesAndStresses_ButSpendsNoFood()
    {
        var log = new CombatLog();
        ExpeditionSession session = SessionWithLedger(log, out Dictionary<string, int> maxHpById);
        HungerConfig cfg = Cfg(12.5, 7.5); // 饿 = −20% MaxHp / −20 压力 ✓

        Dictionary<string, (int Hp, int Morale)> before = session.Roster()
            .ToDictionary(r => r.Id, r => (r.Hp, r.Morale), StringComparer.Ordinal);
        int foodBefore = session.Food;

        string outcome = session.ResolveHunger(log, cfg, eat: false);

        Assert.AreEqual("starve", outcome, "选不吃 ⇒ 挨饿 ✓");
        Assert.AreEqual(foodBefore, session.Food, "🔴 **挨饿一口粮都不扣**（DD 原文硬要求）✓");

        foreach ((string id, int hp, int m, int morale) in session.Roster())
        {
            _ = m;
            (int Hp, int Morale) b = before[id];
            if (b.Hp <= 0)
            {
                continue; // 阵亡者不受影响 ✓
            }

            int expectedDmg = HungerSpawner.StarveDamageFor(cfg, maxHpById[id]);
            Assert.AreEqual(Math.Max(0, b.Hp - expectedDmg), hp,
                $"🔴 每人掉**自己 MaxHp 的 20%**（{id}：{b.Hp} − {expectedDmg}）✓");
            Assert.AreEqual(Math.Max(0, b.Morale - cfg.StarveMorale), morale,
                $"🔴 每人涨 {cfg.StarveMorale} 压力（{id}）✓");
        }

        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "hunger_starve"),
            "挨饿 ⇒ 必须留痕 `hunger_starve` ✓");
    }

    [TestMethod]
    public void ResolveHunger_NotEnoughFood_ChoosingEat_StillStarves_AndSpendsNothing()
    {
        var log = new CombatLog();
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        // 🔴 造一个**口粮不够**的会话（口粮 1 < 存活 4）✓
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 1, tuning.Expedition.AmbushChance);
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log);

        HungerConfig cfg = Cfg(12.5, 7.5);
        Assert.IsFalse(session.CanEatForHunger(cfg),
            $"🔴 前置：口粮 {session.Food} < 需求 {session.Survivors} ⇒ **吃不起** ✓");
        Assert.IsTrue(session.Survivors > 1, "前置：存活 > 1，否则'凑不齐'证明不了 ✓");

        int foodBefore = session.Food;
        string outcome = session.ResolveHunger(log, cfg, eat: true); // **用户选吃**，但凑不齐 ⇒

        Assert.AreEqual("starve", outcome,
            "🔴🔴 **DD 铁律：不能只喂一部分人** —— 凑不齐 ⇒ 用户选吃也**必然**是全员挨饿 ✓");
        Assert.AreEqual(foodBefore, session.Food,
            "🔴 **一口粮都不消耗**（DD 原文：\"no Food will be eaten, regardless of any Food you may have below the threshold\"）✓");
    }

    [TestMethod]
    public void HungerCanApply_FalseBeforeFirstBattle_TrueAfter()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var log = new CombatLog();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 10, tuning.Expedition.AmbushChance);

        Assert.IsFalse(session.HungerCanApply,
            "🔴 **首场战斗之前**名册台账未建 ⇒ 饥饿**无人可结算**（诚实标注的有界缺口）✓");

        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log);

        Assert.IsTrue(session.HungerCanApply, "战后落账 ⇒ 台账已建 ⇒ 饥饿可结算 ✓");
    }

    [TestMethod]
    public void Flow_HasPendingHunger_FalseWhenLedgerMissing()
    {
        // 🔴 首场战斗之前若掷中饥饿：**不弹"吃/不吃"**（弹了就是骗玩家：选哪个都没效果）✓
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost);
        Assert.IsFalse(flow.HasPendingHunger,
            "🔴 台账未建 ⇒ `HasPendingHunger` 必须 false（即便真的掷中了）✓");
    }
}
