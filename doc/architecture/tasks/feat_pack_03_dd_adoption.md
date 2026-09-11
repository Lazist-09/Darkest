# 功能包 03：DD 借鉴落地（眩晕递增 / miss 补偿 / Mark / DoT / 死门后遗症 / 条件解锁 / 暴击情绪链）

> **编号**：ARCH-T-FEAT-PACK-03 · **类型**：task · **状态**：可开工
> **上游**：[设计] **`doc/modules/dd_reference.md`（全文，含每条的确切规格）** · `combat_math.md` §4/§5 · `buff.md` §标记 · `morale.md` · `ui_spec.md` §6
> **决策引用**：#202–#210
> **开放项**：~~O-57（DD 借鉴包）/ O-58（AOE 暴击士气核对）~~ → ⚠️ **架构师已改号（v0.52）**：**O-58（DD 借鉴包 D0~D7）/ O-59（AOE 暴击士气核对 #208）**——原 O-57 已被"敌方意图预留"占用（先到先得）。见 `open_issues.md` 表头重号说明与 §5 回执。
> **依赖**：与 `m6_fix_pack` / `feat_pack_01` / `feat_pack_02` **互不冲突**
> **最近更新**：2026-09-09

> 🔴 **本卡只写"改哪里、怎么验"；每条的确切数值与 DD 原值对照见 `doc/modules/dd_reference.md` §1–§2。**
> ⚠️ **顺序约束**：本包**全部改动都影响战斗结果** → **必须在复测（`m6_fix_pack` P3 v3）之前落地**，
> 否则基线再次作废（已踩 4 次）。

---

## D0 · 眩晕抗性递增（#202）

| 项 | 内容 |
|---|---|
| 规格 | 新增临时状态；每次**成功施加眩晕** → 眩晕抗性 **+50%** 可叠加；**该单位完成一次未被晕的行动即清除**；抗性 = 基础 + 递增，**上限 100%**；实际概率沿用**乘法口径** |
| 改动 | `sim/buffs/BuffLedger.cs` 或 `UnitRuntime`（新增递增计数）+ `compat/EffectsStep`（施加成功时递增）+ `TurnSequencer`（未晕行动时清除）+ `BattleMath.ActualEffectChance` 读递增值 |
| 验收 | 连续晕同一目标 → 实际概率**逐次下降**；出现一次未被晕的行动后**清零**；抗性到 100% → 实际概率 0 |

## D1 · 连续未命中补偿（#203）

| 项 | 内容 |
|---|---|
| 规格 | 每单位连续未命中计数（命中清零）；**补偿 = max(0, 连续未命中 − 1) × 4**；**仅作用于命中判定、不改面板显示**；结果 clamp 100 |
| 改动 | `UnitRuntime`（计数）+ `HitStep`（读补偿）+ `BattleProjector.HitRateFor`（**不得**把补偿算进显示值）+ `TurnSequencer`/回合钩子重置策略 |
| 验收 | 连续 2 次 miss → 第 3 次实际命中率 +4；命中后清零；**面板显示值不变** |

## D2 · Mark 标记（#204）

| 项 | 内容 |
|---|---|
| 规格 | ① `buff_defs` 新增 `mark`（negative / 3 回合 / refresh / 可驱散 / 无数值）；② 技能字段 `bonus_vs_marked_percent`（起手 **+25%**，乘法阶段）；③ 敌方 AI 对带 mark 的**我方**提高权重（**复用 #187 权重制**，起手 ×2）；④ **mark 不可被抵抗**；⑤ 起手：**政委【督战】追加 mark**；**军医【致命注射】、政委【处决令】**（两条 `missing_hp` 收割技能）声明 `bonus_vs_marked_percent: 25` |
| 改动 | `data/buff_defs.json` · `data/skills.json`（3 条）· `SkillsConfig`（新字段 + 校验）· `EnemyAi.ResolveTargets`（权重分支）· `DamageStep`（加伤） |
| 验收 | 施加后目标带 `mark`；声明技能对其加伤；敌方 AI 对带 mark 的我方权重提高；**mark 不可被抵抗**；与「嘲讽」互不干扰 |
| ⚠️ | **职责划清**：嘲讽＝我方防守 / 标记＝我方进攻；**不要造两个同功能机制** |

