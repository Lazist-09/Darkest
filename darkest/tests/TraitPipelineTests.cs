using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// **V5 收紧后的验收**（`#288` ③ / `#289` 裁定 (B)）：**同原型、不同特质的两人，在战斗中的读数确实不同**。
/// 做法：同一 seed、同一原型、同一策略 ⇒ 唯一差异是特质伤害修正 ⇒ `DamageEvent` 合计必须不同。
/// </summary>
[TestClass]
public sealed class TraitPipelineTests
{
    private static string ReadData(string name)
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = System.IO.Path.Combine(dir.FullName, "data", name);
            if (System.IO.File.Exists(candidate))
            {
                return System.IO.File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new System.IO.FileNotFoundException($"data/{name} 未找到。");
    }

    /// <summary>跑一场（同一 seed、同一策略），返回我方对敌方造成的伤害合计。</summary>
    private static int RunOneBattle(int traitDamagePct, long seed)
    {
        var log = new CombatLog();
        var rng = new Darkest.Core.Rng.RngProvider(seed);
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);

        // 🔴 唯一变量：给"第一个我方单位"叠加特质伤害修正（同原型、同基准）
        director.Player.UnitsInSlotOrder().First().ApplyTraitDamagePct(traitDamagePct);

        for (int round = 0; round < 30 && !director.IsBattleOver; round++)
        {
            director.RunFullRound(rng, unit => MonteCarlo.Policies.DecideForUnit(
                MonteCarlo.PolicyKind.SemiRandom, unit, director, rng));
        }

