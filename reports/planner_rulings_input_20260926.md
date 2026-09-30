# 主程序补投 13 张单 · 裁定输入汇总（只读研究 · 不含裁定）

> 🕒 2026-09-26 · **只读**：本次研究**未创建/修改任何既有文件**（唯一新增产物 = 本文档）。
> 📌 **本文档不是裁定** —— 全部「我的建议」都只是**建议**；裁定权在策划（`#480` 起）。
> 来源：`doc/windows/策划窗口.txt`（`DELIVERY-LEAD-REDELIVER-20260926`）· `doc/windows/主程序窗口.txt` ·
> `reports/unity_ref/{...}.md` · `reports/planner_request_*.md` · `reports/planner_20260926_to_lead_tier_and_source.md` ·
> `reports/logic_completion_plan.md` · `darkest/**`（我方数据/代码，只读核对）
> 📎 引用一律写**完整相对路径 : 行号**（行号即 `read`/`grep` 工具打印的行号）。
> ⚠️ **未量** = 读数里没有这个数；**「读数里没有」** = 一手/参考侧在该问题上没有读数。

---

## 0. 一页速览

| 项 | 题目 | 读数里的选项标签 | 我的建议（**建议**，非裁定） | 是否缺信息 |
|---|---|---|---|---|
| ① | Σ 归一 vs A1 门槛 | (甲)/(乙)/(丙) | (甲) 门槛重设，同批登记 §39 | ⚠️ 部分：新门槛只给了示例带，未量完整读数 |
| ② | A6 三件（Effect 表/act-out 表/`queue` 语义） | (甲)/(乙)/(丙)（7 条去留）＋两问无数值标签 | 先 A6a 后 A6b；7 条取 (丙)；`queue` 立即/延迟必做、融合不实现 | 🔴 是：两套「三件」口径并存；65 vs 69 字段矛盾 |
| ③ | A9/A10 ＋ 总口径 | (甲)/(乙)/(丙) | 按用户新裁（一手优先）执行；A9 三块先只落数据；A10 采用 | ⚠️ 部分：改 99 处后的经济后果**未测** |
| ④ | `PartyNames` vs「本地化不采用」 | (甲)/(乙)/(丙) | (乙) 只抽 `PartyNames` 一个分类 | ⚠️ 小：20748 vs 21497 B、186 vs 187 两处矛盾未解释 |
| ⑤ | 失败惩罚的口径 | ①②③（无数值标签） | 先读一手 `exit_penalty`（126 B）再裁；暂留字段标 `placeholder` | 🔴 是：一手文件内容**未读**；`#443` 与本问先后待核 |
| ⑥ | `dungeon_types` 2 种还是 4 种 | (甲) 2 种 / (乙) 4 种 | **(乙) 保持 4 种** —— 一手就写全 4 种 | ⚠️ 部分：`#470` 已裁过一次，需确认是否覆盖 |
| ⑦ | 亮度方向 | (甲)/(乙)/(丙) | (丙) 两者并存（金币给暗、掉落给亮） | ⚠️ 部分：(乙)/(丙) 改动包未量 |
| ⑧ | 取整方向 + 下限 | (甲) 方向/(乙) 下限/(丙) 暴击 ＋ 106 的 (甲)/(乙) | 先 (丙)，(甲)(乙) 可同批拍到底 | ⚠️ 小：推进节奏两处建议不一致；846/846 vs 843/846 |
| ⑨ | `unlocks.json` 建筑解锁要不要留 | **读数里没有选项** | 先保留 + 标注「我方自创·参考无对应」 | 🔴 是：删改成本清单未量；`#423` 已部分裁过 |
| ⑩ | 自加机制 `missing_hp`（3 子项） | **读数里没有编号选项** | 保留并标注 + 3 子项一并入 §39 | 🔴 是：`coefficient`/`50%` 依据**未量** |
| ⑪ | `damage_axis: mental` 1 条存疑 | **读数里没有选项** | 维持现状；只把「为何走 `SpiritHit`」交策划 | ⚠️ 小：`SpiritHit` 消费落点 file:line 未量 |
| ⑫ | 「待接条件」列（7:17→10:14） | (甲)/(乙)（属裁定①，非本列） | 收下 10:14；补测试 + 逐个查真落点 | ⚠️ 小：`21` vs `24` 分母差额未交代 |
| ⑬ | `Target` 枚举混「轴/层」 | (甲) 拆维度 / (乙) 补 1 个取值 | (乙) 补 `ExpeditionMorale`；(甲) 留到 M2 定型 | ⚠️ 小：`21` vs `24` 分母差额未交代 |

**缺口最大（不足以判）的项：⑤ · ⑨ · ⑩ · ②**（详见 §15）。

---

## 1. 项 ① — Σ 归一 vs A1 门槛

- **问的是什么**：策划 `#475` 已裁「`Σ段倍率` 归一为 `1`」；主程序**原样落库后跑全量测试**，A1 平衡判据被打穿 ⇒ 请裁：(甲) 归一对、A1 门槛重设 ／(乙) 归一先挂着、与 P7 阶段 3 同批切换 ／(丙) 只归一我方的。（`reports/planner_request_sigma_vs_a1.md:1-5`；`doc/windows/主程序窗口.txt:8202-8210`）

- **已量到的事实**
  - 裁定①照做：30/30 落实；全库 **44/44 全部有 `dmg_pct`**（14 条有参考出处 `_dmg_pct_source` ＋ 30 条显式取 0，标 `value_source: none` + `origin: ours`）— `reports/planner_request_sigma_vs_a1.md:12-14`
  - 裁定②照做：**17 条被改** — `reports/planner_request_sigma_vs_a1.md:16-17`
  - 口径：Σ **只对 `flat` 段成立**；`missing_hp` 段（2 条）无 `multiplier`、走 `base + coefficient` ⇒ 不参与 Σ；`damage = null` 的 16 条亦无 Σ — `reports/planner_request_sigma_vs_a1.md:18-20`
  - 独立复核：参与 Σ 的 **26 条全部精确 = 1.0**（1 段 24 条 · 2 段 2 条且每段精确 **0.5**）· **`Σ ≠ 1` 为 0** · 另 **16 条 `damage=null` ＋ 2 条 `missing_hp`** 不参与 Σ — `reports/unity_ref/112_sigma_normalization_audit.md:22-40`、`:49-52`
  - **全量测试 846/846 绿 → 842/846（四红）** — `reports/planner_request_sigma_vs_a1.md:26-27`
  - 红：`M6Acceptance_WinRateBand_And_Rhythm`：「判据 A1 未过：**死门 0.48/场 > 0.4**」，全文 **胜率=100% 死门=0.48/场 阵亡=0.13/场 回合=4.36**（门槛 ≥85% / ≤0.4 / ≤0.1 / 4~6）— `reports/planner_request_sigma_vs_a1.md:30-34`
  - 红：`E2UI_Resources_RecomputableFromEventStream`：口粮「应为 **10**，实际 **11**」— `reports/planner_request_sigma_vs_a1.md:35-36`
  - 红：`V5_35_SingleUnitDamageDelta_ConvergesBothSides`：+10% 侧实测 **13.8%**（门槛 ±3pp）；−10% ⇒ 967（**−2.0%**）— `reports/planner_request_sigma_vs_a1.md:37-39`
  - **隔离实验**：只回退 `Σ`、保留那 30 条 `dmg_pct=0` ⇒ 重跑 ⇒ **三条全绿** ⇒ 三个红**完全由 Σ 归一引起** — `reports/planner_request_sigma_vs_a1.md:45-47`
  - 方向 = **敌人变强**：被改 17 条里 **5 条是敌方**且**全部往上调**：`ranged_intimidating_shot` **0.5→1.0（×2.0）**·`caster_mental_shock` 0.7→1.0（×1.43）·`caster_fear_whisper` 0.8→1.0（×1.25）·`ranged_precise_shot` 0.9→1.0（×1.11）·`melee_heavy_slash` 1.0（未变）— `reports/planner_request_sigma_vs_a1.md:55-61`
  - 我方也有一半往上调（`warrior_sweep 0.6→1.0` ×1.67 · `warrior_last_stand 2.0→1.0` ×0.5）⇒ 两侧都动、方向不一致 ⇒ 净效果敌人相对变强 ⇒ **死门 0.33 → 0.48** — `reports/planner_request_sigma_vs_a1.md:64-65`；`doc/windows/主程序窗口.txt:8195`
  - 被改 17 条里 5 条是「跨职业/自加」那批：`warrior_shield_bash 0.6`·`tank_shield_bash 0.7`·`tank_selfless_charge 1.8`·`medic_cross_slash 1.2`·`commissar_total_mobilization 1.5` ⇒ 它们的 `Σ` 正是「我方自加的平衡旋钮」— `reports/planner_request_sigma_vs_a1.md:71-73`
  - `#475` 的落库注记原文：原版没有这个概念 ⇒ 若它 ≠ 1 就不是对齐 ⇒ **这不是「允许永久偏离」，是中性化** — `darkest/data/skills.json:1800`
  - P7 的三前置里，**Σ 归一是前置③（模型级）**，未裁之前**不能换公式** — `reports/logic_completion_plan.md:118-122`

- **候选选项**（标签原样，`reports/planner_request_sigma_vs_a1.md:81-98`；`doc/windows/主程序窗口.txt:8203-8208`）
  - **(甲) Σ 归一是对的，A1 门槛要重设** —— 具体动作：门槛改成新读数附近的带（**如 死门 ≤0.55 / 阵亡 ≤0.15**）＋同批在 `§39` 登记「Σ 归一后的平衡待重定」
  - **(乙) Σ 归一先挂着**，等 P7 那 30 条 `dmg%` 的真值一起切 —— 数据里标 `_pending_switch`（= 架构说的「接线 ≠ 切换」）
  - **(丙) 只归一【我方】的 Σ，敌方保留** —— 主程序**不建议**：同一个字段两套口径 = 两处真值家族
  - 主程序倾向 **(甲)**（`reports/planner_request_sigma_vs_a1.md:97-98`）

- **一手/参考项目怎么说**：**参考** —— 公式是 `区间 × (1+dmg%)`（**一根轴**），**没有「Σ段倍率」这个概念**（`reports/unity_ref/PLAN_adoption.md:169-170`、`:244`）。**一手 E 盘：读数里没有**。

- **代价/风险**
  - (甲)：A1 门槛被改成新读数附近 ⇒ 必须**同批**登记 `§39`；主程序自问「门槛是否被掰弯去量」并自答**不是**（Σ 变中性是有依据的结构修正）— `reports/planner_request_sigma_vs_a1.md:84-86`
  - (乙)：数据与行为**暂时不一致**，要写清否则将来没人知道它为何没生效 — `reports/planner_request_sigma_vs_a1.md:91-92`
  - (丙)：同一字段两套口径（两处真值家族）— `reports/planner_request_sigma_vs_a1.md:94-95`
  - 数据面：17 条被改；全量 **4 红**；`§39` 需登记「Σ 归一后的平衡待重定」— `reports/planner_request_sigma_vs_a1.md:26-27`、`:84-85`

- **我的建议**：**建议取 (甲)** —— 理由：Σ 归一是原版口径（结构修正），而 A1 门槛（死门 ≤0.4）是**我方自定的旧基线**，它本来就建立在「Σ 当平衡旋钮」之上（`reports/planner_request_sigma_vs_a1.md:82-86`、`:97-98`）。🔴 **但这是建议，不是裁定。**

- **缺什么**：新门槛的**完整读数**未量（`≤0.55/≤0.15` 是主程序举的示例带）；且「M6 那三条新读数（增援 18 / 承伤 34%）与 Σ 的关系未逐项归因」— `reports/planner_request_sigma_vs_a1.md:108`。

---

## 2. 项 ② — A6 三件（Effect 表 / act-out 表 / `queue` 语义）

> 🔴 **先纠一处口径**：`doc/windows/策划窗口.txt:26` 把 A6 三件写成「**Effect 表 / act-out 表 / `queue` 语义**」；而 `doc/windows/主程序窗口.txt:8294-8299` 的「**A6 三件**」是「**① 我方那 7 条的去留 ② `chance` 的归一 ③ 形状收敛**」。**这是两套不同的「三件」**（前者是 reports 侧的分层，后者是设计问题）；本项把两套都列出。

- **问的是什么**
  - 设计侧三件（原话）：`doc/windows/主程序窗口.txt:8294-8299`「🔴 **请你裁三件**：① **我方那 7 条的去留**…② **`chance` 的归一**…③ **形状收敛**…」
  - 分层侧三件：A6a Effect 表 · A6b act-out 表 · `queue` 语义（`reports/unity_ref/PLAN_adoption.md:202-203`、`reports/unity_ref/100_event_queue.md:96-107`）

- **已量到的事实 —— (a) Effect 表（A6a）**
  - 源 `Mechanics/Effects.txt`：**1,819 行 · 176,745 B** ⇒ **952 条 effect** · `name` 去重 **952（无重复）** · **字段全集 65 种** — `reports/unity_ref/44_effects_extracted.md:21-23`
  - 🔴 **字段数矛盾**：`44_*.md:22` = **65 种**（并写「不是 68」）；`reports/unity_ref/PLAN_adoption.md:203` 与 `reports/unity_ref/93_effect_field_consumers.md:124` = **69 字段**（`90_*.md` 的 KV 正则 `[a-z_]+` 静默丢了 **4 个驼峰字段**：`dotPoison 51`·`dotBleed 44`·`keyStatus 27`·`monsterType 20` ⇒ **142 处**）— `reports/unity_ref/92_effects_downstream.md:52-55`
  - 高频字段：`target 952`·`on_hit 950`·`on_miss 947`·`chance 941`·`curio_result_type 739`·**`queue 447`**·`combat_stat_buff 353`·`duration 207`·`can_apply_on_death 160`·`apply_once 152`·`damage_low_multiply`/`damage_high_multiply` 各 **139** — `reports/unity_ref/44_effects_extracted.md:25-28`
  - 消费点**三口径**（45 的旧口径已被 46 更正）：**被数据点名 754** · **从未被数据点名 198** · **只在代码里 8** · **两边都没提 190**；自检 `754+198=952` ✓ · `198−8=190` ✓ — `reports/unity_ref/46_effects_two_criteria.md:27-31`
    - ⚠️ 旧读数 `reports/unity_ref/45_effects_consumption.md:32-33` 写「754 / **190**」而 `754+190=944 ≠ 952` ⇒ **46 自陈混了两个口径并更正** — `reports/unity_ref/46_effects_two_criteria.md:11-18`
  - 那 8 条「只在代码里」：`kill_performer_group_other`·`Push 1F`·`Push 3F`·`Pull 1A/1B/1C/1D/1E/1F`·`Pull 3B/3D/3F`·`Stress 3/4/5` ⇒ **采用时只搬数据不够，还得改代码** — `reports/unity_ref/46_effects_two_criteria.md:35-38`
  - 家族级：100% 命中 = `Antiq 38/38`·`Summon 29/29`·`GR 26/26`·`Hound 20/20`·`PD 20/20`·`Arb 16/16`·`Crusader 15/15`·`Stun 11/11`·`Bleed 10/10`·`Strong 10/10`；孤儿集中 = `beast 16/16`·`Pull 9/18`·`Blight 3/11`·`Weak 3/10`·`Heal 7/7` — `reports/unity_ref/46_effects_two_criteria.md:43-53`
  - 落库量结论：**从 952 降到 ~762**（`754 + 8`）— `reports/unity_ref/46_effects_two_criteria.md:71`
  - 引用链闭合：effect 引用的 **buff 名去重 73 个 ⇒ 拿 A1 的 1801 池查 ⇒ 未命中 0**（A1 第四次独立验证）— `reports/unity_ref/44_effects_extracted.md:49-52`
  - 数据笔误：`Effects.txt:504` 只有一个斜杠（前后 502/506 是完整 `//`）⇒ 读取器必须容错 — `reports/unity_ref/44_effects_extracted.md:36-43`
  - 消费点四层齐备：`Effect.LoadData` 扁平 token 流（键带点前缀、键值相邻）；**4/4 都有 `.case`**：`dotPoison`→`PoisonEffect`(`L338`)·`dotBleed`→`BleedEffect`(`L341`)·`keyStatus`→`TargetStatus`(`L451`，4 种)·`monsterType`→`TargetMonsterType`(`L474`，4 种；而 `.kill_enemy_types` 有 **5** 种，多 `corpse`) — `reports/unity_ref/93_effect_field_consumers.md:12-36`、`:43-46`
  - DoT 结算：`PoisonEffect`/`BleedEffect` **各 69 行**；**概率门** `chance ÷100 − 目标抗性 + 英雄加成` · **`Clamp(0, 0.95f)`（永不满 100%）** · **`duration` 缺省 3** · 实例可叠加（`CurrentTickDamage=Sum` / `CombinedDamage=Sum(TicksLeft×TickDamage)` / `ExpirationTime=Max`）— `reports/unity_ref/94_dot_settlement.md:17-27`、`:45-47`
  - DoT 4 个结算点：`2817`/`2835`（英雄自己回合）· `3448`/`3470`（怪物自己回合，**在 `if (!fromBonusTurn)` 区内** ⇒ 先手回合不结算）· `2729`/`2748`（`idleUnit`，**唯一带 `TakeDamage(CeilToInt(×1.5f))`**）· `4683`/`4721`（行走中）— `reports/unity_ref/94_dot_settlement.md:53-57`、`:66-74`

