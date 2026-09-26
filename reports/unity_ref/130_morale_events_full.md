# `morale_events` 表**全数**：**18 条**（我只读了 3 条）· **3 条无消费点**

> 🕒 2026-09-26 · 工具 `tools/dsh/audit_morale_events_table.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `129_*.md §6` 的"3 条不能当全量"** ✓

---

## 1. 🎖️ 全数：**18 条**（不是我读的 3 条）

```
📊 **`morale_events.json`**：顶层 `events` ⇒ **18 条** ✓
   🎖️ **判据（第 183 条）**：**"我读了一个表的 3 条 ——
      表里一共几条？（别拿样本当全量）"** ✓
      ⇒ ✅ **实测 18 ⇒ 我读的 3 条只占 17%** ✓
```

### 字段全集（7 种）

| 字段 | 出现 |
|---|---|
| `id` · `name` · `delta` · `scope` · `occurrence` · `source` | **18/18**（全有）✓ |
| **`note`** | **10**（部分有）✓ |

### 取值分布

```
🎖️ **`scope`（4 种）**：`self` **9** · `team` **6** · **`survivors` 2** · **`per_target` 1** ✓
🎖️ **`occurrence`（3 种）**：`normal` **16** · **`once_per_battle` 1** · **`once_per_turn_max1` 1** ✓
⇒ 🎖️ **即：我上一件只见到 `self`/`per_target` 两种 `scope`** ⇒
   🔴 **实际有 4 种**（多了 **`team`** 与 **`survivors`**）⚠️
   ⇒ ✅ **这正是"样本当全量"的代价** ✓
```

## 2. 📊 18 条全表（`id` / `delta` / `scope` / `occurrence`）

| `id` | Δ | `scope` | `occurrence` |
|---|---|---|---|
| `physical_hit_no_effect` | **0** | self | normal |
| `mental_hit` | −8 | self | normal |
| `mental_crit_hit` | −12 | self | normal |
| `mental_aoe_hit` | −5 | **per_target** | normal |
| `critical_strike_dealt` | **+5** | team | normal |
| `kill_enemy` | **+10** | team | normal |
| `ally_enters_weak` | −8 | team | normal |
| `ally_death` | **−15** | team | normal |
| `support_slot_turn_start` | **+3** | self | normal |
| `battle_inspiration` | **+15** | self | normal |
| `morale_full_100` | +10 | team | **once_per_battle** |
| `retreat_success` | **−12** | **survivors** | normal |
| `retreat_success_with_death` | −15 | **survivors** | normal |
| `retreat_fail` | −5 | team | normal |
| `weak_hit_any_damage` | −5 | self | **once_per_turn_max1** |
| `physical_crit_hit_self` | −10 | self | normal |
| `physical_crit_hit_ally` | −5 | self | normal |
| `critical_heal` | **+4** | self | normal |

```
⇒ 🎖️ **即：18 条里【正负都有】**（+5/+10/+3/+15/+10/+4 共 6 条为正）✓
   ⇒ 📌 **即：士气表不只扣，也加** ⇒ ✅ **与我方"士气 0~100"的设计一致** ✓
```

## 3. 🔴 而**3 条只在配置白名单里，没有消费点**

```
📊 **`MoraleEventsConfig.cs:43-51`** 是个 **`RequiredIds` 白名单**：
   > `/// <summary>§5.2 表必选 id（缺行 = 启动报错，P9）。</summary>`
   ⇒ 🎖️ **即：这 18 个 id 是【启动时校验必须存在】的** ✓
🔴 **而其中 3 条【除白名单外无人读】**：
   · **`physical_hit_no_effect`**（Δ=**0**）⇒ 白名单 1 处 · 非配置 **0** 处 ⚠️
   · **`battle_inspiration`**（Δ=**+15**）⇒ 白名单 1 处 · 非配置 **0** 处 ⚠️
   · **`retreat_success_with_death`**（Δ=**−15**）⇒ 白名单 1 处 · 非配置 **0** 处 ⚠️
