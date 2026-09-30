# 裁定：A6（折磨 / 美德）—— **取 (丙)** · `chance` 按条归一 · 形状收敛；并按一手更正 A6a/A6b 边界

> 🕒 2026-09-30 · 来自**策划** · 回复 `DELIVERY-LEAD-A6-TRAITS-20260926`（主程序请裁 3 件）＋ `DELIVERY-ARCH-READING3-DONE-20260927`（架构已把前置清空）
> 🔴 **送达判据**：`Select-String -Path doc/windows/架构窗口.txt -Pattern "DELIVERY-DESIGNER-A6-RULING-20260930" -SimpleMatch` 命中即送达（**同投主程序窗**）。
> 📄 **归档副本 = 本文件**（入 git 的留存面）· 🔴 已落 `doc/state.md #489`
> 🧰 **复跑**（全只读 · 一手 E 盘 · scratch 不入库）：`python -X utf8 reports/unity_ref/_probe/probe_edrive_traits2.py` · `probe_virtue_buffs_edrive_vs_ours.py` · `probe_actout_effect_table.py` · `probe_actout_shape_pair.py` · `probe_ref_trait_weights.py` ✓

```
DELIVERY-DESIGNER-A6-RULING-20260930
```

---

## 1 · 裁定①：我方那 7 条 ⇒ **取 (丙)**

```
✅ **裁定**：**参考 12 条为准**（7 折磨 + 5 美德 · 一手与参考的 id **完全同名** ✓）·
   **我方 7 条里【有独立价值的】并入参考的 act-out 表**（映射过去，**不另存一套**）✓
```

**理由三条**：

```
① ✅ **纪律 AY**：(甲)/(丙) **都是【结构动作】**（删表 / 映射重定向），**不动平衡数值** ⇒ 本裁定**不破 `#307` 冻结** ✓
② 🔴 **(乙) 并存不采纳** —— 它会造出「**同一个概念两处真值**」（还带名字撞车：两套 fear/selfish）⇒
   按 `#487` 同族判负（**纪律 AB/AP 家族**）✓
③ ✅ **我方那 3 个行为与参考的 act-out 家族【同层】**（都属"回合开始的行为修正"）⇒
   落成**参考表里的条目**（映射），**比并存干净** ✓
```

**映射表（结构层 · 现在可做）**：

| 我方（现行）| 落成参考的哪个 act-out | 依据 |
|---|---|---|
| `refuse_skill 33%`（`affliction_fear`）| `ignore_command` / `random_command` | 同为"拒绝执行 / 改指令" |
| `randomize_attack_target 33%`（`affliction_uncontrolled`）| `random_command` | 同为"目标随机化" |
| `refuse_heal 33%`（`affliction_selfish`）| `block_heal` | 同为"拒绝治疗" |
| 4 美德（`dealt_damage_mult` / `deaths_door_resist_bonus` / `crit_bonus` / `inspired`）| 抗性档 + `virtue<Name>BuffN` | 同一层（属性包）|

🔴 **但"并入"只做【结构】这一步**：我方 3 个行为的 **33%** 与 4 美德的数值**都是平衡数值** ⇒ **入 `§39`**（见本文 §6），
   **留到解冻窗口一次做** ✓（纪律 AY：结构现在做、平衡等窗口）
🔴 `virtue_inspired` 的**值缺失**（`O-27`）⇒ 一并标 `placeholder` + 入 `§39` ⇒ **不许静默取 0** ✓

---

## 2 · 裁定②：`chance` 归一 = **按条归一 `chance / Σchance`** —— 采纳，但补三条约束

```
✅ **采纳主程序的建议**（按条归一 · 保序 · 可复算）· 补三条：

① 🔴 **一手是两个形态**（我实测 · 见 §5）—— 归一前必须先分清：
   · **start 侧 = 整数权重**（一手 Σ = 12 / 20 / 36 / 36 / 38 / 51 / 76 · 美德一律 4）✓
   · **react 侧 = 小数**（0.15 / 0.2 / 0.33 / 0.375 / 1 · 一手 Σ = 1 / 2 / 3 / 4）✓
   ⇒ 📌 「`chance` 是整数权重」**只对 start 成立** ⚠️（`08 §5` 写成了通则 —— 见 §4 的形状更正）

② 🔴 **归一的结果只能用于【按条比较相对作妖频率】**；**跨条比较必须先声明口径**（纪律 BH/AU）✓

③ 🔴 **`chance` 一律回一手**（不采参考的）—— 因为**一手与参考的权重【全不同】**（见 §5③）✓
   ⚠️ **不采纳**「固定频率（每 N 回合作妖一次）」那一套：**一手就是权重**，且用户 2026-09-26 已裁「一手优先」✓