- **已量到的事实 —— (b) act-out 表（A6b）**
  - 参考 **12 条 trait**（**7 折磨 + 5 美德**）· 两张表 **回合开始 168 项（12×14）· 反应 180 项（12×15）** · id 全集 **14 / 15** · `overstress_type`/`curio_tag`/`curio_tag_chance`/`keep_loot` **各 12/12** · **35/35 buff 引用在 A1 池里全部可解析** — `reports/planner_request_a6_traits.md:13-16`
  - 判法（可计算）：比 `buff_ids` 的（抗性档 + 属性集）签名 —— 参考 7 折磨**抗性档全 −15**（`fearful`/`paranoid` **签名完全相同**；`abusive` 是唯一「伤害变高」`DMGL+20/DMGH+20`）· 5 美德**抗性档全 +20**，区分全在各自独有的 `virtue<Name>BuffN` — `reports/planner_request_a6_traits.md:34-41`
  - **逐条判定：我方现有 7 条【一条都不是】参考 12 条的对等物**（连**同名**的 `selfish` 语义都不同）— `reports/planner_request_a6_traits.md:46-54`
  - 真缺口 = **6 折磨 + 4 美德 + 29 种战斗 act-out**（三块都要新建）— `reports/unity_ref/28_actout_layer_gap.md:64`
  - 我方 `ActOut`（36 处置）实际是**探索层**（`ExplorationActOut.RollCurioRefuse`，`scripts/gameplay/sim/run/ExpeditionFlow.RoomInteractions.cs:173`）⇒ **名字撞车、层次不同**，我方**整层缺失** 29 种战斗 act-out — `reports/unity_ref/28_actout_layer_gap.md:42-49`
  - 我方 hooks 只有 **4 条**：`affliction_fear`(`before_use_skill`)·`affliction_selfish`(`before_heal`)·`affliction_uncontrolled`(`before_attack`)·`virtue_inspired`(`round_start`)；**只有 `round_start` 与参考时机相同** — `reports/unity_ref/28_actout_layer_gap.md:70-80`
  - `chance` 是**整数权重**不是百分比：7 个折磨的 `nothing` 权重 = **4/6/6/6/8/8/10**（各不相同）· 5 个美德一律 **3** — `reports/planner_request_a6_traits.md:78-79`
  - 29 种分档（**读数内部有矛盾**）：`31_*.md` = **A 27 可照抄 + B 2 要自己实现**（`block_effect`·`block_combat_retreat`）— `reports/unity_ref/31_actout_turn_consumption.md:45-49`；而 `32_*.md` = **A 24 + B 2 + C 2 未启用（`retreat_from_combat`·`attack_friendly`）+ D 1（`nothing` 靠「无 case」实现）** — `reports/unity_ref/32_actout_switch_detail.md:104-107`（32 自称修正 31）
  - ⚠️ 另一处矛盾：`31_*.md:11-14` 判「**14/14 全部有消费，且是真分支**」；`32_*.md:12-17` 判「**只有 11 个 case** · 3 个无 case（`Nothing`/`RetreatFromCombat`/`AttackFriendly`）」— `reports/unity_ref/32_actout_switch_detail.md:12-17`
  - 必须一并搬的**代码硬编码守卫**：`masochistic` 在 1 号位不换位（`RaidSceneManager.cs:3012`）· `HealSelf` 满血不做（`:3050`）· 单人跳过（`Party.Units.Count < 2`，出现在 5 个 case 里）— `reports/unity_ref/31_actout_turn_consumption.md:59-62`
  - `number_value` 是**分数**：`attack_self 0.1`（3 条）·`heal_self 0.1`（1 条）·其余 10 种 **0.0** — `reports/unity_ref/43_actout_data_semantics.md:12-18`
  - `string_value` **6/6 全部命中 `Effects.txt`**（`BarkStress`·`FocusedAllyBuff`·`HealStressVirtued`·`HealStressVirtuedParty`·`Mark Self`·`PowerfulPartyBuff`）⇒ act-out **依赖 Effect 表**，只搬 act-out 会得到**空壳** — `reports/unity_ref/43_actout_data_semantics.md:31-42`、`:73-84`
  - 5 个美德 `block_effect chance = 1` · 4 个折磨 `block_combat_retreat chance = 0.33`（**数据有、代码没读** ⇒ 红线 21）— `reports/unity_ref/30_actout_consumption.md:73-80`

- **已量到的事实 —— (c) `queue` 语义**
  - 入队点 `Mechanics/Skills/SubEffect.cs:6`（41 行基类）：`queue == false` ⇒ **`ApplyInstant`（立即）**；`true` ⇒ `target.EventQueue.Add(...)`；🔴 **缺省也入队（延迟）** — `reports/unity_ref/100_event_queue.md:33-41`、`:44-48`
  - `EventQueue` ≠ `UnitEventQueue`：前者 = `FormationUnit.EventQueue` = `List<EffectEvent>`（单位拥有）；后者 = `RaidSceneManager.cs:104` = `List<FormationUnit>(8)`（控制器拥有）— `reports/unity_ref/100_event_queue.md:15-22`
  - `EffectEvent.Execute()` 有**融合（Fuse）**：`StackParameter > 0` ⇒ `ApplyFused`，否则 `ApplyQueued`；基类 `Fusable` 默认 **false**、`Fuse` 默认返回 **0** — `reports/unity_ref/100_event_queue.md:59-75`
  - 入队点共 **7 处 / 4 个文件**（`SubEffect.cs:13·16`·`ClearGuardEffect.cs:14`·`CombatStatBuffEffect.cs:50·53`·`GuardEffect.cs:20·23`）— `reports/unity_ref/100_event_queue.md:84-87`
  - 🔴 **采用 A6a 时必须一并实现 `queue` 的立即/延迟**，否则 **447 处 `queue` 语义全错** — `reports/unity_ref/100_event_queue.md:107`
  - 融合**当前不会被触发**：952 条里 `stress` 出现 >1 次的 **0** 条 · `healstress` >1 次的 **0** 条；分布 `(0,0) 849`·`(0,1) 52`·`(1,0) 51` — `reports/unity_ref/102_fuse_not_triggered.md:12-17`
  - 语法层保证：`Effects.txt` **无非 `effect:` 顶层键**；`Effect.cs` 的 `SubEffects.Add` **35 处全在行内** — `reports/unity_ref/102_fuse_not_triggered.md:26-30`
  - 含 ≥2 个「动作类字段」的 effect 只有 **2 条**（`HolyFountainEffects`·`HolyFountainEffectsSuper`，都是 `healstress + heal`）⇒ 不同种 ⇒ **不触发融合** — `reports/unity_ref/102_fuse_not_triggered.md:38-45`
  - 结论：**融合不用实现，但要记下它存在**；`queue` 的立即/延迟属**必做** — `reports/unity_ref/102_fuse_not_triggered.md:50-57`、`:61-70`

- **候选选项**
  - **(甲) 换成参考的 12 条**（我方那 7 条**删掉**）⇒ 与「采用参考」一致，但**丢掉我方自己的机制** — `reports/planner_request_a6_traits.md:72`
  - **(乙) 并存**（参考 12 + 我方 7）⇒ ⚠️ 会出现**两套「恐惧/自私」**（名字撞车）— `reports/planner_request_a6_traits.md:73`
  - **(丙) 参考 12 条为准，我方 7 条里【有独立价值的】合并进去**（如 `refuse_skill` 这个行为）— `reports/planner_request_a6_traits.md:74`
  - `chance` 归一：**按条 `chance / Σchance`**（保序、可复算）vs **「固定频率」**（如「每 N 回合作妖一次」——那是另一套）— `reports/planner_request_a6_traits.md:77-81`
  - 形状收敛：**收敛到参考的形状**（`buff_ids[] + 两张表`）vs 保留我方 `modifiers[] + hooks[] + ends_at` — `reports/planner_request_a6_traits.md:83-86`
  - Effect 表落库量：**~762 条**（754 + 8）；**整族无引用约 60~70 条不抄** — `reports/unity_ref/46_effects_two_criteria.md:68-73`
  - 推进方式：**先只落数据、不接消费点**，等 M3/M4 需求驱动再接（与 A1 的「趟级修正载体」合并成一条裁定）— `reports/planner_request_a6_traits.md:92-96`
  - 主程序倾向：**①②取 (甲) 或 (丙)**（⚠️ (乙) 不建议）；**③收敛到参考形状** — `reports/planner_request_a6_traits.md:75`、`:85`

- **一手/参考项目怎么说**：**参考** = `Mechanics/Effects.txt`（1,819 行 / 176,745 B）· `JsonTraits` 12 条 · 两张 act-out 表 · `SubEffect.cs:6` 的 queue 三分支（`reports/unity_ref/44_effects_extracted.md:21`；`reports/unity_ref/100_event_queue.md:31-41`）。**一手 E 盘：读数里没有**（A6/A6a/act-out/queue 这条线上没有一手读数）。

- **代价/风险**
  - (乙) 并存 ⇒ 同一概念两处真值家族（`reports/planner_request_a6_traits.md:73`）；(甲) ⇒ 丢掉我方自己的机制（`:72`）
  - 形状收敛是**结构改动**（按纪律 AY **可做**）— `reports/planner_request_a6_traits.md:86`
  - 8 条「只在代码里」的 effect ⇒ **搬数据之外还得改代码** — `reports/unity_ref/46_effects_two_criteria.md:37-38`
  - 不实现 `queue` 延迟系统 ⇒ **447 处语义全错** — `reports/unity_ref/100_event_queue.md:107`
  - 给 act-out 的 switch 加 `default` ⇒ **悄悄改掉 `nothing` 的语义** — `reports/unity_ref/32_actout_switch_detail.md:109-111`
  - act-out 需要**一层新结构**（我方没有「回合开始行为」这一层）⇒ 按「需求驱动，不预留结构」，要有真实调用方才造 — `reports/planner_request_a6_traits.md:92-94`
  - 联机侧：**取单机那份**（`doc/windows/主程序窗口.txt:532-549`）；照抄联机会把**重复的 `RevertDeathsDoor()`** 一起抄进去（`reports/unity_ref/36_actout_multiplayer.md:97-101`）
  - 🔴 **未测**：全部读数「只抽不落库」⇒ 接上后行为**完全未测**（`reports/unity_ref/46_effects_two_criteria.md:87`；`reports/unity_ref/100_event_queue.md:124`）

- **我的建议**：**建议** 先落 A6a（Effect 表 ~762 + 4 个驼峰字段 + `queue` 立即/延迟系统），再落 A6b（act-out）；我方 7 条取 **(丙)**；`chance` 按条归一；`queue` 立即/延迟必做、**融合留着但不实现**（数据不触发）。🔴 理由：act-out 的 `string_value` 6/6 指向 Effect，先落 act-out 只会得到空壳（`reports/unity_ref/43_actout_data_semantics.md:73-84`）。**但这是建议，不是裁定。**

---

## 3. 项 ③ — A9/A10 ＋ 总口径

- **问的是什么**
  - 总口径（原话）：`reports/planner_request_a9_a10_a11.md:32-53`「请裁一条**总口径**…**(甲) 冲突时一律取参考**…**(乙) 冲突时取一手（原版），参考只当『缺数据时的补充』**…**(丙) 分域裁**」
  - A9 另一半：我方缺的**三块**（`side_effects` / 3 个 gate 字段 / 各建筑特有升级数组）**要不要做** — `reports/planner_request_a9_a10_a11.md:56-72`
  - A10：`Items.bytes` 57 条 + 补给 3 张清单（含 7 职业本命补给）**要不要采用** — `reports/planner_request_a9_a10_a11.md:86-97`；`doc/windows/主程序窗口.txt:8394`

- **已量到的事实**
  - 三方判定（回 E 盘原文件逐级重读）：**A9 建筑升级 99 个等级 ⇒ 我方 == E 盘 99/99** · **参考 == E 盘 0/99** · 三方都不同 0/99 · 🔴 **每栋建筑都有差异**（`完全一致的建筑：{}`）— `reports/planner_request_a9_a10_a11.md:14-18`
  - 满级全清：`crest` E 盘 **2103** vs 参考 **2161（+58）**·`deed` **421** vs **482（+61）**·`bust` **516** vs **534**·`portrait` **241** vs **277** — `reports/planner_request_a9_a10_a11.md:19-20`
  - A3（英雄 5 阶）另一独立证据：一手 `crit` **5/6/7/8/9** vs 参考 **2.5/3/3.5/4/4.5** — `reports/planner_request_a9_a10_a11.md:22`
  - 参考件自己的注记写着 *"numbers must be blessed by the planner"* — `reports/planner_request_a9_a10_a11.md:25`
  - 三方总账：**建筑升级 一手==我方 99/99 · 一手==参考 0/99**；**英雄升级 一手==我方 645/645 · 一手==参考 345/645**；**合计 一手==我方 744/744（100%）· 参考改了 399 处** — `doc/windows/主程序窗口.txt:9137-9139`
  - 一眼看谁改的：`abbey` 冥想 a 级 —— 一手 `{gold:0, bust:4, crest:5}` ＝ 我方；参考 `crest:4` — `doc/windows/主程序窗口.txt:9153-9157`
  - A9 缺三块：**A. `side_effects`**：**6 个活动**有（abbey 3 · tavern 3）· 形状 `{chance, results:[{type, chance, data[]}]}` · `type` 全集 7 种（`activity_lock`·`go_missing`·`add_quirk`·`apply_buff`·`change_currency`·`add_trinket`·`remove_trinket`）· 各活动 `chance` abbey `0.4/0.35/0.3`、tavern `0.4/0.35/0.3` — `reports/planner_request_a9_a10_a11.md:59-64`
  - A9 缺三块：**B. 3 个建筑级 gate 字段**（`on_start_town_visit_priority`/`number_of_quests_finished`/`highest_dungeon_level`，8/8 全有）⚠️ 而参考**解析了但从不读** — `reports/planner_request_a9_a10_a11.md:65-66`；`reports/unity_ref/PLAN_adoption.md:422-424`
  - A9 缺三块：**C. 各建筑特有的升级数组**（参考**扁平数组** vs 我方「树+等级」）— `reports/planner_request_a9_a10_a11.md:67`
  - A10 形态判定：读前 64 字节 = `b'inventory_item:\t.type "provision"\t\t.id "'` ⇒ **DD1 文本，不是二进制** ⇒ **57 条** · 每行 5 字段（`type`/`id`/`base_stack_limit`/`purchase_gold_value`/`sell_gold_value`）· 类型 `journal_page 22 · gem 10 · supply 9 · quest_item 9 · heirloom 5 · provision 1 · gold 1` — `doc/windows/主程序窗口.txt:8383-8388`
  - 🔴 我方 `provisions.json` / `items.json` **都不存在**（整层缺失）— `reports/planner_request_a9_a10_a11.md:89`
  - `raid_starting_hero_class_item_lists`：**7 个职业各有本命补给**（`houndmaster→dog_treats`·`plague_doctor→antivenom`·`grave_robber→shovel`·`arbalest→bandage`·`crusader→holy_water`·`leper→medicinal_herbs`·`jester→medicinal_herbs`）⇒ **设计意图，不是随机** — `reports/planner_request_a9_a10_a11.md:90-94`
  - 起手补给**除 `firewood` 外全为 0**（档1→4 = `0/1/2/4`）⇒ 「任务越长柴火越多」— `reports/planner_request_a9_a10_a11.md:95`
  - `gem` 10 条是**战利品变现**通道（`trapezohedron 2500` … `citrine/jade 250`），**全部 `purchase_gold_value = 0`（只能卖不能买）** — `reports/planner_request_a9_a10_a11.md:96-97`
  - `heirloom` 5 条堆叠上限：`portrait 3 · bust 6 · crest 12 · deed 6 · urn 1` ⇒ **与兑换价值不成比例**（`crest` 堆叠最高 12 但价值最低 2）⇒ 两条独立的轴，不能互推 — `doc/windows/主程序窗口.txt:8398-8400`
  - 引用完整性：三张清单引用的 id 去重 **8** 个 ⇒ **不在 `Items.bytes` 里的 0** — `doc/windows/主程序窗口.txt:8401`
  - **A11 已经 12/12 完全一致 ⇒ 本项【无需改动】**（参考 12 条逐条相同，E 盘一手也逐条相同）⇒ **第一块三方完全一致的数据** — `reports/planner_request_a9_a10_a11.md:78-80`
  - A11 顺带复算：相对价值 `portrait 6 : bust 3 : deed 3 : crest 2` **为真**；但「**所有**兑换都损失 50%」**过强**（`bust↔deed` 往返损失 **55.6%**）— `reports/planner_request_a9_a10_a11.md:81-83`
  - A11 形状差一处：参考 `{markets:[{id, exchange_rates}]}` **包一层**，我方与 E 盘**平铺** ⇒ **是「结构」不是「数值」** ⇒ 归架构 — `doc/windows/主程序窗口.txt:8376-8377`