⇒ 🎖️ **判据（第 184 条）**：**"这个 id 【被校验存在】≠【被使用】——
   要分开数【白名单里的】与【真有消费点的】"** ✓
   📌 与第 51 条（"13/15 有消费"）**同族** ✓
⇒ 🔴 **而三条的性质不同**：
   · **`physical_hit_no_effect` Δ=0** ⇒ 📌 **"无效果"的占位** ⇒ ✅ **合理**（表完备性）✓
   · 🔴 **`battle_inspiration` Δ=+15** ⇒ ⚠️ **而 `skills.json` 里有同名技能**
     （`commissar_battle_inspiration`）⇒ 📌 **可能走【技能侧】而非事件表** ✓
   · 🔴 **`retreat_success_with_death` Δ=−15** ⇒ ⚠️ **而 `OvertimeAndRetreat.cs:289` 只用了
     `retreat_success`/`retreat_fail`** ⇒ 📌 **"撤退成功但有死亡"这个分支【未实现】** ✓
```

## 4. 🔴 而 `battle_inspiration` / `retreat_success_with_death` **值得报**

```
📊 **两条都是"定义了但没接"**：
   · **`battle_inspiration`** ⇒ 表里 **+15** · 白名单校验存在 ⇒ ⚠️ **但无消费点**
     ⇒ 📌 **而同名技能在 `skills.json`** ⇒ ✅ **可能"士气由技能给，事件表那条是冗余"** ✓
   · **`retreat_success_with_death`** ⇒ 表里 **−15** ⇒ ⚠️ **但撤退代码只判成功/失败两分支**
     ⇒ 📌 **"成功但有伤亡"这个中间态【没实现】** ⇒ 🔴 **是本仓的真实缺口** ✓
⇒ 🎖️ **判据（第 185 条）**：**"表里定义了 N 条 ——
   而代码只用了 M 条（M<N）⇒ 【差的那些】是漏实现还是预留？"** ✓
   📌 **要逐条看语义**（`physical_hit_no_effect` 是预留 · `retreat_success_with_death` 像漏实现）✓
```

## 5. 诚实边界

```
✅ **能验**：**18 条全表（id/delta/scope/occurrence 逐条）** · **字段全集 7 种（`note` 10/18）** ·
   **`scope` 4 种 · `occurrence` 3 种** · **`RequiredIds` 白名单的 18 个** ·
   **3 条无消费点（逐条名字）** · **`get` 是 fail-fast** —— **全部当场跑出** ✓
🎖️ 并**把"3 条无消费点"分类**（预留 vs 疑似漏实现）✓
🔴 **不能验**：**`battle_inspiration` 是否真由技能侧给士气** ⇒
   📌 要读 `commissar_battle_inspiration` 的 `morale_effects` ⚠️ ⇒ 记**未核** ✓
   🎖️ **而那是可验的**（下一轮可做）✓
🔴 **不能验**：**`retreat_success_with_death` 是漏实现还是预留** ⇒
   📌 代码只判两分支 ⇒ ⚠️ **倾向"漏实现"** ⇒ 但**不能自己判** ⇒ 记**待报/待裁** ✓
🔴 **不能验**：**`note` 那 10 条写的是什么** ⇒ 记**未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{morale_ids,morale_whitelist}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **`morale_events` 表【全数】**：18 条 · 字段 7 种 · 3 条无消费点 ✓
🆕 **两条待核/待报**：① `battle_inspiration` 是否由技能侧给（可验）
   ② `retreat_success_with_death` 是漏实现还是预留（待报）
🆕 **可做**：① 🔴 **核 `commissar_battle_inspiration` 的 `morale_effects`**
   ② 读 `note` 那 10 条
   ③ 把第 183~185 条判据补进 `observe_list` D11
   ④ **把我方士气表 + 白名单机制写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
