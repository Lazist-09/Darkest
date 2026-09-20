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
/// 驿站马车（M8.0 ⑤ / `#283` 硬要求③）：**招募免费** + **补的人不比老的强**（新兵 level 1 / morale 50）。
///
/// 🔴 **M7 · 名册/招募对齐（策划 `#423`）** —— **上限的【单一来源】就是这里（马车）** ✓：
///   · `roster_cap_by_level` = **9 → 12 → 16 → 20 → 24 → 28**（6 值 = 未升级 + `stage_coach.rostersize` 的 a~e 五档 ✓）
///     ⇒ 与 `darkest/data/buildings.json` 里 `stage_coach.rostersize` 的 **5 档**一一对应 ✓
///   · `num_recruits_by_level` = **2 ~ 7 人**（端点由策划给；中间为等步长 ramp ⇒ 若要别的 ramp 请裁 ✓）
///   · `upgraded_recruit_chances_pct` = **18.75 / 12.5 / 6.25**（对应 `stage_coach.upgraded_recruits` 的 a/b/c ✓）
///   🔴 **`max_roster` 必须等于曲线末值**（校验断言）—— 否则又是"两处真值" ✓
/// </summary>
public sealed record StagecoachConfig(
    [property: JsonPropertyName("recruit_cost")] int RecruitCost,
    [property: JsonPropertyName("rookie_level")] int RookieLevel,
    [property: JsonPropertyName("rookie_morale")] int RookieMorale,
    [property: JsonPropertyName("max_roster")] int MaxRoster,
    // 🆕 M7（策划 #423）：三条曲线 —— **上限的单一来源** ✓
    [property: JsonPropertyName("roster_cap_by_level")] IReadOnlyList<int>? RosterCapByLevel = null,
    [property: JsonPropertyName("num_recruits_by_level")] IReadOnlyList<int>? NumRecruitsByLevel = null,
    [property: JsonPropertyName("upgraded_recruit_chances_pct")] IReadOnlyList<double>? UpgradedRecruitChancesPct = null)
{
    /// <summary>🔴 上限曲线末值（= 最终硬上限）；未配曲线 ⇒ 退回 `MaxRoster`（老数据不受影响 ✓）</summary>
    public int CapCeiling => RosterCapByLevel is { Count: > 0 } c ? c[^1] : MaxRoster;
}

/// <summary>
/// `economy.json` 根模型 + **P22 ④⑤⑥ 校验（M8.0，`#284`）**：
/// 🔴 **④ 金钱来源必须与【光照档 + 战斗数】挂钩**；
/// 🔴 **⑤ Tavern 与 Abbey 必须【同价同效、风险不同】**；
/// 🔴 **⑥ 招募必须免费，且新兵 `level == 1`（不比老的强）**。
/// </summary>
public sealed record EconomyConfig(
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("battle_reward")] int BattleReward,
    [property: JsonPropertyName("light_tier_bonus")] Dictionary<string, int> LightTierBonus,
    [property: JsonPropertyName("event_reward")] int EventReward,
    [property: JsonPropertyName("stress_relief_cost")] int StressReliefCost,
    [property: JsonPropertyName("ratio_check")] EconomyRatioCheck RatioCheck,
    [property: JsonPropertyName("stress_relief")] StressReliefConfig? StressRelief = null,
    [property: JsonPropertyName("stagecoach")] StagecoachConfig? Stagecoach = null)
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

        // 🔴 P22 ⑥（#283 硬要求③）：招募**免费**且**新兵 level == 1**（补的人不比老的强）
        if (cfg.Stagecoach is null)
        {
            throw new InvalidDataException($"{ResPath}: 缺 stagecoach（招募；P22 ⑥）。");
        }

        if (cfg.Stagecoach.RecruitCost != 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: stagecoach.recruit_cost 必须 = 0（**招募免费**；P22 ⑥ / DD wiki：entirely free of charge）。");
        }

        if (cfg.Stagecoach.RookieLevel != 1)
        {
            throw new InvalidDataException(
                $"{ResPath}: stagecoach.rookie_level 必须 = 1（**补的人不比老的强**；P22 ⑥ / #283 硬要求③）。");
        }

        if (cfg.Stagecoach.RookieMorale != RosterConfig.RookieMorale)
        {
            throw new InvalidDataException(
                $"{ResPath}: stagecoach.rookie_morale 必须 = {RosterConfig.RookieMorale}（新兵入场士气基准；P22 ⑥⑦）。");
        }

        if (cfg.Stagecoach.MaxRoster <= 0)
        {
            throw new InvalidDataException($"{ResPath}: stagecoach.max_roster 必须 > 0（P22 ⑥）。");
        }

        // 🆕 **M7（策划 `#423`）**：三条曲线的**结构校验**（数值本身由契约/用例锁 ✓）
        if (cfg.Stagecoach.RosterCapByLevel is { } caps)
        {
            if (caps.Count < 2)
            {
                throw new InvalidDataException($"{ResPath}: roster_cap_by_level 至少 2 个值（未升级 + ≥1 档）✓");
            }

            if (caps[0] <= 0)
            {
                throw new InvalidDataException($"{ResPath}: roster_cap_by_level[0] 必须 > 0 ✓");
            }

            for (int i = 1; i < caps.Count; i++)
            {
                if (caps[i] < caps[i - 1])
                {
                    throw new InvalidDataException(
                        $"{ResPath}: roster_cap_by_level 必须**非递减**（{caps[i - 1]} → {caps[i]}）✓");
                }
            }

            // 🔴 **单一来源的中间态规则**（M7 分两步，别越界）：
            //   ① 本步：**只落曲线**，`max_roster` 仍是**当前生效**的硬上限（不许动 ⇒ 不然就是我这次犯的错）✓
            //   ② 接线步：由 `Roster` 改读曲线、并把 `max_roster` 提到曲线末值（28）⇒ 那时二者相等 ✓
            //   ⇒ 所以此处的判据是【**不许倒挂**】：曲线末值必须 ≥ max_roster（否则曲线一接上就要**降**上限）✓
            if (caps[^1] < cfg.Stagecoach.MaxRoster)
            {
                throw new InvalidDataException(
                    $"{ResPath}: roster_cap_by_level 末值 {caps[^1]} 不得小于 max_roster {cfg.Stagecoach.MaxRoster}（不许倒挂）✓");
            }
        }

        if (cfg.Stagecoach.NumRecruitsByLevel is { } rec)
        {
            if (rec.Count < 2 || rec.Any(v => v <= 0))
            {
                throw new InvalidDataException($"{ResPath}: num_recruits_by_level 至少 2 个值且全部 > 0 ✓");
            }

            for (int i = 1; i < rec.Count; i++)
            {
                if (rec[i] < rec[i - 1])
                {
                    throw new InvalidDataException(
                        $"{ResPath}: num_recruits_by_level 必须**非递减**（{rec[i - 1]} → {rec[i]}）✓");
                }
            }
        }

        if (cfg.Stagecoach.UpgradedRecruitChancesPct is { } up)
        {
            if (up.Any(v => v < 0 || v > 100))
            {
                throw new InvalidDataException($"{ResPath}: upgraded_recruit_chances_pct 每项必须 ∈ [0,100] ✓");
            }
        }
    }

    /// <summary>驿站马车配置（缺即报错，不给默认值）。</summary>
    public StagecoachConfig Coach => Stagecoach
        ?? throw new InvalidDataException($"{ResPath}: 未配置 stagecoach（P22 ⑥）。");

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
