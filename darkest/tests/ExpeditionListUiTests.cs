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
/// E2/E3 最小列表式界面（`expedition.md` §5.5 **必显 7 条**）：
/// ① 资源 ② 进度 ③ 6 人 HP/士气 ④ 节点类型+代价 ⑤ 扎营入口（柴火不足灰显） ⑥ 事件二选一 ⑦ 回城结算（士气前后+惩罚）。
/// 🔴 ③ 与 ⑦ 是 M7 硬需求（**跨场累积只能靠显示被玩家感知**）。
/// </summary>
[TestClass]
public sealed class ExpeditionListUiTests
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
    public void UI_List_Shows_All_Seven_Required_Fields()
    {
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        s.StartCamp(log, 1, Camp().RespiteBase);

        ExpeditionViewState v = ExpeditionProjector.Project(log, 2, 12, s, Camp(), 6);
        TownReturnEvent town = new("completed", 44, 44, false);
        var lines = ExpeditionProjector.RenderList(v, s, 6, ambushTriggered: false, Camp(), town);
        string all = string.Join("\n", lines);

        // 必显 7 条（①~⑦ 的行首标记）
        foreach (string marker in new[] { "①", "②", "③", "④", "⑤", "⑥", "⑦" })
        {
            Assert.IsTrue(lines.Any(l => l.StartsWith(marker, StringComparison.Ordinal)), $"必显条目 {marker} 缺失");
        }

        Assert.IsTrue(all.Contains("柴火") && all.Contains("口粮"), "① 资源");
        Assert.IsTrue(all.Contains("/6 场"), "② 进度含总数 6 场");
        Assert.IsTrue(all.Contains("HP") && all.Contains("士气"), "③ 6 人 HP/士气条（M7 硬需求）");
        Assert.IsTrue(all.Contains("灰显") || all.Contains("需 "), "④b 档位代价提示");
        Assert.IsTrue(all.Contains("回城结算") && all.Contains("完全不恢复"), "⑦ 回城结算面板 + #245 语义");
    }

    [TestMethod]
    public void UI_List_CampEntry_Grayed_WhenNoFirewood()
    {
        ExpeditionSession s = NewSession(firewood: 0);
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        ExpeditionViewState v = ExpeditionProjector.Project(log, 0, 12, s, Camp(), 6);

        var lines = ExpeditionProjector.RenderList(v, s, 6, false, Camp());
        Assert.IsTrue(lines.Any(l => l.StartsWith("⑤", StringComparison.Ordinal) && l.Contains("灰显")),
            "⑤ 柴火不足 → 扎营入口**灰显**");
    }

    [TestMethod]
    public void UI_List_Roster_Shows_All_Six_With_Hp_And_Morale()
    {
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        ExpeditionViewState v = ExpeditionProjector.Project(log, 2, 12, s, Camp(), 6);

        var roster = s.Roster();
        Assert.AreEqual(6, roster.Count, "名册 6 人（③）");
        string line = ExpeditionProjector.RenderList(v, s, 6, false, Camp())
            .Single(l => l.StartsWith("③", StringComparison.Ordinal));
        foreach ((string id, int hp, int max, int morale) in roster)
        {
            Assert.IsTrue(line.Contains(id), $"③ 缺 {id}");
            Assert.IsTrue(hp >= 0 && morale is >= 0 and <= 100, $"{id} HP/士气越界：{hp}/{morale}");
        }
    }
}
