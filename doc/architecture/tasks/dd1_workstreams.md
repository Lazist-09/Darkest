# DD1 阶段 A（复现）· 任务模块拆分与派发矩阵

> **编号**：ARCH-T-DD1-WORKSTREAMS-01 · **建立**：v2.09（2026-09-20）· **依据**：策划 `#416`~`#439`（`doc/modules/dd1_baseline.md §0~§28`）
> **用途**：把策划已裁定的目标**拆成可派发的模块**（含**依赖 / 验收 / 不做项**）⇒ 🔴 **主程序与 UI 照此排期** ✓
> 📌 **判据来源**：`dd1_conformance.md`（体检表 15 条）· `doc/modules/trinkets.md`（Trinket 规格）· 各条裁定号

---

## §1 依赖图（**先看这个** —— 顺序错会返工）

```
🔴 M1 属性/伤害模型（扩模型 → 换伤害公式）
     ├─ M1a 加字段：抗性 5→8（poison/disease/trap + 各一条判定结算路径）· 合并 PhysDef+Dodge⇒def · 补 prot
     ├─ M1b 支持"按阶取属性"（units.json 加 weapon/armour 各 5 阶 + UnitStats/Mapper）
     └─ M1c 🔴 换伤害公式（武器区间 ×（1+技能 dmg%））← **换模型级 · 走解冻口径**
   ⇒ 🔴 **M2 依赖 M1**（buff 原语映射需要"属性模型有落点"：现估 **76%**，补完抗性 8 种后更高）
        └─ M2 buff 原语层（2020 条 + 翻译器 + 未映射清单）
             ⇒ 🔴 **M4 依赖 M2**（Trinket 的 buffs 引用必须存在）
        └─ M3 22 条 buff 三类处置（依赖 M1 的"态"与 M2 的"原语"）
   ✅ **可并行**：M5 Quirk（从零建）· M6 建筑结构 · M7 名册招募 · M8 职业 15 · M9 4v4 · M10 UIRoot · M11 工程基线
   📌 **M1 是总闸**（它挡住 M2/M4/M3，且改动 157 个测试与全部平衡读数）
```

---

## §2 模块清单（**主程序域**）

