# 两批 `none` **完全同一集合**（16 条）—— 且 `damage_axis: mental` 有**一处语义存疑**

> 🕒 2026-09-26 · 承接 `126_*.md §6` 的两条"未列全/未验集合" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 集合验证：两批 `none` **完全相同**

```
📊 **实测**：
   · **`range_axis: none`** ⇒ **16 条**
   · **`damage_axis: none`** ⇒ **16 条**
   · **交集 ⇒ `16`** · **只在 range ⇒ `0`** · **只在 damage ⇒ `0`** ✓
⇒ 🎖️ **即：两批【完全同一集合】** ✓
   📌 **判据（第 173 条）**：**"两个轴的 'none' 数目相同 ——
      是【同一批技能】吗？验一下（不是巧合就是设计）"** ✓
      ⇒ ✅ **验完是【设计】**（同一批纯辅助/位移技能）✓
   📌 **而那 16 条的名字**（部分）：`move` · `tank_guard_wall` · `tank_hunker` ·
      `tank_taunt` · `tank_war_cry` · `warrior_war_cry` · `medic_group_bandage` ·
      `melee_charge` · `ranged_retreat` … ⇒ ✅ **全是无伤害的技能** ✓
```

## 2. 🎖️ 而 `damage_axis: mental` 的**完整 3 条**

| 技能 | `owner_unit` | 机制 |
|---|---|---|
| **`caster_fear_whisper`** | **`caster`** | `effects: [{stat_mod, resilience, -10, 2 回合}]` ✓ |
| **`caster_mental_shock`** | **`caster`** | `effects: []` · `morale_effects: []` · `tags: [output, aoe, ignore_stealth]` ⚠️ |
| 🔴 **`ranged_intimidating_shot`** | 🔴 **`ranged_archer`** | `effects: []` · **`morale_effects: [{scope: targets, delta: -4}]`** ⚠️ |

```
🎖️ **两条 caster 的** ⇒ ✅ **与"施法者"一致**（`fear_whisper` 削韧性 ⇒ 精神向）✓
🔴 **而 `ranged_intimidating_shot`（弓箭手）标 `mental`** ⇒ ⚠️ **存疑** ✓
   ＋ 它的机制是 **`morale_effects: -4`（削士气）** ⇒ 📌 **"精神"体现在【削士气】** ✓
   ＋ 而它的 **`range_axis` 是 `ranged`** ⇒ ⚠️ **物理箭矢** ✓
   ⇒ 🎖️ **即：它更像"物理伤害 + 削士气"，而不是"精神伤害"** ⚠️
      📌 **判据（第 174 条）**：**"这个字段值 ——
         与它自己的【其它字段】自洽吗？（`damage_axis` vs `range_axis` vs 机制）"** ✓
      ⇒ 🔴 **`ranged` + `mental`** 这个组合**在语义上别扭** ✓
```

## 3. 🔴 而 `caster_mental_shock` 也**值得一看**

```
📊 **它的字段**：`effects: []` · `morale_effects: []` · `tags: [output, aoe, ignore_stealth]` ⚠️
   ⇒ 📌 **即：除了 `damage_axis: mental`，它【没有任何"精神"向的机制】** ✓
      （没有削韧性、没有削士气）⚠️
   ⇒ 🎖️ **即：它的"精神伤害"【只体现在 `damage_axis` 这一个字段上】** ✓
      📌 **那意味着**：**它的"精神"完全靠【伤害计算走 SpiritHit】体现**
         （即**目标用韧性而非护甲来减伤**）✓
      ⇒ ✅ **这是"自洽"的一种**（`damage_axis` 就是决定走哪个公式）✓
   ＋ 🎖️ **与 `caster_fear_whisper` 对比**：那条**既有** `mental` **又有**削韧性 ⇒ ✅ 更完整 ✓
```

## 4. 🎖️ 所以"要不要报"要分清

```
📊 **两件事**：
   ① 🔴 **`ranged_intimidating_shot` 的 `damage_axis: mental`** ⇒
      **可能与"弓箭手 + 削士气"的设计不符** ⇒ ⚠️ **值得报**（**存疑**）✓
   ② ✅ **`caster_mental_shock` 的 `mental`** ⇒ **自洽**（它就是靠 `damage_axis` 决定公式）✓
⇒ 🎖️ **即：只有 ① 存疑** —— **而它也可以"是有意的"**（比如"威吓箭 = 精神冲击"）⚠️
   📌 **判据（第 175 条）**：**"我发现一个'看起来别扭'的组合 ——
      是【数据错】还是【有意设计】？⇒ 不能自己判，要【报出来】"** ✓
      📌 与我方"不代裁"的纪律一致 ✓
```

## 5. 诚实边界

```
✅ **能验**：**两批 `none` 的集合完全相同（16 = 16，差集空）** ·
   **那 16 条的名字样例** · **`damage_axis: mental` 的完整 3 条及其字段** ·
   **`ranged_intimidating_shot` 的 `morale_effects` 与 `range_axis`** ·
   **`caster_mental_shock` 的 `effects: []`** —— **全部当场跑出** ✓
🎖️ 并把"存疑"范围缩到 **1 条**，且区分"自洽"与"别扭" ✓
🔴 **不能验**：**`ranged_intimidating_shot` 标 `mental` 是【刻意】还是【笔误】** ⇒
   📌 **那要设计者判** ⇒ 记**待报/待裁** ✓
🔴 **不能验**：**`morale_effects` 与 `damage_axis` 有没有【耦合】** ⇒
   📌 即"削士气是否要求 `mental`" ⇒ ⚠️ **若没有耦合，那 `mental` 就是独立选择** ⇒ 记**未核** ✓
🔴 **不能验**：**我方 `SpiritHit` 是否【只】由 `damage_axis` 触发** ⇒
   📌 那要读我方代码 ⇒ 记**未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{none_sets,mental3}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **两批 `none`【验完同集】** · **`mental` 3 条【列全】**，其中 1 条存疑 ✓
🆕 **一条待报**：🔴 **`ranged_intimidating_shot` 的 `damage_axis: mental`**
   （弓箭手 + 削士气 ⇒ 标 `mental` 别扭）⇒ **请策划确认** ✓
🆕 **可做**：① 核 `morale_effects` 与 `damage_axis` 有无耦合（我方代码）
   ② 把第 171~175 条判据补进 `observe_list` D11
   ③ **把"`damage_axis` 属自加 + 1 条存疑"写进 `PLAN_adoption` §6** ✓
   ④ 汇总一次"我方自加机制"的**完整清单**（本任务已积累多项）✓
⏸️ **等策划**：十张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
