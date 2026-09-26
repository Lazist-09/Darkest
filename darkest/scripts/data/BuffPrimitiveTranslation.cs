using System;
using System.Collections.Generic;
using System.Linq;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M2 · buff 原语翻译器的【分类表】**（架构规则："**每条原语要么被引用、要么进未映射清单**"）。
///
/// 背景：原版 buff 是 **stat 导向**（`stat_type` / `stat_sub_type` / `amount` / `rule_type`），
///   我们是 **effect 导向**（`modifiers[].kind` = `damage_mod` / `prob_mod` / `stat_mod` / `state_flag`）✓
///   两套 schema **不同源** ⇒ 只有一部分能一一对应 ⇒ 其余**必须显式冻结**（不许静默跳过 ✓）
///
/// 🔴 **R22（A1）改了判据源**：此前本表按【一手 E 盘】的 48 组合 / 27 个 `stat_type` 判；
///   用户指令（2026-09-25"数值采用本地参考项目"）之后，判据源换成
///   `darkest/data/buff_primitives.json`（参考项目 1801 条 / **25 个 `stat_type` / 41 组合**）✓
///   ⇒ 表里**每一个名字都是实测的**，不再有"照感觉写"的条目（见 `reports/ref_buff_primitives_source.md`）✓
///
/// 🔴 本件是**分类器**（纯映射，**不落任何数据、不改 `buff_defs.json`**）✓
///   它的用途：把"翻译到哪一步了"变成**可数**：`Activated` + `Pending` = 全部原语 ✓
/// </summary>
public static class BuffPrimitiveTranslation
{
    /// <summary>一个原语被翻译后的归属（我们这侧的去向）。</summary>
    public enum Target
    {
        /// <summary>⇒ 我们的 `damage_mod`（**物理/精神伤害**乘区）✓</summary>
        DamageMod,

        /// <summary>⇒ 士气（压力）轴：伤害 / 恢复 / 决心检定与经验 ✓</summary>
        MoraleMod,

        /// <summary>⇒ 治疗轴：治疗量 / 受治疗量 ✓</summary>
        HealMod,

        /// <summary>⇒ 我们的 `prob_mod`（**战斗内**施加概率：眩晕/中毒/流血/位移/减益）✓</summary>
        ProbMod,

        /// <summary>⇒ 我们的 `stat_mod`（属性增减/乘：攻/暴/防/盾/速/最大生命）✓</summary>
        StatMod,

        /// <summary>⇒ 我们的 `state_flag`（状态标志位，如眩晕/嘲讽）✓</summary>
        StateFlag,

        /// <summary>⇒ 不是 buff modifier，而是**单位抗性属性**（`units.json` 的 `*_resist`）✓</summary>
        UnitResistance,

        /// <summary>
        /// ⇒ **远征/城镇层**读数（侦察 / 食物 / 伏击 / 移除怪癖 / 升级折扣）——
        /// 它们**不是战斗 modifier** ⇒ 归 `RunSession` / 城镇侧，不归战斗结算 ✓
        /// </summary>
        ExpeditionLayer,

        /// <summary>🔴 **显式冻结**：当前**没有对应关系**（不硬凑、不发明）——必须能数出来 ✓</summary>
        Frozen,
    }

    /// <summary>
    /// `stat_type` → 去向（**实测的 23 个"不带子类型分支"的名字**；另 2 个见 `Classify` 的特判）✓
    /// ⚠️ 这是**映射表**（结构性），不是数值 ⇒ 不违反"平衡数字必须搬去 data" ✓
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Target> ByStatType =
        new Dictionary<string, Target>(StringComparer.Ordinal)
        {
            // ① 伤害 / 属性
            ["damage_received_percent"] = Target.DamageMod,
            ["resistance"] = Target.UnitResistance,
            // ② 士气轴（压力）
            ["stress_dmg_percent"] = Target.MoraleMod,
            ["stress_dmg_received_percent"] = Target.MoraleMod,
            ["stress_heal_percent"] = Target.MoraleMod,
            ["stress_heal_received_percent"] = Target.MoraleMod,
            ["resolve_check_percent"] = Target.MoraleMod,
            ["resolve_xp_bonus_percent"] = Target.MoraleMod,
            // ③ 治疗轴
            ["hp_heal_percent"] = Target.HealMod,
            ["hp_heal_received_percent"] = Target.HealMod,
            ["hp_heal_amount"] = Target.HealMod,
            // ④ 战斗内施加概率
            ["stun_chance"] = Target.ProbMod,
            ["poison_chance"] = Target.ProbMod,
            ["bleed_chance"] = Target.ProbMod,
            ["move_chance"] = Target.ProbMod,
            ["debuff_chance"] = Target.ProbMod,
            // ⑤ 远征 / 城镇层（**不是**战斗 modifier）
            ["scouting_chance"] = Target.ExpeditionLayer,
            ["food_consumption_percent"] = Target.ExpeditionLayer,
            ["starving_damage_percent"] = Target.ExpeditionLayer,
            ["party_surprise_chance"] = Target.ExpeditionLayer,
            ["monsters_surprise_chance"] = Target.ExpeditionLayer,
            ["remove_negative_quirk_chance"] = Target.ExpeditionLayer,
            ["upgrade_discount"] = Target.ExpeditionLayer,
        };

