# act-out 的 `data` 语义查清：`number_value` 是**分数** · `string_value` 指向 **Effect**（6/6 命中）

> 🕒 2026-09-26 · 承接 `42_battle_modifier_audit.md §7` 的"`NumberParameter` 取值未核" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ `number_value` 是**分数**（不是百分比整数）

```
📊 数据侧实测（12 条 trait 的回合开始 act-out，只列 `chance > 0`）：
   · **`attack_self`**：`number_value = **0.1**`（3 条：`masochistic`/`depressed`/`irrational`）✓
   · **`heal_self`**：`number_value = **0.1**`（1 条：`vigorous`）✓
   · 其余 10 种：`number_value = **0.0**`（不用这个参数）✓
🎖️ **语义（读代码）**：
   · `AttackSelf` ⇒ `TakeDamagePercent(actOut.NumberParameter)`
      ⇒ `TakeDamage(最大血量ModifiedValue × **0.1**)` ⇒ **最大血量的 10%** ✓
   · `HealSelf` ⇒ `HealPercent(actOut.NumberParameter)` ⇒ **同样 10%** ✓
⇒ ✅ **即：`number_value` 是【分数】** ⇒ 🔴 **与 buff 的 `amount` 【同量纲】** ⚠️
   📌 **这是第三处"分数 vs 整数百分比"**：
      ① `JsonBuffs.amount`（阻塞 A7）· ② 怪物 `prot`（A4）· ③ **act-out 的 `number_value`（本件）** ✓
   ⇒ 🎖️ **一条重要推论**：**"参考的百分比都存分数"是【系统性的】**，不是个别字段 ✓
      ⇒ ✅ **对我方的意义**：我方一律存**整数百分比** ⇒ **这一整类转换要【一次定口径】** ✓
         📌 而 A1 那条契约单（`contract_change_request_buff_primitives.md §1.3`）问的正是它 ✓
         ⇒ 🎖️ **本件给它补了第二个实例**（不止 buff，act-out 也是）✓
```

## 2. 🎖️ `string_value` **全部命中** Effect 表（6/6）

```
📊 数据里出现的 `string_value`（**6 个**）⇒ 拿 `Mechanics/Effects.txt`（176,745 B）逐个查：
   ✅ **6/6 全部命中** ✓ ⇒ **引用链闭合** ✓
```

| `string_value` | `Effects.txt` 里的定义（节选） |
|---|---|
| **`BarkStress`** | `.target "target"` · `.curio_result_type "negative"` · **`.stress 6`** · `.on_hit true` |
| **`FocusedAllyBuff`** | `.target "target"` · `positive` · **`.attack_rating_add 10%`** · `.crit…` |
| **`HealStressVirtued`** | `.target "target"` · `positive` · **`.healstress 15`** |
| **`HealStressVirtuedParty`** | `.target "target"` · `positive` · **`.healstress 4`** |
| **`Mark Self`** | `.target "**performer**"` · positive · **`.tag 1`** · `.duration 1` |
| **`PowerfulPartyBuff`** | `.target "target"` · positive · **`.damage_low_multiply 15%`** |

```
🎖️ **三条立刻可用的读数**：
   ① **`BarkStress` 的压力值是【硬编码在 Effect 里】** ⇒ `.stress 6`
      ⇒ 📌 **即：act-out 的"效果大小"不在 trait 数据里，而在 Effect 表里** ✓
      🎖️ **对本任务的意义**：**采用 act-out 时必须【一并搬 Effect 表】** ⚠️
         （只搬 trait 的 act-out 是**不完整的**）✓
   ② **`Mark Self` 的 `.target "performer"`** ⇒ **"施法者自己"** ✓
   ③ 🔴 **`Mark Self` 与 `BarkStress` 的 `.on_hit true / .on_miss false`**
      ⇒ 🎖️ **即：act-out 用的 Effect 也带 `on_hit`/`on_miss` 语义** ✓
      📌 而 `PLAN_adoption §7.3 ⑤` 记过"**未命中仍会施加效果**（除非显式 `on_miss false`）"
         ⇒ ✅ **本件在 act-out 这条线上【印证了那条】** ✓
```

