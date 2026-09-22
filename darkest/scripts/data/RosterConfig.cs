using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>英雄特质（M8.0 ①，`#283`：每人 2~3 条【小幅】特质，**正负都有**）。</summary>
public sealed record HeroTraitConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("damage_pct")] int DamagePct = 0,
    [property: JsonPropertyName("morale_damage_pct")] int MoraleDamagePct = 0);

/// <summary>
/// 英雄个体（`#283` 7.2：名字 + 等级 + 个体差异；🔴 **不得含技能字段** —— 7.8 不碰技能表）。
/// 🔴 `Morale`（`#287` = **`#245` 的落地**）：**士气属于跨趟状态** ⇒ 它必须存在于名册（跨会话），
/// 缺省 = **新兵基准 50**；**回城不解算、不重置**（`#245`）。
/// </summary>
public sealed record HeroConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("archetype")] string Archetype,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("traits")] IReadOnlyList<HeroTraitConfig> Traits,
    // 🔴 数字外置（P29）：这里的默认值**不是魔法数**，而是**契约常量** `RosterConfig.RookieMorale`：
    //    P22⑦ 规定"**新兵（level=1）入场士气必须 = 50**"（校验器强制）⇒ 数据里新兵可以不写 morale，
    //    此时取契约值 ✓（我一度直接去掉默认值 + 要求 morale 必填 ⇒ 实测**打红了 3 条既有用例** ⚠️
    //    —— 教训：**先看契约怎么规定，再决定"该不该有默认值"**，别把"契约默认"误当"静默兜底"）
    [property: JsonPropertyName("morale")] int Morale = RosterConfig.RookieMorale,

    /// <summary>
    /// 🆕 **O-101 的「当前阶」· 武器**（策划 `DELIVERY-DESIGNER-FOUR-MEANINGS-TO-LEAD` ③ 的裁定原话）：
    ///   **「『当前阶』= 装备/升级等级 ⇒ 由 Roster/Hero 持有（`weaponTier`/`armourTier`）」** ✓
    ///   ⇒ 取值域 **0~4**（对应 `units.json` 的 5 阶武器表 ✓）
    /// 🔴 **默认 0** ⇒ 未声明 = 第 0 阶 ⇒ **与今天行为一致** ✓
    ///   （因为"伤害/减伤读阶"的两侧机制虽已备 —— `WeaponBaseDamage` / `TierDefence` —— 但**尚未接线** ✓ 零行为 ✓）
    /// ⚠️ **激活条件**：M1c 阶段 3（伤害读阶）与减伤读阶接线时，从本字段取阶 ✓
    /// </summary>
    [property: JsonPropertyName("weapon_tier")] int WeaponTier = 0,

    /// <summary>🆕 **O-101 的「当前阶」· 护甲**（同上 ✓ 取值域 0~4 ⇒ 对应 `armour[]` 5 阶 ✓ 默认 0 = 零行为 ✓）</summary>
    [property: JsonPropertyName("armour_tier")] int ArmourTier = 0);

/// <summary>等级成长（7.6：**只给属性小幅度**，HP+2 / 攻击+1；**不升技能**）。</summary>
/// <summary>
/// 🔴 **升级通道的数值载体**（契约：`hamlet.md` §7.2/§7.6「**战斗给经验 ⇒ 等级成长**」·
/// **1~6 级** · **只给属性小幅度** · **不升技能** ✓）
/// ⚠️ **字段缺省 ⇒ 未接线**（本项目纪律：**不假装已生效**）—— 数值由**策划**给（`#307`：我不动任何数值）✓
/// </summary>
public sealed record RosterExperience(
    [property: JsonPropertyName("xp_per_win")] int XpPerWin,
    [property: JsonPropertyName("xp_per_loss")] int XpPerLoss,
    // 🔴 **语义（策划 `#399` 裁定）**：**每级固定经验**（相对）—— `level_costs[0]` = **1→2 级所需**，
    //    `level_costs[1]` = 2→3 … ⇒ 与"绝对累计"的区别只在**起手等级 > 1** 时显现 ✓
    [property: JsonPropertyName("level_costs")] IReadOnlyList<int> LevelCosts,
    // 🔴 占位标注（`placeholder: true` 是本项目既有纪律）：**值是真值之前的临时值** ✓
    [property: JsonPropertyName("placeholder")] bool Placeholder = false)
{
    /// <summary>升到 `level + 1` 需要多少经验（`null` = 已到顶 / 无该级）✓</summary>
    public int? CostToNextLevel(int level, int levelMin, int levelMax)
    {
        int idx = level - levelMin;
        if (level >= levelMax || idx < 0 || idx >= LevelCosts.Count)
        {
            return null;
        }

        return LevelCosts[idx];
    }

    /// <summary>
    /// 🔴 **A10 读数口**（策划 `#399`）：「**多少场胜利升 1 级**」必须**可读** ——
    /// 玩家感受到的是"我打了 N 场，升了 1 级" ⇒ 这就是升级通道**有没有意义**的判据（同 A4）✓
    /// </summary>
    public int? BattlesToNextLevel(int level, int levelMin, int levelMax)
    {
        int? cost = CostToNextLevel(level, levelMin, levelMax);
        if (cost is null || XpPerWin <= 0)
        {
            return null;
        }

        return (int)Math.Ceiling(cost.Value / (double)XpPerWin);
    }
}

