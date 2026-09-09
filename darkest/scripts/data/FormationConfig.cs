using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>
/// 我方 / 敌方槽位常量（data_schema §3.6：player / enemy）。JSON 键 snake_case。
/// </summary>
public sealed record SlotLayoutConfig(
    [property: JsonPropertyName("slot_count")] int SlotCount,
    [property: JsonPropertyName("combat_slots")] int CombatSlots,
    [property: JsonPropertyName("support_slots")] IReadOnlyList<int> SupportSlots);

/// <summary>编成行（data_schema §3.6 initial_roster 元素）：`unit` = units.json 原型 id。</summary>
public sealed record RosterEntryConfig(
    [property: JsonPropertyName("slot")] int Slot,
    [property: JsonPropertyName("unit")] string Unit);

/// <summary>初始编成（player / enemy 两张表）。</summary>
public sealed record SideRosterConfig(
    [property: JsonPropertyName("player")] IReadOnlyList<RosterEntryConfig> Player,
    [property: JsonPropertyName("enemy")] IReadOnlyList<RosterEntryConfig> Enemy);

/// <summary>关卡预置障碍（data_schema §3.6 obstacles）：`side`=player/enemy（字符串，B1 不依赖内核枚举）；hp 缺省 = 不可摧毁。</summary>
public sealed record ObstacleConfig(
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("slot")] int Slot,
    [property: JsonPropertyName("hp")] int? Hp);

/// <summary>行为规则标记（data_schema §3.6 rules 九键，全部 true）。</summary>
public sealed record FormationRulesConfig(
    [property: JsonPropertyName("boundary_as_hard_wall")] bool BoundaryAsHardWall,
    [property: JsonPropertyName("displacement_only_via_swap_chain")] bool DisplacementOnlyViaSwapChain,
    [property: JsonPropertyName("obstacle_swaps_like_unit")] bool ObstacleSwapsLikeUnit,
    [property: JsonPropertyName("close_up_on_death_immediate")] bool CloseUpOnDeathImmediate,
    [property: JsonPropertyName("close_up_ignores_obstacle")] bool CloseUpIgnoresObstacle,
    [property: JsonPropertyName("close_up_enemy_symmetric")] bool CloseUpEnemySymmetric,
    [property: JsonPropertyName("swap_player_initiated")] bool SwapPlayerInitiated,
    [property: JsonPropertyName("slot_three_state")] bool SlotThreeState,
    [property: JsonPropertyName("deaths_and_close_up_separate_from_displacement")] bool DeathsAndCloseUpSeparateFromDisplacement);

