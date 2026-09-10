using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Enemy;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>M5 敌方固定优先级 AI（T-M5-02）。</summary>
[TestClass]
public sealed class EnemyAiTests
{
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _p;
        private ulong _d;

        public ScriptedRng(params double[] percents)
        {
            _p = new Queue<double>(percents);
        }

        public double NextPercent()
        {
            _d++;
            return _p.Count > 0 ? _p.Dequeue() : 0.0;
        }

        public int NextInt(int a, int b)
        {
            _d++;
            return a;
        }

        public ulong DrawCount => _d;
    }

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

    private static (FormationBoard player, FormationBoard enemy, EnemyAi ai, SkillsConfig skills) World()
    {
        FormationConfig formation = FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json")));
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        SkillsConfig skills = SkillsConfig.Parse(File.ReadAllText(FindDataFile("skills.json")));
        EnemyAiConfig aiCfg = EnemyAiConfig.Parse(File.ReadAllText(FindDataFile("enemy_ai.json")));
        var ai = new EnemyAi(aiCfg, skills, new SkillRuntimeState());
        return (FormationBoardFactory.CreatePlayerBoard(formation, units),
                FormationBoardFactory.CreateEnemyBoard(formation, units), ai, skills);
    }

    [TestMethod]
    public void MeleeSoldier_InBack_Charges_ElseHeavySlash()
    {
        (FormationBoard player, FormationBoard enemy, EnemyAi ai, _) = World();
        UnitRuntime m1 = enemy.UnitRuntimeAt(1)!;
        UnitRuntime m2 = enemy.UnitRuntimeAt(2)!;

        // m1 在前排 1 → 重劈（规则② 默认）；m2 在 2 → 重劈
        SkillChoice? c1 = ai.Choose(m1, enemy, player, null, new ScriptedRng(), new CombatLog());
        Assert.AreEqual("melee_heavy_slash", c1!.SkillId);
        SkillChoice? c2 = ai.Choose(m2, enemy, player, null, new ScriptedRng(), new CombatLog());
        Assert.AreEqual("melee_heavy_slash", c2!.SkillId);

        // 把 m1 移除、m2 推到 3（唯一 melee，避免同 id 首匹配歧义）→ 突进（规则① self_slot_in [3,4]）
        enemy.RemoveUnitAt(1);
        enemy.TrySwapChain(m2.Id, 2, 3, 1);
        UnitRuntime pushed = enemy.UnitRuntimeAt(3)!;
        Assert.AreEqual("melee_soldier", pushed.Id.Value);
        SkillChoice? c3 = ai.Choose(pushed, enemy, player, null, new ScriptedRng(), new CombatLog());
        Assert.AreEqual("melee_charge", c3!.SkillId, "被推到 3/4 → 突进归位");
    }

    [TestMethod]
    public void Archer_InFront_Retreats_ElsePrecise()
    {
        (FormationBoard player, FormationBoard enemy, EnemyAi ai, _) = World();
        UnitRuntime archer = enemy.UnitRuntimeAt(3)!;

        // 射手在 3 → 精准射击（规则② 默认；威吓箭③不可达——O-28 不静默改序）
        SkillChoice? c1 = ai.Choose(archer, enemy, player, null, new ScriptedRng(), new CombatLog());
        Assert.AreEqual("ranged_precise_shot", c1!.SkillId);

        // 把射手换到 1 → 后撤（规则① self_slot_in [1,2]）
        enemy.TrySwapChain(archer.Id, 3, 1, 2);
        SkillChoice? c2 = ai.Choose(enemy.UnitRuntimeAt(1)!, enemy, player, null, new ScriptedRng(), new CombatLog());
        Assert.AreEqual("ranged_retreat", c2!.SkillId);
    }

    [TestMethod]
    public void Caster_AoeWhenTwoFrontTargets_ElseWhisper_StunnedAt1Skips()
    {
        (FormationBoard player, FormationBoard enemy, EnemyAi ai, _) = World();
        UnitRuntime caster = enemy.UnitRuntimeAt(4)!;

        // 我方 1、2 均有（默认满编）→ 精神震荡（目标占位 ≥2）
        SkillChoice? c1 = ai.Choose(caster, enemy, player, null, new ScriptedRng(), new CombatLog());
        Assert.AreEqual("caster_mental_shock", c1!.SkillId);

        // 我方 1、2 只剩 1 个 → 恐惧低语（默认）
        player.RemoveUnitAt(2);
        SkillChoice? c2 = ai.Choose(caster, enemy, player, null, new ScriptedRng(), new CombatLog());
        Assert.AreEqual("caster_fear_whisper", c2!.SkillId);

        // 施法者被推至敌方 1（站位不符全技能）→ 空过
        enemy.TrySwapChain(caster.Id, 4, 1, 3);
        SkillChoice? c3 = ai.Choose(enemy.UnitRuntimeAt(1)!, enemy, player, null, new ScriptedRng(), new CombatLog());
        Assert.IsNull(c3, "推至 1 → 站位不符全部技能 → 本回合不行动");
    }

    [TestMethod]
    public void RandomDisabled_Deterministic_NoDraws()
    {
        (FormationBoard player, FormationBoard enemy, EnemyAi ai, _) = World();
        UnitRuntime m1 = enemy.UnitRuntimeAt(1)!;
        var log = new CombatLog();
        var results = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            SkillChoice? c = ai.Choose(m1, enemy, player, null, new ScriptedRng(), log);
            results.Add(c!.SkillId);
        }

        Assert.AreEqual(1, results.Count, "default off 下 100 次决策相同（纯确定性）");
        Assert.AreEqual(0, log.Events.OfType<RngDraw>().Count(), "未启用随机 → 无抽取");
    }
}