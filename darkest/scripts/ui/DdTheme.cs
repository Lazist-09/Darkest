using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **中央 Theme**（Godot 内置清单 ② / 架构 `godot_builtins_audit.md` §4：Theme + 容器 + 锚点）。
///
/// 为什么：此前**样式散落在 20+ 处**逐节点 `AddThemeFontSizeOverride` / `AddThemeColorOverride` ——
/// 改一次要改多文件，且**新控件容易漏**。Godot 的正解是 **`Theme` 资源 + 挂在根 `Control` 上向下继承**：
/// · `Theme` 是**引擎资源**（可序列化为 `.tres`，编辑器里可见/可改）；
/// · 挂到根节点 ⇒ **所有子控件自动继承**（除非子节点自己 override）✓
///
/// 🔴 **本轮的轴**：只做"**字体/字号/颜色集中**"这一条 —— **容器与锚点在下一轮**（`#244` 一次一个轴）✓
/// 🔴 值与 `ui_spec` 的现有视觉口径一致（**不是新规范** —— 架构 `§4.1`：材质/主题只是"视觉规范的一致实现"）✓
/// </summary>
public static class DdTheme
{
    // ---- 语义色（把散落各处的字面量收敛到这里）----
    public static readonly Color TextPrimary = new(1f, 1f, 0.85f);      // 主文本（原 (1,1,0.85)）
    public static readonly Color TextAccent = new(0.72f, 0.82f, 1f);    // 分组标题（原 (0.72,0.82,1)）
    public static readonly Color TextSkill = new(0.9f, 1f, 0.9f);       // 技能栏标题（原 (0.9,1,0.9)）
    public static readonly Color TextHint = new(1f, 0.85f, 0.5f);       // 提示（原 (1,0.85,0.5)）
    public static readonly Color TextInfo = new(0.85f, 0.9f, 1f);       // 情报/进度（原 (0.85,0.9,1)）
    public static readonly Color Danger = new(1f, 0.6f, 0.6f);          // 危险/敌方（原 (1,0.6,0.6)）
    public static readonly Color Disabled = new(0.55f, 0.55f, 0.6f);    // 不可用

    // ---- 字号（把散落的 font_size override 收敛为三档）----
    public const int FontTitle = 18;
    public const int FontBody = 15;
    public const int FontSmall = 12;

    // ---- 🔴 表现层常量（`#325` D5：「表现层常量不写死在 .cs」⇒ 集中到这里）----
    /// <summary>
    /// C 区技能栏的**列数**（原先是 `BattleUi.cs` 里的局部常量 `perRow = 8` ⇒ 那是 `#325` D5 点名的唯一现存反例）。
    /// ⚠️ **为什么是 4**：`#321`③ 定「C 区固定宽 ≈ 30%（1280 的 ~380px）」且「**技能栏在 C 区内**」
    /// ⇒ 8 列（8×94 = 752px）在 C 区里必然溢出 ⇒ 4 列（4×94 = 376px）刚好 ✓
    /// </summary>
    public const int SkillBarColumns = 4;

    private static Theme? _shared;

    /// <summary>共享实例（Theme 是 Resource ⇒ 全场景共用一份，不重复构建）✓</summary>
    public static Theme Shared => _shared ??= Build();

    /// <summary>构建中央 Theme：默认字号 + 三类控件的字号/颜色（其余走引擎默认）。</summary>
    public static Theme Build()
    {
        var theme = new Theme
        {
            DefaultFontSize = FontBody,
        };

        // Label：三档字号（正文 / 标题 / 小字），颜色**不设默认**（各类文本语义不同，由语义色显式指定）
        theme.SetFontSize("font_size", "Label", FontBody);
        theme.SetFontSize("font_size", "Button", FontBody);
        theme.SetColor("font_color", "Button", TextPrimary);
        theme.SetColor("font_disabled_color", "Button", Disabled); // Godot 4：禁用色是**独立 item 名**，不是第 4 个参数

        // 提示条/进度等常用控件的默认字号（原来每处都写一遍）
        theme.SetFontSize("font_size", "ProgressBar", FontSmall);
        theme.SetFontSize("font_size", "TooltipLabel", FontSmall);

        // 🔴 `ui_spec §14.4`（`#319`）：**Panel 样式一处定义** ——
        //    底深色 · **`BgColor.a = 1.0`（不透明！用户原话"框不能是透明的"）** · 1px 边框 · 圆角 0 ✓
        //    这样 ① "框"有了（读得出边界）② 判据 2（`BgColor.a == 1.0`）天然成立 ✓
        theme.SetStylebox("panel", "PanelContainer", MakePanelStyle());
        theme.SetStylebox("panel", "Panel", MakePanelStyle());
        theme.SetStylebox("panel", "PopupPanel", MakePanelStyle());
        return theme;
    }

    /// <summary>🔴 不透明面板样式（§14.2 ②）：底深色 + a=1.0 + 1px 边框 + 圆角 0。</summary>
    public static StyleBoxFlat MakePanelStyle(Color? bg = null, Color? border = null)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg ?? PanelBg,          // ⚠️ a 必须 = 1.0（判据 2 直接断言它）
            BorderColor = border ?? PanelBorder,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
        box.SetBorderWidthAll(1);              // 1px 边框（§14.2 ②）
        box.SetContentMarginAll(6);            // 内边距（配合 MarginContainer 用；容器给最小尺寸见 §14.2 ④）
        return box;
    }

    /// <summary>深色面板底（**不透明**）：`BgColor.a = 1.0` ✓</summary>
    public static readonly Color PanelBg = new(0.09f, 0.10f, 0.14f, 1.0f);

    /// <summary>面板边框（1px）</summary>
    public static readonly Color PanelBorder = new(0.28f, 0.29f, 0.34f, 1.0f);

    /// <summary>把中央 Theme 挂到某根控件上（**向下继承** ⇒ 子控件自动套用）✓</summary>
    public static void Apply(Control root)
    {
        if (root is null)
        {
            return;
        }

        root.Theme = Shared;
    }

    /// <summary>🔴 审计清单②的**取证**：当前 Theme 的关键读数（headless 可断言）。</summary>
    public static string Audit()
    {
        Theme t = Shared;
        return $"中央 Theme：默认字号 {t.DefaultFontSize}　Label {t.GetFontSize("font_size", "Label")}　" +
               $"Button {t.GetFontSize("font_size", "Button")}（色 {t.GetColor("font_color", "Button").ToHtml()}）　" +
               $"语义色 {6} 个　（本轮轴：字体/颜色集中；容器与锚点下一轮）";
    }

    /// <summary>把 Theme 存成 `.tres`（架构要求的落点目录 `resources/theme/`）—— 便于编辑器里查看/后续改成资源加载。</summary>
    public static Error DumpTo(string resPath = "res://resources/theme/dd_theme.tres")
    {
        DirAccess.MakeDirRecursiveAbsolute(resPath.GetBaseDir());
        return ResourceSaver.Save(Shared, resPath);
    }
}