- **候选选项**
  - **(甲) 冲突时一律取参考**（严格按指令）⇒ 后果：A9 要改 **99 处**成本 · A3 已经按这个改了（**39 处**）— `reports/planner_request_a9_a10_a11.md:38-40`
  - **(乙) 冲突时取一手（原版），参考只当「缺数据时的补充」** ⇒ 后果：A9 **不用改**（我方已 == 一手）· A3 那 39 处**要改回** — `reports/planner_request_a9_a10_a11.md:42-44`
  - **(丙) 分域裁**：数值性（成本/血量）取一手 · 结构性（字段/机制）取参考 ⇒ ⚠️ 容易变成两处真值 — `reports/planner_request_a9_a10_a11.md:46-47`
  - A9 三块 & A10：**读数里没有选项标签**，只有「要不要做」（`reports/planner_request_a9_a10_a11.md:69-72`）

- **一手/参考项目怎么说**：**两方都有**（这是本项的特点）—— 一手 = `744/744` 与我方逐值相同；参考 = 改了 **399 处**（`doc/windows/主程序窗口.txt:9137-9139`）。参考自己写着「数值需策划确认」— `doc/windows/主程序窗口.txt:9167`。

- 🔴 **矛盾（必修）**：(甲)/(乙)/(丙) 三个选项**建立在「数值一律采用参考」这条前提上**（`reports/planner_request_a9_a10_a11.md:35-38`），而用户 2026-09-26 已裁定：**「选1,如果E盘读不到数值就用参考」** ⇒ **一手 E 盘 = 第一来源 · 参考 = 兜底**（`reports/planner_20260926_to_lead_tier_and_source.md:29-30`；`doc/windows/主程序窗口.txt:9227-9228`）；并明确 **`#473`「数值一律采用参考」作废 / 已取代**（`doc/windows/主程序窗口.txt:9354-9355`、`:9357-9358`）。⇒ **本项的三选一必须按新裁定重述，否则会选到已废前提。**

- **代价/风险**
  - (甲)：A9 改 99 处（且参考每级都不同）；A3 已完成 ⇒ 不返工 — `reports/planner_request_a9_a10_a11.md:39`
  - (乙)：A9 不用改；**A3 那 39 处要改回** ⇒ 返工 — `reports/planner_request_a9_a10_a11.md:43`
  - A9 缺三块：**A 需要一层新结构**（我方城镇层没有「活动副作用」）— `reports/planner_request_a9_a10_a11.md:69`
  - A10：我方 `provisions.json`/`items.json` **整层不存在** ⇒ 采用 = 新建整层 — `reports/planner_request_a9_a10_a11.md:89`
  - 🔴 **未测**：**A9/A10 没有落任何数据** ⇒ 「改这 99 个数后经济会怎样」**完全未测** — `reports/planner_request_a9_a10_a11.md:117`
  - 不能验：参考**为什么被改 / 谁改的 / 是否有意** — `reports/planner_request_a9_a10_a11.md:118`
  - 不能验：`quest_item` 9 条 · `journal_page` 22 条的**用途未查** — `reports/planner_request_a9_a10_a11.md:119`

- **我的建议**：**建议** —— ① 总口径**照用户新裁定重述**（一手优先、E 盘读不到才用参考），并在契约/`dd1_conformance` 里把 `#473` 标「已取代」；② A9 三块**先只落数据、不接消费点**（`side_effects` 那层按「需求驱动，不预留结构」先不造）；③ A10 **采用 57 条 + 3 张清单**（含 7 职业本命补给）。🔴 **但这是建议，不是裁定。**

---

## 4. 项 ④ — `PartyNames` 与「本地化不采用」冲突

- **问的是什么**：`doc/windows/主程序窗口.txt:8557-8567`「🔴 **新暴露一条【口径冲突】，请你裁**…**要队伍名就得碰那份「不采用」的本地化** ⇒ 请你裁：(甲)/(乙)/(丙)」

- **已量到的事实**
  - `PartyNames.json`：顶层 1 键 `party_names` · **186 条** — `reports/unity_ref/15_quests_loot_narration_from_ref.md:16`、`:101`
  - 键**只有** `{id, required_hero_class}` ⇒ 🔴 **文件里【没有名字字符串】** — `reports/unity_ref/15_quests_loot_narration_from_ref.md:102`
  - 名字来自**本地化分类 `"PartyNames"`** — `reports/unity_ref/15_quests_loot_narration_from_ref.md:103`
  - 类别列表出处 `LocalizationManager.cs:36`（与 `Monsters`/`Names`/`Quirks`/`Kickstarter`/`TownEvents`/`Journal` 并列）— `reports/unity_ref/04b_quests_loot_narration.md:582`
  - ⇒ 这 186 条是「**队伍组成的约束**」，不是「名字」⇒ **文件名有误导性** — `reports/unity_ref/15_quests_loot_narration_from_ref.md:105`
  - `id` 是 `"0"`~`"185"` 纯数字字符串 ⇒ 是索引，不是名字 — `reports/unity_ref/05_buffs_ai_quirks_trinkets.md:705`
  - 注入点：`DarkestDatabase.cs:1401-1413` `LoadPartyNames()`（路径常量 `Data/PartyNames` @ `DarkestDatabase.cs:35`）— `reports/unity_ref/04b_quests_loot_narration.md:567`
  - 全仓**唯一**读取处 = `PartyCompositionPanel.cs:16`，且用 `List.Find`（**首个匹配**）⇒ 未被匹配到的条目**永远不会被用到** — `reports/unity_ref/04b_quests_loot_narration.md:587`、`:592`、`:862`
  - 另一半读数：`Localization\PartyNames.xml` = **104,475 B** · **1496** `<entry>` · **187** 唯一 key · **8** 语言（english/french/german/spanish/brazilian/russian/polish/czech）— `reports/unity_ref/01_data_inventory.md:3957`
  - 本地化总账：**18** 个 `.xml` · **12,373,367 B** · **100,875** 条 `<entry>` · english 单语 **12,617** — `reports/unity_ref/01_data_inventory.md:24`、`:40`
  - 「**12.37 MB = 全部字节的 80%**」⇒ 🔴 **明确列入「不采用」** — `reports/unity_ref/PLAN_adoption.md:372-373`；`reports/unity_ref/63_dlc_all_categories.md:15`
  - 我方现状：`PartyNames.json` **我方无对应 / 整表缺失**；我方**无**「队伍职业组合 → 队名」表 — `reports/unity_ref/05_buffs_ai_quirks_trinkets.md:736`
  - ⚠️ **矛盾（两处，读数未解释）**：① 文件大小 `20,748 B`（`reports/unity_ref/15_quests_loot_narration_from_ref.md:16`）vs `21497 B`（`reports/unity_ref/01_data_inventory.md:1307`）② 条数 **186**（JSON，`reports/unity_ref/01_data_inventory.md:4090`）vs **187** key（XML，`reports/unity_ref/01_data_inventory.md:3957`）

- **候选选项**（标签原样，`doc/windows/主程序窗口.txt:8564-8566`）
  - **(甲) 不采用队伍名**（`PartyNames` 只当「组成约束」，名字我们自己起）
  - **(乙) 只抽 `PartyNames` 那一个分类**（不整份采用本地化）
  - **(丙) 整份本地化纳入采用范围**（🔴 会打破 §9.1 的结论）
  - 主程序倾向 **(乙)** — `doc/windows/主程序窗口.txt:8567`

- **一手/参考项目怎么说**：**参考** —— 名字来自本地化分类 `"PartyNames"`（`reports/unity_ref/15_quests_loot_narration_from_ref.md:103`；`LocalizationManager.cs:36` @ `reports/unity_ref/04b_quests_loot_narration.md:582`）。**一手 E 盘：读数里没有**。

- **代价/风险**
  - (丙) 打破 `§9.1`（把占字节 80% 的文本纳入范围）— `reports/unity_ref/PLAN_adoption.md:372-373`
  - (乙) 与「80% 是文本、与逻辑无关」的结论**不冲突** — `doc/windows/主程序窗口.txt:8567`
  - 三个选项都要**新建我方整表**（现整表缺失）— `reports/unity_ref/05_buffs_ai_quirks_trinkets.md:736`
  - 消费面只有 **1 处** UI 面板 ⇒ 加表后消费点仍只 1 处 — `reports/unity_ref/04b_quests_loot_narration.md:862`
  - `Find` 首个匹配 ⇒ 186 条中未匹配条目**永远用不到** — `reports/unity_ref/04b_quests_loot_narration.md:592`
  - **未量**：187 key vs 186 条的**逐条差集**（差在哪 1 条）；**未测**：没有落任何任务数据 ⇒ 接上后流程未测 — `reports/unity_ref/15_quests_loot_narration_from_ref.md:115`

- **我的建议**：**建议取 (乙)**（只抽 `PartyNames` 一个分类）—— 理由：消费点只有 1 处、`PartyNames.xml` 只 187 key，且能与 `§9.1`「80% 是文本、与逻辑无关」并存。🔴 裁定前建议先补两处矛盾（`20748 vs 21497 B`、`187 vs 186`）。

---

## 5. 项 ⑤ — 失败惩罚的口径

- **问的是什么**：`doc/windows/主程序窗口.txt:8751-8753`「🔴 **「失败惩罚」这块要【我们自己设计】** —— 参考侧只有数据、没有实现 ⇒ 请你给口径：**① 失败惩罚要不要做 ② 做什么（掉 resolve XP？扣建筑？关任务？）③ 若不做 ⇒ 那 4 个字段是删掉还是留着当占位（标 `placeholder`）**」

- **已量到的事实**
  - `plot_quests` **30** 条 · 19 个字段 · 其中 **7 个是「失败/忽略」路径**（`..._on_failure` ×3 · `..._on_ignore` ×1 · `can_retreat`/`retreat_*` ×3）— `reports/unity_ref/15_quests_loot_narration_from_ref.md:34`、`:42-44`；`doc/windows/主程序窗口.txt:8448-8450`
  - 逐字段非默认值条数：`can_retreat` 非默认 **1**（`plot_darkest_dungeon_4`）· `retreat_always_from_raid` 恒 `false`（没用到）· `retreat_party_kill_count` **5** 条为 `1` · `roster_buff_on_failure_minimum_party_resolve_level` **5** 条为 `5` · `roster_buffs_to_apply_on_failure` **4** 条非空 · `upgrade_tags_to_remove_on_failure` 恒 `[]`（没用到）· `upgrade_tags_to_remove_on_ignore` **1** 条非空 — `reports/unity_ref/19_plot_quest_fields.md:77-83`
  - ⇒ **7 个里 4 个在用 · 3 个恒默认** ⇒ 🔴 在用的那 4 个**全集中在 `darkestdungeon` 主线 + 城镇入侵** ⇒ 「失败惩罚是**主线专属机制**，普通 boss 任务没有」— `reports/unity_ref/19_plot_quest_fields.md:84-86`
  - 消费点实测（读参考 **485** 个 `.cs`）：**13 个字段 ⇒ 有消费点 4 · 无消费点 9** — `reports/unity_ref/20_quest_field_consumption.md:25`
  - 有消费点的 4 个（含文件:行号）：`IsScoutingEnabled` **2 处**（`RaidSceneManager.cs:808` / `:2220`）· `CanRetreat` **2 处**（`RaidQuestPanel.cs:30` / `:84`，隐藏/显示撤退按钮）· `CompletionDungeonXp` **1 处**（`ResultItemWindow.cs:38`）· `PlotDependency` **1 处**（`DungeonProgress.cs:33`）— `reports/unity_ref/20_quest_field_consumption.md:31-34`
  - 无消费点的 9 个（逐名）：`HasStatueContents`·`IsStressClearedOnCompletion`·`IsSurpriseEnabled`·`RetreatKillCount`·`AlwaysRetreatFromRaid`·`RosterBuffsOnFailure`·`UpgradeTagsToRemoveOnIgnore`·`SuggestedTrinkets`·`UpgradeTagsToRemoveOnFailure` ⇒ 每个都只有【声明 + 拷贝】— `reports/unity_ref/20_quest_field_consumption.md:40-44`
  - 两种成因必须分开：① **数据侧恒默认 3 个**（`retreat_always_from_raid` 30/30 false · `upgrade_tags_to_remove_on_failure` 30/30 [] · `additional_provisions` 30/30 空壳）② 🔴 **数据有非默认值但没人读 6 个** — `reports/unity_ref/20_quest_field_consumption.md:52-65`
  - ②的逐条：`has_statue_contents` **29 true/1 false**·`is_surprise_enabled` **25 true/5 false**·`is_roster_stress_cleared_on_completion` **4 true**·`retreat_party_kill_count` **5 条为 1**·`roster_buffs_to_apply_on_failure` **4 条非空**·`upgrade_tags_to_remove_on_ignore` **1 条非空**·`suggested_trinkets` **1 条非空** — `reports/unity_ref/20_quest_field_consumption.md:59-65`
  - 证据强度：`has_statue_contents` **29 条是 `true`**（默认本该 false）⇒ **有人特意填了** — `reports/unity_ref/20_quest_field_consumption.md:67-68`
  - 交叉结论：**参考项目【设计了失败惩罚，但没有实现】** — `reports/unity_ref/20_quest_field_consumption.md:80`
  - 点名到 buff 的一条：`roster_buffs_to_apply_on_failure = ["darkest_dungeon_failure_roster_resolve_xp"]` ⇒ 🔴 **没人读** — `reports/unity_ref/20_quest_field_consumption.md:81-82`
  - 另两条：`retreat_party_kill_count = 1`（5 条）⇒ 没人读；`upgrade_tags_to_remove_on_ignore = [{building, 3}]`（1 条）⇒ 没人读 — `reports/unity_ref/20_quest_field_consumption.md:83-84`
  - 参考**唯一接线**的失败效果在障碍/陷阱：`fail_effects` 是障碍 5 字段之一（`name`,`fail_effects`,`health`,`torchlight`,`ancestor_talk`），**没有 `success_effects`**；`fail_effects` 里的字符串（如 `"Stress 2"`）是效果 DSL，通过 `Effects[...]` 查表 — `reports/unity_ref/04b_quests_loot_narration.md:602`、`:639-646`
  - 参考**没有**静态 JSON 定义的「任务失败惩罚」完整表：只有顶层标量 `stress_damage: 20`（`JsonQuests.json:2`）＋ `plot_quests[].roster_buffs_to_apply_on_failure`（只 4 条非空）— `reports/unity_ref/04b_quests_loot_narration.md:949`、`:220-221`
  - 🔴 **一手 E 盘有对应文件**：原版真数据在 `campaign/quest/`（**5 表 154 KB**）：`generation` 63848 B · `plot_quests` 65894 B · `types` 23447 B · `number.quest.generation` 298 B · **`exit_penalty` 126 B** · `restriction` 410 B — `doc/windows/主程序窗口.txt:5929-5931`；`reports/state_backfill_418_479_sources.md:168`、`:174`
  - 🔴 **一手 `exit_penalty` 的【内容】未读/未量**（读数只给了文件名与 126 B）— `doc/windows/主程序窗口.txt:5931`
  - ⚠️ **矛盾/待核**：既有裁定 `#443`（2026-09-18 · 用户裁）已把「**退出惩罚**」列入「Quest = 全选」—— `doc/modules/dd1_baseline.md:1441`；而本件又问「① 失败惩罚**要不要做**」— `doc/windows/主程序窗口.txt:8752`。**两处口径的先后关系读数里没说清。**
  - 边界：无消费点的结论是**静态扫描**，若用反射/字符串读则扫不到 ⇒ 记「静态未检出」而不是「绝对没有」— `reports/unity_ref/20_quest_field_consumption.md:99-101`

