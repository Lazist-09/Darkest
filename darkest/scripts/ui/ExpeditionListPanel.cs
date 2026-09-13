using System.Collections.Generic;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M7.5 远征层**最小列表式界面**（`ui_spec` §2 必显 **11 光照条 / 12 背包格子 / 13 侦察结果标记**，
/// 外加 `expedition.md` §5.5 必显 7 条与 **⑧ 难度档位预告**）。
///
/// 🔴 **本类只做渲染**：
/// · 文本行**只来自内核投影** `ExpeditionProjector.RenderList(...)`（**数字全部来自事件流**）；
/// · **不读会话内部状态**、不含任何游戏逻辑、不掷骰（红线：UI 是机制的一部分，但不能成为第二套状态源）；
/// · 灰显/禁用也由投影给出（`IsFoodTierDisabled` 等），本类只按标记画字。
/// </summary>
public partial class ExpeditionListPanel : CanvasLayer
{
    private Label _body = null!;

    public override void _Ready()
    {
        _body = new Label
        {
            Name = "ExpeditionListBody",
            Position = new Vector2(24, 24),
            Size = new Vector2(1232, 620),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_body);
        Visible = false;
    }

    /// <summary>刷新面板内容（传入内核投影产出的行）。</summary>
    public void Refresh(IReadOnlyList<string> lines)
    {
        _body.Text = string.Join("\n", lines);
    }

    /// <summary>显示（例如：选路 / 扎营 / 回城结算时）。</summary>
    public void ShowPanel() => Visible = true;

    /// <summary>隐藏（回到战斗视图）。</summary>
    public void HidePanel() => Visible = false;

    /// <summary>当前显示的行数（供自检/冒烟断言；不参与游戏逻辑）。</summary>
    public int LineCount => _body is null || string.IsNullOrEmpty(_body.Text) ? 0 : _body.Text.Split('\n').Length;
}
