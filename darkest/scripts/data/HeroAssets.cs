using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Data;

/// <summary>锚点（`P31` ③：**必须显式**，不得靠默认值猜）✓</summary>
public sealed record HeroAnchor(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

/// <summary>
/// 🔴 `P31` ④（v1.62）：锚点可以是**对象** `{x,y}`，也可以是**字符串** `"inherit"`（= **有意的继承声明**）✓
/// ⇒ 用**哨兵**表示继承（`NaN`）⇒ 下游用 `IsInherit` 问它，而不是靠"缺省猜" ✓
/// </summary>
public static class HeroAnchorKind
{
    /// <summary>是否为"显式继承"（`"anchor": "inherit"`）✓</summary>
    public static bool IsInherit(HeroAnchor? a) => a is not null && double.IsNaN(a.X) && double.IsNaN(a.Y);
}

/// <summary>把 `"inherit"` 字符串收成哨兵对象（否则反序列化阶段就会失败 ⚠️）✓</summary>
internal sealed class HeroAnchorConverter : JsonConverter<HeroAnchor?>
{
    public override HeroAnchor? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string s = reader.GetString() ?? string.Empty;
            if (string.Equals(s, "inherit", StringComparison.OrdinalIgnoreCase))
            {
                return new HeroAnchor(double.NaN, double.NaN); // 哨兵 = 显式继承 ✓
            }

            throw new JsonException($"`anchor` 只接受对象 {{x,y}} 或字符串 \"inherit\"（实测 \"{s}\"）✓");
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        double x = 0, y = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            string name = reader.GetString() ?? string.Empty;
            reader.Read();
            if (string.Equals(name, "x", StringComparison.OrdinalIgnoreCase))
            {
                x = reader.GetDouble();
            }
            else if (string.Equals(name, "y", StringComparison.OrdinalIgnoreCase))
            {
                y = reader.GetDouble();
            }
        }

        return new HeroAnchor(x, y);
    }

    public override void Write(Utf8JsonWriter writer, HeroAnchor? value, JsonSerializerOptions options)
        => throw new NotSupportedException("只读加载器：不写回 `hero.json` ✓");
}

/// <summary>
/// 一个**动作槽**（`hero_assets.md §3.1`：接口就定在【动作槽 → 帧序列 + 锚点】这一层 ⇒ **不绑定 Spine** ✓）
/// 🔴 `Frames` 必须是**引用**（路径/资源 id）—— **不得**把帧数据/尺寸拷进 `hero.json`（`P31` ② · 与 `P26`/`P28` 同族）✓
/// </summary>
public sealed record HeroActionSlot(
    [property: JsonPropertyName("frames")] IReadOnlyList<string> Frames,
    [property: JsonPropertyName("fps")] int Fps = 8,
    [property: JsonPropertyName("loop")] bool Loop = true,
    [property: JsonPropertyName("anchor")] HeroAnchor? Anchor = null,
    // 🔴 `H5`：缺槽要能**显式声明**（"这个英雄确实没有这个动作"）⇒ 声明缺失**允许**，但**必须写明原因** ✓
    [property: JsonPropertyName("missing_reason")] string? MissingReason = null)
{
    /// <summary>是否只是"显式声明缺失"（无帧 ⇒ 必须有 `missing_reason`，否则视为配置错误）✓</summary>
    public bool IsDeclaredMissing => Frames is null or { Count: 0 };
}

