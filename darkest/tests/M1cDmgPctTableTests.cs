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
/// 🔴 **M1c 阶段 3 的【取值表】**：每个技能**要填多少 `dmg%` 才能复现今天的伤害** ✓
///
/// WHY 这张表能帮策划做决定（而不是我替他填数）：
///   新模型是 `武器区间 × (1 + 技能dmg%)`；今天的基础值是 `attack × Σ段倍率` ✓
///   令两者在**武器区间中点**上相等 ⇒ `dmg% = 今天 / 武器中点 − 1` ✓
///   ⇒ 策划只要看这张表就能回答："**我要保持现在的平衡吗？**"（是 ⇒ 照抄；否 ⇒ 按新意图给别的值）✓
///
/// 🔴 **零行为**：只读 `skills.json`/`units.json` + 调纯函数 ⇒ 不改任何数值、不接生产路径 ✓
/// 🔴 表里 **tier0 / tier4 各一列**（武器区间随阶变化 ⇒ 同一个 dmg% 在两阶上的复现程度不同 ✓）
/// </summary>
[TestClass]
public sealed class M1cDmgPctTableTests
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
    public void PrintsDmgPctNeededToReproduceTodaysDamage()
    {
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        JsonDocument doc = JsonDocument.Parse(ReadData("skills.json"));
        JsonElement skills = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.GetProperty("skills");

        var rows = new List<string>
        {
            "[M1c·取值表] 技能 ⇒ 今天的基础值 · 武器中点(tier0/tier4) · **复现今天所需 dmg%**（tier0/tier4）",
        };
        int counted = 0;

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

            UnitConfig? u = units.Units.FirstOrDefault(x => x.Id == owner && x.Weapon is not null);
            if (u?.Weapon is null)
            {
                continue;
            }

            double sumMult = 0;
            foreach (JsonElement seg in segs.EnumerateArray())
            {
                sumMult += seg.TryGetProperty("multiplier", out JsonElement m) ? m.GetDouble() : 0;
            }

            double today = u.Attack * sumMult;
            double mid0 = (u.Weapon[0].DmgMin + u.Weapon[0].DmgMax) / 2.0;
            double mid4 = (u.Weapon[4].DmgMin + u.Weapon[4].DmgMax) / 2.0;

            // dmg% 使 武器中点 ×(1+pct/100) == 今天 ⇒ pct = (今天/中点 − 1)×100
            double pct0 = (today / mid0 - 1.0) * 100.0;
            double pct4 = (today / mid4 - 1.0) * 100.0;

            counted++;
            rows.Add($"[M1c·取值表] {id,-28} 今天 {today,6:0.#} │ tier0 中点 {mid0,5:0.#} ⇒ **{pct0,+7:0.#}%** │ tier4 中点 {mid4,5:0.#} ⇒ **{pct4,+7:0.#}%**");
        }

        rows.Add($"[M1c·取值表] 共 {counted} 个技能 ⇒ **这就是「保持今天平衡」所需的 dmg% 表**（策划照抄或另给 ✓）");
        rows.Add("[M1c·取值表] ⚠️ 两列不同：同一 dmg% 在低阶/高阶的复现度不同 ⇒ 若要「跨阶一致」，需要的是**区间比例**而不是单一 dmg% ✓");
        foreach (string r in rows)
        {
            Console.WriteLine(r);
        }

        Assert.IsTrue(counted > 0, "至少 1 个技能参与 ✓");
        TestContext.WriteLine("[M1c·取值表] 已输出 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
