using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **战斗屏底栏骨架**（用户 2026-09-17：「能在编辑器里直接干预」+ 参考图②的五区布局）✓
///
/// 定位：`scenes/ui/battle_bottombar.tscn` —— 底栏的**五区结构**（顺序即布局，按运行顺序排）：
/// ```
/// BottomRowBox (HBox · 间距 10)                 ← 骨架根（`BattleUI` 的 `_bottomRow` 指向它）
/// ├ BackSlot5      (PanelContainer · 72×112 · **左靠** ShrinkBegin)   ← 5/6 号位长条框（左）
/// ├ LeftStack      (VBox · ExpandFill)
/// │ ├ CArea        (PanelContainer · 最小宽 260)                      ← **橙框**：当前角色 + 技能方块
/// │ └ ActorDetailBox (PanelContainer · 最小高 84)                     ← **技能框下方的角色详情框**
/// └ EArea          (PanelContainer · ExpandFill 横竖)                 ← **紫框**：多功能框（右侧）
/// ```
/// ⇒ **各区的宽度/最小尺寸/间距/对齐方式** 全部可在编辑器里改 ✓
///
/// 🔴🔴 2026-10-03（架构纠偏 · 真缺陷）：**屏幕级占位**（状态托盘 / 地图角 / 攻击覆盖位 / 怪物面板 /
///   换位按钮 / 库存网格 ＋ `RaidPos*`·`RaidSec*`·`RaidX*`·`RaidTorch*`·`RaidCampLayer`·
///   `RaidQuestInfoLayer`·`InvItemIconBody`，共 33 项）**曾经**也写在本文件里当 `BottomRowBox` 的
///   直接子节点 ⚠️ —— 可它们的锚点全是**屏幕比例**（x/1920, y/1080），被 HBox 按流式排布 ⇒ 最小宽累加到
///   **5310 px**（实测），整条底栏被撑爆、E 区被拉成巨宽，骨架自身 `get_combined_minimum_size()` = 2232×360。
///   ⇒ 已**全部搬进** `scenes/ui/battle_overlay.tscn`（挂 FullRect 的 `_uiRoot`，锚点才对得到整块画布）。
///   ⚠️ 纪律：**屏幕空间的东西一律住 `battle_overlay.tscn`**，不要再往底栏 HBox 里加（见 `BattleUI.Build.cs` 注释）✓
///
/// ⚠️ `DungeonHost` **不在骨架里**（`BattleUI.Dungeon.cs` 惰性代码建、挂 `_bottomRow`）—— 名字保留即可 ✓
///
/// ✅ **接线状态：已接线**（`BattleUI.BuildBottomRow()` 采用之；2026-09-21 验证：`[UI 骨架] ✅ 战斗底栏采用骨架` + 七入口真错 0）✓
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

    // ⚠️ `StatusTray` 属性已于 2026-10-03 **删除**：托盘是屏幕空间的东西 ⇒ 搬进 `battle_overlay.tscn`，
    //    骨架里再也取不到它。取用方＝`BattleUI.Build.cs`（`overlay.GetNodeOrNull<Control>("StatusTray")`）
    //    与 `BattleUI.StatusTray.cs`（兜底重解析）。留一个**永远返回 null** 的属性只会骗下一个读代码的人 ✗

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