    /// <summary>
    /// 分类（唯一入口）。判据全部来自实测的两侧结构，不是我凭感觉写的 ✓
    /// 🔴 `combat_stat_add` / `combat_stat_multiply` 是**唯二带子类型分支**的：
    ///   `combat_stat_add` 实测只用 5 个**属性**子类型（attack_rating / crit_chance / defense_rating /
    ///     protection_rating / speed_rating）⇒ **恒为 `StatMod`**（它从不改伤害）✓
    ///   `combat_stat_multiply` 的 `damage_low` / `damage_high`（各 177 条）⇒ `DamageMod`；
    ///     其余（`max_hp` 36 / `defense_rating` 1）⇒ `StatMod` ✓
    /// </summary>
    public static Target Classify(string statType, string? statSubType)
    {
        string st = statType ?? "";
        if (st is "combat_stat_add" or "combat_stat_multiply")
        {
            return (statSubType ?? "") is "damage_low" or "damage_high"
                ? Target.DamageMod
                : Target.StatMod;
        }

        return ByStatType.TryGetValue(st, out Target t) ? t : Target.Frozen;
    }

    /// <summary>
    /// 🔴 **去向说明**（给"这条原语去哪一层 / 为什么还没接"一个可读答案；**不是**"懒得做" ✓）。
    /// 报告与用例都读它 ⇒ 任何 `stat_type` 都必须有话说（不许静默）✓
    /// </summary>
    public static string DestinationNote(string statType) => statType switch
    {
        "combat_stat_add" => "属性加值（攻/暴/防/盾/速）⇒ stat_mod；**实测无伤害子类型** ✓",
        "combat_stat_multiply" => "`damage_low`/`damage_high` ⇒ damage_mod；`max_hp`/`defense_rating` ⇒ stat_mod ✓",
        "damage_received_percent" => "受伤乘区 ⇒ damage_mod（`DamageStep` 的 `raw` 层）✓",
        "resistance" => "**单位抗性属性**（实测 8 轴：poison/bleed/move/debuff/disease/stun/death_blow/trap）⇒ `units.json` ✓",
        "stress_dmg_percent" or "stress_dmg_received_percent" => "士气伤害 ⇒ `MoraleLedger.Apply` 的修正轴 ✓",
        "stress_heal_percent" or "stress_heal_received_percent" => "士气恢复 ⇒ 城镇/扎营恢复的修正轴 ✓",
        "resolve_check_percent" => "决心检定 ⇒ 士气系统（压力 ≥100 的检定）✓",
        "resolve_xp_bonus_percent" => "决心经验加成 ⇒ 结算管线 ✓",
        "hp_heal_percent" => "治疗量 ⇒ **已激活**（`HealAmount.ScaleByCaster`）✓",
        "hp_heal_received_percent" => "受治疗量 ⇒ 治疗结算的受方乘区 ✓",
        "hp_heal_amount" => "治疗量（平加）⇒ 治疗结算 ✓",
        "stun_chance" or "poison_chance" or "bleed_chance" or "move_chance" or "debuff_chance"
            => "**战斗内施加概率** ⇒ prob_mod（按名分发；抗性公式 `Clamp(chance - resist, 0, 0.95)`）✓",
        "scouting_chance" => "**远征层**读数（侦察），不是战斗 modifier ✓",
        "food_consumption_percent" => "**远征层**读数（食物消耗）✓",
        "starving_damage_percent" => "**远征层**读数（饥饿伤害）✓",
        "party_surprise_chance" or "monsters_surprise_chance" => "**远征层**读数（伏击/惊喜）✓",
        "remove_negative_quirk_chance" => "**城镇层**（疗养院移除负面怪癖）✓",
        "upgrade_discount" => "**城镇层**（实测子类型只有 weapon / armour）✓",
        _ => "未定（参考项目此刻没有这个 `stat_type`；若上游新增 ⇒ 必须在此给出去向）✓",
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

    /// <summary>
    /// 🔴 **M2 已激活清单**（**唯一一条**：`hp_heal_percent`，消费点 `HealAmount.ScaleByCaster`）✓
    /// </summary>
    public static IReadOnlyList<string> Activated { get; } = new[] { "hp_heal_percent" };

    /// <summary>
    /// 🔴 **M2 待接清单**（参考项目实测的 25 个 `stat_type` 去掉已激活的 1 条 ⇒ **24 条**；
    /// 顺序 = 池内出现次数从多到少 ⇒ **先接影响面大的** ✓）。
    /// ⚠️ 本清单**必须**与 `buff_primitives.json` 的 `stat_type` 闭集逐一相等 ——
    ///   由 `BuffPrimitivesTests` 双向断言（少一条/多一条都红）✓
    /// </summary>
    public static IReadOnlyList<string> Pending { get; } = new[]
    {
        "combat_stat_add", "combat_stat_multiply", "resistance", "stress_dmg_received_percent",
        "debuff_chance", "resolve_check_percent", "scouting_chance", "hp_heal_received_percent",
        "stress_heal_received_percent", "resolve_xp_bonus_percent", "monsters_surprise_chance",
        "food_consumption_percent", "stun_chance", "poison_chance", "move_chance", "bleed_chance",
        "hp_heal_amount", "starving_damage_percent", "remove_negative_quirk_chance",
        "party_surprise_chance", "damage_received_percent", "stress_heal_percent",
        "upgrade_discount", "stress_dmg_percent",
    };

    /// <summary>清单里所有名字（已激活 + 待接）—— 供报表与用例打印 ✓</summary>
    public static IReadOnlyList<string> Checklist { get; } = Activated.Concat(Pending).ToArray();

    /// <summary>
    /// 🔴 **待接条件**（架构 `M2-ROUTE-B-20260921` 裁定 ② 要求的那一列）——
    /// 回答"**为什么它还没接**"，并区分**两种成本差一个量级的"缺"**：
    ///   · 🔴 **缺载体** ⇒ **要先造一层结构**（趟级/城池级修正载体不存在）⇒ 成本高
    ///   · ⚠️ **未接线** ⇒ **落点已有，只差连上** ⇒ 成本低
    /// 🎖️ **构成本属性【不手工维护】**：它是从 `ByStatType` / `Classify` 的**去向**推出来的
    ///   ⇒ ✅ 不会与前两张清单漂移（纪律：一处本体 + 其余转发 ✓）
    /// ⚠️ **它【不是】"能不能接"的判决** —— 判决在策划/架构；本列只陈述**前置条件** ✓
    /// </summary>
    public static string PendingCondition(string name)
    {
        Target where = Classify(name, null);
        return where switch
        {
            // 战斗级去向：落点都在战斗结算里 ⇒ 只差接线
            Target.DamageMod or Target.MoraleMod or Target.HealMod
                or Target.ProbMod or Target.StatMod or Target.StateFlag
                => "⚠️ 未接线（落点在战斗结算，只差连上）",
            Target.UnitResistance
                => "⚠️ 未接线（落点是 units.json 的抗性属性，不走 buff 台账）",
            // 🔴 趟级/城池级：我们的修正载体（BuffLedger）是【战斗级】⇒ 缺一层结构
            Target.ExpeditionLayer
                => "🔴 缺载体（落点在趟级/城池级，而 buff 修正载体是战斗级 ⇒ 需先造趟级修正载体）",
            _ => "🔴 去向未定（Frozen）",
        };
    }

    /// <summary>待接清单 + 各自的待接条件（供报表与用例打印 ✓）。</summary>
    public static IReadOnlyList<(string Name, string Condition)> PendingWithConditions { get; } =
        Pending.Select(n => (n, PendingCondition(n))).ToArray();

    /// <summary>
    /// 🔴 **加载即校验（红线的防火墙）**：清单里每个名字**都必须**是参考项目真有的 `stat_type`。
    /// WHY 必要：实测踩过 —— 旧清单里有 `resolve_xp_percent` / `remove_quirk_chance` /
    ///   `dmg_received_percent` **三个名字上游根本不存在**（真名是 `..._bonus_percent` /
    ///   `remove_negative_...` / `damage_...`）⇒ 那 3 条**永远接不上**，而清单上却写着"待接" ⚠️
    ///   （`#290` 红线 21："写了但没接上"）⇒ 从此**加载时就报**，不再靠人眼 ✓
    /// **生产消费点**：`DirectorBridge`（战斗装配时调用）✓
    /// </summary>
    public static void ValidateAgainst(BuffPrimitivesConfig primitives)
    {
        if (primitives is null)
        {
            throw new ArgumentNullException(nameof(primitives));
        }

        var missing = Checklist.Where(n => !primitives.StatTypes.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{BuffPrimitivesConfig.ResPath}: M2 清单里 {missing.Length} 个名字参考项目没有 —— "
                + $"{string.Join(", ", missing)}（红线 21：写了但接不上 ⇒ 必须改名或从清单删）✓");
        }
    }
}
