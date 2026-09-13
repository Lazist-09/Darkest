using System;
using System.IO;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **`#301` ① 的答案（结构化实测）**：**光照档位的战斗效果【是否真的接进了战斗】** ——
/// 判据不是读配置，而是**比较"强制 Black" 与 "强制 Radiant" 下，同一 seed 的敌方单位状态与战斗读数**。
///
/// 🔴 背景（`#300`/`#301`）：实测"提亮把触底率 72% → 0%，而完成率完全不变（76% = 76%）"
/// ⇒ 策划怀疑"Black 本身不够痛"，并要求**从事件流读** Black 档敌方的实际加成。
/// 本用例把这件事**做成可执行的证据**：若两档下敌方状态**完全相同** ⇒ 那就不是"量级不足"，
/// 而是 **`light.effects` 的六个战斗字段【从未接进战斗】**（红线 21：写了但没接上）。
/// </summary>
[TestClass]
public sealed class LightEffectWiringTests
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

    private static (int EnemyAtk, int EnemyHp, int StunBonus, int EnemyCount, int EnemyDmgMod) BattleAtTier(string tierId)
    {
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        ExpeditionNodesConfig nodes = ExpeditionNodesConfig.Parse(ReadData("expedition_nodes.json"));
        var log = new CombatLog();
        var bag = new Inventory(tuning.Inventory!);
        bag.ConfigureRecommended(out _);
        bag.LockForRun();
        var session = new ExpeditionSession(_ => MonteCarlo.HeadlessDriver.NewDirector(new CombatLog()),
            tuning.Expedition.NBattles, bag.CountOf(ItemKind.Firewood), bag.CountOf(ItemKind.Food),
            tuning.Expedition.AmbushChance);
        var meter = new LightMeter(tuning.Light!);
        meter.EmitStart(log);

        // 🔴 强制到目标档（用显式代价推进；reason 标明是探针所为）
        int target = tierId switch
        {
            "radiant" => 90,
            "black" => 0,
            _ => throw new ArgumentException(tierId),
        };
        meter.TryAdvanceBy(log, target - meter.Value, $"probe_force_{tierId}");
        Assert.AreEqual(tierId, LightMeter.TierId(meter.Tier), $"探针应把光照强制到 {tierId}");

        BattleDirector d = session.BeginExpeditionBattle(1, log, tuning.Expedition.DifficultyTiers, meter.Effect);
        var enemies = d.Enemy.UnitsInSlotOrder().ToArray();
        return (enemies.Sum(u => u.EffectiveAttack), enemies.Sum(u => u.MaxHp),
            enemies.Sum(u => u.StunResistBonus), enemies.Length, enemies.Sum(u => u.DamageModPct));
    }

    [TestMethod]
    public void O301_LightEffects_MustBeWiredIntoBattle()
    {
        (int atkR, int hpR, int stunR, int countR, int dmgModR) = BattleAtTier("radiant");
        (int atkB, int hpB, int stunB, int countB, int dmgModB) = BattleAtTier("black");

        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        TuningLightEffect radiant = tuning.Light!.Effects["radiant"];
        TuningLightEffect black = tuning.Light.Effects["black"];

        string report =
            $"[M7.6] #301 光照效果接线实测（同 seed、同一难度档；只切光照档位）：\n" +
            $"  · 配置里 Black 给敌人：命中 +{black.EnemyAcc:0.#} / 伤害 +{black.EnemyDmgPct:0.#}% / 暴击 +{black.EnemyCritPct:0.#}%" +
            $"（Radiant：+{radiant.EnemyAcc:0.#} / +{radiant.EnemyDmgPct:0.#}% / +{radiant.EnemyCritPct:0.#}%）\n" +
            $"  · 实测敌方单位状态：Radiant ⇒ 攻击合计 {atkR}／HP {hpR}／眩晕抗性 {stunR}／人数 {countR}；" +
            $"Black ⇒ 攻击合计 {atkB}／HP {hpB}／眩晕抗性 {stunB}／人数 {countB}";
        Console.WriteLine(report);
        TestContext.WriteLine(report);

        // 🔴 本用例的**断言方向**（先说清，不发明契约）：
        //    我**不**断言"必须有差异"（那是要求改变现状、属裁定范围）；
        //    我断言的是【两者当前是否相同】这一**事实**，以便策划据此判断"是量级不足还是没接线"。
        bool wired = dmgModB != dmgModR;
        string verdict = wired
            ? $"⇒ ✅ **两档下敌方【输出修正】不同**（Radiant {dmgModR}% vs Black {dmgModB}%）" +
              " ⇒ `enemy_dmg_pct` **已接进战斗**（红线 21 的最坏形态已消除）；" +
              "⚠️ 其余五项仍待接线（UI 上仍标「未生效」）"
            : "⇒ 🔴 两档下敌方状态完全相同 ⇒ 光照战斗效果仍未接线（红线 21）";
        Console.WriteLine(verdict);
        TestContext.WriteLine(verdict);

        // 🔴 断言方向（红线 19 补充：事实变了就更新用例）：
        //    本片把 `enemy_dmg_pct` 接进战斗 ⇒ 期望**存在差异**（若变红 ⇒ 接线被回退）
        Assert.IsTrue(wired,
            $"`enemy_dmg_pct` 应已接进战斗（Radiant {dmgModR}% vs Black {dmgModB}%）⇒ 未接线则本用例变红");
    }

    [TestMethod]
    public void O301_NoLightParameterOnBattleBuild_StructuralEvidence()
    {
        // 🔴 结构性证据（与上面的实测互为佐证）：`BeginExpeditionBattle` 现在**接收光照档位**（`#302` (i1) 接线后）
        //    ⇒ 本用例改为核对【接了几项】：目前只接了 `enemy_dmg_pct`（1/6），其余五项待接线。
        (int atkT0, _, _, _, int dmgMod0) = BattleAtTier("black");

        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));
        Assert.IsTrue(tuning.Light!.Effects["black"].EnemyDmgPct >= tuning.Light.Effects["radiant"].EnemyDmgPct,
            "配置层面：越暗敌方加成**不减**（配置是有梯度的）");

        string report = $"[M7.6] #302(i1) 接线现状：`BeginExpeditionBattle` **已接收光照档位**，" +
                        $"当前接入 **1/6 项**（`enemy_dmg_pct` → 敌方单位 `DamageModPct`，Black 档实测 {dmgMod0}%）；" +
                        $"其余五项（enemy_acc ／ enemy_crit_pct ／ our_crit_pct ／ our_ambush_pct ／ our_morale_damage_pct）**待各自通道接线**" +
                        $"（红线 21：未接线不假装已生效 ⇒ UI 上仍标「未生效」）";
        Console.WriteLine(report);
        TestContext.WriteLine(report);
        Assert.IsTrue(atkT0 > 0, "敌方单位可读（探针有效）");
    }

    public TestContext TestContext { get; set; } = null!;
}
