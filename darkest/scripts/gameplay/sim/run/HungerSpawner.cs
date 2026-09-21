using System;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>🔴 `D-5`：一次"饿了没"的掷骰结果（**空 = 不饿**，不是 null）✓</summary>
public sealed record HungerRollResult(bool Triggered, double PercentUsed, int TierIndex)
{
    /// <summary>"没饿" —— 显式建出来，**不使用 null**（避免调用方漏判）✓</summary>
    public static readonly HungerRollResult None = new(false, 0, -1);
}

/// <summary>
/// 🔴🔴 **`D-5` 饥饿**（2026-09-20 新增；`dd_replication_roadmap` D-5 / DD wiki "Hunger"）。
///
/// **DD 的行为**（wiki 原文实据）：
/// · 饥饿是**走廊格上的隐藏事件**（与 Fight/Obstacle/Curio 同类，**地图上永不显示**，侦察也不显示）；
/// · 概率**按光照档**：`Radiant/Dim = 7.5%` · `Shadowy/Dark = 10%` · `Black as Pitch = 12.5%`
///   ⇒ 与 D-2 **同向**（越黑越频繁）；
/// · **缓冲**：开场/扎营后 2 条走廊；触发后 1 条；🔴 **只在前行时递减**（回头不算）；
/// · 触发 ⇒ **必须二选一**（不能拖/跳）：**吃**（每人 1 口粮，全队 +5% 最大 HP）/
///   **不吃**（全队 −20% 最大 HP + 20 压力）。**不能只喂一部分人** —— 口粮不够 ⇒ **只能挨饿，且一口粮都不消耗**。
///
/// **本类分工（重要）**：
/// · **本类只管"掷"**（纯函数、零 Godot）—— 与 `RevisitSpawner` 完全同构；
/// · **缓冲状态由调用方持有**（`ExpeditionFlow`）—— 因为它依赖"前行/回头"的行走语义，那是流程的事，
///   放进来会把纯函数变成状态机 ⚠️（`blueprint §9.17`：状态机不得持有规则状态）；
/// · **"吃/不吃"的结算**在 `ExpeditionSession`（它才持有口粮与队伍血量）。
///
/// **纪律**：**随机必写 `RngDraw`**；**数值全部来自 data**（`placeholder`）；**未配置 ⇒ 完全不掷**（连日志都不写）✓
/// </summary>
public static class HungerSpawner
{
    /// <summary>
    /// 掷一次"这一格是否触发饥饿检查"。
    /// </summary>
    /// <param name="log">事件流（**必写 `RngDraw`**）</param>
    /// <param name="rng">随机源</param>
    /// <param name="config">`tuning.dungeon_layer.hunger`（`null` ⇒ **不掷**、返回 `None`）</param>
    /// <param name="lightValue">**掷骰当时**的光照值（决定用哪一档：越暗概率越高）</param>
    public static HungerRollResult Roll(
        CombatLog log, IRngProvider rng, HungerConfig? config, int lightValue)
    {
        if (log is null || rng is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (config is null || config.Tiers is null || config.Tiers.Count == 0)
        {
            return HungerRollResult.None; // 🔴 未配置 ⇒ **一点副作用都没有**（不掷、不写日志）✓
        }

        // 档位选择：取**满足条件的最暗一档**
        // 🔴 数据按「暗 → 亮」(`max_light` 严格递增) 排列，由 `TuningConfig.Validate` 加载期强制；
        //    本类**不排序** ⇒ 首个 `lightValue <= max_light` 命中者即最暗档 ✓（与 RevisitSpawner 同手法）
        HungerTier? tier = null;
        int tierIndex = -1;
        for (int i = 0; i < config.Tiers.Count; i++)
        {
            HungerTier t = config.Tiers[i];
            if (lightValue <= t.MaxLight)
            {
                tier = t;
                tierIndex = i;
                break;
            }
        }

        if (tier is null)
        {
            return HungerRollResult.None; // 比所有档都亮 ⇒ 不饿（不掷）✓
        }

        double roll = rng.NextPercent();
        log.Append(new RngDraw(rng.DrawCount, roll)); // 🔴 确定性红线：抽取必写日志 ✓
        return new HungerRollResult(roll < tier.Percent, tier.Percent, tierIndex);
    }

    /// <summary>
    /// 🔴 **吃得起吗**：每名**存活**成员 <see cref="HungerConfig.FoodPerHero"/> 口粮 ⇒ 总需求。
    /// **DD 铁律**：**不能只喂一部分人** —— 凑不齐就**全员挨饿**，且**一口粮都不消耗** ✓
    /// </summary>
    public static int FoodRequired(HungerConfig config, int survivors)
        => Math.Max(0, survivors) * Math.Max(0, config.FoodPerHero);

    /// <summary>🔴 **吃**：全队回 `EatHealPercent`% 最大 HP（DD = 5%）。返回**实际回了多少点**（各人不同，见下）✓</summary>
    /// <remarks>
    /// ⚠️ **DD 是"每人各按自己 MaxHp 的 5%"**（不是"全队总额"）⇒ 逐人算（wiki："heal every hero for 5% of their maximum HP"）。
    /// 本方法**只算数值**，不写状态 —— 应用由 `ExpeditionSession` 做（它才持有 `Retained`）✓
    /// </remarks>
    public static int EatHealFor(HungerConfig config, int maxHp)
        => (int)Math.Ceiling(Math.Max(0, maxHp) * config.EatHealPercent / 100.0);

    /// <summary>🔴 **挨饿**：每人掉 `StarveHpPercent`% 最大 HP（DD = 20%，向上取整）✓</summary>
    public static int StarveDamageFor(HungerConfig config, int maxHp)
        => (int)Math.Ceiling(Math.Max(0, maxHp) * config.StarveHpPercent / 100.0);
}
