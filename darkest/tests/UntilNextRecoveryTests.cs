using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **`until_next_recovery` 的完整生命周期**（契约 `m7_expedition.md:160` ① / `O-67`）：
/// 「到下次恢复（**扎营**/回城）」⇒ `deaths_door_recovery` **扎营清它**。
///
/// 🔴 本用例是为补一条**实测缺口**而写：此前【只有回城】清它 ⇒ **扎营后下一场仍带着后遗症** ⚠️
/// （`RunSession:133` 会在下一场把 buff 挂回去，而 `StartCamp`/`EndCamp` 都不清保留集合）
/// ⇒ 现在 `EndCamp` 清 ⇒ 本用例锁住这条链：**记 → 下一场生效 → 扎营清 → 再下一场不再有**。
/// </summary>
[TestClass]
public sealed class UntilNextRecoveryTests
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

    private static ExpeditionSession NewSession(TuningConfig tuning, int firewood = 2)
        => new(l => MonteCarlo.HeadlessDriver.NewDirector(l), tuning.Expedition.NBattles, firewood, 3,
            tuning.Expedition.AmbushChance);

    [TestMethod]
    public void DeathsDoorRecovery_SurvivesBattles_ButIsClearedByCamping()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionSession session = NewSession(tuning);
        var log = new CombatLog();

        // ① 第 1 场：让一个单位带着【死门后遗症】结束本场 ⇒ 会话把它记入跨场保留
        var d1 = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        UnitRuntime hero = d1.Player.UnitsInSlotOrder().First();
        d1.Buffs.Add(hero.Id, "deaths_door_recovery", source: null);
        session.EndBattle(d1, 1, "PlayerVictory", rounds: 5);

        // ② 第 2 场：**后遗症跨场保留**（buff 挂回来 + 速度 −1 —— `RunSession:133~137`）
        var d2 = session.BeginExpeditionBattle(2, log, tuning.Expedition.DifficultyTiers);
        UnitRuntime hero2 = d2.Player.UnitsInSlotOrder().First(u => u.Id == hero.Id);
        Assert.IsTrue(d2.Buffs.Has(hero2.Id, "deaths_door_recovery"), "跨场保留：第 2 场应带【死门后遗症】");
        Assert.IsTrue(hero2.SpeedMod <= -1, "契约：死门后遗症在本场也施加（速度 −1）");

        // ③ **扎营**（契约：到下次恢复 = 扎营/回城 ⇒ **扎营清它**）
        Assert.IsTrue(session.StartCamp(log, campIndex: 1, respiteBase: 6), "扎营应可开始（柴火 ≥1）");
        session.EndCamp(log);

        // ④ 第 3 场：**不再带后遗症**（扎营已清）
        var d3 = session.BeginExpeditionBattle(3, log, tuning.Expedition.DifficultyTiers);
        UnitRuntime hero3 = d3.Player.UnitsInSlotOrder().First(u => u.Id == hero.Id);
        Assert.IsFalse(d3.Buffs.Has(hero3.Id, "deaths_door_recovery"),
            "🔴 契约：`until_next_recovery` ⇒ **扎营清它** ⇒ 扎营后下一场不该再有（本用例为补此缺口而写）");
    }

    [TestMethod]
    public void CampClearing_IsAuditable()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionSession session = NewSession(tuning);
        var log = new CombatLog();

        var d1 = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers);
        UnitRuntime hero = d1.Player.UnitsInSlotOrder().First();
        d1.Buffs.Add(hero.Id, "deaths_door_recovery", source: null);
        session.EndBattle(d1, 1, "PlayerVictory", rounds: 5);

        session.StartCamp(log, campIndex: 1, respiteBase: 6);
        session.EndCamp(log);

        Assert.IsTrue(log.Events.OfType<EffectEvent>()
                .Any(e => e.EffectType.StartsWith("camp_cleared_until_next_recovery", StringComparison.Ordinal)),
            "扎营清【到下次恢复】类状态应当**留痕**（可审计，不静默）");
    }
}
