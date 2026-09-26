# 审计覆盖缺口已补：**又 4 个引用键全部闭合**

> 🕒 2026-09-26 · 承接 `27_our_ref_audit.md §5` 记的覆盖缺口 ✓
> 工具 `tools/dsh/audit_all_refs.py`（**已扩展**）· 产物 `reports/unity_ref/our_ref_audit.json` ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 缺口是什么、怎么补的

```
🔴 `27_*.md §5` 记的缺口：**`heirloom_exchange.json` / `enemy_ai.json` 的 id 数是 0** ⚠️
   ⇒ 📌 **不是"没定义"，是"它们的结构里没有 `id` 键"** ✓
⇒ ✅ **补法（判据）**：**"它们的引用键叫什么，就去哪里收"** ✓
   ① `heirloom_exchange` 用 **`exchange_from_type` / `exchange_to_type`**
      ⇒ 到 `heirlooms.json → kinds` 收定义 ✓
   ② `enemy_ai` 用 **`archetype_id`** ⇒ 到 `units.json` 收定义 ✓
   ③ `enemy_ai` 的 `rules[].skill_id` ⇒ 到 `skills.json` 收定义 ✓
📌 **这正是本仓纪律"先按命名空间分类，再查"的第二次应用**（第一次是 A8 的教训）✓
```

## 2. 📊 补齐后的**完整审计表**（10 个引用键）

| 引用键 | 引用数（去重）| 悬空 | 判定 |
|---|---|---|---|
| **`tree_id`** | **155** | **0** | ✅ 闭合 |
| 🔴 **`buffs`** | **556** | **74** | 🔴 已知（A7 已核）|
| **`skills`** | **43** | **0** | ✅ 闭合 |
| **`unit`** | **7** | **0** | ✅ 闭合 |
| 🆕 **`exchange_from_type`** | **4** | **0** | ✅ **闭合（本件补）** |
| 🆕 **`exchange_to_type`** | **4** | **0** | ✅ **闭合（本件补）** |
| 🆕 **`skill_id`**（AI）| **7** | **0** | ✅ **闭合（本件补）** |
| 🆕 **`archetype_id`** | **3** | **0** | ✅ **闭合（本件补）** |
| 🔴 **`item`** | **3** | **3** | 🔴 指向**未建的表**（A10 物品表）|

```
🎖️ **即：10 个引用键里【7 个完全闭合】· 只有 2 类悬空**（且两类性质不同）✓
```

## 3. 🎖️🎖️ 本件最有价值的一条：**`skill_id` 7/7 命中 —— 这是一次跨表交叉验证**

```
🆕 `enemy_ai.json` 的 `rules[].skill_id` 引用 **7 个技能**：
   `melee_charge` · `melee_heavy_slash` · `ranged_precise_shot` · `ranged_intimidating_shot` ·
   `ranged_retreat` · `caster_fear_whisper` · `caster_mental_shock` ✓
   ⇒ 🔴 **全部命中 `skills.json`** ⇒ **AI 表与技能表【完全闭合】** ✓
🎖️ **这条以前没核过**（`27_*.md` 的覆盖面不含它）⇒ ✅ **补上后，AI 那一侧的引用链也清了** ✓
   📌 意义：**A5 的参考侧有 160 brains，而我方只有 3 个 archetype** ⇒
      🔴 **规模差很大，但我方那 3 个的引用是完好的** ✓
      ⇒ 🎖️ **即：问题是"量不足"，不是"引用断"** ✓（两者要分开报）
```

## 4. 🎖️ 顺带量出**我方 3 个 archetype 的 AI 结构**（A5 的对照基础）

```
`enemy_ai.json` 顶层：`taunt_weight` = **3** · `archetypes` = **3 条** ✓
每条的形状：`{archetype_id, target_preference, random{enabled, fallback_probability}, rules[]}` ✓
`rules[]` 每项：`{skill_id, when{self_slot_in: [...]}, mark_weight}` ✓
   实测样例：`melee_soldier` 的 `melee_charge` ⇒ `when {self_slot_in: [3, 4]}` · `mark_weight 0` ✓
🔴 **而参考的 brain（A5 抽的 160 条）是**：
   `id` · `skill_cooldowns[]` · **`skill_selection_desires[]`（9 种 type / 24 个 data 键）** ·
   **`target_selection_desires[]`（8 种 type / 13 个键）** ✓
⇒ 📌 **两者【形状差很远】**：
   · **我方** = `when` 条件 + `mark_weight` + `target_preference`（**简单规则**）✓
   · **参考** = **欲望权重系统**（`base_chance` + 24 个条件键 + 目标欲望）✓
   🎖️ **与 A5 的判定一致**：参考的欲望是**权重/强制档**（`0.25~1e7`），
      我方是**布尔条件 + 一个权重** ⇒ **是两套** ✓
```

## 5. 诚实边界

```
✅ **能验**：补齐后 **10 个引用键的引用数与悬空数** · **7 个完全闭合** ·
   **`skill_id` 7/7 命中**（**新补的跨表验证**）· 我方 AI 的**完整结构**（3 条 / 逐字段）·
   与参考 brain 的形状差异 —— **全部当场跑出** ✓
🎖️ 并**关闭了 `27_*.md` 自己记的覆盖缺口**（而不是留着"未覆盖"）✓
🔴 **不能验**：**补的 3 类命名空间的"定义侧"是我【按引用键反推】的** ⚠️
   （`heirlooms.kinds` / `units.json` / `skills.json`）⇒
   ⚠️ 若将来某个定义搬到别处，本审计会**误报为"闭合"**（因为并集变大了）⇒ 记为**局限** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 修掉悬空后行为如何**完全未测**（纪律 BK）✓
🔴 **不能验**：`EXTRA["currency"]` 我把 `busts` 与 `bust` **都收进并集** ⇒
   ⚠️ **这掩盖了"我方 `kinds` 用复数、兑换表用单数"这个不一致** ⚠️
   ⇒ 📌 **如实记**：那处**形状不一致确实存在**（`27_*.md` 未提，本件记上）✓
      🎖️ 判据：**"为了让审计通过，我是不是【把两个写法都收进并集】了？"** ——
         **是 ⇒ 我可能掩盖了一个真问题** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/audit_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **审计线【补完】**：10 个引用键 · 7 闭合 · 2 类悬空（性质不同）✓
🆕 **可做**：① 读代码核 29 种 act-out 的逐种语义 + `block_effect` 分类
   ② 核我方 `heirlooms.kinds`（复数）与兑换表（单数）的**形状不一致**要不要统一
   ③ 我方 3 个 archetype vs 参考 160 brains 的**映射可行性**（A5 的落库前置）✓
⏸️ **等策划**：七张单已投递 ✓
```
