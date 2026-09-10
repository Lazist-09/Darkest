# 功能包 01：强制目标点击 + 角色可插拔 + 士气 buff 效果 任务卡

> **编号**：ARCH-T-FEAT-PACK-01 · **类型**：task · **状态**：可开工
> **上游**：[设计] `doc/modules/ui_spec.md` §4 · `doc/modules/skill.md` §1/§1.1 · `doc/modules/skill_data.md` §4.5 · `doc/modules/character.md` §7.1b · `doc/modules/enemy.md` §2.1/§5.3/§5.4 · `doc/modules/morale.md` §4.1/§5/§6 · `doc/modules/buff.md`
> **决策引用**：#189, #190, #191, #192, #193, #194
> **开放项**：O-49（强制点击）/ O-50（可插拔 + 通用 move）/ O-51（士气 buff 效果）/ O-52（超时增援偏差）/ **O-27 本包定值** —— ⚠️ **编号已由架构师确认采用**（v0.48 转写，见 `open_issues.md` O-49~O-52）；字段口径另见 **O-53**（嘲讽 self 化）。
> **依赖**：T-M0~T-M5；与 `m6_fix_pack.md` **互不冲突，可并行**（本包不碰胜负判定/敌人目标偏好/模拟策略）
> ⚠️ **顺序约束**：本包 **F0~F3 必须在 `m6_fix_pack.md` P3（复测 M6-08 v2）之前落地**——
> 它们全都改变实际输出/压力（点击强制改变操作口径、可插拔改移动技能数据、士气 buff 改变战斗结果、增援改变敌方压力）。
> 复测若发生在这些改动之前，基线会**再次作废**（这是本项目已踩 4 次的坑）。
> **最近更新**：2026-09-09

---

## 0. 目标与范围

| 子包 | 要解决的问题 | 用户原话 |
|---|---|---|
| **F0** | 现在有 5 类技能"无点击直接执行"，玩家没有选择动作 | 「所有技能不论对象是几个人（除非空）都有一个选择对象的点击，不能有玩家没有选择直接跳过的情况」 |
| **F1** | 新增一个角色要改 5~6 个代码文件；"移动"与"嘲讽"结构性写死 | 「希望角色是那种可插拔的，位置可以放任意角色，便于后续增加新角色」 |
| **F2** | 士气 buff 有骨架无肌肉：折磨/美德的效果**零消费者** | 「现在没有实现士气相关的 buff 系统」 |

---

# F0 · 强制目标点击（#189）

## F0.1 现状缺陷（代码依据）

`BattleRoot.DoUseSkill:134-143`：

```csharp
bool singleton = skill.Damage is not null && !aoe || skill.Damage is null && scope == AnyAlly;
if (singleton && candidates.Length > 1) { _pendingSkill = skillId; return; }   // 进入选目标
ExecutePlayerSkill(actor, skillId, null);                                      // ← 否则直接结算
```

**无点击直接结算的情形**（5 类）：

| 情形 | 例子 |
|---|---|
| 单体技能候选只有 1 个 | 只剩一个敌人时的劈砍 |
| **所有 AOE** | 横扫、精神震荡 |
| **所有 team** | 战吼、动员令、群体绷带 |
| **所有 self** | 铁壁、坚守、喘息 |
| any_ally 候选只有 1 个 | 急救、战场鼓舞 |

## F0.2 规格

**统一操作流：点技能 → 点目标卡 → 结算。无例外。**

```
点技能（候选池 = SkillTargetResolver.Resolve 的结果）
  ├─ 候选池为空 → 技能灰显，不可点（既有）
  └─ 候选池非空 → 【一律】进入选目标：高亮整个候选池，等待玩家点选
        点击池内任意一张卡 → 结算
        点击池外卡 → 提示"该目标不在候选中"，不结算（既有）
        取消（再点技能/Esc/右键）→ 退出，不消耗行动
```

**各 scope 的"点哪张卡"语义**：

