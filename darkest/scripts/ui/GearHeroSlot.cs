using System;
using Godot;

namespace Darkest.UI;

/// <summary>
/// 🆕 **M6u（P4 ②）· 英雄方块【孔】**（DD 式拖放**目标**）—— 玩家把英雄方块拖进来 ⇒ `HeroDropped(英雄 id)` ✓
///
/// 用户 2026-10-01 口径原话：「所有建筑详情界面的选人都是有一个**方块空洞**，玩家把角色对应的方块头像
/// **放入孔中**表示使用这个人」⇒ 本件就是那个"空洞"（DD 的 `hero_slot` 位）✓
///
/// 🔴 **复用引擎内建拖放，不造轮子**：只重写 `_CanDropData` / `_DropData`（Godot 官方 drag-and-drop）✓
/// 🔴 孔**必须** `MouseFilter = Stop`：`Ignore` 的控件收不到 `_DropData`（实测坑 ⇒ 显式设置 + 注释钉住）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见外观；⚠️ 编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class GearHeroSlot : PanelContainer
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/gear_hero_slot.tscn";

    /// <summary>落孔回调（参数 = 英雄 id）；由装备阶行接线到选人 ✓</summary>
    public Action<string>? HeroDropped { get; set; }

    /// <summary>孔里的字形 Label（空孔 = ＋ ／ 已选 = 英雄首字）✓</summary>
    public Label? Glyph => GetNodeOrNull<Label>("SlotGlyph");

    /// <summary>编辑器预览字形 ✓</summary>
    [Export]
    public string PreviewGlyph { get; set; } = "＋";

    /// <summary>造一个孔；**场景缺失/类型不符 ⇒ 返回 null**（调用方如实上报"拖放不可用"，不崩不静默）✓</summary>
    public static GearHeroSlot? TryCreate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        return packed?.Instantiate<GearHeroSlot>();
    }

    /// <summary>换字形与悬停说明（＋ = 空孔；首字 = 已选该英雄）✓</summary>
    public void SetGlyph(string text, string tooltip)
    {
        if (Glyph is { } g)
        {
            g.Text = text;
            g.TooltipText = tooltip;
        }

        TooltipText = tooltip;   // 悬停在孔边缘（字形之外）也能看到同一句 ✓
    }

    /// <summary>🔴 引擎内建拖放**目标**：只接受**非空字符串**载荷（= 英雄 id）✓</summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => data.VariantType == Variant.Type.String && !string.IsNullOrEmpty(data.AsString());

    /// <summary>🔴 落孔 ⇒ 转发英雄 id（真实玩家路径 = 引擎拖动；冒烟用**同一入口**直投，见 HamletRoot.Gear）✓</summary>
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        string heroId = data.AsString();
        GD.Print($"[GearHeroSlot] 英雄方块落孔：{heroId}（引擎 _DropData ⇒ HeroDropped）✓");
        HeroDropped?.Invoke(heroId);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;   // 🔴 孔必须收鼠标事件（Ignore ⇒ 永远收不到 _DropData）✓
        if (Engine.IsEditorHint() && Glyph is { } g)
        {
            g.Text = PreviewGlyph;
        }
    }
}
