using Godot;

namespace Darkest.UI;

/// <summary>
/// DD 1:1 P5：**传家宝兑换屏骨架**（编辑器里可见可改）
/// 依据 DD `shared\estate\heirloom_exchange\heirloom_exchange.layout.darkest`：标题 215,24 · from 起 79,110 间距 44 · to 起 256,75
/// 未接入数据处一律 ColorRect 色块占位（用户硬规矩 14.0.68：不换不删）
/// </summary>
public partial class HeirloomExchangeSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/heirloom_exchange_skeleton.tscn";

    public Label? Title => GetNodeOrNull<Label>("ExchangeCol/HeaderRow/ExchangeTitle");
    public Button? Close => GetNodeOrNull<Button>("ExchangeCol/HeaderRow/ExchangeClose");
    public VBoxContainer? FromCol => GetNodeOrNull<VBoxContainer>("ExchangeCol/BodyRow/FromCol");
    public VBoxContainer? ToCol => GetNodeOrNull<VBoxContainer>("ExchangeCol/BodyRow/ToCol");

    public static HeirloomExchangeSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        HeirloomExchangeSkeleton? skel = packed?.Instantiate() as HeirloomExchangeSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 传家宝兑换] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print($"[UI 传家宝兑换] OK 采用骨架 `{ScenePath}`（编辑器里可改）");
        return skel;
    }
}
