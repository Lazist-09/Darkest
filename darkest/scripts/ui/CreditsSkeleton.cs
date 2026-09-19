using Godot;

namespace Darkest.UI;

/// <summary>阶段2：DD `shared/credits/credits.layout.darkest`（5 section / 37 字段）⇒ 制作人员屏骨架
/// 可用几何：base_pos 0,0（背景）· back_button_pos 64,148（返回）；其余为动画时序参数（无几何）
/// 全为 ColorRect 色块占位（§14.0.68 不换不删）</summary>
public partial class CreditsSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/credits_skeleton.tscn";

    public PanelContainer? BasePanel => GetNodeOrNull<PanelContainer>("CrBasePanel");
    public PanelContainer? BackButton => GetNodeOrNull<PanelContainer>("CrBackButton");
    public PanelContainer? ScrollBody => GetNodeOrNull<PanelContainer>("CrScrollBody");

    public static CreditsSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        CreditsSkeleton? skel = packed?.Instantiate() as CreditsSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 制作人员] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print("[UI-TRACE] credits-skeleton");
        return skel;
    }
}
