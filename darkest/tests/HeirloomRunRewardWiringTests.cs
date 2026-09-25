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
/// 🆕 **传家宝「步骤 ②」的验收：产出通道 = 任务奖励**（策划 `HEIRLOOM-STEP2-ANSWER` ✓）
/// （`#470` 顺序 **(A) 先接线（`49b1908`）→ 后删旧源（本件）** ✓）
///
/// 🔴 本件要证明的**四件事**（每条都有断言，不是"打印一下" ✓ 纪律 BJ）：
///   ① **通道真的被用**：长度 1 ⇒ 发 **0**（旧通道按 `black` 会给 9）✓
///   ② **两套 kind 名的桥是对的**：`kinds` 是**复数**、`amount_table` 是**单数** —— 🔴 不桥 ⇒ **静默一件不发** ⚠️
///   ③ **难度档真的来自队伍平均等级**：avg 1 / 3 / 5 ⇒ 档 1 / 3 / 5（读数 26 / 36 / 54）✓
///   ④ 🆕 **旧通道已删 ⇒ 通道缺失时【抛】**（不静默发 0、也没有退路 ✓）
///
/// 🔴 **口径 (ii)**：`amounts[difficulty][length - 1]` ⇒ **length 1 ⇒ 0** ✓
/// ⚠️ **两个代理量**（`averageLevel` / `steps`）都是**我推的** ⇒ `placeholder` + 观察清单 **O11 / O12** ✓
/// </summary>
[TestClass]
public sealed class HeirloomRunRewardWiringTests
{
    /// <summary>
    /// 🔴 **旧通道（按光照档掉落）的历史读数** —— **步骤 ② 已把它从数据里删掉** ⇒
    ///    这些数字**不再能从 `heirlooms.json` 读出来** ⇒ 按纪律 **AR/BJ 留档成常量 + 指明出处** ✓
    ///    （🗑️ 出处：`dd1_baseline §54.2` 前读数 · 删源提交见 §55 ⇒ 旧段 = `tier_drop` ✓）
    /// </summary>
    private static readonly Dictionary<string, int> OldTierDropReadings = new()
    {
        ["radiant"] = 0, ["dim"] = 1, ["shadowy"] = 2, ["dark"] = 4, ["black"] = 9,
    };

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

    private static HeirloomConfig Cfg() => HeirloomConfig.Parse(ReadData("heirlooms.json"));

    private static HeirloomQuestRewardConfig Reward()
    {
        HeirloomQuestRewardConfig cfg = HeirloomQuestRewardConfig.Parse(ReadData("heirlooms.json"));
        cfg.Validate();
        return cfg;
    }

    /// <summary>挂上通道的库存（= 步骤 ② 之后生产路径的形状 ✓）</summary>
    private static HeirloomStock Stock() => new(Cfg(), Reward());

    // ── ① 通道真的被用（旧通道读数已不可能复现）────────────────────────

    [TestMethod]
    public void RunChannel_IsActuallyUsed_NotTheOldTierDrop()
    {
        var log = new CombatLog();
        HeirloomStock stock = Stock();

        // 长度 1：任务奖励口径 = **0**（一手：[0,3,5,9] 首项为 0 ⇒ 短任务不给 ✓）
        int len1 = stock.AwardForRun(log, steps: 1, averageLevel: 5);
        Assert.AreEqual(0, len1, "长度 1 ⇒ **0** ✓（一手首项为 0）");

        // 长度 4：档 5 ⇒ **54**（= deed 18 + crest 18 + bust 9 + portrait 9 ✓）
        int len4 = stock.AwardForRun(log, steps: 4, averageLevel: 5);
        Assert.AreEqual(54, len4, "档5 长度4 ⇒ **54** ✓");

        // 🔴 旧通道**已从数据删除** ⇒ 它的读数只能作为历史常量对照（不能再从 json 读出来 ✓）
        Assert.AreEqual(9, OldTierDropReadings["black"], "旧通道 black ⇒ 9（历史读数 · 出处 §54.2）✓");
        Assert.AreNotEqual(OldTierDropReadings["black"], len1,
            "🔴 长度 1 时**两条路读数不同**（0 vs 9）⇒ 证明【真的换了通道】✓");
        Assert.AreEqual(16, OldTierDropReadings.Values.Sum(), "旧通道 5 档合计 = 0+1+2+4+9 = 16（历史·已删）✓");

        Console.WriteLine($"[传家宝·步骤②] 通道在用：长度1 ⇒ **{len1}**（旧通道 black 会给 **9** ⇒ 不同 ⇒ 换源生效 ✓）；" +
                          $"长度4 档5 ⇒ **{len4}** ✓");
    }

