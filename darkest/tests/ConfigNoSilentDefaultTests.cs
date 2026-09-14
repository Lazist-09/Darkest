using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **数字外置（P29）· 三个配置的"记录默认值"清除** 的回归防线：
/// `RosterConfig.Morale = 50` ／ `EnemyAiConfig.TauntWeight = 3` ／ `UnlocksConfig.RosterBaseCap = 8`
/// （外加 `RosterBaseCap` 属性里的 `?? 8`）—— 都是**缺键即静默取默认值**的形态 ⚠️
/// ⇒ 现已：① 去默认值 ② Parse 首行 `DataPresence.RequireKeys` 断言键存在
/// ⇒ 本用例锁：**缺键必报错**（并点名缺的键）、**出厂数据必通过** ✓（值不变 = 零数值改动）
/// </summary>
[TestClass]
public sealed class ConfigNoSilentDefaultTests
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
    public void ShippedConfigs_Pass_AndValuesComeFromData()
    {
        RosterConfig roster = RosterConfig.Parse(ReadData("roster.json"));
        EnemyAiConfig ai = EnemyAiConfig.Parse(ReadData("enemy_ai.json"));
        // ⚠️ 校验 `unlocks.json` 必须传【真实目录】（建筑/Curio），否则 `building:tavern` 这类引用会被判"不存在"
        //    —— 那是 P27 ④ 的正确行为，不是缺陷 ✓（我第一次忘传 ⇒ 用例自己红了）
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));
        UnlocksConfig unlocks = UnlocksConfig.Parse(ReadData("unlocks.json"),
            HeirloomConfig.AllowedBuildings.ToHashSet(StringComparer.Ordinal),
            curios.RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal),
            rosterHardCap: 12);

        Assert.IsTrue(roster.Heroes.All(h => h.Morale is >= 0 and <= 100), "每个英雄的士气都来自数据 ✓");
        Assert.IsTrue(ai.TauntWeight > 0, "嘲讽权重来自数据 ✓");
        Assert.IsTrue(unlocks.RosterBaseCap > 0, "起手名册上限来自数据（不再是 `?? 8`）✓");
    }

    [TestMethod]
    public void RookieMorale_IsContractDefault_NotASilentFallback()
    {
        // 🔴 纠正：`Morale = RookieMorale` 是 **P22⑦ 契约**（新兵入场士气必须 = 50，校验器强制），
        //    不是"静默兜底" ⇒ 数据里新兵**可以不写** morale，此时取契约值 ✓（我一度判错并打红了 3 条既有用例）
        RosterConfig roster = RosterConfig.Parse(ReadData("roster.json"));
        Assert.IsTrue(roster.Heroes.All(h => h.Morale is >= 0 and <= 100), "士气值域合法 ✓");
        foreach (HeroConfig rookie in roster.Heroes.Where(h => h.Level == 1))
        {
            Assert.AreEqual(RosterConfig.RookieMorale, rookie.Morale,
                "P22⑦：新兵入场士气必须是契约值（数据缺 morale ⇒ 取 RookieMorale）✓");
        }
    }

    [TestMethod]
    public void MissingTauntWeight_Throws()
    {
        string patched = ReadData("enemy_ai.json").Replace("\"taunt_weight\": 3", "\"taunt_weight_moved\": 3");
        Assert.ThrowsException<InvalidDataException>(() => EnemyAiConfig.Parse(patched),
            "🔴 缺 taunt_weight ⇒ 启动即报错（修复前静默取 3）");
    }

    [TestMethod]
    public void MissingRosterBaseCap_Throws()
    {
        string patched = ReadData("unlocks.json").Replace("\"roster_base_cap\": 8", "\"roster_base_cap_moved\": 8");
        Assert.ThrowsException<InvalidDataException>(() => UnlocksConfig.Parse(patched),
            "🔴 缺 roster_base_cap ⇒ 启动即报错（修复前静默取 8，且属性里还有 `?? 8` 第二重兜底）");
    }
}
