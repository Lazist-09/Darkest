# 02a · Unity 参考项目：技能的具体数值存在哪个文件

- 参考项目（**只读**）：`F:\GithubPro\Darkest-Dungeon-Unity`
- 报告生成时间：本次会话
- 分析脚本与原始输出（均在本目录）：`_q1_hits.py` → `_q1_out.txt`、`_q2_skills_keys.py` → `_q2_out.txt`
- 本报告只记录：字段名、行号、≤200 字符的片段。不誊抄长段源码（参考项目为 GPL）。

---

## 0. 结论先行

| 问题 | 事实结论 |
| --- | --- |
| Q1 技能数值在哪 | **就在 `Assets\Resources\Data\Heroes\Info\<Hero>.bytes` 同一个文件里**。同一文件里有**两块** `combat_skill:` 记录：第一块（`.id/.icon/.anim/.fx`，**没有 `.level`**）只有表现字段；第二块（**带 `.level 0..4`**）才是数值块，含 `.type/.atk/.dmg/.crit/.launch/.target/.effect/.heal/.move`。 |
| Q2 `combat_skill:` 的 key 全集 | 共 **28** 个 key（art 块 9 个 + stat 块 20 个，`.id` 重叠）。全集见 §3.1。 |
| Q3 有没有伤害百分比字段 | **有**。两个层次：(a) 技能本体上的 `.dmg`（百分数，hero 专用，语义是"伤害修正/倍率"）；(b) 效果上的 `.damage_low_multiply` / `.damage_high_multiply`（百分数）。**没有**找到 `dmg%` / `dmg_mod` / `damage_mod` / `dmg_mult` 这类**字面字段名**。 |

一句话链路：
`HeroInfo/<Hero>.bytes` 的 `.dmg X%`（技能级伤害修正）+ `.effect "效果名"` → `Mechanics/Effects.txt` 里 `effect: .name "效果名"` 的 `.damage_low_multiply/.damage_high_multiply`（条件性伤害倍率）。

---

## 1. 环境与命令（可复现）

环境：Windows / PowerShell 7 / Python 3.14.7（`python -c "import sys; print(sys.version)"` → `3.14.7`）。

实际执行的检索（grep 工具内部即 ripgrep；此处给出等价 `rg` 写法，语义一致）：

```
rg -n "smite"                 Assets
rg -n "zealous_accusation"    Assets
rg -n "holy_lance"            Assets
rg -n "battle_heal"           Assets
rg -n "inspiring_cry"         Assets
rg -n "Unholy Killer|Crusader HealStress|Crusader Light"  Assets
rg -n "damage_low_multiply|damage_high_multiply|dmg_mod|damage_mod"  Assets\Resources\Data\Mechanics
rg -n "\.dmg%|dmg_mod|damage_mod|dmg_mult|\.atk%|damage_multiply|dmg_multiplier|damage_multiplier"  Assets
rg -n "damage_low_multiply|damage_high_multiply|damage_multiply"  Assets\Scripts
rg -n "\"\.atk\"|\"\.dmg\"|\"\.heal\"|\"\.level\"|\"\.launch\"|\"\.target\"|\"\.effect\""  Assets\Scripts
rg -n "DamageMin|DamageMax|DamageMod|MinDamage|MaxDamage"  Assets\Scripts
rg -n "damage_low_multiply" -g "*.json"  Assets\Resources\Data      # → 无命中
rg -n "\"dmg\"|\"atk\"|\"damage|\"crit|DamageMod|\"heal\""  Assets\Resources\Data\Upgrades\Heroes\crusader.upgrades.json   # → 无命中
rg -n "damage|Damage|_atk|skillId|SkillId"  Assets\Resources\Prefabs\Heroes\crusader.prefab   # → 无命中
```

Python 脚本（本次新写，只读参考项目；输出 ASCII 到 `_q1_out.txt` / `_q2_out.txt`）：

```
python F:\GithubPro\Darkest\reports\unity_ref\_q1_hits.py
python F:\GithubPro\Darkest\reports\unity_ref\_q2_skills_keys.py
```

遍历范围：`Assets\**`，扩展名 `.cs .json .bytes .txt .asset .prefab .xml`（`.meta` 跳过）。
15 个 hero 文件确认存在：`Get-ChildItem -Recurse Assets -Filter *.bytes | ? FullName -match Heroes` → 15 个。

---

## 2. Q1：5 个技能 id 的命中清单

### 2.1 总览表（是否带数值）