## D3 · DoT 三条硬规则 + 补施加者（#205）

| 项 | 内容 |
|---|---|
| 规格 | ① 流血**不受物防减免**（写死）；② **暴击施加时长 2 → 4**；③ **在目标回合开始结算**；④ 每回合伤害**不暴击**；⑤ **战士【突刺】、军医【致命注射】各追加流血 3×2** |
| 改动 | `pipeline`（流血结算时机从回合末 → **目标回合开始**）· `EffectsStep`（暴击延长）· `data/skills.json`（2 条追加 bleed）· `DamageStep`/`BattleMath`（明确 DoT 不吃物防） |
| 验收 | 流血不被物防减免；暴击施加 → **4 回合**；**目标回合开始**结算；每回合伤害不暴击；**场上有技能能施加流血** |

## D4 · 死门后遗症（#206）

| 项 | 内容 |
|---|---|
| 规格 | 死门**存活并归队**后获得：**受伤 +10% · 命中 −5 · 速度 −1**；**到战斗结束**；**多次进出不叠加**；UI 显著标注 |
| 改动 | `WeakDeathsDoor`（归队时授予）· `buff_defs` 或 `UnitRuntime` 字段 · `BattleMath`（受伤/命中/速度读取）· `BattleUi`（标注） |
| 验收 | 死门存活 → 获得减益；**多次进出只有一层**；到战斗结束；UI 可见 |

## D5 · 条件解锁技能（#207）

| 项 | 内容 |
|---|---|
| 规格 | 技能新增 `requires`：`self_hp_below_percent` / `target_hp_below_percent` / `self_weak` / `self_deaths_door`；不满足 → **灰显 + tooltip 原因**；起手 2 个：**战士【殊死一搏】→ `self_hp_below_percent: 50`**；**政委【处决令】→ `target_hp_below_percent: 50`** |
| 改动 | `SkillsConfig`（字段 + 校验）· `SkillUseResolver`（新判定分支 + `AvailabilityReason`）· `data/skills.json`（2 条）· `ui_spec` tooltip 字串 |
| 验收 | 不满足 → 灰显 + 原因；满足 → 可点；与既有战斗/支援两路一致 |

## D6 · AOE 暴击士气核对（#208）🔴 先查

| 项 | 内容 |
|---|---|
| 要做的事 | **核对** `morale_events.critical_strike_dealt`（全队 +5、`scope: team`）在 **AOE 多目标暴击**时是否**按目标数重复结算** |
| 若重复 | **修**为"**只结算一次**"（DD 规则），并加回归用例锁死 |
| 验收 | 一次 AOE 打出 N 个暴击 → 全队士气 **只 +5 一次**；单体 AOE（1 目标）行为不变 |

## D7 · 暴击情绪链（#209）—— **含对 #157 的修正**

| 项 | 内容 |
|---|---|
| 规格 | ① **被暴击** → **自身 −10 士气**、**队友各 50% −5**；② 打出暴击维持（全队 +5）；③ **暴击治疗** → 治疗量 **×2** + 目标 **+4 士气**，概率**固定**：**单体 12% / 多目标 5%** |
| 改动 | `data/morale_events.json`（新增 `physical_crit_hit_self` / `physical_crit_hit_ally`）· `pipeline`（暴击路径触发）· 治疗路径（暴击治疗判定 + 翻倍 + 士气） |
| 🔴 可读性补偿 | `ui_spec` §6 **升级为"三态可分"**：**普通物理（不掉）／精神（紫）／被暴击（橙·震慑）**——三者必须一眼可分 |
| 验收 | 被暴击 → 自身 −10、队友 50% −5；**普通物理命中仍不掉士气**；暴击治疗 ×2 + 士气 +4，概率 12%/5% 且**不受任何修正影响**；三态视觉可分 |

---

## 4. 红线

