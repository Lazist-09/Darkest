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
/// 🔴 **棋盘/阵型用例**（`board.md` / M3）—— **本文件只用例**；**夹具与助手在 `BoardTests.Fixtures.cs`**
/// （用户 2026-09-18 红线：程序文件 ≤600 行 ⇒ 按**职责**拆：**测试** 与 **夹具** 分开 ⇒ 打开这个文件就是"看用例" ✓）
/// </summary>
[TestClass]   // 🔴 **必须有**：MSTest 靠它发现测试类 ⇒ 我第一版漏了它 ⇒ **静默丢了 25 个用例**（583→558）⚠️
public sealed partial class BoardTests
{
    [TestMethod]
    public void Board_FromFormationJson_PlayerAndEnemyRosterMatch()
    {
        string json = File.ReadAllText(FindFormationJson());
        FormationConfig cfg = FormationConfig.Parse(json);
        UnitsConfig units = UnitsConfig.Parse(File.ReadAllText(FindUnitsJson()));

        FormationBoard player = FormationBoardFactory.CreatePlayerBoard(cfg, units);
        Assert.AreEqual(FormationSide.Player, player.Side);
        Assert.AreEqual(6, player.SlotCount);
        string[] expectedPlayer = { "tank", "warrior", "commissar", "medic", "warrior_2", "medic_2" };
        for (int p = 1; p <= 6; p++)
        {
            Assert.AreEqual(SlotState.Occupied, player.GetSlot(p), $"player 槽 {p} 应为 Occupied");
            Assert.AreEqual(expectedPlayer[p - 1], player.UnitAt(p)!.ToString(), $"player 槽 {p} 单位（同原型多实例 id 唯一化）");
        }

        Assert.AreEqual(SlotKind.Combat, player.SlotKindAt(1));
        Assert.AreEqual(SlotKind.Support, player.SlotKindAt(5));
        Assert.AreEqual(SlotKind.Support, player.SlotKindAt(6));

        FormationBoard enemy = FormationBoardFactory.CreateEnemyBoard(cfg, units);
        Assert.AreEqual(4, enemy.SlotCount);
        string[] expectedEnemy = { "melee_soldier", "melee_soldier_2", "ranged_archer", "caster" };
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







    // ------------------------------------------------------------------
    // T-M1-03：向中靠齐
    // ------------------------------------------------------------------








    // ------------------------------------------------------------------
    // T-M1-04：预置障碍
    // ------------------------------------------------------------------




    [TestMethod]
    public void DamageObstacle_PartialDamage_KeepsObstacleWithReducedHp()
    {
        // 🔴 2026-09-20 接线：障碍**受击**（此前 `TryGetObstacleHp`/`RemoveObstacle` 只有测试在调
        //    ⇒ 实机障碍是"纯无敌墙"）。未归零 ⇒ **仍在**，但血条减少 ✓
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Enemy,
            new[] { (1, "A", false) },
            (2, 5));

        Assert.IsTrue(board.DamageObstacle(2, 3), "扣 3 后仍剩 2 ⇒ 返回 true（槽仍被占据）");
        Assert.AreEqual(SlotState.Blocked, board.GetSlot(2), "未归零 ⇒ 障碍保留");
        Assert.IsTrue(board.TryGetObstacleHp(2, out int? hp) && hp == 2, "血量应 5 → 2");
    }

    [TestMethod]
    public void DamageObstacle_Lethal_RemovesObstacle()
    {
        // 归零 ⇒ **自动移除**（调用方随后自行编排靠齐，GDD §1.1："立即靠齐由调用方负责任务编排"）✓
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Enemy,
            new[] { (1, "A", false), (3, "B", false) },
            (2, 5));

        Assert.IsFalse(board.DamageObstacle(2, 5), "正好归零 ⇒ 返回 false（槽已空）");
        Assert.AreEqual(SlotState.Empty, board.GetSlot(2), "归零 ⇒ 障碍被移除");
        Assert.IsFalse(board.TryGetObstacleHp(2, out _), "移除后读不到障碍");
    }

    [TestMethod]
    public void DamageObstacle_Overkill_RemovesAndNeverGoesNegative()
    {
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Enemy,
            new[] { (1, "A", false) },
            (2, 3));

        Assert.IsFalse(board.DamageObstacle(2, 999), "超额伤害 ⇒ 同样移除（血量不为负）");
        Assert.AreEqual(SlotState.Empty, board.GetSlot(2));
    }

    [TestMethod]
    public void DamageObstacle_Indestructible_AbsorbsWithoutRemoval()
    {
        // `hp == null` = 不可摧毁占位（`data_schema §3.6` 缺省 / #73/#105 口径）⇒ **吸收、不掉血、不移除** ✓
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Player,
            new[] { (1, "A", false) },
            (2, null));

        Assert.IsTrue(board.DamageObstacle(2, 9999), "不可摧毁 ⇒ 永远返回 true（仍在）");
        Assert.AreEqual(SlotState.Blocked, board.GetSlot(2));
        Assert.IsTrue(board.TryGetObstacleHp(2, out int? hp) && hp is null, "血量仍为 null（免疫）");
    }

    [TestMethod]
    public void DamageObstacle_NonObstacleSlot_ReturnsFalse()
    {
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Enemy,
            new[] { (1, "A", false) },
            (2, 5));

        Assert.IsFalse(board.DamageObstacle(1, 5), "该槽是单位不是障碍 ⇒ false（调用方应先 TryGetObstacleHp 判存在）");
        Assert.AreEqual(1, board.UnitAtPosition(UnitId.Of("A")), "单位不受影响");
    }

    [TestMethod]
    public void DamageObstacle_DoesNotPollutePreviewSnapshot()
    {
        // record 不可变 ⇒ 打快照里的障碍**不会**回头改真实板（dry-run 隔离性）✓
        FormationBoard board = MakeBoardWithObstacles(FormationSide.Enemy,
            new[] { (1, "A", false) },
            (2, 5));
        FormationBoard snapshot = board.CreatePreviewSnapshot();

        Assert.IsFalse(snapshot.DamageObstacle(2, 5), "快照里被摧毁");
        Assert.AreEqual(SlotState.Blocked, board.GetSlot(2), "🔴 真实板不受快照改动影响");
        Assert.IsTrue(board.TryGetObstacleHp(2, out int? hp) && hp == 5, "真实板血量原封不动");
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
