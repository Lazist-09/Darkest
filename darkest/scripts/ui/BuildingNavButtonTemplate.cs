using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **建筑左列切换按钮模板**（用户 2026-09-17：「重复的 UI 元素记得能复用就建成能复用的」）✓
///
/// 复用：建筑详情弹窗左列的**三栋切换按钮**（酒馆 / 修道院 / 驿站）——**同构 3 处** ⇒ 抽模板
/// ⇒ **改这一处 = 三个按钮一起变**（尺寸/字号/主题覆盖都能在编辑器里改）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见外观；⚠️ 编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class BuildingNavButtonTemplate : Button
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/building_nav_button.tscn";

    /// <summary>编辑器预览用文案（运行时被真实建筑名覆盖）✓</summary>
    [Export]
    public string PreviewText { get; set; } = "酒馆 Tavern";

    /// <summary>造一个切换按钮；**场景缺失/类型不符 ⇒ 返回 null**（调用方回落 `new Button`，不崩不静默）✓</summary>
    public static BuildingNavButtonTemplate? TryCreate(string text)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        BuildingNavButtonTemplate? nav = packed?.Instantiate<BuildingNavButtonTemplate>();
        if (nav is null)
        {
            return null;
        }

        nav.Text = text;
        return nav;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            Text = PreviewText;
        }
    }
}