## 3. 🎖️ 顺带量出：**`string_value` 有 6 种，`number_value` 只有 2 种在用**

```
📊 **`number_value` 非零的只有 2 种 act-out**（`attack_self` / `heal_self`，都 = `0.1`）✓
📊 **`string_value` 非空的有 6 种**（`bark_stress`/`buff_party`/`buff_random_party_member`/
   `mark_self`/`stress_heal_party`/`stress_heal_self`）✓
⇒ 🎖️ **即：12 种 act-out 里**：
   · **2 种用数值**（`0.1` 固定）✓
   · **6 种用字符串**（指向 Effect）✓
   · **4 种两者都不用**（`nothing`/`change_pos`/`ignore_command`/`random_command`）✓
   📌 **而它们与 §32 的 11 个 case【对得上】**（另加 `nothing` 无 case）✓
```

## 4. 🎖️ 对采用的**修正**：act-out 不是"自包含"的

```
🔴 **本件最重要的一条**：**act-out 的效果【依赖 Effect 表】** ⚠️
   ⇒ 📌 **采用 act-out 的完整依赖链**：
      `traits.json`（12 条 act-out 数据）
        ⇒ 需要 **`Effects.txt`（952 条 effect）** ← 🔴 **我方【没有这张表】** ⚠️
        ⇒ 而 Effect 又引用 **buff 原语**（如 `FocusedAllyBuff` 的 `combat_stat_buff`）
   ⇒ ✅ **即：A6 的落库顺序应当是**：
      ① **先落 Effect 表**（新建一层）
      ② 再落 act-out ✓
      🎖️ **否则抄下来的 act-out【只是空壳】**（`string_value` 指不到东西）✓
      📌 **判据**：**"这段数据引用了【哪张表】？那张表我方有吗？"** ——
         **没有 ⇒ 它是"前置"，不是"同批"** ✓
      🎖️ **与 A7 那次同族**（饰品依赖 buff 池），但**这次多一层**（act-out → Effect → buff）✓
```

## 5. 诚实边界

```
✅ **能验**：**12 种 act-out 的 `number_value` / `string_value` 完整取值** ·
   **`0.1` 的语义（= 最大血量 10%）** · **6 个 `string_value` 全部命中 `Effects.txt`** ·
   **6 个 Effect 的定义原文** · **`number_value`/`string_value` 在用种数（2 / 6）** ——
   **全部当场跑出** ✓
🎖️ 并**给 A1 那条契约单补了第二个"分数"实例**（`number_value`）✓
🔴 **不能验**：`Effects.txt` 的**完整结构未抽**（952 条）⇒
   📌 本件只**按名字查了 6 处** ⇒ 记**未抽全表** ✓
🔴 **不能验**：那 6 个 Effect 引用的 **buff 原语是否都在 A1 池里** ⇒ 记**未核** ✓
🔴 **不能验**：**没有落任何 act-out 数据** ⇒ 接上后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/numberparam*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **act-out 的 data 语义【查完】**：分数 + Effect 引用（6/6 命中）✓
🆕 **新暴露一条硬前置（重要）**：🔴 **act-out 依赖 `Effects.txt`（952 条）而我方没有**
   ⇒ 📌 **A6 的落库顺序要改成：先 Effect 表 → 再 act-out** ✓
🆕 **可做**：① 抽 `Effects.txt`（952 条）—— 它是 A6 的**直接前置**
   ② 扫 `darkest/scenes/**` 节点名一致性
   ③ 核那 6 个 Effect 的 buff 引用是否都在 A1 池里 ✓
⏸️ **等策划**：八张单已投递 ✓
```
