# A10 / A11：补给与物品 · 传家宝兑换 —— 抽取与对账读数

> 🕒 2026-09-26 · 🔴 **只抽不落库**：`darkest/**` 一个字节没动 ⇒ **零行为** ✓

---

# A11：传家宝兑换 —— ✅ **已经 12/12 完全一致**（本项**无需改动**）

## 1. 三方对照（**E 盘一手 · 参考项目 · 我方**）

```
参考：`…/Mechanics/HeirloomExchange.json` ⇒ `markets[0].exchange_rates` **12 条** ✓
我方：`darkest/data/heirloom_exchange.json` ⇒ `exchange_rates` **12 条**（**已在库**）✓
E 盘：`E:\…\campaign\heirloom_exchange\heirloom_exchange.json` ⇒ `exchange_rates` **12 条** ✓
```

| 从 | 到 | 参考 | 我方 | E 盘 |
|---|---|---|---|---|
| `bust` → `crest` | | (2, 3) | (2, 3) ✅ | (2, 3) |
| `bust` → `deed` | | (3, 2) | (3, 2) ✅ | (3, 2) |
| `bust` → `portrait` | | (3, 1) | (3, 1) ✅ | (3, 1) |
| `crest` → `bust` | | (3, 1) | (3, 1) ✅ | (3, 1) |
| `crest` → `deed` | | (3, 1) | (3, 1) ✅ | (3, 1) |
| `crest` → `portrait` | | (6, 1) | (6, 1) ✅ | (6, 1) |
| `deed` → `bust` | | (3, 2) | (3, 2) ✅ | (3, 2) |
| `deed` → `crest` | | (2, 3) | (2, 3) ✅ | (2, 3) |
| `deed` → `portrait` | | (3, 1) | (3, 1) ✅ | (3, 1) |
| `portrait` → `bust` | | (2, 3) | (2, 3) ✅ | (2, 3) |
| `portrait` → `crest` | | (1, 3) | (1, 3) ✅ | (1, 3) |
| `portrait` → `deed` | | (2, 3) | (2, 3) ✅ | (2, 3) |

```
📊 参考 12 条 vs 我方 12 条 ⇒ **一致 12 · 不同 0** ✓
🎖️ **而且 E 盘一手与两者逐条相同** ⇒ 🔴 **这是【第一块三方完全一致的数据】** ✓
   （对比 A3 的 `crit` 与 A9 的 99 处成本 —— 那两块参考都被改过 ⚠️）
⇒ ✅ **A11 无需改动**：三方一致 ⇒ 无论按哪个口径都**不用动** ✓
```

## 2. 🎖️ 顺带核实了我方 `_design` 注记（**注释也在说真话**）

```
我方 `heirloom_exchange.json` 的 `_design` 写：
   "一手实测的设计：**所有兑换都损失 50%** ⇒ 相对价值 portrait : bust : deed : crest = **6 : 3 : 3 : 2**"
⇒ ✅ **我复算了**（用 12 条率做往返）：
   1 bust → portrait → 回 bust：**损失 50.0%** ✓
   1 bust → deed → 回 bust：**损失 55.6%** ⚠️
   1 crest → bust → 回 crest：**损失 50.0%** ✓
   1 bust → crest → 回 bust：**损失 50.0%** ✓
⇒ 🔴 **发现一处细节**：`bust ↔ deed` 的往返损失是 **55.6%**，不是 50% ⚠️
   （`bust 3 → deed 2` 得 0.6667，`deed 3 → bust 2` 得 0.6667 ⇒ 往返 0.4444 = 损失 55.6%）
   📌 **而"相对价值 6:3:3:2"仍然成立**（`portrait 6 / bust 3 / deed 3 / crest 2`）✓
   ⇒ ✅ 注记的**主结论对**，但"**所有**兑换都损失 50%"这句**过强** ⇒ 建议改成
      "**多数**损失 50%，`bust↔deed` 为 55.6%" ✓
   🎖️ **这正是我上次认过的纪律**「**判据不许比事实更强**」（架构 `#468` 收下过我那句）✓
```

## 3. 🔴 形状差一处（**要不要统一，归架构**）

```
参考：`{ markets: [ { id: "default", exchange_rates: [...] } ] }`   ← **包一层 market**
我方：`{ exchange_rates: [...] }`                                  ← **平铺**
E 盘：`{ exchange_rates: [...] }`                                  ← **平铺**
⇒ ⚠️ **我方与 E 盘同形，参考多包了一层** ⇒ 📌 **这是"结构"不是"数值"**
   ⇒ 按纪律 AY：**可做**（零行为：包一层不改变任何读数）✓
   ⇒ 🔴 **但要不要做，归架构**（多 market 是原版的能力 —— 参考里只有 1 个 market）
