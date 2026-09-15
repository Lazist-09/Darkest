using System;
using System.Collections.Generic;
using System.IO;
using Darkest.Data;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **扎营技能测试工具**（`UseCampSkill` 新签名的配套）：
/// 新签名接的是**技能配置对象**（数字住 data：`camp_skills.json: effect_number`）⇒ 用例不该再手抄数字 ✓
/// 这里从**出厂 data** 取技能与 camp 配置 ⇒ 用例与生产**同一真相** ✓
/// </summary>
public static class CampSkillTestKit
{
    private static readonly Lazy<CampSkillsConfig> Skills = new(() => CampSkillsConfig.Parse(ReadData("camp_skills.json")));

    private static readonly Lazy<TuningConfig> Tuning = new(() => TuningConfig.Parse(ReadData("tuning.json")));

    /// <summary>出厂 `tuning.camp`（`pep_talk_battles` 等数字的来源）✓</summary>
    public static TuningCamp Camp => Tuning.Value.Camp!;

    /// <summary>按 id 取 `camp_skills.json` 里的技能（含 `effect_number`）✓</summary>
    public static CampSkillConfig Skill(string id)
    {
        foreach (CampSkillConfig s in Skills.Value.Skills)
        {
            if (s.Id == id)
            {
                return s;
            }
        }

        throw new KeyNotFoundException($"camp_skills.json 里没有技能 \"{id}\"。");
    }

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
}
