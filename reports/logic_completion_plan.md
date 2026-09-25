# 新策划案「未完成的逻辑侧功能」完成清单

- 立项依据：`doc/architecture/tasks/dd1_workstreams.md`（153 行，权威新策划案；§1 依赖图 · §2 模块表 · §7.2 追踪表）
- 裁定依据：`doc/windows/主程序窗口.txt` #460~#472（最新一条 = **#472**，2026-09-25；文件 7736 行）
- 纪律依据：`doc/modules/observe_list.md` 留档规则（① 本表项不许填进 `darkest/data/` ② 推进后移出并写 `dd1_baseline §43/§44`）
- 本清单口径：**只列【逻辑侧/kernel 侧】未完成项**；UI 域（`scripts/ui/**` · `scenes/**` · `resources/**`）与契约域（`doc/**`）不在本清单执行范围，仅标注依赖关系。
- 每项必须有：**实测读数 + 提交号**。读不到读数的项写「不能验」，不写「应该没问题」。

---

## 0. 依赖关系（先做什么）

```
M1a(属性字段) → M1b(按阶取属性) → M1c(换伤害模型) → { M4 Trinket / M3 buff 处置 }
M2(buff 原语层) ────────────────┘（M3 的输入）
M5/M6/M7/M8/M9/M10/M11/M12/M13/M14/M15  并行
```

- **M1c 是总闸**：它一改，157 个用例与全部平衡读数都要重测。所以 M1c 的「换公式」必须独占一轮，且前后读数齐全。
- **#472 ③ 已裁**：tier 切换取 **(乙) 先接线但默认读 0 阶** ⇒ 步骤串 = `接线(默认0) → 前后读数 → 切换 → 前后读数`，且「切换」排在 M1c 之后或同批**分步**。

---

## 1. 立即执行项（本批，顺序已定）

### P1 — #472 ①：清掉候选 12 条 `dmg_pct`

- **现状（已实测）**：`darkest/data/skills.json` 44 条技能，**23 条**带 `dmg_pct`。
  - **明确 11**（有依据，可落）：`warrior_cleave`(0) · `warrior_sweep`(0) · `warrior_lunge`(−50) · `warrior_war_cry`(−100) · `tank_guard_wall`(0) · `tank_shield_bash`(0) · `tank_heavy_ram`(−60) · `medic_scalpel`(0) · `medic_lethal_injection`(−80) · `commissar_command_blade`(15) · `commissar_burst_fire`(−50)
  - **候选 12**（我推的，**不许留在 `darkest/data/`**）：`warrior_battle_fury`(0) · `warrior_last_stand`(20) · `tank_taunt`(−100) · `tank_iron_wall`(0) · `tank_war_cry`(0) · `tank_selfless_charge`(−75) · `medic_anesthetic`(−100) · `medic_medicine_flask`(−90) · `commissar_charge_order`(−20) · `commissar_pistol_shot`(−15) · `commissar_supervise`(−80) · `commissar_execution_order`(−15)
- **做法（步骤串，纪律 BM）**：
  1. 用 LF 删除这 12 条各自的 `dmg_pct` + `_dmg_pct_source` 两行；
  2. **每删完一条立刻 parse 一次** `skills.json`（`89ad718` 的教训：一次删多条容易写坏 JSON）；
  3. 删完 parse 全绿后，`M1cStage3MechanismTests.cs:88-89` 的守卫 **23 → 11**，并把 L86-87 的过期注释（"11 填 / 33 未填"）改成实测值；
  4. 记**前后读数**：`withPct` 23 → 11、`skills.json` 行数变化；
  5. 跑 M1c 相关用例 + 全量测试，取读数；
  6. 提交（提交信息里写清「候选 12 条按 #472 撤出 `darkest/data/`，进 D5 待查」）。
- **完成判据**：`git grep -c dmg_pct darkest/data/skills.json` = 11（且 11 个 id 与上表一致）；测试全绿。
- **风险**：`_dmg_pct_note` 里若写了「23 条」也要同步（不能留假声明）。

