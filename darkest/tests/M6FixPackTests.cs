using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// P3 · M6-08 v2 复测（fix-pack 收口）：300 场、数据零改动，输出判据 A/B + 七项 KPI +
/// **口径健康度诊断**（敌方目标分布：1 号位占比 &lt; 50% 且 3/4 号位非零；移动使用率显著下降）。
/// </summary>
[TestClass]
public sealed class M6FixPackTests
{
    private static string FindDataPath(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    /// <summary>我方实例 id → 起始槽（敌方命中分布诊断用；P2 后移动使用率极低，起始槽近似当前槽）。</summary>
    private static Dictionary<string, int> PlayerSlots()
    {
        FormationConfig cfg = FormationConfig.Parse(File.ReadAllText(FindDataPath("formation.json")));
        var map = new Dictionary<string, int>();
        var seen = new Dictionary<string, int>();
        foreach (RosterEntryConfig e in cfg.InitialRoster.Player)
        {
            int n = seen.TryGetValue(e.Unit, out int c) ? c + 1 : 1;
            seen[e.Unit] = n;
            map[n == 1 ? e.Unit : $"{e.Unit}_{n}"] = e.Slot;
        }

        return map;
    }

    private static bool IsEnemy(UnitId id)
        => id.Value.StartsWith("melee_soldier", StringComparison.Ordinal)
           || id.Value.StartsWith("ranged_archer", StringComparison.Ordinal)
           || id.Value.StartsWith("caster", StringComparison.Ordinal);

    private static readonly string[] PlayerArchetypes = { "tank", "warrior", "medic", "commissar" };

    [TestMethod]
    public void ReMeasure_v2_JudgementAB_Kpi_And_CaliberHealth()
    {
        const int runs = 300;
        const long seedBase = 20260909;
        Dictionary<string, int> slots = PlayerSlots();

        var results = new Dictionary<GameResult, int>();
        var enemyHitsBySlot = new Dictionary<int, int>();
        var skillUses = new Dictionary<string, int>();
        int totalRounds = 0, collapse = 0, weak = 0, dd = 0, retreat = 0, virtue = 0, aff = 0, disp = 0, swaps = 0;
        int totalHits = 0;
        int playerDamageToEnemy = 0;
        var skillUseById = new Dictionary<string, int>();

        for (int i = 0; i < runs; i++)
        {
            (GameOutcome o, CombatLog log) = HeadlessDriver.Run(seedBase + i, PolicyKind.SemiRandom);
            results[o.Result] = results.GetValueOrDefault(o.Result) + 1;
            totalRounds += o.Rounds;
            collapse += o.CollapseCount;
            weak += o.WeakCount;
            dd += o.DeathDoorRolls;
            virtue += o.VirtueCount;
            aff += o.AfflictionCount;
            disp += o.Displacements;
            foreach ((string k, int v) in o.SkillUses)
            {
                skillUses[k] = skillUses.GetValueOrDefault(k) + v;
            }

            foreach (DamageEvent e in log.Events.OfType<DamageEvent>())
            {
                if (e.Attacker is { } a && IsEnemy(a) && e.Target is { } t && slots.TryGetValue(t.Value, out int slot))
                {
                    enemyHitsBySlot[slot] = enemyHitsBySlot.GetValueOrDefault(slot) + 1;
                    totalHits++;
                }
            }

            retreat += log.Events.OfType<RetreatEvent>().Count();
            swaps += log.Events.OfType<SwapEvent>().Count();

            // P3 v3 新增报告要素：D（我方每回合对敌总伤害，用于 #196/#198 的 M）与技能使用率（G0 SkillUseEvent）
            foreach (CombatLog l in new[] { log })
            {
                playerDamageToEnemy += l.Events.OfType<DamageEvent>()
                    .Where(e => e.Attacker is { } at && PlayerArchetypes.Any(p => at.Value == p || at.Value.StartsWith(p + "_", StringComparison.Ordinal)))
                    .Sum(e => e.Amount);
                foreach (SkillUseEvent su in l.Events.OfType<SkillUseEvent>())
                {
                    skillUseById[su.SkillId] = skillUseById.GetValueOrDefault(su.SkillId) + 1;
                }
            }
        }

        int victory = results.GetValueOrDefault(GameResult.PlayerVictory);
        int defeat = results.GetValueOrDefault(GameResult.EnemyVictory);
        int retreatWin = results.GetValueOrDefault(GameResult.DrawRetreat);
        int limit = results.GetValueOrDefault(GameResult.RoundLimit);
        double win = (double)(victory + retreatWin) / runs;
        double avg = (double)totalRounds / runs;
        int moveUses = skillUses.Where(kv => kv.Key == "move").Sum(kv => kv.Value); // F1：池外通用 move
        int allUses = skillUses.Values.Sum();

        // P3 v3：实测 D（我方每回合对敌伤害）+ 技能使用率（技能使用 event 口径）
        double measuredD = totalRounds == 0 ? 0 : (double)playerDamageToEnemy / totalRounds;
        int skillUseEvents = skillUseById.Values.Sum();
        string topSkills = string.Join("、", skillUseById.OrderByDescending(kv => kv.Value).Take(5)
            .Select(kv => $"{kv.Key} {(skillUseEvents == 0 ? 0 : 100.0 * kv.Value / skillUseEvents):F0}%"));
        string usageReport = $"[M6v3] 实测 D={measuredD:F2}/回合　技能使用事件={skillUseEvents}（Top5：{topSkills}）　池外移动占比={(skillUseEvents == 0 ? 0 : 100.0 * moveUses / Math.Max(1, skillUseEvents)):F1}%";
        Console.WriteLine(usageReport);
        TestContext.WriteLine(usageReport);

        string dist = string.Join(" ", Enumerable.Range(1, 6)
            .Select(s =>
            {
                int h = enemyHitsBySlot.GetValueOrDefault(s);
                return $"{s}位 {(totalHits == 0 ? 0 : 100.0 * h / totalHits):F0}%({h})";
            }));
        double slot1Share = totalHits == 0 ? 0 : 100.0 * enemyHitsBySlot.GetValueOrDefault(1) / totalHits;

        string report =
            $"[M6v2] runs={runs} win={win:P0} avgRounds={avg:F2} | 终局 敌灭{victory} 我灭{defeat} 撤退成功{retreatWin} 强切{limit}\n" +
            $"[M6v2] KPI 触底{collapse} 虚弱{weak} 死门{dd} 撤退{retreat} 美德{virtue} 折磨{aff} 位移{disp}\n" +
            $"[M6v2] 口径：敌方命中分布 {dist} | 1位占比 {slot1Share:F0}% | 移动使用 {moveUses}/{allUses} | 增援事件 {swaps}";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        // 判据 A/B：本包只出结论（判定闸在 M6Acceptance）。复测结果：偏难（敌灭 0 / 我灭 234）→ P4.2 回补玩家输出待拍板
        string aState = win is >= 0.40 and <= 0.70 ? "达标" : "未达 40~70%";
        string bState = avg is >= 6 and <= 18 ? "达标" : "未达 6~18";
        Console.WriteLine($"[M6v2] 判据A 胜率 {win:P0}（{aState}）｜判据B 平均回合 {avg:F2}（{bState}）");

        // 口径健康度（P1 是否真生效）
        Assert.IsTrue(slot1Share < 50, $"口径健康度：我方 1 位被命中占比 {slot1Share:F0}% ≥ 50%（P1 未真正生效）。{report}");
        Assert.IsTrue(enemyHitsBySlot.GetValueOrDefault(3) + enemyHitsBySlot.GetValueOrDefault(4) > 0,
            $"口径健康度：3/4 号位被命中为 0（敌方仍只打前排）。{report}");
        Assert.IsTrue(moveUses * 10 < allUses, $"口径健康度：移动使用 {moveUses}/{allUses} 未显著下降。{report}");
    }

    public TestContext TestContext { get; set; } = null!;
}