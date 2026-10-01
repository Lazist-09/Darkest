# Trinket（饰品）—— 对齐规格（**收尾卡**）

> 🔴 **状态**：**已裁 · 待实现**（`#434`/`#435`/`#436` · 用户裁 2026-09-18）
> 🔴 **原版依据 = 一手实测**（E 盘 `trinkets/` · `inventory/` · `shared/buffs/`）—— **每个数都量过** ✅
> 📄 上游：`dd1_baseline.md` **§19/§23/§25/§26** · 体检表第 5 条 · `observe_list.md` **O10（已答）**

---

## §1 🔴 原版规格（**全部一手实测**）

### 1.1 规模（**三层 · 全量过**）

```
🔴 **第一层：饰品 = 196 条**（`base.entries.trinkets.json` **490 条** − `kickstarter` **294 条**）
   🔴🔴 **`#452` 定义（AU）**：**"196" = 【排除 `rarity == "kickstarter"` 后】的数** ⚠️
      · **E 盘全量 = 490 条 · rarity 14 种**
      · **排除后 = 196 条 · rarity 13 种 · `price≤1` = 15 条 · 非 `universal` = 26 条** ✅
      ⇒ 📌 **凡引用"196/13/26"必须带这个定义**（**否则会与"490/14"混淆**）✅
   · rarity 13 种：`uncommon` 44 · `common` 44 · `rare` 33 · `very_rare` 24 · `very_common` 16 ·
     `trophy` 9 · `ancestral` 9 · `ancestral_shambler` 5 · `crow` 4 · `madman` 3 · `collector` 3 ·
     `courtier` 1 · `darkest_dungeon` 1
   · 🔴 **职业限制 = 79/196 = 40%**（**每职业 5~6 件专属 · 覆盖全部 15 职业**）
🔴 **第二层：buff 引用** = **引用数 535**（含重复）⇒ 🔴 **去重后【实体数】374** ⚠️
🔴 **第三层：原版 buff 库** = `shared/buffs/base.buffs.json` **751 KB ⇒ 2020 条**
   · 字段 12 个 · 实质 = "**属性修改器 + 条件**" ✅
```

### 1.2 饰品条目的字段（**一手 · 逐字**）

```json
{ "id": "crow_wingfeather",
  "buffs": [ "TRINKET_CROW_WINGFEATHER_BUFF", … ],      // ✅ 引用原语层 buff id
  "hero_class_requirements": [ ],                        // 🔴 职业限制（空 = 通用）
  "rarity": "crow",                                      // 🔴 稀有度（13 种）
  "price": 1,                                            // 🔴 价格（⚠️ 见 1.4）
  "limit": 1,                                            // 🔴 限购
  "origin_dungeon": "" }                                 // 🔴 来源地牢
```

### 1.3 🔴 槽位（**纠正一处印象**）

```
🔴 **一手依据** `inventory/base.inventory.system_configs.darkest`：
   · `.type "hero_equipped_trinkets"  .max_slots 2`      ⇒ 🔴 **每英雄【2】件**（**不是 3**）✅
   · `.type "trinket_store"           .max_slots 16`     ⇒ **商店库存 16**
   · `.type "trinket_storage"         .max_slots 9999`   ⇒ **仓库 9999**
   · `.type "arena_trinket_storage"   .max_slots 999`    ⇒ **竞技场（DLC · 不做）**
```

### 1.4 🔴 `price` 与 `award_category`（**100% 吻合 · O10 已答**）

```
🟢 **`price ≤ 1` ⇔ `award_category ≠ universal`**（**实测完美吻合**）：
   · `universal|price>1` = **170 条**（**全部可购买**）
   · `quest|price≤1` 4（`crow_*`）· `trophy|price≤1` 9（`boss_*`）· `dd|price≤1` 1（`dd_trinket`）·
     `battle|price≤1` 1（`tempting_goblet`）
⇒ ✅ **170 条可购买 · 26 条不可购买** ✅
⚠️ **而 `battle` 12 条里 11 条可购买 + 1 条不可购买** ⇒
   🎖️ **判据不是"rarity 名"，而是【`price`】** ✅

**`award_category`（获得方式 · 5 种）**：`universal` 170 · `battle` 12 · `trophy` 9 · `quest` 4 · `dd` 1
   ⇒ 🔴 **它决定"饰品从哪来"** —— 但**接线要等条 3（地牢掉落）/条 5（结算）** ✅
