using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **扎营技能的加载级防线**（红线 21 的既有做法，照 `BuffDefsConfig.ConsumedEffectNames`）：
/// 每个 `effect` 名必须登记为【已接线】或【阶段二没落点】之一 ⇒ 否则**启动即报错**。
///
/// 立条依据（**实测审计**）：`camp_skills.json` 12 个技能 / 9 种 effect 名，
/// **只有 `ambush_immunity_once` 有消费点**；其余 8 种在全仓没有实现
/// （`grant_buff:next_battle_*` 在 `buff_defs.json` 有定义、但**无消费**）。
/// </summary>
[TestClass]
public sealed class CampSkillsConfigTests
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
    public void ShippedCampSkills_AllEffectsRegistered_PartitionMatchesAudit()
    {
        CampSkillsConfig cfg = CampSkillsConfig.Parse(ReadData("camp_skills.json"));

        Assert.AreEqual(12, cfg.Skills.Count, "12 个扎营技能");
        Assert.IsTrue(CampSkillsConfig.PartitionsAreDisjoint,
            "🔴 两个清单必须【互斥】（同一 effect 名不得既已接线又阶段二 —— 我踩过一次：导致计数虚高）");
        Assert.AreEqual(6, cfg.ConsumedCount,
            "🔴 已接线 = 6 个技能：守夜 ／ 站岗（ambush_immunity_once）＋ 磨刀 ／ 操练（next_battle_sharpen）" +
            "＋ 加固甲胄（next_battle_armor）＋ 打气（battles:4）");
        Assert.AreEqual(6, cfg.DeferredCount, "其余 6 个（笑谈 ／ 埋锅造饭 ／ 动员 ／ 包扎 ／ 配药 ／ 照料）");
        Assert.AreEqual(cfg.Skills.Count, cfg.ConsumedCount + cfg.DeferredCount, "每个 effect 名都必须被登记（防线）");
    }

    [TestMethod]
    public void UnregisteredEffectName_ThrowsOnLoad()
    {
        string raw = ReadData("camp_skills.json");
        string tampered = raw.Replace("\"effect\": \"heal_5_percent\"",
            "\"effect\": \"brand_new_effect_nobody_implements\"", StringComparison.Ordinal);
        Assert.AreNotEqual(raw, tampered, "（前置）替换应生效");

        Assert.ThrowsException<InvalidDataException>(
            () => CampSkillsConfig.Parse(tampered),
            "🔴 未登记的 effect 名 ⇒ 启动即报错（防止「新增 effect 名但没人实现」再次发生）");
    }
}