| 技能 id | 命中文件（相对项目根） | 命中行数 | 含数值参数？ |
| --- | --- | --- | --- |
| `smite` | `Assets\Resources\Data\Heroes\Info\Crusader.bytes` | 6 | **YES**（`.atk/.dmg/.crit/.launch/.target/.effect/.level`） |
| `smite` | `Assets\Resources\Data\Monsters\necromancer_A.txt` | 2 | **YES**，但那是**怪兽技能 `unholy_smite`**（`.atk 102.5% .dmg 4 8 .crit 6%`） |
| `smite` | `Assets\Resources\Data\Monsters\necromancer_B.txt` | 2 | **YES**，同上（`unholy_smite`，`.dmg 5 11`） |
| `smite` | `Assets\Resources\Data\Monsters\necromancer_C.txt` | 2 | **YES**，同上（`unholy_smite`，`.dmg 8 16`） |
| `smite` | `Assets\FMODStudioCache.asset` | 12 | no（音频 event 路径） |
| `smite` | `Assets\Resources\Data\Localization\Dialogue.xml` | 146 | no（台词 CDATA 文本） |
| `smite` | `Assets\Resources\Data\Localization\Heroes.xml` | 16 | no（显示名词条） |
| `smite` | `Assets\Resources\Data\Localization\Monsters.xml` | 8 | no（`str_monster_skill_unholy_smite` 显示名） |
| `smite` | `Assets\Resources\Data\Upgrades\Heroes\crusader.upgrades.json` | 5 | no（只有升级树 id / `tree_id` / `requirement_code`） |
| `smite` | `Assets\Resources\Prefabs\Heroes\crusader.prefab` | 2 | no（GameObject `m_Name: smite` / `_animationName: smite`） |
| `smite` | `Assets\Resources\Prefabs\Monsters\necromancer.prefab` | 2 | no（同上，怪兽 prefab） |
| `smite` | `Assets\Scripts\Setup\SaveSystem\SaveCampaignData.cs` | 1 | no（存档里已购升级 key） |
| `zealous_accusation` | `Assets\Resources\Data\Heroes\Info\Crusader.bytes` | 6 | **YES** |
| `zealous_accusation` | `Assets\FMODStudioCache.asset` | 4 | no |
| `zealous_accusation` | `Assets\Resources\Data\Localization\Heroes.xml` | 16 | no |
| `zealous_accusation` | `Assets\Resources\Data\Upgrades\Heroes\crusader.upgrades.json` | 5 | no |
| `zealous_accusation` | `Assets\Resources\Prefabs\Heroes\crusader.prefab` | 2 | no |
| `zealous_accusation` | `Assets\Scripts\Setup\SaveSystem\SaveCampaignData.cs` | 1 | no |
| `holy_lance` | `Assets\Resources\Data\Heroes\Info\Crusader.bytes` | 6 | **YES** |
| `holy_lance` | `Assets\FMODStudioCache.asset` | 4 | no |
| `holy_lance` | `Assets\Resources\Data\Localization\Heroes.xml` | 16 | no |
| `holy_lance` | `Assets\Resources\Data\Upgrades\Heroes\crusader.upgrades.json` | 5 | no |
| `holy_lance` | `Assets\Resources\Prefabs\Heroes\crusader.prefab` | 2 | no |
| `battle_heal` | `Assets\Resources\Data\Heroes\Info\Crusader.bytes` | 6 | **YES**（数值是 `.heal`，不是 `.dmg`） |
| `battle_heal` | `Assets\FMODStudioCache.asset` | 4 | no |
| `battle_heal` | `Assets\Resources\Data\Localization\Heroes.xml` | 16 | no |
| `battle_heal` | `Assets\Resources\Data\Upgrades\Heroes\crusader.upgrades.json` | 5 | no |
| `battle_heal` | `Assets\Resources\Prefabs\Heroes\crusader.prefab` | 4 | no（`battle_heal` + `battle_heal_target`） |
| `inspiring_cry` | `Assets\Resources\Data\Heroes\Info\Crusader.bytes` | 6 | **YES**（`.heal` + `.effect`） |
| `inspiring_cry` | `Assets\FMODStudioCache.asset` | 4 | no |
| `inspiring_cry` | `Assets\Resources\Data\Localization\Heroes.xml` | 16 | no |
| `inspiring_cry` | `Assets\Resources\Data\Upgrades\Heroes\crusader.upgrades.json` | 5 | no |
| `inspiring_cry` | `Assets\Resources\Prefabs\Heroes\crusader.prefab` | 2 | no |

**带数值的文件（对 5 个 id 求并集）只有 4 个**：

1. `Assets\Resources\Data\Heroes\Info\Crusader.bytes`（5 个 id 全部）
2. `Assets\Resources\Data\Monsters\necromancer_A.txt`、`necromancer_B.txt`、`necromancer_C.txt`（只因为 `unholy_smite` 含子串 `smite`，**不是** Crusader 的 `smite`）

### 2.2 含数值命中的逐行原文（≤200 字符）

`Assets\Resources\Data\Heroes\Info\Crusader.bytes`：

