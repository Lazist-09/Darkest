using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>事件节点的一个选项（E4）：choice 标识 + 展示标签 + 效果。</summary>
public sealed record NodeOptionConfig(
    [property: JsonPropertyName("choice")] string Choice,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("effect")] NodeEffectConfig Effect);

/// <summary>选项效果（最小版）：资源增减与士气变化（可同时存在）。</summary>
public sealed record NodeEffectConfig(
    [property: JsonPropertyName("resource")] string? Resource = null,
    [property: JsonPropertyName("delta")] int Delta = 0,
    [property: JsonPropertyName("morale")] int Morale = 0);

/// <summary>事件节点的**必然代价**（#272）：无论选哪个选项都要付 —— 治"两个选项都无代价 ⇒ 二选一仍免费"。</summary>
public sealed record NodeCostConfig(
    [property: JsonPropertyName("morale")] int Morale);

/// <summary>节点（阶段一：battle / event；elite 属阶段二，出现即报错）。</summary>
public sealed record ExpeditionNodeConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("cost")] NodeCostConfig? Cost,
    [property: JsonPropertyName("options")] IReadOnlyList<NodeOptionConfig> Options,
    [property: JsonPropertyName("source")] string? Source = null);

/// <summary>
/// `expedition_nodes.json` 根模型 + **P20 ⑤ 校验**（M7 / #240）：
/// ① 节点类型 ∈ {{battle, event}}（**`elite` 出现即报错**——属阶段二）；
/// ② **每个事件节点恰 2 个选项**（两选项强制：否则事件没有代价，退化成"免费资源点"）。
/// </summary>
public sealed record ExpeditionNodesConfig(
    [property: JsonPropertyName("nodes")] IReadOnlyList<ExpeditionNodeConfig> Nodes)
{
    public const string ResPath = "res://data/expedition_nodes.json";

    /// <summary>阶段一允许的节点类型（`elite` 不在内 → 提前启用即报错）。</summary>
    public static readonly IReadOnlyList<string> StageOneTypes = new[] { "battle", "event" };

    public static ExpeditionNodesConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        ExpeditionNodesConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<ExpeditionNodesConfig>(json, new JsonSerializerOptions
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

    private static void Validate(ExpeditionNodesConfig cfg)
    {
        if (cfg.Nodes is null || cfg.Nodes.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: nodes 为空（P20 ⑤）。");
        }

        var ids = new HashSet<string>();
        foreach (ExpeditionNodeConfig n in cfg.Nodes)
        {
            if (!ids.Add(n.Id))
            {
                throw new InvalidDataException($"{ResPath}: 节点 id \"{n.Id}\" 重复（P20 ⑤）。");
            }

            if (n.Type == "elite")
            {
                throw new InvalidDataException(
                    $"{ResPath}: 节点 \"{n.Id}\" 使用了 elite —— **阶段二才允许**，最小版出现即报错（P20 ⑤）。");
            }

            if (!StageOneTypes.Contains(n.Type))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 节点 \"{n.Id}\" type=\"{n.Type}\" 非法（阶段一仅 "
                    + $"{string.Join(" / ", StageOneTypes)}，P20 ⑤）。");
            }

            if (n.Type == "event" && (n.Options is null || n.Options.Count != 2))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 事件节点 \"{n.Id}\" 必须**恰 2 个选项**（实际 {n.Options?.Count ?? 0}，P20 ⑤：二选一强制）。");
            }

            // 🔴 P20 ⑤（#272）：事件节点**必须声明必然代价** —— 只"不允许跳过"不够：
            // 两个选项都无代价时二选一依然免费（实测保守"全事件"路线 = 100% 完成 + 0 补给 = 零损耗通道）。
            if (n.Type == "event" && n.Cost is null)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 事件节点 \"{n.Id}\" **必须声明 cost（必然代价）**（P20 ⑤ / #272：否则二选一免费）。");
            }
        }
    }

    public ExpeditionNodeConfig Get(string id)
        => Nodes.FirstOrDefault(n => n.Id == id)
           ?? throw new InvalidDataException($"{ResPath}: 引用了不存在的节点 \"{id}\"。");
}
