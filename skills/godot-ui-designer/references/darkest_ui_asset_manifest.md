# 阶段3 落位清单（DD 出处 · 尺寸 · 菜单层级 · 归属容器 · 中文标签）

2026-09-21（脚本只读生成，可随施工再生）· v4 富化 spec 消费版

规范：占位块 `color = Color(0.62,0.6,0.58,0.22)`（`UiPalette.PlaceholderFill` α0.22）· 面积够者含 `PurposeLabel`（中文 · 字号11~12 · 非交互）· 层级标题 `LayerTitle`（中文 · 金色 α0.9）

列义：**层级** = v4 `菜单层级`（L1 一级屏 / L2 二级面板 / L3 三级弹层）· **归属容器** = v4 `归属容器`（DD 父 section）· **标签** = 屏内中文用途

## A 栏：有 DD 出处（173 条）

```
场景 | 节点 | 锚点 | 尺寸 | 层级 | 归属容器 | 标签 | DD 出处
battle_bottombar.tscn          | MapCorner                | 0.615              | Vector2(720, 360) |        |                          | 地图         | 地牢地图（DD panel.map · 只读不可点 · 数据未接入 ⇒ 色块占位，不换不删）
battle_bottombar.tscn          | HeroIcon1                | 0.41/0.611         | -                |        |                          | -          | 图标位 Hero1（DD tray_icon_left 58,-38 / right 62,-38 · hot_spot 20x24 · 需
battle_bottombar.tscn          | EnemyIcon1               | 0.547/0.611        | -                |        |                          | -          | 图标位 Enemy1（DD tray_icon_left 58,-38 / right 62,-38 · hot_spot 20x24 · 
battle_bottombar.tscn          | HeroIcon2                | 0.323/0.611        | -                |        |                          | -          | 图标位 Hero2（DD tray_icon_left 58,-38 / right 62,-38 · hot_spot 20x24 · 需
battle_bottombar.tscn          | EnemyIcon2               | 0.634/0.611        | -                |        |                          | -          | 图标位 Enemy2（DD tray_icon_left 58,-38 / right 62,-38 · hot_spot 20x24 · 
battle_bottombar.tscn          | HeroIcon3                | 0.235/0.611        | -                |        |                          | -          | 图标位 Hero3（DD tray_icon_left 58,-38 / right 62,-38 · hot_spot 20x24 · 需
battle_bottombar.tscn          | EnemyIcon3               | 0.722/0.611        | -                |        |                          | -          | 图标位 Enemy3（DD tray_icon_left 58,-38 / right 62,-38 · hot_spot 20x24 · 
battle_bottombar.tscn          | HeroIcon4                | 0.148/0.611        | -                |        |                          | -          | 图标位 Hero4（DD tray_icon_left 58,-38 / right 62,-38 · hot_spot 20x24 · 需
battle_bottombar.tscn          | EnemyIcon4               | 0.809/0.611        | -                |        |                          | -          | 图标位 Enemy4（DD tray_icon_left 58,-38 / right 62,-38 · hot_spot 20x24 · 
battle_bottombar.tscn          | RoundIndicator           | 0.005              | -                |        |                          | 回合         | 回合指示器（DD round_indicator_icon_offset 10,-4 · spacing 8,0 · 需美术 ⇒ 色块占位，
battle_bottombar.tscn          | AttackOverlayAnchor      | 0.485/0.318        | -                |        |                          | 攻击覆盖       | 攻击覆盖位（DD screen.raid.battle .attack_overlay_pos 960,360 · 需特效 ⇒ 色块占位，不
battle_bottombar.tscn          | MonsterPanelAnchor       | 0.478              | -                |        |                          | 怪物面板       | 怪物面板位（DD screen.raid.battle .monster_panel_position 946,712 · 数据未接入 ⇒ 
battle_bottombar.tscn          | RaidReorderPartyButton   | 0.345              | -                |        |                          | -          | 换位按钮位（DD panel.tab reorder_party .button_pos 678,90 · 我域有 mf_tab 模板 ⇒ 
battle_bottombar.tscn          | RaidInventoryGridAnchor  | 0.735/0.30         | -                |        |                          | -          | 库存网格位（DD pannel.inventory raid grid: 8 列 · start_pos 20,28 · offset 80
battle_bottombar.tscn          | MapTabAnchor             | 0.9333/0.7         | -                |        |                          | 地图页签       | 地图页签（DD panel.map tab 672,252 · size 48x90 · 数据未接入 ⇒ 色块占位，不换不删）
battle_bottombar.tscn          | MapHomeButton            | 0.9403/0.0667      | -                |        |                          | 地图回中       | 地图回中按钮（DD panel.map home button_pos 677,24 · 数据未接入 ⇒ 色块占位，不换不删）
battle_bottombar.tscn          | RaidTorchGauge           | 0.3/0.0204         | Vector2(400, 4)  | L1     | screen_guide             | -          | DD screen.raid [torch_layout] gauge_size 400x4 · 尺寸=DD 原值 ✓ · 位置待该段 *_
battle_bottombar.tscn          | RaidBasicScrollBtn       | 0.3/0.0546         | Vector2(384, 36) | L3     | confirm_dialog_layout    | 基础翻页       | DD screen.raid [basic_scroll] button_size 384x36 · 尺寸=DD 原值 ✓ · 位置待该段 
battle_bottombar.tscn          | RaidSidebarSlot          | 0.7021/0.1852      | Vector2(80, 160) | L1     | screen_guide             | 侧栏格        | DD screen.raid [sidebar_scroll] item_slot_size 80x160 · 尺寸=DD 原值 ✓ · 位
battle_bottombar.tscn          | RaidResultScrollBtn      | 0.3/0.125          | Vector2(384, 36) | L3     | confirm_dialog_layout    | 结算翻页       | DD screen.raid [result_scroll] button_size 384x36 · 尺寸=DD 原值 ✓ · 位置待该段
battle_bottombar.tscn          | RaidTorchInfoArea        | 0.30/0.16          | Vector2(200, 130) |        |                          | 火把信息       | DD screen.raid [torch_info] mouseOverAreaSize 200x130 · 尺寸=DD 原值 ✓ · 位
battle_bottombar.tscn          | RaidTorchStripArea       | 0.30/0.195         | Vector2(860, 24) |        |                          | 火把条        | DD screen.raid [torch_info] stripMouseOverAreaSize 860x24 · 尺寸=DD 原值 ✓
battle_bottombar.tscn          | RaidQuestInfoArea        | 0.3/0.2296         | Vector2(300, 150) | L1     | screen_guide             | 任务信息       | DD screen.raid [quest_info] size 300x150 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读
battle_bottombar.tscn          | RaidPos1                 | 0.7969/0.0324      | -                | L1     | screen_guide             | 击杀数        | DD screen.raid [kill_count_display] pos 1530,35 ⇒ 0.7969/0.0324 · 数据未接
battle_bottombar.tscn          | RaidPos2                 | -0.1328/0          | -                | L1     | screen_guide             | -          | DD screen.raid [camp_layout] fourth_pos -255,0 ⇒ -0.1328/0 · 数据未接入 ⇒ 色
battle_bottombar.tscn          | RaidPos3                 | 0.3812/0.0556      | -                | L1     | screen_guide             | 扎营翻页       | DD screen.raid [camp_layout] respite_scroll_pos 732,60 ⇒ 0.3812/0.0556
battle_bottombar.tscn          | RaidPos4                 | 0.0062/0.0185      | -                | L1     | screen_guide             | 任务面板       | DD screen.raid [quest_info] pos 12,20 ⇒ 0.0062/0.0185 · 数据未接入 ⇒ 色块占位，不
battle_bottombar.tscn          | RaidPos5                 | 0.4938/0.5463      | -                | L1     | screen_guide             | 完成提示       | DD screen.raid [quest_info] complete_mid_screen_pos 948,590 ⇒ 0.4938/0
battle_bottombar.tscn          | RaidPos6                 | 0/0.0509           | -                | L1     | screen_guide             | 选择框        | DD screen.raid [quest_info] complete_choice_shared_frame_pos 0,55 ⇒ 0/
battle_bottombar.tscn          | RaidPos7                 | -0.0885/0.0907     | -                | L1     | screen_guide             | 回城         | DD screen.raid [quest_info] complete_return_to_hamlet_pos -170,98 ⇒ -0
battle_bottombar.tscn          | RaidPos8                 | 0.0469/0.0907      | -                | L1     | screen_guide             | 继续远征       | DD screen.raid [quest_info] complete_continue_raid_pos 90,98 ⇒ 0.0469/
battle_bottombar.tscn          | RaidSec1                 | 0.7969/0.125       | -                | L1     | screen_guide             | 碎片寄存       | DD screen.raid [shard_escrow_display] pos 1530,135 ⇒ 0.7969/0.125 · 色块
battle_bottombar.tscn          | RaidSec2                 | 0.7969/0.0324      | -                | L1     | screen_guide             | 波次倒计时      | DD screen.raid [wave_countdown_display] pos 1530,35 ⇒ 0.7969/0.0324 · 
battle_bottombar.tscn          | RaidSec3                 | 0.9167/0.5324      | -                | L1     | screen_guide             | 跳过奇物       | DD screen.raid [skip_curio_display] pos 1760,575 ⇒ 0.9167/0.5324 · 色块占
battle_bottombar.tscn          | InvItemIconBody          | 0.30/0.70          | Vector2(72, 144) |        |                          | 物品图标       | 库存图标本体（DD shared/inventory inventory_item_layout.icon_size 72x144 · 与 
battle_bottombar.tscn          | RaidX1                   | 0.4104/0.6296      | -                | L1     | screen_guide             | 英雄区起点      | DD screen.raid [overlays] hero_start_pos 788,680 => 0.4104/0.6296 · 色块
battle_bottombar.tscn          | RaidX2                   | 0.5469/0.6296      | -                | L1     | screen_guide             | 怪物区起点      | DD screen.raid [overlays] monster_start_pos 1050,680 => 0.5469/0.6296 
battle_bottombar.tscn          | RaidX3                   | 0.699/0.1296       | -                | L1     | screen_guide             | -          | DD screen.raid [basic_scroll] pos 1342,140 => 0.699/0.1296 · 色块占位，不换不删
battle_bottombar.tscn          | RaidX4                   | 0/0.3704           | -                | L1     | screen_guide             | 确定         | DD screen.raid [basic_scroll] ok_button_pos -150,400 => 0/0.3704 · 色块占
battle_bottombar.tscn          | RaidX5                   | 0.0406/0.3704      | -                | L1     | screen_guide             | 取消         | DD screen.raid [basic_scroll] cancel_button_pos 78,400 => 0.0406/0.370
battle_bottombar.tscn          | RaidX6                   | 0.7021/0.1852      | -                | L1     | screen_guide             | -          | DD screen.raid [sidebar_scroll] pos 1348,200 => 0.7021/0.1852 · 色块占位，不
battle_bottombar.tscn          | RaidX7                   | 0/0.2222           | -                | L1     | screen_guide             | 调查         | DD screen.raid [sidebar_scroll] investigate_button_pos -152,240 => 0/0
battle_bottombar.tscn          | RaidX8                   | 0.0391/0.2222      | -                | L1     | screen_guide             | 跳过         | DD screen.raid [sidebar_scroll] pass_button_pos 75,240 => 0.0391/0.222
battle_overlay.tscn            | HeroIcon1                | 0.41/0.611         | -                |        |                          | -          | icon Hero1 (DD tray_icon_left 58,-38 / right 62,-38; needs art => colo
battle_overlay.tscn            | EnemyIcon1               | 0.547/0.611        | -                |        |                          | -          | icon Enemy1 (DD tray_icon; needs art => color placeholder, keep)
battle_overlay.tscn            | HeroIcon2                | 0.323/0.611        | -                |        |                          | -          | icon Hero2 (DD tray_icon; needs art => color placeholder, keep)
battle_overlay.tscn            | EnemyIcon2               | 0.634/0.611        | -                |        |                          | -          | icon Enemy2 (DD tray_icon; needs art => color placeholder, keep)
battle_overlay.tscn            | HeroIcon3                | 0.235/0.611        | -                |        |                          | -          | icon Hero3 (DD tray_icon; needs art => color placeholder, keep)
battle_overlay.tscn            | EnemyIcon3               | 0.722/0.611        | -                |        |                          | -          | icon Enemy3 (DD tray_icon; needs art => color placeholder, keep)
battle_overlay.tscn            | HeroIcon4                | 0.148/0.611        | -                |        |                          | -          | icon Hero4 (DD tray_icon; needs art => color placeholder, keep)
battle_overlay.tscn            | EnemyIcon4               | 0.809/0.611        | -                |        |                          | -          | icon Enemy4 (DD tray_icon; needs art => color placeholder, keep)
battle_overlay.tscn            | RoundIndicator           | 0.005/0.646        | -                |        |                          | -          | round indicator (DD round_indicator_icon_offset 10,-4; needs art => co
battle_overlay.tscn            | MapCorner                | 0.74/0.30          | -                |        |                          | -          | dungeon map (DD panel.map; read-only, no data => color placeholder, ke
battle_overlay.tscn            | AttackOverlayAnchor      | 0.485/0.318        | -                |        |                          | -          | attack overlay anchor (DD screen.raid.battle attack_overlay_pos 960,36
battle_overlay.tscn            | MonsterPanelAnchor       | 0.478/0.645        | -                |        |                          | -          | monster panel anchor (DD screen.raid.battle monster_panel_position 946
battle_overlay.tscn            | RaidReorderPartyButton   | 0.345/0.078        | -                |        |                          | -          | reorder party button (DD panel.tab reorder_party button_pos 678,90; mf
battle_overlay.tscn            | RaidInventoryGridAnchor  | 0.735/0.30         | -                |        |                          | -          | raid inventory grid anchor (DD pannel.inventory raid grid 8 cols; scre
building_popup.tscn            | HeroSlot1                | -                  | Vector2(60, 80)  |        |                          | -          | 英雄槽 1（DD activity hero_slot · 数据未接入 ⇒ 色块占位，不换不删）
building_popup.tscn            | HeroSlot2                | -                  | Vector2(60, 80)  |        |                          | -          | 英雄槽 2（DD activity hero_slot · 数据未接入 ⇒ 色块占位，不换不删）
building_popup.tscn            | HeroSlot3                | -                  | Vector2(60, 80)  |        |                          | -          | 英雄槽 3（DD activity hero_slot · 数据未接入 ⇒ 色块占位，不换不删）
building_popup.tscn            | HeroSlot4                | -                  | Vector2(60, 80)  |        |                          | -          | 英雄槽 4（DD activity hero_slot · 数据未接入 ⇒ 色块占位，不换不删）
building_popup.tscn            | BpNameAnchor             | 0.1095/0.1575      | -                |        |                          | 名称         | 名称位（DD building.layout name_pos 104,126 · 基准 950x800） · 数据未接入 ⇒ 色块占位，不
building_popup.tscn            | BpBodyAnchor             | 0.6274/0.1275      | -                |        |                          | 主体         | 主体位（DD body_base_pos 596,102） · 数据未接入 ⇒ 色块占位，不换不删
building_popup.tscn            | BpUpgradeAnchor          | 0.1811/0.3238      | -                |        |                          | 升级区        | 升级区位（DD upgrade_base_pos 172,259） · 数据未接入 ⇒ 色块占位，不换不删
building_popup.tscn            | BpTreesAnchor            | 0.0/0.2438         | -                |        |                          | 升级树        | 升级树位（DD upgrade_trees_offset 0,195） · 数据未接入 ⇒ 色块占位，不换不删
building_popup.tscn            | BpSlotListAnchor         | 0.4632/0.1488      | -                |        |                          | 英雄槽        | 英雄槽列表位（DD slot_list_pos 440,119 · slot_spacing 135） · 数据未接入 ⇒ 色块占位，不换不
building_popup.tscn            | BpChoiceListAnchor       | 0.0/0.3125         | -                |        |                          | 选项         | 选项列表位（DD choice_list_pos 0,250 · choice_hot_spot_size 120x20 尺寸） · 数据未
building_popup.tscn            | BpGraveyardEntry         | 0.4792             | Vector2(1000, 160) | L2     | dead_hero_layout         | 坟场条目       | 坟场条目（DD graveyard .entry_size 1000x160 · 尺寸=DD 原值；该栋 *_position 偏移需父基准
building_popup.tscn            | BpStatueList             | 0.6274/0.2175      | Vector2(600, 580) | L2     |                          | 雕像列表       | 雕像列表区（DD statue .list_area_size 600x580 · 同上位置口径） · 数据未接入 ⇒ 色块占位，不换不删
building_popup.tscn            | BpUpgradeSlot            | 0.6274/0.3075      | Vector2(102, 72) | L2     |                          | 升级图标       | 升级图标位（DD upgrade .base_size 102x72 · 同上） · 数据未接入 ⇒ 色块占位，不换不删
building_popup.tscn            | BpHeroActionBase         | 0.6274/0.3975      | Vector2(100, 100) | L2     |                          | 英雄动作       | 英雄动作基准（DD hero_action .base_size 100x100 · 同上） · 数据未接入 ⇒ 色块占位，不换不删
building_popup.tscn            | BpSanitariumChoice       | 0.6274/0.4875      | Vector2(120, 20) | L2     | building_base_layout     | 疗养院选项      | 疗养院选项热区（DD sanitarium .choice_hot_spot_size 120x20 · 同上） · 数据未接入 ⇒ 色块占
building_popup.tscn            | BpStageCoachTextBox      | 0.6274/0.5775      | Vector2(350, 200) | L2     | hero_recruit_store       | 驿站文本框      | 驿站文本框（DD stage_coach .text_box_size 350x200 · 同上） · 数据未接入 ⇒ 色块占位，不换不删
controls_skeleton.tscn         | CtrlMouseKbPanel         | 0.2344/0.1389      | -                |        |                          | 键鼠说明       | 键鼠控制面板（DD controls_mouse_keyboard_panel.position 450,150）
controls_skeleton.tscn         | CtrlControllerPanel      | 0.0771/0.0593      | -                |        |                          | 手柄说明       | 手柄控制面板（DD controls_controller_panel.position 148,64）
controls_skeleton.tscn         | CtrlCategoryStart        | 0.1458/0.1852      | -                |        |                          | 分类起点       | 分类起点（DD controls_mouse_keyboard_element.category_start_pos 280,200）
controls_skeleton.tscn         | CtrlVariantStandard      | 0.0771/0.18        | -                |        |                          | 手柄·标准      | 手柄元素（标准变体 · DD controls_controller_element_layout 54 字段 · 色块占位，不换不删）
controls_skeleton.tscn         | CtrlVariantAlternate     | 0.0771/0.26        | -                |        |                          | 手柄·备用      | 手柄元素（备用变体 · DD …_alternate_layout 54 字段 · 色块占位，不换不删）
controls_skeleton.tscn         | CtrlVariantSteamDeck     | 0.0771/0.34        | -                |        |                          | 手柄·掌机      | 手柄元素（SteamDeck 变体 · DD …_steamdeck_layout 54 字段 · 色块占位，不换不删）
credits_skeleton.tscn          | CrBasePanel              | -                  | -                | L2     |                          | 制作人员       | 制作人员屏背景（DD credits base_pos 0,0 · 色块占位，不换不删）
credits_skeleton.tscn          | CrBackButton             | 0.0333/0.137       | -                | L2     |                          | 返回         | 返回（DD credits back_button_pos 64,148 ⇒ 0.0333/0.137 · 色块占位，不换不删）
credits_skeleton.tscn          | CrScrollBody             | 0.15/0.15          | -                |        |                          | 名单正文       | 制作人员滚动正文区（DD credits_level_0/1/2 三档 + animation 6 参数 ⇒ 时序层无几何；本块为正文区占位
fe_flow_skeleton.tscn          | FfSaveSlotWindow         | 0.0625/0.5074      | Vector2(1800, 434) |        |                          | 存档槽        | 存档槽窗口（DD save_slot_window_position 200,548 · size 1800x434 · 色块占位，不换不删
fe_flow_skeleton.tscn          | FfScrollBar              | 0.7813/0.537       | -                |        |                          | 滚动条        | 滚动条（DD scroll_bar_position 1500,580 · 色块占位，不换不删）
fe_flow_skeleton.tscn          | FfModeSelectDialog       | 0.5/0.2778         | -                | L2     |                          | 模式选择       | 模式选择对话框（DD mode_select_dialog base_pos 960,300 · 色块占位，不换不删）
fe_flow_skeleton.tscn          | FfAnswerButton1          | 0.5/0.4            | Vector2(500, 55) |        |                          | 选项按钮       | 模式选择答案按钮（DD button_size 500x55 · 色块占位，不换不删）
fe_flow_skeleton.tscn          | FfBackButton             | 0.1510/0.05        | -                | L1     |                          | 返回         | 返回（DD mode_select_dialog back_pos 290,54 · 色块占位，不换不删）
fe_flow_skeleton.tscn          | FfBuildNum               | 0.0125/0.0185      | -                |        |                          | 版本号        | 版本号位（DD build_num_pos 24,20 · 色块占位，不换不删）
hamlet_skeleton.tscn           | DDNav1_blacksmith        | -                  | Vector2(128, 56) |        |                          | 铁匠铺        | DD building_navigation index 1: blacksmith (placeholder)
hamlet_skeleton.tscn           | DDNav2_guild             | -                  | Vector2(128, 56) |        |                          | 公会         | DD building_navigation index 2: guild (placeholder)
hamlet_skeleton.tscn           | DDNav3_camping_trainer   | -                  | Vector2(128, 56) |        |                          | 训练师        | DD building_navigation index 3: camping_trainer (placeholder)
hamlet_skeleton.tscn           | DDNav6_sanitarium        | -                  | Vector2(128, 56) |        |                          | 疗养院        | DD building_navigation index 6: sanitarium (placeholder)
hamlet_skeleton.tscn           | DDNav7_nomad_wagon       | -                  | Vector2(128, 56) |        |                          | 游商         | DD building_navigation index 7: nomad_wagon (placeholder)
hamlet_skeleton.tscn           | DDNav8_graveyard         | -                  | Vector2(128, 56) |        |                          | 坟场         | DD building_navigation index 8: graveyard (placeholder)
hamlet_skeleton.tscn           | DDNav9_statue            | -                  | Vector2(128, 56) |        |                          | 雕像         | DD building_navigation index 9: statue (placeholder)
hamlet_skeleton.tscn           | EstateSummary            | 0.0/0.903          | -                |        |                          | 地产总览       | DD estate_summary_pos 0,975 (resource bar, placeholder)
hamlet_skeleton.tscn           | RealmInventory           | 0.459/0.119        | -                |        |                          | 领域库存       | DD realm_inventory_pos 881,128 (realm inventory, placeholder)
hamlet_skeleton.tscn           | ActivityLogAnchor        | 0.075/0.1222       | -                |        |                          | 活动日志       | 活动日志/城镇事件位（DD town.layout activity_log_pos / town_event_pos 均 144,132 
hamlet_skeleton.tscn           | EstateCostHotSpot        | 0.0/0.903          | Vector2(100, 50) |        |                          | 地产费用       | 地产费用热区（DD shared/estate estate_cost_hot_spot_layout.size 100x50 · 色块占位
hamlet_skeleton.tscn           | RealmInventoryGrid       | 0.459/0.119        | Vector2(560, 525) | L2     |                          | 库存网格       | 领域库存网格（DD realm_inventory inventory_grid_size 560x525 · grid_pos 30,19
heirloom_exchange_skeleton.tscn | FromSlot1                | -                  | Vector2(120, 40) |        |                          | -          | FROM 1（DD heirloom_exchange from 79,110 间距 44 · 数据未接入 ⇒ 色块占位，不换不删）
heirloom_exchange_skeleton.tscn | ToSlot1                  | -                  | Vector2(120, 40) |        |                          | -          | TO 1（DD heirloom_exchange to 256,75 · 数据未接入 ⇒ 色块占位，不换不删）
heirloom_exchange_skeleton.tscn | FromSlot2                | -                  | Vector2(120, 40) |        |                          | -          | FROM 2（DD heirloom_exchange from 79,110 间距 44 · 数据未接入 ⇒ 色块占位，不换不删）
heirloom_exchange_skeleton.tscn | ToSlot2                  | -                  | Vector2(120, 40) |        |                          | -          | TO 2（DD heirloom_exchange to 256,75 · 数据未接入 ⇒ 色块占位，不换不删）
heirloom_exchange_skeleton.tscn | FromSlot3                | -                  | Vector2(120, 40) |        |                          | -          | FROM 3（DD heirloom_exchange from 79,110 间距 44 · 数据未接入 ⇒ 色块占位，不换不删）
heirloom_exchange_skeleton.tscn | ToSlot3                  | -                  | Vector2(120, 40) |        |                          | -          | TO 3（DD heirloom_exchange to 256,75 · 数据未接入 ⇒ 色块占位，不换不删）
heirloom_exchange_skeleton.tscn | HxChoiceStartAnchor      | 0.1333/0.0694      | -                | L2     | heirloom_exchange_layout | 选项起点       | 选项起点（DD heirloom_exchange choice_start_offset 256,75） · 数据未接入 ⇒ 色块占位，不
heirloom_exchange_skeleton.tscn | HxIconOffsetAnchor       | 0.0/0.0296         | -                |        |                          | 图标位        | 图标位（DD icon_offset 0,32） · 数据未接入 ⇒ 色块占位，不换不删
heirloom_exchange_skeleton.tscn | HxArrowAnchor            | 0.0771/0.0741      | -                |        |                          | 箭头         | 箭头位（DD arrow_offset 148,80） · 数据未接入 ⇒ 色块占位，不换不删
hero_detail_skeleton.tscn      | HeroEquipArea            | 0.1011/0.4778      | -                |        |                          | 装备         | 装备区（DD panel.hero equipment 238,0 · 数据未接入 ⇒ 色块占位，不换不删）
hero_detail_skeleton.tscn      | HeroTrinketArea          | 0.1011/0.66        | -                |        |                          | 饰品         | 饰品区（DD panel.hero trinket 453,0 · 数据未接入 ⇒ 色块占位，不换不删）
hero_detail_skeleton.tscn      | HeroDiseaseIcon          | 0.055              | -                |        |                          | -          | 疾病图标（DD hero_portrait_icon_layout.disease_icon_offset 61,61 · 需美术 ⇒ 色块
hero_detail_skeleton.tscn      | HeroScoutingStat         | 0.0/0.64           | -                |        |                          | -          | 侦察数值（DD hero_scouting_stat_layout · 数据未接入 ⇒ 色块占位，不换不删）
hero_detail_skeleton.tscn      | HeroEquipHighlight       | 0.36/0.0           | -                |        |                          | -          | 装备高亮框（DD hero_equipment_layout.highlight_pos_offset -20,-20 · 需美术 ⇒ 色块
hero_detail_skeleton.tscn      | HeroEquipLevelText       | 0.455              | -                |        |                          | -          | 装备等级位（DD hero_equipment_layout.level_offset 90,12 · 对应攻/防=装备等级口径 · 色块占
hero_detail_skeleton.tscn      | HeroStatsIcon            | 0.0/0.145          | -                |        |                          | -          | 属性图标位（DD hero_base_stats_layout.icon_offset -26,2 · 需美术 ⇒ 色块占位，不换不删）
hero_detail_skeleton.tscn      | HdNameAnchor             | 0.0545/0.0241      | -                |        |                          | 英雄名        | 英雄名位（DD name_pos 76,26） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdClassAnchor            | 0.0545/0.0741      | -                |        |                          | 职业         | 职业位（DD class_pos 76,80） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdQuirksAnchor           | 0.1011/0.1185      | -                |        |                          | 怪癖         | 怪癖区（DD quirks_pos 141,128） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdHeroPipsAnchor         | 0.6065/0.1343      | -                |        |                          | 位点         | 位点（DD hero_pips_pos 846,145） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdTargetPipsAnchor       | 0.8265/0.1343      | -                |        |                          | 目标位点       | 目标位点（DD target_pips_pos 1153,145） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdPosTitleAnchor         | 0.5448/0.0907      | -                |        |                          | 位点标题       | 位点标题（DD position_pips_title_pos 760,98） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdCombatSkillAnchor      | 0.5591/0.1444      | -                |        |                          | 战斗技能       | 战斗技能格（DD combat_skill_grid_pos 780,156） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdCampSkillAnchor        | 0.5591/0.2963      | -                |        |                          | 扎营技能       | 扎营技能格（DD camping_skill_grid_pos 780,320） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdResistAnchor           | 0.5591/0.4037      | -                |        |                          | 抗性         | 抗性区（DD resistances_pos 780,436） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdClassBonusAnchor       | 0.5591/0.5241      | -                |        |                          | 职业加成       | 职业加成（DD class_bonuses_pos 780,566） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdCloseAnchor            | 0.9688/0.0167      | -                |        |                          | 关闭         | 关闭键位（DD close_pos 1344,18） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HdHeroArtAnchor          | 0.0703/0.6481      | -                |        |                          | 立绘         | 英雄立绘位（DD hero_pos 98,700） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HhEquipHotspot           | 0.1011/0.4778      | Vector2(72, 144) | L2     | hero_trinket_grid_layout | 装备热区       | 装备 tooltip 热区（DD hero_equipment_layout tooltip_hotspot_size 72x144 ⇒ 与
hero_detail_skeleton.tscn      | HhBaseStatHotspot        | 0.1011/0.3315      | Vector2(160, 20) | L2     | hero_trinket_grid_layout | 属性热区       | 基础属性热区（DD hero_base_stats_layout tooltip_hotspot_size 160x20） · 色块占位，不
hero_detail_skeleton.tscn      | HhStatHotspot            | 0.5591/0.1444      | Vector2(160, 20) | L2     | hero_trinket_grid_layout | 属性热区       | 属性热区（DD hero_stats_layout tooltip_hotspot_size 160x20） · 色块占位，不换不删
hero_detail_skeleton.tscn      | HhScoutingHotspot        | 0.0/0.626          | Vector2(300, 40) | L2     | hero_trinket_grid_layout | 侦察热区       | 侦察数值热区（DD hero_scouting_stat_layout tooltip_hotspot_size 300x40） · 色块占
loot_overlay_skeleton.tscn     | LootDescBlock            | -                  | Vector2(350, 40) | L3     | loot_background          | -          | 描述（DD loot_description .pos 228,136 · .width 350 · 数据未接入 ⇒ 色块占位，不换不删）
loot_overlay_skeleton.tscn     | TakeAllBlock             | -                  | Vector2(80, 30)  | L3     | loot_background          | -          | 全部拿取（DD loot_buttons .take_all_pos 80,358 · 数据未接入 ⇒ 色块占位，不换不删）
loot_overlay_skeleton.tscn     | CloseBlock               | -                  | Vector2(80, 30)  | L3     | loot_background          | -          | 关闭（DD loot_buttons .close_pos 306,358 · 色块占位，不换不删）
loot_overlay_skeleton.tscn     | LootTakeAllAnchor        | 0.0417/0.3315      | -                |        |                          | 全部拿取       | 全部拿取位（DD overlay.loot take_all_pos 80,358） · 数据未接入 ⇒ 色块占位，不换不删
loot_overlay_skeleton.tscn     | LootCloseAnchor          | 0.1594/0.3315      | -                |        |                          | 关闭         | 关闭位（DD close_pos 306,358） · 数据未接入 ⇒ 色块占位，不换不删
loot_overlay_skeleton.tscn     | LootDescAnchor           | 0.1188/0.1259      | -                | L3     | loot_background          | 描述         | 描述位（DD loot_description pos 228,136 · width 350） · 数据未接入 ⇒ 色块占位，不换不删
main_menu.tscn                 | MenuBackButtonAnchor     | 0.4474/0.0954      | -                |        |                          | 返回         | 返回按钮位（DD shared/menu back_button_pos 859,103 · 屏幕级 ÷1920 ÷1080 · 数据未接入
main_menu.tscn                 | MenuNameTooltipHotArea   | 0.02/0.86          | Vector2(360, 50) |        |                          | 名称提示       | 元素名 tooltip 热区（DD element_name_tooltip_hot_area_size 360x50 · 自带尺寸 ⇒ 直
main_menu.tscn                 | MenuControllerHotArea    | 0.0198/0.9         | Vector2(600, 36) |        |                          | 手柄提示       | 手柄勾选热区（DD element_control_check_box_controller_hotspot_size 600x36 · 自
panel_banner_skeleton.tscn     | BannerPanel              | 0.2/0.02           | Vector2(754, 136) |        |                          | -          | 战斗横幅（DD panel_banner.png 754x136 · 屏位未取得 ⇒ 顶部居中约定）
panel_banner_skeleton.tscn     | BannerPortrait           | 0.0424/0.2353      | -                | L3     | background_layout        | 立绘         | 横幅立绘位（DD portrait_layout pos 32,32 · 基准 754x136 · 需美术 ⇒ 色块占位，不换不删）
panel_banner_skeleton.tscn     | BannerSeal               | 0.0318/0.1691      | -                |        |                          | 徽记         | 横幅封印/徽记位（DD seal_pos 24,23 · 需美术 ⇒ 色块占位，不换不删）
panel_banner_skeleton.tscn     | BannerName               | 0.3607/0.2794      | -                | L3     | background_layout        | 名称         | 横幅名称位（DD name_layout pos 272,38 · 数据未接入 ⇒ 色块占位，不换不删）
panel_banner_skeleton.tscn     | BannerAbility            | 0.3714/0.2574      | -                | L3     | background_layout        | 技能         | 横幅技能位（DD ability_layout pos 280,35 · 数据未接入 ⇒ 色块占位，不换不删）
provision_skeleton.tscn        | ProvQuestInfoAnchor      | 0.6771/0.0889      | -                | L2     |                          | 任务信息       | 任务信息位（DD provision quest_info_pos 1300,96 · 屏幕级 ⇒ ÷1920 ÷1080） · 数据未接入
provision_skeleton.tscn        | ProvScoutingAnchor       | 0.7188/0.0889      | -                |        |                          | 侦察         | 侦察数值位（DD scouting_stat_pos 1380,96） · 数据未接入 ⇒ 色块占位，不换不删
provision_skeleton.tscn        | ProvSellBackAnchor       | 0.6063/0.4722      | -                |        |                          | 售回         | 售回信息位（DD provision_sell_back_info_pos 1164,510） · 数据未接入 ⇒ 色块占位，不换不删
provision_skeleton.tscn        | ProvStoreAnchor          | 0.4167/0.4926      | -                | L2     | map_layout               | 商店         | 商店背景位（DD provision pos 800,532；网格 start_pos 60,28 需面板尺寸 ⇒ 未取得） · 数据未接入
quest_select_skeleton.tscn     | QuestMap                 | -                  | -                |        |                          | -          | 任务地图（按地牢切换：DD quest_map_pos）
quest_select_skeleton.tscn     | AllQuestMap              | 0.4/               | -                | L2     |                          | -          | 全图（DD all_quest_map_pos）
quest_select_skeleton.tscn     | DungeonEffectOverlay     | -                  | -                | L2     | town_quest_select_layout | -          | 地牢效果覆盖（DD dungeon_effect_overlay_pos）
quest_select_skeleton.tscn     | QsQuestMapAnchor         | 0.5208/0.4861      | -                |        |                          | 任务地图       | 任务地图位（DD quest_map_pos 1000,525 · 屏幕级） · 数据未接入 ⇒ 色块占位，不换不删
quest_select_skeleton.tscn     | QsAllQuestMapAnchor      | 0.4427/0.6481      | -                |        |                          | 全图         | 全图位（DD all_quest_map_pos 850,700） · 数据未接入 ⇒ 色块占位，不换不删
quest_select_skeleton.tscn     | QsEffectOverlayAnchor    | 0.5208/0.5185      | -                |        |                          | 地牢效果       | 地牢效果覆盖位（DD dungeon_effect_overlay_pos 1000,560） · 数据未接入 ⇒ 色块占位，不换不删
quest_select_skeleton.tscn     | QsDungeonXpBar           | 0.02/0.75          | Vector2(194, 8)  |        |                          | 地牢经验       | 地牢 XP 条（DD dungeon_xp_bar_size 194x8 · 自带尺寸 ⇒ 直接用 ✓） · 色块占位，不换不删
quest_select_skeleton.tscn     | QsXpBarTtHotArea         | 0.02/0.81          | Vector2(232, 48) |        |                          | 经验热区       | XP 条 tooltip 热区（DD dungeon_xp_bar_tt_hot_area_size 232x48 · 自带尺寸 ✓） · 
quest_select_skeleton.tscn     | QsTownEventIconTt        | 0.02/0.87          | Vector2(32, 32)  |        |                          | 城镇事件       | 城镇事件图标 tooltip（DD town_event_icon_tooltip_hot_area_size 32x32 · 自带尺寸 ✓
quest_select_skeleton.tscn     | QsCampingTt              | 0.02/0.93          | Vector2(80, 30)  |        |                          | 扎营提示       | 扎营 tooltip（DD camping_tt_size 80x30 · 自带尺寸 ✓） · 色块占位，不换不删
quest_select_skeleton.tscn     | QsWaveHighscoreTt        | 0.0198/0.7         | Vector2(260, 160) |        |                          | 波次高分       | 波次高分 tooltip（DD wave_highscore_tt_size 260x160 · 自带尺寸 ✓） · 色块占位，不换不删
raid_results_skeleton.tscn     | RrMore1                  | 0.0208/0           | -                | L1     | raid_results_screen_layout | 任务库存       | DD raid_results [raid_results_quest_inventory_system_grid_layout] star
raid_results_skeleton.tscn     | RrMore2                  | 0.0521/0           | -                | L1     | raid_results_screen_layout | 队伍金币       | DD raid_results [raid_results_party_gold_inventory_system_grid_layout]
raid_results_skeleton.tscn     | RrMore3                  | 0.0521/0           | -                | L1     | raid_results_screen_layout | -          | DD raid_results [raid_results_party_heirloom_inventory_system_grid_lay
raid_results_skeleton.tscn     | RrMore4                  | 0.0521/0           | -                | L1     | raid_results_screen_layout | -          | DD raid_results [wave_raid_results_party_gold_inventory_system_grid_la
raid_results_skeleton.tscn     | RrMore5                  | 0.0677/0.2315      | -                | L1     | raid_results_screen_layout | -          | DD raid_results [raid_results_heroes_state_layout] heroes_start_pos 13
roster_row.tscn                | EquipLevelSlot           | 0.4216/0.6701      | -                |        |                          | -          | 装备等级·攻（DD weapon_level_offset 156,65 · 数据未接入 ⇒ 色块占位，不换不删）
roster_row.tscn                | ArmourLevelSlot          | 0.6162/0.6701      | -                |        |                          | -          | 装备等级·防（DD armour_level_offset 228,65 · 数据未接入 ⇒ 色块占位，不换不删）
unit_card.tscn                 | MonsterType              | 0.0926/0.3043      | -                |        |                          | -          | 类型（DD panel.monster .type_pos 65,112 · 数据未接入 ⇒ 色块占位，不换不删）
unit_card.tscn                 | MonsterResistances       | 0.1425/0.5978      | -                |        |                          | -          | 抗性（DD panel.monster .resistances_title_pos 154,186 / .resistances_pos 
unit_card.tscn                 | MonsterSkillsTitle       | 0.6838/0.5054      | -                |        |                          | -          | 技能标题（DD panel.monster .skills_title_pos 480,186 · 数据未接入 ⇒ 色块占位，不换不删）
unit_card.tscn                 | MonsterHeroStats         | 0.6197/0.3016      | -                |        |                          | -          | 英雄对比属性（DD panel.monster hero_stats_pos 435,111 · 基准 702x368 · 数据未接入 ⇒ 
unit_card.tscn                 | MonsterResistEntryIcon   | 0.0/0.0217         | -                |        |                          | -          | 抗性条目图标（DD panel.monster resistances_entry_icon_pos -10,8 · 基准 702x368 
```

