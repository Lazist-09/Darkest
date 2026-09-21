using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Buffs;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// C-1（v0.99）Stealth 潜行：四条 DD 规则
///  ① 潜行者**不可被直接指定**（单体候选池剔除）
///  ② **含潜行者的多目标技能**仅当**至少存在 1 个非潜行可选目标**时才放行（否则整个技能不可用）
///  ③ 带 `ignore_stealth` 标签的技能**无视** ①②（De-Stealth 由 `SkillExecutor` 负责）
///  ④ **回归保护**：未传台账（旧调用点）行为与改动前**逐字一致**；障碍位不受潜行过滤影响
/// 权威规格：`darkestdungeon.wiki.gg/wiki/Stealth_(Darkest_Dungeon)`（原文 "must be targeting at least
/// one non-Stealthed target"；"Hitting a stealthed unit with an area-of-effect attack will not
/// de-stealth"）；一手实据：`buff_defs.json` stealth 条 source（borrow/3440502939 playwright.effects.darkest
/// `.stealth 1 .duration 2` / `.unstealth 1`；borrow/3424145711 project.xml "Stealth Self (2 Rds)"）。
/// </summary>
[TestClass]
public sealed class StealthTests
{
    private static SkillsConfig Skills() => SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json")));

    private static BuffDefsConfig Buffs() => BuffDefsConfig.Parse(File.ReadAllText(FindDataFile("buff_defs.json")));

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

    /// <summary>我方板：默认 1~6 各原型。</summary>
    private static FormationBoard PlayerBoard()
    {
        FormationConfig formation = FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json")));
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        return FormationBoardFactory.CreatePlayerBoard(formation, units);
    }

    /// <summary>敌方板：在指定槽放单位（其余空）。</summary>
    private static FormationBoard EnemyBoard(params (int slot, string id)[] placed)
    {
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        var dict = new Dictionary<int, UnitRuntime>();
        foreach ((int slot, string id) in placed)
        {
            dict[slot] = new UnitRuntime(UnitId.Of($"{id}#{slot}"), FormationSide.Enemy,
                UnitStatsMapper.From(units.Get(id)));
        }

        return new FormationBoard(FormationSide.Enemy, new SlotLayout(4, 4, Array.Empty<int>()),
            FormationRules.Default(), dict);
    }

    // ---------------------------------------------------------------- 数据层
    [TestMethod]
    public void StealthBuff_Defined_AsStateFlag_WithRoundsDuration()
    {
        BuffDefConfig stealth = Buffs().Get("stealth");
        Assert.AreEqual(BuffPolarity.Positive, stealth.Polarity, "潜行 = 增益（我方），不可被驱散");
        Assert.AreEqual(BuffDurationType.Rounds, stealth.Duration.Type, "限时（原版 .duration 2）");
        // 🔴 C-1a（2026-09-20）**已定案 = 2**：原版一手实据（`playwright.effects.darkest` 里 `.stealth` 效果
        //    的 duration 分布 = 1×2 / 2×4 / 3×2；取最常见的 2）。原版高阶是 3，那份差异属**技能阶**维度 ⇒ M8.1 ✓
        Assert.AreEqual(2, stealth.Duration.Value, "已定案 = 2（不再等裁定）");
        Assert.AreEqual(BuffStackRule.Refresh, stealth.Stack.Rule, "再上潜行 = 刷新时长（同 taunt）");
        Assert.IsTrue(stealth.Modifiers!.Any(m => m.Kind == BuffModifierKind.StateFlag && m.Effect == "stealth"),
            "必须走 state_flag（避开 ConsumedEffectNames 白名单；消费点 = SkillTargetResolver / EnemyAi）");
    }

