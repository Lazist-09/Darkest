using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>空手结果表的一行（`curio.md` §4）：累计概率 + 效果 + **描述文本**（V6：不是"获得 2 个口粮"）。</summary>
public sealed record CurioBareResultConfig(
    [property: JsonPropertyName("chance")] int Chance,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("text")] string Text);

/// <summary>
/// **道具 ⇒ 确定结果**（`curio.md` §1.2：Curio 的灵魂 = "正确道具 ⇒ 100% 确定的好结果"；
/// 反向也成立 ⇒ "**用错道具**也是【确定的坏结果】"，例：骸骨堆 ＋ 口粮 ⇒ 全队 −10 士气）。
/// 🔴 **道具路径【不掷骰】**（`#313` ⑤）⇒ 不写 `RngDraw`。
/// </summary>
public sealed record CurioItemResultConfig(
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("extra_kind")] string? ExtraKind = null,
    [property: JsonPropertyName("extra_amount")] int ExtraAmount = 0);

/// <summary>一个 Curio（可交互物体）：`type` 固定 `curio`；`bare_hands` = 空手结果表（= `#272` 的机制）✓</summary>
public sealed record CurioConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("curio_type")] string CurioType,
    [property: JsonPropertyName("bare_hands")] IReadOnlyList<CurioBareResultConfig> BareHands,
    [property: JsonPropertyName("item_results")] IReadOnlyList<CurioItemResultConfig> ItemResults);

/// <summary>
/// `data/curios.json` 根模型 + **加载级校验**（红线 21：写错就炸，不静默失效）：
/// ① `type` 只能是 `curio`（`note` 为文档行，跳过）；
/// ② **`bare_hands` 的 chance 之和必须 = 100**（否则空手结果表有洞 ⇒ 掷到没定义的分支）；
/// ③ **每个 Curio 都必须有【空手】这条路**（`#272`：代价由空手概率达成）；
/// ④ `kind` ∈ 已实现清单（否则加载即报错 —— 与 `BuffDefsConfig.ConsumedEffectNames` 同一做法）。
/// </summary>
public sealed record CuriosConfig(
    [property: JsonPropertyName("curios")] IReadOnlyList<CurioConfig> Curios)
{
    public const string ResPath = "res://data/curios.json";

    /// <summary>🔴 **已实现**的效果种类（新增 kind ⇒ 必须同时接上消费通道 + 登记到这里）。</summary>
    public static readonly IReadOnlySet<string> ConsumedKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "none",            // 空手什么都没出（仍要写 RngDraw ⇒ 概率是真的）
        "food",            // 口粮 +N
        "firewood",        // 柴火 +N
        "gold",            // 金钱 +N
        "morale_team",     // 全队士气 +N（可负）
        "light",           // 光照 +N（可负）—— 由 `ExpeditionFlow` 施加（它持有 LightMeter）
        "scout",           // 侦察（揭示相邻）—— 由 `ExpeditionFlow` 施加
        "damage_buff",     // 本趟 +N% 伤害（到扎营）—— 跨场 buff（`until_next_recovery`）＋扎营清 ✓ 已接线
    };

    /// <summary>
    /// 阶段二（**已登记但尚未接线**）：出现时**允许加载**，但内核必须**显式拒绝执行**（不静默）——
    /// 🔴 与 `ConsumedKinds` **互斥**（我踩过一次：同一条目同时在两边 ⇒ 计数虚高，用例当场抓住）。
    /// </summary>
    public static readonly IReadOnlySet<string> DeferredKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "trait_positive",  // 随机正面特质 —— 待接 `TraitMutation`
        "disease_one",     // 一人患病 —— 待接"跑图中患病"（现在只有【回城结算】会患病）
    };

    /// <summary>只含真正的 Curio（跳过 `type:"note"` 的文档行）。</summary>
    public IReadOnlyList<CurioConfig> RealCurios =>
        Curios.Where(c => c.Type == "curio").ToArray();

    public CurioConfig? Get(string id) => Curios.FirstOrDefault(c => c.Id == id);

    public static CuriosConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        CuriosConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<CuriosConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new InvalidDataException($"{ResPath}: 反序列化结果为 null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 解析失败 —— {ex.Message}", ex);
        }

        foreach (CurioConfig c in cfg.RealCurios)
        {
            if (c.BareHands is null || c.BareHands.Count == 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{c.Id}\" 没有 `bare_hands` ⇒ 违反 `#272`（空手这条路必须有）⇒ 拒绝加载。");
            }

            int sum = c.BareHands.Sum(b => b.Chance);
            if (sum != 100)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{c.Id}\" 的 `bare_hands` 概率之和 = {sum}（必须恰 100）⇒ 否则掷到未定义分支。");
            }

            foreach (CurioBareResultConfig b in c.BareHands)
            {
                if (!ConsumedKinds.Contains(b.Kind) && !DeferredKinds.Contains(b.Kind))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{c.Id}\" 的空手 kind \"{b.Kind}\" 不在【已实现/已登记】清单里 —— " +
                        "新增 kind 必须同时接上消费通道（红线 21：写了但没接上）。");
                }
            }

            foreach (CurioItemResultConfig r in c.ItemResults ?? Array.Empty<CurioItemResultConfig>())
            {
                if (!ConsumedKinds.Contains(r.Kind) && !DeferredKinds.Contains(r.Kind))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{c.Id}\" 的道具结果 kind \"{r.Kind}\" 不在【已实现/已登记】清单里（红线 21）。");
                }
            }
        }

        return cfg;
    }
}
