using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>扎营技能（`camp_skills.json` 的一行）。</summary>
public sealed record CampSkillConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("owner_unit")] string OwnerUnit,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("effect")] string Effect);

/// <summary>
/// `camp_skills.json` 根模型 + **加载级防线（红线 21 的既有做法，照 `BuffDefsConfig.ConsumedEffectNames`）**。
///
/// 🔴 立此防线的原因（**实测审计**）：文件里 **12 个技能 / 9 种 effect 名**，而
/// **只有 `ambush_immunity_once` 有消费点**（守夜 ／ 站岗 ⇒ `ExpeditionSession.RollAmbush` 消费免疫）。
/// 其余 8 种**在全仓没有实现**（`grant_buff:next_battle_*` 虽在 `buff_defs.json` 有定义、**但无消费**）。
/// ⇒ 若不设防线，**将来任何人加技能/改 effect 名都不会被发现** ⇒ 又是一个"花钱买不到东西"（红线 21 最坏形态）。
///
/// 🔴 因此本类**强制把 effect 名分成两类**（新增未登记的名字 ⇒ **启动即报错**）：
/// · <see cref="ConsumedEffectNames"/>：**已接线**（真的会生效）
/// · <see cref="DeferredStageTwoEffectNames"/>：**契约里存在、但当前无落点**（阶段二）——
///   需要【跨战斗的"待生效效果"层】才能真正生效（见 `#307` 之后的审计结论）：
///   现实现里 buff 台账 ／ 士气 ／ 单位都是【每场新建】⇒ `next_battle` / `morale_plus` / `heal_*` 无处可落。
/// ⚠️ **在 (A) 落地之前，这些技能【不得出现在 UI 上】**（否则玩家花钱买不到东西）；
///    ✅ 目前**没有扎营技能面板** ⇒ 玩家确实选不了 ⇒ 属**潜在风险**而非活跃缺陷。
/// </summary>
public sealed record CampSkillsConfig(
    [property: JsonPropertyName("camp_skills")] IReadOnlyList<CampSkillConfig> Skills)
{
    public const string ResPath = "res://data/camp_skills.json";

    /// <summary>**已接线**的 effect 名（真的会生效）。</summary>
    public static readonly IReadOnlySet<string> ConsumedEffectNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "ambush_immunity_once",
    };

    /// <summary>
    /// **契约里已声明、但当前无落点**的 effect 名（**阶段二**）——
    /// 需【跨战斗待生效效果层】才能生效；**在此之前不得上 UI**（否则违反红线 21）。
    /// </summary>
    public static readonly IReadOnlySet<string> DeferredStageTwoEffectNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "grant_buff:next_battle_sharpen",
        "grant_buff:next_battle_armor",
        "morale_plus_8",
        "morale_plus_5_team",
        "morale_plus_8_team",
        "heal_15_percent_and_clear_bleed",
        "clear_weak_and_deaths_door_recovery",
        "heal_5_percent",
        "morale_damage_minus_15_for_4_battles",
    };

    public static CampSkillsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        CampSkillsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<CampSkillsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
            }) ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    /// <summary>已接线的技能数（供启动日志：让"接线进度"可见）。</summary>
    public int ConsumedCount => Skills.Count(s => ConsumedEffectNames.Contains(s.Effect));

    /// <summary>阶段二（未接线）的技能数。</summary>
    public int DeferredCount => Skills.Count(s => DeferredStageTwoEffectNames.Contains(s.Effect));

    private static void Validate(CampSkillsConfig cfg)
    {
        if (cfg.Skills is null || cfg.Skills.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: skills 不可为空。");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (CampSkillConfig s in cfg.Skills)
        {
            if (string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id) || string.IsNullOrWhiteSpace(s.Name))
            {
                throw new InvalidDataException($"{ResPath}: 技能 id/name 缺失或重复。");
            }

            if (s.Cost < 1)
            {
                throw new InvalidDataException($"{ResPath}: 技能 [{s.Id}] 的 cost 必须 ≥ 1（点数不足要能灰显）。");
            }

            // 🔴 防线：effect 名**必须**登记在【已接线】或【阶段二】之一 ⇒ 否则**启动即报错**
            //    （防止"新增 effect 名但没人实现"再次发生 —— 红线 21 的正确防线）
            if (!ConsumedEffectNames.Contains(s.Effect) && !DeferredStageTwoEffectNames.Contains(s.Effect))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 技能 [{s.Id}] 的 effect \"{s.Effect}\" **未登记** —— " +
                    $"若已接线 ⇒ 加进 ConsumedEffectNames；若属阶段二 ⇒ 加进 DeferredStageTwoEffectNames（红线 21）。");
            }
        }
    }
}
