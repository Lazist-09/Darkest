using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>装备轴：**武器** / **护甲**（DD 原版两条**互相独立**的升级树 ⇒ 阶也各记一份 ✓）。</summary>
public enum GearAxis
{
    Weapon,
    Armour,
}

/// <summary>
/// 🔴 **H-1 · 英雄装备阶**（`Phase 2` 第一刀 · 2026-09-27）—— **内核层（零 Godot）** ✓
///
/// WHY 本件存在（**三份文档共同指向的同一个洞**）：
///   · `reports/tier_source_options.md`：「阶数要影响伤害」的**唯一剩余前置** = 「**当前阶从哪来**」；
///   · `HeroConfig.WeaponTier/ArmourTier` 的注释明写：**激活条件 = 减伤读阶接线时从本字段取阶**；
///   · `WeaponBaseDamage` / `TierDefence` 两个纯机制**都无生产调用方**（被守卫钉住）⇒ 缺的就是"阶"。
///   ⇒ 本件回答那个问题，裁定 = 文档推荐的 **(A) 升级树等级驱动**（不是我发明的 ✓）。
///
/// 🔴 **(A) 的完整语义**（一手 `hero_upgrades.json` + `buildings.json`，逐条对过 ✓）：
///   · 一棵装备树有 **4 个可购买等级**（code `0`/`1`/`2`/`3`）⇒ 起始 0 阶，买到 code N ⇒ **阶 = N+1** ⇒
///     正好对上 `units.json` 的 **5 阶（0~4）**（`UnitsConfig` 校验强制"阶数恰好 5" ✓）⇒ **自洽，不是凑的** ✓
///   · 每个等级的**双前置**（原版结构，**不是我加的** ✓）：
///       ① 本树前一级（买 code N 必须**当前阶 == N**）
///       ② 铁匠铺建筑树等级（`blacksmith.weapon` / `blacksmith.armour` 的 `a`/`b`/`c`/`d` ⇒ 建筑等级 1/2/3/4）
///   · 还有 **`prerequisite_resolve_level`**（英雄决心等级 1/2/3/5）⇒ 用 `HeroConfig.Level`（1~6）判定 ✓
///   · 成本 = **金币**（`currency_cost.type = gold`）⇒ 走 `Economy.TrySpend` ✓
///
/// 🔴 **原型 → DD 职业的映射有【一手出处】**（`units.json` 的 `_align` 注释逐字写明，**不是我编的** ✓）：
///   `warrior → hellion` · `tank → man_at_arms` · `medic → plague_doctor` · `commissar → highwayman`
///   ⇒ 映射表**只认这四条**；遇到未登记的原型的 ⇒ **抛**（不静默当成 0 阶 ⇒ 与"未接线"无法区分）✓
///
/// 🔴 三条纪律（沿用前几刀的静默失效守卫）：
///   ① **拒绝必须给理由**（`Why`）且**必写事件** ⇒ 不静默；
///   ② **阶封顶 4**（超过即拒绝，不静默钳制 ⇒ 让玩家知道"已满级"）；
///   ③ **未配置 `hero_upgrades.json` ⇒ 一次都不判**（opt-in），既有调用点行为逐字不变 ✓
/// </summary>
public static class HeroGear
{
    /// <summary>装备阶的取值域（对应 `units.json` 的 5 阶表 ⇒ 与 `UnitsConfig` 校验同口径 ✓）。</summary>
    public const int MaxTier = 4;

    /// <summary>
    /// 🆕 **原型 → DD 职业**（一手：`units.json` 的 `_align` 注释 ⇒ 5 阶表就是从这些职业对齐来的 ✓）。
    /// 🔴 **查不到即抛** —— 静默回 0 阶 = **与"还没接线"完全无法区分**（静默失效家族 ✓）。
    /// </summary>
    private static readonly Dictionary<string, string> DdClassByArchetype = new(StringComparer.Ordinal)
    {
        ["warrior"] = "hellion",
        ["tank"] = "man_at_arms",
        ["medic"] = "plague_doctor",
        ["commissar"] = "highwayman",
    };

    /// <summary>该原型的 DD 职业 id（未登记 ⇒ null ⇒ 调用方应拒绝，不静默当 0 阶）。</summary>
    public static string? DdClassOf(string archetype)
        => archetype is not null && DdClassByArchetype.TryGetValue(archetype, out string? cls) ? cls : null;

    /// <summary>**建筑树 id**（铁匠铺那条前置；`heirlooms.json` 的 `building` 字段**逐字同此** ✓）。</summary>
    public static string BuildingTreeId(GearAxis axis) => $"blacksmith.{AxisSuffix(axis)}";

