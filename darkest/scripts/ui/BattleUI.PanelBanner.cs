using Godot;

namespace Darkest.UI;

/// <summary>阶段2 施工④：战斗横幅控制器（DD panel.banner）—— 与 BattleUI 同 partial ✓</summary>
public partial class BattleUI : Control
{
    private PanelBannerSkeleton? _banner;

    /// <summary>显示战斗横幅（骨架优先；缺失 ⇒ 回落一行说明，不崩不静默）✓</summary>
    public void ShowBanner()
    {
        if (_banner is not null && GodotObject.IsInstanceValid(_banner))
        {
            return;
        }

        PanelBannerSkeleton? skel = PanelBannerSkeleton.TryInstantiate();
        if (skel is null)
        {
            GD.Print("[UI 横幅] 骨架不可用 ⇒ 不显示（如实留痕，不静默）");
            return;
        }

        _uiRoot.AddChild(skel);
        _banner = skel;
        GD.Print("[UI-TRACE] panel-banner-shown");
    }
}
