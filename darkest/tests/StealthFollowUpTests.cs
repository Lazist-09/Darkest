using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Buffs;
using Darkest.Gameplay.Sim.Director;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **C-1a 潜行收尾**（2026-09-20）：把 C-1 留下的四个不确定项钉死。
/// <list type="number">
///   <item><b>持续回合数</b> ⇒ **2**（原版 `borrow/.../playwright.effects.darkest` 一手实据；
///         该文件 `.stealth` 效果 duration 分布 = 1×2 / 2×4 / 3×2，**原版是技能级、随阶递增** ⇒ 高阶 3 归 M8.1）✓</item>
///   <item><b>De-Stealth 时机</b> ⇒ **真·命中即解除**（原版 `.on_hit true`）：未命中**保留**潜行；同目标多段只解除一次 ✓</item>
///   <item><b>敌人开场潜行</b> ⇒ **不配发**（我方原型池无 DD 的 6 种潜行敌人，且无开场 buff 通道）—— 见文档登记 ✓</item>
///   <item><b>我方 `ignore_stealth`</b> ⇒ 打给**敌方 AOE 输出**（原版精神 = 泼洒型穿过潜行）✓</item>
/// </list>
/// </summary>
[TestClass]
public sealed class StealthFollowUpTests
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

    private static SkillsConfig Skills() => SkillsConfig.Parse(ReadData("skills.json"));

    private static BuffDefsConfig Buffs() => BuffDefsConfig.Parse(ReadData("buff_defs.json"));

    private static (BattleDirector d, BalanceTable balance) World()
    {
        var log = new CombatLog();
        BalanceTable balance = BalanceTable.FromTuning(TuningConfig.Parse(ReadData("tuning.json")));
        var d = new BattleDirector(
            FormationConfig.Parse(ReadData("formation.json")),
            UnitsConfig.Parse(ReadData("units.json")),
            SkillsConfig.Parse(ReadData("skills.json")),
            balance,
            MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            BuffDefsConfig.Parse(ReadData("buff_defs.json")),
            EnemyAiConfig.Parse(ReadData("enemy_ai.json")),
            log);
        d.Morale.Initialize(d.Player.UnitsInSlotOrder());
        return (d, balance);
    }

    /// <summary>`extra_rules` 是 `JsonElement?`（不是字典）⇒ 判键需走 `TryGetProperty` ✓</summary>
    private static bool ExtraRule(BuffDefConfig buff, string key)
        => buff.ExtraRules is { } el
           && el.ValueKind == System.Text.Json.JsonValueKind.Object
           && el.TryGetProperty(key, out _);

    /// <summary>脚本化随机源：命中/暴击等按给定百分数依次取出（同 `DdAdoptionTests` 范式）✓</summary>
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _percents;
        private ulong _draws;
        private const double Fallback = 0.0;

        public ScriptedRng(params double[] percents) => _percents = new Queue<double>(percents);

        public ulong DrawCount => _draws;

        public double NextPercent()
        {
            _draws++;
            return _percents.Count > 0 ? _percents.Dequeue() : Fallback;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            _draws++;
            return minInclusive;
        }
    }

    // ────────────────────────── ① 持续回合数（已定案） ──────────────────────────

    [TestMethod]
    public void Duration_SettledAtTwo_AndNoLongerFlaggedPlaceholder()
    {
        BuffDefConfig stealth = Buffs().Get("stealth");
        Assert.AreEqual(BuffDurationType.Rounds, stealth.Duration.Type);
        Assert.AreEqual(2, stealth.Duration.Value,
            "🔴 已定案 = 2（原版一手实据；不再是 placeholder #307）✓");

        Assert.IsFalse(ExtraRule(stealth, "duration_rounds_placeholder"),
            "🔴 `duration_rounds_placeholder` 必须已移除 —— 它表示「数值未定」，现在已定 ✓");
        Assert.IsTrue(ExtraRule(stealth, "duration_is_per_skill_tier"),
            "🔴 改为 `duration_is_per_skill_tier`：标记**原版高阶是 3**，那份差异归 M8.1 技能升级 ✓");
    }

    // ────────────────────────── ② De-Stealth：真·命中即解除 ──────────────────────────

    [TestMethod]
    public void DeStealth_OnHit_ClearsStealth()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = Skills();
        var rt = new SkillRuntimeState();

        // 让敌方 AI 的 AOE 技能能打到我方 1 位（`caster_mental_shock` 目标含 1、2、5、6）✓
        UnitId victim = d.Player.UnitRuntimeAt(1)!.Id;
        d.Buffs.Add(victim, "stealth", source: null);
        Assert.IsTrue(d.Buffs.HasStateFlag(victim, "stealth"), "前置：目标带潜行 ✓");

        SkillTemplateConfig shock = skills.Get("caster_mental_shock");
        Assert.IsTrue(shock.Tags.Contains(FuncTag.IgnoreStealth),
            "🔴 C-1a：该 AOE 技能已打 `ignore_stealth` 标签（数据层）✓");

        var executor = new SkillExecutor(skills, balance, MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            d.Log, rt, d.Buffs);

        int hpBefore = d.Player.UnitRuntimeAt(1)!.CurrentHp;
        // 0.0 ⇒ 必中（命中骰远小于命中率）✓
        executor.Execute(shock, UnitId.Of("caster"), d.Player, d.Enemy, new ScriptedRng(0.0, 0.0), chosenTargets: new[] { 1 });

        bool wasHit = d.Log.Events.OfType<HitEvent>().Any(h => h.Hit && h.Target == victim);
        Assert.IsTrue(wasHit, "前置：本次确实**命中**了该目标 ✓");
        Assert.IsTrue(d.Player.UnitRuntimeAt(1)!.CurrentHp < hpBefore, "前置：确实掉血 ✓");
        Assert.IsFalse(d.Buffs.HasStateFlag(victim, "stealth"),
            "🔴 ② 命中 ⇒ **解除潜行**（原版 `.on_hit true`）✓");
        Assert.IsTrue(d.Log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "unstealth" && e.Target == victim),
            "解除必须落 `unstealth` 事件（可审计）✓");
    }

    [TestMethod]
    public void DeStealth_OnMiss_KeepsStealth()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = Skills();
        var rt = new SkillRuntimeState();

        UnitId victim = d.Player.UnitRuntimeAt(1)!.Id;
        d.Buffs.Add(victim, "stealth", source: null);

        var executor = new SkillExecutor(skills, balance, MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            d.Log, rt, d.Buffs);

        // 99.0 ⇒ 命中骰 99 ≥ 命中率（上限 100，实际 < 100）⇒ **未命中** ✓
        executor.Execute(skills.Get("caster_mental_shock"), UnitId.Of("caster"), d.Player, d.Enemy,
            new ScriptedRng(99.0, 99.0), chosenTargets: new[] { 1 });

        Assert.IsFalse(d.Log.Events.OfType<HitEvent>().Any(h => h.Hit && h.Target == victim),
            "前置：本次**未命中** ✓");
        Assert.IsTrue(d.Buffs.HasStateFlag(victim, "stealth"),
            "🔴 ② 未命中 ⇒ **保留潜行**（这正是「选中即解除」旧实现的错处：它会把没打中的也解除）✓");
        Assert.IsFalse(d.Log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "unstealth"),
            "未命中 ⇒ 不得写 `unstealth` ✓");
    }

    [TestMethod]
    public void DeStealth_WithoutIgnoreStealthTag_NeverClears()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = Skills();
        var rt = new SkillRuntimeState();

        UnitId victim = d.Player.UnitRuntimeAt(1)!.Id;
        d.Buffs.Add(victim, "stealth", source: null);

        var executor = new SkillExecutor(skills, balance, MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            d.Log, rt, d.Buffs);

        // 🔴 取同一技能、**摘掉标签** ⇒ 即便命中也不得解除（标签才是闸门）✓
        SkillTemplateConfig shock = skills.Get("caster_mental_shock");
        SkillTemplateConfig noTag = shock with
        {
            Tags = shock.Tags.Where(t => t != FuncTag.IgnoreStealth).ToArray(),
        };

        executor.Execute(noTag, UnitId.Of("caster"), d.Player, d.Enemy, new ScriptedRng(0.0, 0.0), chosenTargets: new[] { 1 });

        Assert.IsTrue(d.Log.Events.OfType<HitEvent>().Any(h => h.Hit && h.Target == victim),
            "前置：确实命中了 ✓");
        Assert.IsTrue(d.Buffs.HasStateFlag(victim, "stealth"),
            "🔴 无 `ignore_stealth` ⇒ **命中也不解除**（潜行只被\"穿透技能\"打破）✓");
    }

    [TestMethod]
    public void DeStealth_NonDamageSkill_DoesNotClear()
    {
        (BattleDirector d, BalanceTable balance) = World();
        var skills = Skills();
        var rt = new SkillRuntimeState();

        // 纯支援技能（治疗/士气）挂上 ignore_stealth ⇒ 没有"命中"概念 ⇒ **不得解除** ✓
        SkillTemplateConfig support = skills.Skills.First(s => s.Damage is null && s.Effects.Count == 0);
        SkillTemplateConfig tagged = support with
        {
            Tags = support.Tags.Append(FuncTag.IgnoreStealth).ToArray(),
        };

        var executor = new SkillExecutor(skills, balance, MoraleEventsConfig.Parse(ReadData("morale_events.json")),
            d.Log, rt, d.Buffs);

        // 找一个我方单位上潜行，然后用支援技能（若目标位允许）——不管打到谁，都不该出现 unstealth ✓
        foreach (UnitRuntime u in d.Player.UnitsInSlotOrder())
        {
            d.Buffs.Add(u.Id, "stealth", source: null);
        }

        executor.Execute(tagged, UnitId.Of("medic"), d.Player, d.Enemy, new ScriptedRng(0.0, 0.0));

        Assert.IsFalse(d.Log.Events.OfType<EffectEvent>().Any(e => e.EffectType == "unstealth"),
            $"🔴 纯支援技能（`{support.Id}`）不过命中管线 ⇒ **不得误解除潜行**（保守正确）✓");
        Assert.IsFalse(d.Log.Events.OfType<HitEvent>().Any(),
            "前置：支援技能不产生 `HitEvent` ✓");
    }

    // ────────────────────────── ④ 我方 `ignore_stealth` 配发 ──────────────────────────

    [TestMethod]
    public void IgnoreStealth_AssignedToEnemyAoeOutputSkills()
    {
        var all = Skills().Skills.ToArray();
        SkillTemplateConfig[] tagged = all.Where(s => s.Tags.Contains(FuncTag.IgnoreStealth)).ToArray();

        Assert.IsTrue(tagged.Length >= 1,
            "🔴 C-1a ④：至少要有一个技能带 `ignore_stealth`（否则该标签是死声明；红线 21）✓");

        // 🔴 注意：`target.side` 是【打谁】不是【谁打】—— 归属要看 `owner_unit` 的阵营
        //    （`caster_mental_shock` 的 `target.side` = "player"，但它由敌方 `caster` 拥有）⚠️
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        foreach (SkillTemplateConfig s in tagged)
        {
            Assert.AreEqual("enemy", units.Get(s.OwnerUnit).Side,
                $"`{s.Id}`（owner_unit={s.OwnerUnit}）：原版 `.ignore_stealth` 属于**敌人**的火器/泼洒技能 " +
                "⇒ 我们只配给敌方技能 ✓");
            Assert.IsTrue(s.Damage is not null,
                $"`{s.Id}`：必须**过命中管线**（有 `damage`）—— 否则「命中即解除」永远触发不了 ✓");
        }

        Assert.IsTrue(tagged.Any(s => s.Tags.Contains(FuncTag.Aoe)),
            "🔴 至少一个是 AOE（原版精神 = 泼洒型武器穿过潜行）✓");
    }

    // ────────────────────────── ③ 敌人开场潜行：不配发（登记） ──────────────────────────

    [TestMethod]
    public void OpeningStealth_NotConfigured_AndThatIsDeliberate()
    {
        // 本用例**锁死"刻意不配发"这一事实**，避免以后有人以为"漏配了"而随手加上：
        //   · 我方原型池只有 melee_soldier / ranged_archer / caster 三个通用原型，
        //     与 DD 的 6 种潜行敌人（Brigand Fusilier / Brigand Hunter / Crone / Pelagic Shaman /
        //     Bone Soldier / Swine Slasher）**不是同一套单位概念** ⇒ 硬套等于编造数据；
        //   · `enemy_ai.json` 的 archetype 结构里**没有**"开场 buff"字段；
        //   · 真正需要它的场合（Veteran 难度起、地牢专属敌人）属于 **M8 内容层**。
        EnemyAiConfig ai = EnemyAiConfig.Parse(ReadData("enemy_ai.json"));
        Assert.IsTrue(ai.Archetypes.Count >= 3, "原型池存在 ✓");

        // 断言：没有任何原型被标注为开场潜行（结构上也没有这个字段 ⇒ 用反射不可靠，改为声明式检查）
        string raw = ReadData("enemy_ai.json");
        Assert.IsFalse(raw.Contains("opening_stealth", StringComparison.Ordinal)
                       || raw.Contains("start_stealth", StringComparison.Ordinal),
            "🔴 刻意不配发：`enemy_ai.json` 不得出现开场潜行字段（真要做时属 M8 内容层，需先定难度门槛与专属敌人）✓");

        Assert.IsFalse(ExtraRule(Buffs().Get("stealth"), "auto_assigned"),
            "潜行 buff 不得声明「自动配发」✓");
    }
}
