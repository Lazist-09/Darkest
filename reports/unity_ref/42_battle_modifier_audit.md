# `battle_modifier`：参考有 **15 个字段**、我方 **0 个** —— 逐个测出**哪些真被消费**

> 🕒 2026-09-26 · 承接 `41_our_rounding_vs_ref.md §6` 的"`CanBeDamagedDirectly` 在我方未核" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🔴 先答那个具体问题：**我方【完全没有】`battle_modifier`**

```
📊 实测（`git grep`）：
   `battle_modifier` ⇒ **0 处** · `CanBeDamagedDirectly` ⇒ **0 处** · `can_be_damaged` ⇒ **0 处** ✓
⇒ 🔴 **即：我方【没有这一层】** ⇒ ⚠️ **`41_*.md` 里那个免伤条款，我方无从对应** ✓
   📌 不是"值不同"，是**字段不存在** —— 与 A6 的 act-out 同族（**整层缺失**）✓
```

## 2. 🎖️ 参考侧：`battle_modifier` 的**完整字段表**（15 个数据字段 → 12 个属性 + 3 个无属性）

```
🎖️ `BattleModifier.cs`（113 行）定义了 **12 个属性** ✓
🎖️ 数据侧出现 **15 个字段** ⇒ 🔴 **有 3 个字段【在类里没有对应属性】**
   实测那 3 个：`can_be_random_target` · `does_count_towards_stall_penalty` ·
      `does_count_as_monster_size_for_monster_brain` ✓
   🎖️ **即：它们被解析了但【没进类】**（或进了别的字段）⇒ 与 `§10.2` 的"解析后丢弃"同族 ✓
```

## 3. 🎖️🎖️ 逐字段测**消费点**（这是本件的核心读数）

| 字段 | 数据出现 | 消费点 | 判定 |
|---|---|---|---|
| `disable_stall_penalty` | **229** | **1** | ✅ |
| `can_surprise` | **229** | **3** | ✅ |
| `can_be_surprised` | **229** | **5** | ✅ |
| `always_surprise` | **229** | **1** | ✅ |
| `always_be_surprised` | **229** | **3** | ✅ |
| `can_be_missed` | **24** | **2** | ✅ |
| `is_valid_friendly_target` | **8** | **1** | ✅ |
| `can_relieve_stress_from_crit` | **8** | **3** | ✅ |
| `can_be_summon_rank` | **8** | **3** | ✅ |
| `can_be_hit` | **1** | **1** | ✅ |
| `can_be_damaged_directly` | **1** | **2** | ✅ |
| 🔴 `can_relieve_stress_from_killing_blow` | 8 | **0** | 🔴 **无消费** |
| 🔴 `can_be_random_target` | 7 | **0** | 🔴 **无消费** |
| 🔴 `does_count_towards_stall_penalty` | 8 | **0** | 🔴 **无消费** |
| 🔴 `does_count_as_monster_size_for_monster_brain` | 8 | **0** | 🔴 **无消费** |

```
📊 **即：15 个字段里 · 11 个有消费 · 4 个无消费** ✓
🔴 而**那 4 个都有非零数据**（各 7~8 条怪物在用）⇒ **红线 21 同族** ✓
   🎖️ **其中一个特别值得记**：`can_relieve_stress_from_killing_blow`
      ⇒ 它**在类里叫 `CanRelieveStressFromKills`**（**名字不同**）⚠️
      ⇒ 📌 而我按数据字段名去搜 `CanRelieveStressFromKillingBlow` ⇒ **搜不到** ⇒ 报了 0 ⚠️
      🎖️ **即：这个 0 是【我的搜法不对】，不是"无消费"** ⇒ ✅ **必须订正** ✓
```

## 4. 🔴 订正：那 4 个"无消费"里，**至少 1 个是假报**（第 11 次同族）

