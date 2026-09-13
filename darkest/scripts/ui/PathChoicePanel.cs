using System;
using System.Collections.Generic;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M7.5 **选路界面**（M7 E1 的界面端；每步**恰 2 个可选项**，二选一）。
///
/// 🔴 分工：候选与代价提示取自内核（`PathStep.Options` / 节点数据）；
/// 本类只把候选画成按钮并**转发选择**给 `ExpeditionPathPlanner.ChoosePath`（由调用方传入回调）。
/// 🔴 阶段一每步恰 2 个可选项 ⇒ **不存在"只有 1 个选项"或"跳过"的按钮**（P20 ⑤ / E1 验收）。
/// </summary>
public partial class PathChoicePanel : CanvasLayer
{
    private VBoxContainer _box = null!;
    private Label _title = null!;
    private readonly List<Button> _buttons = new();

    public override void _Ready()
    {
        _title = new Label
        {
            Name = "PathTitle",
            Position = new Vector2(24, 160),
            Size = new Vector2(900, 30),
        };
        AddChild(_title);

        _box = new VBoxContainer
        {
            Name = "PathOptions",
            Position = new Vector2(24, 196),
        };
        AddChild(_box);
        Visible = false;
    }

    /// <summary>渲染某一步的 2 个候选；点击回调携带下标（0/1）。</summary>
    /// <param name="nodes">可选：传入节点表 ⇒ 候选文本显示**真实的必然代价与选项效果**（#272 后事件节点带 cost）。</param>
    public void Refresh(PathStep step, Action<int> onChoose, Darkest.Data.ExpeditionNodesConfig? nodes = null)
    {
        foreach (Node child in _box.GetChildren())
        {
            child.QueueFree();
        }

        _buttons.Clear();
        _title.Text = $"第 {step.Index + 1} 步：二选一（不可跳过）";

        for (int i = 0; i < step.Options.Count; i++)
        {
            int index = i; // 闭包捕获
            PathOption option = step.Options[i];
            Darkest.Data.ExpeditionNodeConfig? node =
                nodes is not null && option.NodeType == "event" ? nodes.Get(option.NodeId) : null;

            var button = new Button
            {
                Name = $"PathOption{i}",
                Text = $"{Describe(option, node)}（选它）",
                CustomMinimumSize = new Vector2(520, 36),
            };
            button.Pressed += () => onChoose(index);
            _box.AddChild(button);
            _buttons.Add(button);
        }

        Visible = true;
    }

    public void HidePanel() => Visible = false;

    /// <summary>候选展示文本：**节点类型 + 代价提示**（④ 必显）。</summary>
    public static string Describe(PathOption option) => option.NodeType switch
    {
        "battle" => $"普通战 {option.NodeId}：有伤亡风险（打完可扎营）",
        "event" => $"事件 {option.NodeId}：二选一，有代价（不掉血但可能掉士气/资源）",
        _ => $"{option.NodeType} {option.NodeId}",
    };

    /// <summary>
    /// 带**真实代价**的候选文本（#272：事件 = 必然代价 + 可选收益）：
    /// 例：`事件 ev_shrine：必然代价 士气-5 ｜ 选它→ 士气+5` —— 玩家要能**算得出**。
    /// </summary>
    public static string Describe(PathOption option, Darkest.Data.ExpeditionNodeConfig? node)
    {
        if (node is null || option.NodeType != "event")
        {
            return Describe(option);
        }

        int mandatory = node.Cost?.Morale ?? 0;
        var parts = new List<string>();
        foreach (Darkest.Data.NodeOptionConfig opt in node.Options)
        {
            string resource = opt.Effect.Resource is { } r && opt.Effect.Delta != 0
                ? $"{r}{opt.Effect.Delta:+#;-#;0}"
                : "无";
            parts.Add($"{opt.Choice}→ {resource} / 士气{opt.Effect.Morale:+#;-#;0}");
        }

        return $"事件 {option.NodeId}：**必然代价 士气{mandatory:+#;-#;0}** ｜ {string.Join(" ｜ ", parts)}";
    }
}
