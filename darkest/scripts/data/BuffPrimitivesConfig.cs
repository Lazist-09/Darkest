using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 一条 buff 原语的 `rule_data`（原版恒为 `{float, string}` 两键，1801/1801 都在 ✓）。
/// 🔴 **实测的用法**（`reports/ref_buff_primitives_source.md`）：
///   · `float` 非零的规则 = `hpabove` / `hpbelow` / `lightabove` / `lightbelow` / `stress_above` /
///     `monsterSize` / `in_rank`（阈值 / 名次）✓
///   · `string` 非空的规则 = `monsterType`（怪物种族）/ `skill` / `actorStatus` / `in_activity` /
///     `in_dungeon` / `in_mode`（实测只有 19 种取值，见报告 ✓）
///   · `always` / `afflicted` / `at_deaths_door` / `firstroundonly` / `in_camp` / `meleeonly` 等
///     **两键都是空的**（0 与 ""）⇒ 规则本身自足 ✓
/// </summary>
public sealed record BuffPrimitiveRuleData(
    [property: JsonPropertyName("float")] double Float,
    [property: JsonPropertyName("string")] string Text);

/// <summary>
/// 🔴 **A1 · 一条 buff 原语**（原版 schema 逐字段转写，**不做任何换算/重命名** ✓）。
///
/// WHY 逐字段转写：原版的 buff 是 **stat 导向**的 ——
///   `(stat_type, stat_sub_type, amount)` 说"改什么、改多少"，
///   `(rule_type, is_false_rule, rule_data)` 说"什么时候改"，
///   `(duration_type, duration)` 说"改多久" ⇒ **一条 = 一个修改器** ✓
///
/// 🔴 **`amount` 是【分数】不是百分比**（实测 1801 条里 1676 条非整数）：
///   `0.04` 读作 **4%** · `combat_stat_multiply` 的 `0.2` 读作 **×1.2**（不是 ×0.2）✓
///   ⚠️ 我方是**整数百分比**（例：`HealAmount.Scale(heal, percent)` 做 `1 + percent/100`）
///   ⇒ **采用参考项目数值时必须 ×100**，并且要先把舍入口径定下来（见报告"待改清单" ✓）
///
/// 🔴 **`remove_if_not_active` 是【未接线字段】**：1801 条里只有 **1 条**为 `true`
///   （`skill_transform` / `combat_stat_add` / `speed_rating` / −5）⇒ 已如实导入并暴露，
///   但**没有任何消费点** ⇒ 用例把 `true` 的条数钉住，改了会红（不许静默 ✓）
/// </summary>
public sealed record BuffPrimitiveConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("stat_type")] string StatType,
    [property: JsonPropertyName("stat_sub_type")] string StatSubType,
    [property: JsonPropertyName("amount")] double Amount,
    [property: JsonPropertyName("remove_if_not_active")] bool RemoveIfNotActive,
    [property: JsonPropertyName("rule_type")] string RuleType,
    [property: JsonPropertyName("is_false_rule")] bool IsFalseRule,
    [property: JsonPropertyName("rule_data")] BuffPrimitiveRuleData RuleData,
    [property: JsonPropertyName("duration_type")] string? DurationType = null,
    [property: JsonPropertyName("duration")] int? Duration = null);

/// <summary>
/// 🔴 **A1 · buff 原语层**（`darkest/data/buff_primitives.json` 的解析 + 校验 ✓）。
///
/// WHY 需要它（实测的病灶）：我方 `quirks.json` + `trinkets.json` 引用了 **556 个去重的 buff id**，
///   而参考项目的定义池**一条都没落库** ⇒ 引用链 **482/556 悬空**（另 74 条参考项目未覆盖或已改名）
///   ⇒ 怪癖/饰品的效果**一条都不会生效** ✓ 本件就是把定义池落下来的那一半（**解析侧**）✓
///
/// 四件套（纪律 AF）里的位置：**数据** = `data/buff_primitives.json` ✓　**解析+校验** = 本文件 ✓
///   **运行时状态** = 无（buff 挂在单位上归 `IBuffLedger`）✓　**接线** = `PrimitiveActivation` ✓
///
/// 🔴 **本件只做【结构】校验，不硬编码任何词表**：
///   `stat_type` / `rule_type` / `duration_type` 的**闭集**是**数据**（实测 25 / 23 / 5）⇒
///   词表的真值在数据侧，由 `tools/dsh/extract_ref_buff_primitives.py` 每次重测、
///   由 `BuffPrimitivesTests` 钉住 ⇒ **不在 C# 里再抄一份**（两份真值必然漂移 ⚠️）✓
/// </summary>
public sealed class BuffPrimitivesConfig
{
    public const string ResPath = "res://data/buff_primitives.json";

    [JsonPropertyName("primitives")]
    public IReadOnlyList<BuffPrimitiveConfig> Primitives { get; init; } = Array.Empty<BuffPrimitiveConfig>();

    private IReadOnlySet<string>? _statTypes;

    /// <summary>
    /// 实测的 `stat_type` 闭集（25 个）—— M2 的"能接哪些原语"就以此为界 ✓
    /// **生产消费点**：`PrimitiveActivation.Validate`（加载即校验我方待接清单全部是真名字 ✓）
    /// </summary>
    [JsonIgnore]
    public IReadOnlySet<string> StatTypes
        => _statTypes ??= new HashSet<string>(Primitives.Select(p => p.StatType), StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static BuffPrimitivesConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        BuffPrimitivesConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<BuffPrimitivesConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    /// <summary>只做**结构**校验（词表是数据，不在这里）✓</summary>
    private static void Validate(BuffPrimitivesConfig cfg)
    {
        if (cfg.Primitives.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: primitives 为空。");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (BuffPrimitiveConfig p in cfg.Primitives)
        {
            if (string.IsNullOrWhiteSpace(p.Id))
            {
                throw new InvalidDataException($"{ResPath}: 存在缺失 id 的原语。");
            }

            if (!seen.Add(p.Id))
            {
                throw new InvalidDataException($"{ResPath}: 原语 id \"{p.Id}\" 重复（引用会歧义）✓");
            }

            if (string.IsNullOrWhiteSpace(p.StatType) || string.IsNullOrWhiteSpace(p.RuleType))
            {
                throw new InvalidDataException($"{ResPath}: \"{p.Id}\" 的 stat_type / rule_type 必填。");
            }

            if (p.RuleData is null)
            {
                throw new InvalidDataException($"{ResPath}: \"{p.Id}\" 缺 rule_data（其余规则无从判定）✓");
            }

            // 🔴 实测的结构不变量：**duration_type 与 duration 同生同死**（63 条都有 / 1738 条都没有）
            //    ⇒ 只出现一半 = 数据坏了，必须当场报，不能靠"缺省当 0"糊过去 ✓
            if ((p.DurationType is null) != (p.Duration is null))
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{p.Id}\" 的 duration_type / duration 必须同时出现或同时缺失。");
            }

            if (p.Duration is <= 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{p.Id}\" duration = {p.Duration} 必须 > 0。");
            }
        }
    }
}
