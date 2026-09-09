using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M3 可用性判定（T-M3-03/04/05）：四步判定顺序、tooltip 文案、敌方跳过携带、幂等无抽取。
/// </summary>
[TestClass]
public sealed class SkillAvailabilityTests
{
    private static SkillsConfig Skills() => SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json")));

    private static FormationBoard PlayerBoard(IEnumerable<(int slot, string id)>? placed = null)
    {
        FormationConfig formation = FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json")));
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        FormationBoard board = FormationBoardFactory.CreatePlayerBoard(formation, units);
        if (placed is null)
        {
            return board;
        }

        // 自定义布阵：重建板（默认 1~6 原型）
        var dict = new Dictionary<int, UnitRuntime>();
        foreach ((int slot, string id) in placed)
        {
            dict[slot] = new UnitRuntime(UnitId.Of(id), FormationSide.Player,
                UnitStatsMapper.From(units.Get(id)));
        }

        return new FormationBoard(FormationSide.Player, new SlotLayout(6, 4, new[] { 5, 6 }),
            FormationRules.Default(), dict);
    }

    private static FormationBoard EmptyEnemyBoard() =>
        new(FormationSide.Enemy, new SlotLayout(4, 4, Array.Empty<int>()), FormationRules.Default());

    private static string FindDataFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"data/{name} 未找到。");
    }

    private static SkillUseContext Ctx(
        SkillsConfig skills, string skillId, UnitId caster, FormationBoard ally, FormationBoard target,
        IReadOnlySet<string>? carried, SkillRuntimeState runtime, bool isEnemy = false)
        => new(skills.Get(skillId), caster, ally, target, carried, runtime, isEnemy);

    [TestMethod]
    public void Resolve_NotCarried_First()
    {
        FormationBoard player = PlayerBoard();
        var resolver = new SkillUseResolver(Skills());
        var runtime = new SkillRuntimeState();
        Availability a = resolver.Resolve(Ctx(Skills(), "warrior_cleave", UnitId.Of("warrior"), player, player, null, runtime));
        Assert.AreEqual(AvailabilityReason.NotCarried, a.Reason);
        Assert.AreEqual("未携带", a.Tooltip);
    }

    [TestMethod]
    public void Resolve_BadStance_Second_EvenWhenTargetValid()
    {
        // 战士在支援位 5；劈砍 self_slots [1,2] → 站位不符（先于目标判定）
        var carried = new HashSet<string> { "warrior_cleave" };
        Availability a = new SkillUseResolver(Skills()).Resolve(
            Ctx(Skills(), "warrior_cleave", UnitId.Of("warrior"),
                PlayerBoard(new[] { (5, "warrior"), (1, "tank") }), PlayerBoard(), carried, new SkillRuntimeState()));
        Assert.AreEqual(AvailabilityReason.BadStance, a.Reason);
        Assert.AreEqual("站位不符", a.Tooltip);
    }

    [TestMethod]
    public void Resolve_NoTarget_Third_WhenAllEmpty()
    {
        // 战士在 1 号位（站位通过）；敌 1、2 全空 → NoTarget
        FormationBoard enemy = EmptyEnemyBoard();
        var carried = new HashSet<string> { "warrior_cleave" };
        Availability a = new SkillUseResolver(Skills()).Resolve(
            Ctx(Skills(), "warrior_cleave", UnitId.Of("warrior"),
                PlayerBoard(new[] { (1, "warrior") }), enemy, carried, new SkillRuntimeState()));
        Assert.AreEqual(AvailabilityReason.NoTarget, a.Reason);
        Assert.AreEqual("范围内没有目标", a.Tooltip);
    }

    [TestMethod]
    public void Resolve_OnCooldown_Fourth()
    {
        var carried = new HashSet<string> { "warrior_shield_bash" };
        var runtime = new SkillRuntimeState();
        runtime.RecordUse(UnitId.Of("warrior"), Skills().Get("warrior_shield_bash")); // CD 2 置位
        Availability a = new SkillUseResolver(Skills()).Resolve(
            Ctx(Skills(), "warrior_shield_bash", UnitId.Of("warrior"),
                PlayerBoard(new[] { (1, "warrior") }), PlayerBoard(), carried, runtime));
        Assert.AreEqual(AvailabilityReason.OnCooldown, a.Reason);
        Assert.AreEqual("CD 中", a.Tooltip);
    }

    [TestMethod]
    public void Resolve_UsesExhausted_Fourth()
    {
        var carried = new HashSet<string> { "warrior_last_stand" };
        var runtime = new SkillRuntimeState();
        runtime.RecordUse(UnitId.Of("warrior"), Skills().Get("warrior_last_stand")); // per_battle 1 → 已用 1
        Availability a = new SkillUseResolver(Skills()).Resolve(
            Ctx(Skills(), "warrior_last_stand", UnitId.Of("warrior"),
                PlayerBoard(new[] { (1, "warrior") }), PlayerBoard(), carried, runtime));
        Assert.AreEqual(AvailabilityReason.UsesExhausted, a.Reason);
        Assert.AreEqual("每场次数用尽", a.Tooltip);
    }

    [TestMethod]
    public void Resolve_Ok_AllPass()
    {
        var carried = new HashSet<string> { "warrior_cleave" };
        Availability a = new SkillUseResolver(Skills()).Resolve(
            Ctx(Skills(), "warrior_cleave", UnitId.Of("warrior"),
                PlayerBoard(new[] { (2, "warrior") }), PlayerBoard(), carried, new SkillRuntimeState()));
        Assert.AreEqual(AvailabilityReason.Ok, a.Reason);
    }

    [TestMethod]
    public void Resolve_EnemySkipsCarried_IsEnemyOk()
    {
        FormationBoard enemyFull = FormationBoardFactory.CreateEnemyBoard(
            FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json"))),
            UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json"))));
        var carried = new HashSet<string> { "melee_heavy_slash" };
        Availability a = new SkillUseResolver(Skills()).Resolve(
            Ctx(Skills(), "melee_heavy_slash", UnitId.Of("melee_soldier"), enemyFull, PlayerBoard(), carried, new SkillRuntimeState(), isEnemy: true));
        Assert.AreEqual(AvailabilityReason.Ok, a.Reason);
    }

    [TestMethod]
    public void Resolve_Idempotent_NoRandom()
    {
        var carried = new HashSet<string> { "warrior_cleave" };
        var resolver = new SkillUseResolver(Skills());
        Availability first = resolver.Resolve(Ctx(Skills(), "warrior_cleave", UnitId.Of("warrior"), PlayerBoard(), PlayerBoard(), carried, new SkillRuntimeState()));
        for (int i = 0; i < 100; i++)
        {
            Availability again = resolver.Resolve(Ctx(Skills(), "warrior_cleave", UnitId.Of("warrior"), PlayerBoard(), PlayerBoard(), carried, new SkillRuntimeState()));
            Assert.AreEqual(first.Reason, again.Reason, "同快照重复判定必须同结果（幂等）");
        }
    }

    [TestMethod]
    public void TargetResolver_ObstacleCountsAsNonEmpty_Ascending()
    {
        // 敌 1 空 / 敌 2 角色 / 敌 3 障碍 → 范围 敌 1~3 输出 [2,3]（跳过空 1；障碍计入）升序
        (FormationBoard player, _, _) = DefaultCore();
        var units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        var enemy = new FormationBoard(FormationSide.Enemy, new SlotLayout(4, 4, Array.Empty<int>()),
            FormationRules.Default(),
            new Dictionary<int, UnitRuntime>
            {
                [2] = new(UnitId.Of("melee_soldier"), FormationSide.Enemy, UnitStatsMapper.From(units.Get("melee_soldier"))),
            },
            new Dictionary<int, ObstacleRuntime> { [3] = new(null) });

        SkillTemplateConfig sweep = Skills().Get("warrior_sweep"); // 敌 1、2、3
        IReadOnlyList<int> targets = SkillTargetResolver.Resolve(sweep, UnitId.Of("warrior"), player, enemy);
        CollectionAssert.AreEqual(new[] { 2, 3 }, targets.ToArray(), "跳过空 1、障碍计非空、槽号升序");
    }

    private static (FormationBoard player, FormationBoard enemy, BalanceTable balance) DefaultCore()
    {
        FormationConfig formation = FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json")));
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        BalanceTable balance = BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json"))));
        return (FormationBoardFactory.CreatePlayerBoard(formation, units),
                FormationBoardFactory.CreateEnemyBoard(formation, units), balance);
    }
}