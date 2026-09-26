# A6：折磨 / 美德 + 两张行为（act-out）表 —— 抽取与缺口读数

> 🕒 2026-09-26 · 工具 `tools/dsh/extract_ref_traits.py`（可重跑）·
> 产物 `reports/unity_ref/traits_from_ref.json`（12 条 · 每条带 `file` + `line`）✓
> 🔴 **只抽不落库**：`darkest/**` 一个字节没动 ⇒ **零行为** ✓

---

## 1. 抽取读数（可复算）

```
源：`…/Assets/Resources/Data/JsonTraits.json`（**41,767 bytes**）
🔴 **它不是严格合法 JSON**（尾随逗号）⇒ 先清洗（与 `JsonAI.json` 同族的坑，第 2 次出现）✓
解析出 **12 条** ⇒ 完整性自检：
   `overstress_type` **12/12** · `curio_tag` **12/12** · `curio_tag_chance` **12/12** · `keep_loot` **12/12** ✓
   类型分布：**`affliction` 7 · `virtue` 5** ✓ · `buff_ids` 引用共 **119**（去重 **35**）✓
   两张表：**回合开始 168 项（12 × 14）· 反应 180 项（12 × 15）** ✓
   act-out **id 全集：回合开始 14 · 反应 15** ✓（**12 条共用同一套 id** —— 差别只在 `chance`）✓
```

## 2. 参考的字段全集 vs 我方（**缺口很干净**）

| 字段 | 参考 | 我方 (`traits.json`) | 差 |
|---|---|---|---|
| `id` / `overstress_type`（=`kind`）| ✓ | ✓ | — |
| `buff_ids[]` | **119 引用** | ✗（我方用 `modifiers[]` + `hooks[]` 自建）| ⚠️ **形状不同** |
| **`combat_start_turn_act_outs`** | **14 项/条** | 🔴 **完全没有** | **14 种** |
| **`reaction_act_outs`** | **15 项/条** | 🔴 **完全没有** | **15 种** |
| `curio_tag` / `curio_tag_chance` | ✓（5 个取值）| 🔴 **完全没有这个概念** | — |
| `keep_loot` | ✓（True/False）| 🔴 **完全没有** | — |

⇒ 🔴 **我方缺 29 种行为**（14 + 15），按 12 条算 = **348 个 (条, 行为) 组合** ✓

## 3. 逐条对名（**我方 7 条 vs 参考 12 条**）

```
参考折磨（7）：fearful · paranoid · selfish · masochistic · abusive · depressed · irrational
我方折磨（3）：fear · selfish · uncontrolled
   ⇒ 🔴 **参考有、我方没有**：`abusive` · `depressed` · `fearful` · `irrational` · `masochistic` · `paranoid`（**6**）
   ⇒ ⚠️ **我方有、参考没有**：`fear` · `uncontrolled`（**2**）
      🔴 **而这两个【不是"我们自加"】** —— 很可能是**改名**：
        `fear` ⇔ 参考 `fearful`（语义同，词形差一个后缀）· `uncontrolled` ⇔ `irrational`（待核）⚠️
      ⇒ 📌 **必须逐条核，不许按名字推**（纪律 BL）—— 见 §6 的待查清单 ✓

参考美德（5）：stalwart · courageous · focused · powerful · vigorous
我方美德（4）：brave · focused · inspired · resolute
   ⇒ 🔴 **参考有、我方没有**：`courageous` · `powerful` · `stalwart` · `vigorous`（**4**）
   ⇒ ⚠️ **我方有、参考没有**：`brave` · `inspired` · `resolute`（**3**）
      🔴 同上：`brave` ⇔ `courageous` · `resolute` ⇔ `stalwart` 都是**语义近**的改名嫌疑 ✓
      `focused` **两边都有**（唯一名对的）✓
```

