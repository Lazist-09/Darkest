using Godot;

namespace Darkest.UI;

/// <summary>
/// DD 1:1 3-2：**英雄面板骨架**（编辑器里可见可改）—— 四块：状态条 / 六属性列 / 装备位 / 饰品 2 列格
/// 数据源与比例依据：`shared\hero\hero.layout.darkest`（见 skill 14.0.47 / 14.0.49）
/// 用法：`TryInstantiate()` 成功则以骨架为准（只填数据）；失败则调用方回落代码建（不崩不静默）
/// </summary>
public partial class HeroDetailSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/hero_detail_skeleton.tscn";

    public VBoxContainer? HeroStatusBars => GetNodeOrNull<VBoxContainer>("HeroStatusBars");
    public ProgressBar? HeroHpBar => GetNodeOrNull<ProgressBar>("HeroStatusBars/HeroHpBar");
    public ProgressBar? HeroMoraleBar => GetNodeOrNull<ProgressBar>("HeroStatusBars/HeroMoraleBar");
    public GridContainer? HeroStatsGrid => GetNodeOrNull<GridContainer>("HeroStatsGrid");
    public HBoxContainer? HeroEquipmentRow => GetNodeOrNull<HBoxContainer>("HeroEquipmentRow");
    public GridContainer? HeroTrinketGrid => GetNodeOrNull<GridContainer>("HeroTrinketGrid");

    public static HeroDetailSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        HeroDetailSkeleton? skel = packed?.Instantiate() as HeroDetailSkeleton;   // 用 as 不抛异常（Instantiate<T> 在类型不符时会抛 InvalidCastException）
        if (skel is null)
        {
            GD.Print($"[UI 英雄面板] `{ScenePath}` 不可用 ⇒ 回落代码构建（不静默）");
            return null;
        }

        GD.Print($"[UI 英雄面板] OK 采用骨架 `{ScenePath}`（编辑器里可改）");
        return skel;
    }
}
