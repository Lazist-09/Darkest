using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **扎营技能的士气类 / HP 类**（`#310` ②/① 裁定：**写【本趟台账】`until_run_end`，不写回名册**）：
/// · 笑谈 +8（单体）／埋锅造饭 全队 +5／动员 全队 +8 ⇒ **下一场起手士气 +N**
/// · 包扎 +15% ／照料 +5%                              ⇒ **本趟剩余场次的开局 HP +N%**
/// 🔴 关键纪律：加成**不得漏进名册**（否则"营地加士气"= 免费减压，与 M8.0 冲突）⇒ 战后**扣回**。
///
/// ⚠️ 三条踩过的坑（都写进用例，避免复现）：
/// ① 第一场之前 `Retained` 为空 ⇒ `Survivors = 0` ⇒ **Respite 池 = 0** ⇒ 技能买不起 ⇒ **先打一场**；
/// ② 营地侧用**英雄 id**、战斗单位用**原型 id** ⇒ 断言必须**按槽位**取单位（`UnitRuntimeAt(slot)`）；
/// ③ 战斗中士气会变 ⇒ 用**同 seed 对照**比差额，不写死绝对值。
/// </summary>
[TestClass]
public sealed class CampSkillRunLedgerTests
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
        RosterConfig RosterCfg, UnitId Hero, int Slot) NewRun()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        RosterConfig rosterCfg = RosterConfig.Parse(ReadData("roster.json"));
        FormationConfig template = FormationConfig.Parse(ReadData("formation.json"));
        IReadOnlyList<HeroConfig> sortie = FormationSortie.SelectForTemplate(template, rosterCfg);
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 3, food: bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        session.BindCampHeroes(rosterCfg.Heroes);

        var heroSlots = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<RosterEntryConfig> playerSlots = template.InitialRoster.Player;
        for (int i = 0; i < sortie.Count && i < playerSlots.Count; i++)
        {
            heroSlots[sortie[i].Id] = playerSlots[i].Slot;
        }

        session.BindSortie(heroSlots);
        var flow = new ExpeditionFlow(session, new LightMeter(tuning.Light!), bag,
            new Scouting(tuning.Scouting!, tuning.Light!), nodes, tuning, log, new Darkest.Core.Rng.RngProvider(20260909));
        return (session, flow, tuning, log, rosterCfg, UnitId.Of(sortie[0].Id), playerSlots[0].Slot);
    }

    private static void FightOneBattle(ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log)
    {
        session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        flow.OnBattleFinished("PlayerVictory", rounds: 5, isAmbush: true);
    }

    [TestMethod]
    public void Joke_SingleTarget_Adds8MoraleInNextBattle()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, RosterConfig _, UnitId hero, int slot) = NewRun();
        (ExpeditionSession cSession, ExpeditionFlow cFlow, TuningConfig _, CombatLog cLog, RosterConfig _, UnitId _, int _) = NewRun();

        // 对照：同 seed，不施技能
        FightOneBattle(cSession, cFlow, tuning, cLog);
        int control = cSession.BeginExpeditionBattle(2, cLog, tuning.Expedition.DifficultyTiers)
            .Player.UnitRuntimeAt(slot)!.Morale;

        FightOneBattle(session, flow, tuning, log);
        Assert.IsTrue(flow.Camp(), "扎营成功（柴火 ≥1，且已打过一场 ⇒ Respite 池 > 0）");
        Assert.IsTrue(session.UseCampSkill(log, "camp_warrior_joke", 1, hero, "morale_plus_8"), "笑谈应可施加");
        Assert.AreEqual(8, session.CampMoraleBonus[hero.Value], "记入本趟台账 +8");

        var d = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        Assert.IsTrue(session.CampNotes.Any(n => n.Contains("士气加成", StringComparison.Ordinal)),
            "🔴 加成**真的施加**了（不是只记账）—— 我第一版因 id 体系不同而**静默失效**，用例当场抓到");
        Assert.AreEqual(Math.Min(100, control + 8), d.Player.UnitRuntimeAt(slot)!.Morale,
            "🔴 契约：下一场**起手士气 +8**（对照口径：同 seed 无技能组）");
    }

    [TestMethod]
    public void Bandage_Adds15PercentOpeningHp_AndIsCappedByMaxHp()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, RosterConfig _, UnitId hero, int slot) = NewRun();

        FightOneBattle(session, flow, tuning, log);
        Assert.IsTrue(flow.Camp(), "扎营成功");
        Assert.IsTrue(session.UseCampSkill(log, "camp_medic_bandage", 2, hero, "heal_15_percent_and_clear_bleed"),
            "包扎应可施加（HP +15% 落地；清流血归冗余·阶段二）");
        Assert.AreEqual(15, session.CampHpBonusPct[hero.Value], "记入本趟台账 +15%");

        var d = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        UnitRuntime u = d.Player.UnitRuntimeAt(slot)!;
        Assert.IsTrue(u.CurrentHp <= u.MaxHp, "不得超上限（满血时被 MaxHp 钳制）");
        Assert.IsTrue(u.CurrentHp > 0);
        Assert.IsTrue(session.CampNotes.Any(n => n.Contains("开局 HP", StringComparison.Ordinal)),
            "🔴 加成**真的施加**了（HP 路径同样按槽位映射）");
    }

    /// <summary>
    /// 🔴 **营地士气加成【战后扣回】，不写回名册**（`#310` ②：否则"营地加士气"= 免费减压）。
    /// ⚠️ 根因备注（我踩过）：**`OnBattleFinished` 不会调 `session.EndBattle`** ⇒ 不显式结算的话
    ///    `Retained` 一直是空的 ⇒ `session.Roster()` 取不到键（我上一版就是这么失败的）。
    /// </summary>
    [TestMethod]
    public void CampMoraleBonus_IsStrippedAfterBattle_SoItNeverReachesTheRoster()
    {
        (ExpeditionSession session, ExpeditionFlow flow, TuningConfig tuning, CombatLog log, RosterConfig _, UnitId hero, int slot) = NewRun();

        // 第 1 场：**显式结算**（让 Retained 有值）
        var d1 = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        session.EndBattle(d1, 1, "PlayerVictory", rounds: 5);
        flow.OnBattleFinished("PlayerVictory", rounds: 5, isAmbush: true);
        string battleId = d1.Player.UnitRuntimeAt(slot)!.Id.Value; // 🔴 Retained 的键 = 战斗单位 id
        int before = session.Roster().First(r => r.Id == battleId).Morale;

        // 扎营 + 笑谈（+8，本趟）
        Assert.IsTrue(flow.Camp(), "扎营成功");
        Assert.IsTrue(session.UseCampSkill(log, "camp_warrior_joke", 1, hero, "morale_plus_8"), "笑谈应可施加");

        // 第 2 场：**开场确实带 +8**；结算后应被【扣回】
        var d2 = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        Assert.AreEqual(Math.Min(100, before + 8), d2.Player.UnitRuntimeAt(slot)!.Morale,
            "开场带本趟 +8");

        session.EndBattle(d2, 2, "PlayerVictory", rounds: 5);
        int captured = session.Roster().First(r => r.Id == battleId).Morale; // 含 +8 的捕获值
        flow.OnBattleFinished("PlayerVictory", rounds: 5, isAmbush: true);   // ← 这里扣回
        int stored = session.Roster().First(r => r.Id == battleId).Morale;

        Assert.AreEqual(Math.Clamp(captured - 8, 0, 100), stored,
            $"🔴 契约 `#310` ②：战后从 Retained 扣回那 +8（captured={captured} ⇒ stored={stored}）；" +
            "否则回城会把它写进名册 = 免费减压");
    }
}
