using System;
using System.IO;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **远征相位 `FlowPhase`**（架构 `blueprint §9.17.0`；片 3 前置）：
/// 相位是**规则状态**（内核单一真值），表现层**只读派生谓词** ✓
///
/// 要治的病（红线 25 典型）：`CanCamp` 原先**只看 `Firewood`** ⇒ **内核不约束"战斗中不许扎营"**
/// ⇒ 面板一挂进地图模式，玩家就能**战斗中扎营** ⚠️
/// 本用例锁：① 相位转移 ② **战斗中三个面板谓词全为假** ③ `CanCamp` 读相位
///           ④ **扣不到柴火时相位不留下**（我自己踩过的半态：脚本误删 `TrySpend` ⇒ 扎营永不成功）✓
/// </summary>
[TestClass]
public sealed class FlowPhaseTests
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

    private static ExpeditionSession NewSession(int firewood)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        return new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: firewood, food: 2, tuning.Expedition.AmbushChance);
    }

    [TestMethod]
    public void StartsWalking_AndPredicatesAreTrue()
    {
        ExpeditionSession s = NewSession(firewood: 2);
        Assert.AreEqual(FlowPhase.Walking, s.Phase, "开局 = 走图 ✓");
        Assert.IsTrue(s.CanShowPathChoice, "走图 ⇒ 可选路 ✓");
        Assert.IsTrue(s.CanShowCurioUi, "走图 ⇒ 可开 Curio ✓");
        Assert.IsTrue(s.CanShowCampUi, "走图 + 有柴火 ⇒ 可扎营 ✓");
    }

    [TestMethod]
    public void InBattle_NoPanelIsAllowed_AndCanCampIsFalse()
    {
        ExpeditionSession s = NewSession(firewood: 2);
        var log = new CombatLog();
        s.BeginExpeditionBattle(1, log, tiers: null);

        Assert.AreEqual(FlowPhase.Battle, s.Phase, "进战斗 ⇒ Battle ✓");
        Assert.IsFalse(s.CanCamp, "🔴 **战斗中不许扎营**（原先只看柴火 ⇒ 会返回 true ⇒ 玩家能战斗中扎营）");
        Assert.IsFalse(s.CanShowCampUi, "🔴 战斗中不显示扎营面板 ✓");
        Assert.IsFalse(s.CanShowPathChoice, "🔴 战斗中不显示选路面板 ✓");
        Assert.IsFalse(s.CanShowCurioUi, "🔴 战斗中不显示 Curio 面板 ✓");
        // 三个谓词**互相一致**（架构 ④：不许"两个同时该显示 / 都不该显示"那种矛盾 ⇒ 它们同读一个相位）✓
        Assert.IsTrue(s.CanShowCampUi == s.CanShowPathChoice && s.CanShowPathChoice == s.CanShowCurioUi,
            "同一相位下三个谓词必须**一致**（都假）✓");
    }

    [TestMethod]
    public void AfterBattle_BackToWalking_BothPaths()
    {
        ExpeditionSession s = NewSession(firewood: 2);
        var log = new CombatLog();
        BattleDirector d = s.BeginExpeditionBattle(1, log, tiers: null);
        s.EndBattle(d, battleIndex: 1, result: "PlayerVictory", rounds: 3); // 🔴 内核写入口（探针走这条）
        Assert.AreEqual(FlowPhase.Walking, s.Phase, "内核路径：打完 ⇒ 回走图 ✓");
        Assert.IsTrue(s.CanCamp && s.CanShowCampUi, "回走图后又能扎营 ✓");
    }

    [TestMethod]
    public void CampPhase_And_NoFirewoodLeavesNoHalfState()
    {
        ExpeditionSession s = NewSession(firewood: 2);
        var log = new CombatLog();
        Assert.IsTrue(s.StartCamp(log, campIndex: 1, respiteBase: 6), "有柴火 ⇒ 可扎营 ✓");
        Assert.AreEqual(FlowPhase.Camp, s.Phase, "扎营中 ⇒ Camp 相位 ✓");
        Assert.IsTrue(s.CanShowCampUi, "扎营中仍可用扎营技能（面板可显示）✓");
        s.EndCamp(log);
        Assert.AreEqual(FlowPhase.Walking, s.Phase, "收营 ⇒ 回走图 ✓（`ExpeditionFlow.FinishCamp` 也会设一次）✓");

        ExpeditionSession broke = NewSession(firewood: 0);
        Assert.IsFalse(broke.StartCamp(new CombatLog(), campIndex: 1, respiteBase: 6), "没柴火 ⇒ 拒绝 ✓");
        Assert.AreEqual(FlowPhase.Walking, broke.Phase,
            "🔴 **扣不到柴火时相位不留下**（半态回归：我曾误删 `TrySpend` ⇒ 扎营恒失败且不扣柴火）✓");
    }
}
