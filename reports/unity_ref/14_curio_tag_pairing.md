# 奇物配对**升级**：从「按名字推」改成「按 tag 交集」（可计算）

> 🕒 2026-09-26 · 承接 `13_curio_pairing.md`（那版是**按名字推**的候选）·
> 工具 `tools/dsh/refine_curios_csv.py`（新）· 产物 `curios_csv_refined.json` ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 先修正数据本身：CSV 的**分段**（我上一版收粗了）

```
🔴 **上一版三个错**：
   ① `TAGS` 只取了「同一行的列 3+」⇒ **漏掉续行** ⚠️
   ② `REGION FOUND` / `ALL` / `FULL CURIO?` 也**没收续行**（`Yes`/`Purge` 就是续行）⚠️
   ③ 🔴 **同一列号在不同行里【口径不同】** —— 元信息行的「列 4」不是块表头的 `RESULT TYPES`
      ⇒ 📌 **纪律 AU 的又一实例：同一列号，口径随段变** ⇒ 必须**按段解析** ✓

🎖️ **本版用【分布】而不是【看一条】定段长**（纪律 AU/BH）：
   `TAGS` 行 到 `Item Interactions` 行之间的行数 ⇒ 实测 **60/60 条都是 3 行** ✓
   ⚠️ **只看第一条会以为只有 1 行** —— 那条的续行恰好被 `Yes`/`Purge` 遮住了 ⚠️
   📌 段内取值列实测只有三种组合：`(列2,列3,列4)` = `(T,F,T)` 91 · `(T,T,T)` 62 · `(F,F,T)` 27 ✓
⇒ ✅ **判据**：**一个段的值 = 段首行到下一段首行之间的【列 2/3/4】全部非空值** ✓

修正后读数：**60 条** · 有真 id **60/60** · 有 TAGS **60/60** ✓
   `Unlocked Strongbox` 示例 ⇒ `TAGS = [Scouting, Treasure, All, Teleport, Disease]` ·
      `FULL CURIO? = [Effect, 1, Yes, Purge]` · `REGION FOUND = [Loot, 3]` ✓
```

## 2. 🎖️ TAGS 词表（**去噪后 17 个** —— 上一版是"混着数值和整句"的脏表）

```
`Scouting 60` · `Teleport 60` · `Disease 60` · `All 59` · `Treasure 17` · `Scrounging 16` ·
`Haunted 13` · `Unholy 9` · `Worship 8` · `Knowledge 7` · `Body 7` · `Reflective 6` ·
`Food 4` · `Drink 4` · `Fountain 3` · `Goal 1` · `Torture 1` ✓
📌 每条 tags 数：`5 个 ×21 · 6 个 ×21 · 7 个 ×11 · 4 个 ×4 · 8 个 ×3` ⇒ **大多数是 5~6 个** ✓
🎖️ 而 `Scouting`/`Teleport`/`Disease` 是**全 60 条都有** ⇒ 它们是**默认标签**，不区分条
   ⇒ **真正有区分度的是后 13 个** ✓
```

## 3. 🎖️🎖️ 关键发现：**我方 `curio_type` 就是参考的 TAG**

```
我方 `curio_type` 取值（6 个）：`Haunted` · `Knowledge` · `Light` · `Scrounging` · `Treasure` · `Worship`
参考 TAGS 词表（17 个）：`All Body Disease Drink Food Fountain Goal Haunted Knowledge Reflective
   Scouting Scrounging Teleport Torture Treasure Unholy Worship`
🔴 **交集 5 个**：`Haunted` · `Knowledge` · `Scrounging` · `Treasure` · `Worship` ✓
🔴 **我方有、参考没有 1 个**：**`Light`** ⚠️
🔴 **参考有、我方没有 12 个**：`All Body Disease Drink Food Fountain Goal Reflective Scouting
   Teleport Torture Unholy` ✓
⇒ 🎖️ **即：我方的"奇物类别"与参考的标签体系【同源】，但只用了其中 5 个标签** ✓
   📌 **这比"名字像"硬得多** —— **标签是数据里的字段，不是我的判断** ✓
```

## 4. 🎖️ 用 tag 交集重做配对（**替换掉 `13_*` 那版的"按名字推"**）

