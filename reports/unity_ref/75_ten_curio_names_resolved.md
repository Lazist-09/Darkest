# 那 10 个 `curio_name` **确判为「任务专用交互物」** —— 参考的 `Curios/` 里 10/10 都没有

> 🕒 2026-09-26 · 工具 `tools/dsh/resolve_ten_curio_names.py`（新，可重跑）✓
> 产物 `reports/unity_ref/ten_curio_names_resolved.json` ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `74_*.md §4` 记的"①②两说待核"** ✓

---

## 1. 🔴🎖️ 判决：**是 ①「任务专用交互物」**（实测）

```
📊 **参考 `Curios/` 目录只有 3 个数据文件**：
   `Curios.csv`（**60 个通用奇物**）· `Obstacles.json`（5 障碍）· `Traps.json`（4 陷阱）✓
   ⇒ 🔴 **那 10 个名字在里面【10/10 都没有】** ✓
```

## 2. 🎖️ 而全 `Data/` 搜，它们的**唯一落点是 `JsonQuests.json`**

| 名字 | 出现在 |
|---|---|
| **`animalistic_shrine`** | `JsonQuests.json` |
| **`beacon`** | `JsonQuests.json` + `Localization/{Dialogue,Heroes,TownEvents}.xml` + **`Maps/DD_map2.bytes`** |
| **`chirurgeons_satchel`** | `JsonQuests.json` |
| **`corrupted_altar`** | `JsonQuests.json` |
| **`foodstuff_crate`** | `JsonQuests.json` |
| **`infected_corpse`** | `JsonQuests.json` |
| **`protective_ward`** | `JsonQuests.json` |
| **`reliquary`** | `JsonQuests.json` + `Localization/Curios.xml` |
| **`shipment_crates`** | `JsonQuests.json` |
| **`teleporter`** | `JsonQuests.json` + **`Maps/DD_map3.bytes`** |

```
🎖️ **10/10 都在 `JsonQuests.json`**，而 **0/10 在 `Curios/`** ⇒ ✅ **判决成立** ✓
   📌 **即：它们是【任务专用交互物】** —— 与 A8 当时的判定**一致**，而本件是**实测坐实** ✓
```

## 3. 🎖️ 三条**新的细读数**

```
① 🔴 **`beacon` 出现在 `Maps/DD_map2.bytes` · `teleporter` 出现在 `Maps/DD_map3.bytes`** ✓
   ⇒ 📌 **即：它们【被地图引用】** ⇒ ⚠️ **与 A12 记的"`Maps/*.bytes` 是 Unity 二进制、不可抽"【呼应】** ✓
      🎖️ **但名字能搜到** ⇒ ✅ **二进制里嵌了明文字符串** ⇒
         📌 **即：`Maps/*.bytes`【不是完全不可读】**（至少名字可读）✓
      ⚠️ **这与 `12_*.md` 的"7/7 不可抽"要区分**：
         · **结构**不可抽 ✓ · **字符串**可读 ✓ ⇒ ✅ **两句话都对，但要说清是哪一层** ✓

② 🔴 **`reliquary` 出现在 `Localization/Curios.xml`** ⚠️
   ⇒ 📌 **即：本地化里【有它的显示名】** ⇒ 这说明**它会被显示给玩家** ✓
      🎖️ 而**它不在 `Curios.csv`** ⇒ ✅ **即："会被显示" ≠ "是奇物"** ✓
      📌 与第 7 条判据（提及是引用还是恰好同名）**互补**：
         这次是**"本地化提到"不代表"是那种实体"** ✓

③ 🎖️ **A8 的 goal id 与名字【一一对应】**（`gather_*` / `activate_*` / `inventory_activate_*`）
   ⇒ 📌 **即：任务是"围绕这些交互物"设计的** ✓
      · **`gather_*`**（4 个）：`chirurgeons_satchel` · `foodstuff_crate` · `reliquary` ·
        `shipment_crates` ⇒ **收集** ✓
      · **`activate_*` / `inventory_activate_*`**（6 个）：`animalistic_shrine` · `beacon` ·
        `corrupted_altar` · `infected_corpse` · `protective_ward` · `teleporter` ⇒ **激活** ✓
```

## 4. 🎖️ 所以这条线的**最终形态**

```
📊 **两个命名空间，各自清楚**：
   · **A12 的 60 个** = **通用奇物**（`Curios.csv` · 有 `kind`/`region`/`results`）✓
   · **A8 的 11 个** = **任务专用交互物**（`JsonQuests.json` · 无 `kind`）✓
     🔴 其中 **`iron_maiden` 两个空间都有**（`74_*.md` 实测的交集 1）✓
     ⇒ 🎖️ **即：它既是通用奇物，又被某个任务指定** ⇒ ✅ **"交集 1"有了机制解释** ✓
⇒ 📌 **即：A8 记的"10 个未匹配"【不是错误】** ——
   而是**"10 个真·任务专用物 + 1 个跨空间复用"** ✓
   🎖️ **判据**：**"对不上的那些，是【数据错】还是【不同命名空间】？"
      ⇒ 去目标那边【逐个搜】，而不是【只看交集大小】"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**参考 `Curios/` 的 3 个文件** · **那 10 个在 `Curios/` 里 10/10 无** ·
   **全 `Data/` 搜的逐个落点（表格）** · **`beacon`/`teleporter` 在地图二进制里** ·
   **`reliquary` 在本地化里** · **goal id 的两类（gather 4 / activate 6）** ——
   **全部当场跑出** ✓
🎖️ 并**把 `74_*.md` 的"①②待核"判掉**（是①）✓
🔴 **不能验**：**`Maps/*.bytes` 里除了名字还能读出什么** ⇒
   📌 本件只确认"名字可搜到" ⇒ 记**未深挖** ✓
🔴 **不能验**：**那 10 个交互物在游戏里的确切外观/交互**（`JsonQuests` 之外的行为）⇒
   📌 可能要读代码 ⇒ 记**未做** ✓
🔴 **不能验**：**`iron_maiden` 为何被两处复用**（机制原因）⇒ 记**未知** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 无 scratch 依赖（工具本身入库）✓
```

## 6. 下一步

```
✅ **A8 × A12 奇物交叉【彻底收口】**：10 个=任务专用 · 1 个=`iron_maiden` 跨空间 ✓
🆕 **一条副产品**：**`Maps/*.bytes` 里名字可读**（与 `12_*.md` 的"不可抽"要分开说）
🆕 **可做**：① 用同法核 **A8 的 `item` 引用 × A10 的 57 个物品**
   ② 核 **A11 的兑换表**与那 60 个奇物有无关系（**应该无关**，可快速确认）
   ③ 把"对不上 ⇒ 去目标那边逐个搜"补进 `observe_list` D11 ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
