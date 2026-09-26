# `damage_axis`/`range_axis` 对照：**`range_axis` 与参考 `.type` 同构** · `damage_axis` 是**我方自加**

> 🕒 2026-09-26 · 工具 `tools/dsh/audit_axis_vs_type.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `125_*.md §6③` 的"核 `damage_axis`/`range_axis` vs 参考 `.type`"** ✓

---

## 1. 📊 两侧的**轴对照**

| | 我方 | 参考 |
|---|---|---|
| **射程轴** | **`range_axis`** 3 种：`melee` **19** · `none` **16** · `ranged` **9** | **`.type`** 2 种：`ranged` **50** · `melee` **47** |
| **伤害轴** | **`damage_axis`** 3 种：`physical` **25** · `none` **16** · `mental` **3** | 🔴 **无对应字段** |

```
⇒ 🎖️ **两条读数**：
   ① **`range_axis` ↔ `.type`【同构】**（都是 melee/ranged 之分）✓
      🔴 **而我方多了 `none`（16 条）** ⇒ 📌 **那是"不造成伤害的技能"**（如 `self` 类）✓
      📌 **而参考的 105 条【每条都有** `.type`**】** ⇒ ⚠️ **即使 `atk 0%` 也有** ✓
      🎖️ **判据（第 171 条）**：**"我方有两个轴 —— 参考有几个？
         多出来的那个是【自加】还是【参考用别的字段表达】"** ✓
```

## 2. 🔴 而 `damage_axis` 的"参考对应"**查了三个角度，全是否**

```
📊 **角度①：参考数据里的 `physical`/`mental`**：
   · `physical` **59 处** ⇒ 🔴 **56 在 `JsonQuirks.json`**（`"classification": "physical"`）
     ＋ 3 处在本地化 ⇒ ⚠️ **那是【怪癖分类】，不是伤害类型** ✓
   · `mental` **171 处** ⇒ 🔴 **107 在 `JsonQuirks.json`** ＋ 其余在**本地化文本**
     ⇒ ⚠️ **同样是【怪癖分类】** ✓
📊 **角度②：参考代码里的伤害类型字段**：
   · **`DamageType` ⇒ `0`** · **`damageType` ⇒ `0`** · **`PhysicalDamage` ⇒ `0`** ✓
   · `AttackRating` 13 处 ⇒ 🔴 **那是【命中属性】**（`AttributeType.AttackRating`），不是伤害类型 ✓
📊 **角度③：参考 `.type` 的取值集**：只有 **`melee`/`ranged`** ⇒ ✅ 不含物理/精神之分 ✓
⇒ 🎖️🎖️ **即：参考【没有"物理 vs 精神"这个伤害轴】** ✓
   📌 **判据（第 172 条）**：**"'换三个角度都找不到'——
      可以下'参考没有'的结论了（单个角度可能是筛子问题）"** ✓
      📌 与第 157 条（"筛子太严"）**对照**：**那次是筛子错，这次是三个角度都空** ✓
```

## 3. 🔴 但等一下 —— 参考**有** `MentalMitigation` 类的东西吗？

```
🔴 **我方 `damage_axis: mental` 对应我方 `BattleMath.SpiritHit`**（精神减免 · 韧性）
   ⇒ 📌 **而 `28_*.md`（`resilience` 属自加）已记过这一层** ✓
   ＋ 本任务 `118_*.md` 也记过：**参考 `resilience` 只 4 处且都是本地化** ✓
⇒ 🎖️ **即：`damage_axis` 是【我方自加】这个结论【与前几件一致】** ✓
   📌 而它涉 **3 条技能**：`caster_fear_whisper` · `caster_mental_shock` ＋ 1 条 ✓
      🎖️ **正好都是 `caster`（敌方施法者）** ⇒ ✅ **与我方"精神伤害"的设计一致** ✓
```

## 4. 🎖️ 而 `none` 在两个轴里**出现两次（各 16 条）** —— 值得记

```
📊 **`range_axis: none` 16 条** ＋ **`damage_axis: none` 16 条** ⇒ 🔴 **数目相同** ⚠️
   ⇒ 🎖️ **即：很可能【同一批技能】（纯辅助/位移类）** ✓
   📌 **而参考对这类技能【仍写 `.type`】**（如 `defender` 是 `.type "melee"` 但 `atk 0%`）✓
   ⇒ ⚠️ **即：我方用 `none` 显式标注，参考用"`atk 0%` + 依然有 type"** ✓
   🎖️ **判据（第 173 条）**：**"两个轴的 'none' 数目相同 ——
      是【同一批技能】吗？验一下（不是巧合就是设计）"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**我方两个轴的取值分布（各 3 种）** · **参考 `.type` 的 2 种（105 条全覆盖）** ·
   **`physical` 59 处里 56 在 `JsonQuirks`（怪癖分类）** ·
   **`mental` 171 处里 107 在 `JsonQuirks`** ·
   **参考代码 `DamageType`/`damageType`/`PhysicalDamage` 全 0** ·
   **`AttackRating` 是命中属性** —— **全部当场跑出** ✓
🎖️ 并**从三个角度确认"参考无伤害轴"** ✓
🔴 **不能验**：**`range_axis: none` 与 `damage_axis: none` 是否【同一批 16 条】** ⇒
   📌 本件只比了**数目**，未比**集合** ⚠️ ⇒ 记**未验集合** ✓
   🎖️ **而这是可验的**（下一轮可做）✓
🔴 **不能验**：**我方 `range_axis: none` 的设计意图** ⇒ ⚠️ **属策划** ⇒ 记**待裁** ✓
🔴 **不能验**：**`mental` 那 3 条技能的完整清单** ⇒ 📌 本件只点名 2 条 ⇒ 记**未列全** ✓
🔴 **不能验**：**参考的怪癖 `classification` 有没有 `mental`** ⇒
   📌 本件只见 `physical` 56 处 ⇒ ⚠️ **`mental` 那 107 处未逐条看** ⇒ 记**部分核** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{phys_hits,mental_hits}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **两个轴【对照完】**：`range_axis` ↔ `.type` 同构 · `damage_axis` 自加（三角度确认）✓
🆕 **可做**：① 🔴 **验 `none` 那两批 16 条是否【同一集合】**（关掉 §5 的口子）
   ② 列 `damage_axis: mental` 的完整 3 条
   ③ 把第 171~173 条判据补进 `observe_list` D11
   ④ **把"`damage_axis` 属自加"写进 `PLAN_adoption` §6** ✓
⏸️ **等策划**：十张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