### P2 — D5 待查清单登记（契约域，属我域 = 述我的依据）

- **现状（已实测）**：`doc/modules/observe_list.md` L193-201 已有 **D5**，列出候选 12 条。
- **待补**：
  1. D5 里补「**推进方式**」= 逐条读原版 `effect` 全串（一手 E 盘）——已有一句，核对是否覆盖 12/12；
  2. 补 **2 条待核实的重复对**（按 #472 判据 (b)）：`warrior_shield_bash` vs `tank_shield_bash` · `warrior_catch_breath` vs `tank_catch_breath` —— 记「同 type 同值，是否同一技能被两个职业各抄一份」待核；
  3. D5 的定位句必须写清「**这 12 条的值我已撤出 `darkest/data/`**，未核实前不得回填」。
- **完成判据**：`observe_list.md` D5 段落能被第三方按行号定位到 12 条 id + 推进方式 + 撤出声明。

### P3 — 祖产代理 ② 接线（#471 / #470 已裁，当前未做的最大一块）

- **现状（已实测）**：
  - `darkest/data/heirlooms.json` L165-166 已有 `roster_cap_delta: 2` 的兑换效果（step ① 数据就位，`3a8df45`）；
  - `darkest/scripts/gameplay/sim/run/HeirloomStock.cs`（180 行）有 `AwardForTier`(L49-62) 与 `Add`(L64-80)，**`AwardForRun` 不存在**（`git grep` = NONE）；
  - 唯一调用点：`darkest/scripts/gameplay/sim/run/ExpeditionFlow.BattleReturn.cs:63` → `_heirlooms?.AwardForTier(_log, LightMeter.TierId(_meter.Tier));`
- **做法**：
  1. `HeirloomStock` 加 `Reward` + `AwardForRun(log, steps, averageLevel, reason)`，**保留 `AwardForTier` 作为桥**（行为不变 ⇒ 纪律 AA：搬家必须带行为）；
  2. 换 `ExpeditionFlow.BattleReturn.cs:63` 的调用；
  3. 接**两个代理**（都标 `placeholder` + 推断 + 进观察清单）：
     - 难度 ← 队伍平均等级：`[0,1,2]→1 / [2,3,4]→3 / [4,5,6]→5`（**重叠取靠后档**，即 2 → 3、4 → 5）；
     - 长度 ← 「这趟的段数」：1~4，索引 `row[length-1]`（口径 (ii)：length 1 ⇒ 0）；
  4. 记**前后读数**：before = 按光照档 `0/1/2/4/9` → after = 档1 `0/10/14/26` · 档3 `0/12/18/36` · 档5 `0/18/28/54`；
  5. 更新受影响的约 8 个测试文件；
  6. 找 `Reward` 的组合根注入点。
- **完成判据**：同一条远征日志、同一队伍等级、同段数，跑两遍祖产入账**数值相同**；`AwardForTier` 仍在且被测试覆盖（桥未断）。
- **不能验的部分**：两个代理值本身**没有一手依据**（是我推的）⇒ 只能验「接线正确」，**不能验「代理值正确」**。

### P4 — 祖产代理 ③ 删 `tier_drop`（**必须等 P3 落地之后**，#470 顺序取 (A) 先接线再删）

- **现状（已实测）**：`darkest/data/heirlooms.json` 有 `tier_drop`；`HeirloomConfig.cs` 有 `TierDrop`/`TierOrder`/`DropFor`/P23；`HeirloomQuestRewardConfig.cs`、`HeirloomQuestRewardStep1Tests.cs` 有相关断言。
- **做法**：数据侧删 `tier_drop` → 代码侧删 4 个成员 + P23 → 清两个文件的残引用 → 前后读数 → 提交。
- **完成判据**：`git grep tier_drop` 在 `darkest/` 下 = 0 命中；测试全绿。