```

---

## 3 · 裁定③：形状 ⇒ **收敛到【一手形状】**（它是参考形状的超集）

```
| 槽 | 一手（实测）| 参考 | ✅ 裁定 |
|---|---|---|---|
| **start 项** | `{id, data{number_value, string_value}, chance}` ＋（可选）`raid_limit` / `valid_hero_class_ids` | 同（**无**那 2 个可选键）| ✅ 落**一手**形状（含 2 个可选键）|
| **react 项** | `{id, data{effect}, chance}` ← 🔴 **单槽** | 同 | ✅ 落**一手**形状 |
```

🔴 **更正一处（重要）**：`08 §5` 写「`data` 恒为 `{float, string}` 两槽」⇒ ✅ **只对 start 成立**；
   **react 是单槽 `{effect: string}`**（我实测：一手 **216/216** 全为 `{effect}` · 参考 **180/180** 同）⇒
   📌 **定义必须写两个**（否则实现会照"两槽"造一个错结构）✓

🔴 我方现行的 `modifiers[] + hooks[] + ends_at` ⇒ **收敛为参考形状**（`buff_ids[]` + 两张表）：

```
✅ **这是结构改动**（纪律 AY：现在可做）✓
🔴 **但执行顺序是【新增先于删除】**：先落 `buff_ids` + 两张表，**旧的三个字段【暂不删】** ——
   因为**我还没核实 `traits.json` 的消费点在哪**（若旧的现在真在跑 ⇒ 先删就是造一个【行为黑洞】）⚠️
   ⇒ 📌 **删旧字段要单独裁**（前置 = 一张"消费点在哪"的读数）⇒ **不在本裁定的授权范围内** ✓
```

---

## 4 · 🔴 A6a / A6b 边界更正 —— **你项④的顺序仍成立**，但**理由与我原先说的不同**

```
🧪 **我核的问题**：act-out 的那个字符串槽，到底是不是 **Effect 表的外键**？（它决定 A6b 是否真等 A6a）

📊 **实测（一手 `shared/trait/trait_library.json` · 91 518 B · 合法 JSON）**：
   · react 的 `data.effect` = `BarkStress` / `BarkStressHeal` ⇒ ✅ **是 Effect 名**（2/2）✓
   · start 的 `data.string_value` = **20 个不同值** ⇒ 我逐个对一手 Effect 表核：
       · `effects/base.effects.darkest`（**224 954 B**）⇒ **命中 18/20** ✓
       · `SB Sabotage Speed Debuff` ⇒ 命中 **DLC** `dlc/702540_shieldbreaker/effects/shieldbreaker.effects.darkest` ✓
       · 🔴 2 个（`Duelist Attack Friendly Dodge Debuff` · `RW Attack Friendly Burn`）⇒
         **全盘 9 636 文件（全扩展名 · 含 DLC）只有【引用处】命中**
         （`crimson_court.quirk_act_outs.json` + `trait_library.json`）⇒ 📌 **一手自己也悬空**（死条目）✅ ⇒ **不追**

⇒ ✅ **结论：act-out 的字符串槽 = Effect 表的外键**（**不是自由文本**）⇒ **A6b 真的等 A6a** ✓
   ＋ 🎖️ **而 A6a（Effect 表 + 4 驼峰字段）不依赖 act-out**（它只依赖 `effects` 数据）⇒ **现在就能开工** ✓
      📌 可与 A2 的 `dmg%` 回一手**并行**（都不破冻结：A6a 是抽取 · A2 那条是复现原版）✓

🔴 **而我方【没有】act-out 的落点** ⇒ 按需求驱动：**只落数据、不接消费点**（同 A1 的趟级修正载体处境 · 我认下）✓
```

---

## 5 · 🔴 一手新证据登记（**纪律 AU：我只登记，不擅改你们的件**）

```
🔴 **本节 7 条全是我这一轮的实测**，其中 **3 条推翻了 A6 请裁件的一处隐含前提**（条目数 / 美德抗性档 / 悬空 buff 的归因）⚠️
```

### 5① 一手 traits = **12 条**（与参考 id 完全同名）

```
一手 `trait_library.json` = **13 条**，其中 `id="test"` 是**测试件** ⇒ ✅ **真实 12 条**（7 折磨 + 5 美德）·
   **id 与参考 12 条完全同名** ✓ ⇒ ✅ **"参考 12 条为准"这条前提成立** ✓
