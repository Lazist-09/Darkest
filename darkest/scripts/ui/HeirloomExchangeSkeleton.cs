using Godot;

namespace Darkest.UI;

/// <summary>
/// DD 1:1 P5：**传家宝兑换屏骨架**（编辑器里可见可改）
/// 依据 DD `campaign/town/heirloom_exchange/heirloom_exchange.layout.darkest`：
///   根 heirloom_exchange_layout · title_pos 215,24
///   转入列 heirloom_exchange_heirloom_from_layout · choice_start_offset 79,110 · choice_spacing 44,0 · icon_offset 0,32
///   转出列 heirloom_exchange_heirloom_to_layout   · choice_start_offset 256,75 · choice_spacing 0,44 · arrow_offset 148,80
///
/// 面板尺寸：DD 布局文件**未给 size**；取自面板自身资产
///   `campaign/town/heirloom_exchange/heirloom_exchange.background.png` = **429x268**
///   佐证：title_pos.x = 215 恰为 429/2（标题正中）；全部 offset 落在 429x268 内
///   ⇒ 面板内部使用 **429x268 局部空间**（from / to 两列各以自己 *_pos 0,0 为原点）
///
/// 未接入数据处一律 ColorRect 色块占位（用户硬规矩 14.0.68：不换不删）
/// </summary>
public partial class HeirloomExchangeSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/heirloom_exchange_skeleton.tscn";

    /// <summary>DD 面板尺寸（来源：heirloom_exchange.background.png 文件头）</summary>
    public static readonly Vector2 PanelSize = new(429f, 268f);

    public Label? Title => GetNodeOrNull<Label>("PanelFrame/ExchangeTitle");
    public Button? Close => GetNodeOrNull<Button>("PanelFrame/CloseBtn");
    public Control? FromLayer => GetNodeOrNull<Control>("PanelFrame/HxFromLayer");
    public Control? ToLayer => GetNodeOrNull<Control>("PanelFrame/HxToLayer");

    public static HeirloomExchangeSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        HeirloomExchangeSkeleton? skel = packed?.Instantiate() as HeirloomExchangeSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 传家宝兑换] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print($"[UI 传家宝兑换] OK 采用骨架 `{ScenePath}`"
                 + $"（面板 {PanelSize.X}x{PanelSize.Y} · 尺寸取自 heirloom_exchange.background.png）");
        return skel;
    }
}
