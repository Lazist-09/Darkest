using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Math;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M1c · 阶段 2 的对照夹具**（架构三步走第 2 步："新路径默认关 + **1~2 技能试点并跑对照**"）。
///
/// 本夹具**不做任何切换**：它把**旧模型**与**新模型（武器区间 × (1+技能 dmg%)）**在同一输入下并排算出来，
/// 给出**差值表** ⇒ 这就是阶段 3"切换默认"时要用的**前后读数对照**的现成工具 ✓
///
/// 输入来源（可核）：
///   · 武器区间 = `units.json` 的 `weapon[i].dmg_min/dmg_max`（策划 `#448` 已对齐到 4 原型 ✓）
///   · 技能 `dmg%` = 参考项目示例（`smite 0%` · `zealous_accusation -40%`）——
///     ⚠️ 它们是**夹具输入**，**不是**我们技能表的值（我们的值归策划 ✓）
///   · 旧模型的"中性帧"= `attack × 1.0`（不吃段倍率/暴击/增益/减伤 ⇒ 只比较**形状**差异 ✓）
/// </summary>
[TestClass]
public sealed class M1cPilotComparisonTests
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

    /// <summary>夹具的输出：同一输入下，旧模型 / 新模型 / 差值（比率）。</summary>
    private readonly record struct Row(string Unit, int Tier, int SkillDmgPct, int OldRaw, double NewRaw)
    {
        public double Delta => NewRaw - OldRaw;

        public double Ratio => OldRaw == 0 ? 0 : NewRaw / OldRaw;
    }

    private static List<Row> Build(IEnumerable<UnitConfig> players, IReadOnlyList<int> tiers, IReadOnlyList<int> skills)
    {
        var rows = new List<Row>();
        foreach (UnitConfig u in players.Where(x => x.Weapon is not null && x.Side == "player"))
        {
            foreach (int tier in tiers)
            {
                var w = u.Weapon![tier];
                foreach (int pct in skills)
                {
                    // 旧模型：中性帧（attack 平推）· 新模型：武器区间中点 × (1 + dmg%)
                    // 🔴 两侧都**只算原始伤害**，不掺减伤/暴击/增益 ⇒ 比较的是模型**形状**而非平衡 ✓
                    double neu = BattleMath.WeaponRoll(w.DmgMin, w.DmgMax, 0.5);
                    rows.Add(new Row(u.Id, tier, pct, u.Attack,
                        BattleMath.WeaponRawDamage(w.DmgMin, w.DmgMax, pct, 0.5) is var r && r >= 0 ? r : 0));
                }
            }
        }

        return rows;
    }

    [TestMethod]
    public void PilotComparison_PrintsTheSideBySideTable_ForTheTwoPilotSkills()
    {
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        int[] pilots = { 0, -40 };          // 参考项目的两个技能 dmg%（smite / zealous_accusation）✓
        int[] tiers = { 0, 4 };             // 最低阶与最高阶（形状差异最明显）✓

        List<Row> rows = Build(units.Units, tiers, pilots);

        foreach (Row r in rows)
        {
            Console.WriteLine($"[M1c·对照] {r.Unit} tier{r.Tier} dmg{r.SkillDmgPct,4}% ｜ "
                + $"旧(中性帧) {r.OldRaw,3} ｜ 新(武器区间) {r.NewRaw,6:0.##} ｜ Δ {r.Delta,+7:0.##}（×{r.Ratio:0.00}）");
        }

        // 🔴 夹具的**自证**：新模型必须随 tier 单调不减（武器阶越高区间越大 ⇒ 同 dmg% 下不降 ✓）
        foreach (UnitConfig u in units.Units.Where(x => x.Side == "player" && x.Weapon is not null))
        {
            var w0 = u.Weapon![0];
            var w4 = u.Weapon[4];
            double n0 = BattleMath.WeaponRawDamage(w0.DmgMin, w0.DmgMax, 0, 0.5);
            double n4 = BattleMath.WeaponRawDamage(w4.DmgMin, w4.DmgMax, 0, 0.5);
            Assert.IsTrue(n4 >= n0, $"{u.Id}: 5 阶原始伤害不应低于 0 阶（{n4} vs {n0}）—— 若失败说明对齐数据有问题 ✓");
        }

        Assert.AreEqual(16, rows.Count, "4 原型 × 2 阶 × 2 技能 = 16 行对照 ✓");
        Console.WriteLine("[M1c·对照] 16 行并排读数已输出 ⇒ 阶段 3 切换时用它做**前后对照**（本夹具不切默认 ✓）");
        TestContext.WriteLine("[M1c·阶段2] 对照夹具 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
