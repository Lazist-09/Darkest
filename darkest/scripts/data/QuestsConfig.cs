using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

// ----------------------------------------------------------------------
// 🔴 M14 · 任务表子结构（`darkest/data/quests.json`；键名逐字对齐实测 ✓）
//    出处 = `reports/unity_ref/04b_quests_loot_narration.md`（消费点 / 缺陷清单 / §5.4 阈值表语义）✓
// ----------------------------------------------------------------------

/// <summary>奖励/消耗物品条目（实测 `type` ∈ {gold, heirloom, trinket, quest_item}；`id` 允许空串）✓</summary>
public sealed record QuestRewardItemConfig(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("amount")] int Amount);

/// <summary>
/// `system_config_type` + `items` 捆绑（`completion_reward.items_definition` ／ `additional_provisions` 共用）✓
/// 🔴 `items` 的键 = **槽位序号**（实测只有 `"0"/"1"/"2"`，参考侧硬编码上限 3）⇒ 用 Dictionary 泛读，
///    键在 `QuestsConfig.RewardSlotKeys` 显式登记 + P 校验⑥ 断言（不发明第 4 个槽位）✓
/// </summary>
public sealed record QuestItemBundleConfig
{
    [JsonPropertyName("system_config_type")]
    public string SystemConfigType { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public Dictionary<string, QuestRewardItemConfig> Items { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// `goals[].data`：按 `type` 取用不同子集（实测全集 8 键，全部可空 ⇒ 单 record 覆盖，不丢键）✓
/// 逐 type 语义见 `04b_quests_loot_narration.md` §「data 子键」✓
/// </summary>
public sealed record QuestGoalDataConfig(
    [property: JsonPropertyName("amount")] int? Amount = null,
    [property: JsonPropertyName("curio_name")] string? CurioName = null,
    [property: JsonPropertyName("is_affliction")] bool? IsAffliction = null,
    [property: JsonPropertyName("is_virtue")] bool? IsVirtue = null,
    [property: JsonPropertyName("item")] QuestRewardItemConfig? Item = null,
    [property: JsonPropertyName("monster_class_ids")] IReadOnlyList<string>? MonsterClassIds = null,
    [property: JsonPropertyName("percentage")] double? Percentage = null,
    [property: JsonPropertyName("room_id")] string? RoomId = null);

/// <summary>`goals[]`（45 条：`id`/`type`/`starting_items`/`ignore_fog_of_war`/`show_as_quest`/`data` 出现率 45/45）✓</summary>
public sealed record QuestGoalConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("starting_items")] IReadOnlyList<QuestRewardItemConfig> StartingItems,
    [property: JsonPropertyName("ignore_fog_of_war")] bool IgnoreFogOfWar,
    [property: JsonPropertyName("show_as_quest")] bool ShowAsQuest,
    [property: JsonPropertyName("data")] QuestGoalDataConfig Data);

/// <summary>`types[].goal_lists[]`（`goals` = 字符串二维表；实测存在 `[[]]` 空表 ⇒ 原样保留，不替参考侧修数据）✓</summary>
public sealed record QuestGoalListConfig(
    [property: JsonPropertyName("dungeon")] string Dungeon,
    [property: JsonPropertyName("goals")] IReadOnlyList<IReadOnlyList<string>> Goals);

/// <summary>`types[]`（6 个任务类型：`plot_quests[].quest.type` 必须命中 —— P 校验③）✓</summary>
public sealed record QuestTypeConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("goal_lists")] IReadOnlyList<QuestGoalListConfig> GoalLists);

/// <summary>`plot_quests[].quest.completion_reward`（`resolve_xp` + 物品定义）✓</summary>
public sealed record QuestCompletionRewardConfig(
    [property: JsonPropertyName("resolve_xp")] int ResolveXp,
    [property: JsonPropertyName("items_definition")] QuestItemBundleConfig ItemsDefinition);

/// <summary>`plot_quests[].quest`（7 键 + 可选 `map_name`；实测 `goal_ids` 恰好 1 条 —— P 校验⑤）✓</summary>
public sealed record PlotQuestDefConfig(
    [property: JsonPropertyName("is_plot_quest")] bool IsPlotQuest,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("dungeon")] string Dungeon,
    [property: JsonPropertyName("difficulty")] int Difficulty,
    [property: JsonPropertyName("length")] int Length,
    [property: JsonPropertyName("goal_ids")] IReadOnlyList<string> GoalIds,
    [property: JsonPropertyName("completion_reward")] QuestCompletionRewardConfig CompletionReward,
    [property: JsonPropertyName("map_name")] string? MapName = null);

