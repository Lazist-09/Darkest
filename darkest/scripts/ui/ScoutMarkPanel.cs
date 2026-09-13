using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M7.5 **侦察结果标记**（`ui_spec` 必显 **13 侦察结果标记**）。
///
/// 🔴 规格硬要求（`#262` 策划原话）：**必须与「未知」有明确区分** ——
/// 否则玩家分不清"看清了"和"没看清"（而侦察的价值正是"让选路基于信息"）。
///
/// 🔴 分工：结果取自内核 `ScoutResultEvent` / `ScoutOutcome`（**失败必为 null**）；
/// 本类只做两态渲染：**已揭示（带类型）** / **未知（未揭示）**。
/// </summary>
public partial class ScoutMarkPanel : CanvasLayer
{
    private Label _label = null!;

    public override void _Ready()
    {
        _label = new Label
        {
            Name = "ScoutMark",
            Position = new Vector2(24, 120),
            Size = new Vector2(900, 30),
        };
        AddChild(_label);
    }

    /// <summary>刷新（`outcome` 为空 = 本次未侦察；成功才带类型）。</summary>
    public void Refresh(ScoutOutcome? outcome)
    {
        _label.Text = Describe(outcome);
        // 两态在**视觉上也不同**（文字前缀 + 颜色），不只靠措辞
        _label.Modulate = outcome is { Success: true }
            ? new Color(0.7f, 1.0f, 0.7f)  // 已揭示：偏绿
            : new Color(1.0f, 0.85f, 0.6f); // 未知：偏黄
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
