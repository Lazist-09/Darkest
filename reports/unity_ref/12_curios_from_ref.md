# A12 下游：奇物 / 障碍 / 陷阱 —— 抽取与 A12 交叉校验

> 🕒 2026-09-26 · 工具 `tools/dsh/extract_ref_curios.py`（可重跑）·
> 产物 `reports/unity_ref/curios_from_ref.json`（131,987 bytes）✓
> 🔴 **只抽不落库**：`darkest/**` 一个字节没动 ⇒ **零行为** ✓

---

## 1. 🎖️ 为什么要抽它们（**A12 的直接下游**）

```
A12 的 `props` 段引用了 **57 个名字**（奇物/宝藏/陷阱/障碍）——
   而我方无奇物表、参考的这三份未抽 ⇒ **那 57 个名字当时【未与任何表校验】** ⚠️
⇒ 本件补上这三份 ⇒ ✅ **A12 的 props 段现在可校验，且 57/57 全部命中** ✓
```

## 2. 三个源，**三种形态**（正好各不相同）

```
① `Curios/Curios.csv` —— **CSV**（51,069 B · **18 列**）⇒ 🔴 **本任务的第一个 csv 形态** ⚠️
   📌 我方现有抽取器**只处理 json/bytes/txt** ⇒ 这是**新形态** ✓
② `Curios/Obstacles.json` —— JSON（`{props: [...]}` · **5 条** · 无尾随逗号）✓
③ `Curios/Traps.json` —— JSON（**4 条** · 🔴 **有尾随逗号**，`§10.2 ⑤` 已记）✓
```

## 3. 🔴🔴 CSV 的块状结构 —— 我**第一版数错了（573 vs 60）**

```
🔴 **第一版**：把**每个含 `ID STRING` 的行**都当数据行 ⇒ 数出 **573 个 "id"** ⚠️
🔴 **实际**：`ID STRING` 是**每条奇物块的表头** ⇒ 那 573 个里有
   `REGION FOUND`×**60** · `FULL CURIO?`×**60** · `TAGS`×**60** · `Item Interactions`×**60** ·
   `ALL`×14 … ⇒ **全是字段名，不是奇物** ⚠️
✅ **正确形状**（实测）：**表头行 60 个 · 间隔恒 15 行**
   块结构：
     行 1（块首）：`  | 1 | Unlocked Strongbox |  | Good | …`   ← **序号 · 显示名 · 类型**
     行 2（表头）：`  |  | ID STRING |  | RESULT TYPES | WEIGHT | % CHANCE | …`（**18 列**）
     行 3+（数据）：每行一条结果
        `  |  | unlocked_strongbox |  | Nothing | …`           ← **真 id 在这里**
        `  |  | REGION FOUND |  | Loot | 3 | 75.00% | A | 2 | …`
        `  |  | ALL |  | Quirk | …`
        `  |  | FULL CURIO? |  | Effect | 1 | 25.00% | Blight 1 | …`
        `  |  | TAGS |  | Treasure | All | …`
        `  |  | Item Interactions |  | ITEM | RESULT TYPE | …`  ← **段内小表**
⇒ ⇒ **60 条奇物** ✓ —— 🎖️ **与 `PLAN_adoption §9①` 的"60 条"逐数相符** ✓
🎖️ **纪律 BH 又一次**：**换个口径（按块 vs 按行），数就从 60 变成 573**
   ⇒ **我读错了口径** ✓（这是本任务里第 N 次同族，已记进代码注释）
```

## 4. 读数（可复算）

```
`Curios.csv` ⇒ **60 条** · 有 id **60** · 去重 **60** ✓
   `kind` 分布：**`Good` 23 · `Mixed` 31 · `Bad` 6** ✓
   样例：`1 Unlocked Strongbox / unlocked_strongbox` · `2 Locked Strongbox` ·
      `3 Goal Strongbox` · `4 Heirloom Chest` · `5 Eldritch Altar` · `6 Altar of Light` ✓
   🎖️ **每条带 `REGION FOUND` / `ALL` / `FULL CURIO?` / `TAGS` 四组元信息** +
      **若干结果行**（`RESULT TYPES` = `Nothing`/`Loot`/`Quirk`/`Effect`/`Purge`/`Scouting`/…）✓
`Obstacles.json` ⇒ **5 条**（`thorny_thicket` · `rubble` · …）带
   `fail_effects` / `health` / `torchlight` / `ancestor_talk` ✓
`Traps.json` ⇒ **4 条**（`poison_cloud` · `spikes` · `blade_wheel` · `lurker`）
   **每条 2 个难度变体** ✓ · 带 `success_effects` / `fail_effects` / `health` ✓
```

## 5. 🎖️ 与 A12 的交叉校验：`props` 段 **57/57 全部命中**

```
A12 `props` 段引用的名字**去重 57 个** ⇒ 拿本件三份表（60 + 5 + 4 = 69 个名字）逐个查：
   🔴 **未命中 0** ✓
⇒ 🎖️ **A12 的 `props` 段现在【完全可校验】** ✓
   📌 而这也**补上了 A12 当时明确记下的那条"不能验"**（"57 个名字未与任何表校验"）✓
```

## 6. 诚实边界

```
✅ **能验**：CSV 的块结构（60 块 · 间隔 15）· 60 条 id 去重 · kind 分布 ·
   Obstacles 5 / Traps 4（各 2 难度变体）· **A12 props 57/57 命中** —— **全部当场跑出** ✓
   🎖️ 并**如实记下我第一版数错（573 vs 60）**及其根因 ✓
🔴 **不能验**：**没有落任何奇物数据** ⇒ "接上奇物后跑图会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：CSV 里 `Item Interactions` **段内小表未解析**（它是"用某物品交互的额外结果"）
   ⇒ 本件**只收了块级元信息与结果行**，**小表留白** ⚠️ ⇒ 记为**未验 / 未做** ✓
🔴 **不能验**：`% CHANCE` / `R1 %` 这些**百分比列的语义未核**
   （它们与 `WEIGHT` 并存 ⇒ 疑似"权重归算出的百分比"）⇒ 记为**推断** ✓
🔴 **不能验**：我方 `curios.json`（**7 条**）与参考（**60 条**）的**逐条对账未做** ⇒ 归下一轮 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/curio_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **A12 链条闭合**（`Dungeons/` 抽取 → `props` 段 57 名字 → 本件三份表校验 57/57）✓
🆕 **可做**：① 我方 `curios.json`（7）vs 参考（60）逐条对账
   ② CSV 的 `Item Interactions` 段内小表
   ③ `% CHANCE` / `R1 %` 的语义（与 `WEIGHT` 的关系）✓
⏸️ **等策划**：三张单已投递（Σ/A6/A9+A10+总口径）✓
```
