using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 **战斗卡牌模板**（用户 2026-09-17：「重复的 UI 元素记得能复用就建成能复用的」）✓
///
/// 复用次数：**10 处同构**（我方前排 4 + 敌方 4 + 支援 2）⇒ 抽模板后**改一处 = 10 张卡一起变** ✓
/// 定位：`scenes/ui/unit_card.tscn` —— 宿主 `BuildCard` 实例化它，之后**只填数据**
/// （名字/属性行/HP·士气条/tag/立绘首字/原型色），**不再 `new` 控件** ✓
///
/// 🔴 `[Tool]` ⇒ 编辑器里就能看到卡片真实外观（改这个场景 = 改所有卡牌）✓
/// ⚠️ 纪律：编辑器逻辑必须 `Engine.IsEditorHint()` 守卫（运行时绝不走编辑器分支）✓
/// ⚠️ **节点名保持不变**：`CardCol` / `CardHead` / `portraitBox` / `glyph` / `nameCol` / `name` / `stats` / `hp` / `morale` / `tag`
///    （`FillCard` 按这些名字取节点；改名字会断线）✓
/// </summary>
[Tool]
public partial class UnitCardTemplate : PanelContainer
{
    /// <summary>模板场景路径 ✓</summary>
    public const string ScenePath = "res://scenes/ui/unit_card.tscn";

    /// <summary>编辑器预览用名字（运行时被真实数据覆盖）✓</summary>
    [Export]
    public string PreviewName { get; set; } = "[-] 单位";

    public Label? NameLabel => GetNodeOrNull<Label>("CardCol/CardHead/nameCol/name");

    public Label? StatsLabel => GetNodeOrNull<Label>("CardCol/CardHead/nameCol/stats");

    public Label? GlyphLabel => GetNodeOrNull<Label>("CardCol/CardHead/portraitBox/glyph");

    public ProgressBar? HpBar => GetNodeOrNull<ProgressBar>("CardCol/hp");

    public ProgressBar? MoraleBar => GetNodeOrNull<ProgressBar>("CardCol/morale");

    public Label? TagLabel => GetNodeOrNull<Label>("CardCol/tag");

    /// <summary>实例化模板；场景缺失/类型不符 ⇒ 返回 null（宿主回落代码构建，不崩不静默）✓</summary>
    public static UnitCardTemplate? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.Print($"[UI 模板] `{ScenePath}` 不存在 ⇒ 战斗卡牌回落代码构建（不静默）✓");
            return null;
        }

        UnitCardTemplate? card = packed.Instantiate<UnitCardTemplate>();
        if (card is null)
        {
            GD.Print($"[UI 模板] `{ScenePath}` 根节点不是 `UnitCardTemplate` ⇒ 回落代码构建（不静默）✓");
            return null;
        }

        return card;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint() && NameLabel is not null)
        {
            NameLabel.Text = PreviewName;
        }
    }
}
