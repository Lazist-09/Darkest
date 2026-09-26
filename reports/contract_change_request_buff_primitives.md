# 契约变更请求 · **A1 落了 buff 原语层之后，契约侧有 5 处要与树对齐**

> 🕒 **提出：2026-09-26** · **提出人：主程序** · **落地提交：`c74954e`（A1）**
> 🔴 **我为什么只提请求、不自己改**：这五处都在 `doc/architecture/**` = **契约域** ⇒
>    按纪律「**不代笔别人域**」我**不动**它 ⇒ 但**必须把"现在树与契约不一致"讲清楚**（否则下一个人会当成缺陷）⚠️
> 📌 **性质**：这不是"请批准"，而是"**我改了实现 ⇒ 契约的描述已成事实错误/缺口**"的**对账单** ✓
> 🔗 姊妹单：`reports/contract_change_request_heirloom_step2.md`（传家宝步骤 ② 的 4 处）✓

---

## 0. 一句话

```
🆕 **本仓现在有了【buff 原语层】**：`darkest/data/buff_primitives.json`（参考项目 1801 条）
   ＋ `scripts/data/BuffPrimitivesConfig.cs`（解析/校验）＋ `BuffPrimitiveTranslation`（分类/清单/加载即校验）✓
⇒ ✅ **实现侧自洽**（构建 0 错 · 841/841 用例 · 六门禁全绿 · 冒烟 13/13 ✓）
⇒ 🔴 **但契约侧有 5 处**：文件**名字对不上**、**形状没登记**、**量纲没定**、
    **判据源换了（一手 → 参考项目）**、**覆盖度差集没登记** ⇒ 本单逐条给出**现状 / 建议改成 / 依据** ✓
```

---

## 1. 逐条（**每条：现在怎样 → 问题 → 建议改成**）

### 1.1 🔴 文件**名字对不上**：契约（注释）写 `dd1_buffs.json`，树里叫 `buff_primitives.json`

