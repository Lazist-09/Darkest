# 障碍/陷阱对账：**名字交集 0** —— 两边按**完全不同的维度**切（参考按"种类" · 我方按"地牢"）

> 🕒 2026-09-26 · 工具 `tools/dsh/crosscheck_obstacles_traps.py`（新，可重跑）✓
> 产物 `reports/unity_ref/obstacles_traps_crosscheck.json` ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 📊 规模相近（5+4 vs 4），但**名字交集 0**

| 集合 | 名字 |
|---|---|
| **参考障碍（5）** | `thorny_thicket` · `rubble` · `shipwreck` · **`ancestor`** · `town_rubble` |
| **参考陷阱（4）** | `poison_cloud` · `spikes` · `blade_wheel` · `lurker` |
| **我方 `trap_defs.json`（4）** | `ruins_pit` · `weald_snare` · `warrens_spike` · `cove_grate` |
| 🔴 **交集** | **0 / 0** |

```
🎖️ **不是"漏抄"** —— 看命名就知道是【两种切法】：
   · **参考** ⇒ 按【物件种类】（荆棘丛 · 瓦砾 · 沉船 · 毒云 · 尖刺 · 刀轮 · 潜伏者）✓
   · **我方** ⇒ 按【地牢】（遗迹·陷坑 / 荒野·绊索 / 贫民窟·尖刺 / 海湾·栅栏）✓
⇒ 🎖️ **即：我方是"每个地牢一个代表陷阱"，参考是"一组通用障碍+陷阱"** ✓
   📌 而**我方的 `note` 自己写着"DD 原文 25%/28%/30%（按难度档）"**
      ⇒ ✅ **即：我方知道原版有难度档，但选了"取首档"** ✓
   🎖️ **判据（第 52 条）**：**"名字交集 0 —— 是【漏抄】还是【切法不同】？
      ⇒ 看两边的【分组维度】（名字里带地牢名？还是带物件名？）"** ✓
```

## 2. 🎖️ 而**字段**也完全不同（这才是实质差异）

### 参考的障碍（5 字段）

| 字段 | 值 | 说明 |
|---|---|---|
| **`fail_effects`** | `["Stress 2"]` | 🔴 **失败时施加 Effect**（**引用 Effects.txt**）|
| **`health`** | **`-0.05`** | 🔴 **负数 = 百分比**（`-0.05` = 5%）⚠️ |
| **`torchlight`** | **`-20.0`** | 🔴 **火把变化**（**绝对值，不是百分比**）⚠️ |
| **`ancestor_talk`** | `false` | 🔴 **是否触发先祖旁白**（**只有 `ancestor` 是 `true`**）|

```
🎖️ **3 条读数**：
   ① **`health` 是【分数】**（`-0.05` = −5%）⇒ ✅ **与 A1/A3/A6 那条"参考一律存分数"【第 5 例】** ✓
   ② **`torchlight` 是【绝对值】**（`-20.0`）⇒ ⚠️ **与 `health` 的量纲不同** ✓
      🎖️ **同一条目里两个字段，一个分数一个绝对** ⇒ ✅ **与"`int` vs `float` 的 chance"同族** ✓
   ③ **4/5 个障碍的数值【完全相同】**（`thorny_thicket`/`rubble`/`shipwreck`/`town_rubble`
      都是 `Stress 2` + `-0.05` + `-20.0`）⇒
      🎖️ **即：它们只是【同一机制换皮】** ⇒ 而 **`ancestor` 是唯一的特殊项**（`ancestor_talk: true`）
      📌 **判据（第 53 条）**：**"N 个条目数值全同 —— 那是【N 个东西】还是【1 个机制 × N 层皮】？"** ✓
```

### 参考的陷阱（5 字段）

```
`poison_cloud` ⇒ `success: ["Heal Stress TrapD"]` · `fail: ["Blight 1", "Stress 2"]` · `health: 0`
`spikes` ⇒ `success: ["Heal Stress TrapD"]` · `fail: ["Stress 2"]` · **`health: -0.25`**
`blade_wheel` ⇒ `success: [… TrapD]` · `fail: ["Bleed 1", "Stress 2"]` · `health: -0.1`
`lurker` ⇒ `success: [… TrapD]` · `fail: ["Lurker Trap Debuff 1", "Stress 2"]` · `health: -0.1`
🎖️ **而 4/4 都有 `difficulty_variations`**（`level: 3` …）⇒
   ✅ **即：陷阱**有**难度分档**，而**障碍没有** ⚠️
   📌 而那正是**我方 `note` 里提到的"按难度档"** ⇒ ✅ **我方知道，但只取了首档** ✓

🔴 **并发现一个共用常量 `"Heal Stress TrapD"`** ⇒ **4/4 陷阱的成功效果【完全相同】** ✓
   ⇒ 🎖️ **即："成功"这条分支在参考里是【统一的】** ✓
```