- **候选选项**：**读数里没有甲/乙/丙标签**，只有三问（①②③）：① 要不要做 ② 做什么（**掉 resolve XP？扣建筑？关任务？**）③ 不做则那 4 个字段**删掉**还是**留着当占位（标 `placeholder`）** — `doc/windows/主程序窗口.txt:8752-8753`

- **一手/参考项目怎么说**
  - **参考**：「设计了失败惩罚，但没有实现」⇒ **答不出「该怎么做」** ⇒ **不能照抄** ⇒ 必须我们自己设计 — `reports/unity_ref/20_quest_field_consumption.md:80-89`
  - **一手 E 盘**：**有** `campaign/quest/exit_penalty`（**126 B**）这个文件，但**内容读数里没有** ⇒ **未量** — `doc/windows/主程序窗口.txt:5931`

- **代价/风险**
  - 参考侧没有实现可对齐 ⇒ 若「做」，无法从参考取口径 — `reports/unity_ref/20_quest_field_consumption.md:86-89`
  - 若「不做」⇒ 需决定那 4 个字段**删**还是**留占位**（`doc/windows/主程序窗口.txt:8753`）；涉及字段 = `retreat_party_kill_count`（5 条为 1）· `roster_buff_on_failure_minimum_party_resolve_level`（5 条为 5）· `roster_buffs_to_apply_on_failure`（4 条非空）— `reports/unity_ref/19_plot_quest_fields.md:77-83`
  - 我方**没有 Quest 层**（M14 待做）⇒ 「做什么」目前**没有落点** — `reports/logic_completion_plan.md:232`
  - 静态扫描的假阴性风险（反射/字符串读）— `reports/unity_ref/20_quest_field_consumption.md:99-101`
  - **未测**：没有落任何任务数据 ⇒ 接上 30 条任务后流程**完全未测** — `reports/unity_ref/20_quest_field_consumption.md:103`

- **我的建议**：**建议先补一条读数再裁** —— 读一手 `campaign/quest/exit_penalty`（126 B）的内容；在读出来之前，「要不要做/做什么」的读数**全部来自参考侧**（而参考没实现）。③ 的「删/留占位」建议：**先留 + 标 `placeholder`**（与我方尚无 Quest 层一致）。🔴 **但这是建议，不是裁定。**

---

## 6. 项 ⑥ — `dungeon_types` 2 种还是 4 种

- **问的是什么**：`doc/windows/主程序窗口.txt:8817-8836`「🔴🔴 **④ 而有一处【可直接执行】的改动，请你确认：`dungeon_types` 我方给 4 种、参考给 2 种**…请你回一句：**(甲) 改成参考的 2 种** ／ **(乙) 保持 4 种**（我方当前设计）」

- **已量到的事实**
  - 🔴 我方 `dungeon_types`：**4 个地牢每个都是 `["bust","portrait","deed","crest"]`**（全给）— `reports/unity_ref/22_generation_tables.md:92`
  - 🔴 参考 `heirloom_type_map`：`crypts` **`["bust","crest"]`** · `warrens` **`["portrait","crest"]`** · `weald` **`["deed","crest"]`** · `cove` **`["portrait","deed"]`** ⇒ **4 个地牢【全部不同】** — `reports/unity_ref/22_generation_tables.md:93-96`
  - `crest` 出现在 **3** 个地牢里（`crypts`/`warrens`/`weald` 含，只有 `cove` 不含）— `reports/unity_ref/04b_quests_loot_narration.md:172`
  - 这条解释 A11 的一个观察：**参考里 `crest == bust` 的等级 18/99，而我方只有 2/99** — `reports/unity_ref/22_generation_tables.md:98`
  - 我方 `heirlooms.json → quest_reward.amount_table` 与参考 `generation.rewards.heirloom_amount_table` **逐值完全相同**（`bust`/`portrait` = `[[], [0,2,2,4], [], [0,2,3,6], [], [0,3,5,9]]`；`deed`/`crest` = `[[], [0,3,5,9], [], [0,4,6,12], [], [0,6,9,18]]`）⇒ 这是**第 3 处三方完全一致** — `reports/unity_ref/22_generation_tables.md:74-84`
  - 🔴🔴 **一手口径与参考不同**：一手 `campaign/quest/quest.generation.json` 的 `generation.rewards.heirloom_type_map`：**`crypts`/`warrens`/`weald`/`cove` 都给【全部 4 种】** — `doc/windows/主程序窗口.txt:7410-7411`
  - ⇒ **两个源的读数明确不同**（一手 = 全 4 种；参考 = 每地牢 2 种）— `doc/windows/主程序窗口.txt:7411` vs `reports/unity_ref/22_generation_tables.md:93-95`
  - 🎖️ **已有一次裁定**（`#470` 系列）：「**③ 每趟 4 种都给**（原版 `dungeon_types` 说四地牢给全 4 种）＋ **标 `placeholder` + 同步 `O11`**」，理由「那是数据最直接的读法，且不需要额外逻辑」— `doc/windows/主程序窗口.txt:7823`、`:7655-7659`
  - 该代理量已进观察清单：祖产「每趟都给 4 种」= **O11** — `reports/logic_completion_plan.md:206`
  - 我方 `quest_reward` 段形状：「**4 地牢 × 全 4 种 · 6 档只有 1/3/5 有值**」— `reports/contract_change_request_heirloom_step2.md:78`
  - 但 `22_*.md` 的采用建议写「✅ 按用户指令『数值采用参考』⇒ **应当改成 2 种**」— `reports/unity_ref/22_generation_tables.md:103`
  - ⚠️ **该前提已被用户 2026-09-26 新裁定取代**：「**选1,如果E盘读不到数值就用参考**」⇒ 一手优先、参考兜底 — `reports/planner_20260926_to_lead_tier_and_source.md:29-30`；`#473` 作废 — `doc/windows/主程序窗口.txt:9354-9355`

- **候选选项**（标签原样，`doc/windows/主程序窗口.txt:8836`）
  - **(甲) 改成参考的 2 种**
  - **(乙) 保持 4 种**（我方当前设计）
  - （既有裁定另有措辞：「**③ 每趟 4 种都给** ＋ 标 `placeholder` + 同步 `O11`」— `doc/windows/主程序窗口.txt:7823`）

- **一手/参考项目怎么说**：**两方都有，且相反** —— **一手** `quest.generation.json` = **全 4 种**（`doc/windows/主程序窗口.txt:7410-7411`）；**参考** `JsonQuests.json` = **每地牢 2 种**（`reports/unity_ref/22_generation_tables.md:93-95`）。

- **代价/风险**
  - 改动面：只改 `heirloom_type_map` 的 **4** 个值 ⇒ **纯数值改动**，按纪律 AY 属「可做」— `doc/windows/主程序窗口.txt:8833-8834`
  - 但它**改的是【产出分布】** ⇒ 主程序选择先问再动 — `doc/windows/主程序窗口.txt:8835`
  - 改后会动 A11 的 `crest == bust` 计数 — `reports/unity_ref/22_generation_tables.md:98`
  - 已是 `placeholder` 且登记在 `O11` ⇒ **改动后 `O11` 如何同步：读数未给（未量）** — `doc/windows/主程序窗口.txt:7657`、`:7823`
  - 🔴 **口径风险**：若 `#473` 没被标「已取代」，后人按它会取 2 种而丢掉一手 — `doc/windows/主程序窗口.txt:9357-9358`
  - **未测**：没有落任何生成表数据 ⇒ 接上后城镇出什么任务**完全未测** — `reports/unity_ref/22_generation_tables.md:125`

- **我的建议**：**建议取 (乙) 保持 4 种** —— 理由：本项的判据源已随用户新裁定改为**一手优先**，而**一手读数是全 4 种**（`doc/windows/主程序窗口.txt:7410-7411`），且 `#470` 系列早已按「每趟 4 种都给 + `placeholder` + `O11`」裁过一次（`doc/windows/主程序窗口.txt:7823`）。🔴 **但这是建议，不是裁定** —— 需先确认 ⑥ 的判据源是否已随新裁定切换。

- **缺什么**：⑥ 的**判据源**（一手 vs 参考）未在读数里明确绑定到本项；`O11` 的同步方式未量。

---

## 7. 项 ⑦ — 亮度方向（甲/乙/丙）

- **问的是什么**：`doc/windows/主程序窗口.txt:8971-8993`「🔴🔴 **③ 一条【真正的设计分歧】，请你裁：亮度方向【两侧相反】**…**请你裁**：(甲) **取我方的「越暗越多」**（保留 `P22 ④`）／(乙) **取参考的「越亮越多」**／(丙) 两者并存（**不同资源**：金币给暗、掉落给亮）」

- **已量到的事实**
  - 5 档**逐档逐界完全一致**：参考 `Raid\TorchMeter.cs:123-129` = `Radiant 76,100`·`Dim 51,75`·`Shadowy 26,50`·`Dark 1,25`·`Out 0,0`；我方 `tuning.json → light.tiers` = `radiant 76~100`·`dim 51~75`·`shadowy 26~50`·`dark 1~25`·`black 0~0` ⇒ **唯一不同是命名**（`Out` ↔ `black`）— `reports/unity_ref/25_light_tiers_match.md:11-23`
  - 结论：**这一块无需任何改动**；这是**第 5 处两侧完全一致** — `reports/unity_ref/25_light_tiers_match.md:104`；`doc/windows/主程序窗口.txt:8948-8950`
  - 覆盖率读数：亮度 5 档 **100%**（逐界一致，12 项里最高）— `reports/unity_ref/31_actout_turn_consumption.md:87`
  - 我方 = **金币档加成**：`economy.light_tier_bonus = {radiant: 0, dim: 0, shadowy: 1, dark: 2, black: 3}` — `reports/unity_ref/25_light_tiers_match.md:64`
  - 消费点：`EconomyConfig.RewardFor(tierId) => BattleReward + LightTierBonus[档]` — `reports/unity_ref/26_light_out_and_darkness_bonus.md:37`
  - 我方另一块 `tuning.light.loot`：`radiant {}`·`dim {food:1}`·`shadowy {firewood:1}`·`dark {firewood:1, food:1}`·`black {firewood:2, food:2}` ⇒ **越暗给越多** — `reports/unity_ref/25_light_tiers_match.md:65-68`
  - 我方方向**有硬约束**：`EconomyConfig.cs:142`「`light_tier_bonus` 必须随变暗**单调不减**（`P22 ④`：越冒险越该有回报）」— `reports/unity_ref/26_light_out_and_darkness_bonus.md:53-55`
  - 参考 `JsonLoot.darkness_bonuses`：`battle`：`darkness 0 ⇒ chance 0.75 codes [B,B]`·`1 ⇒ 0.75 [B]`·`26 ⇒ 0.5`·`51 ⇒ 0.25`·`76 ⇒ 0`；`chest`：`0 ⇒ 0.95 [A]` … `76 ⇒ 0` ⇒ **越亮给越多** — `reports/unity_ref/25_light_tiers_match.md:71-73`
  - 参考的 `darkness` 不是「亮度」而是「该档的 `Min`」（消费点 `Raid\Battle\BattleGround.cs:484`；值 `0/1/26/51/76` 正好是 5 档 5 个 Min）— `reports/unity_ref/25_light_tiers_match.md:37-40`
  - 我方对应机制**只有一半**：`battle` 那一半有（`RewardFor`，同一挂点），`chest` 那一半**没有** — `reports/unity_ref/26_light_out_and_darkness_bonus.md:42`
  - `Out` 特判对比：参考 `TorchMeter.cs:416` 有显式分支；我方 `LightMeter.cs:81` 用**兜底档** `return LightTier.Black`；已知差异：`value=0` 与 `value=-5` **枚举上不可分辨** — `reports/unity_ref/26_light_out_and_darkness_bonus.md:11-27`
  - 三层齐全 + 三条硬校验（`不得为空` · **必须含全部 5 档** · **必须单调不减**）；用例 `EconomyTests.cs:43` — `reports/unity_ref/26_light_out_and_darkness_bonus.md:69-75`
  - ⚠️ **矛盾（同任务内两件结论相反）**：`reports/unity_ref/25_light_tiers_match.md:79-83` 判「**不是矛盾，是【两个不同的机制】**（挂点不同）」；而 `reports/unity_ref/26_light_out_and_darkness_bonus.md:56-61` 判「**挂点相同、方向相反 ⇒ 是分歧**」⇒ 窗口采信后者 — `doc/windows/主程序窗口.txt:8986-8987`
  - ⚠️ **矛盾（旧读数已被自己推翻）**：`reports/unity_ref/_probe/light_compare2.py:41-53` 用 `darkness = 100 − light` 得「只有头尾对得上」；`reports/unity_ref/25_light_tiers_match.md:27-45` 记「第一版**算错了方向**…结论完全错」

- **候选选项**（标签原样）：**(甲)** 取我方的「越暗越多」／**(乙)** 取参考的「越亮越多」／**(丙)** 两者并存（**不同资源**：金币给暗、掉落给亮）— `doc/windows/主程序窗口.txt:8988-8989`；主程序自述「**我倾向 (丙) 或 (甲)**」— `doc/windows/主程序窗口.txt:8990`

- **一手/参考项目怎么说**：**参考** —— `TorchMeter.cs:123-129`·`BattleGround.cs:484`·`TorchMeter.cs:416`·`JsonLoot.darkness_bonuses`（见上）。**一手 E 盘：读数里没有**。

- **代价/风险**
  - **未测**：「改成参考方向后经济会怎样」**完全未测**（纪律 BK）— `reports/unity_ref/26_light_out_and_darkness_bonus.md:84`
  - 参考 `chest` 组我方确实没有 ⇒ 「该不该有」**未判** — `reports/unity_ref/26_light_out_and_darkness_bonus.md:85-86`
  - 我方方向被 `EconomyConfig.cs:142` 的三条校验钉住 ⇒ (乙) 会与「必须单调不减」冲突（**改法未量**）— `reports/unity_ref/26_light_out_and_darkness_bonus.md:69-75`
  - **改动文件清单：读数里没有（未量）**

- **我的建议**：**建议取 (丙) 两者并存**（金币给暗、掉落给亮）—— 理由：我方「越暗给金币」有 `P22 ④` 明文且数据/代码/用例三层齐全（`reports/unity_ref/26_light_out_and_darkness_bonus.md:69-75`），参考「越亮给掉落表」挂在**另一个机制**（`chest`）上（`reports/unity_ref/26_light_out_and_darkness_bonus.md:42`）⇒ 拆成不同资源最省事且不废弃既有校验。若只能选一个 ⇒ 建议 (甲)。🔴 **但这是建议，不是裁定。**

