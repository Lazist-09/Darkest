using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Darkest.Data;

/// <summary>
/// 🔴 **M14 · 任务表 P 校验**（口径全部来自参考实测 —— 不发明规则；出处 `04b_quests_loot_narration.md`）✓
///
///   ① 阈值表只允许 `null`（无上限）或 ≥1 的正整数 —— 原版哨兵 **99** / 负数 ⇒ 红（转写时已归一为 null）✓
///   ② 引用完整性：`goal_ids` ／ `plot_quest_dependency`（`""` = 无依赖）／ `town_progression_goal_ids` ／
///      `types.goal_lists.goals[][]` 必须全部命中 `goals[].id` ✓
///   ③ `plot_quests[].quest.type` 必须命中 `types[].id`（6 个）✓
///   ④ `length` ∈ 1..4 · `difficulty` ∈ 1..6 · `dungeon` ∈ 已知集合 ✓
///   ⑤ 每条 `plot_quest` **恰好 1 个** `goal_id`（参考侧硬约束）✓
///   ⑥ 奖励槽位键 ⊆ {0,1,2}（参考侧硬编码上限 3）✓
///   ⑦ `goals[].id` 唯一（否则引用有歧义）✓
/// </summary>
public sealed partial class QuestsConfig
{
    private const int MinResolveLevelCap = 1;
    private const int NoLevelCapSentinel = 99;   // 原版「无上限」哨兵 —— 落库前必须已归一为 null
    private const int MinQuestLength = 1;
    private const int MaxQuestLength = 4;
    private const int MinQuestDifficulty = 1;
    private const int MaxQuestDifficulty = 6;
    private const int ExpectedGoalIdsPerQuest = 1;

    private static void Validate(QuestsConfig cfg)
    {
        if (cfg.Goals.Count == 0 || cfg.PlotQuests.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: goals / plot_quests 为空。");
        }

        // ⑦ goals[].id 唯一 ⇒ 引用才无歧义 ✓
        var goalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (QuestGoalConfig g in cfg.Goals)
        {
            if (!goalIds.Add(g.Id))
            {
                throw new InvalidDataException($"{ResPath}: goals id「{g.Id}」重复（P 校验⑦）。");
            }
        }

        // ① 阈值表：null 或 ≥1；99 / 负数 ⇒ 红 ✓
        IReadOnlyList<int?> thresholds = cfg.Restriction.Difficulty.ResolveLevelThresholdTable;
        for (int i = 0; i < thresholds.Count; i++)
        {
            int? t = thresholds[i];
            if (t is null)
            {
                continue;
            }

            if (t == NoLevelCapSentinel || t < MinResolveLevelCap)
            {
                throw new InvalidDataException(
                    $"{ResPath}: resolve_level_threshold_table[{i}] = {t} ⇒ 只允许 null（无上限）或 ≥{MinResolveLevelCap} 的正整数（P 校验①：99 哨兵必须已归一为 null）。");
            }
        }

        // ② town_progression_goal_ids 引用 ✓
        foreach (string id in cfg.TownProgressionGoalIds)
        {
            RequireGoal(goalIds, id, "town_progression_goal_ids");
        }

        // ② types.goal_lists.goals[][] 引用 ✓
        var typeIds = new HashSet<string>(cfg.Types.Select(t => t.Id), StringComparer.Ordinal);
        foreach (QuestTypeConfig t in cfg.Types)
        {
            foreach (QuestGoalListConfig gl in t.GoalLists)
            {
                foreach (IReadOnlyList<string> row in gl.Goals)
                {
                    foreach (string id in row)
                    {
                        RequireGoal(goalIds, id, $"types.{t.Id}.goal_lists[{gl.Dungeon}]");
                    }
                }
            }
        }

        var questIds = new HashSet<string>(cfg.PlotQuests.Select(q => q.Id), StringComparer.Ordinal);
        foreach (PlotQuestConfig q in cfg.PlotQuests)
        {
            // ③ 任务类型必须命中 types 表 ✓
            if (!typeIds.Contains(q.Quest.Type))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 「{q.Id}」的任务类型「{q.Quest.Type}」不在 types 表（P 校验③）。");
            }

            // ④ length / difficulty / dungeon 值域 ✓
            if (q.Quest.Length < MinQuestLength || q.Quest.Length > MaxQuestLength)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 「{q.Id}」的 length = {q.Quest.Length} ∉ [{MinQuestLength},{MaxQuestLength}]（P 校验④）。");
            }

            if (q.Quest.Difficulty < MinQuestDifficulty || q.Quest.Difficulty > MaxQuestDifficulty)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 「{q.Id}」的 difficulty = {q.Quest.Difficulty} ∉ [{MinQuestDifficulty},{MaxQuestDifficulty}]（P 校验④）。");
            }

            if (!KnownDungeons.Contains(q.Quest.Dungeon))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 「{q.Id}」的 dungeon「{q.Quest.Dungeon}」不在已知集合（P 校验④）。");
            }

            // ⑤ 恰好 1 个 goal_id（参考侧硬约束）✓
            if (q.Quest.GoalIds.Count != ExpectedGoalIdsPerQuest)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 「{q.Id}」的 goal_ids = {q.Quest.GoalIds.Count} 条 ⇒ 必须恰好 {ExpectedGoalIdsPerQuest} 条（P 校验⑤）。");
            }

            // ② goal_ids / plot_quest_dependency 引用 ✓
            foreach (string id in q.Quest.GoalIds)
            {
                RequireGoal(goalIds, id, $"{q.Id}.quest.goal_ids");
            }

            if (!string.IsNullOrEmpty(q.PlotQuestDependency) && !questIds.Contains(q.PlotQuestDependency))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 「{q.Id}」的 plot_quest_dependency「{q.PlotQuestDependency}」不存在（P 校验②）。");
            }

            // ⑥ 奖励槽位键 ⊆ {0,1,2} ✓
            RequireRewardSlots($"{q.Id}.completion_reward.items_definition", q.Quest.CompletionReward.ItemsDefinition.Items);
            RequireRewardSlots($"{q.Id}.additional_provisions", q.AdditionalProvisions.Items);
        }
    }

    private static void RequireGoal(HashSet<string> goalIds, string id, string where)
    {
        if (!goalIds.Contains(id))
        {
            throw new InvalidDataException($"{ResPath}: {where} 引用的 goal「{id}」不存在（P 校验②）。");
        }
    }

    private static void RequireRewardSlots(string where, Dictionary<string, QuestRewardItemConfig> items)
    {
        foreach (string key in items.Keys)
        {
            if (!RewardSlotKeys.Contains(key))
            {
                throw new InvalidDataException(
                    $"{ResPath}: {where} 的奖励槽位键「{key}」不在实测集合「0/1/2」内（P 校验⑥）。");
            }
        }
    }
}
