using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>路径中的一步：恰 2 个可选项（E1）。</summary>
public sealed record PathStep(int Index, IReadOnlyList<PathOption> Options);

/// <summary>一个可选项：节点 id 与类型（battle / event）。</summary>
public sealed record PathOption(string NodeId, string NodeType);

/// <summary>
/// E1 选路规划（#240 / O-66）：线性 N 步、**每步恰 2 个可选项**（事件 / 普通战）。
/// 随机只在【生成候选】时发生且**必写 `RngDraw`**（确定性红线）；同 seed → 同节点序列。
/// 选路结果只写事件（`PathChosenEvent`），**不持有状态**（状态归 `ExpeditionSession`）。
/// </summary>
public static class ExpeditionPathPlanner
{
    /// <summary>生成节点序列：`steps` 步，每步 2 选 1（事件池抽取写 `RngDraw` + 固定普通战）。</summary>
    public static IReadOnlyList<PathStep> GeneratePath(CombatLog log, IRngProvider rng, int steps,
        ExpeditionNodesConfig nodes)
    {
        if (nodes is null)
        {
            throw new ArgumentNullException(nameof(nodes));
        }

        string[] eventIds = nodes.Nodes.Where(n => n.Type == "event").Select(n => n.Id).ToArray();
        if (eventIds.Length == 0)
        {
            throw new InvalidDataException("expedition_nodes.json 至少需要 1 个事件节点（E4）。");
        }

        var path = new List<PathStep>();
        for (int i = 0; i < Math.Max(1, steps); i++)
        {
            int idx = rng.NextInt(0, eventIds.Length);
            log.Append(new RngDraw(rng.DrawCount, idx)); // 确定性红线：候选抽取必写
            path.Add(new PathStep(i, new List<PathOption>
            {
                new(eventIds[idx], "event"),
                new("battle_standard", "battle"),
            }));
        }

        return path;
    }

    /// <summary>选择本步的节点（0/1）：写 `PathChosenEvent(from, to, nodeType)`；越界即拒（阶段一恰 2 选）。</summary>
    public static PathOption ChoosePath(CombatLog log, PathStep step, int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= step.Options.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(optionIndex), "阶段一每步恰 2 个可选项（P20 ⑤）。");
        }

        PathOption chosen = step.Options[optionIndex];
        log.Append(new PathChosenEvent(step.Index, step.Index + 1, chosen.NodeType));
        return chosen;
    }
}
