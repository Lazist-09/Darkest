using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

// ----------------------------------------------------------------------
// 枚举（data_schema §2.1，JSON 存小写 ASCII；B1 数据层类型，sim 侧可读）
// ----------------------------------------------------------------------

public enum SkillTargetScope { Slots, Self, AnyAlly, Team, AdjacentAllyAndSelf, MoveRange }
public enum SkillDamageAxis { Physical, Mental, None }
public enum SkillRangeAxis { Melee, Ranged, None }
// 🔴 C-1（v0.99）：`IgnoreStealth` = 原版 `.ignore_stealth true`（playwright 投掷油壶/地狱之炎）——
//    语义 = 【穿透潜行】：该技能无视"潜行者不可被直接指定"，且命中即解除其潜行（同 .unstealth）
public enum FuncTag { Output, Control, Displacement, Support, Heal, Aoe, Debuff, IgnoreStealth }
public enum UseLimitType { None, Cooldown, PerBattle, EveryNRounds }
public enum DisplacementType { Push, Pull, SelfForward, SelfBackward }
public enum SkillEffectType { Stun, Taunt, Bleed, StatMod, Shield, GuardAttach, NextAttackBoost, Mark, Stealth }
public enum MoraleEffectScope { Self, Targets, Team, AllyTargets }
public enum DamageSegmentType { Flat, MissingHp }

// ----------------------------------------------------------------------
// 子结构（data_schema §3.2 逐 key）
// ----------------------------------------------------------------------

/// <summary>self_slots："all" 或位置数组。</summary>
public sealed record SelfSlots(bool IsAll, IReadOnlyList<int> Slots)
{
    public bool Allows(int pos) => IsAll || Slots.Contains(pos);
}

public sealed record TargetSpec(
    [property: JsonPropertyName("scope")] SkillTargetScope Scope,
    [property: JsonPropertyName("side")] string? Side,
    [property: JsonPropertyName("slots")] IReadOnlyList<int>? Slots,
    [property: JsonPropertyName("distance")] int? Distance = null);

public sealed record DamageSegment(
    [property: JsonPropertyName("type")] DamageSegmentType Type,
    [property: JsonPropertyName("multiplier")] double? Multiplier,
    [property: JsonPropertyName("base")] double? Base,
    [property: JsonPropertyName("coefficient")] double? Coefficient);
public sealed record DamageSpec([property: JsonPropertyName("segments")] IReadOnlyList<DamageSegment> Segments);

public sealed record EffectSpec(
    [property: JsonPropertyName("type")] SkillEffectType Type,
    [property: JsonPropertyName("probability")] int? Probability,
    [property: JsonPropertyName("resist_axis")] string? ResistAxis,
    [property: JsonPropertyName("apply_to")] string? ApplyTo,
    [property: JsonPropertyName("stat")] string? Stat,
    [property: JsonPropertyName("delta")] int? Delta,
    [property: JsonPropertyName("duration_rounds")] int? DurationRounds,
    [property: JsonPropertyName("charges")] int? Charges);

public sealed record DisplacementSpec(
    [property: JsonPropertyName("type")] DisplacementType Type,
    [property: JsonPropertyName("count")] int Count);

public sealed record UseLimitSpec(
    [property: JsonPropertyName("type")] UseLimitType Type,
    [property: JsonPropertyName("value")] int? Value);

public sealed record MoraleEffectSpec(
    [property: JsonPropertyName("scope")] MoraleEffectScope Scope,
    [property: JsonPropertyName("delta")] int Delta);

