using System;
using System.Collections.Generic;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M2 · buff 原语翻译器的【分类表】**（架构规则："**每条原语要么被引用、要么进未映射清单**"）。
///
/// 背景：原版 buff 是 **stat 导向**（`stat_type` / `stat_sub_type` / `amount` / `rule_type`），
///   我们是 **effect 导向**（`modifiers[].kind` = `damage_mod` / `prob_mod` / `stat_mod` / `state_flag`）✓
///   两套 schema **不同源** ⇒ 只有一部分能一一对应 ⇒ 其余**必须显式冻结**（不许静默跳过 ✓）
///
/// 🔴 本件是**分类器**（纯映射，**不落任何数据、不改 `buff_defs.json`**）✓
///   它的用途：把"翻译到哪一步了"变成**可数**：`Mapped` + `Frozen` = 全部原语 ✓
/// </summary>
public static class BuffPrimitiveTranslation
{
    /// <summary>一个原语被翻译后的归属（我们这侧的去向）。</summary>
    public enum Target
    {
        /// <summary>⇒ 我们的 `damage_mod`（伤害类修正）✓</summary>
        DamageMod,

        /// <summary>⇒ 我们的 `prob_mod`（概率/命中类）✓</summary>
        ProbMod,

        /// <summary>⇒ 我们的 `stat_mod`（属性增减/乘）✓</summary>
        StatMod,

        /// <summary>⇒ 我们的 `state_flag`（状态标志位，如眩晕/嘲讽）✓</summary>
        StateFlag,

        /// <summary>⇒ 不是 buff modifier，而是**单位抗性属性**（`units.json` 的 `*_resist`）✓</summary>
        UnitResistance,

        /// <summary>🔴 **显式冻结**：当前**没有对应关系**（不硬凑、不发明）——必须能数出来 ✓</summary>
        Frozen,
    }

    /// <summary>
    /// 分类（唯一入口）。判据全部来自实测的两侧结构，不是我凭感觉写的 ✓
    /// </summary>
    public static Target Classify(string statType, string? statSubType)
    {
        string st = statType ?? "";
        string sub = statSubType ?? "";

        // ① 属性乘/加：伤害类 ⇒ damage_mod；其余 ⇒ stat_mod ✓
        if (st is "combat_stat_multiply" or "combat_stat_add")
        {
            return sub is "damage_low" or "damage_high" or "damage_received_percent"
                ? Target.DamageMod
                : Target.StatMod;
        }

        // ② 抗性：原版是"抗性轴"⇒ 我们放在**单位属性**（`units.json` 的 `*_resist`）而不是 buff modifier ✓
        if (st == "resistance")
        {
            return Target.UnitResistance;
        }

        // ③ 概率/命中类（原版的 chance 家族）⇒ prob_mod ✓
        if (st.EndsWith("_chance", StringComparison.Ordinal))
        {
            return Target.ProbMod;
        }

        // ④ 状态标志类（我们这侧的 state_flag）✓
        if (st is "stun" or "mark" or "taunt")
        {
            return Target.StateFlag;
        }

        // ⑤ 其余（治疗/压力/侦察/食物/决心/惊喜/移除怪癖…）⇒ 🔴 **显式冻结**（阶段 A 不硬凑 ✓）
        return Target.Frozen;
    }

    /// <summary>
    /// 🔴 **冻结清单的理由**（给"为什么这条还没翻"一个可读的答案；**不是**"懒得做" ✓）：
    ///   它们要么依赖**我们还没有的系统**（治疗量/食欲/惊喜/移除怪癖），
    ///   要么是**远征层读数**（侦察/食物），要么原版把它表达成 `rule`（DoT）⇒ 需要先定规则再翻 ✓
    /// </summary>
    public static string FrozenReason(string statType) => statType switch
    {
        "hp_heal_percent" or "hp_heal_amount" or "hp_heal_received_percent" => "治疗系统（阶段 A 未落：治疗量修正）✓",
        "stress_dmg_percent" or "stress_dmg_received_percent" or "stress_heal_percent" or "stress_heal_received_percent"
            => "压力系统已有，但**修正轴**未接（属 M3）✓",
        "resolve_check_percent" or "resolve_xp_percent" => "决心检定/经验（属 M3）✓",
        "scouting_chance" => "**远征层**读数（侦察），不是战斗 modifier ✓",
        "food_consumption_percent" or "starving_damage_percent" => "饥饿/食物（远征层）✓",
        "party_surprise_chance" or "monsters_surprise_chance" or "monster_surpirse_chance" => "伏击/惊喜（远征层）✓",
        "remove_quirk_chance" or "remove_negative_quirk_chance" => "怪癖移除（M5 之后）✓",
        "debuff_chance" or "dmg_received_percent" => "概率/减伤轴（待与 prob_mod/damage_mod 的细分口径一起定）✓",
        _ => "未定（需先定规则再翻）✓",
    };

    /// <summary>可数读数：把一批原语分类后统计（供报表/用例断言）✓</summary>
    public static IReadOnlyDictionary<Target, int> Tally(IEnumerable<(string StatType, string? StatSubType)> primitives)
    {
        var counts = new Dictionary<Target, int>();
        foreach (var (st, sub) in primitives)
        {
            Target t = Classify(st, sub);
            counts[t] = counts.TryGetValue(t, out int n) ? n + 1 : 1;
        }

        return counts;
    }
}
