using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>疾病惩罚（M8.2：**属性惩罚 = 长线损耗**；每项可测、允许为 0 但不能全 0）。</summary>
public sealed record DiseasePenalty(
    [property: JsonPropertyName("hp_delta")] int HpDelta = 0,
    [property: JsonPropertyName("attack_delta")] int AttackDelta = 0,
    [property: JsonPropertyName("morale_delta")] int MoraleDelta = 0)
{
    public bool IsNone => HpDelta == 0 && AttackDelta == 0 && MoraleDelta == 0;
}

/// <summary>一种疾病：id / 名字 / 惩罚 / **每趟结束的获得概率**。</summary>
public sealed record DiseaseDef(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("penalty")] DiseasePenalty Penalty,
    [property: JsonPropertyName("contract_chance_per_run")] double ContractChancePerRun);

/// <summary>Sanitarium 的一项服务：金钱 + 传家宝成本。</summary>
public sealed record SanitariumService(
    [property: JsonPropertyName("gold")] int Gold,
    [property: JsonPropertyName("heirlooms")] Dictionary<string, int> Heirlooms);

/// <summary>
/// `sanitarium.json` 根模型 + **P24 校验（M8.2）**：
/// ① 疾病 ≥ 3 且**惩罚可测**（不得三项全 0）· 概率 ∈ (0, 1]；
/// ② **三种服务齐全**（`cure_disease` / `remove_negative_trait` / `lock_positive_trait`）；
/// ③ 🔴 **每项服务都必须消耗传家宝**（它是 M8.1 传家宝的稳定出口 —— 否则"长线资源"没有去路）；
/// ④ 🔴 **特质清除不得把特质降到 0 条**（与 7.7「每人 2~3 条」一致）—— 由内核在使用时保证，这里做常量声明。
/// </summary>
public sealed record SanitariumConfig(
    [property: JsonPropertyName("diseases")] IReadOnlyList<DiseaseDef> Diseases,
    [property: JsonPropertyName("services")] Dictionary<string, SanitariumService> Services)
{
    public const string ResPath = "res://data/sanitarium.json";

    /// <summary>特质条数下限（清除后不得低于它；与 `RosterConfig` 的 7.7 规则一致）。</summary>
    public const int MinTraitsPerHero = 1;

    /// <summary>必须齐全的三项服务。</summary>
    public static readonly IReadOnlyList<string> RequiredServices = new[]
    {
        "cure_disease", "remove_negative_trait", "lock_positive_trait",
    };

    public static SanitariumConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        SanitariumConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<SanitariumConfig>(json, new JsonSerializerOptions
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

    public DiseaseDef Disease(string id) => Diseases.FirstOrDefault(d => d.Id == id)
        ?? throw new InvalidDataException($"{ResPath}: 未知疾病 \"{id}\"（P24 ①）。");

    public SanitariumService Service(string name) => Services.TryGetValue(name, out SanitariumService? s) && s is not null
        ? s
        : throw new InvalidDataException($"{ResPath}: 未知服务 \"{name}\"（P24 ②）。");

    private static void Validate(SanitariumConfig cfg)
    {
        if (cfg.Diseases is null || cfg.Diseases.Count < 3)
        {
            throw new InvalidDataException($"{ResPath}: diseases 至少 3 种（P24 ①）。");
        }

        var ids = new HashSet<string>();
        foreach (DiseaseDef d in cfg.Diseases)
        {
            if (string.IsNullOrWhiteSpace(d.Id) || !ids.Add(d.Id) || string.IsNullOrWhiteSpace(d.Name))
            {
                throw new InvalidDataException($"{ResPath}: 疾病 id/名字 缺失或重复（P24 ①）。");
            }

            if (d.Penalty is null || d.Penalty.IsNone)
            {
                throw new InvalidDataException($"{ResPath}: 疾病 \"{d.Id}\" 的惩罚不得三项全 0（P24 ①：惩罚必须可测）。");
            }

            if (d.ContractChancePerRun is <= 0 or > 1)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 疾病 \"{d.Id}\" 的 contract_chance_per_run 必须 ∈ (0, 1]（P24 ①）。");
            }
        }

        foreach (string name in RequiredServices)
        {
            SanitariumService s = cfg.Service(name);
            if (s.Gold <= 0)
            {
                throw new InvalidDataException($"{ResPath}: 服务 \"{name}\" 的金钱成本必须 > 0（P24 ②）。");
            }

            if (s.Heirlooms is null || s.Heirlooms.Count == 0 || s.Heirlooms.Values.Any(v => v <= 0))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 服务 \"{name}\" **必须消耗传家宝**（P24 ③：它是 M8.1 传家宝的稳定出口）。");
            }
        }
    }
}