/// <summary>`additional_trinket_completion_rewards[]`（实测 `rarity` ∈ {very_common, very_rare}）✓</summary>
public sealed record QuestTrinketRewardConfig(
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("amount")] int Amount);

/// <summary>`upgrade_tags_to_remove_on_ignore[]`（实测 `upgrade_tag` = `building`）✓</summary>
public sealed record QuestUpgradeTagConfig(
    [property: JsonPropertyName("upgrade_tag")] string UpgradeTag,
    [property: JsonPropertyName("amount")] int Amount);

/// <summary>`suggested_trinkets[]`（实测 `trinket_id` = `dd_trinket`）✓</summary>
public sealed record QuestSuggestedTrinketConfig(
    [property: JsonPropertyName("trinket_id")] string TrinketId,
    [property: JsonPropertyName("amount")] int Amount);

/// <summary>
/// `plot_quests[]`（30 条；`plot_quest_dependency` 仅 4 条 ⇒ 可空，`""` = 无依赖 —— P 校验②）✓
/// ⚠️ 参考侧 13 个字段里只有 4 个真有消费点（`is_scouting_enabled`/`can_retreat`/`completion_dungeon_xp`/`plot_quest_dependency`），
///    其余 9 个「数据有、代码没接」⇒ 原样落库 + 在报告登记（不删、不伪造消费）✓
/// </summary>
public sealed record PlotQuestConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("dungeon_level")] int DungeonLevel,
    [property: JsonPropertyName("quest")] PlotQuestDefConfig Quest,
    [property: JsonPropertyName("additional_trinket_completion_rewards")] IReadOnlyList<QuestTrinketRewardConfig> AdditionalTrinketCompletionRewards,
    [property: JsonPropertyName("is_progression")] bool IsProgression,
    [property: JsonPropertyName("has_statue_contents")] bool HasStatueContents,
    [property: JsonPropertyName("completion_dungeon_xp")] bool CompletionDungeonXp,
    [property: JsonPropertyName("can_retreat")] bool CanRetreat,
    [property: JsonPropertyName("retreat_always_from_raid")] bool RetreatAlwaysFromRaid,
    [property: JsonPropertyName("retreat_party_kill_count")] int RetreatPartyKillCount,
    [property: JsonPropertyName("is_surprise_enabled")] bool IsSurpriseEnabled,
    [property: JsonPropertyName("is_scouting_enabled")] bool IsScoutingEnabled,
    [property: JsonPropertyName("is_roster_stress_cleared_on_completion")] bool IsRosterStressClearedOnCompletion,
    [property: JsonPropertyName("roster_buff_on_failure_minimum_party_resolve_level")] int RosterBuffOnFailureMinimumPartyResolveLevel,
    [property: JsonPropertyName("upgrade_tags_to_remove_on_ignore")] IReadOnlyList<QuestUpgradeTagConfig> UpgradeTagsToRemoveOnIgnore,
    /// <summary>实测 30/30 为空 ⇒ 元素类型未定（登记）；按字符串表泛读，非空时会走 P 校验前先红（fail-fast）✓</summary>
    [property: JsonPropertyName("upgrade_tags_to_remove_on_failure")] IReadOnlyList<string> UpgradeTagsToRemoveOnFailure,
    [property: JsonPropertyName("roster_buffs_to_apply_on_failure")] IReadOnlyList<string> RosterBuffsToApplyOnFailure,
    [property: JsonPropertyName("suggested_trinkets")] IReadOnlyList<QuestSuggestedTrinketConfig> SuggestedTrinkets,
    [property: JsonPropertyName("additional_provisions")] QuestItemBundleConfig AdditionalProvisions,
    [property: JsonPropertyName("plot_quest_dependency")] string? PlotQuestDependency = null);

/// <summary>`generation.number`（8 档：进城任务数表）✓</summary>
public sealed record QuestNumberGenConfig(
    [property: JsonPropertyName("number_of_quests_per_town_visit_table")] IReadOnlyList<int> NumberOfQuestsPerTownVisitTable);

/// <summary>`generation.dungeon.generated_dungeons[]`（解锁链：crypts2 → weald3 → warrens4 → cove4）✓</summary>
public sealed record QuestDungeonUnlockConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("required_number_of_quests_finished")] int RequiredNumberOfQuestsFinished);

