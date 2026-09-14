using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>一条解锁阈值：达到【已完成出征数】或【已打赢场数】⇒ 解锁 `unlocks` 里的 id。</summary>
public sealed record UnlockEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("required_runs_finished")] int RequiredRunsFinished,
    [property: JsonPropertyName("required_battles_won")] int RequiredBattlesWon,
    [property: JsonPropertyName("unlocks")] IReadOnlyList<string> Unlocks,
    [property: JsonPropertyName("source")] string? Source = null);

public sealed record UnlocksHeader(
    [property: JsonPropertyName("version")] int Version = 1);

/// <summary>
/// 🔴 **解锁阈值表**（`O-86` / 合并包片 D）—— 形态参照真机
/// `generated_dungeons[{ id, required_number_of_quests_finished }]`：
/// **按"已完成出征数（或已打赢场数）"逐步解锁内容**。
///
/// ⚠️ **当前状态（如实标注，红线 21）**：**只有形态，没有消费点** ——
/// **"解锁什么"属内容**（`O-86` 待策划）⇒ 出厂数据只放**一条占位示例**；
/// 因此本类只做【解析 + P27 校验】，**内核不据此改变任何行为**（`blueprint` 登记的形态 ✓）。
///
/// **P27 校验**：① 阈值 `≥ 0` 且**至少给一个阈值**；② **不得出现两个条目解锁同一 id**（否则解锁语义二义）。
/// 内核层：**零 Godot** ✓（组合根喂字符串）
/// </summary>
public sealed record UnlocksConfig(
    [property: JsonPropertyName("config")] UnlocksHeader? Config,
    [property: JsonPropertyName("unlocks")] IReadOnlyList<UnlockEntry> Unlocks)
{
    public const string ResPath = "res://data/unlocks.json";

    public static UnlocksConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        UnlocksConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<UnlocksConfig>(json, new JsonSerializerOptions
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

        Validate(cfg);
        return cfg;
    }

    /// <summary>**P27 校验**（合并包片 D 的验收）。</summary>
    public static void Validate(UnlocksConfig cfg)
    {
        if (cfg.Unlocks is null)
        {
            throw new InvalidDataException($"{ResPath}: 缺少 `unlocks` 数组（形态要求；可为空但必须存在）。");
        }

        var seen = new Dictionary<string, string>(StringComparer.Ordinal); // unlocked id → 归属条目
        foreach (UnlockEntry e in cfg.Unlocks)
        {
            if (e.RequiredRunsFinished < 0 || e.RequiredBattlesWon < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{e.Id}\" 的阈值不得为负（runs={e.RequiredRunsFinished} ／ wins={e.RequiredBattlesWon}）（P27 ①）。");
            }

            if (e.RequiredRunsFinished == 0 && e.RequiredBattlesWon == 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{e.Id}\" 至少要给一个阈值（runs 或 wins ＞ 0）（P27 ①）。");
            }

            if (e.Unlocks is null || e.Unlocks.Count == 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{e.Id}\" 的 `unlocks` 为空（无事可解锁）（P27 ①）。");
            }

            foreach (string target in e.Unlocks)
            {
                if (seen.TryGetValue(target, out string? owner))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: id \"{target}\" 被【两个条目】解锁（\"{owner}\" 与 \"{e.Id}\"）—— " +
                        "解锁语义二义（P27 ②）。");
                }

                seen[target] = e.Id;
            }
        }
    }
}