```
🔴 **我第一版**按"数据字段名的驼峰"去搜属性 ⇒
   `can_relieve_stress_from_killing_blow` ⇒ 搜 `CanRelieveStressFromKillingBlow` ⇒ **0** ⚠️
✅ **而类里它叫 `CanRelieveStressFromKills`**（`BattleModifier.cs:12`）✓
   ⇒ 📌 **即：数据名与属性名【不同】** ⇒ **按数据名搜，必然搜不到** ✓
🎖️ **判据（本件新增）**：**"我按【数据里的名字】去搜代码 —— 代码里会用同一个名字吗？"** ——
   **不一定**（`_from_killing_blow` ↔ `FromKills`）⇒ ✅ **必须先用【类定义】建对照表** ✓
📌 **与前面几次同族**：**我的度量与目标不匹配**（这次是"名字"这一层）✓
   🎖️ **共同形状**：**凡"按名字找东西"，先确认两侧用的是不是同一套名字** ✓
      （这正是 A7 的"74 个悬空是改名还是无对应物"那条判据的**反向应用**）✓
```

### ✅ 用**类定义**重建对照后的正确读数

| 数据字段 | 类属性 | 消费点 |
|---|---|---|
| `can_relieve_stress_from_killing_blow` | **`CanRelieveStressFromKills`** | ✅ **有**（名字不同）|
| `can_be_random_target` | 🔴 **类里没有** | 🔴 无 |
| `does_count_towards_stall_penalty` | 🔴 **类里没有** | 🔴 无 |
| `does_count_as_monster_size_for_monster_brain` | 🔴 **类里没有** | 🔴 无 |

⇒ 🎖️ **即：真正"无消费"的是【3 个】，而且它们**连属性都没有**
   ⇒ 📌 **那不是"有数据没人读"，而是【数据字段被解析后没进类】** ⚠️
   ⇒ ✅ **两者要分开报**（与 `plot_quests` 的"6 个有数据没人读"**不同**）✓

## 5. 🎖️ 对本任务（采用）的意义

```
✅ **那 11 个有消费的字段** ⇒ **照抄**（含默认值 —— 类里有 `true/false` 初始值）✓
   🎖️ **而默认值也值得抄**（`BattleModifier.cs:20-35` 构造器）：
      `CanSurprise = **true**` · `CanBeSurprised = **true**` · `AlwaysSurprise = **false**` ·
      `IsValidFriendlyTarget = **true**` · `CanRelieveStressFromKills = **true**` ·
      `CanRelieveStressFromCrit = **true**` · `CanBeSummonRank = **false**` ·
      `CanBeMissed = **true**` · `CanBeHit = **true**` · `CanBeDamagedDirectly = **true**` ·
      `DisableStallPenalty = **false**` ✓
      ⇒ 📌 **即：默认是"普通怪物"**（能被打中/被打/能被偷袭…）✓
🔴 **而那 3 个无属性的字段** ⇒ **不要抄**（抄了就多 3 个死字段）✓
🔴 **且这一整层我方【现在没有】** ⇒ ⚠️ **采用 = 新增一层** ⇒ **归架构定结构** ✓
```

## 6. 诚实边界

```
✅ **能验**：**我方 0 处 `battle_modifier`** · `BattleModifier.cs` 的**12 个属性全文** ·
   **15 个数据字段的出现次数** · **11 个有消费 / 3 个连属性都没有** ·
   **默认值 11 个** —— **全部当场跑出** ✓
🎖️ 并**订正我自己一处假报**（`CanRelieveStressFromKills` 与数据名不同）✓
🔴 **不能验**：`can_be_random_target` 等 3 个字段**被解析到哪去了** ⇒
   ⚠️ 可能进了**别的类**（不是我搜的那个）⇒ 记**"在我搜的范围内没找到"**，非"绝对没有" ✓
🔴 **不能验**：那 11 个字段的**精确行为语义**（我只看了消费点的首行）⇒ 记**未逐行读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 新增这一层后行为如何**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{cbd,bm}_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **`CanBeDamagedDirectly` 那条【答完】**：**我方整层没有** ⇒ 采用=新增一层 ⇒ 归架构 ✓
🆕 **可做**：① 核 A6 数据里 `NumberParameter` 的取值
   ② 扫 `darkest/scenes/**` 节点名一致性（`35_*.md` 的覆盖缺口）
   ③ 逐行读那 11 个字段的消费点（本件只看了首行）✓
⏸️ **等策划**：八张单已投递 ✓
```