🎖️ **所以"A6 缺口"现在有两种读法，我给两个数**（纪律 AU：**先写清分母**）：
   · **按名字字面**：缺 **6 折磨 + 4 美德** ✓
   · 🔴 **按语义（待核）**：可能只有 **4 折磨 + 3 美德** 是真缺，其余是**改名** ⚠️
   ⇒ ✅ **这条要人核**（我把它列成 §6 的逐条待查清单）✓

## 4. 🎖️ `buff_ids` 全部可解析（与 A1 池交叉验证）

```
参考 12 条的 `buff_ids` **去重 35 个** ⇒ 拿 A1 落的 `darkest/data/buff_primitives.json`
   （**1801 条**）逐个查：**35/35 全部命中 · 解析不了 0** ✓
⇒ 🎖️ **这是 A1 的一次独立验证**：A1 说"556 个引用 ⇒ 482 可解析 / 74 未解析"，
   而**折磨/美德这条线的 35 个引用【一个都不缺】** ⇒ 那 74 个悬空**不在这一块** ✓
   📌 也说明：**A6 的 buff 依赖是【干净】的** ⇒ A7（饰品/怪癖）才是悬空的大头 ✓
```

## 5. 🔴 act-out 的形状（**`data` 恒为两槽** —— 可直接照抄）

```
每项：`{id, data{number_value: float, string_value: string}, chance: int}` ✓
🎖️ **`data` 恒为 `{float, string}` 两槽** —— 与 buff 原语层的 `rule_data{float,string}` **同构** ✓
   ⇒ ✅ **阈值与字符串枚举同槽 ⇒ 不需要 union 类型**（同一条设计优点，第 2 次出现）✓

14 个回合开始 id：`nothing` · `bark_stress` · `change_pos` · `ignore_command` ·
   `random_command` · `retreat_from_combat` · `attack_friendly` · `attack_self` · `mark_self` ·
   `stress_heal_self` · `stress_heal_party` · `buff_random_party_member` · `buff_party` · `heal_self`
15 个反应 id：`block_move` · `block_heal` · `block_buff` · `block_item` · `block_combat_retreat` ·
   `comment_self_hit` · `comment_self_missed` · `comment_ally_hit` · `comment_ally_missed` ·
   `comment_ally_attack_hit` · `comment_ally_attack_missed` · `comment_move` ·
   `comment_curio_interaction` · `comment_trap_triggered` · `block_effect`

🔴 **`chance` 是"权重"不是"百分比"**（实测语义）：
   7 个折磨的 `nothing` chance 是 **4/6/8/10**（各不相同）· 5 个美德的 `nothing` 一律 **3** ✓
   ⇒ 即：**"什么都不做"的权重**决定该条的"作妖频率" ⇒ 美德**普遍比折磨安分** ✓
   📌 而 **`chance` 是【整数权重】不是百分比** —— 采用时**不能直接当概率用** ⚠️
      （需按条归一：`chance / Σchance`）✓
```

## 6. ✅ 待核清单**已核完**（用客观判据，不是"看着像"）

🎖️ **判法**：比 **`buff_ids` 的（抗性档 + 属性集）签名** 与 **两张 act-out 的 `chance` 向量** ✓
   —— **这是可计算的判据**，不是"名字像" ✓

```
参考 7 折磨的签名（**抗性档全是 −15**）：
  fearful      DEF+10 · DMGL−25 · DMGH−25 · MAXHP−10 · SPD+2
  paranoid     DEF+10 · DMGL−25 · DMGH−25 · MAXHP−10 · SPD+2   ← 🔴 与 fearful **签名完全相同**
  selfish      ACC−5 · DEF+5 · DMGL−10 · DMGH−10 · MAXHP−10
  masochistic  DEF−15 · MAXHP−10
  abusive      ACC−5 · DEF−15 · DMGL+20 · DMGH+20 · MAXHP−10    ← 🔴 **唯一"伤害变高"的**
  depressed    ACC−5 · DEF−5 · MAXHP−10 · SPD−3
  irrational   ACC−5 · DEF−5 · DMGL−10 · DMGH−10 · MAXHP−10 · SPD+2
参考 5 美德的签名（**抗性档全是 +20**）：
  stalwart/courageous/focused/powerful/vigorous
     ⇒ 共有 6 个抗性 buff，**区分全在各自独有的 `virtue<Name>BuffN`** ✓
```

