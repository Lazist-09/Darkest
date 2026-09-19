# 阶段3 落位清单（色块位 ↔ DD 出处 ↔ 尺寸）

生成：只读扫描 `darkest/scenes/ui/*.tscn` 中带 `DD ...` 出处的占位块（本轮 27 条）· 2026-09-21

用法：美术按「节点 + 尺寸 + DD 出处」出**同尺寸原创资产**；替换时**只换贴图、不动锚点/尺寸** ⇒ 布局零改动 ✓

```
场景 | 节点 | 锚点(left/top) | 尺寸 + DD 出处
battle_bottombar.tscn          | RaidTorchGauge         | 0.30/0.02          | Vector2(400, 4)  DD screen.raid [torch_layout] gauge_size 400x4 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）
battle_bottombar.tscn          | RaidBasicScrollBtn     | 0.30/0.055         | Vector2(384, 36)  DD screen.raid [basic_scroll] button_size 384x36 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）
battle_bottombar.tscn          | RaidSidebarSlot        | 0.30/0.09          | Vector2(80, 160)  DD screen.raid [sidebar_scroll] item_slot_size 80x160 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）
battle_bottombar.tscn          | RaidResultScrollBtn    | 0.30/0.125         | Vector2(384, 36)  DD screen.raid [result_scroll] button_size 384x36 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）
battle_bottombar.tscn          | RaidTorchInfoArea      | 0.30/0.16          | Vector2(200, 130)  DD screen.raid [torch_info] mouseOverAreaSize 200x130 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）
battle_bottombar.tscn          | RaidTorchStripArea     | 0.30/0.195         | Vector2(860, 24)  DD screen.raid [torch_info] stripMouseOverAreaSize 860x24 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）
battle_bottombar.tscn          | RaidQuestInfoArea      | 0.30/0.23          | Vector2(300, 150)  DD screen.raid [quest_info] size 300x150 · 尺寸=DD 原值 ✓ · 位置待该段 *_pos（未读 ⇒ 约定位，不猜）
battle_bottombar.tscn          | RaidPos1               | 0.7969/0.0324      | DD screen.raid [kill_count_display] pos 1530,35 ⇒ 0.7969/0.0324 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos2               | -0.1328/0          | DD screen.raid [camp_layout] fourth_pos -255,0 ⇒ -0.1328/0 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos3               | 0.3812/0.0556      | DD screen.raid [camp_layout] respite_scroll_pos 732,60 ⇒ 0.3812/0.0556 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos4               | 0.0062/0.0185      | DD screen.raid [quest_info] pos 12,20 ⇒ 0.0062/0.0185 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos5               | 0.4938/0.5463      | DD screen.raid [quest_info] complete_mid_screen_pos 948,590 ⇒ 0.4938/0.5463 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos6               | 0/0.0509           | DD screen.raid [quest_info] complete_choice_shared_frame_pos 0,55 ⇒ 0/0.0509 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidPos7               | -0.0885/0.0907     | DD screen.raid [quest_info] complete_return_to_hamlet_pos -170,98 ⇒ -0.0885/0.0907 · 数据未接入 ⇒ 色块占
battle_bottombar.tscn          | RaidPos8               | 0.0469/0.0907      | DD screen.raid [quest_info] complete_continue_raid_pos 90,98 ⇒ 0.0469/0.0907 · 数据未接入 ⇒ 色块占位，不换不删
battle_bottombar.tscn          | RaidSec1               | 0.7969/0.125       | DD screen.raid [shard_escrow_display] pos 1530,135 ⇒ 0.7969/0.125 · 色块占位，不换不删
battle_bottombar.tscn          | RaidSec2               | 0.7969/0.0324      | DD screen.raid [wave_countdown_display] pos 1530,35 ⇒ 0.7969/0.0324 · 色块占位，不换不删
battle_bottombar.tscn          | RaidSec3               | 0.9167/0.5324      | DD screen.raid [skip_curio_display] pos 1760,575 ⇒ 0.9167/0.5324 · 色块占位，不换不删
hamlet_skeleton.tscn           | DDNav1_blacksmith      |                    | Vector2(128, 56)  DD building_navigation index 1: blacksmith (placeholder)
hamlet_skeleton.tscn           | DDNav2_guild           |                    | Vector2(128, 56)  DD building_navigation index 2: guild (placeholder)
hamlet_skeleton.tscn           | DDNav3_camping_trainer |                    | Vector2(128, 56)  DD building_navigation index 3: camping_trainer (placeholder)
hamlet_skeleton.tscn           | DDNav6_sanitarium      |                    | Vector2(128, 56)  DD building_navigation index 6: sanitarium (placeholder)
hamlet_skeleton.tscn           | DDNav7_nomad_wagon     |                    | Vector2(128, 56)  DD building_navigation index 7: nomad_wagon (placeholder)
hamlet_skeleton.tscn           | DDNav8_graveyard       |                    | Vector2(128, 56)  DD building_navigation index 8: graveyard (placeholder)
hamlet_skeleton.tscn           | DDNav9_statue          |                    | Vector2(128, 56)  DD building_navigation index 9: statue (placeholder)
hamlet_skeleton.tscn           | EstateSummary          | 0.0/0.903          | DD estate_summary_pos 0,975 (resource bar, placeholder)
hamlet_skeleton.tscn           | RealmInventory         | 0.459/0.119        | DD realm_inventory_pos 881,128 (realm inventory, placeholder)
```

注：① 未标尺寸者 = 该块尺寸由 DD 键推得或为比例块（按锚点区域出图）✓
    ② 所有 DD 数字均为**度量事实**（不构成抄袭）；**DD 像素未进入工程** ✓（红线27）
    ③ 本清单由脚本只读生成 ⇒ 可与代码同步再生 ✓
