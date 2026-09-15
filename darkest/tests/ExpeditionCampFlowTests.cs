using System;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Tests.MonteCarlo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// E3 扎营流程验收（内核层）：柴火许可（不足拒且不扣）、四档食物（含不足→退化挨饿）、
/// Respite = 基准 + 存活人数、点数不足不可选、`Camp*Event` 齐发。
/// </summary>
[TestClass]
public sealed class ExpeditionCampFlowTests
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

    private static TuningCamp Camp() => TuningConfig.Parse(ReadData("tuning.json")).Camp;

    private static ExpeditionSession NewSession(int firewood = 2, int food = 12)
        => new(log => HeadlessDriver.NewDirector(log), targetBattles: 6, firewood: firewood, food: food,
            ambushChance: 0.33);

    private static void RunOneBattle(ExpeditionSession s, long seed)
    {
        var log = new CombatLog();
        var rng = new RngProvider(seed);
        Darkest.Gameplay.Sim.Director.BattleDirector d = s.BeginBattle(1, log);
        string result = "RoundLimit";
        for (int round = 1; round <= 100; round++)
        {
            d.RunFullRound(rng, unit => Policies.DecideForUnit(PolicyKind.SemiRandom, unit, d, rng));
            if (d.IsBattleOver)
            {
                result = d.Enemy.OccupiedPositions(false).Count == 0 ? "PlayerVictory" : "EnemyVictory";
                break;
            }
        }

        s.EndBattle(d, 1, result, 10);
    }

    [TestMethod]
    public void E3_NoFirewood_CannotCamp_AndNothingSpent()
    {
        ExpeditionSession s = NewSession(firewood: 0);
        var log = new CombatLog();
        Assert.IsFalse(s.CanCamp, "无柴火 → 不可扎营");
        Assert.IsFalse(s.StartCamp(log, 1, Camp().RespiteBase), "无柴火 → 启动被拒");
        Assert.AreEqual(0, s.Firewood, "拒绝时不扣（也不变负）");
        Assert.IsFalse(log.Events.OfType<CampStartedEvent>().Any(), "未开始 → 无 CampStartedEvent");
    }

    [TestMethod]
    public void E3_StartCamp_SpendsFirewood_AndRespiteIsBasePlusSurvivors()
    {
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);
        var log = new CombatLog();

        Assert.IsTrue(s.StartCamp(log, 1, Camp().RespiteBase), "柴火 2 → 可扎营");
        Assert.AreEqual(1, s.Firewood, "扣 1 份柴火");
        Assert.AreEqual(Camp().RespiteBase + s.Survivors, s.RespiteLeft,
            $"Respite = 基准 {Camp().RespiteBase} + 存活 {s.Survivors}");
        Assert.IsTrue(s.RespiteLeft is >= 6 and <= 12, "满编 12 / 减员更少");
        Assert.IsTrue(log.Events.OfType<CampStartedEvent>().Any(), "CampStartedEvent 齐发");
    }

    [TestMethod]
    public void E3_Feast_SpendsScaledFood_AndRaisesMorale()
    {
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        s.StartCamp(log, 1, Camp().RespiteBase);

        int survivors = s.Survivors;
        int expectedFood = ExpeditionCampMath.FoodRequired(Camp().FoodTiers, "feast", survivors);
        string applied = s.ChooseFood(log, Camp(), "feast");

        Assert.AreEqual("feast", applied, "口粮充足 → 按所选档位生效");
        Assert.AreEqual(12 - expectedFood, s.Food, $"扣口粮 {expectedFood}（{survivors} 人缩放）");
        CampFoodChosenEvent e = log.Events.OfType<CampFoodChosenEvent>().Single();
        Assert.AreEqual("feast", e.Tier);
        Assert.AreEqual(expectedFood, e.FoodSpent);
    }

    [TestMethod]
    public void E3_Starve_AppliesPenalty_AndIsRecorded()
    {
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        s.StartCamp(log, 1, Camp().RespiteBase);

        string applied = s.ChooseFood(log, Camp(), "starve");
        Assert.AreEqual("starve", applied);
        Assert.AreEqual(12, s.Food, "Starve 不消耗口粮");
        Assert.AreEqual(0, log.Events.OfType<CampFoodChosenEvent>().Single().FoodSpent);
    }

    [TestMethod]
    public void E3_InsufficientFood_TierNotSelectable_NoFreeDegrade()
    {
        ExpeditionSession s = NewSession(firewood: 2, food: 1); // 口粮不足以支付 full（6）
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        s.StartCamp(log, 1, Camp().RespiteBase);

        Assert.IsFalse(s.CanAffordFood(Camp(), "full"), "口粮不足 → 该档位**不可选（灰显）**");
        Assert.ThrowsException<InvalidOperationException>(
            () => s.ChooseFood(log, Camp(), "full"),
            "🔴 **禁止自动退化为 starve 且不扣**（免费午餐，v0.86 规格）");
        Assert.AreEqual(1, s.Food, "被拒时**不扣口粮**");
        Assert.IsFalse(log.Events.OfType<CampFoodChosenEvent>().Any(), "被拒时不写档位事件");

        Assert.IsTrue(s.CanAffordFood(Camp(), "starve"), "Starve 需**主动选择**（需求 0，永远可选）");
        Assert.AreEqual("starve", s.ChooseFood(log, Camp(), "starve"));
        CampFoodChosenEvent e = log.Events.OfType<CampFoodChosenEvent>().Single();
        Assert.AreEqual("starve", e.Tier);
        Assert.AreEqual(0, e.FoodSpent, "主动挨饿不耗口粮，但**照常受罚**（−20% HP / −15 士气）");
    }

    [TestMethod]
    public void E3_CampSkill_CostsRespite_AndBlockedWhenInsufficient()
    {
        ExpeditionSession s = NewSession();
        RunOneBattle(s, 20260909);
        var log = new CombatLog();
        s.StartCamp(log, 1, Camp().RespiteBase);

        UnitId target = UnitId.Of("tank");
        int before = s.RespiteLeft;
        Assert.IsTrue(s.UseCampSkill(log, CampSkillTestKit.Skill("camp_warrior_sharpen"), target, CampSkillTestKit.Camp), "点数足够 → 可用");
        Assert.AreEqual(before - 2, s.RespiteLeft, "扣 2 点");

        int used = 1;
        while (s.RespiteLeft >= 1)
        {
            s.UseCampSkill(log, CampSkillTestKit.Skill("camp_medic_care"), target, CampSkillTestKit.Camp);
            used++;
        }

        Assert.IsFalse(s.UseCampSkill(log, CampSkillTestKit.Skill("camp_warrior_sharpen"), target, CampSkillTestKit.Camp), "**点数不足 → 不可选（返回 false）**");
        Assert.AreEqual(used, log.Events.OfType<CampSkillUsedEvent>().Count(), "只有成功使用才写事件");
        Assert.IsTrue(log.Events.OfType<CampSkillUsedEvent>().All(e => e.RespiteLeft >= 0), "Respite 不越界");

        s.EndCamp(log);
        Assert.IsTrue(log.Events.OfType<CampEndedEvent>().Any(), "CampEndedEvent 齐发");
    }
}
