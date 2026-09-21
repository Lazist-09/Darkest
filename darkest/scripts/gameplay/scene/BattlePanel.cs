using System;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **B-1（形态 B · S4）的主程序侧一半**：**把战斗包成一个外壳可挂的「面板」** ✓
///
/// WHY 需要它（架构原话）：
///   "**战斗必须真正成为 panel（S4）** —— 现在 `BattleRoot` 是**场景根** ⇒ 外壳 `ShowPanel` 只释放 panel
///    ⇒ **两套驱动同活** ⚠️"
/// 事实核对（我读代码确认）：
///   · 外壳的 panel 契约 = `IUiPanel`（只有两个成员：`PanelName` / `WantsBaseHud` ✓）+ 必须是 `Control`
///     并挂进 `UIRoot.ScreenLayer` ✓
///   · 而 **`BattleRoot : Node2D`** ✗ ⇒ **它本身不能当 panel** ✓
/// ⇒ 所以本类是**适配器**：一个 `Control`，里面挂着 `BattleRoot`（**不改 BattleRoot 的基类** ✓ 零风险）
///
/// 🔴 **零行为**：本类**不被任何默认路径调用** ✓ 只有显式的 `--battle-panel` 旗标（我域）才会用它 ✓
///   激活条件（写清楚，避免"填了不消费"）：见 `reports/b1_battle_panel_plan.md` ✓
/// </summary>
public partial class BattlePanel : Control, Darkest.UI.IUiPanel
{
    /// <summary>面板名（外壳日志/断言用 ✓）</summary>
    public string PanelName => "battle";

    /// <summary>战斗需要**常显 HUD**（外壳据此决定 BaseLayer 可见性 ✓ 声明式）</summary>
    public bool WantsBaseHud => true;

    /// <summary>里面那个战斗组合根（`Node2D` ✓ 保持原样，不做基类改造）</summary>
    public BattleRoot? Root { get; private set; }

    /// <summary>
    /// 造一个「战斗面板」：`Control`（全屏锚点）+ 其下挂一个 `BattleRoot` ✓
    /// 🔴 用 `BattleRoot` 的**公开成员**装配（不碰它的私有流程 ✓）
    /// </summary>
    public static BattlePanel Create(string name = "BattlePanel")
    {
        var panel = new BattlePanel
        {
            Name = name,
            // 全屏锚点（与项目既有做法一致：`SetAnchorsAndOffsetsPreset(FullRect)` ✓）
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Pass,
        };

        // 🔴 **必须实例化【战斗场景】而不是裸造类**（我第一版裸造 ⇒ 实测报
        //    `Node not found: "UILayer/BattleUI"` ✗ —— 因为那个 UILayer 是**场景里的兄弟节点**，
        //    不在代码里。这一点是**跑出来**才知道的 ✓）
        const string BattleScenePath = "res://scenes/battle/Battle.tscn";
        var packed = GD.Load<PackedScene>(BattleScenePath);
        if (packed is null)
        {
            GD.Print($"[BattlePanel] 🔴 场景加载失败：{BattleScenePath} ⇒ **如实不建面板**（不假装成功 ✓）");
            return panel;
        }

        var battle = packed.Instantiate<BattleRoot>();
        battle.Name = "BattleRoot";
        panel.AddChild(battle);
        panel.Root = battle;

        // 🔴 自证行：冒烟里能直接看到"面板已造好、里面挂着谁" ✓（与项目"留痕"风格一致 ✓）
        GD.Print($"[BattlePanel] 已创建：panel={panel.Name} · 内挂 {battle.GetType().Name}"
            + $" · PanelName={panel.PanelName} · WantsBaseHud={panel.WantsBaseHud} ✓");
        return panel;
    }

    /// <summary>挂到外壳（缺失外壳 ⇒ **如实报 "未接线"**，不假装成功 ✓）</summary>
    public bool TryMountIntoShell()
    {
        Darkest.UI.UIRoot? shell = Darkest.UI.UIRoot.Instance;
        if (shell is null)
        {
            GD.Print("[BattlePanel] 🔴 外壳（UIRoot.Instance）不存在 ⇒ **未接线**（不上屏；这不是成功 ✓）");
            return false;
        }

        shell.ShowPanel(this);   // 外壳只管 Screen 层 ✓（它自己的边界，见 UIRoot 注释）
        GD.Print($"[BattlePanel] 已挂进外壳 ScreenLayer ⇒ 当前面板 = {shell.CurrentPanelName} ✓");
        return true;
    }
}
