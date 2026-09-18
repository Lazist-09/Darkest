using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **阵亡消费**（策划 `#400` 裁定 **(a)+**：**移出名册 + 释放上限 + 留档**；🆕 **A11**：**可读 + 可追溯**）
///
/// 背景（我实测的缺口）：内核早就有 `DeathEvent(UnitId, IsPlayer, Cause)` ✓，但**名册侧从不消费它** ⇒
///   阵亡**不减少名册人数** ⇒ 契约的「`cap 12 = 出征 6 + 替补 6`」里"替补"永远补不上 ⚠️
///
/// 本用例钉住四件事（都是 A11 要的）：
///   ① **人数下降**（⇒ 名额**自然释放** ✓）· ② **Graveyard 留档**（含等级/原因 ✓）
///   ③ **可追溯事件** `HeroDiedEvent(…, RosterCountAfter)` ✓ · ④ **幂等**（重复消费不得重复移除 ✓）· ⑤ 敌方阵亡不影响 ✓
/// </summary>
[TestClass]
public sealed class RosterDeathTests
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
    public void PlayerDeath_RemovesFromRoster_FreesSlot_AndIsAuditable()
    {
        RosterConfig cfg = RosterConfig.Parse(ReadData("roster.json"));
        var log = new CombatLog();
        var roster = new Roster(cfg);

        int before = roster.Heroes.Count;
        string victim = roster.Heroes[0].Id;
        int victimLevel = roster.LevelOf(victim);

        // 内核既有的阵亡事件（**只认事件流** ⇒ 用例只负责把它放进日志 ✓）
        log.Append(new DeathEvent(UnitId.Of(victim), true, "test_kill"));

        int removed = roster.ConsumePlayerDeaths(log);

        Assert.AreEqual(1, removed, "应消费 1 名我方阵亡 ✓");
        Assert.AreEqual(before - 1, roster.Heroes.Count, "阵亡 ⇒ **名册人数 −1**（名额自然释放 ✓ A11①）");
        Assert.IsFalse(roster.Heroes.Any(h => h.Id == victim), "该员必须已移出名册 ✓");

        Assert.AreEqual(1, roster.Graveyard.Count, "必须**留档**（Graveyard 列表 ✓ A11②）");
        Assert.AreEqual(victim, roster.Graveyard[0].HeroId);
        Assert.AreEqual(victimLevel, roster.Graveyard[0].Level, "留档要保留**等级**（可追溯 ✓）");
        Assert.AreEqual("test_kill", roster.Graveyard[0].Cause, "留档要保留**原因**（可追溯 ✓）");

        var died = log.Events.OfType<HeroDiedEvent>().ToList();
        Assert.AreEqual(1, died.Count, "必须写 `HeroDiedEvent`（A11③ 可审计 ✓）");
        Assert.AreEqual(before - 1, died[0].RosterCountAfter, "事件里带**阵亡后的名册人数** ⇒ 可追溯 ✓");

        Assert.AreEqual(0, roster.ConsumePlayerDeaths(log), "**幂等**：再消费一次不得重复移除 ✓");

        log.Append(new DeathEvent(UnitId.Of("enemy_mook_1"), false, "test"));
        Assert.AreEqual(0, roster.ConsumePlayerDeaths(log), "**敌方阵亡不影响名册** ✓");

        // 🆕 A11 的**可读**面：名册构成读数必须把留档显示出来 ✓
        string line = RosterComposition.Describe(roster);
        Console.WriteLine($"[A11] {line}");
        TestContext.WriteLine($"[A11] {line}");
        Assert.IsTrue(line.Contains("阵亡留档 1"), "名册构成读数必须显示留档人数 ✓");
        Assert.IsTrue(line.Contains(roster.Graveyard[0].Name), "并显示最近一名（可追溯 ✓）");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
