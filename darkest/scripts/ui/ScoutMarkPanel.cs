using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;

/// <summary>
/// M7.5 **侦察结果标记**（`ui_spec` 必显 **13 侦察结果标记**）。
///
/// 🔴 规格硬要求（`#262` 策划原话）：**必须与「未知」有明确区分** ——
/// 否则玩家分不清"看清了"和"没看清"（而侦察的价值正是"让选路基于信息"）。
///
/// 🔴 分工：结果取自内核 `ScoutResultEvent` / `ScoutOutcome`（**失败必为 null**）；
/// 本类只做两态渲染：**已揭示（带类型）** / **未知（未揭示）**。
///
/// 🔴 `ui_spec §14`（`#319`）：本类**从 `CanvasLayer` 改为 `PanelContainer`** ——
///    `CanvasLayer` **不是 `Control`** ⇒ ① 没有"框"（子控件不在 Control 链上 ⇒ 不受 Theme 管）
///    ② **容器不会排它**（`Container` 只管理 Control 子节点）⇒ 无法参与 §14.3 的容器树 ⚠️
///    ⇒ 现在它自己就是一个**不透明面板**（挂 Theme），内部用 `VBox`/`Label` 自动堆叠 ✓
/// </summary>
public partial class ScoutMarkPanel : PanelContainer
{
    private Label _label = null!;

    public override void _Ready()
    {
        Darkest.UI.DdTheme.Apply(this); // 自己就是面板 ⇒ 挂 Theme（不透明样式来自 §14.4）
        var col = new VBoxContainer { Name = "ScoutMarkCol" };
        col.AddThemeConstantOverride("separation", 4);
        AddChild(col);

        _label = new Label
        {
            Name = "ScoutMark",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 24), // §14.2 ④：给最小高度
        };
        col.AddChild(_label);
    }

    /// <summary>刷新（`outcome` 为空 = 本次未侦察；成功才带类型）。</summary>
    public void Refresh(ScoutOutcome? outcome)
    {
        _label.Text = Describe(outcome);
        // 两态在**视觉上也不同**（文字前缀 + 颜色），不只靠措辞
        _label.Modulate = outcome is { Success: true }
            ? Darkest.UI.DdTheme.Positive  // 已揭示：偏绿
            : Darkest.UI.DdTheme.TextHint; // 未知：偏黄
    }

    /// <summary>两态文本（**必须可区分**：未知时不带任何类型信息）。</summary>
    public static string Describe(ScoutOutcome? outcome)
    {
        if (outcome is null)
        {
            return "侦察：未进行";
        }

        if (!outcome.Success || outcome.RevealedNodeType is null)
        {
            return "侦察：未知（未揭示下一节点类型）"; // 🔴 失败/未揭示 ⇒ 不透露任何类型
        }

        string kind = outcome.RevealedNodeType == "battle" ? "普通战" : "事件";
        return $"侦察：已揭示 → 下一节点【{kind}】";
    }
}
