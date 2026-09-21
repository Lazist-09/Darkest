using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴🔴 **`D-4` 陷阱转真（`TrapDefs`）** —— 本批用例是 `D-4` 的**唯一判据**。
///
/// **DD wiki 原文实据**（`Trap` / `Scouting` 两页，2026-09-20 取自 `darkestdungeon.fandom.com/wiki/Trap`）：
/// <list type="bullet">
///   <item><description>踏中 ⇒ **随机一名**队员掉 HP（**% 最大 HP**）+ **+15 压力** ✓</description></item>
///   <item><description>**未被侦察**的陷阱：按该队员的 **Trap Resist** 掷骰**闪避**（**不是 DODGE**）✓</description></item>
///   <item><description>**已被侦察**的陷阱：「在地图上显示为**紫色图标**、走近能看到」⇒ 给一次**拆除（Disarm）**机会；
///     **拆除成功 = 完全无害**（原文 "renders it harmless"）✓</description></item>
///   <item><description>拆除概率 = **Trap Resist + 40%**（原文实据）；失败 ⇒ 照样踏中 ✓</description></item>
///   <item><description>拆除成功 **回 8 压力**（原文 "heals 8 stress for the hero"）✓</description></item>
///   <item><description>「Traps can sometimes be encountered along corridors that have already been explored」
///     ⇒ 与 `D-2`（重访刷新，`ThreatTrap` 已登记）**同源** ⇒ 两处**必须共用一份定值**（`#325` D6）✓</description></item>
/// </list>
///
/// 🔴🔴 **本刀与 `D-3` 的直接协同**：**三态揭示就是陷阱的门禁**
///   · `Unexplored` ⇒ 看不见 ⇒ 无从拆除（但**能不能踏中取决于图**，不取决于玩家是否知道）⚠️
///   · `Scouted`   ⇒ 看到紫图标 ⇒ **可拆除**（Trap Resist + disarm_bonus）
///   · `Visited`   ⇒ 已走过 ⇒ 陷阱要么已被拆/已触发（格内容消费掉），要么是 `D-2` 新刷的
/// </summary>
[TestClass]
public sealed class TrapTests
{
    private const int SegmentCost = 30;

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

    // ── ① 加载期 fail-fast（与 `CuriosConfig.Parse` / `BuffDefsConfig.Validate` 同纪律）──

