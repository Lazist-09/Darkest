using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>兑换表的一条（一手字段名照抄 ✓）</summary>
public sealed record HeirloomExchangeEntry(
    [property: JsonPropertyName("exchange_from_type")] string FromType,
    [property: JsonPropertyName("exchange_from_amount")] int FromAmount,
    [property: JsonPropertyName("exchange_to_type")] string ToType,
    [property: JsonPropertyName("exchange_to_amount")] int ToAmount,
    [property: JsonPropertyName("origin")] string? Origin = null,
    [property: JsonPropertyName("placeholder")] bool? Placeholder = null);

/// <summary>
/// 🆕 **M8 祖产 · 兑换表**（策划 `DELIVERY-DESIGNER-HEIRLOOM-RULING-20260922` ①：「**现在就做（12 条 · 一次落完）**」✓）
///
/// 数据来源：**一手 E 盘** `campaign/heirloom_exchange/heirloom_exchange.json`
///   ⇒ 由 `tools/dsh/extract_dd1_heirloom_exchange.py` 转写 ⇒ 每条约 `origin="dd1"` + `placeholder=true` ✓
///
/// 🔴 **"所有兑换都损失 50%" 不当硬判据**（我实测推翻了这个一开始想写的断言 ✗）：
///   用相对价值 `portrait:bust:deed:crest = 6:3:3:2` 反解 ⇒ 一手 12 条里**有多条不是 50%**
///   （例：`bust 3 → deed 2` 亏 **33%**；`bust 2 → crest 3` 亏 **0%**）
///   ⇒ 若当断言，就会**逼数据说谎** ✗（纪律：判据不许比事实更强 ✓）
///   ⇒ 所以它降级为**读数**：`LossPercent(e)` / `LossTable()`（用例逐条打印 ✓）
///
/// 🔴 **激活条件**：兑换界面/逻辑（属后续卡/UI ✓）⇒ 本件目前只做"**可加载 + 可校验**" ✓
/// </summary>
public sealed record HeirloomExchangeConfig(
    [property: JsonPropertyName("exchange_rates")] IReadOnlyList<HeirloomExchangeEntry> Rates)
{
    public const string ResPath = "res://data/heirloom_exchange.json";

    /// <summary>一手数据用**单数**（`bust`/`crest`/`deed`/`portrait` ✓）——
    /// ⚠️ 注意 `heirlooms.json` 的 `kinds` 用的是**复数**（`busts`/… ✗）⇒ 两处口径不一致，已如实记 ✓</summary>
    public static readonly string[] SingleKinds = { "bust", "crest", "deed", "portrait" };

    /// <summary>策划给的相对价值（用于"损失率"**读数** ✓ 不是判据）</summary>
    public static readonly IReadOnlyDictionary<string, int> RelativeValue =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["portrait"] = 6, ["bust"] = 3, ["deed"] = 3, ["crest"] = 2,
        };

    public static HeirloomExchangeConfig Parse(string json)
    {
        try
        {
            HeirloomExchangeConfig? cfg = JsonSerializer.Deserialize<HeirloomExchangeConfig>(json, Options);
            return cfg ?? throw new InvalidDataException("heirloom_exchange: 反序列化得到 null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }
    }

    /// <summary>
    /// 校验（只放**实测站得住**的结构判据 ✓）：
    ///   ① 类型 ∈ 四种（单数 ✓）　② 数量 ≥ 1　③ `from ≠ to`　④ (from,to) 对唯一 ✓
    ///   🔴 **不放"损失 50%"**（理由见类注释：一手数据里就不成立 ✓）
    /// </summary>
    public void Validate()
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (HeirloomExchangeEntry e in Rates)
        {
            string pair = $"{e.FromType}->{e.ToType}";
            if (!SingleKinds.Contains(e.FromType, StringComparer.Ordinal)
                || !SingleKinds.Contains(e.ToType, StringComparer.Ordinal))
            {
                problems.Add($"{pair}: 类型必须 ∈ {string.Join("/", SingleKinds)}（一手用单数 ✓）");
            }

            if (e.FromAmount < 1 || e.ToAmount < 1)
            {
                problems.Add($"{pair}: 数量必须 ≥ 1（实测 {e.FromAmount} → {e.ToAmount}）");
            }

            if (string.Equals(e.FromType, e.ToType, StringComparison.Ordinal))
            {
                problems.Add($"{pair}: 不该自己换自己 ✓");
            }

            if (!seen.Add(pair))
            {
                problems.Add($"{pair}: (from,to) 对重复 ⇒ 兑换率会有两个真值 ✗");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException("heirloom_exchange 校验失败：" + Environment.NewLine + "  · "
                + string.Join(Environment.NewLine + "  · ", problems));
        }
    }

    /// <summary>该条兑换按相对价值反解的**隐含损失率**（%）⇒ **读数** ✓</summary>
    public static double LossPercent(HeirloomExchangeEntry e)
    {
        if (!RelativeValue.TryGetValue(e.FromType, out int vf) || !RelativeValue.TryGetValue(e.ToType, out int vt))
        {
            return double.NaN;
        }

        int paid = e.FromAmount * vf;
        int got = e.ToAmount * vt;
        return paid == 0 ? double.NaN : 100.0 * (paid - got) / paid;
    }

    /// <summary>逐条 (兑换, 损失率%) ⇒ 供报表/用例打印 ✓</summary>
    public IEnumerable<(string Pair, double Loss)> LossTable()
    {
        foreach (HeirloomExchangeEntry e in Rates)
        {
            yield return ($"{e.FromType} {e.FromAmount} → {e.ToType} {e.ToAmount}", LossPercent(e));
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