---

## 2. M1 系（总闸，串行）

### P5 — M1a 剩余

| 子项 | 现状 | 待做 | 完成判据 |
|---|---|---|---|
| 抗性 5→8 | `UnitStats.cs` 有 `PoisonResist/DiseaseResist/TrapResist = null`（3 个是 nullable 临时态） | **判定路径 + 结算路径各一条**（8 抗性全覆盖） | 8 抗性判定/结算用例各 1（读数） |
| `PhysDef`+`Dodge` ⇒ 单一 `def` | **未做**：`UnitStats.cs` 仍是 `Prot`/`Dodge` 两个字段 + 临时别名 `PhysDefAlias => Prot` | 3 个候选方案在 `reports/def_merge_three_plans.md`，**等裁定** | 合并**前后命中率读数对照** |
| `prot` 参与减伤 | 已裁定 **(a) 维持现状**（`2544718` 有 M6 对照读数） | 无 | — |

- **注意（架构裁定）**：`prot` 的「零行为证明」只能表述为「换源不影响现有行为，但新数据的正确性未被任何用例验证」，**不能写成「已验证正确」**。

### P6 — M1b 剩余

- 现状：`units.json` weapon/armour 各 5 阶，`UnitStatsMapper.cs` 已透传（`WeaponTiers: c.Weapon, ArmourTiers: c.Armour`）。
- 待做：① **阶数 ∈ 1~5 校验**（越界报错） ② **缺阶报错** ③ **旧字段零行为**（证明 `WeaponTiers`/`ArmourTiers` 为 null 时走旧路径）。
- 完成判据：3 个校验用例 + 1 个「旧字段不变」对照用例，读数入报告。

### P7 — M1c 换伤害模型（**独占一轮**）

- 现状（已实测）：
  - `darkest/scripts/gameplay/sim/pipeline/DamagePipeline.cs`（276 行）**没有** `WeaponBaseDamage` / `ProtFraction` / 按阶取属性 —— 公式**尚未换**；
  - `sim/board/TierDefence.cs` · `sim/pipeline/WeaponBaseDamage.cs` 存在但**零生产消费者**；
  - 现有 3 个 M1c 测试都是「钉住现状」，不是「钉住新公式」。
- 待做（**解冻口径四件**：① 逐条登记 + 证据 ② 一次只改一类 ③ 前后读数 A1/A2/V10 ④ 改完回冻结）：
  1. 公式：`damage = 武器区间 ×(1+技能 dmg%)`；
  2. `DamagePipeline` 换公式（**只改这一类**）；
  3. 公式单测；
  4. 前后读数 A1/A2/V10 + 157 用例重测。
- **完成判据**：公式单测绿 + 全量测试读数 + A1/A2/V10 三组前后对照表。
- **P7 之后**才能做「第三步 切换（tier source）」（#472 ③ 步骤串的后半段：接线已经默认 0，切换 = 让默认读 `weaponTier`/`armourTier`，来源见 `O-101`）。

---

## 3. M2 / M3（buff 层）

### P8 — M2 buff 原语层

- **现状（已实测）**：`darkest/data/` **没有** `dd1_buffs.json`（25 个 JSON 里无此文件）。
- 待做：① `data/dd1_buffs.json`（原版 **2020 条**，11 字段，`origin:dd1`） ② `BuffPrimitiveTranslator` ③ 未映射清单 ④ P 校验。
- 完成判据：条数读数 = 2020、字段数读数 = 11、未映射清单条数、P 校验用例。

### P9 — M3 22 条 buff 三类处置 step ②

- 现状：step ①（列出 22 条 + 三类）已有（`e251e0b`/`1c34b8d`）；**step ② 未做**。
- 待做：折磨/美德 7 → `traits.json`；机制态 10 → 换原版对应态；自加 5 → 废掉/移层。**8 文件约 20 个消费点必须带行为一起改**（纪律 AA）。
- 完成判据：22/22 有处置结果 + 20 个消费点前后读数 + 无残留悬空引用。

