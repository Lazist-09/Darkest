# 奇物逐条对账：我方 6 条 vs 参考 60 条 —— 🔴 **id 交集 0，但 5/6 有语义配对**

> 🕒 2026-09-26 · 承接 `12_curios_from_ref.md` §6 的"我方 vs 参考逐条对账未做" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 对账读数

```
参考 `Curios/Curios.csv` ⇒ **60 条**（id 去重 60）✓
我方 `darkest/data/curios.json` ⇒ **7 条**（其中 1 条是 `__source__` 注记 ⇒ **真数据 6 条**）✓
🔴 **id 交集 = 0**（我方一律 `cur_*` 前缀，参考一律裸名）⚠️
```

## 2. 🎖️ 但**语义配对显然存在**（逐条核对，不是"看着像"）

| 我方 | 我方 `name` / `curio_type` | 参考 | 参考 `kind` | 配对依据 |
|---|---|---|---|---|
| `cur_sconce` | 壁灯 / `Light` | **`sconce`** | `Good` | 名字**逐字相同**；双方都给"火把/光照" ✓ |
| `cur_altar` | 圣坛 / `Worship` | **`altar_of_light`** | `Good` | 双方都给 **`Curio Damage Buff`**（我方 `damage_buff 20`）✓ |
| `cur_book_stack` | 书堆 / `Knowledge` | **`stack_of_books`** | `Mixed` | 双方都走 **`positive`/`negative` 怪癖** ✓ |
| `cur_bone_pile` | 骸骨堆 / `Haunted` | **`pile_of_bones`** | `Mixed` | 名字对应；参考给 `Loot`（"skeleton key"类）✓ |
| `cur_supply_crate` | 补给箱 / `Treasure` | **`crate`** | `Good` | 双方都给 `Loot`/`Treasure` ✓ |
| `cur_discarded_camp` | 废弃营地 / `Scrounging` | 🟡 **无直接对应** | — | 🎖️ **`discarded_pack` 才是语义近的**（"丢弃的背包"）⇒ ⚠️ **待核** |

```
📊 **5/6 有明确语义配对** · 1/6（`cur_discarded_camp`）**待核** ✓
🎖️ 而 `cur_altar` 那一条**配对依据最硬**：参考 `altar_of_light` 的
   `FULL CURIO?` 段写 `Effect | 1 | 100.00% | Curio Damage Buff | 1 | 100.00%`
   ⇒ ✅ **与我方 `{chance:100, kind:"damage_buff", amount:20}` 语义逐项对上** ✓
```

## 3. 🔴 而参考的 `cur_altar` 有**4 个同名候选**（我方的"圣坛"太笼统）

```
按 `altar` 搜参考表 ⇒ **4 个**：`eldritch_altar` · `altar_of_light` · `skull_altar` ·
   `shamblers_altar` ✓
⇒ 🔴 **即被我方合并成一个"圣坛"的东西，参考里是 4 件不同的奇物** ⚠️
   📌 同类：`cur_book_stack` ⇒ 参考有 `stack_of_books` **与** `bookshelf` **两件** ✓
⇒ 🎖️ **这是"我方粗粒度 vs 参考细粒度"的又一例**（同 A6 的"7 条 vs 12 条"）✓
   ⇒ 📌 **判据（可执行）**：**逐件比 `kind` + `FULL CURIO?` 的效果串**，
      而不是比中文名 ⇒ **不许按名字推**（纪律 BL）✓
```

## 4. 🔴 我方**没有的两条维度**

```
🔴 **① `kind`（`Good` 23 / `Mixed` 31 / `Bad` 6）** —— 我方**完全没有** ⚠️
   📌 语义（推断）：**"打开是好是坏"的预判**（UI 上给玩家的提示）✓
   ⇒ ⚠️ **这是"设计意图"不是"数值"** ⇒ 要采用的话归策划 ✓
🔴 **② `TAGS` 的完整取值** —— 我方 `curio_type` 有 6 个取值
   （`Haunted`/`Knowledge`/`Light`/`Scrounging`/`Treasure`/`Worship`）；
   参考的 `TAGS` 段里**混着结果行**（`Scouting` 与百分比/文本同行）⇒
   ⚠️ **我的提取器把 `TAGS` 段整段收了，未拆出纯标签** ⇒ 📌 **记为"我方提取器偏粗"** ✓
   🎖️ **诚实标注**：所以上面那张"参考 TAGS"的表**不干净**（含 `100.00%`/句子）
      ⇒ ✅ **要看纯标签得再拆一层** ⇒ **归下一轮**（或直接读 CSV 原始列）✓
```

## 5. 诚实边界

```
✅ **能验**：参考 60 条 · 我方真数据 6 条 · **id 交集 0** · **5/6 语义配对**（逐条给了依据）·
   `cur_altar` 的配对依据（`Curio Damage Buff` 逐项对上）· 参考有 4 个 altar 候选 ——
   **全部当场跑出** ✓
🔴 **不能验**：**配对是【我推的】**（id 交集为 0 ⇒ 没有机器可验的对应）⚠️
   ⇒ ✅ **按纪律 BL：这 5 条只能算【候选】** ⇒ **不许据此落库** ✓
   📌 要升级为"明确"，须**逐件比 `kind` + `FULL CURIO?` 效果串**（下轮可做）✓
🔴 **不能验**：我方 `curio_type` 与参考 `TAGS` 的**映射未定**（TAGS 提取偏粗）⇒ 记为**未验** ✓
🔴 **不能验**：**没有落任何奇物数据** ⇒ "接上 60 条后跑图会怎样"**完全未测**（纪律 BK）✓
```

## 6. 下一步

```
🆕 **可做（本轮已到收尾，归下一轮）**：
   ① **拆 `TAGS` 段**，取出纯标签（现在混着结果行）
   ② **逐件比 `kind` + `FULL CURIO?` 效果串** ⇒ 把那 5 条从"候选"升成"明确"或降为"无对应"
   ③ 核实 `cur_discarded_camp` 到底对应 `discarded_pack` 还是真无对应 ✓
⏸️ **等策划**：三张单已投递（Σ / A6 / A9+A10+总口径）✓
```
