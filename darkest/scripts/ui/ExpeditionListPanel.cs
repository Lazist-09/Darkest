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
///
/// 🔴 **`#319` 布局基建改类（实机取证）**：本类**原来是 `CanvasLayer`** —— 而 `CanvasLayer` **不是 `Control`**
/// ⇒ ① 它内部那个**手写 `Position/Size` 的 `Label`（1232×620）无人接管** ⇒ **压住** ScoutMark / MapStatus /
/// MapOptionsTitle（判据实测 3 对重叠，矩形取证见 `LayoutAudit` 的 `pos/size` 输出）；
/// ② 容器不排它 ⇒ 它自己也进不了容器树。
/// ⇒ 正解 = 改成 **`PanelContainer`**（Control 系）+ 内部 **`VBoxContainer`** 堆叠，由容器给 Label 定尺寸 ✓
/// </summary>
public partial class ExpeditionListPanel : PanelContainer
{
    private Label _body = null!;

    public override void _Ready()
    {
        // 🔴 `ui_spec §14.2`③：面板内部 = 容器堆叠（子项自动排布 ⇒ 物理上不可能重叠）
        var col = new VBoxContainer { Name = "ExpeditionListCol" };
        AddChild(col);

        _body = new Label
        {
            Name = "ExpeditionListBody",
            // 🔴 不再手写 `Position`/`Size`（那是"文字各种重叠"的根源）；尺寸交给容器
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            // §14.2④：容器/文本必须给【最小尺寸】，否则高度塌陷 ⇒ 又重叠
            CustomMinimumSize = new Vector2(0, 140),
        };
        col.AddChild(_body);

        Visible = false;
        ClipBody(); // 🔴 相机口径：内容裁切，不撑宽父容器 ✓
    }

    /// <summary>刷新面板内容（传入内核投影产出的行）。</summary>
    /// <summary>🔴 相机口径（规则①）：本面板文本**不得撑宽父容器** ⇒ 内容裁切（超出省略）✓</summary>
    private void ClipBody()
    {
        if (_body is null) { return; }
        _body.AutowrapMode = TextServer.AutowrapMode.Off;
        _body.ClipText = true;
        _body.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _body.CustomMinimumSize = new Vector2(0, 0);
    }

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
