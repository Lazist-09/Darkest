using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴🔴 **`D-6` 隐藏房（`Secret`）** —— 本批用例是 `D-6` 的**唯一判据**。
///
/// **DD wiki 口径**（`dungeon_layer_design.md §F3d` / 「Dungeon Map」④）：
/// <list type="bullet">
///   <item><description>走廊格可含：战斗 ・ Curio ・ 障碍 ・ 陷阱 ・ **隐藏房** ・ 饥饿 ✓</description></item>
///   <item><description>🔴 隐藏房在**地图上完全不显示**（不是「暗」，是**不画**）✓</description></item>
///   <item><description>🔴 **只有侦察成功**才能把它变成一张**可进的 rewards 房** ✓</description></item>
///   <item><description>⇒ 这是给侦察的**非信息类回报**：`D-3` 给「信息」、`D-4` 给「避免损失」、
///     `D-6` 给**实打实的资源** —— 否则「揭示了但没东西」⇒ 玩家**理性地永不侦察** ⚠️</description></item>
/// </list>
///
/// 🔴🔴 **登记纪律**（`§F3d` 紧随其后的一句）：每加一个枚举 ⇒ **必须同时**登记
///   ① `DungeonTileMap` 往返 ② `DungeonGridConfig` P30② 字符集校验
///   —— 本批用例 ① ② **各有一条**（漏登记会被既有护栏 `ToChar(FromChar(c)) != c` 抓出）✓
/// </summary>
[TestClass]
public sealed class SecretTests
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

    private static ExpeditionMap NewMap(long seed)
    {
        ExpeditionMapConfig cfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));
        return ExpeditionMapGenerator.Generate(new CombatLog(), new RngProvider(seed), cfg);
    }

    // ── ① 字符 ⇔ 枚举 往返（登记纪律 ①）────────────────────────────────────────

    [TestMethod]
    public void SecretChar_IsRegisteredInBothDirections()
    {
        // 🔴 `D-6` 的第一条硬要求：新增 `'*'` 字符 + 枚举，**双向**都必须通 ✓
        Assert.AreEqual(DungeonTileKind.Secret, DungeonTileMap.FromChar('*'),
            "`*` ⇒ `Secret`（未登记会被 `FromChar` 静默当墙 ⚠️）✓");
        Assert.AreEqual('*', DungeonTileMap.ToChar(DungeonTileKind.Secret),
            "`Secret` ⇒ `*`（往返）✓");
        Assert.AreEqual(DungeonTileKind.Secret, DungeonTileMap.FromChar(DungeonTileMap.ToChar(DungeonTileKind.Secret)),
            "往返恒等 ✓");
    }

    [TestMethod]
    public void SecretChar_PassesP30_2_CharsetCheck()
    {
        // 🔴 登记纪律 ②：把 `*` 放进真瓷砖图 ⇒ 解析器（P30 ②）**必须接受**它。
        //    若忘登记枚举，`ToChar(FromChar('*'))` 会得到 `#` ⇒ 此处会抛「不在字符表里」✓
        var cfg = new DungeonGridConfig(
            Width: 5, Height: 3,
            Tiles: new[] { "#####", "R*C.G", "#####" },
            Start: new DungeonGridPoint(0, 1),
            Goal: new DungeonGridPoint(4, 1));
        DungeonGridConfig parsed = DungeonGridConfig.Parse(Json(cfg));
        Assert.AreEqual('*', parsed.Tiles[1][1], "`*` 通过字符集校验并原样保留 ✓");
    }

    [TestMethod]
    public void UnregisteredChar_IsStillRejected_GuardNotWeakened()
    {
        // 🔴 **反向判据**：护栏必须仍然拦得住（加字符不能把校验放宽）——
        //    `%` 没登记 ⇒ `ToChar(FromChar('%'))` = `#` ≠ `%` ⇒ P30 ② 抛错 ✓
        var cfg = new DungeonGridConfig(
            Width: 5, Height: 3,
            Tiles: new[] { "#####", "R%C.G", "#####" },
            Start: new DungeonGridPoint(0, 1),
            Goal: new DungeonGridPoint(4, 1));
        Assert.ThrowsException<InvalidDataException>(() => DungeonGridConfig.Parse(Json(cfg)),
            "未登记字符必须被 P30 ② 抓出（护栏不得因新增字符而放宽）✓");
    }

    [TestMethod]
    public void SecretIsWalkable_ElseRewardsRoomIsUnreachable()
    {
        // 🔴 揭示后隐藏房要能**走进去拿东西** —— 不可走 ⇒ 「揭示后成 rewards 房」永久失效 ⚠️
        Assert.IsTrue(DungeonTileMap.IsWalkable(DungeonTileKind.Secret),
            "隐藏房**必须可通行**（它揭示后是一间房）✓");
        Assert.IsFalse(DungeonTileMap.IsWalkable(DungeonTileKind.Wall), "墙仍不可走（对照）✓");
    }

    [TestMethod]
    public void ToRows_RoundTripsSecret_NoSilentConversionToFloorOrWall()
    {
        DungeonGrid grid = DungeonGrid.Parse("test://secret-rows", new[] { "#####", "R*CG#", "#####" });
        Assert.AreEqual(DungeonTileKind.Secret, grid.TileAt(1, 1), "`*` ⇒ Secret ✓");
        CollectionAssert.AreEqual(new[] { "#####", "R*CG#", "#####" }, grid.ToRows().ToArray(),
            "`ToRows` 必须**原样还原** `*`（不得悄悄变成 `.` 或 `#`）✓");
    }

    // ── ② 撒布（`ScatterSecrets`）：与 D-4 同构，且**共用一趟走廊扫描** ──────────

    [TestMethod]
    public void Scatter_DisabledByDefault_LeavesZeroSecretTiles()
    {
        // 🔴 opt-in：不给概率 ⇒ **一格不撒**（既有调用点行为逐字不变）✓
        DungeonGridDeriver.Derived d = DungeonGridDeriver.Derive(NewMap(20260915));
        Assert.AreEqual(0, d.SecretTiles, "未开启撒布 ⇒ 隐藏房格数恒 0 ✓");
        Assert.AreEqual(0, d.TrapTiles, "对照：陷阱也是 0 ✓");
        Assert.IsFalse(d.Grid.ToRows().Any(r => r.Contains('*')), "网格里一个 `*` 都不该有 ✓");
    }

    [TestMethod]
    public void Scatter_OnlyOnCorridorTiles_NeverOnRooms()
    {
        // 🔴 DD：隐藏房与陷阱都在**走廊**里 —— 房间有自己的遭遇物 ⇒ 房间格永不放 ✓
        ExpeditionMap map = NewMap(4242);
        DungeonGridDeriver.Derived d = DungeonGridDeriver.Derive(
            map, DungeonGridDeriver.MaxTrunkSegments, new RngProvider(7), trapChancePercent: 0, secretChancePercent: 100);

        int secrets = 0;
        for (int y = 0; y < d.Grid.Height; y++)
        {
            for (int x = 0; x < d.Grid.Width; x++)
            {
                if (d.Grid.TileAt(x, y) != DungeonTileKind.Secret)
                {
                    continue;
                }

                secrets++;
                Assert.IsFalse(d.TileRoom.ContainsKey((x, y)),
                    $"({x},{y}) 是房间格 ⇒ **不得**放隐藏房（房间有自己的遭遇物）⚠️");
                Assert.AreEqual(DungeonTileKind.Secret, d.Grid.TileAt(x, y));
            }
        }

        Assert.IsTrue(secrets > 0, "概率 100% ⇒ 至少应撒出一个（否则本用例无意义）✓");
        Assert.AreEqual(secrets, d.SecretTiles, "`Derived.SecretTiles` 必须等于图上的实际格数 ✓");
    }

    [TestMethod]
    public void Scatter_TrapsAndSecrets_AreMutuallyExclusive_OneRollPerTile()
    {
        // 🔴🔴 **本刀最关键的结构判据**：`DungeonTileKind` 是**单值** ⇒ 同一格不可能既是陷阱又是隐藏房。
        //    若分两趟各扫一遍，第二趟会**覆盖**第一趟 ⇒ "陷阱密度"悄悄变成"隐藏房概率的函数"（不可解释）⚠️
        //    ⇒ 两类**共用一趟**，每格**至多一次**掷骰 ⇒ 图上一格只可能是其中一种 ✓
        ExpeditionMap map = NewMap(99);
        DungeonGridDeriver.Derived d = DungeonGridDeriver.Derive(
            map, DungeonGridDeriver.MaxTrunkSegments, new RngProvider(31337), trapChancePercent: 50, secretChancePercent: 50);

        int traps = 0;
        int secrets = 0;
        for (int y = 0; y < d.Grid.Height; y++)
        {
            for (int x = 0; x < d.Grid.Width; x++)
            {
                DungeonTileKind k = d.Grid.TileAt(x, y);
                if (k == DungeonTileKind.Trap)
                {
                    traps++;
                }
                else if (k == DungeonTileKind.Secret)
                {
                    secrets++;
                }
            }
        }

        Assert.AreEqual(traps, d.TrapTiles, "`TrapTiles` 读数与图上一致 ✓");
        Assert.AreEqual(secrets, d.SecretTiles, "`SecretTiles` 读数与图上一致 ✓");
        Assert.IsTrue(traps > 0 && secrets > 0,
            $"两类各 50% ⇒ 同一趟里应**各自**都撒出（实测 陷阱 {traps} / 隐藏房 {secrets}）" +
            "—— 🔴 两者合计不得超过走廊格数（格是单值，不可能重复占）✓");
    }

    [TestMethod]
    public void Scatter_IsDeterministic_SameSeedSameGrid()
    {
        // 🔴 确定性：同种子 ⇒ 逐行相同（否则冒烟/回放不可复现）✓
        ExpeditionMap mapA = NewMap(20260915);
        ExpeditionMap mapB = NewMap(20260915);
        DungeonGridDeriver.Derived a = DungeonGridDeriver.Derive(
            mapA, DungeonGridDeriver.MaxTrunkSegments, new RngProvider(555), 30, 30);
        DungeonGridDeriver.Derived b = DungeonGridDeriver.Derive(
            mapB, DungeonGridDeriver.MaxTrunkSegments, new RngProvider(555), 30, 30);
        CollectionAssert.AreEqual(a.Grid.ToRows().ToArray(), b.Grid.ToRows().ToArray(),
            "同种子 ⇒ 撒布结果逐行相同 ✓");
        Assert.AreEqual(a.SecretTiles, b.SecretTiles, "隐藏房格数也相同 ✓");
    }

    [TestMethod]
    public void Scatter_EachRoll_IsLogged()
    {
        // 🔴 红线：**每次掷骰必写 `RngDraw`**（不许"撒了但无从复现"）✓
        var log = new CombatLog();
        DungeonGridDeriver.Derive(NewMap(4242), DungeonGridDeriver.MaxTrunkSegments, new RngProvider(7), 0, 100, log);
        int draws = log.Events.OfType<RngDraw>().Count();
        Assert.IsTrue(draws > 0, "撒隐藏房必须逐格写 `RngDraw` ✓");
    }

    [TestMethod]
    public void Scatter_ChanceWithoutRng_ThrowsInsteadOfSilentlySkipping()
    {
        // 🔴 静默半生效是**禁止**的：想撒却不给随机源 ⇒ 抛错（与 D-4 同款防护）⚠️
        Assert.ThrowsException<InvalidOperationException>(() =>
            DungeonGridDeriver.Derive(NewMap(4242), DungeonGridDeriver.MaxTrunkSegments, null, 0, 25),
            "`secretChancePercent > 0` 但 `rng == null` ⇒ 必须抛错（不许静默不撒）✓");
    }

    [TestMethod]
    public void Scatter_BothDisabled_WritesNoRngDrawAtAll()
    {
        // 🔴 零随机不留痕：两类都关 ⇒ 日志里**一条 `RngDraw` 都没有**（既有调用点行为逐字不变）✓
        var log = new CombatLog();
        DungeonGridDeriver.Derive(NewMap(4242), DungeonGridDeriver.MaxTrunkSegments, new RngProvider(7), 0, 0, log);
        Assert.AreEqual(0, log.Events.OfType<RngDraw>().Count(), "关闭 ⇒ 不掷不写 ✓");
    }

    [TestMethod]
    public void Derived_StillReachable_AndGoalUnique_WithSecretsPlaced()
    {
        // 🔴 隐藏房**可通行** ⇒ 撒了也不会把图搞成不可通关（P30 ③ 的核心保证）✓
        ExpeditionMap map = NewMap(20260915);
        DungeonGridDeriver.Derived d = DungeonGridDeriver.Derive(
            map, DungeonGridDeriver.MaxTrunkSegments, new RngProvider(2024), 40, 40);
        var walker = new DungeonWalker(d.Grid, d.Start);
        Assert.IsTrue(walker.PathTo(d.Grid.Goal.X, d.Grid.Goal.Y).Count > 0,
            "撒了陷阱/隐藏房之后，`start` 仍必须能走到 `goal` ✓");
        Assert.AreEqual(1, d.Grid.ToRows().Sum(r => r.Count(c => c == 'G')), "终点仍恰有一处 ✓");
    }

    // ── ③ 加载期 fail-fast（`tuning.dungeon_layer.secrets`）─────────────────────
    //
    // 🔴 校验入口是 `TuningConfig.Parse`（`Validate` 是 `private`，**故意**不对外 ——
    //    校验只能在加载期发生，不许调用方"先构造再手动验"）⇒ 本批**改 JSON 再 Parse** ✓

    /// <summary>把出货 `tuning.json` 的 `dungeon_layer.secrets` 段**替换**成给定值后重新 `Parse` ✓</summary>
    private static string TuningJsonWithSecrets(double chance, int rewardGold, int quota = 0)
    {
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(ReadData("tuning.json"));
        var root = new Dictionary<string, object?>();
        foreach (System.Text.Json.JsonProperty p in doc.RootElement.EnumerateObject())
        {
            root[p.Name] = System.Text.Json.JsonSerializer.Deserialize<object>(p.Value.GetRawText());
        }

        var layer = (Dictionary<string, object?>)System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(
            doc.RootElement.GetProperty("dungeon_layer").GetRawText())!;
        layer["secrets"] = new Dictionary<string, object?>
        {
            ["corridor_chance_percent"] = chance,
            ["reward_gold"] = rewardGold,
            ["max_rewards_per_run"] = quota,
            ["note"] = "test",
        };
        root["dungeon_layer"] = layer;
        return System.Text.Json.JsonSerializer.Serialize(root);
    }

    [TestMethod]
    public void Secrets_RewardGoldZero_IsRejected_NotSilentlyEmpty()
    {
        // 🔴🔴 **`D-6` 的核心设计判据**：隐藏房 = 给侦察的**非信息类**回报。
        //    回报为 0 ⇒ "揭示了但没东西" ⇒ 玩家**理性地永不侦察** ⇒ 整条链路沦为装饰 ⚠️
        //    ⇒ 加载期当场拒绝（`#307`：不设静默默认）✓
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => TuningConfig.Parse(TuningJsonWithSecrets(12, 0)),
            "`reward_gold = 0` 必须被拒（否则侦察变成纯亏）✓");
        StringAssert.Contains(ex.Message, "非信息类");
    }

    [TestMethod]
    public void Secrets_ChanceOutOfRange_IsRejected()
    {
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(TuningJsonWithSecrets(101, 100)),
            "概率 > 100 ⇒ 拒绝 ✓");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(TuningJsonWithSecrets(-1, 100)),
            "概率 < 0 ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void Secrets_NegativeQuota_IsRejected_ButZeroMeansUnlimited()
    {
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(TuningJsonWithSecrets(12, 100, -1)),
            "配额 < 0 ⇒ 拒绝 ✓");
        TuningConfig ok = TuningConfig.Parse(TuningJsonWithSecrets(12, 100, 0)); // 0 = 不限 ⇒ **合法**（不抛）✓
        Assert.AreEqual(0, ok.DungeonLayer!.Secrets!.MaxRewardsPerRun);
    }

    [TestMethod]
    public void Secrets_NotConfigured_IsInert_NotAnError()
    {
        // 🔴 opt-in：未配置 `secrets` 是**合法状态**（= 机制关闭），不是漏配 ⇒ 不得抛 ✓
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(ReadData("tuning.json"));
        var layer = (Dictionary<string, object?>)System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(
            doc.RootElement.GetProperty("dungeon_layer").GetRawText())!;
        layer.Remove("secrets");
        var root = new Dictionary<string, object?>();
        foreach (System.Text.Json.JsonProperty p in doc.RootElement.EnumerateObject())
        {
            root[p.Name] = System.Text.Json.JsonSerializer.Deserialize<object>(p.Value.GetRawText());
        }

        root["dungeon_layer"] = layer;
        TuningConfig t = TuningConfig.Parse(System.Text.Json.JsonSerializer.Serialize(root));
        Assert.IsNull(t.DungeonLayer?.Secrets, "整段删掉 ⇒ 机制关闭（合法）✓");
    }

    [TestMethod]
    public void ShippedTuning_HasSecretsConfiguredWithPositiveReward()
    {
        // 🔴 出货数据必须**真的开着**（否则 D-6 在默认配置下静默不存在）✓
        TuningConfig t = TuningConfig.Parse(ReadData("tuning.json")); // Parse 内部已跑 Validate（不抛即通过）✓
        Assert.IsNotNull(t.DungeonLayer?.Secrets, "出货 `tuning.json` 应带 `dungeon_layer.secrets` ✓");
        Assert.IsTrue(t.DungeonLayer!.Secrets!.RewardGold > 0, "回报必须 > 0（给侦察一个真回报）✓");
    }

    /// <summary>把配置对象编成 `dungeon_grid.json` 形状的 JSON（只为喂 `Parse` 的字符集校验）✓</summary>
    private static string Json(DungeonGridConfig cfg) => System.Text.Json.JsonSerializer.Serialize(new
    {
        width = cfg.Width,
        height = cfg.Height,
        tiles = cfg.Tiles,
        start = new { x = cfg.Start.X, y = cfg.Start.Y },
        goal = new { x = cfg.Goal.X, y = cfg.Goal.Y },
    });
}
