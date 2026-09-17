using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **弹窗内容行模板**（用户 2026-09-17：「UI 要能在编辑器里直接干预」）✓
///
/// 定位：**所有二级/三级弹窗里的一行文本**（`scenes/ui/popup_line.tscn`）—— 由弹窗构建处实例化，
/// 之后**只填文本**（`Text = "…"`），**不再 `new Label`** ✓
/// 🔴 `[Tool]` ⇒ 编辑器里能看到行的换行/裁切/字号等外观（改这里 = 改所有弹窗行）✓
/// ⚠️ 纪律：编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class PopupLineTemplate : Label
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/popup_line.tscn";

    /// <summary>编辑器预览用文案（运行时会被真实文本覆盖）✓</summary>
    [Export]
    public string PreviewText { get; set; } = "（弹窗内容行 · 在编辑器里改这一行 = 改所有弹窗行）";

    /// <summary>造一行；**场景缺失/类型不符 ⇒ 返回 null**（调用方回落 `new Label`，不崩不静默）✓</summary>
    public static PopupLineTemplate? TryCreate(string text)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        PopupLineTemplate? line = packed?.Instantiate<PopupLineTemplate>();
        if (line is null)
        {
            return null;
        }

        line.Text = text;
        return line;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            Text = PreviewText;
        }
    }
}
