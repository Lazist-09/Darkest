using Godot;

namespace Darkest.UI;

/// <summary>
/// 阶段2：DD `shared/controls/controls.layout.darkest`（6 section / 170 字段）⇒ **按键提示屏**骨架
/// 3 个位置块（mouse_kb 450,150 · controller 148,64 · category 280,200）+ 3 套手柄变体占位；全为色块（§14.0.68 不换不删）
/// </summary>
public partial class ControlsSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/controls_skeleton.tscn";

    public PanelContainer? MouseKbPanel => GetNodeOrNull<PanelContainer>("CtrlMouseKbPanel");
    public PanelContainer? ControllerPanel => GetNodeOrNull<PanelContainer>("CtrlControllerPanel");
    public PanelContainer? CategoryStart => GetNodeOrNull<PanelContainer>("CtrlCategoryStart");
    public PanelContainer? VariantStandard => GetNodeOrNull<PanelContainer>("CtrlVariantStandard");
    public PanelContainer? VariantAlternate => GetNodeOrNull<PanelContainer>("CtrlVariantAlternate");
    public PanelContainer? VariantSteamDeck => GetNodeOrNull<PanelContainer>("CtrlVariantSteamDeck");

    public static ControlsSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        ControlsSkeleton? skel = packed?.Instantiate() as ControlsSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 按键提示] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print("[UI-TRACE] controls-skeleton");
        return skel;
    }
}
