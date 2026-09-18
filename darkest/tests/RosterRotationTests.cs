using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **轮换（④）+ 名册构成读数（⑤ · `m8_verification.md` ㉝）**
///
/// 契约：`roster.cap = 12 = 出征 6 + 替补 6` ⇒ **「轮换休息成为策略」** ——
/// 而实测（我这一轮之前）：`FormationSortie.SelectForTemplate` **只按"槽位原型 + 未用"取人**
/// ⇒ **恒取同一批 6 人** ⇒ 🔴 **轮换在机制上不可能发生** ⚠️
///
/// 本轮给出的**机制**（不发明规则 ✓）：
///   · `chosenIds` 非空 ⇒ **按给定名单取人**（**选人规则由调用方决定**：玩家点选 / 策略 / …）✓
///   · `chosenIds` 缺省 ⇒ **行为与以前完全一致**（既有路径零变化 ✓）
///
/// 本用例同时给两条读数：**轮换频率**（10 趟里出征名单变了几次）＋ **名册构成**（㉝）✓
/// </summary>
[TestClass]
public sealed class RosterRotationTests
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

    private static (RosterConfig Roster, FormationConfig Template) Load()
        => (RosterConfig.Parse(ReadData("roster.json")), FormationConfig.Parse(ReadData("formation.json")));

    /// <summary>默认路径：**行为与以前完全一致**（否则这一轮就动了既有路径 ⚠️）✓</summary>
    [TestMethod]
    public void DefaultPath_IsUnchanged_AndChosenPathRotates()
    {
        (RosterConfig cfg, FormationConfig tpl) = Load();
        IReadOnlyList<HeroConfig> def = FormationSortie.SelectForTemplate(tpl, cfg);

        Assert.AreEqual(tpl.InitialRoster.Player.Count, def.Count, "默认必须填满槽位 ✓");
        Assert.AreEqual(def.Count, def.Select(h => h.Id).Distinct().Count(), "默认不得重复取人 ✓");

        // 🔴 **轮换路径**：把默认那批**换掉**（取名册里**不在默认名单**的人）⇒ 出征名单确实可指定 ✓
        var other = cfg.Heroes.Where(h => def.All(d => d.Id != h.Id)).Select(h => h.Id).ToList();
        if (other.Count >= def.Count)
        {
            IReadOnlyList<HeroConfig> rotated = FormationSortie.SelectForTemplate(tpl, cfg, other.Take(def.Count).ToList());
            CollectionAssert.AreEquivalent(other.Take(def.Count).ToList(), rotated.Select(h => h.Id).ToList(),
                "指定名单 ⇒ 出征的**必须正是**那批人（轮换生效 ✓）");
            Assert.AreNotEqual(string.Join(",", def.Select(h => h.Id)), string.Join(",", rotated.Select(h => h.Id)),
                "轮换后名单应与默认不同 ✓");
        }

        // 🔴 不合法名单**如实炸**（不静默兜底 ✓）
        Assert.ThrowsException<InvalidDataException>(
            () => FormationSortie.SelectForTemplate(tpl, cfg, new[] { "hero_not_exist", def[1].Id, def[2].Id, def[3].Id, def[4].Id, def[5].Id }),
            "名单里有不存在的英雄 ⇒ 必须抛 ✓");
        Assert.ThrowsException<InvalidDataException>(
            () => FormationSortie.SelectForTemplate(tpl, cfg, new[] { def[0].Id, def[0].Id, def[2].Id, def[3].Id, def[4].Id, def[5].Id }),
            "名单里重复 ⇒ 必须抛 ✓");
        Assert.ThrowsException<InvalidDataException>(
            () => FormationSortie.SelectForTemplate(tpl, cfg, new[] { def[0].Id }),
            "名单人数 ≠ 槽位 ⇒ 必须抛 ✓");
    }

    /// <summary>
    /// 📊 **轮换频率 + 名册构成**（㉝）：10 趟，每趟由调用方按**最低士气优先**选人（**用例内策略** ✓ 不是游戏规则）——
    /// 目的只是证明"轮换在机制上**可发生**"，并把**频率**变成可读 ✓
    /// </summary>
    [TestMethod]
    public void RotationFrequency_AndRosterComposition_AreReadable()
    {
        (RosterConfig baseCfg, FormationConfig tpl) = Load();
        RosterConfig cfg = baseCfg with
        {
            Experience = new RosterExperience(XpPerWin: 1, XpPerLoss: 0, LevelCosts: new[] { 2, 3, 4, 5, 6 }),
        };
        var log = new CombatLog();
        var roster = new Roster(cfg);
        int need = tpl.InitialRoster.Player.Count;

        string? prevIds = null;
        int changed = 0;
        var lines = new List<string>();

        for (int run = 1; run <= 10; run++)
        {
            // 调用方策略（用例内）：**士气最低者优先出征**（"累了的休息" ⇒ 这就是轮换的动机 ✓）
            // 🔴 策略必须是"会自我纠正"的那种：**最休息（士气最高）者优先** ⇒ 出征会消耗士气 ⇒ 下次自然换人 ✓
            //    （我第一版写反成"最低者优先"⇒ 出征后他们更低 ⇒ 恒是同一批 ⇒ 实测轮换 0 次 ⚠️ 这就是"轮换需要动机"的实证 ✓）
            var picked = roster.Heroes.OrderByDescending(h => roster.MoraleOf(h.Id)).ThenBy(h => h.Id)
                .Take(need).Select(h => h.Id).ToList();
            IReadOnlyList<HeroConfig> sortie = FormationSortie.SelectForTemplate(tpl, cfg, picked);
            string ids = string.Join(",", sortie.Select(h => h.Id));
            if (prevIds is not null && ids != prevIds)
            {
                changed++;
            }

            prevIds = ids;

            // 一趟：发经验（升级通道 ✓）+ 士气波动（模拟归来 ✓）
            roster.AwardExperienceForBattle(log, win: true, reason: "rotation_probe");
            foreach (var h in sortie)
            {
                roster.ApplyRelief(log, h.Id, -3, "rotation_probe"); // 出征 ⇒ 士气 −3（只读口径：只为让轮换有动机）✓
            }
        }

        string freq = $"[轮换频率] 10 趟里出征名单**变了 {changed} 次**（策略=用例内「最休息者优先」）" +
                      $"　⇒ {(changed > 0 ? "✅ **轮换在机制上可发生**" : "⚠️ 10 趟都没变（策略本身没产生差异）")}";
        string comp = RosterComposition.Describe(roster);
        lines.Add(freq);
        lines.Add(comp);
        lines.Add($"[轮换频率] 口径：**10 趟 = 10 次比较**（第 1 趟无前次可比）⇒ 变 {changed}／9 ✓");

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        Assert.IsTrue(lines.Any(l => l.Contains("[名册构成]")), "名册构成读数必须可打印 ✓");
        Assert.IsTrue(roster.ExperienceOf(roster.Heroes.OrderBy(h => h.Id).First().Id) >= 0, "经验读数可用 ✓");
        Assert.IsTrue(changed > 0, "🔴 给定『最休息者优先』策略，10 趟里**必须发生过轮换**（否则机制没通）✓");
    }

    /// <summary>
    /// 🆕 **把等级分布 / 轮换也接进"本次 vs 上次"**（让 `RunStartSnapshot` 一行同时显示成长与轮换）——
    /// 实测要看的：`等级分布 … → …` 与 `出征名单换人 N 名` 是否**真的出现** ✓
    /// </summary>
    [TestMethod]
    public void SnapshotDiff_ShowsLevelHistogramAndRotation()
    {
        (RosterConfig baseCfg, FormationConfig tpl) = Load();
        RosterConfig cfg = baseCfg with
        {
            Experience = new RosterExperience(XpPerWin: 1, XpPerLoss: 0, LevelCosts: new[] { 2, 3, 4, 5, 6 }),
        };
        var log = new CombatLog();
        var roster = new Roster(cfg);
        var stock = new HeirloomStock(HeirloomConfig.Parse(ReadData("heirlooms.json")));
        var economy = new Economy(EconomyConfig.Parse(ReadData("economy.json")));
        int need = tpl.InitialRoster.Player.Count;

        RunStartSnapshot? prev = null;
        var lines = new List<string>();
        for (int run = 1; run <= 6; run++)
        {
            var picked = roster.Heroes.OrderByDescending(h => roster.MoraleOf(h.Id)).ThenBy(h => h.Id)
                .Take(need).Select(h => h.Id).ToList();
            RunStartSnapshot snap = RunStartSnapshot.Capture(run, roster, stock, economy, sortieIds: picked);
            foreach (string l in snap.DiffLines(prev))
            {
                lines.Add(l);
            }

            prev = snap;
            roster.AwardExperienceForBattle(log, win: true, reason: "snapshot_diff");
            foreach (var id in picked)
            {
                roster.ApplyRelief(log, id, -4, "snapshot_diff");
            }
        }

        string hasHist = lines.Any(l => l.Contains("等级分布")) ? "✅ 出现" : "⚠️ 未出现";
        string hasRot = lines.Any(l => l.Contains("换人")) ? "✅ 出现" : "⚠️ 未出现";
        Console.WriteLine($"[快照·新增读数] 等级分布变化：{hasHist}　轮换（换人）：{hasRot}");
        Console.WriteLine($"[快照·基线] {lines.FirstOrDefault()}");
        foreach (string l in lines.Skip(1).Take(4))
        {
            Console.WriteLine($"[快照·对比] {l}");
        }

        Assert.IsTrue(lines.Any(l => l.Contains("等级分布")), "等级分布变化必须可见 ✓");
        Assert.IsTrue(lines.Any(l => l.Contains("换人")), "轮换（换人 N 名）必须可见 ✓");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
