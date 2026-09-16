using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **5/6 号位行模板**（用户 2026-09-17：「重复的 UI 元素记得能复用就建成能复用的」）✓
///
/// 复用：战斗底栏左侧长条框里的**两行**（5 号位 / 6 号位）——**同构 2 处** ⇒ 抽模板
/// ⇒ **改这一处 = 两行一起变**（标题字号 / 立绘框尺寸 / 名字换行都能在编辑器里改）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见外观；⚠️ 编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// ⚠️ 节点名保持：`Title` / `Frame` / `Placeholder` / `Name`（宿主按名取；`TryCreate` 会重命名成
///    `BackSlot{n}Title` / `BackSlot{n}Frame` / `BackSlot{n}Name`，与既有验收读数一致）✓
/// </summary>
[Tool]
public partial class SlotRowTemplate : VBoxContainer
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/slot_row.tscn";

    /// <summary>编辑器预览用的槽位号（运行时被真实槽位覆盖）✓</summary>
    [Export]
    public int PreviewSlot { get; set; } = 5;

    public Label? TitleLabel => GetNodeOrNull<Label>("Title");

    public PanelContainer? Frame => GetNodeOrNull<PanelContainer>("Frame");

    public ColorRect? Placeholder => GetNodeOrNull<ColorRect>("Frame/Placeholder");

    public Label? NameLabel => GetNodeOrNull<Label>("Name");

    /// <summary>造一行；**场景缺失/类型不符 ⇒ 返回 null**（调用方回落代码构建，不崩不静默）✓</summary>
    public static SlotRowTemplate? TryCreate(int slot)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        SlotRowTemplate? row = packed?.Instantiate<SlotRowTemplate>();
        if (row is null)
        {
            return null;
        }

        // 🔴 **先取到局部变量，再改名**（我踩过的坑：先改名 ⇒ 旧路径取不到 ⇒ NRE 每帧）
        Label? title = row.TitleLabel;
        PanelContainer? frame = row.Frame;
        Label? name = row.NameLabel;

        row.Name = $"BackSlot{slot}Row";
        if (title is not null)
        {
            title.Name = $"BackSlot{slot}Title";
            title.Text = $"{slot} 号位";
        }

        if (frame is not null)
        {
            frame.Name = $"BackSlot{slot}Frame";
        }

        if (name is not null)
        {
            name.Name = $"BackSlot{slot}Name";
        }

        return row;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint() && TitleLabel is not null)
        {
            TitleLabel.Text = $"{PreviewSlot} 号位";
        }
    }
}
