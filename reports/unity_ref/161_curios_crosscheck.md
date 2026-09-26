# 逐条比对 `curios.json`：**6/6 有参考对应物，但只采用了 6/60** · 🔴 且**数值语义不同**

> 🕒 2026-09-26 · 工具 `tools/dsh/compare_curios_table.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **判据（第 308 条）**：**"'可能有对应物'要变成'确认抄了'——
>    要【逐条比对】，不能停在'词命中'"** ✓

---

## 1. 🎖️ 先纠正上一件的一处**粗判**（判据 307/308 的兑现）

```
🔴 **`160_*.md` 我写**："`curio` ⇒ 15 份（`Effects.txt` 771 次）" ⇒ 判"有对应物" ⚠️
✅ **本件逐条比**（用参考 `Curios.csv` 的**结构**抽 id，共 **60 条**）：
   | 我方 | 参考对应物 |
   |---|---|
   | `cur_supply_crate`（补给箱）| **`crate`**（Crate）✓ |
   | `cur_sconce`（壁灯）| **`sconce`**（Sconce）✓ |
   | `cur_altar`（圣坛）| **`eldritch_altar`**（Eldritch Altar）✓ |
   | `cur_book_stack`（书堆）| **`stack_of_books`**（Stack of Books）✓ |
   | `cur_bone_pile`（骸骨堆）| **`pile_of_bones`**（Pile of Bones）✓ |
   | `cur_discarded_camp`（废弃营地）| **`discarded_pack`**（Discarded Pack）✓ |
⇒ 🎖️🎖️ **即：6/6 都有参考对应物** ✓ ⇒ ✅ **我上一件的"有对应物"判对了** ✓
   🔴 **而我第一版判据说"只有 `sconce` 有"** ⚠️ —— 因为**我按 id 字面比**
     （`cur_supply_crate` ≠ `crate`）⇒ ⚠️ **名字不同但对象相同** ✓
   🎖️ **判据（第 309 条）**：**"两边'同名'才叫对应吗？
      名字不同、对象相同 ⇒ 也是对应（要按【语义】比 id）"** ✓
```

## 2. 🔴 但**采用率是 6/60** —— 这才是真读数

```
📊 **参考 `Curios.csv` ⇒ 60 条**（`unlocked_strongbox` · `locked_strongbox` · `goal_strongbox` ·
   `heirloom_chest` · `eldritch_altar` · `altar_of_light` · `stack_of_books` · `iron_maiden` ·
   `pile_of_bones` · `discarded_pack` · `sconce` · `crate` · `sack` · `suit_of_armor` ·
   `dinner_cart` · `decorative_urn` · `locked_display_cabinet` · `confession_booth` ·
   `holy_fountain` · `bookshelf` · `alchemy_table` …）
📊 **我方 ⇒ 6 条** ⇒ 🎖️ **采用率 10%** ✓
⇒ 🔴 **即：`curios.json` 不是"抄了没记"，是【只抄了 1/10】** ✓
   ＋ 🎖️ **而我方那 6 条【确实取材于参考】**（一一对应）⇒ ✅ **来源是参考，非自造** ✓
```

## 3. 🔴🔴 而**数值语义不同**（`sconce` 逐字段比）

```
📊 **参考 `sconce`**（`Curios.csv` L155-161）：
   · `RESULT TYPES` = **`Loot`** · `WEIGHT` = **1** · `% CHANCE` = **100.00%**
   · `RESULT 1` = **`TORCHONLY`**（**一件火把**）· `R1 WEIGHT` = 1
   · `TAGS` = **`Scrounging`** · `FULL CURIO?` = **`No`**（**不能整件拿走**）
   · `STRING` = "An unburned torch is found." ✓
📊 **我方 `cur_sconce`**：
   · `curio_type` = **`Light`** · `bare_hands` = `[{chance: **100**, kind: **light**, amount: **30**}]`
   · 文案："你取下壁灯，火光把走廊推开了一截。" ✓
