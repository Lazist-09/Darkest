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
    // ---- 语义色（把散落各处的字面量收敛到这里；四色定义见下方 `§14.4` 段）----

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

    /// <summary>构建中央 Theme：默认字体/字号 + 三类控件的字号/颜色（其余走引擎默认）。</summary>
    public static Theme Build()
    {
        var theme = new Theme
        {
            DefaultFontSize = FontBody,
        };

        // 🔴 `ui_spec §13.4①`（策划 `#321`④ 裁定）：**字体 = 收益最大、成本最低的一步**
        //    · **CJK 必须给**（我们 UI 是中文）⇒ 主字体 = `Noto Serif SC`（思源宋体，**OFL**，含 CJK）
        //    · 拉丁/数字优先 `EB Garamond`（OFL）⇒ fallback 链：`EB Garamond` → `Noto Serif SC`
        //    🔴 落点固定 = `res://resources/theme/fonts/`（**放进去即生效**，不必改代码）：
        //        `EBGaramond.ttf`（可选）· `NotoSerifSC-Subset.ttf`（必需，**建议子集**：全量可变字重 25MB）
        //    ⚠️ **缺文件 ⇒ 不崩、不静默**：用引擎默认字体 + **打印留痕**（红线 21：不留不可分辨的状态）✓
        (Font? main, string fontNote) = ResolveFonts();
        FontNote = fontNote;
        if (main is not null)
        {
            theme.DefaultFont = main;
        }

        GD.Print($"[Theme] 字体：{fontNote}");

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

        // 🔴 `ui_spec §1.4`① + `§12.3`：**深色粗描边** —— DD 最关键的一条（"暗背景下所有文字都有描边"）。
        //    🔴 用【引擎内置】的字体描边项（`font_outline_color` + `outline_size`），**不自研 shader**：
        //       红线 26「引擎内置已能做不得自研」；一处定义 ⇒ 全 UI 继承，取代逐节点样式 ✓
        theme.SetColor("font_outline_color", "Label", Outline);
        theme.SetConstant("outline_size", "Label", OutlineSize);
        theme.SetColor("font_outline_color", "Button", Outline);
        theme.SetConstant("outline_size", "Button", OutlineSize);

        // 🔴 `§1.4`④⑤（选中态 / 对比度）：**按钮四态一处定义**（引擎默认是灰蓝渐变，与"深底 + 强对比 + 金/红点缀"不符）
        theme.SetStylebox("normal", "Button", MakeButtonStyle(PanelBgRaised, PanelBorder));
        theme.SetStylebox("hover", "Button", MakeButtonStyle(Gold.Darkened(0.62f), Gold));
        theme.SetStylebox("pressed", "Button", MakeButtonStyle(Gold.Darkened(0.45f), Gold));
        theme.SetStylebox("disabled", "Button", MakeButtonStyle(PanelBg, PanelBorder.Darkened(0.4f)));
        theme.SetStylebox("focus", "Button", MakeFocusStyle());  // 选中态 = 亮边框（§1.4 ④）

        // 条：槽压暗 + 填充金（具体条再用 `Modulate` 区分 HP/士气 —— 颜色常量都在本类里）
        theme.SetStylebox("background", "ProgressBar", MakeBarStyle(PanelBg));
        theme.SetStylebox("fill", "ProgressBar", MakeBarStyle(Morale));
        return theme;
    }

    /// <summary>按钮态样式（`§1.4`④⑤）：不透明底 + 1px 边框 + 内边距（圆角 0 = 暗黑风）。</summary>
    public static StyleBoxFlat MakeButtonStyle(Color bg, Color border)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(0);
        box.SetContentMarginAll(6);
        return box;
    }

    /// <summary>焦点/选中态（`§1.4`④：选中 ⇒ **亮边框**）：只画 2px 金边，不遮底。</summary>
    public static StyleBoxFlat MakeFocusStyle()
    {
        var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = Gold };
        box.SetBorderWidthAll(2);
        box.SetCornerRadiusAll(0);
        return box;
    }

    /// <summary>进度/士气条的槽与填充样式（无内边距、圆角 0）。</summary>
    public static StyleBoxFlat MakeBarStyle(Color bg)
    {
        var box = new StyleBoxFlat { BgColor = bg };
        box.SetCornerRadiusAll(0);
        box.SetContentMarginAll(0);
        return box;
    }

    /// <summary>字体目录（**放进去即生效**；`§13.4①` 的实施落点）。</summary>
    public const string FontDir = "res://resources/theme/fonts/";

    private static (Font? Main, string Note) ResolveFonts()
    {
        Font? latin = TryLoadFont($"{FontDir}EBGaramond.ttf");
        Font? cjk = TryLoadFont($"{FontDir}NotoSerifSC-Subset.ttf");
        Font? main = latin ?? cjk;
        if (main is null)
        {
            return (null, $"🔴 未找到字体文件（预期 {FontDir}EBGaramond.ttf ／ NotoSerifSC-Subset.ttf）" +
                          " ⇒ **用引擎默认字体（占位）**；把 OFL 字体放进该目录即自动生效");
        }

        // 🔴 fallback 链：拉丁字体在前、CJK 在后（缺字形时逐级回退）✓
        if (latin is not null && cjk is not null)
        {
            latin.Fallbacks = new Godot.Collections.Array<Font> { cjk };
        }

        return (main, $"{Describe(main)}（fallback {(cjk is null ? "无" : Describe(cjk))}）⇒ 已接（`§13.4①`）");
    }

    private static string Describe(Font f) => $"{f.ResourcePath.GetFile()}";

    private static Font? TryLoadFont(string resPath)
        => ResourceLoader.Exists(resPath) ? ResourceLoader.Load<Font>(resPath) : null;

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

    // ---- 🔴 `ui_spec §14.4` 的**四色**（正文近白 / 强调金 / 危险红 / 弱化灰）+ 不透明深底 ----
    //    🔴 纪律：**颜色不得在节点上硬写**（`§14.4`）⇒ 一律走这里或 Theme；本类 = 语义色的唯一出处 ✓
    public static readonly Color TextPrimary = new(0.93f, 0.91f, 0.86f);  // ① 正文【近白】（暖白，非冷白）
    public static readonly Color Gold = new(0.85f, 0.70f, 0.36f);         // ② 【强调金】（标题 / 分组 / 提示）
    public static readonly Color Danger = new(0.80f, 0.28f, 0.26f);       // ③ 【危险红】（敌方 / 死门 / 告警）
    public static readonly Color Disabled = new(0.52f, 0.50f, 0.47f);     // ④ 【弱化灰】（**降饱和，不降透明度**：`§1.4`⑤）

    // 语义别名（既有调用点不变；值统一到上面四色 —— 一次性去掉原来的冷蓝/浅绿）
    public static readonly Color TextAccent = Gold;                       // 分组标题
    public static readonly Color TextSkill = Gold;                        // 技能栏标题
    public static readonly Color TextHint = Gold;                         // 提示
    public static readonly Color TextInfo = new(0.86f, 0.84f, 0.78f);     // 情报 / 进度（暖白略压）

    // 条与状态（原来是 `BattleUi.FillCard` 里硬写的字面量 ⇒ 收敛到这里，`§14.4` 纪律）
    public static readonly Color Hp = new(0.55f, 0.80f, 0.45f);           // HP 条（正常）
    public static readonly Color HpWeak = new(0.92f, 0.38f, 0.34f);       // HP 条（虚弱）
    public static readonly Color Morale = new(0.85f, 0.70f, 0.36f);       // 士气条（我方 = 金）
    public static readonly Color MoraleEnemy = new(0.55f, 0.52f, 0.48f);  // 士气条（敌方 = 灰）
    public static readonly Color Positive = new(0.62f, 0.78f, 0.45f);     // 正面/增益（可读性用的第 5 色，谨慎使用）

    /// <summary>面板内部填充底（不透明近黑暖褐；`§13.2`① 底色 = 近黑 + 低饱和暖褐）。</summary>
    public static readonly Color PanelBg = new(0.10f, 0.09f, 0.08f, 1.0f);

    /// <summary>抬升面（按钮底、卡片内嵌块）—— 比 `PanelBg` 稍亮 ⇒ 满足 `§1.4`⑤「可交互项提亮」。</summary>
    public static readonly Color PanelBgRaised = new(0.15f, 0.14f, 0.12f, 1.0f);

    /// <summary>面板边框（1px；暖色线条）。</summary>
    public static readonly Color PanelBorder = new(0.32f, 0.27f, 0.20f, 1.0f);

    /// <summary>场景底（最深；`BattleUi` 背景等）。</summary>
    public static readonly Color BgDeep = new(0.07f, 0.07f, 0.08f, 1.0f);

    // ---- 🔴 `§1.4`① + `§12.3` 文字描边（**引擎内置**：`font_outline_color` + `outline_size`）----
    /// <summary>描边色：近黑暖（把文字从暗底"抠"出来；`§13.2`③）</summary>
    public static readonly Color Outline = new(0.03f, 0.02f, 0.02f, 1.0f);

    /// <summary>描边宽度（`§1.4`① "**深色粗描边**"；2px 在 15px 正文上可读且不糊）</summary>
    public const int OutlineSize = 2;

    // ---- 交互 / 状态（`§1.4`④⑤：选中 = 提亮；灰显 = 降饱和）----
    public static readonly Color Highlight = new(1.00f, 0.95f, 0.70f);    // 当前行动者 / 选中项（提亮）
    public static readonly Color Ally = new(0.62f, 0.72f, 0.90f);         // 我方阵营底色（冷钢蓝，与红=敌方成对）
    public static readonly Color Muted = new(0.35f, 0.35f, 0.35f);        // 空位 / 未探索（压暗）
    public static readonly Color Mental = new(0.78f, 0.55f, 1.00f);       // 精神伤害（士气）
    public static readonly Color Shock = new(1.00f, 0.62f, 0.25f);        // 震慑 / 死门后遗症

    // ---- 地图（远征地图视图 与 战斗小地图 **共用一套**，避免两处各写一套色）----
    public static readonly Color MapEdge = new(0.35f, 0.35f, 0.42f);
    public static readonly Color MapCurrent = new(1.00f, 0.85f, 0.30f);
    public static readonly Color MapReachable = new(0.55f, 0.72f, 0.45f);
    public static readonly Color MapVisited = new(0.35f, 0.35f, 0.40f);
    public static readonly Color MapUnknown = new(0.18f, 0.18f, 0.24f);
    public static readonly Color MapFrame = new(0.45f, 0.42f, 0.38f);
    public static readonly Color TeamDot = new(1.00f, 0.70f, 0.20f);

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
               $"§14.4 四色：正文 {TextPrimary.ToHtml()} ／ 金 {Gold.ToHtml()} ／ 红 {Danger.ToHtml()} ／ 灰 {Disabled.ToHtml()}　" +
               $"Panel 底 {PanelBg.ToHtml()}（a={PanelBg.A:0.##}）＋ 1px 边框 {PanelBorder.ToHtml()}　" +
               $"按钮四态 + 条样式 ✅　§1.4① 文字描边：{OutlineSize}px {Outline.ToHtml()}（**引擎内置** font_outline_color/outline_size）　" +
               $"字体：{FontNote}";
    }

    /// <summary>字体接入状态（`§13.4①`；由 `Build()` 写入 —— 缺文件时是"占位"而不是崩溃/静默）✓</summary>
    public static string FontNote { get; private set; } = "（未构建）";

    /// <summary>把 Theme 存成 `.tres`（架构要求的落点目录 `resources/theme/`）—— 便于编辑器里查看/后续改成资源加载。</summary>
    public static Error DumpTo(string resPath = "res://resources/theme/dd_theme.tres")
    {
        DirAccess.MakeDirRecursiveAbsolute(resPath.GetBaseDir());
        return ResourceSaver.Save(Shared, resPath);
    }
}
