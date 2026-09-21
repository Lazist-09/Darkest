using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// 面板布局档位（2026-09-20 L2/L3 布局整改）✓
/// 🔴 决策依据：用户裁定「**混合**」—— **整屏页跟 DD**（1395×776 @ 144,132 左偏，不居中），
///    **弹窗类小盒居中**。满屏仅保留 DD 原版**真的**是 1920×1080 的那两个（任务选择 / 供应）✓
/// </summary>
public enum PopupLayout
{
    /// <summary>1920×1080 满屏 —— **只给 DD 原版真满屏的**：`quest_select.background.png` / `provision.background.png` 实测均 1920×1080 ✓</summary>
    FullScreen,

    /// <summary>整屏页 1395×776 @ (144,132) —— DD `shared/character/character.layout.darkest`（`.character_pos 144 132`；`characterpanel_bg.png` 实测 1395×776）✓ 左偏不居中 ✓</summary>
    Sheet,

    /// <summary>建筑 694×858 居中（内容 662×764 = `blgupgradebg.png` 实测，沿用项目既有裁定）✓</summary>
    Building,

    /// <summary>传家宝兑换 461×362 居中（内容 429×268 = `heirloom_exchange.background.png` 实测）✓</summary>
    Heirloom,

    /// <summary>库存 667×780 居中 —— `realminv_bg.png` 实测 ✓</summary>
    Inventory,

    /// <summary>L3 确认框 840×600 居中 —— DD `shared/confirm_dialog`（`base_pos 960` ⇒ 水平居中）✓</summary>
    Confirm,

    /// <summary>L3 模态框 840×464 居中 —— DD `shared/modal_dialog`（`screen_centre_offset 0 0`）✓</summary>
    Modal,
}

/// <summary>
/// 🔴 **L2/L3 布局规格 —— 一处定义**（2026-09-20）✓
///
/// 为什么要有这个类：此前每个 L2/L3 都 `SetAnchorsAndOffsetsPreset(FullRect)` ⇒ **一律 1920×1080 不透明大框**，
/// 屏幕上只看到一个铺满的框，既不整洁也挡住背景。尺寸/位置散在各处 Hardcode ⇒ 改一处要找多文件 ✓
///
/// 数值来源：**全部实测自 DD 安装目录** `/e/SteamLibrary/steamapps/common/DarkestDungeon/` 的
/// `.layout.darkest` 与背景图 PNG 像素尺寸 —— **不是猜的**（红线 27：学 DD 构图，不抄素材）✓
///
/// ⚠️ 纪律：**新面板请从这里取尺寸**，不要在调用点写 `new Vector2(840, 464)` 之类的字面量 ✓
/// </summary>
public static class UILayoutSpec
{
    /// <summary>逻辑画布（与项目 ① 屏幕空间一致；`CanvasLayer` + FullRect 根）✓</summary>
    public const float ScreenW = 1920f;
    public const float ScreenH = 1080f;

    // ---- 整屏页（DD character.layout；左偏**不**居中）----
    /// <summary>1395×776 —— `characterpanel_bg.png` 实测 ✓</summary>
    public static readonly Vector2 SheetSize = new(1395f, 776f);

    /// <summary>(144,132) —— DD `.character_pos 144 132` ✓</summary>
    public static readonly Vector2 SheetPos = new(144f, 132f);

    // ---- 小盒（DD 背景图实测；一律**屏幕居中**）----
    // ---- 建筑：`blgupgradebg.png` 实测 **内容** 662×764 ----
    /// <summary>建筑内容区 662×764 —— `blgupgradebg.png` 实测（`building_popup.tscn` 的 `PanelFrame` 就是它）✓</summary>
    public static readonly Vector2 BuildingContentSize = new(662f, 764f);

    /// <summary>
    /// 建筑**弹窗面板** 694×858 = 内容 662×764 + 装饰开销（左右 margin 各 16 ⇒ +32 宽；
    /// 标题行 34 + 分隔 + 容器间隙 ⇒ +94 高）✓
    /// ⚠️ 为什么要 +装饰：`MakePopup` 会给面板套 `MarginContainer(16)` + 标题行 + 分隔线；
    ///    若面板就给 662×764，内容会被挤出 **82px** ⇒ 底部被 `clip_contents` 裁掉（实测会丢升级树底部）✓
    /// </summary>
    public static readonly Vector2 BuildingSize = new(694f, 858f);

    /// <summary>传家宝内容区 429×268 —— `heirloom_exchange.background.png` 实测 ✓</summary>
    public static readonly Vector2 HeirloomContentSize = new(429f, 268f);

