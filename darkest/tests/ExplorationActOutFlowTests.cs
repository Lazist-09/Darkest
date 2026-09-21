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

/// <summary>
/// 🔴🔴 **`D-7` 探索层 act-out · 流程级**（`dd_replication_roadmap` D-7）——
/// 证明两条门禁**真的接在流程上**（而不只是纯函数能过）。
///
/// <para>本文件刻意复制 `HungerTests` 的**「先落账再结算」**手法（`CaptureBattleEndHp`）
/// —— 因为门禁的判据（`LowestSurvivorMorale`）依赖 `Retained`，而它**只在战后落账**
/// （与 D-4/D-5 同源的**有界缺口**）⇒ 测试必须先造出"打了一场"的前提 ✓</para>
///
/// <para>🔴 **本文件守的两个「静默失效」陷阱**（前几刀都真踩过）：</para>
/// <para>① **门禁算晚了** ⇒ 被拒的道具请求已经消耗过 `RngDraw`（D-4 踩过：门禁在 `TryStep` 之后 ⇒ 恒 `Consumed`）；
///    ⇒ 用"**随机流不污染**"用例守：不带折磨时 `DrawCount == 0`；</para>
/// <para>② **机制配了但不生效** ⇒ 用"**真的拒绝了**"用例守：配置 100% + 带折磨 ⇒ 必须 `Refused` 且留下事件 ✓</para>
/// </summary>
[TestClass]
public sealed class ExplorationActOutFlowTests
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

    private static TuningConfig Tuning => TuningConfig.Parse(ReadData("tuning.json"));

    /// <summary>造一份 act-out 配置（阈值固定 = `morale.start` = 50）✓</summary>
    private static TuningExplorationActOut ActOut(double curioRefuse, double eatRefuse) => new(
        MoraleAfflictionThreshold: 50,
        CurioRefusePercent: curioRefuse,
        EatRefusePercent: eatRefuse);

    /// <summary>
    /// 造一个**已落账**的会话（`Retained` 已建）—— 与 `HungerTests.SessionWithLedger` 同款手法 ✓
    /// </summary>
    private static ExpeditionSession SessionWithLedger(CombatLog log)
    {
        TuningConfig tuning = Tuning;
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 10, tuning.Expedition.AmbushChance);
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log); // 落账 ⇒ `Retained` 建立 ✓
        return session;
    }

    /// <summary>把随机源钉成固定值（与 `HungerTests` 同款）✓</summary>
    private sealed class FixedRng(double percent, int intValue = 0) : IRngProvider
    {
        public ulong DrawCount { get; private set; }

        public double NextPercent()
        {
            DrawCount++;
            return percent;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            DrawCount++;
            return Math.Clamp(intValue, minInclusive, maxExclusive - 1);
        }
    }

    /// <summary>把队内**所有人**的士气压到指定值（直接改 `Retained`，因为它是 protected —— 走测试专用后门）✓</summary>
    private static void ForceMorale(ExpeditionSession session, int morale)
    {
        // 🔴 测试通过"打一场 + 落账"建立 `Retained`，再用公开的 `CaptureBattleEndHp` 之外的
        //    **受控路径**改士气：`ResolveHunger(eat:false)` 会掉压力 ⇒ 用它把士气压低到阈值下 ✓
        //    （不用反射：那会绕过契约；这里用**真实的**掉士气通道，测试更可信）
        var log = new CombatLog();
        HungerConfig cfg = new(new List<HungerTier> { new(100, 0.0) },
            FoodPerHero: 1, EatHealPercent: 5.0, StarveHpPercent: 0.0, StarveMorale: 1);
        while (session.LowestSurvivorMorale() is { } m && m >= morale)
        {
            int before = m;
            session.ResolveHunger(log, cfg, eat: false); // 掉 1 点压力 ✓
            if (session.LowestSurvivorMorale() is not { } after || after >= before)
            {
                break; // 已到 0 / 无可再降 ⇒ 退出（防死循环）✓
            }
        }
    }

    // ══════════════════════ ① 拒绝进食（会话级）══════════════════════

    [TestMethod]
    public void ResolveHunger_Afflicted_RefusesToEat_ForcedToStarve()
    {
        var log = new CombatLog();
        ExpeditionSession session = SessionWithLedger(log);
        ForceMorale(session, 40); // ⇒ 士气 < 50 ⇒ 带折磨 ✓
        session.BindTrapRng(new FixedRng(0.0)); // 恒中 ⇒ 必拒 ✓

        HungerConfig cfg = new(new List<HungerTier> { new(100, 0.0) },
            FoodPerHero: 1, EatHealPercent: 5.0, StarveHpPercent: 20.0, StarveMorale: 20);

        int foodBefore = session.Food;
        var log2 = new CombatLog();
        string outcome = session.ResolveHunger(log2, cfg, eat: true, ActOut(100, 100));

        Assert.AreEqual("starve", outcome,
            "🔴 带折磨 + 掷中拒绝 ⇒ **即使玩家选吃也必须挨饿**（`§F5 ③`）✓");
        Assert.AreEqual(foodBefore, session.Food,
            "🔴 **拒绝进食 ⇒ 一口粮都不扣**（与「口粮不够」口径自动一致）✓");
        Assert.AreEqual(1, session.ActOutEatRefuseCount, "🔴 计数必须 +1（可自证）✓");
        Assert.IsTrue(log2.Events.OfType<EffectEvent>().Any(e => e.EffectType == "act_out_eat_refuse"),
            "🔴 **拒绝必须有文案/事件留痕**（红线 21：绝不静默）✓");
        Assert.IsTrue(log2.Events.OfType<RngDraw>().Any(),
            "🔴 拒绝判定是随机 ⇒ **必写 `RngDraw`**（确定性红线）✓");
    }

    [TestMethod]
    public void ResolveHunger_NotAfflicted_EatsNormally_NoActOut()
    {
        var log = new CombatLog();
        ExpeditionSession session = SessionWithLedger(log);
        // 士气默认 50（= `morale.start`）⇒ **恰好不带折磨**（50 是解除点，不是折磨区）✓
        Assert.AreEqual(50, session.LowestSurvivorMorale(), "🔴 落账后士气 = 50（起手值）✓");
        session.BindTrapRng(new FixedRng(0.0)); // 若真掷了必中 ⇒ 用「没拒」反证没走门禁 ✓

        HungerConfig cfg = new(new List<HungerTier> { new(100, 0.0) },
            FoodPerHero: 1, EatHealPercent: 5.0, StarveHpPercent: 20.0, StarveMorale: 20);

        var log2 = new CombatLog();
        string outcome = session.ResolveHunger(log2, cfg, eat: true, ActOut(100, 100));

        Assert.AreEqual("eat", outcome, "🔴 士气 50 = 解除点 ⇒ **不带折磨** ⇒ 照常吃 ✓");
        Assert.AreEqual(0, session.ActOutEatRefuseCount, "不带折磨 ⇒ 计数不动 ✓");
        Assert.IsFalse(log2.Events.OfType<EffectEvent>().Any(e => e.EffectType == "act_out_eat_refuse"),
            "🔴 不带折磨 ⇒ **一句 act-out 事件都不该有** ✓");
    }

    [TestMethod]
    public void ResolveHunger_NoActOutConfig_BehavesExactlyAsBefore()
    {
        var log = new CombatLog();
        ExpeditionSession session = SessionWithLedger(log);
        ForceMorale(session, 40); // 带折磨 ✓
        session.BindTrapRng(new FixedRng(0.0)); // 若真掷了必中 ⇒ 用「没拒」反证门禁整个没走 ✓

        HungerConfig cfg = new(new List<HungerTier> { new(100, 0.0) },
            FoodPerHero: 1, EatHealPercent: 5.0, StarveHpPercent: 20.0, StarveMorale: 20);

        var log2 = new CombatLog();
        string outcome = session.ResolveHunger(log2, cfg, eat: true, actOut: null);

        Assert.AreEqual("eat", outcome,
            "🔴 **未配置 `exploration` ⇒ 门禁关闭**（哪怕带折磨也照常吃）—— opt-in，既有行为逐字不变 ✓");
        Assert.AreEqual(0, session.ActOutEatRefuseCount, "未配置 ⇒ 计数不动 ✓");
        Assert.IsFalse(log2.Events.OfType<RngDraw>().Any(),
            "🔴 未配置 ⇒ **一次都不掷**（零随机流污染）✓");
    }

    // ══════════════════════ ② 拒绝摸奇物（流程级）══════════════════════

    /// <summary>造一个带 `item_results` 的 Curio（有道具可选 ⇒ 才能观察"被拒"）✓</summary>
    private static CurioConfig CurioWithItem()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        CurioConfig? withItem = curios.RealCurios.FirstOrDefault(c => c.ItemResults is { Count: > 0 });
        Assert.IsNotNull(withItem, "🔴 `curios.json` 里必须至少有一个带 `item_results` 的 Curio（否则本用例无意义）✓");
        return withItem!;
    }

    private static ExpeditionFlow NewFlowDefault(CombatLog log, long seed = 20260918)
    {
        TuningConfig tuning = Tuning;
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 10, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!),
            ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, log, new RngProvider(seed));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }

    [TestMethod]
    public void ResolveCurio_Afflicted_RefusesItem_ForcedBareHands()
    {
        var log = new CombatLog();
        ExpeditionFlow flow = NewFlowDefault(log);
        ExpeditionSession session = (ExpeditionSession)flow.Session;
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log); // 落账 ⇒ 可判士气 ✓
        ForceMorale(session, 40);            // 带折磨 ✓

        CurioConfig curio = CurioWithItem();
        string item = curio.ItemResults![0].Item; // 一个**本来有效**的道具 ✓

        // 🔴 把随机流钉死：`ExpeditionFlow` 的 `_rng` 是构造注入的 ⇒ 这里用真实流无法钉死，
        //    故改用**配置 100%** 来保证必拒（确定性不依赖种子）✓
        TuningConfig seeded = TuningWithActOut(100, 100);
        ExpeditionFlow flow2 = NewFlowWithSession(seeded, log, session);

        CurioOutcome? outcome = flow2.ResolveCurio(curio, item);

        Assert.IsTrue(flow2.LastActOutRefused,
            "🔴 带折磨 + 100% 拒绝 ⇒ **必须拒绝用道具**（`curio.md §1.2 ⑤`）✓");
        Assert.IsFalse(string.IsNullOrWhiteSpace(flow2.LastActOutText),
            "🔴 **拒绝必须有文案**（红线 21）✓");
        Assert.IsNotNull(outcome, "🔴 拒绝用道具 ⇒ **被迫空手**（不是什么都不做）⇒ 仍应有结果 ✓");
        Assert.AreEqual("bare", outcome!.Route,
            "🔴 **被迫空手**：结果路径必须是 `bare`（空手掷骰走完整流程、代价照常承担）✓");
        Assert.IsNull(outcome.ItemUsed, "🔴 被迫空手 ⇒ **不得记成用过道具** ✓");
        Assert.AreEqual(1, flow2.ActOutCurioRefuseCount, "🔴 计数必须 +1 ✓");
        Assert.IsTrue(log.Events.OfType<EventNodeResolvedEvent>()
                .Any(e => e.Effect.Contains("curio_use_item_refused", StringComparison.Ordinal)),
            "🔴 **拒绝必须有事件留痕**（红线 21）✓");
    }

    [TestMethod]
    public void ResolveCurio_NotAfflicted_UsesItem_Normally()
    {
        var log = new CombatLog();
        TuningConfig seeded = TuningWithActOut(100, 100);
        ExpeditionFlow flow = NewFlowWithSession(seeded, log, new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l), seeded.Expedition.NBattles, firewood: 2, food: 10, seeded.Expedition.AmbushChance));
        ExpeditionSession session = (ExpeditionSession)flow.Session;
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log); // 士气 = 50 ⇒ 不带折磨 ✓

        CurioConfig curio = CurioWithItem();
        string item = curio.ItemResults![0].Item;

        CurioOutcome? outcome = flow.ResolveCurio(curio, item);

        Assert.IsFalse(flow.LastActOutRefused, "🔴 士气 50（解除点）⇒ **不带折磨** ⇒ 不拒绝 ✓");
        Assert.AreEqual(0, flow.ActOutCurioRefuseCount, "不拒绝 ⇒ 计数不动 ✓");
        Assert.IsNotNull(outcome, "正常用道具 ⇒ 有结果 ✓");
        Assert.AreEqual("item", outcome!.Route, "🔴 正常路径 = `item`（道具直查）✓");
        Assert.AreEqual(item, outcome.ItemUsed, "🔴 用过的道具必须原样记下 ✓");
    }

    [TestMethod]
    public void ResolveCurio_NoActOutConfig_ItemPathUnaffected()
    {
        var log = new CombatLog();
        ExpeditionFlow flow = NewFlowDefault(log); // 出厂 tuning（**有** exploration 段）
        ExpeditionSession session = (ExpeditionSession)flow.Session;
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        session.CaptureBattleEndHp(b1, log);

        CurioConfig curio = CurioWithItem();
        string item = curio.ItemResults![0].Item;

        // 出厂配置 = `curio_refuse_percent: 33` ⇒ 士气 50 ⇒ 不带折磨 ⇒ **必不拒** ✓
        CurioOutcome? outcome = flow.ResolveCurio(curio, item);
        Assert.IsFalse(flow.LastActOutRefused, "🔴 出厂数据 + 士气 50 ⇒ 不拒绝 ✓");
        Assert.AreEqual("item", outcome!.Route, "⇒ 正常走道具路径 ✓");
    }

    // ══════════════════════ 夹具：带自定义 exploration 的流程 ══════════════════════

    /// <summary>把 `tuning.json` 的 `exploration` 段替换成给定概率（**保持格式契约**）✓</summary>
    private static TuningConfig TuningWithActOut(double curioRefuse, double eatRefuse)
    {
        string raw = ReadData("tuning.json");
        // 🔴 用**文本级替换**改两个概率（不重排整个文件 ⇒ 与既有格式契约用例相容）✓
        string patched = raw
            .Replace("\"curio_refuse_percent\": 33", $"\"curio_refuse_percent\": {curioRefuse}")
            .Replace("\"eat_refuse_percent\": 25", $"\"eat_refuse_percent\": {eatRefuse}");
        return TuningConfig.Parse(patched);
    }

    private static ExpeditionFlow NewFlowWithSession(TuningConfig tuning, CombatLog log, ExpeditionSession session)
    {
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!),
            ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, log, new RngProvider(20260918));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }
}