---

## 4. M4~M9 剩余

| 卡 | 现状 | 待做 | 完成判据 |
|---|---|---|---|
| **M4** Trinket | 196 条 7 字段已落（`4c04e13`） | **T1~T6 验收**（尤其 **T6 装 2 件后属性/行为真的变了**）· 槽位 2/英雄 · 职业限制 · `price ≤ 1` 不可购 | T1~T6 各 1 读数；T6 必须列出属性前后值 |
| **M5** Quirk | 170 条中先 ~20 已落（`9a3d2c8`）；`QuirksConfig.cs` 里 `IsDisease`/`IncompatibleQuirks`/引用完整性 throw/**对称性检查**/`IncompatibleEdgeCount` **已在** | 逐条**验证**（互斥对称 · 疾病必须 `is_disease` · `curio_tag` 引用存在）+ 「同时带 A 与 B ⇒ 必须拒绝」用例 | 4 个 P 校验用例读数 |
| **M6** 建筑 | 8 建筑/20 树/99 等级（`2e09649`）；`BuildingsConfig.cs` 三个 P 校验（code 树内唯一 · 前置存在且无环 · 花费类型在集合内）+ 一手实测注释已在 | 确认 `prerequisite_requirements` P 校验（引用存在 · 不得成环 · **允许跨树**）+ 内容批1~批4 | 跨树前置必须**能过**（不是报错）；内容批逐批读数 |
| **M7** 名册招募 | ① 数据侧已落（`unlocks.json` 无 `roster_cap_delta:` 目标，`UnlocksConfigTests.cs:54` 断言"已撤"）但 `UnlocksConfig.cs:136-142` / `RunProgress.cs:110-111` **仍解析该前缀** | ① 删代码侧解析 ② 刷新 2~7 人走 `IRngProvider`（**不许 `Random`**） ③ 高级新兵 18.75%/12.5%/6.25% + `first_hero_classes [plague_doctor, vestal]` | ①`git grep roster_cap_delta` 只剩测试的"不存在"断言 ②`git grep Random` 在招募路径 = 0 ③概率用例读数 |
| **M8** 职业 15 | 15 职业/135 树/645 等级已落（`4d98c6a`） | 校验 + 验收读数 | 15/135/645 读数复核 |
| **M9** 4v4 | 夹具已钉（`c60689a`）；但 `BattleProjector.cs:80` / `BattleDirector.cs:189` **仍有 support-slot SP 维度**；`formation.json` 玩家 `support_slots [5,6]`、敌人 `[]` | ① **两侧对称 4 槽**（`support_slots` 非空） ② 契约仍能表达 6 槽 ③ 去掉 support-slot 的 SP 维度 | ①敌人 `support_slots` 非空读数 ②6 槽契约用例 ③SP 用例读数 |

- **#469 提醒**：4v4 的「三种 4 人」= ① 开局名单 `first_hero_classes` ② 编队上限 4 ③ **验收夹具 = warrior + tank + medic + commissar**（**夹具不是规则**）。

---

## 5. M10~M15 剩余（逻辑侧）

| 卡 | 现状 | 待做 |
|---|---|---|
| **M11** 工程基线 | 未核 | 逻辑侧基线项（门禁 0 命中已常态） |
| **M12** 补给 | `darkest/data/` **无**补给数据文件；仅 `scripts/ui/ProvisionSkeleton.cs`（UI 骨架） | **kernel 侧接口** + 补给数据（一手：`inventory/base.supply.inventory.items.darkest`，1618B，10~11 条，字段 `base_stack_limit`/`purchase_gold_value`/`sell_gold_value`/`act_out_consume_priority`/`replace_buffs`） |
| **M13** 出征队伍 | 未核 | 出征队伍逻辑侧 |
| **M14** Quest | `darkest/data/` **无** quest 文件 | Quest 层（**长期承载 `length`/`difficulty`**，见 #471）；一手参考：`campaign/quest/` 5 表 154KB |
| **M15** Hamlet 可读性 | **P0 已落**：`RunStartSnapshot.cs`（`Capture` L52/L134 · `DiffLines` L158），被 `ExpeditionContext.cs` L53/L79-81 等消费 | **P2 同 seed 对照** |

