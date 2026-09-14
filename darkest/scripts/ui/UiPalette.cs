using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **表现层调色板 = 数据资产**（`#325` 的两层裁定 + D5/D6 在我这层的落法）：
/// ```
/// 业务数据  = data/*.json      （内核读，零 Godot）
/// 表现数据  = resources/**/*.tres（表现层读 ← **本类**）
/// ```
/// 🔴 **为什么是资源而不是 `const`**：`#325` D5「**表现层常量不写死在 `.cs`**」——
/// 把配色/字号/描边/技能栏列数放进 `.tres` 之后，**策划改视觉不用改代码**（也能在编辑器里直接看/调）✓
/// 🔴 纪律（D6：两视图**不得手抄**）：本资源**只放纯表现**（颜色/字号），**不放任何业务数值**（那些在 `data/*.json`）✓
///
/// ⚠️ **实现要点**：属性**不写初值**（否则 Godot 序列化时会省略"等于默认值"的项 ⇒ 生成的文件里没有可调项），
/// 出厂值只在 <see cref="Default"/> 里给 ⇒ `--dump-palette` 导出的 `.tres` **完整列出所有可调项** ✓
/// ⚠️ 缺文件 ⇒ 不崩、不静默：`DdTheme.Palette` 退回 `Default()` 并**打印留痕**（红线 21）。
/// </summary>
[GlobalClass]
public partial class UiPalette : Resource
{
    // ---- 🔴 `§14.4` 四色 + 底（正文近白 / 强调金 / 危险红 / 弱化灰）----
    [Export] public Color TextPrimary { get; set; }
    [Export] public Color Gold { get; set; }
    [Export] public Color Danger { get; set; }
    [Export] public Color Disabled { get; set; }
    [Export] public Color TextInfo { get; set; }
    [Export] public Color PanelBg { get; set; }
    [Export] public Color PanelBgRaised { get; set; }
    [Export] public Color PanelBorder { get; set; }
    [Export] public Color BgDeep { get; set; }

    // ---- `§1.4`① / `§12.3` 文字描边（引擎内置项用它）----
    [Export] public Color Outline { get; set; }
    [Export] public int OutlineSize { get; set; }

    // ---- 字号三档 ----
    [Export] public int FontTitle { get; set; }
    [Export] public int FontBody { get; set; }
    [Export] public int FontSmall { get; set; }

    /// <summary>C 区技能栏列数（`#321`③：C 区固定宽 ≈30% ⇒ 4 列刚好；原为 `BattleUi` 里写死的 `perRow = 8`）</summary>
    [Export] public int SkillBarColumns { get; set; }

    // ---- 条 / 状态 ----
    [Export] public Color Hp { get; set; }
    [Export] public Color HpWeak { get; set; }
    [Export] public Color Morale { get; set; }
    [Export] public Color MoraleEnemy { get; set; }
    [Export] public Color Positive { get; set; }
    [Export] public Color Highlight { get; set; }
    [Export] public Color Ally { get; set; }
    [Export] public Color Muted { get; set; }
    [Export] public Color Mental { get; set; }
    [Export] public Color Shock { get; set; }

    // ---- 地图（远征地图视图 × 战斗小地图 共用）----
    [Export] public Color MapEdge { get; set; }
    [Export] public Color MapCurrent { get; set; }
    [Export] public Color MapReachable { get; set; }
    [Export] public Color MapVisited { get; set; }
    [Export] public Color MapUnknown { get; set; }
    [Export] public Color MapFrame { get; set; }
    [Export] public Color TeamDot { get; set; }

    /// <summary>资源落点（**放/改文件即生效**，不必改代码）。</summary>
    public const string ResPath = "res://resources/theme/ui_palette.tres";

    /// <summary>
    /// 出厂调色板 = **全部可调项的初值**（与 `ui_palette.tres` 同值 ⇒ 缺文件时视觉不变，只是少一层可编辑性）✓
    /// 🔴 值本身来自 `ui_spec §14.4/§1.4` 与策划 `#321`；**改视觉请改 `.tres`，不要改这里**。
    /// </summary>
    public static UiPalette Default() => new()
    {
        // `§14.4` 四色
        TextPrimary = new Color(0.93f, 0.91f, 0.86f),
        Gold = new Color(0.85f, 0.70f, 0.36f),
        Danger = new Color(0.80f, 0.28f, 0.26f),
        Disabled = new Color(0.52f, 0.50f, 0.47f),
        TextInfo = new Color(0.86f, 0.84f, 0.78f),

        // 底 / 边框
        PanelBg = new Color(0.10f, 0.09f, 0.08f, 1.0f),
        PanelBgRaised = new Color(0.15f, 0.14f, 0.12f, 1.0f),
        PanelBorder = new Color(0.32f, 0.27f, 0.20f, 1.0f),
        BgDeep = new Color(0.07f, 0.07f, 0.08f, 1.0f),

        // 描边（`§1.4`① 深色粗描边）
        Outline = new Color(0.03f, 0.02f, 0.02f, 1.0f),
        OutlineSize = 2,

        // 字号三档 + C 区技能栏列数
        FontTitle = 18,
        FontBody = 15,
        FontSmall = 12,
        SkillBarColumns = 4,

        // 条 / 状态
        Hp = new Color(0.55f, 0.80f, 0.45f),
        HpWeak = new Color(0.92f, 0.38f, 0.34f),
        Morale = new Color(0.85f, 0.70f, 0.36f),
        MoraleEnemy = new Color(0.55f, 0.52f, 0.48f),
        Positive = new Color(0.62f, 0.78f, 0.45f),
        Highlight = new Color(1.00f, 0.95f, 0.70f),
        Ally = new Color(0.62f, 0.72f, 0.90f),
        Muted = new Color(0.35f, 0.35f, 0.35f),
        Mental = new Color(0.78f, 0.55f, 1.00f),
        Shock = new Color(1.00f, 0.62f, 0.25f),

        // 地图
        MapEdge = new Color(0.35f, 0.35f, 0.42f),
        MapCurrent = new Color(1.00f, 0.85f, 0.30f),
        MapReachable = new Color(0.55f, 0.72f, 0.45f),
        MapVisited = new Color(0.35f, 0.35f, 0.40f),
        MapUnknown = new Color(0.18f, 0.18f, 0.24f),
        MapFrame = new Color(0.45f, 0.42f, 0.38f),
        TeamDot = new Color(1.00f, 0.70f, 0.20f),
    };
}
