using System;
using System.IO;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **数字外置（用户 2026-09-14）·"不许静默兜底"** 的回归防线：
/// `overtime_reinforcement` 的 `safety_factor` / `wave_interval_rounds` 原先在 C# 里有**默认值**
/// （`= 0.8` / `= 3`），而 `wave_interval_rounds` 连 key 都不在 `tuning.json` 里 ⚠️
/// ⇒ 那两个数就是"藏在代码里的平衡数字"：策划改它们**必须动 C#**，且**缺键时会静默取默认值**。
///
/// 现已：① 两个字段**不给默认值** ② `tuning.json` 补齐 `wave_interval_rounds` ③ 加载级校验必填且 > 0
/// ⇒ 本用例锁：**缺键 / 非法值 ⇒ 启动即报错**（这是"数字住在数据里"的可执行证据）✓
/// </summary>
[TestClass]
public sealed class TuningNoSilentDefaultTests
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
    public void ShippedTuning_ProvidesTheKeys_AndValuesAreReadable()
    {
        TuningConfig cfg = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.IsTrue(cfg.OvertimeReinforcement.SafetyFactor > 0, "safety_factor 必须由数据给出（> 0）");
        Assert.IsTrue(cfg.OvertimeReinforcement.WaveIntervalRounds > 0, "wave_interval_rounds 必须由数据给出（> 0）");
    }

    [TestMethod]
    public void MissingSafetyFactor_ThrowsAtLoad_NotSilentlyDefaulted()
    {
        string shipped = ReadData("tuning.json");
        string patched = shipped.Replace("\"safety_factor\": 0.8,", string.Empty);
        Assert.AreNotEqual(shipped, patched, "用例自身必须真的删掉了那个键");

        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(patched),
            "🔴 缺 safety_factor ⇒ 启动即报错（修复前会**静默取 0.8**）");
    }

    [TestMethod]
    public void MissingWaveIntervalRounds_ThrowsAtLoad_NotSilentlyDefaulted()
    {
        string shipped = ReadData("tuning.json");
        string patched = shipped.Replace("\"wave_interval_rounds\": 3,", string.Empty);
        Assert.AreNotEqual(shipped, patched, "用例自身必须真的删掉了那个键");

        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(patched),
            "🔴 缺 wave_interval_rounds ⇒ 启动即报错（修复前代码里 `= 3` 静默生效）");
    }
}