```

---

## §2 ✅ 裁定（**用户裁**）

| # | 项 | 裁定 |
|---|---|---|
| **①** | **纳原版 buff 库** | ✅ **纳**（`base.buffs.json` 2020 条当**基准数据**落盘）✅ |
| **②** | **我们那 22 条 buff** | ✅ **逐步废掉** —— 而"废掉"**分三类**（见 §4）✅ |
| **③** | **196 条分批** | ✅ **3 批**：`common+uncommon`(88) → `rare+very_rare`(57) → 其余(51) ✅ |
| **④** | **`award_category`** | ✅ **先只落字段 · 接线后做** ✅ |
| **⑤** | **`price=0/1` 那 15 条** | ✅ **已连 `award_category`**（**结果 100% 吻合**）✅ |
| **⑥** | **顺序** | 🔴 **buff 原语层 → Trinket 表 → 商店接线** ✅ |

---

## §3 🔴 目标结构（**四件套 · 按纪律 AF**）

> 🔴 **纪律 AF**：**"这个名字能不能从既有同族直接推出来？"** —— 能 ⇒ 照推 · 不能 ⇒ 才需要裁 ✅

| 层 | 形态 | **本件要不要新建** | 理由 |
|---|---|---|---|
| **数据** | `data/trinkets.json` | ✅ **要**（照 `buff_defs.json` 推）| — |
| **解析+校验** | `TrinketsConfig`（`scripts/data/`） | ✅ **要**（照 `BuffDefsConfig` 推）| — |
| **运行时状态** | `TrinketLedger`？ | 🔴 **【不要】** | 🎖️ **理由**：**饰品只是"挂在英雄上的 buff 集合"** ——<br>**而"已装备关系"归 `Roster`**（**每英雄 2 槽**）⇒ **不需要独立的 ledger** ✅ |
| **内核契约** | `ITrinketLedger`？ | 🔴 **【不要】** | ✅ **同上** —— **内核只需读 `Roster` 的装备关系** ✅ |

```
🎖️ **即：本件是【两件套】（数据 + 校验），不是四件套** ——
   而那正是【纪律 AF 的价值】：**能从既有同族推出来的层，就不该造新类** ✅
```

### 3.1 🆕 而"原语层"要单独一件（**架构的处方**）

```
🔴 **`data/dd1_buffs.json`（原语层 · 2020 条 · 只读对齐 · 机器生成 · 标 `origin:dd1`）** ✅
   · ✅ **与我方 `buff_defs.json`（概念层 · 22 条 · `origin:ours`）【并存】** ✅
   · ⚠️ **两层之间【不强行映射】**（**能映射就映射、不能就并存 —— 别造第三套 truth**）✅
   ⇒ 📄 **详见**：`dd1_baseline.md` **§26.3** · **纪律 AI**（"这两个东西【同层】吗？"）✅
```

---

## §4 🔴 依赖：属性模型要先扩（**映射可行性 76%**）

```
🔴 **口径（守纪律 AG/AH）**：「有落点」= `stat_type`/`stat_sub_type` 能映射到既有 `UnitStats`
   或既有管线步骤，**且不需新增内核能力** · **分子/分母均为 2020 条全集** ✅

🔴 **粗估：有落点 ≈ 1530/2020 = 【76%】** ⇒ 按架构判据 = **"可纳，但要先扩属性模型"** ⚠️
   · ✅ **很可能有落点**：`combat_stat_add/multiply`（**1041 = 52%**，sub_type 全是 UnitStats 有的）＋
     `resistance`（**358 = 18%**，⚠️ **原版 8 种 vs 我们 5~6 种**）＋ `*_chance`（**131 = 6.5%**）
   · ⚠️ **待评估**：`stress_*`(173) · `hp_heal_*`(105) · `resolve_*`(74) · `scouting_chance`(43) ·
     `food/starving`(31) · `surprise`(28) …
   ⇒ 🎖️ **即：不是"直接映射就行"** —— **要先把属性模型补齐**（**尤其 `resistance` 的 8 种**）✅
   ⇒ 📌 **`resistance` 8 种**（原版）：`stun` · `poison` · `bleed` · `disease` · `move` · `debuff` ·
      `death_blow` · `trap` ⚠️ —— 而**我们只有 5~6 种** ✅
