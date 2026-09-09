using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Data;
using Darkest.Gameplay.Sim.Board;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M1 阵型骨架单测（tasks/m1_formation.md T-M1-01~05）：三态查询 / 交换链 / 靠齐 /
/// 障碍建模 / 位移预览 dry-run 一致性。内核零 Godot。
/// </summary>
[TestClass]
public sealed class BoardTests
{
    private static readonly SlotLayout PlayerLayout = new(6, 4, new[] { 5, 6 });
    private static readonly SlotLayout EnemyLayout = new(4, 4, Array.Empty<int>());

    // ------------------------------------------------------------------
    // 测试脚手架
    // ------------------------------------------------------------------

    private static UnitStats DummyStats()
        => new(Hp: 10, Attack: 12, PhysDef: 8, Speed: 8, Dodge: 10, Crit: 5, Resilience: 50,
            StunResist: 30, BleedResist: 30, StatDebuffResist: 25, DisplaceResist: 40,
            DeathsDoorResist: null);

    private static UnitRuntime U(string id, FormationSide side, bool weak = false)
        => new(UnitId.Of(id), side, DummyStats(), weak);

    private static FormationBoard MakeBoard(FormationSide side, params (int slot, string id, bool weak)[] roster)
    {
        SlotLayout layout = side == FormationSide.Player ? PlayerLayout : EnemyLayout;
        var units = roster.ToDictionary(r => r.slot, r => U(r.id, side, r.weak));
        return new FormationBoard(side, layout, FormationRules.Default(), units);
    }

    private static FormationBoard MakeBoardWithObstacles(
        FormationSide side,
        (int slot, string id, bool weak)[] roster,
        params (int slot, int? hp)[] obstacles)
    {
        SlotLayout layout = side == FormationSide.Player ? PlayerLayout : EnemyLayout;
        var units = roster.ToDictionary(r => r.slot, r => U(r.id, side, r.weak));
        var obs = obstacles.ToDictionary(o => o.slot, o => new ObstacleRuntime(o.hp));
        return new FormationBoard(side, layout, FormationRules.Default(), units, obs);
    }