```
L5   combat_skill: .id "smite" .icon "one" .anim "attack_sword" .fx "smite" .targchestfx "blood_splatter"
L6   combat_skill: .id "zealous_accusation" .icon "two" .anim "attack_scroll" .fx "zealous_accusation" .targchestfx "blood_splatter"
L9   combat_skill: .id "battle_heal" .icon "five" .anim "attack_heal" .fx "battle_heal" .targfx "battle_heal_target"
L10  combat_skill: .id "holy_lance" .icon "six" .anim "attack_charge" .fx "holy_lance" .targchestfx "blood_splatter"
L11  combat_skill: .id "inspiring_cry" .icon "seven" .anim "attack_banner" .fx "inspiring_cry"
L26  combat_skill: .id "smite" .level 0 .type "melee" .atk 85% .dmg 0% .crit 0% .launch 21 .target 12 .is_crit_valid True  .effect "Unholy Killer 1" .generation_guaranteed true
L27  combat_skill: .id "smite" .level 1 .type "melee" .atk 90% .dmg 0% .crit 0% .launch 21 .target 12 .is_crit_valid True  .effect "Unholy Killer 2"
L28  combat_skill: .id "smite" .level 2 .type "melee" .atk 95% .dmg 0% .crit 1% .launch 21 .target 12 .is_crit_valid True  .effect "Unholy Killer 3"
L29  combat_skill: .id "smite" .level 3 .type "melee" .atk 100% .dmg 0% .crit 2% .launch 21 .target 12 .is_crit_valid True  .effect "Unholy Killer 4"
L30  combat_skill: .id "smite" .level 4 .type "melee" .atk 105% .dmg 0% .crit 2% .launch 21 .target 12 .is_crit_valid True  .effect "Unholy Killer 5"
L31  combat_skill: .id "zealous_accusation" .level 0 .type "ranged" .atk 85% .dmg -40% .crit 0% .launch 21 .target ~12 .is_crit_valid True
L32  combat_skill: .id "zealous_accusation" .level 1 .type "ranged" .atk 90% .dmg -40% .crit 0% .launch 21 .target ~12 .is_crit_valid True
L33  combat_skill: .id "zealous_accusation" .level 2 .type "ranged" .atk 95% .dmg -40% .crit 1% .launch 21 .target ~12 .is_crit_valid True
L34  combat_skill: .id "zealous_accusation" .level 3 .type "ranged" .atk 100% .dmg -40% .crit 2% .launch 21 .target ~12 .is_crit_valid True
L35  combat_skill: .id "zealous_accusation" .level 4 .type "ranged" .atk 105% .dmg -40% .crit 2% .launch 21 .target ~12 .is_crit_valid True
L47  combat_skill: .id "battle_heal" .level 0 .heal 2 2 .launch 12 .target @123
L48  combat_skill: .id "battle_heal" .level 1 .heal 2 3 .launch 12 .target @123
L49  combat_skill: .id "battle_heal" .level 2 .heal 3 4 .launch 12 .target @123
L50  combat_skill: .id "battle_heal" .level 3 .heal 4 4 .launch 12 .target @123
L51  combat_skill: .id "battle_heal" .level 4 .heal 5 6 .launch 12 .target @123
L52  combat_skill: .id "holy_lance" .level 0 .type "melee" .atk 85% .dmg 0% .crit 5% .move 0 1 .launch 34 .target 34 .is_crit_valid True  .effect "Unholy Killer 1"
L53  combat_skill: .id "holy_lance" .level 1 .type "melee" .atk 90% .dmg 0% .crit 6% .move 0 1 .launch 34 .target 34 .is_crit_valid True  .effect "Unholy Killer 2"
L54  combat_skill: .id "holy_lance" .level 2 .type "melee" .atk 95% .dmg 0% .crit 6% .move 0 1 .launch 34 .target 34 .is_crit_valid True  .effect "Unholy Killer 3"
L55  combat_skill: .id "holy_lance" .level 3 .type "melee" .atk 100% .dmg 0% .crit 6% .move 0 1 .launch 34 .target 34 .is_crit_valid True  .effect "Unholy Killer 4"
L56  combat_skill: .id "holy_lance" .level 4 .type "melee" .atk 105% .dmg 0% .crit 7% .move 0 1 .launch 34 .target 34 .is_crit_valid True  .effect "Unholy Killer 5"
L57  combat_skill: .id "inspiring_cry" .level 0 .heal 1 1 .launch 1234 .target @1234  .effect "Crusader HealStress 1" "Crusader Light 1"
L58  combat_skill: .id "inspiring_cry" .level 1 .heal 1 1 .launch 1234 .target @1234  .effect "Crusader HealStress 2" "Crusader Light 2"
L59  combat_skill: .id "inspiring_cry" .level 2 .heal 1 2 .launch 1234 .target @1234  .effect "Crusader HealStress 3" "Crusader Light 3"
L60  combat_skill: .id "inspiring_cry" .level 3 .heal 1 2 .launch 1234 .target @1234  .effect "Crusader HealStress 4" "Crusader Light 4"
L61  combat_skill: .id "inspiring_cry" .level 4 .heal 2 2 .launch 1234 .target @1234  .effect "Crusader HealStress 5" "Crusader Light 5"
```