| scope | 候选池 | 点击语义 |
|---|---|---|
| `slots`（单体） | 范围内非空位 | 点中的那个就是唯一目标 |
| `slots` + `aoe` | 范围内非空位 | **点池内任意一张 = 确认施放**，命中**整池**（UI 高亮整池） |
| `any_ally` | 我方非空位 | 点中的那个就是唯一目标 |
| `team` | 我方全部非空位 | 点任意友方 = 确认，**全队**生效 |
| `self` | 自身所在位 | 点**自己的卡** = 确认 |
| `adjacent_ally_and_self` | 自身 + 相邻友方 | 点池内任意一张 = 确认，**整池**生效 |
| `move_range` | 自身 ±N 内被占战斗位 | 点中的那个 = 交换对象（既有） |

## F0.3 改动点

| 文件 | 改动 |
|---|---|
| `darkest/scripts/gameplay/scene/BattleRoot.cs` | `DoUseSkill` 去掉 `singleton && candidates.Length > 1` → **`if (candidates.Length > 0)` 一律进入选目标**；`PendingCandidates` 已按 scope 解析，直接复用 |
| `darkest/scripts/ui/BattleUi.cs` | 选目标阶段高亮**整个候选池**（含 AOE/team）；明示"请选择目标"；`IsTargeting` 状态下鼠标交互 |
| `darkest/tests/**` | 新增 `TargetClickFlowTests` |

> `SkillExecutor` **无需改动**：非 singleton 分支本就忽略 `chosenTargets` 而用全池（`SkillExecutor.cs:71`），所以 AOE/team 点击后仍命中整池。

## F0.4 验收

| 用例 | 期望 |
|---|---|
| 单体技能、候选 **1** 个 | **必须点击**才结算；不点击永不结算 |
| AOE（横扫/精神震荡） | 必须点击；点击后命中**整池** |
| team（战吼） | 必须点击任意友方；全队生效 |
| self（铁壁） | 必须点击自己的卡 |
| any_ally、候选 1 个 | 必须点击 |
| 候选池为空 | 灰显不可点（不进选目标） |
| 点击非候选卡 | 提示，不结算，仍在选目标 |
| 取消选目标 | 不消耗行动、技能可重选 |

---

# F1 · 角色可插拔（#190）

## F1.1 现状：新增角色要改 5~6 个代码文件

> ✅ **好消息**：`formation.json` 的槽位是**自由映射**（现在 5 号位放战士、6 号位放军医），
> **"任意角色放任意位置"在数据层已经成立**，位置没有锁角色。真正的阻塞是下面的硬编码。

| 位置 | 硬编码 |
|---|---|
| `UnitsConfig.cs:38` | 玩家原型白名单 `{warrior, tank, medic, commissar}` |
| `SkillsConfig.cs:194` | `owner_unit` 白名单（7 个原型枚举） |
| `SkillsConfig.cs:245` | `IsPlayerArchetype` 写死 4 个 |
| `SkillsConfig.cs:305-308` | 校验**按原型名分别计数** `player[0..3]`（第 5 个角色直接漏算） |
| `SkillsConfig.cs:326/331/366` | **`*_move` 命名约定**（池外技能必须叫 `X_move`） |
| `BattleRoot.cs:182`、`BattleUi.cs:433/444` | 拼接 `$"{archetype}_move"`、按后缀排除移动 |
| `BattleUi.cs:292-295` | 4 个原型颜色写死 |
| `EnemyAi.cs:181` | `ArchetypeId == "tank"`（嘲讽识别写死坦克） |

## F1.2 规格：新增一个角色 = **只改数据，零代码改动**

**数据清单（3 处）**：

```
① units.json   加一条：id / name / side:"player" / 12 项属性 / skills(9 条 id) / move_distance
② skills.json  加该角色的 9 条池内技能（owner_unit = 新 id）
③ formation.json  initial_roster 里把该角色放进任意槽（1~6 任意）
→ 不改任何代码
```

**代码去硬编码**：

