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

/// <summary>
/// `economy.json` 根模型 + **P22 ④ 校验（M8.0，`#284`）**：
/// 🔴 **金钱来源必须与【光照档 + 战斗数】挂钩** —— 那才让"冒险"第一次有**可跨趟积累**的回报。
/// 可测定义（红线 19）：① `battle_reward ≥ 1`（与战斗数挂钩）；
/// ② `light_tier_bonus` **随变暗单调不减**且 **总和 ≥ 1**（与光照档挂钩，越暗越多）；
/// ③ `stress_relief_cost > 0` 且比例自检 = 一场战斗 1 单位 / 一趟 3~6 / 一次减压 3。
/// </summary>
public sealed record EconomyConfig(
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("battle_reward")] int BattleReward,
    [property: JsonPropertyName("light_tier_bonus")] Dictionary<string, int> LightTierBonus,
    [property: JsonPropertyName("event_reward")] int EventReward,
    [property: JsonPropertyName("stress_relief_cost")] int StressReliefCost,
    [property: JsonPropertyName("ratio_check")] EconomyRatioCheck RatioCheck)
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
    }
}
