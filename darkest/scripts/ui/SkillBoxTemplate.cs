using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **技能方块模板**（用户 2026-09-17：「像 DD 那样只用简单小方块表示行动」+「UI 要能在编辑器里直接干预」）✓
///
/// 定位：**战斗 C 区技能栏的方块模板**（`scenes/ui/skill_box.tscn`）—— 宿主用 `PackedScene` 实例化，
/// 之后**只填数据**（两字简称 / 可用性 / 悬停说明 / 点击回调），**不再 `new` 控件** ✓
///
/// 🔴 `[Tool]` ⇒ **编辑器里就能看到方块的真实尺寸/字号/样式**（改这个场景 = 改所有技能方块）✓
/// ⚠️ 纪律：编辑器逻辑必须 `Engine.IsEditorHint()` 守卫（运行时绝不走编辑器分支）✓
/// ⚠️ 尺寸/字号以**场景为准**（`48×48`、`font_size 20` 现在可在编辑器里直接调）——
///    这正是用户要的"能在编辑器里直接干预" ✓
/// </summary>
[Tool]
public partial class SkillBoxTemplate : Button
{
    /// <summary>模板场景路径（宿主用它实例化）✓</summary>
    public const string ScenePath = "res://scenes/ui/skill_box.tscn";

    /// <summary>编辑器预览用简称（运行时会被真实技能名覆盖）✓</summary>
    [Export]
    public string PreviewText { get; set; } = "重劈";

    /// <summary>实例化模板；场景缺失/类型不符 ⇒ 返回 null（宿主回落代码构建，不崩不静默）✓</summary>
    public static SkillBoxTemplate? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.Print($"[UI 模板] `{ScenePath}` 不存在 ⇒ 技能方块回落代码构建（不静默）✓");
            return null;
        }

        SkillBoxTemplate? box = packed.Instantiate<SkillBoxTemplate>();
        if (box is null)
        {
            GD.Print($"[UI 模板] `{ScenePath}` 根节点不是 `SkillBoxTemplate` ⇒ 回落代码构建（不静默）✓");
            return null;
        }

        return box;
    }

    public override void _Ready()
    {
        // 🔴 只在编辑器里填"占位文字"，让设计时就能看到方块长什么样 ✓
        if (Engine.IsEditorHint())
        {
            Text = PreviewText;
        }
    }
}