| 位置 | 改为 |
|---|---|
| `UnitsConfig.cs:38` | 删除白名单；**玩家原型 = `units.json` 中 `side == "player"` 的 id 集合** |
| `SkillsConfig.cs:194` | `owner_unit` 校验 = **必须存在于 `units.json`**（不再枚举） |
| `SkillsConfig.cs:245` | `IsPlayerArchetype` = 查 `units.json` 的 `side` |
| `SkillsConfig.cs:305-308` | 池内技能计数改为**按数据分组**：`∀ player unit: 池内技能数 == 9`（不写死 4 个键） |
| `SkillsConfig.cs:326/331/366` 等 | **删除 `_move` 命名约定** → 改为按 **`pool_external` 标志**过滤；移动技能 id 用**常量 `"move"`** |
| `BattleUi.cs:292-295` | 颜色改为**配置表查表 + 查不到用默认色**（新角色不会崩/不显示） |
| `EnemyAi.cs:181` | 嘲讽识别改为"**我方带 `taunt` buff 者**"（见 F1.4） |

## F1.3 移动技能数据化（#191）

**删除 4 条 `*_move`，改为 1 条通用 `move` + 单位属性**：

| 项 | 规格 |
|---|---|
| `skills.json` | **删除** `warrior_move` / `tank_move` / `medic_move` / `commissar_move`；**新增 1 条通用技能** `id: "move"`，`pool_external: true`，**不绑 `owner_unit`**，`self_slots: [1,2,3,4]`，`target: { scope: "move_range" }` |
| `units.json` | 每单位新增 **`move_distance`**：坦克 **1** / 战士 **2** / 军医 **2** / 政委 **2** |
| `TargetSpec.distance` | **技能不再写 distance**——`move_range` 的距离**从施法单位的 `move_distance` 读** |
| 技能总数 | 47 → **44**（36 池内 + **1 通用移动** + 7 敌方） |
| 校验 | `pool_external` 技能数 == **1**；`∀ player unit: move_distance >= 1` |

> **命名与过滤**：一律用 **`pool_external` 布尔标志**判断池外技能，**禁止**再用 `id.EndsWith("_move")`。

## F1.4 嘲讽改为挂在嘲讽者身上（#192）

**现状问题**：`tank_taunt` 把 `taunt` buff 打在**敌人**身上（target = enemy slots [1,2]），
再由 `EnemyAi` 去 `FirstOrDefault(u => u.ArchetypeId == "tank")` **猜谁是嘲讽者**——
新增一个会嘲讽的角色就完全失效。

**改为**：

| 项 | 规格 |
|---|---|
| `skills.json` `tank_taunt` | `target`：`{scope: slots, side: enemy, slots: [1,2]}` → **`{scope: "self"}`**（给自己上嘲讽）；效果 `taunt` 2 回合、CD 3 不变 |
| `buff_defs.json` `taunt` | `polarity`：`negative` → **`positive`**（挂在我方嘲讽者身上，是对我方有利的状态）；hook 文案 → `enemy weighted prefers attacker (taunt holder)` |
| `EnemyAi.ResolveTargets` | **删除 `ArchetypeId == "tank"`**；改为遍历**我方单位**找 `buffs.Has(id, "taunt")` 者；每个嘲讽者权重 **×`taunt_weight`**（#187 起手 3） |
| UI | 嘲讽现在是 **self 技能** → 按 F0 需**点击自己的卡**确认 |

**边界**：

| 情形 | 行为 |
|---|---|
| 多个嘲讽者并存 | 各自 ×3 权重（不叠加成一个） |
| 嘲讽者在候选池外 | 忽略该嘲讽者 |
| 无人嘲讽 | 走原型偏好（`enemy.md` §2.1 规则②） |
| 嘲讽者是池内唯一候选 | 100%，不掷骰（沿用 #187 边界） |

**⚠️ 连带需要重新审视的字段（改 self 后语义变了）**：

