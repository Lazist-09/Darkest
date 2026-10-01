using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M12 · 供应（provision）条目** —— 一手 E 盘 `inventory/base.supply.inventory.items.darkest` 逐字段转写 ✓
///
/// <para>一手字段：`base_stack_limit` / `purchase_gold_value` / `sell_gold_value` ＋ 两个可选
/// （`act_out_consume_priority` / `replace_buffs`）⇒ 5 个有值字段 + 2 个可空 ⇒ 全部转写，**不丢字段** ✓</para>
/// <para>我方字段（`_field_classes` 里逐条标了归属）：`name_zh`（商店行显示）/ `desc_zh`（留空串，一手无描述 ⇒ 不编造）/
/// `raid_item`（**DD 一手全树无 raid_item 字段，已实测** ⇒ 本列 = 本项目的背包落点口径，取值封闭：food / firewood / null）✓</para>
/// </summary>
public sealed record ProvisionConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name_zh")] string NameZh,
    [property: JsonPropertyName("desc_zh")] string DescZh,
    [property: JsonPropertyName("stack_limit")] int StackLimit,
    [property: JsonPropertyName("buy_gold")] int BuyGold,
    [property: JsonPropertyName("sell_gold")] int SellGold,
    [property: JsonPropertyName("raid_item")] string? RaidItem = null,
    [property: JsonPropertyName("act_out_consume_priority")] int? ActOutConsumePriority = null,
    [property: JsonPropertyName("replace_buffs")] bool? ReplaceBuffs = null);

/// <summary>
/// 🔴 **M12 · 供应表** —— 结构与合法性（**坏数据必须当场炸，不许静默置灰**）。
///
/// <para>校验（全部 fail-fast ⇒ 抛 `InvalidDataException`）：① 空表 ② 缺 id ③ id 重复
/// ④ `stack_limit < 1` ⑤ `buy_gold < 0` / `sell_gold < 0` ⑥ `raid_item` 不在封闭取值里
/// ⑦ 缺 `name_zh`（商店行没有名字 = 玩家归因不到）✓</para>
/// <para>🔴 **为什么没有抛异常的 `Get`**：本表的消费者是**商店 UI**（id 来自数据自己给的行）与**购买路径**，
/// 购买失败必须返回**拒绝理由**（余额不足 / 库存不足 / 零价），**不许抛** ⇒ 只提供不抛的
/// <see cref="Find"/>（单一落点，UI 与内核共用同一份查找）✓</para>
/// </summary>
public sealed class ProvisionsConfig
{
    public const string ResPath = "res://data/provisions.json";

    /// <summary>raid_item 的封闭取值之一：进背包 ⇒ `ItemKind.Food` ✓</summary>
    public const string RaidItemFood = "food";

    /// <summary>raid_item 的封闭取值之一：进背包 ⇒ `ItemKind.Firewood` ✓</summary>
    public const string RaidItemFirewood = "firewood";

    [JsonPropertyName("provisions")]
    public IReadOnlyList<ProvisionConfig> Provisions { get; init; } = Array.Empty<ProvisionConfig>();

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static ProvisionsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        ProvisionsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<ProvisionsConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    private static void Validate(ProvisionsConfig cfg)
    {
        if (cfg.Provisions.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: provisions 为空。");
        }

        var byId = new Dictionary<string, ProvisionConfig>(StringComparer.Ordinal);
        foreach (ProvisionConfig p in cfg.Provisions)
        {
            if (string.IsNullOrWhiteSpace(p.Id))
            {
                throw new InvalidDataException($"{ResPath}: 存在缺失 id 的 provision。");
            }

            if (string.IsNullOrWhiteSpace(p.NameZh))
            {
                throw new InvalidDataException($"{ResPath}: \"{p.Id}\" 缺 name_zh（商店行不许没有名字）✓");
            }

            if (p.StackLimit < 1)
            {
                throw new InvalidDataException($"{ResPath}: \"{p.Id}\" 的 stack_limit = {p.StackLimit}（必须 ≥ 1）✓");
            }

            if (p.BuyGold < 0 || p.SellGold < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{p.Id}\" 的价格为负（buy={p.BuyGold} / sell={p.SellGold}）✓");
            }

            if (p.RaidItem is not null and not RaidItemFood and not RaidItemFirewood)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{p.Id}\" 的 raid_item = \"{p.RaidItem}\" 不在封闭取值（{RaidItemFood} / {RaidItemFirewood}）✓");
            }

            if (!byId.TryAdd(p.Id, p))
            {
                throw new InvalidDataException($"{ResPath}: provision id \"{p.Id}\" 重复。");
            }
        }
    }

    /// <summary>按 id 取（**不抛**）：给商店 UI 与购买路径共用（未知 id ⇒ null ⇒ 调用方如实拒绝）✓</summary>
    public ProvisionConfig? Find(string id)
        => string.IsNullOrEmpty(id) ? null : Provisions.FirstOrDefault(p => p.Id == id);

    /// <summary>读数：条目数（供状态行 / 报告；**不影响行为** ✓）。</summary>
    public int Count => Provisions.Count;
}