/// <summary>`generation.dungeon`（每地牢生成上限 + 解锁链）✓</summary>
public sealed record QuestDungeonGenConfig(
    [property: JsonPropertyName("max_number_of_generated_quests_per_dungeon")] int MaxNumberOfGeneratedQuestsPerDungeon,
    [property: JsonPropertyName("generated_dungeons")] IReadOnlyList<QuestDungeonUnlockConfig> GeneratedDungeons);

/// <summary>`generation.difficulty.generated_resolve_level_difficulties[]`（与 `heirlooms.json` 的难度表同口径）✓</summary>
public sealed record QuestResolveDifficultyConfig(
    [property: JsonPropertyName("resolve_levels")] IReadOnlyList<int> ResolveLevels,
    [property: JsonPropertyName("difficulty")] int Difficulty);

/// <summary>`generation.difficulty`✓</summary>
public sealed record QuestDifficultyGenConfig(
    [property: JsonPropertyName("generated_resolve_level_difficulties")] IReadOnlyList<QuestResolveDifficultyConfig> GeneratedResolveLevelDifficulties);

/// <summary>`generation.type.available_quests_table[].generated_quest_table[][]`（难度档 → 条目；`chance` 实测恒 1）✓</summary>
public sealed record QuestGeneratedEntryConfig(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("chance")] int Chance,
    [property: JsonPropertyName("length")] int Length);

/// <summary>`generation.type.available_quests_table[]`（4 地牢 × 8 档）✓</summary>
public sealed record QuestAvailableTableConfig(
    [property: JsonPropertyName("dungeon")] string Dungeon,
    [property: JsonPropertyName("generated_quest_table")] IReadOnlyList<IReadOnlyList<QuestGeneratedEntryConfig>> GeneratedQuestTable);

/// <summary>`generation.type`✓</summary>
public sealed record QuestTypeGenConfig(
    [property: JsonPropertyName("available_quests_table")] IReadOnlyList<QuestAvailableTableConfig> AvailableQuestsTable);

/// <summary>`generation.rewards.heirloom_type_map[]`（是 list 不是 map；4 地牢 → 2 种传家宝）✓</summary>
public sealed record QuestHeirloomTypeConfig(
    [property: JsonPropertyName("dungeon")] string Dungeon,
    [property: JsonPropertyName("types")] IReadOnlyList<string> Types);

/// <summary>`generation.rewards.trinket_chance_table[]`（6 稀有度 × 7×4 概率表：行 = 难度 · 列 = 长度）✓</summary>
public sealed record QuestTrinketChanceConfig(
    [property: JsonPropertyName("rarity")] string Rarity,
    [property: JsonPropertyName("chances")] IReadOnlyList<IReadOnlyList<int>> Chances);

/// <summary>
/// `generation.rewards`（4 子键）✓
/// 🔴 `heirloom_amount_table` **有意不落库** —— 唯一真相在 `heirlooms.json` 的 `quest_reward.amount_table`
///    （`land_ref_quests.py` 每次运行都断言两者逐值相同）✓
/// </summary>
public sealed record QuestRewardsGenConfig(
    [property: JsonPropertyName("heirloom_type_map")] IReadOnlyList<QuestHeirloomTypeConfig> HeirloomTypeMap,
    [property: JsonPropertyName("item_table")] IReadOnlyList<IReadOnlyList<IReadOnlyList<QuestRewardItemConfig>>> ItemTable,
    [property: JsonPropertyName("resolve_xp_table")] IReadOnlyList<IReadOnlyList<int>> ResolveXpTable,
    [property: JsonPropertyName("trinket_chance_table")] IReadOnlyList<QuestTrinketChanceConfig> TrinketChanceTable);

/// <summary>`generation`（5 子键）✓</summary>
public sealed record QuestGenerationConfig(
    [property: JsonPropertyName("number")] QuestNumberGenConfig Number,
    [property: JsonPropertyName("dungeon")] QuestDungeonGenConfig Dungeon,
    [property: JsonPropertyName("difficulty")] QuestDifficultyGenConfig Difficulty,
    [property: JsonPropertyName("type")] QuestTypeGenConfig Type,
    [property: JsonPropertyName("rewards")] QuestRewardsGenConfig Rewards);

