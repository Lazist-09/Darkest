using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Pipeline;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M2 结算集成（T-M2-04~09 + T-M2-10 前半）：固定次序管线 / 士气派生 / 弱链 / 死门 /
/// 敌方直接死亡 / 位移 / 同命令流确定性。
/// </summary>
[TestClass]
public sealed class CombatResolutionTests
{
    private sealed class ScriptedRng : IRngProvider
    {
        private readonly Queue<double> _percents;
        private ulong _draw;

        public ScriptedRng(params double[] percents)
        {
            _percents = new Queue<double>(percents);
        }

        public double NextPercent()
        {
            _draw++;
            return _percents.Count > 0 ? _percents.Dequeue() : 50.0;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            _draw++;
            return minInclusive;
        }

        public ulong DrawCount => _draw;
    }

    private static UnitStats MookStats() => new(
        Hp: 50, Attack: 12, PhysDef: 8, Speed: 8, Dodge: 10, Crit: 5, Resilience: 55,
        StunResist: 30, BleedResist: 30, StatDebuffResist: 25, DisplaceResist: 55, DeathsDoorResist: null);

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

    private static (FormationBoard player, FormationBoard enemy, BalanceTable balance) DefaultBoards()
    {
        FormationConfig formation = FormationConfig.Parse(File.ReadAllText(FindDataFile("formation.json")));
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindDataFile("units.json")));
        BalanceTable balance = BalanceTable.FromTuning(TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json"))));
        return (FormationBoardFactory.CreatePlayerBoard(formation, units),
                FormationBoardFactory.CreateEnemyBoard(formation, units), balance);
    }

    private static MoraleEventsConfig MoraleEvents()
        => MoraleEventsConfig.Parse(File.ReadAllText(FindDataFile("morale_events.json")));

    private static DamagePipeline NewPipeline(BalanceTable balance)
        => new(balance, MoraleEvents(), new CombatLog());

    private static T Last<T>(CombatLog log) where T : BattleEvent
        => log.Events.OfType<T>().Last();

    // ------------------------------------------------------------------

    [TestMethod]
    public void Hit_RollEqualsThreshold_Misses_NoDamageOrMorale()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance) = DefaultBoards();
        var log = new CombatLog();
        var pipeline = new DamagePipeline(balance, MoraleEvents(), log);

        // 战士打近战小兵：命中率 = 100−10+0 = 90；roll=90 → 未命中
        pipeline.Execute(new SkillFixture("probe", UnitId.Of("warrior"), FormationSide.Enemy,
            new[] { 1 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null),
            player, enemy, new ScriptedRng(90.0));

        HitEvent hit = Last<HitEvent>(log);
        Assert.IsFalse(hit.Hit);
        Assert.AreEqual(90, hit.HitRate);
        Assert.IsFalse(log.Events.OfType<DamageEvent>().Any(), "未命中不得产生伤害事件");
        Assert.IsFalse(log.Events.OfType<MoraleEvent>().Any(), "未命中不得产生士气事件");
        Assert.AreEqual(60, enemy.UnitRuntimeAt(1)!.CurrentHp);
    }

    [TestMethod]
    public void PhysicalDamage_WarriorToMeleeMook_Is9_AndMoraleUnchanged()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance) = DefaultBoards();
        var pipeline = NewPipeline(balance);

        pipeline.Execute(new SkillFixture("probe", UnitId.Of("warrior"), FormationSide.Enemy,
            new[] { 1 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null),
            player, enemy, new ScriptedRng(0.0, 100.0)); // hit roll 0<90 命中；暴击 roll 100≥5 不暴击

        Assert.AreEqual(9, Last<DamageEvent>(pipeline.Log).Amount, "战士→近战小兵 = 9（§7.1）");
        Assert.AreEqual(51, enemy.UnitRuntimeAt(1)!.CurrentHp);
        Assert.IsFalse(pipeline.Log.Events.OfType<MoraleEvent>().Any(), "物理不掉士气（#157）");
    }

    [TestMethod]
    public void MentalDamage_CasterToWarrior_MoraleMinus8()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance) = DefaultBoards();
        var pipeline = NewPipeline(balance);
        pipeline.InitializeMorale(player);

        // 敌方施法者打我方 2 号位（战士：韧性 50 → 精神减免 20%）
        pipeline.Execute(new SkillFixture("probe", UnitId.Of("caster"), FormationSide.Player,
            new[] { 2 }, HitMod: 0, CritMod: 0, Axis: "mental", Segments: new[] { 0.8 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null),
            player, enemy, new ScriptedRng(0.0, 100.0));

        Assert.AreEqual(32, player.UnitRuntimeAt(2)!.CurrentHp, "战士 40−8");
        MoraleEvent morale = Last<MoraleEvent>(pipeline.Log);
        Assert.AreEqual("mental_hit", morale.Source);
        Assert.AreEqual(-8, morale.Delta);
        Assert.AreEqual(42, player.UnitRuntimeAt(2)!.Morale);
    }

    [TestMethod]
    public void MentalAoe_TwoTargets_EachMinus5_NoTeamExtra()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance) = DefaultBoards();
        var pipeline = NewPipeline(balance);
        pipeline.InitializeMorale(player);

        pipeline.Execute(new SkillFixture("probe", UnitId.Of("caster"), FormationSide.Player,
            new[] { 1, 2 }, HitMod: 0, CritMod: 0, Axis: "mental", Segments: new[] { 0.8 }, IsAoe: true,
            Effects: Array.Empty<EffectRequest>(), Displacement: null),
            player, enemy, new ScriptedRng(0.0, 100.0, 0.0, 100.0));

        MoraleEvent[] moraleEvents = pipeline.Log.Events.OfType<MoraleEvent>().ToArray();
        Assert.AreEqual(2, moraleEvents.Length, "AOE 精神每目标各自 −5（per_target）");
        Assert.IsTrue(moraleEvents.All(e => e.Source == "mental_aoe_hit" && e.Delta == -5));
        Assert.AreEqual(45, player.UnitRuntimeAt(1)!.Morale);
        Assert.AreEqual(45, player.UnitRuntimeAt(2)!.Morale);
    }

    [TestMethod]
    public void Stun_NoProbability_AppliedWithoutDraw_And_ProbabilityBoundary()
    {
        (FormationBoard player, _, BalanceTable balance) = DefaultBoards();
        var pipe1 = NewPipeline(balance);
        // 无概率直挂（O-24）：不掷骰直接生效（目标 = 2 号位战士）
        int drawsBefore = pipe1.Log.Events.OfType<RngDraw>().Count();
        pipe1.Execute(new SkillFixture("probe", UnitId.Of("caster"), FormationSide.Player,
            new[] { 2 }, HitMod: 0, CritMod: 0, Axis: "mental", Segments: new[] { 0.5 }, IsAoe: false,
            Effects: new[] { new EffectRequest("stun", LabeledPercent: null, ResistAxis: "stun_resist", Stat: null, Delta: 0) },
            Displacement: null),
            player, DefaultBoards().enemy, new ScriptedRng(0.0, 100.0));
        Assert.IsTrue(player.UnitRuntimeAt(2)!.Stunned, "无概率眩晕直挂");
        Assert.AreEqual(drawsBefore + 2, pipe1.Log.Events.OfType<RngDraw>().Count(), "无概率效果不额外掷骰");

        // 概率型边界：40% × (1−30%) = 28%（战士 stun_resist 30）；roll=28 不触发、roll=27.9 触发
        (FormationBoard p2, FormationBoard e2, BalanceTable b2) = DefaultBoards();
        var pipeMiss = NewPipeline(b2);
        pipeMiss.Execute(new SkillFixture("probe", UnitId.Of("caster"), FormationSide.Player,
            new[] { 2 }, HitMod: 0, CritMod: 0, Axis: "mental", Segments: new[] { 0.5 }, IsAoe: false,
            Effects: new[] { new EffectRequest("stun", LabeledPercent: 40, ResistAxis: "stun_resist", Stat: null, Delta: 0) },
            Displacement: null), p2, e2, new ScriptedRng(0.0, 100.0, 28.0));
        Assert.IsFalse(p2.UnitRuntimeAt(2)!.Stunned, "roll==阈值 → 不触发");
        Assert.AreEqual(28.0, Last<EffectEvent>(pipeMiss.Log).ActualChance, 1e-9);

        (FormationBoard p3, FormationBoard e3, BalanceTable b3) = DefaultBoards();
        var pipeHit = NewPipeline(b3);
        pipeHit.Execute(new SkillFixture("probe", UnitId.Of("caster"), FormationSide.Player,
            new[] { 2 }, HitMod: 0, CritMod: 0, Axis: "mental", Segments: new[] { 0.5 }, IsAoe: false,
            Effects: new[] { new EffectRequest("stun", LabeledPercent: 40, ResistAxis: "stun_resist", Stat: null, Delta: 0) },
            Displacement: null), p3, e3, new ScriptedRng(0.0, 100.0, 27.9));
        Assert.IsTrue(p3.UnitRuntimeAt(2)!.Stunned, "27.9 < 28 → 触发");
    }

    [TestMethod]
    public void Displacement_GeBoundary_Succeed_FailKeepsDamage()
    {
        // 敌方板自定义：m1/m2 便于断言交换（位移抗性 55）
        var enemyBoard = new FormationBoard(FormationSide.Enemy,
            new SlotLayout(4, 4, Array.Empty<int>()), FormationRules.Default(),
            new Dictionary<int, UnitRuntime>
            {
                [1] = new(UnitId.Of("m1"), FormationSide.Enemy, MookStats()),
                [2] = new(UnitId.Of("m2"), FormationSide.Enemy, MookStats()),
            });
        (FormationBoard player, _, BalanceTable balance) = DefaultBoards();

        // roll=55 → >= 55 成功：与 m2 交换
        var pipe1 = NewPipeline(balance);
        pipe1.Execute(new SkillFixture("push", UnitId.Of("warrior"), FormationSide.Enemy,
            new[] { 1 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(),
            Displacement: new SkillDisplace(FromPos: 1, ToPos: 2, Distance: 1, SelfDisplacement: false)),
            player, enemyBoard, new ScriptedRng(0.0, 100.0, 55.0));
        DisplaceEvent disp = Last<DisplaceEvent>(pipe1.Log);
        Assert.IsTrue(disp.PassedResist && disp.ChainSucceeded, "roll==抗性 → 位移成功（唯一 >= 例外）");
        Assert.AreEqual("m2", enemyBoard.UnitAt(1)!.ToString(), "交换链：m1 与 m2 互换");
        Assert.AreEqual("m1", enemyBoard.UnitAt(2)!.ToString());
        Assert.IsFalse(pipe1.Log.Events.OfType<DeathDoorEvent>().Any(), "位移不触发死门（#117）");

        // roll=54.9 → 失败：伤害照常（DamageEvent 仍在），板不动
        var enemyBoard2 = new FormationBoard(FormationSide.Enemy,
            new SlotLayout(4, 4, Array.Empty<int>()), FormationRules.Default(),
            new Dictionary<int, UnitRuntime>
            {
                [1] = new(UnitId.Of("m1"), FormationSide.Enemy, MookStats()),
                [2] = new(UnitId.Of("m2"), FormationSide.Enemy, MookStats()),
            });
        var pipe2 = NewPipeline(balance);
        pipe2.Execute(new SkillFixture("push", UnitId.Of("warrior"), FormationSide.Enemy,
            new[] { 1 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(),
            Displacement: new SkillDisplace(FromPos: 1, ToPos: 2, Distance: 1, SelfDisplacement: false)),
            player, enemyBoard2, new ScriptedRng(0.0, 100.0, 54.9));
        DisplaceEvent disp2 = Last<DisplaceEvent>(pipe2.Log);
        Assert.IsFalse(disp2.PassedResist, "54.9 < 55 → 位移失败");
        Assert.IsTrue(pipe2.Log.Events.OfType<DamageEvent>().Any(), "位移失败不影响伤害结算（combat_math §3）");
        Assert.AreEqual("m1", enemyBoard2.UnitAt(1)!.ToString());
    }

    [TestMethod]
    public void WeakChain_EnterThenDeathDoor_OrderAndProbability()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance) = DefaultBoards();
        var pipeline = NewPipeline(balance);
        UnitRuntime warrior = player.UnitRuntimeAt(2)!; // 2 号位 = 战士（死门抗性 70）
        warrior.CurrentHp = 1; // 濒死，便于第一击归零

        var skill = new SkillFixture("probe", UnitId.Of("melee_soldier"), FormationSide.Player,
            new[] { 2 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null);

        // 第 1 击：HP 1 → 归零 → 进虚弱（roll: 命中0, 暴击100, 崩溃50）
        pipeline.Execute(skill, player, enemy, new ScriptedRng(0.0, 100.0, 50.0));
        Assert.IsTrue(warrior.Weak, "归零不死亡进虚弱");
        Assert.AreEqual(1, warrior.CurrentHp, "HP 锁 1");
        Assert.AreEqual(0, warrior.Morale, "士气立即置 0");
        Assert.IsTrue(pipeline.Log.Events.OfType<CollapseRollEvent>().Any(), "崩溃判定调用点已记录");
        Assert.IsTrue(pipeline.Log.Events.OfType<WeakEnterEvent>().Any());
        Assert.IsTrue(pipeline.Log.Events.OfType<MoraleEvent>().Any(m => m.Source == "ally_enters_weak"));

        // 第 2 击：虚弱受伤 → weak_hit −5（先）→ 死门 roll=69 < 70 → 存活、HP 仍 1
        var pipe2 = NewPipeline(balance);
        FormationBoard p2 = DefaultBoards().player;
        p2.UnitRuntimeAt(2)!.CurrentHp = 1;
        p2.UnitRuntimeAt(2)!.Weak = true;
        pipe2.Morale.ResetTurnCounters();
        pipe2.Execute(skill, p2, enemy, new ScriptedRng(0.0, 100.0, 69.0));
        UnitRuntime w2 = p2.UnitRuntimeAt(2)!;
        Assert.IsTrue(w2.Weak);
        Assert.AreEqual(1, w2.CurrentHp, "O-15：虚弱中伤害不改 HP");
        DeathDoorEvent dd = Last<DeathDoorEvent>(pipe2.Log);
        Assert.IsTrue(dd.Survived, "69 < 70 → 存活");
        int weakHitIdx = pipe2.Log.Events.ToList().FindIndex(e => e is MoraleEvent { Source: "weak_hit_any_damage" });
        int ddIdx = pipe2.Log.Events.ToList().FindIndex(e => e == dd);
        Assert.IsTrue(weakHitIdx >= 0 && ddIdx > weakHitIdx, "死门判定在 −5 士气之后（combat_math §6）");

        // 第 3 击：死门 roll=70 → 失败 → 真死 → 移除+靠齐+ally_death −15
        var pipe3 = NewPipeline(balance);
        FormationBoard p3 = DefaultBoards().player;
        p3.UnitRuntimeAt(2)!.CurrentHp = 1;
        p3.UnitRuntimeAt(2)!.Weak = true;
        pipe3.Morale.ResetTurnCounters();
        pipe3.Execute(skill, p3, enemy, new ScriptedRng(0.0, 100.0, 70.0));
        Assert.IsFalse(Last<DeathDoorEvent>(pipe3.Log).Survived, "70 < 70 不成立 → 死亡");
        Assert.IsTrue(pipe3.Log.Events.OfType<DeathEvent>().Any(e => e.IsPlayer));
        Assert.AreEqual(5, p3.OccupiedPositions(false).Count, "真死单位已移除（靠齐收敛，人数 −1）");
        Assert.AreNotEqual(UnitId.Of("warrior"), p3.UnitAt(2), "2 号位已由后方补位");
        Assert.IsTrue(pipe3.Log.Events.OfType<MoraleEvent>().Any(m => m.Source == "ally_death"));
        Assert.AreEqual(70, Last<DeathDoorEvent>(pipe3.Log).SurvivePercent);
    }

    [TestMethod]
    public void EnemyKill_DirectDeath_NoWeakChain_KillEnemyTeamOnce()
    {
        (FormationBoard player, FormationBoard enemy, BalanceTable balance) = DefaultBoards();
        var pipeline = NewPipeline(balance);
        enemy.UnitRuntimeAt(1)!.CurrentHp = 5; // 一击 9 点致死

        pipeline.Execute(new SkillFixture("probe", UnitId.Of("warrior"), FormationSide.Enemy,
            new[] { 1 }, HitMod: 0, CritMod: 0, Axis: "physical", Segments: new[] { 1.0 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null),
            player, enemy, new ScriptedRng(0.0, 100.0));

        Assert.IsTrue(pipeline.Log.Events.OfType<DeathEvent>().Any(e => !e.IsPlayer), "敌方直接死亡");
        Assert.IsFalse(pipeline.Log.Events.OfType<WeakEnterEvent>().Any(), "敌方无虚弱链");
        Assert.IsFalse(pipeline.Log.Events.OfType<DeathDoorEvent>().Any(), "敌方无死门");
        Assert.IsTrue(pipeline.Log.Events.OfType<MoraleEvent>().Any(m => m.Source == "kill_enemy" && m.Delta == 10));
        Assert.AreEqual(3, enemy.OccupiedPositions(false).Count, "击杀 → 移除 → 靠齐（敌方 4→3）");
    }

    [TestMethod]
    public void ExplicitMoraleEffect_ReplacesDerivedMentalHit_O21()
    {
        // O-21/#170：威吓箭显式 morale_effects −4 取代精神派生 −8（不叠加）
        (FormationBoard player, FormationBoard enemy, BalanceTable balance) = DefaultBoards();
        var pipeline = NewPipeline(balance);
        pipeline.InitializeMorale(player);

        pipeline.Execute(new SkillFixture("intimidating_shot", UnitId.Of("caster"), FormationSide.Player,
            new[] { 2 }, HitMod: 0, CritMod: 0, Axis: "mental", Segments: new[] { 0.5 }, IsAoe: false,
            Effects: Array.Empty<EffectRequest>(), Displacement: null,
            ExplicitMoraleEffects: new[] { new MoraleEffectRequest("targets", -4) }),
            player, enemy, new ScriptedRng(0.0, 100.0));

        MoraleEvent[] moraleEvents = pipeline.Log.Events.OfType<MoraleEvent>().ToArray();
        Assert.AreEqual(1, moraleEvents.Length, "显式 −4 取代派生，只有一条士气事件");
        Assert.AreEqual(-4, moraleEvents[0].Delta);
        Assert.IsFalse(pipeline.Log.Events.OfType<MoraleEvent>().Any(m => m.Source == "mental_hit"),
            "不再出现精神派生 −8（O-21）");
        Assert.AreEqual(46, player.UnitRuntimeAt(2)!.Morale);
    }

    [TestMethod]
    public void Determinism_SameSeed_SameCommand_IdenticalLog()
    {
        (FormationBoard p1, FormationBoard e1, BalanceTable b1) = DefaultBoards();
        (FormationBoard p2, FormationBoard e2, BalanceTable b2) = DefaultBoards();
        var log1 = new CombatLog();
        var log2 = new CombatLog();
        var pipe1 = new DamagePipeline(b1, MoraleEvents(), log1);
        var pipe2 = new DamagePipeline(b2, MoraleEvents(), log2);

        var skill = new SkillFixture("probe", UnitId.Of("caster"), FormationSide.Player,
            new[] { 1, 2 }, HitMod: 0, CritMod: 0, Axis: "mental", Segments: new[] { 0.8 }, IsAoe: true,
            Effects: new[] { new EffectRequest("stun", LabeledPercent: 40, ResistAxis: "stun_resist", Stat: null, Delta: 0) },
            Displacement: null);

        pipe1.Execute(skill, p1, e1, new RngProvider(20260909));
        pipe2.Execute(skill, p2, e2, new RngProvider(20260909));

        Assert.AreEqual(log1.Events.Count, log2.Events.Count, "同 seed 同命令 → 事件数一致");
        for (int i = 0; i < log1.Events.Count; i++)
        {
            Assert.AreEqual(log1.Events[i], log2.Events[i], $"第 {i} 条事件必须逐项一致（含 RngDraw 序列）");
        }
    }
}