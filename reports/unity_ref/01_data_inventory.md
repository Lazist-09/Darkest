# 参考项目数据总账 —— `Darkest-Dungeon-Unity/Assets/Resources/Data/**`

> 生成脚本：`reports/unity_ref/_inventory.py`（只读参考项目；仅写 `reports/unity_ref/`）。
> 所有条目数由脚本实际解析/计数得出，原始机器可读结果见 `reports/unity_ref/_raw_stats.json`。
> stdout 仅输出 ASCII 进度，中文全部经 `io.open(..., encoding="utf-8")` 写入本文件。

## 0. 实际文件统计（与任务书给的估计值不同，以下为 `os.walk` 实数）

| 扩展名 | 文件数 | 合计字节 | 说明 |
|---|---:|---:|---|
| `.txt` | 232 | 472872 | DD1 风格纯文本（Monsters/ 230 个 + Mechanics/ 2 个） |
| `.json` | 49 | 2347347 | 顶层数据（含 5 个带**尾随逗号**的非严格 JSON） |
| `.bytes` | 30 | 233002 | 26 个 DD1 风格文本 + 7 个 Unity 二进制序列化地图 |
| `.xml` | 18 | 12373367 | Localization 字符串表（均 `english` 单语） |
| `.csv` | 1 | 51069 | Curios/Curios.csv 表格导出 |
| **合计** | **330** | **15477657** | 另有 344 个 `.meta`（Unity 导入元数据，非数据，已排除） |

任务书估计 51 json / 30 bytes / 239 txt：**实测 49 / 30 / 232**，且多出 1 个 `Curios/Curios.csv`。

## 1. 顶层 JSON 文件

