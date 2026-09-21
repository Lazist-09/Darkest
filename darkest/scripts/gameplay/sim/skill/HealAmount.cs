using System;
using System.Collections.Generic;
using Darkest.Core.Contracts;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Skill;

/// <summary>
/// 🆕 **M2 激活（架构裁 (丙) · 第 1 条）：`hp_heal_percent` 的纯算法** ✓
///
/// WHY 抽成纯函数：激活一条原语时，**"改了行为"必须能被单测钉住** ✓ ——
///   否则只能在"整场战斗"里间接看 ✗（那既慢又说不清是哪一步改的 ✓）
///   所以：**算法在这里（可单测）**，`SkillExecutor` 只负责"取修正 + 调这里" ✓
///
/// 语义（原版）：`hp_heal_percent` = **施法者**的"治疗量"百分比修正 ⇒ `治疗量 ×(1 + pct/100)` ✓
/// 🔴 **pct = 0 ⇒ 原样返回**（**前置读数**：激活前后逐位相同 ✓）
/// </summary>
public static class HealAmount
{
    /// <summary>按百分比修正缩放治疗量（四舍五入到整数 ✓；`pct = 0` ⇒ 原样 ✓）</summary>
    public static int Scale(int baseHeal, int percent)
        => percent == 0 ? baseHeal : (int)Math.Round(baseHeal * (1.0 + percent / 100.0));

    /// <summary>
    /// 从台账取**施法者**的 `hp_heal_percent` 并缩放（`null` 台账 ⇒ 视为 0 ✓）
    /// </summary>
    public static int ScaleByCaster(IBuffLedger? buffs, UnitId caster, int baseHeal)
        => Scale(baseHeal, buffs?.PercentModAny(caster, "hp_heal_percent") ?? 0);

    /// <summary>
    /// 🆕 **BuffsCrossChecked 的自我声明**（延续 M4 的做法：**"未跑"就标"未跑"**✓）：
    ///   本文件**只覆盖 `hp_heal_percent` 一条** ⇒ 其余 14 条冻结原语**仍未激活** ✓
    /// </summary>
    public static IReadOnlyList<string> ActivatedPrimitives { get; } = new[] { "hp_heal_percent" };

    /// <summary>仍未激活的（冻结清单的其余部分 · 保持**可打印** ✓）</summary>
    public static IReadOnlyList<string> StillFrozen { get; } = new[]
    {
        "hp_heal_received_percent", "stress_dmg_percent", "stress_dmg_received_percent",
        "resolve_check_percent", "resolve_xp_percent", "scouting_chance",
        "food_consumption_percent", "starving_damage_percent",
        "party_surprise_chance", "monsters_surprise_chance", "remove_quirk_chance",
        "debuff_chance", "dmg_received_percent",
    };
}