/// <summary>技能模板（data_schema §3.2 13 字段 + 标识 + 补充字段；技能数据唯一来源）。</summary>
public sealed record SkillTemplateConfig(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("owner_unit")] string OwnerUnit,
    [property: JsonPropertyName("self_slots")] SelfSlots SelfSlots,
    [property: JsonPropertyName("target")] TargetSpec Target,
    [property: JsonPropertyName("damage")] DamageSpec? Damage,
    [property: JsonPropertyName("hit_mod")] int HitMod,
    [property: JsonPropertyName("crit_mod")] int CritMod,
    [property: JsonPropertyName("effects")] IReadOnlyList<EffectSpec> Effects,
    [property: JsonPropertyName("displacement")] DisplacementSpec? Displacement,
    [property: JsonPropertyName("use_limit")] UseLimitSpec UseLimit,
    [property: JsonPropertyName("morale_effects")] IReadOnlyList<MoraleEffectSpec> MoraleEffects,
    [property: JsonPropertyName("tags")] IReadOnlyList<FuncTag> Tags,
    [property: JsonPropertyName("range_axis")] SkillRangeAxis RangeAxis,
    [property: JsonPropertyName("damage_axis")] SkillDamageAxis DamageAxis,
    /// <summary>
    /// 🆕 **M1c 阶段 3 · 技能 `dmg%`**（策划给的值；`smite 0` / `zealous_accusation −40` ✓ 是一手例）。
    /// 🔴 **`null` = 仍走旧模型**（`attack × 段倍率`）⇒ **默认零行为** ✓（阶段 3 才逐个填 ✓）
    /// ⚠️ 激活条件：填了它 + **阶段 3 的伤害路径接上**（见 `WeaponBaseDamage` 的两个前置 ✓）才会生效 ✓
    /// </summary>
    [property: JsonPropertyName("dmg_pct")] int? DmgPct = null,

    /// <summary>
    /// 🆕 **A2/A3 · `dmg_pct` 的【出处】（纪律 AT/AZ：契约里的断言要标出处）** ✓
    ///
    /// 🔴 **为什么它必须是个【被声明的字段】而不是"随手加的 JSON 键"**：
    ///    它是**出处标注**（provenance），**不是玩法字段** —— 但**不声明**的话，
    ///    死数据门禁会把它报成"疑似死数据"（实测：**30 处**）⚠️
    ///    ⇒ ✅ 声明 = **把"这个键是有意加的"写进契约**，而不是让门禁猜 ✓
    ///    📌 同族：**红线 21 的反面** —— 数据键必须**有一个说得清的身份** ✓
    ///
    /// 取值：`ref:<Hero>/<skill>`（有参考出处，点名来源技能）· `none`（显式声明"我们自加、无来源"）✓
    /// 🔴 **`none` 与"忘了填"是两件事** —— 前者是**声明**，后者是**缺陷** ⇒ 靠本字段可分辨 ✓
    /// </summary>
    [property: JsonPropertyName("_dmg_pct_source")] string? DmgPctSource = null,

    /// <summary>
    /// 🆕 **A 维：值的来源**（策划 `#475` 立的两维之一）✓
    /// `ref` = 有参考出处 · `none` = 我们自加、无来源（⇒ 归 `§39` 解冻清单）✓
    /// ⚠️ 与 <see cref="Origin"/> 是**两个维度**：本字段答"**值**哪来的"，`origin` 答"**这条技能**归谁" ✓
    /// </summary>
    [property: JsonPropertyName("value_source")] string? ValueSource = null,

    /// <summary>🆕 **B 维：归属** —— `dd1` = 对应到原版同职业技能 · `ours` = 我们自加 ✓</summary>
    [property: JsonPropertyName("origin")] string? Origin = null,

    [property: JsonPropertyName("heal_fixed")] int? HealFixed = null,
    [property: JsonPropertyName("self_damage_fixed")] int? SelfDamageFixed = null,
    [property: JsonPropertyName("pool_external")] bool PoolExternal = false,
    [property: JsonPropertyName("requires")] RequiresSpec? Requires = null,
    [property: JsonPropertyName("bonus_vs_marked_percent")] int BonusVsMarkedPercent = 0,
    [property: JsonPropertyName("support_point_cost")] int? SupportPointCost = null);

/// <summary>D5（#207）技能前置条件：不满足 → 灰显 + tooltip；不改携带集（O-50 契约不受影响）。</summary>
public sealed record RequiresSpec(
    [property: JsonPropertyName("self_hp_below_percent")] int? SelfHpBelowPercent = null,
    [property: JsonPropertyName("target_hp_below_percent")] int? TargetHpBelowPercent = null,
    [property: JsonPropertyName("self_weak")] bool? SelfWeak = null,
    [property: JsonPropertyName("self_deaths_door")] bool? SelfDeathsDoor = null);