## B 栏：我方自建（52 条）

```
场景 | 节点 | 锚点 | 尺寸 | 层级 | 归属容器 | 标签
battle_bottombar.tscn          | BackSlot5                | -                  | Vector2(72, 112) |        |                          | 5·6号位
battle_bottombar.tscn          | CArea                    | -                  | Vector2(260, 0)  |        |                          | 技能区
battle_bottombar.tscn          | ActorDetailBox           | -                  | Vector2(0, 84)   |        |                          | 角色详情
battle_bottombar.tscn          | EArea                    | -                  | -                |        |                          | 页区
battle_bottombar.tscn          | InvSlot1                 | -                  | Vector2(80, 160) |        |                          | -
battle_bottombar.tscn          | InvSlot2                 | -                  | Vector2(80, 160) |        |                          | -
battle_bottombar.tscn          | InvSlot3                 | -                  | Vector2(80, 160) |        |                          | -
battle_bottombar.tscn          | InvSlot4                 | -                  | Vector2(80, 160) |        |                          | -
battle_bottombar.tscn          | InvSlot5                 | -                  | Vector2(80, 160) |        |                          | -
battle_bottombar.tscn          | InvSlot6                 | -                  | Vector2(80, 160) |        |                          | -
battle_bottombar.tscn          | InvSlot7                 | -                  | Vector2(80, 160) |        |                          | -
battle_bottombar.tscn          | InvSlot8                 | -                  | Vector2(80, 160) |        |                          | -
building_popup.tscn            | ShopkeeperSlot           | -                  | Vector2(0, 96)   |        |                          | 店主
building_popup.tscn            | CostPlaceholder          | -                  | Vector2(160, 24) |        |                          | -
building_popup.tscn            | ConfirmPlaceholder       | -                  | Vector2(120, 28) |        |                          | -
building_popup.tscn            | NamePlaceholder          | -                  | Vector2(200, 22) |        |                          | -
building_popup.tscn            | DescPlaceholder          | -                  | Vector2(260, 40) |        |                          | -
hamlet_skeleton.tscn           | TopBar                   | -                  | -                |        |                          | -
hamlet_skeleton.tscn           | StatusBar                | -                  | -                |        |                          | -
hamlet_skeleton.tscn           | LeftColumn               | -                  | -                |        |                          | -
hamlet_skeleton.tscn           | RightColumn              | -                  | Vector2(370, 0)  |        |                          | -
hamlet_skeleton.tscn           | BottomBar                | -                  | -                |        |                          | -
hamlet_skeleton.tscn           | DDNav0_stage_coach       | -                  | Vector2(128, 56) |        |                          | -
hamlet_skeleton.tscn           | DDNav4_tavern            | -                  | Vector2(128, 56) |        |                          | -
hamlet_skeleton.tscn           | DDNav5_abbey             | -                  | Vector2(128, 56) |        |                          | -
loot_overlay_skeleton.tscn     | LootTile1                | -                  | Vector2(53, 53)  |        |                          | -
loot_overlay_skeleton.tscn     | LootTile2                | -                  | Vector2(53, 53)  |        |                          | -
loot_overlay_skeleton.tscn     | LootTile3                | -                  | Vector2(53, 53)  |        |                          | -
loot_overlay_skeleton.tscn     | LootTile4                | -                  | Vector2(53, 53)  |        |                          | -
main_menu.tscn                 | TitlePanel               | -                  | Vector2(0, 56)   |        |                          | -
main_menu.tscn                 | OptionsPanel             | -                  | Vector2(600, 432) |        |                          | -
main_menu.tscn                 | StatusPanel              | -                  | Vector2(0, 36)   |        |                          | -
modal_dialog.tscn              | ModalDialog              | -                  | -                |        |                          | -
provision_skeleton.tscn        | BlockPlaceholder         | -                  | -                |        |                          | -
provision_skeleton.tscn        | BlockPlaceholder         | -                  | -                |        |                          | -
provision_skeleton.tscn        | BlockPlaceholder         | -                  | -                |        |                          | -
provision_skeleton.tscn        | BlockPlaceholder         | -                  | -                |        |                          | -
raid_results_skeleton.tscn     | RrQuestTitle             | 0.5/0.1963         | -                |        |                          | 任务标题
raid_results_skeleton.tscn     | RrQuestResult            | 0.5/0.1389         | -                |        |                          | 任务结果
raid_results_skeleton.tscn     | RrState                  | 0.2656/0.0         | -                |        |                          | 状态
raid_results_skeleton.tscn     | RrCompletionBg           | 0.5/0.0            | -                |        |                          | 完成背景
raid_results_skeleton.tscn     | RrLevelBg                | 0.0/0.0            | -                |        |                          | 等级背景
raid_results_skeleton.tscn     | RrProgression            | 0.0/0.8870         | -                |        |                          | 进度条
raid_results_skeleton.tscn     | RrNext                   | 0.9740/0.9074      | -                |        |                          | 下一场
raid_results_skeleton.tscn     | RrBack                   | 0.0260/0.9074      | -                |        |                          | 返回
raid_results_skeleton.tscn     | RrQuestInvGrid           | 0.0438/0.2852      | -                |        |                          | 任务战利品
roster_row.tscn                | PortraitFrame            | 0.0568/0.0928      | -                |        |                          | -
roster_row.tscn                | HeroLevel                | 0.6973/0.0412      | -                |        |                          | -
slot_row.tscn                  | Frame                    | -                  | Vector2(26, 26)  |        |                          | -
tooltip.tscn                   | Tooltip                  | -                  | -                |        |                          | -
unit_card.tscn                 | UnitCard                 | -                  | -                |        |                          | -
unit_card.tscn                 | portraitBox              | -                  | Vector2(44, 44)  |        |                          | -
```

注：① DD 数字=度量事实；DD 像素未进工程（红线27）｜② 三项几何判据（越界/尺寸不自洽/offset-at-root）现为 **0/0/0** ✓
    ③ 生成脚本只用数组拼接（List.Add 在本环境致变量退化 ⇒ 弃用 ✓）｜④ DD 内部互证：图标 72×144 · 舞台锚点 788/1050/680
