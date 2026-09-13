using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>传家宝掉落：某光照档给各类传家宝的**份数**（0 = 不给）。</summary>
public sealed record HeirloomDropSpec(
    [property: JsonPropertyName("busts")] int Busts = 0,
    [property: JsonPropertyName("crests")] int Crests = 0,
    [property: JsonPropertyName("deeds")] int Deeds = 0,
    [property: JsonPropertyName("portraits")] int Portraits = 0)
{
    public int Total => Busts + Crests + Deeds + Portraits;
}

/// <summary>升级一级的效果（两轴：**降费** 与 **解锁/增强**）。</summary>
public sealed record UpgradeEffect(
    [property: JsonPropertyName("relief_cost_delta")] int ReliefCostDelta = 0,
    [property: JsonPropertyName("morale_restore_delta")] int MoraleRestoreDelta = 0,
    [property: JsonPropertyName("roster_cap_delta")] int RosterCapDelta = 0,
    [property: JsonPropertyName("rookie_level")] int? RookieLevel = null);

/// <summary>升级一级：等级 + **传家宝消耗**（按 kind 名给份数）+ 效果。</summary>
public sealed record UpgradeLevel(
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("cost")] Dictionary<string, int> Cost,
    [property: JsonPropertyName("effect")] UpgradeEffect Effect);

/// <summary>一座建筑的升级路线（`axis` ∈ cost_down / effect_up / unlock）。</summary>
public sealed record UpgradePath(
    [property: JsonPropertyName("building")] string Building,
    [property: JsonPropertyName("axis")] string Axis,
    [property: JsonPropertyName("levels")] IReadOnlyList<UpgradeLevel> Levels,
    [property: JsonPropertyName("note")] string? Note = null);

/// <summary>
/// `heirlooms.json` 根模型 + **P23 校验（M8.1）**：
/// ① 四种传家宝齐全；**掉落与光照档挂钩**（每档总份数**单调不减**、且 **shadowy 起必须 > 0**）；
/// ② **每级消耗固定几种传家宝、数量递增**（同一建筑各级的 kind 集合相同、且每类的份数**严格递增**）；
/// ③ **两轴都在**（至少一条 `cost_down`、至少一条 `unlock`）；
/// ④ 🔴 **首批只允许 M8.0 已存在的三个建筑**（tavern / abbey / stagecoach）—— 其余属 M8.2/M8.3，出现即报错。
/// </summary>
public sealed record HeirloomConfig(
    [property: JsonPropertyName("kinds")] IReadOnlyList<string> Kinds,
    [property: JsonPropertyName("tier_drop")] Dictionary<string, HeirloomDropSpec> TierDrop,
    [property: JsonPropertyName("upgrade_paths")] IReadOnlyList<UpgradePath> UpgradePaths)
{
    public const string ResPath = "res://data/heirlooms.json";

    /// <summary>光照档顺序（与 light.loot / economy.light_tier_bonus 同序）。</summary>
    public static readonly IReadOnlyList<string> TierOrder = new[] { "radiant", "dim", "shadowy", "dark", "black" };

    /// <summary>M8.1 首批允许升级的建筑（其余建筑的子系统尚未落地 ⇒ 出现即报错）。</summary>
    public static readonly IReadOnlyList<string> AllowedBuildings = new[] { "tavern", "abbey", "stagecoach" };

    public static HeirloomConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        HeirloomConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<HeirloomConfig>(json, new JsonSerializerOptions
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

    /// <summary>某档掉落的传家宝（缺档位即报错，不给默认值）。</summary>
    public HeirloomDropSpec DropFor(string tierId) => TierDrop.TryGetValue(tierId, out HeirloomDropSpec? d) && d is not null
        ? d
        : throw new InvalidDataException($"{ResPath}: tier_drop 缺档 \"{tierId}\"（P23 ①）。");

    public UpgradePath PathFor(string building) => UpgradePaths.FirstOrDefault(p => p.Building == building)
        ?? throw new InvalidDataException($"{ResPath}: 未知建筑 \"{building}\"（P23 ④）。");

    private static void Validate(HeirloomConfig cfg)
    {
        if (cfg.Kinds is null || cfg.Kinds.Count != 4 || cfg.Kinds.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException($"{ResPath}: kinds 必须为四种传家宝且非空（P23 ①）。");
        }

        // ① 掉落与光照档挂钩：总份数单调不减 + shadowy 起必须 > 0
        int last = -1;
        bool darkZone = false;
        foreach (string id in TierOrder)
        {
            HeirloomDropSpec d = cfg.DropFor(id);
            if (d.Total < last)
            {
                throw new InvalidDataException($"{ResPath}: tier_drop 总份数必须随变暗**单调不减**（{id}；P23 ①）。");
            }

            if (id == "shadowy")
            {
                darkZone = true;
            }

            if (darkZone && d.Total < 1)
            {
                throw new InvalidDataException(
                    $"{ResPath}: tier_drop 从 shadowy 起必须 > 0（{id}；P23 ①：否则传家宝不可获取）。");
            }

            last = d.Total;
        }

        if (cfg.UpgradePaths is null || cfg.UpgradePaths.Count == 0)
        {
            throw new InvalidDataException($"{ResPath}: upgrade_paths 不得为空（P23 ③）。");
        }

        bool hasCostDown = false, hasUnlock = false;
        foreach (UpgradePath p in cfg.UpgradePaths)
        {
            // ④ 只允许首批建筑
            if (!AllowedBuildings.Contains(p.Building))
            {
                throw new InvalidDataException(
                    $"{ResPath}: 建筑 \"{p.Building}\" 不在 M8.1 首批内（只允许 {string.Join(" / ", AllowedBuildings)}；P23 ④）。");
            }

            if (p.Axis == "cost_down")
            {
                hasCostDown = true;
            }

            if (p.Axis == "unlock")
            {
                hasUnlock = true;
            }

            if (p.Levels is null || p.Levels.Count == 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{p.Building}\" levels 为空（P23 ②）。");
            }

            // ② 每级固定几种 + 数量递增
            string[] kinds = p.Levels[0].Cost.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            foreach (string k in kinds)
            {
                if (!cfg.Kinds.Contains(k))
                {
                    throw new InvalidDataException($"{ResPath}: \"{p.Building}\" 用了未定义的传家宝 \"{k}\"（P23 ①）。");
                }
            }

            int prevSum = -1;
            foreach (UpgradeLevel lv in p.Levels)
            {
                string[] lvKinds = lv.Cost.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
                if (!lvKinds.SequenceEqual(kinds, StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{p.Building}\" 每级必须**固定同几种**传家宝（Lv{lv.Level} 与首级不同；P23 ② / DD 规律）。");
                }

                int sum = lv.Cost.Values.Sum();
                if (sum <= prevSum)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: \"{p.Building}\" 的消耗**数量必须逐级递增**（Lv{lv.Level} 合计 {sum} 不大于前一级 {prevSum}；P23 ②）。");
                }

                if (lv.Cost.Values.Any(v => v <= 0))
                {
                    throw new InvalidDataException($"{ResPath}: \"{p.Building}\" Lv{lv.Level} 的份数必须 > 0（P23 ②）。");
                }

                prevSum = sum;
            }
        }

        if (!hasCostDown || !hasUnlock)
        {
            throw new InvalidDataException(
                $"{ResPath}: **两轴都要在** —— 至少一条 cost_down（降费）与一条 unlock（解锁更高阶）（P23 ③）。");
        }
    }
}