    [TestMethod]
    public void StealthBuff_Grants_DealtDamageAndHit_FromOriginalData()
    {
        // 原版 borrow/3424145711/project.xml:139-140 "+30% DMG while Stealthed" / "+10 ACC while Stealthed"
        // 两个 effect 名都已在 ConsumedEffectNames 里（dealt_damage_mult @DamageStep / hit_mod @HitStep）
        // ⇒ 无需新增白名单条目，加载期校验不会报错（红线 21）
        BuffDefConfig stealth = Buffs().Get("stealth");
        BuffModifierSpec dmg = stealth.Modifiers!.Single(m => m.Effect == "dealt_damage_mult");
        Assert.AreEqual(BuffModifierKind.DamageMod, dmg.Kind);
        Assert.AreEqual(30, dmg.Percent, "伤害 +30%（原版 project.xml 明文；数值本身仍属 #307 占位）");

        BuffModifierSpec acc = stealth.Modifiers!.Single(m => m.Effect == "hit_mod");
        Assert.AreEqual(BuffModifierKind.ProbMod, acc.Kind);
        Assert.AreEqual(10, acc.Percent, "命中 +10pp（原版 project.xml 明文；数值本身仍属 #307 占位）");

        // 白名单防线：新增 effect 名必须已登记（否则 BuffDefsConfig.Validate 启动即报错）
        foreach (BuffModifierSpec m in stealth.Modifiers!)
        {
            if (m.Kind is BuffModifierKind.DamageMod or BuffModifierKind.ProbMod)
            {
                Assert.IsTrue(BuffDefsConfig.ConsumedEffectNames.Contains(m.Effect!),
                    $"effect \"{m.Effect}\" 必须在 ConsumedEffectNames 里（红线 21）");
            }
        }
    }

    [TestMethod]
    public void Ledger_StealthPercentMods_ReadableByPipeline()
    {
        // 消费可读性：BuffLedger.PercentMod 能把潜行的两个收益读出来（管线实际走这两个口）
        var ledger = new BuffLedger(Buffs());
        UnitId u = UnitId.Of("warrior");
        ledger.Add(u, "stealth", source: null);

        Assert.AreEqual(30, ledger.PercentMod(u, "dealt_damage_mult"), "伤害 +30%（DamageStep 的 buffDamageMult）");
        Assert.AreEqual(10, ledger.PercentMod(u, "hit_mod"), "命中 +10pp（HitStep 的 aftereffect 同层）");
    }

    [TestMethod]
    public void SkillEffectTypeAndFuncTag_ResolveFromJson()
    {
        // LowerEnumJsonConverter 必须给全枚举成员（否则序列化 KeyNotFound）；此处用往返序列化验证
        SkillsConfig cfg = Skills();
        string json = SkillsConfig.Serialize(cfg);
        SkillsConfig again = SkillsConfig.Parse(json);
        Assert.AreEqual(cfg.Skills.Count, again.Skills.Count, "枚举映射齐备 ⇒ 往返不丢技能");
    }

    // ---------------------------------------------------------------- ① 不可被直接指定
    [TestMethod]
    public void Resolve_Singleton_ExcludesStealthedSlot_WhenLedgerProvided()
    {
        var ledger = new BuffLedger(Buffs());
        FormationBoard enemy = EnemyBoard((1, "melee_soldier"), (2, "ranged_archer"), (3, "caster"));
        ledger.Add(UnitId.Of("melee_soldier#1"), "stealth", source: null);

        // 范围收窄到敌 1，使其成为唯一（且潜行）的目标
        SkillTemplateConfig cleave = Skills().Get("warrior_cleave");
        SkillTemplateConfig singleEnemy1 = cleave with { Target = cleave.Target with { Slots = new[] { 1 } } };

        IReadOnlyList<int> withLedger = SkillTargetResolver.Resolve(
            singleEnemy1, UnitId.Of("warrior"), PlayerBoard(), enemy, ledger);
        Assert.AreEqual(0, withLedger.Count, "① 范围内唯一目标潜行 ⇒ 不可被指定（无候选）");

        IReadOnlyList<int> noLedger = SkillTargetResolver.Resolve(
            singleEnemy1, UnitId.Of("warrior"), PlayerBoard(), enemy);
        CollectionAssert.AreEqual(new[] { 1 }, noLedger.ToArray(), "④ 未传台账 ⇒ 原行为（该槽仍可打）");
    }