| 字段 | 现状 | 改 self 后 | **架构裁定（O-53，已定）** |
|---|---|---|---|
| `target` | `{slots, enemy, [1,2]}` | `{scope: self}` | ✅ `{scope:"self"}` |
| `hit_mod` | 0 | **self 技能无命中判定** | ✅ **置 0，注明"占位、引擎不读"**——`self`/`team`/`adjacent` 类技能一律不掷命中骰 |
| `range_axis` | `melee` | **self 技能无距离轴** | ✅ 改 **`none`**（**不保留 `melee`**：避免 UI/AI 按"近战"筛选或按距离轴做可用性判断时误判） |
| `tags` | `[control]` | 保留 `control`（语义仍是控制敌方目标选择） | ✅ 保留 `["control"]` |
| `damage_axis` | `none` | 不变 | ✅ 不变 |
| 可用站位 `self_slots` | `[1,2]` | 不变（坦克仍须在前排才能嘲讽） | ✅ 不变 |
| **极性 / 驱散** | `polarity: negative`、`dispellable: true` | 现在是**我方身上的正面状态** | ✅ **`polarity:"positive"` + `dispellable:false`**（按 `buff.md` §5.1「正面不可驱散」；切片无敌方驱散技能，未来若引入敌方驱散再评估——本条标"可推翻"） |
| 效果时长 / CD | `rounds:2` / CD 3 | 不变 | ✅ 不变 |
| hook 文案 | `enemy prefers attacker (tank)` | — | ✅ `enemy weighted prefers attacker (taunt holder)`（权重读 `enemy_ai.taunt_weight`） |

> **裁定要点（写入 data_schema §3.2 字段约定 + §3.4 taunt 行 + P13）**：`hit_mod`/`range_axis` 在 self 类技能上是**占位字段**——保留键位以满足 13 字段模板与数据校验，但引擎不读取；这样"数据形状统一、语义不歧义"。

> **语义收益**：任何角色只要有"嘲讽"技能且自身带 taunt buff 就能嘲讽——**天然可插拔**，且不需要 Buff 台账记录施加者来源。

## F1.5 验收

| 用例 | 期望 |
|---|---|
| **零代码新增角色**（夹具） | 只加 `units.json` + `skills.json` + `formation.json` → **编译与运行全绿**，新角色可选技能、可移动、可被 增援/移动 |
| 第 5 个角色 | 技能计数校验**不越界、不漏算**（按数据分组） |
| 通用 `move` | 4 个现有原型距离分别为 1/2/2/2；`move_range` 候选 = 自身 ±move_distance 内被占战斗位 |
| 删除 `_move` 后端 | 池外技能过滤仍正确（按 `pool_external`，非 id 后缀） |
| 嘲讽可插拔 | **给新角色加一个嘲讽技能** → 敌人同样会优先打他（不再依赖 `"tank"`） |
| UI 颜色 | 新角色（无配色）→ 默认色，不崩 |
| 值校验 | `pool_external` 数 == 1；`move_distance ≥ 1`；`owner_unit` 均存在于 units.json |

---

# F2 · 士气 buff 效果接入（#193）

## F2.1 现状：骨架全在，肌肉全缺

| 部分 | 状态 |
|---|---|
| 崩溃判定 33% → 美德/折磨 | ✅ |
| 折磨/美德 buff 授予与台账 | ✅ |
| 折磨 −10% 死门抗性 | ✅（走**专用参数**，非 buff 系统） |
| 美德清除（士气再降 0） | ✅ |
| 崩溃余烬标记 | ✅（仅标记，无玩法效果） |
| **折磨 proc**（恐惧拒绝技能 / 自私拒绝治疗 / 失控随机换目标） | ❌ **零消费者**（`affliction_proc_percent: 33` 没人读） |
| **美德战斗效果** | ❌ 零消费者 |
| buff `modifiers` 通用消费 | ❌ `StatModTotal` **零调用者** |
| 属性减益（攻/防/韧/速） | ✅ 生效（但走 `UnitRuntime` 的 Mod 字段，**绕过** buff 系统） |

## F2.2 两个通用执行器（必须先做，否则上面两条只能各写一份硬编码）

**① `prob_mod` 执行器**：在指定时机按 `percent` 掷骰 → 触发特殊结果。**必须写 `RngDraw`**。

**② `modifiers` 通用消费**——把 buff 的 `modifiers` 接进既有数学：

| modifier | 接进哪里 |
|---|---|
| `damage_mod.dealt_damage_mult`（+25%） | `DamageStep` 伤害计算（乘法阶段） |
| `damage_mod.next_attack_mult`（+20%） | `DamageStep`（消耗后移除） |
| `prob_mod.crit_bonus`（+15%） | `HitStep` / 暴击判定 |
| `prob_mod.deaths_door_resist_bonus`（+20%） | `WeakDeathsDoor` 存活率 |
| `state_flag.immune_fear` | 恐惧 proc 的**前置豁免**检查 |

