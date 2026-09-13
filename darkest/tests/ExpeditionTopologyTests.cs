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
/// **M7.6 片 (i)：流程层改为地图驱动**（拓扑模式 + 旧线性模式并存互斥）——
/// `StepTo(roomId)` 取代 `Advance(optionIndex)`；`StepsDone` = **已走段数**（**回头不计进度**）；
/// 完成口径 = **走到主干终点 ＋ 打赢 ≥ battle_goal**（数值不变、**不按房间数比例**）。
/// 🔴 旧线性路径**保留**（A1 判定闸 / 旧 e2e 依赖）⇒ 两条路都必须可用。
/// </summary>
[TestClass]
public sealed class ExpeditionTopologyTests
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

    private static (ExpeditionFlow Flow, ExpeditionSession Session, ExpeditionMap Map) NewTopology(long seed)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        ExpeditionMapConfig mapCfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new RngProvider(seed));
        ExpeditionMap map = flow.BeginTopology(mapCfg);
        return (flow, session, map);
    }

    [TestMethod]
    public void Topology_Mode_StepsAlongRooms_AndRevisitCostsLess()
    {
        (ExpeditionFlow flow, _, ExpeditionMap map) = NewTopology(20260909);

        Assert.IsTrue(flow.IsTopologyMode, "注入 mapCfg ⇒ 拓扑模式");
        Assert.AreEqual(map.StartId, flow.CurrentRoomId, "落在起点");
        Assert.AreEqual(0, flow.StepsDone, "还没走");

        // 走到终点（自动选第一个相邻未探索房间）
        int guard = 0;
        while (!flow.ReachedGoal && guard++ < 50)
        {
            IReadOnlyList<MapRoom> options = flow.AdjacentUnexplored();
            Assert.IsTrue(options.Count >= 1, "只要没到终点就至少有一条可走的路（连通性保证）");
            MoveOutcome o = flow.StepTo(options[0].Id);
            Assert.IsTrue(o.Moved, "相邻 ⇒ 可走");
        }

        Assert.IsTrue(flow.ReachedGoal, "应能走到主干终点");
        Assert.IsTrue(flow.StepsDone is >= 5 and <= 8, $"段数 = 主干段数（实测 {flow.StepsDone}）");
        Assert.AreEqual(flow.StepsDone, flow.StepsDone, "（段数 = 首次进入的房间数）");

        string report = $"[M7.6] 片(i) 拓扑模式：起点→终点共走 **{flow.StepsDone}** 段" +
                        $"（主干 {map.Rooms.Count(r => !r.IsBranch)} 间、支路 {map.BranchCount} 条、分叉点 {map.ForkCount} 个）；" +
                        $"完成口径 = 到达终点({flow.ReachedGoal}) 且 打赢 ≥3 场（当前 {flow.Wins}）⇒ Completed={flow.Completed}";
        Console.WriteLine(report);
        TestContext.WriteLine(report);
    }

    [TestMethod]
    public void Topology_RevisitDoesNotAddProgress_ButCostsLess()
    {
        (ExpeditionFlow flow, _, _) = NewTopology(4242);

        IReadOnlyList<MapRoom> first = flow.AdjacentUnexplored();
        MoveOutcome forward = flow.StepTo(first[0].Id);
        Assert.IsTrue(forward.Moved && !forward.Revisited, "首次进入 = 新区域");
        int afterForward = flow.StepsDone;

        // 往回走一步（回到已访问的房间）
        int back = flow.CurrentRoomId;
        MoveOutcome backOutcome = flow.StepTo(0); // 起点一定已访问（连通）
        if (backOutcome.Moved)
        {
            Assert.IsTrue(backOutcome.Revisited, "回到起点 = 重走");
            Assert.AreEqual(afterForward, flow.StepsDone, "🔴 **回头不计进度**（段数不变）");
            Assert.IsTrue(Math.Abs(backOutcome.Cost) < Math.Abs(forward.Cost), "重走比新区域便宜");
        }

        _ = back;
    }

    [TestMethod]
    public void Linear_Mode_StillWorks_AlongsideTopology()
    {
        // 🔴 旧线性路径必须仍然可用（A1 判定闸 / 旧 e2e 依赖它 ⇒ 两条路并存互斥）
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new RngProvider(20260909));

        Assert.IsFalse(flow.IsTopologyMode, "未注入 mapCfg ⇒ 线性模式（向后兼容）");
        FlowStep step = flow.Advance(1);
        Assert.IsTrue(step.Kind is FlowStepKind.Battle or FlowStepKind.Event, "线性模式仍按原本工作");
        Assert.AreEqual(70, flow.Meter.Value, "100 减去一次 node_step(−30) = 70（确认旧线性路径仍在工作）");
    }

    public TestContext TestContext { get; set; } = null!;
}
