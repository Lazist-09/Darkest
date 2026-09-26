# A9：建筑与升级 —— 抽取 + 对账读数（**99/99 个等级的成本都不同**）

> 🕒 2026-09-26 · 工具 `tools/dsh/extract_ref_buildings.py`（可重跑）·
> 产物 `reports/unity_ref/buildings_from_ref.json`（8 建筑 + 8 升级文件 · 78,799 bytes）✓
> 🔴 **只抽不落库**：`darkest/**` 一个字节没动 ⇒ **零行为** ✓

---

## 1. 抽取读数（可复算）

```
源是**两处，缺一不可**（我方此前只用了第 ② 处 ⇒ 漏掉了第 ① 处）：
   ① `…/Data/Buildings/*.building.json`（**8 个**）—— **活动（activities）** 的结构 ✓
   ② `…/Data/Upgrades/Building/*.upgrades.json`（**8 个**）—— **升级树** ✓

✅ **结构对上了**：**8 个建筑（同名同集合）** · **20 棵树** · **99 个等级** ✓
   ⇒ 🎖️ 与我方 `buildings.json` 的 `20 树 / 99 等级`**逐数相等** ✓
   📌 形状也**几乎同构**：
      参考 `requirements[] { code, currency_cost[], prerequisite_requirements[] }`
      我方 `levels[]       { code, currency_cost[], prerequisites[] }` ✓
🔴 **4 个 `.building.json` 有尾随逗号**（`abbey`/`nomad_wagon`/`sanitarium`/`tavern`）⇒ 先清洗 ✓
   （与 `JsonAI.json` / `JsonTraits.json` 同族 —— **这是第 3 次**遇到同类非法 JSON）✓
完整性自检：**8 个文件的树都没有空 `requirements`** ✓
```

## 2. 🔴 我方**完全没有**的三块（都在 ① 里）

```
🔴 **A. `side_effects`（活动副作用概率表）** —— **6 个活动**有（abbey 3 · tavern 3）✓
   形状：`{chance: 0.4, results: [{type, chance, data[]}]}` ✓
   实测的 `type` 全集：`activity_lock` · `go_missing`（**带 `duration`**）· `add_quirk`
      （**带 `quirk_library_name`**）· `apply_buff` · `change_currency` ·
      `add_trinket` · `remove_trinket` ✓
   各活动的 `chance`：abbey `meditation 0.4 / prayer 0.35 / flagellation 0.3` ·
      tavern `bar 0.4 / gambling 0.35 / brothel 0.3` ⇒ 🎖️ **"越贵的服务副作用越大"** ✓

🔴 **B. 3 个建筑级 gate 字段**：`on_start_town_visit_priority` · `number_of_quests_finished` ·
   `highest_dungeon_level`（8/8 全有）✓
   ⇒ ⚠️ 而 `PLAN_adoption §10.1 ⑥` 已实测：**参考解析了但【从不被读】** ⇒ 建筑**从不按任务数/等级解锁** ✓
   📌 即：**我方现在也没有这套 gate** ⇒ 要不要自己做，是**设计决定** ✓

🔴 **C. 各建筑特有的"升级数组"**（参考用**扁平数组**，我方用"树 + 等级"）：
   `blacksmith.equipment_cost_discount_upgrades` · `camping_trainer.camping_skill_cost_discount_upgrades` ·
   `guild.combat_skill_cost_discount_upgrades` · `nomad_wagon.{number_of_trinkets,trinket_cost_discount}_upgrades` ·
   `stage_coach.{number_of_recruits,roster_size,upgraded_recruits}_upgrades` ·
   `sanitarium.treatment{...}` / `disease_treatment{...}` ✓
```

## 3. 🔴🔴 而**升级成本：99/99 个等级都不同**（A9 的主要读数）

```
逐等级比较 **99 个**：**一致 0 · 不同 99** ✓
差异的**逐分量**归类：`crest 99` · `deed 19` · `bust 3` · `portrait 3` ✓
方向：我方更高 **91** · 参考更高 **33** ✓

满级全清总需（两侧对照）：
| 货币 | 我方 | 参考 | 差 |
|---|---|---|---|
| `gold` | **0** | **0** | 0 ✅（两侧都"只花传家宝"）|
| `crest` | 2103 | **2161** | **−58**（参考更多）|
| `bust` | 516 | **534** | **−18** |
| `deed` | 421 | **482** | **−61** |
| `portrait` | 241 | **277** | **−36** |
```

### 🎖️ 我方是**两族混合**（不是"整体偏高或偏低"）

```
按建筑分：
  abbey          18 级  我方更高 18 · 参考更高  0   → **整栋我方更高**
  blacksmith     13 级  我方更高 13 · 参考更高 13   → **双向**（币种不同？）
  camping_trainer 5 级  我方更高  0 · 参考更高  5   → **整栋参考更高**
  guild           9 级  我方更高  9 · 参考更高  0   → 整栋我方更高
  nomad_wagon     9 级  我方更高  0 · 参考更高  9   → **整栋参考更高**
  sanitarium     14 级  我方更高 14 · 参考更高  0   → 整栋我方更高
  stage_coach    13 级  我方更高 19 · 参考更高  6   → 双向
  tavern         18 级  我方更高 18 · 参考更高  0   → 整栋我方更高

crest 分建筑总量：
  abbey +63 · blacksmith +61 · guild +61 · sanitarium +34 · stage_coach +78 · tavern +63
  ⇒ 🔴 **6 栋我方 crest 偏高**
  camping_trainer **−212** · nomad_wagon **−206**
  ⇒ 🔴 **2 栋我方 crest 大幅偏低**（参考 480 vs 我方 ~270）
```

### 🔴 更关键的一条：**币种本身不同**

```
🔴 `stage_coach.upgraded_recruits`：
     参考 `[0] {bust:12, portrait:8}` · `[1] {bust:18, portrait:12}` · `[2] {bust:24, portrait:16}`
     我方 `[0] {bust:9,  crest:12}`   · `[1] {bust:12, crest:16}`   · `[2] {bust:15, crest:20}`
   ⇒ 🔴 **参考用 `portrait`，我方用 `crest`** —— **不是数值差，是【币种不同】** ⚠️
🔴 **参考里 `crest == bust` 的等级：18/99 · 我方：2/99** ⇒
   ⚠️ 即：**参考多数建筑的"第二货币"就是 `crest`**，而我方把它改成了别的组合 ✓

各建筑的**第二货币**（参考）：
   abbey → `bust` · blacksmith → `deed` · guild → `portrait` · sanitarium → `bust` ·
   tavern → `portrait` · stage_coach → `deed`(10) + `bust`(3) + `portrait`(3) ·
   camping_trainer → **只有 `crest`** · nomad_wagon → **只有 `crest`** ✓
```

## 4. ✅ 三方判定**已做完**：`我方 == E盘` **99/99**，`参考 == E盘` **0/99**

🎖️ **判法执行**：回到 E 盘原文件 `E:\…\DarkestDungeon\upgrades\building\*.upgrades.json`
   （**8 个 / 8 个**，与我方 `_source` 指向同一目录）**逐级重读** ⇒ 三方对照 ✓

```
逐级比较 **99 个**：
  ✅ **我方 == E 盘**：**99/99**   ← 🎖️ 我方转写器**完全忠实**，一个数都没错 ✓
  参考 == E 盘      ：**0/99**
  参考 == 我方      ：0/99
  三方都不同        ：0/99
⇒ ✅ **明确落在 (乙) 案**：**一手与参考确实不同** ⇒ **参考项目被人动过手脚** ✓
   📌 **与 A3 的 `crit` 那次同族**（一手 5/6/7/8/9 vs 参考 2.5/3/3.5/4/4.5）——
      这是**第二个独立证据**：参考项目的**数值被系统性改过** ⚠️
   ⇒ 🎖️ 也还了我方转写器一个清白：`extract_dd1_buildings.py` **逐级可复算、零误差** ✓
```

### 差异的**形状**（参考 vs E 盘）

```
逐分量：`crest` **99** · `deed` **19** · `portrait` **3** · `bust` **3** ✓
满级全清对照：
| 货币 | E 盘（= 我方）| 参考 | 参考相对一手 |
|---|---|---|---|
| `gold` | 0 | 0 | 相同 |
| `crest` | **2103** | **2161** | **+58** |
| `bust` | 516 | 534 | +18 |
| `deed` | 421 | 482 | +61 |
| `portrait` | 241 | 277 | +36 |
🔴 **每一栋建筑都有差异**（`完全一致的建筑：{}`）：
   abbey 18 · blacksmith 13 · camping_trainer 5 · guild 9 · nomad_wagon 9 ·
   sanitarium 14 · stage_coach 13 · tavern 18 ✓
样本（`abbey.meditation`）：一手 `bust 4 / crest 5` vs 参考 `bust 4 / crest 4` ⇒
   🔴 **参考把"第二货币 = crest"的结构抹平成了 `crest == bust`** ⚠️
   （实测：参考里 `crest == bust` 的等级 **18/99**，而一手只有 **2/99**）✓
🔴 而 `stage_coach.upgraded_recruits` 更直接：一手 `{bust 9, crest 12}` vs 参考 `{bust 12, portrait 8}`
   ⇒ **币种本身都换了**（`crest` → `portrait`）⚠️
```

### 🔴 而这条结果**改变了对账口径**（重要，纪律 AU）

```
⚠️ 前文 §3 那张"我方 vs 参考，99/99 不同"的表**现在要重新解读**：
   它**不是"我方错了 99 处"** —— 而是 **"一手与参考不同 99 处"** ✓
⇒ 📌 **按用户指令「数值一律采用参考项目」⇒ 仍然要改 99 处**，但**理由完全不同**：
   · 不是"修正错误"（我方没错，与一手逐级相符）⚠️
   · 而是"**按指令换来源**"（把一手值换成参考值）✓
⇒ 🎖️ **两者的后果不同**：前者要查工具，后者只需落库 ⇒ **少走一圈** ✓
```

## 5. 诚实边界

```
✅ **能验**：8 建筑 / 20 树 / 99 等级 · 4 个尾随逗号文件 · 三方逐级对照（99/99 / 0/99 / 0/99）·
   逐分量归类 · 满级总需四方 · 每栋都有差异 —— 全部当场跑出 ✓
   🎖️ 并**还了我方转写器一个清白**（99/99 与一手相符 = 工具正确）✓
🔴 **不能验**：**没有落任何建筑数据** ⇒ "改这 99 个数后经济会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：**参考为什么被改**、改的人是谁、是否有意 —— **只能说明"改了"，不能说明"为何"** ✓
   📌 而 `PLAN_adoption §0` 已记：参考件的注记自称 *"numbers must be blessed by the planner"*
      ⇒ ⚠️ **暗示参考项目的数值【本来就是被调过的】** ⇒ 与本次实测一致 ✓
🔴 **不能验**：`side_effects` 的落点 —— 我方**没有"城镇活动副作用"这一层** ⚠️
   ⇒ 同 A6 的 act-out、A1 的趟级载体：**可能需要一层新结构** ⇒ 按架构判据**需求驱动，不预留** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a9_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **已完成**：抽取 + 三方判定（原定的"下一轮先做"本件就做完了）✓
⏸️ **等策划**：① 99 处成本**按参考改还是保一手**（⚠️ 指令说"采用参考"⇒ 倾向改，但这是**经济平衡**）·
   ② `side_effects` / gate 三字段 / 币种差异要不要按参考落 ✓
⏸️ **等架构**：`side_effects` 与 gate 的落点（是否需要新结构）✓
🆕 **并建议给策划一条**：🔴 **参考项目已被证明【两次数值改动】（A3 的 `crit` + 本次 99 处成本）** ⇒
   当"采用参考"与"对齐原版"冲突时**该以谁为准** ⇒ **这可能需要一条总口径裁定** ✓
```

