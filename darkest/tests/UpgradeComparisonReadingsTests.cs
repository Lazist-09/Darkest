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
/// 🔴 **P2 升级对照读数**（用户清单 ⑤）：证明"**升级真的更强**"—— **只报数不判红**（`O-82`）✓
///
/// 分三层，**能验/不能验分开写**（诚实）：
///   ✅ **规则层（本用例实测）**：每座建筑每级的 `Effective*` 生效值 ⇒ "升级真的改了数字" ✓
///   ✅ **经济层（本用例实测）**：固定金钱预算下，Lv0 vs Lv1 能买到的**减压次数 / 士气总量** ✓
///   ❌ **战斗层（本用例不测）**：士气更高 ⇒ 同 seed 战斗更不容易崩 —— 那需要"带开局士气的整场对照"，
///      属探针工作（`M75`/`M76` 那套）⇒ **我不在这里假装测过** ⚠️
/// </summary>
[TestClass]
public sealed class UpgradeComparisonReadingsTests
{
    /// <summary>MSTest 注入（本仓其它用例同写法）✓</summary>
    public TestContext TestContext { get; set; } = null!;

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
    public void Upgrade_MovesTheEffectiveNumbers_AndTheGoldBudgetBuysMoreMorale()
    {
        var log = new CombatLog();
        HeirloomConfig cfg = HeirloomConfig.Parse(ReadData("heirlooms.json"));
        EconomyConfig eco = EconomyConfig.Parse(ReadData("economy.json"));
        var stock = new HeirloomStock(cfg);

        // 给足传家宝（只为把升级路线走通；**这不是改数值** ⇒ 用例内部的输入）✓
        foreach (string k in cfg.Kinds)
        {
            stock.Add(log, k, 40, "test");
        }

        int reliefBase = eco.StressReliefCost;
        int reliefRestoreBase = eco.Building("tavern").MoraleRestore; // 🔴 士气恢复在【建筑配置】里（`economy.json`）✓
        var lines = new List<string>();
        int changed = 0;

        foreach (UpgradePath path in cfg.UpgradePaths)
        {
            int lv0Cost = stock.EffectiveReliefCost(reliefBase);
            int lv0Restore = stock.EffectiveMoraleRestore(path.Building, reliefRestoreBase);
            int lv0Cap = stock.EffectiveRosterCap(8, 12);
            int lv0Rookie = stock.EffectiveRookieLevel(1);

            bool ok = stock.TryUpgrade(log, path.Building);
            Assert.IsTrue(ok, $"升级 {path.Building} 应成功（传家宝已给足）✓");

            int lv1Cost = stock.EffectiveReliefCost(reliefBase);
            int lv1Restore = stock.EffectiveMoraleRestore(path.Building, reliefRestoreBase);
            int lv1Cap = stock.EffectiveRosterCap(8, 12);
            int lv1Rookie = stock.EffectiveRookieLevel(1);

            if (lv1Cost != lv0Cost || lv1Restore != lv0Restore || lv1Cap != lv0Cap || lv1Rookie != lv0Rookie)
            {
                changed++;
            }

            lines.Add($"[P2] {path.Building,-11}（axis={path.Axis}）Lv0→Lv1：" +
                      $"减压价 {lv0Cost}→{lv1Cost}　减压恢复 {lv0Restore}→{lv1Restore}　" +
                      $"名册上限 {lv0Cap}→{lv1Cap}　新兵等级 {lv0Rookie}→{lv1Rookie}");
        }

        // ✅ 结构性判据（唯一一条）：**升级必须真的改变数字**（`HeirloomStock` 的验收口径）✓
        Assert.IsTrue(changed > 0, "🔴 升级必须至少改变一项生效值（否则「升级」是假的）✓");

        // ✅ 经济层：**固定预算**下能买到多少士气（Lv0 vs Lv1）—— 用酒馆（减压）做例 ✓
        const int budget = 12;
        string tavern = cfg.UpgradePaths.FirstOrDefault(p => p.Axis == "cost_down")?.Building ?? "tavern";
        int beforeCost = reliefBase;
        int restorePerRelief = reliefRestoreBase;
        // 现值（升级前）与升级后（已升过一级）各能买几次、共恢复多少士气 ✓
        int timesBefore = beforeCost <= 0 ? 0 : budget / beforeCost;
        int afterCost = stock.EffectiveReliefCost(reliefBase);
        int timesAfter = afterCost <= 0 ? 0 : budget / afterCost;
        lines.Add($"[P2] 经济层（{tavern} · 固定 {budget} 金）：升级前 {timesBefore} 次 × {restorePerRelief} 士气 = " +
                  $"{timesBefore * restorePerRelief}　⇒ 升级后 {timesAfter} 次 × {restorePerRelief} 士气 = {timesAfter * restorePerRelief}　" +
                  $"（同级建筑下**同样的钱买到更多士气** = 【规则层】的「更强」）✓");

        foreach (string l in lines)
        {
            Console.WriteLine(l);
            TestContext.WriteLine(l);
        }

        Console.WriteLine("[P2] ⚠️ **战斗层未测**：'士气更高 ⇒ 同 seed 战斗更不容易崩' 需要带开局士气的整场对照（探针工作）" +
                          "⇒ 本用例**不假装测过** ✓");
        TestContext.WriteLine("[P2] ⚠️ 战斗层未测（同上）✓");
    }
}
