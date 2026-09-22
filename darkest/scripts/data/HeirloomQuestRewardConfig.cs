using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 🆕 **传家宝「任务奖励」通道**（策划 `DELIVERY-DESIGNER-HEIRLOOM-RULING-20260922` ② 的**步骤 ①** ✓）
///
/// 策划原话：传家宝 **不按光照档掉**，改为 **任务奖励** ⇒ 按纪律 **AY 分两步**：
///   **步骤 ①（本件）= 加通道（结构 · 零行为）** ✓
///   **步骤 ② = 删 `tier_drop` + 它的 3 条 P23 校验**（**改行为** ⇒ **另起一轮** ✓）
///
/// 数据：一手 E 盘 `campaign/quest/quest.generation.json` 的 `generation.rewards` ✓
///   （由 `tools/dsh/extract_dd1_quest_heirloom_rewards.py` 转写进 `heirlooms.json` 的 `quest_reward` ✓）
///   · `dungeon_types`：**4 个地牢都给全部 4 种** ✓（一手实测 ✓ 与策划表逐字一致 ✓）
///   · `amount_table`：**6 档难度 × 4 个任务长度**，且**只有档 1/3/5 有值** ✓
///     （每行首项 `0` = "长度 0 不存在" ✓ 也是 0 基索引的占位 ✓）
///
/// 🔴 **激活条件**：任务生成/奖励结算（后续卡 ✓）⇒ 本件目前只做"**可加载 + 可校验 + 可查表**" ✓
/// </summary>
public sealed record HeirloomQuestRewardConfig
{
    /// <summary>地牢 → 它能给的传家宝种类（一手：四个地牢都是全 4 种 ✓）</summary>
    [property: JsonPropertyName("dungeon_types")]
    public IReadOnlyDictionary<string, List<string>> DungeonTypes { get; init; } = new Dictionary<string, List<string>>();

    /// <summary>种类 → 6 档难度，每档是 4 个任务长度对应的数量（空数组 = 该档没有配 ✓）</summary>
    [property: JsonPropertyName("amount_table")]
    public IReadOnlyDictionary<string, List<List<int>>> AmountTable { get; init; } = new Dictionary<string, List<List<int>>>();

    public const string ResPath = "res://data/heirlooms.json";   // 通道挂在 heirlooms.json 的 quest_reward 下 ✓

