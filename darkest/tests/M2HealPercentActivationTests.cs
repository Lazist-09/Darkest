using System;
using System.Linq;
using Darkest.Gameplay.Sim.Skill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Darkest.Tests;

/// <summary>
/// 🔴 **M2 激活（架构裁 (丙)）第 1 条：`hp_heal_percent`** 的用例 ✓
///
/// 架构的要求（原话）：「**激活一条 ⇒ 一条用例 + 前后读数**（否则"激活了"只是声称）」✓
///   · **前（before）**：**全量 798/798 不变** + `Scale(x, 0) == x`（默认路径逐位相同 ✓）
///   · **后（after）**：`Scale` 在 +25% / −40% / +100% 下的**具体数**（下面逐个钉住 ✓）
/// 🔴 并**保持冻结清单可打印** ✓（架构要求："未映射清单保持可打印" ✓）
/// </summary>
[TestClass]
public sealed class M2HealPercentActivationTests
{
    [TestMethod]
    public void Before_NoModifier_IsBitForBitIdentity()
    {
        // 🔴 这是"前置读数"：没有 `hp_heal_percent` 时，治疗量与激活前**逐位相同** ✓
        foreach (int baseHeal in new[] { 1, 5, 12, 15, 100, 999 })
        {
            Assert.AreEqual(baseHeal, HealAmount.Scale(baseHeal, 0), $"pct=0 ⇒ 原样（base={baseHeal}）✓");
        }

        Console.WriteLine("[M2·激活] 前：pct=0 ⇒ 原样（1/5/12/15/100/999 全等 ✓）");
    }

    [TestMethod]
    public void After_PercentScalesTheHeal_RoundedToInteger()
    {
        // +25% / −40% / +100% —— 与策划给的试点口径同族（0% 与负修正都见过 ✓）
        Assert.AreEqual(15, HealAmount.Scale(12, 25), "12 +25% = 15 ✓");
        Assert.AreEqual(7, HealAmount.Scale(12, -40), "12 −40% = 7.2 ⇒ 7 ✓");
        Assert.AreEqual(24, HealAmount.Scale(12, 100), "12 +100% = 24 ✓");
        Assert.AreEqual(5, HealAmount.Scale(5, -1), "5 −1% = 4.95 ⇒ 5 ✓");
        Assert.AreEqual(10, HealAmount.Scale(15, -33), "15 −33% = 10.05 ⇒ 10 ✓");

        Console.WriteLine("[M2·激活] 后：12 +25%⇒15 · 12 −40%⇒7 · 12 +100%⇒24 · 5 −1%⇒5 · 15 −33%⇒10 ✓");
    }

    [TestMethod]
    public void FrozenList_StaysCountedAndPrintable()
    {
        // 架构要求：**未映射清单保持可打印** ✓ ⇒ 已激活 + 仍冻结 = 冻结清单总数（可核对 ✓）
        int activated = HealAmount.ActivatedPrimitives.Count;
        int frozen = HealAmount.StillFrozen.Count;

        Assert.AreEqual(1, activated, "已激活 = 1 条（`hp_heal_percent`）✓");
        Assert.AreEqual(13, frozen, "仍冻结 = 13 条 ✓");
        Assert.IsFalse(HealAmount.StillFrozen.Contains("hp_heal_percent"), "已激活的不应还在冻结清单里 ✓");

        Console.WriteLine($"[M2·激活] 清单：已激活 {activated} 条（{string.Join(",", HealAmount.ActivatedPrimitives)}）"
            + $" · 仍冻结 {frozen} 条（可打印 ✓）");
    }

    public TestContext TestContext { get; set; } = null!;
}
