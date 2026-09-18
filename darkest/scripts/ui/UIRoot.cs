using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 **面板的自我声明**（Track 1 · 架构裁定要求「声明式，不许有人记得」）✓
/// 判据（架构原话）：**"新加一个 panel 需要有人【记得】做某件事吗？"需要 ⇒ 接口形状不对** ✓
/// ⇒ panel 只需实现本接口，**声明自己要不要常显 HUD**；由 `UIRoot` 决定默认 ⇒ 新增 panel **不需要改 shell** ✓
/// </summary>
public interface IUiPanel
{
    /// <summary>panel 名（日志/断言用；缺省可用节点名）✓</summary>
    string PanelName { get; }

    /// <summary>本 panel 是否需要**常显 HUD**（BaseLayer 可见）—— **声明式**：panel 说、shell 照做 ✓</summary>
    bool WantsBaseHud { get; }
}

/// <summary>
/// 🔴 **UIRoot 流程外壳**（2026-09-21 架构裁定：DD `fe_flow` 的对应物；**S1 收窄版**）✓
///
/// 三层（**层级不得混用** —— 架构裁定）：
/// ```
/// UIRoot (Control)
/// ├ BaseLayer   （常显 HUD；是否可见由**当前 panel 声明**决定 ⇒ 声明式，无需有人记得 ✓）
/// ├ ScreenLayer （当前 panel：一次只挂一个；ShowPanel 只碰这一层 ✓）
/// └ OverlayLayer（**复用已有** `scenes/ui/overlay_layer.tscn`：模态栈 + 悬停层 ✓）
/// ```
/// 🔴 **边界（裁定原文）**：`ShowPanel` **只管 Screen 层**；**`DungeonView` 的模式切换不经 `UIRoot`** ⇒ 本类
///    **不提供**任何"进战斗/进地图"的模式切换 API ✓（那是各屏自己的事）
///
/// ⚠️ **接线状态（S1 收窄版）**：
///   · ✅ 骨架就位（本文件 + 复用 OverlayLayer）；`ShowPanel/OpenOverlay/Back` 可调用、有留痕、缺失不崩
///   · 🔴 **尚未接管转场**：`BattleRoot.ChangeSceneToFile` 等调用点**未改**（S2~S4 按裁定暂缓）⇒ 现在**没有屏在用本外壳**，
///        属"就位待接管"，**如实标注、不谎报已接管** ✓
///   · 🔴 **autoload 注册不在我域**：`project.godot` 由**主程序**注册（架构裁定分工）⇒ 我提供 `Instance` 与 `TryInstantiate()` 两条路 ✓
///
/// 🔴 命名纪律：命名空间一律 **`Darkest.UI`**（大写 UI），类/文件用大写缩写风格 ✓
/// 🔴 `[Tool]` ⇒ 编辑器里可见结构；编辑器逻辑必须 `Engine.IsEditorHint()` 守卫 ✓
/// </summary>
[Tool]
public partial class UIRoot : Control
{
    /// <summary>场景路径（可作为 autoload 场景注册；**注册由主程序做**）✓</summary>
    public const string ScenePath = "res://scenes/ui/ui_root.tscn";

    /// <summary>autoload 注册后的单例（未注册 ⇒ null；调用方走 `TryInstantiate` 回落）✓</summary>
    public static UIRoot? Instance { get; private set; }

    private Control? _baseLayer;
    private Control? _screenLayer;
    private OverlayLayer? _overlay;
    private Control? _currentPanel;

    public Control? BaseLayer => _baseLayer;

    public Control? ScreenLayer => _screenLayer;

    public OverlayLayer? Overlay => _overlay;

    /// <summary>当前屏名（无屏 ⇒ 空串；供冒烟断言）✓</summary>
    public string CurrentPanelName => _currentPanel is null ? string.Empty : NameOf(_currentPanel);

    /// <summary>当前屏实例（供断言；类型不符 ⇒ null）✓</summary>
    public T? CurrentPanel<T>() where T : class => _currentPanel as T;

    public override void _Ready()
    {
        Instance = this;
        EnsureLayers();

        if (Engine.IsEditorHint())
        {
            GD.Print("[UI Root] 编辑器预览：UIRoot（BaseLayer / ScreenLayer / OverlayLayer）✓");
        }
    }