`Assets\Resources\Data\Monsters\necromancer_A.txt` / `_B.txt` / `_C.txt`（怪兽技能 `unholy_smite`，非 hero `smite`）：

```
A L6   skill: .id "unholy_smite" .anim "attack_melee" .fx "smite" .targchestfx "blood_splatter" .area_pos_offset 0 -50
A L17  skill: .id "unholy_smite" .type "melee" .atk 102.5% .dmg 4 8 .crit 6%  .effect "NecroSummon 1" .launch 1234 .target ~12 .move 1 0
B L17  skill: .id "unholy_smite" .type "melee" .atk 108.75% .dmg 5 11 .crit 11%  .effect "NecroSummon 2" .launch 1234 .target ~12 .move 1 0
C L17  skill: .id "unholy_smite" .type "melee" .atk 122.5% .dmg 8 16 .crit 12%  .effect "NecroSummon 3" .launch 1234 .target ~12 .move 1 0
```

### 2.3 不含数值的命中（逐行 / 行号）

| 文件 | 行号 | 原文（截断 200） |
| --- | --- | --- |
| `Assets\FMODStudioCache.asset` | 3646, 9659, 44737, 79868（crusader_smite） | `Path: event:/char/ally/crusader_smite` |
| 同上 | 23880, 55082（skeleton_bishop） / 46298, 64473 | `Path: event:/char/enemy/skeleton_bishop_unholy_smite(_miss)` |
| 同上 | 64578, 88982 / 54124, 82232 | `Path: event:/char/enemy/necromancer_unholy_smite(_miss)` |
| 同上 | 8454, 51545, 56267, 87203 | `Path: event:/char/ally/crusader_zealous_accusation(_miss)` |
| 同上 | 41213, 44581, 46361, 77567 | `Path: event:/char/ally/crusader_holy_lance(_miss)` |
| 同上 | 5819, 51314, 52114, 63672 | `Path: event:/char/ally/crusader_battle_heal(_miss)` |
| 同上 | 6551, 47514, 61990, 88241 | `Path: event:/char/ally/crusader_inspiring_cry(_miss)` |
| `Assets\Resources\Data\Upgrades\Heroes\crusader.upgrades.json` | `smite`: 123,147,160,173,186 / `zealous_accusation`: 194,218,231,244,257 / `holy_lance`: 478,502,515,528,541 / `battle_heal`: 407,431,444,457,470 / `inspiring_cry`: 549,573,586,599,612 | `"id" : "crusader.smite",` 与 `{ "tree_id" : "crusader.smite", "requirement_code" : "0" },` |
| `Assets\Resources\Prefabs\Heroes\crusader.prefab` | 67/1789（inspiring_cry）、102/1714（smite）、281/2302、403/2057（battle_heal）、350/2129（holy_lance）、493/2081（zealous_accusation） | `m_Name: smite` / `_animationName: smite` |
| `Assets\Resources\Prefabs\Monsters\necromancer.prefab` | 157, 1062 | `m_Name: smite` / `_animationName: smite` |
| `Assets\Scripts\Setup\SaveSystem\SaveCampaignData.cs` | 188, 189 | `InstancedPurchases[1]["crusader.smite"].PurchasedUpgrades.Add("0");` |
| `Assets\Resources\Data\Localization\Heroes.xml` | 每个 id 16 行，如 `smite`: 19,27,652,660,1285,1293,1918,1926,2551,2559,3184,3192,3817,3825,4450,4458 | `<entry id="combat_skill_name_crusader_smite"><![CDATA[Smite]]></entry>` |
| `Assets\Resources\Data\Localization\Monsters.xml` | 23,455,887,1319,1751,2183,2615,3047 | `<entry id="str_monster_skill_unholy_smite">…</entry>` |
| `Assets\Resources\Data\Localization\Dialogue.xml` | 291, 4116, 4855–4872, 11158–11175, 17458–17475, 23757–23774, 30056–30073, 36355–36372, 42654–42671, 48953–48970（共 146 行） | `<entry id="crusader+str_afflicted_abusive"><![CDATA[Stand in my way and I will smite you down!]]></entry>` |

> `Dialogue.xml` 的 146 行全部是 `<![CDATA[…]]>` 台词文本（8 种语言 × 18 行），不含任何字段名或数值。
> **Q1 全量原始命中表（含每一行行号+原文，250 行）**：`_q1_out.txt`。

### 2.4 连带命中（通过 `.effect "效果名"` 反查到的数值文件）

`smite` / `holy_lance` 的 `.effect "Unholy Killer N"`、`inspiring_cry` 的 `.effect "Crusader HealStress N" "Crusader Light N"` 指向：

- `Assets\Resources\Data\Mechanics\Effects.txt` — **含数值**（`.damage_low_multiply` / `.damage_high_multiply` / `.healstress` / `.torch_increase`）