    private static string AxisSuffix(GearAxis axis) => axis switch
    {
        GearAxis.Weapon => "weapon",
        GearAxis.Armour => "armour",
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "未知的装备轴。"),
    };

    /// <summary>取该轴的那棵升级树（职业未登记 / 数据里没有 ⇒ null ⇒ 调用方拒绝，不静默）。</summary>
    public static HeroUpgradeTreeConfig? TreeOf(HeroUpgradesConfig cfg, string archetype, GearAxis axis)
    {
        if (cfg?.Heroes is null || DdClassOf(archetype) is not { } cls)
        {
            return null;
        }

        string treeId = $"{cls}.{AxisSuffix(axis)}";
        return cfg.Heroes.TryGetValue(cls, out HeroUpgradeClassConfig? c)
            ? c.Trees.FirstOrDefault(t => string.Equals(t.Id, treeId, StringComparison.Ordinal))
            : null;
    }

    /// <summary>
    /// 🔴 **能不能升这一阶** —— 把"为什么不能"变成**可展示的字符串**（红线 21 (b)：可用性由内核回答）。
    /// </summary>
    /// <param name="currentTier">**当前阶**（0~4；= 已购等级数 ✓）</param>
    /// <param name="buildingLevel">铁匠铺该轴的**建筑等级**（`HeirloomStock.LevelOf(BuildingTreeId(axis))` ✓）</param>
    /// <param name="resolveLevel">英雄决心等级（= `HeroConfig.Level` ✓）</param>
    /// <returns>`null` = 可以升；否则 = **人话理由**（UI 直接显示 ✓）</returns>
    public static string? Why(
        HeroUpgradesConfig cfg,
        string archetype,
        GearAxis axis,
        int currentTier,
        int buildingLevel,
        int resolveLevel,
        int gold)
    {
        if (currentTier < 0 || currentTier > MaxTier)
        {
            return $"当前阶 {currentTier} 越界（合法 0~{MaxTier}）";
        }

        if (currentTier >= MaxTier)
        {
            return $"已满级（第 {MaxTier} 阶）";
        }

        HeroUpgradeTreeConfig? tree = TreeOf(cfg, archetype, axis);
        if (tree is null)
        {
            return $"原型「{archetype}」没有对应的 DD 职业升级树（未登记原型 ⇒ 拒绝，不静默当 0 阶）";
        }

        // 买 code N 必须当前阶 == N（原版"本树前一级"的结构 ✓）
        string wantCode = currentTier.ToString(CultureInfo.InvariantCulture);
        HeroUpgradeLevelConfig? lv = tree.Levels.FirstOrDefault(l => l.Code == wantCode);
        if (lv is null)
        {
            return $"{tree.Id} 没有 code {wantCode} 这一级（数据缺级 ⇒ 拒绝，不静默跳级）";
        }

        foreach (UpgradePrerequisiteConfig p in lv.Prerequisites ?? Array.Empty<UpgradePrerequisiteConfig>())
        {
            if (string.Equals(p.TreeId, BuildingTreeId(axis), StringComparison.Ordinal))
            {
                int need = BuildingLevelOfCode(p.RequirementCode);
                if (buildingLevel < need)
                {
                    return $"铁匠铺「{AxisSuffix(axis)}」等级不足（需 {p.RequirementCode} = 等级 {need}，当前 {buildingLevel}）";
                }
            }
            else if (lv.PrerequisiteResolveLevel is { } needResolve && resolveLevel < needResolve)
            {
                return $"决心等级不足（需 {needResolve}，当前 {resolveLevel}）";
            }
        }

        int cost = GoldCostOf(lv);
        if (gold < cost)
        {
            return $"金币不足（需 {cost}，当前 {gold}）";
        }

        return null; // 可以升 ✓
    }

    /// <summary>该级需要的**金币**（`currency_cost.type = gold` 的合计；没有 gold ⇒ 0 ✓）。</summary>
    public static int GoldCostOf(HeroUpgradeLevelConfig lv)
        => lv.CurrencyCost is null
            ? 0
            : lv.CurrencyCost.Where(c => string.Equals(c.Type, "gold", StringComparison.Ordinal)).Sum(c => c.Amount);

    /// <summary>建筑 code（`a`/`b`/`c`/`d`）⇒ 等级（1/2/3/4）；未知 code ⇒ **抛**（不静默当 0）。</summary>
    public static int BuildingLevelOfCode(string code)
        => code?.Trim().ToLowerInvariant() switch
        {
            "a" => 1,
            "b" => 2,
            "c" => 3,
            "d" => 4,
            _ => throw new InvalidOperationException(
                $"未知的建筑等级 code「{code}」（只认 a/b/c/d ⇒ 不静默当成等级 0）"),
        };
}

