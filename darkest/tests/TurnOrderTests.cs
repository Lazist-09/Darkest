using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Turn;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M2 行动序列（T-M2-03）：速度浮动排序 / 同速破平局（#80 + 跨阵营我方先手 §6 默认）/
/// 眩晕跳过即清除 / OnRemoved 实时剔除 / 同 seed 确定性。
/// </summary>
[TestClass]
public sealed class TurnOrderTests
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
            return _percents.Count > 0 ? _percents.Dequeue() : 100.0; // 兜底：超大 roll（不影响 < 判定可见性）
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            _draw++;
            return minInclusive;
        }

        public ulong DrawCount => _draw;
    }

    private static UnitStats Stats(int speed)
        => new(Hp: 10, Attack: 12, PhysDef: 8, Speed: speed, Dodge: 10, Crit: 5, Resilience: 50,
            StunResist: 30, BleedResist: 30, StatDebuffResist: 25, DisplaceResist: 40,
            DeathsDoorResist: 70);

    private static UnitRuntime U(FormationSide side, string id, int speed)
        => new(UnitId.Of(id), side, Stats(speed));

    private static FormationBoard Board(FormationSide side, params (int slot, string id, int speed)[] roster)
    {
        SlotLayout layout = side == FormationSide.Player
            ? new SlotLayout(6, 4, new[] { 5, 6 })
            : new SlotLayout(4, 4, Array.Empty<int>());
        var units = roster.ToDictionary(r => r.slot, r => U(side, r.id, r.speed));
        return new FormationBoard(side, layout, FormationRules.Default(), units);
    }

    private static BalanceTable Table() => BalanceTable.FromTuning(
        TuningConfig.Parse(File.ReadAllText(FindDataFile("tuning.json"))));

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

    [TestMethod]
    public void RoundOrder_Tiebreak_SmallerSlotFirst_And_PlayerFirstAcrossSides()
    {
        FormationBoard player = Board(FormationSide.Player,
            (1, "tank", 5), (2, "warrior", 8), (3, "commissar", 10), (4, "medic", 10));
        FormationBoard enemy = Board(FormationSide.Enemy,
            (1, "melee", 8), (4, "caster", 9));

        var seq = new TurnSequencer(player, enemy, Table());
        IReadOnlyList<UnitId> order = seq.BuildRoundOrder(new ScriptedRng(0, 0, 0, 0, 0, 0));

        // 浮动 0% → 实际速度 = (10,10,9,8,8,5)
        // 同速 10：政委(3) 先于 军医(4)（编号小者先动 #80）；同速 8：我方战士(2) 先于 敌方小兵（跨阵营我方先手）
        string[] expected = { "commissar", "medic", "caster", "warrior", "melee", "tank" };
        CollectionAssert.AreEqual(expected, order.Select(o => o.ToString()).ToArray());
    }

    [TestMethod]
    public void SpeedFloat_RollCanReorder_NeverReorderFixedTiesBeyondPrecision()
    {
        // 坦克 5 ×(1+10%) = 5.5 > 敌方 5 ×(1+0) = 5 → 坦克反超
        FormationBoard player = Board(FormationSide.Player, (1, "tank", 5));
        FormationBoard enemy = Board(FormationSide.Enemy, (1, "melee", 5));

        var seq = new TurnSequencer(player, enemy, Table());
        IReadOnlyList<UnitId> order = seq.BuildRoundOrder(new ScriptedRng(0.10, 0.0)); // 坦克 10%/小兵 0%
        Assert.AreEqual("tank", order[0].ToString(), "速度浮动 10% 使 5.5 > 5 反超");
        Assert.AreEqual("melee", order[1].ToString());

        // 浮动相同 → 我方先手
        var seq2 = new TurnSequencer(player, enemy, Table());
        IReadOnlyList<UnitId> order2 = seq2.BuildRoundOrder(new ScriptedRng(0.0, 0.0));
        Assert.AreEqual("tank", order2[0].ToString(), "同速同浮动 → 我方先手（§6 默认）");
    }

    [TestMethod]
    public void NextActor_SkipsStunnedAndClearsFlag()
    {
        FormationBoard player = Board(FormationSide.Player,
            (1, "tank", 5), (2, "warrior", 8), (3, "commissar", 10), (4, "medic", 10));
        FormationBoard enemy = Board(FormationSide.Enemy, (4, "caster", 9));
        var seq = new TurnSequencer(player, enemy, Table());

        UnitRuntime medic = player.UnitsInSlotOrder().First(u => u.Id == UnitId.Of("medic"));
        medic.Stunned = true;

        seq.BuildRoundOrder(new ScriptedRng(0, 0, 0, 0, 0));

        Assert.AreEqual("commissar", seq.NextActor()!.ToString());
        Assert.AreEqual("caster", seq.NextActor()!.ToString(), "medic 被眩晕跳过");
        Assert.IsFalse(medic.Stunned, "眩晕=跳过 1 次行动随即结束（GDD §2.5）");
        Assert.AreEqual("warrior", seq.NextActor()!.ToString());
    }

    [TestMethod]
    public void OnRemoved_DropsFromRemainingQueue()
    {
        FormationBoard player = Board(FormationSide.Player,
            (1, "tank", 5), (2, "warrior", 8), (3, "commissar", 10), (4, "medic", 10));
        FormationBoard enemy = Board(FormationSide.Enemy, (4, "caster", 9));
        var seq = new TurnSequencer(player, enemy, Table());

        seq.BuildRoundOrder(new ScriptedRng(0, 0, 0, 0, 0));
        seq.OnRemoved(UnitId.Of("caster"));

        var acted = new List<string>();
        while (seq.NextActor() is { } id)
        {
            acted.Add(id.ToString());
        }

        CollectionAssert.DoesNotContain(acted, "caster", "已死亡/离场单位不得再行动");
        Assert.AreEqual(4, acted.Count, "共 5 单位、移除 caster 后应仍行动 4 次");
    }

    [TestMethod]
    public void SameSeed_TwoSequencers_ProduceIdenticalOrder()
    {
        FormationBoard p1 = Board(FormationSide.Player,
            (1, "tank", 5), (2, "warrior", 8), (3, "commissar", 10), (4, "medic", 10));
        FormationBoard e1 = Board(FormationSide.Enemy, (1, "melee", 8), (4, "caster", 9));
        FormationBoard p2 = Board(FormationSide.Player,
            (1, "tank", 5), (2, "warrior", 8), (3, "commissar", 10), (4, "medic", 10));
        FormationBoard e2 = Board(FormationSide.Enemy, (1, "melee", 8), (4, "caster", 9));

        var a = new TurnSequencer(p1, e1, Table());
        var b = new TurnSequencer(p2, e2, Table());

        IReadOnlyList<UnitId> orderA = a.BuildRoundOrder(new RngProvider(20260909));
        IReadOnlyList<UnitId> orderB = b.BuildRoundOrder(new RngProvider(20260909));

        CollectionAssert.AreEqual(
            orderA.Select(o => o.ToString()).ToArray(),
            orderB.Select(o => o.ToString()).ToArray(),
            "同 seed 行动序列必须一致");
        Assert.AreEqual(orderA.Count, 6);
    }
}