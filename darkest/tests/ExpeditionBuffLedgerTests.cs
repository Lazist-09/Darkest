using System;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Buffs;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// E3 台账注入（会话级、**单源**）：`next_battle` 类效果（磨刀/加固甲胄）由**同一份 buff 台账**跨场持有，
/// 会话**不另存**"下一场加成"；到期清除由该台账负责并写事件。
/// </summary>
[TestClass]
public sealed class ExpeditionBuffLedgerTests
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
    public void SharedLedger_IsTheSameInstance_AcrossBattleDirectors()
    {
        var sharedLog = new CombatLog();
        var ledger = new BuffLedger(BuffDefsConfig.Parse(ReadData("buff_defs.json")), sharedLog);

        Darkest.Gameplay.Sim.Director.BattleDirector d1 = HeadlessDriver.NewDirector(new CombatLog(), ledger);
        Darkest.Gameplay.Sim.Director.BattleDirector d2 = HeadlessDriver.NewDirector(new CombatLog(), ledger);

        Assert.IsTrue(ReferenceEquals(d1.Buffs, d2.Buffs), "两个导演**共用同一份台账**（单源，非各存一份）");
        Assert.IsTrue(ReferenceEquals(ledger, d1.Buffs));
    }

    [TestMethod]
    public void NextBattleBuff_GrantedInBattle1_IsVisibleInBattle2_AndWritesEvent()
    {
        var sharedLog = new CombatLog();
        var ledger = new BuffLedger(BuffDefsConfig.Parse(ReadData("buff_defs.json")), sharedLog);
        UnitId warrior = UnitId.Of("warrior");

        Darkest.Gameplay.Sim.Director.BattleDirector d1 = HeadlessDriver.NewDirector(new CombatLog(), ledger);
        ledger.Add(warrior, "next_battle_sharpen", source: null); // 扎营【磨刀】（E3）

        Darkest.Gameplay.Sim.Director.BattleDirector d2 = HeadlessDriver.NewDirector(new CombatLog(), ledger);
        Assert.IsTrue(d2.Buffs.Has(warrior, "next_battle_sharpen"),
            "**下一场仍生效**（跨场持久；会话未另存副本）");
        Assert.IsTrue(sharedLog.Events.OfType<BuffAppliedEvent>().Any(),
            "buff 生命周期写事件（台账负责）");
    }

    [TestMethod]
    public void NextBattleBuff_CanBeClearedByLedger_AndWritesRemovedEvent()
    {
        var sharedLog = new CombatLog();
        var ledger = new BuffLedger(BuffDefsConfig.Parse(ReadData("buff_defs.json")), sharedLog);
        UnitId warrior = UnitId.Of("warrior");

        ledger.Add(warrior, "next_battle_armor", source: null); // 扎营【加固甲胄】
        Assert.IsTrue(ledger.Has(warrior, "next_battle_armor"));

        ledger.Remove(warrior, "next_battle_armor"); // 到期由**同一台账**清除
        Assert.IsFalse(ledger.Has(warrior, "next_battle_armor"), "到期后不再持有");
        Assert.IsTrue(sharedLog.Events.OfType<BuffRemovedEvent>().Any(), "清除写事件");
    }

    [TestMethod]
    public void DeathsDoorRecovery_Duration_IsUntilNextRecovery_NotBattleEnd()
    {
        BuffDefsConfig defs = BuffDefsConfig.Parse(ReadData("buff_defs.json"));
        BuffDefConfig dd = defs.Buffs.Single(b => b.Id == "deaths_door_recovery");
        Assert.AreEqual(BuffDurationType.UntilNextRecovery, dd.Duration.Type,
            "死门后遗症跨场保留属『到下次恢复』（与战斗内 buff 清除**并列非冲突**）");
    }
}