    [TestMethod]
    public void RunChannel_LengthIsOneBased_AndClamped()
    {
        var log = new CombatLog();

        // 段数 0 / 负数 ⇒ 长度 1（钳）⇒ 0 ✓；段数 >4 ⇒ 长度 4 ✓
        Assert.AreEqual(0, Stock().AwardForRun(log, 0, 5), "段数 0 ⇒ 钳到长度 1 ⇒ 0 ✓");
        Assert.AreEqual(0, Stock().AwardForRun(log, -3, 5), "段数负数 ⇒ 钳到长度 1 ⇒ 0 ✓");
        Assert.AreEqual(54, Stock().AwardForRun(log, 9, 5), "段数 9 ⇒ 钳到长度 4 ⇒ 54 ✓");
        Assert.AreEqual(54, Stock().AwardForRun(log, 4, 5), "段数 4 ⇒ 长度 4 ⇒ 54 ✓");

        Console.WriteLine("[传家宝·步骤②] `ProxyQuestLengthFromSteps`：0/-3 ⇒ 1 · 4/9 ⇒ 4（钳位生效 ✓）");
    }

    // ── ② 两套 kind 名的桥 ───────────────────────────────────────────

    [TestMethod]
    public void TwoKindNamingSchemes_AreBridged_SoAllFourKindsArrive()
    {
        var log = new CombatLog();
        HeirloomStock stock = Stock();

        int total = stock.AwardForRun(log, 4, 5); // 档5 长度4

        Assert.AreEqual(54, total, "总数对上 ✓");
        // 🔴 `kinds`（库存）= 复数；`amount_table`（奖励表）= 单数 ⇒ 必须逐条对上（不是"总数对就算过"）
        Assert.AreEqual(9, stock.Count("busts"), "busts ← bust 9 ✓（🔴 不桥 ⇒ 这里会是 0 ⚠️）");
        Assert.AreEqual(9, stock.Count("portraits"), "portraits ← portrait 9 ✓");
        Assert.AreEqual(18, stock.Count("deeds"), "deeds ← deed 18 ✓");
        Assert.AreEqual(18, stock.Count("crests"), "crests ← crest 18 ✓");
        Assert.AreEqual(4, new[] { stock.Count("busts"), stock.Count("portraits"), stock.Count("deeds"),
            stock.Count("crests") }.Count(n => n > 0), "**四种全发**（每趟 4 种都给 ✓ placeholder ⚠️ O11）");

        // 事件流里也必须四种都有（可检索 ✓）
        List<HeirloomChangedEvent> evts = log.Events.OfType<HeirloomChangedEvent>().ToList();
        Assert.AreEqual(4, evts.Count, "四种各写一条事件 ✓");
        CollectionAssert.AreEquivalent(new[] { "busts", "portraits", "deeds", "crests" },
            evts.Select(e => e.Kind).ToArray(), "事件里的 kind 是**复数**（库存口径 ✓）");

        Console.WriteLine("[传家宝·步骤②] kind 两套名的桥：kinds(busts/crests/deeds/portraits) ← " +
                          "amount_table(bust/crest/deed/portrait) ⇒ 9/18/18/9 合计 54 ✓");
    }

    // ── ③ 难度档 ← 队伍平均等级 ──────────────────────────────────────

