using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>比例自检（7.1：先定比例）—— 一趟收入应落在 3~6 单位（⇒ 一趟能减 1~2 次压）。</summary>
public sealed record EconomyRatioCheck(
    [property: JsonPropertyName("battles_per_run")] int BattlesPerRun,
    [property: JsonPropertyName("expected_run_income_low")] int ExpectedRunIncomeLow,
    [property: JsonPropertyName("expected_run_income_high")] int ExpectedRunIncomeHigh);

/// <summary>减压建筑（M8.0 ④ / `#283` 7.3）：**同价同效、风险不同** —— 差别只在 `penalty_chance`（副作用触发率）。</summary>
public sealed record StressReliefBuildingConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("cost")] int Cost,
    [property: JsonPropertyName("morale_restore")] int MoraleRestore,
    [property: JsonPropertyName("next_run_penalty")] int NextRunPenalty,
    [property: JsonPropertyName("penalty_chance")] double PenaltyChance,
    [property: JsonPropertyName("style")] string? Style = null);

/// <summary>减压配置（Tavern / Abbey 两栋）。</summary>
public sealed record StressReliefConfig(
    [property: JsonPropertyName("morale_restore")] int MoraleRestore,
    [property: JsonPropertyName("next_run_penalty")] int NextRunPenalty,
    [property: JsonPropertyName("buildings")] IReadOnlyList<StressReliefBuildingConfig> Buildings);