---

## 8. 项 ⑧ — 取整方向 + 下限（(甲)/(乙)）

- **问的是什么**：`doc/windows/主程序窗口.txt:9089-9099`「🔴 **④ 请你裁（两条会改平衡的，建议一并入 `§39`）**…**(甲) 取整方向**：`Round(AwayFromZero)` ⇒ 改成参考的 `Ceil`…**(乙) 伤害下限**：`1` ⇒ 改成参考的 `0`…**(丙) 暴击那一步**」；另 `reports/unity_ref/106_rounding_compare.md:55-60` 另给一组 (甲)/(乙)（两件里含义不同，见下）

- **已量到的事实**
  - 参考全库 5 种取整：`(int)` 强转 **142**（截断，57%）· `RoundToInt` **73** · `CeilToInt` **19** · `Math.Round` **10** · `FloorToInt` **3** — `reports/unity_ref/105_rounding_audit_all.md:13-17`
  - 战斗侧 **35 处**：`RoundToInt` **28** · `CeilToInt` **7**（**7 处全在伤害/治疗**：`Character.Heal L1095/L1096`·`BattleSolver L387`·`L450/L453`·`RaidSceneManager L2729/L2748`）— `reports/unity_ref/105_rounding_audit_all.md:29-40`
  - 规律：**HP 走 `CeilToInt`（向上）· 压力/减压/火把/折扣走 `RoundToInt`** — `reports/unity_ref/105_rounding_audit_all.md:61-67`
  - **两段取整**：`BattleSolver.cs:387` `Ceil(initialDamage×(1-Protection))` → `Character.cs:1109` `RoundToInt(...)`；🔴 **第二段只在中间又乘小数时起作用**（暴击 ×1.5）— `reports/unity_ref/105_rounding_audit_all.md:76-84`
  - 下限**不统一**：伤害 **0**（`BattleSolver:388`）· 压力 **1**（`StressEffect`）· `Character.TakeDamage`/`Character.Heal` **无** — `reports/unity_ref/105_rounding_audit_all.md:93-98`
  - 两侧分布对照：`(int)` 强转 参考 **142** / 我方 **47**；`Round` **73** / **23**；`Ceil` **19** / **6**；`Floor` **3** / **0**；`Math.Round` **10** / **0** — `reports/unity_ref/106_rounding_compare.md:11-17`
  - 我方现状：`darkest/scripts/core/math/BattleMath.cs:182` `ApplyDamageRounding` = **`Round(AwayFromZero)` + 下限 1** ⇒ **双重不一致**；原文见 `darkest/scripts/core/math/BattleMath.cs:180-183`（`doc/windows/主程序窗口.txt:9066-9073`）
  - 差异具体值：`7.4` ⇒ 我方 **7** · 参考 **8**；`0.0` ⇒ 我方 **1** · 参考 **0**；`x=5.4` ⇒ 参考 **6** · 我方 **5** — `reports/unity_ref/106_rounding_compare.md:38-44`；`doc/windows/主程序窗口.txt:9062`
  - 第二处不一致：`darkest/scripts/gameplay/sim/skill/HealAmount.cs:22` 用 `Round`，参考 `Character.Heal:1095`/`:1096` 用 `CeilToInt` — `reports/unity_ref/106_rounding_compare.md:29`
  - **调用点全列（定义 1 · 调用 14）**：定义 `darkest/scripts/core/math/BattleMath.cs:180`；**生产代码只 3 处** = `sim/pipeline/DamageStep.cs:105`·`sim/pipeline/WeaponBaseDamage.cs:59`·`sim/director/BattleProjector.cs:229` — `reports/unity_ref/107_rounding_callsites.md:13-21`
  - 下限的真实来源：`darkest/data/tuning.json:134` `"damage_floor": 1` · `darkest/scripts/data/BalanceTable.cs:17` · `darkest/scripts/data/TuningConfig.cs:116`；🔴 `darkest/scripts/data/TuningConfig.Validate.CombatSide.cs:59` `if (t.DamageFloor < 1)` 强制 ≥1 — `reports/unity_ref/107_rounding_callsites.md:33-39`
  - 会红的断言共 **6 条**：`L43`·`L51`·`L59`·`L67`（`PhysicalHit`，旧 **9/9/9/10** ⇒ 新 **10/10/10/11**；**间接**走 `ApplyDamageRounding`，见 `BattleMath.cs:107`/`:134`）＋ `L93`（`1.49` ⇒ **2**）· `L95`（`-3.2` ⇒ **0**）；另 `L90` 测试名（`RoundsAwayFromZero`）也要改 — `reports/unity_ref/109_rounding_impact_corrected.md:12-15`、`:53`
  - 另 **7 条仍过**：`L47`·`L55`·`L63`·`L74`·`L92`·`L94`·`L96` — `reports/unity_ref/PLAN_adoption.md:282`
  - 差异**不是边角**：穷举 `x = 0.0~200.0` 步长 0.1 共 **2001** 点，两种顺序**不等 900 处（≈45%）**；整数上恒等 — `reports/unity_ref/40_rounding_exhaustive.md:13-15`、`:38`
  - 另 2 条「我方自有 ⇒ 不改」：`sim/survival/WeakDeathsDoor.cs:71`·`sim/board/UnitRuntime.cs:57`（都是 `Max(1, Round(…))`）；`sim/turn/TurnSequencer.cs` 的 1 处 `Round` 记**未读** — `reports/unity_ref/106_rounding_compare.md:30-32`
  - **改动包 = 4 个文件 · 6 处编辑**：① `darkest/scripts/core/math/BattleMath.cs:182` `Round(AwayFromZero)`→`Ceil` ＋ clamp 用参数 · ② `darkest/data/tuning.json:134` `damage_floor` 1→0 · ③ 🔴 `darkest/scripts/data/TuningConfig.Validate.CombatSide.cs:59` `if (< 1)`→`< 0`（否则 0 会被拒）· ④ `darkest/tests/FormulaTests.cs`（6 条断言 + `L90` 名字）· ⑤ `darkest/scripts/gameplay/sim/skill/HealAmount.cs:22` `Round`→`Ceil` · ⑥ ⚠️「加不加第二段 `Round`」参考**有** ⇒ 建议加 — `reports/unity_ref/PLAN_adoption.md:271-285`；`doc/windows/主程序窗口.txt:9193`
  - **实跑基线**：`FormulaTests` **失败 0 · 通过 15 · 总计 15**；`M1cStage3DiffTableTests` **失败 0 · 通过 1 · 总计 1** — `reports/unity_ref/110_rounding_baseline_verified.md:13-14`
  - 全量基线 **843/846**（3 个既有红 = `M6Acceptance_WinRateBand_And_Rhythm`·`E2UI_Resources_RecomputableFromEventStream`·`V5_35_SingleUnitDamageDelta_ConvergesBothSides`）⇒ 改动后**预计 6 红**，可对照 — `reports/unity_ref/PLAN_adoption.md:291-293`；`reports/unity_ref/110_rounding_baseline_verified.md:79-81`
  - 差异表测试**不会红**：只有 2 条断言，实测 20000 组随机区间两口径各 0 次违反单调 — `reports/unity_ref/108_rounding_test_impact.md:30-45`
  - 下限 1 vs 0 会改变行为（最值得裁的一处）：敌人剩 1 HP 且这一击被 `Prot` 吃光 ⇒ **参考 `ceil(x)` 可能是 0（打不死）** · **我方 `max(1, round(x))` 永远至少 1（一定打死）** ⇒ **我方「补刀」能力比参考强** ⇒ 穿透**死门判定 / 击杀奖励 / 胜负判定** — `doc/windows/主程序窗口.txt:9078-9082`
  - ⚠️ **矛盾（同为全量测试基线）**：`reports/unity_ref/PLAN_adoption.md:291-293` 记 **843/846（3 红）**；而 `doc/windows/主程序窗口.txt:8181` 记 **846 → 842（四红）**（后者是 Σ 归一落库之后）— 两处口径不同，读数未说明 3 个既有红的变化
  - ⚠️ **另一处口径差（同一事实两种扫描）**：`reports/unity_ref/41_our_rounding_vs_ref.md:16-17` 给 `Mathf.RoundToInt 73 · Mathf.CeilToInt 19 · Mathf.Floor 14 · Mathf.FloorToInt 3 · Mathf.Round 2`；`reports/unity_ref/105_rounding_audit_all.md:13-17` 给 `(int) 142 / RoundToInt 73 / CeilToInt 19 / Math.Round 10 / FloorToInt 3`

- **候选选项**（标签原样，⚠️ **两件里 (甲)/(乙) 含义不同**）
  - 窗口版（`doc/windows/主程序窗口.txt:9091-9096`）：**(甲) 取整方向**（`Round(AwayFromZero)` → `Ceil`）／**(乙) 伤害下限**（`1` → `0`）／**(丙) 暴击那一步**（**新增能力**，我方现在没有）
  - `106_*.md` 版（`reports/unity_ref/106_rounding_compare.md:55-60`）：**（甲）** `ApplyDamageRounding` 改成 **`Ceil` + 下限 0** ＋ 再单独加一处 `Round` 给「倍率后」的场合；**（乙）** 保持一处但**先 `Ceil` 再 `Round`** ⇒ 🔴 实测 `Ceil` 后是整数 ⇒ `Round` 无影响 ⇒ **（乙）等价于（甲）** ⇒ 两者只差在「暴击是否再取整」
  - 主程序建议（**两处不同，见矛盾**）：
    - `doc/windows/主程序窗口.txt:9099-9100`：「**(丙) 现在做（新增能力、默认不触发）· (甲)(乙) 入 `§39` 与解冻一次做完**」，理由 (甲)(乙) 都是「改每一个伤害值」，与 Σ 那次打穿 A1 同一类风险
    - `doc/windows/主程序窗口.txt:9193`：「🆕 **【本封补：已量出完整改动包 ⇒ 可一次拍到底】**」
  - ⚠️ **矛盾/需注意**：同一封窗口里，推进节奏有两说（先 (丙)、(甲)(乙) 入 `§39`；vs 可一次拍到底）— `doc/windows/主程序窗口.txt:9099-9100` vs `:9193`

- **一手/参考项目怎么说**：**参考** —— `BattleSolver.cs:387`/`:388`·`Character.cs:1107-1112`·`Character.Heal:1095`/`:1096`·`StressEffect`·`TorchMeter`（`reports/unity_ref/105_rounding_audit_all.md:37-40`、`:50-55`、`:93-98`）。`Mathf.RoundToInt` 是 **Unity 引擎内置**，源码不在参考项目里 — `reports/unity_ref/41_our_rounding_vs_ref.md:12-14`。**一手 E 盘：读数里没有**。

- **代价/风险**
  - 改 **4 个文件**（函数 · json · 校验 · 测试）；**6 条断言会红**；`M1cStage3DiffTableTests` 不会红 — `reports/unity_ref/109_rounding_impact_corrected.md:53`；`reports/unity_ref/108_rounding_test_impact.md:44`
  - 生产调用点只 **3 处** ⇒ 影响面窄 — `reports/unity_ref/107_rounding_callsites.md:24`
  - 方向改的是**每一个伤害值**（任何非整数 `x` 上约 **50%** 的点差 1 ⇒ 穷举 **900/2001** 点不同）⇒ 归 `§39` — `doc/windows/主程序窗口.txt:9091-9093`
  - 下限会穿透**死门判定 / 击杀奖励 / 胜负判定** — `doc/windows/主程序窗口.txt:9082`
  - **未测**：改成 `Ceil` + 下限 0 后伤害变多少**完全未测** — `reports/unity_ref/41_our_rounding_vs_ref.md:92`

- **我的建议**：**建议 (丙) 现在做（新增能力，不改现有数值），(甲)(乙) 一次拍到底并同批登记 `§39`** —— 理由：改动包与基线都已量全（`reports/unity_ref/110_rounding_baseline_verified.md:13-14`；`doc/windows/主程序窗口.txt:9193`），而 (甲)(乙) 是「改每一个伤害值」，与 Σ 那次打穿 A1 同类风险（`doc/windows/主程序窗口.txt:9100`）⇒ **同批做、同批跑基线**最省一次返工。🔴 **但这是建议，不是裁定。**

---

## 9. 项 ⑨ — 我方 `unlocks.json` 的建筑解锁要不要留

- **问的是什么**：`doc/windows/策划窗口.txt:34`「⑨ 🔴 **我方 `unlocks.json` 的建筑解锁要不要留**（参考没有这机制）」；上位问题原话：`doc/windows/主程序窗口.txt:9186-9187`「🔴 **而我方 `unlocks.json` 是「第 1 趟解锁 Tavern、第 3 趟解锁 Abbey」** ⚠️ 📌 **那是我方自创**（或把「升级门槛」当成了「建筑解锁门槛」）⇒ ✅ **请裁**」