    private static string Snapshot(IFormation board)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        return string.Join("|", Enumerable.Range(1, count)
            .Select(p => $"{p}:{board.GetSlot(p)}:{(board.UnitAt(p)?.ToString() ?? "-")}"));
    }

    /// <summary>紧凑单位排布视图（"-"=空槽），便于断言。</summary>
    private static string UnitsBrief(IFormation board)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        return string.Join(",", Enumerable.Range(1, count).Select(p => board.UnitAt(p)?.ToString() ?? "-"));
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

        throw new FileNotFoundException($"data/{name} 未找到（应从 darkest/ 运行测试）。");
    }

    private static string FindFormationJson() => FindDataFile("formation.json");

    private static string FindUnitsJson() => FindDataFile("units.json");

    /// <summary>按序回放 Step 序列（模型版），用于断言「序列可无歧义还原最终板」。</summary>
    private static string ReplayAndSnapshot(
        FormationBoard board, IReadOnlyList<SlotChange> steps)
    {
        return ReplayAndSnapshot(board, steps, upto: -1);
    }

    private static string ReplayAndSnapshot(
        FormationBoard board, IReadOnlyList<SlotChange> steps, int upto)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        var state = new SlotState[count];
        var unit = new UnitId?[count];
        for (int p = 1; p <= count; p++)
        {
            state[p - 1] = board.GetSlot(p);
            unit[p - 1] = board.UnitAt(p);
        }

        int effective = upto < 0 ? steps.Count : upto;
        for (int i = 0; i < effective; i++)
        {
            SlotChange step = steps[i];
            int a = step.FromSlot - 1;
            int b = step.ToSlot - 1;
            switch (step.Kind)
            {
                case SlotChangeKind.Swap:
                    (state[a], state[b]) = (state[b], state[a]);
                    (unit[a], unit[b]) = (unit[b], unit[a]);
                    break;
                case SlotChangeKind.Move:
                    state[a] = step.FromSlotState; // 移走后（Empty / Blocked 不会用于 Move 源）
                    unit[a] = null;
                    state[b] = step.ToSlotState;
                    unit[b] = step.UnitA;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return string.Join("|", Enumerable.Range(1, count)
            .Select(p => $"{p}:{state[p - 1]}:{(unit[p - 1]?.ToString() ?? "-")}"));
    }

    /// <summary>仅回放前 upto 步后的「槽位:单位」视图（逐级断言用）。</summary>
    private static string ReplayUnitView(FormationBoard board, IReadOnlyList<SlotChange> steps, int upto)
    {
        int count = board.Side == FormationSide.Player ? 6 : 4;
        var unit = new UnitId?[count];
        for (int p = 1; p <= count; p++)
        {
            unit[p - 1] = board.UnitAt(p);
        }

        for (int i = 0; i < upto; i++)
        {
            SlotChange step = steps[i];
            int a = step.FromSlot - 1;
            int b = step.ToSlot - 1;
            if (step.Kind == SlotChangeKind.Swap)
            {
                (unit[a], unit[b]) = (unit[b], unit[a]);
            }
            else
            {
                unit[a] = null;
                unit[b] = step.UnitA;
            }
        }

        return string.Join(",", Enumerable.Range(1, count)
            .Select(p => $"{p}:{unit[p - 1]?.ToString() ?? "-"}"));
    }

    // ------------------------------------------------------------------
    // T-M1-01：数据结构与编成装载
    // ------------------------------------------------------------------

    [TestMethod]
    public void Board_FromFormationJson_PlayerAndEnemyRosterMatch()
    {
        string json = File.ReadAllText(FindFormationJson());
        FormationConfig cfg = FormationConfig.Parse(json);
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindUnitsJson()));

        FormationBoard player = FormationBoardFactory.CreatePlayerBoard(cfg, units);
        Assert.AreEqual(FormationSide.Player, player.Side);
        Assert.AreEqual(6, player.SlotCount);
        string[] expectedPlayer = { "tank", "warrior", "commissar", "medic", "warrior", "medic" };
        for (int p = 1; p <= 6; p++)
        {
            Assert.AreEqual(SlotState.Occupied, player.GetSlot(p), $"player 槽 {p} 应为 Occupied");
            Assert.AreEqual(expectedPlayer[p - 1], player.UnitAt(p)!.ToString(), $"player 槽 {p} 单位");
        }

        Assert.AreEqual(SlotKind.Combat, player.SlotKindAt(1));
        Assert.AreEqual(SlotKind.Support, player.SlotKindAt(5));
        Assert.AreEqual(SlotKind.Support, player.SlotKindAt(6));

        FormationBoard enemy = FormationBoardFactory.CreateEnemyBoard(cfg, units);
        Assert.AreEqual(4, enemy.SlotCount);
        string[] expectedEnemy = { "melee_soldier", "melee_soldier", "ranged_archer", "caster" };
        for (int p = 1; p <= 4; p++)
        {
            Assert.AreEqual(SlotState.Occupied, enemy.GetSlot(p));
            Assert.AreEqual(expectedEnemy[p - 1], enemy.UnitAt(p)!.ToString());
        }
    }

    [TestMethod]
    public void SlotState_And_Side_JsonVocabulary_Match()
    {
        string[] states = Enum.GetNames(typeof(SlotState)).Select(s => s.ToLowerInvariant()).OrderBy(s => s).ToArray();
        CollectionAssert.AreEqual(new[] { "blocked", "empty", "occupied" }, states);

        string[] sides = Enum.GetNames(typeof(FormationSide)).Select(s => s.ToLowerInvariant()).OrderBy(s => s).ToArray();
        CollectionAssert.AreEqual(new[] { "enemy", "player" }, sides);
    }

    [TestMethod]
    public void FormationConfig_InvalidEnemySupportSlots_Throws()
    {
        string json = """
            { "player": { "slot_count": 6, "combat_slots": 4, "support_slots": [5,6] },
              "enemy": { "slot_count": 4, "combat_slots": 4, "support_slots": [5] },
              "numeration": "center_outward",
              "initial_roster": { "player": [], "enemy": [] },
              "obstacles": [],
              "rules": { "boundary_as_hard_wall": true, "displacement_only_via_swap_chain": true,
                         "obstacle_swaps_like_unit": true, "close_up_on_death_immediate": true,
                         "close_up_ignores_obstacle": true, "close_up_enemy_symmetric": true,
                         "swap_player_initiated": true, "slot_three_state": true,
                         "deaths_and_close_up_separate_from_displacement": true } }
            """;
        Assert.ThrowsException<InvalidDataException>(() => FormationConfig.Parse(json));
    }

    [TestMethod]
    public void FormationConfig_InvalidRosterSlot_Throws()
    {
        string json = """
            { "player": { "slot_count": 6, "combat_slots": 4, "support_slots": [5,6] },
              "enemy": { "slot_count": 4, "combat_slots": 4, "support_slots": [] },
              "numeration": "center_outward",
              "initial_roster": { "player": [ { "slot": 7, "unit": "tank" } ], "enemy": [] },
              "obstacles": [],
              "rules": { "boundary_as_hard_wall": true, "displacement_only_via_swap_chain": true,
                         "obstacle_swaps_like_unit": true, "close_up_on_death_immediate": true,
                         "close_up_ignores_obstacle": true, "close_up_enemy_symmetric": true,
                         "swap_player_initiated": true, "slot_three_state": true,
                         "deaths_and_close_up_separate_from_displacement": true } }
            """;
        Assert.ThrowsException<InvalidDataException>(() => FormationConfig.Parse(json));
    }

    [TestMethod]
    public void FormationConfig_RulesNotAllTrue_Throws()
    {
        string json = """
            { "player": { "slot_count": 6, "combat_slots": 4, "support_slots": [5,6] },
              "enemy": { "slot_count": 4, "combat_slots": 4, "support_slots": [] },
              "numeration": "center_outward",
              "initial_roster": { "player": [], "enemy": [] },
              "obstacles": [],
              "rules": { "boundary_as_hard_wall": false, "displacement_only_via_swap_chain": true,
                         "obstacle_swaps_like_unit": true, "close_up_on_death_immediate": true,
                         "close_up_ignores_obstacle": true, "close_up_enemy_symmetric": true,
                         "swap_player_initiated": true, "slot_three_state": true,
                         "deaths_and_close_up_separate_from_displacement": true } }
            """;
        Assert.ThrowsException<InvalidDataException>(() => FormationConfig.Parse(json));
    }

    [TestMethod]
    public void Board_TryGetSlotOutOfRange_Throws()
    {
        FormationBoard board = MakeBoard(FormationSide.Player, (1, "tank", false));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => board.GetSlot(0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => board.GetSlot(7));
    }

    // ------------------------------------------------------------------
    // T-M1-02：逐级交换链
    // ------------------------------------------------------------------

    [TestMethod]
    public void TrySwapChain_Example_D4to1_StepwiseMatches()
    {
        FormationBoard board = MakeBoard(FormationSide.Enemy,
            (1, "A", false), (2, "B", false), (3, "C", false), (4, "D", false));

        FormationBoard pre = board.CreatePreviewSnapshot();
        DisplaceResult result = board.TrySwapChain(UnitId.Of("D"), fromPos: 4, toPos: 1, distance: 3);

        Assert.IsTrue(result.Success, $"应成功；失败原因 {result.Failure}");
        Assert.AreEqual(3, result.Steps.Count, "必须逐步交换（每步=一次双槽交换）");

        // 每步后逐槽断言（在【调用前快照】上按序回放前缀步骤，formation.md §2 示例逐位一致）
        Assert.AreEqual("1:A,2:B,3:D,4:C", ReplayUnitView(pre, result.Steps, 1), "第 1 步后：4↔3");
        Assert.AreEqual("1:A,2:D,3:B,4:C", ReplayUnitView(pre, result.Steps, 2), "第 2 步后：3↔2");
        Assert.AreEqual("1:D,2:A,3:B,4:C", ReplayUnitView(pre, result.Steps, 3), "第 3 步后：2↔1");
        Assert.AreEqual("D,A,B,C", UnitsBrief(board), "最终 1:D 2:A 3:B 4:C");
        Assert.AreEqual(DisplaceFailureReason.None, result.Failure);

        // 步骤序列（槽位对 + 单位标识，顺序即动画播放顺序）
        Assert.AreEqual(4, result.Steps[0].FromSlot);
        Assert.AreEqual(3, result.Steps[0].ToSlot);
        Assert.IsTrue(result.Steps[0].UnitA == UnitId.Of("D") && result.Steps[0].UnitB == UnitId.Of("C"));

        Assert.AreEqual(3, result.Steps[1].FromSlot);
        Assert.AreEqual(2, result.Steps[1].ToSlot);

        Assert.AreEqual(2, result.Steps[2].FromSlot);
        Assert.AreEqual(1, result.Steps[2].ToSlot);
    }

    [TestMethod]
    public void TrySwapChain_Property_NoEmptyProduced_OrderPreserved()
    {
        var random = new Random(20260909);
        for (int trial = 0; trial < 150; trial++)
        {
            // 随机满阵（Occupied + Blocked 混合，留若干 Empty）
            var roster = new List<(int slot, string id, bool weak)>();
            var obstacles = new List<(int slot, int? hp)>();
            var taken = new HashSet<int>();
            int unitCount = random.Next(4, 7);
            while (taken.Count < unitCount)
            {
                int s = random.Next(1, 7);
                if (taken.Add(s))
                {
                    roster.Add((s, $"U{trial}-{s}", false));
                }
            }

            int obsCount = random.Next(0, Math.Min(2, 6 - unitCount));
            while (obstacles.Count < obsCount)
            {
                int s = random.Next(1, 7);
                if (taken.Add(s))
                {
                    obstacles.Add((s, null));
                }
            }

            FormationBoard board = MakeBoardWithObstacles(FormationSide.Player,
                roster.ToArray(), obstacles.ToArray());

            var occBefore = new HashSet<int>(board.OccupiedPositions(true));
            int emptyBefore = Enumerable.Range(1, 6).Count(p => board.GetSlot(p) == SlotState.Empty);
            string before = Snapshot(board);

            // 随机合法命令
            int from = roster[random.Next(roster.Count)].slot;
            int to;
            do
            {
                to = random.Next(1, 7);
            }
            while (to == from);

            DisplaceResult r = board.TrySwapChain(board.UnitAt(from)!.Value, from, to, Math.Abs(from - to));

            if (r.Success)
            {
                CollectionAssert.AreEqual(occBefore.ToArray(), board.OccupiedPositions(true).ToArray(),
                    $"trial {trial}: 占据槽位集合必须不变（无空位产生）");
                Assert.AreEqual(emptyBefore, Enumerable.Range(1, 6).Count(p => board.GetSlot(p) == SlotState.Empty),
                    $"trial {trial}: Empty 计数不得增加");
                Assert.AreEqual(Math.Abs(from - to), r.Steps.Count, "实际交换步数 = distance");
            }
            else
            {
                Assert.AreEqual(before, Snapshot(board), $"trial {trial}: 失败必须板不动");
            }
        }
    }

    [TestMethod]
    public void TrySwapChain_OutermostPush_FailsWithoutMoving_PlayerAndEnemy()
    {
        FormationBoard player = MakeBoard(FormationSide.Player,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false), (5, "e", false), (6, "f", false));
        string before = Snapshot(player);
        DisplaceResult r1 = player.TrySwapChain(UnitId.Of("f"), 6, 7, 1);
        Assert.IsFalse(r1.Success);
        Assert.AreEqual(DisplaceFailureReason.InvalidToSlot, r1.Failure);
        Assert.AreEqual(0, r1.Steps.Count);
        Assert.AreEqual(before, Snapshot(player), "最外侧外推失败必须不动（#78）");

        DisplaceResult r2 = player.TrySwapChain(UnitId.Of("a"), 1, 0, 1);
        Assert.IsFalse(r2.Success);
        Assert.AreEqual(DisplaceFailureReason.InvalidToSlot, r2.Failure);
        Assert.AreEqual(before, Snapshot(player));

        FormationBoard enemy = MakeBoard(FormationSide.Enemy,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false));
        string eBefore = Snapshot(enemy);
        DisplaceResult r3 = enemy.TrySwapChain(UnitId.Of("d"), 4, 5, 1);
        Assert.IsFalse(r3.Success);
        Assert.AreEqual(DisplaceFailureReason.InvalidToSlot, r3.Failure);
        Assert.AreEqual(eBefore, Snapshot(enemy));
    }

    [TestMethod]
    public void TrySwapChain_TargetEmpty_FailsAndBoardUnchanged()
    {
        // 缩编：只有 1、2 有人（3~6 空），推 2 号向右 1 格 → 目标格 Empty → 失败不动
        FormationBoard board = MakeBoard(FormationSide.Player, (1, "a", false), (2, "b", false));
        string before = Snapshot(board);
        DisplaceResult r = board.TrySwapChain(UnitId.Of("b"), 2, 3, 1);
        Assert.IsFalse(r.Success);
        Assert.AreEqual(DisplaceFailureReason.TargetSlotEmpty, r.Failure);
        Assert.AreEqual(before, Snapshot(board));
    }

    [TestMethod]
    public void TrySwapChain_CrossesObstacle_ObstacleSwapsLikeUnit()
    {
        // [1:A 2:障碍 3:B]，B(3→1, dist=2)：先与障碍换、再与 A 换
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Enemy,
            new[] { (1, "A", false), (3, "B", false) },
            (2, null));

        DisplaceResult r = board.TrySwapChain(UnitId.Of("B"), 3, 1, 2);
        Assert.IsTrue(r.Success, $"撞障碍必须照常交换（#22）；失败原因 {r.Failure}");
        Assert.AreEqual(2, r.Steps.Count);

        Assert.AreEqual(SlotState.Blocked, board.GetSlot(3), "障碍应被推到 3 号位");
        Assert.AreEqual("A", board.UnitAt(2)!.ToString());
        Assert.AreEqual("B", board.UnitAt(1)!.ToString());

        // 第一步含障碍交换：mover 落到 ToSlot，障碍落到 FromSlot（结果态编码）
        Assert.AreEqual(SlotChangeKind.Swap, r.Steps[0].Kind);
        Assert.AreEqual(SlotState.Blocked, r.Steps[0].FromSlotState, "障碍被交换到 mover 原位（FromSlot）");
        Assert.AreEqual(SlotState.Occupied, r.Steps[0].ToSlotState, "mover 抵达 ToSlot");
        Assert.IsNull(r.Steps[0].UnitB);
    }

    [TestMethod]
    public void TrySwapChain_NotMoverAtFrom_Fails()
    {
        FormationBoard board = MakeBoard(FormationSide.Player, (1, "a", false), (2, "b", false));
        DisplaceResult r = board.TrySwapChain(UnitId.Of("zzz"), 1, 2, 1);
        Assert.IsFalse(r.Success);
        Assert.AreEqual(DisplaceFailureReason.NotMoverAtFrom, r.Failure);
    }

    // ------------------------------------------------------------------
    // T-M1-03：向中靠齐
    // ------------------------------------------------------------------

    [TestMethod]
    public void CloseUp_Normal_AllHealthyShiftLeft_EmptyAtTail_AtomicAndReplayable()
    {
        // 1 号死亡（离场）→ 2..6 全健康 → 全队前移、空位落在队尾
        FormationBoard board = MakeBoard(FormationSide.Player,
            (2, "b", false), (3, "c", false), (4, "d", false), (5, "e", false), (6, "f", false));
        Assert.AreEqual(SlotState.Empty, board.GetSlot(1));

        FormationBoard pre = board.CreatePreviewSnapshot();
        IReadOnlyList<SlotChange> steps = board.CloseUp(1);

        Assert.AreEqual(5, steps.Count, "一次调用返回全部变更（原子）");
        Assert.AreEqual("b,c,d,e,f,-", UnitsBrief(board), "全健康靠齐：空位只在队尾");
        Assert.AreEqual(SlotState.Empty, board.GetSlot(6));

        string replayed = ReplayAndSnapshot(pre, steps);
        Assert.AreEqual(Snapshot(board), replayed, "按序回放该序列 == CloseUp 后板");
    }

    [TestMethod]
    public void CloseUp_WeakStays_HealthyCrosses_EmptyNotBetweenTwoHealthy()
    {
        // [1:∅, 2:B(虚弱), 3:C, 4:D] → 期望 [1:C, 2:D, 3:∅, 4:B]（B 编号只增不减，跨过补位）
        FormationBoard board = MakeBoard(FormationSide.Enemy,
            (2, "B", true), (3, "C", false), (4, "D", false));

        FormationBoard pre = board.CreatePreviewSnapshot();
        IReadOnlyList<SlotChange> steps = board.CloseUp(1);

        Assert.AreEqual("C", board.UnitAt(1)!.ToString());
        Assert.AreEqual("D", board.UnitAt(2)!.ToString());
        Assert.AreEqual(SlotState.Empty, board.GetSlot(3), "空位不能落在两个健康角色之间（C@1、D@2 相邻）");
        Assert.AreEqual("B", board.UnitAt(4)!.ToString(), "虚弱者 B 编号不减小（2→4 只右移）");

        Assert.IsTrue(steps.Count > 0);
        Assert.AreEqual(Snapshot(board), ReplayAndSnapshot(pre, steps));
    }

    [TestMethod]
    public void CloseUp_AllWeak_NoMovement_Idempotent()
    {
        // 1 号死亡 + 2..4 全虚弱 → 留空不补（#115）；重复调用无新变更
        FormationBoard board = MakeBoard(FormationSide.Enemy,
            (2, "B", true), (3, "C", true), (4, "D", true));
        string before = Snapshot(board);

        IReadOnlyList<SlotChange> first = board.CloseUp(1);
        Assert.AreEqual(0, first.Count, "后方全虚弱 → 空位留空不补");
        Assert.AreEqual(before, Snapshot(board));

        IReadOnlyList<SlotChange> second = board.CloseUp(1);
        Assert.AreEqual(0, second.Count, "恢复后不自动前移；再次调用无新移动");
    }

    [TestMethod]
    public void CloseUp_ObstacleDoesNotBlock_ObstaclePushedBack()
    {
        // [1:∅, 2:障碍, 3:C, 4:D] → 期望 [1:C, 2:D, 3:∅, 4:障碍]（#114：交换推进、空位照样被填）
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Enemy,
            new[] { (3, "C", false), (4, "D", false) },
            (2, null));

        FormationBoard pre = board.CreatePreviewSnapshot();
        IReadOnlyList<SlotChange> steps = board.CloseUp(1);

        Assert.AreEqual("C", board.UnitAt(1)!.ToString());
        Assert.AreEqual("D", board.UnitAt(2)!.ToString());
        Assert.AreEqual(SlotState.Empty, board.GetSlot(3), "空位要收敛（被填）");
        Assert.IsTrue(board.TryGetObstacleHp(4, out _), "障碍应被推后到 4 号位（不阻挡靠齐）");

        Assert.IsTrue(steps.Count > 0);
        Assert.AreEqual(Snapshot(board), ReplayAndSnapshot(pre, steps));
    }

    [TestMethod]
    public void CloseUp_OnOccupiedSlot_Throws()
    {
        FormationBoard board = MakeBoard(FormationSide.Player, (1, "a", false), (2, "b", false));
        Assert.ThrowsException<ArgumentException>(() => board.CloseUp(1));
    }

    [TestMethod]
    public void CloseUp_FromTailSlot_NoMovement()
    {
        // 6 号离场 → 之后无单位 → 无移动、空位留在队尾
        FormationBoard board = MakeBoard(FormationSide.Player,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false), (5, "e", false));
        string before = Snapshot(board);
        IReadOnlyList<SlotChange> steps = board.CloseUp(6);
        Assert.AreEqual(0, steps.Count);
        Assert.AreEqual(before, Snapshot(board));
    }

    [TestMethod]
    public void CloseUp_EnemySymmetric_AppliesSameRules()
    {
        FormationBoard enemy = MakeBoard(FormationSide.Enemy,
            (2, "b", false), (3, "c", false), (4, "d", false));
        IReadOnlyList<SlotChange> steps = enemy.CloseUp(1);
        Assert.AreEqual("b", enemy.UnitAt(1)!.ToString());
        Assert.AreEqual("c", enemy.UnitAt(2)!.ToString());
        Assert.AreEqual("d", enemy.UnitAt(3)!.ToString());
        Assert.AreEqual(SlotState.Empty, enemy.GetSlot(4));
        Assert.AreEqual(3, steps.Count);
    }

    // ------------------------------------------------------------------
    // T-M1-04：预置障碍
    // ------------------------------------------------------------------

    [TestMethod]
    public void Obstacles_FromJsonFixture_BlockedSemantics()
    {
        string json = """
            { "player": { "slot_count": 6, "combat_slots": 4, "support_slots": [5,6] },
              "enemy": { "slot_count": 4, "combat_slots": 4, "support_slots": [] },
              "numeration": "center_outward",
              "initial_roster": { "player": [ { "slot": 1, "unit": "tank" } ], "enemy": [ { "slot": 1, "unit": "melee_soldier" } ] },
              "obstacles": [ { "side": "player", "slot": 3 }, { "side": "enemy", "slot": 2, "hp": 5 } ],
              "rules": { "boundary_as_hard_wall": true, "displacement_only_via_swap_chain": true,
                         "obstacle_swaps_like_unit": true, "close_up_on_death_immediate": true,
                         "close_up_ignores_obstacle": true, "close_up_enemy_symmetric": true,
                         "swap_player_initiated": true, "slot_three_state": true,
                         "deaths_and_close_up_separate_from_displacement": true } }
            """;
        FormationConfig cfg = FormationConfig.Parse(json);
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindUnitsJson()));

        FormationBoard player = FormationBoardFactory.CreatePlayerBoard(cfg, units);
        Assert.AreEqual(SlotState.Blocked, player.GetSlot(3));
        Assert.IsNull(player.UnitAt(3));
        CollectionAssert.Contains(player.OccupiedPositions(true).ToArray(), 3, "includeObstacle=true 应含障碍槽");
        CollectionAssert.DoesNotContain(player.OccupiedPositions(false).ToArray(), 3, "includeObstacle=false 不含障碍槽");
        Assert.IsTrue(player.TryGetObstacleHp(3, out int? hp) && hp is null, "hp 缺省 = 不可被摧毁占位");

        FormationBoard enemy = FormationBoardFactory.CreateEnemyBoard(cfg, units);
        Assert.AreEqual(SlotState.Blocked, enemy.GetSlot(2));
        Assert.IsTrue(enemy.TryGetObstacleHp(2, out int? hp2) && hp2 == 5);
    }

    [TestMethod]
    public void Obstacles_Invalid_OverlapOrOutOfRange_Throws()
    {
        string overlap = """
            { "player": { "slot_count": 6, "combat_slots": 4, "support_slots": [5,6] },
              "enemy": { "slot_count": 4, "combat_slots": 4, "support_slots": [] },
              "numeration": "center_outward",
              "initial_roster": { "player": [ { "slot": 3, "unit": "tank" } ], "enemy": [] },
              "obstacles": [ { "side": "player", "slot": 3 } ],
              "rules": { "boundary_as_hard_wall": true, "displacement_only_via_swap_chain": true,
                         "obstacle_swaps_like_unit": true, "close_up_on_death_immediate": true,
                         "close_up_ignores_obstacle": true, "close_up_enemy_symmetric": true,
                         "swap_player_initiated": true, "slot_three_state": true,
                         "deaths_and_close_up_separate_from_displacement": true } }
            """;
        Assert.ThrowsException<InvalidDataException>(() => FormationConfig.Parse(overlap));

        string outOfRange = """
            { "player": { "slot_count": 6, "combat_slots": 4, "support_slots": [5,6] },
              "enemy": { "slot_count": 4, "combat_slots": 4, "support_slots": [] },
              "numeration": "center_outward",
              "initial_roster": { "player": [], "enemy": [] },
              "obstacles": [ { "side": "enemy", "slot": 7 } ],
              "rules": { "boundary_as_hard_wall": true, "displacement_only_via_swap_chain": true,
                         "obstacle_swaps_like_unit": true, "close_up_on_death_immediate": true,
                         "close_up_ignores_obstacle": true, "close_up_enemy_symmetric": true,
                         "swap_player_initiated": true, "slot_three_state": true,
                         "deaths_and_close_up_separate_from_displacement": true } }
            """;
        Assert.ThrowsException<InvalidDataException>(() => FormationConfig.Parse(outOfRange));
    }

    [TestMethod]
    public void RemoveObstacle_ThenCloseUp_Converges()
    {
        // [1:A 2:障碍 3:B 4:C]：打掉障碍 → 移除 → 立即靠齐（GDD §1.1 表）
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Enemy,
            new[] { (1, "A", false), (3, "B", false), (4, "C", false) },
            (2, 5));

        Assert.IsTrue(board.RemoveObstacle(2), "障碍应可移除");
        Assert.AreEqual(SlotState.Empty, board.GetSlot(2));

        IReadOnlyList<SlotChange> steps = board.CloseUp(2);
        Assert.AreEqual(2, steps.Count);
        Assert.AreEqual("A", board.UnitAt(1)!.ToString());
        Assert.AreEqual("B", board.UnitAt(2)!.ToString());
        Assert.AreEqual("C", board.UnitAt(3)!.ToString());
        Assert.AreEqual(SlotState.Empty, board.GetSlot(4), "靠齐后空位收敛到队尾");
    }

    // ------------------------------------------------------------------
    // T-M1-05：位移预览 dry-run（内核同源）
    // ------------------------------------------------------------------

    [TestMethod]
    public void DryRun_MatchesRealExecution_AndDoesNotMutateBoard()
    {
        FormationBoard exec = MakeBoard(FormationSide.Player,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false), (5, "e", false), (6, "f", false));
        FormationBoard preview = MakeBoard(FormationSide.Player,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false), (5, "e", false), (6, "f", false));
        string previewBefore = Snapshot(preview);

        DisplaceResult real = exec.TrySwapChain(UnitId.Of("c"), 3, 1, 2);
        DisplaceResult dry = preview.DryRunSwapChain(UnitId.Of("c"), 3, 1, 2);

        Assert.IsTrue(real.Success);
        Assert.IsTrue(dry.Success, $"dry-run 应成功；失败原因 {dry.Failure}");
        Assert.AreEqual(real.Steps.Count, dry.Steps.Count, "步骤数一致");
        for (int i = 0; i < real.Steps.Count; i++)
        {
            Assert.AreEqual(real.Steps[i], dry.Steps[i], $"dry-run 第 {i} 步必须与真实结算逐项一致");
        }

        Assert.AreEqual(previewBefore, Snapshot(preview), "dry-run 不得污染真实板");

        // 同一输入两次 dry-run 输出一致
        DisplaceResult dry2 = preview.DryRunSwapChain(UnitId.Of("c"), 3, 1, 2);
        CollectionAssert.AreEqual(dry.Steps.ToArray(), dry2.Steps.ToArray(), "同一输入两次 dry-run 输出一致");
    }

    [TestMethod]
    public void DryRun_Failure_OutermostPush_EmptySteps_BoardUntouched()
    {
        FormationBoard board = MakeBoard(FormationSide.Enemy,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false));
        string before = Snapshot(board);

        DisplaceResult dry = board.DryRunSwapChain(UnitId.Of("d"), 4, 5, 1);

        Assert.IsFalse(dry.Success);
        Assert.AreEqual(0, dry.Steps.Count, "失败返回空步骤");
        Assert.AreEqual(DisplaceFailureReason.InvalidToSlot, dry.Failure);
        Assert.AreEqual(before, Snapshot(board), "真实板不动");
    }

    [TestMethod]
    public void DryRun_Sequence_ReplaysToSameFinalBoard()
    {
        FormationBoard exec = MakeBoard(FormationSide.Enemy,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false));
        FormationBoard preview = MakeBoard(FormationSide.Enemy,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false));

        DisplaceResult real = exec.TrySwapChain(UnitId.Of("d"), 4, 1, 3);
        DisplaceResult dry = preview.DryRunSwapChain(UnitId.Of("d"), 4, 1, 3);

        Assert.IsTrue(real.Success);
        Assert.IsTrue(dry.Success, $"dry-run 应成功；失败原因 {dry.Failure}");
        Assert.AreEqual(real.Steps.Count, dry.Steps.Count);
        CollectionAssert.AreEqual(real.Steps.ToArray(), dry.Steps.ToArray(), "dry-run 序列需与真实结算一致");

        // 在同一初始板上回放 dry-run 序列 → 应还原真实结算后的最终板
        FormationBoard fresh = MakeBoard(FormationSide.Enemy,
            (1, "a", false), (2, "b", false), (3, "c", false), (4, "d", false));
        string replayed = ReplayAndSnapshot(fresh, dry.Steps);
        Assert.AreEqual(Snapshot(exec), replayed, "回放 dry-run 序列可无歧义还原最终板");
        Assert.AreEqual("d,a,b,c", UnitsBrief(exec));
    }
}