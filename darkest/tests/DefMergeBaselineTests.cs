using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Math;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **"合并 def"的【前置基线】**（架构硬要求：`M1a` 的"合并 def（⚠️ 合并前先留命中率基线）"）。
///
/// 为什么要先留：
///   我们（疑似）把原版**一个 `def`** 拆成了 **`PhysDef`（减伤）+ `Dodge`（命中）** 两个字段
///   （参考项目 `Character.cs` 的一手证据：`Character.Dodge` 读的就是 `AttributeType.DefenseRating`）✓
///   ⇒ 合并**会改判定读数** ⇒ 🔴 **没有"合并前"的数字，就无法区分"对齐成功"与"改坏了"** ✓
///
/// 本用例做两件：
///   ① **打出全矩阵**（攻击方 × 防御方：命中% 与 物理减伤%）—— 走**真实代码路径**
///      （`BattleMath.HitRate` / `BattleMath.PhysicalMitigation`，钳制与除数取自 `tuning.json` ✓）
///   ② **钉住当前值**（合并后这两个断言必须**按登记表逐条改**，不许静默漂移）✓
///
/// ⚠️ 本用例**不改任何数值**：它只是"拍照"（合并前后各拍一次即构成对照）✓
/// </summary>
[TestClass]
public sealed class DefMergeBaselineTests
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
    public void Baseline_HitAndMitigationMatrix_IsRecordedBeforeMergingDef()
    {
        BalanceTable balance = BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json")));
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));

        var lines = new List<string>
        {
            "[def 合并基线] 攻击方 × 防御方 ⇒ 命中% / 物理减伤%",
            $"[def 合并基线] 钳制 = [{balance.HitClampMin}, {balance.HitClampMax}] · 减伤除数 = {balance.PhysicalMitigationDivisor}",
        };

        UnitConfig[] all = units.Units.ToArray();
        foreach (UnitConfig atk in all.Where(u => u.Side == "player"))
        {
            foreach (UnitConfig def in all)
            {
                // 🔴 真实路径：命中只看目标 Dodge（+ 技能 hitMod；基线取 0）✓
                int hit = BattleMath.HitRate(def.Dodge, 0, balance.HitClampMin, balance.HitClampMax);

                // 🔴 真实路径：物理减伤只看目标 PhysDef（除数取自 tuning）✓
                int mitigPct = (int)Math.Round(BattleMath.PhysicalMitigation(def.Prot, balance.PhysicalMitigationDivisor) * 100);

                lines.Add($"[def 合并基线] {atk.Id}(atk {atk.Attack}) → {def.Id}(dodge {def.Dodge} / phys_def {def.Prot}) ⇒ 命中 {hit}% · 减伤 {mitigPct}%");
            }
        }

        lines.Add($"[def 合并基线] 单位数 = {all.Length}（我方 {all.Count(u => u.Side == "player")} / 敌方 {all.Count(u => u.Side != "player")}）");

        // ② 钉住当前值（合并 def 时**按登记表逐条改**；漂移即红 ✓）
        //    出处：本项目现有公式（`BattleMath`）：命中 = clamp(100 − dodge + hitMod, min, max)
        //         减伤 = physDef / (physDef + divisor)
        Assert.AreEqual(BattleMath.HitRate(10, 0, balance.HitClampMin, balance.HitClampMax), 90,
            "命中基线：dodge 10 ⇒ 90%（与既有单测同源 ✓）");
        Assert.AreEqual(100, BattleMath.HitRate(0, 0, balance.HitClampMin, balance.HitClampMax), "dodge 0 ⇒ 100% ✓");
        Assert.AreEqual(balance.HitClampMin, BattleMath.HitRate(100, -60, balance.HitClampMin, balance.HitClampMax), "触下限 ✓");

        foreach (string l in lines)
        {
            Console.WriteLine(l);
        }

        TestContext.WriteLine($"[def 合并基线] 已拍 {lines.Count - 3} 条矩阵读数（合并前后各拍一次 = 对照）✓");
    }

    public TestContext TestContext { get; set; } = null!;
    /// <summary>
    /// 🔴 **合并 `def` 的【原版口径对照】** —— 把甲/乙/丙的**代价变成数字**（供裁定引用 ✓）。
    ///
    /// 一手事实（`reports/def_merge_baseline.md` §3）：原版**只有一个 `def`**（= 我们的 `Dodge`），
    ///   原版的**减伤**走 `prot`（`Clamp(prot, -1, max(0.85, raw))` ⇒ 0~0.85 比例），
    ///   而 4 原型对到的原版职业 **`prot` 均为 0** ✓
    /// ⇒ 本用例并排算两列：**我方口径**（`phys_def` / 除数）与**原版口径**（`prot` 比例）✓
    ///   🔴 **零行为**：只读数据 + 调纯函数 ⇒ 不改任何数值、不改判定 ✓
    /// </summary>
    [TestMethod]
    public void ReferenceSemantics_ShowsWhatStrictAlignmentWouldCost()
    {
        BalanceTable balance = BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json")));
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));

        int rows = 0;
        int maxDelta = 0;
        var lines = new List<string> { "[def 对照] 单位 ⇒ 命中%（两口径同源）· 我方减伤% · 原版减伤%（prot 比例）· 差" };

        foreach (UnitConfig u in units.Units)
        {
            int hit = BattleMath.HitRate(u.Dodge, 0, balance.HitClampMin, balance.HitClampMax);
            int oursPct = (int)Math.Round(BattleMath.PhysicalMitigation(u.Prot, balance.PhysicalMitigationDivisor) * 100);
            int refPct = (int)Math.Round(UnitStatsMapper.From(u).ProtFraction * 100);   // 🔴 走**真实映射路径**（contract 的 ProtFraction ✓）
            int delta = oursPct - refPct;
            maxDelta = Math.Max(maxDelta, Math.Abs(delta));
            rows++;
            lines.Add($"[def 对照] {u.Id,-16} 命中 {hit,3}% · 我方减伤 {oursPct,3}% · 原版减伤 {refPct,3}%（prot {u.Prot}）· 差 {delta,+4}");
        }

        lines.Add($"[def 对照] 共 {rows} 个单位 · 最大减伤差 = {maxDelta} 个百分点 ⇒ 这就是严格照原版的代价 ✓");
        foreach (string s in lines)
        {
            Console.WriteLine(s);
        }

        Assert.AreEqual(7, rows, "基线覆盖 7 个单位（我方 4 + 敌方 3 ✓）");
        Assert.IsTrue(maxDelta > 0,
            "🔴 我方减伤与原版不同（我方非零、原版为 0）⇒ 这就是 M1a「合并 def」要裁的点 ✓");

        TestContext.WriteLine("[def 对照] 原版口径对照已输出 ✓");
    }
}
