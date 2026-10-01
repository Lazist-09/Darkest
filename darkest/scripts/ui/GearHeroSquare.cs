using Godot;

namespace Darkest.UI;

/// <summary>
/// 🆕 **M6u（P4 ②）· 英雄方块**（DD 式拖放**源**）—— 玩家把方块**拖进孔中**表示"用这个人"；
/// 点击方块 = 同一动作的兜底（无鼠标 / 键盘场景）✓
///
/// 🔴 **复用引擎内建拖放，不造轮子**：Godot 的 `Control._GetDragData` / `SetDragPreview` 就是官方那套
///    （官方文档 "Drag and drop" 一节：源控件重写 `_GetDragData` 并 `SetDragPreview`，
///    目标控件重写 `_CanDropData` / `_DropData`）⇒ **零第三方库、零自研协议** ✓
///    ⇒ 载荷 = **英雄 id 字符串**（`Variant` 的 String 类型；孔那边只认这一种载荷 ✓）
///
/// 🔴 `[Tool]` ⇒ 编辑器里可见外观；⚠️ 编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class GearHeroSquare : Button
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/gear_hero_square.tscn";

    /// <summary>方块代表的英雄 id（空 ⇒ 不可拖：不静默拖一个空载荷）✓</summary>
    public string HeroId { get; set; } = string.Empty;

    /// <summary>编辑器预览用文案（运行时被真实英雄首字覆盖）✓</summary>
    [Export]
    public string PreviewText { get; set; } = "战";

    /// <summary>造一个方块；**场景缺失/类型不符 ⇒ 返回 null**（调用方回落 `new Button`，不崩不静默）✓</summary>
    public static GearHeroSquare? TryCreate(string heroId, string text, string tooltip)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        GearHeroSquare? square = packed?.Instantiate<GearHeroSquare>();
        if (square is null)
        {
            return null;
        }

        square.Name = $"GearHero_{heroId}";   // 节点名 = 冒烟锚点（按 id 找方块 ⇒ 真实按下）✓
        square.HeroId = heroId;
        square.Text = text;
        square.TooltipText = tooltip;
        return square;
    }

    /// <summary>🔴 引擎内建拖放**源**：返回英雄 id（`Variant` 字符串）作为拖动载荷 ✓</summary>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (string.IsNullOrEmpty(HeroId))
        {
            return default;   // 没有英雄 ⇒ 不开始拖动（如实拒绝，不静默拖空载荷）✓
        }

        SetDragPreview(new Label
        {
            Text = Text,
            CustomMinimumSize = new Vector2(34, 34),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,   // 预览跟着光标走 ⇒ 绝不能抢鼠标（否则孔收不到落点）✓
            Modulate = DdTheme.Highlight,
        });
        GD.Print($"[GearHeroSquare] 开始拖动英雄方块：{HeroId}（载荷 = 英雄 id 字符串；引擎内建 drag-and-drop）✓");
        return HeroId;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            Text = PreviewText;
        }
    }
}
