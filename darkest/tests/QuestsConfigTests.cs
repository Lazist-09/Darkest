using System;
using System.IO;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M14 · 任务表落库 + P 校验**（一手实测：30 plot_quests ／ 45 goals ／ 6 types ／ 阈值表 [2,2,3,4,5,null,null]）
/// 只测两件必要性：① 落库读数与实测一致（含上限语义 = **上限表**）② 哨兵 99 ／ 悬空 goal 引用 ⇒ fail-fast ✓
/// </summary>
[TestClass]
public sealed class QuestsConfigTests
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
    public void LandedQuests_MatchTheMeasuredNumbers()
    {
        QuestsConfig cfg = QuestsConfig.Parse(ReadData("quests.json"));

        Assert.AreEqual(30, cfg.PlotQuests.Count, "plot_quests = 30 ✓");
        Assert.AreEqual(45, cfg.Goals.Count, "goals = 45 ✓");
        Assert.AreEqual(6, cfg.Types.Count, "types = 6 ✓");
        Assert.AreEqual(7, cfg.PlotQuestsFor("crypts").Count, "crypts = 7 条 ✓");
        Assert.AreEqual(0, cfg.PlotQuestsFor("nowhere").Count, "未知地牢 ⇒ 0 条（不崩）✓");

        // 上限语义 = 「该难度允许的**最高** resolve level」；null = 无上限（原版 99 哨兵已归一）✓
        Assert.AreEqual(2, cfg.ResolveLevelCapFor(1), "难度 1 ⇒ 上限 2 ✓");
        Assert.AreEqual(5, cfg.ResolveLevelCapFor(4), "难度 4 ⇒ 上限 5 ✓");
        Assert.IsNull(cfg.ResolveLevelCapFor(5), "难度 5 ⇒ 无上限 ✓");
        Assert.IsNull(cfg.ResolveLevelCapFor(99), "越界 ⇒ null（不崩）✓");

        // 样例：首条教程任务（一手 plot_tutorial_crypts · explore · length 1 · 槽位 0/1）✓
        PlotQuestConfig first = cfg.PlotQuests[0];
        Assert.AreEqual("plot_tutorial_crypts", first.Id);
        Assert.AreEqual(1, first.Quest.Length);
        Assert.AreEqual(2, first.Quest.CompletionReward.ItemsDefinition.Items.Count, "奖励槽位 = 2 条（0/1）✓");
        Assert.IsNotNull(cfg.GoalById("explore_all_rooms"), "goal 引用可解析 ✓");
        Assert.IsNull(cfg.GoalById("zzz"), "未知 goal ⇒ null ✓");

        Console.WriteLine($"[M14] 落库验收：{cfg.PlotQuests.Count} 任务 · {cfg.Goals.Count} 目标 · {cfg.Types.Count} 类型 · crypts 7 条 ✓");
        TestContext.WriteLine("[M14] 30/45/6 + 上限语义 + 样例字段通过 ✓");
    }

    [TestMethod]
    public void PChecks_RejectSentinel99_AndDanglingGoalReference()
    {
        string json = ReadData("quests.json");

        // ① 原版「无上限」哨兵 99 必须已归一为 null ⇒ 出现即红 ✓
        var sentinel = Assert.ThrowsException<InvalidDataException>(
            () => QuestsConfig.Parse(json.Replace("\"resolve_level_threshold_table\": [", "\"resolve_level_threshold_table\": [99, ")));
        StringAssert.Contains(sentinel.Message, "P 校验①");

        // ② plot_quests 里的 goal 引用悬空 ⇒ 红（只改引用处，不动 goals[].id 定义）✓
        const string goal = "\"explore_all_rooms\"";
        int at = json.IndexOf(goal, json.IndexOf("\"plot_quests\"", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.IsTrue(at > 0, "样例引用应存在于 plot_quests 段内");
        string dangling = json[..at] + "\"zzz_missing_goal\"" + json[(at + goal.Length)..];
        var miss = Assert.ThrowsException<InvalidDataException>(() => QuestsConfig.Parse(dangling));
        StringAssert.Contains(miss.Message, "不存在");

        Console.WriteLine($"[M14] 两条 P 校验都拦得住：{sentinel.Message.Split('：')[^1]} ／ {miss.Message.Split('：')[^1]} ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
