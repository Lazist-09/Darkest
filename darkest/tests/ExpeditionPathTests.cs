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
/// E1（选路）+ P20 ⑤（远征层节点一致性）：**每步恰 2 个可选项**、选路写 `RngDraw`、
/// 事件节点**恰 2 选项**、`elite` 出现即报错、同 seed 同序列。
/// </summary>
[TestClass]
public sealed class ExpeditionPathTests
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

    [TestMethod]
    public void P20_EventNodes_HaveExactlyTwoOptions_AndNoElite()
    {
        ExpeditionNodesConfig cfg = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        Assert.IsTrue(cfg.Nodes.Count > 0);
        foreach (ExpeditionNodeConfig n in cfg.Nodes.Where(x => x.Type == "event"))
        {
            Assert.AreEqual(2, n.Options.Count, $"事件节点 \"{n.Id}\" 必须恰 2 个选项（P20 ⑤）");
        }

        Assert.IsFalse(cfg.Nodes.Any(n => n.Type == "elite"), "阶段一不得出现 elite（P20 ⑤）");
    }

    [TestMethod]
    public void P20_EliteNode_ThrowsOnLoad()
    {
        const string bad = "{ \"nodes\": [ { \"id\": \"elite_probe\", \"type\": \"elite\", \"name\": \"精英\", \"options\": [] } ] }";
        Assert.ThrowsException<InvalidDataException>(() => ExpeditionNodesConfig.Parse(bad),
            "elite 提前启用 → 加载即报错（P20 ⑤）");
    }

    [TestMethod]
    public void P20_EventNodeWithOneOption_ThrowsOnLoad()
    {
        const string bad = "{ \"nodes\": [ { \"id\": \"ev_one\", \"type\": \"event\", \"name\": \"单选项\", " +
                           "\"options\": [ { \"choice\": \"a\", \"label\": \"A\", \"effect\": { \"morale\": 1 } } ] } ] }";
        Assert.ThrowsException<InvalidDataException>(() => ExpeditionNodesConfig.Parse(bad),
            "事件节点非二选一 → 加载即报错（P20 ⑤：二选一强制）");
    }

    [TestMethod]
    public void PathGeneration_EachStepHasTwoOptions_WritesRngDraw_AndIsDeterministic()
    {
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var logA = new CombatLog();
        var logB = new CombatLog();

        IReadOnlyList<PathStep> pa = ExpeditionPathPlanner.GeneratePath(logA, new RngProvider(20260909), 6, nodes);
        IReadOnlyList<PathStep> pb = ExpeditionPathPlanner.GeneratePath(logB, new RngProvider(20260909), 6, nodes);

        Assert.AreEqual(6, pa.Count, "线性 6 步（N = expedition.n_battles）");
        Assert.IsTrue(pa.All(s => s.Options.Count == 2), "每步恰 2 个可选项（E1 验收）");
        Assert.IsTrue(logA.Events.OfType<RngDraw>().Count() >= 6, "选路生成必写 RngDraw（确定性红线）");
        CollectionAssert.AreEqual(
            pa.Select(s => string.Join(",", s.Options.Select(o => o.NodeId))).ToArray(),
            pb.Select(s => string.Join(",", s.Options.Select(o => o.NodeId))).ToArray(),
            "同 seed → 同节点序列");
    }

    [TestMethod]
    public void ChoosePath_WritesPathChosenEvent_WithNodeType()
    {
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        IReadOnlyList<PathStep> path = ExpeditionPathPlanner.GeneratePath(log, new RngProvider(7), 6, nodes);

        PathOption chosen = ExpeditionPathPlanner.ChoosePath(log, path[0], optionIndex: 0);
        PathChosenEvent e = log.Events.OfType<PathChosenEvent>().Single();
        Assert.AreEqual(0, e.From);
        Assert.AreEqual(1, e.To, "to = 步序号 + 1");
        Assert.AreEqual(chosen.NodeType, e.NodeType);
        Assert.IsTrue(chosen.NodeType is "battle" or "event", "阶段一仅 battle/event");
    }

    [TestMethod]
    public void ChoosePath_OutOfRange_Throws()
    {
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        IReadOnlyList<PathStep> path = ExpeditionPathPlanner.GeneratePath(log, new RngProvider(1), 6, nodes);
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => ExpeditionPathPlanner.ChoosePath(log, path[0], optionIndex: 2),
            "阶段一每步恰 2 个可选项，越界即拒（P20 ⑤）");
    }
}