> 实现方式二选一（架构定）：接 `BuffLedger.StatModTotal(u, kind)`，或直接查 `buffs.Modifiers`。
> **要求**：与既有 `UnitRuntime.AttackMod/ResilienceMod/SpeedMod` 路径**不冲突、不双重计算**。

## F2.3 折磨 proc 三件套（#192）

| 折磨 | 时机 | 效果 | 关键约束 |
|---|---|---|---|
| **恐惧** | 使用技能时 | 33% **拒绝释放** | 技能灰掉、**不消耗行动**、必须重选（`morale.md` §5） |
| **自私** | 被治疗 / 鼓舞时 | 33% **拒绝这一次** | 本次治疗/鼓舞无效；施法者行动**照常消耗** |
| **失控** | 攻击时 | 33% **随机更换目标** | 可能打到队友；**支援位 → 改为【捆缚】**（#48）；需 `RngDraw` |

- 概率取 `tuning.affliction_proc_percent`（现 33），**不得硬编码**。
- 失控新目标：在**合法候选池内**随机（受站位/技能目标位约束），不是全图随机。
- 勇猛的 `immune_fear` **豁免恐惧**（但不豁免自私/失控）。

## F2.4 美德池补齐 4 个 + O-27 定值（#193）

| 美德 | 效果 | 数据现状 |
|---|---|---|
| 勇猛 | 伤害 +25%、免疫恐惧 | ✅ 已有 |
| 坚韧 | 死门抗性 +20% | ✅ 已有 |
| **振奋** | **每回合开始 全队 +3 士气** | ⚠️ 数值缺失 → **本包定值 +3/回合** |
| 专注 | 暴击率 +15% | ✅ 已有 |

- `tuning.json` `collapse.virtue_pool`：`["virtue_brave"]` → **4 个全量**。
- **O-27 定值理由**：与"支援位被动 +3/回合"同档，是**纯收益但要避免滚雪球**；
  起手值，随复测可调。
- ⚠️ **滚雪球风险（登记待实测）**：振奋 → 士气更快冲满 → 更多满值美德 → 更多振奋。
  复测须监控"每场美德数"是否失控（既有 KPI 只要求 ≥1）。
- 既有规则不变：美德**上限 1**；已有美德时再冲满 100 → **保留旧美德 + 全队 +10**。

## F2.5 验收

| 用例 | 期望 |
|---|---|
| 恐惧 | 1000 次使用技能 → 拒绝率 ≈ **33%±5%**；拒绝时**行动未消耗**；可换技能继续 |
| 自私 | 被治疗 → 拒绝率 ≈33%，治疗量为 **0**；施法者行动仍消耗 |
| 失控 | 攻击 → 目标随机化率 ≈33%；**新目标在合法候选池内**；支援位 → 变捆缚而非随机 |
| 勇猛 | 伤害 ×**1.25**；**不再触发恐惧** |
| 坚韧 | 死门存活率 **+20pp** |
| 振奋 | 每回合开始**全队 +3** 士气 |
| 专注 | 暴击率 **+15pp** |
| 美德池 | `virtue_pool` 4 个；随机抽取可用 |
| 确定性 | 同 seed 同结果；所有新随机有 `RngDraw` |
| 无双重计算 | 属性减益（Mod 字段）与 buff modifiers **不叠加重复** |

---

# F3 · 敌方超时增援：每 3 回合一波 + 一次性补齐全部空位（#194）

## F3.1 现状：文档对、实现错（两处偏差）

`GDD.md` §1.5.2 原文写的是：

> "敌人开始呼叫增援，**填满其空位**" · "只填满空位" · "之后**每隔 M 回合再来一波**（M 待数值阶段定）"

`BattleDirector.ApplyReinforcement:339` 实际做的是：

```csharp
int emptySlot = Enumerable.Range(1, _enemy.SlotCount)
                          .FirstOrDefault(s => _enemy.GetSlot(s) == SlotState.Empty, 0);
// → 只取【第一个】空位，补 1 个就 return
```

