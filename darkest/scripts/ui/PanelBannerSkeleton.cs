using Godot;

namespace Darkest.UI;

/// <summary>
/// 阶段2 施工④：DD `panel.banner` 战斗横幅骨架（编辑器里可见可改）
/// 依据：`panels/panel_banner.png = 754x136`（asset_sizes 真实尺寸）· background −33,0 · portrait 32,32 ·
///       seal 24,23 · name 272,38 · ability 280,35（面板内偏移，已换算为面板内比例）
/// 未接入数据/美术处一律 ColorRect 色块占位（§14.0.68 不换不删）
/// </summary>
public partial class PanelBannerSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/panel_banner_skeleton.tscn";

    public PanelContainer? Panel => GetNodeOrNull<PanelContainer>("BannerPanel");
    public ColorRect? Portrait => GetNodeOrNull<ColorRect>("BannerPortrait");
    public ColorRect? Seal => GetNodeOrNull<ColorRect>("BannerSeal");
    public ColorRect? NameBlock => GetNodeOrNull<ColorRect>("BannerName");
    public ColorRect? Ability => GetNodeOrNull<ColorRect>("BannerAbility");

    public static PanelBannerSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        PanelBannerSkeleton? skel = packed?.Instantiate() as PanelBannerSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 横幅] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print($"[UI 横幅] OK 采用骨架 `{ScenePath}`（编辑器里可改）");
        return skel;
    }
}
