# 阶段3 落位清单（色块位 ↔ DD 出处 ↔ 尺寸）

2026-09-21（脚本只读生成，可随施工再生）

用法：美术按「节点 + 尺寸 + DD 出处」出**同尺寸原创资产**；替换时**只换贴图、不动锚点/尺寸** ⇒ 布局零改动 ✓

## A 栏：有 DD 出处的占位块（27 条，优先出图）

```
场景 | 节点 | 锚点(left/top) | 尺寸 | DD 出处
battle_bottombar.tscn          | RaidTorchGauge             | 0.30/0.02            | Vector2(400, 4)      | DD screen.raid [torch_layout] gauge_size 400x4 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）· 色块占位，不换不删
battle_bottombar.tscn          | RaidBasicScrollBtn         | 0.30/0.055           | Vector2(384, 36)     | DD screen.raid [basic_scroll] button_size 384x36 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）· 色块占位，不换不删
battle_bottombar.tscn          | RaidSidebarSlot            | 0.30/0.09            | Vector2(80, 160)     | DD screen.raid [sidebar_scroll] item_slot_size 80x160 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）· 色块占位，不换不删
battle_bottombar.tscn          | RaidResultScrollBtn        | 0.30/0.125           | Vector2(384, 36)     | DD screen.raid [result_scroll] button_size 384x36 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）· 色块占位，不换不删
battle_bottombar.tscn          | RaidTorchInfoArea          | 0.30/0.16            | Vector2(200, 130)    | DD screen.raid [torch_info] mouseOverAreaSize 200x130 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）· 色块占位，不换不删
battle_bottombar.tscn          | RaidTorchStripArea         | 0.30/0.195           | Vector2(860, 24)     | DD screen.raid [torch_info] stripMouseOverAreaSize 860x24 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）· 色块占位，
battle_bottombar.tscn          | RaidQuestInfoArea          | 0.30/0.23            | Vector2(300, 150)    | DD screen.raid [quest_info] size 300x150 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）· 色块占位，不换不删
battle_bottombar.tscn          | RaidPos1                   | 0.7969/0.0324        | -                    | DD screen.raid [kill_count_display] pos 1530,35 ⇒ 0.7969/0.0324 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos2                   | -0.1328/0            | -                    | DD screen.raid [camp_layout] fourth_pos -255,0 ⇒ -0.1328/0 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos3                   | 0.3812/0.0556        | -                    | DD screen.raid [camp_layout] respite_scroll_pos 732,60 ⇒ 0.3812/0.0556 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos4                   | 0.0062/0.0185        | -                    | DD screen.raid [quest_info] pos 12,20 ⇒ 0.0062/0.0185 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos5                   | 0.4938/0.5463        | -                    | DD screen.raid [quest_info] complete_mid_screen_pos 948,590 ⇒ 0.4938/0.5463 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos6                   | 0/0.0509             | -                    | DD screen.raid [quest_info] complete_choice_shared_frame_pos 0,55 ⇒ 0/0.0509 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos7                   | -0.0885/0.0907       | -                    | DD screen.raid [quest_info] complete_return_to_hamlet_pos -170,98 ⇒ -0.0885/0.0907 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos8                   | 0.0469/0.0907        | -                    | DD screen.raid [quest_info] complete_continue_raid_pos 90,98 ⇒ 0.0469/0.0907 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidSec1                   | 0.7969/0.125         | -                    | DD screen.raid [shard_escrow_display] pos 1530,135 ⇒ 0.7969/0.125 · 色块占位，不换不删
battle_bottombar.tscn          | RaidSec2                   | 0.7969/0.0324        | -                    | DD screen.raid [wave_countdown_display] pos 1530,35 ⇒ 0.7969/0.0324 · 色块占位，不换不删
battle_bottombar.tscn          | RaidSec3                   | 0.9167/0.5324        | -                    | DD screen.raid [skip_curio_display] pos 1760,575 ⇒ 0.9167/0.5324 · 色块占位，不换不删
hamlet_skeleton.tscn           | DDNav1_blacksmith          | -                    | Vector2(128, 56)     | DD building_navigation index 1: blacksmith (placeholder)
hamlet_skeleton.tscn           | DDNav2_guild               | -                    | Vector2(128, 56)     | DD building_navigation index 2: guild (placeholder)
hamlet_skeleton.tscn           | DDNav3_camping_trainer     | -                    | Vector2(128, 56)     | DD building_navigation index 3: camping_trainer (placeholder)
hamlet_skeleton.tscn           | DDNav6_sanitarium          | -                    | Vector2(128, 56)     | DD building_navigation index 6: sanitarium (placeholder)
hamlet_skeleton.tscn           | DDNav7_nomad_wagon         | -                    | Vector2(128, 56)     | DD building_navigation index 7: nomad_wagon (placeholder)
hamlet_skeleton.tscn           | DDNav8_graveyard           | -                    | Vector2(128, 56)     | DD building_navigation index 8: graveyard (placeholder)
hamlet_skeleton.tscn           | DDNav9_statue              | -                    | Vector2(128, 56)     | DD building_navigation index 9: statue (placeholder)
hamlet_skeleton.tscn           | EstateSummary              | 0.0/0.903            | -                    | DD estate_summary_pos 0,975 (resource bar, placeholder)
hamlet_skeleton.tscn           | RealmInventory             | 0.459/0.119          | -                    | DD realm_inventory_pos 881,128 (realm inventory, placeholder)
```