public sealed record RosterLevelGrowth(
    [property: JsonPropertyName("hp_per_level")] int HpPerLevel,
    [property: JsonPropertyName("attack_per_level")] int AttackPerLevel);

/// <summary>
/// `roster.json` 根模型 + **P22 校验（M8.0，`#284`）**：
/// ① `roster_cap == 12` 且 **> 6**（出征 6 + 替补 6 ⇒ 轮换成为策略）；
/// ② 等级 ∈ `[level_min, level_max]` = `[1, 6]`，且**不得出现技能升级字段**（7.8：不碰技能表）；
/// ③ 每人**特质 2~3 条**、**小幅**（|伤害%| ≤ 15、|士气伤害%| ≤ 20）、且**每人都正负兼有**。
/// （P22 的 ④金钱来源 / ⑤两减压建筑同价同效 / ⑥招募免费 属 M8.0 ②④⑤，落点不在本文件。）
/// </summary>
public sealed record RosterConfig(
    [property: JsonPropertyName("roster_cap")] int RosterCap,
    [property: JsonPropertyName("level_min")] int LevelMin,
    [property: JsonPropertyName("level_max")] int LevelMax,
    [property: JsonPropertyName("level_growth")] RosterLevelGrowth LevelGrowth,
    [property: JsonPropertyName("heroes")] IReadOnlyList<HeroConfig> Heroes,
    // 🆕 升级通道（契约 `hamlet.md` §7.2/§7.6「战斗给经验 ⇒ 等级成长」）：
    //    🔴 **缺省 = 未接线**（不假装生效 ✓）；**数值由策划给**（`#307`：我零数值改动）✓
    [property: JsonPropertyName("experience")] RosterExperience? Experience = null)
{
    public const string ResPath = "res://data/roster.json";

    /// <summary>新兵入场士气基准（`#287` / P22 ⑦）。</summary>
    public const int RookieMorale = 50;

    /// <summary>出征人数（与 formation 的 6 个槽位一致）。</summary>
    public const int SortieSize = 6;

    /// <summary>特质幅度上限（"小幅"的可测定义）。</summary>
    public const int MaxTraitDamagePct = 15;

    public const int MaxTraitMoraleDamagePct = 20;

    public static RosterConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        // ⚠️ 这里**不做** `morale` 的存在性断言：新兵（level=1）的士气由 **P22⑦ 契约** 规定为
        //    `RookieMorale`，数据里可以不写 ⇒ 那是**契约默认**、不是"静默兜底"（我先前判错，已纠正）✓

        RosterConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<RosterConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
            }) ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg, json);
        return cfg;
    }

    /// <summary>按原型筛出英雄（个体化后的"编成来源"）。</summary>
    public IReadOnlyList<HeroConfig> ByArchetype(string archetype)
        => Heroes.Where(h => h.Archetype == archetype).ToArray();

    private static void Validate(RosterConfig cfg, string rawJson)
    {
        // ① 名册上限 —— 🆕 **M7②（策划 `#423`）**：**上限的单一来源 = 马车曲线** ⇒ 此处**不再写死 12** ✓
        //   保留 P22 ① 的【实质】判据：**名册必须容得下出征人数**（`> 出征`）✓
        //   🔴 "= 12" 是旧口径的硬编码（`#423` 明列"6 处硬编码收敛"）⇒ 曲线末值 28 由
        //      `economy.json` 的 `stagecoach.max_roster` + `roster_cap_by_level` 与用例共同锁定 ✓
        if (cfg.RosterCap <= SortieSize)
        {
            throw new InvalidDataException(
                $"{ResPath}: roster_cap 必须 > 出征 {SortieSize}（实际 {cfg.RosterCap}；P22 ① 的实质口径）。");
        }

        if (cfg.LevelMin != 1 || cfg.LevelMax != 6)
        {
            throw new InvalidDataException($"{ResPath}: 等级区间必须 = [1, 6]（实际 [{cfg.LevelMin}, {cfg.LevelMax}]；P22 ②）。");
        }

        if (cfg.LevelGrowth is null || cfg.LevelGrowth.HpPerLevel <= 0 || cfg.LevelGrowth.AttackPerLevel <= 0)
        {
            throw new InvalidDataException($"{ResPath}: level_growth 必须为正的小幅度（P22 ②）。");
        }

        // ② 不得出现技能升级字段（7.8：不碰技能表 ⇒ M8.0 只给属性）
        foreach (string bad in new[] { "\"skill", "\"skills", "\"upgrade", "\"skill_upgrade" })
        {
            if (rawJson.Contains(bad, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 不得出现技能/升级字段（发现 {bad}；P22 ② / #283 7.8：不碰技能表）。");
            }
        }

        // 🔴 #283 7.4：名册**从 8 人起步、上限 12** —— 必须留出招募空间（M8.0 ⑤），
        // 否则"招募"在初始状态就永远不会被用到（实测踩到：初始 12/12 满员 ⇒ ⑤ 成死内容）。
        if (cfg.Heroes.Count < SortieSize || cfg.Heroes.Count > cfg.RosterCap)
        {
            throw new InvalidDataException(
                $"{ResPath}: heroes 数量必须 ∈ [出征 {SortieSize}, 上限 {cfg.RosterCap}]（实际 {cfg.Heroes.Count}；P22 ①）。");
        }

        var ids = new HashSet<string>();
        foreach (HeroConfig h in cfg.Heroes)
        {
            if (!ids.Add(h.Id))
            {
                throw new InvalidDataException($"{ResPath}: 英雄 id \"{h.Id}\" 重复（P22 ①）。");
            }

            if (string.IsNullOrWhiteSpace(h.Name) || string.IsNullOrWhiteSpace(h.Archetype))
            {
                throw new InvalidDataException($"{ResPath}: 英雄 \"{h.Id}\" 必须有 name 与 archetype（7.2）。");
            }

            if (h.Level < cfg.LevelMin || h.Level > cfg.LevelMax)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 英雄 \"{h.Id}\" level={h.Level} 不在 [{cfg.LevelMin}, {cfg.LevelMax}]（P22 ②）。");
            }

            // 🔴 P22 ⑦（#287 = #245 的落地）：士气是**跨趟**状态 ⇒ 必须在这里（名册），且
            // 新兵（1 级）入场基准 = 50（否则新兵与老兵分属两套语义）；回城不解算不重置 ⇒ 本文件只给"入册值"。
            if (h.Morale is < 0 or > 100)
            {
                throw new InvalidDataException($"{ResPath}: 英雄 \"{h.Id}\" morale={h.Morale} 必须 ∈ [0,100]（P22 ⑦）。");
            }

            if (h.Level == cfg.LevelMin && h.Morale != RookieMorale)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 新兵（level={cfg.LevelMin}）入场士气必须 = {RookieMorale}（英雄 \"{h.Id}\" 为 {h.Morale}；P22 ⑦）。");
            }

            if (h.Traits is null || h.Traits.Count is < 2 or > 3)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 英雄 \"{h.Id}\" 必须 2~3 条特质（实际 {h.Traits?.Count ?? 0}；P22 ③ / #283 7.7）。");
            }

            bool positive = false, negative = false;
            foreach (HeroTraitConfig t in h.Traits)
            {
                if (Math.Abs(t.DamagePct) > MaxTraitDamagePct || Math.Abs(t.MoraleDamagePct) > MaxTraitMoraleDamagePct)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: 特质 \"{t.Id}\" 幅度超出「小幅」（伤害 {t.DamagePct}% / 士气伤害 {t.MoraleDamagePct}%；P22 ③）。");
                }

                if (t.DamagePct == 0 && t.MoraleDamagePct == 0)
                {
                    throw new InvalidDataException($"{ResPath}: 特质 \"{t.Id}\" 必须有非零效果（P22 ③）。");
                }

                positive |= t.DamagePct > 0 || t.MoraleDamagePct < 0; // 伤害↑ / 受士气伤害↓ = 正
                negative |= t.DamagePct < 0 || t.MoraleDamagePct > 0; // 伤害↓ / 受士气伤害↑ = 负
            }

            if (!positive || !negative)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 英雄 \"{h.Id}\" 的特质必须**正负都有**（P22 ③ / #283 7.7）。");
            }
        }
    }
}
