using System;
using System.IO;
using Darkest.Core.Math;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **数字外置（用户 2026-09-14）**：物理减免除数从 `BattleMath` 函数体搬到
/// `tuning.json` 的 `physical_mitigation.divisor` ⇒ 本用例是【**"改它就变"**】的取证：
///
/// ① **改数据 ⇒ 行为变**（策划只改 JSON、不动 C# 也能调平衡）；
/// ② **数据缺失/非法 ⇒ 启动即报错**（不许回落到代码里的默认值 —— 那正是本纪律禁止的"静默兜底"）；
/// ③ `BalanceTable` 必须忠实暴露该值（否则"数字住在 data"是假的）。
/// </summary>
[TestClass]
public sealed class PhysicalMitigationDataTests
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
    public void ShippedTuning_HasPhysicalMitigationDivisor_AndBalanceTableExposesIt()
    {
        TuningConfig cfg = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.IsTrue(cfg.PhysicalMitigation.Divisor > 0, "出厂 tuning 必须显式给出 physical_mitigation.divisor");

        BalanceTable table = BalanceTable.FromTuning(cfg);
        Assert.AreEqual(cfg.PhysicalMitigation.Divisor, table.PhysicalMitigationDivisor,
            "BalanceTable 必须忠实暴露该值（否则'数字住在 data'是假的）");
    }

    [TestMethod]
    public void ChangingTheDataKey_ChangesTheFormulaOutput()
    {
        // 🔴 只改 JSON 的一个数字 ⇒ 公式输出必须变（"改它就变"）
        string shipped = ReadData("tuning.json");
        int shippedDivisor = TuningConfig.Parse(shipped).PhysicalMitigation.Divisor;

        string patched = shipped.Replace(
            $"\"physical_mitigation\": {{ \"divisor\": {shippedDivisor} }}",
            "\"physical_mitigation\": { \"divisor\": 60 }");
        Assert.AreNotEqual(shipped, patched, "测试自身必须真的替换到了那个键（否则本用例无效）");

        TuningConfig varied = TuningConfig.Parse(patched);
        Assert.AreEqual(60, varied.PhysicalMitigation.Divisor);

        double withShipped = BattleMath.PhysicalMitigation(physDef: 8, divisor: shippedDivisor);
        double withVaried = BattleMath.PhysicalMitigation(physDef: 8, divisor: 60);
        Assert.AreNotEqual(withShipped, withVaried,
            "🔴 改数据必须改行为：8 点物防在 divisor=30 与 60 下的减免率必须不同");
        Assert.IsTrue(withVaried < withShipped, "除数变大 ⇒ 减免率变小（单调性）");
    }

    [TestMethod]
    public void MissingOrInvalidDivisor_ThrowsAtLoad_NoSilentFallback()
    {
        string shipped = ReadData("tuning.json");

        string missing = shipped.Replace("\"physical_mitigation\": { \"divisor\": 30 },", string.Empty);
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(missing),
            "缺少 physical_mitigation ⇒ 启动即报错（**不许**回落到代码默认值）");

        string zero = shipped.Replace("\"physical_mitigation\": { \"divisor\": 30 }",
            "\"physical_mitigation\": { \"divisor\": 0 }");
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(zero),
            "divisor ≤ 0 ⇒ 启动即报错");
    }
}
