using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **Overlay 层**（2026-09-21 架构诊断 Track 3：对齐 DD 的 `fe_flow/overlays`）✓
///
/// 定位：**一切"浮在屏上的东西"都只住在这里** —— 而不是各自挂在各屏的树上：
/// ```
/// OverlayLayer (Control · mouse_filter=Ignore ⇒ 空层不吃鼠标)
/// ├ ModalHost   （模态：不透明 PanelContainer + ✕；同一时刻只保留栈顶可交互）
/// └ TooltipHost （悬停说明：**优先用 `scenes/ui/tooltip.tscn` 模板** ⇒ 外观在编辑器可改 ✓）
/// ```
/// ⇒ 实现 `ui_spec §11.4⑤` 的**信息分层**：**常显（面板）→ 悬停（tooltip）→ 点开（modal）** ✓
///
/// 🔴 命名纪律（用户 2026-09-21）：命名空间一律 **`Darkest.UI`**（大写 UI），类/文件用大写缩写风格 ✓
/// ⚠️ **接线状态：已接线**（战斗侧 `BattleUI.Build + Modals.MakeOpaqueModal` ✓ · 城池侧 `HamletRoot.PopupMenu.MakePopup` ✓；
///    两处挂载点均为 `Overlay 缺失 ⇒ 回落旧父容器`，不崩不静默）✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见结构；编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class OverlayLayer : Control
{
    /// <summary>骨架场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/overlay_layer.tscn";

    private readonly System.Collections.Generic.List<Control> _modals = new();
    private Control? _tooltip;

    public Control? ModalHost => GetNodeOrNull<Control>("ModalHost");

    public Control? TooltipHost => GetNodeOrNull<Control>("TooltipHost");

    /// <summary>当前模态栈深（供冒烟/自检读数）✓</summary>
    public int ModalDepth => _modals.Count;

    /// <summary>实例化骨架；场景缺失/类型不符 ⇒ null（宿主回落旧路径，不崩不静默）✓</summary>
    public static OverlayLayer? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        OverlayLayer? layer = packed?.Instantiate<OverlayLayer>();
        if (layer is null)
        {
            GD.Print($"[UI Overlay] `{ScenePath}` 不可用 ⇒ 回落各屏自挂（不静默）✓");
            return null;
        }

        GD.Print($"[UI Overlay] ✅ Overlay 层就绪（`{ScenePath}`）：模态 + 悬停分层 ✓");
        return layer;
    }

    /// <summary>🔴 **统一开模态**（Track 3）：把模态挂进 `ModalHost` 并压栈 ✓</summary>
    public void OpenModal(Control? modal)
    {
        if (modal is null)
        {
            return;
        }

        Control host = ModalHost ?? this;
        if (modal.GetParent() is null)
        {
            host.AddChild(modal);
        }

        modal.Visible = true;
        _modals.Remove(modal);
        _modals.Add(modal);
        GD.Print($"[UI Overlay] OpenModal：`{modal.Name}`（栈深 {_modals.Count}）✓");
    }

    /// <summary>关掉栈顶模态（✕ / Esc 的第二出口由此统一）✓</summary>
    public bool CloseTopModal()
    {
        while (_modals.Count > 0)
        {
            Control top = _modals[^1];
            _modals.RemoveAt(_modals.Count - 1);
            if (GodotObject.IsInstanceValid(top))
            {
                top.Visible = false;
                GD.Print($"[UI Overlay] CloseTopModal：`{top.Name}`（余 {_modals.Count}）✓");
                return true;
            }
        }

        return false;
    }

    /// <summary>悬停说明（§11.4⑤ 的中间层）：只读、不接管点击；**优先用模板场景** ✓</summary>
    public void ShowTooltip(string text, Vector2 at)
    {
        if (string.IsNullOrEmpty(text))
        {
            HideTooltip();
            return;
        }

        if (_tooltip is null || !GodotObject.IsInstanceValid(_tooltip))
        {
            _tooltip = TooltipTemplate.TryCreate(text) ?? BuildFallbackTooltip(text);
            (TooltipHost ?? this).AddChild(_tooltip);
            GD.Print("[UI Overlay] 悬停层已建（模板优先，缺失回落代码构建）✓");
        }

        TooltipTemplate.SetText(_tooltip, text);
        _tooltip.Position = at;
        _tooltip.Visible = true;
    }

    /// <summary>模板缺失时的回落（不崩、不静默）✓</summary>
    private static Control BuildFallbackTooltip(string text)
    {
        var box = new PanelContainer { Name = "OverlayTooltip", MouseFilter = MouseFilterEnum.Ignore };
        var label = new Label { Name = "TooltipText", Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", DdTheme.FontSmall);
        box.AddChild(label);
        GD.Print("[UI Overlay] `tooltip.tscn` 不可用 ⇒ 回落代码构建的悬停框（不静默）✓");
        return box;
    }

    public void HideTooltip()
    {
        if (_tooltip is not null && GodotObject.IsInstanceValid(_tooltip))
        {
            _tooltip.Visible = false;
        }
    }

    /// <summary>`Esc`（`ui_cancel`，引擎内置动作）关栈顶模态 —— 与 ✕ 等价的第二出口 ✓</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") && CloseTopModal())
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            GD.Print("[UI Overlay] 编辑器预览：OverlayLayer（ModalHost / TooltipHost）✓");
        }
    }
}