    /// <summary>
    /// 传家宝**弹窗面板** 461×362 = 内容 429×268 + 装饰开销（左右 margin 各 16 ⇒ +32 宽；标题行 + 分隔 ⇒ +94 高）✓
    /// ⚠️ 同 `BuildingSize`：面板若就给 429×268，内容会被挤出并被 `clip_contents` 裁掉 ✓
    /// </summary>
    public static readonly Vector2 HeirloomSize = new(461f, 362f);

    /// <summary>667×780 —— `realminv_bg.png` 实测 ✓</summary>
    public static readonly Vector2 InventorySize = new(667f, 780f);

    /// <summary>840×600 —— `confirm_dialog.background.png` 实测 ✓</summary>
    public static readonly Vector2 ConfirmSize = new(840f, 600f);

    /// <summary>840×464 —— `modal_dialog.background.png` 实测 ✓</summary>
    public static readonly Vector2 ModalSize = new(840f, 464f);

    /// <summary>在 1920×1080 内居中（用于所有"小盒"档位）✓</summary>
    public static Vector2 Centered(Vector2 size) => new((ScreenW - size.X) / 2f, (ScreenH - size.Y) / 2f);

    /// <summary>
    /// 档位 → (尺寸, 位置)。满屏档位位置为 (0,0)、尺寸为 1920×1080。
    /// ⚠️ 未登记档位 ⇒ 退回 `Modal`（840×464 居中）并**如实返回**，不静默 ✓
    /// </summary>
    public static (Vector2 Size, Vector2 Pos) Resolve(PopupLayout layout) => layout switch
    {
        PopupLayout.FullScreen => (new Vector2(ScreenW, ScreenH), Vector2.Zero),
        PopupLayout.Sheet => (SheetSize, SheetPos),
        PopupLayout.Building => (BuildingSize, Centered(BuildingSize)),
        PopupLayout.Heirloom => (HeirloomSize, Centered(HeirloomSize)),
        PopupLayout.Inventory => (InventorySize, Centered(InventorySize)),
        PopupLayout.Confirm => (ConfirmSize, Centered(ConfirmSize)),
        PopupLayout.Modal => (ModalSize, Centered(ModalSize)),
        _ => (ModalSize, Centered(ModalSize)),
    };

    /// <summary>
    /// 🔴 **把面板按档位摆位**（取代此前一律 `FullRect`）✓
    ///
    /// 实现要点（这是修"骨架错挂"的关键）：
    /// · 面板**自身**就是那个盒子（不再有 FullRect 外层）⇒ 不再画一个铺满屏的框，背景可见 ✓
    /// · 锚点归到 **TopLeft** 后用 `Position` + `Size` 定位 ⇒ **像素直落**，与 ① 屏幕空间口径一致 ✓
    /// · `clip_contents = true` ⇒ 内容溢出时**被裁剪**（DD 原设计就是裁掉，见既有裁定）✓
    ///
    /// ⚠️ 前提：父节点应是**非容器** `Control`（如 `OverlayLayer.ModalHost`）。
    ///    若父是 `VBoxContainer` 等容器，锚点与 `Position` 会被容器接管而失效 —— 那正是此前的 bug ✓
    /// </summary>
    public static void Place(Control panel, PopupLayout layout)
    {
        if (panel is null)
        {
            return;
        }

        (Vector2 size, Vector2 pos) = Resolve(layout);

        if (layout == PopupLayout.FullScreen)
        {
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            panel.ClipContents = true;
            return;
        }

        // 🔴 关键：先清锚点 + 清偏移（`SetAnchorsPreset(keepOffset: false)`）⇒ 之后 Position/Size 才是干净的像素直落
        //    ⚠️ 原写法 `SetAnchorsAndOffsetsPreset(..., keepOffsets: false)` 不成立：Godot 4.6 该方法的第二参是
        //       `LayoutPresetMode`，没有 `keepOffsets` 形参（CS1739）。`keepOffset` 属 `SetAnchorsPreset` ✓
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft, false);
        panel.Position = pos;
        panel.CustomMinimumSize = size;
        panel.Size = size;
        panel.ClipContents = true;   // 溢出即裁剪（DD 原设计；不加会糊出面板外）
    }

    /// <summary>人类可读读数（供日志/自检：证明尺寸有出处，不是拍脑袋）✓</summary>
    public static string Describe(PopupLayout layout)
    {
        (Vector2 size, Vector2 pos) = Resolve(layout);
        return $"{layout}：{size.X:0}×{size.Y:0} @ ({pos.X:0},{pos.Y:0})";
    }
}
