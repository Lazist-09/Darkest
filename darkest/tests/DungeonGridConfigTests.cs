using System;
using System.Collections.Generic;
using System.IO;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **`dungeon_grid.json` 的 `P30` 七条校验**用例（架构契约；用户裁 (B) 瓷砖网格）。
/// 重点三条：
///   ③ **`goal` 必须从 `start` 可达**（架构评"本表最有价值的一条门禁"）✓
///   ⑤ **`encounter` 必须为 null**（用户：每格遭遇规则**暂留**）⇒ 非 null 一律拒绝 ✓（**要求一条负向用例锁死** ✓）
///   ⑥ `move.cost_light_per_tile` **未定案 ⇒ 必须 null** ✓
/// </summary>
[TestClass]
public sealed class DungeonGridConfigTests
{
    /// <summary>最小合法样本：5×3，起点房间 → 终点；`encounter` 不写（= null ✓）</summary>
    private static string Json(string extra = "") => $$"""
{
  "width": 5,
  "height": 3,
  "tiles": [ "#####", "R.E!G", "#####" ],
  "start": { "x": 0, "y": 1 },
  "goal": { "x": 4, "y": 1 },
  "room_anchors": [ { "id": "r1", "x": 0, "y": 1, "type": "battle", "content_ref": "room_battle" } ],
  "vision": { "reveal_on_enter": true, "radius": 1, "scout_bonus": 2 }
  {{extra}}
}
""";

    [TestMethod]
    public void HappyPath_Parses_AndEncounterStaysNull()
    {
        DungeonGridConfig cfg = DungeonGridConfig.Parse(Json());
        Assert.AreEqual(5, cfg.Width);
        Assert.AreEqual((4, 1), (cfg.Goal.X, cfg.Goal.Y));
        Assert.IsNull(cfg.Encounter, "🔴 未写 `encounter` ⇒ **加载后仍为 null**（不许任何默认值凭空出现）✓");
        Assert.IsNull(cfg.Move, "同：`move` 未定案 ⇒ null ✓");
        Assert.AreEqual(DungeonTileKind.Battle, cfg.ToGrid().TileAt(3, 1), "转网格可用 ✓");
        Assert.IsTrue(cfg.Vision!.RevealOnEnter, "视野：进格揭示 ✓");
    }

    [TestMethod]
    public void Encounter_NonNull_IsRejected_NoOneFillsADefault()
    {
        // 🔴 架构要求的那条**负向用例**：任何非 null 的 encounter 都是"谁替策划定了一个数"
        string withChance = Json(""","encounter": { "per_tile_chance": 5 }""");
        InvalidDataException ex1 = Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(withChance), "填 per_tile_chance ⇒ 拒绝 ✓");
        Assert.IsTrue(ex1.Message.Contains("encounter"), "报错要点名 `encounter` ✓");

        string withNoteOnly = Json(""","encounter": { "note": "后续安排" }""");
        DungeonGridConfig ok = DungeonGridConfig.Parse(withNoteOnly);
        Assert.IsNotNull(ok.Encounter, "只写 note（无数值）⇒ 允许（它是「暂留」的说明，不是规则）✓");
        Assert.IsTrue(ok.Encounter!.IsEmpty, "note-only ⇒ IsEmpty（无任何规则值）✓");

        string withCooldown = Json(""","encounter": { "cooldown_tiles": 3 }""");
        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(withCooldown), "填 cooldown ⇒ 同样拒绝 ✓");
    }

    [TestMethod]
    public void MoveCostPerTile_IsRejected_UntilTheMagnitudeIsDecided()
    {
        string withCost = Json(""","move": { "cost_light_per_tile": 30 }""");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(withCost), "`cost_light_per_tile` 未定案 ⇒ 拒绝 ✓");
        Assert.IsTrue(ex.Message.Contains("cost_light_per_tile"), "报错点名该键 ✓");
    }

    [TestMethod]
    public void ShapeAndCharset_Violations_AreRejected()
    {
        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(Json().Replace("\"R.E!G\"", "\"R.E!\"")), "行宽 ≠ width ⇒ 拒绝 ✓");

        // 未登记字符（P30 ②：新增字符必须同时登记枚举）✓
        string badChar = Json().Replace("\"R.E!G\"", "\"RXE!G\"");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(badChar), "`X` 不在字符表 ⇒ 拒绝 ✓");
        Assert.IsTrue(ex.Message.Contains("X"), "报错要点名那个字符 ✓");
    }

    [TestMethod]
    public void GoalMustBeReachableFromStart()
    {
        // 🔴 P30 ③：把"这张图能不能通关"变成**启动期可判** —— 用墙把起点与终点隔开 ✓
        string walled = Json().Replace("\"R.E!G\"", "\"R#E!G\"");
        InvalidDataException ex = Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(walled), "起点被墙隔开 ⇒ 拒绝（不让玩家撞墙）✓");
        Assert.IsTrue(ex.Message.Contains("不可达"), "报错说明「不可达」 ✓");
    }

    [TestMethod]
    public void GoalPoint_MustMatchTheGoalTile_AndStartMustBeWalkable()
    {
        string mismatch = Json().Replace("\"goal\": { \"x\": 4, \"y\": 1 }", "\"goal\": { \"x\": 3, \"y\": 1 }");
        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(mismatch), "`goal` 与图上的 `G` 不一致 ⇒ 拒绝 ✓");

        string onWall = Json().Replace("\"start\": { \"x\": 0, \"y\": 1 }", "\"start\": { \"x\": 0, \"y\": 0 }");
        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(onWall), "起点落在墙格 ⇒ 拒绝 ✓");
    }

    [TestMethod]
    public void RoomAnchorContentRef_MustExistWhenCatalogIsGiven()
    {
        var catalog = new HashSet<string>(StringComparer.Ordinal) { "room_battle" };
        DungeonGridConfig ok = DungeonGridConfig.Parse(Json(), catalog);
        Assert.AreEqual("room_battle", ok.RoomAnchors![0].ContentRef, "引用存在 ⇒ 通过 ✓");

        var missing = new HashSet<string>(StringComparer.Ordinal) { "room_event" };
        Assert.ThrowsException<InvalidDataException>(
            () => DungeonGridConfig.Parse(Json(), missing), "引用不存在 ⇒ 拒绝（P30 ④ / P26 同族）✓");
    }
}