/// <summary>
/// `economy.json` 根模型 + **P22 ④⑤ 校验（M8.0，`#284`）**：
/// 🔴 **④ 金钱来源必须与【光照档 + 战斗数】挂钩**（冒险要有可跨趟积累的回报）；
/// 🔴 **⑤ Tavern 与 Abbey 必须【同价同效、风险不同】** —— 否则"选风格"退化成"选更优"。
/// </summary>
public sealed record EconomyConfig(
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("battle_reward")] int BattleReward,
    [property: JsonPropertyName("light_tier_bonus")] Dictionary<string, int> LightTierBonus,
    [property: JsonPropertyName("event_reward")] int EventReward,
    [property: JsonPropertyName("stress_relief_cost")] int StressReliefCost,
    [property: JsonPropertyName("ratio_check")] EconomyRatioCheck RatioCheck,
    [property: JsonPropertyName("stress_relief")] StressReliefConfig? StressRelief = null)
{
    public const string ResPath = "res://data/economy.json";

    /// <summary>光照档顺序（由亮到暗；与 light.loot / light.effects 同序）。</summary>
    public static readonly IReadOnlyList<string> TierOrder = new[] { "radiant", "dim", "shadowy", "dark", "black" };

    public static EconomyConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        EconomyConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<EconomyConfig>(json, new JsonSerializerOptions
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

    /// <summary>按光照档给的单场金钱（战斗数挂钩由调用方每场调用一次实现）。</summary>
    public int RewardFor(string tierId) => BattleReward + LightTierBonus.GetValueOrDefault(tierId);

    private static void Validate(EconomyConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.Currency))
        {
            throw new InvalidDataException($"{ResPath}: currency 不得为空（P22 ④）。");
        }

        // ① 与【战斗数】挂钩
        if (cfg.BattleReward < 1)
        {
            throw new InvalidDataException(
                $"{ResPath}: battle_reward 必须 ≥ 1 —— 金钱来源必须与【战斗数】挂钩（P22 ④ / #283 硬要求①）。");
        }

        // ② 与【光照档】挂钩（越暗越多；且必须有实际差异）
        if (cfg.LightTierBonus is null || cfg.LightTierBonus.Count == 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: light_tier_bonus 不得为空 —— 金钱来源必须与【光照档】挂钩（P22 ④ / #283 硬要求①）。");
        }

        int last = -1;
        foreach (string id in TierOrder)
        {
            if (!cfg.LightTierBonus.TryGetValue(id, out int bonus))
            {
                throw new InvalidDataException($"{ResPath}: light_tier_bonus 缺档 \"{id}\"（P22 ④）。");
            }

            if (bonus < 0)
            {
                throw new InvalidDataException($"{ResPath}: light_tier_bonus[\"{id}\"] 必须 ≥ 0（P22 ④）。");
            }

            if (bonus < last)
            {
                throw new InvalidDataException(
                    $"{ResPath}: light_tier_bonus 必须随变暗**单调不减**（{id}；P22 ④：越冒险越该有回报）。");
            }

            last = bonus;
        }

        if (cfg.LightTierBonus.Values.Sum() < 1)
        {
            throw new InvalidDataException(
                $"{ResPath}: light_tier_bonus 总和必须 ≥ 1 —— 否则「与光照档挂钩」只是名义上的（P22 ④ / 红线 19）。");
        }

        // ③ 比例自检（7.1：一场战斗 1 单位 / 一趟 3~6 / 一次减压 3）
        if (cfg.StressReliefCost <= 0)
        {
            throw new InvalidDataException($"{ResPath}: stress_relief_cost 必须 > 0（P22 ④ / #283 7.1）。");
        }

        if (cfg.RatioCheck is null || cfg.RatioCheck.BattlesPerRun < 1
            || cfg.RatioCheck.ExpectedRunIncomeLow < 1
            || cfg.RatioCheck.ExpectedRunIncomeHigh < cfg.RatioCheck.ExpectedRunIncomeLow)
        {
            throw new InvalidDataException($"{ResPath}: ratio_check 不合法（P22 ④ / #283 7.1）。");
        }

        // 🔴 P22 ⑤（#283 硬要求②）：两栋减压建筑必须【同价同效、风险不同】
        if (cfg.StressRelief is not null)
        {
            IReadOnlyList<StressReliefBuildingConfig> b = cfg.StressRelief.Buildings;
            if (b is null || b.Count != 2)
            {
                throw new InvalidDataException($"{ResPath}: stress_relief.buildings 必须**恰 2 栋**（Tavern / Abbey；P22 ⑤）。");
            }

            if (b[0].Cost != b[1].Cost)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 两栋必须**同价**（{b[0].Id}={b[0].Cost} vs {b[1].Id}={b[1].Cost}；P22 ⑤：否则变成「选更优」）。");
            }

            if (b[0].MoraleRestore != b[1].MoraleRestore || b[0].NextRunPenalty != b[1].NextRunPenalty)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 两栋必须**同效**（恢复量与下趟惩罚都相同；P22 ⑤）。");
            }

            if (Math.Abs(b[0].PenaltyChance - b[1].PenaltyChance) < 1e-9)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 两栋必须有**不同的副作用风险**（penalty_chance 相同 ⇒ 两栋等价 ⇒ 选风格不成立；P22 ⑤）。");
            }

            foreach (StressReliefBuildingConfig x in b)
            {
                if (x.PenaltyChance is < 0 or > 1)
                {
                    throw new InvalidDataException($"{ResPath}: {x.Id}.penalty_chance 必须 ∈ [0,1]（P22 ⑤）。");
                }

                if (x.Cost != cfg.StressReliefCost)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: {x.Id}.cost 必须等于 stress_relief_cost（{cfg.StressReliefCost}；P22 ⑤ / 7.1）。");
                }
            }
        }
    }

    /// <summary>按 id 取减压建筑（不存在即报错，不给默认值）。</summary>
    public StressReliefBuildingConfig Building(string id)
    {
        if (StressRelief is null)
        {
            throw new InvalidDataException($"{ResPath}: 未配置 stress_relief（P22 ⑤）。");
        }

        foreach (StressReliefBuildingConfig b in StressRelief.Buildings)
        {
            if (b.Id == id)
            {
                return b;
            }
        }

        throw new InvalidDataException($"{ResPath}: 未知减压建筑 \"{id}\"（P22 ⑤）。");
    }
}