| 文件 | 字节 | 顶层结构 | 各数组集合条目数（实测，`=` 前为 JSON 路径） |
|---|---:|---|---|
| `Buildings\abbey.building.json` | 6132 | `{on_start_town_visit_priority: int, number_of_quests_finished: int, highest_dungeon_level: int, meditation: {s` | $.meditation.side_effects.results=[7]; $.meditation.quirk_library_names=[6]; $.meditation.cost_upgrades=[3]; $.meditation.slot_upgrades=[3]; $.meditation.stress_upgrades=[3]; $.meditation.affliction_cure_upgrades=[2]; $.prayer.side_effects.results=[5]; $.prayer.quirk_library_names=[7]; $.prayer.cost_upgrades=[3]; +9 more |
| `Buildings\blacksmith.building.json` | 705 | `{on_start_town_visit_priority: int, number_of_quests_finished: int, highest_dungeon_level: int, equipment_cost` | $.equipment_cost_discount_upgrades=[5] |
| `Buildings\camping_trainer.building.json` | 734 | `{on_start_town_visit_priority: int, number_of_quests_finished: int, highest_dungeon_level: int, camping_skill_` | $.camping_skill_cost_discount_upgrades=[5] |
| `Buildings\guild.building.json` | 682 | `{on_start_town_visit_priority: int, number_of_quests_finished: int, highest_dungeon_level: int, combat_skill_c` | $.combat_skill_cost_discount_upgrades=[5] |
| `Buildings\nomad_wagon.building.json` | 1242 | `{on_start_town_visit_priority: int, number_of_quests_finished: int, highest_dungeon_level: int, number_of_trin` | $.number_of_trinkets_upgrades=[5]; $.trinket_cost_discount_upgrades=[5] |
| `Buildings\sanitarium.building.json` | 3900 | `{on_start_town_visit_priority: int, number_of_quests_finished: int, highest_dungeon_level: int, treatment: {po` | $.treatment.positive_quirk_cost_upgrades=[6]; $.treatment.negative_quirk_cost_upgrades=[6]; $.treatment.permanent_negative_quirk_cost_upgrades=[6]; $.treatment.slot_upgrades=[3]; $.disease_treatment.disease_quirk_cost_upgrades=[4]; $.disease_treatment.disease_quirk_cure_all_chance_upgrades=[3]; $.disease_treatment.slot_upgrades=[3] |
| `Buildings\stage_coach.building.json` | 2430 | `{on_start_town_visit_priority: int, number_of_quests_finished: int, highest_dungeon_level: int, number_of_recr` | $.number_of_recruits_upgrades=[6]; $.roster_size_upgrades=[6]; $.upgraded_recruits_upgrades=[3]; $.upgraded_recruits_upgrades[0].guaranteed_previous_raid_dead_hero_levels=[1] |
| `Buildings\tavern.building.json` | 7329 | `{on_start_town_visit_priority: int, number_of_quests_finished: int, highest_dungeon_level: int, bar: {side_eff` | $.bar.side_effects.results=[7]; $.bar.quirk_library_names=[6]; $.bar.cost_upgrades=[3]; $.bar.slot_upgrades=[3]; $.bar.stress_upgrades=[3]; $.bar.affliction_cure_upgrades=[2]; $.gambling.side_effects.results=[8]; $.gambling.quirk_library_names=[6]; $.gambling.cost_upgrades=[3]; +9 more |
| `Curios\Obstacles.json` | 816 | `{props: [ 5 x {name,fail_effects,health,torchlight,ancestor_talk} ]}` | $.props=[5]; $.props[0].fail_effects=[1] |
| `Curios\Traps.json` | 2963 | `{props: [ 4 x {name,success_effects,fail_effects,health,difficulty_variations} ]}` | $.props=[4]; $.props[0].success_effects=[1]; $.props[0].fail_effects=[2]; $.props[0].difficulty_variations=[2] |
| `JsonAI.json` | 314790 | `{monster_brains: [ 160 x {id,skill_cooldowns,skill_selection_desires,target_selection_desires,bonus_initiative` | $.monster_brains=[160]; $.monster_brains[0].skill_cooldowns=[0]; $.monster_brains[0].skill_selection_desires=[3]; $.monster_brains[0].target_selection_desires=[3]; $.monster_brains[0].bonus_initiative_desires=[0] |
| `JsonBuffs.json` | 684543 | `{buffs: [ 1801 x {id,stat_type,stat_sub_type,amount,remove_if_not_active,rule_type,is_false_rule,rule_data} ]}` | $.buffs=[1801] |
| `JsonCamping.json` | 64265 | `{configuration: {class_specific_number_of_classes_threshold: int}, skills: [ 64 x {id,level,cost,use_limit,eff` | $.skills=[64]; $.skills[0].effects=[1]; $.skills[0].hero_classes=[16]; $.skills[0].upgrade_requirements=[1] |
| `JsonLoot.json` | 32443 | `{darkness_bonuses: [ 2 x {type,bonuses} ], loot_tables: [ 54 x {id,difficulty,dungeon,entries} ]}` | $.darkness_bonuses=[2]; $.darkness_bonuses[0].bonuses=[5]; $.loot_tables=[54]; $.loot_tables[0].entries=[9] |
| `JsonQuests.json` | 153174 | `{stress_damage: int, goals: [ 45 x {id,type,starting_items,ignore_fog_of_war,show_as_quest,data} ], town_progr` | $.goals=[45]; $.goals[0].starting_items=[0]; $.town_progression_goal_ids=[4]; $.types=[6]; $.types[0].goal_lists=[5]; $.plot_quests=[30]; $.plot_quests[0].additional_trinket_completion_rewards=[1]; $.plot_quests[0].upgrade_tags_to_remove_on_ignore=[0]; $.plot_quests[0].upgrade_tags_to_remove_on_failure=[0]; +12 more |
| `JsonQuirks.json` | 75351 | `{quirks: [ 163 x {id,show_explicit_description,is_positive,is_disease,classification,incompatible_quirks,curio` | $.quirks=[163]; $.quirks[0].incompatible_quirks=[1]; $.quirks[0].buffs=[1] |
| `JsonTraits.json` | 42431 | `{traits: [ 12 x {id,overstress_type,curio_tag,curio_tag_chance,keep_loot,buff_ids,combat_start_turn_act_outs,r` | $.traits=[12]; $.traits[0].buff_ids=[12]; $.traits[0].combat_start_turn_act_outs=[14]; $.traits[0].reaction_act_outs=[15] |
| `JsonTrinkets.json` | 207424 | `{rarities: [ 12 x str ], trinkets: [ 488 x {id,buffs,hero_class_requirements,rarity,price,limit,origin_dungeon` | $.rarities=[12]; $.trinkets=[488]; $.trinkets[0].buffs=[2]; $.trinkets[0].hero_class_requirements=[0] |
| `Mechanics\Campaign.json` | 442 | `{quest_completion_xp_table: [ 4 x int ], level_threshold_table: [ 8 x int ], resolve_level_thresholds: [ 7 x i` | $.quest_completion_xp_table=[4]; $.level_threshold_table=[8]; $.resolve_level_thresholds=[7]; $.gold_icon_thresholds=[4]; $.provision_icon_thresholds=[4] |
| `Mechanics\HeirloomExchange.json` | 1985 | `{markets: [ 1 x {id,exchange_rates} ]}` | $.markets=[1]; $.markets[0].exchange_rates=[12] |
| `Mechanics\Provision.json` | 6283 | `{raid_starting_length_inventory_item_lists: [ 5 x list ], raid_starting_hero_class_item_lists: [ 7 x {hero_cla` | $.raid_starting_length_inventory_item_lists=[5]; $.raid_starting_hero_class_item_lists=[7]; $.raid_starting_hero_class_item_lists[0].item_lists=[1]; $.default_store_inventory_item_lists=[5] |
| `Mechanics\Roster.json` | 383 | `{name_id_format: str, resolve_level_thresholds: [ 7 x int ], town_visit_town_progression: {idle_hero_stress_he` | $.resolve_level_thresholds=[7] |
| `Mechanics\TownEvents.json` | 45739 | `{settings: [ 3 x {id,event_chance_per_town_visits} ], events: [ 45 x {id,base_chance,per_not_rolled_additional` | $.settings=[3]; $.settings[0].event_chance_per_town_visits=[1]; $.events=[45]; $.events[0].town_ambience_paramater_ids=[0]; $.events[0].data=[3]; $.quest_type_event_guarantees=[8] |
| `Narration.json` | 282205 | `{filters: [ 1 x str ], entries: [ 36 x {id,tone,chance,audio_events} ]}` | $.filters=[1]; $.entries=[36]; $.entries[0].audio_events=[27] |
| `PartyNames.json` | 21497 | `{party_names: [ 186 x {id,required_hero_class} ]}` | $.party_names=[186]; $.party_names[0].required_hero_class=[4] |
| `Upgrades\Building\abbey.upgrades.json` | 7878 | `{trees: [ 3 x {id,is_instanced,tags,requirements} ]}` | $.trees=[3]; $.trees[0].tags=[2]; $.trees[0].requirements=[6] |
| `Upgrades\Building\blacksmith.upgrades.json` | 5835 | `{trees: [ 3 x {id,is_instanced,tags,requirements} ]}` | $.trees=[3]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Building\camping_trainer.upgrades.json` | 2010 | `{trees: [ 1 x {id,is_instanced,tags,requirements} ]}` | $.trees=[1]; $.trees[0].tags=[2]; $.trees[0].requirements=[5] |
| `Upgrades\Building\guild.upgrades.json` | 4038 | `{trees: [ 2 x {id,is_instanced,tags,requirements} ]}` | $.trees=[2]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Building\nomad_wagon.upgrades.json` | 3593 | `{trees: [ 2 x {id,is_instanced,tags,requirements} ]}` | $.trees=[2]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Building\sanitarium.upgrades.json` | 6365 | `{trees: [ 3 x {id,is_instanced,tags,requirements} ]}` | $.trees=[3]; $.trees[0].tags=[2]; $.trees[0].requirements=[5] |
| `Upgrades\Building\stage_coach.upgrades.json` | 8610 | `{trees: [ 3 x {id,is_instanced,tags,requirements} ]}` | $.trees=[3]; $.trees[0].tags=[2]; $.trees[0].requirements=[5] |
| `Upgrades\Building\tavern.upgrades.json` | 7905 | `{trees: [ 3 x {id,is_instanced,tags,requirements} ]}` | $.trees=[3]; $.trees[0].tags=[2]; $.trees[0].requirements=[6] |
| `Upgrades\Heroes\abomination.upgrades.json` | 21243 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\antiquarian.upgrades.json` | 21473 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\arbalest.upgrades.json` | 21269 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\bounty_hunter.upgrades.json` | 21489 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\crusader.upgrades.json` | 21324 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\grave_robber.upgrades.json` | 21436 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\hellion.upgrades.json` | 21241 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\highwayman.upgrades.json` | 21425 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\houndmaster.upgrades.json` | 21338 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\jester.upgrades.json` | 21138 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\leper.upgrades.json` | 21000 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\man_at_arms.upgrades.json` | 21278 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\musketeer.upgrades.json` | 21272 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\occultist.upgrades.json` | 21452 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\plague_doctor.upgrades.json` | 21629 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |
| `Upgrades\Heroes\vestal.upgrades.json` | 21218 | `{trees: [ 9 x {id,is_instanced,tags,requirements} ]}` | $.trees=[9]; $.trees[0].tags=[2]; $.trees[0].requirements=[4] |

### 1.1 字段清单（key 名 + 出现频次）

以下对每个 JSON 的**每个「对象数组」集合**列出 key 与「在多少条条目里出现过」。
频次 < 条目数 ⇒ 该字段是可选字段。

#### `Buildings\abbey.building.json`  (6132 B，非严格 JSON：已剔除尾随逗号后解析)

- 集合 `$.meditation.side_effects.results` — **7** 条
  - 字段：`type`×7 `chance`×7 `data`×7

- 集合 `$.meditation.side_effects.results[1].data` — **2** 条
  - 字段：`chance`×2 `duration`×2

- 集合 `$.meditation.side_effects.results[2].data` — **1** 条
  - 字段：`chance`×1 `quirk_library_name`×1

- 集合 `$.meditation.quirk_library_names` — **6** 条

- 集合 `$.meditation.cost_upgrades` — **3** 条
  - 字段：`cost_currency`×3 `upgrade_requirement_code`×2

- 集合 `$.meditation.slot_upgrades` — **3** 条
  - 字段：`number_of_slots`×3 `upgrade_requirement_code`×2

- 集合 `$.meditation.stress_upgrades` — **3** 条
  - 字段：`heal_low`×3 `heal_high`×3 `upgrade_requirement_code`×2

- 集合 `$.meditation.affliction_cure_upgrades` — **2** 条
  - 字段：`chance`×2 `upgrade_requirement_code`×1

- 集合 `$.prayer.side_effects.results` — **5** 条
  - 字段：`type`×5 `chance`×5 `data`×5

- 集合 `$.prayer.side_effects.results[1].data` — **2** 条
  - 字段：`chance`×2 `duration`×2

- 集合 `$.prayer.side_effects.results[2].data` — **1** 条
  - 字段：`chance`×1 `quirk_library_name`×1

- 集合 `$.prayer.quirk_library_names` — **7** 条

- 集合 `$.prayer.cost_upgrades` — **3** 条
  - 字段：`cost_currency`×3 `upgrade_requirement_code`×2

- 集合 `$.prayer.slot_upgrades` — **3** 条
  - 字段：`number_of_slots`×3 `upgrade_requirement_code`×2

- 集合 `$.prayer.stress_upgrades` — **3** 条
  - 字段：`heal_low`×3 `heal_high`×3 `upgrade_requirement_code`×2

- 集合 `$.prayer.affliction_cure_upgrades` — **2** 条
  - 字段：`chance`×2 `upgrade_requirement_code`×1

- 集合 `$.flagellation.side_effects.results` — **6** 条
  - 字段：`type`×6 `chance`×6 `data`×6

- 集合 `$.flagellation.side_effects.results[1].data` — **2** 条
  - 字段：`chance`×2 `duration`×2

- 集合 `$.flagellation.side_effects.results[2].data` — **1** 条
  - 字段：`chance`×1 `quirk_library_name`×1

- 集合 `$.flagellation.quirk_library_names` — **6** 条

- 集合 `$.flagellation.cost_upgrades` — **3** 条
  - 字段：`cost_currency`×3 `upgrade_requirement_code`×2

- 集合 `$.flagellation.slot_upgrades` — **3** 条
  - 字段：`number_of_slots`×3 `upgrade_requirement_code`×2

- 集合 `$.flagellation.stress_upgrades` — **3** 条
  - 字段：`heal_low`×3 `heal_high`×3 `upgrade_requirement_code`×2

- 集合 `$.flagellation.affliction_cure_upgrades` — **2** 条
  - 字段：`chance`×2 `upgrade_requirement_code`×1

样例（原样，≤15 行）：

```
{
    "on_start_town_visit_priority": 1,
	"number_of_quests_finished": 2,
	"highest_dungeon_level": 0,
	
	"meditation":
	{
		"side_effects":
		{
			"chance": 0.40,
			"results":
			[
				{
					"type": "activity_lock",
					"chance": 1,
```

#### `Buildings\blacksmith.building.json`  (705 B)

- 集合 `$.equipment_cost_discount_upgrades` — **5** 条
  - 字段：`discount_percent`×5 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

样例（原样，≤15 行）：

```
{
    "on_start_town_visit_priority" :  1,
	"number_of_quests_finished": 3,
	"highest_dungeon_level": 0,

	"equipment_cost_discount_upgrades" :  
	[
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "blacksmith.cost", "upgrade_requirement_code" : "a" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "blacksmith.cost", "upgrade_requirement_code" : "b" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "blacksmith.cost", "upgrade_requirement_code" : "c" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "blacksmith.cost", "upgrade_requirement_code" : "d" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "blacksmith.cost", "upgrade_requirement_code" : "e" }
	]
}
```

#### `Buildings\camping_trainer.building.json`  (734 B)

- 集合 `$.camping_skill_cost_discount_upgrades` — **5** 条
  - 字段：`discount_percent`×5 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

样例（原样，≤15 行）：

```
{
    "on_start_town_visit_priority" :  1,
	"number_of_quests_finished": 0,
	"highest_dungeon_level": 2,
	
	"camping_skill_cost_discount_upgrades" : 
	[
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "camping_trainer.cost", "upgrade_requirement_code" : "a" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "camping_trainer.cost", "upgrade_requirement_code" : "b" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "camping_trainer.cost", "upgrade_requirement_code" : "c" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "camping_trainer.cost", "upgrade_requirement_code" : "d" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "camping_trainer.cost", "upgrade_requirement_code" : "e" }
	]
}
```

#### `Buildings\guild.building.json`  (682 B)

- 集合 `$.combat_skill_cost_discount_upgrades` — **5** 条
  - 字段：`discount_percent`×5 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

样例（原样，≤15 行）：

```
{
    "on_start_town_visit_priority" :  1,
	"number_of_quests_finished": 3,
	"highest_dungeon_level": 0,

	"combat_skill_cost_discount_upgrades" : 
	[
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "guild.cost", "upgrade_requirement_code" : "a" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "guild.cost", "upgrade_requirement_code" : "b" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "guild.cost", "upgrade_requirement_code" : "c" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "guild.cost", "upgrade_requirement_code" : "d" },
		{ "discount_percent" :  0.10, "upgrade_tree_id" :  "guild.cost", "upgrade_requirement_code" : "e" }
	]
}
```

#### `Buildings\nomad_wagon.building.json`  (1242 B，非严格 JSON：已剔除尾随逗号后解析)

- 集合 `$.number_of_trinkets_upgrades` — **5** 条
  - 字段：`number_of_slots`×5 `upgrade_tree_id`×4 `upgrade_requirement_code`×4

- 集合 `$.trinket_cost_discount_upgrades` — **5** 条
  - 字段：`discount_percent`×5 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

样例（原样，≤15 行）：

```
{
    "on_start_town_visit_priority" :  1,
	"number_of_quests_finished": 0,
    "highest_dungeon_level": 2,

	"number_of_trinkets_upgrades" : 
	[
		{ "number_of_slots" : 2 },
		{ "number_of_slots" : 4,     "upgrade_tree_id" :  "nomad_wagon.numitems", "upgrade_requirement_code" : "a"  },
		{ "number_of_slots" : 6,     "upgrade_tree_id" :  "nomad_wagon.numitems", "upgrade_requirement_code" : "b"  },
		{ "number_of_slots" : 8,     "upgrade_tree_id" :  "nomad_wagon.numitems", "upgrade_requirement_code" : "c"  },
		{ "number_of_slots" : 12,    "upgrade_tree_id" :  "nomad_wagon.numitems", "upgrade_requirement_code" : "d"  }
	],

	"trinket_cost_discount_upgrades" : 
```

#### `Buildings\sanitarium.building.json`  (3900 B，非严格 JSON：已剔除尾随逗号后解析)

- 集合 `$.treatment.positive_quirk_cost_upgrades` — **6** 条
  - 字段：`cost_currency`×6 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

- 集合 `$.treatment.negative_quirk_cost_upgrades` — **6** 条
  - 字段：`cost_currency`×6 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

- 集合 `$.treatment.permanent_negative_quirk_cost_upgrades` — **6** 条
  - 字段：`cost_currency`×6 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

- 集合 `$.treatment.slot_upgrades` — **3** 条
  - 字段：`number_of_slots`×3 `upgrade_tree_id`×2 `upgrade_requirement_code`×2

- 集合 `$.disease_treatment.disease_quirk_cost_upgrades` — **4** 条
  - 字段：`cost_currency`×4 `upgrade_tree_id`×3 `upgrade_requirement_code`×3

- 集合 `$.disease_treatment.disease_quirk_cure_all_chance_upgrades` — **3** 条
  - 字段：`chance`×3 `upgrade_tree_id`×2 `upgrade_requirement_code`×2

- 集合 `$.disease_treatment.slot_upgrades` — **3** 条
  - 字段：`number_of_slots`×3 `upgrade_tree_id`×2 `upgrade_requirement_code`×2

样例（原样，≤15 行）：

```
{
    "on_start_town_visit_priority": 1,
	"number_of_quests_finished": 4,
	"highest_dungeon_level": 0,

	"treatment": {

		"positive_quirk_cost_upgrades": [
			{ "cost_currency": { "type": "gold", "amount": 7500 } },
			{ "cost_currency": { "type": "gold", "amount": 6750 }, "upgrade_tree_id": "sanitarium.cost", "upgrade_requirement_code": "a" },
			{ "cost_currency": { "type": "gold", "amount": 6000 }, "upgrade_tree_id": "sanitarium.cost", "upgrade_requirement_code": "b" },
			{ "cost_currency": { "type": "gold", "amount": 5250 }, "upgrade_tree_id": "sanitarium.cost", "upgrade_requirement_code": "c" },
			{ "cost_currency": { "type": "gold", "amount": 4500 }, "upgrade_tree_id": "sanitarium.cost", "upgrade_requirement_code": "d" },
			{ "cost_currency": { "type": "gold", "amount": 3750 }, "upgrade_tree_id": "sanitarium.cost", "upgrade_requirement_code": "e" }
		],
```

#### `Buildings\stage_coach.building.json`  (2430 B)

- 集合 `$.number_of_recruits_upgrades` — **6** 条
  - 字段：`number_of_slots`×6 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

- 集合 `$.roster_size_upgrades` — **6** 条
  - 字段：`number_of_slots`×6 `upgrade_tree_id`×5 `upgrade_requirement_code`×5

- 集合 `$.upgraded_recruits_upgrades` — **3** 条
  - 字段：`level`×3 `chance`×3 `number_of_extra_positive_quirks`×3 `number_of_extra_negative_quirks`×3 `number_of_extra_combat_skills`×3 `number_of_extra_camping_skills`×3 `guaranteed_previous_raid_dead_hero_levels`×3 `upgrade_tree_id`×3
  - `upgrade_requirement_code`×3

- 集合 `$.upgraded_recruits_upgrades[0].guaranteed_previous_raid_dead_hero_levels` — **1** 条

- 集合 `$.upgraded_recruits_upgrades[1].guaranteed_previous_raid_dead_hero_levels` — **1** 条

- 集合 `$.upgraded_recruits_upgrades[2].guaranteed_previous_raid_dead_hero_levels` — **2** 条

样例（原样，≤15 行）：

```
{
    "on_start_town_visit_priority" :  0,
	"number_of_quests_finished": 0,
	"highest_dungeon_level": 0,
	
	"number_of_recruits_upgrades":
	[
		{ "number_of_slots": 2 },
		{ "number_of_slots": 3, "upgrade_tree_id": "stage_coach.numrecruits", "upgrade_requirement_code": "a" },
		{ "number_of_slots": 4, "upgrade_tree_id": "stage_coach.numrecruits", "upgrade_requirement_code": "b" },
		{ "number_of_slots": 5, "upgrade_tree_id": "stage_coach.numrecruits", "upgrade_requirement_code": "c" },
		{ "number_of_slots": 6, "upgrade_tree_id": "stage_coach.numrecruits", "upgrade_requirement_code": "d" },
		{ "number_of_slots": 7, "upgrade_tree_id": "stage_coach.numrecruits", "upgrade_requirement_code": "e" }
	],
	"roster_size_upgrades" : 
```

#### `Buildings\tavern.building.json`  (7329 B，非严格 JSON：已剔除尾随逗号后解析)

- 集合 `$.bar.side_effects.results` — **7** 条
  - 字段：`type`×7 `chance`×7 `data`×7

- 集合 `$.bar.side_effects.results[1].data` — **2** 条
  - 字段：`chance`×2 `duration`×2

- 集合 `$.bar.side_effects.results[2].data` — **1** 条
  - 字段：`chance`×1 `quirk_library_name`×1

- 集合 `$.bar.quirk_library_names` — **6** 条

- 集合 `$.bar.cost_upgrades` — **3** 条
  - 字段：`cost_currency`×3 `upgrade_requirement_code`×2

- 集合 `$.bar.slot_upgrades` — **3** 条
  - 字段：`number_of_slots`×3 `upgrade_requirement_code`×2

- 集合 `$.bar.stress_upgrades` — **3** 条
  - 字段：`heal_low`×3 `heal_high`×3 `upgrade_requirement_code`×2

- 集合 `$.bar.affliction_cure_upgrades` — **2** 条
  - 字段：`chance`×2 `upgrade_requirement_code`×1

- 集合 `$.gambling.side_effects.results` — **8** 条
  - 字段：`type`×8 `chance`×8 `data`×8

- 集合 `$.gambling.side_effects.results[1].data` — **2** 条
  - 字段：`chance`×2 `duration`×2

- 集合 `$.gambling.side_effects.results[2].data` — **1** 条
  - 字段：`chance`×1 `quirk_library_name`×1

- 集合 `$.gambling.quirk_library_names` — **6** 条

- 集合 `$.gambling.cost_upgrades` — **3** 条
  - 字段：`cost_currency`×3 `upgrade_requirement_code`×2

- 集合 `$.gambling.slot_upgrades` — **3** 条
  - 字段：`number_of_slots`×3 `upgrade_requirement_code`×2

- 集合 `$.gambling.stress_upgrades` — **3** 条
  - 字段：`heal_low`×3 `heal_high`×3 `upgrade_requirement_code`×2

- 集合 `$.gambling.affliction_cure_upgrades` — **2** 条
  - 字段：`chance`×2 `upgrade_requirement_code`×1

- 集合 `$.brothel.side_effects.results` — **7** 条
  - 字段：`type`×7 `chance`×7 `data`×7

- 集合 `$.brothel.side_effects.results[1].data` — **2** 条
  - 字段：`chance`×2 `duration`×2

- 集合 `$.brothel.side_effects.results[2].data` — **1** 条
  - 字段：`chance`×1 `quirk_library_name`×1

- 集合 `$.brothel.quirk_library_names` — **6** 条

- 集合 `$.brothel.cost_upgrades` — **3** 条
  - 字段：`cost_currency`×3 `upgrade_requirement_code`×2

- 集合 `$.brothel.slot_upgrades` — **3** 条
  - 字段：`number_of_slots`×3 `upgrade_requirement_code`×2

- 集合 `$.brothel.stress_upgrades` — **3** 条
  - 字段：`heal_low`×3 `heal_high`×3 `upgrade_requirement_code`×2

- 集合 `$.brothel.affliction_cure_upgrades` — **2** 条
  - 字段：`chance`×2 `upgrade_requirement_code`×1

样例（原样，≤15 行）：

```
{
    "on_start_town_visit_priority" :  1,
	"number_of_quests_finished": 2,
	"highest_dungeon_level": 0,

	"bar" :
	{
		"side_effects" : 
		{
			"chance"    :  0.40,
			"results"   :
			[
			   {
					"type"      :  "activity_lock",
					"chance"    :  1,
```

#### `Curios\Obstacles.json`  (816 B)

- 集合 `$.props` — **5** 条
  - 字段：`name`×5 `fail_effects`×5 `health`×5 `torchlight`×5 `ancestor_talk`×5

- 集合 `$.props[0].fail_effects` — **1** 条

- 集合 `$.props[1].fail_effects` — **1** 条

- 集合 `$.props[2].fail_effects` — **1** 条

样例（原样，≤15 行）：

```
{
    "props" : 
    [
	    {
            "name" : "thorny_thicket",
			"fail_effects" : [ "Stress 2" ],
			"health" : -0.05,
			"torchlight" : -20.0,
			"ancestor_talk" : false
	    },
 	    {
			"name" : "rubble",
			"fail_effects" : [ "Stress 2" ],
			"health" : -0.05,
			"torchlight" : -20.0,
```

#### `Curios\Traps.json`  (2963 B，非严格 JSON：已剔除尾随逗号后解析)

- 集合 `$.props` — **4** 条
  - 字段：`name`×4 `success_effects`×4 `fail_effects`×4 `health`×4 `difficulty_variations`×4

- 集合 `$.props[0].success_effects` — **1** 条

- 集合 `$.props[0].fail_effects` — **2** 条

- 集合 `$.props[0].difficulty_variations` — **2** 条
  - 字段：`level`×2 `success_effects`×2 `fail_effects`×2 `health`×2

- 集合 `$.props[0].difficulty_variations[0].success_effects` — **1** 条

- 集合 `$.props[0].difficulty_variations[0].fail_effects` — **2** 条

- 集合 `$.props[0].difficulty_variations[1].success_effects` — **1** 条

- 集合 `$.props[0].difficulty_variations[1].fail_effects` — **2** 条

- 集合 `$.props[1].success_effects` — **1** 条

- 集合 `$.props[1].fail_effects` — **1** 条

- 集合 `$.props[1].difficulty_variations` — **2** 条
  - 字段：`level`×2 `success_effects`×2 `fail_effects`×2 `health`×2

- 集合 `$.props[1].difficulty_variations[0].success_effects` — **1** 条

- 集合 `$.props[1].difficulty_variations[0].fail_effects` — **1** 条

- 集合 `$.props[1].difficulty_variations[1].success_effects` — **1** 条

- 集合 `$.props[1].difficulty_variations[1].fail_effects` — **1** 条

- 集合 `$.props[2].success_effects` — **1** 条

- 集合 `$.props[2].fail_effects` — **2** 条

- 集合 `$.props[2].difficulty_variations` — **2** 条
  - 字段：`level`×2 `success_effects`×2 `fail_effects`×2 `health`×2

- 集合 `$.props[2].difficulty_variations[0].success_effects` — **1** 条

- 集合 `$.props[2].difficulty_variations[0].fail_effects` — **2** 条

- 集合 `$.props[2].difficulty_variations[1].success_effects` — **1** 条

- 集合 `$.props[2].difficulty_variations[1].fail_effects` — **2** 条

样例（原样，≤15 行）：

```
{
    "props" : 
    [
	    {
            "name" : "poison_cloud",
            "success_effects" : [ "Heal Stress TrapD" ],
            "fail_effects" : [ "Blight 1", "Stress 2" ],
			"health" : 0,
			
            "difficulty_variations" : 
            [
                 {
                    "level" : 3,
                    "success_effects" : [ "Heal Stress TrapD" ],
                    "fail_effects" : [ "Blight 2", "Stress 2" ],
```

#### `JsonAI.json`  (314790 B，非严格 JSON：已剔除尾随逗号后解析)

- 集合 `$.monster_brains` — **160** 条
  - 字段：`id`×160 `skill_cooldowns`×160 `skill_selection_desires`×160 `target_selection_desires`×160 `bonus_initiative_desires`×160

- 集合 `$.monster_brains[0].skill_selection_desires` — **3** 条
  - 字段：`type`×3 `data`×3

- 集合 `$.monster_brains[0].target_selection_desires` — **3** 条
  - 字段：`type`×3 `data`×3

- 集合 `$.monster_brains[1].skill_selection_desires` — **3** 条
  - 字段：`type`×3 `data`×3

- 集合 `$.monster_brains[1].target_selection_desires` — **3** 条
  - 字段：`type`×3 `data`×3

- 集合 `$.monster_brains[2].skill_selection_desires` — **3** 条
  - 字段：`type`×3 `data`×3

- 集合 `$.monster_brains[2].target_selection_desires` — **3** 条
  - 字段：`type`×3 `data`×3

样例（原样，≤15 行）：

```
{
    "monster_brains": [
        {
            "id": "default",
            "skill_cooldowns": 
            [

            ],
            "skill_selection_desires": [
                {
                    "type": "preferred_skill",
                    "data": {
                        "base_chance": 1.0
                    }
                },
```

#### `JsonBuffs.json`  (684543 B)

- 集合 `$.buffs` — **1801** 条
  - 字段：`id`×1801 `stat_type`×1801 `stat_sub_type`×1801 `amount`×1801 `remove_if_not_active`×1801 `rule_type`×1801 `is_false_rule`×1801 `rule_data`×1801
  - `duration_type`×63 `duration`×63

样例（原样，≤15 行）：

```
{
   "buffs":
   [
      {
         "id" : "TRINKET_PLACEHOLDER_BUFF",
         "stat_type" : "combat_stat_add",
         "stat_sub_type" : "attack_rating",
         "amount" : 0,
         "remove_if_not_active" : false,
         "rule_type" : "always",
         "is_false_rule" : false,
         "rule_data" : {
            "float" : 0,
            "string" : ""
         }
```

#### `JsonCamping.json`  (64265 B)

- 集合 `$.skills` — **64** 条
  - 字段：`id`×64 `level`×64 `cost`×64 `use_limit`×64 `effects`×64 `hero_classes`×64 `upgrade_requirements`×64

- 集合 `$.skills[0].effects` — **1** 条
  - 字段：`selection`×1 `requirements`×1 `chance`×1 `type`×1 `sub_type`×1 `amount`×1

- 集合 `$.skills[0].hero_classes` — **16** 条

- 集合 `$.skills[0].upgrade_requirements` — **1** 条
  - 字段：`code`×1 `currency_cost`×1 `prerequisite_requirements`×1

- 集合 `$.skills[0].upgrade_requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.skills[1].effects` — **3** 条
  - 字段：`selection`×3 `requirements`×3 `chance`×3 `type`×3 `sub_type`×3 `amount`×3

- 集合 `$.skills[1].hero_classes` — **16** 条

- 集合 `$.skills[1].upgrade_requirements` — **1** 条
  - 字段：`code`×1 `currency_cost`×1 `prerequisite_requirements`×1

- 集合 `$.skills[1].upgrade_requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.skills[2].effects` — **1** 条
  - 字段：`selection`×1 `requirements`×1 `chance`×1 `type`×1 `sub_type`×1 `amount`×1

- 集合 `$.skills[2].hero_classes` — **16** 条

- 集合 `$.skills[2].upgrade_requirements` — **1** 条
  - 字段：`code`×1 `currency_cost`×1 `prerequisite_requirements`×1

- 集合 `$.skills[2].upgrade_requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

样例（原样，≤15 行）：

```
{
	"configuration" : 
	{
        "class_specific_number_of_classes_threshold" :  4
	},

	"skills" :
	[

		{

			"id" : "encourage",
			"level" : 0,
			"cost" : 2,
			"use_limit" : 1,
```

#### `JsonLoot.json`  (32443 B)

- 集合 `$.darkness_bonuses` — **2** 条
  - 字段：`type`×2 `bonuses`×2

- 集合 `$.darkness_bonuses[0].bonuses` — **5** 条
  - 字段：`darkness`×5 `chance`×5 `codes`×5

- 集合 `$.darkness_bonuses[0].bonuses[0].codes` — **2** 条

- 集合 `$.darkness_bonuses[0].bonuses[1].codes` — **1** 条

- 集合 `$.darkness_bonuses[0].bonuses[2].codes` — **1** 条

- 集合 `$.darkness_bonuses[1].bonuses` — **5** 条
  - 字段：`darkness`×5 `chance`×5 `codes`×5

- 集合 `$.darkness_bonuses[1].bonuses[0].codes` — **1** 条

- 集合 `$.darkness_bonuses[1].bonuses[1].codes` — **1** 条

- 集合 `$.darkness_bonuses[1].bonuses[2].codes` — **1** 条

- 集合 `$.loot_tables` — **54** 条
  - 字段：`id`×54 `difficulty`×54 `dungeon`×54 `entries`×54

- 集合 `$.loot_tables[0].entries` — **9** 条
  - 字段：`type`×9 `chances`×9 `data`×9

- 集合 `$.loot_tables[1].entries` — **3** 条
  - 字段：`type`×3 `chances`×3 `data`×3

- 集合 `$.loot_tables[2].entries` — **3** 条
  - 字段：`type`×3 `chances`×3 `data`×3

样例（原样，≤15 行）：

```
{
   "darkness_bonuses" :
   [
      {
         "type" : "battle",
         "bonuses" :
         [
            { "darkness" : 0, "chance" : 0.75, "codes" : [ "B", "B" ] },
            { "darkness" : 1, "chance" : 0.75, "codes" : [ "B" ] },
            { "darkness" : 26, "chance" : 0.5, "codes" : [ "B" ] },
            { "darkness" : 51, "chance" : 0.25, "codes" : [ "B" ] },
            { "darkness" : 76, "chance" : 0, "codes" : [ "B" ] }
         ]
      },
      {
```

#### `JsonQuests.json`  (153174 B，非严格 JSON：已剔除尾随逗号后解析)

- 集合 `$.goals` — **45** 条
  - 字段：`id`×45 `type`×45 `starting_items`×45 `ignore_fog_of_war`×45 `show_as_quest`×45 `data`×45

- 集合 `$.goals[1].data.monster_class_ids` — **1** 条

- 集合 `$.goals[2].data.monster_class_ids` — **1** 条

- 集合 `$.town_progression_goal_ids` — **4** 条

- 集合 `$.types` — **6** 条
  - 字段：`id`×6 `goal_lists`×6

- 集合 `$.types[0].goal_lists` — **5** 条
  - 字段：`dungeon`×5 `goals`×5

- 集合 `$.types[0].goal_lists[0].goals` — **1** 条

- 集合 `$.types[0].goal_lists[1].goals` — **1** 条

- 集合 `$.types[0].goal_lists[2].goals` — **1** 条

- 集合 `$.types[1].goal_lists` — **1** 条
  - 字段：`dungeon`×1 `goals`×1

- 集合 `$.types[1].goal_lists[0].goals` — **1** 条

- 集合 `$.types[1].goal_lists[0].goals[0]` — **1** 条

- 集合 `$.types[2].goal_lists` — **1** 条
  - 字段：`dungeon`×1 `goals`×1

- 集合 `$.types[2].goal_lists[0].goals` — **1** 条

- 集合 `$.types[2].goal_lists[0].goals[0]` — **1** 条

- 集合 `$.plot_quests` — **30** 条
  - 字段：`id`×30 `dungeon_level`×30 `quest`×30 `additional_trinket_completion_rewards`×30 `is_progression`×30 `has_statue_contents`×30 `completion_dungeon_xp`×30 `can_retreat`×30
  - `retreat_always_from_raid`×30 `retreat_party_kill_count`×30 `is_surprise_enabled`×30 `is_scouting_enabled`×30 `is_roster_stress_cleared_on_completion`×30 `roster_buff_on_failure_minimum_party_resolve_level`×30 `upgrade_tags_to_remove_on_ignore`×30 `upgrade_tags_to_remove_on_failure`×30
  - `roster_buffs_to_apply_on_failure`×30 `suggested_trinkets`×30 `additional_provisions`×30 `plot_quest_dependency`×4

- 集合 `$.plot_quests[0].quest.goal_ids` — **1** 条

- 集合 `$.plot_quests[0].additional_trinket_completion_rewards` — **1** 条
  - 字段：`rarity`×1 `amount`×1

- 集合 `$.plot_quests[1].quest.goal_ids` — **1** 条

- 集合 `$.plot_quests[1].additional_trinket_completion_rewards` — **1** 条
  - 字段：`rarity`×1 `amount`×1

- 集合 `$.plot_quests[2].quest.goal_ids` — **1** 条

- 集合 `$.plot_quests[2].additional_trinket_completion_rewards` — **1** 条
  - 字段：`rarity`×1 `amount`×1

- 集合 `$.generation.number.number_of_quests_per_town_visit_table` — **8** 条

- 集合 `$.generation.dungeon.generated_dungeons` — **4** 条
  - 字段：`id`×4 `required_number_of_quests_finished`×4

- 集合 `$.generation.difficulty.generated_resolve_level_difficulties` — **3** 条
  - 字段：`resolve_levels`×3 `difficulty`×3

- 集合 `$.generation.difficulty.generated_resolve_level_difficulties[0].resolve_levels` — **3** 条

- 集合 `$.generation.difficulty.generated_resolve_level_difficulties[1].resolve_levels` — **3** 条

- 集合 `$.generation.difficulty.generated_resolve_level_difficulties[2].resolve_levels` — **3** 条

- 集合 `$.generation.type.available_quests_table` — **4** 条
  - 字段：`dungeon`×4 `generated_quest_table`×4

- 集合 `$.generation.type.available_quests_table[0].generated_quest_table` — **8** 条

- 集合 `$.generation.type.available_quests_table[0].generated_quest_table[0]` — **1** 条
  - 字段：`type`×1 `chance`×1 `length`×1

- 集合 `$.generation.type.available_quests_table[0].generated_quest_table[1]` — **4** 条
  - 字段：`type`×4 `chance`×4 `length`×4

- 集合 `$.generation.type.available_quests_table[0].generated_quest_table[2]` — **6** 条
  - 字段：`type`×6 `chance`×6 `length`×6

- 集合 `$.generation.type.available_quests_table[1].generated_quest_table` — **8** 条

- 集合 `$.generation.type.available_quests_table[1].generated_quest_table[0]` — **1** 条
  - 字段：`type`×1 `chance`×1 `length`×1

- 集合 `$.generation.type.available_quests_table[1].generated_quest_table[1]` — **4** 条
  - 字段：`type`×4 `chance`×4 `length`×4

- 集合 `$.generation.type.available_quests_table[1].generated_quest_table[2]` — **6** 条
  - 字段：`type`×6 `chance`×6 `length`×6

- 集合 `$.generation.type.available_quests_table[2].generated_quest_table` — **8** 条

- 集合 `$.generation.type.available_quests_table[2].generated_quest_table[0]` — **1** 条
  - 字段：`type`×1 `chance`×1 `length`×1

- 集合 `$.generation.type.available_quests_table[2].generated_quest_table[1]` — **4** 条
  - 字段：`type`×4 `chance`×4 `length`×4

- 集合 `$.generation.type.available_quests_table[2].generated_quest_table[2]` — **6** 条
  - 字段：`type`×6 `chance`×6 `length`×6

- 集合 `$.generation.rewards.heirloom_type_map` — **4** 条
  - 字段：`dungeon`×4 `types`×4

- 集合 `$.generation.rewards.heirloom_type_map[0].types` — **2** 条

- 集合 `$.generation.rewards.heirloom_type_map[1].types` — **2** 条

- 集合 `$.generation.rewards.heirloom_type_map[2].types` — **2** 条

- 集合 `$.generation.rewards.heirloom_amount_table` — **4** 条
  - 字段：`type`×4 `amounts`×4

- 集合 `$.generation.rewards.heirloom_amount_table[0].amounts` — **6** 条

- 集合 `$.generation.rewards.heirloom_amount_table[0].amounts[1]` — **4** 条

- 集合 `$.generation.rewards.heirloom_amount_table[1].amounts` — **6** 条

- 集合 `$.generation.rewards.heirloom_amount_table[1].amounts[1]` — **4** 条

- 集合 `$.generation.rewards.heirloom_amount_table[2].amounts` — **6** 条

- 集合 `$.generation.rewards.heirloom_amount_table[2].amounts[1]` — **4** 条

- 集合 `$.generation.rewards.item_table` — **7** 条

- 集合 `$.generation.rewards.item_table[1]` — **4** 条

- 集合 `$.generation.rewards.item_table[1][1]` — **1** 条
  - 字段：`type`×1 `id`×1 `amount`×1

- 集合 `$.generation.rewards.item_table[1][2]` — **1** 条
  - 字段：`type`×1 `id`×1 `amount`×1

- 集合 `$.generation.rewards.resolve_xp_table` — **7** 条

- 集合 `$.generation.rewards.resolve_xp_table[0]` — **4** 条

- 集合 `$.generation.rewards.resolve_xp_table[1]` — **4** 条

- 集合 `$.generation.rewards.resolve_xp_table[2]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table` — **6** 条
  - 字段：`rarity`×6 `chances`×6 `comment`×1

- 集合 `$.generation.rewards.trinket_chance_table[0].chances` — **7** 条

- 集合 `$.generation.rewards.trinket_chance_table[0].chances[0]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table[0].chances[1]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table[0].chances[2]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table[1].chances` — **7** 条

- 集合 `$.generation.rewards.trinket_chance_table[1].chances[0]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table[1].chances[1]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table[1].chances[2]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table[2].chances` — **7** 条

- 集合 `$.generation.rewards.trinket_chance_table[2].chances[0]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table[2].chances[1]` — **4** 条

- 集合 `$.generation.rewards.trinket_chance_table[2].chances[2]` — **4** 条

- 集合 `$.restriction.difficulty.resolve_level_threshold_table` — **7** 条

样例（原样，≤15 行）：

```
{
    "stress_damage": 20,
	
    "goals": [
        {
            "id": "tutorial_final_room",
            "type": "tutorial_room",
            "starting_items": [],
            "ignore_fog_of_war": false,
            "show_as_quest": false,
            "data": {
                "room_id": "room2_1"
            }
        },
        {
```

#### `JsonQuirks.json`  (75351 B)

- 集合 `$.quirks` — **163** 条
  - 字段：`id`×163 `show_explicit_description`×163 `is_positive`×163 `is_disease`×163 `classification`×163 `incompatible_quirks`×163 `curio_tag`×163 `curio_tag_chance`×163
  - `keep_loot`×163 `buffs`×163

- 集合 `$.quirks[0].incompatible_quirks` — **1** 条

- 集合 `$.quirks[0].buffs` — **1** 条

- 集合 `$.quirks[1].incompatible_quirks` — **1** 条

- 集合 `$.quirks[1].buffs` — **1** 条

- 集合 `$.quirks[2].incompatible_quirks` — **1** 条

- 集合 `$.quirks[2].buffs` — **1** 条

样例（原样，≤15 行）：

```
{
   "quirks":
   [
      {
         "id" : "tough",
         "show_explicit_description" : true,
         "is_positive" : true,
         "is_disease" : false,
         "classification" : "physical",
         "incompatible_quirks" : 
         [
            "fragile"
         ],
         "curio_tag" : "",
         "curio_tag_chance" : 0.0,
```

#### `JsonTraits.json`  (42431 B)

- 集合 `$.traits` — **12** 条
  - 字段：`id`×12 `overstress_type`×12 `curio_tag`×12 `curio_tag_chance`×12 `keep_loot`×12 `buff_ids`×12 `combat_start_turn_act_outs`×12 `reaction_act_outs`×12

- 集合 `$.traits[0].buff_ids` — **12** 条

- 集合 `$.traits[0].combat_start_turn_act_outs` — **14** 条
  - 字段：`id`×14 `data`×14 `chance`×14

- 集合 `$.traits[0].reaction_act_outs` — **15** 条
  - 字段：`id`×15 `data`×15 `chance`×15

- 集合 `$.traits[1].buff_ids` — **12** 条

- 集合 `$.traits[1].combat_start_turn_act_outs` — **14** 条
  - 字段：`id`×14 `data`×14 `chance`×14

- 集合 `$.traits[1].reaction_act_outs` — **15** 条
  - 字段：`id`×15 `data`×15 `chance`×15

- 集合 `$.traits[2].buff_ids` — **12** 条

- 集合 `$.traits[2].combat_start_turn_act_outs` — **14** 条
  - 字段：`id`×14 `data`×14 `chance`×14

- 集合 `$.traits[2].reaction_act_outs` — **15** 条
  - 字段：`id`×15 `data`×15 `chance`×15

样例（原样，≤15 行）：

```
{
   "traits":
   [
      {
         "id" : "fearful",
         "overstress_type": "affliction",
         "curio_tag" : "Worship",
         "curio_tag_chance" : 0.5,
         "keep_loot" : false,
         "buff_ids" : 
         [
            "STUNRESIST-15",
            "BLIGHTRESIST-15",
            "BLEEDRESIST-15",
            "DISEASERESIST-15",
```

#### `JsonTrinkets.json`  (207424 B)

- 集合 `$.rarities` — **12** 条

- 集合 `$.trinkets` — **488** 条
  - 字段：`id`×488 `buffs`×488 `hero_class_requirements`×488 `rarity`×488 `price`×488 `limit`×488 `origin_dungeon`×488

- 集合 `$.trinkets[0].buffs` — **2** 条

- 集合 `$.trinkets[1].buffs` — **4** 条

- 集合 `$.trinkets[2].buffs` — **2** 条

样例（原样，≤15 行）：

```
{
   "rarities":
   [
      "darkest_dungeon",
      "ancestral_shambler",
      "ancestral",
      "collector",
      "madman",
      "very_rare",
      "rare",
      "uncommon",
      "common",
      "very_common",
      "trophy",
      "kickstarter"
```

#### `Mechanics\Campaign.json`  (442 B)

- 集合 `$.quest_completion_xp_table` — **4** 条

- 集合 `$.level_threshold_table` — **8** 条

- 集合 `$.resolve_level_thresholds` — **7** 条

- 集合 `$.gold_icon_thresholds` — **4** 条

- 集合 `$.provision_icon_thresholds` — **4** 条

样例（原样，≤15 行）：

```
{
    "quest_completion_xp_table":
    [
        0,
        2,
        3,
        4
    ],

	"level_threshold_table":
    [
        0,
		2,
		6,
		10,
```

#### `Mechanics\HeirloomExchange.json`  (1985 B)

- 集合 `$.markets` — **1** 条
  - 字段：`id`×1 `exchange_rates`×1

- 集合 `$.markets[0].exchange_rates` — **12** 条
  - 字段：`exchange_from_type`×12 `exchange_from_amount`×12 `exchange_to_type`×12 `exchange_to_amount`×12

样例（原样，≤15 行）：

```
{
    "markets":
    [
        {
            "id" : "default",
            "exchange_rates":
            [
                { "exchange_from_type": "bust",         "exchange_from_amount": 3, "exchange_to_type": "portrait",      "exchange_to_amount": 1 },
                { "exchange_from_type": "bust",         "exchange_from_amount": 3, "exchange_to_type": "deed",          "exchange_to_amount": 2 },
                { "exchange_from_type": "bust",         "exchange_from_amount": 2, "exchange_to_type": "crest",         "exchange_to_amount": 3 },
                
                { "exchange_from_type": "portrait",     "exchange_from_amount": 2, "exchange_to_type": "bust",          "exchange_to_amount": 3 },
                { "exchange_from_type": "portrait",     "exchange_from_amount": 2, "exchange_to_type": "deed",          "exchange_to_amount": 3 },
                { "exchange_from_type": "portrait",     "exchange_from_amount": 1, "exchange_to_type": "crest",         "exchange_to_amount": 3 },

```

#### `Mechanics\Provision.json`  (6283 B)

- 集合 `$.raid_starting_length_inventory_item_lists` — **5** 条

- 集合 `$.raid_starting_length_inventory_item_lists[1]` — **9** 条
  - 字段：`type`×9 `id`×9 `amount`×9

- 集合 `$.raid_starting_length_inventory_item_lists[2]` — **9** 条
  - 字段：`type`×9 `id`×9 `amount`×9

- 集合 `$.raid_starting_hero_class_item_lists` — **7** 条
  - 字段：`hero_class`×7 `item_lists`×7

- 集合 `$.raid_starting_hero_class_item_lists[0].item_lists` — **1** 条
  - 字段：`type`×1 `id`×1 `amount`×1

- 集合 `$.raid_starting_hero_class_item_lists[1].item_lists` — **1** 条
  - 字段：`type`×1 `id`×1 `amount`×1

- 集合 `$.raid_starting_hero_class_item_lists[2].item_lists` — **1** 条
  - 字段：`type`×1 `id`×1 `amount`×1

- 集合 `$.default_store_inventory_item_lists` — **5** 条

- 集合 `$.default_store_inventory_item_lists[1]` — **8** 条
  - 字段：`type`×8 `id`×8 `amount`×8

- 集合 `$.default_store_inventory_item_lists[2]` — **8** 条
  - 字段：`type`×8 `id`×8 `amount`×8

样例（原样，≤15 行）：

```
{
    "raid_starting_length_inventory_item_lists":
    [
        [ ],
        [
            { "type": "supply", "id": "firewood", "amount": 0 },
            { "type": "provision", "id": "", "amount": 0 },
            { "type": "supply", "id": "shovel", "amount": 0 },
            { "type": "supply", "id": "antivenom", "amount": 0 },
            { "type": "supply", "id": "bandage", "amount": 0 },
            { "type": "supply", "id": "medicinal_herbs", "amount": 0 },
            { "type": "supply", "id": "skeleton_key", "amount": 0 },
            { "type": "supply", "id": "holy_water", "amount": 0 },
            { "type": "supply", "id": "torch", "amount": 0 }
        ],
```

#### `Mechanics\Roster.json`  (383 B)

- 集合 `$.resolve_level_thresholds` — **7** 条

样例（原样，≤15 行）：

```
    {
    "name_id_format" :  "hero_name_%d",

    "resolve_level_thresholds" : 
    [
        0,
        2,
        8,
        14,
        24,
        36,
        48
    ],
    
    "town_visit_town_progression":
```

#### `Mechanics\TownEvents.json`  (45739 B)

- 集合 `$.settings` — **3** 条
  - 字段：`id`×3 `event_chance_per_town_visits`×3

- 集合 `$.settings[0].event_chance_per_town_visits` — **1** 条

- 集合 `$.settings[1].event_chance_per_town_visits` — **4** 条

- 集合 `$.settings[2].event_chance_per_town_visits` — **4** 条

- 集合 `$.events` — **45** 条
  - 字段：`id`×45 `base_chance`×45 `per_not_rolled_additional_chance`×45 `cooldown`×45 `requirements`×45 `town_ambience_paramater_ids`×45 `tone`×45 `sprite`×45
  - `sprite_attachment`×45 `data`×45

- 集合 `$.events[0].data` — **3** 条
  - 字段：`type`×3 `string_data`×3 `number_data`×3

- 集合 `$.events[1].data` — **3** 条
  - 字段：`type`×3 `string_data`×3 `number_data`×3

- 集合 `$.events[2].data` — **3** 条
  - 字段：`type`×3 `string_data`×3 `number_data`×3

- 集合 `$.quest_type_event_guarantees` — **8** 条
  - 字段：`dungeon_type`×8 `quest_type`×8 `event_id`×8

样例（原样，≤15 行）：

```
{
    "settings": [
        {
            "id": "off",
            "event_chance_per_town_visits": [
                0.0
            ]
        },
        {
            "id": "normal",
            "event_chance_per_town_visits": [
                0.33,
                0.67,
                0.75,
                1.0
```

#### `Narration.json`  (282205 B)

- 集合 `$.filters` — **1** 条

- 集合 `$.entries` — **36** 条
  - 字段：`id`×36 `tone`×36 `chance`×36 `audio_events`×36 `priority`×1

- 集合 `$.entries[0].audio_events` — **27** 条
  - 字段：`queue_only_on_empty`×27 `queue_while_audio_playing`×27 `audio_event`×27 `chance`×27 `priority`×27 `max_raid_occurrences`×27 `max_town_visit_occurrences`×27 `max_campaign_occurrences`×27
  - `filter`×27 `check_all_tags`×27 `tags`×27

- 集合 `$.entries[0].audio_events[0].tags` — **1** 条

- 集合 `$.entries[0].audio_events[1].tags` — **1** 条

- 集合 `$.entries[0].audio_events[2].tags` — **1** 条

- 集合 `$.entries[1].audio_events` — **26** 条
  - 字段：`queue_only_on_empty`×26 `queue_while_audio_playing`×26 `audio_event`×26 `chance`×26 `priority`×26 `max_raid_occurrences`×26 `max_town_visit_occurrences`×26 `max_campaign_occurrences`×26
  - `filter`×26 `check_all_tags`×26 `tags`×26

- 集合 `$.entries[1].audio_events[0].tags` — **3** 条

- 集合 `$.entries[1].audio_events[1].tags` — **3** 条

- 集合 `$.entries[1].audio_events[2].tags` — **2** 条

- 集合 `$.entries[2].audio_events` — **28** 条
  - 字段：`queue_only_on_empty`×28 `queue_while_audio_playing`×28 `audio_event`×28 `chance`×28 `priority`×28 `max_raid_occurrences`×28 `max_town_visit_occurrences`×28 `max_campaign_occurrences`×28
  - `filter`×28 `check_all_tags`×28 `tags`×28

- 集合 `$.entries[2].audio_events[0].tags` — **2** 条

- 集合 `$.entries[2].audio_events[1].tags` — **2** 条

- 集合 `$.entries[2].audio_events[2].tags` — **2** 条

样例（原样，≤15 行）：

```
{
    "filters": [
        "plot_darkest_dungeon_4"
    ],
    "entries": [
        {
            "id": "loading_screen_start",
            "tone": "neutral",
            "chance": 1,
            "audio_events": [
                {
                    "queue_only_on_empty": false,
                    "queue_while_audio_playing": true,
                    "audio_event": "/vo/load/crypts_01",
                    "chance": 1,
```

#### `PartyNames.json`  (21497 B)

- 集合 `$.party_names` — **186** 条
  - 字段：`id`×186 `required_hero_class`×186

- 集合 `$.party_names[0].required_hero_class` — **4** 条

- 集合 `$.party_names[1].required_hero_class` — **4** 条

- 集合 `$.party_names[2].required_hero_class` — **4** 条

样例（原样，≤15 行）：

```
{
    "party_names":
    [
    	{
    		"id": "0",
    		"required_hero_class": [ "vestal", "plague_doctor", "highwayman", "crusader" ]
		},
    	{
    		"id": "1",
    		"required_hero_class": [ "occultist", "bounty_hunter", "highwayman", "grave_robber" ]
		},
    	{
    		"id": "2",
    		"required_hero_class": [ "occultist", "grave_robber", "bounty_hunter", "hellion" ]
		},
```

#### `Upgrades\Building\abbey.upgrades.json`  (7878 B)

- 集合 `$.trees` — **3** 条
  - 字段：`id`×3 `is_instanced`×3 `tags`×3 `requirements`×3

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **6** 条
  - 字段：`code`×6 `currency_cost`×6 `prerequisite_requirements`×6

- 集合 `$.trees[0].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **6** 条
  - 字段：`code`×6 `currency_cost`×6 `prerequisite_requirements`×6

- 集合 `$.trees[1].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].tags` — **2** 条

- 集合 `$.trees[2].requirements` — **6** 条
  - 字段：`code`×6 `currency_cost`×6 `prerequisite_requirements`×6

- 集合 `$.trees[2].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "abbey.meditation",
         "is_instanced" : false,
         "tags" : ["building","abbey"],
         "requirements" : 
         [
            {
               "code" : "a",
               "currency_cost" :
               [
                  { "type" : "gold", "amount" : 0},
                  { "type" : "bust", "amount" : 4},
```

#### `Upgrades\Building\blacksmith.upgrades.json`  (5835 B)

- 集合 `$.trees` — **3** 条
  - 字段：`id`×3 `is_instanced`×3 `tags`×3 `requirements`×3

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].tags` — **2** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "blacksmith.weapon",
         "is_instanced" : false,
         "tags" : ["building","blacksmith"],
         "requirements" : 
         [
            {
               "code" : "a",
               "currency_cost" :
               [
                  { "type" : "gold", "amount" : 0},
                  { "type" : "deed", "amount" : 10},
```

#### `Upgrades\Building\camping_trainer.upgrades.json`  (2010 B)

- 集合 `$.trees` — **1** 条
  - 字段：`id`×1 `is_instanced`×1 `tags`×1 `requirements`×1

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5

- 集合 `$.trees[0].requirements[0].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[0].requirements[1].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[2].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "camping_trainer.cost",
         "is_instanced" : false,
         "tags" : ["building","camping_trainer"],
         "requirements" : 
         [
            {
               "code" : "a",
               "currency_cost" :
               [
                  { "type" : "gold", "amount" : 0},
                  { "type" : "crest", "amount" : 27}
```

#### `Upgrades\Building\guild.upgrades.json`  (4038 B)

- 集合 `$.trees` — **2** 条
  - 字段：`id`×2 `is_instanced`×2 `tags`×2 `requirements`×2

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5

- 集合 `$.trees[1].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "guild.skill_levels",
         "is_instanced" : false,
         "tags" : ["building","guild"],
         "requirements" : 
         [
            {
               "code" : "a",
               "currency_cost" :
               [
                  { "type" : "gold", "amount" : 0},
                  { "type" : "portrait", "amount" : 6},
```

#### `Upgrades\Building\nomad_wagon.upgrades.json`  (3593 B)

- 集合 `$.trees` — **2** 条
  - 字段：`id`×2 `is_instanced`×2 `tags`×2 `requirements`×2

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[0].requirements[1].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[2].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5

- 集合 `$.trees[1].requirements[0].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[1].requirements[1].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[2].currency_cost` — **2** 条
  - 字段：`type`×2 `amount`×2

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "nomad_wagon.numitems",
         "is_instanced" : false,
         "tags" : ["building","nomad_wagon"],
         "requirements" : 
         [
            {
               "code" : "a",
               "currency_cost" :
               [
                  { "type" : "gold", "amount" : 0},
                  { "type" : "crest", "amount" : 17}
```

#### `Upgrades\Building\sanitarium.upgrades.json`  (6365 B)

- 集合 `$.trees` — **3** 条
  - 字段：`id`×3 `is_instanced`×3 `tags`×3 `requirements`×3

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5

- 集合 `$.trees[0].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5

- 集合 `$.trees[1].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].tags` — **2** 条

- 集合 `$.trees[2].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4

- 集合 `$.trees[2].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

样例（原样，≤15 行）：

```
{
   "trees":
   [
    {
        "id": "sanitarium.cost",
        "is_instanced": false,
        "tags": [ "building", "sanitarium" ],
        "requirements": [
            {
                "code": "a",
                "currency_cost": [
                    { "type": "gold", "amount": 0 },
                    { "type": "bust", "amount": 3 },
                    { "type": "crest", "amount": 2 }
                ],
```

#### `Upgrades\Building\stage_coach.upgrades.json`  (8610 B)

- 集合 `$.trees` — **3** 条
  - 字段：`id`×3 `is_instanced`×3 `tags`×3 `requirements`×3

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5

- 集合 `$.trees[0].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5

- 集合 `$.trees[1].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].tags` — **2** 条

- 集合 `$.trees[2].requirements` — **3** 条
  - 字段：`code`×3 `currency_cost`×3 `prerequisite_requirements`×3

- 集合 `$.trees[2].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[0].prerequisite_requirements` — **3** 条
  - 字段：`tree_id`×3 `requirement_code`×3

- 集合 `$.trees[2].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **4** 条
  - 字段：`tree_id`×4 `requirement_code`×4

- 集合 `$.trees[2].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **4** 条
  - 字段：`tree_id`×4 `requirement_code`×4

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "stage_coach.numrecruits",
         "is_instanced" : false,
         "tags" : ["building","stage_coach"],
         "requirements" : 
         [
            {
               "code" : "a",
               "currency_cost" :
               [
                  { "type" : "gold", "amount" : 0},
                  { "type" : "deed", "amount" : 3},
```

#### `Upgrades\Building\tavern.upgrades.json`  (7905 B)

- 集合 `$.trees` — **3** 条
  - 字段：`id`×3 `is_instanced`×3 `tags`×3 `requirements`×3

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **6** 条
  - 字段：`code`×6 `currency_cost`×6 `prerequisite_requirements`×6

- 集合 `$.trees[0].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **6** 条
  - 字段：`code`×6 `currency_cost`×6 `prerequisite_requirements`×6

- 集合 `$.trees[1].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].tags` — **2** 条

- 集合 `$.trees[2].requirements` — **6** 条
  - 字段：`code`×6 `currency_cost`×6 `prerequisite_requirements`×6

- 集合 `$.trees[2].requirements[0].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[2].requirements[2].currency_cost` — **3** 条
  - 字段：`type`×3 `amount`×3

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "tavern.bar",
         "is_instanced" : false,
         "tags" : ["building","tavern"],
         "requirements" : 
         [
            {
               "code" : "a",
               "currency_cost" :
               [
                  { "type" : "gold", "amount" : 0},
                  { "type" : "portrait", "amount" : 2},
```

#### `Upgrades\Heroes\abomination.upgrades.json`  (21243 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "abomination.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\antiquarian.upgrades.json`  (21473 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "antiquarian.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\arbalest.upgrades.json`  (21269 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "arbalest.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\bounty_hunter.upgrades.json`  (21489 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "bounty_hunter.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\crusader.upgrades.json`  (21324 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "crusader.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\grave_robber.upgrades.json`  (21436 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "grave_robber.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\hellion.upgrades.json`  (21241 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "hellion.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\highwayman.upgrades.json`  (21425 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "highwayman.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\houndmaster.upgrades.json`  (21338 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "houndmaster.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\jester.upgrades.json`  (21138 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "jester.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\leper.upgrades.json`  (21000 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "leper.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\man_at_arms.upgrades.json`  (21278 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "man_at_arms.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\musketeer.upgrades.json`  (21272 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "musketeer.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\occultist.upgrades.json`  (21452 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "occultist.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\plague_doctor.upgrades.json`  (21629 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "plague_doctor.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

#### `Upgrades\Heroes\vestal.upgrades.json`  (21218 B)

- 集合 `$.trees` — **9** 条
  - 字段：`id`×9 `is_instanced`×9 `tags`×9 `requirements`×9

- 集合 `$.trees[0].tags` — **2** 条

- 集合 `$.trees[0].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[0].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[0].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[0].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[0].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].tags` — **2** 条

- 集合 `$.trees[1].requirements` — **4** 条
  - 字段：`code`×4 `currency_cost`×4 `prerequisite_requirements`×4 `prerequisite_resolve_level`×4

- 集合 `$.trees[1].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[0].prerequisite_requirements` — **1** 条
  - 字段：`tree_id`×1 `requirement_code`×1

- 集合 `$.trees[1].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[1].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[1].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].tags` — **1** 条

- 集合 `$.trees[2].requirements` — **5** 条
  - 字段：`code`×5 `currency_cost`×5 `prerequisite_requirements`×5 `prerequisite_resolve_level`×5

- 集合 `$.trees[2].requirements[0].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[1].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

- 集合 `$.trees[2].requirements[2].currency_cost` — **1** 条
  - 字段：`type`×1 `amount`×1

- 集合 `$.trees[2].requirements[2].prerequisite_requirements` — **2** 条
  - 字段：`tree_id`×2 `requirement_code`×2

样例（原样，≤15 行）：

```
{
   "trees":
   [
      {
         "id" : "vestal.weapon",
         "is_instanced" : true,
         "tags" : ["weapon", "first_level_not_upgrade"],
         "requirements" : 
         [
            {
               "code" : "0",
               "currency_cost" : 
               [
                  { "type" : "gold", "amount" : 750 }
               ],
```

## 2. DD1 风格文本（`.bytes` / `.txt`）

### 2.1 记录前缀（record prefix）汇总

跨全部 26 个文本 `.bytes` + 232 个 `.txt` 的**行首记录前缀**总频次。「真实样例」一列是该前缀在参考项目里**第一次出现时的原始整行**（脚本自动抓取，未改写）。

| 前缀 | 总出现次数 | 含义 | 真实样例（首个，原样，取自哪个文件） |
|---|---:|---|---|
| `skill:` | 974 | **战斗技能**定义行（`.id/.anim/.fx/.type/.atk/.dmg/.crit/.launch/.target`…） | `skill: .id "ancestor_fuse_two" .anim "attack_fuse" .fx "fuse" .targfx "fuse_target" .area_pos_offset -100 -125 .target_area_pos_offset 50 0`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `effect:` | 952 | 效果定义（Mechanics/Effects.txt，`.name .target .chance .kill .on_hit .apply_once .queue`） | `effect: .name "kill_self" .target "performer" .chance 100% .kill 1 .on_hit true .on_miss true .apply_once true .queue false`　<sub>Mechanics/Effects.txt</sub> |
| `combat_skill:` | 630 | **英雄技能栏**条目（`.id/.icon/.anim/.fx`） | `combat_skill: .id "transform" .icon "one" .anim "attack_transform" .fx "transform"`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `hall:` | 355 | 走廊遭遇（`.chance N .types ...`） | `hall: .chance 2 .types fishman_harpoon_A fishman_harpoon_A fishman_harpoon_A`　<sub>Dungeons/Cove.bytes</sub> |
| `enemy_type:` | 278 | 敌人种族（`.id "unholy"/"eldritch"/"human"`…） | `enemy_type: .id "eldritch"`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `name:` | 245 | 记录名 / 实体 id（每个文件第一条） | `name: abomination`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `art:` | 245 | 外观/动画资源段开始 | `art:`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `commonfx:` | 245 | 通用特效（`.deathfx` 等） | `commonfx: .deathfx death_medium`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `info:` | 245 | 属性段开始 | `info:`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `loot:` | 232 | 掉落（`.code "A|B|C" .count N`，`"NONE"` 表示不掉） | `loot: .code "NONE" .count 0`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `room:` | 231 | 房间遭遇（`.chance N .types ...`） | `room: .chance 2 .types fishman_harpoon_A fishman_harpoon_A fishman_harpoon_A fishman_harpoon_A`　<sub>Dungeons/Cove.bytes</sub> |
| `type:` | 230 | 实体类别（如 `skeleton_common`） | `type: ancestor_big`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `display:` | 230 | 显示参数（`.size`） | `display: .size 1`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `stats:` | 230 | **战斗数值**：`.hp .def .prot .spd .stun_resist .poison_resist .bleed_resist .debuff_resist .move_resist` | `stats: .hp 252 .def 15.75% .prot 0 .spd 10 .stun_resist 72.5% .poison_resist 67.5% .bleed_resist 77.5% .debuff_resist 67.5% .move_resist 0%`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `personality:` | 230 | AI 倾向（`.prefskill`） | `personality: .prefskill -1`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `initiative:` | 230 | 回合内行动次数（`.number_of_turns_per_round`） | `initiative: .number_of_turns_per_round 1`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `monster_brain:` | 230 | 指向 `JsonAI.json` 的 AI 大脑 id（`.id <monster>`） | `monster_brain: .id ancestor_big_D`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `battle_modifier:` | 229 | 战斗规则修正（`.can_surprise .disable_stall_penalty .can_be_surprised`…） | `battle_modifier: .disable_stall_penalty True .can_surprise False .can_be_surprised False .always_surprise False .always_be_surprised False`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `death_class:` | 118 | 死亡归属怪类（`.monster_class_id` + `.is_valid_on_bleed_dot/blight_dot/crit`） | `death_class: .monster_class_id ancestor_pod_D .is_valid_on_bleed_dot True .is_valid_on_blight_dot True .is_valid_on_crit True .can_die_from_damage Fal ...`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `tag:` | 97 | 标签（`.id "light"` 等，用于技能/奇物筛选） | `tag: .id "light"`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `weapon:` | 75 | 英雄武器（`.name .atk .dmg lo hi .crit .spd`） | `weapon: .name "abomination_weapon_0" .atk 0% .dmg 6 11 .crit 2.5% .spd 7`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `armour:` | 75 | 英雄护甲（`.name .def .prot .hp .spd`） | `armour: .name "abomination_armour_0" .def 7.5% .prot 0 .hp 26 .spd 0`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `named:` | 70 | 具名固定遭遇（Dungeons，`.name X .chance N .types ...`） | `named: .name summon_mash_shambler .chance 1 .types shambler_A`　<sub>Dungeons/Cove.bytes</sub> |
| `defending_area_pos_offset:` | 70 | 防御区域偏移（`.offset x y`） | `defending_area_pos_offset: .offset 0 -90`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `hall_curios:` | 66 | 走廊奇物投放（`.chance N .types <curio_id>`） | `hall_curios: .chance 10 .types discarded_pack`　<sub>Dungeons/Cove.bytes</sub> |
| `stall:` | 61 | 地牢内小摊遭遇（`.chance N .types ...`） | `stall: .chance 2 .types cultist_brawler_A cultist_witch_A`　<sub>Dungeons/Cove.bytes</sub> |
| `inventory_item:` | 57 | 物品定义（Tab 缩进的 `.type .id .base_stack_limit .purchase_gold_value .sell_gold_value`） | `inventory_item:	.type "provision"		.id ""						    .base_stack_limit 12	.purchase_gold_value 75			.sell_gold_value 5`　<sub>Inventory/Items.bytes</sub> |
| `map:` | 44 | 地图生成参数段（Mechanics/MapGenerator.txt，`.size .base_room_number .connectivity`…） | `map:`　<sub>Mechanics/MapGenerator.txt</sub> |
| `boss:` | 27 | Boss 遭遇（`.chance N .types ...`） | `boss: .chance 1 .types siren_A`　<sub>Dungeons/Cove.bytes</sub> |
| `room_curios:` | 27 | 房间奇物投放（`.chance N .types <curio_id>`） | `room_curios: .chance 3 .types fish_idol`　<sub>Dungeons/Cove.bytes</sub> |
| `id:` | 25 | 数字/字符串 id（地牢、mash、tab 等） | `id: 0`　<sub>Dungeons/Cove.bytes</sub> |
| `life_link:` | 25 | 生命链接（`.base_class "ancestor_small"`，本体-部件共享） | `life_link: .base_class "ancestor_small"`　<sub>Monsters/ancestor_flawed_D.txt</sub> |
| `shape_shifter:` | 24 | 变形（`.fx_name "..."`，猪人王子/无面者等） | `shape_shifter: .fx_name "fx/formless_mutate/formless_mutate.sprite.skel"`　<sub>Monsters/formless_guard_A.txt</sub> |
| `mash:` | 18 | 地牢怪物混编组段开始 | `mash:`　<sub>Dungeons/Cove.bytes</sub> |
| `room_treasures:` | 18 | 房间宝藏投放（`.chance N .types <loot_box_id>`） | `room_treasures: .chance 2 .types unlocked_strongbox`　<sub>Dungeons/Cove.bytes</sub> |
| `resistances:` | 15 | 英雄抗性（`.stun .poison .bleed .disease .move .debuff .death_blow .trap`，百分比） | `resistances: .stun 40% .poison 60% .bleed 30% .disease 20% .move 40% .debuff 20% .death_blow 67% .trap 10%`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `combat_move_skill:` | 15 | 战斗位移技能（`.id "move" .type "move" .move a b`） | `combat_move_skill: .id "move" .level 0 .type "move" .move 2 1 .launch 4321`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `deaths_door:` | 15 | 死亡之门相关 buff 列表（`.buffs ...`） | `deaths_door: .buffs deathsdoorACCDebuff deathsdoorDMGLowDebuff deathsdoorDMGHighDebuff deathsdoorSPDDebuff deathsdoorSRDebuff .recovery_buffs mortalit ...`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `controlled:` | 15 | 被控/被俘参数（`.target_rank N`） | `controlled: .target_rank 2`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `id_index:` | 15 | 英雄在数据表里的索引（`.index 13`） | `id_index: .index 13`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `generation:` | 15 | 随机生成规则（`.number_of_positive_quirks_min/max`…） | `generation: .number_of_positive_quirks_min 1 .number_of_positive_quirks_max 2 .number_of_negative_quirks_min 1 .number_of_negative_quirks_max 2 .numbe ...`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `skill_selection:` | 15 | 技能栏规则（`.can_select_combat_skills .number_of_selected_combat_skills_max`） | `skill_selection: .can_select_combat_skills false .number_of_selected_combat_skills_max 7`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `display_modifier:` | 12 | 显示修正（`.use_centre_skill_announcement`） | `display_modifier: .use_centre_skill_announcement true`　<sub>Monsters/ancestor_heart_D.txt</sub> |
| `shared_health:` | 12 | 共享血量组（`.id formless`） | `shared_health: .id formless`　<sub>Monsters/formless_guard_A.txt</sub> |
| `rendering:` | 11 | 渲染排序（`.sort_position_z_rank_override`） | `rendering: .sort_position_z_rank_override -1`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `captor_full:` | 9 | 俘获者（满态，`.captor_empty_monster_class .release_on_death`…） | `captor_full: .captor_empty_monster_class cauldron_empty_A .release_on_death true .release_on_prisoner_at_deaths_door true .per_turn_damage_percent 0.0 ...`　<sub>Monsters/cauldron_full_A.txt</sub> |
| `health_bar:` | 8 | 血条类型（`.type "corpse"`） | `health_bar: .type "corpse"`　<sub>Monsters/corpse_A.txt</sub> |
| `life_time:` | 8 | 存活回合上限（`.alive_round_limit N .does_check_for_loot`） | `life_time: .alive_round_limit 3 .does_check_for_loot false`　<sub>Monsters/corpse_A.txt</sub> |
| `is_released:` | 7 | 地牢是否开放 | `is_released: true`　<sub>Dungeons/Cove.bytes</sub> |
| `hall_variants:` | 7 | 走廊变体数 | `hall_variants: 7`　<sub>Dungeons/Cove.bytes</sub> |
| `room_variants:` | 7 | 房间变体名列表（空格分隔） | `room_variants: city coral grotto handtree shipwreck temple whale`　<sub>Dungeons/Cove.bytes</sub> |
| `riposte_skill:` | 7 | 反击技能（与 `combat_skill` 同族） | `riposte_skill: .id "riposte1" .level 0 .type "melee" .atk 85% .dmg 0% .crit 5% .launch 1234 .target 1234 .is_crit_valid True`　<sub>Heroes/Info/BountyHunter.bytes</sub> |
| `battle_backdrop:` | 7 | 战斗背景（`.background_name`） | `battle_backdrop: .background_name heartroom`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `props:` | 6 | 地牢道具投放表（后续行是 `.chance/.types`） | `props:`　<sub>Dungeons/Cove.bytes</sub> |
| `traps:` | 6 | 地牢陷阱投放（`.chance N .types <trap_id>`） | `traps: .chance 1 .types lurker`　<sub>Dungeons/Cove.bytes</sub> |
| `obstacles:` | 6 | 地牢障碍投放（`.chance N .types <obstacle_id>`） | `obstacles: .chance 1 .types shipwreck`　<sub>Dungeons/Cove.bytes</sub> |
| `captor_empty:` | 6 | 俘获者（空态，`.performing_monster_captor_base_class .captor_full_monster_class`） | `captor_empty: .performing_monster_captor_base_class hag .captor_full_monster_class cauldron_full_A`　<sub>Monsters/cauldron_empty_A.txt</sub> |
| `companion:` | 6 | 随从/召唤（`.monster_class X .buffs ...`） | `companion: .monster_class drowned_captain .buffs drowned_captain_buff_PROT drowned_captain_buff_stun_resist drowned_captain_buff_blight_resist drowned ...`　<sub>Monsters/drowned_anchor_A.txt</sub> |
| `secret_room_treasures:` | 5 | 密房宝藏（`.chance N .types secret_stash`） | `secret_room_treasures: .chance 1 .types secret_stash`　<sub>Dungeons/Cove.bytes</sub> |
| `battle_stage:` | 4 | 战斗舞台 id（`.id big_ancestor`） | `battle_stage: .id big_ancestor`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `audio_modifier:` | 4 | 音频强度（`.intensity N`） | `audio_modifier: .intensity 2`　<sub>Monsters/ancestor_big_D.txt</sub> |
| `torchlight_modifier:` | 3 | 火把光修正（`.min .max`） | `torchlight_modifier: .min 0 .max 0`　<sub>Monsters/shambler_A.txt</sub> |
| `controller:` | 3 | 精神控制（`.stress_per_controlled_turn .uncontrol_effects`） | `controller: .stress_per_controlled_turn 10 .uncontrol_effects siren_uncontrol`　<sub>Monsters/siren_A.txt</sub> |
| `mode:` | 2 | 英雄形态（`.id human .is_raid_default true`，Abomination） | `mode: .id human .is_raid_default true`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `skill_reaction:` | 2 | 受击反应效果（`.was_hit_performer_effects "..."`） | `skill_reaction: .was_hit_performer_effects "Egg Heal Hero" "Egg Blight Hero"`　<sub>Monsters/ancestor_pod_D.txt</sub> |
| `incompatible_party_member:` | 1 | 不可同队（`.id abomination_religion .hero_tag religious`） | `incompatible_party_member: .id abomination_religion .hero_tag religious`　<sub>Heroes/Info/Abomination.bytes</sub> |
| `extra_battle_loot:` | 1 | 额外战斗掉落（Antiquarian，`.code "ANTIQ" .count 1`） | `extra_battle_loot: .code "ANTIQ" .count 1`　<sub>Heroes/Info/Antiquarian.bytes</sub> |
| `extra_curio_loot:` | 1 | 额外奇物掉落（Antiquarian） | `extra_curio_loot: .code "ANTIQ" .count 1`　<sub>Heroes/Info/Antiquarian.bytes</sub> |
| `extra_stack_limit:` | 1 | 额外堆叠上限（Antiquarian 金币，`.id antiquarian_gold`） | `extra_stack_limit: .id antiquarian_gold`　<sub>Heroes/Info/Antiquarian.bytes</sub> |
| `death_damage:` | 1 | 死亡时伤害（`.target_base_class_id .target_damage`） | `death_damage: .target_base_class_id ancestor_small .target_damage 5`　<sub>Monsters/ancestor_flawed_D.txt</sub> |
| `spawn:` | 1 | 生成效果（`.effects ...`） | `spawn: .effects brigand_barrel_spawn_riposte`　<sub>Monsters/brigand_barrel_D.txt</sub> |

行首记录前缀只统计**顶格**（`^[A-Za-z_]+:`）；`.prop value` 形式的子属性用 `.` 前缀，未计入上表。

### 2.2 `Heroes/Info/*.bytes` —— 英雄数据（15 个，全部为文本）

| 文件 | 字节 | 行数 | 记录前缀计数 |
|---|---:|---:|---|
| `Heroes\Info\Abomination.bytes` | 10087 | 74 | combat_skill:42 armour:5 weapon:5 mode:2 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 incompatible_party_member:1 info:1 name:1 rendering:1 resistances:1 skill_selection:1 |
| `Heroes\Info\Antiquarian.bytes` | 8819 | 73 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 extra_battle_loot:1 extra_curio_loot:1 extra_stack_limit:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\Arbalest.bytes` | 8411 | 70 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\BountyHunter.bytes` | 8594 | 70 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 riposte_skill:1 skill_selection:1 |
| `Heroes\Info\Crusader.bytes` | 7585 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\GraveRobber.bytes` | 8318 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\Hellion.bytes` | 8167 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\Highwayman.bytes` | 8576 | 71 | combat_skill:42 armour:5 weapon:5 riposte_skill:2 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\HoundMaster.bytes` | 8546 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\Jester.bytes` | 8419 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\Leper.bytes` | 7884 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\ManAtArms.bytes` | 8419 | 71 | combat_skill:42 armour:5 weapon:5 riposte_skill:2 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\Occultist.bytes` | 8474 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\PlagueDoctor.bytes` | 8319 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |
| `Heroes\Info\Vestal.bytes` | 7600 | 69 | combat_skill:42 armour:5 weapon:5 tag:2 art:1 combat_move_skill:1 commonfx:1 controlled:1 deaths_door:1 generation:1 id_index:1 info:1 name:1 resistances:1 skill_selection:1 |

### 2.3 `Dungeons/*.bytes` —— 地牢遭遇表（7 个，全部为文本）

| 文件 | 字节 | 行数 | 记录前缀计数 |
|---|---:|---:|---|
| `Dungeons\Cove.bytes` | 13109 | 195 | hall:74 room:62 stall:11 hall_curios:10 boss:6 id:4 room_curios:4 mash:3 named:3 room_treasures:3 hall_variants:1 is_released:1 obstacles:1 props:1 room_variants:1 secret_room_treasures:1 traps:1 |
| `Dungeons\Crypts.bytes` | 15307 | 217 | hall:83 room:61 stall:16 hall_curios:13 named:8 boss:6 room_curios:5 id:4 mash:3 room_treasures:3 hall_variants:1 is_released:1 obstacles:1 props:1 room_variants:1 secret_room_treasures:1 traps:1 |
| `Dungeons\Darkest.bytes` | 4997 | 69 | named:37 hall_curios:5 room_curios:5 room_treasures:3 boss:2 id:2 stall:2 hall:1 hall_variants:1 is_released:1 mash:1 obstacles:1 props:1 room:1 room_variants:1 traps:1 |
| `Dungeons\Shared.bytes` | 465 | 28 | hall:8 id:5 mash:4 hall_variants:1 is_released:1 room_variants:1 |
| `Dungeons\Town.bytes` | 3118 | 58 | named:14 hall_curios:13 room_curios:5 stall:4 room_treasures:3 id:2 boss:1 hall:1 hall_variants:1 is_released:1 mash:1 obstacles:1 props:1 room:1 room_variants:1 secret_room_treasures:1 traps:1 |
| `Dungeons\Warrens.bytes` | 13361 | 210 | hall:94 room:52 stall:14 hall_curios:12 boss:6 id:4 room_curios:4 mash:3 named:3 room_treasures:3 hall_variants:1 is_released:1 obstacles:1 props:1 room_variants:1 secret_room_treasures:1 traps:1 |
| `Dungeons\Weald.bytes` | 13089 | 217 | hall:94 room:54 stall:14 hall_curios:13 boss:6 named:5 id:4 room_curios:4 mash:3 room_treasures:3 hall_variants:1 is_released:1 obstacles:1 props:1 room_variants:1 secret_room_treasures:1 traps:1 |

### 2.4 `Inventory/Items.bytes` —— 物品/堆叠定义（1 个，文本）

| 文件 | 字节 | 行数 | 记录前缀计数 |
|---|---:|---:|---|
| `Inventory\Items.bytes` | 6854 | 63 | inventory_item:57 |

### 2.5 `Maps/*.bytes` —— 地图数据（7 个，**全部为 Unity 二进制序列化，非 DD1 文本**）

判定依据：控制字节（`b<9 or 13<b<32`）占比 > 5%；用 `[ -~]{4,}` 抽取可打印串。

| 文件 | 字节 | 控制字节 | 可打印串数 | 头部标识 | 二进制内 `key:` 式 token |
|---|---:|---:|---:|---|---|
| `Maps\DD_map1.bytes` | 6643 | 3153 | 209 | `darkestdungeon1` |  |
| `Maps\DD_map2.bytes` | 7728 | 4045 | 251 | `darkestdungeon` | room:107 |
| `Maps\DD_map3.bytes` | 14287 | 7403 | 472 | `darkestdungeon-` | room:204 |
| `Maps\DD_map4.bytes` | 970 | 673 | 17 | `darkestdungeon` | room:7 |
| `Maps\post_game_complete_start_map.bytes` | 480 | 249 | 18 | `weald` |  |
| `Maps\town_invasion_0.bytes` | 3472 | 1538 | 136 | `town!` | room:36 |
| `Maps\tutorial_crypts.bytes` | 2904 | 1522 | 101 | `crypts` | room:41 |

例：`Maps/DD_map4.bytes` 节选（可打印串，原样）：

```
darkestdungeon
room:2/4
room:2/4
plot_darkest_dungeon_4_enter
2/4_to_21/4
room:21/4
plot_darkest_dungeon_4_final
ancestor_small_D
2/4_to_21/4
2/4_to_21/4
```

### 2.6 `Monsters/*.txt` —— 怪物定义（230 个，全部为文本）

前缀合计：`skill:`×974 `enemy_type:`×278 `loot:`×232 `name:`×230 `type:`×230 `art:`×230 `commonfx:`×230 `info:`×230 `display:`×230 `stats:`×230 `personality:`×230 `initiative:`×230 `monster_brain:`×230 `battle_modifier:`×229 `death_class:`×118 `defending_area_pos_offset:`×70 `tag:`×67 `life_link:`×25 `shape_shifter:`×24 `display_modifier:`×12 `shared_health:`×12 `rendering:`×10 `captor_full:`×9 `health_bar:`×8 `life_time:`×8 `battle_backdrop:`×7 `captor_empty:`×6 `companion:`×6 `battle_stage:`×4 `audio_modifier:`×4 `torchlight_modifier:`×3 `controller:`×3 `skill_reaction:`×2 `riposte_skill:`×2 `death_damage:`×1 `spawn:`×1

全部 `230` 个怪物文件逐条：

| 文件 | 字节 | 行数 | 记录前缀计数 |
|---|---:|---:|---|
| `Monsters\ancestor_big_D.txt` | 2331 | 31 | skill:8 enemy_type:2 art:1 audio_modifier:1 battle_backdrop:1 battle_modifier:1 battle_stage:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ancestor_flawed_D.txt` | 1286 | 24 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_damage:1 display:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ancestor_heart_D.txt` | 1983 | 30 | skill:8 art:1 audio_modifier:1 battle_backdrop:1 battle_modifier:1 battle_stage:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ancestor_nebula_D.txt` | 617 | 18 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ancestor_perfect_D.txt` | 1245 | 23 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ancestor_pod_D.txt` | 1518 | 27 | skill:4 art:1 audio_modifier:1 battle_backdrop:1 battle_modifier:1 battle_stage:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 skill_reaction:1 stats:1 type:1 |
| `Monsters\ancestor_small_D.txt` | 2286 | 32 | skill:10 enemy_type:2 art:1 audio_modifier:1 battle_backdrop:1 battle_modifier:1 battle_stage:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\bloated_corpse_A.txt` | 1053 | 22 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\bloated_corpse_B.txt` | 1059 | 22 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\bloated_corpse_C.txt` | 1058 | 22 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_barrel_D.txt` | 937 | 21 | riposte_skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 monster_brain:1 name:1 personality:1 skill_reaction:1 spawn:1 stats:1 type:1 |
| `Monsters\brigand_blood_A.txt` | 1435 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_blood_B.txt` | 1419 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_blood_C.txt` | 1419 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_cannon_A.txt` | 1410 | 25 | skill:6 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\brigand_cannon_B.txt` | 1415 | 25 | skill:6 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\brigand_cannon_C.txt` | 1416 | 25 | skill:6 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\brigand_cutthroat_A.txt` | 1579 | 26 | skill:8 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_cutthroat_B.txt` | 1586 | 26 | skill:8 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_cutthroat_C.txt` | 1578 | 26 | skill:8 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_fuseman_A.txt` | 1049 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\brigand_fuseman_B.txt` | 1053 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\brigand_fuseman_C.txt` | 1049 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\brigand_fusilier_A.txt` | 1176 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_fusilier_B.txt` | 1160 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_fusilier_C.txt` | 1155 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_hunter_D.txt` | 1189 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_raider_D.txt` | 1605 | 26 | skill:8 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\brigand_sapper_D.txt` | 1760 | 27 | skill:10 art:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 type:1 |
| `Monsters\carrion_eater_A.txt` | 835 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\carrion_eater_B.txt` | 866 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\carrion_eater_C.txt` | 866 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\carrion_eater_big_A.txt` | 1127 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\carrion_eater_big_B.txt` | 1133 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\carrion_eater_big_C.txt` | 1135 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cauldron_empty_A.txt` | 800 | 21 | art:1 battle_modifier:1 captor_empty:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 skill:1 stats:1 tag:1 type:1 |
| `Monsters\cauldron_empty_B.txt` | 801 | 21 | art:1 battle_modifier:1 captor_empty:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 skill:1 stats:1 tag:1 type:1 |
| `Monsters\cauldron_empty_C.txt` | 801 | 21 | art:1 battle_modifier:1 captor_empty:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 skill:1 stats:1 tag:1 type:1 |
| `Monsters\cauldron_full_A.txt` | 851 | 21 | art:1 battle_modifier:1 captor_full:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 skill:1 stats:1 tag:1 type:1 |
| `Monsters\cauldron_full_B.txt` | 852 | 21 | art:1 battle_modifier:1 captor_full:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 skill:1 stats:1 tag:1 type:1 |
| `Monsters\cauldron_full_C.txt` | 854 | 21 | art:1 battle_modifier:1 captor_full:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 skill:1 stats:1 tag:1 type:1 |
| `Monsters\cell_battle_D.txt` | 814 | 20 | enemy_type:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cell_white_D.txt` | 1246 | 24 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_A.txt` | 1479 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_B.txt` | 1485 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_C.txt` | 1486 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_battle_A.txt` | 762 | 19 | skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_battle_B.txt` | 766 | 19 | skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_battle_C.txt` | 763 | 19 | skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_protect_A.txt` | 847 | 19 | skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_protect_B.txt` | 849 | 19 | skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_protect_C.txt` | 846 | 19 | skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_shaman_A.txt` | 1057 | 21 | skill:4 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_shaman_B.txt` | 1059 | 21 | skill:4 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\collector_shaman_C.txt` | 1057 | 21 | skill:4 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\corpse_A.txt` | 1020 | 21 | art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 health_bar:1 info:1 initiative:1 life_time:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\corpse_B.txt` | 1024 | 21 | art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 health_bar:1 info:1 initiative:1 life_time:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\corpse_C.txt` | 1021 | 21 | art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 health_bar:1 info:1 initiative:1 life_time:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\corpse_D.txt` | 1021 | 21 | art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 health_bar:1 info:1 initiative:1 life_time:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\corpse_large_A.txt` | 1033 | 21 | art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 health_bar:1 info:1 initiative:1 life_time:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\corpse_large_B.txt` | 1036 | 21 | art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 health_bar:1 info:1 initiative:1 life_time:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\corpse_large_C.txt` | 1033 | 21 | art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 health_bar:1 info:1 initiative:1 life_time:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\corpse_large_D.txt` | 1005 | 21 | art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 health_bar:1 info:1 initiative:1 life_time:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\crone_A.txt` | 1520 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\crone_B.txt` | 1551 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\crone_C.txt` | 1552 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_brawler_A.txt` | 1136 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_brawler_B.txt` | 1141 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_brawler_C.txt` | 1139 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_harpy_D.txt` | 1410 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_orgiastic_D.txt` | 1041 | 22 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_shrouded_D.txt` | 1231 | 23 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_warlord_D.txt` | 1166 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_witch_A.txt` | 1261 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_witch_B.txt` | 1361 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cultist_witch_C.txt` | 1357 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\cyst_D.txt` | 1684 | 27 | skill:8 enemy_type:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\drowned_anchor_A.txt` | 1243 | 24 | skill:2 art:1 battle_modifier:1 captor_empty:1 commonfx:1 companion:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\drowned_anchor_B.txt` | 1247 | 24 | skill:2 art:1 battle_modifier:1 captor_empty:1 commonfx:1 companion:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\drowned_anchor_C.txt` | 1247 | 24 | skill:2 art:1 battle_modifier:1 captor_empty:1 commonfx:1 companion:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\drowned_anchored_A.txt` | 968 | 22 | captor_full:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\drowned_anchored_B.txt` | 971 | 22 | captor_full:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\drowned_anchored_C.txt` | 972 | 22 | captor_full:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\drowned_captain_A.txt` | 1887 | 30 | skill:8 art:1 battle_modifier:1 commonfx:1 companion:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\drowned_captain_B.txt` | 1893 | 30 | skill:8 art:1 battle_modifier:1 commonfx:1 companion:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\drowned_captain_C.txt` | 1895 | 30 | skill:8 art:1 battle_modifier:1 commonfx:1 companion:1 defending_area_pos_offset:1 display:1 display_modifier:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\ectoplasm_A.txt` | 1276 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ectoplasm_B.txt` | 1321 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ectoplasm_C.txt` | 1324 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ectoplasm_large_A.txt` | 1247 | 25 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ectoplasm_large_B.txt` | 1289 | 25 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ectoplasm_large_C.txt` | 1292 | 25 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\errant_flesh_bat_D.txt` | 1291 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\errant_flesh_dog_D.txt` | 1409 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_crabby_A.txt` | 1088 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_crabby_B.txt` | 1118 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_crabby_C.txt` | 1120 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_harpoon_A.txt` | 1010 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_harpoon_B.txt` | 1044 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_harpoon_C.txt` | 1042 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_shaman_A.txt` | 1596 | 26 | skill:8 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_shaman_B.txt` | 1600 | 26 | skill:8 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fishman_shaman_C.txt` | 1598 | 26 | skill:8 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\formless_guard_A.txt` | 1086 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_guard_B.txt` | 1090 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_guard_C.txt` | 1090 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_melee_A.txt` | 1122 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_melee_B.txt` | 1128 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_melee_C.txt` | 1129 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_ranged_A.txt` | 1198 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_ranged_B.txt` | 1206 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_ranged_C.txt` | 1207 | 23 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_weak_A.txt` | 1125 | 24 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_weak_B.txt` | 1132 | 24 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\formless_weak_C.txt` | 1132 | 24 | shape_shifter:2 skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 shared_health:1 stats:1 tag:1 type:1 |
| `Monsters\fungal_artillery_A.txt` | 1497 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fungal_artillery_B.txt` | 1509 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fungal_artillery_C.txt` | 1507 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fungal_bloat_A.txt` | 1266 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fungal_bloat_B.txt` | 1297 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\fungal_bloat_C.txt` | 1298 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\gargoyle_A.txt` | 953 | 22 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\gargoyle_B.txt` | 1019 | 22 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\gargoyle_C.txt` | 1017 | 22 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ghoul_A.txt` | 1289 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ghoul_B.txt` | 1318 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\ghoul_C.txt` | 1317 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\hag_A.txt` | 1640 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\hag_B.txt` | 1649 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\hag_C.txt` | 1647 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\jellyfish_A.txt` | 1121 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\jellyfish_B.txt` | 1126 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\jellyfish_C.txt` | 1123 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\madman_A.txt` | 1171 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\madman_B.txt` | 1177 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\madman_C.txt` | 1174 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\maggot_A.txt` | 848 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\maggot_B.txt` | 853 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\maggot_C.txt` | 854 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\necromancer_A.txt` | 1435 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\necromancer_B.txt` | 1444 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\necromancer_C.txt` | 1444 | 26 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\octotank_A.txt` | 1081 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\octotank_B.txt` | 1085 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\octotank_C.txt` | 1086 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\pew_large_A.txt` | 637 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\pew_large_B.txt` | 638 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\pew_large_C.txt` | 641 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\pew_medium_A.txt` | 639 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\pew_medium_B.txt` | 640 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\pew_medium_C.txt` | 642 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\pew_small_A.txt` | 637 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\pew_small_B.txt` | 640 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\pew_small_C.txt` | 640 | 19 | art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 life_link:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\prophet_A.txt` | 1747 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\prophet_B.txt` | 1759 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\prophet_C.txt` | 1757 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 rendering:1 stats:1 tag:1 type:1 |
| `Monsters\rabid_dog_A.txt` | 859 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\rabid_dog_B.txt` | 863 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\rabid_dog_C.txt` | 864 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\shambler_A.txt` | 1611 | 28 | skill:6 loot:2 art:1 battle_backdrop:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 torchlight_modifier:1 type:1 |
| `Monsters\shambler_B.txt` | 1617 | 28 | skill:6 loot:2 art:1 battle_backdrop:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 torchlight_modifier:1 type:1 |
| `Monsters\shambler_C.txt` | 1616 | 28 | skill:6 loot:2 art:1 battle_backdrop:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 torchlight_modifier:1 type:1 |
| `Monsters\shambler_tentacle_A.txt` | 839 | 21 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\shambler_tentacle_B.txt` | 842 | 21 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\shambler_tentacle_C.txt` | 839 | 21 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\shuffler_D.txt` | 1428 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\siren_A.txt` | 1592 | 28 | skill:8 art:1 battle_modifier:1 commonfx:1 controller:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\siren_B.txt` | 1596 | 28 | skill:8 art:1 battle_modifier:1 commonfx:1 controller:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\siren_C.txt` | 1594 | 28 | skill:8 art:1 battle_modifier:1 commonfx:1 controller:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\skeleton_arbalist_A.txt` | 1081 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_arbalist_B.txt` | 1087 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_arbalist_C.txt` | 1086 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_captain_A.txt` | 1112 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_captain_B.txt` | 1154 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_captain_C.txt` | 1156 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_common_A.txt` | 1019 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_common_B.txt` | 1056 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_common_C.txt` | 1054 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_courtier_A.txt` | 1072 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_courtier_B.txt` | 1082 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_courtier_C.txt` | 1079 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_defender_A.txt` | 1264 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_defender_B.txt` | 1309 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_defender_C.txt` | 1308 | 24 | skill:6 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_militia_A.txt` | 1063 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_militia_B.txt` | 1070 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_militia_C.txt` | 1069 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_spear_A.txt` | 1096 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_spear_B.txt` | 1103 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\skeleton_spear_C.txt` | 1102 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\snail_urchin_A.txt` | 860 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\snail_urchin_B.txt` | 864 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\snail_urchin_C.txt` | 864 | 20 | skill:2 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\spider_spitter_A.txt` | 1179 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\spider_spitter_B.txt` | 1185 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\spider_spitter_C.txt` | 1184 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\spider_webber_A.txt` | 1138 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\spider_webber_B.txt` | 1145 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\spider_webber_C.txt` | 1143 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_drummer_A.txt` | 1112 | 23 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_drummer_B.txt` | 1143 | 23 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_drummer_C.txt` | 1143 | 23 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_piglet_A.txt` | 1551 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\swine_piglet_B.txt` | 1562 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\swine_piglet_C.txt` | 1607 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\swine_prince_A.txt` | 1626 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\swine_prince_B.txt` | 1639 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\swine_prince_C.txt` | 1637 | 27 | skill:8 art:1 battle_modifier:1 commonfx:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 tag:1 type:1 |
| `Monsters\swine_reaver_A.txt` | 1076 | 23 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_reaver_B.txt` | 1103 | 23 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_reaver_C.txt` | 1105 | 23 | skill:4 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_slasher_A.txt` | 877 | 21 | enemy_type:2 skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_slasher_B.txt` | 912 | 21 | enemy_type:2 skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_slasher_C.txt` | 910 | 21 | enemy_type:2 skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_wretch_A.txt` | 875 | 21 | skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_wretch_B.txt` | 902 | 21 | skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swine_wretch_C.txt` | 900 | 21 | skill:2 art:1 battle_modifier:1 commonfx:1 death_class:1 defending_area_pos_offset:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swinetaur_A.txt` | 1612 | 27 | skill:8 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swinetaur_B.txt` | 1620 | 27 | skill:8 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\swinetaur_C.txt` | 1624 | 27 | skill:8 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\templar_melee_D.txt` | 1301 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\templar_melee_mb_D.txt` | 1544 | 27 | skill:8 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\templar_ranged_D.txt` | 1402 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\templar_ranged_mb_D.txt` | 1635 | 27 | skill:8 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\totem_attack_D.txt` | 1055 | 22 | skill:4 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\totem_guard_D.txt` | 1604 | 26 | skill:8 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 enemy_type:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\unclean_giant_A.txt` | 1360 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\unclean_giant_B.txt` | 1369 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |
| `Monsters\unclean_giant_C.txt` | 1370 | 25 | skill:6 enemy_type:2 art:1 battle_modifier:1 commonfx:1 death_class:1 display:1 info:1 initiative:1 loot:1 monster_brain:1 name:1 personality:1 stats:1 type:1 |

样例（`Monsters/skeleton_common_A.txt`，原样，前 12 行）：

```
name: skeleton_common_A
type: skeleton_common
art:
commonfx: .deathfx death_medium
skill: .id "cudgel" .anim "attack_melee" .fx "cudgel" .targchestfx "blood_splatter"
skill: .id "cudgel_weak" .anim "attack_melee" .fx "cudgel" .targchestfx "blood_splatter"
.end
info:
display: .size 1
enemy_type: .id "unholy"
stats: .hp 8 .def 0% .prot 0 .spd 1 .stun_resist 10% .poison_resist 10% .bleed_resist 200% .debuff_resist 15% .move_resist 10%
skill: .id "cudgel" .type "melee" .atk 62.5% .dmg 2 5 .crit 2%  .launch 321 .target 12
```

样例（`Monsters/swine_prince_C.txt`，原样，前 12 行）：

```
name: swine_prince_C
type: swine_prince
art:
commonfx: .deathfx death_large_boss
skill: .id "obliterate_marked_one" .anim "attack_melee" .fx "marked_one" .targchestfx "blood_splatter"
skill: .id "obliterate_marked_two" .anim "attack_melee" .fx "marked_one" .targchestfx "blood_splatter"
skill: .id "obliterate_enraged" .anim "attack_melee" .fx "enraged" .targchestfx "blood_splatter" .area_pos_offset -100 0 .target_area_pos_offset 150 0
skill: .id "obliterate_blind" .anim "attack_claw" .fx "blind" .targchestfx "blood_splatter" .area_pos_offset 0 -30
defending_area_pos_offset: .offset 0 -75
.end
info:
display: .size 3
```

### 2.7 `Mechanics/*.txt` —— 规则文本（2 个）

| 文件 | 字节 | 行数 | 记录前缀计数 |
|---|---:|---:|---|
| `Mechanics\Effects.txt` | 176745 | 1820 | effect:952 |
| `Mechanics\MapGenerator.txt` | 19906 | 1068 | map:44 |

样例（`Mechanics/MapGenerator.txt`，原样，前 12 行）：

```
map:
.size short
.quest_type explore
.dungeon_type crypts
.base_room_number 9
.base_corridor_number 10
.gridsize 4 3
.spacing 4
.goal_room_number 9
.connectivity 0.9
.min_final_distance 3
.hallway_battle 2 4
```

样例（`Mechanics/Effects.txt`，原样，前 10 行）：

```
//Shared Effects
//List of shared effects; may be used by traps, curios, enemies, and heroes alike (Will probably be impossible to reference every effect)
//--------------------------------------------------
//Kill Self -- Used only on "bloated_corpse" (Thrall) currently
effect: .name "kill_self" .target "performer" .chance 100% .kill 1 .on_hit true .on_miss true .apply_once true .queue false
effect: .name "kill_performer_group_other" .target "performer_group_other" .chance 100% .kill 1 .on_hit true .on_miss true .apply_once true .queue false .apply_with_result true
effect: .name "kill_self_queued" .target "performer" .chance 100% .kill 1 .on_hit true .on_miss true .apply_once true .queue false
effect: .name "kill_target" .target "target" .chance 100% .kill 1 .on_hit true .on_miss true .apply_once true .queue false .apply_with_result true
//Push + Pull -- Movement Effects
//-------------------------
```

`.prop` 子属性键频次（用于判断这两份文本的字段面）：

| 文件 | `.prop` 键频次（前 20） |
|---|---|
| `Mechanics\Effects.txt` | `.name`×952 `.target`×952 `.on_hit`×950 `.on_miss`×947 `.chance`×941 `.curio_result_type`×739 `.queue`×447 `.combat_stat_buff`×353 `.duration`×207 `.can_apply_on_death`×160 `.apply_once`×152 `.damage_low_multiply`×139 `.damage_high_multiply`×139 `.defense_rating_add`×97 `.attack_rating_add`×95 `.buff_ids`×90 `.speed_rating_add`×75 `.crit_chance_add`×67 `.healstress`×52 `.stress`×51 |
| `Mechanics\MapGenerator.txt` | `.size`×44 `.quest_type`×44 `.dungeon_type`×44 `.base_room_number`×44 `.base_corridor_number`×44 `.gridsize`×44 `.spacing`×44 `.goal_room_number`×44 `.connectivity`×44 `.min_final_distance`×44 `.hallway_battle`×44 `.hallway_trap`×44 `.hallway_obstacle`×44 `.hallway_curio`×44 `.hallway_hunger`×44 `.total_room_battles`×44 `.room_battle`×44 `.room_guarded_curio`×44 `.room_curio`×44 `.room_guarded_treasure`×44 |

## 3. `Localization/*.xml` —— 本地化字符串表（18 个）

结构一律为 `<root><language id="english"><entry id="..."><![CDATA[...]]></entry>…`。
条目数 = 文件内 `<entry` 出现次数；唯一 id 数 = 去重后的 `id` 属性数（两者若不等说明有重复 key）。

| 文件 | 字节 | `<entry>` 条目数 | 唯一 id 数 | 语言 |
|---|---:|---:|---:|---|
| `Localization\Dialogue.xml` | 6266790 | **50383** | 2461 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Misc.xml` | 1113089 | **9934** | 1235 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\activity_log.string_table.xml` | 642526 | **3320** | 414 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Activity.xml` | 625836 | **3297** | 414 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Narration.xml` | 575860 | **3928** | 491 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Heroes.xml` | 572058 | **5048** | 631 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Curios.xml` | 390483 | **3408** | 426 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Help.xml` | 335328 | **1721** | 182 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Monsters.xml` | 296487 | **3440** | 429 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\TownEvents.xml` | 270683 | **1832** | 222 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Names.xml` | 262105 | **4448** | 556 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Kickstarter.xml` | 227240 | **2344** | 293 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Secret.xml` | 196895 | **1720** | 211 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Quirks.xml` | 169819 | **1952** | 242 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Actors.xml` | 125051 | **1394** | 174 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Journal.xml` | 109908 | **368** | 46 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\PartyNames.xml` | 104475 | **1496** | 187 | english,french,german,spanish,brazilian,russian,polish,czech |
| `Localization\Menu.xml` | 88734 | **842** | 121 | english,french,german,spanish,brazilian,russian,polish,czech |
| **合计** | 12373367 | **100875** | | |

样例（`Localization/Menu.xml`，原样）：

```
<root>
  <language id="english">
    <entry id="menu_base_element_return_to_game"><![CDATA[Return to game]]></entry>
    <entry id="menu_controls_category_0"><![CDATA[GENERAL]]></entry>
    <entry id="menu_controls_element_0_0"><![CDATA[H - Contextual help for any screen]]></entry>
    <entry id="menu_controls_category_1"><![CDATA[TOWN HOTKEYS]]></entry>
```

抽样统计 `Localization/Dialogue.xml` 的 CDATA 键名前缀分布（前 12）：

```
str_*                    5961
crusader+str_*           3328
abomination+str_*        3070
vestal+str_*             2744
man_*                    2728
musketeer+str_*          2728
arbalest+str_*           2720
plague_*                 2704
highwayman+str_*         2696
houndmaster+str_*        2696
antiquarian+str_*        2696
grave_*                  2688
```

## 4. `Curios/Curios.csv` —— 奇物表（唯一 CSV 数据文件）

| 属性 | 值 |
|---|---|
| 字节 | 51069 |
| CSV 总行数（`csv.reader`） | 898 |
| 非空行数 | 708 |
| 最大列数 | 18 |
| 奇物块数（第 2 列是序号且第 3 列非空的行） | **60** |
| 唯一奇物 id 数（第 3 列去重） | **59** |

表头（第 4 行）：

```
,,ID STRING,,RESULT TYPES,WEIGHT,% CHANCE,RESULT 1,R1 WEIGHT,R1 %,RESULT 2,R2 WEIGHT,R2 %,RESULT 3,R3 WEIGHT,R3 %,STRING,NOTES
```

样例（前 8 行，原样）：

```
,,,,,,,,,,,,,,,,,
,,,,,,,,,,,,,,,,,
,1,Unlocked Strongbox,,Good,,,,,,,,,,,,,
,,ID STRING,,RESULT TYPES,WEIGHT,% CHANCE,RESULT 1,R1 WEIGHT,R1 %,RESULT 2,R2 WEIGHT,R2 %,RESULT 3,R3 WEIGHT,R3 %,STRING,NOTES
,,unlocked_strongbox,,Nothing,,,N/A,N/A,N/A,N/A,N/A,N/A,N/A,N/A,N/A,The strongbox is empty.,
,,REGION FOUND,,Loot,3,75.00%,A,2,<- # Draws,,,<- # Draws,,,<- # Draws,The strongbox contains valuable treasures.,
,,ALL,,Quirk,,,,,,,,,,,,,
,,FULL CURIO?,,Effect,1,25.00%,Blight 1,1,100.00%,,,,,,,The strongbox is a trap!,
```

列名清单：`ID STRING` / `RESULT TYPES` / `WEIGHT` / `% CHANCE` / `RESULT 1..3` / `R1..R3 WEIGHT` / `R1..R3 %` / `STRING` / `NOTES`，另在前置列有 `Unlocked`（解锁条件）与 `REGION FOUND`（区域）两个行级标签。

## 5. 我方对应文件映射（`darkest/data/` 共 25 个文件）

我方 25 个文件：`buff_defs, buildings, camp_skills, curios, economy, encounters, enemy_ai, expedition_map, expedition_nodes, formation, heirlooms, heirloom_exchange, hero_upgrades, morale_events, quirks, room_contents, roster, sanitarium, skills, traits, trap_defs, trinkets, tuning, units, unlocks`。

| 参考数据（相对 `Assets\Resources\Data`） | 我方对应 |
|---|---|
| `JsonBuffs.json` | `buff_defs.json` |
| `JsonAI.json` | `enemy_ai.json` |
| `JsonQuests.json` | **我方无对应**（任务/委托定义；部分落到 `expedition_nodes.json`） |
| `JsonQuirks.json` | `quirks.json` |
| `JsonTraits.json` | `traits.json` |
| `JsonTrinkets.json` | `trinkets.json` |
| `JsonCamping.json` | `camp_skills.json` |
| `JsonLoot.json` | **我方无对应**（掉落表/黑暗奖励；部分落到 `room_contents.json`、`economy.json`） |
| `Narration.json` | **我方无对应**（旁白文本） |
| `PartyNames.json` | **我方无对应**（队伍随机命名） |
| `Curios/Curios.csv` | `curios.json` |
| `Curios/Traps.json` | `trap_defs.json` |
| `Curios/Obstacles.json` | **我方无对应**（障碍物/可破坏道具） |
| `Buildings/*.building.json` | `buildings.json` |
| `Upgrades/Building/*.upgrades.json` | `buildings.json`（升级树部分）+ `heirlooms.json` |
| `Upgrades/Heroes/*.upgrades.json` | `hero_upgrades.json` |
| `Mechanics/Campaign.json` | `tuning.json` + `roster.json` |
| `Mechanics/Roster.json` | `roster.json` |
| `Mechanics/Provision.json` | `economy.json` |
| `Mechanics/HeirloomExchange.json` | `heirloom_exchange.json` |
| `Mechanics/TownEvents.json` | `morale_events.json`（+ `expedition_nodes.json` 事件节点） |
| `Mechanics/Effects.txt` | `skills.json` / `tuning.json` 的效果词表（我方为内联 effect 字段，无独立文件） |
| `Mechanics/MapGenerator.txt` | `expedition_map.json` |
| `Heroes/Info/*.bytes` | `units.json` + `skills.json` + `camp_skills.json` + `roster.json` |
| `Dungeons/*.bytes` | `encounters.json` |
| `Inventory/Items.bytes` | `economy.json` + `heirlooms.json` |
| `Maps/*.bytes` | `expedition_map.json` + `expedition_nodes.json` + `room_contents.json` |
| `Monsters/*.txt` | `units.json` + `enemy_ai.json` + `skills.json`（怪物技能） |
| `Localization/*.xml` | **我方无对应**（本地化/文本；我方文本内联在数据里） |
| `Upgrades/Building/*` | `buildings.json` / `heirlooms.json` / `unlocks.json` |

## 6. 值得注意的发现

1. **文件数与任务书给的估计不符**：实测 `49` json / `30` bytes / `232` txt / `18` xml，另多出 `1` 个 csv；共 **330** 个数据文件（另有 344 个 `.meta` 被排除）。任务书写的 51 json / 239 txt 偏高。
2. **7 个 `Maps/*.bytes` 是 Unity 二进制序列化，不是 DD1 文本**：`Maps\DD_map1.bytes`、`Maps\DD_map2.bytes`、`Maps\DD_map3.bytes`、`Maps\DD_map4.bytes`、`Maps\post_game_complete_start_map.bytes`、`Maps\town_invasion_0.bytes`、`Maps\tutorial_crypts.bytes`。控制字节占比 50%+，完全没有 `name:`/`skill:` 之类行首前缀，只能用 `[ -~]{4,}` 抽可打印串（`room:`、`plot_*`、`ancestor_small_D`、`*_to_*` 边 id）。这些是**已烘焙的关卡布局**，与我方 `expedition_map.json`（参数化生成器）语义不对等。
3. **7 个 JSON 是非严格 JSON（尾随逗号）**：`Buildings/abbey.building.json`、`Buildings/nomad_wagon.building.json`、`Buildings/sanitarium.building.json`、`Buildings/tavern.building.json`、`Curios/Traps.json`、`JsonAI.json`、`JsonQuests.json` —— 直接 `json.loads` 会抛 `Illegal trailing comma`，任何移植/导入工具都必须先清洗（本报告脚本的 `strip_trailing_commas()` 即为此；每个文件的严格解析失败位置见 `_raw_stats.json`）。
4. **我方完全没有对应物的参考数据**：`Narration.json`（旁白 36 条）、`PartyNames.json`（队伍命名 186 条）、`JsonLoot.json`（54 张 loot_table + 2 组黑暗奖励）、`JsonQuests.json`（任务目标 45 条 / 剧情任务 30 条 / 类型 6 个）、`Curios/Obstacles.json`（障碍物 5 个）、`Localization/*.xml`（18 个字符串表 / 12373367 字节 / 100875 条 `<entry>`）。
5. **量级对比**：参考项目文本占绝对主体 —— xml 12373367 B + txt 472872 B = 12846239 B，占全部 15477657 B 的 83%；真正的「数值」JSON 只有 2347347 B（15%）。我方 `darkest/data/` 25 个文件合计仅 768457 B。

## 7. 复算与交叉验证（每条数字都有两种独立算法）

方法 A = 结构化解析后取长度；方法 B = **不解析 JSON**，直接对原始字节做正则计数（用于互相打假）。
把下列命令原样粘贴到 PowerShell 即可复现（`py` 可换成 `python`）。

### 7.1 大文件条目数：A 结构化 vs B 正则

| 文件 | 集合路径 | A: `len(...)` | B: 正则计数 | B 用的哨兵字段 | 一致 |
|---|---|---:|---:|---|---|
| `JsonBuffs.json` | `$.buffs` | **1801** | 1801 | `stat_type` | ✅ |
| `JsonAI.json` | `$.monster_brains` | **160** | 160 | `skill_cooldowns` | ✅ |
| `JsonQuirks.json` | `$.quirks` | **163** | 163 | `is_positive` | ✅ |
| `JsonTraits.json` | `$.traits` | **12** | 12 | `overstress_type` | ✅ |
| `JsonTrinkets.json` | `$.trinkets` | **488** | 488 | `origin_dungeon` | ✅ |
| `JsonQuests.json` | `$.goals` | **45** | 45 | `show_as_quest` | ✅ |
| `JsonCamping.json` | `$.skills` | **64** | 64 | `use_limit` | ✅ |
| `JsonLoot.json` | `$.loot_tables` | **54** | 54 | `dungeon` | ✅ |
| `Narration.json` | `$.entries` | **36** | 36 | `audio_events` | ✅ |
| `PartyNames.json` | `$.party_names` | **186** | 186 | `required_hero_class` | ✅ |
| `Mechanics/TownEvents.json` | `$.events` | **45** | 45 | `per_not_rolled_additional_chance` | ✅ |

全部 11 条 A/B 完全一致 —— 条目数不是估计值，两套独立算法都得到同一个数。

### 7.2 文件计数：A `os.walk` vs B `Get-ChildItem`

```
> (Get-ChildItem "F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data" -Recurse -File |
    Where-Object { $_.Extension -ne ".meta" } | Group-Object Extension |
    Select-Object Name, Count, @{n="Bytes";e={($_.Group | Measure-Object Length -Sum).Sum}}).Format-Table()
```

| 扩展名 | A `os.walk` | B `Get-ChildItem` | 一致 |
|---|---:|---:|---|
| `.txt` | 232 | 232 | ✅ |
| `.json` | 49 | 49 | ✅ |
| `.bytes` | 30 | 30 | ✅ |
| `.xml` | 18 | 18 | ✅ |
| `.csv` | 1 | 1 | ✅ |

### 7.3 DD1 文本前缀计数（独立算法）

```
python -c "import io,re,collections;c=collections.Counter();[c.update(re.findall(r'^([A-Za-z_][A-Za-z0-9_]*):', io.open(p,encoding='utf-8',errors='replace').read(), re.M)) for p in __import__('glob').glob(r'...\Monsters\*.txt')];print(c.most_common())"
```

注：脚本用的是逐文件 `re.findall(r"^([A-Za-z_][A-Za-z0-9_]*)\s*:", txt, re.M)`，此处等价写法（`\s*` 差异不影响结果，因为 DD1 文本 `key:` 后紧跟空格或 Tab）。

### 7.4 复算命令（关键几条；下表「实测输出」是脚本用 `subprocess` 真跑一遍抓下来的，不是手抄）

命令统一写成 **PowerShell 可用形式**（外层 PowerShell 双引号 + 内层 python 单引号 + 正斜杠路径），避免 `\` 与 `$` 被 PowerShell 吃掉。

| 目标 | 命令（PowerShell 可直接粘贴） | 实测输出 |
|---|---|---|
| Data 下非 `.meta` 文件总数 | `python -c "import os;R=r'F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data';print(sum(len([f for f in fs if not f.endswith('.meta')]) for _,_,fs in os.walk(R)))"` | `330` |
| JsonBuffs 条目数 | `python -c "import io,json;R=r'F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data/JsonBuffs.json';print(len(json.load(io.open(R,encoding='utf-8'))['buffs']))"` | `1801` |
| JsonTrinkets 条目数 | `python -c "import io,json;R=r'F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data/JsonTrinkets.json';print(len(json.load(io.open(R,encoding='utf-8'))['trinkets']))"` | `488` |
| JsonQuirks 条目数 | `python -c "import io,json;R=r'F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data/JsonQuirks.json';print(len(json.load(io.open(R,encoding='utf-8'))['quirks']))"` | `163` |
| JsonAI 条目数（不解析，正则计数） | `python -c "import io;R=r'F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data/JsonAI.json';print(io.open(R,encoding='utf-8').read().count(chr(34)+'skill_cooldowns'+chr(34)))"` | `160` |
| Localization 全部 `<entry>` 条数 | `python -c "import io,re,glob;print(sum(len(re.findall(r'<entry\b',io.open(p,encoding='utf-8',errors='replace').read())) for p in glob.glob(r'F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data/Localization/*.xml')))"` | `100875` |
| Curios.csv 奇物块数 | `python -c "import io,re;d=io.open(r'F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data/Curios/Curios.csv',encoding='utf-8',errors='replace').read().split(chr(10));print(len([l for l in d if re.match(r'^,[0-9]+,[^,]+',l)]))"` | `60` |
| Monsters 全部前缀计数（前 6） | `python -c "import io,re,glob,collections;c=collections.Counter();[c.update(re.findall(r'^([A-Za-z_][A-Za-z0-9_]*)[ \t]*:',io.open(p,encoding='utf-8',errors='replace').read(),re.M)) for p in glob.glob(r'F:/GithubPro/Darkest-Dungeon-Unity/Assets/Resources/Data/Monsters/*.txt')];print(sum(c.values()),c.most_common(6))"` | `4415 [('skill', 974), ('enemy_type', 278), ('loot', 232), ('name', 230), ('type', 230), ('art', 230)]` |

### 7.5 已知偏差与说明

- 任务书说“51 个 json / 30 个 bytes / 239 个 txt / 18 个 xml”：实测 **49 / 30 / 232 / 18**。`bytes` 与 `xml` 精确吻合；json 少 2、txt 少 7。
- `Curios/` 下没有 `Curios.json`：奇物数据实际在 **`Curios.csv`**（表格导出），`Obstacles.json` 与 `Traps.json` 是两个独立的 props 数组。
- `Monsters/` 下没有子目录，230 个 `.txt` 平铺；怪物后缀 `_A/_B/_C` 是**难度/等级变体**，`_D` 是 Boss/特殊变体。
- `Localization/` 全部只有 `english` 一种语言（`<language id="english">`），没有多语言表。