```

---

## §5 🔴 验收（**怎么算"对齐"**）

| # | 判据 |
|---|---|
| **T1** | 🔴 **`trinkets.json` 有 196 条 · 每条 6 字段齐全**（`id`/`buffs`/`hero_class_requirements`/`rarity`/`price`/`limit`/`origin_dungeon`）✅ |
| **T2** | 🔴 **校验（照 `P28` 那类）**：`id` 唯一 · `buffs` 引用**必须存在于原语层** · `rarity` ∈ 13 种 · `price ≥ 0` ✅ |
| **T3** | 🔴 **槽位 = 2**（每英雄）· 超装被拒 ✅ |
| **T4** | 🔴 **职业限制可断言**：**非本职业的饰品【装不上】**（**且报错/置灰，不静默**）✅ |
| **T5** | 🔴 **非 `universal` 的 26 条【不可购买】**（**商店不列**）—— ⚠️ **判据用【`award_category`】，不是 `price`**（**`battle` 12 条里 11 条 `price>1`**）✅ |
| **T6** | 🔴 **读数**：**"装 2 件后属性/行为真的变了"必须可测**（**同 `A13`：以实际变化为准**）✅ |

```
⚠️ **明确不做（本期）**：
   · 🔴 **`award_category` 的掉落/任务接线**（**属条 3/条 5**）✅
   · 🔴 **`nomad_wagon` 商店接线**（**等 buff 原语层 + Trinket 表就位**）✅
### §5.1 🔴 实测读数（2026-10-02 · `M4u` 装卸接线后 · **只登记不改判据**）

| 判据 | 读数（原文） | 出处 |
|---|---|---|
| **T3** | `槽1=crow_wingfeather｜槽2=crow_tailfeather｜候选「crow_talon」=不可装备｜【饰品】2/2 格已装｜🔴 不可装备：「crow_talon」—— 「老铁」的饰品格已满（2 格）—— 先卸下一件再装「crow_talon」` | 冒烟 `--hamlet-trinket-seed=…` |
| **T4** | ✅ **同件不重复**：第二次投同一件 ⇒ 孔**收下载荷**、内核**原样拒** ⇒ 详情整行呈现（`🔴 不可装备：「crow_wingfeather」—— 「老铁」已经装着「crow_wingfeather」—— 同件不重复装`）· ⚠️ **职业不符**：`🔴 不可装备：「sacred_scroll」—— 「老铁」（warrior）职业不符：「sacred_scroll」限定 vestal` ⇒ **判据成立**，但**真实名册上 79 条专属件整体不可装**（断在数据口径 ⇒ `O-111`） | 冒烟 ＋ `reports/m4u_trinket_ui_20261002.md §四 ③` |
| **T6** | ⚠️ **验不了（如实登记）**：buff 原语层（`M2`）未接线（`#307` 冻结）⇒「装 2 件后属性 / 行为真的变了」**没有消费点** ⇒ 不假装生效；解冻条件 = `§39` 解冻 ＋ 原语层接线 | `reports/m4u_trinket_drag_20261002.md §七 ②` |
| **装卸（三条玩家路径）** | ① 落孔 ⇒ `HeroTrinketEquippedEvent { Slot = 2, Reason = drop }` ② 点方块 ⇒ `HeroTrinketUnequippedEvent { Slot = 1, Reason = click }` ③ 拖出孔外松手 ⇒ `HeroTrinketUnequippedEvent { Slot = 1, Reason = drag-out }` | `reports/m4u_trinket_drag_20261002.md §三` |

🔴 **两条真手势都验「点得到吗」**（红线 26 功能级验收）：点击**不是**直调回调 —— 按方块 `GetGlobalRect().GetCenter()` 合成 `Viewport.PushInput`（按下 ＋ 抬起）⇒ 命中测试由**引擎**做 ✓
   · 🔴 **`kickstarter` 294 条**（**众筹专属 · 整体排除**）✅
```

---

## §6 🎖️ 而本条（Trinket）贡献了 5 条纪律

```
· **AC**（裁前先量）：**两次生效**（第一层 196 · 第三层 2020）
· **AH**（引用数 vs 实体数）：**535 → 374** ✅
· **AE**（占位值）：`price = 1` 不是"便宜"是"不可购买" ✅
· **AD**（正式发行 vs 特殊来源）：`kickstarter` 294 条排除 ⇒ **省 60%** ✅
· **AI**（同层吗）：**交集 0 是正常的**（概念层 vs 原语层）✅
⇒ 🎖️ **即：一条"内容体系"的复核，把【量规模 · 辨占位 · 分层次】三种判据全用上了** ✅
```
