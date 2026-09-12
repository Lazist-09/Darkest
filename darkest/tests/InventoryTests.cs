using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// M7.5 D2 背包（`#260`）：**12 格**、口粮不堆叠、**整备仅出发前（局内不可改）**、
/// 推荐配置 **2/9/1 = 恰满 12**、**包满禁止静默丢弃 → 必须"选择丢弃哪一格"**（P21 ⑥⑬）、
/// 支援包 = `while_carried`（离开背包即失效）。
/// </summary>
[TestClass]
public sealed class InventoryTests
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

    private static TuningInventory Cfg() => TuningConfig.Parse(ReadData("tuning.json")).Inventory!;

    [TestMethod]
    public void D2_RecommendedLoadout_IsExactlyTwelveSlots()
    {
        var inv = new Inventory(Cfg());
        IReadOnlyList<InventoryItem> loadout = inv.RecommendedLoadout();

        Assert.AreEqual(12, Cfg().SlotCap, "12 格（P21 ⑥：不许调到 15）");
        Assert.AreEqual(12, loadout.Count, "推荐配置 2/9/1 **恰满 12 格**");
        Assert.AreEqual(2, loadout.Count(i => i.Kind == ItemKind.Firewood));
        Assert.AreEqual(9, loadout.Count(i => i.Kind == ItemKind.Food), "口粮不堆叠 ⇒ 9 份占 9 格");
        Assert.AreEqual(1, loadout.Count(i => i.Kind == ItemKind.SupportCrate));
    }

    [TestMethod]
    public void D2_Configure_OnlyBeforeDeparture_ThenLocked()
    {
        var inv = new Inventory(Cfg());
        Assert.IsTrue(inv.ConfigureRecommended(out string r1), "出发前可整备");
        Assert.AreEqual("configured", r1);
        Assert.AreEqual(12, inv.Count);

        inv.LockForRun();
        Assert.IsFalse(inv.TryConfigure(new[] { new InventoryItem(ItemKind.Food, "f1") }, out string r2),
            "🔴 **局内不可改**（否则「出发前决策」没有分量）");
        Assert.AreEqual("locked_for_run", r2);
    }

    [TestMethod]
    public void D2_OverSlotCap_RejectedWithoutChange()
    {
        var inv = new Inventory(Cfg());
        var tooMany = Enumerable.Range(0, 13).Select(i => new InventoryItem(ItemKind.Food, $"f{i}")).ToArray();
        Assert.IsFalse(inv.TryConfigure(tooMany, out string reason), "13 > 12 → 拒绝");
        Assert.AreEqual("over_slot_cap", reason);
        Assert.AreEqual(0, inv.Count, "拒绝时**不改内容**");
    }

    [TestMethod]
    public void D2_Full_DoesNotSilentlyDiscard_MustChooseWhatToDiscard()
    {
        var inv = new Inventory(Cfg());
        inv.ConfigureRecommended(out _);
        inv.LockForRun();
        Assert.IsTrue(inv.IsFull);

        // 🔴 包满：拾取失败且**不静默丢**（P21 ⑬）
        Assert.IsFalse(inv.TryAdd(new InventoryItem(ItemKind.Firewood, "extra"), out string reason));
        Assert.AreEqual("full_choose_discard", reason);
        Assert.AreEqual(12, inv.Count, "未被静默丢弃");

        // 玩家选择丢弃哪一格 → 腾格后可拾取
        Assert.IsTrue(inv.TryDiscardAt(0, out InventoryItem? removed));
        Assert.IsNotNull(removed);
        Assert.AreEqual(11, inv.Count);
        Assert.IsTrue(inv.TryAdd(new InventoryItem(ItemKind.Firewood, "extra"), out _));

        // 备选路径：就地消耗口粮腾格
        Assert.IsTrue(inv.TryConsumeFoodToFreeSlot(out InventoryItem? eaten));
        Assert.AreEqual(ItemKind.Food, eaten!.Kind);
        Assert.AreEqual(11, inv.Count);
    }

    [TestMethod]
    public void D2_SupportCrate_PresenceDrivesWhileCarriedBonus()
    {
        var inv = new Inventory(Cfg());
        inv.ConfigureRecommended(out _);
        Assert.IsTrue(inv.CarriesSupportCrate, "推荐配置含 1 个支援包");

        inv.TryDiscardAt(inv.Slots.ToList().FindIndex(i => i.Kind == ItemKind.SupportCrate), out _);
        Assert.IsFalse(inv.CarriesSupportCrate, "🔴 离开背包即失效（while_carried 语义）");
    }

    [TestMethod]
    public void P21_14_RecommendedLoadout_MustContainSupportCrate()
    {
        // 把 support_crate 换成 support_pack ⇒ 默认配置下 SP 完全不恢复（支援位废）⇒ 启动报错
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(
            ReadData("tuning.json").Replace("\"support_crate\": 1", "\"support_pack\": 1", StringComparison.Ordinal)),
            "推荐配置缺 support_crate → 启动报错（P21 ⑭ / #265）");
    }

    [TestMethod]
    public void D2_SupportCrate_And_SupportPack_AreDistinctIds()
    {
        Assert.AreNotEqual(ItemKind.SupportCrate, ItemKind.SupportPack, "#265：两个不同 id，不得混用");

        var inv = new Inventory(Cfg());
        inv.ConfigureRecommended(out _);
        Assert.IsTrue(inv.CarriesSupportCrate, "推荐配置含**支援箱**（while_carried / 每回合 +1 SP）");
        Assert.AreEqual(0, inv.SupportPackCount, "推荐配置**不含**支援包（支援包是可选消耗品）");

        // 拾取一个支援包（先腾一格）→ 一次性使用 +2 SP（事件由调用方按 reason:item 记）
        inv.TryDiscardAt(0, out _);
        Assert.IsTrue(inv.TryAdd(new InventoryItem(ItemKind.SupportPack, "pack_1"), out _));
        Assert.AreEqual(1, inv.SupportPackCount);
        Assert.IsTrue(inv.TryUseSupportPack(out InventoryItem? used));
        Assert.AreEqual(ItemKind.SupportPack, used!.Kind);
        Assert.AreEqual(0, inv.SupportPackCount, "一次性消耗后不再持有");
    }

    [TestMethod]
    public void P21_BadInventoryData_ThrowsOnLoad()
    {
        // 推荐配置超格
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(
            ReadData("tuning.json").Replace("\"food\": 9", "\"food\": 15", StringComparison.Ordinal)),
            "recommended_loadout 超格 → 启动报错（P21 ⑥）");

        // 包满策略被改回"静默丢弃"
        Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(
            ReadData("tuning.json").Replace("\"choose_what_to_discard\"", "\"silent_drop\"", StringComparison.Ordinal)),
            "full_policy 非「选择丢弃」→ 启动报错（P21 ⑬）");
    }
}