## B 栏：我方自建占位块（178 条，无 DD 出处 ⇒ 按现有锚点区域出图即可）

```
场景 | 节点 | 锚点(left/top) | 尺寸
battle_bottombar.tscn          | BackSlot5                  | -                    | Vector2(72, 112)
battle_bottombar.tscn          | CArea                      | -                    | Vector2(260, 0)
battle_bottombar.tscn          | ActorDetailBox             | -                    | Vector2(0, 84)
battle_bottombar.tscn          | EArea                      | -                    | -
battle_bottombar.tscn          | MapCorner                  | 0.615                | Vector2(720, 360)
battle_bottombar.tscn          | HeroIcon1                  | 0.41/0.611           | -
battle_bottombar.tscn          | EnemyIcon1                 | 0.547/0.611          | -
battle_bottombar.tscn          | HeroIcon2                  | 0.323/0.611          | -
battle_bottombar.tscn          | EnemyIcon2                 | 0.634/0.611          | -
battle_bottombar.tscn          | HeroIcon3                  | 0.235/0.611          | -
battle_bottombar.tscn          | EnemyIcon3                 | 0.722/0.611          | -
battle_bottombar.tscn          | HeroIcon4                  | 0.148/0.611          | -
battle_bottombar.tscn          | EnemyIcon4                 | 0.809/0.611          | -
battle_bottombar.tscn          | RoundIndicator             | 0.005                | -
battle_bottombar.tscn          | AttackOverlayAnchor        | 0.485/0.318          | -
battle_bottombar.tscn          | MonsterPanelAnchor         | 0.478                | -
battle_bottombar.tscn          | RaidReorderPartyButton     | 0.345                | -
battle_bottombar.tscn          | RaidInventoryGridAnchor    | 0.735/0.30           | -
battle_bottombar.tscn          | MapTabAnchor               | 0.9333/0.7           | -
battle_bottombar.tscn          | MapHomeButton              | 0.9403/0.0667        | -
battle_bottombar.tscn          | InvSlot1                   | -                    | Vector2(80, 160)
battle_bottombar.tscn          | InvSlot2                   | -                    | Vector2(80, 160)
battle_bottombar.tscn          | InvSlot3                   | -                    | Vector2(80, 160)
battle_bottombar.tscn          | InvSlot4                   | -                    | Vector2(80, 160)
battle_bottombar.tscn          | InvSlot5                   | -                    | Vector2(80, 160)
battle_bottombar.tscn          | InvSlot6                   | -                    | Vector2(80, 160)
battle_bottombar.tscn          | InvSlot7                   | -                    | Vector2(80, 160)
battle_bottombar.tscn          | InvSlot8                   | -                    | Vector2(80, 160)
battle_overlay.tscn            | HeroIcon1                  | 0.41/0.611           | -
battle_overlay.tscn            | EnemyIcon1                 | 0.547/0.611          | -
battle_overlay.tscn            | HeroIcon2                  | 0.323/0.611          | -
battle_overlay.tscn            | EnemyIcon2                 | 0.634/0.611          | -
battle_overlay.tscn            | HeroIcon3                  | 0.235/0.611          | -
battle_overlay.tscn            | EnemyIcon3                 | 0.722/0.611          | -
battle_overlay.tscn            | HeroIcon4                  | 0.148/0.611          | -
battle_overlay.tscn            | EnemyIcon4                 | 0.809/0.611          | -
battle_overlay.tscn            | RoundIndicator             | 0.005/0.646          | -
battle_overlay.tscn            | MapCorner                  | 0.74/0.30            | -
battle_overlay.tscn            | AttackOverlayAnchor        | 0.485/0.318          | -
battle_overlay.tscn            | MonsterPanelAnchor         | 0.478/0.645          | -
battle_overlay.tscn            | RaidReorderPartyButton     | 0.345/0.078          | -
battle_overlay.tscn            | RaidInventoryGridAnchor    | 0.735/0.30           | -
building_popup.tscn            | ShopkeeperSlot             | -                    | Vector2(0, 96)
building_popup.tscn            | HeroSlot1                  | -                    | Vector2(60, 80)
building_popup.tscn            | HeroSlot2                  | -                    | Vector2(60, 80)
building_popup.tscn            | HeroSlot3                  | -                    | Vector2(60, 80)
building_popup.tscn            | HeroSlot4                  | -                    | Vector2(60, 80)
building_popup.tscn            | CostPlaceholder            | -                    | Vector2(160, 24)
building_popup.tscn            | ConfirmPlaceholder         | -                    | Vector2(120, 28)
building_popup.tscn            | NamePlaceholder            | -                    | Vector2(200, 22)
building_popup.tscn            | DescPlaceholder            | -                    | Vector2(260, 40)
building_popup.tscn            | BpNameAnchor               | 0.1095/0.1575        | -
building_popup.tscn            | BpBodyAnchor               | 0.6274/0.1275        | -
building_popup.tscn            | BpUpgradeAnchor            | 0.1811/0.3238        | -
building_popup.tscn            | BpTreesAnchor              | 0.0/0.2438           | -
building_popup.tscn            | BpSlotListAnchor           | 0.4632/0.1488        | -
building_popup.tscn            | BpChoiceListAnchor         | 0.0/0.3125           | -
building_popup.tscn            | BpGraveyardEntry           | 0.6274/0.1275        | Vector2(1000, 160)
building_popup.tscn            | BpStatueList               | 0.6274/0.2175        | Vector2(600, 580)
building_popup.tscn            | BpUpgradeSlot              | 0.6274/0.3075        | Vector2(102, 72)
building_popup.tscn            | BpHeroActionBase           | 0.6274/0.3975        | Vector2(100, 100)
building_popup.tscn            | BpSanitariumChoice         | 0.6274/0.4875        | Vector2(120, 20)
building_popup.tscn            | BpStageCoachTextBox        | 0.6274/0.5775        | Vector2(350, 200)
controls_skeleton.tscn         | CtrlMouseKbPanel           | 0.2344/0.1389        | -
controls_skeleton.tscn         | CtrlControllerPanel        | 0.0771/0.0593        | -
controls_skeleton.tscn         | CtrlCategoryStart          | 0.1458/0.1852        | -
controls_skeleton.tscn         | CtrlVariantStandard        | 0.0771/0.18          | -
controls_skeleton.tscn         | CtrlVariantAlternate       | 0.0771/0.26          | -
controls_skeleton.tscn         | CtrlVariantSteamDeck       | 0.0771/0.34          | -
credits_skeleton.tscn          | CrBasePanel                | -                    | -
credits_skeleton.tscn          | CrBackButton               | 0.0333/0.137         | -
credits_skeleton.tscn          | CrScrollBody               | 0.15/0.15            | -
fe_flow_skeleton.tscn          | FfSaveSlotWindow           | 0.1042/0.5074        | Vector2(1800, 434)
fe_flow_skeleton.tscn          | FfScrollBar                | 0.7813/0.537         | -
fe_flow_skeleton.tscn          | FfModeSelectDialog         | 0.5/0.2778           | -
fe_flow_skeleton.tscn          | FfAnswerButton1            | 0.5/0.4              | Vector2(500, 55)
fe_flow_skeleton.tscn          | FfBackButton               | 0.1510/0.05          | -
fe_flow_skeleton.tscn          | FfBuildNum                 | 0.0125/0.0185        | -
hamlet_skeleton.tscn           | TopBar                     | -                    | -
hamlet_skeleton.tscn           | StatusBar                  | -                    | -
hamlet_skeleton.tscn           | LeftColumn                 | -                    | -
hamlet_skeleton.tscn           | RightColumn                | -                    | Vector2(370, 0)
hamlet_skeleton.tscn           | BottomBar                  | -                    | -
hamlet_skeleton.tscn           | DDNav0_stage_coach         | -                    | Vector2(128, 56)
hamlet_skeleton.tscn           | DDNav4_tavern              | -                    | Vector2(128, 56)
hamlet_skeleton.tscn           | DDNav5_abbey               | -                    | Vector2(128, 56)
hamlet_skeleton.tscn           | ActivityLogAnchor          | 0.075/0.1222         | -
heirloom_exchange_skeleton.tscn | FromSlot1                  | -                    | Vector2(120, 40)
heirloom_exchange_skeleton.tscn | ToSlot1                    | -                    | Vector2(120, 40)
heirloom_exchange_skeleton.tscn | FromSlot2                  | -                    | Vector2(120, 40)
heirloom_exchange_skeleton.tscn | ToSlot2                    | -                    | Vector2(120, 40)
heirloom_exchange_skeleton.tscn | FromSlot3                  | -                    | Vector2(120, 40)
heirloom_exchange_skeleton.tscn | ToSlot3                    | -                    | Vector2(120, 40)
heirloom_exchange_skeleton.tscn | HxChoiceStartAnchor        | 0.1333/0.0694        | -
heirloom_exchange_skeleton.tscn | HxIconOffsetAnchor         | 0.0/0.0296           | -
heirloom_exchange_skeleton.tscn | HxArrowAnchor              | 0.0771/0.0741        | -
hero_detail_skeleton.tscn      | HeroEquipArea              | 0.1011/0.4778        | -
hero_detail_skeleton.tscn      | HeroTrinketArea            | 0.1011/0.66          | -
hero_detail_skeleton.tscn      | HeroDiseaseIcon            | 0.055                | -
hero_detail_skeleton.tscn      | HeroScoutingStat           | 0.0/0.64             | -
hero_detail_skeleton.tscn      | HeroEquipHighlight         | 0.36/0.0             | -
hero_detail_skeleton.tscn      | HeroEquipLevelText         | 0.455                | -
hero_detail_skeleton.tscn      | HeroStatsIcon              | 0.0/0.145            | -
hero_detail_skeleton.tscn      | HdNameAnchor               | 0.0545/0.0241        | -
hero_detail_skeleton.tscn      | HdClassAnchor              | 0.0545/0.0741        | -
hero_detail_skeleton.tscn      | HdQuirksAnchor             | 0.1011/0.1185        | -
hero_detail_skeleton.tscn      | HdHeroPipsAnchor           | 0.6065/0.1343        | -
hero_detail_skeleton.tscn      | HdTargetPipsAnchor         | 0.8265/0.1343        | -
hero_detail_skeleton.tscn      | HdPosTitleAnchor           | 0.5448/0.0907        | -
hero_detail_skeleton.tscn      | HdCombatSkillAnchor        | 0.5591/0.1444        | -
hero_detail_skeleton.tscn      | HdCampSkillAnchor          | 0.5591/0.2963        | -
hero_detail_skeleton.tscn      | HdResistAnchor             | 0.5591/0.4037        | -
hero_detail_skeleton.tscn      | HdClassBonusAnchor         | 0.5591/0.5241        | -
hero_detail_skeleton.tscn      | HdCloseAnchor              | 0.9634/0.0167        | -
hero_detail_skeleton.tscn      | HdHeroArtAnchor            | 0.0703/0.6481        | -
loot_overlay_skeleton.tscn     | LootDescBlock              | -                    | Vector2(350, 40)
loot_overlay_skeleton.tscn     | LootTile1                  | -                    | Vector2(53, 53)
loot_overlay_skeleton.tscn     | LootTile2                  | -                    | Vector2(53, 53)
loot_overlay_skeleton.tscn     | LootTile3                  | -                    | Vector2(53, 53)
loot_overlay_skeleton.tscn     | LootTile4                  | -                    | Vector2(53, 53)
loot_overlay_skeleton.tscn     | TakeAllBlock               | -                    | Vector2(80, 30)
loot_overlay_skeleton.tscn     | CloseBlock                 | -                    | Vector2(80, 30)
loot_overlay_skeleton.tscn     | LootTakeAllAnchor          | 0.0417/0.3315        | -
loot_overlay_skeleton.tscn     | LootCloseAnchor            | 0.1594/0.3315        | -
loot_overlay_skeleton.tscn     | LootDescAnchor             | 0.1188/0.1259        | -
main_menu.tscn                 | TitlePanel                 | -                    | Vector2(0, 56)
main_menu.tscn                 | OptionsPanel               | -                    | Vector2(600, 432)
main_menu.tscn                 | StatusPanel                | -                    | Vector2(0, 36)
main_menu.tscn                 | MenuBackButtonAnchor       | 0.4474/0.0954        | -
main_menu.tscn                 | MenuNameTooltipHotArea     | 0.02/0.86            | Vector2(360, 50)
main_menu.tscn                 | MenuControllerHotArea      | 0.02/0.93            | Vector2(600, 36)
modal_dialog.tscn              | ModalDialog                | -                    | -
panel_banner_skeleton.tscn     | BannerPanel                | 0.2/0.02             | Vector2(754, 136)
panel_banner_skeleton.tscn     | BannerPortrait             | 0.0424/0.2353        | -
panel_banner_skeleton.tscn     | BannerSeal                 | 0.0318/0.1691        | -
panel_banner_skeleton.tscn     | BannerName                 | 0.3607/0.2794        | -
panel_banner_skeleton.tscn     | BannerAbility              | 0.3714/0.2574        | -
provision_skeleton.tscn        | ProvQuestInfoAnchor        | 0.6771/0.0889        | -
provision_skeleton.tscn        | BlockPlaceholder           | -                    | -
provision_skeleton.tscn        | ProvScoutingAnchor         | 0.7188/0.0889        | -
provision_skeleton.tscn        | BlockPlaceholder           | -                    | -
provision_skeleton.tscn        | ProvSellBackAnchor         | 0.6063/0.4722        | -
provision_skeleton.tscn        | BlockPlaceholder           | -                    | -
provision_skeleton.tscn        | ProvStoreAnchor            | 0.4167/0.4926        | -
provision_skeleton.tscn        | BlockPlaceholder           | -                    | -
quest_select_skeleton.tscn     | QuestMap                   | -                    | -
quest_select_skeleton.tscn     | AllQuestMap                | 0.4/                 | -
quest_select_skeleton.tscn     | DungeonEffectOverlay       | -                    | -
quest_select_skeleton.tscn     | QsQuestMapAnchor           | 0.5208/0.4861        | -
quest_select_skeleton.tscn     | QsAllQuestMapAnchor        | 0.4427/0.6481        | -
quest_select_skeleton.tscn     | QsEffectOverlayAnchor      | 0.5208/0.5185        | -
quest_select_skeleton.tscn     | QsDungeonXpBar             | 0.02/0.75            | Vector2(194, 8)
quest_select_skeleton.tscn     | QsXpBarTtHotArea           | 0.02/0.81            | Vector2(232, 48)
quest_select_skeleton.tscn     | QsTownEventIconTt          | 0.02/0.87            | Vector2(32, 32)
quest_select_skeleton.tscn     | QsCampingTt                | 0.02/0.93            | Vector2(80, 30)
quest_select_skeleton.tscn     | QsWaveHighscoreTt          | 0.02/0.99            | Vector2(260, 160)
raid_results_skeleton.tscn     | RrQuestTitle               | 0.5/0.1963           | -
raid_results_skeleton.tscn     | RrQuestResult              | 0.5/0.1389           | -
raid_results_skeleton.tscn     | RrState                    | 0.2656/0.0           | -
raid_results_skeleton.tscn     | RrCompletionBg             | 0.5/0.0              | -
raid_results_skeleton.tscn     | RrLevelBg                  | 0.0/0.0              | -
raid_results_skeleton.tscn     | RrProgression              | 0.0/0.8870           | -
raid_results_skeleton.tscn     | RrNext                     | 0.9740/0.9074        | -
raid_results_skeleton.tscn     | RrBack                     | 0.0260/0.9074        | -
raid_results_skeleton.tscn     | RrQuestInvGrid             | 0.0438/0.2852        | -
roster_row.tscn                | PortraitFrame              | 0.0568/0.0928        | -
roster_row.tscn                | EquipLevelSlot             | 0.4216/0.6701        | -
roster_row.tscn                | ArmourLevelSlot            | 0.6162/0.6701        | -
roster_row.tscn                | HeroLevel                  | 0.6973/0.0412        | -
slot_row.tscn                  | Frame                      | -                    | Vector2(26, 26)
tooltip.tscn                   | Tooltip                    | -                    | -
unit_card.tscn                 | UnitCard                   | -                    | -
unit_card.tscn                 | portraitBox                | -                    | Vector2(44, 44)
unit_card.tscn                 | MonsterType                | 0.0926/0.3043        | -
unit_card.tscn                 | MonsterResistances         | 0.1425/0.5978        | -
unit_card.tscn                 | MonsterSkillsTitle         | 0.6838/0.5054        | -
unit_card.tscn                 | MonsterHeroStats           | 0.6197/0.3016        | -
unit_card.tscn                 | MonsterResistEntryIcon     | 0.0/0.0217           | -
```

注：① A 栏 DD 数字均为度量事实（不构成抄袭）；DD 像素未进入工程（红线27）
    ② B 栏为既有 UI 占位（保留不删 §14.0.68）
    ③ 生成脚本只用数组拼接（本环境 `List.Add` 模式两次致变量退化成 String ⇒ 已弃用 ✓）
