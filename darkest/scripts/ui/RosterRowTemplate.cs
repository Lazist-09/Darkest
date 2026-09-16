using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **名册行模板**（用户 2026-09-17：「修改目前所有 UI ⇒ 能在编辑器里直接干预」）✓
///
/// 定位：**动态列表项的模板场景**（`scenes/ui/roster_row.tscn`）—— 由宿主用 `PackedScene` 实例化，
/// 每次实例化后**只填数据**（名字/等级/压力/防御/原型色/头像），**不再 `new` 控件** ✓
///
/// 🔴 `[Tool]` ⇒ **在编辑器里就能看到真实样式与占位内容**（改这个场景 = 改所有名册行的外观）✓
/// ⚠️ 纪律：**编辑器逻辑必须 `Engine.IsEditorHint()` 守卫**（绝不在运行时执行编辑器分支）✓
/// ⚠️ 节点名与既有代码/验收读数**保持一致**（`RosterRowBody` / `PortraitFrame` / `PortraitPlaceholder` / `RosterInfo`），
///    这样 `--ui-audit`、S1 骨架指纹、冒烟"右键头像 ⇒ 详情"都**继续有效** ✓
/// </summary>
[Tool]
public partial class RosterRowTemplate : Button
{
    /// <summary>编辑器预览用文案（运行时会被真实数据覆盖）✓</summary>
    [Export]
    public string PreviewInfo { get; set; } = "Lv2　●●●●○○○○○○　防10　·可减压";

    /// <summary>模板里的三个节点（编辑器与运行时的**同一套**取法）✓</summary>
    public Label? InfoLabel => GetNodeOrNull<Label>("RosterRowBody/RosterInfo");

    public Control? PortraitFrame => GetNodeOrNull<Control>("RosterRowBody/PortraitFrame");

    public ColorRect? PortraitPlaceholder =>
        GetNodeOrNull<ColorRect>("RosterRowBody/PortraitFrame/PortraitPlaceholder");

    public override void _Ready()
    {
        // 🔴 只在编辑器里填"占位内容"，让设计时就能看到行长什么样 ✓
        if (Engine.IsEditorHint() && InfoLabel is not null)
        {
            InfoLabel.Text = PreviewInfo;
        }
    }
}
