using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **战斗屏顶栏骨架**（用户 2026-09-17：「能在编辑器里直接干预」+「重复元素抽模板」）✓
///
/// 定位：`scenes/ui/battle_topbar.tscn` = 战斗屏顶栏的**分区骨架**（用户参考图②：**左上=任务与撤退 ／ 正中=火把条 ／ 右侧=顺序与意图**）：
/// ```
/// BattleTopRow (HBox)
/// ├ TopLeftGroup (HBox) → MissionLabel      ← 左上：任务（+ 代码追加的撤退/放弃远征按钮）
/// ├ TorchWrap (CenterContainer · ExpandFill) ← 正中：火把条容器（**火把条本体仍由代码建**：LightBarPanel 是 C# 类）
/// └ RightGroup (HBox)                        ← 右侧：顺序/意图/进度/日志（内容仍由代码追加）
/// ```
/// ⇒ **各分区的位置/间距/宽度占比** 可在编辑器里改 ✓
///
/// ✅ **接线状态：已接线**（`BattleUI.BuildTopRow()` 采用之；2026-09-21 验证：`[UI 骨架] ✅ 战斗顶栏采用骨架`）✓
/// ⚠️ **火把条本体（`LightBarPanel`）不在骨架里**（它是 C# 类，手写 `.tscn` 放不进脚本类实例 ⇒ **不猜**，
///    仍由代码 `new LightBarPanel()` 建，只是**容器** `TorchWrap` 进骨架）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见结构；编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// ⚠️ 节点名保持：`TopLeftGroup` / `MissionLabel` / `TorchWrap` / `RightGroup` ✓
/// </summary>
[Tool]
public partial class BattleTopBarSkeleton : HBoxContainer
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/battle_topbar.tscn";

    public HBoxContainer? TopLeftGroup => GetNodeOrNull<HBoxContainer>("TopLeftGroup");

    public Label? MissionLabel => GetNodeOrNull<Label>("TopLeftGroup/MissionLabel");

    public CenterContainer? TorchWrap => GetNodeOrNull<CenterContainer>("TorchWrap");

    public HBoxContainer? RightGroup => GetNodeOrNull<HBoxContainer>("RightGroup");

    /// <summary>实例化骨架；场景缺失/类型不符 ⇒ null（宿主回落代码构建，不崩不静默）✓</summary>
    public static BattleTopBarSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        BattleTopBarSkeleton? skel = packed?.Instantiate<BattleTopBarSkeleton>();
        if (skel is null)
        {
            GD.Print($"[UI 骨架] `{ScenePath}` 不可用 ⇒ 战斗顶栏回落代码构建（不静默）✓");
            return null;
        }

        GD.Print($"[UI 骨架] ✅ 战斗顶栏采用骨架 `{ScenePath}`（**编辑器里可编辑**）✓");
        return skel;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint() && MissionLabel is not null && string.IsNullOrEmpty(MissionLabel.Text))
        {
            MissionLabel.Text = "任务：本趟 第 0 步　已胜 0";
        }
    }
}