| # | 模块 | 内容（来源裁定） | 验收（可测） | 依赖 |
|---|---|---|---|---|
| **M1a** | **属性字段扩展** | ✅ 抗性 **5→8** 种（`poison`/`disease`/`trap`；`death_blow` 已有 `DeathsDoorResist`）⇒ ⚠️ **各要一条【判定+结算】路径**（不是加数据）· ✅ **合并 `PhysDef`+`Dodge` ⇒ 单一 `def`** · ✅ **补 `prot`**（`#439` ②③） | 单测：8 种抗性各有判定/结算用例 · **`prot` 参与减伤** · ✅ **合并 `def` 前后【命中率读数对照】**（`#439` ③ 要求先确认"闪避判定用哪个"）⚠️ | — |
| **M1b** | **按阶取属性** | `units.json` 加 `weapon`/`armour` 各 **5 阶**（`atk%`/`dmg min-max`/`crit%`/`spd` ／ `def%`/`prot`/`hp`/`spd`）· `UnitStats`/`UnitStatsMapper` 支持按阶（现为**压平终值**）（`#439` ①） | 校验：阶数 ∈ 1~5 · 缺阶报错 · **旧字段仍可用（零行为改动）** | M1a |
| **M1c** | 🔴 **换伤害模型** | `damage = 武器区间 ×（1 + 技能 dmg%）`（**武器是底、技能是修正**）· `DamagePipeline` 重写那一段 · `skills.json` 的 `damage.segments ⇒ dmg%`（`#439` ①） | 🔴 **解冻口径四件**：① 逐条登记（+ 原版证据）② 一次只改一类 ③ **前后读数对照**（A1/A2/V10）④ 改完回冻结 · **公式单测复算** | M1b |
| **M2** | **buff 原语层** | 🆕 `data/dd1_buffs.json`（原版 **2020 条** · 11 字段 · **只读对齐** · `origin:dd1`）· 🆕 `BuffPrimitiveTranslator`（原语 → 我方 `modifiers`/`duration`/条件）· 🆕 **未映射清单（必须可见）** + 🆕 **P 校验**："每条原语要么被引用、要么进未映射清单"（`#433`/`#436`/`#437`） | 翻译器单测（抽样 20 条 · 双向）· **未映射清单可打印** · 门禁 `check_godot_refs` 仍 OK（**内核零 Godot**） | M1 |
| **M3** | **22 条 buff 三类处置** | ① **折磨/美德 7** ⇒ `traits.json`（`TraitsConfig` · **无 Ledger**）② **机制态 10** ⇒ **换原版对应态**（⚠️ **先量原版落在哪张表**）③ **自加 5** ⇒ 废掉或按红线 30 移层（`#434` ③） | 🔴 **"搬家必须带行为"**（纪律 AA）：**8 文件约 20 处消费点一起改** + **前后读数**（士气/折磨） | M1/M2 |
| **M4** | **Trinket** | 🆕 `data/trinkets.json`（**196 条** · 7 字段）· 🆕 `TrinketsConfig`（**两件套，不要 Ledger** —— 装备关系归 `Roster`）· 槽位 **2/英雄** · 职业限制 · `price ≤ 1` 不可购买（`#437` · `doc/modules/trinkets.md`） | **T1~T6**（196 条 7 字段 · 引用必须存在于原语层 · 槽位 2 · 职业限制可断言 · 26 条不可购 · 🔴 **T6：装 2 件后属性/行为真的变了**） | M2 |
| **M5** | **Quirk 系统（从零建）** | schema（`is_positive`/`is_disease`/`classification`/`incompatible_quirks`/`curio_tag`）· 库（**170 条**，先 ~20）· **互斥必须可断言** · `curio_tag` 引用校验（`#425` ⑥ / `#429` ②） | 新 P 校验（**互斥对称** · 疾病必须 `is_disease` · `curio_tag` 引用存在）· **"同时带 A 与 B"⇒ 必须拒绝** | — |
| **M6** | **建筑结构对齐** | `upgrade_paths[]` ⇒ `buildings[]{id, trees[]{id, tags[], levels[]{code, currency_cost[], prerequisites[]}}}` · 🆕 **`prerequisite_requirements`（现在完全没有）** · `axis` 保留但标"非原版概念"（`#421`） | 新 P 校验：**前置引用存在 · 不得成环 · 允许跨树前置** · 内容分批（批1 tavern+abbey / 批2 stage_coach / 批3 blacksmith·guild·sanitarium / 批4 camping_trainer） | — |
| **M7** | **名册/招募对齐** | ① **上限单一来源 = 马车**（**9→12→16→20→24→28**）⇒ **删 `unlocks.roster_cap_delta`** · **硬上限 12⇒28（6 处硬编码收敛）** ② **招募刷新 2~7 人**（🔴 **必须确定性：走 `IRngProvider`，不许 `Random`**）③ **高级新兵三档**（18.75%/12.5%/6.25%）＋ `first_hero_classes [plague_doctor, vestal]`（`#423`） | 上限：**改马车第 3 级 ⇒ 只有一处跟着变** · 刷新：**同 seed 同结果** · 高级新兵：**可复算**（"为什么是 Lv2 带某 Quirk"能从 seed 复算）· ⚠️ **高级新兵依赖 M5（Quirk）** | M5（③） |
| **M8** | **职业 15 个** | `units.json`/`skills.json` 扩 **原版 15 职业**（`origin:dd1`）· 我方 4 职业与 36 技能 **保留**（`origin:ours`）· 起点 **crusader/vestal/plague_doctor**（`#426`/`#428`/`#429`） | 每批：**构建绿 + `--ui-audit` + 平衡读数** · 名册/技能栏可开 · 头衔/美术占位齐（`P31`） | M1（属性）/M2（buff） |
| **M9** | **4v4 槽位** | `SlotLayout` **降级：`SupportSlots` ⇒ 通用扩展位（可为空）** · `formation.json` 我方 4 槽 · **SP 去掉"支援位"维度**（按技能声明；`Skill.SupportPointCost ?? 0`）· 保留 `P19 regen_per_round ≥ 1`（`#419`/`O-60′`） | 两侧对称 4 槽 · **契约仍能表达 6 槽**（回归用例）· SP：未声明不扣 / 声明才扣 | — |
| **M11** | **工程基线** | ① **6 个超限文件按职责拆**（边界见 `file_size_split.md §1.2`）② 🆕 `check_no_external_assets.py`（**B6：发行包不得含 E 盘提取物**）③ **autoload 恢复** ④ **CI 接门禁**（`file_size`/`godot_refs`/`data_discipline`/`namespace`/`placeholders`） | `check_file_size.py` ⇒ **0 超限** · `selfcheck` **8/8** · B6 门禁 **双向自检** | — |