/// <summary>
/// `restriction.difficulty` —— 🔴 **它是上限表**（不是下限）：该难度允许的**最高** resolve level，
/// 英雄 resolve **高于**它 ⇒ 不可参战（出处 `04b_quests_loot_narration.md` §5.4 · RaidPartyPanel.IsResolveEligible 实测）✓
/// `null` = 原版「无等级上限」哨兵（原值 99 已在转写时归一 —— P 校验①拒收 99/负数）✓
/// </summary>
public sealed record QuestDifficultyRestrictionConfig(
    [property: JsonPropertyName("resolve_level_threshold_table")] IReadOnlyList<int?> ResolveLevelThresholdTable);

/// <summary>`restriction`✓</summary>
public sealed record QuestRestrictionConfig(
    [property: JsonPropertyName("difficulty")] QuestDifficultyRestrictionConfig Difficulty);

/// <summary>
/// 🔴 **M14 · 任务表**（`quests.json`）根模型 + fail-fast 解析（P 校验见 `QuestsConfig.Validate.cs`）✓
///
/// 消费面（本包接线）：城池「📜 任务选择」屏按地牢筛 `plot_quests` + 显示该难度的 resolve 上限（`ResolveLevelCapFor`）✓
/// 其余段（`generation.*` ／ `types` ／ `goals[].data`）本轮**只落库 + 校验**，消费点登记在报告里（不假装已接）✓
/// </summary>
public sealed partial class QuestsConfig
{
    public const string ResPath = "res://data/quests.json";

    /// <summary>奖励槽位键（参考侧硬编码上限 3 ⇒ 只有「0/1/2」；`items` 泛读的键在此显式登记 + P 校验⑥）✓</summary>
    public static readonly IReadOnlyList<string> RewardSlotKeys = new[] { "0", "1", "2" };

    /// <summary>已知地牢名（实测 `plot_quests` 取值全集 —— 不发明第 7 个）✓</summary>
    public static readonly IReadOnlyList<string> KnownDungeons =
        new[] { "cove", "crypts", "darkestdungeon", "town", "warrens", "weald" };

    [JsonPropertyName("stress_damage")]
    public int StressDamage { get; init; }

    [JsonPropertyName("goals")]
    public IReadOnlyList<QuestGoalConfig> Goals { get; init; } = Array.Empty<QuestGoalConfig>();

    [JsonPropertyName("town_progression_goal_ids")]
    public IReadOnlyList<string> TownProgressionGoalIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("types")]
    public IReadOnlyList<QuestTypeConfig> Types { get; init; } = Array.Empty<QuestTypeConfig>();

    [JsonPropertyName("plot_quests")]
    public IReadOnlyList<PlotQuestConfig> PlotQuests { get; init; } = Array.Empty<PlotQuestConfig>();

    [JsonPropertyName("generation")]
    public QuestGenerationConfig? Generation { get; init; }

    [JsonPropertyName("restriction")]
    public QuestRestrictionConfig Restriction { get; init; } = new(new QuestDifficultyRestrictionConfig(Array.Empty<int?>()));

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions() => new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static QuestsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        QuestsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<QuestsConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    /// <summary>
    /// 按地牢筛任务（键 = 数据侧词表，实测 `darkestdungeon` **无下划线**）✓
    /// ⚠️ UI 的坐标表用 `darkest_dungeon`（带下划线）⇒ 归一由调用方做（两套词表不在此处混）✓
    /// </summary>
    public IReadOnlyList<PlotQuestConfig> PlotQuestsFor(string dungeon)
        => PlotQuests.Where(q => q.Quest.Dungeon == dungeon).ToList();

    /// <summary>
    /// 该难度允许的**最高** resolve level（`null` = 无等级上限）✓
    /// ⚠️ 越界索引 ⇒ 返回 null（**不崩**；参考侧是裸索引 —— 本表长度与难度值域由 P 校验④ 保证）✓
    /// </summary>
    public int? ResolveLevelCapFor(int difficulty)
    {
        IReadOnlyList<int?> table = Restriction.Difficulty.ResolveLevelThresholdTable;
        if (difficulty < 0 || difficulty >= table.Count)
        {
            return null;
        }

        return table[difficulty];
    }

    /// <summary>按 id 取目标定义（不存在 ⇒ null；引用完整性由 P 校验② 保证）✓</summary>
    public QuestGoalConfig? GoalById(string id)
        => Goals.FirstOrDefault(g => g.Id == id);
}
