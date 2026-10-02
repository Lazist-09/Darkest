using Godot;

namespace Darkest.UI;

// ① 来源：从 `DdTheme.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **语义色口族**（原 `:306-382` 逐字节；M11 ① 预警面第二十二件·第二片）✓
// ② 职责：`§14.4` 四色 ＋ 语义别名 ＋ 条/状态 ＋ 面板与场景底 ＋ 描边（色/宽）＋ 交互状态 ＋ 地图 8 色 ＋
//    原型色板（`ArchetypePalette`／`ArchetypeColor`）—— 调用点一律走这里，节点上不硬写颜色（`§14.4` 纪律）✓
// ③ 🔴 依赖（实测扫描本片 · 代码区 48 行）：主片成员 `Palette` **x30**（唯一数据源 = `res://resources/theme/ui_palette.tres`）；
//    本片成员自引用 `Gold` x4 ／ `ArchetypePalette` x2 ／ 其余各 x1 ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 1 条；构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
public static partial class DdTheme
{
    // ---- 🔴 `ui_spec §14.4` 的**四色**（正文近白 / 强调金 / 危险红 / 弱化灰）+ 不透明深底 ----
    //    🔴 纪律：**颜色不得在节点上硬写**（`§14.4`）⇒ 一律走这里或 Theme；**值来自 `ui_palette.tres`**（`#325` D5）✓
    public static Color TextPrimary => Palette.TextPrimary;   // ① 正文【近白】（暖白，非冷白）
    public static Color Gold => Palette.Gold;                 // ② 【强调金】（标题 / 分组 / 提示）
    public static Color Danger => Palette.Danger;             // ③ 【危险红】（敌方 / 死门 / 告警）
    public static Color Disabled => Palette.Disabled;         // ④ 【弱化灰】（**降饱和，不降透明度**：`§1.4`⑤）

    // 语义别名（调用点不变；值统一到上面四色）
    public static Color TextAccent => Gold;                   // 分组标题
    public static Color TextSkill => Gold;                    // 技能栏标题
    public static Color TextHint => Gold;                     // 提示
    public static Color TextInfo => Palette.TextInfo;         // 情报 / 进度（暖白略压）

    // 条与状态（`§14.4`：不再散落字面量）
    public static Color Hp => Palette.Hp;                     // HP 条（正常）
    public static Color HpWeak => Palette.HpWeak;             // HP 条（虚弱）
    public static Color Morale => Palette.Morale;             // 士气条（我方 = 金）
    public static Color MoraleEnemy => Palette.MoraleEnemy;   // 士气条（敌方 = 灰）
    public static Color Positive => Palette.Positive;         // 正面 / 增益

    /// <summary>面板内部填充底（不透明近黑暖褐；`§13.2`① 底色 = 近黑 + 低饱和暖褐）。</summary>
    public static Color PanelBg => Palette.PanelBg;

    /// <summary>抬升面（按钮底、卡片内嵌块）—— 比 `PanelBg` 稍亮 ⇒ 满足 `§1.4`⑤「可交互项提亮」。</summary>
    public static Color PanelBgRaised => Palette.PanelBgRaised;

    /// <summary>面板边框（1px；暖色线条）。</summary>
    public static Color PanelBorder => Palette.PanelBorder;

    /// <summary>场景底（最深；`BattleUI` 背景等）。</summary>
    public static Color BgDeep => Palette.BgDeep;

    // ---- 🔴 `§1.4`① + `§12.3` 文字描边（**引擎内置**：`font_outline_color` + `outline_size`）----
    /// <summary>描边色：近黑暖（把文字从暗底"抠"出来；`§13.2`③）</summary>
    public static Color Outline => Palette.Outline;

    /// <summary>🔴 用户规则②：**空闲/待填位置的半透明占位填充**（色值在调色板里 ⇒ 不写死在 `.cs`）✓</summary>
    public static Color PlaceholderFill => Palette.PlaceholderFill;

    /// <summary>描边宽度（`§1.4`① "**深色粗描边**"；2px 在 15px 正文上可读且不糊）</summary>
    public static int OutlineSize => Palette.OutlineSize;

    // ---- 交互 / 状态（`§1.4`④⑤：选中 = 提亮；灰显 = 降饱和）----
    public static Color Highlight => Palette.Highlight;       // 当前行动者 / 选中项（提亮）
    public static Color Ally => Palette.Ally;                 // 我方阵营底色（冷钢蓝，与红=敌方成对）
    public static Color Muted => Palette.Muted;               // 空位 / 未探索（压暗）
    public static Color Mental => Palette.Mental;             // 精神伤害（士气）
    public static Color Shock => Palette.Shock;               // 震慑 / 死门后遗症

    // ---- 地图（远征地图视图 与 战斗小地图 **共用一套**，避免两处各写一套色）----
    public static Color MapEdge => Palette.MapEdge;
    public static Color MapCurrent => Palette.MapCurrent;
    public static Color MapReachable => Palette.MapReachable;
    public static Color MapVisited => Palette.MapVisited;
    public static Color MapScouted => Palette.MapScouted;   // 🔴 D-3：三态中间态（暗 + 亮轮廓）
    public static Color MapUnknown => Palette.MapUnknown;
    public static Color MapFrame => Palette.MapFrame;
    public static Color TeamDot => Palette.TeamDot;

    /// <summary>原型色板（立绘占位块 / 单位卡）：**颜色集中在这里**，UI 侧不再硬写（`§14.4` 纪律）✓</summary>
    private static readonly System.Collections.Generic.Dictionary<string, Color> ArchetypePalette =
        new(System.StringComparer.Ordinal)
        {
            ["tank"] = new(0.42f, 0.52f, 0.62f),
            ["warrior"] = new(0.62f, 0.35f, 0.32f),
            ["commissar"] = new(0.66f, 0.58f, 0.30f),
            ["medic"] = new(0.34f, 0.55f, 0.42f),
            ["melee_soldier"] = new(0.50f, 0.28f, 0.30f),
            ["ranged_archer"] = new(0.42f, 0.44f, 0.28f),
            ["caster"] = new(0.45f, 0.32f, 0.58f),
        };

    /// <summary>取某原型的颜色（未登记 ⇒ 按阵营给中性色 —— 仍然"有出处"，不是散落字面量）。</summary>
    public static Color ArchetypeColor(string archetype, bool isPlayer)
        => ArchetypePalette.TryGetValue(archetype, out Color c)
            ? c
            : isPlayer ? new Color(0.40f, 0.45f, 0.55f) : new Color(0.50f, 0.35f, 0.35f);

}
