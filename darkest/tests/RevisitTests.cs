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
/// 🔴🔴 **`D-1` 回头代价 + `D-2` 重访刷新威胁**（`dd_replication_roadmap` D-1/D-2 判据）：
/// ① 回头**必付代价**（重走已站过的格 ⇒ 额外扣光）；
/// ② 反复走同一格会**刷新威胁**，且**全黑更频繁**（档位随光照走）；
/// ③ **每掷骰都有 `RngDraw`**（确定性红线）。
///
/// 关键设计（**已实现、必须锁死**）：
/// · D-1 的"站过"是 `_visited`（**亲自站过**）≠ `_revealed`（**见过**）—— 两者混淆会让"看一眼就付钱" ⚠️
/// · `wasRevisit` 在**移动之前**判定（移动后再判就是"永远已站过" ⇒ 第一次走也扣钱）⚠️
/// · 走廊格**只补差额**（段守恒已为它扣过 `perTile`）⇒ 避免走廊回头双倍扣 ⚠️
/// · 未配置 ⇒ **零副作用**（不扣、不掷、不写日志）—— 既有调用点行为逐字不变 ✓
/// </summary>
[TestClass]
public sealed partial class RevisitTests
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

    /// <summary>最近一次 `NewFlow` 造出的日志（流程不暴露 `_log`，故在此留一份只读引用）✓</summary>
    private static CombatLog _lastLog = new();

    private static ExpeditionFlow NewFlow(long seed = 20260915)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json")),
            tuning, _lastLog = new CombatLog(), new RngProvider(seed));
        flow.BeginTopology(ExpeditionMapConfig.Parse(ReadData("expedition_map.json")));
        return flow;
    }

    /// <summary>
    /// 造一份**最小**回头威胁配置（不碰真实 tuning）：两档，暗 → 亮。
    /// 🔴 **末档必须覆盖到 100**（满光照）—— 否则光照高时一档都不命中 ⇒ 完全不掷，
    ///    而 `TuningConfig.Validate` 已在加载期拒绝这种数据（**我在初版真实数据里踩过这个坑** ⚠️）。
    /// **此处刻意用"两档"**：三档以上在 `TuningConfig` 侧校验，本帮助器只服务于纯函数用例 ✓
    /// </summary>
    private static RevisitThreatConfig Cfg(double darkPercent, double dimPercent) => new(
        new List<RevisitThreatTier>
        {
            new(0, darkPercent),
            new(100, dimPercent),
        },
        BattleWeight: 1, TrapWeight: 1);

    /// <summary>三档版（含 0 / 50 / 100）—— 用来验证「中间档也确实参与选择」✓</summary>
    private static RevisitThreatConfig Cfg3(double black, double mid, double bright) => new(
        new List<RevisitThreatTier> { new(0, black), new(50, mid), new(100, bright) },
        BattleWeight: 1, TrapWeight: 1);

    /// <summary>把随机源钉成"每次必中"或"每次必不中"（`NextPercent` 由 `RngProvider` 给）✓</summary>
    private sealed class FixedRng(double percent, int intValue) : IRngProvider
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
}