    public static HeirloomQuestRewardConfig Parse(string heirloomsJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(heirloomsJson);
            if (!doc.RootElement.TryGetProperty("quest_reward", out JsonElement qr))
            {
                return new HeirloomQuestRewardConfig();
            }

            return qr.Deserialize<HeirloomQuestRewardConfig>(Options) ?? new HeirloomQuestRewardConfig();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: quest_reward JSON 有误 —— {ex.Message}");
        }
    }

    /// <summary>
    /// 校验（全部来自**一手实测**的形状 ✓，不放"我们猜的"规则 ✓）：
    ///   ① 至少一个地牢，且**每个地牢都给全 4 种**（一手实测 ✓）
    ///   ② `amount_table` 恰好 4 种（bust/portrait/deed/crest ✓）
    ///   ③ 6 档结构：**只有 1/3/5 档有值**（一手实测 ✓ 偶数档是空数组 ✓）
    ///   ④ 每行长度 = 4（任务长度 1~4 ✓）且**首项为 0**（长度 0 不存在 ✓）
    ///   ⑤ 🔴 **档越高给得不少**（逐项非递减 ✓ —— 一手三行 [0,2,2,4]→[0,2,3,6]→[0,3,5,9] 满足 ✓）
    /// </summary>
    public void Validate()
    {
        var problems = new List<string>();
        var kinds = new[] { "bust", "portrait", "deed", "crest" };

        if (DungeonTypes.Count == 0)
        {
            problems.Add("dungeon_types 为空 ⇒ 通道没有来源 ✓");
        }

        foreach (KeyValuePair<string, List<string>> kv in DungeonTypes)
        {
            if (kv.Value.Count != 4 || kinds.Except(kv.Value).Any())
            {
                problems.Add($"{kv.Key}: 一手实测**每个地牢都给全 4 种** ⇒ 实际 [{string.Join(",", kv.Value)}]");
            }
        }

        foreach (string kind in kinds)
        {
            if (!AmountTable.TryGetValue(kind, out List<List<int>>? tiers) || tiers is null)
            {
                problems.Add($"amount_table 缺 {kind} ✓");
                continue;
            }

            if (tiers.Count != 6)
            {
                problems.Add($"{kind}: 应为 **6 档**（一手）⇒ 实际 {tiers.Count}");
                continue;
            }

            for (int i = 0; i < tiers.Count; i++)
            {
                bool shouldHaveValues = i is 1 or 3 or 5;
                if (shouldHaveValues && tiers[i].Count == 0)
                {
                    problems.Add($"{kind} 档{i}: 一手实测**档 1/3/5 有值** ⇒ 它是空的 ✗");
                }

                if (!shouldHaveValues && tiers[i].Count != 0)
                {
                    problems.Add($"{kind} 档{i}: 一手实测**偶数档为空** ⇒ 它有 {tiers[i].Count} 项 ✗");
                }
            }

            foreach (List<int> row in tiers.Where(r => r.Count > 0))
            {
                if (row.Count != 4)
                {
                    problems.Add($"{kind}: 每行应 = **4 个任务长度** ⇒ 实际 {row.Count}");
                }
                else if (row[0] != 0)
                {
                    problems.Add($"{kind}: 首项应为 **0**（长度 0 不存在 ✓）⇒ 实际 {row[0]}");
                }
            }

            List<List<int>> nonEmpty = tiers.Where(r => r.Count == 4).ToList();
            for (int i = 1; i < nonEmpty.Count; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (nonEmpty[i][j] < nonEmpty[i - 1][j])
                    {
                        problems.Add($"{kind}: **档越高给得不少**被破 ⇒ 第 {i} 档 [{string.Join(",", nonEmpty[i])}] "
                            + $"低于上一档 [{string.Join(",", nonEmpty[i - 1])}]");
                    }
                }
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException("quest_reward 校验失败：" + Environment.NewLine + "  · "
                + string.Join(Environment.NewLine + "  · ", problems));
        }
    }

    /// <summary>
    /// 查表：某档难度 · 某**任务长度（1~4）**⇒ 数量 ✓
    /// 🔴 **索引口径 = (ii)**（策划 `DELIVERY-DESIGNER-HEIRLOOM-ACK2` 裁定 ✓ 三条一手证据）：
    ///   `amounts[difficulty][length - 1]` ✓ —— 即 **4 个数 = length 1,2,3,4**
    ///   （`length` 从 **1** 起，不是 0；而 `length 1 ⇒ 0` 也说得通：**短任务不给传家宝** ✓）
    /// ⚠️ 我第一版写成 `row[questLength]` ⇒ 那是 **(i) 形** ✗（会让 length 4 没有值 ⇒ 一手有 length=4 的任务 ⇒ 不成立 ✓）
    /// </summary>
    public int AmountAt(string kind, int difficultyTier, int questLength)
    {
        if (!AmountTable.TryGetValue(kind, out List<List<int>>? tiers) || tiers is null
            || difficultyTier < 0 || difficultyTier >= tiers.Count)
        {
            return 0;
        }

        List<int> row = tiers[difficultyTier];
        return questLength >= 1 && questLength <= row.Count ? row[questLength - 1] : 0;
    }

    /// <summary>
    /// 🆕 **难度档 ← 队伍的 resolve level**（策划 `HEIRLOOM-STEP2-ANSWER` ① · **一手** ✓）：
    ///   `generated_resolve_level_difficulties` = **[0,1,2]→1 · [2,3,4]→3 · [4,5,6]→5** ✓
    ///   （间隔重叠：[2] 与 [4] 各出现两次 ⇒ 一手表如此 ✓ 取**最靠后的命中**以对上"档 1/3/5" ✓）
    /// </summary>
    public static int DifficultyForResolveLevel(int resolveLevel)
        => resolveLevel >= 4 ? 5 : resolveLevel >= 2 ? 3 : 1;

    /// <summary>
    /// 🆕 **一趟的传家宝产出**（按裁定：**每趟 4 种都给** ✓ `placeholder` ⚠️ ⇒ 观察清单 O11 ✓）：
    ///   ⇒ 返回 (kind ⇒ 数量) —— **只对角色的 4 种都给**，数量取自 `AmountAt` ✓
    /// </summary>
    public IReadOnlyDictionary<string, int> RewardFor(int difficultyTier, int questLength)
    {
        var outMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string kind in new[] { "bust", "portrait", "deed", "crest" })
        {
            int n = AmountAt(kind, difficultyTier, questLength);
            if (n > 0)
            {
                outMap[kind] = n;
            }
        }

        return outMap;
    }

    /// <summary>一趟产出的**总份数**（读数用 ✓）</summary>
    public int RewardTotalFor(int difficultyTier, int questLength)
        => RewardFor(difficultyTier, questLength).Values.Sum();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
