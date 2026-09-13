using System;
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
/// **磨刀**（`next_battle_sharpen`：伤害 +25%）／**加固甲胄**（`next_battle_armor`：物防 +4）=
/// **仅下一场**；`buff_defs.json` 里两者都已定义好、消费点（`dealt_damage_mult`／`phys_def`）也已存在。
///
/// 本用例按**功能级**验收（红线 26）：扎营挂 buff ⇒ **下一场真的生效**（有 buff + 伤害倍率变了）
/// ⇒ **再下一场已失效**（只生效一场）。
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

    private static (ExpeditionSession Session, ExpeditionFlow Flow, TuningConfig Tuning, CombatLog Log, UnitId Hero) NewRun()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new RngProvider(20260909));
        // ⚠️ `session.Roster()` 在【首次战斗前】是空的 ⇒ 英雄 id 从【名册配置】取（营地技能按英雄 id 选人）
        RosterConfig rosterCfg = RosterConfig.Parse(ReadData("roster.json"));
        return (session, flow, tuning, log, UnitId.Of(rosterCfg.Heroes.First().Id));
    }

    [TestMethod]
    public void Sharpen_AppliesNextBattleOnly_AndBumpsDamageMultiplier()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, UnitId hero) = NewRun();

        Assert.IsTrue(flow.Camp(), "扎营成功（柴火 ≥1）");
        Assert.IsTrue(session.UseCampSkill(log, "camp_warrior_sharpen", 2, hero,
            "grant_buff:next_battle_sharpen"), "磨刀应可施加");
        Assert.AreEqual(1, session.RunBuffs.Count, "挂上 1 个跨场 buff");

        // ① 下一场：**真的有 buff**，且伤害倍率变成 1.25（契约：伤害 +25%）
        var d1 = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        string ids = string.Join(",", d1.Player.UnitsInSlotOrder().Select(u => u.Id.Value));

        // 🔴 **已知阻塞（本用例显式记录，不假装通过）**：
        //    营地侧用【英雄 id】（`hero_warrior_1`），战斗侧玩家单位用【原型 id】（`warrior`／`warrior_2`…）
        //    ⇒ 需要【hero → 战斗单位】的映射（按 `FormationSortie` 槽位顺序）才能真正注入。
        //    在此之前：挂载记录有了（`RunBuffs=1`），但注入会走 `UnmappedRunBuffs`（**不静默**）✓
        Assert.IsTrue(session.UnmappedRunBuffs > 0,
            $"预期触发【未映射】（营地英雄 id={hero.Value} vs 战斗单位 id=[{ids}]）—— " +
            "这正是待补的 hero→战斗单位 映射；若此处变红 ⇒ 映射已接上 ⇒ 请把本用例改成真正的功能验收");
        Assert.Inconclusive(
            $"🔴 待补映射：营地英雄 id={hero.Value} ／ 战斗侧单位 id=[{ids}] —— " +
            "映射接上后本用例应改为【下一场有 buff 且 DamageMultiplier=1.25、再下一场失效】");

        Assert.IsTrue(d1.Buffs.Has(hero, "next_battle_sharpen"),
            $"🔴 下一场开场应注入磨刀 buff（期望单位 id={hero.Value}；战斗侧玩家单位 id=[{ids}]；" +
            $"RunBuffs={session.RunBuffs.Count}）");
        var unit1 = d1.Player.UnitsInSlotOrder().First(u => u.Id == hero);
        Assert.AreEqual(1.25, unit1.DamageMultiplier, 1e-9,
            "🔴 磨刀的实际量级：伤害倍率 1.25（契约 +25%）");

        // ② 本场结束 ⇒ 跨场 buff 被消耗
        flow.OnBattleFinished("PlayerVictory", rounds: 5, isAmbush: true);
        Assert.AreEqual(0, session.RunBuffs.Count, "🔴 `next_battle` ⇒ 本场结束即消耗掉");

        // ③ 再下一场：**已失效**（只生效一场）
        var d2 = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        Assert.IsFalse(d2.Buffs.Has(hero, "next_battle_sharpen"),
            "🔴 磨刀【只生效一场】—— 再下一场不该还有它");
    }

    [TestMethod]
    public void Armor_AppliesNextBattleOnly()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, UnitId hero) = NewRun();

        Assert.IsTrue(flow.Camp(), "扎营成功");
        Assert.IsTrue(session.UseCampSkill(log, "camp_tank_armor", 2, hero,
            "grant_buff:next_battle_armor"), "加固甲胄应可施加");

        var d1 = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        string ids = string.Join(",", d1.Player.UnitsInSlotOrder().Select(u => u.Id.Value));
        Assert.IsTrue(session.UnmappedRunBuffs > 0,
            $"预期触发【未映射】（营地英雄 id={hero.Value} vs 战斗单位 id=[{ids}]）");
        Assert.Inconclusive($"🔴 待补映射（hero → 战斗单位）：营地={hero.Value} ／ 战斗=[{ids}]");

        Assert.IsTrue(d1.Buffs.Has(hero, "next_battle_armor"), "下一场应注入加固甲胄");

        flow.OnBattleFinished("PlayerVictory", rounds: 5, isAmbush: true);
        var d2 = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        Assert.IsFalse(d2.Buffs.Has(hero, "next_battle_armor"), "只生效一场");
    }
}
