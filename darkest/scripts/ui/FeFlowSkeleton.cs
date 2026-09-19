using Godot;

namespace Darkest.UI;

/// <summary>阶段2：DD `fe_flow/fe_flow.layout.darkest`（9 section / 76 字段）⇒ 外壳流程骨架
/// 自带尺寸 save_slot_window 1800×434 · answer button 500×55（来源①⇒直接用 ✓）；全为色块占位（§14.0.68 不换不删）</summary>
public partial class FeFlowSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/fe_flow_skeleton.tscn";

    public PanelContainer? SaveSlotWindow => GetNodeOrNull<PanelContainer>("FfSaveSlotWindow");
    public PanelContainer? ScrollBar => GetNodeOrNull<PanelContainer>("FfScrollBar");
    public PanelContainer? ModeSelectDialog => GetNodeOrNull<PanelContainer>("FfModeSelectDialog");
    public PanelContainer? AnswerButton => GetNodeOrNull<PanelContainer>("FfAnswerButton1");
    public PanelContainer? BackButton => GetNodeOrNull<PanelContainer>("FfBackButton");
    public PanelContainer? BuildNum => GetNodeOrNull<PanelContainer>("FfBuildNum");

    public static FeFlowSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        FeFlowSkeleton? skel = packed?.Instantiate() as FeFlowSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 外壳流程] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print("[UI-TRACE] fe-flow-skeleton");
        return skel;
    }
}