- **已量到的事实**
  - 参考**没有建筑解锁机制** —— 四条独立证据：① `Estate.cs:45-74` 的 `Buildings` 字典**无条件装入** 8 栋 + 墓地 + 雕像（中间没有任何 `if`）；`InitializeBuilding` 恢复的是**升级进度**不是解锁状态 ② `TownManager.buildingSlots` 是 `[SerializeField]`（场景手配），`InitializeBuildings()` 循环里无条件 ③ `BuildingSlot` 类只有 4 个序列化字段 + 3 个 UI 方法，**没有 `IsUnlocked`/`SetActive`** ④ 唯一进度计数器 `QuestsComleted` 的 **12 个使用点**只喂任务生成（`QuestGenerator.cs:62/80-92`，含 7 档分支）— `reports/unity_ref/57_no_building_unlock.md:11-36`
  - 数据里那三个字段是死数据：`number_of_quests_finished`/`highest_dungeon_level`/`on_start_town_visit_priority` 在 `DarkestDatabase.cs` 里**各出现 0 次** — `reports/unity_ref/56_unlock_fields_dead.md:11-18`、`:24-33`
  - 结论：🔴 **8 栋建筑【从一开始就全在】**（与 DD1 原版一致）— `doc/windows/主程序窗口.txt:9185`
  - 我方数据本体：`unlock_tavern` `required_runs_finished: 1` ⇒ `["building:tavern"]` — `darkest/data/unlocks.json:6-7`；`unlock_abbey_and_curios` `required_runs_finished: 3` ⇒ `["building:abbey","curio:cur_book_stack","curio:cur_altar"]` — `darkest/data/unlocks.json:9-10`
  - 起手态 = 「城池只有 Stage Coach ＋ 4 种 Curio ＋ 名册可用上限 8（硬上限 12 见 C1）」— `darkest/data/unlocks.json:4`
  - **起手态三断言复核 3/3 全对**：① `config.base_curios` 恰好 4 种 ✓ ② `roster_base_cap = 8` ✓ ③ 只有 Stage Coach（实现 `scripts/ui/hamlet/HamletRoot.Build.cs:224`：`bool unlocked = bId == "stagecoach" || unlockedBuildings.Contains(bId);`）✓ — `reports/unity_ref/151_unlocks_start_verified.md:20-23`、`:70`
  - 参考侧三件套（8 栋逐值）：`stage_coach` priority **0**/quests **0**/dungeon_lv **0**；`abbey` 1/2/0；`tavern` 1/2/0；`blacksmith` 1/3/0；`guild` 1/3/0；`sanitarium` 1/4/0；`camping_trainer` 1/0/**2**；`nomad_wagon` 1/0/**2** — `reports/unity_ref/55_unlock_reconcile.md:20-29`
  - 双方差异四处：① 计数单位不同（参考 `number_of_quests_finished` vs 我方 `required_runs_finished` + `required_battles_won`）② 参考多 `highest_dungeon_level` 维，我方没有 ③ 参考多 `on_start_town_visit_priority`，我方没有 ④ **我方只有 2 个建筑有解锁条件，参考 7 个都有** — `reports/unity_ref/55_unlock_reconcile.md:53-78`
  - 三个**真实消费点**：(a) 城池建筑可见性 (b) Curio 池可用种类（起手 4 → 6）(c) 名册当前可用上限；⚠️ 「只在 UI 上『锁着』而内核不拦」的风险已被注意到 — `doc/architecture/tasks/next_round.md:15-16`
  - 触及面（若删/改）：**8 个代码文件**（`HamletRoot.Build.cs:204/209/224/381`·`RunProgress.cs:39/46/55/72/132/160-166`·`ExpeditionFlow.cs:209`·`ExpeditionContext.cs:91/181/184/191/210`·`ExpeditionComposition.cs:169`·`MainMenuRoot.cs:139`·`HamletRoot.cs:107`）＋ **6 个测试文件**（`UnlocksConfigTests`·`UnlockConsumptionTests`·`CurioUnlockGateWiringTests`·`NextUnlockProgressionTests`·`RosterCapGrowthTests`·`ConfigNoSilentDefaultTests`）＋ 契约 `P27` — `reports/unity_ref/151_unlocks_start_verified.md`（引 `doc/architecture/data_schema.md:774`）
  - ⚠️ **本项已有一半被裁过**：用户/策划 `#423`（「完全对齐原版」）已裁：**删 `unlocks.json` 的 `roster_cap_delta:2 / :4`**，**保留** `unlock_tavern`（第 1 趟）· `unlock_abbey_and_curios`（第 3 趟）— `doc/windows/主程序窗口.txt:5559-5562`；登记见 `doc/architecture/dd1_conformance.md:63`、`doc/state.md:639`
  - 代码侧残留仍在解析该前缀：`darkest/scripts/data/UnlocksConfig.cs:47-50`（`NamespacePrefixes`）· `:150-159` · `darkest/scripts/gameplay/sim/run/RunProgress.cs:110-111`（M7 ① 仍待做）— `reports/logic_completion_plan.md:177`
  - ⚠️ **矛盾（同一条线内前后推翻，两处都引）**：`reports/unity_ref/55_unlock_reconcile.md:37-40` 从数据形状**推断**三件套两维是「或」关系（已标「推断」）；而 `reports/unity_ref/56_unlock_fields_dead.md:39-42` 实测三字段无消费点 ⇒「『或/与』这个问题**不存在**——因为它**没有被判定过**」；`reports/unity_ref/55_unlock_reconcile.md:60-61` 措辞隐含参考「有机制」，而 `reports/unity_ref/57_no_building_unlock.md:8-19` 证明参考**根本没有**机制
  - ⚠️ 「不能验」标注：「DD1 原版建筑起手全开」**是 wiki 常识，不是本仓读数** ⇒ 只作佐证 — `reports/unity_ref/57_no_building_unlock.md:86-87`；「我方 `unlocks.json` 该不该改 ⇒ 归策划裁（**我方可能是有意设计**）」— `reports/unity_ref/57_no_building_unlock.md:89`

- **候选选项**：**读数里没有编号选项**（无 甲/乙/丙、(A)/(B)、1/2/3）。只有散文式处置：① 「参考没有建筑解锁 ⇒ **不能照抄** ⇒ 归策划裁」— `reports/unity_ref/57_no_building_unlock.md:97`；② 二选一措辞：「我方把『升级门槛』误当成了『建筑解锁门槛』」**或者**「我方**有意**做了不同的设计（要问策划）」— `reports/unity_ref/57_no_building_unlock.md:61-62`；③ 5 个缺失建筑解锁条件「该不该补 ⇒ 归策划」— `reports/unity_ref/55_unlock_reconcile.md:105`

- **一手/参考项目怎么说**：**参考** —— 8 栋起手全在、代码里没有解锁判断（四条证据见上）— `reports/unity_ref/57_no_building_unlock.md:11-36`；三字段是死数据 — `reports/unity_ref/56_unlock_fields_dead.md:15-18`。**一手 E 盘：读数里没有**（E 盘在这条线上只被用作**升级消耗**的三方对账源）— `reports/unity_ref/09_buildings_from_ref.md:104-105`。

- **代价/风险**
  - 选「**留**」（零改动）：与参考/DD1 行为不一致；但**零行为、零测试改动**，且起手态 3 断言已全对 — `reports/unity_ref/151_unlocks_start_verified.md:70`
  - 选「**删/改**」：触及 **8 个代码文件 + 6 个测试文件 + 契约 `P27`**，且会牵动「区分硬上限与当前可用上限」的口径 — `doc/architecture/data_schema.md:774`；`doc/architecture/tasks/next_round.md:15`
  - `#423` 已登记过的**牵连清单**（更精确）：`UnlocksConfig.cs:47/78/139/142/147/150` + `ExpeditionContext.cs:198` + `HeirloomStock.cs:145` — `doc/architecture/dd1_conformance.md:63`
  - 选「**照参考补 5 栋阈值**」：`55_*.md:84-86` 说阈值可照抄，`56_*.md:57` 说**不能照抄**（参考没在用它们）⇒ **同一条线内两件读数结论相反**
  - **未测**：换阈值后行为**完全未测** — `reports/unity_ref/55_unlock_reconcile.md:106`
  - **未量**：删改的**完整文件/测试清单**只是 grep 触及面，**改动量与读数对照未量**

- **我的建议**：**建议先保留**（含 2 条 `building:` 项 —— 注意 `#423` 已明确「保留」这两条），立即在 `unlocks.json` 的 `_note` 里标注「**我方自创 · 参考项目无对应机制**」，并把「留 / 删 / 改成对齐参考」交 `#480` 起的逐条裁定。🔴 理由：参考侧三字段是**死数据**（照抄无来源），我方三个消费点已接线且有 6 个测试文件钉住（删除代价高且牵动 `P27`/`P22` 口径）。🔴 **但这是建议，不是裁定。**

---

## 10. 项 ⑩ — 我方自加的伤害机制 `missing_hp`（3 个子项）

- **问的是什么**：`doc/windows/主程序窗口.txt:9194`「⑩ **本封新增：我方自加的伤害机制 `missing_hp` ⇒ 请裁 3 项**…**请裁 3 个子项**：① **段类型**保留（并标「参考无对应」）还是中性化？（我倾向**保留**）· ② **`coefficient` 取值**（`0.6` / `0.7`）有无依据？· ③ **`target_hp_below_percent: 50`** 同上。」

- **已量到的事实**
  - 参考**数据** 11 种模式**全 0 命中**：`missing_hp 0`·`missing_health 0`·`hp_missing 0`·`MissingHp 0`·`missingHp 0`·`coefficient 0`·`base_value 0` — `reports/unity_ref/113_missing_hp_no_ref.md:13-14`
  - 参考**代码** 0 命中：`MissingHealth 0`·`missingHealth 0`·`HealthLost 0`·`MissingHp 0`；另 7 个「按生命比例」候选 `hp_percent 0`·`target_hp 0` ⇒ **共 18 种模式 0 命中** — `reports/unity_ref/113_missing_hp_no_ref.md:16-17`
  - 看着像的 `damage_low_multiply` **139 次**，但读码 `Effect.cs:707-720` 是给 `AttributeType.DamageLow` 加乘数的**属性 buff**，**不是等价物** — `reports/unity_ref/113_missing_hp_no_ref.md:26-39`
  - 我方两条（逐字）：`medic_lethal_injection` ⇒ `"segments": [{"type":"missing_hp","base":1.0,"coefficient":0.6}]` ＋ `dmg_pct: -80` — `darkest/data/skills.json:995-997`（叙述见 `reports/unity_ref/113_missing_hp_no_ref.md:45-48`）；`commissar_execution_order` ⇒ `{"type":"missing_hp","base":1.0,"coefficient":0.7}` ＋ `"requires": {"target_hp_below_percent": 50}` — `darkest/data/skills.json:1345-1352`
  - 段类型**穷尽只有 2 种**：`flat` **×28**（与参考 `Lerp(MinDamage,MaxDamage)` 结构**同构**，`BattleSolver.cs:383-385`）· `missing_hp` **×2**（🔴 自加）⇒ **不是系统性问题，只影响这 2 条** — `reports/unity_ref/115_damage_shape_audit.md:13-15`、`:24-32`；`doc/windows/主程序窗口.txt:9194`
  - Σ 口径：44 条 = `damage=null` **16** ＋ 无 `flat` 段（`missing_hp`）**2** ＋ 有 `flat` 段 **26** ⇒ `missing_hp` 用 `base`+`coefficient`，**没有 `multiplier`** — `reports/unity_ref/112_sigma_normalization_audit.md:22-34`、`:49-52`
  - 🔴 **未入 `dd1_baseline §39.2` 解冻清单**：`dd1_baseline.md` 全文 `missing_hp` **0 次**·`coefficient` **0 次**·`target_hp_below` **0 次** — `reports/unity_ref/114_missing_hp_ownership.md:23-26`
  - `§39` 是解冻后校准清单的**单一载体** — `reports/unity_ref/114_missing_hp_ownership.md:11-13`（引 `doc/modules/dd1_baseline.md:1904`）
  - `§39.1` 入清单条件 = 「它是**平衡数值**改动（不是结构命名）」；而 `§39.2` 现有 **9 项**里 `#5`『主干段数 ≤3 段』是**结构性**的 ⇒ 清单**实际接纳了结构性项** — `reports/unity_ref/114_missing_hp_ownership.md:55-62`
  - 判定：**形状** = 机制本体（按字面不算平衡数值，但按实践**可入**）· **`coefficient: 0.6`/`0.7`** = 平衡数值 ⇒ **确定该入** · **`target_hp_below_percent: 50`** ⇒ **该入** ⇒ 「建议：入清单（**3 个子项**）」— `reports/unity_ref/114_missing_hp_ownership.md:63-69`
  - 🔴 `§43` 已把这两条技能判过：`medic_lethal_injection ← noxious_blast`（`−80%`，「语义同」，✅ **对齐**）· `commissar_execution_order ← opened_vein`（`−15%`，「语义近似」，⚠️ **中**）⇒ **`§43` 的映射粒度是【技能级】，不是【伤害形状级】** ⇒ 「**技能对上了 ≠ 伤害算法对上了**」— `reports/unity_ref/114_missing_hp_ownership.md:36-46`
  - 同族第二处自加：`commissar_execution_order` 的 `requires.target_hp_below_percent` 也是「按生命」的条件 ⇒ 与 `missing_hp` 是**同族的两处自加** ⇒ **两条一起处置** — `reports/unity_ref/113_missing_hp_no_ref.md:67-69`
  - **未量**：`base: 1.0` 是常量还是配置（2 条都是 `1.0` ⇒ 样本不足）— `reports/unity_ref/115_damage_shape_audit.md:74-75`；`coefficient` 的取值**有无依据**（只有 2 个样本）— `reports/unity_ref/114_missing_hp_ownership.md:96-97`；它是**哪次改动**加的（无历史可查）— `reports/unity_ref/114_missing_hp_ownership.md:93`

- **候选选项**：**读数里没有编号选项**。出现的处置只有三种措辞：「**中性化**」（纪律 BS：原版没有 ⇒ 必须中性化）／「**归解冻清单**（保留我方设计，明确标注『参考无对应』）」／「**保留并标注**」；判据是「看它是**额外修数**还是**机制本体**」— `reports/unity_ref/113_missing_hp_no_ref.md:42`、`:53-61`（原文：「**『参考没有』这个结论【有三种处置】**」）。主程序倾向**保留** — `reports/unity_ref/113_missing_hp_no_ref.md:56-57`；`doc/windows/主程序窗口.txt:9194`

- **一手/参考项目怎么说**：**参考** —— 18 种模式**全 0 命中**（`reports/unity_ref/113_missing_hp_no_ref.md:13-17`）；参考的伤害形状只有 `Lerp(min,max)` 一个自由度（`reports/unity_ref/115_damage_shape_audit.md:53-54`）。**一手 E 盘：读数里没有**。

- **代价/风险**
  - 只涉 **2 条技能** — `reports/unity_ref/115_damage_shape_audit.md:39-42`
  - **未入 `§39.2`** ⇒ 现在处于「自加了但没登记」状态 — `reports/unity_ref/114_missing_hp_ownership.md:23-26`
  - 若**中性化** ⇒ 等于拿掉这条技能**存在的理由**（「越残血越痛」是机制本体，不是额外修数）— `reports/unity_ref/113_missing_hp_no_ref.md:53-57`
  - `coefficient` 取值**无参考可对 ⇒ 属策划** — `reports/unity_ref/113_missing_hp_no_ref.md:86-87`
  - 连带给架构一条：建议 `§43` 加一列「**公式形状是否同**」— `reports/unity_ref/114_missing_hp_ownership.md:81-82`
  - **未测**：改动后的战斗读数完全未测

- **我的建议**：**建议 ① 保留并标注「参考无对应」；②③ 与形状一并入 `§39.2` 解冻清单（3 个子项一次裁）** —— 理由：它是**机制本体**（`reports/unity_ref/113_missing_hp_no_ref.md:55-57`），但它的**值**（`0.6`/`0.7`/`50`）是**平衡数值**且**无参考可对**（`reports/unity_ref/114_missing_hp_ownership.md:63-69`）⇒ 只能由策划给值。🔴 **但这是建议，不是裁定。**

---

## 11. 项 ⑪ — `damage_axis: mental` 的 1 条存疑（`ranged_intimidating_shot`）

- **问的是什么**：旧描述「⚠️ **「`ranged` + `mental`」在语义上别扭** ⇒ **请确认这是刻意（「威吓箭＝精神冲击」）还是笔误**」— `reports/unity_ref/129_morale_table_matches.md:66`；**已收窄**为：「🔴 **仅剩一个【设计选择】待确认**：**「为何一支箭走 `SpiritHit`（韧性减伤）而非护甲减伤？」**⇒ 这不再是字段矛盾，是数值/主题取舍」— `doc/windows/主程序窗口.txt:9194`

- **已量到的事实**
  - `damage_axis` 取 `mental` 的我方只有 **3 条**：`ranged_intimidating_shot`（`darkest/data/skills.json:1633`）· `caster_fear_whisper`（`:1719`）· `caster_mental_shock`（`:1765`）；其中 `caster_*` 两条自洽 — `doc/windows/主程序窗口.txt:9194`
  - `ranged_intimidating_shot` 定义：`owner_unit: ranged_archer` · `range_axis: ranged` · `damage_axis: mental` · `damage.segments [{flat, multiplier 1.0}]` · `morale_effects [{scope: targets, delta: -4}]` · `dmg_pct: 0` · `origin: ours` — `darkest/data/skills.json:1589-1636`
  - **机制解释（关键）**：`darkest/scripts/gameplay/sim/pipeline/DamagePipeline.cs:144-159` 是**互斥二选一** —— 有显式 `morale_effects` ⇒ 只走显式 `skill_morale_effect`；否则才按 `skill.Axis` 派生 — `reports/unity_ref/128_morale_axis_decoupled.md:12-21`
  - 派生侧早退条件：`MoraleLedger.cs:140-149` `if (axis != "mental") return;` — `reports/unity_ref/128_morale_axis_decoupled.md:24-30`
  - ⇒ 三条读数：**互斥** · 派生**只在 `axis == "mental"` 时发生** · **显式存在时 `axis` 完全不参与士气** ⇒ 「`mental` 的作用是**换伤害公式**，不是**触发削士气**」— `reports/unity_ref/128_morale_axis_decoupled.md:34-36`、`:45`
  - 表值对上注释：`darkest/data/morale_events.json` 三条 = `mental_hit -8`（`self`）·`mental_crit_hit -12`（`self`，注「**覆盖同一次伤害的 −8**」）·`mental_aoe_hit -5`（**`per_target`**，注「AOE 折扣」）⇒ 与代码注释 `−8/−12/−5` **逐个对上** — `reports/unity_ref/129_morale_table_matches.md:14-19`、`:40-41`
  - 三条都 `occurrence: normal`；`source` 指向 `combat_math §5.2 · #157` — `reports/unity_ref/129_morale_table_matches.md:31-32`
  - ⇒ **设计自洽**：「用韧性而非护甲减伤的箭」＋「削 4 点士气」；`mental_hit` 的 −8 对它**不适用**，削士气只用它自己的 **−4** — `reports/unity_ref/129_morale_table_matches.md:53-58`
  - `damage_axis` 整体是**我方自加**：参考数据 `physical` **59 处**（🔴 **56 在 `JsonQuirks.json`** 的怪癖分类）· `mental` **171 处**（🔴 **107 在 `JsonQuirks.json`**）· 参考代码 `DamageType`/`damageType`/`PhysicalDamage` **全 0** · 参考 `.type` 只有 `melee`/`ranged` — `reports/unity_ref/126_axis_vs_type.md:29-36`；`doc/windows/主程序窗口.txt:9194`
  - 同维度的 `range_axis` 与参考 `.type` **同构**（`melee 19`/`none 16`/`ranged 9` vs 参考 `ranged 50`/`melee 47`）⇒ **只有 `damage_axis` 这一轴是自加** — `reports/unity_ref/126_axis_vs_type.md:13-20`；`doc/windows/主程序窗口.txt:9194`
  - 参考侧机制对应**存在**：`ranged_intimidating_shot` = 远程 + 士气削减 ⇒ 参考 `.stress` **8307 处** ＋ `.damage_low_multiply` **139 处**；我方 `morale_effects` = 参考 `.stress`（**同构异名**）— `reports/unity_ref/118_enemy_skill_analogues.md:27`、`:64-69`
  - 相关但不同的改动：Σ 归一把它 **0.5 → 1.0（×2.0）** — `doc/windows/主程序窗口.txt:8192`
  - **未量**：`morale_events.json` 完整条数与字段全集；`occurrence` 除 `normal` 还有什么取值 — `reports/unity_ref/129_morale_table_matches.md:82-85`
  - **未量**：`SpiritHit` 的消费落点 **file:line**（读数只以 `SpiritHit` 之名出现，未给落点行号）— `reports/unity_ref/128_morale_axis_decoupled.md:43`

- **候选选项**：**读数里没有编号选项**。存在的只有二选一措辞：「**刻意**（威吓箭＝精神冲击）」还是「**笔误**」— `reports/unity_ref/129_morale_table_matches.md:66`；收窄后只剩「**一个设计选择待确认（为何走 `SpiritHit`）**」— `reports/unity_ref/129_morale_table_matches.md:59-60`

- **一手/参考项目怎么说**：**参考** —— **没有物理/精神伤害轴**（三角度查证）— `reports/unity_ref/126_axis_vs_type.md:29-37`；参考有 `.stress` **8307 处** — `reports/unity_ref/118_enemy_skill_analogues.md:27`；参考 `resilience` 只 **4 处**且都是本地化文本 — `reports/unity_ref/126_axis_vs_type.md:48`。**一手 E 盘：读数里没有**。

- **代价/风险**
  - 若把 `damage_axis` 改成 `physical` ⇒ 改走物理公式（护甲减伤）⇒ 会改这条技能的伤害读数（且它已被 Σ 归一推到 **×2.0**，`doc/windows/主程序窗口.txt:8192`），**具体伤害差未量**
  - 若保留 ⇒ 需确认主题合理性（「为何用韧性减伤」属**设计**）— `reports/unity_ref/129_morale_table_matches.md:86`
  - 只需**修报告措辞**：判据 182 —— 「我报了存疑后【又查清了】⇒ 要【回撤/收窄】那条报告，**否则策划按旧描述判**」— `reports/unity_ref/129_morale_table_matches.md:70-72`

- **我的建议**：**建议维持现状**（`mental` + `morale_effects` 不动），只把「**为何这支箭走 `SpiritHit`（韧性减伤）而非护甲减伤**」作为一条**数值/主题选择**交策划 —— 理由：`128`/`129` 已实测两条路**互斥**、代码与表值**逐字一致**（`reports/unity_ref/128_morale_axis_decoupled.md:34-36`；`reports/unity_ref/129_morale_table_matches.md:16-19`），「别扭」已不成立。🔴 **但这是建议，不是裁定。**

---

## 12. 项 ⑫ — 架构 `M2-ROUTE-B` 裁定 ② 的「待接条件」列（7:17 → 10:14）

- **问的是什么**：架构原要求（`M2-ROUTE-B-20260921` 第 ② 项）：「🔴 **但请给它们加一列【待接条件】**：例 `party_surprise_chance ⇒ 待接条件：趟级修正载体存在`」；理由「**『缺载体』与『未接线』是两件事** —— 未接线 ⇒ 接线就行；🔴 缺载体 ⇒ **要先造一层结构**（成本高一个量级）」— 原要求 `reports/unity_ref/141_old_report_audit.md:21-25` · 裁定① `:14-20` · 裁定③ `:26-30`
  - 自查：全仓搜 `待接条件` = **0 处** ⇒ **该要求从未执行**；请裁「这一列由谁加、现在加还是等 M4」— `doc/windows/主程序窗口.txt:9194`
  - ✅ **策划已确认**：「**⑫ 我确认**：架构 `M2-ROUTE-B` 裁定 ② 的「待接条件」列 —— 你已实现（**7:17 → 10:14**）✓」— `doc/windows/主程序窗口.txt:9285`；同句留档 `reports/planner_20260926_to_lead_tier_and_source.md:87`

- **已量到的事实**
  - **实现本体**：改 `darkest/scripts/data/BuffPrimitiveTranslation.cs`（**只加不改**），新增 `PendingCondition(name)` + `PendingWithConditions`；三类条件 = `ExpeditionLayer` → 🔴 **缺载体** · 战斗级 6 种 → ⚠️ **未接线** · `UnitResistance` → ⚠️ **未接线**（落点 `units.json`，不走 buff 台账）— `reports/unity_ref/142_pending_condition_implemented.md:13-21`
  - 提交 **`e31a045`**（代码）· **`7710033`**（窗口更新）— `reports/unity_ref/142_pending_condition_implemented.md:4`
  - **不手工维护**：从 `ByStatType`/`Classify` 的去向**推出**（`where switch { … }`）⇒ 不与 `Activated`/`Pending` 漂移（一处本体 + 其余转发）— `reports/unity_ref/142_pending_condition_implemented.md:27-30`
  - 注释原文划界：「⚠️ **它【不是】『能不能接』的判决** —— 判决在策划/架构；本列只陈述**前置条件**」— `reports/unity_ref/142_pending_condition_implemented.md:31-34`
  - **独立复算**（`tools/dsh/verify_pending_conditions.py`，独立于 C# 实现）：**24 条 ⇒ 7 缺载体 + 17 未接线 = 24（覆盖完整）** — `reports/unity_ref/142_pending_condition_implemented.md:42-57`
  - **缺载体 7 条**（全 `ExpeditionLayer`）：`scouting_chance`·`monsters_surprise_chance`·`food_consumption_percent`·`starving_damage_percent`·`remove_negative_quirk_chance`·`party_surprise_chance`·`upgrade_discount` — `reports/unity_ref/142_pending_condition_implemented.md:47-50`
  - 对架构原判断的增量：架构当年说 **6 条**属趟级/城池级，实测 **7 条** ⇒ **多 1 条 = `upgrade_discount`** — `reports/unity_ref/142_pending_condition_implemented.md:63-69`
  - **零行为证据（三类）**：① `dotnet build Darkest.sln` ⇒ **0 错误 / 99 警告**（与基线同）② `dotnet test` ⇒ **失败 3 · 通过 843 · 总计 846**（与改动前完全一致）③ **`darkest/data/**` 零改动** — `reports/unity_ref/142_pending_condition_implemented.md:78-81`
  - 17 条落点**逐条核完**：**17/17 都有 `DestinationNote` 说明**，其中 **12 条明确点名战斗层** — `reports/unity_ref/143_unwired_landing_verified.md:27-41`
  - 🔴 **口径不齐（2 条存疑）**：`stress_heal_percent`/`stress_heal_received_percent` 的 note 说「士气恢复 ⇒ **城镇/扎营恢复**的修正轴」，而 `ByStatType` 把它们归 `Target.MoraleMod`（战斗级）⇒ **同一对名字的枚举去向与文字说明指向不同的层** — `reports/unity_ref/143_unwired_landing_verified.md:50-59`
  - 已查清答案：战斗侧 4 个符号**全 0**（`StressHealPercent`/`HealStress`/`IncreaseMorale`/`MoraleHeal`）；「士气恢复」实际在趟层 —— `ExpeditionSession.ApplyTeamMorale`（`darkest/scripts/gameplay/sim/run/ExpeditionSession.cs:153`）操作 `Retained`，两个调用点全在趟层（`ExpeditionFlow.RoomInteractions.cs:263` 的 `"curio"` · `ExpeditionSession.Traps.cs:57` 的 `"trap_disarmed"`），另有扎营士气 `GrantCampMorale`/`GrantCampMoraleTeam` ⇒ **落点是趟级，`DestinationNote` 的文字本来就对，错的是 `ByStatType` 归了 `MoraleMod`** — `doc/windows/主程序窗口.txt:9194`
  - **比的四次修正（同一数字改了 4 次）**：`142` = **7:17**（按 `Target` 枚举）→ `144` = **9:15** → `145` = **11:13** → `146` = **10:14** — `reports/unity_ref/146_morale_mod_fully_audited.md:54-59`
    - `146` 下调 1 条的理由：`MoraleMod` **6 条全核 ⇒ 2 战斗级（枚举对）: 4 趟级（枚举错）**；2 条战斗级 = `stress_dmg_percent`/`stress_dmg_received_percent`（落点 `MoraleLedger.Apply`，证据 `darkest/scripts/data/BuffDefsConfig.cs:233` 注释 + `DamagePipeline.cs:158` 调 `ApplyIncomingDamageMorale`）— `reports/unity_ref/146_morale_mod_fully_audited.md:32-37`、`:41-46`
  - **仍未决**：那 4 条**该改枚举还是拆维度** ⇒ **属架构** — `reports/unity_ref/146_morale_mod_fully_audited.md:95`
  - ⚠️ **这一列没有测试钉住**：与 `Activated`/`Pending` 的双向断言相比，**目前没有断言** ⇒ 待补；`PendingWithConditions` 只提供 API、**未接进报表/用例打印** — `reports/unity_ref/142_pending_condition_implemented.md:98-104`
  - 裁定① 现状：**取 (乙) 先做**（不现在造载体）；裁定③ 转 (甲) 的条件 = **当 M4（Trinket）真的需要趟级修正时**，而 **M4 未落 ⇒ 条件尚未满足** — `reports/unity_ref/141_old_report_audit.md:14-20`、`:100-102`
  - 架构原话（留档）：「**`dormant-by-data`（数据一改就生效）是合法的；`dormant-by-structure`（没人用的结构）不是**」— `reports/unity_ref/141_old_report_audit.md:26-30`

- **候选选项**：**本列本身没有选项**（它是「陈述事实」的一列，已实现并被策划确认）。唯一带标签的选项属**裁定①**：**(甲) 现在造 `RunModifiers`** ／ **(乙) 先不做、留在冻结清单等 M4 需求** —— 架构**取 (乙)**，**读数里没有 (丙)** — `reports/unity_ref/141_old_report_audit.md:14`

- **一手/参考项目怎么说**：**读数里没有** —— `DestinationNote` 是**我方**的原文要点，读数没说是参考项目/一手 E 盘的内容（`reports/unity_ref/143_unwired_landing_verified.md:28-40`）。

- **代价/风险**
  - 已实现且**零行为**（build 0 错/99 警告 · 全量 843/846 与基线一致 · `darkest/data/**` 零改动）— `reports/unity_ref/142_pending_condition_implemented.md:78-81`
  - 这一列**没有测试** ⇒ 会被改坏而无人知 — `reports/unity_ref/142_pending_condition_implemented.md:99-102`
  - 17 条分类是**按 `Target` 枚举推的**，不是逐个查落点 ⇒ 记**推断** — `reports/unity_ref/142_pending_condition_implemented.md:93-95`
  - 改 `ByStatType` **会影响 `Tally` 读数 ⇒ 要报** — `reports/unity_ref/144_stress_heal_is_run_layer.md:84`
  - 归属：那两处文件属 `scripts/data` ⇒ **主程序域**，可做但**要先报** — `reports/unity_ref/141_old_report_audit.md:96-99`

- **我的建议**：**建议把「7:17 → **10:14**」读成「**10 缺载体 / 14 未接线（待架构确认 `Target` 枚举要不要拆成『轴 × 层』）**」，并优先做两件：**① 给这一列补测试 ② 逐个查那 17 条的真落点** —— 理由：四个数字依次 7:17 → 9:15 → 11:13 → 10:14，修正方向暴露的正是「枚举混了轴与层」这个根本问题（与项 ⑬ 同源）。🔴 **但这是建议，不是裁定。**

---

## 13. 项 ⑬ — `Target` 枚举混了「轴/层」两维度

- **问的是什么**：`doc/windows/策划窗口.txt:38-39`「⑬ 🔴 **`Target` 枚举混了「轴/层」两维度** —— 🎖️ **已核完全量：21 条里 4 条不符（19%）**，给两条修法 **(甲) 拆维度 / (乙) 补 1 个取值** ⇒ **4/21 下两条都可接受，按成本选**」；策划已划入「**可直接拍**」— `doc/windows/主程序窗口.txt:9286`

- **已量到的事实**
  - 枚举本体（**9 个取值**）：`public enum Target` @ `darkest/scripts/data/BuffPrimitiveTranslation.cs:25`；取值 = `DamageMod`(`:28`)·`MoraleMod`(`:31`)·`HealMod`(`:34`)·`ProbMod`(`:37`)·`StatMod`(`:40`)·`StateFlag`(`:43`)·`UnitResistance`(`:46`)·`ExpeditionLayer`(`:52`)·`Frozen`(`:55`)
  - **8 个按【轴】· 只有 `ExpeditionLayer` 按【层】**（定义原文为证：`MoraleMod` 写「士气（压力）**轴**」、`HealMod` 写「**治疗轴**」…）；🔴 `ProbMod` 定义里还兼提「**战斗内**」⇒ **第 2 处混用** — `reports/unity_ref/147_target_enum_axes.md:12-25`
  - `Frozen` 不是层也不是轴 ⇒ **第三态**（可单列）— `reports/unity_ref/147_target_enum_axes.md:73`
  - 9 个取值下的原语数：`ExpeditionLayer` **7** · `MoraleMod` **6**（🔴 **其中 4 条实际是趟级**）· `ProbMod` **5** · `HealMod` **3** · `DamageMod` **1** · `UnitResistance` **1** · `StatMod`/`StateFlag`/`Frozen` **各 0** ⇒ **合计 23**（＋ `Classify` 特判 2 条 = **25**）— `reports/unity_ref/147_target_enum_axes.md:45-56`
  - 源码事实：`ByStatType` 实 **23** 条目 — `darkest/scripts/data/BuffPrimitiveTranslation.cs:62-93`；`Activated` = **1** 条（`hp_heal_percent`）— `:160`；`Pending` = **24** 条 — `:163`、`:168-177`；测试硬断言 `Activated==1` / `Pending==24` — `darkest/tests/BuffPrimitivesTests.cs:220-221`
  - **4 条不符的具体名单**（`MoraleMod` 里的趟级项）：`stress_heal_percent` → `ExpeditionSession.ApplyTeamMorale`（趟）· `stress_heal_received_percent` → 同上 · `resolve_check_percent` → `ExplorationActOut.IsAfflicted`（趟）· `resolve_xp_bonus_percent` → `Roster.AwardExperienceForBattle`（趟）— `reports/unity_ref/146_morale_mod_fully_audited.md:34-37`
  - 同时**枚举对**的 2 条：`stress_dmg_percent`/`stress_dmg_received_percent` → `MoraleLedger.Apply`（战斗）— `reports/unity_ref/146_morale_mod_fully_audited.md:32-33`
  - **19% 出处**：`MoraleMod` 的 4 条**不符** · 其余 **10 条相符**（枚举战斗、落点战斗）· `ExpeditionLayer` 的 **7 条相符** ⇒ **21 条里 4 条不符（19%）** — `reports/unity_ref/148_remaining_layers.md:44-50`
  - 其余 10 条的消费侧实测（**全 0**；`combat_stat_add`/`combat_stat_multiply` 各 **1**，**都在 `BuffPrimitivesConfig.cs` 的注释里**）— `reports/unity_ref/148_remaining_layers.md:11-19`
  - 按语义找落点 ⇒ **其余 10 条全在战斗侧**：`resistance`→`UnitStats`（`darkest/scripts/gameplay/sim/units/UnitStats.cs:19-21`）· `*_chance` ×5 → `BattleMath.ActualEffectChance`（调用点 `EffectsStep.cs:47`）· `hp_heal_*` ×3 → `HealAmount.Scale`（`SkillExecutor.cs:367`）· `damage_received_percent` → `DamageStep` 的 `raw` 层（仅 note）· `combat_stat_*` ×2 → `BuffDefsConfig` 按名分发 — `reports/unity_ref/148_remaining_layers.md:28-38`
  - **根因**：不是填错，是**枚举表达不了**（枚举给的是「轴」，「层」的信息只在一个取值里 ⇒ 「轴=士气、层=趟级」的原语**只能塞进 `MoraleMod`**）— `reports/unity_ref/147_target_enum_axes.md:30-40`
  - 误判机制在代码里可见：`PendingCondition` 只按 `Classify` 的去向分三类（战斗级 6 种 ⇒「未接线」· `UnitResistance` ⇒「未接线」· `ExpeditionLayer` ⇒「缺载体」· 其余落 `_ => "🔴 去向未定（Frozen）"`）— `darkest/scripts/data/BuffPrimitiveTranslation.cs:191-207`
  - 🎖️ **不是普遍误判**，是 `MoraleMod` 一类独有 — `reports/unity_ref/148_remaining_layers.md:41-55`
  - **修正链**：`142` **7:17** → `144` **9:15** → `145` **11:13** → `146` **10:14**（策划回执引的是中间值「7:17 → 10:14」）— `reports/unity_ref/146_morale_mod_fully_audited.md:54-59`；`doc/windows/主程序窗口.txt:9285`
  - ⚠️ **矛盾/口径未对齐**：`148` 的分母是 **21**（4 + 10 + 7）；`142`/`146` 的分母是 **24**（`7+17=24`、`10:14` = 24）；源码 `Pending` 实为 **24** 条 ⇒ **21 与 24 的 3 条差额，读数里没有交代 ⇒ 记「未量」**
  - **(甲) 的具体方案**（`147 §4`）：两个维度各自独立 —— 轴（原枚举保留）= `Damage`·`Morale`·`Heal`·`Prob`·`Stat`·`State`·`Resistance`；层（新增）= `Battle`·`Expedition`·`Town`·`UnitConfig`·`Frozen` ⇒ 9 个取值重组成 `(轴, 层)` 二元组 ⇒ **9 个变 ~25 种组合**；`ExpeditionLayer` 现有 7 条 ⇒ `(_, Expedition)`；`Frozen` 单列；**方案覆盖 9/9** — `reports/unity_ref/147_target_enum_axes.md:66-77`
  - 成本原话：「拆维度成本高（动枚举 + `Tally` + `PendingCondition` + 测试），而**补特例只动 1 处**」— `reports/unity_ref/148_remaining_layers.md:69-70`

- **候选选项**（标签原样）
  - **(甲) 拆维度**（根治，但动 9 个取值）— `reports/unity_ref/148_remaining_layers.md:65`
  - **(乙) 只补 1 个取值**（如 `ExpeditionMorale`）⇒ 治 4 条、**不动结构** — `reports/unity_ref/148_remaining_layers.md:66`
  - **无第三个选项**（144~148 + 窗口里未出现 (丙)/(C)）
  - 选择判据：「是【根治】还是【补特例】？⇒ 按【误判占比】选（**4/21 ⇒ 特例也可接受**）」— `reports/unity_ref/148_remaining_layers.md:67-68`

- **一手/参考项目怎么说**：**读数里没有** —— 该枚举是我方「翻译去向」分类器（判据源是参考的 1801 条 / 25 个 `stat_type`）（`darkest/scripts/data/BuffPrimitiveTranslation.cs:14-17`）。参考侧**目标语义**是**另一个东西**（勿混）：`.target` 编码 = 前缀 `@`(己方)/`~`(多目标)/`?`(随机) + 逐字符即 rank，末尾 `Sort()` ⇒ `@4321` ≡ `@1234`（`reports/unity_ref/119_target_encoding.md:19-30`）；参考只有 **3 种 `SkillTargetType`**（`reports/unity_ref/120_target_mapping_table.md:63-66`）。**一手 E 盘：读数里没有**。

- **代价/风险**
  - (甲)：动枚举 + `Tally` + `PendingCondition` + 测试；且**改它会影响 `Tally` 读数** — `reports/unity_ref/148_remaining_layers.md:69`；`reports/unity_ref/144_stress_heal_is_run_layer.md:84`。受影响断言：`Tally` 读 `Target.Frozen`（`darkest/tests/BuffPrimitivesTests.cs:240-241`）· `ExpeditionLayer` 计数（`:291-293`）· `PendingWithConditions` 条数 = `Pending.Count`（`:268-270`）
  - (乙)：读数说**只动 1 处**；源码落点 = `ByStatType` 的 **4 行**（`darkest/scripts/data/BuffPrimitiveTranslation.cs:71-74`）— `reports/unity_ref/148_remaining_layers.md:70`
  - **未定**：「拆分后 `PendingCondition` 该怎么写」⇒ 要先定结构 — `reports/unity_ref/147_target_enum_axes.md:92`
  - **含判断**：「轴」与「层」的判定部分依据的是**定义原文里的词**，`ProbMod` 那处模糊 ⇒ 读数自称「含判断」— `reports/unity_ref/147_target_enum_axes.md:87-90`
  - **未量**：`21` vs `24` 的 3 条差额（上文矛盾）

- **我的建议**：**建议取 (乙) 补 1 个取值**（如 `ExpeditionMorale`）—— 只治那 4 条、不动 9 个取值与 `Tally` 口径；**(甲) 拆维度留到 M2 原语层定型时一并做**（那时本就要动 `Tally`/清单）。🔴 理由：读数自己写明「**4/21 下两条都可接受，按成本选**」（`reports/unity_ref/148_remaining_layers.md:68`），且 (甲) 会同时改 `Tally` 读数与多条现成断言。🔴 **但这是建议，不是裁定；且裁定前建议先补齐「21 vs 24」的分母差额。**

---

## 14. 读数之间的矛盾清单（按规则 4 并列，不掩）

| # | 项 | 矛盾内容 | 两处引用 |
|---|---|---|---|
| 1 | ② | Effect 表**字段数**：**65 种** vs **69 字段**（后者含修回 4 个驼峰字段） | `reports/unity_ref/44_effects_extracted.md:22` ／ `reports/unity_ref/PLAN_adoption.md:203`、`reports/unity_ref/92_effects_downstream.md:52-55` |
| 2 | ② | Effect 消费**口径**：754 /**190**（`754+190=944≠952`）vs 754/**198**/8/190（自检通过） | `reports/unity_ref/45_effects_consumption.md:32-33` ／ `reports/unity_ref/46_effects_two_criteria.md:27-31`（46 自称更正 45） |
| 3 | ② | act-out 回合开始**消费点**：「14/14 全是真分支」vs「只有 11 个 case · 3 个无 case」 | `reports/unity_ref/31_actout_turn_consumption.md:11-14` ／ `reports/unity_ref/32_actout_switch_detail.md:12-17`（32 自称订正 31） |
| 4 | ② | act-out **分档**：A27/B2 vs A24/B2/C2/D1 | `reports/unity_ref/31_actout_turn_consumption.md:45-49` ／ `reports/unity_ref/32_actout_switch_detail.md:104-107` |
| 5 | ② | 「A6 三件」**两套口径**：Effect 表/act-out 表/`queue` 语义 vs 7 条去留/`chance` 归一/形状收敛 | `doc/windows/策划窗口.txt:26` ／ `doc/windows/主程序窗口.txt:8294-8299` |
| 6 | ③ | 总口径三选一的**前提已废**：选项建立在「数值一律采用参考」上，而用户已裁「一手优先、E 盘读不到才用参考」 | `reports/planner_request_a9_a10_a11.md:35-44` ／ `reports/planner_20260926_to_lead_tier_and_source.md:29-30`、`doc/windows/主程序窗口.txt:9354-9355` |
| 7 | ④ | `PartyNames.json` **文件大小** 20,748 B vs 21497 B；**条数** 186 vs XML 187 key | `reports/unity_ref/15_quests_loot_narration_from_ref.md:16` ／ `reports/unity_ref/01_data_inventory.md:1307`、`:3957` |
| 8 | ⑤ | `#443` 已把「退出惩罚」列入 Quest「全选」，而本件又问「失败惩罚**要不要做**」⇒ 先后关系未说清 | `doc/modules/dd1_baseline.md:1441` ／ `doc/windows/主程序窗口.txt:8752` |
| 9 | ⑥ | **判据源**不同结论：一手 = **全 4 种**，参考 = **每地牢 2 种**；而 `22_*.md` 的建议基于已废前提 | `doc/windows/主程序窗口.txt:7410-7411` ／ `reports/unity_ref/22_generation_tables.md:93-95`、`:103` |
| 10 | ⑥ | ⑥ 曾被 `#470` 系列裁过（「每趟 4 种都给 + `placeholder` + `O11`」），与本轮 (甲)/(乙) 的关系未说明 | `doc/windows/主程序窗口.txt:7655-7659`、`:7823` ／ `doc/windows/主程序窗口.txt:8836` |
| 11 | ⑦ | 同任务内两件结论相反：「不是矛盾，是两个不同机制」vs「挂点相同、方向相反 ⇒ 是分歧」 | `reports/unity_ref/25_light_tiers_match.md:79-83` ／ `reports/unity_ref/26_light_out_and_darkness_bonus.md:56-61` |
| 12 | ⑦ | 旧读数已被自己推翻：`darkness = 100 − light` 得「只有头尾对得上」⇒ 判定「算错了方向」 | `reports/unity_ref/_probe/light_compare2.py:41-53` ／ `reports/unity_ref/25_light_tiers_match.md:27-45` |
| 13 | ⑧ | **全量测试基线**：843/846（3 红）vs 846→842（四红） | `reports/unity_ref/PLAN_adoption.md:291-293` ／ `doc/windows/主程序窗口.txt:8181` |
| 14 | ⑧ | **取整计数**（同一事实两种扫描）：`Floor 14 / Math.Round 2` vs `Math.Round 10 / FloorToInt 3`、并多出 `(int) 142` | `reports/unity_ref/41_our_rounding_vs_ref.md:16-17` ／ `reports/unity_ref/105_rounding_audit_all.md:13-17` |
| 15 | ⑧ | **推进节奏两说**：(丙) 现在做、(甲)(乙) 入 `§39` vs 「可一次拍到底」 | `doc/windows/主程序窗口.txt:9099-9100` ／ `doc/windows/主程序窗口.txt:9193` |
| 16 | ⑧ | 旧读数「两种取整顺序恰好等价」已被穷举推翻（900/2001 点不同） | `reports/unity_ref/39_damage_rounding_chain.md:44-49` ／ `reports/unity_ref/40_rounding_exhaustive.md:13-15` |
| 17 | ⑨ | `55_*.md` 的「两维互补/或」推断被 `56_*.md` 推翻；`55` 措辞隐含参考「有机制」而 `57_*.md` 证明没有 | `reports/unity_ref/55_unlock_reconcile.md:37-40`、`:60-61` ／ `reports/unity_ref/56_unlock_fields_dead.md:39-42`、`reports/unity_ref/57_no_building_unlock.md:8-19` |
| 18 | ⑨ | 阈值能否照抄：`55` 说可照抄 vs `56` 说不能（参考没在用它们） | `reports/unity_ref/55_unlock_reconcile.md:84-86` ／ `reports/unity_ref/56_unlock_fields_dead.md:57` |
| 19 | ⑫/⑬ | 「缺载体:未接线」**四个数**并存：7:17 → 9:15 → 11:13 → 10:14；窗口只记首末 | `reports/unity_ref/146_morale_mod_fully_audited.md:54-59` ／ `doc/windows/主程序窗口.txt:9285` |
| 20 | ⑬ | **分母** 21 vs 24（源码 `Pending` 实为 24）⇒ 3 条差额**未量** | `reports/unity_ref/148_remaining_layers.md:44-50` ／ `reports/unity_ref/142_pending_condition_implemented.md:57`、`darkest/scripts/data/BuffPrimitiveTranslation.cs:163` |