| 红线 | 说明 |
|---|---|
| **必须在复测前落地** | 本包**全部**影响战斗结果；晚于复测即令基线作废 |
| **所有新随机写 `RngDraw`** | 被暴击队友 50%、暴击治疗、连续 miss 补偿（若引入抽样） |
| **不改面板显示** | 连续 miss 补偿**不得**进入 `HitRateFor`（DD 是隐藏的） |
| **可读性不许退步** | #209 必须同时交付"三态可分"的视觉补偿，否则 §6 的设计前提被破坏 |
| **一步一提交** | D0~D7 独立提交 |
| **架构侧需镜像** | ✅ **已完成（v0.52，见 §5）**：`data_schema`（`mark` buff / `requires` / `bonus_vs_marked_percent` / DoT 四规则 / `miss_compensation` / `stun_resist_buildup` / `deaths_door_recovery` / `crit_chain` + **P18** + §8 事件映射）· `open_issues` **O-58/O-59**（原 O-57/O-58 顺延）· `blueprint`/任务卡口径公告 · **策划侧待同步项已登记**（combat_math §4/§5、buff.md §标记、ui_spec §6 已由策划更新、skill_data 追加行） |

---

# 5. 架构镜像记录（v0.52，架构师）

> 回执本包 §4「架构侧需镜像」+ 编号与顺序事项。**本包 D0~D7 规格未改动**，仅改号、补回执与依赖指针。

| 项 | 回执 |
|---|---|
| **编号改号（重号处理）** | ⚠️ 本包原提案 **O-57/O-58** 中 **O-57 已被 v0.50「敌方意图预留」占用** → 按"先到先得 + 引用面最小"**顺延为 O-58（DD 借鉴包）/ O-59（AOE 暴击核对）**；`open_issues.md` 表头已记录 |
| **顺序约束（强化，采纳本包原文）** | 🔴 本包 **D0~D7 全部影响战斗结果** → **必须在 `m6_fix_pack` P3 v3 复测之前落地**。与 `feat_pack_01` F0~F4、`feat_pack_02` G0 一起构成复测前置；P3 v3 报告须同时含实测 `D` / 口径健康度 / M 值 / 技能使用率 |
| **data_schema 镜像** | §2.1：新增 `mark`（EffectType）与 `until_battle_end`（DurationType）；§3.2：新增 **`requires`** 与 **`bonus_vs_marked_percent`** 字段（含起手落地点）；§3.4：新增 `mark`、`deaths_door_recovery` 两条 buff + `bleed` 改写为 **DoT 四规则** + unit_state 增 **眩晕抗性递增**；§3.7：新增 `miss_compensation` / `stun_resist_buildup` / `deaths_door_recovery` / `crit_chain` / `mark` 五组常量，`bleed` 扩为四规则；**§4.2 新增 P18**；§8 新增"v0.52 机制 → 事件映射" |
| **可读性交付物** | ✅ 已确认把 **D7 的 `ui_spec` §6 三态可分**列为**不可省交付物**（策划已更新 §6；m5 卡已挂该验收）——它是 #157 设计前提的补偿，缺它则"物理 vs 精神"可读性被破坏 |
| **漏洞预判（架构侧补充）** | ① `mark` 与 `taunt` 必须**职责分离**（进攻 vs 防守），P18 已把两者分开校验；② `requires` 与"携带 5 个"的关系：`requires` 只在**可用性判定**层灰显，**不改变携带集**（O-50 可插拔契约不受影响）；③ `miss_compensation` **不得进入投影/面板**（P18 ⑤），否则"隐藏补偿"变成可被玩家算出的明牌；④ 眩晕递增**不写入基础属性**（否则跨战斗污染） |
| **未代写项（留策划）** | `combat_math.md` §4（DoT 结算时机/不吃物防）、§5（暴击情绪链新增士气行）与 `buff.md` §标记（mark 落地）**尚未更新**——本包 §4 把它列为"架构侧需镜像"，但它们是**策划层文档**，架构侧**不代写**；已在 `open_issues.md` O-58 备注与 Project_Memory 登记为策划待同步项。`skill_data.md` 的三条技能追加行（督战 mark / 突刺·致命注射 bleed / 两条 requires）同属策划层 |
| **实施期数据落点（提醒）** | D0~D7 落地的 JSON 改动：`buff_defs.json`（mark）· `skills.json`（`requires` 2 条 / `bonus_vs_marked_percent` 2 条 / mark 1 条 / bleed 2 条）· `morale_events.json`（`physical_crit_hit_self` / `physical_crit_hit_ally`）· `tuning.json`（5 组新常量 + bleed 四规则）——**数字以 `dd_reference.md` 与 P18 为准**，不得顺手改既有数值 |
