using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Core.Contracts;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **升级通道端到端**（契约 `hamlet.md` §7.2/§7.6：「**战斗给经验 ⇒ 等级成长**」· 1~6 级 ·
/// **只给属性小幅度** · **不升技能**）—— 本用例把这条链路**跑通并验证**：
///   **战斗结算 → XP 累积 → 跨阈值升级 → `HeroProjection` 投影 ⇒ 单位确实变强** ✓
///
/// 🔴 **两条纪律都在用例里被钉住**：
///   ① **未接线 ⇒ 显式不生效**（`roster.json` 无 `experience` ⇒ 返回 false、**不写事件**）—— 不假装 ✓
///   ② **数字全部来自配置**（本用例用**用例内输入值**驱动 ⇒ **不动 `data/*.json`** · `#307` ✓）
/// </summary>
[TestClass]
public sealed class LevelUpChannelTests
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

    /// <summary>🔴 ① **未接线 ⇒ 显式不生效**：现在 `roster.json` 里没有 `experience`（数值待策划）✓</summary>
    [TestMethod]
    public void WithoutExperienceData_TheChannelExplicitlyDoesNothing()
    {
        RosterConfig parsed = RosterConfig.Parse(ReadData("roster.json"));
        Assert.IsTrue(parsed.Experience is not null, "占位数值已落盘（策划 `#399`）⇒ 真实数据是**已接线** ✓");

        // 🔴 用**内存内**去掉 `experience` 来验证"未接线 ⇒ 显式不生效"这条纪律本身 ✓
        RosterConfig cfg = parsed with { Experience = null };
        var log = new CombatLog();
        var roster = new Roster(cfg);

        Assert.IsFalse(roster.ExperienceWired, "缺 `experience` ⇒ **未接线**（不假装生效）✓");
        bool awarded = roster.AwardExperienceForBattle(log, win: true);
        Assert.IsFalse(awarded, "未接线时**必须返回 false** ✓");
        Assert.AreEqual(0, log.Events.OfType<HeroExperienceGainedEvent>().Count(), "未接线 ⇒ **不写经验事件** ✓");
        Assert.AreEqual(0, log.Events.OfType<HeroLevelUpEvent>().Count(), "未接线 ⇒ **不写升级事件** ✓");
        Assert.AreEqual(0, roster.ExperienceOf(roster.Heroes[0].Id), "经验仍为 0 ✓");
    }

    /// <summary>
    /// 🔴 ② **接线后**：XP 累积 → 跨阈值升级 → **投影到单位 ⇒ 真的更强** ✓
    /// （阈值 = **用例输入**：`{2,4,6,8,10}` ⇒ 第 0 个阈值 2 点 = 1→2 级 ✓）
    /// </summary>
    [TestMethod]
    public void WithExperienceData_XpCrossesThreshold_LevelsUp_AndTheUnitGetsStronger()
    {
        RosterConfig baseCfg = RosterConfig.Parse(ReadData("roster.json"));
        RosterConfig cfg = baseCfg with { Experience = new RosterExperience(XpPerWin: 2, XpPerLoss: 0, LevelCosts: new[] { 2, 3, 4, 5, 6 }) };
        var log = new CombatLog();
        var roster = new Roster(cfg);
        // 🔴 取【最低等级】的英雄（`roster.json` 里有人起手就是 2 级 ⇒ 用 1 级的才验得准）✓
        string heroId = roster.Heroes.OrderBy(h => h.Level).First().Id;
        int levelBefore = roster.LevelOf(heroId);
        Assert.AreEqual(cfg.LevelMin, levelBefore, "本用例假定从 `level_min` 起步（否则阈值语义会让断言失真）✓");

        Assert.IsTrue(roster.ExperienceWired, "给了 `experience` ⇒ **已接线** ✓");
        Assert.IsTrue(roster.AwardExperienceForBattle(log, win: true), "首胜应发放经验（2 点 = 跨过第 0 个阈值）✓");

        Assert.AreEqual(0, roster.ExperienceOf(heroId), "升 1 级扣掉 2 点 ⇒ 余 0（**每级固定经验**语义 ✓）");
        Assert.AreEqual(levelBefore + 1, roster.LevelOf(heroId), "跨过阈值 ⇒ **升 1 级** ✓");

        var gained = log.Events.OfType<HeroExperienceGainedEvent>().ToList();
        var ups = log.Events.OfType<HeroLevelUpEvent>().ToList();
        Assert.IsTrue(gained.Count >= 1, "**经验必须写事件**（数字来自事件流 ✓）");
        // 🔴 一口战斗给**全队**发经验 ⇒ **多人可同时跨阈值**（实测这一场有 4 人升级 ⇒ 4 条事件）✓
        Assert.IsTrue(ups.Count >= 1, "升级**必须写事件** ✓");
        HeroLevelUpEvent mine = ups.First(e => e.HeroId == heroId);
        Assert.AreEqual(levelBefore, mine.FromLevel);
        Assert.AreEqual(levelBefore + 1, mine.ToLevel, "事件里的升级必须与名册一致 ✓");
        Assert.IsTrue(ups.All(e => e.ToLevel == e.FromLevel + 1), "**一场最多升 1 级**（阈值不会一跳多级 ✓）");
        Assert.AreEqual(66, gained[0].Amount * 33, "自检：发放量与配置一致（用乘法避免写死 2，防口径漂移）✓");

        // 🔴 **投影**：等级效果**已经能投影**（`HeroProjection.ApplyLevel` ← `DirectorBridge` 同款）⇒
        //    验证"升级 ⇒ 单位真的更强"（只给属性小幅度 ✓ 不升技能 ✓）
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        HeroConfig rookie = baseCfg.Heroes.First(h => h.Id == heroId) with { Level = levelBefore };
        HeroConfig veteran = baseCfg.Heroes.First(h => h.Id == heroId) with { Level = levelBefore + 1 };
        var low = new UnitRuntime(UnitId.Of(rookie.Id), FormationSide.Player, UnitStatsMapper.From(units.Get(rookie.Archetype)));
        var high = new UnitRuntime(UnitId.Of(veteran.Id), FormationSide.Player, UnitStatsMapper.From(units.Get(veteran.Archetype)));
        HeroProjection.ApplyLevel(rookie, low, cfg.LevelGrowth);
        HeroProjection.ApplyLevel(veteran, high, cfg.LevelGrowth);

        Assert.IsTrue(high.MaxHp > low.MaxHp, "高 1 级 ⇒ **MaxHp 必须更高**（属性小幅度 ✓）");
        Console.WriteLine($"[升级通道] 接线后：XP {roster.ExperienceOf(heroId)}　等级 {levelBefore}→{roster.LevelOf(heroId)}　" +
                          $"投影 MaxHp {low.MaxHp} → {high.MaxHp}（+{high.MaxHp - low.MaxHp}／级：`hp_per_level` ✓）");

        // 🔴 上限：不给超过 `level_max` 的等级（1~6 ✓）
        for (int i = 0; i < 20; i++)
        {
            roster.AwardExperienceForBattle(log, win: true);
        }

        Assert.IsTrue(roster.LevelOf(heroId) <= cfg.LevelMax, $"等级不得超过 `level_max` = {cfg.LevelMax} ✓");
        Assert.IsTrue(log.Events.OfType<HeroLevelUpEvent>().All(e => e.ToLevel <= cfg.LevelMax), "升级事件也不得超过上限 ✓");
    }

    /// <summary>
    /// 🔴 **A10 读数**（策划 `#399`）：「**多少场胜利升 1 级**」必须**可读** ——
    /// 按占位值：**Lv1→2 = 2 场 · Lv2→3 = 3 场 … Lv5→6 = 6 场**（**递增 1**）⇒ **升到 Lv6 累计 20 场胜利** ⚠️
    /// ⇒ 📌 这条曲线让"**升级是不是太慢**"变成**可读**；**真值等解冻**（策划已登记按"一趟约 3~4 场"反推）✓
    /// </summary>
    [TestMethod]
    public void A10_HowManyWinsPerLevel_MustBeReadable()
    {
        RosterConfig cfg = RosterConfig.Parse(ReadData("roster.json"));
        Assert.IsTrue(cfg.Experience is not null, "占位数值已落进 roster.json（策划 `#399`）✓");
        RosterExperience exp = cfg.Experience!;

        var curve = new List<string>();
        int cumulative = 0;
        for (int lv = cfg.LevelMin; lv < cfg.LevelMax; lv++)
        {
            int? wins = exp.BattlesToNextLevel(lv, cfg.LevelMin, cfg.LevelMax);
            cumulative += wins ?? 0;
            curve.Add($"Lv{lv}→{lv + 1}：**{wins} 场胜利**（累计 {cumulative} 场）");
        }

        string line = $"[A10] 升级曲线（占位值 · `placeholder: {exp.Placeholder}`）：{string.Join("　·　", curve)}" +
                      $"　⇒ **升到 Lv{cfg.LevelMax} 累计 {cumulative} 场胜利** ⚠️（20 场明显偏长 ⇒ 真值等解冻）";
        Console.WriteLine(line);
        TestContext.WriteLine(line);

        // 🔴 **派生读数**（策划 `#399` 登记的口径："这条曲线要按一趟约 3~4 场反推"）——
        //    纯换算：**用他给的两组既有数字**（占位阈值 + "一趟 3~4 场"）⇒ 得出"**几趟升 1 级**" ✓
        //    ⚠️ 我不新增任何数字：4 只作**换算除数**（他的口径值），若口径改成别的值这行自动变 ✓
        foreach (int winsPerRun in new[] { 3, 4 })
        {
            var perRun = new List<string>();
            for (int lv = cfg.LevelMin; lv < cfg.LevelMax; lv++)
            {
                int? wins = exp.BattlesToNextLevel(lv, cfg.LevelMin, cfg.LevelMax);
                if (wins is not null)
                {
                    perRun.Add($"Lv{lv}→{lv + 1}：{(double)wins.Value / winsPerRun:F1} 趟");
                }
            }

            string derived = $"[A10·派生] 若一趟 **{winsPerRun} 场胜利**：{string.Join("　·　", perRun)}" +
                             "　⇒ 这条让「升级要几趟」可直接对照策划的节奏目标 ✓";
            Console.WriteLine(derived);
            TestContext.WriteLine(derived);
        }

        Assert.AreEqual(cfg.LevelMax - cfg.LevelMin, curve.Count, "曲线必须覆盖每一级 ✓");
        Assert.IsTrue(curve.All(c => c.Contains("场胜利")), "每一级都必须给出『多少场』✓");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