```
L216  effect: .name "Unholy Killer 1" .target "performer" .chance 100% … .monsterType "unholy" .combat_stat_buff 1 .damage_low_multiply 15%	.damage_high_multiply 15%	.on_hit true .on_miss false
L220  effect: .name "Unholy Killer 5" … .damage_low_multiply 35%	.damage_high_multiply 35%	.on_hit true .on_miss false
L681  effect: .name "Crusader HealStress 1" .target "target" .curio_result_type "positive" .chance 100% .healstress 6		.on_hit true .on_miss false .queue true
L691  effect: .name "Crusader Light 5" .target "global" .chance 100% .curio_result_type "positive" .torch_increase 10	.on_hit true .on_miss true .apply_once true
```

---

## 3. Q2：`Heroes\Info\*.bytes` 的 key 与记录前缀统计

脚本：`_q2_skills_keys.py`（逐行找 `^prefix:`，用引号感知的空白切分，取形如 `^\.[A-Za-z_]\w*$` 的 token 作为 key）。
样本：15 个文件全部解析成功，每个文件 `combat_skill:` 记录数完全一致（art 7 + stat 35 = 42）。

### 3.1 `combat_skill:` 里出现过的所有 `.key`（频次）

`combat_skill:` 记录总数 **630**；key 出现总次数 5661；不同 key **28** 个。

| key | 频次 | 属于哪一块 |
| --- | --- | --- |
| `.id` | 630 | art + stat |
| `.level` | 525 | stat |
| `.launch` | 525 | stat |
| `.target` | 525 | stat |
| `.type` | 485 | stat |
| `.atk` | 485 | stat |
| `.dmg` | 485 | stat |
| `.crit` | 485 | stat |
| `.is_crit_valid` | 485 | stat |
| `.effect` | 430 | stat |
| `.icon` | 105 | art |
| `.anim` | 105 | art |
| `.fx` | 96 | art |
| `.targchestfx` | 62 | art |
| `.move` | 60 | stat |
| `.heal` | 40 | stat |
| `.valid_modes` | 35 | stat |
| `.targfx` | 22 | art |
| `.self_target_valid` | 15 | stat |
| `.generation_guaranteed` | 14 | stat |
| `.area_pos_offset` | 12 | art |
| `.target_area_pos_offset` | 8 | art |
| `.human_effects` | 5 | stat |
| `.beast_effects` | 5 | stat |
| `.is_continue_turn` | 5 | stat |
| `.per_turn_limit` | 5 | stat |
| `.per_battle_limit` | 5 | stat |
| `.targheadfx` | 2 | art |

拆开看两块（判别条件：该行是否含 `.level`）：

- **art 记录（无 `.level`）共 105 条**，key 出现 517 次，9 个 key：
  `.id x105, .icon x105, .anim x105, .fx x96, .targchestfx x62, .targfx x22, .area_pos_offset x12, .target_area_pos_offset x8, .targheadfx x2`
- **stat 记录（含 `.level`）共 525 条**，key 出现 5144 次，20 个 key：
  `.id x525, .level x525, .launch x525, .target x525, .type x485, .atk x485, .dmg x485, .crit x485, .is_crit_valid x485, .effect x430, .move x60, .heal x40, .valid_modes x35, .self_target_valid x15, .generation_guaranteed x14, .human_effects x5, .beast_effects x5, .is_continue_turn x5, .per_turn_limit x5, .per_battle_limit x5`

> 注意：`.dmg` 只在 stat 记录里出现（485 条）。art 记录里**没有**任何数值字段——这正回答了"为什么 `.id "smite" .icon "one" …` 那一行看起来没有数值"。

### 3.2 `combat_skill:` 记录数 / 文件（15 个文件完全一致）

```
Abomination.bytes      art=7 stat=35 total=42
Antiquarian.bytes      art=7 stat=35 total=42
Arbalest.bytes         art=7 stat=35 total=42
BountyHunter.bytes     art=7 stat=35 total=42
Crusader.bytes         art=7 stat=35 total=42
GraveRobber.bytes      art=7 stat=35 total=42
Hellion.bytes          art=7 stat=35 total=42
Highwayman.bytes       art=7 stat=35 total=42
HoundMaster.bytes      art=7 stat=35 total=42
Jester.bytes           art=7 stat=35 total=42
Leper.bytes            art=7 stat=35 total=42
ManAtArms.bytes        art=7 stat=35 total=42
Occultist.bytes        art=7 stat=35 total=42
PlagueDoctor.bytes     art=7 stat=35 total=42
Vestal.bytes           art=7 stat=35 total=42
```

（art 7 = 7 个技能各 1 条表现记录；stat 35 = 7 个技能 × 5 个 `.level`。）

### 3.3 其他记录前缀（15 个文件合计，总记录 987 条）