/// <summary>
/// 🔴 **`hero.json`**（架构 `data_schema` **`P31`** · 策划 `hero_assets.md` · 用户 `#343`）：
/// 英雄资产**接口**（本阶段只用 `.png` 帧序列；Spine/FMOD 等第三方 runtime **本阶段不引入** ✓）
/// 本类 = **内核零 Godot** 的"只读数据 + 加载门禁" ⇒ 表现层（UI）只取帧与锚点 ✓
/// </summary>
public sealed record HeroAssetsConfig(
    [property: JsonPropertyName("archetype")] string Archetype,
    [property: JsonPropertyName("actions")] IReadOnlyDictionary<string, HeroActionSlot> Actions,
    // 🔴 合规三条（`§2`）：占位**必须显式标注**（临时 · 来源 · 无授权 · 不发布）✓
    [property: JsonPropertyName("placeholder")] bool Placeholder = false,
    [property: JsonPropertyName("source")] string? Source = null,
    // 🆕 **名册头像**（策划 `#347`②(b)：已请架构补 `P31`；此处**先按同纪律实现** = **引用** + **允许缺失**）✓
    //    理由：这批素材里**唯一能完整用**的就是 `*_portrait_roster.png`（85×85 独立 PNG，不依赖 Spine）✓
    [property: JsonPropertyName("portrait")] string? Portrait = null,
    // 🔴 `P31` ⑧（v1.64）：**顶层字段**（不进 12 槽表）· 头像**允许缺失，但必须【显式声明】** ⇒ 缺失时根上要有 `missing_reason`（**不得静默留空**）✓
    [property: JsonPropertyName("missing_reason")] string? PortraitMissingReason = null)
{
    public const string ResPath = "res://data/hero.json";

    /// <summary>
    /// 🔴 **根覆盖**（策划 `#347`②(a) 的"鸡生蛋"解法）：占位的 `hero.json` **不能**放 `res://data/`
    /// （那进 git ⇒ 等于**分发**）⇒ 由**调用方显式给根**（`Load(json, root)`）⇒ 正式路径仍走 `ResPath` ✓
    /// ⚠️ 不参与 JSON 序列化（它是**运行时注入**，不是数据字段 ✓）
    /// </summary>
    [JsonIgnore]
    public string? RootOverride { get; init; }
}

/// <summary>
/// 🔴 **英雄资产加载器**（我的分工：内核零 Godot 只读数据；表现层取帧 —— `tasks/hero_asset_interface.md` §3）✓
///
/// 门禁（`P31` ①~③ + 卡 §3"启动报错清单"）—— **缺一条都会让"缺动作"变成"静默不画"** ⚠️：
///   ① **必需 5 槽**（`idle`/`combat`/`attack`/`defend`/`walk`）缺落点 ⇒ 🔴 **报错并【点名】**（`H1`/`H2`）
///   ② 槽名必须 ∈【固定 12 槽表】或形如 `skill.<名>`（技能类按该英雄技能表命名 ⇒ 允许自由命名）⇒ 否则拒绝
///   ③ `frames` 必须是**引用**（路径样式）⇒ 出现内联数据（`base64`/`{`/过长串）⇒ 拒绝
///   ④ **锚点必须显式**（`P31` ③）⇒ 有帧的槽没有 `anchor` ⇒ 拒绝并点名
///   ⑤ "声明缺失"的槽必须有 `missing_reason`（**不许静默留空**）✓
/// </summary>
public static class HeroAssets
{
    /// <summary>
    /// 🔴 **占位根目录**（策划 `#344` §8 采纳的"**唯一能跑的路**"）：
    /// 占位资产**只从这里加载** ⇒ 若有人把占位拷进 `resources/` ⇒ **它不会被加载**（错误做法**跑不通**，而不是"跑得通但违规"）✓
    /// 📌 该目录**也被 gitignore**（不进 git / 不进构建产物 / 不发布 ⇒ 合规三条）✓
    /// </summary>
    public const string PlaceholderRoot = "res://assets/heroes_placeholder";

    /// <summary>正式英雄资产根（**进 git**）；每个原型一个子目录 ✓</summary>
    public const string HeroesRoot = "res://assets/heroes";

    /// <summary>固定 12 槽（`hero_assets.md §2`：必备 5 + 状态 3 + 场景 4）✓</summary>
    public static readonly IReadOnlySet<string> FixedSlots = new HashSet<string>(StringComparer.Ordinal)
    {
        // 必备（战斗必需）5 ✓
        "idle", "combat", "attack", "defend", "walk",
        // 状态类 3 ✓
        "afflicted", "heroic", "flashing",
        // 场景类 4 ✓
        "camp", "investigate", "tracking", "battlefield",
    };

    /// <summary>🔴 **战斗必需的 5 槽**（`H1`：全部必须有落点；缺 ⇒ 报错并**点名**）✓</summary>
    public static readonly IReadOnlyList<string> RequiredSlots =
        new[] { "idle", "combat", "attack", "defend", "walk" };

    /// <summary>技能槽前缀（技能类槽按**该英雄的技能表**命名 ⇒ 形如 `skill.grape` ✓）</summary>
    public const string SkillSlotPrefix = "skill.";