| 我方 | `curio_type` | 该 tag 筛出的参考条数 | 我猜的那条**在不在里面** |
|---|---|---|---|
| `cur_altar` | `Worship` | **8** | ✅ `altar_of_light` **在** |
| `cur_book_stack` | `Knowledge` | **7** | ✅ `stack_of_books` **在** |
| `cur_bone_pile` | `Haunted` | **13** | ✅ `pile_of_bones` **在** |
| `cur_discarded_camp` | `Scrounging` | **16** | ✅ `discarded_pack` **在** |
| `cur_supply_crate` | `Treasure` | **17** | 🔴 `crate` **不在**（`crate` 是 `Scrounging`）⚠️ |
| `cur_sconce` | `Light` | **0** | 🔴 **该 tag 不是参考标签** ⚠️ |

```
📊 **4/6 通过 tag 检验**（我猜的那条确实带该 tag）✓
🔴 **2/6 没通过** —— 而这两条**恰好暴露了真问题**：
   ① `cur_supply_crate`（补给箱，`Treasure`）⇒ 参考的 `crate` **是 `Scrounging` 不是 `Treasure`** ⚠️
      ⇒ 📌 **要么我猜错了对应条**（`Treasure` 下 17 条里可能有更合适的），
        **要么我方的 `curio_type` 标错了** ⇒ 🔴 **两种可能都要人核** ✓
   ② `cur_sconce`（壁灯，`Light`）⇒ 🔴 **`Light` 根本不是参考标签**，
      且参考 tags 里**没有任何"光/火把"相关词** ⚠️
      ⇒ 🎖️ **判据（可执行）**：比 `FULL CURIO?` 的效果串 ——
         参考 `sconce` = `[Effect, No, Purge]` + `REGION FOUND [Loot, 1]`
         我方 `cur_sconce` = `{chance:100, kind:"light", amount:30}`
         ⇒ 🔴 **两者效果【不同】**：参考那条**不给光**（`FULL CURIO? = No`）⚠️
         ⇒ 📌 而 `PLAN_adoption §7.3` 提过 `sconce` 给的是 **`TORCHONLY`**（另一个 sconce 变体）
      ⇒ ✅ **结论：`cur_sconce` 与 `sconce` 只是名字像，效果不同** ⇒ **必须降为"待核"** ✓
```

## 5. 🔴 而"tag 相同"仍**不等于**"就是同一条"（纪律 BL 的提醒）

```
以 `Worship` 为例：8 条共有该 tag ——
   `altar_of_light` · `confession_booth` · `holy_fountain` · `sacrificial_stone` ·
   `skull_altar` · `troubling_effigy` · `fish_idol` · `bas_relief` ✓
⇒ 🔴 **一个 tag 平均筛出 8~17 条** ⇒ ⚠️ **tag 相同只能缩小范围，不能定条** ✓
✅ **定条要再比一层**：**`kind`（Good/Mixed/Bad）** + **`FULL CURIO?` 的效果串** ✓
📌 即 **三段判据**：**① tag 命中 → ② kind 一致 → ③ 效果串逐项对上** ⇒ 才可升"明确" ✓
```

## 6. 诚实边界

```
✅ **能验**：CSV 分段（60 块 · TAGS 恒 3 行）· 真 id 60/60 · TAGS 词表 17 个（**去噪**）·
   我方 `curio_type` 与参考 tag **交集 5 个** · 4/6 通过 tag 检验 ·
   `cur_sconce` 效果串**确实不同** —— **全部当场跑出** ✓
🎖️ 并**如实记下上一版的三个错**（未收续行 · 列语义随段变 · 段长只看了 1 条）✓
🔴 **不能验**：**配对仍是【候选】** ——
   tag 相同平均有 8~17 个候选 ⇒ ⚠️ **没有升到"明确"** ⇒ **不许据此落库**（纪律 BL）✓
   ⇒ 📌 **要定条还差第 ②③ 段判据**（`kind` + 效果串逐项比）⇒ **归下一轮** ✓
🔴 **不能验**：`cur_supply_crate` 的 `Treasure` 标签**是我方标错还是对应错条**，**未判** ✓
🔴 **不能验**：CSV 的 `Item Interactions` 段内小表**仍未解析** ⇒ 记为**未做** ✓
🔴 **不能验**：**没有落任何奇物数据** ⇒ "接上 60 条后跑图会怎样"**完全未测**（纪律 BK）✓
```

## 7. 下一步

```
🆕 **可做（归下一轮）**：① **第 ②③ 段判据** —— 逐条比 `kind` + `FULL CURIO?` 效果串
   ② `cur_supply_crate` 的 `Treasure` 定性（我方标错？还是对应错条？）
   ③ CSV 的 `Item Interactions` 段内小表 ✓
⏸️ **等策划**：三张单已投递（Σ / A6 / A9+A10+总口径）✓
```