```

---

# A10：补给 / 物品 —— 抽取读数（**我方没有这一层**）

## 4. 参考 `Mechanics/Provision.json` 的形状（6,136 bytes）

```
顶层 **3 张清单**：
   ① `raid_starting_length_inventory_item_lists` —— **5 组**（按任务长度档）
      各组长度 `[0, 9, 9, 9, 9]`（**第 0 档是空表**）✓
      `type` 只有两种：`supply` **8 个 id** · `provision` **1 个空 id** ✓
      id 全集：`firewood` · `shovel` · `antivenom` · `bandage` · `medicinal_herbs` ·
         `skeleton_key` · `holy_water` · `torch`（+ 一个 `""`）✓
      🔴 **实测：除了 `firewood`，其余 7 种的 amount 全是 0** ⚠️
         ⇒ 逐档只有 `firewood` 有值：**档1 = 0 · 档2 = 1 · 档3 = 2 · 档4 = 4** ✓
         ⇒ 🎖️ 即"**任务越长，起手柴火越多**"（其余补给要玩家自己买）✓

   ② `raid_starting_hero_class_item_lists` —— **7 组**（**按职业给起手补给**）
      `houndmaster → dog_treats ×2` · `plague_doctor → antivenom ×1` ·
      `grave_robber → shovel ×1` · `arbalest → bandage ×1` · `crusader → holy_water ×1` ·
      `leper → medicinal_herbs ×1` · `jester → medicinal_herbs ×1` ✓
      🎖️ **这条很有信息量**：**7 个职业各有"本命补给"** ⇒ 是**设计意图**，不是随机 ✓

   ③ `default_store_inventory_item_lists` —— **5 组**（**商店库存**，按档）
      逐档库存（档 1→4）：
        `shovel 4/6/8/10` · `antivenom 6/9/12/15` · `bandage 6/9/12/15` ·
        `medicinal_herbs 6/9/12/15` · `skeleton_key 6/9/12/15` · `holy_water 6/9/12/15` ·
        `torch 18/24/36/42` · `(空 id) 18/24/36/42` ✓
      🔴 档 0 是空表 ✓ · 🎖️ `torch` 的库存**远高于其他**（18~42 vs 4~15）⇒ 火把是消耗大头 ✓
```

## 5. 我方状态：🔴 **`provisions.json` / `items.json` 都不存在**（实测）

```
`darkest/data/` 里**没有** `provisions.json`，也**没有** `items.json` ✓
⇒ 🔴 **A10 是"整层缺失"**，不是"数值要校准" ⚠️
📌 而 `PLAN_adoption §4` 已记：A10 是 **P16（M12 补给 kernel）** 的载体 ✓
```

## 6. ✅ `Inventory/Items.bytes` **已抽**（形态判定先做）

```
🎖️ **形态判定先做**（纪律：**先判形态，再选解析器**）：
   读前 64 字节 = `b'inventory_item:\t.type "provision"\t\t.id "'`
   ⇒ ✅ **是 DD1 文本，不是 Unity 二进制** ✓
   ⚠️ **`.bytes` 后缀不定形态**（`Maps/*.bytes` 是二进制 · `Heroes/Info/*.bytes` 与本源都是文本）
      ⇒ 📌 **不能按后缀选解析器** ✓

📊 **57 条** · 每行 **5 个字段**：`.type` · `.id` · `.base_stack_limit` ·
   `.purchase_gold_value` · `.sell_gold_value` ✓
类型分布：`journal_page` **22** · `gem` **10** · `supply` **9** · `quest_item` **9** ·
   `heirloom` **5** · `provision` **1** · `gold` **1** ✓

🎖️ **`heirloom` 5 条给出了堆叠上限**：`portrait 3` · `bust 6` · `crest 12` · `deed 6` · `urn 1` ✓
   ⇒ 🔴 **与 A11 的兑换口径对照**：相对价值 `portrait 6 : bust 3 : deed 3 : crest 2`
      ⇒ ⚠️ **堆叠上限与相对价值【不成比例】**（`crest` 堆叠最高 12 但价值最低 2）⇒
      📌 这是**两条独立的轴**（堆叠上限 ≠ 价值）⇒ **不能互推** ✓
   🎖️ **`gem` 10 条的 `sell_gold_value`**：`trapezohedron 2500` · `pewrelic 1250` ·
      `ruby/sapphire 1000` · `antiqrelic 1000` · `emerald/onyx 500` · `antiqrelicsmall 275` ·
      `citrine/jade 250` ✓ ⇒ 是"**战利品变现**"通道 ✓
   🔴 `gem` 全部 `purchase_gold_value = 0`（**只能卖不能买**）✓

✅ **引用完整性**：`Provision.json` 三张清单里引用的 id 去重 **8 个** ⇒
   **不在 `Items.bytes` 里的 0** ✓（空 id `""` 是合法的 `provision` 占位 ✓）
```

## 7. 诚实边界

```
✅ **能验**：A11 的 12/12 三方一致 · 手续费复算（含发现 55.6% 那处）·
   A10 的三张清单规模与逐档值 · `Items.bytes` 形态判定（前 64 字节）+ 57 条逐行解析 +
   引用完整性 0 悬空 · 我方 `provisions.json`/`items.json` 不存在 —— **全部当场跑出** ✓
   🎖️ 并核实了我方 `_design` 注记**主结论为真**（相对价值 6:3:3:2）✓
🔴 **不能验**：**没有落任何补给/物品/兑换数据** ⇒ "商店库存接上后经济会怎样"**完全未测**（纪律 BK）✓
🔴 **不能验**：`quest_item` 9 条 · `journal_page` 22 条的**用途未查**（要读别的文件）⇒ 记为**未验** ✓
🔴 **不能验**：补给层的落点 —— 我方**没有"城镇商店/起手补给"这一层** ⚠️
   ⇒ 同 A1/A6/A9 ⇒ 按架构判据**需求驱动，不预留** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/a1*.py` **不入库**（scratch）✓
```

## 8. 下一步

```
✅ **A11 结案**（12/12 一致，**无需改动**）✓ · ✅ **A10 抽取完成**（57 物品 + 3 张清单）✓
⏸️ **等架构**：兑换表要不要包 `markets[]`（结构改动）· 补给/物品层的落点 ✓
⏸️ **等策划**：① `raid_starting_hero_class_item_lists` 的"**本命补给**"（**7 个职业各 1 种**）
   要不要采用 ② `gem` 的变现价（`trapezohedron 2500` 等）要不要用 ✓
🆕 **A10 的下一步（可做）**：`quest_item` 9 条 + `journal_page` 22 条的用途（读别的文件）✓
```