/// <summary>
/// formation.json 绑定模型（data_schema §3.6 唯一权威；字段逐键对齐）。
/// 解析/校验 fail-fast：缺文件 / JSON 语法错 / 校验失败 → 抛异常（data_schema §4.1/§4.2）。
/// </summary>
public sealed record FormationConfig(
    [property: JsonPropertyName("player")] SlotLayoutConfig Player,
    [property: JsonPropertyName("enemy")] SlotLayoutConfig Enemy,
    [property: JsonPropertyName("numeration")] string Numeration,
    [property: JsonPropertyName("initial_roster")] SideRosterConfig InitialRoster,
    [property: JsonPropertyName("obstacles")] IReadOnlyList<ObstacleConfig> Obstacles,
    [property: JsonPropertyName("rules")] FormationRulesConfig Rules)
{
    public const string ResPath = "res://data/formation.json";
    public const string ExpectedNumeration = "center_outward";

    /// <summary>解析并校验（P2 同级校验：槽位范围/唯一/编成与障碍不重叠/rules 键齐全）。</summary>
    public static FormationConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        FormationConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<FormationConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
    };

    private static void Validate(FormationConfig cfg)
    {
        if (cfg.Player is null || cfg.Enemy is null || cfg.InitialRoster is null
            || cfg.Obstacles is null || cfg.Rules is null)
        {
            throw new InvalidDataException($"{ResPath}: 必填段缺失（player/enemy/initial_roster/obstacles/rules）。");
        }

        if (cfg.Numeration != ExpectedNumeration)
        {
            throw new InvalidDataException(
                $"{ResPath}: numeration 必须为 \"{ExpectedNumeration}\"，实际 \"{cfg.Numeration}\"。");
        }

        ValidateLayout("player", cfg.Player);
        ValidateLayout("enemy", cfg.Enemy);
        if (cfg.Enemy.SupportSlots is { Count: > 0 })
        {
            throw new InvalidDataException($"{ResPath}: 敌方无支援位（enemy.md §1），support_slots 必须为空。");
        }

        ValidateRoster("player", cfg.Player.SlotCount, cfg.InitialRoster.Player);
        ValidateRoster("enemy", cfg.Enemy.SlotCount, cfg.InitialRoster.Enemy);

        var playerRosterSlots = new HashSet<int>(cfg.InitialRoster.Player.Select(e => e.Slot));
        var enemyRosterSlots = new HashSet<int>(cfg.InitialRoster.Enemy.Select(e => e.Slot));
        var playerObstacleSlots = new List<int>();
        var enemyObstacleSlots = new List<int>();

        foreach (ObstacleConfig o in cfg.Obstacles)
        {
            if (o.Side is not ("player" or "enemy"))
            {
                throw new InvalidDataException($"{ResPath}: obstacles[].side 非法 \"{o.Side}\"（player/enemy）。");
            }

            int max = o.Side == "player" ? cfg.Player.SlotCount : cfg.Enemy.SlotCount;
            if (o.Slot < 1 || o.Slot > max)
            {
                throw new InvalidDataException($"{ResPath}: obstacles[] slot {o.Slot} 越界 [1, {max}]（side={o.Side}）。");
            }

            if (o.Side == "player")
            {
                if (!playerRosterSlots.Add(o.Slot))
                {
                    throw new InvalidDataException($"{ResPath}: 障碍与编成重叠（player 槽 {o.Slot}）。");
                }

                playerObstacleSlots.Add(o.Slot);
            }
            else
            {
                if (!enemyRosterSlots.Add(o.Slot))
                {
                    throw new InvalidDataException($"{ResPath}: 障碍与编成重叠（enemy 槽 {o.Slot}）。");
                }

                enemyObstacleSlots.Add(o.Slot);
            }
        }

        // rules 九键缺一即校验失败（结构完整性，data_schema §3.6）。
        FormationRulesConfig r = cfg.Rules;
        if (!r.BoundaryAsHardWall || !r.DisplacementOnlyViaSwapChain || !r.ObstacleSwapsLikeUnit
            || !r.CloseUpOnDeathImmediate || !r.CloseUpIgnoresObstacle || !r.CloseUpEnemySymmetric
            || !r.SwapPlayerInitiated || !r.SlotThreeState || !r.DeathsAndCloseUpSeparateFromDisplacement)
        {
            throw new InvalidDataException($"{ResPath}: rules 九键必须均为 true（data_schema §3.6）。");
        }
    }

    private static void ValidateLayout(string side, SlotLayoutConfig layout)
    {
        if (layout is null)
        {
            throw new InvalidDataException($"{ResPath}: {side} 段缺失。");
        }

        if (layout.SlotCount <= 0)
        {
            throw new InvalidDataException($"{ResPath}: {side}.slot_count 必须 > 0。");
        }

        if (layout.CombatSlots < 0 || layout.CombatSlots > layout.SlotCount)
        {
            throw new InvalidDataException($"{ResPath}: {side}.combat_slots 越界 [0, {layout.SlotCount}]。");
        }

        foreach (int slot in layout.SupportSlots)
        {
            if (slot < 1 || slot > layout.SlotCount)
            {
                throw new InvalidDataException($"{ResPath}: {side}.support_slots 元素 {slot} 越界 [1, {layout.SlotCount}]。");
            }
        }
    }

    private static void ValidateRoster(string side, int slotCount, IReadOnlyList<RosterEntryConfig> roster)
    {
        if (roster is null)
        {
            throw new InvalidDataException($"{ResPath}: initial_roster.{side} 缺失。");
        }

        var seen = new HashSet<int>();
        foreach (RosterEntryConfig e in roster)
        {
            if (e.Slot < 1 || e.Slot > slotCount)
            {
                throw new InvalidDataException($"{ResPath}: initial_roster.{side} 槽 {e.Slot} 越界 [1, {slotCount}]。");
            }

            if (!seen.Add(e.Slot))
            {
                throw new InvalidDataException($"{ResPath}: initial_roster.{side} 槽 {e.Slot} 重复。");
            }

            if (string.IsNullOrWhiteSpace(e.Unit))
            {
                throw new InvalidDataException($"{ResPath}: initial_roster.{side} 槽 {e.Slot} 的 unit 为空（须为 units.json 原型 id）。");
            }
        }
    }
}