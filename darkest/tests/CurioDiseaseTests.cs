using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Survival;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **Curio 骸骨堆的 `disease_one`（`curio.md` §3 #6）** —— 从"阶段二"**转正**后锁行为：
/// 契约：空手 20% ⇒ **一人患病**。实现走既有 `Roster.Infect`（与回城患病**同一通道**，不另造）。
/// 🔴 受害者是**随机**的 ⇒ **必须写 `RngDraw`**（红线：随机留痕，可审计）。
/// </summary>
[TestClass]
public sealed class CurioDiseaseTests
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
    public void BonePile_BareHands_CanInfectExactlyOneHero_AndWritesRngDraw()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        SanitariumConfig sani = SanitariumConfig.Parse(ReadData("sanitarium.json"));
        CurioConfig bones = curios.Get("cur_bone_pile")!;
        RosterConfig rosterCfg = RosterConfig.Parse(ReadData("roster.json"));

        int found = 0;
        for (int seed = 1; seed <= 200 && found == 0; seed++)
        {
            var log = new CombatLog();
            var bag = new Inventory(tuning.Inventory!);
            bag.ConfigureRecommended(out _);
            bag.LockForRun();
            var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
                tuning.Expedition.NBattles, firewood: 1, food: 1, tuning.Expedition.AmbushChance);
            var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
                new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log,
                new Darkest.Core.Rng.RngProvider(seed));
            var roster = new Roster(rosterCfg);

            // 空手（骸骨堆的 20% 分支 = `disease_one`）
            CurioOutcome outcome = flow.ResolveCurio(bones, itemUsed: null, roster, sani)!;
            if (outcome.Kind != "disease_one")
            {
                continue; // 这个 seed 没掷到患病分支 ⇒ 换一个 seed（确定性可复现）
            }

            found = seed;

            // ② 患病**走既有通道**（`Roster.Infect`）⇒ 名册状态真的变了（状态断言，不只看事件）
            int sick = rosterCfg.Heroes.Count(h => roster.DiseasesOf(h.Id).Count > 0);
            Assert.AreEqual(1, sick, $"🔴 契约：一人患病（seed={seed}）；实际 {sick} 人");

            // ③ 🔴 **随机受害者必须写 `RngDraw`**（可审计、可复现）
            Assert.IsTrue(log.Events.OfType<RngDraw>().Any(),
                "随机选受害者 ⇒ 必须写 RngDraw（红线：随机留痕）");

            // ④ 结果带**描述文本**（V6）
            Assert.IsFalse(string.IsNullOrWhiteSpace(outcome.Text));
            Assert.IsFalse(outcome.Deferred, "disease_one 已转正 ⇒ 不该再标【阶段二】");
        }

        Assert.AreNotEqual(0, found,
            "200 个 seed 里应至少有一个掷到骸骨堆的 20% 患病分支（否则说明分支不可达 —— 也是问题）");
        Console.WriteLine($"🔴 骸骨堆患病分支命中 seed = {found}（用例确定性复现）");
    }
}
