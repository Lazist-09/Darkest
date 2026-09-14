using System;
using System.IO;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **数字外置（P29）· 连续未命中补偿** 的回归防线：
/// 原先 `HitStep` 里硬写着 `Math.Max(0, misses - 1) * 4` ⇒ 策划改不了（而它是**手感**参数）⚠️
/// ⇒ 现搬到 `tuning.consecutive_miss.hit_bonus_per_miss`（值不变 = 零数值改动）✓
/// 本用例锁：**值来自数据**（改它就变）＋ **缺键/越界必报错** ✓
/// </summary>
[TestClass]
public sealed class ConsecutiveMissDataTests
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
    public void ConsecutiveMissBonus_ComesFromData_AndChangingItChangesTheValue()
    {
        TuningConfig shipped = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.AreEqual(4, shipped.ConsecutiveMiss.HitBonusPerMiss,
            "出厂值 = 4（与修复前硬编码一致 ⇒ 零数值改动）");
        Assert.AreEqual(4, BalanceTable.FromTuning(shipped).ConsecutiveMissHitBonusPerMiss,
            "BalanceTable 必须暴露它（否则 HitStep 读不到 ⇒ 又成死数据）✓");

        string patched = ReadData("tuning.json")
            .Replace("\"hit_bonus_per_miss\": 4", "\"hit_bonus_per_miss\": 7");
        Assert.AreEqual(7, TuningConfig.Parse(patched).ConsecutiveMiss.HitBonusPerMiss,
            "🔴 改数据必须改行为（只改 JSON、不动 C#）✓");
    }

    [TestMethod]
    public void ConsecutiveMissBonus_OutOfRangeOrMissing_Throws()
    {
        string shipped = ReadData("tuning.json");

        string ranged = shipped.Replace("\"hit_bonus_per_miss\": 4", "\"hit_bonus_per_miss\": 200");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(ranged), "越界（>100）⇒ 报错 ✓");

        string missing = shipped.Replace("\"hit_bonus_per_miss\": 4", "\"hit_bonus_per_miss_moved\": 4");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(missing),
            "🔴 缺键 ⇒ 报错（0 合法 ⇒ 必须靠 RequireKeys 存在性列表兜住，否则静默取 0）✓");
    }
}