| 前缀 | 频次 |
| --- | --- |
| `combat_skill:` | 630 |
| `weapon:` | 75 |
| `armour:` | 75 |
| `tag:` | 30 |
| `name:` | 15 |
| `art:` | 15 |
| `commonfx:` | 15 |
| `info:` | 15 |
| `resistances:` | 15 |
| `combat_move_skill:` | 15 |
| `deaths_door:` | 15 |
| `controlled:` | 15 |
| `id_index:` | 15 |
| `generation:` | 15 |
| `skill_selection:` | 15 |
| `riposte_skill:` | 5 |
| `mode:` | 2 |
| `rendering:` | 1 |
| `incompatible_party_member:` | 1 |
| `extra_battle_loot:` | 1 |
| `extra_curio_loot:` | 1 |
| `extra_stack_limit:` | 1 |

每文件差异只在：`mode:` x2 + `rendering:` + `incompatible_party_member:`（Abomination）；`extra_battle_loot:` / `extra_curio_loot:` / `extra_stack_limit:`（Antiquarian）；`riposte_skill:`（BountyHunter x1、Highwayman x2、ManAtArms x2）。其余 15 个前缀在 15 个文件里都恰好各出现 1 次（`weapon:`/`armour:` 各 5 次、`tag:` 2 次）。

### 3.4 其他前缀用到的 key（带频次）

| 前缀 | 记录数 | key（频次） |
| --- | --- | --- |
| `resistances:` | 15 | `.stun x15, .poison x15, .bleed x15, .disease x15, .move x15, .debuff x15, .death_blow x15, .trap x15` |
| `weapon:` | 75 | `.name x75, .atk x75, .dmg x75, .crit x75, .spd x75, .upgradeRequirementCode x60` |
| `armour:` | 75 | `.name x75, .def x75, .prot x75, .hp x75, .spd x75, .upgradeRequirementCode x60` |
| `generation:` | 15 | `.number_of_positive_quirks_min/max, .number_of_negative_quirks_min/max, .number_of_class_specific_camping_skills, .number_of_shared_camping_skills, .number_of_random_combat_skills`（各 x15） |
| `skill_selection:` | 15 | `.can_select_combat_skills x15, .number_of_selected_combat_skills_max x15` |
| `deaths_door:` | 15 | `.buffs x15, .recovery_buffs x15, .recovery_heart_attack_buffs x15` |
| `commonfx:` | 15 | `.deathfx x15` |
| `controlled:` | 15 | `.target_rank x15` |
| `id_index:` | 15 | `.index x15` |
| `tag:` | 30 | `.id x30` |
| `art:` | 15 | （无 `.key`，纯节标题） |
| `info:` | 15 | （无 `.key`，纯节标题） |
| `name:` | 15 | （无 `.key`，值是英雄名） |
| `combat_move_skill:` | 15 | `.id x15, .level x15, .type x15, .move x15, .launch x15` |
| `riposte_skill:` | 5 | `.id x5, .level x3, .type x3, .atk x3, .dmg x3, .crit x3, .launch x3, .target x3, .is_crit_valid x3, .anim x2, .fx x2, .targchestfx x2` |
| `mode:` | 2 | `.id x2, .is_raid_default x1, .bark_override_id x1, .affliction_combat_skill_id x1, .battle_complete_combat_skill_id x1, .stress_damage_per_turn x1` |
| `incompatible_party_member:` | 1 | `.id x1, .hero_tag x1` |
| `rendering:` | 1 | `.sort_position_z_rank_override x1` |
| `extra_battle_loot:` | 1 | `.code x1, .count x1` |
| `extra_curio_loot:` | 1 | `.id x1` + `.code x1, .count x1` |
| `extra_stack_limit:` | 1 | `.id x1` |

> Q2 全量原始输出：`_q2_out.txt`（265 行）。

---

## 4. Q3：有没有"技能伤害百分比"这类字段？

**有。** 分两层，另有若干"百分数但不是伤害"的字段。

### 4.1 字段 A：`.dmg` —— 技能级伤害修正（百分数）

| 属性 | 值 |
| --- | --- |
| 字段名 | `.dmg` |
| 出现文件 | `Assets\Resources\Data\Heroes\Info\<Hero>.bytes`（15 个文件全覆盖）；**只在带 `.level` 的 `combat_skill:` 记录里** |
| 覆盖记录数 | `combat_skill:` stat 记录中 **485** 条；整个 `Info\*.bytes` 里 `.dmg` 共 563 次，其中百分数形式 **488** 次 |
| 取值样例 1 | `Crusader.bytes:26` → `.id "smite" .level 0 … .atk 85% .dmg 0% .crit 0% …` |
| 取值样例 2 | `Crusader.bytes:31` → `.id "zealous_accusation" .level 0 .type "ranged" .atk 85% .dmg -40% .crit 0% …`（5 个 level 全是 `-40%`） |
| 取值样例 3 | `Crusader.bytes:36` → `.id "stunning_blow" .level 0 … .dmg -75% …` |

