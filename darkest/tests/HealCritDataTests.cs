using System;
using System.IO;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **数字外置（P29）· 暴击治疗概率** 的回归防线：
/// 原先 `SkillExecutor` 里硬写 `critRoll < (targets.Length > 1 ? 5.0 : 12.0)`（D7 / `#209`）⇒ 策划改不了 ⚠️
/// ⇒ 现搬到 `tuning.heal_crit.single_target_percent / multi_target_percent`（值不变 = 零数值改动）✓
/// 本用例锁：**值来自数据**（改它就变）＋ **缺键/越界必报错** ✓
/// </summary>
[TestClass]
public sealed class HealCritDataTests
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
    public void HealCritChances_ComeFromData_AndChangingThemChangesTheValues()
    {
        TuningConfig shipped = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(12, shipped.HealCrit.SingleTargetPercent, "出厂单体 = 12%（与修复前硬编码一致）");
        Assert.AreEqual(5, shipped.HealCrit.MultiTargetPercent, "出厂多目标 = 5%（同上）");

        BalanceTable table = BalanceTable.FromTuning(shipped);
        Assert.AreEqual(12, table.HealCritSinglePercent, "BalanceTable 必须暴露它（否则又成死数据）✓");
        Assert.AreEqual(5, table.HealCritMultiPercent, "同上 ✓");

        string patched = ReadData("tuning.json")
            .Replace("\"single_target_percent\": 12", "\"single_target_percent\": 20")
            .Replace("\"multi_target_percent\": 5", "\"multi_target_percent\": 9");
        TuningConfig varied = TuningConfig.Parse(patched);
        Assert.AreEqual(20, varied.HealCrit.SingleTargetPercent, "🔴 改数据必须改行为（只改 JSON）✓");
        Assert.AreEqual(9, varied.HealCrit.MultiTargetPercent, "同上 ✓");
    }

    [TestMethod]
    public void HealCrit_OutOfRangeOrMissing_Throws()
    {
        string shipped = ReadData("tuning.json");

        string ranged = shipped.Replace("\"single_target_percent\": 12", "\"single_target_percent\": 120");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(ranged), "越界（>100）⇒ 报错 ✓");

        string missing = shipped.Replace("\"single_target_percent\": 12", "\"single_target_percent_moved\": 12");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(missing),
            "🔴 缺键 ⇒ 报错（0 合法 ⇒ 必须靠 RequireKeys 存在性列表兜住）✓");
    }
}