        return log.Events.OfType<DamageEvent>()
            .Where(e => e.Attacker is not null && director.Player.UnitAtPosition(e.Attacker.Value) is not null)
            .Sum(e => e.Amount);
    }

    [TestMethod]
    public void V5_TraitDamage_ChangesBattleReadings_SameArchetype()
    {
        RosterConfig rcfg = RosterConfig.Parse(ReadData("roster.json"));
        Assert.IsTrue(RosterConfig.MaxTraitDamagePct == 15, "特质伤害幅度必须可测（红线 19：≤15%）");

        const long seed = 20260909L;
        int plus = RunOneBattle(+10, seed);
        int minus = RunOneBattle(-10, seed);
        int none = RunOneBattle(0, seed);

        string report = $"[M8] V5 特质接管线验收（同原型、同 seed、同策略，唯一差异 = 特质伤害修正）：" +
                        $"特质 +10% ⇒ 我方伤害合计 {plus}；0% ⇒ {none}；−10% ⇒ {minus}";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(plus > none, $"+10% 特质必须打得更多（{plus} 大于 {none}）");
        Assert.IsTrue(minus < none, $"−10% 特质必须打得更少（{minus} 小于 {none}）");
        Assert.AreNotEqual(plus, minus, "**同原型、不同特质 ⇒ 战斗读数确实不同**（V5 收紧后的验收）");
    }

    [TestMethod]
    public void V5_NoDeadDeclaration_DamageModIsConsumed()
    {
        // 红线 21：DamageMod 不再是"已声明未消费" —— 单位上的修正确实能被结算读到
        var log = new CombatLog();
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);
        var unit = director.Player.UnitsInSlotOrder().First();

        Assert.AreEqual(1.0, unit.DamageMultiplier, 1e-9, "默认无修正（1.0）");
        unit.ApplyTraitDamagePct(15);
        Assert.AreEqual(1.15, unit.DamageMultiplier, 1e-9, "+15% ⇒ 1.15（加法先于乘法：多来源先累加）");
        unit.ApplyTraitMoraleDamagePct(20);
        Assert.AreEqual(1.20, unit.MoraleDamageTakenMultiplier, 1e-9, "受士气伤害 +20% ⇒ 1.20（士气通道）");

        // 特质投影到单位（HeroProjection 的出口）
        HeroConfig hero = RosterConfig.Parse(ReadData("roster.json")).Heroes.First();
        TraitEffects eff = HeroProjection.ApplyTraits(hero, unit);
        Assert.AreEqual(eff.DamagePct, unit.DamageModPct - 15, "投影把特质伤害写进单位（与既有层同层叠加）");
    }

    /// <summary>
    /// 跑一场，返回**带特质那个单位自己**造成的伤害合计（㉟：不被 6 人摊薄）。
    /// 🔴 同时返回 `Raw`（**取整/下限之前**的逐次裸伤）—— 见 ㉟ 的判据分层：
    /// `Amount` 是玩家可见值（含取整/下限/结转的非线性），`Raw` 是模型值（应当严格成比例）。
    /// </summary>
    private static (int Amount, double Raw, double FirstRaw) RunOneBattle_SingleUnit(int traitDamagePct, long seed)
    {
        var log = new CombatLog();
        var rng = new Darkest.Core.Rng.RngProvider(seed);
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);
        var hero = director.Player.UnitsInSlotOrder().First();
        var heroId = hero.Id;
        hero.ApplyTraitDamagePct(traitDamagePct);

        for (int round = 0; round < 30 && !director.IsBattleOver; round++)
        {
            director.RunFullRound(rng, unit => MonteCarlo.Policies.DecideForUnit(
                MonteCarlo.PolicyKind.SemiRandom, unit, director, rng));
        }

        var mine = log.Events.OfType<DamageEvent>().Where(e => e.Attacker == heroId).ToList();
        // 🔴 首击（`[0]`）是**唯一**不受「战斗演化反馈」污染的一笔：
        //    到它为止，两臂的命令流与 RNG 抽号**逐字相同**（特质的差异还没改变任何 HP）
        //    ⇒ 它的 `Raw` 必须**严格**成比例（×1.1 / ×0.9）。
        return (mine.Sum(e => e.Amount), mine.Sum(e => e.Raw), mine.Count > 0 ? mine[0].Raw : 0.0);
    }

    /// <summary>跑 **N 场（同一批 seed）**，返回**带特质那个单位自己**造成的伤害合计（㉟ 配对口径）。</summary>
    private static (int Amount, double Raw, double FirstRaw) RunBattles_SingleUnit(int traitDamagePct, int battles)
    {
        int amount = 0;
        double raw = 0;
        double firstRaw = 0;
        for (int i = 0; i < battles; i++)
        {
            (int a, double r, double f) = RunOneBattle_SingleUnit(traitDamagePct, 20260909L + (i * 7));
            amount += a;
            raw += r;
            firstRaw += f;
        }

        return (amount, raw, firstRaw);
    }

    [TestMethod]
    public void V5_35_SingleUnitDamageDelta_ConvergesBothSides()
    {
        // 🔴 ㉟ 口径修正（用户裁定）：**单场样本太小** ⇒ 【百分比 → 取整 → 下限】会把 ±10% 扭曲成
        //    +19% / −4.8%（一边被下限/取整抬高、另一边被吃掉）⇒ 改为**多场配对累计**（同一批 seed，三臂同源）
        // 🔴 归因修正（2026-09-30）：`Amount`（玩家可见）侧 +13.8% 不是「特质没生效」，而是
        //    【取整 + 下限】的非线性（`DamageFloor` 会把低伤一侧抬高、`DamageCarry` 又把小数结转）
        //    ⇒ **主判据改测 `DamageEvent.Raw`（取整前的模型值，应当严格成比例）**；
        //    `Amount` 侧差值**照常打印**（玩家可见口径），但不再拿它当 ±3pp 的判据。
        const int battles = 40;
        (int plus, double plusRaw, double plusFirst) = RunBattles_SingleUnit(+10, battles);
        (int none, double noneRaw, double noneFirst) = RunBattles_SingleUnit(0, battles);
        (int minus, double minusRaw, double minusFirst) = RunBattles_SingleUnit(-10, battles);

        double plusPct = none == 0 ? 0 : 100.0 * (plus - none) / none;
        double minusPct = none == 0 ? 0 : 100.0 * (minus - none) / none;
        double plusRawPct = noneRaw == 0 ? 0 : 100.0 * (plusRaw - noneRaw) / noneRaw;
        double minusRawPct = noneRaw == 0 ? 0 : 100.0 * (minusRaw - noneRaw) / noneRaw;
        double plusFirstRatio = noneFirst == 0 ? 0 : plusFirst / noneFirst;
        double minusFirstRatio = noneFirst == 0 ? 0 : minusFirst / noneFirst;

        string report = $"[M8] ㉟ 带特质者**单体**伤害差（{battles} 场配对累计，同 seed 同策略）：" +
                        $"模型值（Raw，取整前）：+10% ⇒ {plusRaw:F1}（基准 {noneRaw:F1}，{plusRawPct:F1}%）；" +
                        $"−10% ⇒ {minusRaw:F1}（{minusRawPct:F1}%）　⇒ 【含战斗演化反馈】也不等于 ±10%（干净口径看首击）\n" +
                        $"[M8] ㉟ 玩家可见值（Amount，含取整/下限/结转）：" +
                        $"+10% ⇒ {plus}（基准 {none}，{plusPct:F1}%）；−10% ⇒ {minus}（{minusPct:F1}%）" +
                        $"　⇒ 【含战斗演化反馈】两侧都不等于 ±10%（原因见下）\n" +
                        $"[M8] ㉟ **首击**（同一状态、同一 RNG 抽号 ⇒ 无反馈污染，模型值）：" +
                        $"+10% ⇒ {plusFirst:F3} / 基准 {noneFirst:F3} = ×{plusFirstRatio:F6}（应 == 1.1）；" +
                        $"−10% ⇒ {minusFirst:F3} = ×{minusFirstRatio:F6}（应 == 0.9）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        Assert.IsTrue(plus > none && minus < none, "方向正确（+10 更高、−10 更低）");
        // 🔴 主判据（2026-09-30 归因修正）：**首击的模型值必须严格成比例** ——
        //    这是「特质真的进了伤害模型」的**干净**证据（不含取整/下限/战斗演化反馈）。
        Assert.AreEqual(1.1, plusFirstRatio, 1e-6,
            $"首击 Raw 应严格 ×1.1（实测 ×{plusFirstRatio:F6}）—— 不等 ⇒ 特质倍率没进模型");
        Assert.AreEqual(0.9, minusFirstRatio, 1e-6,
            $"首击 Raw 应严格 ×0.9（实测 ×{minusFirstRatio:F6}）—— 不等 ⇒ 特质倍率没进模型");
        // 🔴 并如实登记：**全场累计**（Raw 与 Amount 两侧）都**不等于** ±10%（13.2% / −1.8%），
        //    这**不是**取整/下限 —— 因为取整前的 Raw 也偏 ⇒ 真因是**战斗演化反馈**
        //    （伤害变 ⇒ 击杀时点变 ⇒ 出手数与承伤变 ⇒ 后续抽号分流）⇒ 本量**只登记不设判据**：
        //    登记见 `doc/modules/dd1_baseline.md §39`（#307 冻结期 · 到期条件=§39 解冻重定标）。
        Assert.IsTrue(noneRaw > 0 && plusRaw > 0 && minusRaw > 0,
            $"三臂 Raw 必须都真的取到（防夹具空跑产 0 ⇒ 百分比无意义）；实测 +{plusRaw:F1} / 基准 {noneRaw:F1} / −{minusRaw:F1}");
    }

    /// <summary>
    /// 🔴 **片③ (b) / V5 士气侧读数**：**特质的"受士气伤害"必须真的进结算** ——
    /// 此前 `MoraleDamageTakenMultiplier` **只有测试在读**（生产无消费点 ⇒ 从未生效，红线 21）。
    /// 判据（可测）：同一起点、同一 `delta`，**带 +20% 受士气伤害**者的实际下降**明显更大**。
    /// </summary>
    [TestMethod]
    public void V5_MoraleTrait_ChangesMoraleDrop_NotJustAField()
    {
        var log = new CombatLog();
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);
        var ledger = director.Morale; // 🔴 直接用战力台账（BattleDirector.Morale），不另造
        var units = director.Player.UnitsInSlotOrder().ToArray();
        var plain = units[0];
        var fragile = units[1];
        fragile.ApplyTraitMoraleDamagePct(20); // 受士气伤害 +20%

        int d1 = ledger.Apply(plain, -10, "test", log);
        int d2 = ledger.Apply(fragile, -10, "test", log);

        string report = $"[V5] 士气侧读数（同起点 50 / 同 delta −10）：无特质 ⇒ {d1}；受士气伤害 +20% ⇒ {d2}" +
                        $"　（比例 {(double)d2 / d1:F2}）";
        Console.WriteLine(report);

        Assert.AreEqual(-10, d1, "无特质 ⇒ 原样 −10");
        Assert.IsTrue(d2 < d1, $"🔴 带 +20% 受士气伤害者【实际下降更大】（{d1} vs {d2}）—— 否则又是" +
                                "「字段有值、结算不用」（红线 21）");
        Assert.AreEqual(-12, d2, "−10 × 1.2 = −12（可核）");
    }

    /// <summary>
    /// 🔴 **片③ (a)：`our_morale_damage_pct` 真的进结算** —— 且**只作用于"受击派生"**的士气损失
    /// （事件/流程导致的士气变化**不受光照影响**）。
    /// </summary>
    [TestMethod]
    public void Slice3a_LightMoraleDamage_AppliesToHitDerivedOnly()
    {
        var log = new CombatLog();
        var director = MonteCarlo.HeadlessDriver.NewDirector(log);
        var ledger = director.Morale;
        var units = director.Player.UnitsInSlotOrder().ToArray();

        ledger.OurMoraleDamagePct = 20; // 模拟"当前光照档：我方士气伤害 +20%"

        int hitDerived = ledger.Apply(units[0], -10, "mental_hit", log);   // 受击派生 ⇒ 加成生效
        int eventDriven = ledger.Apply(units[1], -10, "event_stress", log); // 事件类 ⇒ 不受光照影响

        string report = $"[片③a] 光照 our_morale_damage_pct=+20%：受击派生(mental_hit) ⇒ {hitDerived}；" +
                        $"事件类(event_stress) ⇒ {eventDriven}（应仍为 −10）";
        Console.WriteLine(report);

        Assert.AreEqual(-12, hitDerived, "受击派生 ⇒ −10 × 1.2 = −12（可核）");
        Assert.AreEqual(-10, eventDriven, "🔴 事件类士气变化不受光照影响（加成只作用于受击派生）");
    }

    public TestContext TestContext { get; set; } = null!;
}
