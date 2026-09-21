using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Darkest.Core.Math;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M1c 阶段 3 的准备步：逐技能【差异表】**（架构三步走的"前后读数对照" ✓ · **不切默认** ✓）。
///
/// 它回答的是策划做决定时要看的那一个问题：
///   「把伤害的**基础值来源**从 `attack × 技能倍率` 换成 **武器区间 × (1 + 技能 dmg%)** 之后，
///     每个技能的原始伤害会变成多少？」✓
///
/// 🔴 **两个刻意的口径选择（都是为了让差异"干净"）**：
///   ① **只用 `dmg% = 0`** ⇒ 只隔离"**基础值从哪来**"这一个变化；技能 dmg% 的值**属策划**（未给 ⇒ 我不编 ✓）
///   ② **中性帧**（不吃减伤/暴击/浮动/增益）⇒ 对照的是**模型形状**，不是平衡 ✓
///
/// 🔴 **零行为**：只读 `skills.json` / `units.json` + 调纯函数 ⇒ 不改数值、不接生产路径 ✓
/// </summary>
[TestClass]
public sealed class M1cStage3DiffTableTests
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
    public void PrintsPerSkillDiff_CurrentBaseValueVsWeaponRange()
    {
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        JsonDocument skillsDoc = JsonDocument.Parse(ReadData("skills.json"));
        JsonElement skills = skillsDoc.RootElement.ValueKind == JsonValueKind.Array
            ? skillsDoc.RootElement
            : skillsDoc.RootElement.GetProperty("skills");

        var rows = new List<string> { "[M1c·阶段3] 技能 ⇒ 当前基础值（attack×Σ倍率）· 新基础值（武器区间中点，dmg%=0）· 差" };
        int counted = 0, tier0Worse = 0, tier4Worse = 0;

        foreach (JsonElement s in skills.EnumerateArray())
        {
            string id = s.GetProperty("id").GetString()!;
            string owner = s.TryGetProperty("owner_unit", out JsonElement o) ? o.GetString() ?? "" : "";
            if (!s.TryGetProperty("damage", out JsonElement dmg)
                || !dmg.TryGetProperty("segments", out JsonElement segs)
                || segs.ValueKind != JsonValueKind.Array
                || segs.GetArrayLength() == 0)
            {
                continue;
            }

            // 技能的"倍率之和"（当前口径里 attack 就是被乘的基础值 ✓）
            double sumMult = 0;
            foreach (JsonElement seg in segs.EnumerateArray())
            {
                sumMult += seg.TryGetProperty("multiplier", out JsonElement m) ? m.GetDouble() : 0;
            }

            UnitConfig? u = units.Units.FirstOrDefault(x => x.Id == owner && x.Weapon is not null);
            if (u?.Weapon is null)
            {
                rows.Add($"[M1c·阶段3] {id,-26} 跳过（owner \"{owner}\" 无武器区间 ⇒ 敌方/无主技能）✓");
                continue;
            }

            double now0 = BattleMath.ApplyDamageRounding(u.Attack * sumMult);
            double new0 = BattleMath.ApplyDamageRounding(
                BattleMath.WeaponRawDamage(u.Weapon[0].DmgMin, u.Weapon[0].DmgMax, 0, 0.5));
            double new4 = BattleMath.ApplyDamageRounding(
                BattleMath.WeaponRawDamage(u.Weapon[4].DmgMin, u.Weapon[4].DmgMax, 0, 0.5));

            if (new0 < now0) { tier0Worse++; }
            if (new4 < now0) { tier4Worse++; }
            counted++;

            rows.Add($"[M1c·阶段3] {id,-26} Σ倍率 {sumMult:0.##} · 当前 {now0,5:0} │ tier0 新 {new0,5:0} (Δ{new0 - now0,+6:0}) │ tier4 新 {new4,5:0} (Δ{new4 - now0,+6:0})");
        }

        rows.Add($"[M1c·阶段3] 参与对照 {counted} 个技能 · tier0 变低 {tier0Worse} 个 · tier4 变低 {tier4Worse} 个");
        rows.Add("[M1c·阶段3] ⚠️ 技能 dmg% 仍属策划（本表按 **0%** 算，只为隔离『基础值来源』这一步 ✓）");
        foreach (string r in rows)
        {
            Console.WriteLine(r);
        }

        Assert.IsTrue(counted > 0, "至少要有 1 个带武器的主程序技能参与对照 ✓");
        Assert.IsTrue(tier4Worse <= tier0Worse,
            "🔴 高阶武器不该比低阶更差（同 dmg% 下）⇒ 否则说明区间数据有问题 ✓");
        TestContext.WriteLine("[M1c·阶段3] 逐技能差异表已输出 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
