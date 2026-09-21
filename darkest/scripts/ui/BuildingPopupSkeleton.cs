using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **建筑详情弹窗【内容布局】骨架**（用户 2026-09-17：「能在编辑器里直接干预」+「重复元素抽模板」）✓
///
/// 定位：`scenes/ui/building_popup.tscn` —— 弹窗外框由 `HamletRoot.MakePopup` 工厂出（标题 + ✕ 关闭），
/// 本骨架负责**内容布局**（改这里 = 改建筑详情弹窗的内部排布）。
///
/// 🔴 **2026-09-19 重构：坐标空间改正为 662x764 局部像素空间**（原为满屏 1920x1080 比例锚点）✓
///   依据（实测实证，见 `doc/UI_STRUCTURE_DECISIONS.md` §5.2）：
///   - 面板整体 = 662 x 764 ← E盘 `campaign/town/buildings/blgupgradebg.png` PNG 固有尺寸
///   - `building_base_layout` 的 `name_pos(104,126)` / `body_base_pos(596,102)` /
///     `upgrade_base_pos(172,259)` 三者均落在 662x764 内且分布合理（左列 / 右列 / 中下）
///   - `close_pos(1496,144)` 超出 662 ⇒ 属屏幕空间 1920x1080，不在本面板内 ✓
///   ⇒ 本面板所有 `*.darkest` 锚点一律 = **662x764 局部像素、原点左上**（与 Godot 一致）✓
///
/// 尺寸来源优先级：布局自带 size > 尺寸表参数 > 资产 PNG 固有尺寸（兜底）
///   blgupgradebg.png               662 x 764  → 面板外框
///   blg_name_background.png        208 x 224  → 名称底
///   blg_townupgrade_costframe.png  103 x 139  → 花费框
///
/// 合规口径（红线27「学构图不抄素材」）：只取尺寸数字与锚点构图，不使用像素图像 ✓
/// 占位 `ColorRect` 一律 `UiPalette.PlaceholderFill = Color(0.62,0.6,0.58,0.22)`（硬规矩 §14.0.68：不换不删）✓
///
/// ⚠️ 节点名保持：`PanelFrame` / `BpNameBack` / `BpBodyLayout` / `BpUpgradeLayout` / `BpActivityLayout` ✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见结构；编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class BuildingPopupSkeleton : Control
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/building_popup.tscn";

    /// <summary>DD 面板外框尺寸：`blgupgradebg.png` 实测 662x764 ✓</summary>
    public static readonly Vector2 PanelSize = new(662f, 764f);

    /// <summary>DD 名称底尺寸：`blg_name_background.png` 实测 208x224 ✓</summary>
    public static readonly Vector2 NameBackSize = new(208f, 224f);

    /// <summary>DD 花费框尺寸：`blg_townupgrade_costframe.png` 实测 103x139 ✓</summary>
    public static readonly Vector2 CostFrameSize = new(103f, 139f);

    /// <summary>662x764 局部坐标空间承载层（所有 DD 锚点的共同父）✓</summary>
    public Control? Frame => GetNodeOrNull<Control>("PanelFrame");

    /// <summary>面板外框色块（662x764 占位）✓</summary>
    public ColorRect? PanelBackground => GetNodeOrNull<ColorRect>("PanelFrame/PanelBackground");

    // ---- [1] 名称底 208x224 @ name_pos 104,126 ----
    public PanelContainer? NameBack => GetNodeOrNull<PanelContainer>("PanelFrame/BpNameBack");

    // ---- [2] 主体区 body_base_pos 596,102 ----
    public Control? BodyLayout => GetNodeOrNull<Control>("PanelFrame/BpBodyLayout");

    public PanelContainer? BodyAnchor =>
        GetNodeOrNull<PanelContainer>("PanelFrame/BpBodyLayout/BpBodyAnchor");

    // ---- [3] 升级区 upgrade_base_pos 172,259 · frame_offset -18,-115 ----
    public Control? UpgradeLayout => GetNodeOrNull<Control>("PanelFrame/BpUpgradeLayout");

    public PanelContainer? UpgradeAnchor =>
        GetNodeOrNull<PanelContainer>("PanelFrame/BpUpgradeLayout/BpUpgradeAnchor");

    /// <summary>升级树位 `upgrade_trees_offset 0,195` → 绝对 (172,454) ✓</summary>
    public PanelContainer? TreesAnchor =>
        GetNodeOrNull<PanelContainer>("PanelFrame/BpUpgradeLayout/BpTreesAnchor");

    // ---- [4] 活动区 building_activity_layout · base_size 800x200 ----
    public Control? ActivityLayout => GetNodeOrNull<Control>("PanelFrame/BpActivityLayout");

    /// <summary>活动条目基准 800x200 @ base_pos 70,50（超面板宽 ⇒ DD 同行为：被面板裁剪）✓</summary>
    public PanelContainer? ActivityBase =>
        GetNodeOrNull<PanelContainer>("PanelFrame/BpActivityLayout/BpActivityBase");

    /// <summary>英雄槽列表位 `slot_list_pos 440,119`（相对 800x200 条目局部空间）✓</summary>
    public PanelContainer? SlotListAnchor =>
        GetNodeOrNull<PanelContainer>("PanelFrame/BpActivityLayout/BpActivityBase/BpSlotListAnchor");

    /// <summary>选项列表位 `choice_list_pos 0,250` · 热区 120x20 ✓</summary>
    public PanelContainer? ChoiceListAnchor =>
        GetNodeOrNull<PanelContainer>("PanelFrame/BpActivityLayout/BpChoiceListAnchor");

    // ---- [5] 花费框 103x139（右下角对齐）----
    public PanelContainer? CostFrame => GetNodeOrNull<PanelContainer>("PanelFrame/BpCostFrame");

    // ---- [6] 各栋建筑独立 L2 子面板（依据 UI_STRUCTURE_DECISIONS.md §1 方案 A）----
    // 坟场 graveyard.layout.darkest
    public Control? GraveyardPanel => GetNodeOrNull<Control>("PanelFrame/GraveyardPanel");

    /// <summary>坟场列表区 720x600 @ list_position 148,148 ✓</summary>
    public PanelContainer? GraveyardList =>
        GetNodeOrNull<PanelContainer>("PanelFrame/GraveyardPanel/BpGraveyardList");

    /// <summary>坟场条目 1000x160（宽于列表 ⇒ DD 同行为：被列表裁剪）✓</summary>
    public PanelContainer? GraveyardEntry =>
        GetNodeOrNull<PanelContainer>("PanelFrame/GraveyardPanel/BpGraveyardList/BpGraveyardEntry");

    // 雕像 statue.layout.darkest
    public Control? StatuePanel => GetNodeOrNull<Control>("PanelFrame/StatuePanel");

    /// <summary>雕像列表区 600x580 @ list_position 270,170 ✓</summary>
    public PanelContainer? StatueList =>
        GetNodeOrNull<PanelContainer>("PanelFrame/StatuePanel/BpStatueList");

    // 驿站 stage_coach.layout.darkest
    public Control? StageCoachPanel => GetNodeOrNull<Control>("PanelFrame/StageCoachPanel");

    /// <summary>招募商店 780x780 @ hero_recruit_store.base_size ✓</summary>
    public PanelContainer? HeroRecruitStore =>
        GetNodeOrNull<PanelContainer>("PanelFrame/StageCoachPanel/BpHeroRecruitStore");

    /// <summary>驿站文本框 350x200 @ add_hero_dialog_contents.text_box_size ✓</summary>
    public PanelContainer? StageCoachTextBox =>
        GetNodeOrNull<PanelContainer>("PanelFrame/StageCoachPanel/BpStageCoachTextBox");

    // 疗养院 sanitarium.layout.darkest
    public Control? SanitariumPanel => GetNodeOrNull<Control>("PanelFrame/SanitariumPanel");

    /// <summary>疗养院选项热区 120x20（`choice_list_pos 0,250` 相对活动条目 → 面板级 70,300）✓</summary>
    public PanelContainer? SanitariumChoice =>
        GetNodeOrNull<PanelContainer>("PanelFrame/SanitariumPanel/BpSanitariumChoice");

    // 英雄动作 hero_action.layout.darkest
    public Control? HeroActionPanel => GetNodeOrNull<Control>("PanelFrame/HeroActionPanel");

    /// <summary>英雄动作基准 100x100 @ base_pos 220,44 ✓</summary>
    public PanelContainer? HeroActionBase =>
        GetNodeOrNull<PanelContainer>("PanelFrame/HeroActionPanel/BpHeroActionBase");

    // 升级需求 upgrade.layout.darkest
    public Control? UpgradePanel => GetNodeOrNull<Control>("PanelFrame/UpgradePanel");

    /// <summary>升级需求图标 102x72 @ upgrade_requirement_layout.base_size ✓</summary>
    public PanelContainer? UpgradeSlot =>
        GetNodeOrNull<PanelContainer>("PanelFrame/UpgradePanel/BpUpgradeSlot");

    /// <summary>实例化骨架；场景缺失/类型不符 ⇒ null（宿主回落代码构建，不崩不静默）✓</summary>
    public static BuildingPopupSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        BuildingPopupSkeleton? skel = packed?.Instantiate<BuildingPopupSkeleton>();
        if (skel is null)
        {
            GD.Print($"[UI 骨架] `{ScenePath}` 不可用 ⇒ 建筑详情弹窗回落代码构建（不静默）✓");
            return null;
        }

        GD.Print($"[UI 骨架] ✅ 建筑详情采用骨架 `{ScenePath}`（662x764 局部空间 · 编辑器里可编辑）✓");
        return skel;
    }

    public override void _Ready()
    {
        _ = Engine.IsEditorHint();
    }
}