其余 563 − 485 − 3(`riposte`) = 75 次 `.dmg` 是 `weapon:` 记录的**平值**（`Crusader.bytes:16` → `weapon: .name "crusader_weapon_0" .atk 0% .dmg 6 12 .crit 5% .spd 1`），与"技能伤害百分比"无关。

**语义事实（有代码证据）**：`.dmg` 是"伤害修正/倍率"，不是伤害绝对值；`0%` 表示"无修正"，不是 0 伤害：

- `Assets\Scripts\Mechanics\Skills\CombatSkill.cs:283-290`：hero 走 `DamageMod = float.Parse(data[++i]) / 100;`；怪兽走 `DamageMin/DamageMax`（平值）。
- `Assets\Scripts\Mechanics\Battle\BattleSolver.cs:443-448`：`performer.MinDamage * (1 + skill.DamageMod)`（hero）/ `skill.DamageMin * performer.DamageMod`（monster）。
- `Assets\Resources\Data\Localization\Misc.xml:1084`：该字段的 UI 文案 id 是 `str_skill_tooltip_dmg_mod`，值 `DMG mod: {0}%`（即"伤害修正 %"）。

### 4.2 字段 B：`.damage_low_multiply` / `.damage_high_multiply` —— 效果级伤害倍率（百分数）

| 属性 | 值 |
| --- | --- |
| 字段名 | `.damage_low_multiply` 与 `.damage_high_multiply`（成对出现） |
| 出现文件 | `Assets\Resources\Data\Mechanics\Effects.txt` |
| 覆盖记录数 | 各 **139** 条 `effect:` 记录（139/139 全部是百分数形式） |
| 取值样例 1 | `Effects.txt:216` → `effect: .name "Unholy Killer 1" … .damage_low_multiply 15% .damage_high_multiply 15% …`（被 `smite`/`holy_lance` 的 `.effect` 引用） |
| 取值样例 2 | `Effects.txt:220` → `effect: .name "Unholy Killer 5" … .damage_low_multiply 35% .damage_high_multiply 35% …` |
| 取值样例 3 | `Effects.txt:367` → `effect: .name "Damage Buff 1" .target "target" … .damage_low_multiply 15% .damage_high_multiply 15% …` |
| 取值样例 4 | `Effects.txt:755` → `effect: .name "Hellion Exhaust" … .damage_low_multiply -20% .damage_high_multiply -20% …`（负值存在） |
| 解析代码 | `Assets\Scripts\Mechanics\Skills\Effect.cs:707`（`.damage_low_multiply`）、`:721`（`.damage_high_multiply`） |

### 4.3 其他百分数（不是伤害，列出以免误认）

| 字段名 | 文件 | 覆盖记录数 | 样例 |
| --- | --- | --- | --- |
| `.atk` | `Info\*.bytes`（stat 记录） | 485（另有 `weapon:` 75、`riposte_skill:` 3，共 563，全部含 `%`） | `Crusader.bytes:26` → `.atk 85%` |
| `.crit` | `Info\*.bytes` | 563（全含 `%`） | `Crusader.bytes:28` → `.crit 1%` |
| `.attack_rating_add` | `Mechanics\Effects.txt` | 95（全含 `%`） | `Effects.txt:361` → `.attack_rating_add 4%` |
| `.crit_chance_add` | `Mechanics\Effects.txt` | 67（全含 `%`） | `Effects.txt:361` → `.crit_chance_add 5%` |
| `.chance` | `Mechanics\Effects.txt` | 941（全含 `%`） | `Effects.txt:381` → `.chance 110%` |
| `.resistances` 系列（`.stun/.poison/…`） | `Info\*.bytes` | 各 15 | `Crusader.bytes:15` → `.stun 40% …` |
| `.def` | `Info\*.bytes`（`armour:` 记录） | 75（全含 `%`） | `Crusader.bytes:21` → `.def 5% …` |

### 4.4 非百分数的伤害/治疗数值（对照）

| 字段名 | 文件 | 覆盖记录数 | 样例 |
| --- | --- | --- | --- |
| `.dmg`（平值 min/max） | `Assets\Resources\Data\Monsters\*.txt`（230 个文件，`skill:` 记录 974 条） | 484 条为平值、**0** 条为百分数 | `necromancer_A.txt:17` → `.atk 102.5% .dmg 4 8 .crit 6%` |
| `.dmg`（平值 min/max） | `Info\*.bytes` 的 `weapon:` 记录 | 75 | `Crusader.bytes:16` → `.dmg 6 12` |
| `.heal`（平值 min/max） | `Info\*.bytes` 的 stat 记录 | 40 | `Crusader.bytes:51` → `.id "battle_heal" .level 4 .heal 5 6 .launch 12 .target @123` |
| `.healstress` | `Mechanics\Effects.txt` | 52 | `Effects.txt:685` → `.healstress 10` |
| `.torch_increase` | `Mechanics\Effects.txt` | 19 | `Effects.txt:691` → `.torch_increase 10` |