---

## 15. 读数不足以直接判的项（建议先补读数）

| 项 | 缺什么 | 出处 |
|---|---|---|
| ⑤ | 🔴 **一手 `campaign/quest/exit_penalty`（126 B）的【内容】未读** —— 现在「要不要做/做什么」的读数**全部来自参考侧**，而参考**没有实现** | `doc/windows/主程序窗口.txt:5931`；`reports/unity_ref/20_quest_field_consumption.md:80-89` |
| ⑤ | `#443`「退出惩罚已入全选」与本问「要不要做」的**先后关系**未说清 | `doc/modules/dd1_baseline.md:1441` vs `doc/windows/主程序窗口.txt:8752` |
| ⑨ | 删/改 `unlocks.json` 的**完整改动清单与前后读数未量**（只知 grep 触及 8 个代码文件 + 6 个测试文件 + 契约 `P27`） | `doc/architecture/data_schema.md:774`；`doc/architecture/tasks/next_round.md:15-16` |
| ⑨ | **读数里没有编号选项**；且参考侧三字段是死数据（照抄无来源） | `reports/unity_ref/57_no_building_unlock.md:97`；`reports/unity_ref/56_unlock_fields_dead.md:15-18` |
| ⑩ | 🔴 **`coefficient 0.6/0.7` 与 `target_hp_below_percent: 50` 的依据完全未量**（只有 2 个样本，无参考可对） | `reports/unity_ref/114_missing_hp_ownership.md:96-97`；`reports/unity_ref/113_missing_hp_no_ref.md:86-87` |
| ⑩ | `base: 1.0` 是常量还是配置（2 条都是 1.0 ⇒ 样本不足） | `reports/unity_ref/115_damage_shape_audit.md:74-75` |
| ② | 两套「三件」口径并存；`65` vs `69` 字段；`754/198/8/190` 与 `754/190` 两组消费口径 | §14 #1/#2/#5 |
| ③ | **改 99 处成本后的经济后果完全未测**；参考为何被改/谁改的**不能验** | `reports/planner_request_a9_a10_a11.md:117-118` |
| ② | `queue` 的 `EffectBoolParams.Queue` **解析点未读**；**哪些子类覆盖 `Fusable`/`Fuse` 未查**；**跨 effect 的相邻事件会不会 fuse 未查** | `reports/unity_ref/100_event_queue.md:119`、`:123`；`reports/unity_ref/102_fuse_not_triggered.md:82-86` |
| ⑦ | (乙)/(丙) 的**改动文件清单与改动包未量**；改成参考方向后经济后果**未测** | `reports/unity_ref/26_light_out_and_darkness_bonus.md:84` |
| ⑪ | `SpiritHit` 的**消费落点 file:line 未量**；`morale_events.json` **完整条数未数** | `reports/unity_ref/128_morale_axis_decoupled.md:43`；`reports/unity_ref/129_morale_table_matches.md:82-85` |
| ④ | `20,748 vs 21497 B`、`187 vs 186` 两处矛盾读数未解释；差集未量 | §14 #7 |
| ⑬ | `21 vs 24` 分母差额**未量** | §14 #20 |
| ① | 新 A1 门槛的**完整读数未量**（`≤0.55/≤0.15` 只是示例带） | `reports/planner_request_sigma_vs_a1.md:84`、`:108` |

---

## 16. 本次研究的边界（诚实交代）

- **只读**：未修改任何既有文件；唯一新增 = 本文档 `reports/planner_rulings_input_20260926.md`。
- **一手 E 盘未直接读取**：所有「一手」结论均**转引读数原文**并标注了出处行号；其中 ⑤ 的一手 `exit_penalty` **有文件但内容无人读过**（`doc/windows/主程序窗口.txt:5931`）⇒ 记为**未量**。
- **不替策划裁定**：全文「我的建议」都是**建议**；未对任何项下最终结论。
- **未逐条复核**：`reports/unity_ref/` 共 100+ 份读数，本汇总以**13 项相关**的读数为主；与 13 项无关的读数未通读。
