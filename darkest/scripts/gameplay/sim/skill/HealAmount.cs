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
    ///   本文件**只覆盖 `hp_heal_percent` 一条** ⇒ 其余原语**仍未激活** ✓
    ///
    /// 🔴 **R22（A1）改成【单一真值】**：清单本体搬去 `BuffPrimitiveTranslation.Activated` /
    ///   `.Pending`（那是 M2 的清单表），这里只做**转发** ⇒ 不再有两份会互相漂移的名单 ✓
    ///   ⚠️ 同时**修掉了 3 个上游不存在的名字**（`resolve_xp_percent` / `remove_quirk_chance` /
    ///   `dmg_received_percent` 的真名见 `DestinationNote`）—— 旧名单里它们"永远接不上" ✓
    ///   条数由 13 变 **24**：旧名单是手写的 14 条，新名单是**参考项目实测的 25 个 `stat_type` − 已激活 1 条** ✓
    /// </summary>
    public static IReadOnlyList<string> ActivatedPrimitives => BuffPrimitiveTranslation.Activated;

    /// <summary>仍未激活的（`BuffPrimitiveTranslation.Pending` 的转发 · 保持**可打印** ✓）</summary>
    public static IReadOnlyList<string> StillFrozen => BuffPrimitiveTranslation.Pending;
}