```

### 5② 🔴 两张表的条目数**不是 14 / 15**（`08 §1` 的"14/15 · 168/180"是**参考侧**读数）

```
· 一手 **start = 34 项 / 15 个 id**（比参考多 `consume_item`）✓
· 一手 **react = 18 项 / 18 个 id**（比参考多 `block_camping_meal` · `block_camping_skill_performer` · `block_camping_skill_target`）✓
🔴 **且 `attack_friendly` 在一手里出现 20 次**（按 `valid_hero_class_ids` **分英雄职业**）——
   而**参考件把它压成了 1 项** ⚠️ ⇒ 📌 **参考件在 act-out 这张表上【信息有损】** ✓
🔴 一手的 act-out 条目还带 2 个**参考没有的键**：`raid_limit`（start 侧 12 处）· `valid_hero_class_ids`（240 处）✓
```

### 5③ 🔴 `chance` 的权重：**一手与参考【全不同】**（可复算）

| trait | 一手 start Σ | 参考 start Σ | 一手 `nothing` | 参考 `nothing` |
|---|---:|---:|---:|---:|
| `abusive` | 36 | 6 | 12 | 4 |
| `depressed` | 20 | 13 | 13 | 8 |
| `fearful` | 38 | 9 | 26 | 6 |
| `irrational` | 76 | 16 | 38 | 10 |
| `masochistic` | 12 | 10 | 8 | 6 |
| `paranoid` | 51 | 9 | 22 | 6 |
| `selfish` | 36 | 12 | 24 | 8 |
| 5 美德 | **一律 4** | **一律 4** | **一律 3** | **一律 3** |

```
⇒ ✅ **`chance` 回一手**（§2③）· 📌 **美德两侧一致 = 唯一例外** ✓
   （react 侧也不同：一手 Σ = 1 / 2 / 3 / 4 · 参考 Σ = 1.08 ~ 3.705）✓

🔴 **并顺带更正 `08` 内部一处"两数"**：`§5` 写 7 个折磨的 `nothing` = `4/6/8/10`（**4 个数**）·
   `§7` 写 `4/6/6/6/8/8/10`（**7 个数**）⇒ ✅ **以 `§7` 为准** —— 我给的是**参考侧**实测：
   abusive 4 / fearful 6 / masochistic 6 / paranoid 6 / depressed 8 / selfish 8 / irrational 10 ✓
   ⇒ 🆕 **判据（同族 BH）**：🔴 **"同一个量给了两个数 ⇒ 先数【个数】对不对得上"**（4 个数 vs 7 条 × 1 值）✓
```

### 5④ 🔴 参考件的【5 个美德】抗性档是错的（**7 个折磨两侧逐条相同** ✓）

```
· 参考用 `*RESIST20`（+20）· 一手 `*RESIST25`（**+25**）⇒ 🔴 **一手为准 ⇒ 落 +25** ✓
· 参考**没有** `virtueCourageousBuff2` / `virtueStalwartBuff2`（**一手有**）⇒ 📌 参考件美德的**专属 buff 也缺 2 个** ⚠️
· ✅ 反过来：**7 个折磨的 `buff_ids` 两手【逐条完全相同】**（abusive/depressed/fearful/irrational/masochistic/paranoid/selfish）
   ⇒ 📌 **错只在美德侧**，不在折磨侧 ✓
```

### 5⑤ 🔴 我方 A1 池"解析不了的那 8 个"，**全在一手**（归因更正）

```
那 8 个 = `BLEEDRESIST25` `BLIGHTRESIST25` `DEBUFFRESIST25` `DISEASERESIST25` `MOVERESIST25` `STUNRESIST25`
        ＋ `virtueCourageousBuff2` ＋ `virtueStalwartBuff2`
⇒ ✅ 一手 `base.buffs.json` 里 **8/8 存在**（我逐条打印过）⇒ 📌 **不是"一手缺失"，是我们 A1 抽取漏了** ⚠️
   ⇒ 责任域 = **A1**（**我只登记，不代改**）✓

🔴 **池的双向差**（纪律 BH：**两个方向都要报**）——
   一手 **2017** 条有 `id` · 我方池 **1801** ⇒ **一手不覆盖 220 条** · **我方多出 4 条** ✓
   ⇒ 📌 只报一向会让下一轮以为"单向缺失"（**两个方向都要登记**）✓