    [TestMethod]
    public void DifficultyBand_ComesFromPartyAverageLevel()
    {
        var log = new CombatLog();

        Assert.AreEqual(26, Stock().AwardForRun(log, 4, 1.0), "avg 1 ⇒ 档1 长度4 ⇒ **26** ✓");
        Assert.AreEqual(26, Stock().AwardForRun(log, 4, 1.4), "avg 1.4 ⇒ 四舍五入 1 ⇒ 档1 ⇒ 26 ✓");
        // 🔴 **当场修正我自己写错的一条**：`avg 1.9` ⇒ `Math.Round` = **2** ⇒ 而 2 落**靠后档** `[2,3,4]→3`
        //    ⇒ 读数 **36**（我第一版写 26 ✗ ⇒ 是**断言错**，不是代码错 —— 纪律 BJ：假红灯更要命 ⚠️）
        Assert.AreEqual(36, Stock().AwardForRun(log, 4, 1.9),
            "avg 1.9 ⇒ round=2 ⇒ **靠后档 3**（重叠取靠后，一手口径 ✓）⇒ 36 ✓");
        Assert.AreEqual(36, Stock().AwardForRun(log, 4, 3.0), "avg 3 ⇒ 档3 长度4 ⇒ **36** ✓");
        Assert.AreEqual(54, Stock().AwardForRun(log, 4, 5.0), "avg 5 ⇒ 档5 长度4 ⇒ **54** ✓");
        Assert.AreEqual(54, Stock().AwardForRun(log, 4, 6.0), "avg 6 ⇒ 档5（带上限）⇒ 54 ✓");

        Console.WriteLine("[传家宝·步骤②] 难度档 ← 平均等级：1 / 1.4 ⇒ 档1(26) · **1.9 ⇒ round=2 ⇒ 靠后档3(36)** · " +
                          "3 ⇒ 档3(36) · 5/6 ⇒ 档5(54) ✓");
    }

    /// <summary>
    /// 🔴 **与光照解耦的判据在步骤 ② 变了形**（旧版：拿 `radiant`/`black` 两个光照档比读数）：
    ///   现在**没有光照档参数了** ⇒ 解耦的证明变成【**结构性的**】：
    ///   `AwardForRun` 的签名里**没有光照档**，且数据里**没有 `tier_drop`** ✓
    ///   （🎖️ 这比"读数不变"更强：**不是"换了值也一样"，而是"这个输入已经不存在了"** ✓）
    /// </summary>
    [TestMethod]
    public void AwardNoLongerTakesALightTier_SoItIsDecoupledByConstruction()
    {
        var sig = typeof(HeirloomStock).GetMethod(nameof(HeirloomStock.AwardForRun))!;
        string[] names = sig.GetParameters().Select(p => p.Name!).ToArray();
        CollectionAssert.AreEqual(new[] { "log", "steps", "averageLevel", "reason" }, names,
            "🔴 签名里**没有**光照档参数 ⇒ 产出与光照**结构上**解耦 ✓");
        Assert.IsNull(typeof(HeirloomStock).GetMethod("AwardForTier"),
            "🔴 旧口 `AwardForTier` **已删**（不是留着不用 ⇒ 纪律：同一个 vs 恰好一样 ✓）");
        Assert.IsNull(typeof(HeirloomConfig).GetMethod("DropFor"), "🔴 `DropFor` 同批删 ✓");

        Console.WriteLine("[传家宝·步骤②] 解耦是**结构性**的：`AwardForRun(log, steps, averageLevel, reason)` " +
                          "无光照档 · `AwardForTier`/`DropFor` 已删 ✓");
    }

    // ── ④ 步骤 ②：**旧通道已删** ⇒ 通道缺失时**抛**（不静默发 0，也没有退路）────────────

    /// <summary>
    /// 🔴 **前提被步骤 ② 推翻 ⇒ 当场重写**（旧名 `Bridge_WhenChannelMissing_FallsBackToTheOldTierDrop`）：
    ///   · **P3 当时**：通道缺失 ⇒ 回落 `AwardForTier`（桥，读数 = 9）
    ///   · **P4 现在**：旧通道**已删** ⇒ 通道缺失 ⇒ **抛**（**不静默发 0** ✓ 红线 20 ⑤ 的同族）
    ///   ⇒ 📌 **"桥"的生命周期被钉住**：接线那轮它有用例覆盖，删源那轮它的用例**同时消失**（不留空壳）✓
    /// </summary>
    [TestMethod]
    public void NoChannel_ThrowsInsteadOfSilentlyAwardingZero()
    {
        var bare = new HeirloomStock(Cfg());           // 🔴 不带通道
        Assert.IsFalse(bare.HasRunReward, "未挂通道 ⇒ `HasRunReward` = false ✓");

        InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
            () => bare.AwardForRun(new CombatLog(), steps: 4, averageLevel: 5.0),
            "🔴 通道缺失 ⇒ **必须抛**（旧行为已删 ⇒ 不许静默发 0、也不许装作有产出）✓");
        Assert.IsTrue(ex.Message.Contains("quest_reward", StringComparison.Ordinal),
            "错误信息要**点名缺的是哪个通道**（可检索 ✓）");

