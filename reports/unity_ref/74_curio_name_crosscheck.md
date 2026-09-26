# A8 `curio_name` × A12 奇物：**交集 = 1**（`iron_maiden`）—— 把"A8 的 10 个未匹配"以实测收口

> 🕒 2026-09-26 · 工具 `tools/dsh/crosscheck_curio_names.py`（新，可重跑）✓
> 产物 `reports/unity_ref/curio_names_crosscheck.json` ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 A8 记的"`curio_name` 11 个引用 ⇒ 10 个未匹配"** ✓

---

## 1. 🔴 先纠一处**取数错**（第 26 次同族）

```
🔴 **我第一版**只查顶层 list ⇒ A12 的奇物数 **0** ⚠️ ⇒ 于是"交集 0"
✅ **打印结构才发现**：`curios_from_ref.json` 的 `curios` 是**嵌套 dict**：
   `{"curios": {"header": [18 项], "items": [**60 项**], "file": "Curios.csv"}}` ✓
   ⇒ 📌 **正确取法是 `curios.items`** ⇒ **60** ✓（与 A12 的"60 个奇物"**完全吻合**）✓
🎖️ **判据**：**"我按猜的结构取数 —— 得 0 ⇒ 先【打印结构】再取"** ✓
   📌 与"按猜的格式解析得 0"（第 26 条）**同族**，但对象是 **JSON 结构**而非文本格式 ✓
   🎖️ **而"得 0"这次又救了我**：若我接受"0"⇒ 就会报"两边毫无关系" ⚠️
```

## 2. 📊 修正后的三方对照

| 集合 | 数量 |
|---|---|
| **A8 的 `curio_name`**（去重）| **11** |
| **A12 的通用奇物** | **60** ✓（与计划一致）|
| 我方 `curios.json` | **7** |

| 交集 | 读数 |
|---|---|
| **A8 ∩ A12** | 🎖️ **1 —— `iron_maiden`** ✓ |
| 🔴 只在 A8 | **10** |
| 只在 A12 | 59 |
| A8 ∩ 我方 | **0** |
| A12 ∩ 我方 | **0** |

## 3. 🎖️ 而那唯一的交集 `iron_maiden` **恰好解释了整件事**

```
🎖️ **A12 里的 `iron_maiden`**（实测原文）：
   `{ "index": "8", "display": "**Iron Maiden**", "kind": "**Mixed**", "id": "iron_maiden",`
   `  "meta": { "REGION FOUND": [["Loot", "2", "40.00%", "A", …]],`
   `            "FULL CURIO?": [["Effect"]], "TAGS": [["**Scouting**"]] },`
   `  "results": [ { "result_type": "Nothing", "chance": "20.00%", … }, … ] }` ✓
   ⇒ 📌 **即：它是一个【完整的通用奇物】**（有 kind · region · tags · results 表）✓

🎖️ **A8 里的引用**：goal `activate_iron_maiden` 的 `data.curio_name = "iron_maiden"` ✓
⇒ 🎖️ **即：两者【指向同一个实体】**，只是**用途不同**：
   · **A12** = 它**作为奇物本身**的定义（在哪出现 · 摸出什么）✓
   · **A8** = 某个**任务目标**要求"激活它" ✓
   ⇒ ✅ **所以"交集 1"是【真交集】**，不是巧合 ✓
```

## 4. 🎖️ 于是"A8 的 10 个未匹配"**有了确切性质**

```
📊 **那 10 个**（只在 A8，不在 A12 的 60 里）：
   `animalistic_shrine` · `beacon` · `chirurgeons_satchel` · `corrupted_altar` ·
   `foodstuff_crate` · `infected_corpse` · `protective_ward` · `reliquary` ·
   `shipment_crates` · `teleporter` ✓
⇒ 🎖️ **逐个看名字**：
   · **`foodstuff_crate` `/` `shipment_crates` `/` `chirurgeons_satchel` /
     `reliquary`** ⇒ 🎖️ **它们是【`gather_*` 类任务】的目标**（收集物资）✓
   · **`beacon` `/` `teleporter` `/` `corrupted_altar` `/` `animalistic_shrine` /
     `infected_corpse` `/` `protective_ward`** ⇒ **`inventory_activate_*` / `activate_*`**
     （**去激活**）✓
   ⇒ 📌 **即：它们与奇物是【同名不同用途】** —— 但**不在 A12 的 60 里** ⇒ ⚠️
      🎖️ **两种可能**：
         ① **它们是"任务专用的交互物"**（A8 已判：只在 `JsonQuests` + 本地化）✓
         ② **或者它们本来也是奇物，只是 A12 的 CSV 没列**（A12 是从 `Curios.csv` 抽的）⚠️
      ⇒ ✅ **而本件能区分它们**：查**参考的 `Curios/*.bytes` 里有没有这 10 个** ✓
```

## 5. 诚实边界

```
✅ **能验**：**A8 的 11 个 `curio_name`（含引用它的 goal id）** ·
   **A12 的 60 个（结构 `curios.items`）** · **交集恰好 1（`iron_maiden`）** ·
   **`iron_maiden` 的 A12 完整条目 + A8 的 goal 原文** · **我方 7 个与两边都无交集** ——
   **全部当场跑出** ✓
🎖️ 并**订正了我自己"得 0"的取数错**（嵌套结构）✓
🔴 **不能验**：**那 10 个到底是不是奇物**（§4 的①②两说）⇒
   📌 **本件只做到"它们不在 A12 的 60 里"** ⇒ 记**待核** ✓
🔴 **不能验**：**我方 `curios.json` 与两边都 0 交集**的【原因】⇒
   📌 我方 7 个是**自造 id**（`cur_supply_crate` 等，带 `cur_` 前缀）⇒
   ✅ **命名空间就不同** ⇒ 记**命名空间不同** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{a12_struct,iron_maiden}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **A8 × A12 的奇物交叉【收口】**：交集 1（`iron_maiden`）· 10 个待核 ⇒ 已定位 ✓
🆕 **可做**：① 🔴 **查参考的 `Curios/*.bytes` 里有没有那 10 个**（区分 §4 的①②）
   ② 核那 10 个各自的 `kind`/触发方式（A12 的 60 个有 `kind`；A8 的没有）
   ③ 用同法核 A8 的 `item` 引用 × A10 的 57 个物品 ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