### 4.5 明确"没有找到"的部分

我用以下关键词全项目（`Assets\**`，含 `.cs .json .bytes .txt .asset .prefab .xml`）搜过，**没有**找到以这些名字命名的字段：

```
\.dmg%    dmg_mod    damage_mod    dmg_mult    \.atk%    damage_multiply
dmg_multiplier    damage_multiplier
```

唯一带 `dmg_mod` 字样的命中是**本地化字符串 id**（不是数据字段）：`Assets\Resources\Data\Localization\Misc.xml:1084` 的 `str_skill_tooltip_dmg_mod`（8 种语言共 9 行，`DMG mod: {0}%` 等）。

另外确认：

- `rg "damage_low_multiply" -g "*.json" Assets\Resources\Data` → **无命中**（效果倍率只存在于 `Effects.txt`，不在任何 JSON 里）。
- `rg '"dmg"|"atk"|"damage|"crit|DamageMod|"heal"' Assets\Resources\Data\Upgrades\Heroes\crusader.upgrades.json` → **无命中**（升级树 JSON 里没有任何伤害字段）。
- `rg "damage|Damage|_atk|skillId|SkillId" Assets\Resources\Prefabs\Heroes\crusader.prefab` → **无命中**（prefab 里只有 GameObject 名与动画名）。

---

## 5. 参考项目自身的解析路径（事实记录，只给行号）

| 位置 | 事实 |
| --- | --- |
| `Assets\Scripts\Character\HeroClass.cs:121-146` | `case "combat_skill:"` → 把整行按 `"` 切开、去掉 `%`、再按空白切，构造 `new CombatSkill(combatData, true)`。art 块与 stat 块走同一条路径。 |
| `Assets\Scripts\Mechanics\Skills\CombatSkill.cs:265-398` | `LoadData`：`.level/.type/.atk/.dmg/.crit/.launch/.target/.is_crit_valid/.self_target_valid/.extra_targets_chance/.can_miss/.is_continue_turn/.per_turn_limit/.per_battle_limit/.valid_modes/.human_effects/.beast_effects/.effect/.generation_guaranteed/.is_knowledgeable/.heal/.move` |
| `Assets\Scripts\Mechanics\Skills\CombatSkill.cs:280-290` | `.atk` → `Accuracy = v/100`；`.dmg` 依 `isHeroSkill` 分叉：hero → `DamageMod = v/100`，monster → `DamageMin/DamageMax` |
| `Assets\Scripts\Mechanics\Skills\CombatSkill.cs:12-14` | 属性 `DamageMin / DamageMax / DamageMod` |
| `Assets\Scripts\Mechanics\Skills\CombatSkill.cs:136-138` | Tooltip 用 `str_skill_tooltip_dmg_mod` 显示 `DamageMod * 100` |
| `Assets\Scripts\Mechanics\Battle\BattleSolver.cs:384-385, 443-448` | 伤害计算：hero `MinDamage * (1 + DamageMod)`；monster `DamageMin * DamageMod` |
| `Assets\Scripts\Mechanics\Skills\Effect.cs:707, 721` | `.damage_low_multiply` / `.damage_high_multiply` 的解析分支 |
| `Assets\Scripts\Character\Character.cs:126-152` | `MinDamage / MaxDamage / DamageMod` 属性 |

---

## 6. 边界与不确定项

1. **`.dmg 0%` 的含义**：`Crusader.bytes` 里 `smite` 的 5 个 level 全是 `.dmg 0%`，`holy_lance` 也全是 `0%`。按 `BattleSolver.cs:444` 的 `(1 + DamageMod)`，`0%` = 无修正（即 100% 武器伤害）。这是从代码推导的事实，不是文件里的字面说明。
2. **`unholy_smite` 混淆**：`smite` 的 grep 命中里有 12 处来自怪兽 `unholy_smite`（necromancer / skeleton_bishop），与 Crusader 的 `smite` 无关；报告已分开标注。
3. **`.effect` 的数值不在 hero 文件里**：`Unholy Killer`（条件性 `monsterType "unholy"` 增伤）与 `Crusader HealStress/Light` 的具体百分比/数值全在 `Mechanics\Effects.txt`，hero `.bytes` 里只有效果名。
4. **未检查项**：本次没有遍历 `Assets\Resources\Data\Dungeons`、`Maps`、`Curios`、`Trinkets` 等目录去反查是否还有引用这些效果名的其它文件——Q1 要求的 5 个技能 id 维度已经全项目覆盖，但"效果名"维度只查了 `Unholy Killer` / `Crusader HealStress` / `Crusader Light` 三个前缀。
5. **`riposte_skill:` 里也有 `.atk/.dmg/.crit`**（BountyHunter / Highwayman / ManAtArms 共 5 条），说明"技能数值"不止 `combat_skill:` 一种前缀；但它不属于本次 Q1 的 5 个 id。
