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
/// 🔴 **片 C 的"分布读数"探针**（`O-82` 的规矩：读数要能证明"确实生效"，且**只报数不判红**）。
///
/// 为什么需要它：片 E 的 V10/A1/A2 读数在 B/C 之后**完全没变** ⇒ 说明**那些探针不消费 Curio**
/// （V10 只是走/打/扎营/提亮）⇒ 所以 B/C 的影响**不在那些读数上** ⇒ 我补这一条**真正覆盖片 C** 的读数：
/// **内容表加权抽取的分布**（1000 次/每类型）与**表里声明的权重**是否吻合。
/// </summary>
[TestClass]
public sealed class RoomContentDistributionProbeTests
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

    private static (ExpeditionFlow Flow, CombatLog Log) NewFlow(int seed)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);
        return (new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log,
            new Darkest.Core.Rng.RngProvider(seed)), log);
    }

    [TestMethod]
    public void Reading_CurioPickDistribution_TracksDeclaredWeights()
    {
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        RoomContentsConfig contents = RoomContentsConfig.Parse(ReadData("room_contents.json"), curios);
        const int n = 1000;

        var lines = new List<string> { $"[片C读数] 内容表加权抽取分布（每类型 {n} 次；只报数，不判红）" };
        foreach (string roomType in new[] { "event", "branch" })
        {
            // 表里声明的期望分布（按行 weight × 行内 id 数 展开）
            var weightById = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (RoomContentEntry row in contents.ForType(roomType))
            {
                foreach (string id in row.CurioPool ?? Array.Empty<string>())
                {
                    weightById[id] = weightById.GetValueOrDefault(id) + row.Weight;
                }
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            (ExpeditionFlow flow, CombatLog log) = NewFlow(20260909);
            for (int i = 0; i < n; i++)
            {
                string? picked = flow.PickCurioForRoom(contents, roomType, isBranch: roomType == "branch");
                if (picked is not null)
                {
                    counts[picked] = counts.GetValueOrDefault(picked) + 1;
                }
            }

            int total = counts.Values.Sum();
            Assert.AreEqual(n, total, $"\"{roomType}\" 应每次都能抽到（表给了池）");
            Assert.AreEqual(n, log.Events.OfType<RngDraw>().Count(), "每次抽取一条 RngDraw（留痕）");

            lines.Add($"　· {roomType}：{string.Join("　", counts.OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key} {kv.Value / (double)n:P0}（权重占比 " +
                              $"{weightById.GetValueOrDefault(kv.Key) / (double)weightById.Values.Sum():P0}）"))}");
        }

        Console.WriteLine(string.Join("\n", lines));
    }
}
