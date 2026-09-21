using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **`#316`② / P26：`curio_pool` 的两种写法** —— **id 数组** 或 **`$pool:&lt;name&gt;`**（**引用**，不是拷贝）。
/// 本片**只让校验通过、不实现解析**（解析留给"具名池"那一轮）⇒ 用例锁三件事：
/// ① `$pool:` 引用的池**必须存在**（池定义放 `curios.json` 的 `pools` 段，**不新增文件**）；
/// ② 池成员必须是真实 Curio id；③ **运行期遇到 `$pool:` 必须显式留痕地跳过**（不静默当成 id）✓
/// </summary>
[TestClass]
public sealed class CurioPoolReferenceTests
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

    private static CuriosConfig ShippedCurios() => CuriosConfig.Parse(ReadData("curios.json"));

    [TestMethod]
    public void P26_RejectsPoolReferenceWhenPoolDoesNotExist()
    {
        const string contents = """
        { "config": { "version": 1 },
          "rooms": { "event": [ { "weight": 1, "encounter": null, "curio_pool": ["$pool:common"] } ] } }
        """;
        Assert.ThrowsException<InvalidDataException>(
            () => RoomContentsConfig.Parse(contents, ShippedCurios()),
            "引用了不存在的具名池 ⇒ 启动即报错（P26 ④：引用必须存在）");
    }

    [TestMethod]
    public void P26_AcceptsPoolReference_WhenPoolIsDefinedInCuriosJson()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json")
            .Replace("\"curios\": [", "\"pools\": { \"common\": [\"cur_supply_crate\", \"cur_sconce\"] }, \"curios\": ["));
        Assert.IsTrue(curios.HasPool("common"), "池定义在 curios.json 的 pools 段（不新增文件）✓");

        const string contents = """
        { "config": { "version": 1 },
          "rooms": { "event": [ { "weight": 1, "encounter": null, "curio_pool": ["$pool:common", "cur_altar"] } ] } }
        """;
        RoomContentsConfig cfg = RoomContentsConfig.Parse(contents, curios); // 不抛 = 校验通过
        Assert.IsNotNull(cfg.ForType("event"));
    }

    [TestMethod]
    public void CuriosConfig_RejectsPoolMemberThatIsNotACurio()
    {
        const string bad = """
        { "pools": { "common": ["no_such_curio"] },
          "curios": [ { "id": "x", "name": "x", "type": "curio", "curio_type": "T",
            "bare_hands": [ { "chance": 100, "kind": "none", "amount": 0, "text": "t" } ],
            "item_results": [] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(() => CuriosConfig.Parse(bad),
            "池成员必须是真实 Curio id（引用而非拷贝）");
    }

    [TestMethod]
    public void Runtime_SkipsUnresolvedPool_WithExplicitTrace_NotSilently()
    {
        // 运行期：内容表里若真出现 `$pool:` ⇒ **显式留痕地跳过**（解析未实现 ⇒ 不能偷偷当成 id）
        CuriosConfig curios = ShippedCurios();
        const string contents = """
        { "config": { "version": 1 },
          "rooms": { "event": [ { "weight": 1, "encounter": null, "curio_pool": ["$pool:common"] } ] } }
        """;

        // 先造一个"池存在"的 curios 版本，好让 P26 通过
        CuriosConfig withPool = CuriosConfig.Parse(ReadData("curios.json")
            .Replace("\"curios\": [", "\"pools\": { \"common\": [\"cur_supply_crate\"] }, \"curios\": ["));
        RoomContentsConfig cfg = RoomContentsConfig.Parse(contents, withPool);

        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var log = new CombatLog();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l), tuning.Expedition.NBattles,
            firewood: 1, food: 1, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log,
            new Darkest.Core.Rng.RngProvider(20260909));

        string? picked = flow.PickCurioForRoom(cfg, "event", isBranch: false, allowedCurios: null);
        Assert.IsNull(picked, "池引用未实现解析 ⇒ 没有候选 ⇒ null（**不静默当成一个 id**）");
        Assert.IsTrue(log.Events.OfType<EffectEvent>()
                .Any(e => e.EffectType.StartsWith("curio_pool_unresolved:", StringComparison.Ordinal)),
            "🔴 必须**显式留痕**（`curio_pool_unresolved:$pool:common`）—— 不是静默跳过（红线 21）");
        _ = curios;
    }
}
