using Godot;

namespace Darkest.UI;

/// <summary>
/// 阶段2：DD `raid_results.layout.darkest`（E 盘直读，80 字段 / 12 section）⇒ 结算屏布局骨架
/// 9 个位置块全部为 ColorRect 色块占位（§14.0.68 不换不删）；锚点 = 屏幕级 ÷1920、÷1080
/// </summary>
public partial class RaidResultsSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/raid_results_skeleton.tscn";

    /// <summary>挂进结算模态（骨架优先；失败 ⇒ 留痕并返回 null，不崩不静默）✓</summary>
    public static RaidResultsSkeleton? AttachInto(Control host)
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        RaidResultsSkeleton? skel = packed?.Instantiate() as RaidResultsSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 结算] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        host.AddChild(skel);
        GD.Print("[UI-TRACE] raid-results-layout");
        return skel;
    }
}
