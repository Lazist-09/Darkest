# `missing_hp` 段**在参考里没有对应** —— 确认是**我方自加**（11 种模式 0 命中）

> 🕒 2026-09-26 · 工具 `tools/dsh/check_missing_hp_in_ref.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `112_*.md §6` 的"`missing_hp` 的 `base`/`coefficient` 与参考对应未核"** ✓

---

## 1. 🔴 判决：**参考里没有这个机制**（11 种模式 **全部 0 命中**）

```
📊 **在参考【数据】`Assets/Resources/Data/**` 里找**：
   `missing_hp` **0** · `missing_health` **0** · `hp_missing` **0** ·
   `MissingHp` **0** · `missingHp` **0** · `coefficient` **0** · `base_value` **0** ✓
📊 **在参考【代码】`Assets/Scripts/**` 里找**：
   `MissingHealth` **0** · `missingHealth` **0** · `HealthLost` **0** · `MissingHp` **0** ✓
＋ **另查 7 个"按生命比例"的候选**：`hp_percent` **0** · `target_hp` **0** ✓
⇒ 🎖️ **即：参考【没有"按目标缺失生命加成伤害"这个概念】** ✓
   🎖️ **判据（第 124 条）**：**"我方这个结构 —— 参考里有对应吗？
      有 ⇒ 对齐；没有 ⇒ 那是【我方自加】⇒ 归解冻清单"** ✓
```

## 2. 🎖️ 而"换个角度找"时遇到的 `damage_low_multiply` **不是等价物**

```
📊 **它出现 139 次**（`Effects.txt` 的 `Unholy Killer 1~4` 等）⇒ ⚠️ **看着像候选** ✓
   `effect: .name "Unholy Killer 1" .target "performer" … .monsterType "unholy" `
   `        .combat_stat_buff 1 .damage_low_multiply 15% .damage_high_multiply 15% …` ✓
🔴 **而读代码**（`Effect.cs:707-720`）：
   `case ".damage_low_multiply":`
   `    … riposteEffect.StatMultBuffs.Add(AttributeType.**DamageLow**, float.Parse(data[++i]) / 100);` ✓
⇒ 🎖️ **即：它是"给 `DamageLow` 属性加一个乘数"** ——
   **一个【属性 buff】**，不是"按缺失生命算伤害" ✓
   📌 **两者的差别**：
      · `damage_low_multiply` ⇒ `DamageLow × (1 + 15%)` ⇒ **改了攻击区间** ✓
      · `missing_hp` ⇒ `base + coefficient × (1 − 当前HP/最大HP)` ⇒ **改了伤害公式的形状** ✓
   🎖️ **判据（第 125 条）**：**"'换个角度找'找到的候选 ——
      它与我要找的【同一个东西】吗？⇒ 看代码【消费它的那一行】"** ✓
      📌 与第 7/26/66/83 条**同族（第 4 次）**：**名字像 ≠ 是同一个** ✓
```

## 3. 🎖️ 而"参考没有"这个结论**有三种处置**，我方已选一种

```
📊 **我方那 2 条**（逐条原文）：
   · **`medic_lethal_injection`**（致命注射）⇒
     `"damage": {"segments": [{"type": "**missing_hp**", "base": 1.0, "coefficient": 0.6}]}`
     ＋ `dmg_pct: -80` · `_dmg_pct_source: "dd1:noxious_blast (语义同(注入毒素))"` ✓
   · **`commissar_execution_order`**（处决令）⇒
     `"segments": [{"type":"missing_hp","base":1.0,"coefficient":0.7}]`
     ＋ **`"requires": {"target_hp_below_percent": 50}`** ⚠️
⇒ 🎖️ **即：我方这两条是【自加的设计】**（参考没有）✓
   📌 **而纪律 BS 说**：**「这个字段/乘数，原版有吗？
      没有 ⇒ 它必须【中性化】」** ✓
      ⇒ 🔴 **但这里【不适合中性化】** —— 因为**它不是"额外乘数"，
         而是这条技能【存在的理由】**（致命注射 = 越残血打越痛）✓
      ⇒ ✅ **所以它应当归【解冻清单】**（保留我方设计，明确标注"参考无对应"）✓
   🎖️ **判据（第 126 条）**：**"参考没有这个东西 ——
      是【中性化】还是【归解冻清单】？⇒ 看它是【额外修数】还是【机制本体】"** ✓
      · **额外修数**（如 Σ 归一）⇒ **中性化** ✓
      · **机制本体**（如 `missing_hp`）⇒ **解冻清单**（保留并标注）✓
```

## 4. 🎖️ 而 `commissar_execution_order` 还有**第二层自加**

```
📊 **它的 `requires`**：`{"target_hp_below_percent": 50}` ⇒ ⚠️ **也是"按生命"的条件** ✓
   ⇒ 📌 **与 `missing_hp` 是【同族的两处自加】**（一个改伤害 · 一个改可用性）✓
   🎖️ **即：这条技能整体是我方自加** ⇒ ✅ **两条一起归解冻清单** ✓
      📌 判据：**"同一技能里有两处自加 —— 它们要【一起处置】，不能只标一处"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**11 种模式在参考数据+代码里的 0 命中** ·
   **另 7 个候选（`hp_percent`/`target_hp` 也是 0）** ·
   **`damage_low_multiply` 的 139 处与 `Effect.cs:707-720` 的消费** ·
   **我方 2 条技能的完整定义** —— **全部当场跑出** ✓
🎖️ 并**把"参考无对应"判为【解冻清单】而非【中性化】**，且给出区分标准 ✓
🔴 **不能验**：**参考里有没有【别的方式】实现"越残血越痛"** ⇒
   📌 本件只按"字符串/字段名"找 ⇒ ⚠️ **若它通过【技能组合】或【Effect 链】实现，搜不到** ✓
   🎖️ **而本件已查了 18 种模式** ⇒ ✅ **覆盖率较高，但不保证穷尽** ⇒ 记**可能不全** ✓
🔴 **不能验**：**`解冻清单` 里现在有没有这 2 条** ⇒ 📌 本件只见 `_dmg_pct_note` 提到"归 §39"
   ⇒ ⚠️ **而那说的是那 30 条 `dmg_pct`，不是这 2 条** ⇒ 记**未核** ✓
🔴 **不能验**：**`missing_hp` 的 `base`/`coefficient` 数值有没有依据** ⇒
   📌 无参考可对 ⇒ **属策划** ⇒ 记**待裁** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{mhp2,mhp3}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **`missing_hp` 的参考对应【查清】**：11+7 种模式 0 命中 ⇒ **我方自加** ✓
🆕 **可做**：① **核 `commissar_execution_order` 的 `requires.target_hp_below_percent`** 是否也在解冻清单
   ② **查"解冻清单"在哪**（`§39` 是取整那件还是另一件？易混）✓
   ③ 把第 124~126 条判据补进 `observe_list` D11
   ④ **在窗口报一条**：`missing_hp` ＋ `target_hp_below_percent` 是**我方自加机制**，请策划确认归属 ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