---

## §3 模块清单（**UI 域**）

| # | 模块 | 内容 | 验收 |
|---|---|---|---|
| **M4u** | **Trinket UI** | 装备位（**2 槽/英雄**）· 详情/悬停 · 不可购买/职业不符的**置灰 + 理由**（`HamletRoot.HeroDetail.cs` 现有 10 处占位 ⇒ 真接线） | 🔴 **非本职业 ⇒ 置灰且报错不静默**（T4）· `--ui-audit` 0 重叠/0 透明 |
| **M5u** | **Quirk UI** | 名册/英雄详情显示 Quirk（正/负/疾病分类）· 互斥提示 | 显示与数据一致 · 悬停出完整信息 |
| **M6u** | **建筑升级树 UI** | 树 × 级（**`code` 不是数字 level**）· 前置显示 · 多货币 | 每级 `prerequisite_requirements` 可见 · `--ui-audit` |
| **M7u** | **招募/名册 UI** | 招募列表（**2~7 人刷新**）· 名册上限显示（**9→28 阶梯**）· 高级新兵标注（三档） | 上限显示 = **唯一来源**（与内核一致）· 刷新可点且确定性 |
| **M8u** | **职业 UI** | 名册/详情/技能栏适配 **15 职业** · 头像占位（`P31` 15 槽） | 每职业可开可读 · 占位不崩（缺失点名） |
| **M9u** | **4v4 战斗布局** | 移除"场上支援位"（原 `O-62` 支援位在镜头外）· 4v4 对称布局 | `--ui-audit` 0 重叠 · **镜头内可见**（对齐原版） |
| **M10u** | **UIRoot 形态 B** | **B-1 绘层**（外壳上 `CanvasLayer`，或设层号）· **B-2 S4 战斗面板化** · **B-3 回落留痕** | 🔴 **判据"外壳三层都在当前场景之上"** · S1 指纹（切 panel 后三层 id 不变）· 回落留痕可数 |

---

## §4 架构认领（不派发，自查）

```
A1 🔴 **`data_schema` 规格**：M1a/M1b（属性/抗性/def/prot）· M6（建筑 `code`/`prerequisites`）· M4（Trinket 7 字段）· M5（Quirk）⇒ **各出一条 P 校验** ✓
A2 🔴 **`support_points` 语义更新**（去掉"支援位"绑定；`§3.7` + `P19`）✓
A3 🔴 **`SlotLayout` 处方落地规格**（`SupportSlots` ⇒ 通用扩展位）✓
A4 🔴 **体检表维护**：第 2 条拆出 **2b 换伤害模型**（`#439` 要求）· 第 3 条标"已裁"✓
A5 ✅ **两层 buff 结构**（原语层/概念层）写进 `data_schema`（`§3.4` 字段分类 + 新节）✓
```

---

## §5 明确**不做**（本期 · 由各裁定钉住）

```
· `kickstarter` 294 条（众筹专属 · 已排除）· `arena_*`（竞技场 = DLC）
· `award_category` 的**掉落/任务接线**（属条 3 / 条 5）· `nomad_wagon` **商店接线**（等 Trinket 表 + buff 层就位）
· 转场动效（`R4` 缓做）· 阶段 B 的一切"我们觉得更好玩"的改动（`#307` 仍冻结）
```

## §6 派发记录

| 轮次 | 对象 | 内容 |
|---|---|---|
| v2.09 | **主程序** | M1a/M1b/M1c · M2 · M3 · M4 · M5 · M6 · M7 · M8 · M9 · M11 |
| v2.09 | **UI 设计师** | M4u · M5u · M6u · M7u · M8u · M9u · M10u |