/// <summary>skills.json 根模型 + fail-fast 校验（P2/O-24/O-16/43 断言；P1/P3 跨文件在 Validators）。</summary>
public sealed partial record SkillsConfig(
    [property: JsonPropertyName("skills")] IReadOnlyList<SkillTemplateConfig> Skills)
{
    public const string ResPath = "res://data/skills.json";
    public const int ExpectedCount = 44; // 36 池内 + 1 通用池外 move + 7 敌方（F1/#191）

    /// <summary>按 id 取技能（缺失抛异常——fail-fast）。</summary>
    public SkillTemplateConfig Get(string id)
    {
        foreach (SkillTemplateConfig s in Skills)
        {
            if (s.Id == id)
            {
                return s;
            }
        }

        throw new InvalidDataException($"{ResPath}: 引用了不存在的技能 \"{id}\"。");
    }

    public static SkillsConfig Parse(string json, IReadOnlyCollection<string>? playerArchetypes = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        _playerSet = playerArchetypes; // F1（#190）：注入数据派生的我方原型集合（null = 旧白名单行为，兼容既有测试）

        SkillsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<SkillsConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{ResPath}: 内容为空（null）。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 语法/枚举错误 —— {ex.Message}");
        }

        Validate(cfg);
        return cfg;
    }

    /// <summary>用同一份 options（含枚举/self_slots 转换器）序列化——测试反例构造用。</summary>
    public static string Serialize(SkillsConfig cfg)
        => JsonSerializer.Serialize(cfg, JsonOptions);

    private static JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = false,
        };
        o.Converters.Add(new LowerEnumJsonConverter<SkillTargetScope>(
            ("slots", SkillTargetScope.Slots), ("self", SkillTargetScope.Self),
            ("any_ally", SkillTargetScope.AnyAlly), ("team", SkillTargetScope.Team),
            ("adjacent_ally_and_self", SkillTargetScope.AdjacentAllyAndSelf),
            ("move_range", SkillTargetScope.MoveRange)));
        o.Converters.Add(new LowerEnumJsonConverter<SkillDamageAxis>(
            ("physical", SkillDamageAxis.Physical), ("mental", SkillDamageAxis.Mental), ("none", SkillDamageAxis.None)));
        o.Converters.Add(new LowerEnumJsonConverter<SkillRangeAxis>(
            ("melee", SkillRangeAxis.Melee), ("ranged", SkillRangeAxis.Ranged), ("none", SkillRangeAxis.None)));
        o.Converters.Add(new LowerEnumJsonConverter<FuncTag>(
            ("output", FuncTag.Output), ("control", FuncTag.Control), ("displacement", FuncTag.Displacement),
            ("support", FuncTag.Support), ("heal", FuncTag.Heal), ("aoe", FuncTag.Aoe), ("debuff", FuncTag.Debuff),
            ("ignore_stealth", FuncTag.IgnoreStealth))); // C-1（v0.99）：原版 `.ignore_stealth true`
        o.Converters.Add(new LowerEnumJsonConverter<UseLimitType>(
            ("none", UseLimitType.None), ("cooldown", UseLimitType.Cooldown),
            ("per_battle", UseLimitType.PerBattle), ("every_n_rounds", UseLimitType.EveryNRounds)));
        o.Converters.Add(new LowerEnumJsonConverter<DisplacementType>(
            ("push", DisplacementType.Push), ("pull", DisplacementType.Pull),
            ("self_forward", DisplacementType.SelfForward), ("self_backward", DisplacementType.SelfBackward)));
        o.Converters.Add(new LowerEnumJsonConverter<SkillEffectType>(
            ("stun", SkillEffectType.Stun), ("taunt", SkillEffectType.Taunt), ("bleed", SkillEffectType.Bleed),
            ("stat_mod", SkillEffectType.StatMod), ("shield", SkillEffectType.Shield),
            ("guard_attach", SkillEffectType.GuardAttach), ("next_attack_boost", SkillEffectType.NextAttackBoost),
            ("mark", SkillEffectType.Mark)));
        o.Converters.Add(new LowerEnumJsonConverter<MoraleEffectScope>(
            ("self", MoraleEffectScope.Self), ("targets", MoraleEffectScope.Targets),
            ("team", MoraleEffectScope.Team), ("ally_targets", MoraleEffectScope.AllyTargets)));
        o.Converters.Add(new LowerEnumJsonConverter<DamageSegmentType>(
            ("flat", DamageSegmentType.Flat), ("missing_hp", DamageSegmentType.MissingHp)));
        o.Converters.Add(new SelfSlotsConverter());
        return o;
    }
}
