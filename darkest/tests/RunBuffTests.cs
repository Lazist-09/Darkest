using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **补欠账：跨场 buff（`next_battle` 类）** —— 契约 `m7_expedition.md:160`（三类型之二）：
/// **磨刀**（`next_battle_sharpen`：伤害 +25%）／**加固甲胄**（`next_battle_armor`：物防 +4）= **仅下一场**。
///
/// 🔴 **本用例同时锁住一条实测事实（两套 id 体系）**：
/// 营地侧用**英雄 id**（`hero_warrior_1`），战斗侧玩家单位用**原型 id**（`warrior` ／ `warrior_2` …）
/// ⇒ 跨场 buff 必须**按阵型槽位**挂载（`BindSortie`），注入时用 `Player.UnitRuntimeAt(slot)` 换成本场单位 ✓
/// </summary>
[TestClass]
public sealed class RunBuffTests
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

    private static (ExpeditionSession Session, ExpeditionFlow Flow, TuningConfig Tuning, CombatLog Log,
        UnitId Hero, int Slot) NewRun()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        RosterConfig roster = RosterConfig.Parse(ReadData("roster.json"));
        FormationConfig template = FormationConfig.Parse(ReadData("formation.json"));
        IReadOnlyList<HeroConfig> sortie = FormationSortie.SelectForTemplate(template, roster);

        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);

        // 🔴 与组合根同一套映射：英雄 id → 阵型槽位（`FormationSortie` 的顺序 = 模板槽位顺序）
        var heroSlots = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<RosterEntryConfig> playerSlots = template.InitialRoster.Player;
        for (int i = 0; i < sortie.Count && i < playerSlots.Count; i++)
        {
            heroSlots[sortie[i].Id] = playerSlots[i].Slot;
        }

        session.BindSortie(heroSlots);

        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new RngProvider(20260909));
        return (session, flow, tuning, log, UnitId.Of(sortie[0].Id), playerSlots[0].Slot);
    }

    [TestMethod]
    public void Sharpen_AppliesNextBattleOnly_AndBumpsDamageMultiplier()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, UnitId hero, int slot) = NewRun();

        Assert.IsTrue(flow.Camp(), "扎营成功（柴火 ≥1）");
        Assert.IsTrue(session.UseCampSkill(log, "camp_warrior_sharpen", 2, hero,
            "grant_buff:next_battle_sharpen"), "磨刀应可施加");
        Assert.AreEqual(1, session.RunBuffs.Count, "挂上 1 个跨场 buff");
        Assert.AreEqual(slot, session.RunBuffs[0].Slot, "🔴 按【槽位】记账（hero→战斗单位映射）");

        // ① 下一场：**真的有 buff**，且伤害倍率 = 1.25（契约 +25%）
        var d1 = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        Assert.AreEqual(0, session.UnmappedRunBuffs, "映射已接 ⇒ 不应有未映射");
        var unit1 = d1.Player.UnitRuntimeAt(slot);
        Assert.IsNotNull(unit1, $"槽位 {slot} 应有单位");
        Assert.IsTrue(d1.Buffs.Has(unit1!.Id, "next_battle_sharpen"), "🔴 下一场开场应注入磨刀 buff");
        // ⚠️ 量级要读【消费点的输入值】：`dealt_damage_mult` 由 `DamageStep` 在 `raw` 处消费
        //    （`unit.DamageMultiplier` 是【特质层】，不含 buff 层 ⇒ 在这里读到 1 是正常的）
        Assert.AreEqual(25, d1.Buffs.PercentMod(unit1.Id, "dealt_damage_mult"), 1e-9,
            "🔴 磨刀的实际量级：`dealt_damage_mult` = +25（`DamageStep` 的 `raw` 处乘 1.25）");

        // ② 本场结束 ⇒ 跨场 buff 被消耗
        flow.OnBattleFinished("PlayerVictory", rounds: 5, isAmbush: true);
        Assert.AreEqual(0, session.RunBuffs.Count, "🔴 `next_battle` ⇒ 本场结束即消耗掉");

        // ③ 再下一场：**已失效**
        var d2 = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        var unit2 = d2.Player.UnitRuntimeAt(slot);
        Assert.IsFalse(d2.Buffs.Has(unit2!.Id, "next_battle_sharpen"),
            "🔴 磨刀【只生效一场】—— 再下一场不该还有它");
    }

    [TestMethod]
    public void Armor_AppliesNextBattleOnly()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, UnitId hero, int slot) = NewRun();

        Assert.IsTrue(flow.Camp(), "扎营成功");
        Assert.IsTrue(session.UseCampSkill(log, "camp_tank_armor", 2, hero,
            "grant_buff:next_battle_armor"), "加固甲胄应可施加");

        var d1 = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        var unit1 = d1.Player.UnitRuntimeAt(slot);
        Assert.IsTrue(d1.Buffs.Has(unit1!.Id, "next_battle_armor"), "下一场应注入加固甲胄");

        flow.OnBattleFinished("PlayerVictory", rounds: 5, isAmbush: true);
        var d2 = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        var unit2 = d2.Player.UnitRuntimeAt(slot);
        Assert.IsFalse(d2.Buffs.Has(unit2!.Id, "next_battle_armor"), "只生效一场");
    }
}