---

## 6. 需裁定才能动（不能自行决定）

| 项 | 卡在哪 | 需要谁裁 |
|---|---|---|
| `PhysDef`+`Dodge` ⇒ 单一 `def` | 3 个候选方案（`reports/def_merge_three_plans.md`），三种都改命中率 | 架构 |
| `prot` 是否真参与减伤 | 已裁 (a) 维持现状 ⇒ 如要改，需重开 | 架构 |

## 7. 观察清单联动（纪律 BL）

- **必须进观察清单**（都是**推断/placeholder**）：祖产「难度 = 队伍平均等级」· 祖产「长度 = 这趟段数」· 祖产「每趟都给 4 种」= 已有 **O11**。
- **已在清单**：**D5**（`dmg_pct` 候选 12 条）· D1~D4（commissar/medic 技能映射）· **O11**。
- 规则：观察清单里的值 **不许填进 `darkest/data/`**；推进后移出并写 `dd1_baseline §43/§44`。

---

## 8. 本清单自身状态

| # | 项 | 状态 |
|---|---|---|
| P1 | 清掉候选 12 条 `dmg_pct` | ✅ **已完成** `ce5cebd`（23→11 条，bytes 33672→32639，行 1728→1704，全量 823/823 绿） |
| P2 | D5 登记补全 | ✅ **已完成** `ce5cebd`（D5 重写＝12 条按原型分组＋撤出声明；**当场更正**原 D5 的两处台账错误：`warrior_lunge` 不是候选 / `medic_medicine_flask` 是候选，依据＝窗口 L7161-7176、L7178-7185） |
| P3 | 祖产代理 ② 接线 | ✅ **已完成** `49b1908`（生产结算点 `AwardForTier` → `AwardForRun`；新读数 档1 0/10/14/26 · 档3 0/12/18/36 · 档5 0/18/28/54 · 端到端 3 场 = 46；全量 831/831 绿） |
| P4 | 祖产代理 ③ 删 `tier_drop` | ✅ **已完成**（`tier_drop`/`drop_note`/`HeirloomDropSpec`/`DropFor`/`TierDrop`/P23①两条/`AwardForTier`/`KindAmount`/回落参数 **九件全删**；**零行为=删前删后真跑读数逐位相同** 11/22/22/11=66；全量 832/832 绿）🔴 **并当场打穿契约 P23 ③**（一趟 46 ≥ 三栋首级 14 ⇒ 见 §39 第 9 项 + `reports/contract_change_request_heirloom_step2.md`） |
| P5 | M1a 剩余 | 待做（`def` 合并阻塞于裁定） |
| P6 | M1b 剩余 | 待做 ← **下一项** |
| P7 | M1c 换伤害模型 | 待做（总闸，独占一轮） |
| P8 | M2 buff 原语层 | 待做 |
| P9 | M3 step ② | 待做 |
| P10 | M4 T1~T6 验收 | 待做 |
| P11 | M5 P 校验复核 | 待做 |
| P12 | M6 P 校验 + 内容批 | 待做 |
| P13 | M7 ①②③ | 待做 |
| P14 | M8 复核 | 待做 |
| P15 | M9 4v4 剩余 | 待做 |
| P16 | M12 补给 kernel | 待做 |
| P17 | M14 Quest | 待做 |
| P18 | M15 P2 同 seed 对照 | 待做 |

> 执行顺序：**P1 → P2 → P3 → P4 → P6 → P7 → 第三步切换 → P5/P8/P9/P10…**，每项落地即回填本表的「状态」列与提交号。
