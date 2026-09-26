# A4 步1：参考项目怪物**全表抽取**（230 条 · 只抽不落库）

> 🕒 2026-09-26 · 工具 `tools/dsh/extract_ref_monsters.py`（**可重跑**）·
> 产物 `reports/unity_ref/monsters_from_ref.json`（230 条 · **每条带 `file` + `line` 出处**）✓
> 🔴 **本件只抽不落库**：`darkest/**` 一个字节没动 ⇒ **零行为**（纪律 AY：结构 / 数值分开）✓

---

## 1. 为什么分两步（步1 现在做 · 步2 等策划）

```
🔴 实测代价面：我方 3 个敌方原型（`melee_soldier` / `ranged_archer` / `caster`）
   被 **47 / 35 / 62** 个文件引用（`data/*.json` · `scripts/**` · **大量测试**）⚠️
⇒ **"换怪"不是往 `units.json` 加 230 条，而是换掉一套被 60 处引用的敌人** ✓
⇒ ✅ **步1（本件）**：抽出全表 + 量出卡点（**零行为**）✓
⇒ ⏸️ **步2（等策划）**：裁"用哪些怪替换现有 3 个 / 变体怎么算 / 三个量纲怎么换算" ✓
```

## 2. 抽取读数（可复算）

```
源：`…/Assets/Resources/Data/Monsters/*.txt` —— **230 个文件**（另有 230 个 `.meta`，**不入账**）✓
解析出 **230 条记录** · 完整性自检（**不许有字段大面积为空**）：
   `type` **230/230** · `name` **230/230** · `stats` **230/230** ·
   `monster_brain` **230/230** · `initiative` **230/230** · skill 行共 **484** ✓
✅ **与源文件逐值复核**：230 条 × 9 个 stats 字段 = **2070 个值 ⇒ 不匹配 0** ✓
   （第二读数：回到 `.txt` 原文用正则重找每个值 ✓）

文件布局（实测，`brigand_cutthroat_A.txt`）：
   L1 `name: brigand_cutthroat_A`  ← **文件级头两行，在任何 `xxx:` 段【之前】**
   L2 `type: brigand_cutthroat`
   L4 `art:` 段 … L10 `.end` ／ L12 `info:` 段 … L26 `.end`
🔴 ⚠️ **我第一版把 `name`/`type` 当"段内字段"找 ⇒ 230/230 全空** ⚠️
   ⇒ ✅ 已改为单独解析"段名前的前缀区" ✓
   📌 **这是本任务里第三次同族错**（纪律 BH）：**换个口径，数就变了 ⇒ 我读窄了** ✓
```

## 3. `.info` 段的字段全集（实测）

```
**`stats:` 行 —— 固定 9 个字段，230/230 一个不缺**：
   `.hp .def .prot .spd .stun_resist .poison_resist .bleed_resist .debuff_resist .move_resist`
   ⇒ 🔴 **我方 8 种抗性里的 `death_blow` / `trap` 在这里【没有】**
     （= 怪物侧只有 5 种抗性，与 `PLAN_adoption §7.4` 一致 ✓）

**skill 行（484 条）的字段**：`id`/`type`/`atk`/`dmg`/`crit`/`launch`/`target` 各 484 ·
   `effect` **398** · `is_crit_valid` 161 · `move` 76 · `extra_targets_count`/`chance` 34 ·
   `heal` **14** · …

**其余 `info:` 字段**：`enemy_type` 278 · `loot` 232 · `display`/`personality`/`initiative`/
   `monster_brain` 230 · `battle_modifier` 229 · `death_class` 118 · `tag` 67 · `life_link` 25 ·
   `shape_shifter`/`shared_health` 12 · `life_time` 8 · `captor_empty/full` 6 · `companion` 6 ·
   `torchlight_modifier`/`controller` 3 · `skill_reaction` 2 · `death_damage`/`riposte_skill`/`spawn` 1
```

## 4. 🔴🔴 三个**量纲 / 字段**卡点（A4 真正的难点，**必须是"一个裁定"**）

| # | 参考项目 | 我方 | 卡在哪 |
|---|---|---|---|
| ① | **`prot` = 分数 `0~1`**（15 个取值，**63/230 非整数**）| `prot` = **`[0,85]` 整数**（`UnitsConfig` 硬校验 >85 抛）| 要搬**必须先定换算**（×100？还是把我方改成小数？）⚠️ |
| ② | **`def` = 带 `%` 的数**（**154/230 非整数**，范围 **−20 ~ 999**）| `dodge`（= 原版 `def`，`§7.2` 已裁）**`[0,100]`** | 值可搬，但**负值 −20 与 999 都越界** ⚠️ |
| ③ | **`move_resist` 上界 1000**；`stun/poison/bleed/debuff_resist` **上界也是 1000** | 字段名 **`displace_resist`**、`[0,100]` | **名字不同 + 上限不同** ⇒ 需先裁：改名 / 换算 / **把 1000 当"免疫"哨兵** ✓ |
| ④ | `hp` **5 ~ 999**（80 个取值，**全整数**）| `hp` 整数 | 值可直搬，🔴 **但会穿透 `enemy_full_hp` 判据**（`DataGateTests`）⇒ **换怪 = 该判据基准要一起改** ⚠️ |

📌 🎖️ **① 与 A7 的阻塞是同一个坑**：参考项目给"百分比"时**两处都是分数/带 `%` 的小数**
   （这里 `prot 0.15` = 15% · `JsonBuffs.amount 0.04` = 4%），而我方一律存**整数百分比** ⚠️
   ⇒ ✅ **这两件应当用【同一个裁定】解决**（一次定"分数 → 整数百分比"的换算与舍入口径）✓

## 5. 变体结构（采用时要决定"变体 = 独立原型"还是"同原型 + 难度档"）

```
**94 个不同 `type`** · 230 = `_A 67 / _B 67 / _C 67 / _D 29` ✓
   出现 3 次的 type：**65** · 出现 1 次的：**27** · 出现 4 次的：**2** ✓
🔴 变体之间的差异**不只在数值**（`bandit_stabby` vs `bandit_stabby_weak` 是**不同技能**）⇒
   证据：`brigand_cutthroat_A` 的第 4 个技能是 `bandit_stabby_weak`（atk 42.5% · dmg 2-4 · `.is_crit_valid False`）
   而 `bandit_stabby` 是 atk 72.5% · dmg 4-8 ✓ ⇒ ⚠️ **变体携带双重语义**（难度 + 技能集）✓
```

## 6. 🔴 `.dmg` 是**区间两数** —— 与我方模型不同（重要）

```
参考（怪物 skill）：`.dmg 3 5` ⇒ **两个数**（区间）✓
我方（`skills.json`）：`damage.segments[].multiplier` ⇒ **倍率**（乘以武器区间）✓
⇒ 🔴 **两者不同构** ⇒ 怪物技能不能直接套我方 `skills.json` 的形状 ⚠️
   📌 而我方第 2.3 节说"英雄技能的 `.dmg` 是**单个百分比**" ⇒
     **英雄技能与怪物技能的 `.dmg` 语义【不同】**（英雄是修正百分比，怪物是绝对区间）⚠️
```

## 7. 🎖️ 顺带把 A5 的 join **预检做了**（省一轮）

```
实测（`JsonAI.json` 160 个 brain id vs 230 个怪物）：
   按 brain 看：**精确同名 107 · 去掉 `_A/_B/_C/_D` 后缀能配 28 · 配不上 23 · 孤儿 brain 2** ✓
   按怪物看：**精确同名 107 · 去掉后缀同名 52 · 指向别的 base（共享）71 · 指向不存在的 brain 0** ✓
🎖️ **最有价值的一条**：**"共享 brain"是常态**（`bloated_corpse_B/C → bloated_corpse_A` ·
   `brigand_cannon_A/B/C → cannon_A/B/C`）⇒ ⚠️ **A5 不能按"一怪一 brain"做**，
   **必须按 brain id 建表 + 允许多怪共享** ✓
🆕 并更正 `PLAN_adoption §9④` 那组数（当时写"107 精确 / 50 后缀 / 73 无 / 28 orphan"）：
   🔴 那是**按【怪物】看**的口径（107/50/73 与 28 orphan 不同源）⇒
      ✅ 本件给**两个口径各自的数**（按 brain / 按怪物）✓（纪律 AU：**先写清分母**）
```

## 8. 诚实边界

```
✅ **能验**：230/230 解析 · 2070 个值逐值复核 0 不匹配 · 分布/变体/join 全部当场跑出 ✓
🔴 **不能验**：**没有落任何怪物数据** ⇒ "换成参考怪物后战斗会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：三个量纲卡点**未解决**（`prot` 分数 / `def` 负数与 999 / `move_resist` 的 1000 哨兵）⚠️
🔴 **不能验**：变体的"难度档"语义**是我的推断**（证据是"技能集不同"）⇒ **归策划确认** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a4_*.py` **不入库**（scratch）✓
```

## 9. 下一步（步2 的施工单，等裁定）

```
⏸️ **等策划**：① 用哪些怪替换 `melee_soldier`/`ranged_archer`/`caster`（我给候选：
   `brigand_cutthroat` / `brigand_fusilier` / `cultist_witch`，各 3 个变体 ✓）
   ② 变体 = 独立原型还是"同原型 + 档位" ③ 三个量纲的换算口径（与 A7 合并一次裁）
⏸️ **等架构**：`WeaponTier`/`ArmourTier` 那样的"怪物阶"要不要建（我方现在**没有怪物 tier 概念**）⚠️
✅ **可并行的**：A6（折磨/美德，`JsonTraits.json` 12 条 · 我方缺 5 美德）· A9/A10/A11（建筑/补给/传家宝）✓
```
