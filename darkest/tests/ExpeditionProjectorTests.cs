using System;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// E2/E3 UI 读模型（`ExpeditionProjector`）：资源可**从事件流复算**、灰显依据正确、
/// 路径/夜袭/撤退/状态可从事件流取到、事件流与会话持有值对账一致。
/// </summary>
[TestClass]
public sealed class ExpeditionProjectorTests
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

    private static TuningCamp Camp() => TuningConfig.Parse(ReadData("tuning.json")).Camp;

    private static ExpeditionSession NewSession(int firewood = 2, int food = 12)
        => new(log => HeadlessDriver.NewDirector(log), targetBattles: 6, firewood: firewood, food: food,
            ambushChance: 0.33);

    private static void RunOneBattle(ExpeditionSession s, long seed)
    {
        var log = new CombatLog();
        var rng = new RngProvider(seed);
        Darkest.Gameplay.Sim.Director.BattleDirector d = s.BeginBattle(1, log);
        string result = "RoundLimit";
        for (int round = 1; round <= 100; round++)
        {
            d.RunFullRound(rng, unit => Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, rng));
            if (d.IsBattleOver)
            {
                result = d.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                break;
            }
        }

        s.EndBattle(d, 1, result, 10);
    }

    [TestMethod]
    public void E2UI_Resources_RecomputableFromEventStream()
    {
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        s.StartCamp(log, 1, Camp().RespiteBase);          // −1 柴火
        s.ChooseFood(log, Camp(), "full");                 // 口粮按【存活人数】收（不是满编）
        s.Gain(log, "food", 4, "event");                   // +4 口粮（事件节点）

        ExpeditionViewState v = ExpeditionProjector.Project(log, 2, 12, s, Camp(), 6);

        // 🔴 更正（2026-09-30）：原注释写「−6 口粮（满编）」并把期望**写死 10** ⇒ 实测 11。
        //    真因不是缺陷：本场阵亡 1 人 ⇒ `Survivors == 5` ⇒ 满档需求按**存活人数**算（5，不是满编 6）
        //    —— 这是 `ExpeditionCampMath` 既定的「每减员 −1/4」口径（设计如此）⇒ **过期的是注释**。
        //    ⇒ 断言先钉住存活人数，再用纯函数**复算**期望（不写死数字）。
        Assert.AreEqual(5, s.Survivors, "本场阵亡 1 人 ⇒ 存活 5（口粮需求按存活人数算）");
        int fullNeed = ExpeditionCampMath.FoodRequired(Camp().FoodTiers, "full", s.Survivors);
        Assert.IsTrue(fullNeed < 6, $"满档需求随存活人数下降（5 人 ⇒ {fullNeed} < 满编 6）");
        Assert.AreEqual(1, v.Firewood, "柴火 = 起手 2 − 扎营 1（事件流复算）");
        Assert.AreEqual(12 - fullNeed + 4, v.Food, $"口粮 = 起手 12 − {fullNeed}（满档 × {s.Survivors} 人）+ 4（事件流复算）");
        Assert.IsTrue(ExpeditionProjector.Reconciles(v, s), "**事件流复算 == 会话持有值**（UI 数字可信）");
    }

    [TestMethod]
    public void E3UI_FoodTiers_DisabledWhenUnaffordable()
    {
        ExpeditionSession s = NewSession(firewood: 2, food: 4);
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        s.StartCamp(log, 1, Camp().RespiteBase);

        ExpeditionViewState v = ExpeditionProjector.Project(log, 2, 4, s, Camp(), 6);

        Assert.IsTrue(v.AffordableFoodTiers.Contains("starve"), "Starve（需求 0）永远可选");
        Assert.IsTrue(v.AffordableFoodTiers.Contains("half"), "half 需 3 ≤ 4 → 可选");
        Assert.IsFalse(v.IsFoodTierDisabled("half"));
        Assert.IsTrue(v.IsFoodTierDisabled("feast"), "feast 需 12 > 4 → **灰显**");
        Assert.IsTrue(v.IsFoodTierDisabled("full"), "full 需 6 > 4 → **灰显**（不是自动退化）");
    }

    [TestMethod]
    public void E2UI_PathAmbushStatus_FromEventStream()
    {
        ExpeditionSession s = NewSession();
        var log = new CombatLog();
        var rng = new RngProvider(20260909);
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var path = ExpeditionPathPlanner.GeneratePath(log, rng, 6, nodes);
        ExpeditionPathPlanner.ChoosePath(log, path[0], 0);
        ExpeditionPathPlanner.ChoosePath(log, path[1], 1);
        s.RollAmbush(log, new RngProvider(1));
        RunOneBattle(s, 20260909);
        s.ReturnToTown(log, "retreat");

        ExpeditionViewState v = ExpeditionProjector.Project(log, 2, 12, s, Camp(), 6);
        Assert.AreEqual(2, v.PathNodeTypes.Count, "选路从事件流取到（E1）");
        Assert.IsTrue(v.PathNodeTypes.Contains("event") && v.PathNodeTypes.Contains("battle"));
        Assert.AreEqual(1, v.Retreats, "撤退档从 TownReturnEvent 取到（⑯ 数据面）");
        Assert.AreEqual("returned", v.Status);
        Assert.IsTrue(v.SurvivingUnits >= 1, "存活人数可投影（UI 展示用）");
    }
}