        // ✅ 而挂上通道之后同一调用就正常（证明"抛"是通道问题，不是参数问题 ✓）
        HeirloomStock bound = Stock();
        Assert.AreEqual(54, bound.AwardForRun(new CombatLog(), 4, 5.0), "挂了通道 ⇒ 54 ✓");

        Console.WriteLine("[传家宝·步骤②] 通道缺失 ⇒ **抛**（点名 `quest_reward`）；挂上 ⇒ 54 ✓" +
                          "（旧通道 `tier_drop` 已删 ⇒ **没有退路** ✓）");
    }

    [TestMethod]
    public void BindQuestReward_IsIdempotent_AndNeverDowngrades()
    {
        var log = new CombatLog();

        // `HamletRoot.Build` 先建（**不带通道**）⇒ 远征组合根后补 ⇒ 必须补上（否则步骤 ② 静默不生效 ⚠️）
        var late = new HeirloomStock(Cfg());
        Assert.IsFalse(late.HasRunReward, "建时无通道 ✓");
        late.BindQuestReward(Reward());
        Assert.IsTrue(late.HasRunReward, "🔴 **后到者补绑生效**（实测的顺序陷阱：Hamlet 先建 · 远征后补）✓");
        Assert.AreEqual(54, late.AwardForRun(log, 4, 5), "补绑后走通道 ⇒ 54 ✓");

        // 已挂 ⇒ 再挂 / 挂 null 都不覆盖（不降级 ✓）
        late.BindQuestReward(null);
        Assert.IsTrue(late.HasRunReward, "再挂 null ⇒ **不覆盖**（不降级 ✓）");
        Assert.AreEqual(54, late.AwardForRun(log, 4, 5), "仍然走通道 ✓");

        Console.WriteLine("[传家宝·步骤②] `BindQuestReward`：晚绑生效 ✓ · 再绑 null 不降级 ✓");
    }

    // ── 📊 前后读数（策划要的「传家宝产出量（每趟/每档）」✓）

    [TestMethod]
    public void Readings_BeforeAndAfter_ForThePlanner()
    {
        HeirloomQuestRewardConfig reward = Reward();

        Console.WriteLine("[传家宝·步骤②] 📊 **前读数**（旧通道 = `tier_drop` 按光照档 · 每场战斗一次 · **已删**）：");
        foreach (string tier in HeirloomConfig.TierOrder)
        {
            Console.WriteLine($"    {tier,-8} ⇒ 合计 **{OldTierDropReadings[tier]}**");
        }

        Console.WriteLine("[传家宝·步骤②] 📊 **后读数**（新通道 = 任务奖励 · 每场战斗一次 · 难度档 ← 队伍平均等级）：");
        foreach (int tier in new[] { 1, 3, 5 })
        {
            string line = string.Join(" · ", new[] { 1, 2, 3, 4 }
                .Select(len => $"长度{len}=**{reward.RewardTotalFor(tier, len)}**"));
            Console.WriteLine($"    档{tier}（平均等级 {Band(tier)}）⇒ {line}");
        }

        // 🔴 断言（不是只打印）：三档 × 四长度 = 12 格，逐格对上"一手表"
        var expect = new Dictionary<(int Tier, int Len), int>
        {
            [(1, 1)] = 0, [(1, 2)] = 10, [(1, 3)] = 14, [(1, 4)] = 26,
            [(3, 1)] = 0, [(3, 2)] = 12, [(3, 3)] = 18, [(3, 4)] = 36,
            [(5, 1)] = 0, [(5, 2)] = 18, [(5, 3)] = 28, [(5, 4)] = 54,
        };
        foreach (KeyValuePair<(int Tier, int Len), int> kv in expect)
        {
            Assert.AreEqual(kv.Value, reward.RewardTotalFor(kv.Key.Tier, kv.Key.Len),
                $"档{kv.Key.Tier} 长度{kv.Key.Len} 应为 {kv.Value}");
        }

        // 旧通道逐档合计（历史常量）= 0+1+2+4+9 = 16；新通道 3 档 × 4 长度 = 50 + 66 + 100 = **216**
        //   （🔴 我第一版写 206 ✗ ⇒ **我算错了**，不是数据错 —— 当场改成逐档相加的实测值 216 ✓）
        Assert.AreEqual(16, OldTierDropReadings.Values.Sum(), "旧通道逐档合计 = 0+1+2+4+9 = 16 ✓");
        Assert.AreEqual(50, new[] { 1, 2, 3, 4 }.Sum(l => reward.RewardTotalFor(1, l)), "档1 四长度合计 0+10+14+26 = 50 ✓");
        Assert.AreEqual(66, new[] { 1, 2, 3, 4 }.Sum(l => reward.RewardTotalFor(3, l)), "档3 四长度合计 0+12+18+36 = 66 ✓");
        Assert.AreEqual(100, new[] { 1, 2, 3, 4 }.Sum(l => reward.RewardTotalFor(5, l)), "档5 四长度合计 0+18+28+54 = 100 ✓");
        Assert.AreEqual(216, expect.Values.Sum(), "新通道 12 格合计 = 50+66+100 = **216** ✓");
    }

    private static string Band(int tier) => tier switch
    {
        1 => "[0,1,2]",
        3 => "[2,3,4]",
        _ => "[4,5,6]",
    };

    // ── ⑤ 端到端：真的从 `ExpeditionFlow` 的结算点发出来 ────────────────

    /// <summary>
    /// 🔴 **生产路径的判据**：不打桩、不直接调 `AwardForRun` ⇒ 走 `OnBattleFinished("PlayerVictory")` ✓
    /// 🔴 **用拓扑模式**（= 生产唯一形态 ✓ `ExpeditionComposition`：必须是拓扑模式）：
    ///    · 生产在拓扑模式下结算 ⇒ `OnBattleFinished` **跳过**"当前步骤必须是战斗节点"的守卫 ✓
    ///    · ⚠️ 我第一版用线性模式 ⇒ 该 seed 的 3 段路径**可能全是事件步** ⇒ 走不到战斗格 ⇒ 抛 ✗
    ///      （**是测试的驱动方式错**，不是被测代码错 ⇒ 改成生产的真实形态 ✓）
    /// 每场胜利结算一次 ⇒ 长度 = `StepsDone + 1`（**本场打完才算走过** ✓）
    /// </summary>
    [TestMethod]
    public void EndToEnd_BattleVictory_AwardsThroughTheRunChannel()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        HeirloomStock stock = Stock();
        HeirloomQuestRewardConfig reward = Reward();

        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new RngProvider(20260909),
            economy: null, heirlooms: stock, heirloomConfig: Cfg(), partyAverageLevel: 5.0);
        // 🔴 `Progress` / `Unlocks` **不设**（都可空 ⇒ 测试项目**引用不到** `scripts/gameplay/scene` 的
        //    `ExpeditionContext` ⇒ 设了会 `CS0234`；且本件不验解锁 ⇒ 如实不碰 ✓）
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));

        var shape = new List<string>();
        var expected = 0;
        for (int i = 1; i <= 3; i++)
        {
            int steps = flow.StepsDone + 1;             // 本场打完 ⇒ 已走过段数
            int band = reward.RewardTotalFor(5, Math.Clamp(steps, 1, 4));
            expected += band;
            flow.OnBattleFinished("PlayerVictory", rounds: 3);
            Assert.AreEqual(i, flow.StepsDone, $"第 {i} 场结算后步数 = {i} ✓");
            Assert.AreEqual(expected, Total(stock), $"第 {i} 场：长度 {steps} ⇒ 本场 {band} ⇒ 累计 {expected} ✓");
            shape.Add($"{steps}⇒{band}");
        }

        // 前 3 场 = 长度 1/2/3 ⇒ 0 + 18 + 28 = 46 ✓
        Assert.AreEqual(46, Total(stock), "3 场累计 **46** ✓");
        Assert.IsTrue(shape[0].EndsWith("⇒0"), "第 1 场长度 1 ⇒ **0**（口径 (ii) ✓）");

        Console.WriteLine($"[传家宝·步骤②] 端到端（真走 `OnBattleFinished` · 拓扑模式）：3 场胜利 ⇒ 长度{string.Join(" / ", shape)} " +
                          $"= **{Total(stock)}** ✓");
    }

    private static int Total(HeirloomStock stock)
        => stock.Kinds.Sum(stock.Count);

    public TestContext TestContext { get; set; } = null!;
}