/// <summary>
/// 🔴 **英雄装备阶的可变持有者**（跨趟状态 · 与 `Economy` / `HeirloomStock` 同层）。
///
/// WHY 单独一个类（而不是写进 `HeroConfig`）：`HeroConfig` 是 **record（不可变）**，
///   而 `Roster.Heroes` 是 `IReadOnlyList&lt;HeroConfig&gt;` ⇒ 阶**涨不了**；
///   ⇒ 把"可变的阶"放在这里（按英雄 id 索引），`Roster` 只管名册本身 ✓
///
/// 🔴 **变更必写事件**（`CombatLog`）：升级成功写 `gear_upgraded`，**被拒也写** `gear_upgrade_refused`（理由入账）⇒ 不静默 ✓
/// </summary>
public sealed partial class HeroGearState
{
    private readonly Dictionary<string, (int Weapon, int Armour)> _tiers = new(StringComparer.Ordinal);

    /// <summary>该英雄的武器阶（未登记 ⇒ 0 = 第 0 阶 ✓）。</summary>
    public int WeaponTierOf(string heroId)
        => _tiers.TryGetValue(heroId, out (int W, int A) t) ? t.W : 0;

    /// <summary>该英雄的护甲阶（未登记 ⇒ 0 ✓）。</summary>
    public int ArmourTierOf(string heroId)
        => _tiers.TryGetValue(heroId, out (int W, int A) t) ? t.A : 0;

    /// <summary>该英雄某轴的阶。</summary>
    public int TierOf(string heroId, GearAxis axis)
        => axis == GearAxis.Weapon ? WeaponTierOf(heroId) : ArmourTierOf(heroId);

    /// <summary>
    /// **升一阶**（武器或护甲）。成功 ⇒ 扣金币 + 阶 +1 + 写事件；被拒 ⇒ **不扣钱** + 写拒绝事件（带理由）✓
    /// </summary>
    /// <returns>升级后的新阶（被拒 ⇒ 返回原阶不变）</returns>
    public int TryUpgrade(
        CombatLog log,
        HeroUpgradesConfig cfg,
        Economy economy,
        HeroConfig hero,
        GearAxis axis,
        int buildingLevel)
    {
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (cfg is null || economy is null || hero is null)
        {
            throw new ArgumentNullException(cfg is null ? nameof(cfg) : economy is null ? nameof(economy) : nameof(hero));
        }

        int current = TierOf(hero.Id, axis);
        string? why = HeroGear.Why(cfg, hero.Archetype, axis, current, buildingLevel, hero.Level, economy.Gold);

        if (why is not null)
        {
            log.Append(new EffectEvent(default,
                $"gear_upgrade_refused:{hero.Id}:{axis}:{why}", 100.0, Triggered: false));
            return current;
        }

        int cost = HeroGear.GoldCostOf(HeroGear.TreeOf(cfg, hero.Archetype, axis)!
            .Levels.First(l => l.Code == current.ToString(CultureInfo.InvariantCulture)));

        if (!economy.TrySpend(log, cost, $"gear:{hero.Id}:{axis}"))
        {
            log.Append(new EffectEvent(default,
                $"gear_upgrade_refused:{hero.Id}:{axis}:扣款失败", 100.0, Triggered: false));
            return current;
        }

        (int W, int A) t = _tiers.TryGetValue(hero.Id, out (int W, int A) old) ? old : (0, 0);
        _tiers[hero.Id] = axis == GearAxis.Weapon ? (t.W + 1, t.A) : (t.W, t.A + 1);

        log.Append(new EffectEvent(default,
            $"gear_upgraded:{hero.Id}:{axis}:{current}→{TierOf(hero.Id, axis)}（花费 {cost} 金）", 100.0, Triggered: true));
        return TierOf(hero.Id, axis);
    }

    /// <summary>🟩 取证：一行摘要（供 UI / 打印自证 ✓）。</summary>
    public string Audit(IEnumerable<HeroConfig> heroes)
    {
        var parts = new List<string>();
        foreach (HeroConfig h in heroes ?? Enumerable.Empty<HeroConfig>())
        {
            parts.Add($"{h.Id}[武{WeaponTierOf(h.Id)}/甲{ArmourTierOf(h.Id)}]");
        }

        return parts.Count == 0 ? "（无英雄）" : string.Join(" ", parts);
    }
}
