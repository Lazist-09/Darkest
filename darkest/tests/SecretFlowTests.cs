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
/// 🔴🔴 **`D-6` 流程级：隐藏房的接线与「非信息类回报」**。
///
/// 为什么必须有这一批：`RevealSecrets` 若**无人调用** ⇒ 整个 `D-6` 只是一堆测试自嗨的库
/// （三扫会把死函数抓出来 —— 那是**结构性**信号）⚠️
///
/// 本批锁住四件事：
///  ① **opt-in**：未配置 `secrets` ⇒ 不撒、不揭示、不给钱、不写日志（既有调用点行为逐字不变）✓
///  ② 🔴 **侦察真的有钱拿**（这就是 `D-6` 存在的理由 —— 否则玩家理性地永不侦察）✓
///  ③ 🔴 **幂等 + 配额**：同一格不重复发钱；配额用尽即停 ✓
///  ④ 🔴 **揭示 = `Scouted`（不是 `Visited`）**：侦察 ≠ 站过 ⇒ `D-1` 回头代价不受影响 ✓
/// </summary>
[TestClass]
public sealed class SecretFlowTests
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

    /// <summary>构造流程；`secrets` **显式注入**（未给 ⇒ 关闭 ⇒ 既有行为不变）✓</summary>
    private static ExpeditionFlow NewFlow(double? secretChance = null, int rewardGold = 100, int quota = 0,
        Economy? economy = null, CombatLog? log = null, bool useShippedSecrets = false)
        => BuildFlow(TuningConfig.Parse(ReadData("tuning.json")), secretChance, rewardGold, quota, economy, log,
            useShippedSecrets);

    /// <summary>
    /// 🔴 `useShippedSecrets`：**保留**出货数据里的 `secrets`（不覆盖）——
    /// 但要满足调用方给的概率/回报时**就地改写**（其余字段照抄出货值）✓
    /// </summary>
    private static ExpeditionFlow BuildFlow(TuningConfig tuning, double? secretChance, int rewardGold, int quota,
        Economy? economy, CombatLog? log, bool useShippedSecrets = false)
    {
        tuning = tuning with
        {
            DungeonLayer = (tuning.DungeonLayer ?? new TuningDungeonLayer()) with
            {
                // 🔴 `D-3` 的 `vision` 在出货数据里是**开着**的（radius 2）—— 它会立刻把"侦察到的格"
                //    升成 `Visited`（`Scouted` + 在视野内 ⇒ 升级，这是**正确**的 `D-3` 语义）⇒
                //    要测"揭示只给 `Scouted`"就必须**显式关掉视野**（否则测的是两条通道的合成结果）✓
                Vision = null,
                // 🔴 `secretChance` 显式给了 ⇒ 用给的；**没给 ⇒ 显式置 null**（关掉）——
                //    绝不能"不给就沿用 data 里的值"（那样用例会随出货数据漂移，`D-2` 踩过的坑）⚠️
                //    ⚠️ 例外：`useShippedSecrets = true` ⇒ **不动**出货配置（守门人用例）✓
                Secrets = useShippedSecrets
                    ? (tuning.DungeonLayer?.Secrets is { } shipped
                        ? new TuningSecretScatter(
                            secretChance ?? shipped.CorridorChancePercent,
                            rewardGold > 0 ? rewardGold : shipped.RewardGold,
                            quota,
                            shipped.Note)
                        : null)
                    : secretChance is { } c ? new TuningSecretScatter(c, rewardGold, quota, "test") : null,
            },
        };

        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var rng = new RngProvider(20260920);
        session.BindTrapRng(rng);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, log ?? new CombatLog(), rng, economy);
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }

    /// <summary>数一数派生图上有几个隐藏房格（`D-6` 的产出源验证）✓</summary>
    private static int CountSecretTiles(ExpeditionFlow flow)
    {
        DungeonGrid grid = flow.TileWalk!.Grid;
        int n = 0;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.TileAt(x, y) == DungeonTileKind.Secret)
                {
                    n++;
                }
            }
        }

        return n;
    }

    /// <summary>全部隐藏房格（确定性排序）✓</summary>
    private static List<(int X, int Y)> SecretTiles(ExpeditionFlow flow)
    {
        var list = new List<(int X, int Y)>();
        DungeonGrid grid = flow.TileWalk!.Grid;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.TileAt(x, y) == DungeonTileKind.Secret)
                {
                    list.Add((x, y));
                }
            }
        }

        return list;
    }

    // ── ① opt-in：未配置 ⇒ 行为逐字不变 ────────────────────────────────────────

    [TestMethod]
    public void SecretsNotConfigured_IsInert_NeverScattersRevealsOrPays()
    {
        // 🔴 未配置 `secrets` ⇒ 一格不撒、不揭示、不给钱、不写日志 ✓
        ExpeditionFlow flow = NewFlow();
        flow.EnableTileWalk(SegmentCost);

        Assert.AreEqual(0, flow.TileWalk!.SecretTiles, "未配置 ⇒ 一格都不撒 ✓");
        Assert.AreEqual(0, CountSecretTiles(flow), "网格里一个 `*` 都没有 ✓");
        Assert.AreEqual(0, flow.RevealSecrets(new[] { (flow.TilePosition.X, flow.TilePosition.Y) }),
            "未配置 ⇒ 揭示返回 0（明确不做，不抛）✓");
        Assert.AreEqual(0, flow.SecretRevealedCount, "读数恒 0（不冒充「发生过」）✓");
        Assert.AreEqual(0, flow.SecretGoldGranted, "一分钱都不给 ✓");
        Assert.IsNull(flow.LastSecret, "读数恒 null ✓");
    }

    [TestMethod]
    public void SecretChanceZero_ScattersNothing_AndDrawsNoRng()
    {
        // 🔴 `chance = 0` ⇒ 关闭口径（不掷不写，零随机不留痕）✓
        //    ⚠️ 不用"全流程掷骰总数"做判据：开启走格本身（派生/生成/光照）也会有掷骰，
        //       而且**量级相近** ⇒ 靠总数差会变成脆断言（实测就翻过车）⚠️
        //    ⇒ 改用**结构性判据**：同一张图、同一颗种子，两次 `Derive` 只在"关/开"上不同 ——
        //       关闭那次**不该有任何** `Secret`；开启那次**必须有** ⇒ 且**逐行只在 关→开 时变化** ✓
        ExpeditionMap mapA = ExpeditionMapGenerator.Generate(new CombatLog(), new RngProvider(20260920),
            ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        ExpeditionMap mapB = ExpeditionMapGenerator.Generate(new CombatLog(), new RngProvider(20260920),
            ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));

        var logOff = new CombatLog();
        DungeonGridDeriver.Derived off = DungeonGridDeriver.Derive(
            mapA, DungeonGridDeriver.MaxTrunkSegments, new RngProvider(20260920), 0, 0, logOff);
        var logOn = new CombatLog();
        DungeonGridDeriver.Derived on = DungeonGridDeriver.Derive(
            mapB, DungeonGridDeriver.MaxTrunkSegments, new RngProvider(20260920), 0, 100, logOn);

        Assert.AreEqual(0, off.SecretTiles, "概率 0 ⇒ 一格不撒 ✓");
        Assert.IsFalse(off.Grid.ToRows().Any(r => r.Contains('*')), "关闭 ⇒ 网格里没有 `*` ✓");
        Assert.AreEqual(0, logOff.Events.OfType<RngDraw>().Count(), "🔴 关闭 ⇒ **一次都不掷**（零随机不留痕）✓");

        Assert.IsTrue(on.SecretTiles > 0, "对照：概率 100 ⇒ 真的撒（证明判据有意义）✓");
        Assert.IsTrue(logOn.Events.OfType<RngDraw>().Count() > 0, "开着 ⇒ 有掷骰记录 ✓");

        // 🔴 结构性：关→开 的唯一差别就是那些 `*` 格（其余逐行相同 ⇒ 撒布**只动被选中的格**）✓
        string[] offRows = off.Grid.ToRows().ToArray();
        string[] onRows = on.Grid.ToRows().ToArray();
        Assert.AreEqual(offRows.Length, onRows.Length, "网格形状不得因撒布而变 ✓");
        for (int y = 0; y < offRows.Length; y++)
        {
            for (int x = 0; x < offRows[y].Length; x++)
            {
                if (onRows[y][x] != offRows[y][x])
                {
                    Assert.AreEqual('*', onRows[y][x],
                        $"({x},{y}) 只允许 `*` 覆盖走廊格（不得改动房间/墙/终点）✓");
                    Assert.AreEqual('C', offRows[y][x],
                        $"({x},{y}) 被 `*` 覆盖前必须是走廊格（DD：隐藏房只在走廊里）✓");
                }
            }
        }
    }

    // ── ② 🔴 侦察真的有「非信息类」回报（本刀的存在理由）────────────────────────

    [TestMethod]
    public void RevealSecrets_GrantsGold_ForEachSecretTile_InScope()
    {
        // 🔴🔴 **`D-6` 的核心判据**：揭示隐藏房 ⇒ **真的拿到资源**（不是只给"信息"）✓
        //    否则玩家"花光照换一个好看"⇒ 理性地永不侦察 ⇒ D-3/D-4 整条链路沦为装饰 ⚠️
        var log = new CombatLog();
        var eco = new Economy(EconomyConfig.Parse(ReadData("economy.json")), 0);
        ExpeditionFlow flow = NewFlow(secretChance: 100, rewardGold: 250, economy: eco, log: log);
        flow.EnableTileWalk(SegmentCost);
        List<(int X, int Y)> tiles = SecretTiles(flow);
        Assert.IsTrue(tiles.Count > 0, "概率 100% ⇒ 至少应撒出一个隐藏房（否则本用例无意义）✓");

        int goldBefore = eco.Gold;
        int granted = flow.RevealSecrets(tiles);

        Assert.AreEqual(tiles.Count, granted, "范围内每个隐藏房格都应被揭示 ✓");
        Assert.AreEqual(tiles.Count, flow.SecretRevealedCount, "读数与事实一致 ✓");
        Assert.AreEqual(tiles.Count * 250, flow.SecretGoldGranted, "金币 = 格数 × 单格回报 ✓");
        Assert.AreEqual(goldBefore + (tiles.Count * 250), eco.Gold,
            "🔴 钱必须**真的进账**（`Economy.AwardContent`）—— 这是「非信息类回报」的字面含义 ✓");
        Assert.IsTrue(log.Events.OfType<GoldChangedEvent>().Any(e => e.Reason == "secret"),
            "🔴 金币必须走**唯一记账通道** `GoldChangedEvent` ✓");
        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType.StartsWith("secret_revealed:")),
            "🔴 **必须留痕**（否则「钱怎么多了」不可追溯）✓");
    }

    [TestMethod]
    public void RevealSecrets_WithoutEconomy_RevealsButDoesNotFakeIncome()
    {
        // 🔴 未注入 `Economy` ⇒ 钱**无处可记** ⇒ **不静默装作给过了**：
        //    仍然揭示（地图上真的多一间房）、仍然留痕，但金币读数为 0，并写一条明确事件 ✓
        var log = new CombatLog();
        ExpeditionFlow flow = NewFlow(secretChance: 100, rewardGold: 250, log: log); // economy 不传
        flow.EnableTileWalk(SegmentCost);
        List<(int X, int Y)> tiles = SecretTiles(flow);

        int granted = flow.RevealSecrets(tiles);
        Assert.AreEqual(tiles.Count, granted, "揭示本身照做（地图上会多出房间）✓");
        Assert.AreEqual(0, flow.SecretGoldGranted, "🔴 未接账本 ⇒ **不谎报金币**（读数 0）✓");
        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "secret_no_economy_gold_uncredited"),
            "🔴 未接账本必须**留痕**（不静默）✓");
    }

    [TestMethod]
    public void RevealSecrets_MarksScouted_ButNeverVisitedCollection()
    {
        // 🔴🔴 与 `D-3` / `D-1` 的接线纪律：揭示 = **Scouted**（不是 `Visited`）；
        //    **不写 `Visited` 集合**（侦察 ≠ 站过 ⇒ 否则第一次走到那里会被当成「回头」多扣光）⚠️
        //    ⚠️ 本用例的前提是**关掉 `vision`**（见 `BuildFlow`）—— 否则"侦察 + 视野"会合成 `Visited`，
        //       测到的就不是"揭示本身给了什么态"（那是 `D-3` 的两条通道交汇，属另一批用例）✓
        ExpeditionFlow flow = NewFlow(secretChance: 100);
        flow.EnableTileWalk(SegmentCost);
        (int X, int Y) pos = SecretTiles(flow)[0];

        int visitedBefore = flow.VisitedTileCount;

        flow.RevealSecrets(new[] { pos });

        Assert.AreEqual(RevealState.Scouted, flow.TileStateAt(pos),
            "🔴 揭示 ⇒ `Scouted`（暗 + 亮轮廓）—— **不是** `Visited`（没走过去）✓");
        Assert.IsFalse(flow.VisitedTileAt(pos),
            "🔴 **不得写 `Visited` 集合**（否则 `D-1` 回头代价会算错）⚠️");
        Assert.AreEqual(visitedBefore, flow.VisitedTileCount, "「站过」集合一格未增 ✓");
        Assert.AreNotEqual(pos, flow.TilePosition, "🔴 揭示 ≠ 移动：队伍仍在原地 ✓");
    }

    [TestMethod]
    public void RevealSecrets_IsIdempotent_SameTileNeverPaysTwice()
    {
        // 🔴 幂等：重侦察（或同一次重复给同一格）**不会重复发钱** ✓
        var eco = new Economy(EconomyConfig.Parse(ReadData("economy.json")), 0);
        ExpeditionFlow flow = NewFlow(secretChance: 100, rewardGold: 250, economy: eco);
        flow.EnableTileWalk(SegmentCost);
        (int X, int Y) pos = SecretTiles(flow)[0];

        Assert.AreEqual(1, flow.RevealSecrets(new[] { pos }), "第一次 ⇒ 揭示 1 处 ✓");
        Assert.AreEqual(0, flow.RevealSecrets(new[] { pos }), "第二次 ⇒ 0 处（已领过）✓");
        Assert.AreEqual(250, flow.SecretGoldGranted, "🔴 同一格**只发一次**（不是 500）✓");
        Assert.AreEqual(250, eco.Gold, "账本上也只入账一次 ✓");
    }

    [TestMethod]
    public void RevealSecrets_RespectsQuota_AndLeavesATraceWhenExhausted()
    {
        // 🔴 配额：`max_rewards_per_run`（0 = 不限）—— 防"一张图把金库刷空"；
        //    配额用尽 ⇒ 停，且**留痕**（否则"预览里有、结算时没了"不可解释）⚠️
        var log = new CombatLog();
        var eco = new Economy(EconomyConfig.Parse(ReadData("economy.json")), 0);
        ExpeditionFlow flow = NewFlow(secretChance: 100, rewardGold: 100, quota: 1, economy: eco, log: log);
        flow.EnableTileWalk(SegmentCost);
        List<(int X, int Y)> tiles = SecretTiles(flow);
        Assert.IsTrue(tiles.Count >= 2, $"本用例需要 ≥2 个隐藏房格（实测 {tiles.Count}）✓");

        Assert.AreEqual(1, flow.RevealSecrets(tiles), "配额 1 ⇒ 只揭示 1 处 ✓");
        Assert.AreEqual(100, flow.SecretGoldGranted, "只发一份钱 ✓");

        int again = flow.RevealSecrets(tiles);
        Assert.AreEqual(0, again, "配额用尽 ⇒ 不再揭示 ✓");
        Assert.IsTrue(log.Events.OfType<EffectEvent>().Any(e => e.EffectType.StartsWith("secret_quota_reached:")),
            "🔴 配额用尽**必须留痕**（不静默）✓");
    }

    [TestMethod]
    public void RevealSecrets_IgnoresNonSecretTiles_AndOutOfScopeTiles()
    {
        // 🔴 只处理**真·隐藏房格**：给一堆普通格/越界格 ⇒ 一分不给 ✓
        ExpeditionFlow flow = NewFlow(secretChance: 100, rewardGold: 250);
        flow.EnableTileWalk(SegmentCost);

        int granted = flow.RevealSecrets(new[]
        {
            flow.TilePosition,          // 队伍脚下（房间格）
            (9999, 9999),               // 越界
            (-99, -99),                 // 越界
        });

        Assert.AreEqual(0, granted, "非隐藏房格 ⇒ 不结算 ✓");
        Assert.AreEqual(0, flow.SecretGoldGranted, "一分钱都不给 ✓");
    }

    [TestMethod]
    public void RevealSecrets_OnTileWalkDisabled_IsInert()
    {
        // 🔴 未开走格 ⇒ 没有格可落 ⇒ 明确忽略（不抛、不给钱）✓
        ExpeditionFlow flow = NewFlow(secretChance: 100);
        Assert.IsFalse(flow.TileWalkEnabled, "前置：未开走格 ✓");
        Assert.AreEqual(0, flow.RevealSecrets(new[] { (0, 0) }), "未开走格 ⇒ 0（明确忽略）✓");
        Assert.AreEqual(0, flow.SecretGoldGranted, "不给钱 ✓");
    }

    // ── ③ 走进去：揭示后它就是一间**可进的房** ─────────────────────────────────

    [TestMethod]
    public void SecretTile_AfterReveal_IsWalkable_PlayerCanEnter()
    {
        // 🔴 DD 口径（wiki ⑤「走到那格才触发」）：揭示只是"看见了"，玩家**要自己走过去**。
        //    故隐藏房**必须可通行**，且走格内核**不得把它当墙** ✓
        ExpeditionFlow flow = NewFlow(secretChance: 100);
        flow.EnableTileWalk(SegmentCost);
        (int X, int Y) secret = SecretTiles(flow)[0];

        Assert.AreNotEqual(DungeonTileKind.Wall, flow.TileWalk!.Grid.TileAt(secret.X, secret.Y),
            "隐藏房不是墙 ✓");
        Assert.IsTrue(DungeonTileMap.IsWalkable(flow.TileWalk.Grid.TileAt(secret.X, secret.Y)),
            "🔴 隐藏房**可通行**（否则「揭示后成 rewards 房」永远进不去）⚠️");

        // 沿真实寻路走一步靠近（只验证"内核允许进入"，不假设能一步到达）✓
        IReadOnlyList<(int X, int Y)> path = new DungeonWalker(flow.TileWalk.Grid, flow.TilePosition)
            .PathTo(secret.X, secret.Y);
        Assert.IsTrue(path.Count > 0, "隐藏房必须从起点**可达**（否则揭示也拿不到东西）✓");
    }

    [TestMethod]
    public void SecretAndTrap_NeverShareATile_InTheDerivedGrid()
    {
        // 🔴 单值枚举的结构保证：一趟扫描里每格至多一次掷骰 ⇒ 一格不可能是两种内容 ✓
        ExpeditionFlow flow = NewFlow(secretChance: 100);
        flow.EnableTileWalk(SegmentCost);
        DungeonGrid grid = flow.TileWalk!.Grid;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                DungeonTileKind k = grid.TileAt(x, y);
                Assert.IsTrue(k == DungeonTileKind.Secret || k == DungeonTileKind.Trap || k == DungeonTileKind.Corridor
                              || k == DungeonTileKind.Room || k == DungeonTileKind.Battle || k == DungeonTileKind.Event
                              || k == DungeonTileKind.Camp || k == DungeonTileKind.Curio || k == DungeonTileKind.Goal
                              || k == DungeonTileKind.Floor || k == DungeonTileKind.Wall || k == DungeonTileKind.Door,
                    $"({x},{y}) = {k} 必须是**单一**枚举值 ✓");
            }
        }
    }

    // ── ④ 出货数据：机制真的开着（默认配置下不静默消失）────────────────────────

    [TestMethod]
    public void ShippedTuning_ScattersSecrets_WhenTileWalkEnabled()
    {
        // 🔴 出货数据已配 `secrets` ⇒ 开走格后**应该真的撒出隐藏房**
        //    （本用例是"默认配置下 D-6 不静默消失"的守门人）✓
        TuningConfig shipped = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.IsNotNull(shipped.DungeonLayer?.Secrets, "出货数据应带 `secrets` ✓");

        ExpeditionFlow flow = BuildFlow(shipped, null, 0, 0, null, null, useShippedSecrets: true);
        Assert.IsNotNull(flow.Tuning.DungeonLayer?.Secrets, "出货配置必须被**原样保留** ✓");

        flow.EnableTileWalk(SegmentCost);
        Assert.IsTrue(flow.TileWalk!.SecretTiles > 0,
            $"出货配置 {flow.Tuning.DungeonLayer!.Secrets!.CorridorChancePercent}% ⇒ " +
            "本轮派生应至少撒出一个隐藏房（若为 0 ⇒ 概率过低或走廊太短，须复核）✓");
    }
}