```
现在写的：`darkest/scripts/data/TrinketsConfig.cs` 的注释 ——「原语层（M2 的 `dd1_buffs.json`）」
          （这是**实现侧**注释；但它是照契约文本抄的 ⇒ 契约文本里应也写作 `dd1_buffs.json`）
🔴 问题：树里**从来没有** `dd1_buffs.json` 这个文件（GitHub 全历史可查）⇒ 名字是**旧计划名**；
   而本轮落的文件叫 `darkest/data/buff_primitives.json` ✓
✅ 建议改成：契约统一写作 **`buff_primitives.json`**（与 `BuffPrimitiveTranslation` / `BuffPrimitivesConfig`
   的命名一致 ⇒ "原语层"这个词在本仓已是**既有词汇**，不必另造 `dd1_` 前缀）✓
   ⚠️ 并请**顺手清掉** `dd1_buffs.json` 这个名字在 `doc/**` 里的其它出现处（若有）——
      **一个不存在的文件名留在契约里 = 下一个人会去找它** ✓
```

### 1.2 🔴 **形状没登记**：`buff_primitives.json` 的结构与闭集应当写进 `data_schema`

```
🔴 现状：原语层的**字段形状 / 闭集 / 结构不变量**都没有在 `data_schema` 里登记
   （校验实现在 `BuffPrimitivesConfig.Validate`，词表真值在数据侧 ✓）✓
✅ 建议按 P23 的体例补一条，**形状**（实测，逐字来自参考项目）：
   · 根：`{"_note","_source","_ruling","_field_classes","primitives":[...]}` ✓
   · 每条 **10 个字段**：`id` / `stat_type` / `stat_sub_type` / `amount` / `remove_if_not_active` /
     `rule_type` / `is_false_rule` / `rule_data.{float,string}` / `duration_type`? / `duration`? ✓
   · **闭集**（实测）：`stat_type` **25** · `(stat_type, stat_sub_type)` **41** · `rule_type` **23** ·
     `duration_type` **5**（`combat_end` / `quest_end` / `activity_end` / `idle_start_town_visit` / `quest_complete`）✓
   · **结构不变量**：`id` 唯一 · `stat_type`/`rule_type` 非空 · `rule_data` 恒为 `{float,string}` 两键 ·
     🔴 **`duration_type` 与 `duration` 同生同死**（实测 63 条都有 / 1738 条都没有）· `duration > 0` ✓
   · **触发条件**：违反任一条 ⇒ **加载即抛**（`BuffPrimitivesConfig.Parse`，`InvalidDataException`）✓
   · **出处**：本地参考项目 `Assets/Resources/Data/JsonBuffs.json` ✓
   依据：`darkest/data/buff_primitives.json` · `darkest/scripts/data/BuffPrimitivesConfig.cs` ·
        `reports/ref_buff_primitives_source.md`（每次运行重测）✓
```

### 1.3 🔴🔴 **量纲没定（本单最重要的一条）**：参考项目的 `amount` 是**分数**，我方是**整数百分比**

```
🔴 实测：`amount` **非整数 1676/1801** ✓
   · `0.04` 读作 **4%**；`combat_stat_multiply` 的 `0.2` 读作 **×1.2**（**不是 ×0.2**）✓
   · 我方是**整数百分比**（`HealAmount.Scale(baseHeal, pct)` 做 `baseHeal ×(1 + pct/100)`）✓
🔴 问题：**契约里没有这条换算规则** ⇒ 任何一个"把参考项目数值搬进我方表"的人都会遇到同一个岔口：
   **要不要 ×100？乘完怎么舍入？**（0.005 这种值 ×100 之后 = 0.5 ⇒ 舍成 0 还是 1？）
   ⇒ 🎖️ 这正是"两个人都按自己的理解做 ⇒ 数值表出现两套口径"的经典入口 ⚠️
✅ 建议：在 `data_schema` 里**显式写死一条换算口径**（内容请架构与策划定，我给实测依据）：
   · **方向**：参考项目分数 → 我方整数百分比 **必须 ×100** ✓
   · **舍入**：请指定 —— (甲) `Math.Round` 半上入（与 `HealAmount.Scale` 同口径）／
     (乙) `Floor`（保守，不让 buff 变强）／(丙) 保留一位小数改我方字段类型
   · **负值**：`-0.03 ×100 = -3` ⇒ 舍入口径对负数要**同一套**（`Math.Round` 的银行家舍入会咬人）✓
   依据：`BuffPrimitivesTests.Amount_IsAFraction_NotAnIntegerPercent`（把 **1676** 钉住）·
        `reports/ref_buff_primitives_source.md §实测` ✓
🔴 **在口径定下来之前，A2/A7 的数值搬运我先不做**（做了就是**替两方**定口径 ⇒ 违反"不代笔"）✓
```

### 1.4 🔴 **判据源换了**：原语分类表的"谁说了算"从**一手 E 盘**变成**本地参考项目**

```
现在写的：`darkest/tests/BuffPrimitiveTranslationTests.cs`（**已删**）的判据源 =
          `reports/dd1_buff_primitives_primary.json` ⇒ **一手 E 盘** `base.buffs.json`
          （2020 条 / **27** 个 `stat_type` / **27** 个 `rule_type`）✓
          ⚠️ 它的注释写着「**一手对账发现漏 8 多 1** ⇒ 判据源必须是一手」—— 那是**旧优先级**下的结论 ✓
🔴 问题：用户指令（2026-09-25）「**数值采用本地参考项目的来源**」**顶替**了 `dd1_baseline §32.1/§32.3`
   的「一手 > 二手 > 第三方」⇒ **"谁说了算"变了** ⇒ 契约里凡写"以一手为准"的地方都要重述 ✓
✅ 现状（树里已是事实）：判据源 = **`darkest/data/buff_primitives.json`**（参考项目 1801 条 / 25 / 23）✓
   一手退化为**覆盖性交叉校验**（`PrimitiveSourceCoverageTests`：只回答"参考项目比一手少了什么"）✓
✅ 建议改成：在 `dd1_conformance.md` / `data_schema.md` 里**显式写清优先级的新表述**，例：
   「**数值来源 = 本地参考项目**（用户指令 2026-09-25）；一手 E 盘降为**覆盖度交叉校验**，
    两者差集必须显式登记（见 1.5）」✓
```

### 1.5 🆕 **覆盖度差集要登记**：采用参考项目会**丢 3 个 `stat_type` + 4 个 `rule_type`**

```
🔴 实测差集（两侧都是实测，不是估计）：
   · `stat_type`：一手 **27** / 参考 **25**
       - 一手独有 **3**：`activity_side_effect_chance` · `crit_received_chance` · `ignore_stealth`
       - 参考独有 **1**：`hp_heal_amount`
   · `rule_type`：一手 **27** / 参考 **23**
       - 一手独有 **4**：`attacking_monster_type` · `is_actor_status` · `is_guarded` · `monster_type_count_min`
       - 参考独有 **0**
🔴 问题：**采用参考项目 = 这 3 个原语 + 4 条规则在参考项目里"换了写法或干脆没有"** ⇒
   依赖它们的怪癖/饰品效果会**静默变弱或消失** ⚠️（"静默丢功能"正是本仓纪律明令禁止的）✓
✅ 建议：在 `dd1_conformance.md` 的偏离登记里**加一行**（**不是我改坏的，是"采用新来源的代价"**）：
   「🟡 **原语表达力差集**：采用参考项目后，`activity_side_effect_chance` / `crit_received_chance` /
     `ignore_stealth` 与 4 个 `rule_type` **无定义** ⇒ 🔴 **待策划裁**：
     (甲) 接受丢失 ／ (乙) 这 3 类原语改从一手补（**混合来源**）／ (丙) 用参考项目的等价写法替代」✓
   依据：`darkest/tests/PrimitiveSourceCoverageTests.cs`（把两个集合**逐个钉住**，上游一变就红）✓
```

---

## 2. 我这边**已经做完**的（供对账，不是请求）

```
· 数据：`darkest/data/buff_primitives.json`（584KB · 1801 条 · 逐字段转写，**不做任何换算**）✓
· 代码：`BuffPrimitivesConfig.cs`（解析 + 结构校验 fail-fast）·
        `BuffPrimitiveTranslation.cs`（`ByStatType` 逐名实测 · 新增 `MoraleMod`/`HealMod`/`ExpeditionLayer` ·
        `Activated`/`Pending` **单一真值** · `ValidateAgainst`）·
        `HealAmount`（只转发清单）· `DirectorBridge`（**加载即校验 = 唯一生产消费点**）✓
· 修正：旧 M2 冻结清单里 **3 个上游不存在的名字**（`resolve_xp_percent` / `remove_quirk_chance` /
        `dmg_received_percent`）⇒ 真名 = `resolve_xp_bonus_percent` / `remove_negative_quirk_chance` /
        `damage_received_percent`（**旧名单里那 3 条永远接不上** · 红线 21）✓
        条数 13 → **24**（= 实测 25 个 `stat_type` − 已激活的 1 条）✓
· 用例：`BuffPrimitivesTests`（5 条）+ `PrimitiveSourceCoverageTests`（1 条）；
        删除已被取代的 `BuffPrimitiveTranslationTests`（旧判据源）✓
· 工具：`tools/dsh/extract_ref_buff_primitives.py`（每次运行**重测**，与指令定义不符即 exit 1）✓
· 读数：556 个去重引用 ⇒ **可解析 482 / 未解析 74**（50 已改名 + 24 未覆盖）·
        去向分布 `StatMod 579 / DamageMod 359 / UnitResistance 329 / MoraleMod 232 /
        ExpeditionLayer 109 / ProbMod 99 / HealMod 94` ⇒ **Frozen 0** ✓
· 环境：构建 0 错 · 全量 **841/841** · 六门禁全绿（443 文件 / 0 白名单）· 冒烟 **13/13** ✓
· 记录：`dd1_baseline §57` · `reports/ref_buff_primitives_source.md` ✓
```

---

## 3. 阻塞 / 待裁定

```
🔴 **待架构（契约）**：1.1 文件名 · 1.2 形状登记 · 1.3 **量纲口径** · 1.4 优先级重述 · 1.5 差集登记 ✓
🔴 **待策划（数值/取舍）**：1.5 的三选一（接受丢失 / 混合来源 / 等价替代）——
   ⚠️ 这条**不阻塞我**（我可以先做 A2 技能 `dmg%`，它只用 `combat_stat_*` 与 3 个 `_chance`，
     全在参考项目覆盖范围内）✓
🟡 **待架构**：1.3 的舍入口径（甲/乙/丙）—— ⚠️ **这条阻塞 A2/A7 的数值搬运**
   （口径没定就搬 = 我替两方定了口径 ⇒ 违反"不代笔"）⇒ 请先给口径 ✓
✅ **不阻塞的部分**：A1 的实现与门禁全绿；`TrinketsConfig` 的 `knownBuffIds` 交叉校验
   **现在【能】开**（482 条可解析），但**要等 A7 把 74 条悬空清零才该开**
   （否则 `Parse` 会 fail-fast 抛 ⇒ 现在开等于把 74 个已知问题变成加载崩溃）✓
```
