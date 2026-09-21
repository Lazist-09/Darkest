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
/// 🔴 **M9（4v4）子任务 D + B 的【改前读数】**（卡 M9 的验收：**契约仍能表达 6 槽（回归用例）** ✓）。
///
/// 两件事：
///   **D 契约仍能表达 6 槽**（**零行为** ✓）：`SlotLayout(6, 4, [5,6])` 必须仍能构造、仍能区分
///      `SlotKind.Combat/Support`；同时 **4 槽也成立** ⇒ 证明 4v4 是**数据选择**，不是把模型砍到 4 ✓
///   **B 现状读数**（改语义前的"before" ✓）：把 `formation.json` 的实际配置与"支援位上有谁、
///      未声明技能时会被扣多少 SP"打出来 ⇒ 供 M9 子任务 B/C 做**前后对照** ✓
///
/// 🔴 **零行为**：只构造纯数据模型 + 读 JSON ⇒ 不改任何数值、不接生产路径 ✓
/// </summary>
[TestClass]
public sealed class M9SlotContractTests
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

    // ── D：契约仍能表达 6 槽（回归用例 ✓）─────────────────────────────────────────────
    [TestMethod]
    public void Contract_StillExpressesSixSlots_EvenAfter4v4BecomesTheDataChoice()
    {
        var six = new SlotLayout(6, 4, new[] { 5, 6 });
        Assert.AreEqual(6, six.SlotCount, "6 槽仍可表达 ✓");
        Assert.AreEqual(4, six.CombatSlots, "其中 4 个是战斗位 ✓");
        CollectionAssert.AreEqual(new[] { 5, 6 }, six.ExtensionSlots.ToArray(), "5/6 是扩展（原支援）位 ✓");

        var four = new SlotLayout(4, 4, Array.Empty<int>());
        Assert.AreEqual(4, four.SlotCount, "4 槽同样成立 ✓");
        Assert.AreEqual(0, four.ExtensionSlots.Count, "4v4 没有扩展位 ✓");

        // 通用扩展位：**可为空**（卡里"ExtensionSlots ⇒ 通用扩展位（可为空）"✓）
        var noExt = new SlotLayout(5, 5, Array.Empty<int>());
        Assert.AreEqual(0, noExt.ExtensionSlots.Count, "扩展位可以为空 ✓");

        Console.WriteLine($"[M9·D] 6 槽 ✓（combat {six.CombatSlots} + 扩展 [{string.Join(",", six.ExtensionSlots)}]）· "
            + $"4 槽 ✓ · 空扩展位 ✓ ⇒ **契约仍能表达 6 槽** ✓");
        TestContext.WriteLine("[M9·D] 6 槽契约回归 ✓");
    }

    // ── B：改前读数（现状配置 + 支援位占用 + 未声明技能的 SP 兜底）────────────────────
    [TestMethod]
    public void CurrentState_BeforeSlotSemanticsChange()
    {
        FormationConfig formation = FormationConfig.Parse(ReadData("formation.json"));
        TuningConfig tuning = TuningConfig.Parse(ReadData("tuning.json"));

        string p = $"player: slot_count {formation.Player.SlotCount} · combat {formation.Player.CombatSlots}"
            + $" · support [{string.Join(",", formation.Player.ExtensionSlots)}]";
        string e = $"enemy: slot_count {formation.Enemy.SlotCount} · combat {formation.Enemy.CombatSlots}"
            + $" · support [{string.Join(",", formation.Enemy.ExtensionSlots)}]";

        Console.WriteLine($"[M9·B 改前] {p}");
        Console.WriteLine($"[M9·B 改前] {e}");

        // 支援位上现在坐着谁（一手数据 ✓）
        var support = new HashSet<int>(formation.Player.ExtensionSlots);
        foreach (var u in formation.InitialRoster.Player.Where(x => support.Contains(x.Slot)))
        {
            Console.WriteLine($"[M9·B 改前] 支援位 {u.Slot} = {u.Unit}（未声明 SP 的技能会按兜底被扣）✓");
        }

        Console.WriteLine($"[M9·B 改前] SP：start {tuning.SupportPoints.Start} · cap {tuning.SupportPoints.Cap}"
            + $" · regen/round {tuning.SupportPoints.RegenPerRound}（P19 要求 ≥ 1 ✓）");

        // 🔴 这些是"before"数字，供 B/C 做前后对照（本用例不改它们 ✓）
        Assert.IsTrue(formation.Player.ExtensionSlots.Count > 0, "改前我方**有**支援位（这正是 M9 要降级的对象 ✓）");
        Assert.IsTrue(tuning.SupportPoints.RegenPerRound >= 1, "P19：regen_per_round ≥ 1 ✓（M9 不动它 ✓）");
        TestContext.WriteLine("[M9·B] 改前读数已输出 ✓");
    }

    public TestContext TestContext { get; set; } = null!;
}
