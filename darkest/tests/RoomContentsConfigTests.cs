using System;
using System.IO;
using System.Linq;
using Darkest.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **内容层片 B 验收**（`tasks/merged_content_layer_pack.md` §3 片 B / §4 / `O-85`）：
/// `data/room_contents.json` = **房间类型 → [{weight, encounter, curio_pool}]** + `RoomContentsConfig` + **P26 校验**：
/// ① 至少一类房间 · ② 每行 `weight > 0` · ③ 房间类型键必须是已知类型 · ④ 🔴 **引用的 id 必须存在**
/// （`curio_pool` ⇒ 在 `curios.json` 里；`encounter` ⇒ 在**编成目录**里，而**该目录尚未建立** ⇒ **任何非空 encounter 都判错**）。
/// </summary>
[TestClass]
public sealed class RoomContentsConfigTests
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

    private static CuriosConfig Curios() => CuriosConfig.Parse(ReadData("curios.json"));

    [TestMethod]
    public void ShippedRoomContents_LoadAndPassP26()
    {
        RoomContentsConfig cfg = RoomContentsConfig.Parse(ReadData("room_contents.json"), Curios());

        Assert.IsTrue(cfg.Rooms.Count >= 1, "至少一类房间");
        Assert.IsTrue(cfg.Rooms.ContainsKey("battle") && cfg.Rooms.ContainsKey("event"),
            "至少覆盖 battle 与 event（本项目的房间类型词汇）");
        foreach ((string type, var entries) in cfg.Rooms)
        {
            Assert.IsTrue(entries.All(e => e.Weight > 0), $"\"{type}\" 每行 weight 必须 > 0（组内权重）");
        }

        // 🔴 出厂数据**不得**引用编成（编成目录尚未建立）—— 这是"引用必须存在"的正确含义
        Assert.IsTrue(cfg.Rooms.Values.SelectMany(v => v).All(e => e.Encounter is null),
            "编成目录未建立 ⇒ 出厂数据不得引用任何 encounter id（P26 ④ / B6）");

        // event 房 = Curio 房：其 curio_pool 必须都是 curios.json 里真实存在的 id（引用而非拷贝）
        var curioIds = Curios().RealCurios.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string id in cfg.ForType("event").SelectMany(e => e.CurioPool ?? Array.Empty<string>()))
        {
            Assert.IsTrue(curioIds.Contains(id), $"event 房引用的 Curio \"{id}\" 必须在 curios.json 里存在");
        }
    }

    [TestMethod]
    public void P26_RejectsUnknownCurioReference()
    {
        const string bad = """
        { "config": { "version": 1 },
          "rooms": { "event": [ { "weight": 1, "encounter": null, "curio_pool": ["no_such_curio"] } ] } }
        """;
        Assert.ThrowsException<InvalidDataException>(() => RoomContentsConfig.Parse(bad, Curios()),
            "引用了不存在的 curio id ⇒ 启动即报错（P26 ④）");
    }

    [TestMethod]
    public void P26_RejectsEncounterReferenceWhileCatalogIsMissing()
    {
        const string bad = """
        { "config": { "version": 1 },
          "rooms": { "battle": [ { "weight": 1, "encounter": "fishman_patrol_a", "curio_pool": [] } ] } }
        """;
        Assert.ThrowsException<InvalidDataException>(() => RoomContentsConfig.Parse(bad, Curios()),
            "编成目录尚未建立 ⇒ 任何非空 encounter 都必须判错（不静默接受）");
    }

    [TestMethod]
    public void P26_RejectsNonPositiveWeight_UnknownType_AndEmptyRooms()
    {
        const string zeroWeight = """
        { "config": { "version": 1 }, "rooms": { "event": [ { "weight": 0, "encounter": null, "curio_pool": [] } ] } }
        """;
        Assert.ThrowsException<InvalidDataException>(() => RoomContentsConfig.Parse(zeroWeight, Curios()),
            "weight = 0 ⇒ 报错（组内权重必须 > 0）");

        const string unknownType = """
        { "config": { "version": 1 }, "rooms": { "hall": [ { "weight": 1, "encounter": null, "curio_pool": [] } ] } }
        """;
        Assert.ThrowsException<InvalidDataException>(() => RoomContentsConfig.Parse(unknownType, Curios()),
            "未知房间类型 ⇒ 报错（防止把真机的 hall/room 直接抄进来）");

        const string emptyRooms = """
        { "config": { "version": 1 }, "rooms": {} }
        """;
        Assert.ThrowsException<InvalidDataException>(() => RoomContentsConfig.Parse(emptyRooms, Curios()),
            "rooms 为空 ⇒ 报错（P26 ①）");
    }
}