### 🔴 逐条判定：**我方 7 条【一条都不是】参考 12 条的对等物**

```
我方 `affliction_fear`  = `refuse_skill 33%`（33% 拒绝行动）
参考 `fearful`          = 抗性全 −15 · MAXHP−10 · DMGL/DMGH−25 · DEF+10 · SPD+2
   ⇒ 🔴 **毫无共同点** ⚠️
我方 `affliction_uncontrolled` = `randomize_attack_target 33%`
参考 `irrational`              = 抗性全 −15 · 属性变差 · SPD+2
   ⇒ 🔴 同上 ⚠️
我方 `affliction_selfish` = `refuse_heal 33%`
参考 `selfish`            = 抗性全 −15 · 属性变差 · **`keep_loot=True`**
   ⇒ 🔴 **连名字完全相同的这一条，语义也不同**（参考靠 `keep_loot` 表达"自私"）⚠️
我方 `virtue_brave`     = `dealt_damage_mult +25` + `immune_fear`
参考 `courageous`       = 抗性全 +20 + `virtueCourageousBuff1`
   ⇒ 🔴 名字与语义都不同 ⚠️
```

### 🎖️ 结论（证据充分）

```
🔴 **两者是【不同的机制】，不是"同一套的不同实现"**：
   · **我方** = 【概率拒绝 / 随机化目标】的**行为修正**（`prob_mod` + `hooks`），**7 条**
   · **参考** = 【属性惩罚 buff 包】+ 【两张行为表（act-out）】+ 【奇物标签 / 掠夺行为】，
     **12 条**（7 折磨 + 5 美德），**每条 14 + 15 项行为** ✓
⇒ ✅ **A6 的缺口按名字字面算就是对的**：缺 **6 折磨 + 4 美德**，
   且**我方那 7 条不能算作"已覆盖"** —— 它们表达的是**别的东西** ⚠️
⇒ 🎖️ **纪律 BL 的第 N 次应验**：**名字对上 ≠ 语义对上**
   （最强的一例：连 `selfish` 这个**同名**条，语义都不同）✓
```


## 7. 诚实边界

```
✅ **能验**：12 条解析 · 7/5 分布 · 14/15 个 id · 168/180 项 · 35/35 buff 可解析 ·
   §6 的逐条判定（签名比对是可计算的）—— 全部当场跑出 ✓
🔴 **不能验**：**没有落任何 trait 数据** ⇒ "接上 act-out 后士气流会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：`chance` 的**归一方式**（权重 → 概率）**未定** ⇒ 归架构/策划 ✓
🆕 **但实测到一条硬事实**：**`chance` 不是百分比，是整数权重** ——
   7 个折磨的 `nothing` 权重 = **4/6/6/6/8/8/10**（各不相同）· 5 个美德一律 **3** ✓
   ⇒ ⚠️ 即"什么都不做"的权重决定"作妖频率" ⇒ **美德普遍比折磨安分** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a6_*.py` **不入库**（scratch）✓
```

## 8. 下一步

```
✅ **已做完**：抽取 + 缺口 + §6 逐条判定 ✓
⏸️ **等裁定**：
   ① `chance` 权重 → 概率的归一方式（`chance / Σchance`？）✓
   ② **act-out 的落点** —— 🔴 我方现在**没有"回合开始行为"这一层** ⚠️
      ⇒ 这可能是**一层新结构**（同 A1 的"趟级修正载体"）⇒ 按架构判据**需求驱动，不预留** ✓
   ③ 我方那 7 条的**去留** —— 🔴 它们**不是**参考 12 条的对等物 ⇒
      是"**保留为我们的自加机制**"还是"**换成参考的 12 条**"（含我方自加的那 7 条要不要留）⚠️
      ⇒ 📌 **这是设计决定，不是实现决定** ⇒ 归策划 ✓
```

