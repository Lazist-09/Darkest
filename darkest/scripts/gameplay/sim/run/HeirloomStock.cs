using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>
/// M8.1（`hamlet.md §1` / `m8_roadmap §1`）：**传家宝库存 + 建筑升级**（**跨会话状态**，与 `Economy` 同层）。
///
/// 🔴 三条设计要点（照规格）：
/// · **第三种资源**：传家宝**从光照档掉落**（**与金钱同源**：越暗越多）；
/// · **两轴升级**：**降费**（服务更便宜）与**解锁/增强**（更高阶）—— 每级**消耗固定几种、数量递增**；
/// · 🔴 **升级必须真的改变数字**（验收）：本类提供**生效值**读取口（`EffectiveReliefCost` / `EffectiveMoraleRestore` /
///   `EffectiveRosterCap` / `EffectiveRookieLevel`），供 Hamlet 与经济使用 —— **UI 只读不算**。
///
/// 归属：**跨会话持有者**（组合根注入；`BattleDirector` 不持有它）。变更**必写事件**。
/// </summary>
public sealed class HeirloomStock
{
    private readonly HeirloomConfig _cfg;
    private readonly Dictionary<string, int> _counts;
    private readonly Dictionary<string, int> _levels = new();

    /// <summary>
    /// 🆕 **传家宝产出通道 = 任务奖励**（策划 `HEIRLOOM-STEP2-ANSWER` 的**步骤 ②** ✓）。
    /// 🔴 **可空**：缺它 ⇒ `AwardForRun` **回落**旧的「按光照档掉落」（= **P4 前的桥** ⚠️）；
    ///    P4（删 `tier_drop`）之后本字段变**必填**、桥与 `AwardForTier` 一起删 ✓
    /// </summary>
    private HeirloomQuestRewardConfig? _reward;

