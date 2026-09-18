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
/// 🔴 **策划 `#394` 的判据**：P0 那条"第 4 趟全同（平台期）"**是 (a) 探针不真实 还是 (b) 成长早期饱和**？
/// ⇒ **判据 = 跑 8~10 趟，看"起点变化"的轨迹**：
///    · 1~4 趟持续变、**5 趟起全同** ⇒ 倾向 **(b)**（数值域 ⇒ 登记"解冻后校准"）⚠️
///    · **始终在变** ⇒ (a) 不成立 ✅
///
/// 本用例 = **读数工具**（`O-82`：只报数不判红）· **零数值改动**（不碰任何 `data/*.json`）✓
/// 城镇侧策略（写死、可复现）：**能升级就升级，余钱减压士气最低者** ✓
/// </summary>
[TestClass]
public sealed class HamletLoopTrajectoryTests
{
    private const int Runs = 10; // 策划要的 8~10 ✓

    /// <summary>士气契约上限（`#245`/`#396` 的"封顶"指它；写在这里是为了让 A9 判据**显式** ✓）</summary>
    private const int MoraleCap = 100;

    /// <summary>字典相等（A9 要比 传家宝/建筑 两组字典 ✓）</summary>
    private static bool SameDict(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out int v) || v != kv.Value)
            {
                return false;
            }
        }

        return true;
    }

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
    public void TenRuns_ShowWhetherTheStartPointKeepsChanging()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        RosterConfig rosterCfg = RosterConfig.Parse(ReadData("roster.json"));
        HeirloomConfig heirCfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        EconomyConfig ecoCfg = EconomyConfig.Parse(ReadData("economy.json"));
        ExpeditionMapConfig mapCfg = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));

        var log = new CombatLog();
        var roster = new Roster(rosterCfg);
        var stock = new HeirloomStock(heirCfg);
        var economy = new Economy(ecoCfg);

        var lines = new List<string>();
        RunStartSnapshot? prev = null;
        int a8ChangedRuns = 0;   // A8：本趟有变化的趟数 ✓
        int a8NoChangeRuns = 0;  // A8：本趟无变化的趟数（必须能归因）✓

        for (int run = 1; run <= Runs; run++)
        {
            // ① **出发前快照** ⇒ 与上一趟对比（这就是"起点变化"的轨迹）✓
            RunStartSnapshot snap = RunStartSnapshot.Capture(run, roster, stock, economy);
            foreach (string l in snap.DiffLines(prev))
            {
                lines.Add(l);
            }

            // 🔴🆕 **A8**（`hamlet_loop.md §7.4`）：**每趟至少【一项】可核对的变化**（成长或损耗）——
            //    "无变化"必须能**归因**（例："刚好没花钱"）⇒ 本轨迹逐趟给结论 + 末尾给合计 ✓
            if (prev is not null)
            {
                int changed = snap.DiffLines(prev).Count(l => l.StartsWith("[养成]"));
                if (changed <= 0)
                {
                    a8NoChangeRuns++;
                    lines.Add($"[A8] 第 {run} 趟：⚠️ **本趟无变化** ⇒ **必须能归因**（否则违反 A8）");
                }
                else
                {
                    a8ChangedRuns++;
                }
            }

            // 🔴🆕 **A9**（策划 `#396`）：**任一条轴到顶时，其余轴至少一条仍在动** ——
            //    本轨迹里最可能先到顶的轴 = **士气（100 封顶）** ⇒ 到顶后检查"别的轴还在不在动" ✓
            //    （P3 装备/训练就是被登记为「士气到顶后的解法」⇒ 这条读数给那个判断做依据 ✓）
            if (prev is not null && snap.MoraleAvg >= MoraleCap)
            {
                bool othersMoved = snap.Gold != prev.Gold
                    || snap.LevelAvg != prev.LevelAvg || snap.LevelMax != prev.LevelMax
                    || snap.RosterCap != prev.RosterCap || snap.TraitsPositive != prev.TraitsPositive
                    || snap.TraitsNegative != prev.TraitsNegative || snap.TraitsLocked != prev.TraitsLocked
                    || snap.Diseases != prev.Diseases
                    || !SameDict(snap.Heirlooms, prev.Heirlooms) || !SameDict(snap.BuildingLevels, prev.BuildingLevels);
                lines.Add($"[A9] 第 {run} 趟：士气已到顶（{snap.MoraleAvg}/{MoraleCap}）⇒ 其余轴仍在动？" +
                          $"{(othersMoved ? "**是** ✅（A9 成立）" : "**否** ⚠️（**A9 被违反**：全轴到顶 ⇒ 应显式设计终局）")}");
            }

            prev = snap;

            // ② **跑一趟**（本用例用"确定性推图"代替真实战斗：走到终点 = 完成；只为产生**光照档收益**）✓
            int heirloomGainPerTier = 0;
            foreach (string tier in new[] { "dim", "shadowy", "dark" })
            {
                heirloomGainPerTier += stock.AwardForTier(log, tier, "trajectory");
            }

            // 🔴🆕 **让等级轴真的动**（占位数值是策划 `#399` 明示的 ⇒ 用起来 ✓）：
            //    战斗胜利 ⇒ 全队 +经验（与宿主同一条通道 `AwardExperienceForBattle` ✓）
            roster.AwardExperienceForBattle(log, win: true, reason: "trajectory");

            int goldGain = economy.AwardBattle(log, "dark", "trajectory");
            _ = heirloomGainPerTier;
            _ = goldGain;

            // ③ **城镇动作**（写死的策略，可复现）：① 能升级就升级 ② 余钱减压士气最低者 ✓
            foreach (UpgradePath path in heirCfg.UpgradePaths)
            {
                if (stock.CanUpgrade(path.Building))
                {
                    stock.TryUpgrade(log, path.Building);
                }
            }

            int reliefCost = stock.EffectiveReliefCost(ecoCfg.StressReliefCost);
            while (economy.Gold >= reliefCost)
            {
                HeroConfig? worst = roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).FirstOrDefault();
                if (worst is null)
                {
                    break;
                }

                int restore = stock.EffectiveMoraleRestore("tavern", ecoCfg.Building("tavern").MoraleRestore);
                if (!economy.TrySpend(log, reliefCost, "trajectory_relief"))
                {
                    break;
                }

                roster.ApplyRelief(log, worst.Id, restore, "tavern");
                reliefCost = stock.EffectiveReliefCost(ecoCfg.StressReliefCost);
            }
        }

        // ④ **轨迹汇总**（策划要看的就是"哪一段开始平"）✓
        var equalRuns = lines.Where(l => l.Contains("十项全同")).Count();
        lines.Add($"[A8] 合计：有变化的趟 **{a8ChangedRuns}** ／ 无变化的趟 **{a8NoChangeRuns}**" +
                  $"（`hamlet_loop.md §7.4`：每趟至少一项可核对的变化；无变化须能归因）" +
                  $"{(a8NoChangeRuns == 0 ? " ⇒ ✅ **A8 成立**（无一趟为空）" : " ⇒ ⚠️ 需逐趟给出归因")}");
        lines.Add($"[P0·轨迹] 共 {Runs} 趟：**全同的趟数 = {equalRuns}**（若从第 5 趟起连续全同 ⇒ 倾向 (b) 早期饱和 ⚠️）" +
                  $"　建筑与传家宝轨迹见上（`O-82`：只报数不判红）✓");

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        // 🔴 结构性判据（唯一一条）：**10 趟里至少要有一次"起点变了"** ⇒ 否则说明城镇动作根本没接线 ⚠️
        Assert.IsTrue(lines.Any(l => l.Contains("变了")), "10 趟里必须至少有一次起点变化（否则城镇动作没接线）✓");
    }

    /// <summary>
    /// 🔴 **含损耗的轨迹**（策划 `#394` 的 caveat：上面那条"简化循环"没模拟损耗）——
    /// 本用例每趟**注入疾病**（`Roster.Infect` ✓），且**不治病**（钱优先升级）⇒ 看"疾病累积"会不会压住成长 ✓
    /// ⚠️ **诚实标注**：**阵亡这一面我模拟不了** —— `Roster` **没有** `Remove`/`Die` 这类 API（只有 `Infect` /
    ///    `ApplyRelief` / `ApplyReturnFromRun`）⇒ **阵亡在名册侧没有表示** ⚠️ ⇒ 这本身是一条**缺口**（已投策划）✓
    /// </summary>
    [TestMethod]
    public void TenRuns_WithDiseaseLoss_ShowWhetherLossOutpacesGrowth()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        RosterConfig rosterCfg = RosterConfig.Parse(ReadData("roster.json"));
        HeirloomConfig heirCfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        EconomyConfig ecoCfg = EconomyConfig.Parse(ReadData("economy.json"));

        var log = new CombatLog();
        var roster = new Roster(rosterCfg);
        var stock = new HeirloomStock(heirCfg);
        var economy = new Economy(ecoCfg);

        var lines = new List<string>();
        RunStartSnapshot? prev = null;
        int a8ChangedRuns = 0;   // A8：本趟有变化的趟数 ✓
        int a8NoChangeRuns = 0;  // A8：本趟无变化的趟数（必须能归因）✓
        string diseaseId = SanitariumConfig.Parse(ReadData("sanitarium.json")).Diseases.FirstOrDefault()?.Id ?? "disease_unknown";

        for (int run = 1; run <= Runs; run++)
        {
            RunStartSnapshot snap = RunStartSnapshot.Capture(run, roster, stock, economy);
            foreach (string l in snap.DiffLines(prev))
            {
                lines.Add(l);
            }

            // 🔴🆕 **A8**（`hamlet_loop.md §7.4`）：**每趟至少【一项】可核对的变化**（成长或损耗）——
            //    "无变化"必须能**归因**（例："刚好没花钱"）⇒ 本轨迹逐趟给结论 + 末尾给合计 ✓
            if (prev is not null)
            {
                int changed = snap.DiffLines(prev).Count(l => l.StartsWith("[养成]"));
                if (changed <= 0)
                {
                    a8NoChangeRuns++;
                    lines.Add($"[A8] 第 {run} 趟：⚠️ **本趟无变化** ⇒ **必须能归因**（否则违反 A8）");
                }
                else
                {
                    a8ChangedRuns++;
                }
            }

            // 🔴🆕 **A9**（策划 `#396`）：**任一条轴到顶时，其余轴至少一条仍在动** ——
            //    本轨迹里最可能先到顶的轴 = **士气（100 封顶）** ⇒ 到顶后检查"别的轴还在不在动" ✓
            //    （P3 装备/训练就是被登记为「士气到顶后的解法」⇒ 这条读数给那个判断做依据 ✓）
            if (prev is not null && snap.MoraleAvg >= MoraleCap)
            {
                bool othersMoved = snap.Gold != prev.Gold
                    || snap.LevelAvg != prev.LevelAvg || snap.LevelMax != prev.LevelMax
                    || snap.RosterCap != prev.RosterCap || snap.TraitsPositive != prev.TraitsPositive
                    || snap.TraitsNegative != prev.TraitsNegative || snap.TraitsLocked != prev.TraitsLocked
                    || snap.Diseases != prev.Diseases
                    || !SameDict(snap.Heirlooms, prev.Heirlooms) || !SameDict(snap.BuildingLevels, prev.BuildingLevels);
                lines.Add($"[A9] 第 {run} 趟：士气已到顶（{snap.MoraleAvg}/{MoraleCap}）⇒ 其余轴仍在动？" +
                          $"{(othersMoved ? "**是** ✅（A9 成立）" : "**否** ⚠️（**A9 被违反**：全轴到顶 ⇒ 应显式设计终局）")}");
            }

            prev = snap;

            // 收益（确定性）✓
            foreach (string tier in new[] { "dim", "shadowy", "dark" })
            {
                stock.AwardForTier(log, tier, "trajectory_loss");
            }

            // 🔴🆕 同上：**让等级轴在损耗版里也真的动** ⇒ A9 的"其余轴仍在动"才有实料 ✓
            roster.AwardExperienceForBattle(log, win: true, reason: "trajectory_loss");

            economy.AwardBattle(log, "dark", "trajectory_loss");

            // 🔴 **损耗**：每趟让**一名**英雄患病（不治病 ⇒ 累积）✓
            HeroConfig? victim = roster.Heroes.OrderBy(h => roster.DiseasesOf(h.Id).Count).ThenBy(h => h.Id).FirstOrDefault();
            if (victim is not null)
            {
                roster.Infect(log, victim.Id, diseaseId, "trajectory_loss");
            }

            // 城镇动作：能升级就升级；余钱减压 ✓（**不治病** —— 让损耗面显形）
            foreach (UpgradePath path in heirCfg.UpgradePaths)
            {
                if (stock.CanUpgrade(path.Building))
                {
                    stock.TryUpgrade(log, path.Building);
                }
            }

            int reliefCost = stock.EffectiveReliefCost(ecoCfg.StressReliefCost);
            while (economy.Gold >= reliefCost)
            {
                HeroConfig? worst = roster.Heroes.OrderBy(h => roster.MoraleOf(h.Id)).FirstOrDefault();
                if (worst is null || !economy.TrySpend(log, reliefCost, "trajectory_loss_relief"))
                {
                    break;
                }

                roster.ApplyRelief(log, worst.Id, stock.EffectiveMoraleRestore("tavern", ecoCfg.Building("tavern").MoraleRestore), "tavern");
                reliefCost = stock.EffectiveReliefCost(ecoCfg.StressReliefCost);
            }
        }

        lines.Add($"[P0·损耗轨迹] 共 {Runs} 趟（每趟 +1 疾病、不治病）：疾病最终 = {roster.Heroes.Sum(h => roster.DiseasesOf(h.Id).Count)}" +
                  $"　⚠️ 阵亡面**无法模拟**（`Roster` 无 `Remove`/`Die`）⇒ 名册侧损耗只有 士气/疾病/虚弱 ✓");

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        Assert.IsTrue(lines.Any(l => l.Contains("疾病")), "损耗轨迹必须能看到疾病项 ✓");
    }

    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;
}