| 项 | 文档 | 实现 |
|---|---|---|
| 每波补几个 | **填满空位**（全补） | **只补 1 个** |
| 波次节奏 | **每隔 M 回合**（M 待定） | **每回合**都触发，**M 从未实现** |

## F3.2 规格（#194 拍板）

```
首波：第 6 回合（trigger_round）仍未分胜负
  ├─ 有空位 → 【一次性补齐当时全部空位】（循环所有 Empty 槽）
  └─ 无空位 → 给【在场全体】敌人 +攻/+速（每波叠加一次）
此后每 3 回合再来一波（6 / 9 / 12 / 15 …）
```

| 参数 | 值 |
|---|---|
| 首波回合 | **6**（既有 `trigger_round`） |
| **波次间隔 M** | **3**（新增 tuning 键，如 `wave_interval_rounds: 3`） |
| 每波补位 | **全部空位**（循环） |
| 满编分支 | 全体 +攻/+速，**每波**叠加 |
| 增援强度 | 沿用 O-20（切片不走随机分布，按原型轮换） |
| 上限 | 敌方 4 位，不扩编 |

## F3.3 🔴 为什么否决 M=1（必须记录，防止以后被"优化"回去）

若**每回合都补齐所有空位** → 敌人恒为 4 人 → 玩家要赢必须在**单个回合内**清掉满编：

```
敌方满编总 HP = 60+60+46+41 = 207
我方每回合输出 ≈ 27~58（combat_math §7.2 实测）
58 < 207  →  单回合清场不可能  →  第 6 回合后【永远打不赢】
（只剩"第 6 回合前解决战斗"这一种胜法）
```

**M=3** 给出 3 回合窗口 ≈ 需 **69 伤/回合**，落在当前输出区间上沿 ✅ 可玩；
**M=2** 需 104 伤/2 回合，偏紧。

## F3.4 改动点

| 文件 | 改动 |
|---|---|
| `darkest/scripts/gameplay/sim/director/BattleDirector.cs` | `ApplyReinforcement`：`FirstOrDefault` → **遍历所有 Empty 槽逐个补齐**；新增**波次间隔判定**（`_round >= 6 && (_round - 6) % 3 == 0`） |
| `darkest/data/tuning.json` | `overtime_reinforcement` 新增 `wave_interval_rounds: 3` |
| `darkest/tests/**` | 新增 `OvertimeReinforcementTests` |

## F3.5 验收

| 用例 | 期望 |
|---|---|
| 第 6 回合、2 个空位 | **一波补 2 个**（不是 2 回合各补 1 个） |
| 第 7、8 回合 | **不触发**（未到下一波） |
| 第 9 回合 | 触发第二波 |
| 满编（无空位） | 全体 +攻/+速；**下一波再叠一次** |
| 第 6 回合前 | 不触发 |
| 上限 | 补位后敌人 ≤ 4 |
| 可胜性回归 | **构造"玩家每回合稳定击杀 1 个"的局面 → 战斗仍能收敛到胜利**（防 M=1 退化） |

---

# 3. 不在本包范围（**已知缺口，登记不实现**）

| 缺口 | 说明 |
|---|---|
| **流血** | `buff_defs` 有定义（3/回合 ×2），但**没有任何技能施加流血** |
| **标记（Mark）** | `buff.md` §标记 已裁决"加伤 + 改 AI"，但 `buff_defs.json` **无定义、无技能** |
| 崩溃余烬 | 只有标记位，**无玩法效果** |
| 状态标记互斥表 | 眩晕 + 捆缚并存（`buff.md` §8.1 开放） |
| 驱散类技能 | 36 技能里一个没有（预留接口） |

---

# 4. 红线与协作