```

### 5⑥ 🔴 我方池的**美德专属 buff** 与一手 **6/10 不符** ⇒ ⚠️ 全是**数值** ⇒ 入 `§39`（§6）

| buff | 一手 | 我方池 | 判 |
|---|---|---|---|
| `virtueCourageousBuff1` | `stress_dmg_received_percent` **−0.33** | −0.2 | 🔴 不符 |
| `virtueCourageousBuff2` | `speed_rating +2` | **缺失** | 🔴 缺 |
| `virtueFocusedBuff1` | `attack_rating +0.1` | 同 | ✅ |
| `virtueFocusedBuff2` | `crit_chance +0.08` | 0.1 | 🔴 不符 |
| `virtuePowerfulBuff1/2` | `damage_low/high ×0.25` | 同 | ✅ |
| 🔴 `virtueStalwartBuff1` | `protection_rating +0.15` | **`stress_dmg_received_percent −0.5`** | 🔴 **明显错配**（形似 Courageous 家族）|
| `virtueStalwartBuff2` | `death_blow +0.08` | **缺失** | 🔴 缺 |
| `virtueVigorousBuff1` | `speed_rating +4` | 5 | 🔴 不符 |
| `virtueVigorousBuff2` | `defense_rating ×0.1` | 同 | ✅ |

```
⇒ 📌 **"我方 7 条不是对等物"的结论仍然成立**（§1 的裁定不变）—— 但**参考件在美德抗性/专属 buff 上不可靠** ⚠️
   ⇒ ✅ **裁定必须按一手重写这三条**（= §1 的映射 + 本表的数值入 `§39`）✓
```

### 5⑦ 一手还有 2 个**参考没有的 trait 字段**

```
`generate_chance_modifier` · `is_generated`（参考件 10 个顶层键里没有这 2 个）
⇒ ⚠️ **语义未核** —— 纪律 BL：**我只看键名，不看语义** ⇒ **只登记** ✓
```

---

## 6 · 入 `§39` 的清单（**我已代落：`dd1_baseline §39.6` 预登记区**）

```
🔴 **我已追加** `doc/modules/dd1_baseline.md §39.6`（**预登记 · 明确标"未落地"**），含 5 项：
   ① 7 个折磨的 act-out `chance` 权重（**一手整套** ⇒ 作妖频率整体重定标）
   ② 5 美德抗性档 **+25**（vs 参考 +20）
   ③ 5 个美德专属 buff 的值（**6/10 不符** · 含 1 处疑似错配 + 2 处缺失）
   ④ `virtue_inspired` 的**缺失值**（`O-27`）
   ⑤ 我方 3 个行为并入时**"33%"这个概率要不要保留**
⚠️ **计数注照旧不动**（`§39.5` 的注：表头/表体计数校正**留到解冻一次性做**）⇒ **预登记区不参与那个计数** ✓
```

---

## 7 · 诚实边界（纪律 BK：能验的与不能验的分开写）

```
✅ **能验（全当场跑出）**：12 条 / 7+5 / 一手 34+18 项 / 15+18 个 id / `attack_friendly` × 20 ·
   两套权重逐条 / 20 个 string_value 的落点（18 base + 1 DLC + 2 悬空）/ 8 个悬空 buff 的归因 /
   美德专属 buff 10 项逐条 / 池的双向差 220 与 4 / `{effect}` 单槽 216+180 ✓

🔴 **不能验（如实标）**：
   ① **没有落任何 trait 数据** ⇒ "接上 act-out 后士气流会怎样"**完全未测** ✓
   ② `paranoid` 与 `fearful` 的**属性签名完全相同**（两手都相同）⇒
      **语义差异要读本地化文本** ⇒ 记为**未验**（`08 §7` 认领的是同一件事 · 我复核成立）✓
   ③ 一手那 2 个**悬空 effect 名**为什么悬空（死条目？运行时回退？）⇒ 只登记 ✓
   ④ `traits.json` 的**消费点在哪**（§3 删旧字段的前置）⇒ **我没查** ⇒ 所以删旧字段不授权 ✓
```

---

## 8 · 我认领的下一步

```
✅ **本裁定的执行拆分**：① **结构**（映射并入 + 形状收敛 + 落 act-out 数据）⇒ **现在可做** ✓
                        ② **数值**（§6 那 5 项）⇒ **等解冻窗口** ✓
🔴 **我认领一张新规格**：**A6 落库规格**（12 条 × 字段级映射 + 两张表的项形状 + `chance` 归一写法 + 2 个可选键）⇒
   排在 `#485 §5` 我自留的 **S1 / S2 / S3** 之后 ⇒ ⚠️ **但它【不阻塞 A6a】**（A6a = Effect 表 · 不含 trait 数据）✓
📌 **本轮我未做（如实标）**：**S1 / S2 / S3 三张规格仍未开工** —— 本轮预算全用在本裁定的一手复核上 ✓
```

