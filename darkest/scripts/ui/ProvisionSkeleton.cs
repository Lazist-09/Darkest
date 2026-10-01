using Godot;

namespace Darkest.UI;

/// <summary>
/// DD 1:1 ②：**供应屏骨架**（编辑器里可见可改）—— 左队伍格 / 右商店格 / 右上信息区
/// 依据：DD `campaign\town\provision\provision.layout.darkest`（store grid 120,20 格 80x170 · party grid 60,28 格 80x160 ·
///        quest_info 1300,96 · scouting_stat 1380,96 · sell_back_info 1164,510）
/// 用法：TryInstantiate 成功则以骨架为准；失败 ⇒ 调用方回落（不崩不静默）
/// </summary>
public partial class ProvisionSkeleton : Control
{
    public const string ScenePath = "res://scenes/ui/provision_skeleton.tscn";

    public Label? Title => GetNodeOrNull<Label>("ProvisionCol/TitleRow/ProvisionTitle");
    public Button? Close => GetNodeOrNull<Button>("ProvisionCol/TitleRow/ProvisionClose");
    public GridContainer? PartyGrid => GetNodeOrNull<GridContainer>("ProvisionCol/BodyRow/PartyGrid");

    /// <summary>
    /// 🔴 2026-10-02：商店网格**搬到 DD 商店背景位之内**（`ProvStoreAnchor` 814,144 ⇒ 网格 start_pos 120,20）——
    /// 此前它在 `BodyRow` 流式布局里（与 DD 的 `provision_store_background_layout` 无关，属推导位）✓
    /// ⚠️ 网格住在 `ScrollContainer` 之内 ⇒ `LayoutAudit` 的「内容需求超相机」判据对它豁免（`InsideScroll`）✓
    /// </summary>
    public GridContainer? StoreGrid => GetNodeOrNull<GridContainer>("ProvStoreAnchor/StoreCol/StoreMargin/StoreScroll/StoreGrid");

    /// <summary>商店状态行（读数：库存总件数 / 金币 / 最近一次买卖原文）✓</summary>
    public Label? StoreStatus => GetNodeOrNull<Label>("ProvStoreAnchor/StoreCol/StoreStatus");
    public Label? QuestInfo => GetNodeOrNull<Label>("ProvisionCol/BodyRow/InfoCol/QuestInfo");
    public Label? ScoutingStat => GetNodeOrNull<Label>("ProvisionCol/BodyRow/InfoCol/ScoutingStat");
    public Label? SellBackInfo => GetNodeOrNull<Label>("ProvisionCol/BodyRow/InfoCol/SellBackInfo");
    public PanelContainer? ProvQuestInfoAnchor => GetNodeOrNull<PanelContainer>("ProvQuestInfoAnchor");
    public PanelContainer? ProvScoutingAnchor => GetNodeOrNull<PanelContainer>("ProvScoutingAnchor");
    public PanelContainer? ProvSellBackAnchor => GetNodeOrNull<PanelContainer>("ProvSellBackAnchor");
    public PanelContainer? ProvStoreAnchor => GetNodeOrNull<PanelContainer>("ProvStoreAnchor");

    public static ProvisionSkeleton? TryInstantiate()
    {
        PackedScene? packed = GD.Load<PackedScene>(ScenePath);
        ProvisionSkeleton? skel = packed?.Instantiate() as ProvisionSkeleton;
        if (skel is null)
        {
            GD.Print($"[UI 供应] `{ScenePath}` 不可用 ⇒ 回落（不静默）");
            return null;
        }

        GD.Print($"[UI 供应] OK 采用骨架 `{ScenePath}`（编辑器里可改）");
        return skel;
    }
}
