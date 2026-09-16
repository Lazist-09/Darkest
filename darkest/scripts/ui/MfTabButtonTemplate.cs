using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **多功能框页签模板**（用户 2026-09-17：「重复的 UI 元素记得能复用就建成能复用的」）✓
///
/// 复用：战斗屏右侧【多功能框】的**四个页签**（详情 / 日志 / 行动序列 / 编成 / 地图）——**同构 4~5 处** ⇒ 抽模板
/// ⇒ **改这一处 = 所有页签一起变**（尺寸/字号/主题覆盖在编辑器里改）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见外观；⚠️ 编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class MfTabButtonTemplate : Button
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/mf_tab.tscn";

    /// <summary>编辑器预览用文案（运行时被真实页签名覆盖）✓</summary>
    [Export]
    public string PreviewText { get; set; } = "日志";

    /// <summary>造一个页签；**场景缺失/类型不符 ⇒ 返回 null**（调用方回落 `new Button`，不崩不静默）✓</summary>
    public static MfTabButtonTemplate? TryCreate(string text)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        MfTabButtonTemplate? tab = packed?.Instantiate<MfTabButtonTemplate>();
        if (tab is null)
        {
            return null;
        }

        tab.Text = text;
        return tab;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            Text = PreviewText;
        }
    }
}