    public static HeroAssetsConfig Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{HeroAssetsConfig.ResPath}: 内容为空。");
        }

        HeroAssetsConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<HeroAssetsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new HeroAnchorConverter() }, // 🔴 `P31` ④：`"anchor": "inherit"` 也要能收 ✓
            }) ?? throw new InvalidDataException($"{HeroAssetsConfig.ResPath}: 反序列化得到 null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{HeroAssetsConfig.ResPath}: JSON 解析失败（{ex.Message}）。", ex);
        }

        if (string.IsNullOrWhiteSpace(cfg.Archetype))
        {
            throw new InvalidDataException($"{HeroAssetsConfig.ResPath}: `archetype` 必填（接口要能【脱离素材独立解释】，`H6`）✓");
        }

        if (cfg.Actions is null || cfg.Actions.Count == 0)
        {
            throw new InvalidDataException($"{HeroAssetsConfig.ResPath}: `actions` 为空 ⇒ 拒绝加载。");
        }

        // ① 必需 5 槽：缺 ⇒ **点名**（不是静默不画）✓
        var missing = new List<string>();
        foreach (string slot in RequiredSlots)
        {
            if (!cfg.Actions.TryGetValue(slot, out HeroActionSlot? s) || s.IsDeclaredMissing)
            {
                missing.Add(slot);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidDataException(
                $"{HeroAssetsConfig.ResPath}（{cfg.Archetype}）: 🔴 **战斗必需的 5 槽缺落点** → " +
                $"[{string.Join(" / ", missing)}] ⇒ 拒绝加载（`P31` ① / `H1`：不许静默不画）✓");
        }

        foreach ((string name, HeroActionSlot slot) in cfg.Actions)
        {
            // ② 槽名：固定 12 槽 或 `skill.<名>`
            bool known = FixedSlots.Contains(name) || name.StartsWith(SkillSlotPrefix, StringComparison.Ordinal);
            if (!known)
            {
                throw new InvalidDataException(
                    $"{HeroAssetsConfig.ResPath}: 槽名 \"{name}\" 不在【固定 12 槽表】也不形如 `{SkillSlotPrefix}<名>` " +
                    "⇒ 拒绝加载（`P31` ②：槽表是接口，不许随手新增）✓");
            }

            // ⑤ 声明缺失 ⇒ 必须写原因（不许静默留空）
            if (slot.IsDeclaredMissing)
            {
                if (string.IsNullOrWhiteSpace(slot.MissingReason))
                {
                    throw new InvalidDataException(
                        $"{HeroAssetsConfig.ResPath}: 槽 \"{name}\" 没有帧 ⇒ 必须用 `missing_reason` **显式声明缺失**" +
                        "（`H5` / `§2`：不许静默留空、也不许靠默认值补）✓");
                }

                continue;
            }

            // ③ `frames` 必须是引用（路径样式）⇒ 出现内联数据 ⇒ 拒绝
            foreach (string f in slot.Frames)
            {
                if (!IsReference(f))
                {
                    throw new InvalidDataException(
                        $"{HeroAssetsConfig.ResPath}: 槽 \"{name}\" 的帧 \"{Trim(f)}\" **不是引用**（`P31` ②：`frames` 必须是路径/资源 id，" +
                        "不得把帧数据/尺寸拷进 `hero.json`）⇒ 拒绝加载 ✓");
                }
            }

            // ④ 锚点必须显式（`P31` ④ · v1.62 裁定）：**写 `anchor` 对象** 或 **显式写 `"anchor": "inherit"`** ⇒ 二者皆可 ✓
            //    —— "省"必须是【有意的声明】，不是【缺省的猜测】（否则锚点不一致会肉眼才发现错位/抖动）✓
            if (slot.Anchor is null && !HeroAnchorKind.IsInherit(slot.Anchor) && !AnchorDeclaredInherit(json, name))
            {
                throw new InvalidDataException(
                    $"{HeroAssetsConfig.ResPath}: 槽 \"{name}\" **没有显式回答锚点** ⇒ 拒绝加载" +
                    "（`P31` ④：要么写 `anchor` 对象，要么写 `\"anchor\": \"inherit\"`）✓");
            }
        }

        // 🔴 `P31` ⑧（v1.64 · **顶层字段** ⇒ **单独一条校验**，不进 12 槽表）：
        //    · 给了值 ⇒ 必须是**引用** ✓
        //    · **允许缺失** ⇒ **但必须【显式声明】**（根上写 `missing_reason`）⇒ **不得静默留空** ✓
        bool hasPortrait = cfg.Portrait is { Length: > 0 };
        if (hasPortrait && !IsReference(cfg.Portrait!))
        {
            throw new InvalidDataException(
                $"{HeroAssetsConfig.ResPath}: `portrait` 的 \"{Trim(cfg.Portrait!)}\" **不是引用**（与 `frames` 同纪律：路径/资源 id）✓");
        }

        if (!hasPortrait && string.IsNullOrWhiteSpace(cfg.PortraitMissingReason))
        {
            throw new InvalidDataException(
                $"{HeroAssetsConfig.ResPath}: `portrait` 缺失 ⇒ 🔴 **必须在根上写 `missing_reason` 显式声明**" +
                "（`P31` ⑧：允许缺失，但**不得静默留空**）✓");
        }

        // 合规：占位必须显式标注来源（临时 · 来源 · 无授权 · 不发布）✓
        if (cfg.Placeholder && string.IsNullOrWhiteSpace(cfg.Source))
        {
            throw new InvalidDataException(
                $"{HeroAssetsConfig.ResPath}: `placeholder = true` 时必须写 `source`（合规三条：**不发布 + 显式标注**）✓");
        }

        return cfg;
    }

    /// <summary>该槽的帧路径（**表现层取帧**用；已保证是引用）✓</summary>
    public static IReadOnlyList<string> FramesOf(HeroAssetsConfig cfg, string slot)
        => cfg.Actions.TryGetValue(slot, out HeroActionSlot? s) && !s.IsDeclaredMissing
            ? s.Frames
            : Array.Empty<string>();

    /// <summary>
    /// 资产根：**调用方给了根** ⇒ 用它（占位/测试）✓；否则 占位 ⇒ `PlaceholderRoot`（**只从这里加载**）；
    /// 正式 ⇒ `HeroesRoot/<archetype>` ✓
    /// </summary>
    public static string RootFor(HeroAssetsConfig cfg)
        => cfg.RootOverride ?? (cfg.Placeholder ? PlaceholderRoot : $"{HeroesRoot}/{cfg.Archetype}");

    /// <summary>
    /// 🆕 **带根加载**（策划 `#347`②(a) / 卡 `#348`①）：`Load(json)` = 正式（根由 `RootFor` 推）；
    /// `Load(json, root)` = **由调用方给根**（占位或测试）⇒ 解决"不知道根就不知道去哪读"的鸡生蛋 ✓
    /// </summary>
    public static HeroAssetsConfig Load(string json, string? root = null)
    {
        HeroAssetsConfig cfg = Parse(json);
        return root is null ? cfg : cfg with { RootOverride = root };
    }

    /// <summary>把**引用**解析成完整路径（表现层据此取帧/头像：`<根>/<引用>`）✓</summary>
    public static string Resolve(HeroAssetsConfig cfg, string reference)
        => $"{RootFor(cfg)}/{reference}";

    /// <summary>名册头像引用：**允许缺失**（缺 ⇒ 空串，表现层不画；但**不静默造一个默认图**）✓</summary>
    public static string PortraitRef(HeroAssetsConfig cfg) => cfg.Portrait ?? string.Empty;

    /// <summary>
    /// 🔴 `P31` ④（v1.62）：该槽是否**显式写了** `"anchor": "inherit"`（= **有意的继承声明**）——
    /// 因为 `Anchor` 是对象类型，JSON 里的字符串形式反序列化不到 ⇒ 必须**回看原始 JSON** ✓
    /// </summary>
    public static bool AnchorDeclaredInherit(string json, string slot)
    {
        try
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("actions", out System.Text.Json.JsonElement actions)
                && actions.TryGetProperty(slot, out System.Text.Json.JsonElement s)
                && s.TryGetProperty("anchor", out System.Text.Json.JsonElement a)
                && a.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return string.Equals(a.GetString(), "inherit", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return false; // JSON 本身的问题由主解析路径报（这里不抢答）✓
        }

        return false;
    }

    private static string Trim(string s) => s.Length <= 24 ? s : s[..24] + "…";

    /// <summary>
    /// 「**是不是引用**」的统一判据（`frames` 与 `portrait` **共用** ⇒ 两处纪律不会各自漂移）✓
    /// 反例：内联 `base64` · 内联对象/尺寸（含 `{`）· 超长串 · 既无 `.` 又无 `/` 的裸词 ✓
    /// </summary>
    private static bool IsReference(string s)
        => !string.IsNullOrWhiteSpace(s)
           && !s.Contains("base64", StringComparison.OrdinalIgnoreCase)
           && !s.Contains('{')
           && s.Length <= 200
           && (s.Contains('.') || s.Contains('/'));
}
