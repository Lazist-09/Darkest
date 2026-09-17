using System;
using System.Collections.Generic;
using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 `#327` **片 2 #5：Curio（事件房）面板的【内容】独立化**（从 `ExpeditionRoot` 的内联模态里抽出来）。
/// 目的：让**任何宿主**都能复用同一份内容（远征场景 / 战斗的"地图模式" —— 后者等片 3 的**行走相位**到位后挂）。
///
/// 🔴 分工（照 `§13.8` 抽取方案）：
/// · **模态/外框由宿主提供**（`ExpeditionRoot.MakeModal` 仍归它）
/// · 本类只管**内容**：说明文本 + 逐选项按钮
/// · "有哪些选项 / 点了做什么"**由宿主给**（宿主知道 `_flow`）⇒ 本类**不碰内核**、**不重算数字** ✓
/// </summary>
public partial class CurioPanel : PanelContainer
{
    private Label _text = null!;
    private VBoxContainer _box = null!;
    private readonly List<Button> _buttons = new();

    /// <summary>逐选项按钮（顺序 = 宿主传入的选项顺序；供宿主/冒烟按序点击）✓</summary>
    public IReadOnlyList<Button> Buttons => _buttons;

    /// <summary>说明文本（宿主写文案；本类不改写）✓</summary>
    public Label Text => _text;

    public override void _Ready()
    {
        var col = new VBoxContainer { Name = "CurioCol" };
        col.AddThemeConstantOverride("separation", 6);
        AddChild(col);

        _text = new Label
        {
            Name = "CurioText",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 48), // `§14.2④`：给最小尺寸防塌陷
        };
        col.AddChild(_text);

        _box = new VBoxContainer { Name = "CurioActions" };
        _box.AddThemeConstantOverride("separation", 6);
        col.AddChild(_box);
        Visible = false;
    }

    /// <summary>宿主无关刷新：说明文本 + 逐选项（`label` 显示、`onPick` 由宿主决定做什么）✓</summary>
    public void Refresh(string description, IReadOnlyList<(string Label, Action OnPick)> options)
    {
        ClearButtons();
        _text.Text = description;

        foreach ((string label, Action onPick) in options)
        {
            string text = label;
            Action pick = onPick;
            var b = new Button
            {
                Name = $"CurioOption_{_buttons.Count}",
                Text = text,
                CustomMinimumSize = new Vector2(360, 32),
            };
            b.Pressed += pick;
            _box.AddChild(b); // 🔴 进容器（不手摆坐标）✓
            _buttons.Add(b);
        }

        Visible = true;
    }

    /// <summary>清空按钮（内容层）✓</summary>
    public void ClearButtons()
    {
        foreach (Button b in _buttons)
        {
            if (GodotObject.IsInstanceValid(b))
            {
                _box.RemoveChild(b);
                b.QueueFree();
            }
        }

        _buttons.Clear();
    }

    /// <summary>清空并隐藏（宿主收起模态时调用）✓</summary>
    public void Clear()
    {
        ClearButtons();
        Visible = false;
    }
}