| 红线 | 说明 |
|---|---|
| **不得调数值** | 除 **O-27 振奋 +3** 外，本包不改任何 HP/倍率/抗性/tuning |
| **所有新随机写 `RngDraw`** | 折磨 proc、失控换目标——保确定性与回放 |
| **一步一提交** | F0 / F1 / F2 独立提交，便于二分 |
| **别同文件并行编辑** | 与架构侧 `data_schema.md` 镜像错开时段 |
| **架构侧需镜像** | ✅ **已完成（v0.48，见 §5）**：`data_schema`：`units.json` 加 `move_distance`、`skills.json` 通用 `move`（不绑 `owner_unit`）、`TargetSpec.distance` 语义、`taunt` 极性/驱散、`virtue_pool` 4 项、**P12 改写**（按 `pool_external` 而非 `_move` 后缀）、新增 **P14/P15**；`open_issues`：O-27 定值、O-49/O-50/O-51/O-52 转写 + **O-53**（self 字段裁定） |
| **不改既有判定链** | 本包**不碰**胜负判定 / 敌人目标偏好 / 模拟策略（属 `m6_fix_pack.md`） |

---

# 5. 架构镜像记录（v0.48，架构师）

> 回执本包 §4「架构侧需镜像」+ 三项请裁定事项。**本包 P0~P3 规格未改动**，仅补回执、决策号订正与字段裁定。

| 项 | 回执 |
|---|---|
| **顺序约束（确认）** | ✅ 与 `m6_fix_pack.md` P3 的双向约定成立：**F0~F3 必须先于 P3 复测落地**；两个包的改动都作用于"实际输出/敌方压力"，任一晚于复测即令基线作废（已踩 4 次）。已同步写入架构建档（README §4 红线 / Project_Memory） |
| **决策号订正** | 按 `state.md` 权威映射修正本包 3 处错位：F1.3 通用 move = **#191**（原写 #190①）、F1.4 嘲讽挂自己 = **#192**（原写 #191）、F2 士气 buff = **#193**（原写 #192/#193）；#189/#190/#194 原标注正确 |
| **F1.4 字段裁定** | ✅ 见本包 F1.4 表（架构裁定）：`hit_mod=0`（占位不读）、`range_axis="none"`（不保留 melee）、`polarity="positive"` + `dispellable=false`、`tags=["control"]`、`self_slots=[1,2]`；**登记 O-53** |
| **data_schema 镜像** | §3.1 新增 `move_distance`（≥1，距离来源）；§3.2 总量 **44**（36 池内 + 1 通用 `move` + 7 敌）、`owner_unit` 改"池内必填/池外勿填"、`pool_external` 改"恰 1 条、按标志过滤"、`target.distance` 改"切片不填、从单位读"；§3.4 `taunt` 改 positive/不可驱散/self 施加 + 美德 4 个全量（振奋 +3/回合）+ `modifiers` 消费映射；§3.7 `overtime_reinforcement` 增 `wave_interval_rounds:3 / refill:all_empty_slots / full_branch:buff_all_each_wave`、`collapse.virtue_pool` 4 项；§5.4 示例改为通用 `move` |
| **校验镜像** | **P12 改写**（`pool_external` 恰 1、`id=="move"`、禁 `_move` 后缀、`owner_unit==null`、`target.distance==null`）；**P13** 增补 taunt 来源=我方带 `taunt` buff 者（禁依赖 `ArchetypeId`）；新增 **P14**（`move_distance ≥1` + 池外恰 1 + distance 必须为 null）、**P15**（士气 buff modifiers/prob_mod 白名单、proc 概率唯一来源 `tuning.affliction_proc_percent`、美德池 4 项、新随机写 `RngDraw`） |
| **open_issues 转写** | **O-49~O-52 编号确认采用**（分别对应 F0/F1/F2/F3，作者提案号未改号）；新增 **O-53**（嘲讽 self 字段口径）；回写 **O-27 = +3/回合（#193）**、**O-20/O-52 = M3 + 每波补齐（#194）**、**O-39/O-46 = taunt 来源改 buff holder（#192）** |
| **未代写项（留给策划）** | `morale.md` §6 的振奋数值（现登记 +3，建议正文补齐）；`buff.md` §7.1 嘲讽极性/驱散语义（negative→positive、可驱散→不可驱散）；`skill_data.md` §4.5 四条 `*_move` → 1 条通用 `move`（距离移到 `character.md` §7.1b 的单位属性口径） |
