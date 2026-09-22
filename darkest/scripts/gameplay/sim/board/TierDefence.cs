using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;

namespace Darkest.Gameplay.Sim.Board;

/// <summary>
/// 🔴 **目标 ③④ 的【减伤侧纯机制】**：把「**当前阶**」变成可算的护甲读数 ✓
///
/// WHY（现状盘点，实测）：
///   · **伤害侧**已有纯机制 `WeaponBaseDamage`（一手 5 阶区间 × (1+dmg%) ✓ 无生产调用方 ✓）
///   · **减伤侧缺** ✗ —— 战斗现在读的是 `UnitStats.Prot` / `UnitStats.Dodge`（**顶层各一个数** ✗）
///     而一手/第三方的护甲表是**逐阶**的：`armour[i].{def_pct, prot, hp}` ✓
///   ⇒ 架构裁"阶数要影响伤害"两步走：① 数据能表达阶 ✓（已做）② 伤害/减伤读阶 ⇒ **本件补第②步的减伤半** ✓
///
/// 🔴 **不重复实现**：取阶护甲直接复用 `UnitStats.ArmourAt(tier)`（**单一出处** ✓）；
///   本件只加"没有 5 阶时退回顶层值"的语义 + 比例换算 ✓
///
/// 🔴 **零行为**：本文件**无任何生产调用方** ⇒ 被 `WeaponDamageModelStage1Tests` 的"**无人消费**"守卫钉住 ✓
/// ⚠️ **激活条件**：先定「**当前阶从哪来**」—— 三选项见 `reports/tier_source_options.md`（架构/策划裁 ✓）
/// </summary>
public static class TierDefence
{
/// <summary>🔴 **钳制**：`UnitStats.ArmourAt` 对越界返回 null（它不钳 ✗）⇒ 本件负责钳，
    /// 让"越界 = 取最近一阶"这条**文档化的语义**真的成立 ✓（否则会静默退回顶层 ✗）</summary>
    private static int Clamp(UnitStats stats, int tier)
    {
        int count = stats.ArmourTiers?.Count ?? 0;
        return count == 0 ? 0 : Math.Clamp(tier, 0, count - 1);
    }

    /// <summary>该单位**第 `tier` 阶护甲**的减伤（原版 `prot`，百分比整数 0~85 ✓）；
    /// 该单位没有 5 阶（敌人 = 我们自设 ✓）⇒ **退回顶层 `Prot`** ✓</summary>
    public static int ProtAt(UnitStats stats, int tier)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return stats.ArmourAt(Clamp(stats, tier))?.Prot ?? stats.Prot;
    }

    /// <summary>该单位**第 `tier` 阶护甲**的闪避（原版 `def` ✓）；没有 5 阶 ⇒ **退回顶层 `Dodge`** ✓</summary>
    public static int DefAt(UnitStats stats, int tier)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return stats.ArmourAt(Clamp(stats, tier))?.DefPct ?? stats.Dodge;
    }

    /// <summary>同上，但换算成**减伤比例**（沿用 M1a 已定的封顶规则：0~0.85 ✓）</summary>
    public static double ProtFractionAt(UnitStats stats, int tier)
        => Math.Clamp(ProtAt(stats, tier) / 100.0, 0.0, 0.85);

    /// <summary>
    /// **阶数就位后，顶层读数会长什么样**（**只供对照/报表**，不改任何数据 ✓）：
    ///   返回 (Prot, Dodge, Hp)，取自该阶护甲；没有 5 阶 ⇒ 顶层 ✓
    /// </summary>
    public static (int Prot, int Dodge, int Hp) ArmourViewAt(UnitStats stats, int tier)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArmourTier? a = stats.ArmourAt(Clamp(stats, tier));
        return a is null ? (stats.Prot, stats.Dodge, stats.Hp) : (a.Prot, a.DefPct, a.Hp);
    }
}
