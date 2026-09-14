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
/// 🔴 **`O-83` 验收：HP 跨战斗结转**（契约 `blueprint` 明文：
/// 「战后【不自动恢复】：**HP 与士气跨战斗完全保留**；场间无恢复（切片无扎营/回城/战后回血）」）。
///
/// ⚠️ 缺口有**两半**，两边都要接（否则就是"接了个空"）：
/// ① **战后落账**：`CaptureBattleEndHp` 把本场结束血量写进 `Retained`（此前**根本没人写** ⇒ 台账里永远是满哨兵值）
/// ② **开场承上**：`ApplyRetainedHpToBattle` 把上一场结转值应用到本场开局（此前**从未使用**）✓
///
/// 本用例**走真实 API**（不种子化私有台账）：打一场 ⇒ 把某单位打到 7 血 ⇒ 落账 ⇒ 开第 2 场 ⇒ 断言开局 7 血；
/// 并对照【没受伤的英雄仍是满血】⇒ 证明是"读台账结转"，不是"一律压低" ✓
/// </summary>
[TestClass]
public sealed class RetainedHpCarryTests
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

    private static ExpeditionSession NewSessionWithSortie()
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        RosterConfig roster = RosterConfig.Parse(ReadData("roster.json"));
        var session = new ExpeditionSession(l => MonteCarlo.HeadlessDriver.NewDirector(l),
            tuning.Expedition.NBattles, firewood: 2, food: 2, tuning.Expedition.AmbushChance);

        // 🔴 按【槽位】绑定（两套 id 体系的唯一正确接法）：前 4 个英雄占槽 1..4
        var slots = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyList<string> heroIds = roster.Heroes.Take(4).Select(h => h.Id).ToList();
        for (int i = 0; i < heroIds.Count; i++)
        {
            slots[heroIds[i]] = i + 1;
        }

        session.BindSortie(slots);
        return session;
    }

    [TestMethod]
    public void O83_NextBattle_StartsWithCarriedHp_NotFullHp()
    {
        ExpeditionSession session = NewSessionWithSortie();
        var log = new CombatLog();

        // ---- 第 1 场：把槽 1 打到 7 血，然后【战后落账】 ----
        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        UnitRuntime u1 = b1.Player.UnitRuntimeAt(1)!;
        Assert.IsTrue(u1.MaxHp > 7, "前置：该单位 MaxHp 必须 > 7，否则证明不了'承上'");
        u1.CurrentHp = 7;
        session.CaptureBattleEndHp(b1, log);
        Assert.IsTrue(log.Events.OfType<EffectEvent>()
                .Any(e => e.EffectType.StartsWith("battle_end_hp_captured:", StringComparison.Ordinal)),
            "① 战后必须落账（否则第 2 场的'承上'读到的是满哨兵值 ⇒ 惰性改动）");

        // ---- 第 2 场：开局 HP 必须 = 7 ----
        BattleDirector b2 = session.BeginExpeditionBattle(1, log, tiers: null);
        Assert.AreEqual(7, b2.Player.UnitRuntimeAt(1)!.CurrentHp,
            "🔴 O-83：第 2 场开局 HP 必须承接第 1 场（修复前恒为满血）");
        Assert.IsTrue(log.Events.OfType<EffectEvent>()
                .Any(e => e.EffectType.StartsWith("retained_hp_carried:", StringComparison.Ordinal)),
            "② 承上必须留痕（可审计：承了多少）");

        // ---- 对照：没受伤的槽位仍满血（证明是"读台账"，不是"一律压低"）----
        UnitRuntime c2 = b2.Player.UnitRuntimeAt(2)!;
        Assert.AreEqual(c2.MaxHp, c2.CurrentHp, "未受伤的英雄 ⇒ 满血开局（无副作用）");
    }

    [TestMethod]
    public void O83_ZeroHpAtBattleEnd_IsNotSilentlyRefilled()
    {
        ExpeditionSession session = NewSessionWithSortie();
        var log = new CombatLog();

        BattleDirector b1 = session.BeginExpeditionBattle(0, log, tiers: null);
        b1.Player.UnitRuntimeAt(1)!.CurrentHp = 0; // 阵亡
        session.CaptureBattleEndHp(b1, log);

        BattleDirector b2 = session.BeginExpeditionBattle(1, log, tiers: null);
        UnitRuntime? u2 = b2.Player.UnitRuntimeAt(1);

        // 🔴 不变式（两种结果都合法，但**绝不允许**"静默补满"）：
        //    · 阵亡者**不被编入**（`UnitRuntimeAt` 返回 null）⇒ 由流程侧保证 ✓
        //    · 若它**仍被编入** ⇒ 开局必须是 0 血 **且**留痕 `retained_hp_zero_but_deployed` ✓
        if (u2 is null)
        {
            Assert.IsTrue(true, "阵亡者未被编入（流程侧拦下）—— 合法结果");
        }
        else
        {
            Assert.AreEqual(0, u2.CurrentHp, "🔴 仍被编入 ⇒ 开局必须 0 血（**不静默补满**）");
            Assert.IsTrue(log.Events.OfType<EffectEvent>()
                    .Any(e => e.EffectType == "retained_hp_zero_but_deployed"),
                "必须留痕 `retained_hp_zero_but_deployed`（供审计抓流程 bug）");
        }

        // 无论如何：**第 2 场的开局绝不能满血**（这条才是回归防线）
        Assert.IsTrue(u2 is null || u2.CurrentHp < u2.MaxHp,
            "🔴 O-83 回归防线：结转 0 血的英雄**绝不允许**满血开局");
    }
}

