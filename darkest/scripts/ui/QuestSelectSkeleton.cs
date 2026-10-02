using Godot;

namespace Darkest.UI;

/// <summary>
/// DD 1:1 ②：任务选择屏骨架（编辑器里可见可改）
/// 依据 DD `campaign\town\quest_select\quest_select.layout.darkest`：name_pos 104,122 · party_name_pos 756,834 ·
///   每地牢一套 quest_map_pos / all_quest_map_pos / dungeon_effect_overlay_pos（cove / crypts / darkestdungeon / town）
/// 用法：TryInstantiate 成功则以骨架为准；失败 ⇒ 调用方回落（不崩不静默）
/// </summary>
public partial class QuestSelectSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/quest_select_skeleton.tscn";

    public Label? Title => GetNodeOrNull<Label>("QuestSelectCol/TitleRow/QuestSelectTitle");
    public Button? Close => GetNodeOrNull<Button>("QuestSelectCol/TitleRow/QuestSelectClose");
    /// <summary>
    /// 🔴 2026-10-02（压占位修复）：列表住在**内建 `ScrollContainer`** 里（路径含 `QuestListScroll`）——
    /// 宿主定高 442（推导见场景内注释：占位盒顶 648 − 列表顶 204 − 2px 缝）⇒ 内容再长也不压左下角 5 个屏幕级占位 ✓
    /// </summary>
    public VBoxContainer? QuestList => GetNodeOrNull<VBoxContainer>("QuestSelectCol/BodyRow/QuestListScroll/QuestList");
    public Control? MapArea => GetNodeOrNull<Control>("QuestSelectCol/BodyRow/MapArea");
    public PanelContainer? QuestMap => GetNodeOrNull<PanelContainer>("QuestSelectCol/BodyRow/MapArea/QuestMap");
    public PanelContainer? AllQuestMap => GetNodeOrNull<PanelContainer>("QuestSelectCol/BodyRow/MapArea/AllQuestMap");
    public PanelContainer? DungeonEffectOverlay => GetNodeOrNull<PanelContainer>("QuestSelectCol/BodyRow/MapArea/DungeonEffectOverlay");
    public Label? PartyName => GetNodeOrNull<Label>("QuestSelectCol/BodyRow/MapArea/PartyName");

    public static QuestSelectSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        QuestSelectSkeleton? skel = packed?.Instantiate() as QuestSelectSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 任务选择] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print($"[UI 任务选择] OK 采用骨架 `{ScenePath}`（编辑器里可改）");
        return skel;
    }
}