⇒ 🔴 **即：语义不同 ——**
   · **参考**：给**一件火把**（`TORCHONLY`）⇒ ✅ **那是【道具】** ✓
   · **我方**：给**光照 30**（`light` +30）⇒ ⚠️ **那是【直接加光照值】** ✓
   📌 **两者不等价** —— **参考的火把是"道具"，玩家之后能用**；
      **我方的 +30 是"立即生效的光照"** ⇒ ⚠️ **体验不同** ✓
   🎖️ **判据（第 310 条）**：**"两边'同类'就够了吗？
      还要比【给的东西的形态】（道具 vs 数值）"** ✓
```

## 4. 🎖️ 所以 `curios.json` 的**定性结论**（判据 302 的答案）

```
📊 **三种可能 → 本件的判定**：
   | 可能 | 判 |
   |---|---|
   | ✅ 自造 | 🔴 **否** —— 6 条都能在参考里找到对应物 ✓ |
   | 🔴 抄了没记 | ✅ **是** —— 取材于参考**但无任何来源标注** ✓ |
   | ⚠️ 抄了但改过 | ✅ **也是** —— **数值形态被改**（火把 ⇒ 光照值）✓ |
⇒ 🎖️ **即：`curios.json` = "取材参考 + 改动数值 + 未记来源"** ✓
   ＋ 🔴 **且只采用了 6/60** ⇒ ⚠️ **与目标的"全面采用"差距很大** ✓
   ⇒ 🎖️ **判据（第 311 条）**：**"一个文件'有对应物'——
      要问【采用比例】与【是否改过】，不能只答'是/否'"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**参考 `Curios.csv` 的 60 条 id（逐个列出）** ·
   **我方 6 条的一一对应（逐个）** · **`sconce` 的参考字段（`Loot`/`TORCHONLY`/`Scrounging`/`No`）** ·
   **我方 `cur_sconce` 的字段（`Light`/`light`+30）** · **采用率 6/60** ——
   **全部当场跑出** ✓
🎖️ 并把 `curios.json` 定性为"**取材 + 改值 + 未记来源**"，且给出采用率 ✓
🔴 **不能验**：**另 5 条的数值是否也被改过** ⇒ 📌 本件**只逐字段比了 `sconce`**
   ⇒ ⚠️ **其余 5 条未比**（`crate`/`eldritch_altar`/`stack_of_books`/`pile_of_bones`/
     `discarded_pack` 的参考字段未读）⇒ 记**未比** ✓
   🎖️ **判据（第 312 条）**：**"我逐字段比了 1 条 ——
      其余几条凭什么不说？（要么都比，要么说清'只比了 1 条'）"** ✓
🔴 **不能验**：**`curio_type`（`Light`/`Treasure`/`Knowledge`/`Worship`/`Haunted`/`Scrounging`）
   与参考的 `TAGS` 是否同源** ⇒ 📌 参考的 tags 是 `Scrounging`（**sconce 那条**）⇒
   ⚠️ **我方 `curio_type` 取值看着像参考的 tags** ⇒ 记**待核** ✓
🔴 **不能验**：**该不该补那 54 条** ⇒ 📌 **那是内容量决策** ⇒ ⚠️ **属策划** ⇒ 记**待裁** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{curio_source,curio_ref_names,csv_head,sconce_cmp,csv_semantics}.py`
   **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **`curios.json`【逐条比完（6 条 + sconce 逐字段）】**：
   **6/6 对应参考 · 采用率 6/60 · 数值形态被改 · 无来源标注** ✓
🆕 **可做**：① 🔴 **比其余 5 条的字段**（判据 312）
   ② 🔴 **核 `curio_type` vs 参考 `TAGS` 是否同源**
   ③ 把第 309~312 条判据补进 `observe_list` D11
   ④ **把"采用率"列加进 `PLAN_adoption` 的来源状态表** ✓
⏸️ **等策划**：13 项已补投送达 · 窗口无新回复 ⇒ 等 ✓
```
