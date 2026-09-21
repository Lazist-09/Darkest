using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// **M8 验收复测包**（`tasks/m8_verification.md` V1~V6 + 字段 ㉚~㉞）。
/// 判据与读数一并输出；🔴 **不达标处如实报**（V5 的"特质影响战斗"一半未接）。
/// </summary>
[TestClass]
public sealed class M8VerificationPackTests
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

    private static EconomyConfig Econ() => EconomyConfig.Parse(ReadData("economy.json"));

    private static RosterConfig RosterCfg() => RosterConfig.Parse(ReadData("roster.json"));

    [TestMethod]
    public void V1_EconomyLoop_EarnIsTraceable_SpendExists()
    {
        EconomyConfig cfg = Econ();
        var log = new CombatLog();
        var econ = new Economy(cfg);

        // ① 来源**可追溯到「光照档 + 战斗数」**（不得只跟趟数挂钩）
        int radiantBattles3 = Enumerable.Range(0, 3).Sum(_ => cfg.RewardFor("radiant"));
        int blackBattles3 = Enumerable.Range(0, 3).Sum(_ => cfg.RewardFor("black"));
        int blackBattles6 = Enumerable.Range(0, 6).Sum(_ => cfg.RewardFor("black"));
        Assert.IsTrue(blackBattles3 > radiantBattles3, "同场数下越暗越多（光照档挂钩）");
        Assert.IsTrue(blackBattles6 > blackBattles3, "同档下打得越多越多（战斗数挂钩）");

        // ② **存在明确的支出出口**（减压 + 招募）
        Assert.IsTrue(cfg.StressReliefCost > 0, "减压是出口（花钱）");
        Assert.AreEqual(0, cfg.Coach.RecruitCost, "招募免费 ⇒ 它不是「花钱」出口，但**是名册出口**");

        // ③ 闭环模拟：一趟 3 场（暗档）⇒ 挣；回城减压一次 ⇒ 花
        int income = 0;
        for (int i = 0; i < 3; i++)
        {
            income += econ.AwardBattle(log, "shadowy", "battle");
        }

        int goldAfterEarn = econ.Gold;
        Assert.IsTrue(econ.TrySpend(log, cfg.StressReliefCost, "stress_relief"), "挣到的钱花得掉（V1 闭环）");

        string report =
            $"[M8] ㉚ 每趟金钱收支：来源 战斗×3（Shadowy 档）= {income}（battle_reward {cfg.BattleReward} + 档加成 {cfg.RewardFor("shadowy") - cfg.BattleReward}／场；事件来源 {cfg.EventReward}）" +
            $"　支出 减压×1 = {cfg.StressReliefCost}（招募 {cfg.Coach.RecruitCost} 免费）　余 {econ.Gold}（挣后 {goldAfterEarn}）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);
        Assert.IsTrue(econ.Gold >= 0);
    }

    [TestMethod]
    public void V2_MoraleLivesAcrossRuns_ReliefIsTheOutlet()
    {
        RosterConfig rcfg = RosterCfg();
        EconomyConfig ecfg = Econ();
        var log = new CombatLog();
        var roster = new Roster(rcfg);
        var econ = new Economy(ecfg, gold: 100);
        var rng = new RngProvider(20260909);

        // 跨趟曲线：出征 → 归来（士气下降）→ 回城减压（唯一出口）
        var curve = new List<int>();
        string hero = roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).First().Id;
        curve.Add(roster.MoraleOf(hero));

        for (int run = 0; run < 3; run++)
        {
            roster.ApplyReturnFromRun(log, new[] { (hero, roster.MoraleOf(hero) - 10) }); // 一趟掉 10
            curve.Add(roster.MoraleOf(hero));
        }

        int beforeRelief = roster.MoraleOf(hero);
        int relieved = roster.ApplyRelief(log, hero, ecfg.Building("tavern").MoraleRestore, "tavern");
        curve.Add(relieved);

        Assert.IsTrue(curve[0] > beforeRelief, "跨趟士气**确实下降**（无出口就会单调掉到底）");
        Assert.IsTrue(relieved > beforeRelief, "减压是**真出口**（把士气抬起来）");
        Assert.AreEqual(relieved, roster.OpeningMorale(new[] { hero })[hero], "下一趟开局士气 = 减压后的值（跨趟持有）");

        string report = $"[M8] ㉛ 跨趟士气曲线：{string.Join(" → ", curve)}（出征 ／ 归来×3 ／ 减压一次 +{ecfg.Building("tavern").MoraleRestore}）" +
                        $"　减压次数 1　有效减压量 +{relieved - beforeRelief}";
        Console.WriteLine(report);
        TestContext.WriteLine(report);
        _ = rng;
    }

    [TestMethod]
    public void V3_TavernVsAbbey_TradeoffNotDominance()
    {
        EconomyConfig cfg = Econ();
        StressReliefBuildingConfig tavern = cfg.Building("tavern");
        StressReliefBuildingConfig abbey = cfg.Building("abbey");

        Assert.AreEqual(tavern.Cost, abbey.Cost, "同价");
        Assert.AreEqual(tavern.MoraleRestore, abbey.MoraleRestore, "同效");
        Assert.AreEqual(tavern.NextRunPenalty, abbey.NextRunPenalty, "同效（副作用幅度也相同）");
        Assert.AreNotEqual(tavern.PenaltyChance, abbey.PenaltyChance, "**风险不同** ⇒ 选择依据是风格，不是优劣");

        // 选择分布 + 各自触发率（㉜）
        var log = new CombatLog();
        var econ = new Economy(cfg, gold: 1000000);
        var rng = new RngProvider(777);
        int tavernPicks = 0, abbeyPicks = 0, tavernHits = 0, abbeyHits = 0;
        const int n = 300;
        for (int i = 0; i < n; i++)
        {
            bool pickTavern = i % 2 == 0; // 玩家按风格选（此处 1:1）
            string id = pickTavern ? "tavern" : "abbey";
            StressReliefOutcome o = StressRelief.Apply(cfg, econ, rng, log, id, "hero", 50);
            if (pickTavern)
            {
                tavernPicks++;
                tavernHits += o.PenaltyTriggered ? 1 : 0;
            }
            else
            {
                abbeyPicks++;
                abbeyHits += o.PenaltyTriggered ? 1 : 0;
            }
        }

        string report = $"[M8] ㉜ Tavern ／ Abbey 选择分布：{tavernPicks} ／ {abbeyPicks}（按风格 1:1）" +
                        $"　副作用触发率 Tavern {tavernHits / (double)tavernPicks:P0}（配置 {tavern.PenaltyChance:P0}）" +
                        $" ／ Abbey {abbeyHits / (double)abbeyPicks:P0}（配置 {abbey.PenaltyChance:P0}）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);
        Assert.IsTrue(tavernHits / (double)tavernPicks > abbeyHits / (double)abbeyPicks, "Tavern 更不稳");
    }

    [TestMethod]
    public void V4_Roster_RotationIsReal_AndRookieIsWeaker()
    {
        RosterConfig rcfg = RosterCfg();
        EconomyConfig ecfg = Econ();
        var log = new CombatLog();
        var roster = new Roster(rcfg);

        Assert.AreEqual(0, ecfg.Coach.RecruitCost, "招募免费（V4）");
        Assert.AreEqual(1, ecfg.Coach.RookieLevel, "新兵 1 级（不比老的强）");
        // 🔴 M7②（#423）：上限单一来源 = 马车曲线 ⇒ 终值 28（旧口径 12 已收敛）✓
        Assert.AreEqual(28, rcfg.RosterCap, "名册终值上限 28（= 曲线末值；#423）");
        Assert.IsTrue(roster.Heroes.Count < rcfg.RosterCap, "起步未满 ⇒ **招募可达**（否则 V4 是死内容）");

        // 招募到上限 ⇒ 替补 ≥ 6 ⇒ **轮换休息真的可行**（不是摆设）
        while (roster.Heroes.Count < rcfg.RosterCap)
        {
            roster.Recruit(log, ecfg.Coach, "medic", "补员");
        }

        int substitutes = roster.Heroes.Count - RosterConfig.SortieSize;
        Assert.IsTrue(substitutes >= RosterConfig.SortieSize, "替补数 ≥ 出征数 ⇒ 轮换可行");
        Assert.IsTrue(roster.Heroes.Count(h => h.Level == 1) >= 1, "至少有新兵（1 级）");

        string dist = string.Join(" ／ ", roster.Heroes.GroupBy(h => h.Level).OrderBy(g => g.Key)
            .Select(g => $"Lv{g.Key}×{g.Count()}"));
        string traits = string.Join(" ／ ", roster.Heroes.SelectMany(h => h.Traits).GroupBy(t => t.Id)
            .OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key}×{g.Count()}"));
        string report = $"[M8] ㉝ 名册构成：在册 {roster.Heroes.Count}/{rcfg.RosterCap}（出征 6 + 替补 {substitutes}）" +
                        $"　等级分布 {dist}　特质分布（前 4）{traits}　轮换频率：每趟可换满 6 人 ⇒ 轮换成立";
        Console.WriteLine(report);
        TestContext.WriteLine(report);
    }

    [TestMethod]
    public void V5_LevelAffectsBattle_TraitsComputedButNotYetWired()
    {
        RosterConfig rcfg = RosterCfg();
        Assert.IsTrue(rcfg.LevelGrowth.HpPerLevel == 2 && rcfg.LevelGrowth.AttackPerLevel == 1, "等级只给属性小幅度");
        Assert.IsTrue(rcfg.Heroes.All(h => h.Traits.Count is >= 2 and <= 3), "特质 2~3 条");
        Assert.IsTrue(rcfg.Heroes.All(h => h.Traits.Any(t => t.DamagePct > 0 || t.MoraleDamagePct < 0)
                                          && h.Traits.Any(t => t.DamagePct < 0 || t.MoraleDamagePct > 0)), "正负都有");

        // 等级**确实影响战斗**（投影到单位）
        var director = MonteCarlo.HeadlessDriver.NewDirector(new CombatLog());
        var unit = director.Player.UnitsInSlotOrder().First();
        int hpBefore = unit.MaxHp, atkBefore = unit.EffectiveAttack;
        HeroConfig veteran = rcfg.Heroes.OrderByDescending(h => h.Level).First();
        HeroProjection.ApplyLevel(veteran, unit, rcfg.LevelGrowth);
        Assert.AreEqual(hpBefore + (2 * (veteran.Level - 1)), unit.MaxHp, "等级 → HP 生效（可被看见）");
        Assert.AreEqual(atkBefore + (1 * (veteran.Level - 1)), unit.EffectiveAttack, "等级 → 攻击生效（可被看见）");

        // 特质：**能算出**（汇总）但**尚未接进伤害管线**
        TraitEffects eff = HeroProjection.TraitEffectsOf(veteran);
        string report = $"[M8] V5 英雄个体化：等级投影**已生效**（{veteran.Name} Lv{veteran.Level} ⇒ HP+{2 * (veteran.Level - 1)} 攻击+{1 * (veteran.Level - 1)}）" +
                        $"　特质可计算（伤害 {eff.DamagePct:+0;-0;0}% ／ 受士气伤害 {eff.MoraleDamagePct:+0;-0;0}%）" +
                        $"　🔴 **但特质尚未接进伤害管线 ⇒ V5 的「影响战斗」只达成一半（如实报，不判红）**";
        Console.WriteLine(report);
        TestContext.WriteLine(report);
    }

    public TestContext TestContext { get; set; } = null!;
}
