using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **具名编成目录验收**（内容层补缺：此前登记「编成目录尚未建立」）：
/// · 出厂 `data/encounters.json` 过 **P28**（id 唯一 · enemy 非空 · 槽位 1..4 不重复 · **unit 必须是真实敌方原型**）
/// · 🔴 **与 `room_contents.json` 的链打通**：内容表引用 `encounter` 时，
///   **传了编成目录 ⇒ 校验通过；没传 ⇒ 仍按"引用不存在"判错**（fail-fast，不静默接受）✓
/// · 五种坏数据必须抛（缺 id ／ id 重复 ／ enemy 空 ／ 槽位越界或重复 ／ 引用了不存在的原型）
/// </summary>
[TestClass]
public sealed class EncountersConfigTests
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

    /// <summary>敌方原型 id（从 units.json 取，与运行期同一口径）。</summary>
    private static HashSet<string> EnemyIds()
    {
        UnitsConfig units = UnitsConfig.Parse(ReadData("units.json"));
        return units.Units.Where(u => string.Equals(u.Side, "enemy", StringComparison.Ordinal))
            .Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
    }

    [TestMethod]
    public void ShippedEncounters_PassP28_AndReferenceRealPrototypes()
    {
        HashSet<string> enemyIds = EnemyIds();
        EncountersConfig cfg = EncountersConfig.Parse(ReadData("encounters.json"), slotCount: 4,
            enemyUnitIds: enemyIds);

        Assert.IsTrue(cfg.Encounters.Count >= 1, "至少一条占位编成");
        foreach (EncounterConfig e in cfg.Encounters)
        {
            Assert.IsTrue(e.Enemy.All(u => enemyIds.Contains(u.Unit)),
                $"编成 \"{e.Id}\" 的每个 unit 都必须是真实敌方原型（引用而非杜撰）");
        }

        Assert.IsNotNull(cfg.Find("enc_basic_line"), "占位编成 enc_basic_line 应存在（模板同形）");
        Assert.IsNull(cfg.Find("no_such_encounter"), "不存在的 id ⇒ Find 返回 null（调用方走回退，不静默）");
    }

    [TestMethod]
    public void P28_RejectsFiveKindsOfBadData()
    {
        HashSet<string> enemyIds = EnemyIds();

        const string noId = """
        { "config": { "version": 1 },
          "encounters": [ { "enemy": [ { "slot": 1, "unit": "melee_soldier" } ] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(
            () => EncountersConfig.Parse(noId, 4, enemyIds), "缺 id（P28 ①）");

        const string dupId = """
        { "config": { "version": 1 },
          "encounters": [
            { "id": "x", "enemy": [ { "slot": 1, "unit": "melee_soldier" } ] },
            { "id": "x", "enemy": [ { "slot": 1, "unit": "melee_soldier" } ] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(
            () => EncountersConfig.Parse(dupId, 4, enemyIds), "id 重复（P28 ①）");

        const string emptyEnemy = """
        { "config": { "version": 1 }, "encounters": [ { "id": "x", "enemy": [] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(
            () => EncountersConfig.Parse(emptyEnemy, 4, enemyIds), "enemy 为空（P28 ②）");

        const string badSlot = """
        { "config": { "version": 1 },
          "encounters": [ { "id": "x", "enemy": [ { "slot": 5, "unit": "melee_soldier" } ] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(
            () => EncountersConfig.Parse(badSlot, 4, enemyIds), "槽位越界（P28 ③）");

        const string dupSlot = """
        { "config": { "version": 1 },
          "encounters": [ { "id": "x", "enemy": [
            { "slot": 1, "unit": "melee_soldier" }, { "slot": 1, "unit": "caster" } ] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(
            () => EncountersConfig.Parse(dupSlot, 4, enemyIds), "同槽两个单位（P28 ③）");

        const string unknownUnit = """
        { "config": { "version": 1 },
          "encounters": [ { "id": "x", "enemy": [ { "slot": 1, "unit": "no_such_unit" } ] } ] }
        """;
        Assert.ThrowsException<InvalidDataException>(
            () => EncountersConfig.Parse(unknownUnit, 4, enemyIds), "引用了不存在的敌方原型（P28 ④）");
    }

    [TestMethod]
    public void ContentTable_EncounterReference_ValidatesOnlyWhenCatalogIsProvided()
    {
        HashSet<string> enemyIds = EnemyIds();
        EncountersConfig enc = EncountersConfig.Parse(ReadData("encounters.json"), 4, enemyIds);
        CuriosConfig curios = CuriosConfig.Parse(ReadData("curios.json"));

        // 内容表里**真的引用**一个具名编成（合成表，出厂表暂无引用）
        const string contents = """
        { "config": { "version": 1 },
          "rooms": { "battle": [ { "weight": 1, "encounter": "enc_basic_line", "curio_pool": [] } ] } }
        """;

        // ① 传了编成目录 ⇒ 校验通过（引用存在）
        RoomContentsConfig ok = RoomContentsConfig.Parse(contents, curios,
            enc.Encounters.Select(e => e.Id).ToHashSet(StringComparer.Ordinal));
        Assert.IsNotNull(ok.ForType("battle"));

        // ② 没传目录 ⇒ 仍按"引用必须存在"判错（fail-fast：不静默接受）
        Assert.ThrowsException<InvalidDataException>(
            () => RoomContentsConfig.Parse(contents, curios),
            "未提供编成目录时，任何非空 encounter 都必须判错（P26 ④ 的原口径保持不变）");
    }
}
