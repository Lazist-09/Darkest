using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **战斗屏底栏骨架**（用户 2026-09-17：「能在编辑器里直接干预」+ 参考图②的五区布局）✓
///
/// 定位：`scenes/ui/battle_bottombar.tscn` —— 底栏的**五区结构**（顺序即布局，按运行顺序排）：
/// ```
/// BottomRowBox (HBox · 间距 10)
/// ├ BackSlot5      (PanelContainer · 72×112 · **左靠** ShrinkBegin)   ← 5/6 号位长条框（左）
/// ├ LeftStack      (VBox · ExpandFill)
/// │ ├ CArea        (PanelContainer · 最小宽 260)                      ← **橙框**：当前角色 + 技能方块
/// │ └ ActorDetailBox (PanelContainer · 最小高 84)                     ← **技能框下方的角色详情框**
/// ├ EArea          (PanelContainer · ExpandFill 横竖)                 ← **紫框**：多功能框（右侧）
/// └ DungeonHost    (VBoxContainer · 240 宽 · ShrinkEnd)               ← 地牢宿主（地图模式用）
/// ```
/// ⇒ **各区的宽度/最小尺寸/间距/对齐方式** 全部可在编辑器里改 ✓
///
/// ⚠️ **接线状态：未接线**（`BattleUi.BuildBottomRow()` 仍在代码里建这些容器；接线单独一轮做）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见结构；编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// ⚠️ 节点名保持：`BottomRowBox` / `BackSlot5` / `LeftStack` / `CArea` / `ActorDetailBox` / `EArea` / `DungeonHost` ✓
/// </summary>
[Tool]
public partial class BattleBottomBarSkeleton : HBoxContainer
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/battle_bottombar.tscn";

    public PanelContainer? BackSlot5 => GetNodeOrNull<PanelContainer>("BackSlot5");

    public VBoxContainer? LeftStack => GetNodeOrNull<VBoxContainer>("LeftStack");

    public PanelContainer? CArea => GetNodeOrNull<PanelContainer>("LeftStack/CArea");

    public PanelContainer? ActorDetailBox => GetNodeOrNull<PanelContainer>("LeftStack/ActorDetailBox");

    public PanelContainer? EArea => GetNodeOrNull<PanelContainer>("EArea");

    public VBoxContainer? DungeonHost => GetNodeOrNull<VBoxContainer>("DungeonHost");

    /// <summary>实例化骨架；场景缺失/类型不符 ⇒ null（宿主回落代码构建，不崩不静默）✓</summary>
    public static BattleBottomBarSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        BattleBottomBarSkeleton? skel = packed?.Instantiate<BattleBottomBarSkeleton>();
        if (skel is null)
        {
            GD.Print($"[UI 骨架] `{ScenePath}` 不可用 ⇒ 战斗底栏回落代码构建（不静默）✓");
            return null;
        }

        GD.Print($"[UI 骨架] ✅ 战斗底栏采用骨架 `{ScenePath}`（**编辑器里可编辑**）✓");
        return skel;
    }

    public override void _Ready()
    {
        _ = Engine.IsEditorHint();
    }
}
