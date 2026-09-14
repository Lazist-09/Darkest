using System;
using System.IO;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **数据键存在性**（数字外置纪律 `P29` / 用户 2026-09-14）：
/// C# 记录默认值（`int BattleGoal = 3`）会让 **JSON 缺键时静默取 3** ⇒ 策划改不动 + 缺键错误永不暴露 ⚠️
/// ⇒ 现用 `DataPresence.RequireKeys` 在原始 JSON 上断言"必需键存在"，并去掉对应默认值 ✓
/// 本用例锁：**缺键必报错**（且报错信息里点名那个键）、**有键必通过** ✓
/// </summary>
[TestClass]
public sealed class DataPresenceTests
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
    public void RequireKeys_PassesWhenPresent_ThrowsWhenMissing()
    {
        DataPresence.RequireKeys("test.json", """{ "a": 1, "nested": { "b": 2 } }""", "a", "b");

        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => DataPresence.RequireKeys("test.json", """{ "a": 1 }""", "a", "b"));
        Assert.IsTrue(ex.Message.Contains("b", StringComparison.Ordinal), "报错信息必须点名缺的那个键 ✓");
    }

    [TestMethod]
    public void Tuning_BattleGoal_MissingKeyThrows_NotSilentlyDefaulted()
    {
        string shipped = ReadData("tuning.json");
        string patched = shipped.Replace("\"battle_goal\": 3", "\"battle_goal_moved\": 3");
        Assert.AreNotEqual(shipped, patched, "用例自身必须真的改掉了那个键名");

        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(patched),
            "🔴 缺 battle_goal ⇒ 启动即报错（修复前会**静默取记录默认值 3**）");
        Assert.IsTrue(ex.Message.Contains("battle_goal", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Tuning_VirtueInspiredMoralePerTurn_MissingKeyThrows()
    {
        string shipped = ReadData("tuning.json");
        string patched = shipped.Replace("\"virtue_inspired_morale_per_turn\": 3",
            "\"virtue_inspired_morale_per_turn_moved\": 3");
        Assert.AreNotEqual(shipped, patched, "用例自身必须真的改掉了那个键名");

        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(patched),
            "🔴 缺 virtue_inspired_morale_per_turn ⇒ 启动即报错（修复前静默取 3）");
    }

    [TestMethod]
    public void ShippedTuning_HasAllRequiredKeys()
    {
        TuningConfig cfg = TuningConfig.Parse(ReadData("tuning.json")); // 不抛 = 必需键齐全 ✓
        Assert.IsTrue(cfg.Expedition.BattleGoal > 0, "battle_goal 来自数据且 > 0");
    }
}