    public HeirloomStock(HeirloomConfig config, HeirloomQuestRewardConfig? questReward = null)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
        _reward = questReward;
        _counts = config.Kinds.ToDictionary(k => k, _ => 0);
        foreach (UpgradePath p in config.UpgradePaths)
        {
            _levels[p.Building] = 0; // 未升级
        }
    }

    /// <summary>
    /// 🆕 **补挂任务奖励通道**（幂等 · 只补不换）。
    /// 🔴 **为什么需要它**（实测出来的顺序陷阱）：`HamletRoot.Build` 先调 `EnsureHeirlooms(heirloomCfg)`
    ///    （**不带通道** —— 那是 UI 域的文件，我不改它），远征组合根后调带通道的那次 ⇒
    ///    若 `EnsureHeirlooms` 只是 `??=`，**通道永远不会被注入** ⇒ 步骤 ② 在真实路径上**静默不生效** ⚠️
    ///    ⇒ 故按本仓既有惯例（`session.BindTrapRng` / `ExpeditionContext.BindConfigs`）给一个**补绑**口 ✓
    /// </summary>
    public void BindQuestReward(HeirloomQuestRewardConfig? reward)
    {
        _reward ??= reward; // 已有通道 ⇒ 不覆盖（不降级）
    }

    /// <summary>本库存是否已挂上任务奖励通道（读数 / 用例判据用 ✓）。</summary>
    public bool HasRunReward => _reward is not null;

    /// <summary>四种传家宝的 kind 名（UI 只读渲染用）。</summary>
    public IReadOnlyList<string> Kinds => _cfg.Kinds;

    /// <summary>某类传家宝的持有数。</summary>
    public int Count(string kind) => _counts.TryGetValue(kind, out int n)
        ? n
        : throw new InvalidOperationException($"未定义的传家宝 \"{kind}\"（P23 ①）。");

    /// <summary>某建筑的当前升级等级（0 = 未升级）。</summary>
    public int LevelOf(string building) => _levels.TryGetValue(building, out int lv)
        ? lv
        : throw new InvalidOperationException($"未知建筑 \"{building}\"（P23 ④）。");

    /// <summary>
    /// 🔴 **按【光照档】发放本趟的传家宝**（**旧通道**；`#470` 顺序 (A) 里的"**旧源**"）。
    ///
    /// ⚠️ **现状**：`#472` 之前它是**生产路径唯一入口**；步骤 ② 之后**生产路径改走 `AwardForRun`**，
    ///    本方法**只作为回落桥**（`_reward` 缺失时）存活 ⇒ 🆕 **P4 删 `tier_drop` 时连同它一起删** ✓
    /// </summary>
    public int AwardForTier(CombatLog log, string tierId, string reason = "battle")
    {
        HeirloomDropSpec drop = _cfg.DropFor(tierId);
        int total = 0;
        foreach (string kind in _cfg.Kinds)
        {
            int amount = KindAmount(drop, kind);
            if (amount > 0)
            {
                Add(log, kind, amount, reason);
                total += amount;
            }
        }

        return total;
    }

    /// <summary>
    /// 🆕 **步骤 ②（接线）：按【任务奖励】通道发放本趟的传家宝** —— 与金钱同一结算点（每场战斗胜利）。
    ///
    /// 策划口径（`HEIRLOOM-STEP2-ANSWER` · 一手两条 + 一条数据说不出）：
    ///   · **难度档 ← 队伍 resolve level**：`[0,1,2]→1 · [2,3,4]→3 · [4,5,6]→5`（重叠取靠后档 ✓ 一手）
    ///   · **任务长度 ← 任务自身的 `length`**（一手）
    ///   · **每趟 4 种都给**（数据说不出 ⇒ 最直接读法 ⇒ ⚠️ `placeholder` + 观察清单 **O11**）
    /// 🔴 **两个输入都要代理**（我们**没有任务层**、**没有 resolve level 模型** ⚠️）：
    ///   · `averageLevel` ⇒ `ProxyDifficultyFromAverageLevel`（⚠️ 我推的）
    ///   · `steps`        ⇒ `ProxyQuestLengthFromSteps`（⚠️ 我推的）
    ///   ⇒ 两个代理都在 `HeirloomQuestRewardConfig` 里**标了 placeholder**，并登记 `observe_list.md` **O11** ✓
    ///
    /// 🔴 **口径 (ii)**：`amounts[difficulty][length - 1]` ⇒ **length 1 ⇒ 0（短任务不给）** ✓
    /// </summary>
    /// <param name="log">结算账本（变更必写事件 ✓）</param>
    /// <param name="steps">**这趟【已走过】的段数**（1 起 ⇒ 长度 1~4，>4 钳 4；长度 1 ⇒ 0 ✓）</param>
    /// <param name="averageLevel">**队伍平均等级**（代理量 ⚠️ ⇒ 用它查难度带 1/3/5）</param>
    /// <param name="lightTierId">
    /// 🔴 **仅用于 P4 前的回落桥**：`_reward` 缺失时按这个光照档走 `AwardForTier`。
    ///    ⇒ **P4 删 `tier_drop` 时连本参数一起删**（那时通道是必填的）✓
    /// </param>
    /// <param name="reason">事件理由（与金钱同源口径 ✓）</param>
    public int AwardForRun(CombatLog log, int steps, double averageLevel, string lightTierId, string reason = "battle")
    {
        if (_reward is null)
        {
            // ⚠️ **P4 前的桥**：通道没挂上 ⇒ 退回旧行为（不静默不发 ✓）
            return AwardForTier(log, lightTierId, reason);
        }

        int difficulty = HeirloomQuestRewardConfig.ProxyDifficultyFromAverageLevel(averageLevel);
        int length = HeirloomQuestRewardConfig.ProxyQuestLengthFromSteps(steps);
        IReadOnlyDictionary<string, int> reward = _reward.RewardFor(difficulty, length);

        int total = 0;
        foreach (string stockKind in _cfg.Kinds) // 按 kinds 顺序 ⇒ 事件顺序确定（用例可复现 ✓）
        {
            if (TryStockKindToReward(stockKind, out string rewardKind)
                && reward.TryGetValue(rewardKind, out int amount) && amount > 0)
            {
                Add(log, stockKind, amount, reason);
                total += amount;
            }
        }

        return total;
    }

    /// <summary>增加/减少传家宝（变更必写事件；减少不足即拒绝）。</summary>
    public bool Add(CombatLog log, string kind, int delta, string reason)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        int current = Count(kind);
        if (current + delta < 0)
        {
            return false; // 不足 ⇒ 拒绝（不静默变负）
        }

        _counts[kind] = current + delta;
        log.Append(new HeirloomChangedEvent(kind, delta, _counts[kind], reason));
        return true;
    }

    /// <summary>下一级的消耗（已满级 ⇒ null）。</summary>
    public UpgradeLevel? NextLevel(string building)
    {
        UpgradePath p = _cfg.PathFor(building);
        int next = LevelOf(building) + 1;
        return p.Levels.FirstOrDefault(l => l.Level == next);
    }

    /// <summary>
    /// 🔴 红线 21 (b)：**按钮的可用性必须由内核回答**（UI 只渲染）—— 传家宝够且未满级才可升级。
    /// </summary>
    public bool CanUpgrade(string building)
    {
        UpgradeLevel? next = NextLevel(building);
        return next is not null && next.Cost.All(kv => Count(kv.Key) >= kv.Value);
    }

    /// <summary>**升级**：按曲线扣传家宝并升一级（不足即拒绝且不扣）。</summary>
    public bool TryUpgrade(CombatLog log, string building)
    {
        UpgradeLevel? next = NextLevel(building);
        if (next is null)
        {
            return false; // 已满级
        }

        foreach ((string kind, int need) in next.Cost)
        {
            if (Count(kind) < need)
            {
                return false; // 传家宝不足 ⇒ 拒绝（不部分扣、不静默）
            }
        }

        foreach ((string kind, int need) in next.Cost)
        {
            Add(log, kind, -need, $"upgrade:{building}");
        }

        _levels[building] = next.Level;
        log.Append(new BuildingUpgradedEvent(building, next.Level,
            string.Join(",", next.Cost.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}:{k.Value}"))));
        return true;
    }

    // ---------------------------------------------------------------
    // 生效值（**升级真的改变数字** —— 验收口径；UI 只读不算）
    // ---------------------------------------------------------------

    /// <summary>减压价格（含降费升级）。</summary>
    public int EffectiveReliefCost(int baseCost)
        => Math.Max(0, baseCost + SumEffects("tavern").ReliefCostDelta + SumEffects("abbey").ReliefCostDelta);

    /// <summary>某减压建筑的恢复量（含增强升级）。</summary>
    public int EffectiveMoraleRestore(string building, int baseRestore)
        => Math.Max(0, baseRestore + SumEffects(building).MoraleRestoreDelta);

    /// <summary>名册上限（含 Stage Coach 解锁）。</summary>
    public int EffectiveRosterCap(int baseCap, int hardCap)
        => Math.Min(hardCap, baseCap + SumEffects("stagecoach").RosterCapDelta);

    /// <summary>新兵起始等级（含 Stage Coach 解锁；缺省 1）。</summary>
    public int EffectiveRookieLevel(int baseLevel)
    {
        UpgradeEffect e = SumEffects("stagecoach");
        return e.RookieLevel ?? baseLevel;
    }

    private UpgradeEffect SumEffects(string building)
    {
        int level = LevelOf(building);
        var costDown = 0;
        var restore = 0;
        var cap = 0;
        int? rookie = null;
        foreach (UpgradeLevel lv in _cfg.PathFor(building).Levels.Where(l => l.Level <= level))
        {
            costDown += lv.Effect.ReliefCostDelta;
            restore += lv.Effect.MoraleRestoreDelta;
            cap += lv.Effect.RosterCapDelta;
            rookie = lv.Effect.RookieLevel ?? rookie;
        }

        return new UpgradeEffect(costDown, restore, cap, rookie);
    }

    private static int KindAmount(HeirloomDropSpec d, string kind) => kind switch
    {
        "busts" => d.Busts,
        "crests" => d.Crests,
        "deeds" => d.Deeds,
        "portraits" => d.Portraits,
        _ => 0,
    };

    /// <summary>
    /// 🔴 **两套 kind 名的桥**（**实测出来的真陷阱**，不是洁癖）：
    ///   · `heirlooms.json` 的 **`kinds`**（= `HeirloomConfig.Kinds` / 库存的键）= **复数** `busts / crests / deeds / portraits` ✓
    ///   · 一手 `quest.generation.json` 的 **`amount_table`** 键（= `HeirloomQuestRewardConfig`）= **单数** `bust / crest / deed / portrait` ✓
    /// ⇒ 🔴 不桥 ⇒ `reward.TryGetValue("busts")` **永远 miss** ⇒ **静默一件都不发**（`Add` 都不会被调）⚠️
    ///    ⇒ 故这里**显式列出**（不用 `TrimEnd('s')` 之类的猜法 —— 那是推断，不是数据 ✓ 纪律 BL）✓
    /// </summary>
    private static bool TryStockKindToReward(string stockKind, out string rewardKind)
    {
        switch (stockKind)
        {
            case "busts": rewardKind = "bust"; return true;
            case "crests": rewardKind = "crest"; return true;
            case "deeds": rewardKind = "deed"; return true;
            case "portraits": rewardKind = "portrait"; return true;
            default: rewardKind = string.Empty; return false;
        }
    }
}
