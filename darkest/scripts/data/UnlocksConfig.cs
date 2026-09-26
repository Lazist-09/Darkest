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
    // 🔴 数字外置（P29）：`roster_base_cap` 去默认值（数据里已有）⇒ **必需参数在前**（C# 规则：可选参数必须都在最后）
    [property: JsonPropertyName("roster_base_cap")] int RosterBaseCap,
    [property: JsonPropertyName("version")] int Version = 1,
    [property: JsonPropertyName("base_curios")] IReadOnlyList<string>? BaseCurios = null);

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

    /// <summary>
    /// 🔴 合法的解锁 id 命名空间前缀（**唯一真相** —— 报错清单由它插值而来）✓
    /// ⚠️ 加一支分支时，**必须同时加到这里**：`UnlocksConfigTests` 有一条
    ///   `NamespacePrefixes_CoverEveryBranch` 断言"清单 ⇔ 实际分支"（防漂移复发）✓
    /// </summary>
    public static readonly IReadOnlyList<string> NamespacePrefixes = new[]
    {
        "building:", "curio:", "roster_cap_delta:", "roster_cap:",
    };

    /// <summary>起手【名册可用上限】（`config.roster_base_cap`；**硬上限**是 `roster.cap = 12`，见 C1）。</summary>
    /// 🔴 数字外置（P29）：**不再有 `?? 8` 兜底** —— 缺键由 `DataPresence.RequireKeys` 在 Parse 里拦下 ✓
    public int RosterBaseCap => Config?.RosterBaseCap ?? 0;

    public static UnlocksConfig Parse(string json, IReadOnlySet<string>? buildingIds = null,
        IReadOnlySet<string>? curioIds = null, int rosterHardCap = 12)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        // 🔴 数字外置（P29）：`roster_base_cap` 必须显式存在（原记录默认值 8 + 属性里的 `?? 8` 双重兜底都已去掉）✓
        DataPresence.RequireKeys(ResPath, json, "roster_base_cap");

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

        Validate(cfg, buildingIds, curioIds, rosterHardCap);
        return cfg;
    }

    /// <summary>**P27 校验**（合并包片 D 的验收）+ 🔴 **id 命名空间校验**（`#316`③ 的三种解锁对象）。</summary>
    public static void Validate(UnlocksConfig cfg, IReadOnlySet<string>? buildingIds = null,
        IReadOnlySet<string>? curioIds = null, int rosterHardCap = 12)
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

                // 🔴 命名空间校验（**引用的对象必须存在** —— 与 P26 同一条纪律）：
                //    building:<id> ／ curio:<id> ／ roster_cap_delta:<N>（**增量**，策划 #403）
                //    ／ roster_cap:<N>（**绝对值**，旧语法 · 仍兼容，见 C1）
                //    ⚠️ 两者语义不同：delta 加在起手 8 之上，绝对值直接给上限 ✓
                //    🔴 报错清单由 `NamespacePrefixes`【插值】而来 ⇒ **加一支即自动同步**，
                //       不再手写（教训：那处漏改正是"手写清单"造成的）✓
                if (target.StartsWith("building:", StringComparison.Ordinal))
                {
                    string id = target["building:".Length..];
                    if (buildingIds is null || !buildingIds.Contains(id))
                    {
                        throw new InvalidDataException(
                            $"{ResPath}: 解锁引用了不存在的建筑 \"{id}\"（须在建筑目录里）（P27 ④）。");
                    }
                }
                else if (target.StartsWith("curio:", StringComparison.Ordinal))
                {
                    string id = target["curio:".Length..];
                    if (curioIds is null || !curioIds.Contains(id))
                    {
                        throw new InvalidDataException(
                            $"{ResPath}: 解锁引用了不存在的 Curio \"{id}\"（须在 `curios.json` 里）（P27 ④）。");
                    }
                }
                else if (target.StartsWith("roster_cap_delta:", StringComparison.Ordinal))
                {
                    // 🆕 策划 `#403`：**增量语义**（与马车同语法 ✓）—— 值 = 增量（1..硬上限）
                    if (!int.TryParse(target["roster_cap_delta:".Length..], out int delta) || delta <= 0 || delta > rosterHardCap)
                    {
                        throw new InvalidDataException(
                            $"{ResPath}: `roster_cap_delta:` 的值必须是 1..{rosterHardCap}（增量）—— 实际 \"{target}\"。");
                    }
                }
                else if (target.StartsWith("roster_cap:", StringComparison.Ordinal))
                {
                    if (!int.TryParse(target["roster_cap:".Length..], out int cap) || cap <= 0 || cap > rosterHardCap)
                    {
                        throw new InvalidDataException(
                            $"{ResPath}: `roster_cap:` 的值必须是 1..{rosterHardCap}（**硬上限**，见 C1）—— 实际 \"{target}\"。");
                    }
                }
                else
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{target}\" 的命名空间未知（合法：{string.Join(" ／ ", NamespacePrefixes)}）（P27 ④）。");
                }
            }
        }
    }
}