### 我方的 4 个陷阱（5 字段）

```
`ruins_pit`   ⇒ `region: "ruins"` · `hp_percent: 25.0` · `weight: 1` · 🔴 **`note: "placeholder:true"`**
`weald_snare` ⇒ `region: "weald"` · `hp_percent: 5.0`  · …
`warrens_spike` ⇒ `region: "warrens"` · `hp_percent: 10.0`
`cove_grate`  ⇒ `region: "cove"` · `hp_percent: 10.0`
🎖️ **顶层还有 4 个全局常量**：`unscouted_dodge_percent` · `disarm_bonus_percent` ·
   `stress_damage` · `disarm_stress_heal` ✓
⇒ 🔴 **即：我方【有解除/闪避机制】，而参考的障碍/陷阱【没有这两个字段】** ⚠️
   📌 而参考的"解除"可能在**别处**（技能侧）⇒ 记**待核** ✓
```

## 3. 🎖️🎖️ 所以这条线的**结论分三层**

```
① ✅ **规模相近是巧合** —— 5+4 vs 4 看着像，实则**切法不同**（种类 vs 地牢）✓
② 🔴 **字段维度完全不同**：
   | | 参考 | 我方 |
   |---|---|---|
   | 效果 | **`fail_effects`/`success_effects` 引用 Effect 名** | 🔴 **只有 `hp_percent`** |
   | 火把 | **`torchlight: -20.0`** | 🔴 **无** |
   | 旁白 | **`ancestor_talk`** | 🔴 **无** |
   | 难度分档 | **陷阱有 `difficulty_variations`** | 🔴 **无**（`note` 里承认只取首档）|
   | 解除/闪避 | 🔴 **无** | ✅ `disarm_bonus_percent`/`unscouted_dodge_percent` |
   | 归属 | 🔴 **无**（通用）| ✅ `region`（按地牢）|

③ 🔴 **我方 4 条全部标着 `placeholder:true`**（策划 `#307`）⇒
   ✅ **即：我方自己知道这是占位** ✓
   ＋ 并注明"DD 原文 25%/28%/30%（按难度档）⇒ **本项目取首档 25%**" ✓
   ⇒ 🎖️ **即：这不是"抄错"，是【我方有意的简化】** ⇒ ⚠️ **采用参考时要一并改机制** ✓
      📌 **判据（第 54 条）**：**"我方标了 `placeholder` ⇒ 那是【待替换】还是【有意简化】？
         看 `note` 里有没有【为什么】"** ✓
```

## 4. 诚实边界

```
✅ **能验**：**参考 5 障碍 / 4 陷阱的逐条全文** · **字段全集（5 / 5）** ·
   **`health: -0.05` 与 `torchlight: -20.0` 的量纲差** · **4/5 障碍数值全同** ·
   **4/4 陷阱共用 `"Heal Stress TrapD"`** · **我方 4 条的 `region`/`hp_percent`/`placeholder`** ·
   **名字交集 0** —— **全部当场跑出** ✓
🎖️ 并**判定为"切法不同"而非"漏抄"**，且**指出我方是有意简化**（`note` 为证）✓
🔴 **不能验**：**参考的"解除陷阱"机制在哪**（我方有 `disarm_*`）⇒ 记**待核** ✓
🔴 **不能验**：**`torchlight` 是绝对值还是百分比** ⇒ 📌 见 `-20.0` **推为绝对值**（火把量纲 0~100）⇒ 记**推断** ✓
🔴 **不能验**：**`difficulty_variations` 的完整内容**（只见到 `level: 3` 开头）⇒ 记**未读全** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 无 scratch 依赖（工具本身入库）✓
```

## 5. 下一步

```
✅ **障碍/陷阱对账【完成】**：交集 0 · 切法不同 · 字段维度不同 · 我方是有意简化 ✓
🆕 **可做**：① 读 `difficulty_variations` 的完整内容（参考的陷阱难度分档 —— 采用时要补）
   ② 找参考的"解除陷阱"机制（我方 `disarm_*` 的对应）
   ③ 把第 52/53/54 条判据补进 `observe_list` D11
   ④ **把 A12 的 obstacles/traps 补进 `PLAN_adoption`**（A12 行现在只提了 curios）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
