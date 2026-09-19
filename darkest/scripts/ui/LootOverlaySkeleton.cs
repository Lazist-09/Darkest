using Godot;

namespace Darkest.UI;

/// <summary>
/// DD 1:1 战利品弹层骨架（编辑器里可见可改）
/// 依据 DD `scripts\layout\overlay.loot.darkest`：标题 228,40 · 描述 228,136 宽 350 · 格子 startPosY 195 offset 74,0 ·
///   全部拿取 take_all_pos 80,358 · 关闭 close_pos 306,358。未接入数据处一律 ColorRect 色块占位（不换不删）
/// </summary>
public partial class LootOverlaySkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/loot_overlay_skeleton.tscn";

    public Label? Title => GetNodeOrNull<Label>("LootCol/TitleRow/LootTitle");
    public Button? Close => GetNodeOrNull<Button>("LootCol/TitleRow/LootClose");
    public HBoxContainer? Tiles => GetNodeOrNull<HBoxContainer>("LootCol/LootTiles");
    public PanelContainer? TakeAll => GetNodeOrNull<PanelContainer>("LootCol/ButtonRow/TakeAllBlock");
    public PanelContainer? CloseBlock => GetNodeOrNull<PanelContainer>("LootCol/ButtonRow/CloseBlock");

    public static LootOverlaySkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        LootOverlaySkeleton? skel = packed?.Instantiate() as LootOverlaySkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 战利品] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print($"[UI 战利品] OK 采用骨架 `{ScenePath}`（编辑器里可改）");
        return skel;
    }
}