    /// <summary>建三层（缺场景 ⇒ 代码建；**不崩不静默**）✓</summary>
    private void EnsureLayers()
    {
        _baseLayer ??= GetNodeOrNull<Control>("BaseLayer") ?? NewLayer("BaseLayer");
        _screenLayer ??= GetNodeOrNull<Control>("ScreenLayer") ?? NewLayer("ScreenLayer");

        if (_overlay is null || !GodotObject.IsInstanceValid(_overlay))
        {
            _overlay = GetNodeOrNull<OverlayLayer>("OverlayLayer") ?? OverlayLayer.TryInstantiate();
            if (_overlay is not null && _overlay.GetParent() is null)
            {
                AddChild(_overlay);
            }
        }

        GD.Print($"[UI Root] ✅ 三层就绪：Base={Id(_baseLayer)} ／ Screen={Id(_screenLayer)} ／ Overlay={Id(_overlay)}（声明式 HUD ✓）");
    }

    private Control NewLayer(string name)
    {
        var layer = new Control { Name = name, MouseFilter = MouseFilterEnum.Ignore };
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(layer);
        GD.Print($"[UI Root] `{name}` 不在场景里 ⇒ 回落代码建（不静默）✓");
        return layer;
    }

    /// <summary>独立实例化（未注册 autoload 时用；场景缺失 ⇒ null）✓</summary>
    public static UIRoot? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        UIRoot? root = packed?.Instantiate<UIRoot>();
        if (root is null)
        {
            GD.Print($"[UI Root] `{ScenePath}` 不可用 ⇒ 回落各屏自管（不静默）✓");
        }

        return root;
    }

    /// <summary>
    /// 🔴 **换屏**（S1：只管 Screen 层 ✓）：挂上新 panel、卸下旧 panel，并按**panel 自己的声明**决定常显 HUD ✓
    /// </summary>
    public void ShowPanel(Control? panel)
    {
        EnsureLayers();
        if (panel is null || _screenLayer is null)
        {
            GD.Print("[UI Root] ShowPanel：缺 panel/屏幕层 ⇒ 不动作（留痕，不崩）✓");
            return;
        }

        if (_currentPanel is not null && GodotObject.IsInstanceValid(_currentPanel))
        {
            _currentPanel.Visible = false;
            _currentPanel.QueueFree();
        }

        if (panel.GetParent() is null)
        {
            _screenLayer.AddChild(panel);
        }

        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.Visible = true;
        _currentPanel = panel;

        ApplyHudDeclaration(panel);
        GD.Print($"[UI Root] ShowPanel：`{NameOf(panel)}`（Screen 层；常显 HUD={(WantsHud(panel) ? "开" : "关")}）✓");
    }

    /// <summary>`ShowPanel&lt;T&gt;()`：由场景路径装载（场景名 = `res://scenes/ui/<kebab>.tscn` 由调用方给全路径）✓</summary>
    public T? ShowPanel<T>(string scenePath) where T : Control
    {
        PackedScene? packed = GD.Load<PackedScene>(scenePath);
        T? panel = packed?.Instantiate<T>();
        if (panel is null)
        {
            GD.Print($"[UI Root] ShowPanel<{typeof(T).Name}>：`{scenePath}` 装载失败 ⇒ 不换屏（留痕）✓");
            return null;
        }

        ShowPanel(panel);
        return panel;
    }

    /// <summary>按 panel 的声明决定常显 HUD（**声明式**：没人需要"记得"）✓</summary>
    private void ApplyHudDeclaration(Control panel)
    {
        if (_baseLayer is not null && GodotObject.IsInstanceValid(_baseLayer))
        {
            _baseLayer.Visible = WantsHud(panel);
        }
    }

    private static bool WantsHud(Control panel) => panel is IUiPanel ui && ui.WantsBaseHud;

    private static string NameOf(Control panel) => panel is IUiPanel ui && !string.IsNullOrEmpty(ui.PanelName) ? ui.PanelName : panel.Name;

    private static string Id(Node? n) => n is null ? "无" : n.GetInstanceId().ToString();

    /// <summary>开模态（**转发 Overlay 层**，层级不混用）✓</summary>
    public void OpenOverlay(Control? modal)
    {
        EnsureLayers();
        _overlay?.OpenModal(modal);
    }

    /// <summary>返回：先关栈顶模态；否则 false（**不由本类管屏栈** —— S1 只管一层，S2 起再议）✓</summary>
    public bool Back()
    {
        if (_overlay is not null && GodotObject.IsInstanceValid(_overlay) && _overlay.CloseTopModal())
        {
            return true;
        }

        return false;
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}