    [TestMethod]
    public void Resolve_MultiTarget_IncludesStealthed_PassesThrough_WhenOneVisible()
    {
        var ledger = new BuffLedger(Buffs());
        FormationBoard enemy = EnemyBoard((1, "melee_soldier"), (2, "ranged_archer"), (3, "caster"));
        ledger.Add(UnitId.Of("melee_soldier#1"), "stealth", source: null);

        SkillTemplateConfig sweep = Skills().Get("warrior_sweep"); // 敌 1、2、3
        IReadOnlyList<int> hits = SkillTargetResolver.Resolve(sweep, UnitId.Of("warrior"), PlayerBoard(), enemy, ledger);

        // wiki 原文："Stealthed monsters can be hit with attacks that hit multiple targets."
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, hits.ToArray(),
            $"② 多目标技能【穿过】潜行 —— 潜行的敌 1 仍在命中列表内（实际 [{string.Join(",", hits)}]）");
    }

    [TestMethod]
    public void Resolve_MultiTarget_Blocked_WhenAllTargetsStealthed()
    {
        var ledger = new BuffLedger(Buffs());
        FormationBoard enemy = EnemyBoard((1, "melee_soldier"), (2, "ranged_archer"), (3, "caster"));
        foreach (string id in new[] { "melee_soldier#1", "ranged_archer#2", "caster#3" })
        {
            ledger.Add(UnitId.Of(id), "stealth", source: null);
        }

        SkillTemplateConfig sweep = Skills().Get("warrior_sweep");
        IReadOnlyList<int> hits = SkillTargetResolver.Resolve(sweep, UnitId.Of("warrior"), PlayerBoard(), enemy, ledger);
        Assert.AreEqual(0, hits.Count,
            "② 全体潜行 ⇒ 多目标技能不可用（wiki：Grapeshot 对全体潜行的 Swine Gorers 不可用）");
    }

    // ---------------------------------------------------------------- ③ ignore_stealth 豁免
    [TestMethod]
    public void Resolve_IgnoreStealthTag_BypassesAllTargetRules()
    {
        var ledger = new BuffLedger(Buffs());
        FormationBoard enemy = EnemyBoard((1, "melee_soldier"), (2, "ranged_archer"), (3, "caster"));
        foreach (string id in new[] { "melee_soldier#1", "ranged_archer#2", "caster#3" })
        {
            ledger.Add(UnitId.Of(id), "stealth", source: null);
        }

        SkillTemplateConfig sweep = Skills().Get("warrior_sweep");
        SkillTemplateConfig bypass = sweep with { Tags = sweep.Tags.Append(FuncTag.IgnoreStealth).ToArray() };

        IReadOnlyList<int> hits = SkillTargetResolver.Resolve(bypass, UnitId.Of("warrior"), PlayerBoard(), enemy, ledger);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, hits.ToArray(),
            "③ ignore_stealth ⇒ 即使全体潜行也可用（原版 Bypass Stealth）");
    }

    // ---------------------------------------------------------------- ④ 障碍位不受影响
    [TestMethod]
    public void Resolve_Obstacle_NotTreatedAsStealthed()
    {
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        var ledger = new BuffLedger(Buffs());
        var enemy = new FormationBoard(FormationSide.Enemy, new SlotLayout(4, 4, Array.Empty<int>()),
            FormationRules.Default(),
            new Dictionary<int, UnitRuntime>
            {
                [2] = new(UnitId.Of("melee_soldier#2"), FormationSide.Enemy,
                    UnitStatsMapper.From(units.Get("melee_soldier"))),
            },
            new Dictionary<int, ObstacleRuntime> { [3] = new(null) });

        SkillTemplateConfig sweep = Skills().Get("warrior_sweep");
        IReadOnlyList<int> hits = SkillTargetResolver.Resolve(sweep, UnitId.Of("warrior"), PlayerBoard(), enemy, ledger);
        CollectionAssert.AreEqual(new[] { 2, 3 }, hits.ToArray(), "④ 障碍位无 buff ⇒ 不受潜行过滤影响（#73/#77/#105）");
    }

    // ---------------------------------------------------------------- 可用性门禁
    [TestMethod]
    public void Availability_TargetStealthed_WhenOnlyStealthedTargets()
    {
        var ledger = new BuffLedger(Buffs());
        FormationBoard enemy = EnemyBoard((1, "melee_soldier"));
        ledger.Add(UnitId.Of("melee_soldier#1"), "stealth", source: null);

        var carried = new HashSet<string> { "warrior_cleave" };
        var resolver = new SkillUseResolver(Skills());
        FormationBoard player = PlayerBoard();
        // 范围收窄到敌 1，使其成为唯一（且潜行）的目标
        SkillTemplateConfig cleave = Skills().Get("warrior_cleave") with
        {
            Target = Skills().Get("warrior_cleave").Target with { Slots = new[] { 1 } },
        };

        Availability withLedger = resolver.Resolve(new SkillUseContext(
            cleave, UnitId.Of("warrior"), player, enemy, carried, new SkillRuntimeState(),
            IsEnemy: false, Buffs: ledger));
        Assert.AreEqual(AvailabilityReason.TargetStealthed, withLedger.Reason);
        Assert.AreEqual("目标处于潜行", withLedger.Tooltip);

        Availability noLedger = resolver.Resolve(new SkillUseContext(
            cleave, UnitId.Of("warrior"), player, enemy, carried, new SkillRuntimeState(),
            IsEnemy: false));
        Assert.AreEqual(AvailabilityReason.Ok, noLedger.Reason, "④ 未传台账 ⇒ 仍判定 Ok（旧调用点不受影响）");
    }

    [TestMethod]
    public void Availability_NoTarget_StillReported_WhenTrulyEmpty()
    {
        var ledger = new BuffLedger(Buffs());
        var enemy = new FormationBoard(FormationSide.Enemy, new SlotLayout(4, 4, Array.Empty<int>()),
            FormationRules.Default());
        var carried = new HashSet<string> { "warrior_cleave" };
        FormationBoard player = PlayerBoard();
        SkillTemplateConfig cleave = Skills().Get("warrior_cleave") with
        {
            Target = Skills().Get("warrior_cleave").Target with { Slots = new[] { 1 } },
        };

        Availability a = new SkillUseResolver(Skills()).Resolve(new SkillUseContext(
            cleave, UnitId.Of("warrior"), player, enemy, carried,
            new SkillRuntimeState(), IsEnemy: false, Buffs: ledger));
        Assert.AreEqual(AvailabilityReason.NoTarget, a.Reason,
            "真·空池仍报 NoTarget（不得误报成 TargetStealthed）");
        Assert.AreEqual("范围内没有目标", a.Tooltip);
    }

    // ---------------------------------------------------------------- 台账原语
    [TestMethod]
    public void Ledger_HasStateFlag_And_ClearStateFlag_RemoveStealth()
    {
        var ledger = new BuffLedger(Buffs());
        UnitId u = UnitId.Of("warrior");
        ledger.Add(u, "stealth", source: null);
        Assert.IsTrue(ledger.HasStateFlag(u, "stealth"));
        Assert.IsTrue(ledger.Has(u, "stealth"));

        ledger.ClearStateFlag(u, "stealth");
        Assert.IsFalse(ledger.HasStateFlag(u, "stealth"), "解除（原版 .unstealth）⇒ 标记消失");
        Assert.IsFalse(ledger.Has(u, "stealth"), "同 stun 语义：清标记 = 移除整个 buff");
    }

    [TestMethod]
    public void Ledger_StealthExpires_AfterRoundsTick()
    {
        var ledger = new BuffLedger(Buffs());
        UnitId u = UnitId.Of("warrior");
        ledger.Add(u, "stealth", source: null); // rounds 2（placeholder）
        Assert.IsTrue(ledger.HasStateFlag(u, "stealth"));

        ledger.TickRounds();
        Assert.IsTrue(ledger.HasStateFlag(u, "stealth"), "还剩 1 回合");

        ledger.TickRounds();
        Assert.IsFalse(ledger.HasStateFlag(u, "stealth"), "2 回合后自动到期（BuffLedger.TickRounds 递减）");
    }
}
