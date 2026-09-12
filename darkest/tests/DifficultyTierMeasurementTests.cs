using System;
using System.Collections.Generic;
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
/// ⑳ 分档读数（#252 / O-70 ①：乘数**只作用敌 HP**）。
/// 按 **第 1~2 / 3~4 / 5~6 场**分组、**固定全样本**（按场序分组，非按结果）：
/// 胜率 · 掉血 · 死门 · 阵亡 · **回合数（🔴 ×1.25 的真正闸门）**。
/// </summary>
[TestClass]
public sealed class DifficultyTierMeasurementTests
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
    public void O70_TieredReadings_With_RoundsPerTier()
    {
        const int runs = 120;
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        IReadOnlyList<TuningDifficultyTier> tiers = tuning.Expedition.DifficultyTiers!;

        // 按**场序**（1..6）聚合；每档 = 两场
        var wins = new int[7];
        var battles = new int[7];
        var rounds = new long[7];
        var dd = new int[7];
        var deaths = new int[7];
        var hpAfter = new double[7];
        var hits = new int[7];

        for (int i = 0; i < runs; i++)
        {
            var session = new ExpeditionSession(_ => HeadlessDriver.NewDirector(new CombatLog()), 6,
                firewood: 2, food: 12, ambushChance: 0.33);

            for (int b = 1; b <= 6; b++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + i * 100 + b);
                Darkest.Gameplay.Sim.Director.BattleDirector d = session.BeginExpeditionBattle(b, log, tiers);

                string result = "RoundLimit";
                int r = 1;
                for (; r <= 100; r++)
                {
                    d.RunFullRound(rng, unit => Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, rng));
                    if (d.IsBattleOver)
                    {
                        result = d.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                        break;
                    }
                }

                session.EndBattle(d, b, result, Math.Min(r, 100));

                // 🔴 口径对齐（否则与 E7 不可比）：每场后按真实流程**扎营**（柴火许可 + 择档 + 用尽 Respite）
                if (result == "PlayerVictory" && session.CanCamp && session.StartCamp(log, b, tuning.Camp.RespiteBase))
                {
                    string tier = session.CanAffordFood(tuning.Camp, "feast") ? "feast"
                        : session.CanAffordFood(tuning.Camp, "full") ? "full"
                        : session.CanAffordFood(tuning.Camp, "half") ? "half" : "starve";
                    session.ChooseFood(log, tuning.Camp, tier);
                    while (session.RespiteLeft >= 2)
                    {
                        session.UseCampSkill(log, "camp_warrior_sharpen", 2, UnitId.Of("warrior"));
                    }

                    session.EndCamp(log);
                }

                battles[b]++;
                rounds[b] += Math.Min(r, 100);
                if (result == "PlayerVictory")
                {
                    wins[b]++;
                }

                dd[b] += log.Events.OfType<DeathDoorEvent>().Count();
                deaths[b] += log.Events.OfType<DeathEvent>().Count(e => e.IsPlayer);
                hpAfter[b] += session.Curve[^1].AvgHpPercent;
                hits[b]++;

                if (result != "PlayerVictory")
                {
                    break; // 该趟中止（撤退/全灭）→ 后续场次不计入（固定全样本按"到达该场"计分母）
                }
            }
        }

        var lines = new List<string> { $"[M7] ⑳ 分档读数（{runs} 趟；乘数只作用敌 HP；按场序分组）" };
        foreach ((string label, int from, int to) in new[] { ("第 1~2 场 ×1.0", 1, 2), ("第 3~4 场 ×1.1", 3, 4), ("第 5~6 场 ×1.25", 5, 6) })
        {
            int battlesN = battles.Skip(from).Take(to - from + 1).Sum();
            int winsN = wins.Skip(from).Take(to - from + 1).Sum();
            long roundsN = rounds.Skip(from).Take(to - from + 1).Sum();
            int ddN = dd.Skip(from).Take(to - from + 1).Sum();
            int deathsN = deaths.Skip(from).Take(to - from + 1).Sum();
            double hpN = 0;
            int hitsN = 0;
            for (int b = from; b <= to; b++)
            {
                hpN += hpAfter[b];
                hitsN += hits[b];
            }

            lines.Add($"[M7] ⑳ {label}：样本 {battlesN}　胜率 {(battlesN == 0 ? 0 : 100.0 * winsN / battlesN):F0}%" +
                      $"　**回合 {roundsN / (double)Math.Max(1, battlesN):F2}**" +
                      $"　掉血后 HP {(hitsN == 0 ? 0 : hpN / hitsN):F0}%" +
                      $"　死门 {ddN / (double)Math.Max(1, battlesN):F2}/场　阵亡 {deathsN / (double)Math.Max(1, battlesN):F2}/场");
        }

        string report = string.Join("\n", lines);
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        // 🔴 护栏读数（**先诊断，不硬判**）：第 5~6 场（×1.25）平均回合。
        // 口径说明（重要）：本节按**链式**统计（跨场带伤、含扎营）。A1 的「回合 4~6」是**单场新队**口径
        // （实测 5.36 ✓）。链式口径下连 ×1.0 档都已 6.4 回合 → **该带不能直接搬到链式读数上**；
        // 故此处只输出读数 + 明细，阈值判定待策划给出链式回合带后再挂硬断言。
        int b56 = battles[5] + battles[6];
        double rounds56 = (rounds[5] + rounds[6]) / (double)Math.Max(1, b56);
        Console.WriteLine($"[M7] ⑳ 护栏读数：×1.25 档链式平均回合 {rounds56:F2}" +
                          $"（A1 单场新队口径 5.36；链式 ×1.0 档 {rounds[1] / (double)Math.Max(1, battles[1]):F2}）" +
                          "　→ 若链式也要求 ≤6，则 ×1.25 **破带**，应按备用轴换『敌抗性↑』（不得硬加 HP）");

        Assert.IsTrue(rounds56 > 0, "末档必须有样本");
    }

    public TestContext TestContext { get; set; } = null!;
}
