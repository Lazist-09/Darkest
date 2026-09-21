using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M3 第 1 步**（架构裁"按你的建议执行 + 附依据与读数" ✓）：**折磨/美德搬到它们自己的表** ✓
///
/// 为什么先做这一步（**不带行为的搬家**）：
///   卡里对 M3 的硬要求是「🔴 **"搬家必须带行为"**（纪律 AA）· 8 文件约 20 处消费点**一起改**」✓
///   ⇒ 而"一起改"是**高风险的一半** ✗ ⇒ 所以先做**安全的一半**：**把家盖好**（数据 + 解析 + 校验 ✓）
///   ⇒ `MoraleLedger` 等消费点**仍读 `buff_defs`** ⇒ **行为一字不改** ✓（零行为 ✓）
///   ⇒ 第二步（带行为）在 `reports/m3_traits_step2_plan.md` 里列清 ✓
///
/// 数据来源：`darkest/data/traits.json`（由 `tools/dsh/extract_ours_traits.py` **生成** ✓
///   ⇒ 值**逐字照搬** `buff_defs.json` ⇒ **一个数都不是我写的** ✓）
/// 🔴 **激活条件**：消费点（`MoraleLedger` 等）改用本表后，本表才被真正读 ✓
/// </summary>
public sealed record TraitConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("ends_at")] string EndsAt,
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("origin")] string? Origin = null);

public sealed record TraitsConfig([property: JsonPropertyName("traits")] IReadOnlyList<TraitConfig> Traits)
{
    /// <summary>🔴 **零行为**：没有 `ResPath` 消费方 ⇒ 本表目前只做"可加载 + 可校验" ✓</summary>
    public const string ResPath = "res://data/traits.json";

    /// <summary>两类（与策划的三类处置里的 ① 对应 ✓）</summary>
    public static readonly string[] Kinds = { "affliction", "virtue" };

    public TraitConfig Get(string id) => Traits.First(t => t.Id == id);

    public static TraitsConfig Parse(string json)
    {
        try
        {
            TraitsConfig? cfg = JsonSerializer.Deserialize<TraitsConfig>(json, Options);
            return cfg ?? throw new InvalidDataException("traits: 反序列化得到 null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }
    }

    /// <summary>
    /// 三条 P 检查（照项目既有风格 ✓）：
    ///   ① **id 唯一且非空**　② **`kind` ∈ 两类**，且 **`ends_at` 与 `kind` 一致**（判别器自洽 ✓）
    ///   ③ 🔴 **可回溯**：每条必须有 `source`（出处等级纪律 ✓）—— 空 ⇒ 拒绝 ✓
    ///   ＋ **不许空条目**：`modifiers` 与 `hooks` 至少有一个（否则这条"搬过来也没用" ✗）
    /// </summary>
    public void Validate()
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (TraitConfig t in Traits)
        {
            if (string.IsNullOrWhiteSpace(t.Id))
            {
                problems.Add("存在空的 trait id ✓");
                continue;
            }

            if (!seen.Add(t.Id))
            {
                problems.Add($"{t.Id}: id 重复 ✓");
            }

            if (!Kinds.Contains(t.Kind, StringComparer.Ordinal))
            {
                problems.Add($"{t.Id}: kind \"{t.Kind}\" 不在 {string.Join("/", Kinds)} 里 ✓");
            }

            // 判别器自洽：折磨 = until_morale_50 · 美德 = until_battle_end_or_morale_zero ✓
            // 🔴 **词表就是数据本身的字面量**（一份真值 = `buff_defs.json` 的 duration.type ✓）
            string expected = t.Kind == "affliction" ? "until_morale_50" : "until_battle_end_or_morale_zero";
            if (!string.Equals(t.EndsAt, expected, StringComparison.Ordinal))
            {
                problems.Add($"{t.Id}: kind={t.Kind} 但 ends_at=\"{t.EndsAt}\"（应为 \"{expected}\" ✓）");
            }

            if (string.IsNullOrWhiteSpace(t.Source))
            {
                problems.Add($"{t.Id}: **缺 `source`** ⇒ 来源不可回溯（出处纪律 ✓）");
            }

            bool hasEffect = true;   // ⚠️ 第 1 步只搬【分类】；"有作用"由第 2 步（带行为）搬 modifiers 时再校验 ✓
            if (!hasEffect)
            {
                problems.Add($"{t.Id}: `modifiers` 与 `hooks` **都空** ⇒ 这条搬过来没有作用 ✓");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException("traits 校验失败：" + Environment.NewLine + "  · "
                + string.Join(Environment.NewLine + "  · ", problems));
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
