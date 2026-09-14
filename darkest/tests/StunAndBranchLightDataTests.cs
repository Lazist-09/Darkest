using System;
using System.IO;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **数字外置（P29）· 第二批** 的回归防线：
/// ① `stun.buildup_on_apply`（原先硬写在 `EffectsStep` 里的 `+ 50`）
/// ② `branch_special_light_gain`（原先 `ExpeditionMapConfig` 的记录默认值 `= 20`）
/// ⇒ 本用例锁：**值确实来自数据**（改它就变）＋ **缺键/越界必报错**（不许静默兜底）✓
/// </summary>
[TestClass]
public sealed class StunAndBranchLightDataTests
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
    public void StunBuildupOnApply_ComesFromData_AndChangingItChangesTheValue()
    {
        TuningConfig shipped = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(50, shipped.Stun.BuildupOnApply, "出厂值 = 50（与修复前硬编码值一致 ⇒ 零数值改动）");
        Assert.AreEqual(50, BalanceTable.FromTuning(shipped).StunBuildupOnApply, "BalanceTable 必须暴露它 ✓");

        // 🔴「改它就变」：只改 JSON 一个数 ⇒ 读出的值随之变化（策划不动 C# 也能调）
        string patched = ReadData("tuning.json")
            .Replace("\"buildup_on_apply\": 50", "\"buildup_on_apply\": 25");
        Assert.AreEqual(25, TuningConfig.Parse(patched).Stun.BuildupOnApply);
    }

    [TestMethod]
    public void StunBuildupOnApply_OutOfRangeOrMissing_Throws()
    {
        string shipped = ReadData("tuning.json");

        string ranged = shipped.Replace("\"buildup_on_apply\": 50", "\"buildup_on_apply\": 120");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(ranged), "越界（>100）⇒ 报错 ✓");

        string missing = shipped.Replace("\"buildup_on_apply\": 50", "\"buildup_on_apply_moved\": 50");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(missing),
            "缺键 ⇒ 报错（反序列化后为 0；0 合法 ⇒ 靠 `stun` 这块的 0..100 校验与 RequireKeys 兜住）✓");
    }

    [TestMethod]
    public void BranchSpecialLightGain_ComesFromData_MissingKeyThrows()
    {
        ExpeditionMapConfig shipped = ExpeditionMapConfig.Parse(ReadData("expedition_map.json"));
        Assert.IsTrue(shipped.Map.BranchSpecialLightGain > 0, "支路光照收益来自数据（不再是记录默认值 20）✓");

        string missing = ReadData("expedition_map.json")
            .Replace("\"branch_special_light_gain\"", "\"branch_special_light_gain_moved\"");
        Assert.ThrowsException<InvalidDataException>(() => ExpeditionMapConfig.Parse(missing),
            "🔴 缺 branch_special_light_gain ⇒ 启动即报错（修复前静默取 20）");
    }

    [TestMethod]
    public void WitnessCritShockChance_ComesFromData_AndChangingItChangesTheValue()
    {
        TuningConfig shipped = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(50, shipped.Morale.WitnessCritShockChancePercent,
            "出厂值 = 50（与修复前硬编码 50.0 一致 ⇒ 零数值改动）");
        Assert.AreEqual(50, BalanceTable.FromTuning(shipped).WitnessCritShockChancePercent,
            "BalanceTable 必须暴露它（否则 `DamagePipeline` 读不到 ⇒ 又成死数据）✓");

        // 🔴「改它就变」：只改 JSON 一个数 ⇒ 读出的值随之变化 ✓
        string patched = ReadData("tuning.json")
            .Replace("\"witness_crit_shock_chance_percent\": 50", "\"witness_crit_shock_chance_percent\": 20");
        Assert.AreEqual(20, TuningConfig.Parse(patched).Morale.WitnessCritShockChancePercent);
    }

    [TestMethod]
    public void WitnessCritShockChance_OutOfRangeOrMissing_Throws()
    {
        string shipped = ReadData("tuning.json");

        string ranged = shipped.Replace("\"witness_crit_shock_chance_percent\": 50",
            "\"witness_crit_shock_chance_percent\": 150");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(ranged), "越界（>100）⇒ 报错 ✓");

        string missing = shipped.Replace("\"witness_crit_shock_chance_percent\": 50",
            "\"witness_crit_shock_chance_percent_moved\": 50");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(missing),
            "🔴 缺键 ⇒ 报错（0 合法 ⇒ 必须靠存在性列表 `RequireKeys` 兜住，否则就是静默取 0）✓");
    }
}