    [TestMethod]
    public void Traps_MissingOrEmpty_IsRejected_NotSilentlyDisabled()
    {
        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse("""{ "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": 8 }"""),
            "没有 `traps` 数组 ⇒ 视为**数据写错**（要关就整段删 `trap_defs`）⚠️");

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse("""{ "traps": [], "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": 8 }"""),
            "空数组 ⇒ 拒绝（与 `P21`/`D-2`/`D-5` 同族纪律）✓");
    }

    [TestMethod]
    public void Traps_RegionWeightSumZero_IsRejected()
    {
        // 🔴 权重全 0 ⇒ **一个陷阱都刷不出来** ⇒ 整个 `D-4` 静默失效（`D-2` 末档不覆盖满光照同款坑）⚠️
        string json = """
        {
          "traps": [
            { "id": "ruins_pit", "region": "ruins", "hp_percent": 25, "weight": 1 },
            { "id": "weald_snare", "region": "weald", "hp_percent": 0, "weight": 0 }
          ],
          "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40,
          "stress_damage": 15, "disarm_stress_heal": 8
        }
        """;

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse(json, requireRegions: new[] { "ruins", "weald" }),
            "某区域的权重和 = 0 ⇒ 该区域永远无陷阱 ⇒ 加载期拒绝 ⚠️");
    }

    [TestMethod]
    public void Traps_InvalidNumbers_AreRejected()
    {
        string Slim(string extra, string traps = """[ { "id": "a", "region": "ruins", "hp_percent": 10, "weight": 1 } ]""") =>
            $$"""{ "traps": {{traps}}, {{extra}} }""";

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse(Slim(""""unscouted_dodge_percent": -1, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": 8"""")),
            "闪避率 < 0 ⇒ 拒绝 ✓");

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse(Slim(""""unscouted_dodge_percent": 101, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": 8"""")),
            "闪避率 > 100 ⇒ 拒绝（超过 100 的骰子无意义）✓");

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse(Slim(""""unscouted_dodge_percent": 40, "disarm_bonus_percent": -5, "stress_damage": 15, "disarm_stress_heal": 8"""")),
            "拆除加成 < 0 ⇒ 拒绝（负加成 = 拆除反而更难，静默失效）⚠️");

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse(Slim(""""unscouted_dodge_percent": 40, "disarm_bonus_percent": 40, "stress_damage": 0, "disarm_stress_heal": 8"""")),
            "`stress_damage = 0` ⇒ 拒绝 —— 0 会让'压力伤害'整条**静默失效**（DD 原文恒 +15）⚠️");

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse(Slim(""""unscouted_dodge_percent": 40, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": -1"""")),
            "拆除回压 < 0 ⇒ 拒绝（DD 是回压，不是加压）✓");
    }

    [TestMethod]
    public void Traps_HpPercentOutOfRange_AndUnknownRegion_AreRejected()
    {
        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse("""
            {
              "traps": [ { "id": "a", "region": "ruins", "hp_percent": 0, "weight": 1 } ],
              "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": 8
            }
            """),
            "`hp_percent = 0` ⇒ 拒绝（无伤害的陷阱 = 只有压力，必是漏配键）⚠️");

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse("""
            {
              "traps": [ { "id": "a", "region": "ruins", "hp_percent": 101, "weight": 1 } ],
              "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": 8
            }
            """),
            "`hp_percent > 100` ⇒ 拒绝（一击必杀不是设计意图）✓");

        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse("""
            {
              "traps": [ { "id": "a", "region": "atlantis", "hp_percent": 10, "weight": 1 } ],
              "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": 8
            }
            """),
            "未登记的地区 ⇒ 拒绝（**不静默当成 ruins** —— 那是'谁替策划选了一个地区'）⚠️");
    }

    [TestMethod]
    public void Traps_DuplicateId_IsRejected()
    {
        // 🔴 重复 id ⇒ `Find` 取第一个 ⇒ 第二个**永远拿不到**（静默死数据）⚠️
        Assert.ThrowsException<InvalidDataException>(
            () => TrapDefs.Parse("""
            {
              "traps": [
                { "id": "dup", "region": "ruins", "hp_percent": 10, "weight": 1 },
                { "id": "dup", "region": "warrens", "hp_percent": 20, "weight": 1 }
              ],
              "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40, "stress_damage": 15, "disarm_stress_heal": 8
            }
            """),
            "重复 id ⇒ 拒绝（否则第二条永远取不到）⚠️");
    }

    // ── ② 伤害/压力结算（DD 口径）────────────────────────────────────────────

    private static TrapDefs Minimal(int hp = 20) => TrapDefs.Parse(
        $$"""
        {
          "traps": [ { "id": "test_trap", "region": "ruins", "hp_percent": {{hp}}, "weight": 1 } ],
          "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40,
          "stress_damage": 15, "disarm_stress_heal": 8
        }
        """,
        // 🔴 只声明 `ruins`（本夹具真只覆盖它）—— 而不是"把其它地区权重调 0 蒙过校验"⚠️
        requireRegions: new[] { "ruins" });

    [TestMethod]
    public void Damage_IsPercentOfMaxHp_AndNeverBelowOne_whenMaxHpIsOne()
    {
        TrapDefs defs = Minimal(hp: 20);
        TrapDef def = defs.Traps[0];

        Assert.AreEqual(20, TrapResolver.DamageFor(def, 100), "100 血 × 20% ⇒ 20 ✓");
        Assert.AreEqual(3, TrapResolver.DamageFor(def, 15), "15 血 × 20% ⇒ 3 ✓");
        Assert.AreEqual(1, TrapResolver.DamageFor(def, 1), "🔴 极小值 ⇒ **至少 1**（不能是 0 ⇒ 否则陷阱对低血者完全无效）⚠️");
        Assert.AreEqual(1, TrapResolver.DamageFor(def, 4), "4 × 20% = 0.8 ⇒ 取 1（不是四舍五入成 0）⚠️");
    }

    [TestMethod]
    public void StressDamage_IsTheSameForEveryHero_NotAPercent()
    {
        // 🔴 DD 原文：「The health damage will be a percentage of the hero's MAX HP,
        //    while **stress will always be +15**」⇒ 压力是**定值**，不是百分比 ⚠️
        TrapDefs defs = Minimal();
        Assert.AreEqual(15, defs.StressDamage, "压力伤害 = 定值 15（不是 15% of something）✓");
    }

    // ── ③ 三态门禁（`D-3` 协同）—— 本刀最关键的口径 ─────────────────────────

    [TestMethod]
    public void ScoutingGate_IsThreeState_NotABoolean()
    {
        // 🔴🔴 本刀的**核心判据**：陷阱的可拆除性由 **`D-3` 的三态**决定，不是"侦察过/没侦察过"二态
        TrapDefs defs = Minimal();

        Assert.AreEqual(TrapGate.Hidden, TrapResolver.GateFor(RevealState.Unexplored),
            "未探索 ⇒ **看不见**（DD：未侦察的陷阱只是'没显示'，不是'没触发'）✓");
        Assert.AreEqual(TrapGate.Disarmable, TrapResolver.GateFor(RevealState.Scouted),
            "🔴 **只被侦察到** ⇒ 紫色图标 + 可拆除 —— 这正是 `D-3` 的中间态**唯一**的用武之地 ✓");
        Assert.AreEqual(TrapGate.Consumed, TrapResolver.GateFor(RevealState.Visited),
            "已走过 ⇒ 该格内容已消费（陷阱要么被拆、要么已触发）✓");
    }

    [TestMethod]
    public void HiddenTrap_StillTriggers_VisibleTrapCanBeDisarmed_ButIsHarmlessIfDisarmFails()
    {
        // 🔴 DD 原文实据：**未侦察**的陷阱**照样会踩中**（只是玩家看不见）——
        //    绝不能把"没侦察到"当成"不会触发"（那是把 DD 的紧张感整个删掉）⚠️
        TrapDefs defs = Minimal();

        var rng = new RngProvider(20260920);
        var log = new CombatLog();

        TrapOutcome hidden = TrapResolver.Resolve(log, rng, defs, defs.Traps[0],
            TrapGate.Hidden, trapResistPercent: 40);
        Assert.IsTrue(hidden.Triggered, "未侦察 ⇒ **照样踏中**（能不能躲是 Trap Resist 的事，不是侦察的事）⚠️");

        TrapOutcome disarmed = TrapResolver.Resolve(log, rng, defs, defs.Traps[0],
            TrapGate.Disarmable, trapResistPercent: 200);
        Assert.IsFalse(disarmed.Triggered, "🔴 已侦察 + Trap Resist 高 ⇒ 拆除成功 ⇒ **完全无害** ✓");
        Assert.IsTrue(disarmed.Disarmed, "且**必须**记下'拆除成功'（表现层要播回压 8 的反馈）✓");
    }

    [TestMethod]
    public void DisarmChance_UsesTrapResistPlusBonus_AndCanExceedOneHundred()
    {
        TrapDefs defs = Minimal(); // disarm_bonus 40

        Assert.AreEqual(80, TrapResolver.DisarmChancePercent(defs, 40),
            "DD 原文：拆除概率 = **Trap Resist + 40%** ⇒ 40 + 40 = 80 ✓");
        Assert.AreEqual(140, TrapResolver.DisarmChancePercent(defs, 100),
            "🔴 **允许超过 100%**（DD 原文「Disarm Chance can go above 100%」）⇒ 实现**不能**把它钳到 100 ⚠️");

        // 掷骰时：>100 ⇒ **必然成功**（不是 100 的钳位，而是"任意 roll 都小于它"）✓
        var rng = new RngProvider(1);
        var log = new CombatLog();
        for (int i = 0; i < 5; i++)
        {
            TrapOutcome o = TrapResolver.Resolve(log, rng, defs, defs.Traps[0], TrapGate.Disarmable, 100);
            Assert.IsFalse(o.Triggered, "拆除概率 140 > 100 ⇒ 必成功（不掷也成立）✓");
        }
    }

    [TestMethod]
    public void DisarmCapacity_IsPerHero_SoThereMustBeADeclaredSourcePerHero()
    {
        // 🔴🔴 `#325` D6：**不许为同一语义造第二份真值**。
        //    陷阱抗性/拆除能力是**每个队员**的属性 ⇒ 调用方传的是"谁在哪一格拆"，
        //    不是"整队一个数"。本用例锁住：**同一格、不同队员 ⇒ 可以得出不同结果** ✓
        TrapDefs defs = Minimal();
        var log = new CombatLog();

        // Grave Robber 档（50+40=90）vs Flagellant 档（0+40=40）—— 同格同陷阱，概率不同 ✓
        Assert.AreEqual(90, TrapResolver.DisarmChancePercent(defs, 50));
        Assert.AreEqual(40, TrapResolver.DisarmChancePercent(defs, 0));
        Assert.AreNotEqual(
            TrapResolver.DisarmChancePercent(defs, 50),
            TrapResolver.DisarmChancePercent(defs, 0),
            "陷阱能力**按人**不同 ⇒ 调用方必须以「人」为单位问（不能整队取一个平均）⚠️");
        _ = log;
    }

    // ── ④ 随机必写 `RngDraw`（红线）──────────────────────────────────────────

    [TestMethod]
    public void EveryRoll_WritesRngDraw_NeverSilentlyRandom()
    {
        TrapDefs defs = Minimal();
        var log = new CombatLog();
        var rng = new RngProvider(777);

        int before = log.Events.Count;
        TrapResolver.Resolve(log, rng, defs, defs.Traps[0], TrapGate.Hidden, 40);
        Assert.IsTrue(log.Events.Count > before, "未侦察掷闪避 ⇒ **必须写日志**（红线：所有随机必写 `RngDraw`）✓");

        int afterFirst = log.Events.Count;
        TrapResolver.Resolve(log, rng, defs, defs.Traps[0], TrapGate.Disarmable, 40);
        Assert.IsTrue(log.Events.Count > afterFirst, "已侦察掷拆除 ⇒ 也必须写日志 ✓");

        // 🔴 已消费的格 ⇒ **不掷、不写**（不该有"凭空多出来的随机"）✓
        int beforeConsumed = log.Events.Count;
        TrapOutcome consumed = TrapResolver.Resolve(log, rng, defs, defs.Traps[0], TrapGate.Consumed, 40);
        Assert.IsFalse(consumed.Triggered, "已消费 ⇒ 不触发 ✓");
        Assert.AreEqual(beforeConsumed, log.Events.Count, "已消费 ⇒ **不掷骰、不写日志**（行为逐字不变）✓");
    }

    // ── ⑤ 与 `D-2` 共用一份定值（`#325` D6）─────────────────────────────────

    [TestMethod]
    public void RevisitThreatTrap_UsesTheSameTrapTable_NotASecondSourceOfTruth()
    {
        // 🔴 DD 原文：「Traps **can sometimes be encountered along corridors that have already been explored**」
        //    ⇒ `D-2` 的 `ThreatTrap` 与本刀的陷阱**是同一件事** ⇒ 必须**同一份表**（`#325` D6）⚠️
        //    判据：`TrapResolver` 必须提供"按地区 + 权重抽一条"的**唯一**入口，`D-2` 复用它 ✓
        TrapDefs defs = TrapDefs.Parse("""
        {
          "traps": [
            { "id": "ruins_pit",   "region": "ruins",   "hp_percent": 25, "weight": 3 },
            { "id": "weald_snare", "region": "weald",   "hp_percent": 5,  "weight": 0 },
            { "id": "cove_grate",  "region": "cove",    "hp_percent": 10, "weight": 1 }
          ],
          "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40,
          "stress_damage": 15, "disarm_stress_heal": 8
        }
        """, requireRegions: new[] { "ruins", "cove" });

        var rng = new RngProvider(20260920);
        var log = new CombatLog();

        // 🔴 权重只有 cove 那条非 0 ⇒ **种类已定，不必掷**（与 `RevisitSpawner` 同款"单侧权重"纪律）✓
        TrapDef? only = TrapResolver.Pick(log, rng, defs, "cove");
        Assert.IsNotNull(only, "cove 权重大于 0 ⇒ 能抽到 ✓");
        Assert.AreEqual("cove_grate", only!.Id, "权重 0 的条目**永不出现**（不是'抽到 0 次'而是'根本不参与'）✓");

        // 权重和为 0 的地区 ⇒ **明确返回 null**（不静默挑一条兜底）⚠️
        int before = log.Events.Count;
        TrapDef? none = TrapResolver.Pick(log, rng, defs, "weald");
        Assert.IsNull(none, "该地区权重全 0 ⇒ 返回 null（**不**静默挑别的地区的陷阱）⚠️");
        Assert.AreEqual(before, log.Events.Count, "没有可抽项 ⇒ **不掷骰**（零随机不留痕）✓");
    }

    [TestMethod]
    public void Pick_IsDeterministic_ForTheSameSeed()
    {
        TrapDefs defs = TrapDefs.Parse("""
        {
          "traps": [
            { "id": "a", "region": "ruins", "hp_percent": 10, "weight": 1 },
            { "id": "b", "region": "ruins", "hp_percent": 20, "weight": 2 },
            { "id": "c", "region": "ruins", "hp_percent": 30, "weight": 1 }
          ],
          "unscouted_dodge_percent": 40, "disarm_bonus_percent": 40,
          "stress_damage": 15, "disarm_stress_heal": 8
        }
        """, requireRegions: new[] { "ruins" });

        var picks1 = new List<string>();
        var picks2 = new List<string>();
        var r1 = new RngProvider(4242);
        var r2 = new RngProvider(4242);
        var l1 = new CombatLog();
        var l2 = new CombatLog();
        for (int i = 0; i < 40; i++)
        {
            picks1.Add(TrapResolver.Pick(l1, r1, defs, "ruins")!.Id);
            picks2.Add(TrapResolver.Pick(l2, r2, defs, "ruins")!.Id);
        }

        CollectionAssert.AreEqual(picks1, picks2, "同种子 ⇒ **同序列**（确定性）✓");

        // 权重 2 的那条应显著多于权重 1 的两条（分布检验，弱断言以免脆）✓
        int b = picks1.Count(x => x == "b");
        Assert.IsTrue(b > picks1.Count(x => x == "a"), $"权重 2 > 权重 1（实测 b={b} a={picks1.Count(x => x == "a")}）✓");
    }

    // ── ⑥ 真实数据可加载（`tuning.json` / `trap_defs.json`）───────────────────

    [TestMethod]
    public void ShippedTrapData_LoadsCleanly_AndCoversEveryRegion()
    {
        TrapDefs defs = TrapDefs.Parse(ReadData("trap_defs.json"));

        Assert.IsTrue(defs.Traps.Count >= 4, $"出货数据应至少覆盖 4 个地区（实测 {defs.Traps.Count}）✓");
        foreach (string region in new[] { "ruins", "weald", "warrens", "cove" })
        {
            Assert.IsTrue(defs.Traps.Any(t => t.Region == region && t.Weight > 0),
                $"地区 `{region}` 必须有**权重 > 0** 的陷阱（否则该地区永远无陷阱 = 静默失效）⚠️");
        }

        Assert.IsTrue(defs.StressDamage > 0, "压力伤害必须 > 0 ✓");
        Assert.IsTrue(defs.DisarmBonusPercent > 0, "拆除加成必须 > 0（否则'侦察'没有战术价值）✓");
    }
}
