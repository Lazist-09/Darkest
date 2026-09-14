using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴🔴 **`O-83` 的第二条真缺陷**（2026-09-15，靠"**绑了阵型读数仍逐字不变**"追出来）：
///
/// 跨场台账 `Retained` 有**两个写入口，键空间不同** ⚠️：
///   · **场景路径**：`ExpeditionSession.CaptureBattleEndHp`（我加的）按【**战斗单位 id**】写 ✓
///   · **内核路径**：`RunSession.EndBattle`（探针/V10/A1/A2 走这条）按【`_roster` 里的 id】写 ✓
/// 而"承上"原先**只按战斗单位 id 查** ⇒ 内核路径**必然落空** ⇒ 静默跳过（连留痕都没有）⚠️
/// ⇒ 症状：**探针读数对 HP 结转完全不敏感**（我因此一度以为"改了没用"）
///
/// 本用例锁【内核路径】：用 `EndBattle`（内核写入口）后，下一场开局必须是**结转血量** ——
/// 既不能是全满（= 承上没生效），也不能是 0（= 把"查不到"误当"血为 0"）✓
/// </summary>
[TestClass]
public sealed class RetainedHpCarryKernelPathTests
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
    public void KernelPath_EndBattle_ThenNextBattle_StartsWithCarriedHp()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);

        // 🔴 用**单一来源**的映射绑阵型（与场景同一套；`#327`① 探针口径修复的同一处）
        session.BindSortie(FormationSortie.HeroSlotMap(
            FormationConfig.Parse(ReadData("formation.json")),
            RosterConfig.Parse(ReadData("roster.json"))));

        var log = new CombatLog();
        BattleDirector b1 = session.BeginExpeditionBattle(1, log, tiers: null);
        int slot = b1.Player.OccupiedPositions(includeObstacle: false).First();
        UnitRuntime first = b1.Player.UnitRuntimeAt(slot) ?? throw new InvalidOperationException("槽位应有我方单位");
        first.CurrentHp = Math.Max(1, first.MaxHp / 3); // 打掉 2/3（留一条命，避免死门/阵亡语义干扰）
        int expected = first.CurrentHp;

        // 🔴 **内核写入口**（探针走的就是这条；与场景的 `CaptureBattleEndHp` 是**另一个键空间**）
        session.EndBattle(b1, battleIndex: 1, result: "PlayerVictory", rounds: 3);

        BattleDirector b2 = session.BeginExpeditionBattle(2, log, tiers: null);
        UnitRuntime second = b2.Player.UnitRuntimeAt(slot) ?? throw new InvalidOperationException("槽位应有我方单位");
        Assert.AreEqual(expected, second.CurrentHp,
            $"🔴 内核路径必须结转：槽位 {slot} 第二场开局应 = 上一场结束的 {expected}（全满=没生效；0=把查不到误当 0）");
    }
}
