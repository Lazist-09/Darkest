using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M7 数据门禁（P20 ④/⑥/⑦ 的数据面）：
/// ④ `camp_skills.json` = **v0.84 的 12 条**（四原型各 3）、`cost ≥ 1`、`target ∈ {{single_ally, team}}`、owner 合法；
/// ⑥ `morale_events` 撤退两档与 `tuning.retreat` **对账一致**（scope=survivors）；
/// ⑦ `deaths_door_recovery.duration == until_next_recovery`（"到下次恢复"）。
/// </summary>
[TestClass]
public sealed class ExpeditionDataGateTests
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

    private sealed record CampSkill(string Id, string Name, string? OwnerUnit, int Cost, string Target, string Effect);

    private static List<CampSkill> CampSkills()
    {
        using JsonDocument doc = JsonDocument.Parse(ReadData("camp_skills.json"));
        return doc.RootElement.GetProperty("camp_skills").EnumerateArray()
            .Select(e => new CampSkill(
                e.GetProperty("id").GetString()!,
                e.GetProperty("name").GetString()!,
                e.TryGetProperty("owner_unit", out JsonElement o) ? o.GetString() : null,
                e.GetProperty("cost").GetInt32(),
                e.GetProperty("target").GetString()!,
                e.GetProperty("effect").GetString()!))
            .ToList();
    }

    [TestMethod]
    public void P20_CampSkills_TwelveEntries_ThreePerArchetype_ShapeValid()
    {
        List<CampSkill> skills = CampSkills();
        Assert.AreEqual(12, skills.Count, "v0.84：四原型 × 3 = 12 条（取代旧「恰 3 条」口径）");

        string[] archetypes = { "warrior", "tank", "medic", "commissar" };
        foreach (string a in archetypes)
        {
            Assert.AreEqual(3, skills.Count(s => s.OwnerUnit == a), $"原型 {a} 恰 3 个扎营技能（E3）");
        }

        foreach (CampSkill s in skills)
        {
            Assert.IsTrue(archetypes.Contains(s.OwnerUnit), $"\"{s.Id}\" owner_unit 必须是玩家原型（实际 {s.OwnerUnit}）");
            Assert.IsTrue(s.Cost >= 1, $"\"{s.Id}\" cost 必须 ≥ 1（P20 ④；实际 {s.Cost}）");
            Assert.IsTrue(s.Target is "single_ally" or "team", $"\"{s.Id}\" target ∈ {{single_ally, team}}（实际 {s.Target}）");
            Assert.IsFalse(string.IsNullOrWhiteSpace(s.Effect), $"\"{s.Id}\" 必须有 effect");
        }

        // 磨刀/加固甲胄必须是 **buff 授予**（走台账 + next_battle，不得由会话另存）
        Assert.AreEqual("grant_buff:next_battle_sharpen", skills.Single(s => s.Id == "camp_warrior_sharpen").Effect,
            "磨刀 → 授予 next_battle_sharpen（下一战伤害 +25%）");
        Assert.AreEqual("grant_buff:next_battle_armor", skills.Single(s => s.Id == "camp_tank_armor").Effect,
            "加固甲胄 → 授予 next_battle_armor（下一战物防 +4）");
    }

    [TestMethod]
    public void P20_Retreat_TwoTiers_ReconcileWithTuning()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        MoraleEventsConfig morale = MoraleEventsConfig.Parse(ReadData("morale_events.json"));

        MoraleEventConfig noDeath = morale.Get("retreat_success");
        MoraleEventConfig withDeath = morale.Get("retreat_success_with_death");

        Assert.AreEqual(-12, noDeath.Delta, "无死亡档 −12（#240 推翻 #43 的 −10）");
        Assert.AreEqual(-15, withDeath.Delta, "有死亡档 −15");
        Assert.AreEqual(MoraleEventScope.Survivors, noDeath.Scope, "只作用于存活者（scope=survivors）");
        Assert.AreEqual(MoraleEventScope.Survivors, withDeath.Scope);

        Assert.AreEqual(-noDeath.Delta, tuning.Expedition.RetreatPenalty.NoDeath, "P20 ⑥ 对账：tuning ↔ morale_events");
        Assert.AreEqual(-withDeath.Delta, tuning.Expedition.RetreatPenalty.WithDeath, "P20 ⑥ 对账（有死亡档）");
        Assert.AreEqual("survivors", tuning.Retreat.Scope);
    }

    [TestMethod]
    public void P20_DeathsDoorRecovery_Duration_IsUntilNextRecovery()
    {
        BuffDefsConfig defs = BuffDefsConfig.Parse(ReadData("buff_defs.json"));
        BuffDefConfig dd = defs.Buffs.Single(b => b.Id == "deaths_door_recovery");
        Assert.AreEqual(BuffDurationType.UntilNextRecovery, dd.Duration.Type,
            "P20 ⑦ / P18 ⑦：deaths_door_recovery.duration = until_next_recovery（到下次恢复=扎营/回城）");

        BuffDefConfig sharpen = defs.Buffs.Single(b => b.Id == "next_battle_sharpen");
        BuffDefConfig armor = defs.Buffs.Single(b => b.Id == "next_battle_armor");
        Assert.AreEqual(BuffDurationType.NextBattle, sharpen.Duration.Type, "磨刀：next_battle");
        Assert.AreEqual(BuffDurationType.NextBattle, armor.Duration.Type, "加固甲胄：next_battle");
        Assert.IsTrue(sharpen.Modifiers!.Any(m => m.Effect == "dealt_damage_mult" && m.Percent == 25), "磨刀 +25% 伤害");
        Assert.IsTrue(armor.Modifiers!.Any(m => m.Effect == "phys_def" && m.Value == 4), "加固甲胄 +4 物防");
    }
}
