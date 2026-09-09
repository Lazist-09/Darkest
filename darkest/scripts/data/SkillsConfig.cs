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

public enum SkillTargetScope { Slots, Self, AnyAlly, Team, AdjacentAllyAndSelf }
public enum SkillDamageAxis { Physical, Mental, None }
public enum SkillRangeAxis { Melee, Ranged, None }
public enum FuncTag { Output, Control, Displacement, Support, Heal, Aoe, Debuff }
public enum UseLimitType { None, Cooldown, PerBattle, EveryNRounds }
public enum DisplacementType { Push, Pull, SelfForward, SelfBackward }
public enum SkillEffectType { Stun, Taunt, Bleed, StatMod, Shield, GuardAttach, NextAttackBoost }
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
    [property: JsonPropertyName("slots")] IReadOnlyList<int>? Slots);

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
    [property: JsonPropertyName("heal_fixed")] int? HealFixed = null,
    [property: JsonPropertyName("self_damage_fixed")] int? SelfDamageFixed = null);

/// <summary>skills.json 根模型 + fail-fast 校验（P2/O-24/O-16/43 断言；P1/P3 跨文件在 Validators）。</summary>
public sealed record SkillsConfig(
    [property: JsonPropertyName("skills")] IReadOnlyList<SkillTemplateConfig> Skills)
{
    public const string ResPath = "res://data/skills.json";
    public const int ExpectedCount = 43; // 36 我方 + 7 敌方（O-16：README「42」为笔误）

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

    public static SkillsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

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
            ("adjacent_ally_and_self", SkillTargetScope.AdjacentAllyAndSelf)));
        o.Converters.Add(new LowerEnumJsonConverter<SkillDamageAxis>(
            ("physical", SkillDamageAxis.Physical), ("mental", SkillDamageAxis.Mental), ("none", SkillDamageAxis.None)));
        o.Converters.Add(new LowerEnumJsonConverter<SkillRangeAxis>(
            ("melee", SkillRangeAxis.Melee), ("ranged", SkillRangeAxis.Ranged), ("none", SkillRangeAxis.None)));
        o.Converters.Add(new LowerEnumJsonConverter<FuncTag>(
            ("output", FuncTag.Output), ("control", FuncTag.Control), ("displacement", FuncTag.Displacement),
            ("support", FuncTag.Support), ("heal", FuncTag.Heal), ("aoe", FuncTag.Aoe), ("debuff", FuncTag.Debuff)));
        o.Converters.Add(new LowerEnumJsonConverter<UseLimitType>(
            ("none", UseLimitType.None), ("cooldown", UseLimitType.Cooldown),
            ("per_battle", UseLimitType.PerBattle), ("every_n_rounds", UseLimitType.EveryNRounds)));
        o.Converters.Add(new LowerEnumJsonConverter<DisplacementType>(
            ("push", DisplacementType.Push), ("pull", DisplacementType.Pull),
            ("self_forward", DisplacementType.SelfForward), ("self_backward", DisplacementType.SelfBackward)));
        o.Converters.Add(new LowerEnumJsonConverter<SkillEffectType>(
            ("stun", SkillEffectType.Stun), ("taunt", SkillEffectType.Taunt), ("bleed", SkillEffectType.Bleed),
            ("stat_mod", SkillEffectType.StatMod), ("shield", SkillEffectType.Shield),
            ("guard_attach", SkillEffectType.GuardAttach), ("next_attack_boost", SkillEffectType.NextAttackBoost)));
        o.Converters.Add(new LowerEnumJsonConverter<MoraleEffectScope>(
            ("self", MoraleEffectScope.Self), ("targets", MoraleEffectScope.Targets),
            ("team", MoraleEffectScope.Team), ("ally_targets", MoraleEffectScope.AllyTargets)));
        o.Converters.Add(new LowerEnumJsonConverter<DamageSegmentType>(
            ("flat", DamageSegmentType.Flat), ("missing_hp", DamageSegmentType.MissingHp)));
        o.Converters.Add(new SelfSlotsConverter());
        return o;
    }

    private static void Validate(SkillsConfig cfg)
    {
        if (cfg.Skills is null || cfg.Skills.Count != ExpectedCount)
        {
            throw new InvalidDataException(
                $"{ResPath}: 技能数必须为 {ExpectedCount}（O-16：36 我方 + 7 敌方），实际 {cfg.Skills?.Count}。");
        }

        var ids = new HashSet<string>();
        foreach (SkillTemplateConfig s in cfg.Skills)
        {
            if (string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id))
            {
                throw new InvalidDataException($"{ResPath}: 技能 id \"{s.Id}\" 缺失或重复（P1）。");
            }

            if (string.IsNullOrWhiteSpace(s.Name) || s.OwnerUnit is not ("warrior" or "tank" or "medic" or "commissar" or "melee_soldier" or "ranged_archer" or "caster"))
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" owner_unit 非法（P1）。");
            }

            ValidateTarget(s);
            ValidateSelfSlotsRange(s);
            ValidateEffectPairing(s);
            ValidateUseLimit(s);
            ValidateSegments(s);
        }

        ValidateCounts(cfg);
    }

    private static void ValidateTarget(SkillTemplateConfig s)
    {
        TargetSpec t = s.Target;
        if (t.Scope == SkillTargetScope.Slots)
        {
            int max = t.Side switch { "player" => 6, "enemy" => 4, _ => 0 };
            if (max == 0)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" target.side 必须为 player/enemy（P2）。");
            }

            if (t.Slots is null || t.Slots.Count == 0 || t.Slots.Any(pos => pos is < 1 or > 6))
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" target.slots 越界（P2；敌方 ≤4、我方 ≤6）。");
            }
        }
    }

    private static void ValidateSelfSlotsRange(SkillTemplateConfig s)
    {
        if (s.SelfSlots.IsAll)
        {
            return;
        }

        int max = PlayerOwner(s.OwnerUnit) ? 6 : 4; // 我方原型 1~6、敌方原型 1~4
        if (s.SelfSlots.Slots.Any(pos => pos < 1 || pos > max))
        {
            throw new InvalidDataException(
                $"{ResPath}: \"{s.Id}\" self_slots 越界（{s.OwnerUnit} 侧合法 [1,{max}]，P2 同级）。");
        }
    }

    private static bool PlayerOwner(string owner)
        => owner is "warrior" or "tank" or "medic" or "commissar";

    private static void ValidateEffectPairing(SkillTemplateConfig s)
    {
        // O-24 成对规则：probability 与 resist_axis 必须同有同无（无概率 = 直挂、不过抗性）
        foreach (EffectSpec e in s.Effects)
        {
            bool hasP = e.Probability is not null;
            bool hasR = !string.IsNullOrEmpty(e.ResistAxis);
            if (hasP != hasR)
            {
                throw new InvalidDataException(
                    $"{ResPath}: \"{s.Id}\" effects[{e.Type}] 的 probability 与 resist_axis 必须同有同无（O-24）。");
            }
        }
    }

    private static void ValidateUseLimit(SkillTemplateConfig s)
    {
        UseLimitSpec u = s.UseLimit;
        if (u.Type == UseLimitType.EveryNRounds)
        {
            throw new InvalidDataException($"{ResPath}: \"{s.Id}\" every_n_rounds 为模板预留，切片不得启用。");
        }

        if ((u.Type is UseLimitType.Cooldown or UseLimitType.PerBattle) && (u.Value is null or <= 0))
        {
            throw new InvalidDataException($"{ResPath}: \"{s.Id}\" use_limit.value 必须 > 0。");
        }
    }

    private static void ValidateSegments(SkillTemplateConfig s)
    {
        if (s.Damage is null)
        {
            return;
        }

        foreach (DamageSegment seg in s.Damage.Segments)
        {
            if (seg.Type == DamageSegmentType.Flat && seg.Multiplier is null)
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" flat 段缺 multiplier。");
            }

            if (seg.Type == DamageSegmentType.MissingHp && (seg.Base is null || seg.Coefficient is null))
            {
                throw new InvalidDataException($"{ResPath}: \"{s.Id}\" missing_hp 段缺 base/coefficient。");
            }
        }
    }

    private static void ValidateCounts(SkillsConfig cfg)
    {
        int[] player = { 0, 0, 0, 0 };
        int[] enemy = { 0, 0, 0 };
        foreach (SkillTemplateConfig s in cfg.Skills)
        {
            switch (s.OwnerUnit)
            {
                case "warrior": player[0]++; break;
                case "tank": player[1]++; break;
                case "medic": player[2]++; break;
                case "commissar": player[3]++; break;
                case "melee_soldier": enemy[0]++; break;
                case "ranged_archer": enemy[1]++; break;
                case "caster": enemy[2]++; break;
            }
        }

        if (player.Any(c => c != 9) || enemy is not [2, 3, 2])
        {
            throw new InvalidDataException(
                $"{ResPath}: 各原型技能数必须为 9/9/9/9 + 2/3/2（实际 {string.Join("/", player)} + {string.Join("/", enemy)}）。");
        }
    }

    // ------------------------------------------------------------------
    // 转换器
    // ------------------------------------------------------------------

    private sealed class LowerEnumJsonConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        private readonly Dictionary<string, T> _read = new();
        private readonly Dictionary<T, string> _write = new();

        public LowerEnumJsonConverter(params (string Word, T Value)[] vocab)
        {
            foreach ((string word, T value) in vocab)
            {
                _read[word] = value;
                _write[value] = word;
            }
        }

        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            string? raw = reader.GetString();
            if (raw is not null && _read.TryGetValue(raw, out T value))
            {
                return value;
            }

            throw new JsonException($"未知枚举值 \"{raw}\"（{typeof(T).Name}）。");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(_write[value]);
    }

    private sealed class SelfSlotsConverter : JsonConverter<SelfSlots>
    {
        public override SelfSlots Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? s = reader.GetString();
                if (s == "all")
                {
                    return new SelfSlots(IsAll: true, Slots: Array.Empty<int>());
                }

                throw new JsonException($"self_slots 字符串只能为 \"all\"，实际 \"{s}\"。");
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = JsonSerializer.Deserialize<List<int>>(ref reader, options) ?? new List<int>();
                return new SelfSlots(IsAll: false, Slots: list);
            }

            throw new JsonException("self_slots 必须为 \"all\" 或位置数组。");
        }

        public override void Write(Utf8JsonWriter writer, SelfSlots value, JsonSerializerOptions options)
        {
            if (value.IsAll)
            {
                writer.WriteStringValue("all");
            }
            else
            {
                writer.WriteStartArray();
                foreach (int pos in value.Slots)
                {
                    writer.WriteNumberValue(pos);
                }

                writer.WriteEndArray();
            }
        }
    }
}