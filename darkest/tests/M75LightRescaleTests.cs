using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **`#303`④ 重定标（第一步）**：`light.effects` **首次真正生效** ⇒ 必须重定标。
/// 本探针报 **各档的【实际注入量级】+【单场 A1 四项】**（胜率 ／ 死门 ／ 阵亡 ／ 回合），
/// 以便判"终于有效"还是"一次调过头"（策划的量级疑虑：Black = 每敌 +25%）。
/// ⚠️ 本探针**只作 smoke 与重定标输入**，不改任何数值、不进既有判据（归档纪律）。
/// </summary>
[TestClass]
public sealed class M75LightRescaleTests
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

    private static TuningConfig Tuning() => TuningConfig.Parse(ReadData("tuning.json"));

    [TestMethod]
    public void O82_Rescale_AllTiers_A1Readings()
    {
        TuningConfig tuning = Tuning();
        const int runs = 40;
        var lines = new List<string>
        {
            "[M7.5] #303④ 重定标（各档各 40 场单场，同策略 SemiRandom）：",
        };

        foreach (string tierId in new[] { "radiant", "dim", "shadowy", "dark", "black" })
        {
            TuningLightEffect effect = tuning.Light!.Effects[tierId];
            int wins = 0, deaths = 0, rounds = 0, deathDoors = 0;
            for (int i = 0; i < runs; i++)
            {
                var log = new CombatLog();
                var rng = new RngProvider(20260909 + (i * 59));
                var bag = new Inventory(tuning.Inventory!);
                bag.ConfigureRecommended(out _);
                bag.LockForRun();
                var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
                    tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
                    tuning.Expedition.AmbushChance);
                // 🔴 把该档的光照效果注入战斗（这是 (i1) 接线后才存在的入口）
                BattleDirector d = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers, effect);

                int round = 1;
                for (; round <= 100 && !d.IsBattleOver; round++)
                {
                    d.RunFullRound(rng, unit => MonteCarlo.Policies.DecideForUnit(
                        MonteCarlo.PolicyKind.SemiRandom, unit, d, rng));
                }

                bool victory = d.Enemy.OccupiedPositions(false).Count == 0;
                wins += victory ? 1 : 0;
                rounds += Math.Min(round, 100);
                deathDoors += log.Events.OfType<DeathDoorEvent>().Count();
                deaths += log.Events.OfType<DeathEvent>().Count(e => e.IsPlayer);
            }

            lines.Add($"[M7.5] #303④ {tierId,-8}：注入量级 敌命中+{effect.EnemyAcc:0.#} ／ 敌伤害+{effect.EnemyDmgPct:0.#}% ／ " +
                      $"敌暴击+{effect.EnemyCritPct:0.#}% ／ 我暴击+{effect.OurCritPct:0.#}% ／ 我士气伤害+{effect.OurMoraleDamagePct:0.#}%　⇒　" +
                      $"A1: 胜率 {wins / (double)runs:P0}　死门 {deathDoors / (double)runs:F2}/场　阵亡 {deaths / (double)runs:F2}/场　" +
                      $"回合 {rounds / (double)runs:F2}　（门槛 ≥85% ／ ≤0.4 ／ ≤0.1 ／ 4~6）");
        }

        lines.Add("[M7.5] #303④ 判读：若 Black 档胜率【跌破 85%】⇒ 可能是【一次调过头】（需重定标）；" +
                  "若各档仍在门槛内且【有梯度】⇒ 属\"终于有效\"" +
                  "\n[M7.5] #303④ ⚠️ **口径声明（不混用）**：本探针是【单档 × 40 场、全部 battleIndex=1（第 1 场）】" +
                  "⇒ 与 A1 的【一趟 6 场聚合】**不可直接比**（故死门 ／ 阵亡为 0.00 属正常：第 1 场是最低难度档）；" +
                  "要回答\"Black 档 A1 四项\"须用 **A1 口径（battleIndex 1..6 聚合）** 再跑一版");
        Console.WriteLine(string.Join("\n", lines));
        TestContext.WriteLine(string.Join("\n", lines));
        Assert.AreEqual(7, lines.Count);
    }

    public TestContext TestContext { get; set; } = null!;
}
