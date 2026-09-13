# 数据层 Schema（data_schema.md）

> **#178/#179 目标语义派生表（2026-09-09 策划拍板）**：`target` 字段（scope+slots）= **可选目标池**，≠ 生效范围：

| 技能画像 | 生效目标 | 代表 |
|---|---|---|
| 有 `damage` 且 **无** `aoe` 标签 | 候选池内 **选一**（实机=玩家点选；headless=固定调用点随机） | 劈砍/突刺/精准射击/威吓箭/低语/震荡/盾击 |
| 有 `damage` 且 **带** `aoe` 标签 | 全范围（范围内全部占用非空位） | 横扫/精神震荡（P11 仅此二） |
| `heal_fixed`（any_ally） | 候选池内 **选一**（单体治疗） | 急救/战场鼓舞 |
| `scope=team` | 全体（逐成员施加） | 战吼/群体绷带/动员令 |
| `scope=self / adjacent_ally_and_self` | 自指/邻近 | 喘息/盾墙 |
| `scope=move_range` + `pool_external:true`（通用移动，#180/#191） | 自身 ±`units.move_distance` 格内**被占用**战斗位**选一**（空位不可选，#21；不过抗性、无伤害） | 1 条通用 `move`（坦 1 / 战医政 2，距离读单位） |

- **P11 校验（新增，已入 SkillsConfig.Validate）**：带 `aoe` 标签的伤害技能仅允许 `warrior_sweep`、`caster_mental_shock`；其余伤害技能必须走"选一"路径。
- **备注 4（两档口径，v0.75 更新：本档已收敛）**：🔴 **现生效 = 决策值（已收敛，无滞后项）**：敌 HP **`32/32/25/21`（总 110，#230）**、**`m_value: 7`**（= `ceil(110 ÷ (21.04×0.8))`，**P16 断言一致性**）、**首波增援 `trigger_round: 7`**、敌攻 **12/13/12**、急救 **12 HP/CD 1**、死门抗性 **`80/85/70/70`**、**近战/射手 `target_preference = random`**、收割 **0.4/0.5**（决策值 0.6/0.7 待落地）、劈砍 0.9（决策值 1.0 待落地）、横扫 0.6。**回合目标带 4~6**；**M 是导出量**（#196，§3.7），改 HP/输出后必须重算并由 P16 拦住。
- 执行器落点：`SkillExecutor.Execute(skill, caster, …, chosenTargets)`（选一）；**敌方池内选人＝三层语义**（taunt **加权抽取** `taunt_weight`=3 → `target_preference` 原型偏好【**#230：近战/射手＝`random` 池内随机，写 `RngDraw`；唯一候选不掷骰**】 → 槽号兜底）。

> **编号**：ARCH-DS · **类型**：schema · **状态**：草案 v0.1
> **上游**：[设计] doc/modules/{glossary, skill, skill_data, combat_math, character, enemy, morale, buff, formation, ui_spec}.md · **决策引用**：#106, #110, #112, #123, #126, #136, #143, #149, #153, #154, #155, #156, #157, #158, #159, #163, #164, #165
> **依赖**：doc/architecture/_conventions.md · **最近更新**：2026-09-09

---

## 0. 文档定位与唯一权威性

- 本文是 **`res://data/` 下全部配置 JSON 与对应 C# 强类型模型的唯一权威字段来源**：字段名（JSON key）、C# 类型、单位、范围、必填性、数值出处，一律以本文表格为准；本文未列出的字段，实现方**不得**自行加入 JSON 或 C# 模型。
- 全部数值（属性 / 倍率 / 概率 / CD / 士气增减等）为**起手值**，逐字来自上游策划表（skill_data.md 等），本文**不改任何数值**，只做字段映射与结构设计。
- **字段命名**：优先采用 `glossary.md §9` 的建议英文标识（如 `Morale / Resilience / Virtue / Affliction / DeathsDoor / DisplaceResist / Shield / Guard`）；glossary 未覆盖的字段由本文给出 ASCII 标识，并在命名速查表（§6）与字段表中标注 **【新命名】**。
- 覆盖范围：**垂直切片 4v4**（我方 6 人满编 4 原型 + 敌方 3 原型 4 位 1 套编成，见 character.md §1 / enemy.md §5 / #149）。切片外的预留项（驱散、标记、流血施加、每 N 回合限制等）仅收录定义/字段，标注"切片未启用"，禁止实现方擅自启用。
- 冲突处理：上游文档冲突时以 **`modules/` 为准**（_conventions.md §1 / README §6 条 2）；模块内部仍有歧义或跨模块矛盾而影响本 schema 处，一律记入 `open_issues.md` B 节（权威编号 **O-11~O-39**，本文件 §7 同步归档），**不得在本文件内替策划拍板**。

---

## 1. 配置分片总览

| 配置文件（`res://data/` 下） | 策划表出处 | 用途 | 变更归属 |
|---|---|---|---|
| `units.json` | glossary §7.1 单位属性 / combat_math §0 / character §7 / enemy §5.2 | 7 个原型（4 我方 + 3 敌方）静态属性 + 技能池引用 | 数值迭代：按 `verification.md` §2 Monte Carlo（≥300 场）只改 JSON，**不改代码** |
| `skills.json` | skill_data.md（**36 池内我方 + 4 池外移动 + 7 敌方 = 47 条**，#180）按 skill.md 13 字段模板 + 池外字段 | 技能定义：灰显判定、命中/暴击、伤害、附加效果、位移、**移动（move_range）**、使用限制、士气、三轴标签 | 同上；**新增/改名 id、改枚举、改结构**须架构师评审并同步本文与 C# 模型 |
| `morale_events.json` | combat_math §5.2 士气增减表 / morale §2 | 系统级士气增减事件（delta / 对象 / 触发频次 / 出处） | 数值迭代按 §5.2 表；事件增减需同步 morale_events.json 与 morale.md §2 |
| `buff_defs.json` | buff.md §7/§7.1 / morale §5~§8 / combat_math §4 | 可挂状态（buff 与部分控制类）定义：类型/数值/持续/叠层/驱散/钩子 | 加新 buff = 加一条数据（buff.md §1 设计目标），不改代码 |
| `enemy_ai.json` | enemy.md §5.4 AI 优先级表（固定 + 随机） | 每敌人原型的技能优先级规则（切片关闭随机 #112） | 改 AI 规则只改 JSON；切片只 1 套编成（#89） |
| `formation.json` | GDD §1.1/§1.3/§1.6 / formation.md / character.md §1 / enemy.md §5.1 | 槽位常量（我方 6 = 4 战斗 + 2 支援、敌方 4）、初始编成、编号/边界/障碍规则标记 | 编成属内容配置，切片固定 1 套；改编成只改此文件 |
| `tuning.json` | combat_math / morale / GDD 各节（每字段单独标出处） | 系统级过程常量（钳制、公式系数、回合/次数参数、减益默认值） | 数值迭代按 verification.md；结构变更须架构师评审 |
| `camp_skills.json` 🆕 | **`expedition.md` §3.2**（M7，**v0.84 改角色专属**） | **角色专属扎营技能**（**12 个 = 4 原型 × 3**）：`owner_archetype` 归属、点数、效果、目标语义 | M7 新增；改数只改 JSON；**加技能须标 `owner_archetype`** |
| `expedition_nodes.json` 🆕 | **`expedition.md` §1.3**（M7） | **选路节点表**（`battle`/`event` 与事件选项/效果）；**阶段二再加 `elite`** | M7 新增；加节点类型须架构师评审 |
| `items.json` 🆕 | **`expedition.md` §5.6**（M7.5/v0.90） | **背包物品**（柴火 / 口粮 / 支援包 / **SP 恢复物品**）+ **格子占用**；**物品可带 buff**（如每回合 +1 SP） | v0.90 新增；加物品须标 `slots` 与是否可携带 |

> 备注 1：README §2（M3 行）写"42 条技能数据"为更早笔误；skill_data.md 现为 **36 池内 + 4 池外移动 + 7 敌方 = 47 条**（#180），本文与 skill_data 以 **47 条**为准。
> 备注 2：所有配置 JSON 存放于 `res://data/`（工程根 `darkest/data/`，_conventions §3）；C# 模型/加载/校验代码归属 `res://scripts/data/`；导入后的只读资源按蓝图约定放 `res://resources/`。
> 备注 3：**切片**没有装备/营地/招募/存档等外部数据（GDD §15），故**切片**数据文件 7 个。🔴 **M7（远征层）起新增 3 个**：**`camp_skills.json`**（扎营技能）、**`expedition_nodes.json`**（选路节点/事件）、**`items.json`**（背包物品，v0.90）→ **共 10 个**；`tuning.json` 内新增 `expedition` / `resources` / `camp` 三个子结构（§3.8）。**新增文件仍走同一加载管线（§4.1）与 fail-fast 校验**，不得旁路加载。
> 🔴 **备注 5（C 轴，v0.67+ 部分收回；v0.76 文案与 `enemy.md` 逐字对齐）**：**C 轴 = 敌方攻击范围是否覆盖我方支援位 5/6**（#214 的 ②）。**口径原句：`敌方覆盖 5/6 ＝ 除重劈外`** —— **重劈（近战小兵 `melee_heavy_slash`）`[1,2,5,6] → [1,2]`**；**其余敌方技能（威吓箭 / 精神震荡 / 精准射击 / 恐惧低语）保留覆盖 5/6**。→ `skills.json` 仅此一条敌方技能需要改判据；`skill_data.md` §5 本表即写 `我 1、2`（**收回后与策划表一致**）。✅ **策划侧已同步**：`enemy.md` §5.3 技能表全表重写 + 新增上述口径原句（#231）；**同批划线作废了 §2.1 两处 + §5.4 一处的残留设计论证（#234）**——即"策划文档仍在挂"的真因是**过时论证**，不是规则表漏改。
> 备注 4：**口径约定（v0.54 起，取代旧"起手值/生效值"读法）**：**策划表（`skill_data.md`/`enemy.md`/`combat_math.md`）记录的是「最新决策值」**——即"策划已拍板、可能尚未落地"；**实现滞后时由本文件记两档**（**现生效** / **目标（决策值）**），落地后两档合并。
> - 🔴 **v0.75 起本档收敛**：**现生效 = 决策值** —— 敌 HP **`32/32/25/21`（110）**、**`m_value: 7`**、**首波增援第 7 回合**、敌攻 **12/13/12**、**急救 12 HP/CD 1**、**死门抗性 `80/85/70/70`**、**近战/射手 `target_preference = random`**；仍滞后待落地的**仅剩**：收割 **0.6/0.7**、劈砍 **1.0**（其余已在生效值内）。
> - 🔴 **v0.67/#225 撤回 v0.62 剂量包**：**急救回到 `12 HP / CD 1`**、**敌攻回到 `12/13/12`**（与本文件 §3.1/§3.2 已记录值一致，**无需改数据**——撤回 = 撤销 v0.62 那次改动）；**`(a) 敌攻 17/19/17` 正式作废**（它是为"压单场胜率"服务的，而 **#225 判定单场不该被压**）；**保留**：C 轴、喘息 SP 豁免（修 bug）、群体绷带阈值 ≥2。
> - 🔴 **跨场状态不属 JSON**：**HP/士气跨战斗完全保留、场间无恢复**（#225）是 **run 级状态契约**，落点在 `blueprint` **§9.12**（`IRunSession`）与 **O-63**——**不得写进 `units.json`/`tuning.json` 当数据项**。
> - 🔴 **实现方不得把策划表当"当前生效值"照抄**（`skill_data.md` 已加同等警示）；**运行以 `units.json`/`skills.json` 实值与本文件两档比对为准**；**增援波次 M 是导出量**（#196，§3.7），改 HP/输出后必须重算。

---

## 2. 通用约定

### 2.1 枚举定义总表

JSON 一律存小写 ASCII 字符串，C# 用对应 PascalCase 枚举（由 `JsonStringEnumConverter` 或显式映射绑定）。

| 概念 | JSON 取值 | C# 枚举值 | 含义 | 出处 |
|---|---|---|---|---|
| 阵营 Side | `player` / `enemy` | `Player` / `Enemy` | 我方 / 敌方 | GDD §1（我方 6 位 / 敌方 4 位） |
| 伤害轴 DamageAxis | `physical` / `mental` / `none` | `Physical` / `Mental` / `None` | 物理（走物防、不掉士气、护盾/护卫可挡）/ 精神（走韧性、掉士气、护盾穿透）/ 无伤害（不走减免与死门，效果照常） | skill.md 字段13 / glossary §2 / #157 |
| 距离轴 RangeAxis | `melee` / `ranged` / `none` | `Melee` / `Ranged` / `None` | 近战 / 远程 / 无（无伤害支援类技能距离写 `—`，落 `none`） | skill.md 字段12；skill_data 轴列"−·无" |
| 功能标签 FuncTag | `output` / `control` / `displacement` / `support` / `heal` / `aoe` / `debuff` | `Output` / `Control` / `Displacement` / `Support` / `Heal` / `Aoe` / `Debuff` | 前 5 个=功能轴；`aoe`、`debuff` 为 skill_data 标签列（如"输出·AOE / 输出·减益"）使用的补充描述符 | 前 5：skill.md §3 / glossary §2；后 2：skill_data 各表标签列（整理所得） |
| 槽类型 SlotKind | `combat` / `support` | `Combat` / `Support` | 战斗位 / 支援位（位置不是职业 #139） | GDD §1.1 |
| 槽位三态 SlotState | `occupied` / `empty` / `blocked` | `Occupied` / `Empty` / `Blocked` | 有角色 / 空 / 被障碍占据 | GDD §1.1 / glossary §1 |
| 使用限制类型 UseLimitType | `none` / `cooldown` / `per_battle` / `every_n_rounds` | `None` / `Cooldown` / `PerBattle` / `EveryNRounds` | 无限制 / 冷却 N 回合 / 每场 N 次 / 每 N 回合一次 | skill.md §4 / glossary §2；切片仅用前三者，`every_n_rounds` 为模板预留 |
| 位移方向 DisplacementType | `push` / `pull` / `self_forward` / `self_backward` | `Push` / `Pull` / `SelfForward` / `SelfBackward` | 推（目标向"后排/编号增大"方向位移）/ 拉（向"前排/编号减小"方向，切片未用）/ 自我前移 / 自我后移 | skill.md 字段8 / skill_data 位移列（"推 1 / 自我前移 1 / 自我后移 1"） |
| 技能目标范围 TargetScope | `slots` / `self` / `any_ally` / `team` / `adjacent_ally_and_self` / **`move_range`** | `Slots` / `Self` / `AnyAlly` / `Team` / `AdjacentAllyAndSelf` / **`MoveRange`** | 位置列表 / 自身 / 任意友方 / 本方全队 / 相邻友方 + 自身 / **自身左右 N 格内被占用战斗位（池外移动，#180）** | skill_data 目标列 + skill.md §1.1 |
| 士气影响对象 MoraleScope | `self` / `targets` / `team` / `ally_targets` | `Self` / `Targets` / `Team` / `AllyTargets` | 本人 / 本次技能目标 / 全队 / 目标中的友方（盾墙"被守护者 +3"用） | skill_data 士气列（"自身 +3 / 全队 +5 / 目标 +15 / 被守护者 +3"） |
| 士气事件对象 EventScope | `self` / `team` / `per_target` | `Self` / `Team` / `PerTarget` | 本人 / 全队 / 每个被命中目标（AOE 精神 −5/目标） | combat_math §5.2 |
| 敌人目标偏好 TargetPreference | **`random`（默认）** / `lowest_hp` / `backmost` / `lowest_morale` | `Random` / `LowestHp` / `Backmost` / `LowestMorale` | **池内选人偏好**：🔴 **`random` = 池内均匀随机、必须写 `RngDraw`；池内唯一候选不掷骰**（#230：近战小兵与远程射手均改此档，**治"趁伤收残＝集火"**）；`lowest_hp`/`backmost`/`lowest_morale` 保留为可选（`backmost` 曾用于射手点后排） | **state #230 / O-44 / O-46** |
| 状态类别 StatusClass | `buff` / `unit_state` | `Buff` / `UnitState` | 数据驱动可挂 buff / 机制性单位状态（虚弱、崩溃余烬） | 见 §3.4 分类说明 |
| 状态极性 Polarity | `positive` / `negative` | `Positive` / `Negative` | 正面（不可驱散）/ 负面（可驱散） | buff.md §5.1 |
| 持续时间类型 DurationType | `rounds` / `action_skip` / `charges` / `until_morale_50` / `until_battle_end_or_morale_zero` / `next_attack_within_rounds` / `until_battle_end` / `until_run_end` / **`until_next_recovery`** / **`battles`** / **`next_battle`** / 🔴 **`while_carried`** | … / `UntilNextRecovery` / `Battles` / `NextBattle` / **`WhileCarried`** | … / 🔴 **`while_carried`（M7.5 新增）= "只要在背包里就生效"** —— **扎营/回城都【不清】**，**只有丢弃/掏出才失效**（用途：**SP 恢复物品的 buff**，见 §3.13；**架构裁定，理由见下**） | buff.md §6 / **`m7_5_dungeon_layer.md` D2** / **#258** |
| 叠层规则 StackRule | `refresh` / `stack` / `none` | `Refresh` / `Stack` / `None` | 有时限 buff 再次获得=刷新时长 / 无限时 buff=叠层有上限 / 不叠层（美德上限 1） | buff.md §5 / #159 |
| 附加效果类型 EffectType | `stun` / `taunt` / `bleed` / `stat_mod` / `shield` / `guard_attach` / `next_attack_boost` / **`mark`** | `Stun` / `Taunt` / `Bleed` / `StatMod` / `Shield` / `GuardAttach` / `NextAttackBoost` / **`Mark`** | 眩晕 / 嘲讽 / 流血 / 属性修正（增减益）/ 护盾（次数型）/ 护卫守护链接 / 下次攻击加成（敌人突进）/ **标记（#204，无数值；防守=嘲讽 vs 进攻=标记）** | skill_data 附加列 + combat_math §4 + buff.md §7 + **dd_reference §1.3** |

### 2.2 数值钳制与"派生量"约定

| 规则 | 值 | 出处 |
|---|---|---|
| 士气范围 | [0, 100]，起手 50 | morale §1 / GDD §2.1 |
| 命中率钳制 | [55, 100]（`命中率 = 100 − 目标闪避 + 技能命中修正`） | combat_math §1 |
| 精神减免 | `韧性 / 250`，上限 **40%**（连续公式） | #158 / combat_math §2.2 |
| 美德率 | `10% + 韧性 ÷ 2` | #56 / #116 / #158 |
| 伤害下限 | 任何来源伤害最低 **1** 点 | combat_math §2.3 |
| 伤害浮动 | 默认 **1.0（关闭）**；开启区间 0.9~1.1 | combat_math §2.1 / README 开放题 1 |
| 速度浮动 | 实际速度 = 基础 ×(1 + 0~10% 随机)，每回合重掷 | #163 / GDD §2.5 |
| 暴击倍率 | 1.5（未暴击 1.0） | combat_math §2.1 |
| 附加效果实际概率 | `实际触发率 = 技能标注概率 × (1 − 目标对应抗性)`（乘法） | combat_math §4 |
| 属性减益固定值（通用默认） | −3 / 2 回合（与技能自带韧性减益 −15/−10 的张力见 O-23） | combat_math §4 / §10 |

- **派生量不入库**：`精神减免 = 韧性/250`、`美德率 = 10% + 韧性/2` 是运行时由韧性算出的派生量，units.json **不得**出现这两个字段（见 §3.1 备注与 §4 校验 P4）。glossary §7.1 亦明确"推导量（不入库，运行时算）"。
- **百分比存储约定**：百分比属性与概率一律存**整数百分数**（`10` 表示 10%，范围 0~100）；倍率/系数等乘性量存浮点（double）。伤害取整 `round()` 四舍五入（combat_math §2.3）。

### 2.3 随机判定写法（与 combat_math 通用约定逐字一致）

combat_math 文档头通用约定："骰子一律 `rand(0, 100)`，比较用 `<`（小于判定值算成功）。"

| 判定 | 写法 | 出处 |
|---|---|---|
| 命中 | `命中 = rand(0,100) < 命中率`（命中率见 §2.2 表） | combat_math §1 |
| 附加效果触发 | `触发 = rand(0,100) < 实际触发率`（实际触发率见 §2.2 表） | combat_math §4 |
| 位移过抗性 | `过抗性 = rand(0,100) >= 目标位移抗性` → 成功（**大于等于**，唯一例外） | combat_math §3 / glossary §1 |
| 死门判定 | `存活 = rand(0,100) < 存活概率`，`存活概率 = 死门抗性 − (折磨中 ? 10 : 0)` | combat_math §6 / #123 |
| 崩溃判定（折磨/美德） | 按美德率（`10% + 韧性÷2`）判定美德，否则折磨；判定后士气留在 0 | morale §4 / #67 |

### 2.4 UI 9 条必显信息 → 数据来源字段（决定哪些数据必须暴露）

见 ui_spec §2。本表说明每条必显信息从哪份配置/字段读：

| # | 必显信息（ui_spec §2） | 数据来源 |
|---|---|---|
| 1 | 行动序列（本回合 + 下回合预览） | 运行时：units.json `speed` × tuning `speed_float`，破平局"编号小者先动"（GDD §2.5） |
| 2 | 撤退成功率具体数字 | 运行时按 **#169 已定公式**（`基础 = 50% + (我方−敌方存活平均实际速度)×4%`，钳 [15%,85%]；`实际 = 基础 + uniform(−10,+10)`，钳 [5%,95%]，tuning `retreat_formula`）；显示代价读 tuning `retreat` |
| 3 | 技能灰显 + 原因（站位不符/范围内无人/CD/次数用尽） | skills.json `self_slots` / `target` / `use_limit` + 运行时占用 |
| 4 | 命中率 | units.json `dodge` + skills.json `hit_mod` + tuning `hit_clamp` |
| 5 | 目标范围高亮 | skills.json `target`（位置列表语义，formation 编号可见） |
| 6 | 位移预览（整条交换链） | skills.json `displacement` + formation §2 交换链规则 |
| 7 | 士气条（0~100 + 数值） | 运行时士气（tuning `morale` 钳制），事件来源 morale_events.json / skills.json `morale_effects` |
| 8 | HP / 护盾次数 / buff 图标 | units.json `hp`、技能盾效果 `charges`（铁壁 2 次 #156）、buff_defs.json 图标与次数语义 |
| 9 | 位置编号 | formation.json `numeration`（我方 1~6、敌方 1~4，中央向两侧递增） |

---

## 3. 分表 Schema

### 3.1 `units.json` —— 7 个单位原型

**用途**：4 我方原型 + 3 敌方原型的静态属性与技能池（运行时每个单位实例从原型复制基础值）。字段逐项来自 glossary §7.1（与 combat_math §0 / character §7 / enemy §5.2 数值一致）。

**JSON 字段表**：

| JSON key | C# 类型 | 单位·范围 | 必填 | 来源 | 备注 |
|---|---|---|---|---|---|
| `id` | string | ASCII，唯一 | ✅ | 【新命名】 | 原型标识：`warrior`/`tank`/`medic`/`commissar`/`melee_soldier`/`ranged_archer`/`caster` |
| `name` | string | 中文名 | ✅ | skill_data/character/enemy | 显示名（战士/坦克/军医/政委/近战小兵/远程射手/施法者） |
| `side` | string(Side) | `player`/`enemy` | ✅ | §2.1 | 敌方无士气/虚弱/死门（enemy §1） |
| `hp` | int | >0 | ✅ | glossary §7.1 | 我方 战士40/坦克55/军医32/政委33（不变）；敌方 🔴 **现生效 `32/32/25/21`（总 110，−34%，#230 已落地）**（上一档 48/48/37/33=166 为 #195 值，已过档）——目的：把单场压到 **4~6 回合**、让首波增援（**第 7 回合**）成为"拖太久"的惩罚 |
| `attack` | int | >0 | ✅ | 同上 | 12/8/11/11；12/13/12 |
| `phys_def` | int | ≥0 | ✅ | 同上 | 物防（减免走 `物防/(物防+30)`，combat_math §2.1） |
| `speed` | int | >0 | ✅ | 同上 | 8/5/10/10；8/12/9 |
| `dodge` | int（百分数） | 0~100 | ✅ | 同上 | 10/5/10/10；10/15/10 |
| `crit` | int（百分数） | 0~100 | ✅ | 同上 | 暴击 5/3/5/5；5/8/5 |
| `resilience` | int | 0~100 | ✅ | 同上 | 韧性（glossary §9 `Resilience`）：50/55/60/65；55/60/70 |
| `stun_resist` | int（百分数） | 0~100 | ✅ | 同上 | 眩晕抗性：30/45/30/30；30/25/40 |
| `bleed_resist` | int（百分数） | 0~100 | ✅ | 同上 | 流血抗性：30/40/30/30；30/30/30 |
| `stat_debuff_resist` | int（百分数） | 0~100 | ✅ | 同上 | 属性减益抗性：25/35/30/35；25/25/30 |
| `displace_resist` | int（百分数） | 0~100 | ✅ | 同上 | 位移抗性（glossary §9 `DisplaceResist`）：40/60/25/30；55/25/30（#154 补入） |
| `deaths_door_resist` | int?（百分数） | 0~100，**仅我方** | 我方 ✅ / 敌方 null | 同上 | 死门抗性（glossary §9 `DeathsDoor`）：🔴 **v0.71 起 `80/85/70/70`**（原 70/75/60/60，**+10pp**，`5892da0` 已落地——用"减少死门失败率"压阵亡）；**敌方为 null**（敌方无死门，enemy §1）。⚠️ 该 +10 的落点是**单位属性直改**（非技能/buff，故 **O-64 按此关闭**） |
| `skills` | string[] | 技能 id 列表 | ✅ | skill_data | 我方每原型 9 个 id；敌方：melee_soldier 2 个 / ranged_archer 3 个 / caster 2 个 |
| `move_distance` | int | ≥1 | 我方 ✅ | #191 / feat_pack F1.3 | **移动距离**：坦克 1 / 战士 2 / 军医 2 / 政委 2；**`move` 技能不再自带 distance**，`move_range` 解析时从**施法单位**读取；校验见 **P14** |

> **禁止入库存派生量**：`精神减免 = 韧性/250（上限 40%）`、`美德率 = 10% + 韧性/2` 均为派生量，**不在 JSON 中**（glossary §7.1 注）。
> 敌方死门抗性必须为 `null`（不允许省略后按 0 处理，便于校验区分"敌方无此系统"与"数值漏填"）。
> 原型 vs 实例：6 人编成中重复原型（战士×2、军医×2、近战小兵×2）共享同一原型定义，初始位置由 formation.json 编成引用（§3.6），不在此文件复制。

### 3.2 `skills.json` —— 44 条技能（36 池内 + 1 通用移动 + 7 敌方，v0.47/#191）

**用途**：skill.md 13 字段模板的落盘实现（skill_data 为"填实版"）。技能按"施法者原型"归属；同名技能数值不同时是**独立记录**（如战士喘息 回 HP 8、坦克喘息 回 HP 10；战士盾击 0.6 / 坦克盾击 0.7）。

**记录索引（id / 中文名 / owner / skill_data 出处）**——数值列一律以 skill_data.md 对应行**逐字抄录**进 JSON，本文不重复展开 44 条数值（避免转录漂移），仅在 §5 给出三条示例 + 1 条池外移动示例。

| 原型 | 记录（id — 中文名） | skill_data 出处 |
|---|---|---|
| 战士 warrior | `warrior_cleave`劈砍 / `warrior_sweep`横扫 / `warrior_lunge`突刺 / `warrior_javelin`投掷短矛 / `warrior_shield_bash`盾击 / `warrior_war_cry`战吼 / `warrior_battle_fury`战意 / `warrior_catch_breath`喘息 / `warrior_last_stand`殊死一搏 | §1 行 1~9 |
| 坦克 tank | `tank_guard_wall`盾墙 / `tank_taunt`嘲讽 / `tank_shield_bash`盾击 / `tank_heavy_ram`重盾猛撞 / `tank_iron_wall`铁壁 / `tank_war_cry`战吼 / `tank_hunker`坚守 / `tank_catch_breath`喘息 / `tank_selfless_charge`舍身 | §2 行 1~9 |
| 军医 medic | `medic_scalpel`手术刀 / `medic_cross_slash`十字斩 / `medic_anesthetic`麻醉针 / `medic_medicine_flask`投掷药瓶 / `medic_double_hit`双连击 / `medic_field_strike`战地搏击 / `medic_lethal_injection`致命注射 / `medic_first_aid`急救 / `medic_group_bandage`群体绷带 | §3 行 1~9 |
| 政委 commissar | `commissar_command_blade`指挥刀 / `commissar_charge_order`冲锋令 / `commissar_pistol_shot`手枪射击 / `commissar_supervise`督战 / `commissar_battle_inspiration`战场鼓舞 / `commissar_mobilize`动员令 / `commissar_execution_order`处决令 / `commissar_burst_fire`连射 / `commissar_total_mobilization`总动员 | §4 行 1~9 |
| **池外通用（#191）** | `move` 移动（**1 条通用、不绑 `owner_unit`**；距离读施法单位 `units.move_distance`） | skill_data §4.5 / feat_pack F1.3 |
| 近战小兵 melee_soldier | `melee_heavy_slash`重劈 / `melee_charge`突进 | §5 行 1~2 |
| 远程射手 ranged_archer | `ranged_precise_shot`精准射击 / `ranged_intimidating_shot`威吓箭（#165）/ `ranged_retreat`后撤 | §5 行 3~5 |
| 施法者 caster | `caster_fear_whisper`恐惧低语 / `caster_mental_shock`精神震荡 | §5 行 6~7 |

> 敌方技能归属字段仍写 `owner_unit`（敌方原型 id），目标是 AI 可用性过滤（enemy §5.4）与"该技能属于谁"的一致性校验。

**JSON 字段表（对应 skill.md / glossary §7.2 的 13 字段 + 标识字段）**：

| JSON key | C# 类型 | 单位·范围 | 必填 | 来源 | 备注 |
|---|---|---|---|---|---|
| `id` | string | ASCII，全局唯一 | ✅ | 【新命名】 | `<原型>_<动作>` 蛇形 |
| `name` | string | 中文名 | ✅ | skill_data | 13 字段之 1（技能名） |
| `owner_unit` | string | units.json id | **池内 ✅ / 池外勿填** | 【新命名】 | 归属原型（技能池归属 + 校验用）；**`pool_external:true` 的通用 `move` 不绑**（#191，P14） |
| `pool_external` | bool | 默认 false | ✅ | skill.md §1（#180/#191） | **池外常备**：true = 不进 9 选 5 池、人人可用；**切片恰 1 条 = 通用 `move`**；**一律按本标志过滤，禁止 `id.EndsWith("_move")`**；加载/校验见 P14 |
| `self_slots` | string \| int[] | `"all"` 或位置数组 | ✅ | 13 字段之 2 | **自身站位要求**。`"all"`=任意位置可用（战吼/急救/战场鼓舞等兜底）；数组元素：我方 1~6（含支援位 5、6）、敌方 1~4（相对本方编号） |
| `target` | object(TargetSpec) | 见下 | ✅ | 13 字段之 3 | **目标位置**：技能不直接选目标、只选位置（glossary §2） |
| `damage` | object? | 见下 | ⬜ | 13 字段之 4 | **伤害倍率**；治疗/纯增益类为 null |
| `hit_mod` | int | 对基础命中的加减（百分点） | ✅ | 13 字段之 5 | skill_data 命中列（+5 / −5 / +0 等）；**`scope=self`/`team`/`adjacent` 类恒 `0` 且引擎不掷命中（占位，O-53）** |
| `crit_mod` | int | 对基础暴击的加减（百分点） | ✅ | 13 字段之 6 | skill_data 暴击列 |
| `effects` | array(Effect[]) | 见下 | ✅（可为空数组） | 13 字段之 7 | **附加效果 + 概率**（眩晕/嘲讽/属性减益/护盾/守护等） |
| `displacement` | object? | 见下 | ⬜ | 13 字段之 8 | **位移效果**（推/拉/自移 + 格数） |
| `use_limit` | object(UseLimit) | 见下 | ✅ | 13 字段之 9 | **使用限制** |
| `morale_effects` | array(MoraleEffect[]) | 见下 | ✅（可为空数组） | 13 字段之 10 | **士气影响**（对象+数值）；精神伤害派生士气见下文"士气扣减衔接" |
| `tags` | string[](FuncTag) | 见 §2.1 | ✅ | 13 字段之 11 | **功能标签**（数组=组合，如 输出+AOE）；skill_data 标签列以"·"连接的词拆入数组 |
| `range_axis` | string(RangeAxis) | 见 §2.1 | ✅ | 13 字段之 12 | **距离轴**；**`scope=self`/`team` 类恒 `"none"`（无距离轴，O-53——不填 `melee`，避免 UI/AI 误按近战筛选）** |
| `damage_axis` | string(DamageAxis) | 见 §2.1 | ✅ | 13 字段之 13 | **伤害轴**（决定减免轴、掉不掉士气、护盾/护卫挡不挡，#157） |
| `heal_fixed` | int? | ≥0，固定值 | ⬜ | combat_math §8 | **治疗固定值**：不吃攻击力。急救 12 / 群体绷带 5（每人）/ 喘息 8、10（自身）。对象随 `target` |
| `self_damage_fixed` | int? | ≥0，固定值 | ⬜ | combat_math §8 / glossary §3 | **自我伤害固定值**：殊死一搏 6 / 舍身 8。不被护盾吸收、可致死（走死门） |
| `requires` | object? | 见下 | ⬜ | **dd_reference §1.6（#207）** | **条件解锁**：不满足 → **灰显 + tooltip 原因**（`AvailabilityReason.RequirementNotMet`）。白名单字段：`self_hp_below_percent` / `target_hp_below_percent` / `self_weak` / `self_deaths_door`（布尔）；起手落地 2 处：殊死一搏 `self_hp_below_percent:50`；致命注射 / 处决令 `target_hp_below_percent:50` |
| `bonus_vs_marked_percent` | int? | 0~100 | ⬜ | **dd_reference §1.3（#204）** | 对**带 `mark` 的目标**加伤（**乘法阶段**，起手 **+25**）；**受益技能起手 2 条：军医【致命注射】、政委【处决令】**（⚠️ 曾误写为"战士【失血收割】"——该技能**不存在**，"失血收割"只是两条 `missing_hp` 技能的通称）；与「嘲讽」职责划清（防守 vs 进攻） |

**`target`（TargetSpec）子结构**：

| 字段 | C# 类型 | 说明 | 备注 |
|---|---|---|---|
| `scope` | TargetScope | `slots`/`self`/`any_ally`/`team`/`adjacent_ally_and_self`/**`move_range`** | skill_data 目标列逐条映射；`move_range` = 池外移动专用（skill.md §1.1） |
| `side` | Side? | `scope=slots` 时必填：`player`/`enemy` | 敌方技能目标写我方（skill_data §5"我 X"）；我方技能目标可写敌方或己方 |
| `slots` | int[]? | `scope=slots` 时必填 | 取值须合法：`side=player` → 1~6；`side=enemy` → 1~4 |
| `distance` | int? | **切片不填（#191）** | **已数据化到单位**：`move_range` 距离从施法单位 `units.move_distance` 读取（坦 1 / 战医政 2）；本字段保留作"未来非单位标度移动"扩展位，**切片禁止填写**（填了即 P14 报错） |

映射示例：`敌 1、2`→`{"scope":"slots","side":"enemy","slots":[1,2]}`；`我方全队`→`{"scope":"team"}`；`任意友方`→`{"scope":"any_ally"}`；`自身`→`{"scope":"self"}`；`相邻友方 + 自身`→`{"scope":"adjacent_ally_and_self"}`（施法时刻按所在槽位展开相邻己方，formation 相邻语义）。
> 生效语义（**#178/#179 已拍板**，推翻旧"部分非空→全部生效"读法；出处 GDD §1.2 / skill.md §2 字段3 / O-38）：
> 范围 = **可选目标池**，命中数量**不经数据声明**，由引擎按 `scope` + `tags` 含 `aoe` 派生：
>
> | 条件 | 命中目标 |
> |---|---|
> | `scope=slots` 且 tags 含 `aoe`（切片仅：横扫、精神震荡） | 范围内**全部**非空（含障碍位） |
> | `scope=slots` 无 `aoe`（劈砍/短矛/重劈/精准射击/威吓箭…） | 范围内**选一个**非空目标（玩家点选 / AI 规则 / 策略选择） |
> | `scope=any_ally`（急救/战场鼓舞） | **选一**单体友方 |
> | `scope=team`（战吼/动员令/群体绷带） | 全体友方（含支援位） |
> | `scope=self` / `adjacent_ally_and_self` | 自身 / 自身+相邻（盾墙多目标**有意**，#159） |
> | `scope=move_range` + `pool_external:true`（移动，#180） | 自身左右 N 格内**被占用**战斗位**选一**（空位不可选 → NoTarget，#21） |
>
> 范围内**全部为空 → 灰显不可释放**（NoTarget）；**部分非空 → 单体选其一、AOE 打全部非空**（不浪费、不落空）。**双段（0.55×2 / 0.5×2）= 同一目标两段，目标只选一次（#179）**。此为引擎规则、不入数据——**池内 36 条技能 JSON 无需改动；v0.48 起池外仅 1 条通用 `move`（总量 44 = 36 池内 + 1 移动 + 7 敌，#191）**：自我移动**不过位移抗性**、无伤害 → 不触发死门（#117），结算 = 与目标位**两点直接互换**（`FormationBoard.SwapSlots`；**逐级交换链仅属 push/pull 位移技能**——O-48 / d290e10）。

**`damage`（DamageSpec）子结构 —— 多段倍率与公式型倍率的 JSON 表达**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `segments` | DamageSegment[] | 伤害段列表；每段独立成伤。`0.55×2` = 两段各 0.55（skill_data §0"0.55×2 = 两段各 0.55"） |

段类型 `DamageSegment`（二选一）：

- 定值倍率段：`{"type":"flat","multiplier":1.0}` —— 段伤害 = 攻击 × multiplier。
- **"已失血%"公式段**（军医【致命注射】/ 政委【处决令】）：`{"type":"missing_hp","base":1.0,"coefficient":0.6}`，语义 `倍率 = base + (最大HP − 当前HP)/最大HP × coefficient`；满血时加成 0（skill_data §3 注：`已失血% = (最大HP − 当前HP)/最大HP`）。
  - 🔴 **两档口径（v0.54）**：**现生效 `0.4/0.5`（#173）→ 目标 = 决策值 `0.6/0.7`（#195）**——致命注射 **0.6**、处决令 **0.7**（**skill_data 现记决策值 `1.0 + 失血% × 0.6/0.7`**，实现未落地前不得照抄）；**劈砍 `warrior_cleave` 0.9 → 1.0**；**横扫 `warrior_sweep` 保持 0.6（不得顺带改动）**。
- 例：致命注射 → `{"segments":[{"type":"missing_hp","base":1.0,"coefficient":0.6}]}`；双连击 → `{"segments":[{"type":"flat","multiplier":0.55},{"type":"flat","multiplier":0.55}]}`。

**`effects`（附加效果）子结构**：

| 字段 | C# 类型 | 单位 | 必填 | 说明 |
|---|---|---|---|---|
| `type` | EffectType | — | ✅ | `stun`/`taunt`/`bleed`/`stat_mod`/`shield`/`guard_attach`/`next_attack_boost` |
| `probability` | int?（百分数） | 0~100 | 概率型必填 | 技能侧标注概率；**实际概率 = 标注 × (1 − 对应抗性)**（combat_math §4）。眩晕 35/40/30 属概率型；嘲讽/护盾/属性减益未标概率（见 O-24 是否吃抗性） |
| `resist_axis` | string | — | 概率型必填 | 眩晕→`stun_resist`、流血→`bleed_resist`、属性减益→`stat_debuff_resist`（combat_math §4） |
| `stat` | string? | — | `stat_mod` 必填 | 修正属性：`attack`/`phys_def`/`resilience`/`speed`（攻/防/韧/速四类，combat_math §4） |
| `delta` | int? | — | `stat_mod` 必填 | 固定值增减：战意 攻击+4、盾墙自身物防+6、坚守 物防+8、动员令 全队攻+3（2 回合）；韧性 −15（投掷药瓶）/ −10（督战、恐惧低语） |
| `duration_rounds` | int? | 回合 | 有时限必填 | 上表增减益持续 **2 回合**（skill_data 各行动括号值） |
| `charges` | int? | 次数 | `shield` 必填 | **护盾按次数不按点数**（#156）：铁壁 = 2 次，只挡物理；流血/位移/自我伤害不耗次数，AOE 耗 1 次 |
| `apply_to` | string | — | ⬜ | 缺省=`targets`；可选 `self`（盾墙的自身物防+6）、`ally_targets`（盾墙的守护挂在相邻友方上） |
| `buff_id` | string? | buff_defs id | 有对应状态定义时填 | 指向 buff_defs.json 中带生命周期的状态（眩晕/嘲讽/护盾/守护等），概率与持续回合由技能行覆盖/确认 |

> 眩晕持续 = "跳过本次行动，状态随即结束"（1 次行动，GDD §2.5 / combat_math §4）——不在技能行写回合数，由状态定义（buff_defs）承载。

**`displacement` 子结构**：

| 字段 | C# 类型 | 说明 |
|---|---|---|
| `type` | DisplacementType | `push`（推 1）/`self_forward`（突进：自我前移 1）/`self_backward`（后撤：自我后移 1）；`pull` 切片未用 |
| `count` | int | 格数（本切片全部为 1） |

结算语义：**推/拉**（`push`/`pull`）→ 过目标位移抗性（`rand(0,100) >= 抗性`）后走逐级交换链（formation §2）；**自我位移**（`self_forward`/`self_backward`）与池外「移动」（#180，`move_range`）→ **不过位移抗性**（推的是自己）；撞边界 = 位移失败不动（#78），撞障碍 = 交换（#22）；位移不产生空位、不产生伤害、不触发死门（#117）。敌人位移仅能由技能触发（#88）。**注（O-48）**：逐级交换链只属 push/pull；增援（#181）与池外「移动」是**两点直接互换** `SwapSlots`（途经槽位不动）。

**`use_limit` 子结构**：

| 字段 | C# 类型 | 说明 |
|---|---|---|
| `type` | UseLimitType | `none` / `cooldown` / `per_battle` / `every_n_rounds` |
| `value` | int? | `cooldown` → N 回合（盾击 2 / 铁壁 4 / 恐惧低语 **1** 等）；`per_battle` → N 次（殊死一搏、舍身、总动员 = **每场 1 次**）；`every_n_rounds` → 切片未用 |

**`morale_effects` 子结构**：`[{"scope": MoraleScope, "delta": int, "note"?: string}]`。示例：战吼 `[{"scope":"team","delta":5}]`；喘息 `[{"scope":"self","delta":8}]`；战场鼓舞 `[{"scope":"targets","delta":15}]`；盾墙 `[{"scope":"ally_targets","delta":3}]`（被守护者 +3，#136）；督战 `[{"scope":"team","delta":3}]`；总动员 `[{"scope":"team","delta":10}]`；敌人威吓箭 `[{"scope":"targets","delta":-4}]`（#165）。

**士气扣减型伤害与 §5.2 表的衔接（#157）**：
- `damage_axis = mental` 的普通命中士气损失**不在技能里声明**，由引擎在结算伤害时按 morale_events.json 的行自动扣减：单目标精神 −8 / 精神暴击 −12；带 `aoe` 标签的精神技能 → 每个被命中目标 −5（AOE 折扣）。物理与 `none` 伤害不掉士气。
- 技能显式声明 `morale_effects` 且同时为精神伤害的只有**威吓箭**（targets −4），其与"受到精神伤害 −8"通用规则的关系（替换/叠加/按倍率折算）见 **O-21**。

### 3.3 `morale_events.json` —— 士气增减事件表

**用途**：combat_math §5.2 士气增减表（= morale §2）全部行 → 可落盘事件（delta / 对象 / 触发频次 / 出处）。引擎不硬编码士气数值，改数值只改此文件。

**JSON 字段表**：

| JSON key | C# 类型 | 单位·范围 | 必填 | 备注 |
|---|---|---|---|---|
| `id` | string | ASCII 唯一 | ✅ | 事件标识（如 `mental_hit`、`team_kill`） |
| `name` | string | 中文 | ✅ | 事件名（与 §5.2 表"事件"列一致） |
| `delta` | int | 士气增减 | ✅ | 逐字取 §5.2"变动"列 |
| `scope` | string(EventScope) | `self`/`team`/`per_target` | ✅ | §5.2"对象"列 |
| `occurrence` | string | `normal`/`once_per_battle`/`once_per_turn_max1` | ✅ | 一次性（士气冲满 +10）/ 每回合最多 1 次（虚弱受伤 −5）等；"常规"=每次发生都结算 |
| `source` | string | — | ✅ | 出处：[设计] combat_math §5.2 / morale §2 · #N |
| `note` | string? | — | ⬜ | 备注（含例外与张力说明） |

**事件行（数值逐字来自 combat_math §5.2）**：

| id | 事件（name） | delta | scope | occurrence | 出处 | note |
|---|---|---|---|---|---|---|
| `physical_hit_no_effect` | 受到物理伤害 | 0 | — | normal | #157 | 物理不掉士气，实际不产生士气变化；保留行作口径记录 |
| `mental_hit` | 受到精神伤害 | −8 | self | normal | #157 | 本人 |
| `mental_crit_hit` | 被精神暴击 | −12 | self | normal | #157 | 覆盖同一次伤害的 −8（精神暴击按 −12） |
| `mental_aoe_hit` | AOE 精神伤害 | −5 | per_target | normal | #157 | AOE 折扣，每个被命中目标各 −5 |
| `critical_strike_dealt` | 打出暴击 | +5 | team | normal | morale §2 | — |
| `kill_enemy` | 击杀敌人 | +10 | team | normal | morale §2 | — |
| `ally_enters_weak` | 队友进入虚弱 | −8 | team | normal | morale §2 | — |
| `ally_death` | 队友死亡 | −15 | team | normal | GDD §3.7 | — |
| `support_slot_turn_start` | 站在支援位（每回合） | +3 | self | normal（每回合一次） | #60 | — |
| `battle_inspiration` | 政委「战场鼓舞」 | +15 | self（施放给的目标） | normal | morale §2 | 单一目标；同值也由技能声明，供 §4 校验 P5 对账 |
| `morale_full_100` | 士气冲满 100 | +10 | team | once_per_battle（一次性） | #104/#151/#155 | 附加效果：自身回 50（该复位值属系统行为，见 tuning §3.7 士气条目说明） |
| `retreat_success` | 撤退成功 | 🔴 **两档（#240 改写，推翻 #43 字面值）**：本趟**无死亡 −12 / 有死亡 −15** | **`survivors`（只罚存活者）** | normal（**撤退发生的那一刻立即结算一次**；回城不再二次扣） | #126/#43 → **#240** | 🔴 **不再是"team 固定 −10"**（旧写法含阵亡者） |
| `retreat_fail` | 撤退失败 | −5 | team | normal（一次撤退结算；失败当回合不可再试 #118） | #126/#43 | — |
| `weak_hit_any_damage` | 虚弱状态下受伤（任意类型） | −5 | self | once_per_turn_max1 | #157 | §5.1 例外：物理不掉士气的唯一例外；每回合最多 1 次；可改"仅精神"（开放题 #README-3） |

> 事件触发顺序（combat_math §5.2）：**先结算伤害 → 再结算士气 → 再判定虚弱/死门**，为引擎规则。
> 士气钳制 [0, 100]（morale §1）。技能自带的士气（战吼/喘息/战场鼓舞等）在 skills.json `morale_effects`，此处仅收录系统事件 + 战场鼓舞（用于一致性校验）。

### 3.4 `buff_defs.json` 与状态建模

**用途**：把 buff.md §7/§7.1、morale §5~§8、combat_math §4 里出现的**可挂状态**提炼为数据结构。设计目标 = buff.md §1："一个 buff = 一组修改器 + 一组钩子 + 一条生命周期"，加 buff 只加数据。

**分类裁定（buff vs 单位状态）**：

| 类别 | 收录位置 | 成员 | 依据 |
|---|---|---|---|
| **buff**（数据驱动、可挂可摘、走 buff.md §1 管线） | buff_defs.json | 眩晕、嘲讽、流血、**标记 mark（#204）**、护盾（次数型）、护卫/守护（链接）、捆缚（预留）、折磨×3、美德×4、突进增伤、**死门后遗症（#206，到战斗结束）**；属性增减益以技能内 `stat_mod` 效果承载（不单列 buff 记录） | buff.md §7 / dd_reference §1.3/§1.5 |
| **单位状态 unit_state**（机制状态机字段，**非 buff 数据**，不收录进 buff_defs） | tuning.json（数值）+ 士气/生存模块状态机 | 虚弱 Weak、崩溃余烬 CollapseEmber、**眩晕抗性递增（#202：每次成功眩晕 +50% 可叠、完成一次未被晕的行动即清除、上限 100%）** | buff.md §5.1（虚弱不可驱散）、morale §4.0（崩溃余烬）、dd_reference §1.1 |

理由：虚弱与崩溃余烬没有"可驱散/可叠层/有图标周期"的 buff 语义（虚弱被驱散会跳穿整个虚弱/死门系统，buff.md §5.1 红线），放进独立状态机比混入 buff 管线更干净；其数值常量集中到 tuning.json（§3.7）。

**状态总表（每个可挂状态的数据结构）**：

| 状态（id / 中文） | 类别 / 极性 | 数值 | 持续 | 叠层 | 驱散 | 施加来源 | 出处 |
|---|---|---|---|---|---|---|---|
| `stun` 眩晕 | buff / negative | 跳过本次行动（无伤害数值） | `action_skip`（1 次行动后结束） | refresh（上限 1，再次施加刷新） | ✅ 可驱散 | 盾击（概率 35/40）、麻醉针（30） | GDD §2.5 / combat_math §4 / buff.md §5.1 |
| `taunt` 嘲讽 | buff / **positive**（#192） | **挂在我方嘲讽者身上**（`tank_taunt.target = {scope:self}`）；效果 = 改敌方 AI **加权**优先攻击嘲讽者：嘲讽者权重 `taunt_weight`（起手 3），其余候选各 1（#187；唯一候选=100%）；hook 文案 `enemy weighted prefers attacker (taunt holder)` | `rounds` = 2 | refresh | ❌ **不可驱散**（正面不可驱散，buff.md §5.1；O-53） | 坦克·嘲讽（CD 3，**self 技能**→按 O-49 需点自己的卡） | skill_data §2 / buff.md §7 / #187/#192 / O-53 |
| `bleed` 流血 | buff / negative | 每回合 **3** 点固定伤害 —— 🔴 **v0.52/DoT 三规则（#205）**：① **不受物防减免**（写死）；② **暴击施加时长 2 → 4 回合**；③ **在「目标回合开始」结算**（非回合结束）；④ **每回合伤害不可暴击** | `rounds` = 2（普通）/ **4（暴击施加）** | refresh | ✅ 可驱散 | **v0.52 起有施加者**：战士【突刺】、军医【致命注射】各追加 **3×2** | combat_math §4 / **dd_reference §1.4** |
| `mark` 标记 | buff / negative（#204） | **无数值纯标记**；效果 = ① 带 mark 目标被声明 `bonus_vs_marked_percent` 的技能**加伤**（起手 +25%，乘法阶段）；② 敌方 AI 对带 mark 的**我方**提高权重（复用 #187 权重制，起手 **×2**）；**不可被抵抗**（施加技能本身仍可 miss） | `rounds` = 3 | refresh | ✅ 可驱散 | 政委【督战】（起手落地）；敌方亦可给我方上 mark＝威胁点名 | **dd_reference §1.3** / buff.md §7 / #204 |
| `deaths_door_recovery` 死门后遗症 | buff / negative（#206） | **受到伤害 +10% · 命中 −5 · 速度 −1**；**多次进出死门不叠加**（只一层，防死亡螺旋）；UI 必须显著标注 | 🔴 **`until_next_recovery`（到下次恢复：扎营/回城；#240 起由 `until_run_end` 改义**——切片无恢复手段时两者等价） | **none（不叠加）** | ✅ 可驱散（负面） | 死门**存活并归队**（`WeakDeathsDoor.TryRecover`）时授予 | dd_reference §1.5 / #206 / **#240** |
| `shield` 护盾（次数型） | buff / positive | 抵挡 N 次物理攻击（铁壁 = 2 次） | `charges`（次数归 0 消失） | 待拍板（见 O-25）；不消耗流血/位移/自我伤害；AOE 消耗 1 次 | ❌ 不可驱散 | 铁壁（CD 4） | #156 / combat_math §8.1 / buff.md §7.1 |
| `guard_attach` 护卫/守护链接 | buff / positive（挂在保护者） | 伤害重定向到保护者自身（**只挡物理**，见 O-22）；被守护者 +3 士气（#136） | 未定（盾墙未标 → **O-26**） | 每回合最多重定向 1 次（#159） | ❌ 不可驱散 | 坦克·盾墙（目标=相邻友方） | #136/#159 / buff.md §7.1 |
| 属性增减益 `stat_mod` | buff（效果内嵌，不单列 id） | 攻/防/韧/速固定值增减（见 §3.2 effects） | `rounds` = 2 | refresh（buff.md §5 有时限→刷新） | 正面 ❌ / 负面 ✅ | 战意/盾墙/坚守/动员令/投掷药瓶/督战/恐惧低语 | combat_math §4 / buff.md §5.1 |
| `affliction_fear` 恐惧 | buff / negative | 使用技能时有概率（33%）拒绝释放；不消耗行动、技能灰掉必须重选（#61） | `until_morale_50` | 单折磨（自然不并存，见备注） | ✅ 可驱散 | 崩溃判定 | morale §4.1 / §5 / #57/#61 |
| `affliction_selfish` 自私 | buff / negative | 被治疗/鼓舞时有概率（33%）拒绝这一次 | `until_morale_50` | 同上 | ✅ 可驱散 | 崩溃判定 | morale §4.1 / §5 |
| `affliction_uncontrolled` 失控 | buff / negative | 攻击时有概率（33%）随机更换目标（战斗位）；支援位触发改为「捆缚」（#48） | `until_morale_50` | 同上 | ✅ 可驱散 | 崩溃判定 | morale §4.1 / §5 / #48 |
| `bound` 捆缚 | buff / negative | 无法行动（行动前钩子） | 见备注（O-17） | refresh | ✅ 可驱散 | 支援位失控的兜底（#48）；切片实际不触发（支援位无攻击动作） | morale §5.1 / buff.md §4 |
| `virtue_brave` 勇猛 | buff / positive | 造成伤害 +25%，免疫恐惧 | `until_battle_end_or_morale_zero` | **none（上限 1）** | ❌ 不可驱散 | 崩溃判定（小概率）/ 士气冲满 100 | morale §6 / buff.md §5 |
| `virtue_resolute` 坚韧 | buff / positive | 死门抗性 +20% | 同上 | 同上 | ❌ | 同上（**切片含**，#193） | morale §6 / #193 |
| `virtue_inspired` 振奋 | buff / positive | **每回合开始 全队 +3 士气**（#193 定值，替代 O-27 缺失值） | 同上 | 同上 | ❌ | 同上 | morale §6 / #193 |
| `virtue_focused` 专注 | buff / positive | 暴击率 +15% | 同上 | 同上 | ❌ | 同上（**切片含**，#193） | morale §6 / #193 |
| `next_attack_boost` 突进增伤 | buff / positive | 下次攻击 +20% | `next_attack_within_rounds`（2 回合内） | refresh | ❌ | 敌人·突进 | skill_data §5 行 2 |

> **美德不叠层、上限 1**（#94/#159）：已有美德时再次冲满 100 → 保留旧美德、只结算全队 +10（#100）。美德持续到"战斗结束 **或** 士气再次归 0（先到为准）"；再次归 0 → 消除美德 → 重新判定（morale §8）。
> **折磨持续到士气回初始值 50**，结束点 = 恢复可判定资格点（morale §4.0）；崩溃余烬期间（士气停在 0）不再触发判定（事件触发，非状态触发）。
> 折磨降死门抗性 −10%（#123）→ tuning。折磨自然不并存：恢复判定资格时旧折磨已结束（回 50）。
> 美德池切片范围（**#193 起 4 个全量**）：勇猛 / 坚韧 / 振奋（每回合全队 +3）/ 专注；池成员列表在 tuning.json `collapse.virtue_pool`（§3.7）。⚠️ **滚雪球风险**（振奋→士气更快冲满→更多满值美德→更多振奋）登记待复测监控"每场美德数"（既有 KPI 仅要求 ≥1）。

**buff_defs.json 记录字段表**：

| JSON key | C# 类型 | 必填 | 备注 |
|---|---|---|---|
| `id` | string | ✅ | 唯一（上表 id 列） |
| `name` | string | ✅ | 中文名 |
| `class` | StatusClass | ✅ | `buff`/`unit_state`（虚弱等不在此文件，见分类裁定） |
| `polarity` | Polarity | ✅ | 决定可否驱散（buff.md §5.1） |
| `icon` | string? | ⬜ | 资源名（ui_spec §5 状态显示用；切片可用占位） |
| `duration` | object | ✅ | `{type: DurationType, value?: int}`（`rounds` 带回合数；`charges` 带次数；`action_skip` 无值等） |
| `stack` | object | ✅ | `{rule: StackRule, max?: int}`；美德 = `none`/1；时常 buff = `refresh` |
| `dispellable` | bool | ✅ | 负面 true / 正面 false（含虚弱例外不入表） |
| `modifiers` | array | ⬜ | 修改器（buff.md §3）：`{kind: stat_mod|state_flag|damage_mod|prob_mod, ...}`；**消费映射（#193，feat_pack F2.2）**：`damage_mod.dealt_damage_mult`→`DamageStep` 乘法阶段、`damage_mod.next_attack_mult`→`DamageStep`（消耗后移除）、`prob_mod.crit_bonus`→`HitStep` 暴击判定、`prob_mod.deaths_door_resist_bonus`→`WeakDeathsDoor` 存活率、`state_flag.immune_fear`→恐惧 proc 前置豁免；**与 `UnitRuntime.AttackMod/ResilienceMod/SpeedMod` 路径不得双重计算**（校验 P15） |
| `hooks` | array | ⬜ | 钩子（buff.md §4）：`{timing: ..., effect: ...}`；如眩晕"行动前→跳过行动"、振奋"回合开始→全队回士气"、守卫"友方受伤前→重定向" |
| `extra_rules` | object? | ⬜ | 例外规则标记：护盾"仅物理/次数不耗于流血位移自我伤害/AOE 耗1"、护卫"每回合≤1 重定向/实时判范围/重定向后再算保护者防御/虚弱保护者替挨打触发死门/只挡物理(见 O-22)"、折磨"触发概率 33%（tuning）" |

示例（护盾，数值铁壁 = 2 次 / CD 4 出自 skill_data §2 坦克#5，次数由技能效果行 `charges:2` 提供，此处只描述生命周期）：

```json
{ "id": "shield", "name": "护盾", "class": "buff", "polarity": "positive",
  "duration": { "type": "charges" }, "stack": { "rule": "refresh" },
  "dispellable": false,
  "hooks": [ { "timing": "before_taking_damage",
               "effect": "block_physical_attack_once; if blocked -> attack fully negated (#156)" } ] }
```

### 3.5 `enemy_ai.json` —— 敌人 AI 固定优先级

**用途**：enemy.md §5.4 的固定优先级表 → 配置（enemy §2"优先级表是配置，不是通用逻辑"）。切片**不做随机**（#112），但保留随机参数字段供后续（默认关闭）。

**JSON 结构**：

```json
{
  "archetype_id": "caster",
  "random": { "enabled": false, "fallback_probability": 0.15 },   // #112 切片不做随机；0.15 见 enemy §5.4
  "target_preference": "lowest_morale",                            // #185 → #230：池内选人偏好（random / lowest_hp / backmost / lowest_morale）；近战与射手 = random
  "taunt_weight": 3,                                               // #187：嘲讽者相对权重（起手 3；其余候选各 1）
  "rules": [ { "skill_id": "...", "when": { /* 条件谓词 */ } }, ... ]
}
```

**条件谓词词汇表**（`when` 支持 0~n 个，全部满足才命中；空对象 `{}` = 无条件即默认）：

| 谓词 key | 语义 | 例 |
|---|---|---|
| `self_slot_in` | 施法者自身处于这些位置 | 突进：`[3,4]`；后撤：`[1,2]` |
| `target_slots_occupied_min` | 目标阵营指定位置中"有人"数 ≥ min | 精神震荡打多人：`{"side":"player","slots":[1,2],"min":2}` |
| `player_morale_all_at_least` | 我方（玩家侧）所有人士气 ≥ 值（=无人低于该值） | 威吓箭条件：`40`（enemy §5.4"无人 <40"） |
| （空） | 无条件 → 该规则为默认项 | — |

**rules 解析语义**（可用性判定与玩家一致，skill.md §5：①战前携带（敌方无此概念）②自身站位 ③目标范围非全空 ④使用限制 CD/每场次数）：
- 按数组顺序求值，选择**第一个"条件满足且技能可用"**的规则释放；无条件规则即兜底。
- 引擎层过滤"技能可用"（含 `self_slots`、CD），AI 表只负责"同类可选时优先谁"。

**目标选择（池内选一）语义（#185/#186/#187，v0.46）**——作用于"技能 `target` 已确定的候选池"，三层按序判定：

| 层 | 条件 | 选谁 | 出处 |
|---|---|---|---|
| ① taunt 优先 | 嘲讽 buff 生效且嘲讽者在候选池内 | **加权抽取**：嘲讽者权重 = `taunt_weight`（起手 3），其余每个候选 = 1 → 池内 2 人 P=75% / 3 人 60% / 4 人 50%；唯一候选 = 100%。**抽取必须写 `RngDraw` 日志**（确定性/可回放） | #187 |
| ② 原型偏好 | 否则 | 🔴 **v0.76 起三原型统一 = `random`（池内随机）**：**近战小兵 / 远程射手 / 施法者**均 `random`（**射手仍只打 5/6 后排**——改的是"池内选谁"，不是"能打哪"）；**随机必须写 `RngDraw`，池内唯一候选不掷骰**。→ **切片内敌方选人不再有"原型差异"**，差异只剩**技能选择**与**目标位**（`lowest_hp`/`backmost`/`lowest_morale` 保留为枚举值但切片不使用） | **#230 + #231**（改写 #185） |
| ③ 兜底 | 平局或未声明 | 槽号最小 | #185（原 O-39「槽号最小」降为兜底） |

**新增字段表**：

| JSON key | C# 类型 | 必填 | 值 / 说明 | 出处 |
|---|---|---|---|---|
| `target_preference` | string(TargetPreference) | ✅ | 🔴 **v0.76：三原型统一 `random`**（`melee_soldier` / `ranged_archer` / `caster` 均 `random`）；**射手仍只打 5/6 后排**；`random` 必须写 `RngDraw`、唯一候选不掷骰 | **state #230 + #231** / O-44 / O-46 |
| `taunt_weight` | int（≥1） | ✅ | 全局键（起手 **3**）；仅当 taunt 生效且有嘲讽者在池内时参与加权抽取 | state #187 / O-46 |

> 大前提：**目标位仍由技能 `target` 决定**（#186 后恐惧低语 `[1,2,3,4]`），AI 只在池内选人。🔴 **v0.72 / #230 改写**：**近战小兵与远程射手改为「池内随机」**（`target_preference: random`，**写 `RngDraw`；池内唯一候选不掷骰**）——推翻 `lowest_hp`（"趁伤收残"**恰好就是集火**，实测 2 位 46% + 6 位 31% = **77% 伤害集中在两个位置**）与 `backmost`（射手点后排仍是集火）；**射手保留"只能打 5/6 后排"的目标位限制**（改的是**池内选谁**，不是**能打哪**）。原「槽号最小」兜底仍保留为**最后**兜底。

**切片 3 原型规则（逐字对应 enemy.md §5.4）**：

| 原型 | 顺序 | when | skill_id | 对应策划原文 |
|---|---|---|---|---|
| melee_soldier | 1 | `self_slot_in:[3,4]` | `melee_charge` 突进 | ① 若不在前排（被推到 3/4）→ 突进归位 |
| melee_soldier | 2 | （默认） | `melee_heavy_slash` 重劈 | ② 否则重劈 |
| ranged_archer | 1 | `self_slot_in:[1,2]` | `ranged_retreat` 后撤 | ① 若被近战贴脸（敌方在 1/2）→ 后撤拉开（口径歧义见 O-28；与后撤站位要求 [1,2] 对齐） |
| ranged_archer | 2 | （默认） | `ranged_precise_shot` 精准射击 | ② 否则【精准射击】 |
| ranged_archer | 3 | `player_morale_all_at_least:40` | `ranged_intimidating_shot` 威吓箭 | ③ 我方士气整体偏高（无人 <40）且【威吓箭】CD 就绪 → 用威吓箭压士气（#165）；按首条命中语义与第 2 条存在互斥问题 → **O-28** |
| caster | 1 | `target_slots_occupied_min:{player,[1,2],min:2}` | `caster_mental_shock` 精神震荡 | ① 优先【精神震荡】打多人（AOE 收益高） |
| caster | 2 | （默认） | `caster_fear_whisper` 恐惧低语 | ② 否则【恐惧低语】；**#186 已放宽目标位 `[1,2,3,4]`**，再由 **`target_preference=random`（#231）** 在池内选人（**"点名最低韧性/最低士气"的旧文字口径已随 #234 划线作废**；见 O-21/O-46） |

> 全部技能不可用（CD/站位不符/目标全空）时该敌人本回合不行动（切片技能多无 CD、属边角，行为待实现期确认，不阻塞）。
> 位移技能的站位要求与 AI 优先级对齐（skill_data §5 末注）：小兵在 3/4 才突进、射手在 1/2 才后撤——已体现在 `self_slot_in` 条件。
> 施法者被推至 1 号位后其全部技能站位不符（self_slots 2~4）→ 空过，是位置驱动的预期张力（玩家可预判）。

### 3.6 `formation.json` —— 槽位常量与初始编成

**用途**：槽位常量、初始编成、编号与边界/障碍规则标记（引擎骨架数据，行为细节见 formation.md）。

**JSON 字段表**：

| JSON key | C# 类型 | 必填 | 值 / 说明 | 出处 |
|---|---|---|---|---|
| `player` | object | ✅ | `{slot_count:6, combat_slots:4, support_slots:[5,6]}` | GDD §1.1 / #3 |
| `enemy` | object | ✅ | `{slot_count:4, combat_slots:4, support_slots:[]}`（敌方无支援位） | GDD §1.5 / enemy §1 |
| `numeration` | string | ✅ | `"center_outward"`：编号由屏幕中央向两侧递增，我方 1 与敌方 1 隔"空气"对峙；空气不占槽位（glossary §1） | GDD §1.1 / formation §1 |
| `initial_roster` | object | ✅ | `{player:[{slot,unit}×6], enemy:[{slot,unit}×4]}`，`unit` = units.json id（重复原型共享定义） | 见下两张编成表 |
| `obstacles` | array | ✅（可为空） | 关卡预置障碍占位（`{side, slot, hp?}`）；切片用关卡预置（#39），此表切片默认空，规则能力由 `rules` 标记保留 | formation §5 / GDD §15 |
| `rules` | object | ✅ | 规则标记（下表） | 见下 |

**初始编成（我方，character.md §1 / #149；敌方，enemy.md §5.1）**：

| 槽位 | 我方 unit | 槽位 | 敌方 unit |
|---|---|---|---|
| player 1（战斗·前排） | `tank` | enemy 1 | `melee_soldier`（近战小兵 A） |
| player 2（战斗） | `warrior` | enemy 2 | `melee_soldier`（近战小兵 B） |
| player 3（战斗） | `commissar` | enemy 3 | `ranged_archer` |
| player 4（战斗） | `medic` | enemy 4 | `caster` |
| player 5（支援） | `warrior` | — | — |
| player 6（支援） | `medic` | — | — |

**`rules` 标记（行为常量，逐条带出处，引擎据以执行）**：

| key | 值 | 语义 | 出处 |
|---|---|---|---|
| `boundary_as_hard_wall` | true | 最外侧被向外推 → 位移失败不动 | #78 / GDD §1.7 |
| `displacement_only_via_swap_chain` | true | **推/拉位移**只走逐级交换链，永不产生空位，角色不可走进空格；**例外（O-48）**：增援（#181）与池外「移动」（#180）为两点直接互换 `SwapSlots`（途经槽位不动） | #20/#21 / #180/#181 / formation §2 / O-48 |
| `obstacle_swaps_like_unit` | true | 撞障碍 = 与障碍交换位置 | #22 / formation §2 |
| `close_up_on_death_immediate` | true | 死亡/离场立即向 1 号位靠齐，空位只留队尾；虚弱者不参与前移；后方全虚弱留空不补 | #24/#115 / formation §3 |
| `close_up_ignores_obstacle` | true | 靠齐遇障碍不阻挡，交换推进 | #114 |
| `close_up_enemy_symmetric` | true | 敌方对称执行靠齐 | formation §3 |
| `swap_player_initiated` | true | 换位/增援由战斗位角色发起（非技能数据） | #41/#41b / GDD §1.4.1 |
| `slot_three_state` | true | 槽位三态：有角色/空/被障碍占据 | GDD §1.1 |
| `deaths_and_close_up_separate_from_displacement` | true | 位移 ≠ 靠齐（防误触发，glossary §8 辨析 1） | glossary §1 |

### 3.7 `tuning.json` —— 系统级过程常量

**用途**：跨系统通用常量（钳制 / 公式系数 / 回合与次数参数 / 减益与状态默认值）。每个字段带独立出处。士气增减值**唯一**存于 morale_events.json（§3.3），不在此重复。

| JSON key | C# 类型 | 值 | 出处 | 备注 |
|---|---|---|---|---|
| `morale` | object | `{min:0, max:100, start:50}` | morale §1 / GDD §2.1 | 士气钳制 [0,100]，起手 50 |
| `virtue_rate` | object | `{base_percent:10, resilience_divisor:2}` | #56/#116/#158 | 美德率 = 10% + 韧性÷2 |
| `mental_reduction` | object | `{resilience_divisor:250, cap_percent:40}` | #158 | 精神减免 = 韧性/250，上限 40% |
| `weak` | object | `{damage_mult:0.5, speed_mult:0.7, hp_lock:1}` | GDD §3.3 / #37 | **虚弱**：伤害 −50%、速度 −30%、HP 锁 1（虚弱为机制性单位状态，数值集中于此） |
| `weak_recovery` | object | `{base:10, per_healthy_support_ally:5, resilience_divisor:20, cap:25}` | GDD §3.3 / #59 | 虚弱士气回升 = 10 + 健康支援位队友×5 + 韧性÷20，上限 25；虚弱者互不提供加成 |
| `weak_exit_hp_ratio` | double | `0.10` | #164 | 离开虚弱时 HP 恢复为最大血量的 10% |
| `deaths_door` | object | `{affliction_penalty_percent:10}` | #123 | 折磨状态下死门抗性 −10%（基础抗性在 units.json 各原型） |
| `retreat` | object | `{success_morale:-12, with_death_morale:-15, fail_morale:-5, scope:"survivors"}` | #126/#43 → **#240** | 🔴 **撤退成功士气代价（M7 改写）**：**只对存活者**、**固定两档（无死亡 −12 / 有死亡 −15）**、**撤退时立即结算**（回城不二次扣）。🔴 **单一事实源（v0.82 补）**：M7 起**权威键 = `tuning.expedition.retreat_penalty{no_death,with_death}`**，本行 `success_morale`/`with_death_morale` 仅作**展示与对账**（**不得另立一套数值**）；三处（`expedition.retreat_penalty` / 本行 / `morale_events.retreat_success`）**必须一致**（**P5/P20 ⑥ 对账**）；`fail_morale:-5` 沿用 |
| `support_slot_morale_per_turn` | int | `3` | #60 | 站支援位每回合 +3（本人） |
| `support_points` | object | `{start:3, **regen_per_round:1**, cap:4, cost_skill:2, cost_reinforce:2, **regen_provider:null**}` | GDD §1.4.2 / **#211+#213 / O-60** → 🔴 **v0.98/#255 定调：M7 期＝「机制版」** | **支援点 SP（战斗级资源）**：起手 **3**、上限 **4**；**支援位（5/6）用技能 −2 / 增援 −2**（**同价**，#213）；**战斗位技能 / 被动 / 撤退 0 消耗**；不足 → 灰显 + `SupportPointsNotEnough` 且不吞行动；每次变动写 `SupportPointEvent`。<br>🔴 **v0.98（#255）恢复来源分期**：**M7（本轮）＝机制版** —— **`regen_per_round = 1`（引擎每回合 +1）**，`regen_provider = null`；🔴 **物品版（v0.90/#248：背包里的「SP 恢复物品」提供 +1、不带则不恢复）推迟到 M7.5** —— 届时应把 `regen_per_round` 记 0、`regen_provider` 指向 **`item:support_crate`**（**支援箱**，见 §3.12；🔴 **id 以 `#265` 裁定为准**），**但数值一个都不改**（start/cap/cost 全部沿用）。**边界：本轮不减功能，M7.5 只换来源**（主程序 v0.90 已落地的物品骨架**保留但本轮不启用**）。<br>**稳态**（8 回合 5 次 = 0.63 次/回合）；🔴 **结构天花板**：治疗一半来自战斗位军医 → 对总治疗削减上限 ≈ −50%（#214 ③ 待 `E` 与 KPI 定） |
| `affliction_proc_percent` | int | `33` | #57 | 折磨各状态统一触发概率 33%（自私/恐惧/失控；失控建议后续最低档） |
| `guard_redirect` | object | `{max_per_turn:1, physical_only:true}` | #159 / buff.md §7.1 | 护卫每回合最多重定向 1 次；"只挡物理"见 O-22 |
| `overtime_reinforcement` | object | `{trigger_round:**7**, refill:"all_empty_slots", full_branch:"buff_all_each_wave", safety_factor:0.8, wave_interval_min:3, **m_value:7**, elastic:{enabled:true, k_rounds:2, idle_output_slots:3, max_bonus:3}, wave_interval_rounds:"导出值（非手填常数）"}` | GDD §1.5.2 / enemy §4 / **#194+#196+#198 / #230 / O-52 / O-54** | 🔴 **v0.75：首波 `trigger_round: 6 → 7`**（4~6 回合的战斗**够不到它** → "超时增援"成为真正的惩罚）；此后间隔为 **M（自适应，#198）**：<br>**基准** `M_base = max(wave_interval_min, ceil(敌方满编总HP ÷ (D × safety_factor)))`（`D`=我方每回合对敌总伤害**实测值**；`safety_factor=0.8`；🔴 **实测 `D` = 21.04**（本轮字段齐备、基线成立；估算 24/35 作废）→ **敌 HP 已落地 `32/32/25/21`（总 110，#230）** ⇒ **`M_base` = `ceil(110 ÷ (21.04×0.8))` = `ceil(6.53)` = 7** ✅ **与 tuning `m_value: 7` 一致**（v0.75 指令 `10 → 7` 即此值）。<br>**弹性（#198）**：最近 K=2 回合内未用 `output` 技能的存活战斗位 ≥3/4 → `M += 1`；达标回落 `M = M_base`；浮区 **`M ∈ [M_base, M_base+3]`**；**弹性只作用于后续波次，首波不受影响**。<br>**每波一次性补齐当时全部空位**；无空位→全体在场敌人 +攻/+速（每波叠加、**无上限**——防苟活兜底）；上限 4 位不扩编；增援强度按原型轮换（O-20）。⚠️ **改敌 HP 或我方输出必须重算 `M_base`**（**v0.75 起 P16 加"`m_value` 与公式一致性"断言**，防再漂移） |
| `bleed` | object | `{per_round_damage:3, rounds:2, crit_rounds:4, ignores_phys_def:true, resolve_at:"target_turn_start", can_crit:false}` | combat_math §4 / **dd_reference §1.4（#205）** | **DoT 四规则（v0.52）**：① **不受物防减免**（写死）；② **暴击施加时长 2 → 4**；③ **在「目标回合开始」结算**；④ 每回合伤害**不可暴击**；须与 buff_defs `bleed` 一致（P6/P18） |
| `miss_compensation` | object | `{enabled:true, per_miss:4, free_misses:1, cap:100, hidden:true}` | **dd_reference §1.2（#203）** | 每单位**连续未命中计数**（命中清零）；补偿 = `max(0, 连续未命中 − free_misses) × per_miss`，**仅作用于命中判定**（结果仍钳 100）；🔴 **不得进入 `HitRateFor`/面板显示**（隐藏补偿） |
| `stun_resist_buildup` | object | `{per_stack:50, cap:100, clear_on:"own_action_not_stunned"}` | **dd_reference §1.1（#202）** | 每次**成功施加眩晕** → 眩晕抗性 **+50%（可叠）**；抗性 = 基础 + 递增、**上限 100%**（= 实际概率 0）；**完成一次未被眩晕的行动即清除**；**不写入单位基础属性**（unit_state） |
| `deaths_door_recovery` | object | `{damage_taken_percent:10, hit_mod:-5, speed:-1, stack:"none", duration:"until_next_recovery"}` | dd_reference §1.5（#206）/ **#240** | 死门**存活并归队**后获得「死门后遗症」；**多次进出不叠加**；🔴 **`until_next_recovery`（到下次恢复＝扎营/回城）**——由 run 级状态承载（`blueprint` §9.12）；**回城必定清除**（expedition §1.2） |
| `crit_chain` | object | `{taken_self_morale:-10, taken_ally_chance:50, taken_ally_morale:-5, dealt_team_morale:5, heal_multiplier:2, heal_target_morale:4, heal_crit_chance_single:12, heal_crit_chance_multi:5}` | **dd_reference §2（#209）** | **暴击情绪链**：**被暴击** → 自身 **−10**、队友各 **50% −5**（新增 `physical_crit_hit_*` 事件；这是 #157"物理不掉士气"的**有名字例外「震慑」**）；打出暴击 → 全队 +5（不变，**AOE 只结算一次**，#208）；**暴击治疗** → 治疗量 **×2** + 目标 **+4 士气**，概率**固定不受任何修正**：单体 **12%** / 多目标 **5%** |
| `mark` | object | `{rounds:3, stack:"refresh", dispellable:true, bonus_vs_marked_percent_default:25, enemy_ai_weight:2}` | **dd_reference §1.3（#204）** | 标记**无数值**：加伤走技能 `bonus_vs_marked_percent`（起手 +25，乘法阶段）；敌方 AI 对带 mark 的我方**按 #187 权重制 ×2**；**不可被抵抗**（施加技能仍可 miss）；与嘲讽职责划清（嘲讽=防守 / 标记=进攻） |
| `stat_debuff_default` | object | `{delta:-3, rounds:2}` | combat_math §4 / §10 | **属性减益固定 −3 / 2 回合**（速度也走固定值）；与技能韧性 −15/−10 的张力见 O-23 |
| `stun` | object | `{effect:"skip_own_action"}` | GDD §2.5 | 眩晕跳过本次行动，随后状态结束 |
| `damage_floor` | int | `1` | combat_math §2.3 | 任何来源伤害最低 1 点 |
| `hit_clamp` | object | `{min:55, max:100}` | combat_math §1 | 命中率钳制 [55, 100] |
| `crit_multiplier` | double | `1.5` | combat_math §2.1 | 暴击倍率（未暴击 1.0） |
| `damage_float` | object | `{enabled:false, min:0.9, max:1.1}` | combat_math §2.1 / README 开放题 1 | 伤害浮动默认 1.0 关闭 |
| `speed_float` | object | `{enabled:true, percent:10}` | #163 / GDD §2.5 | 速度浮动 0~10%，每回合重掷 |
| `collapse` | object | `{affliction_pool:["affliction_fear","affliction_selfish","affliction_uncontrolled"], virtue_pool:["virtue_brave","virtue_resolute","virtue_inspired","virtue_focused"], proc: "morale drop >0→0 event"}` | morale §4~§6 / **#193 / O-27** | 崩溃判定：事件触发（士气从 >0 降到 0）；**美德池 4 个全量**（勇猛/坚韧/振奋/专注）；振奋 = 每回合全队 +3（见 §3.4 `virtue_inspired`）；判定后士气留在 0（#67） |

> 士气满 100 的系统行为：给自己上美德（随机抽取 #55）+ 全队 +10（morale_events `morale_full_100`）+ 自身回 50——复位值属行为常量，随 `morale.start` 语义（50）执行，不单列键。

---

### 3.8 `tuning.json` 的 M7 子结构 —— `expedition` / `resources` / `camp`（#240）

> 来源：`expedition.md` §1~§3 · 落地卡 `tasks/m7_expedition.md`（E0/E2/E3/E5/E6）。**仍是 `tuning.json` 内的子对象**，不新开文件。

| key | 结构 | 值（起手） | 说明 |
|---|---|---|---|
| `expedition` | object | `{n_battles:6, **battle_goal:3**, ambush_chance:0.33, pack_slots:12, difficulty_tiers:[{battles:[1,2],mult:**1.0**,stun_resist_pp:**0**},{battles:[3,4],mult:**1.0**,stun_resist_pp:**0**},{battles:[5,6],mult:**1.0**,stun_resist_pp:**15**}], retreat_penalty:{no_death:12, with_death:15}, town_recovery:{hp:"full", morale:"none", clear_states:true}}` | 🔴 **术语（#251）**：**趟（run）= N 场战斗**；**场 = 单场战斗**。🔴🔴 **完成定义（v1.09/#273）＝【走完 6 步】＋【打赢 ≥ `battle_goal`(3) 场】**（"打赢" = **敌方全灭**；起手 **3**、`tuning` 可调、**禁硬编码**）—— **根因**：**"走完 6 步"是【过程】不是【目标】** ⇒ 玩家可"**零代价走完**"（全事件、只掉士气）= **路过**而非完成；加目标后 **"全事件"（0 战）不可能完成** ✅ ⇒ **强制吃战斗损耗** ⇒ **掉血 ⇒ 扎营 ⇒ 要柴火/口粮 ⇒ "摸黑搏补给"终于有理由**（**风险/收益闭环**）；🔴 **不是"结构强制"**（不强制路径含战斗，**保留玩家自由**：可以全选事件，但那样不算完成）；**夜袭 33%**；🔴 **`pack_slots:12`（#248，本轮不启用）**；🔴🔴 **`difficulty_tiers`＝难度递进（v0.92/#250 → #252 → #253 → 🔴 v0.98/#255 定稿）**：**① 敌 HP 递进【已撤销】** —— `mult` **全部 = 1.0**（**实测推翻线性外推**，见下）；**② 难度改由【眩晕抗性】承担** —— `stun_resist_pp` = **第 5~6 场 +15pp**（敌 `30/25/40 → 45/40/55`）；**③ 位移抗性不动**（`#253`：**"位置驱动"是核心定位，削它就是削核心**；备用轴里的"位移抗性"**不得使用**）；**④ 全部走 tuning、不得硬编码；档位标签必须由 `tuning` 生成**（历史上出现过"硬写旧档位乘数（×1.25）"的报告 bug，主程序已修 —— **数值以 `tuning` 为准**）；**接线点**：`ExpeditionSession.BeginExpeditionBattle()` → **`UnitRuntime.ApplyMaxHpMultiplier()` / `ApplyStunResistBonus(pp)`**（**运行时投影，不改 `units.json` 基准**；P4 同源）。<br>🔴 **本次实测的硬教训（#255，写进契约防复发）**：**"回合数 ∝ 敌 HP" 只在"没人变弱"时成立** —— 本作有**虚弱（输出 −25%）/ 死门** ⇒ **正反馈放大**：`敌 HP +10% → 敌人多活 1 回合 → 敌方行动数增加 → 玩家掉血更多 → 虚弱更多 → 清场更慢` ⇒ **+10% HP 实测变成 +64% 回合**（策划线性推算 7.02，实测 **10.45**，差 49%）。⇒ 🔴 **禁止对回合数做线性外推**（与 #216「实测 D vs 估算 D」同源）；**回合带护栏按【链式口径 5~7】**判（见 `m7_verification` V2）。 |
| `resources` | object | `{firewood:**1**, food:12}` | 🔴 **v1.10/#274：起手柴火 `2 → 1`**（口粮 **12 不动**）—— 理由：**保守档靠起手资源就 95% 完成（掉落 0.00）⇒ 起手资源过剩 ⇒ 掉落白给**；柴火 **1** ⇒ **扎营与提亮只能选一个 = 尖锐取舍** ⇒ **"事件可补柴火"⇒ 掉落第一次有价值**；🔴 **口粮为什么不动**：`柴火 1 + 口粮 12 + support_crate 1 = 14 > 12 格` ⇒ **保住"背包强制取舍"**（若口粮也减 ⇒ 不再超格 ⇒ 取舍消失 ❌）；**不足时拒绝且不扣**（禁硬编码） |
| `camp` | object | `{food_tiers:{starve:0,half:3,full:6,feast:12}, respite_base:6, food_scale_by_survivors:true, pep_talk_battles:4, hp_percent_denominator:"roster_max_hp"}` | **食物四档**（Starve 0 → 全队 −20% HP/−15 士气 ｜ Half 3 → 无 ｜ Full 6 → +10% HP ｜ Feast 12 → +25% HP/+10 士气）；**Respite = `respite_base`(6) + 存活人数**（满编 12）；**口粮按存活人数等比缩放**（照抄 DD）；**`pep_talk_battles:4`** = 政委【训话】的**跨场战斗计数**（🔴 **不是** `until_next_recovery`——**扎营不清它，回城才清**，#241） |

> 🔴 **「回城恢复」语义（v0.87 / #245，已生效）——HP 恢复 / 士气不恢复**：
> | 轴 | 回城后 | 说明 |
> |---|---|---|
> | **HP** | ✅ **完全恢复** | （`(b)` **先不做**，保持现状：一次只动一个轴，才能归因） |
> | **士气** | 🔴 **完全不恢复**（**完成档也一样**，保持跑图结束时的值） | 这条是 #245 的裁定 ⇒ **士气成为跨趟资源**，#210「压力长线化」才真正落实；与 DD「**战斗伤害会治 / 压力不会降**」对齐 |
> | **状态**（虚弱 / 死门后遗症） | 清除 | （`(c)` **先不做**，保持现状） |
>
> ⚠️ **连带义务（必须写明，设计已认账）**：**士气完全不恢复 ⇒ 跨趟单调下降 ⇒ 最终必崩**；而 **"崩"不是问题（那是硬核）——问题是【没有出口】**（DD 的出口是 Tavern/Abbey：**花钱**减压；**M7 只做跑图闭环，暂无花钱系统**）。
> ⇒ 🔴 **M7 处置**：**规则落、跨趟平衡不验**（留给 **M8** 配"减压渠道"）；**M7 验收不变**（单趟 6 场完成率 [40,70]）；🆕 **诊断读数 = 连续 3 趟的士气曲线**（看累积斜率、看第几趟崩——见 `m7_verification` **⑱**）。
> 📌 **实现纪律**：**不得**在回城时把士气重置为基准 50（**完成/撤退/全灭三档都不重置**）；撤退惩罚仍在**撤退那一刻**结算（§3.3 `retreat_success`），回城**不二次扣**。**P20 ⑫** 校验。
>
> 🔴 **另外两条度量口径（#242/#244，已生效，必须写实现）**：
> 1. **所有 HP%（食物档位 −20%/+10%/+25%、扎营技能 +15%/+5%、曲线读数）的分母 = `roster_max_hp`＝整编名册最大 HP 之和**（**阵亡者仍在名册、按 0 HP 参与**；**分母固定 6 人**）——否则"人少了百分比自然虚高"，各档比的是**不同集合**，是非单调的根因之一。
> 2. **`Respite = respite_base(6) + 存活人数`**（满编 **12**；减员后天然变少）。
>
> 🔴 **口粮不足的规格（v0.86 / #244 补缺口——此前只写了四档效果，没写"不足时怎么办"）**：
> ① **需要更多口粮的档位 → 不可选**（灰显 + tooltip「口粮不足」）；
> ② **选 `Starve` 照常受罚**（全队 −20% HP/−15 士气）——它是**主动选择**，**不是免费**；
> ③ 🔴 **禁止"自动退化为 `starve` 且不扣"**——那是**免费午餐**，会造出**非单调**（P20 ⑨）。

> 🔴 **`DurationType` 的两条不同类型（#241，必须分清，否则扎营会误清训话）**：
> | 效果 | duration 类型 | 扎营时 | 回城时 |
> |---|---|---|---|
> | **`deaths_door_recovery`**（死门后遗症） | **`until_next_recovery`**（到下次恢复） | ✅ **清除** | ✅ 清除 |
> | 🔴 **政委【训话】/ 原「打气」** | 🔴 **`battles:4`**（跨场战斗计数，随场次递减） | ❌ **不清除** | ✅ 清除 |
> | **磨刀 / 加固甲胄**（下一场战斗生效） | **`next_battle`**（下一场战斗内） | ❌ 不清除（**打完下一场即失效**） | ✅ 清除 |
> **若把"训话"判成"到下次恢复"，一扎营它就没了**——而 DD 原意是"持续 4 场战斗"；**两者并列、语义不同，不得合并**（`expedition.md` §3.2）。

### 3.9 `camp_skills.json` 🆕 —— **角色专属**扎营技能（**12 个** = 4 原型 × 3，v0.84/#242）

> 来源：`expedition.md` §3.2（**DD 式**：`Each class has their own skill sets`）。**12 点 Respite 是共享池，但花它的是"某个角色用他自己的技能"** → 真正的决策 = "**哪个角色的哪个技能 × 点数怎么分**"。
> 🔴 **不做 DD 的 3 个共享技能**（Encourage / Wound Care / Pep Talk 不搬，#243 用户拍板）——**扎营能力完全由"带了谁"决定**。

| id | 名称 | **`owner_archetype`** | 点数 | 目标 | 效果 | duration |
|---|---|---|---|---|---|---|
| `warrior_whetstone` | 磨刀 | **`warrior`** | **2** | 单个友方 | **下一场战斗伤害 +25%** | `next_battle` |
| `warrior_keep_watch` | 轮流守夜 | **`warrior`** | **3** | 全队 | **免疫接下来的夜袭**（一次） | **run 级标记**（`ambush_immunity`；**保留至消耗或本趟结束**） |
| `warrior_joke` | 笑谈 | **`warrior`** | **1** | 单个友方 | **+8 士气** | 即时 |
| `tank_reinforce_armor` | 加固甲胄 | **`tank`** | **2** | 单个友方 | **下一场战斗物防 +4** | `next_battle` |
| `tank_stand_guard` | 站岗 | **`tank`** | **3** | 全队 | **免疫接下来的夜袭**（一次） | **run 级标记**（`ambush_immunity`；**保留至消耗或本趟结束**） |
| `tank_cook_meal` | 埋锅造饭 | **`tank`** | **2** | 全队 | **+5 士气** | 即时 |
| `medic_bandage` | 包扎 | **`medic`** | **2** | 单个友方 | **+15% HP** 且**清除流血** | 即时 |
| `medic_medicine` | 配药 | **`medic`** | **3** | 单个友方 | **清除【虚弱】与【死门后遗症】** | 即时 |
| `medic_tend` | 照料 | **`medic`** | **1** | 单个友方 | **+5% HP** | 即时 |
| `commissar_encourage` | 鼓舞 | **`commissar`** | **2** | 单个友方 | **+15 士气** | 即时 |
| `commissar_pep_talk` | 训话 | **`commissar`** | **2** | 单个友方 | **4 场战斗内 −15% 士气伤害**（原「打气」） | **`battles:4`** |
| `commissar_rally` | 战前动员 | **`commissar`** | **3** | 全队 | **+10 士气** | 即时 |

> **字段**：`{id, name, **owner_archetype**, cost, target: single_ally\|team, effects:[…]}`。
> 🔴 **`owner_archetype` 是"谁能用"的唯一依据**：扎营时**只有该原型的角色**能把这条技能放进自己的行动槽；**使用者与 `owner_archetype` 不一致 → 不可选（灰显）**——**否则"角色专属"会退化成"共享"**（验收项，见 `m7_verification` **V8** 与 **P20 ④**）。
> **点数不足 → 不可选（灰显）**；**Respite 是"分配"而非"全队生效"**（决策密度来源）。
> ✅ **连带（设计意图）**：**编成影响扎营能力**（带 2 军医 ⇒ 治疗/清减益类多；带 2 战士 ⇒ 战斗准备类多）——**编成决策从"单场层"延伸到"远征层"**。

> ## 🔴 跨场效果的**清算规则**（O-67 ✅ 已定，v0.86：用户"按架构建议"）
> 三条效果类型**各有一套清算规则**，**不得互相套用**：
>
> | 效果 | 类型 | 扎营 | **本趟中止/回城** | **打完下一场** | 事件可读性 |
> |---|---|---|---|---|---|
> | `deaths_door_recovery` | `until_next_recovery` | ✅ 清 | ✅ 清 | — | `BuffRemovedEvent(Reason:"recovery")` |
> | 政委【训话】 | `battles:4` | ❌ **不清** | ✅ 清 | 每打完 1 场 **−1**；到 0 清除 | 每次递减写 `BuffAppliedEvent`（`Stacks/剩余场次`）或专用字段；到 0 写 `BuffRemovedEvent(Reason:"expired")` |
> | 磨刀 / 加固甲胄 | `next_battle` | ❌ 不清 | ✅ 清 | ✅ **下一场结束时清除** | 同上（`Reason:"expired"`） |
> | 轮流守夜 / 站岗 | **run 级标记 `ambush_immunity`** | ❌ 不清 | ✅ 清 | — | 施加写 `CampSkillUsedEvent`；**消耗写 `AmbushTriggeredEvent(immunityConsumed:true, ambushSuppressed:true)`**；未消耗则本趟结束清除 |
>
> 🔴 **两条关键判定（O-67 结论）**：
> 1. **`next_battle` 不结转**：若本趟**没有"下一场"**（撤退/全灭/打满 6 场后回城）→ **效果作废**，**不得留到下一趟**（它绑的是本趟的"下一场"）。
> 2. **`ambush_immunity` 保留至消耗或本趟结束**：**当次未触发夜袭时不清除**（即"**下一次夜袭必被免疫**"，同 DD"取消下一次夜袭"）——**不是"只在本次判定有效"**。
>
> ⚠️ **可读性红线**：以上**每一条清除/消耗都必须能从事件流读出**（`BuffRemovedEvent`/`AmbushTriggeredEvent`），否则 `m7_verification` ⑫/⑭（扎营花费分布、夜袭次数）统计不到 → 又是"事件字典缺项"（`logging.md` §1）。

### 3.10 `expedition_nodes.json` 🆕 —— 选路节点表（M7 最小版）

> 来源：`expedition.md` §1.3（**阶段一**：普通战 + 事件节点；**阶段二**再加 `elite`）。

| 字段 | 说明 |
|---|---|
| `nodes[]` | 每个节点：`{id, type: battle\|event, …}`；**`elite` 属阶段二（本卡不做）** |
| `nodes[].event` | 事件节点：`{title, **cost:{morale:{delta:-5, scope:"team"}}**, options:[{id, label, effect:{resource:{kind,delta}} \| morale:{delta,scope}}]}` |
| 🔴🔴 **【必然代价】层（v1.08/#272 新增，**必须有**）** | **每个事件节点必须声明"必然代价"**（起手 **全队 −5 士气**＝赶路疲惫，`tuning` 可调）—— 🔴 **"不允许跳过"不够**：**两个选项都无代价时，二选一仍然是免费的**（实测印证：保守档"全事件"路线 **100% 完成 + 0.00 补给** ⇒ **零损耗通道** ⇒ 冒险纯亏 ⇒ 光照计白做） |
| 🔴 **可选收益（二选一，v1.08/#272）** | **A 搜刮** → **+2 补给**（柴火/口粮）但**再 −5 士气** ／ **B 直接走** → 无额外 ⇒ 于是"**全事件**"路线也**每节点 −5 士气** ⇒ **与"战斗"路线可比** ✅ |
| 🔴 **两选项强制** | **事件节点必须恰好 2 个选项**，且 **UI 不允许跳过**（必须二选一），**且必须带【必然代价】**（两者缺一即退化成"免费资源点" —— `#240` + `#272`；**P20 ⑤ 校验**） |
| 选路 | **线性 6 步 × 每步 2 选 1**；**节点序列生成必须写 `RngDraw`**（`PathChosenEvent(from,to,nodeType)`） |

### 3.11 远征级事件（M7 新增，接 §8 事件族）

`PathChosenEvent(from,to,nodeType)`（1）· `ResourceChangedEvent(kind,delta,newValue,reason)`（2）· `CampStartedEvent`（2）· `CampFoodChosenEvent(tier)`（2）· `CampSkillUsedEvent(skillId,target,respiteLeft)`（2）· `CampEndedEvent`（2）· **`AmbushTriggeredEvent(roll, triggered, immunityConsumed, ambushSuppressed)`**（2；🔴 **O-67**：**免疫被消耗时 `immunityConsumed=true` 且 `ambushSuppressed=true`（不插入夜袭战）**，**未消耗则可读为标记仍在**）· `EventNodeResolvedEvent(nodeId,choice,effect)`（2）· `TownReturnEvent(outcome,moraleBefore,moraleAfter,penaltyApplied)`（1）· 🔴 **M7.5 新增（#258 / 🔴 v1.01 字段裁定）**：**`LightChangedEvent(from, to, tier, reason)`**（1）—— 🔴 **字段口径以本契约为准**（字段名 `from/to/tier/reason`），🔴 **`reason` 取值采用实现侧更细的集合** = **`{start, advance, brighten, camp, event_torch, event_dark}`**（**合并两版**：形状用契约版、粒度用实现版；`advance_node` → **`advance`**）。理由：**(a) `tier` 必须有**（`#262` 硬要求：否则复测**无法归因"这一场的难度是哪个档给的"**）；**(b) 事件节点的"举火把 / 摸黑"必须是两个 reason**（它们是**不同的玩家决策**，V4 的"摸黑 run"口径要靠它统计，合并成一个会丢信息）；**(c) 光照每次变化必发**（㉑/㉒ 只能从它统计）· 🔴 **`ScoutResultEvent(roll, success, revealedNodeType)`**（2；**架构侧补充要求**：`m7_5_verification` **V5** 要统计"侦察成功率"与"因侦察改变选路次数" —— **没有这个事件就统计不到**，属 P17 可读性缺口）。

> 🔴 **纪律不变**：远征层的数字（资源、完成率、扎营/夜袭统计）**必须能从事件流算出**（`logging.md` §1）；**所有随机（选路/夜袭/事件结果/敌人抽取）写 `RngDraw`**；新增事件**不得引入新抽取源**。

### 3.12 `items.json` 🆕 —— 背包物品（v0.90 / #248；M7 落"SP 恢复物品"，其余随 M7.5）

> 来源：`expedition.md` **§5.6**。**背包格子 = `tuning.expedition.pack_slots`（12）**；**每件物品占 `slots` 格**（起手均为 1）。
> 🔴 **背包的意义 = 取舍**：`2 柴火 + 12 口粮 = 14 格 > 12` ⇒ **装不下** ⇒ **"带什么出门"是远征前的决策**（DD 也是先买补给再出发）。

| id | 名称 | 占格 | 用途 / 效果 | 来源 |
|---|---|---|---|---|
| `firewood` | 柴火 | **1** | **扎营许可**（1 次 1 份） | 起手 2 / 事件节点 |
| `food` | 口粮 | **1** | **扎营吃饭**（食物四档） | 起手 12 / 事件节点 |
| **`support_crate`** 🔴 | **支援箱**（**`#265` 定名**） | **1** | 🔴 **在背包里时：每回合 +1 SP**（**由该物品的 buff 提供**，`duration:"while_carried"`）；**不带 → SP 完全不恢复**；🔴 **必需**（推荐配置必带）；**起手默认带 1 个** | 起手默认 1 个 |
| **`support_pack`** 🆕 | **支援包**（**消耗品**） | **1** | **一次性：消耗 1 个 → 立即 +2 SP**（🔴 **必须钳 `cap 4`**）；🔴 **使用不消耗行动**（架构裁定 v1.06：它是"**用库存换资源**"、不是技能；支援位已受 SP 成本 + Pass −5 士气双约束，再加行动属**过罚**）；🔴 **SP 的加减必须由 `BattleDirector` 结算**（`blueprint` §9.11：**SP 只有一个写入口**），远征层只当库存走 `IInventory.TryConsume` | 事件 / 战斗掉落（越暗越多） |

> **字段**：`{id, name, slots, carryable:true, effects:[…], buffs:[…]}`。
> 🔴 **`support_crate` 的恢复是"物品 buff"**：`buffs:[{id:"sp_regen", modifiers:[{kind:"resource_mod", sp_regen_per_round:1}]}]` → **`tuning.support_points.regen_per_round` 引擎值 = 0**（M7.5 期），恢复由它在场与否决定（P19 / P15 / **P21 ⑨⑭**）。
> 🔴 **UI 硬要求（#248，否则它变成陷阱）**：**不带该物品时，必须明确提示「支援位不会恢复 SP」**——否则玩家不知道**为什么支援位废了**，那不叫决策，叫**惩罚玩家没读设计文档**（见 `m7_verification` **V10**）。
> 🔴🔴 **命名裁定（v1.04 / `#265`，**必须按此实现**）**：**`support_crate`（支援箱）≠ `support_pack`（支援包）** —— **两个不同的物品**：
> · **`support_crate` = 载体/装备**（`while_carried`：在包里就每回合 +1 SP；**不带 ⇒ SP 完全不恢复 ⇒ 支援位直接废掉** ⇒ 🔴 **必需**）
> · **`support_pack` = 消耗品**（一次性 +2 SP ⇒ **可选**）
> 🔴 **两条硬要求**：**(1) `tuning.inventory.recommended_loadout` 必须包含 `support_crate`（不是 `support_pack`）** —— 否则**默认配置下 SP 完全不恢复**，成为**玩家看不见的陷阱**（与 #248 同源）；**(2) 数据与事件里必须区分两个 id，不得都叫"支援X"**（同类教训：`#253` 三抗性并列、`#247` `(b)/(c)` 同名异义 —— 纪律 15）。
> ✅ **定名已闭**：中文名 = **支援箱**；占位名「后勤箱」及旧 id **`sp_support_crate` / `sp_supply_pack` 作废**（**勿双建**，否则又会分裂成两套物品）→ `open_issues` **O-69 ① 关闭**。

### 3.13 M7.5 地牢层结构 —— `light` / `scouting` / `inventory`（#258）

> 来源：`tasks/m7_5_dungeon_layer.md` **D0/D1/D2**。**仍是 `tuning.json` 子结构**；物品定义走 `items.json`（§3.12）。
> 🔴 **本层的存在理由**：`#256/#257` 证明 **A1 与 A2 在 M7 机制集合下不可兼得** ⇒ 按**纪律 16 推论**，缺的是**"让玩家选择难度的机制"**，不是数值。

| key | 结构 | 值（起手） | 说明 |
|---|---|---|---|
| `light` | object | `{start:100, node_step:-30, brighten:{delta:30, firewood:1}, tiers:{radiant:76..100, dim:51..75, shadowy:26..50, dark:1..25, black:0}, effects:{…5 档 × 7 项…}, loot:{radiant:{}, dim:{food:1}, shadowy:{firewood:1}, dark:{firewood:1,food:1}, black:{firewood:2,food:2}}}` | **光照 0~100**；**进图 100**；🔴🔴 **v1.14/#278：前进消耗 `−15 → −30`** —— **根因（由 ㉒ 分布数据定到）**：**6 节点 × (−15) = −90，而起手 100 + 【扎营回满 100】（每趟至少扎营一次）⇒ 光照永远降不下来**（实测 **radiant 56~68% / dim 31~33% / shadowy ≤9% / dark ≤2% / black 0%**）⇒ **玩家"选暗"的唯一方式是【不扎营】= 放弃恢复** ⇒ 🔴 **「光照」与「恢复」被绑死 ⇒ 玩家无法【独立】选光照**（而"自选难度"正是光照计存在的唯一理由，`#258 §0`）；⇒ **−30 后：`6×(−30) = −180`，不管它 ⇒ 第 3 个节点就进 Dark(10)**；**全程维持亮需 2 次回满，而柴火只有 1（+ 掉落）⇒ 光照成为必须持续管理的资源** ✅；🔴 **为何不改成"扎营不再回满"**：**保留 DD 的"扎营回满"**（不偏离 DD），且**"掉落柴火"的价值同时大增**（1 份柴火 = 一次回满）；🔴 **自洽性**：**前进 −30 / 提亮 +30 ⇒ 提亮 = "买一个节点"**；**扎营回满 = "买三个节点"**（但有夜袭风险）。<br>🔴🔴 **收益端 = 补给掉落（v1.12/#276：「类型 + 份数」，柴火优先）**：**Radiant 无 / Dim `1 口粮` / Shadowy `1 柴火` / Dark `1 柴火 + 1 口粮` / Black `2 柴火 + 2 口粮`**，**去掉掷骰** —— **为什么必须"柴火优先"**：实测掉落只给口粮 ⇒ **口粮不能换扎营（扎营要柴火）** ⇒ **补给再多也换不来续航** ⇒ **链断在"掉落类型"与"续航资源"不匹配**；**"摸黑 ⇒ 多一份柴火 ⇒ 多一次扎营 ⇒ 直接变成续航"** ✅；⚠️ **顺带解决"提亮次数恒为 0"**（柴火 >1 时提亮才有机会被选中）。**提亮 +30 / 1 柴火**；🔴 **扎营 → 回满 100**（**柴火 = Torch + Firewood 合一**）；**档位边界：`>75` = Radiant、`75` = Dim** |
| `light.effects` | object | 5 档 × {`player_morale_damage_pct`, `enemy_acc`, `enemy_dmg_pct`, `enemy_crit_pct`, `player_ambush_pct`, `player_crit_pct`, `scout_bonus_pct`} | **只改敌方 ACC/DMG/暴击 + 我方士气伤害/暴击/被偷袭 + 侦察加成**；🔴 **不改任何一方的 HP** ⇒ **不拉长战斗**（这正是它避开 A1 的原因） |
| `scouting` | object | `{base:0.25, light_bonus:{radiant:0.15, dim:0.075, shadowy:0, dark:0, black:0}, reveal_depth:1..3}` | 🔴🔴 **加成随光照变暗【单调不增】（即越亮越好）—— 这是【设计意图】，不是 bug（v1.24/#294 裁定）**：`越亮 ⇒ 看得清（信息多）＋收益少（掉落少）` / `越暗 ⇒ 看不清（信息少）＋收益多（掉落多）` ⇒ 🔴 **那才是一个【真实的取舍】**；反过来说，**若"越暗又看得清"，那"亮"就【没有任何理由】了** ⇒ ⚠️ **不得改成"越暗侦察越好"**（`#294` 已裁定"不改"，并把主程序用例里"越暗侦察越好"的断言定性为**假设、不是契约**）。**M7.6 起**：**成功揭示【前方 1~3 步拓扑】**（不再是"下一个节点类型"）⇒ **`RevealWithin` 不泄露更深层**；失败 ⇒ **不揭示任何东西**；**写 `RngDraw`**；UI 必须与"未知"区分。⚠️ **接手 M7.6 流程后须监控**：**若激进档因"看不清"而完成率掉太多**（而不是因为战斗难）⇒ **"信息惩罚过重"，再议** |
| `inventory` | object | `{slot_cap:12, stack:{food:false}, recommended_loadout:{firewood:1, food:9, support_crate:1}, items:…}` | **格子 12**；**口粮不堆叠** ⇒ 默认"想装的"（柴火 + 口粮 12 + 支援箱 1）**> 12 ⇒ 必然超格**（起手柴火 1 时 `1+12+1 = 14 > 12` ⇒ **背包取舍保住**）；🔴 **v1.11/#275 定稿：推荐配置 = `{柴火 1, 口粮 9, support_crate 1}` = 11 格**（🔴 **刻意留 1 格空位，不是装满 12**）—— **理由：「摸黑搏补给」要有地方放**；**若默认装满 12 格，路上捡到的补给必须先丢掉东西 ⇒ 收益端在入口就被堵住**（与 `#275`"补给要产生价值"是同一件事）；🔴 **必须带 `support_crate` 而非 `support_pack`**（否则默认 SP 不恢复）；🔴 **局内不可改**；**超格不可携带**（UI「背包已满」）；🔴 **包满时不得静默丢弃** —— 走**「丢弃哪一格」选择**（或**就地消耗口粮腾格**） |

> 🔴 **SP 恢复物品的 `DurationType` 由架构定（D6 #3）—— 答案是：两者都不合适，需第三类**：
> | 候选 | 为什么不合适 |
> |---|---|
> | `until_next_recovery` | **扎营会清它** —— 但玩家扎营时**仍背着那个物品**，SP 恢复不该消失 ❌ |
> | `remaining_battles: N` | 它是**计数**语义（训话那类），而 SP 恢复是**持续态**，不该"用几次就没了" ❌ |
> | 🔴 **`while_carried`（新增）** | **"只要在背包里就生效"** —— **扎营/回城都不清**，**只有丢弃/掏出才失效** ✅ |
> ⇒ 写入 §2.1 `DurationType`；**与 #241 的"三类不可合并"并存，现在是四类**（`until_next_recovery` / `battles` / `next_battle` / **`while_carried`**）。

> 🔴 **测量口径（`#259`+`#263`，写死）**：**"摸黑 run" ＝【跑图结束时（回城前）光照 ≤ 50】（Shadowy 及以下）** —— **不得用"中途经过 Shadowy"**（什么都不做也会经过 ⇒ 无区分度）；它反映的是"**玩家有没有花柴火提亮**"。**A1 的闸只在不摸黑 run 上成立**（摸黑 run 允许超标 —— 那是玩家自选风险，见 `m7_5_verification` V1/V4c）。
> 🔴 **取档时点（O-72 ✅ 已裁定，#262）**：**进节点时取档、本场战斗内固定**；🔴 **"本场固定"是自然结果、不是额外规则** —— **光照只在【节点之间】变化**（前进 −15 / 提亮 +30 / 扎营回满 / 事件 ±20），且我们**刻意不做"战斗内改光照"的技能**（DD 有，那会动手序与数值）⇒ **不要为此加一条"战斗内锁定"机制**；**每次光照变化立即写 `LightChangedEvent`（含 `tier`）**，供复测归因。
> 📌 **另登记一条设计论证（`m7_5_dungeon_layer.md` §D0.6(2)，防被当"垃圾选项"删掉）**：**「提亮 +30」不被「扎营回满」支配** —— 扎营有 **33% 夜袭**风险，提亮**无风险** ⇒ 提亮 = "花 1 柴火买 +30 且不冒险"（**应急 + 规避夜袭**）。

### 3.14 M8 局外养成层结构 —— `hamlet` / `roster` / `economy`（骨架 · `#281~#285`）

> 来源：🆕 `doc/modules/hamlet.md`（DD Hamlet wiki 原文 + M8.0 规格）。🔴 **本节为【准备态骨架】**：**结构与关键值已定，字段名随 M8.0 开工推进**（策划要求"不要提前细化"）。
> 🔴 **生命周期归属**：**本节三者都是【跨会话状态】**（与 `ExpeditionSession` 的"一趟"、`BattleDirector` 的"单场纯"**分属不同拥有者**；组合根注入引用，**不得反向持有**）。

| key | 结构（骨架） | 值（M8.0 起手） | 说明 |
|---|---|---|---|
| `economy` | object | `{gold:0, scale:{per_battle:1, per_run:"3~6", per_relief:3}}` | 🔴 **金钱先定【比例】**（`#283` 7.1）：**一场战斗 = 1 单位 / 一趟 ≈ 3~6 / 一次减压 = 3** ⇒ **一趟能减 1~2 次压**（绝对值待实测校准）；🔴 **来源必须与【光照档 + 战斗数】挂钩**（跨趟回报；`#276~278` 的补给只是单趟内的）；**传家宝（Busts/Crests/Deeds/Portraits）留 M8.1** |
| `roster` | object | `{cap:12, heroes:[{id, archetype, name, level:1..6, **morale:0..100**, traits:[…], hp_bonus, atk_bonus}]}` | 🔴 **名册上限 12**（`#283` 7.4：出征 6 + 替补 6 ⇒ **"轮换休息"成为策略**）；🔴🔴 **英雄士气 `morale` = 跨趟持久（v1.18/#287）** —— **它不是新字段语义，而是 `#245`「回城士气完全不恢复」的落地**（此前士气是**单趟状态**（`ExpeditionSession.Retained`、回城即随趟结束）⇒ **与 `#245` 直接矛盾**）；**新兵开局士气 = 基准 50**（否则新老兵士气分属两套语义）；**名册没有的待命人员继续持有自己的士气**；**英雄个体 = 名字 + 等级 + 个体差异**（7.2）；**等级 1~6 只给属性小幅度**（🔴 **可测定义：`level_growth = HP +2/级、攻击 +1/级`**）**🔴 不升技能**（技能升级留 M8.1，7.6）；**特质每人 2~3 条【小幅、正负都有】—— 🔴 可测定义（v1.17/#286）：`伤害 ≤15%` / `士气伤害 ≤20%`，且【正负兼有】必须可校验**（⚠️ **"小幅/正负都有"这类模糊措辞必须有可测定义** —— 实测 P22 当场抓到 3 个不合规英雄，`#286`）；🔴 **不碰技能表 ⇒ `#243`「只用角色专属扎营技能」保持**（7.8） |
| `sanitarium` | object | `{diseases:[{id, hp_delta, attack_delta, morale_delta, contract_chance}], services:[cure_disease, remove_negative_trait, lock_positive_trait]}` | （M8.2 片 a）**三种疾病**（腐疮/热病/蚁行）**各带可测惩罚**（HP/攻击/士气的 `delta`，🔴 **不得三项全 0**）+ **每趟患病概率**（0.15 / 0.12 / 0.10）；**三种服务 = `cure_disease` / `remove_negative_trait` / `lock_positive_trait`** —— 🔴 **全部消耗【金钱 + 传家宝】**（**这是 M8.1 传家宝的稳定出口**）；🔴 **疾病是【跨趟状态】**（与士气/金钱同层，`Roster.DiseasesOf`）；**惩罚必须【投影到单位】**才算生效（否则"只记在账本上"= 红线 21） |
| `hamlet` | object | `{buildings:[tavern, abbey, stage_coach], prices:{…}, relief:{side_effect:"next_run_start_morale_-N"}}` | **M8.0 只做 3 个**：**Tavern**（减压，**概率型副作用**）/ **Abbey**（减压，**同价同效但更稳**，7.3）/ **Stage Coach**（**免费招募**）；🔴 **减压副作用 = 下一趟开局士气 −N**（Tavern 概率高 / Abbey 更稳）—— 这正是 **M7 遗留的 `#245`「士气没有出口」的出口**（`O-68`）；🔴 **Tavern vs Abbey 必须同价同效、风险不同**（"选风格"而非"选更优"） |

> 🔴 **与 A1 的关系（`#283` 7.9）**：**A1 按【新兵等级】测**；**高等级队另立判据**（长线成长 × 单场判据的矛盾 ⇒ **给不同成长阶段立不同判据**，不是"让判据不变"）。
> 🔴 **英雄个体化是本层最大结构改动**：把 `units.id`（**原型**）变成 **原型 + 实例** ⇒ 影响 `ExpeditionSession`/`BattleDirector` 的调用方；**若改动面超出预期，先回策划**（`#285`）。

---

## 4. 加载管线与校验规则

### 4.1 JSON → C# 绑定方式（建议契约，非实现代码）

- 路径：`res://data/{units,skills,morale_events,buff_defs,enemy_ai,formation,tuning}.json`，**启动时一次性加载**，校验通过后冻结为**只读**注册表（C# 侧以 `ImmutableArray`/只读容器 + 服务单例持有），运行期不再读盘、不再变更。
- 绑定：`System.Text.Json` 反序列化到 record 类/类（`JsonSerializerOptions` 关闭大小写不敏感、字段一律经 `[JsonPropertyName("snake_case")]` 显式映射）；枚举用小写字符串值 ↔ C# PascalCase 枚举（§2.1）。加载器归属 `res://scripts/data/`（_conventions §3）。
- 数值语义：百分比字段为整数百分数；倍率/系数为 double；多段伤害为 `segments` 数组（§3.2）；"每场 N 次 / CD"为技能运行时计数（每场计数初始化自 `use_limit`）。
- 加载失败策略（开发期 fail-fast）：任一文件缺失 / JSON 语法错 / 校验失败 → 启动报错并给出"文件 + 记录 id + 失败规则"，**禁止带病进入战斗**（切片为 1 场定胜负，静默降级会掩盖数据错误）。

### 4.2 自动校验规则清单（每条给判定式）

| # | 校验名 | 判定式（伪码） | 依据 | 失败处理 |
|---|---|---|---|---|
| P1 | 全库 id 唯一 | `∀f∈{units,skills,enemy_ai,buff_defs}: ids(f) 无重复`，且 `skills.owner_unit ∪ formation.initial_roster[].unit ⊆ units.id` | 引用完整性 | 启动报错 |
| P2 | 技能引用完整性 | `∀s∈skills: s.target.side ∈ {player,enemy}`；`s.target.scope=slots ⇒ 0≤slot≤(side=player?6:4)`；`s.effects[].buff_id（若有）∈ buff_defs.id`；`s.owner_unit ∈ units.id` | glossary §1 位置编号 / enemy §1（敌方只有 4 位） | 同上 |
| P3 | **覆盖校验（skill_data §6 接口口径：只数池内技能）** | `∀u∈player_units: ∀pos∈1..6: count{s∈skills(u) ∧ ¬pool_external: s.self_slots="all" ∨ pos∈s.self_slots} ≥ 2`（战斗位与支援位分别计入）；且每角色 ≥1 个 `self_slots="all"` 池内兜底（战吼/急救/战场鼓舞）；**池外移动兜底**：`∀u: count{s∈skills(u) ∧ pool_external: 移动} ≥ 1`（任何战斗位 1~4 恒有可用技能） | skill_data §6 / character §2 / GDD §1.3.1（#180） | 启动报错；§6 表声明数与逐行自数口径差异仍按"≥2 硬校验、禁相等断言"；**v0.45 起仅数池内，池外另计** |
| P4 | 派生量不入库 | `∀u∈units: u 不含 mental_reduction 与 virtue_rate 字段`（schema 级白名单校验，反序列化模型无此属性即可） | glossary §7.1 注 | 结构校验（建模即满足） |
| P5 | 士气事件 ↔ 技能对账 | `∀e∈morale_events: e.source 指向技能 ⇒ skills 中对应技能 morale_effects 含同 delta/scope`（战场鼓舞 +15 与 morale_events `battle_inspiration`） | morale §2 与 skill_data §4 | 启动报错（防止双源漂移） |
| P6 | tuning ↔ buff/技能常量一致 | `tuning.bleed.per_round_damage == buff_defs(bleed).modifiers.damage` 且 `tuning.bleed.rounds == buff_defs(bleed).duration.value`；`tuning.weak.*` 与相关技能/状态无矛盾 | combat_math §4 与 §3.4 | 启动报错（同一数值两处定义，改动须同步） |
| P7 | 敌人数据边界 | `∀e∈units(side=enemy): e.deaths_door_resist == null`；enemy_ai 的 rules[].skill_id 属于该原型且 `self_slots` 与 AI 触发位置不矛盾（如后撤 self_slots=[1,2] 与 ranged rule1 一致）；`target_preference` 与原型一致（**🔴 v0.76：三原型均 `random`**，见 enemy.md §2.1/§5.4（#231）） | enemy §1 / §2.1 / §5.4 / **#231** | 同上 |
| P8 | 数值范围 | 百分比 ∈ [0,100]；`resilience ∈ [0,100]`；`hp/attack/speed > 0`；`damage.segments` 非空（当 damage 非 null）；`use_limit.cooldown.value ≥ 1` | glossary §7.1 / 通用约定 | 同上 |
| P9 | 士气事件表完整性 | morale_events 必须包含 §5.2 全部 14 行 id（缺失=实现期士气数值静默丢来源） | combat_math §5.2 | 启动告警/报错（缺失即失败） |
| P10 | 派生一致性抽查 | 运行时复算样例（combat_math §7.1 单次伤害表）应复现文档数字（如战士→近战小兵 = 9）；该样例验证属 M2 验收而非启动校验 | verification §1 / README M2 | 测试阶段断言 |
| P11 | **目标语义一致性（#178/#179）** | `∀s∈skills: (tags 含 aoe) ⇒ scope=slots`（aoe 只允许"范围型多格目标"）；切片 aoe 集 = {`warrior_sweep`, `caster_mental_shock`}；非 aoe 的 `slots`/`any_ally` 技能在执行层必须走**选一**路径（SkillTargetResolver 输出候选池 + 调用方选择，禁止"范围内全命中"） | GDD §1.2 / skill.md §2 字段3 / O-38 | 启动报错（aoe 标签越界）+ 执行层行为断言（单体技能单次结算） |
| P12 | **池外通用技能一致性（#180/#191）** | `∀s∈skills: s.pool_external=true ⇒ id=="move" ∧ scope=move_range ∧ self_slots=[1,2,3,4] ∧ damage==null ∧ effects==∅ ∧ use_limit.type=none ∧ owner_unit==null ∧ target.distance==null`；`scope=move_range ⇒ pool_external=true`；**池外技能恰 1 条**；**过滤一律按 `pool_external` 标志，禁止 `id.EndsWith("_move")`** | skill.md §1/§1.1 / feat_pack F1.3 / #191 | 启动报错（池外滥用 / 残留 `*_move` / 误填 distance） |
| P13 | **敌人目标选择一致性（#185 → 🔴 #230/#231 改写 / #186/#187/#192）** | `∀a∈enemy_ai: a.target_preference ∈ {**random**,lowest_hp,backmost,lowest_morale}` 且与原型映射一致（**v0.76：三原型均 `random`**；其余三值保留为枚举但切片不用）；**`random` 的抽取必须写 `RngDraw`，且池内唯一候选时不得掷骰**；**射手仍只打 5/6 后排**（目标位限制不变）；`a.taunt_weight ≥ 1`（起手 3，全局同值）；`skills(caster_fear_whisper).target.slots == [1,2,3,4]`（#186）；**taunt 来源 = 我方带 `taunt` buff 者**（`polarity=="positive"` ∧ `dispellable==false` ∧ 施加=自身；EnemyAi **禁止**依赖 `ArchetypeId`）；taunt 抽取写 `RngDraw` | state **#230/#231**/#186/#187/**#192** / O-44 / O-46 / **O-53** | 启动报错 + 执行层确定性断言（**池内随机抽取入日志**） |
| P14 | **单位移动距离与池外数据化（#191）** | `∀u∈player_units: u.move_distance ≥ 1`（切片 1~2）；`count{s∈skills: s.pool_external} == 1`（通用 `move`）；`move_range` 候选解析用例：距离取自**施法单位** `move_distance`，`target.distance` 必须为 null | feat_pack F1.3 / state #191 | 启动报错 + 执行断言（±N 候选/空位 NoTarget/两点互换） |
| P15 | **士气 buff 消费白名单（#193，v0.48）** | `∀b∈buff_defs: b.modifiers[].kind ∈ {stat_mod,state_flag,damage_mod,prob_mod,**resource_mod**}`；`damage_mod`/`prob_mod` 槽位 ∈ {`dealt_damage_mult`,`next_attack_mult`,`crit_bonus`,`deaths_door_resist_bonus`,`immune_fear`}；🔴 **`resource_mod` 槽位 ∈ {`sp_regen_per_round`}（v0.90/#248：SP 恢复物品用；**只允许"每回合 +N SP"，不得借此直接改 `start`/`cap`**）**；`tuning.affliction_proc_percent` 是折磨 proc **唯一**概率源（禁硬编码 33）；`collapse.virtue_pool` 恰好 4 项；折磨 proc 与失控换目标的新随机**必须写 `RngDraw`** | feat_pack F2.2~F2.4 / state #193 / O-51 | 启动报错（modifier 越界/美德池不全）+ 执行层确定性断言（无双重计算） |
| P16 | **增援 M 导出契约 + 复测 D 输出（#196+#198，v0.49/v0.50）** | 启动：`tuning.overtime_reinforcement` 必须含 `safety_factor > 0`（0.8）、`wave_interval_min ≥ 1`（3）与 `elastic{k_rounds ≥ 1, idle_output_slots ≥ 1, max_bonus ≥ 0}`；**禁止**把 `wave_interval_rounds` 当手填常数（若存在仅作校准留痕）。运行时：`M_base == max(wave_interval_min, ceil(enemy_full_hp ÷ (D × safety_factor)))` 且 `M_base ≥ 3`，其中 🔴 **`D` 取实测值**——**实测 `D` = 21.04**（估算 24/35 已作废，#216）→ 敌 HP **110** ⇒ **`M_base` = 7**（`ceil(110 ÷ (21.04×0.8)) = ceil(6.53)`）；🔴 **v0.75 新增断言：`tuning.m_value` 必须等于按当前 `enemy_full_hp` 与实测 `D` 算出的 `M_base`**（`m_value == M_base`）——**不一致即启动报错**（治"改了 HP 忘了回填 M"的老毛病）；**`M ∈ [M_base, M_base + max_bonus]`**，且**首波固定 `trigger_round`（= 7，不受弹性影响）**；每次 M 变动写 **`ReinforcementElasticEvent(MFrom, MTo, Reason)`**（级 2）。复测：**报告必须输出实测 `D` + 实测 `E`（v0.57 强制）+ 口径健康度（单一位置占比 ≤40%）+ 增援事件数（4~6 回合节奏下预期 ≈0）**，否则该次基线不成立 | GDD §1.5.2 / enemy §4 / feat_pack F3+F4 / state #195/#196/**#198**/**#213**/**#230** / O-54 / **O-60** | 启动报错（缺键/常量化 M/弹性参数越界/**`m_value` 与公式不一致**）+ 复测报告缺 `D` 或 `E` 即判基线无效 |
| P18 | **DD 借鉴包一致性（#202~#209，v0.52）** | ① `buff_defs(mark)` 存在、`polarity=negative`、**无数值**、`stack=refresh`、`dispellable=true`；② `skills.*.bonus_vs_marked_percent ∈ [0,100]`（起手 25），且**受益技能必须声明**（**军医【致命注射】/ 政委【处决令】**；"失血收割"仅为二者通称，不是技能名）；③ `skills.*.requires` 字段名 ∈ {`self_hp_below_percent`,`target_hp_below_percent`,`self_weak`,`self_deaths_door`}、百分比 ∈ (0,100]、且不满足时 `AvailabilityReason == RequirementNotMet`；④ `tuning.bleed` 四规则齐备（`ignores_phys_def` / `crit_rounds` / `resolve_at:"target_turn_start"` / `can_crit:false`）且**至少 2 条技能能施加流血**（突刺/致命注射）；⑤ `tuning.miss_compensation.hidden == true` 且 **`HitRateFor` 不读补偿**；⑥ `morale_events` 含 `physical_crit_hit_self` 与 `physical_crit_hit_ally`（50%/队友），且 `critical_strike_dealt`（team）**每动作只结算一次**（AOE 多暴击不重复）；⑦ `deaths_door_recovery.stack=="none"` **且 `duration=="until_next_recovery"`**（#240：**到下次恢复（扎营/回城）**，取代 `until_run_end`） | dd_reference §1.1~§1.7 + §2 / state #202~#209 / O-58 / O-59 | 启动报错（mark/requires/tuning 缺项或越界）+ 执行层断言（隐藏补偿不入显示、AOE 暴击只 +5 一次、死门后遗症只一层） |
| P19 | **支援点一致性（#211/#212/#213 → 🔴 v0.90/#248 恢复来源改物品）** | ① `tuning.support_points` 存在且 `start ≥ 0`、`cap ≥ start`、`cost_skill ≥ 1`（起手 **2**）、`cost_reinforce ≥ 1`（起手 **2**）；🔴 **引擎 `regen_per_round == 0`**（**恢复不再由引擎提供**），且 **`regen_provider` 指向的物品必须存在**（`items.json`，起手 **`support_crate`**；🔴 **id 以 `#265` 裁定为准**）、**该物品的 buff 提供 `sp_regen_per_round == 1`**；② **有效恢复断言**：**背包含该物品 → 每回合 +1（钳 `cap`）**；**不含 → SP 完全不恢复**（这是合法状态，**不是 bug**）；**起手默认带 1 个**；③ **零消耗断言**：**战斗位（1~4）技能 / 被动（支援位回士气 +3）/ 撤退** 执行前后 SP 不变（回归用例锁死）；④ **不足拒绝且不吞行动**：SP 不足 → `AvailabilityReason.SupportPointsNotEnough` 灰显；`Reinforce` SP 不足 → **拒绝且不消耗发起者行动**；⑤ **每次变动写 `SupportPointEvent`**（含 `reason: regen/skill/reinforce/rejected`），否则 UI 常驻 SP 与"SP 存量/花费分布"无法从事件流算出（P17）；⑥ 待命走 `TurnSkippedEvent(Reason:"passed")`，**不是技能、不耗 SP**；⑦ **调参纪律**：只允许改 `cost_skill`（#213）；**`cap` / `cost_reinforce` / 物品是否提供恢复 非经策划拍板不得变动**；🔴 **不得**把 `regen_per_round` 改回引擎侧（那会退回"凭空恢复"，丢掉 #248 的取舍）；⑧ 🔴 **支援包（`support_pack`）的接线（v1.06 架构裁定）**：**`+2 SP` 由 `BattleDirector` 结算**（**SP 唯一写入口**，见 `blueprint` §9.11）→ **必须钳 `cap`** 并写 **`SupportPointEvent(reason:"item")`**；**远征层只负责扣物品**（`IInventory.TryConsume`）——**禁止远征层直接改 SP**（否则两套台账） | GDD §1.4.2 / ui_spec 必显 #10 / feat_pack_04 S0~S5 + **§S7** / state #211+#212+**#213**+**#248** / O-60 | 启动报错（键缺失/值域越界/`regen_provider` 指向不存在的物品）+ 执行层断言（**带/不带物品的恢复差异**、战斗位零消耗、不足不吞行动、SP 变动有事件） |
| **P23** | **传家宝与建筑升级一致性（M8.1 / `#292`）** | ① `heirlooms.json`：**四种传家宝**（busts / crests / deeds / portraits）齐备、`tier_drop` **与光照档挂钩**（**越暗越多**）、**未注入时不记**（不静默造平行账）；② **三条路线 = 三个轴**：**`tavern` = cost_down / `abbey` = effect_up / `stagecoach` = unlock** —— 🔴 **三轴都必须存在**（防简化回单轴）；③ **升级有代价**：**一趟收入 < 三栋首级总需**（实测 6 < 14）⇒ **不得设计成"迟早都满"**；④ **不足即置灰** + **升级按钮走真实 `Pressed` 信号**（红线 21 (b)） | `doc/modules/hamlet.md` · state **#292** | 启动报错（传家宝缺种 / 轴缺失 / 掉率不随档）+ 执行层断言（不足即置灰） |
| **P25** | **地牢地图与移动一致性（M7.6 / `#294`）** | ① **房间数 ∈ [6,8]**（**主干**；🔴 **[口径 (A)]：主干 6~8 ＋ 支路另加 ⇒ 总房间 ≤10** —— **不卡总数**，因为**节奏感由主干保证**，且 `battle_goal=3` 与房间数**无绑定**）；② **概率 ∈ [0,1]**、**两类房间权重都 > 0**；③ **`max_branches ∈ [1,3]`**（**至少允许一条分叉**）；④ **移动两值**：`move.new_area < 0`（−30，**沿用已调平的 `light.node_step`**）且 `move.revisit < 0`（−10）**且 `|revisit| < |new_area|`**（**"回头更便宜"可测**）；两者走**同一 `LightChangedEvent` 通道**（`reason` 区分 `advance`/`revisit`）；⑤ **`scout.reveal_depth ∈ [1,3]`**；⑥ 🔴 **支路必须"有去有回"的代价**（**重走 −10/段**）—— 否则"多探索"是**纯赚**（多房间=多掉落机会+无代价）⇒ **那支路就不是决策**；**验收：支路的总收益（掉落机会）不应压倒性划算**（至少要有**可感的往返代价**）；⑦ **不相邻移动即拒绝且不耗光**；**所有随机必写 `RngDraw`**、**同 seed 完全复现**；⑧ 🔴 **支路房间类型（v1.27 / `O-79`（c））**：**支路可以含【直接降低撤退风险】的房间类型** —— **免费恢复房（不耗柴火）／ 光照补给房（+光照，不耗柴火）／ 士气房（全队 +士气）**；**新旋钮默认 0 = 现状不变**（便于对照）；⚠️ 🔴 **不得只加"更多资源类"房间** —— **拓扑下补给已过剩**（绕支路 **14.38/趟** vs 旧线性激进 **2.83**、完成率**不升**）⇒ **"再给更多补给"花不出去、无效**；🔴 **要补的是【当前稀缺的东西】（安全/光照），不是"量"** | `m8_roadmap §4.3` · state **#294** | 启动报错（房间数越界 / 权重 0 / 不许分叉 / reveal_depth 越界 / 重走不比新区域便宜）+ 执行层断言（不相邻拒绝且不耗光、支路往返有代价） |
| **P24** | **Sanitarium 与疾病一致性（M8.2 / `#293`）** | ① **疾病 ≥3**，且**每种疾病的惩罚可测**（`hp_delta` / `attack_delta` / `morale_delta` **不得三项全 0** —— **红线 19 的落实**）；② **患病概率 ∈ (0,1]**（起手 0.15 / 0.12 / 0.10）；③ **三种服务齐全**：`cure_disease` / `remove_negative_trait` / `lock_positive_trait`；④ 🔴 **每项服务必须消耗【传家宝】**（**给 M8.1 的传家宝开稳定出口**）+ 金钱；⑤ 🔴 **惩罚必须能【投影到单位】**（只记在 `Roster` 不算 —— **否则是"写了但没接上"，红线 21**）；⑥ **扣费规则：先整体校验再扣** ⇒ **钱不够 / 传家宝不够都拒绝且不部分扣**；⑦ **患病掷骰必写 `RngDraw`**（每人每病各一次） | `doc/modules/hamlet.md` · state **#293** | 启动报错（疾病 <3 / 惩罚全 0 / 概率越界 / 服务不耗传家宝）+ 执行层断言（不部分扣、掷骰入审计） |
| **P22** | **局外养成层数据一致性（M8 骨架 / `#281~#285`）** | ① `roster.cap == 12`、**出征 6 + 替补 6 ⇒ cap 必须 > 6**（否则"轮换休息"不成立）；🔴 **`heroes` 数量 == `cap` 且 `id` 不重复**（v1.17/#286，主程序已实现）；② **`roster.heroes[].level ∈ [1,6]`**，且**升级只给属性小幅度**（🔴 **可测：`level_growth = HP +2/级、攻击 +1/级`**；🔴 **原始文本不得含 `skill` / `upgrade` 字段** —— `#283` 7.6/7.8：**不碰技能表**、`#243` 保持）；③ **特质每人 2~3 条**、**正负都有（可校验）**、🔴 **幅度可测：`伤害 ≤15%` / `士气伤害 ≤20%`**（**"小幅"必须当场给可测定义** —— `#286` 实测：P22 抓到 3 个特质非正负兼有的英雄）；④ 🔴 **`economy` 的金钱来源必须与【光照档 + 战斗数】挂钩**（配置里**必须能看出关联**；**不得只跟"趟数"挂钩** —— 否则"冒险"没有跨趟回报）；⑤ **`hamlet.buildings` 的减压服务必须「同价同效」**（Tavern 与 Abbey 的 `price`/`effect` 相同，**只有副作用形态不同**）—— 否则退化成"选更优"；⑥ **招募免费**（`price == 0`）**且"补的人不比老的强"**（新兵 `level == 1`）；⑦ 🔴 **英雄士气必须存在于【跨趟持有者】**（`roster.heroes[].morale`；**v1.18/#287 = `#245` 的落地**）—— **不得只存在 `ExpeditionSession`（单趟）里**；**新兵入场 `morale == 50`**；**回城【不解算/不重置】士气**（`#245`）；⑧ 🔴 **不留死声明（v1.20/#289 → 🔴 v1.22/#290/#292 定稿）**：**判据 = "有没有人真的会读它"，不是"有没有集中消费点"** —— **`DamageMod` 已消费**（`DamageStep` 的 `raw`）；🔴 **`ProbMod` 【不是】死声明**（数据里在用：恐惧/自私/失控/两种美德/死门后遗症；**按 `effect` 名走专用通道**）⇒ **真正要防的是【新增 `effect` 名但没人实现】** ⇒ ✅ **已落地：`BuffDefsConfig.ConsumedEffectNames` = 已实现的 `effect` 名清单（`damage_mod` 4 / `prob_mod` 6）+ 🔴 加载级校验（用了但不在清单 ⇒ 启动报错）**（红线 21） | `doc/modules/hamlet.md` · state **#281~#285** | 启动报错（skill 字段出现 / 名册 cap ≤6 / 两减压建筑不同价效）+ 执行层断言（金钱来源于光照档与战斗数） |
| **P21** | **地牢层数据一致性（M7.5 / #258）** | ① `light.tiers` **五档覆盖 0~100 无缝隙无重叠**，且**边界取档正确**（`>75`=Radiant、`75`=Dim、`50`=Shadowy、`25`=Dark、`0`=Black）；② `light.node_step < 0`（🔴 **v1.14/#278：`−30`**；原 −15 因"结构上暗不下来"而改）、`brighten.delta > 0`（+30）且 `brighten.firewood ≥ 1`、`start` 落在最高档（100）；③ `light.effects` **5 档 × 7 项齐备**，且 🔴 **不得出现任何 HP/最大 HP 字段**（本层只改命中/伤害倍率/士气/暴击/侦察 —— **加 HP 就等于把 #255 已否决的杠杆搬回来**）；④ 🔴 **`light.loot` 校验（v1.12/#276 改对象）**：**「柴火份数按档单调不减」**（`0/0/1/1/2`）+ 🔴 **下限：`shadowy` 起必须 ≥1 柴火**（否则"**摸黑换续航**"这条链又会断）；**收益物仍 ∈ {柴火, 口粮, 支援包}**（**不得引入金钱/战利品品类**）；⑤ `scouting.base ∈ [0,1]`（0.25）、`light_bonus` **随光照变暗单调不增**（否则"越亮越难侦察"违反直觉）；⑥ `inventory.slot_cap == 12`、**"默认想装"必须超格**（`firewood + food 12 + support_crate 1 > 12` —— 起手柴火 1 时为 `1+12+1=14 > 12`，**这是"取舍"的来源，不许调 cap 到 15 来"修好"它**）、🔴 **`recommended_loadout` 的校验（v1.11/#275 定稿）＝「`sum ≤ slot_cap` **且留 ≥1 格余量**」**（**删掉旧的"正好 12 格"要求** —— **留空位是为了让"路上捡到的补给"放得进去**；起手 `{firewood:1, food:9, support_crate:1}` = **11 格**）、**且 `recommended_loadout.firewood ≤ 起手 firewood`**（否则默认配置不可实现）、🔴 **整备只在【出发前】**（**局内不可改**）；⑦ 🔴 **`Pass`（待命）：机制【保留】、士气惩罚【撤销】（v1.08/#272，O-73 收口）** —— `tuning.expedition.pass_morale_delta = **0**`（或移除该键）；`TurnSkippedEvent(Reason:"passed", MoraleDelta:0)`。**理由**：实测 **180 场 / 1245 回合 ⇒ 待命 0 次**（支援位总有不耗 SP 的事可做）⇒ **不去调 SP 数值迎合一个没人用的机制**（本末倒置）；**机制保留**是作为"**没 SP、CD 中**"的安全阀；DD 的 Pass 惩罚存在是因为 DD 的 Pass 有战术价值（躲 AoE）—— **我们没有**；将来若真出现"SP 会耗尽"的场景，再考虑加回。🔴 **一致性门禁仍适用**：`MoraleEvent.delta == TurnSkippedEvent.MoraleDelta`（现在两边都为 **0**，**也同样必须一致**）；⑧ **侦察**必须写 `RngDraw`；🔴 **掉落不再掷骰**（**v1.07/#270 改为确定性按档给份数**）⇒ **不得为掉落引入抽取**；⑨ **`support_crate` 的 buff `duration == "while_carried"`**（§2.1；**扎营不清**）；⑩ 🔴 **光照每次变化必发 `LightChangedEvent(from,to,tier,reason)`** —— **`tier` 必须在事件里**（`#262`：否则复测无法归因"这一场的难度是哪个档给的"）；`reason ∈ {start, advance, brighten, camp, event_torch, event_dark}`（**"举火把/摸黑"必须分开**）；否则 `m7_5_verification` ㉑ 光照曲线 / ㉒ 各档停留占比统计不到；⑪ 🔴 **侦察结果必须可读**：`ScoutResultEvent(roll, success, revealedNodeType)`（否则 V5"侦察成功率 / 因侦察改变选路次数"统计不到）；⑫ 🔴 **`ScoutResultEvent` 字段齐备性（加载级，v1.02 架构裁定）**：**事件必须注册且字段名恰为 `{roll, success, revealedNodeType}`**（类型与可空性正确）—— 这是**可读性的静态底线**（防"事件名对但字段被改名/缺字段"再次导致 V5/㉕ 空）；⚠️ **"只揭示下一个节点类型 / 不改变节点内容 / `success==false ⇒ revealedNodeType==null`" 属【执行级断言】**（运行时语义，**加载级查不到**）—— 见 `m7_5_verification` **V5**；⑬ 🔴 **包满处理（v1.03/#264）**：**禁止静默丢弃** —— 必须走**「丢弃哪一格」选择**（或**就地消耗口粮腾格**）⇒ 保证"**摸黑搏到的补给能拿回来**"（否则光照计只有风险、没有收益，核心闭环漏掉）；⑭ 🔴 **命名区分（v1.04/#265）**：**`support_crate`（支援箱，载体/`while_carried`）与 `support_pack`（支援包，消耗品）必须是两个独立 id**（**数据与事件都不许合并成一个"支援X"**）；🔴 **`recommended_loadout` 必须包含 `support_crate`**（**缺它即启动报错** —— 否则默认配置下 SP 完全不恢复，玩家看不见的陷阱） | `tasks/m7_5_dungeon_layer.md` D0~D3 / state **#258** | 启动报错（档位缝隙/出现 HP 字段/收益物越界/默认配置不超格）+ 执行层断言（边界取档、超格拒绝、包满丢弃有提示、Pass −5 入事件） |
| **P20** | **远征层数据一致性（M7 / #240）** | ① `tuning.expedition.n_battles ≥ 1`（起手 **6**）、🔴 **`battle_goal ≥ 1` 且 `≤ n_battles`（起手 3；`#273`）**、`ambush_chance ∈ [0,1]`（起手 0.33）、`retreat_penalty{no_death, with_death} ≥ 0` 且 **两档都只作用于 `scope:"survivors"`**；② `tuning.resources{firewood, food} ≥ 0`（🔴 **起手 `1`/`12`；v1.10/#274**），**支付不足必须拒绝且不扣**（执行层断言）；🔴 **推荐配置与起手资源的一致性（v1.11/#275 定稿）**：`recommended_loadout.firewood ≤ 起手 firewood`（否则**默认配置不可实现**；起手 1 ⇒ 推荐里的柴火必须是 **1**），且 **`recommended_loadout` 校验 =「`sum ≤ slot_cap` 且留 ≥1 格余量」**（**不要求装满**）⇒ 起手 **`{firewood:1, food:9, support_crate:1}` = 11 格**（**留 1 格给路上的补给**）；③ `tuning.camp.food_tiers` **四档齐备且单调**（0 < half < full < feast = 0/3/6/12）、`respite_base ≥ 1`（6）、`pep_talk_battles ≥ 1`（4）；④ `camp_skills.json` **恰 12 条 = 4 原型 × 3**、**每条必填 `owner_archetype` ∈ {warrior,tank,medic,commissar}**（= `units.id` 且 `side=player`）、每原型恰 3 条、`cost ≥ 1`；🔴 **执行层断言：扎营时"使用者原型 ≠ `owner_archetype`"的技能不可选**（灰显；**不得出现跨原型使用**——否则"角色专属"退化为"共享"，见 `m7_verification` **V8**）；**不得存在 `owner_archetype == null` 的"共享技能"**（#243：不做共享）；⑤ `expedition_nodes.json`：**每个事件节点恰 2 个选项**且 **UI 无"跳过"路径**，🔴 **且必须声明【必然代价】层**（`cost.morale` = 全队 −5 起手值；**缺失即报错** —— `#272`：只"不允许跳过"不够，**两选项都无代价时二选一依然是免费的**）；**阶段一节点类型 ∈ {battle, event}**（`elite` 出现即报错——属阶段二）；⑥ **`morale_events.retreat_success` 与 `tuning.retreat` 对账一致**（两档 −12/−15、`scope=survivors`）；⑦ **`deaths_door_recovery.duration == "until_next_recovery"`**（P18 ⑦ 同源，M7 起生效）；⑧ 🔴 **跨场效果清算（O-67 已定）**：`next_battle` 类效果**必须在本趟内结算**（**无"下一场"则作废、不结转**；打了下一场 → **该场结束清除**）；`ambush_immunity` 标记**保留至消耗或本趟结束**（**当次未触发夜袭不得清除**）；**每次清除/消耗必须能从事件流读出**（`BuffRemovedEvent(Reason:"recovery"\|"expired")` / `AmbushTriggeredEvent(immunityConsumed)`）；⑨ 🔴 **口粮不足路径（#244）**：**需要更多口粮的档位必须不可选**（灰显），**`Starve` 照常受罚**（主动选择），**禁止"自动退化为 `starve` 且不扣"**（执行层断言：该路径不存在）；⑩ 🔴 **两条度量口径（#242/#244）**：**HP% 分母 = 整编名册 MaxHp 之和**（阵亡者按 0 参与、**分母固定 6 人**；**不得**按"当前存活者 MaxHp"算），**`Respite == respite_base + 存活人数`**；⑪ 🔴 **buff 台账会话级**：跨场 buff（`battles`/`next_battle`/run 标记）必须存活于**会话级 ledger**（`BattleDirector` 仍单场纯、由组合根注入同一 ledger 引用）；**场间推进走 `AdvanceBattleBoundary()`、扎营/回城走 `OnRecovery()`**（后者**只清 `until_next_recovery`**、**不清** `battles`/`next_battle`）；⑫ 🔴 **回城恢复语义（#245）**：`town_recovery == {hp:"full", morale:"none", clear_states:true}` —— **回城后士气必须等于回城前士气**（**不得重置为 `morale.start`(50)**；完成/撤退/全灭**三档都不重置**），撤退惩罚**只在撤退那一刻**结算一次；⑬ 🔴 **背包 / 物品一致性（v0.90 / #248）**：`tuning.expedition.pack_slots == 12`；`items.json` 每件 `slots ≥ 1`、**`∀item: Σ(携带数量 × slots) ≤ pack_slots`**（**装不下必须在远征前被拒且 UI 可见**——"2 柴火 + 12 口粮 = 14 > 12"就是设计好的取舍）；**`sp_support_crate` 起手默认带 1 个**、其 buff 提供 `resource_mod.sp_regen_per_round == 1`；`sp_supply_pack` 为**一次性 +2 SP**（用后消耗、写 `SupportPointEvent(reason:"item")`）；🔴 **UI 断言**：**不带 `sp_support_crate` 时必须显示「支援位不会恢复 SP」提示**（否则玩家无从知道，属陷阱而非决策）；⑭ 🔴 **敌难度递进（v0.92/#250 → v0.93/#252 裁定）——加载级 fail-fast**：`difficulty_tiers` **三档覆盖第 1~6 场、无缝隙无重叠、乘数单调不减**、**`target == "enemy_hp"`（已裁定；可选值域 `enemy_hp`/`enemy_attack`/`both`/`enemy_resist`，后三者未启用）**、**全部走 tuning（禁止硬编码到 units/enemy 数据里）**；🔴 **回合护栏（hard，按【链式口径】判）**：**第 5~6 场在"实况队伍"（含跨场保留：HP/士气/虚弱/死门后遗症）下的平均回合必须 ≤6**（**现 5.36 回合 ⇒ 幅度上限 ≈ +12%**）——**若破带 → 不得硬加 HP，改走备用轴**；**链式 vs 新队两口径必须分栏报（`m7_verification` ⑳a/⑳b）**，**不得用新队数字判链式护栏**（v0.95 / **O-71**）；🔴 **备用轴排序（#252，若破带按序换）**：**1️⃣ 敌抗性↑**（`enemy_resist`：位移/眩晕/减益抗性 —— **唯一同时避开"回合带"与"濒死带"的轴**）→ **2️⃣ 资源更少**（柴火 2→1 / 口粮 12→9：不动战斗本身，只让累积更陡）→ **3️⃣ 延长 N**（6→7：**改跑图结构，须重跑基线**）；🔴 **`M_base` 按 O-70 ② 取"基准（×1.0）满编 HP"**（#252 的"敌总 110"即基准口径）；**UI 必显 ⑧**：**当前档位 + 后续难度预告**；🔴🔴 **v0.98/#255 覆盖（本条为准）**：**① 敌 HP 递进已【全部撤销】**（`mult` 全 **1.0**，实测 +10% HP ⇒ **链式 +64% 回合**，**线性外推失效**）；**② 难度改由 `stun_resist_pp` 承担**（起手 `0/0/15`，**单调不减**；敌 `30/25/40 → 45/40/55`）；**③ 回合护栏改为【链式口径 5~7】**（>**7.2** → 再降一档）；**④ 位移抗性必须保持 0**（`#253`：削核心定位）；**⑤ 禁止对回合数做线性外推**（#255，与 #216 同源）；**⑥ 档位标签必须由 tuning 生成**（禁硬写） | `expedition.md` §1~§3 / `tasks/m7_expedition.md` E0~E6 / state #240/#244 | 启动报错（缺键/值域/事件非二选一/`elite` 越前）+ 执行层断言（选路与夜袭写 `RngDraw`、回城清状态、撤退只罚存活者） |

> 校验失败时日志给出"文件 / 记录 id / 规则 / 期望 vs 实际"，便于追到策划原文行（每条规则带 §/行号出处，如上表"依据"列）。

---

## 5. 完整 JSON 示例（三条）

示例数字与 skill_data.md **逐字一致**。

### 5.1 我方战士「劈砍」（skill_data §1 行 1）

```json
{
  "id": "warrior_cleave",
  "name": "劈砍",
  "owner_unit": "warrior",
  "self_slots": [1, 2],
  "target": { "scope": "slots", "side": "enemy", "slots": [1, 2] },
  "damage": { "segments": [ { "type": "flat", "multiplier": 1.0 } ] },
  "hit_mod": 0,
  "crit_mod": 0,
  "effects": [],
  "displacement": null,
  "use_limit": { "type": "none" },
  "morale_effects": [],
  "tags": [ "output" ],
  "range_axis": "melee",
  "damage_axis": "physical",
  "heal_fixed": null,
  "self_damage_fixed": null
}
```

> 对应表：站位 1、2；目标 敌 1、2；倍率 1.0；命中 +0；暴击 +0；附加 —；位移 —；限制 —；士气 —；轴 近战·物理；标签 输出。

### 5.2 我方坦克「铁壁」（skill_data §2 行 5：护盾次数型 + CD 4）

```json
{
  "id": "tank_iron_wall",
  "name": "铁壁",
  "owner_unit": "tank",
  "self_slots": [1, 2],
  "target": { "scope": "self" },
  "damage": null,
  "hit_mod": 0,
  "crit_mod": 0,
  "effects": [ { "type": "shield", "charges": 2, "apply_to": "self" } ],
  "displacement": null,
  "use_limit": { "type": "cooldown", "value": 4 },
  "morale_effects": [],
  "tags": [ "support" ],
  "range_axis": "none",
  "damage_axis": "none",
  "heal_fixed": null,
  "self_damage_fixed": null
}
```

> 对应表：站位 1、2；目标 自身；附加 = 护盾：抵挡 **2 次**物理攻击（#156，按次数不按点数）；限制 CD 4；轴 —·无；标签 支援。护盾只挡物理、精神穿透、流血/位移/自我伤害不耗次数、AOE 消耗 1 次（combat_math §8.1）。

### 5.3 敌方施法者「恐惧低语」（skill_data §5 行 6：精神伤害 + 韧性减益）

```json
{
  "id": "caster_fear_whisper",
  "name": "恐惧低语",
  "owner_unit": "caster",
  "self_slots": [2, 3, 4],
  "target": { "scope": "slots", "side": "player", "slots": [1, 2, 3, 4] },   // #186：由 [1] 放宽（供 lowest_morale 池内选人）
  "damage": { "segments": [ { "type": "flat", "multiplier": 0.8 } ] },
  "hit_mod": 0,
  "crit_mod": 0,
  "effects": [ { "type": "stat_mod", "stat": "resilience", "delta": -10, "duration_rounds": 2, "resist_axis": "stat_debuff_resist" } ],
  "displacement": null,
  "use_limit": { "type": "cooldown", "value": 1 },
  "morale_effects": [],
  "tags": [ "output", "debuff" ],
  "range_axis": "ranged",
  "damage_axis": "mental",
  "heal_fixed": null,
  "self_damage_fixed": null
}
```

> 对应表：站位 2、3、4；目标 **我 1~4 位（#186 放宽）**；倍率 0.8；附加 = 目标韧性 −10（2 回合）；限制 CD 1（#165 后持续压力源）；轴 远程·精神；标签 输出·减益。
> 士气衔接：`damage_axis=mental` 单目标命中 → 按 morale_events `mental_hit` 对目标 −8（精神暴击 −12）；韧性 −10 使精神减免与美德率同降（#158 双威胁）。**池内选人**由 `enemy_ai.json target_preference=lowest_morale` 决定（#185/#187）；"点名最低韧性"的文字口径见 O-21/O-46。

### 5.4 池外通用「移动」（#191：1 条通用 `move`，不绑单位；距离读施法者 `move_distance`）

```json
{
  "id": "move",
  "name": "移动",
  "pool_external": true,
  "self_slots": [1, 2, 3, 4],
  "target": { "scope": "move_range" },
  "damage": null,
  "hit_mod": 0,
  "crit_mod": 0,
  "effects": [],
  "displacement": null,
  "use_limit": { "type": "none" },
  "morale_effects": [],
  "tags": [ "displacement" ],
  "range_axis": "none",
  "damage_axis": "none",
  "heal_fixed": null,
  "self_damage_fixed": null
}
```

> 对应表：池外常备（不占 9 选 5）；**1 条通用、不绑 `owner_unit`**（#191，故无 `owner_unit` 键）；仅战斗位 1~4（self_slots）；目标 = 自身 ±`units.move_distance` 格内**被占用**战斗位（坦克 1 / 战士 2 / 军医 2 / 政委 2；**技能不再写 `distance`**，空位不可选 #21）；结算 = 与目标位**两点直接互换**（`SwapSlots`，**非**逐级交换链——O-48）；无 CD；**自我移动不过位移抗性**、无伤害 → 不触发死门（#117）。技能总量 44（36 池内 + 1 移动 + 7 敌）。

---

## 6. 命名速查表（中文 → 英文标识）

合并 glossary §9 与本文新增标识（新增一律标注 **【新命名】**）。文件/资源命名遵循 _conventions §3：C# 类型 PascalCase、文件名与资源 snake_case、配置字段以本表为准。

| 中文 | 标识（JSON key / C# 类型建议） | 来源 |
|---|---|---|
| 战斗位 / 支援位 | `combat_slot` / `support_slot`（SlotKind: Combat/Support） | glossary §9（CombatSlot/SupportSlot） |
| 向中靠齐 | `close_up` | glossary §9（CloseUp） |
| 交换链 | `swap_chain` | glossary §9（SwapChain） |
| 士气 | `morale` | glossary §9（Morale） |
| 韧性 | `resilience` | glossary §9（Resilience） |
| 美德 / 折磨 | `virtue` / `affliction` | glossary §9（Virtue/Affliction） |
| 崩溃余烬 | `collapse_ember` | glossary §9（CollapseEmber） |
| 虚弱 | `weak` | glossary §9（Weak） |
| 死门（抗性） | `deaths_door_resist` | glossary §9（DeathsDoor） |
| 位移抗性 | `displace_resist` | glossary §9（DisplaceResist） |
| 护盾 / 护卫 | `shield` / `guard` | glossary §9（Shield/Guard） |
| HP / 攻击 / 物防 / 速度 | `hp` / `attack` / `phys_def` / `speed` | glossary §7.1 字段（英文部分为本文整理）【新命名】 |
| 闪避 / 暴击 | `dodge` / `crit`（百分数） | 同上【新命名】 |
| 眩晕抗性 / 流血抗性 / 属性减益抗性 | `stun_resist` / `bleed_resist` / `stat_debuff_resist` | 同上【新命名】 |
| 眩晕 / 流血 / 嘲讽 | `stun` / `bleed` / `taunt` | combat_math §4 / skill_data【新命名】 |
| 精神减免 / 美德率（派生，不入库） | `mental_reduction` / `virtue_rate` | #158 / #56【新命名】 |
| 伤害轴（物理/精神/无） | `damage_axis`: physical/mental/none | skill.md 字段13【新命名】 |
| 距离轴（近战/远程/无） | `range_axis`: melee/ranged/none | skill.md 字段12【新命名】 |
| 功能标签 | `tags`: output/control/displacement/support/heal/aoe/debuff | skill.md 字段11【新命名】 |
| 自身站位要求 / 目标位置 | `self_slots` / `target` | skill.md 字段 2/3【新命名】 |
| 倍率（多段 / 失血公式） | `damage.segments`（flat/missing_hp） | skill_data §0【新命名】 |
| 命中修正 / 暴击修正 | `hit_mod` / `crit_mod` | skill.md 字段 5/6【新命名】 |
| 附加效果 / 概率 | `effects`（EffectType + `probability`） | skill.md 字段7【新命名】 |
| 使用限制 | `use_limit`（none/cooldown/per_battle/every_n_rounds） | skill.md 字段9【新命名】 |
| 士气影响 | `morale_effects`（MoraleScope + delta） | skill.md 字段10【新命名】 |
| 位移（推/拉/自移） | `displacement`（push/pull/self_forward/self_backward） | skill.md 字段8【新命名】 |
| 治疗固定值 / 自我伤害固定值 | `heal_fixed` / `self_damage_fixed` | combat_math §8【新命名】 |
| 护盾次数 | `charges` | #156【新命名】 |
| 敌方原型（近战小兵/远程射手/施法者） | `melee_soldier` / `ranged_archer` / `caster` | enemy.md【新命名】 |
| 我方原型（战士/坦克/军医/政委） | `warrior` / `tank` / `medic` / `commissar` | character.md【新命名】 |
| 每场 N 次 | `per_battle`（UseLimitType） | skill.md §4 / glossary §2【新命名】 |
| 池外常备（不进 9 选 5） | `pool_external`（bool） | skill.md §1 / #180【新命名】 |
| 移动范围（自身左右 N 格内被占用） | `target.scope = "move_range"` + `distance`（坦克 1 / 战医政 2） | skill.md §1.1 / skill_data §4.5【新命名】 |
| 槽位三态 | `occupied` / `empty` / `blocked`（SlotState） | GDD §1.1【新命名】 |
| 空气（间隙） | `air_gap` | glossary §1【新命名】 |
| 换人 / 增援 | `reinforce`（战斗位发起，GDD §1.4.1） | #41【新命名】 |
| 覆盖校验 / 灰显 | `coverage_check` / `grayed_out_reason`（运行时） | skill_data §6 / ui_spec §4【新命名】 |
| 附加效果实际概率 | `actual_prob = labeled_prob × (1 − resist)` | combat_math §4【新命名】 |

---

## 7. 开放问题（已并入 open_issues.md B 节，编号与其一致）

> 按 _conventions §5：O-01~O-10 已被策划 README §5 的 10 条占用；以下为 schema 侧新增疑问，**编号即 open_issues.md B 节的权威编号（O-11~O-66）**，标注【策划拍板】/【架构裁定】/【实现期待定】与阻塞范围。**本文不改 open_issues.md**，以下仅供架构师归档与任务卡引用。

| 编号 | 议题 | 描述 | 阻塞什么 | 建议 |
|---|---|---|---|---|
| O-21 | 【策划拍板】威吓箭的士气 −4 与"受到精神伤害 −8"通用规则的关系 | skill_data §5 威吓箭效果列 = "目标士气 −4"（#165），而 combat_math §5.1 规定精神伤害 −8（AOE −5）；若叠加则单箭 −12，与"次一级压力 −4"（combat_math §5.2 备注）矛盾；另施法者 AI"点名最低韧性单位"与技能固定目标"我 1 位"（skill_data §5）冲突 | 威吓箭士气结算口径、施法者目标行为、Monte Carlo"士气触底 ≥1/场"校准 | 倾向：显式士气值取代派生 −8（作次一级压力）；技能只选位置总则下"我 1 位"为准 |
| O-22 | 【策划拍板】护卫是否"只挡物理" | buff.md §7.1 新增第 5 条 vs 该节"未单独立为决策，可推翻"自述；README 开放题 5 仍标未定，state #159 又含该条 | 盾墙守护结算、buff_defs `guard` 记录 | 倾向按 #159/护盾 #156 同向：只挡物理 |
| O-23 | 【策划拍板】属性减益"固定 −3"与技能韧性 −15/−10 的关系 | combat_math §4 属性减益固定值 −3/2 回合，而投掷药瓶韧性 −15、督战/恐惧低语韧性 −10（各 2 回合）——"固定值"仅适用于攻/防/速，还是韧性也该 −3？ | tuning `stat_debuff_default` 与技能效果数值的并存口径 | 倾向：−3 为攻/防/速通用默认；韧性减益走技能标注值（−15/−10） |
| O-24 | 【策划拍板】无概率标注的属性减益/嘲讽是否吃对应抗性 | 眩晕标注概率（30/35/40），韧性减益与嘲讽未标概率；combat_math §4 把"属性减益"列为过抗性效果 | 属性减益与嘲讽的实际触发率（是否 = 100% ×(1−抗性)） | 待拍板；当前按必中（概率缺失=直接生效）倾向记录，吃抗性会显著削弱施法者 |
| O-25 | 【策划拍板】护盾（次数型）重复施加的叠层语义 | buff.md §5 只分"有时限→刷新 / 无限时→叠层上限 2"；次数型护盾不属于两者，再次施放是刷新次数、叠次数（上限 2）还是替换？ | 铁壁（CD 4）重复施放的盾数 | 建议按"叠层上限 2（每次自带次数）"或"刷新"，待拍板 |
| O-26 | 【策划拍板】盾墙"守护/护卫"的持续时长 | skill_data §2 盾墙仅给物防 +6 标注（2 回合），守护链接未标时长；buff.md §2 的"3 回合"仅为示例数据 | buff_defs `guard_attach` 的 duration | 建议 2 回合（与物防同频），待拍板 |
| O-17 | 【实现期待定】捆缚的持续语义 | morale §5.1"无法行动"，未定义持续多久（本次行动 / 本回合 / 到回合结束） | bound 记录 duration | 切片实际不触发（支援位无攻击动作），低优先 |
| O-11 | 【策划拍板】撤退成功率的 f 函数 | ✅ **已定（#169，2026-09-09）**：`基础 = 50% + (我方平均实际速度 − 敌方平均实际速度) × 4%` 钳 [15,85]；`实际 = 基础 + uniform(−10,+10)` 钳 [5,95]；tuning `retreat_formula` 默认示例即上值 | 已关闭 | 撤退按钮数字 / M6 撤退 KPI 按 #169 公式 |
| O-27 | 【策划拍板】振奋（美德）"每回合回士气"数值缺失 | morale §6 只有效果名"每回合给全队回士气"，无数值 | 美德池第二项、buff_defs 振奋记录数值字段 | 补齐前切片美德池仅 [勇猛] |
| O-28 | 【策划拍板】远程射手 AI 规则 ②③ 的判定语义 | enemy §5.4：②否则精准射击、③士气整体偏高且威吓箭 CD 就绪→威吓箭；按"首个条件满足即执行"，无条件且无 CD 的精准射击会让③永不可达；"被近战贴脸（敌方在 1/2）"判定对象口径也有歧义 | enemy_ai 表 ranged 规则序、威吓箭实际使用率（#165 意图落空） | 需澄清 ② 隐含条件或调整规则序；倾向 if 贴脸→后撤 / elif 士气高且威吓箭就绪→威吓箭 / else 精准射击 |
| O-20 | 【实现期待定】超时增援"无空位上增益（+攻/+速）"的具体数值 | GDD §1.5.2 / #71 只写"给在场敌人上增益"，+攻/+速值未给；增援强度与波次频率亦待数值阶段 | tuning `overtime_reinforcement` 增益数值 | 按 verification 调节；切片该分支仅在满编拖到 6 回合后出现 |
| O-13 | 【架构裁定】多段倍率（0.55×2 / 0.5×2）各段是否分别过命中/暴击/护盾 | ✅ **已定形（#179 + 架构裁定）**：双段 = **同一目标两段、目标只选一次**；各段独立结算实例（各自过命中/暴击/护盾）——与 M3 执行骨架、M2 §7.3 一致 | 已关闭 | 双连击/连射逐段结算 |
| O-31 | 【架构裁定】威吓箭伤害表达式 vs 远程射手攻击 13 | ✅ **已修正**：enemy.md §5.3 已改为 `13 × 0.5`（#170 同批更新）；skills.json 倍率 0.5 不变，攻击 13 × 0.5 自洽 | 已关闭 | 无 |
| O-40 | 通用「移动」/ 增援单按钮两步 / 技能栏去使用要求（**#180/#181/#182**） | ✅ **策划拍板·已定（v0.45）**：4 条 `*_move`（`pool_external` + `move_range` + distance 坦克1/战医政2）；增援命令 `Reinforce(A,B,X)` 单按钮两步；技能栏移除内联要求文字、候选高亮必需。落地：m3（47 断言/P12/移动卡）、m5（③b 高亮必需 + 增援两步 + 去文字）、m6（SemiRandom 含移动/增援两步 + T-M6-08 复测含移动） | 策划拍板·已定 | M3/M5/M6（改造进行中） |
| O-41 | 通用「移动」执行细节：① 移动目标是否含障碍位（建议**允许**——与障碍交换 = 推动障碍，#22/#114，非"清除"）；② 移动是否计入"位移实际使用"KPI（建议**计入**——移动=自我交换链位移，verification §3 位移 ≥2 复测）；③ 移动可用性展示（支援位 5/6 不可用） | **架构裁定·已定（可推翻）**：① 允许选障碍位（走交换链，#22）；② 计入位移 KPI 统计（重新基线 T-M6-08 一并复测）；③ 战斗位常驻按钮、支援位灰显/不显示 | 架构裁定·已定 | M3/M5/M6 |

> 非阻塞说明（不挂开放题，仅记录）：README §2 M3 行"42 条技能"为更早笔误，**v0.48 起总量 = 44**（36 池内 + 1 通用 `move` + 7 敌方，#191）；skill_data §6 覆盖表计数与逐行自数存在口径差异，P3 只数池内、按 ≥2 硬规则执行（池外 `move` 另计兜底）。事件族见 **§8**。

---

## 8. 事件族清单（运行时契约，**不是** JSON 配置；v0.50 镜像 #199）

> **来源**：[设计] `doc/modules/logging.md`（全文）· 落地卡 `doc/architecture/tasks/feat_pack_02_observability.md` G0 · 决策 **#199**。
> **定位**：`CombatLog` 是 append-only 不可变事件流、`Sequence` 单调递增，**一处写入、处处只读**（blueprint §6.2/§8.3）；
> 三消费者共用同一份——**UI（DevLog）/ 回放 / 统计（Monte Carlo）**。
> 🔴 **纪律（原文 logging.md §1）**：任何"UI 要显示的数字"**必须先从事件流能算出来**；算不出来 = **事件字典缺项**，
> **不得为某个消费者单独造数据**。

### 8.1 现状与缺口（核实 2026-09-09）

`core/events/BattleEventTypes.cs` 现有 **18 类**；硬缺口：

| 缺口 | 后果 |
|---|---|
| **无 `SkillUseEvent`** | **没有任何事件记录"谁用了哪个技能"**——`HitEvent` 仅 `(Hit, HitRate, Attacker, Target)`、**无 `SkillId`** → **M6「技能使用率」KPI 目前是空的** |
| **buff 生命周期零事件** | `BuffLedger.Add/Remove/TickRounds` 无事件；`EffectEvent` 只记掷骰 → "谁给谁上了什么、几层、多久"全无 |
| 无 `TurnStartEvent` / 跳过行动 / 靠齐 / `BattleEndEvent` / 敌方 AI 决策 / 折磨 proc / 属性减益 / 增援弹性 | 对应流程在日志中**无声消失** |
| 基类只有 `Sequence`、**无 `Round`** | 无法按回合分组，只能靠 `RoundStartEvent` 反推 |
| `SwapEvent(Actor, FromPos, ToPos)` | **未记被调动者 B**（#181 实际移动的是 B）→ 增援日志半盲 |
| `DamageEvent` 无 `SkillId`、`EffectEvent` 无 `Source`、`DeathEvent` 无 `Cause` | 多段伤害/状态/死因无法归属 |

### 8.2 新增事件（**M6：15 类** → 🔴 **M7 追加 9 类，共 24 类**；字段为**最小必需集**：实现可加、不可减）

> 🔴 **M7 追加之 9 类见 §3.11**（`PathChosen` / `ResourceChanged` / `CampStarted` / `CampFoodChosen` / `CampSkillUsed` / `CampEnded` / `AmbushTriggered` / `EventNodeResolved` / `TownReturn`）；
> **既有 18 类 + M6 的 15 类 + M7 的 9 类 + M7.5 的 2 类（`LightChanged` / `ScoutResult`）= 事件族总数 44 类**。**P17 的可重建性/不引入抽取要求对 M7/M7.5 的新事件同样适用**（尤其 `PathChosen`/`AmbushTriggered`/`ScoutResult` 必须写 `RngDraw`）。

| 事件 | 字段 | 级 | 备注 |
|---|---|---|---|
| `BattleEndEvent` | `Outcome(Victory/Defeat/Retreat)` · `Round` · `Reason` | 1 | 取代 `BattleRoot.EndGame` 的 `GD.Print` |
| `TurnStartEvent` | `Actor` · `Slot` · `EffectiveSpeed` | 1 | 回合内"轮到谁" |
| `TurnSkippedEvent` | `Actor` · `Reason(stunned/bound/no_usable_skill/**passed**)` | 1 | 眩晕/捆缚/全不可用空过；**`passed` = 显式「待命」**（#212/S5.2，非技能、不耗 SP） |
| **`SupportPointEvent`** | `Delta` · `NewValue` · `Reason(skill/reinforce/regen/rejected)` | 2 | 🔴 **v0.55 新增（第 15 类）**：SP 每次变动（#211/O-60）——**UI 常驻 SP 与 Monte Carlo「SP 存量/花费分布」都只能从此事件统计**（logging 纪律） |
| **`SkillUseEvent`** | `Actor` · `CasterSlot` · `SkillId` · `TargetSlots[]` | 1 | 🔴 **技能使用率 KPI 的唯一来源** |
| `SkillRefusedEvent` | `Actor` · `SkillId` · `Reason(affliction_fear/no_target/cooldown/per_battle)` | 1 | 折磨·恐惧拒绝 / 不可用 |
| **`BuffAppliedEvent`** | `Source` · `Target` · `BuffId` · `DurationRounds` · `Stacks` | 1 | buff 台账 `Add/AddCharged` |
| **`BuffRemovedEvent`** | `Target` · `BuffId` · `Reason(expired/dispelled/consumed/morale_reset/death/`**`recovery`**`)` | 1 | buff 台账 `Remove/Tick`；🔴 **`recovery` = 因扎营/回城清除**（O-67：`until_next_recovery` 类走它，与**到期 `expired`** 必须可分） |
| `AfflictionProcEvent` | `Unit` · `AfflictionId` · `ProcKind(refuse_skill/refuse_heal/randomize_target)` · `Roll` · `Triggered` · `NewTargetSlot` | 1 | #193 折磨 proc |
| `CloseUpEvent` | `Moves[]`(`Unit` · `From` · `To`) | 2 | 靠齐逐槽 From→To |
| `MoraleEmberEvent` | `Unit` · `Kind(enter/exit)` | 2 | 崩溃余烬 |
| `EnemyDecisionEvent` | `Actor` · `SkillId` · `RuleIndex` · `RuleCondition` · `TargetSlots[]` | 2 | 敌方 AI 决策透明化 |
| `ReinforcementElasticEvent` | `MFrom` · `MTo` · `Reason(not_all_out/recovered)` | 2 | #198 自适应 M（见 §3.7/P16） |
| `StatModEvent` | `Target` · `Stat` · `Delta` · `DurationRounds` | 3 | 属性减益（**当前绕过 buff 台账**） |
| `ObstacleEvent` | `Slot` · `Kind(placed/destroyed/pushed)` | 3 | 障碍 |

### 8.3 补字段（既有事件，**7 处**；一律**尾部追加 + 默认值**，不得破坏既有测试）

| 事件 | 补 |
|---|---|
| `BattleEvent`（基类） | **`Round`**（导演 append 时盖章；`Sequence` 语义不变） |
| `SwapEvent` | `MovedUnit`（被调动者 B）+ `Kind(swap/reinforce)` |
| `DamageEvent` | `SkillId` |
| `EffectEvent` | `Source` |
| `DeathEvent` | `Cause`（`killed_by:<unit>` / `deaths_door` / `self_damage` / `bleed`） |
| `HealEvent` | `Source` · `SkillId` |
| `DisplaceEvent` | `SkillId` · `Source` |

**v0.52 新增机制 → 事件映射（#202~#209，不改事件族形状）**：

| 机制 | 走哪个事件 |
|---|---|
| `mark` 施加/移除、`deaths_door_recovery` 授予 | 现有 **`BuffAppliedEvent` / `BuffRemovedEvent`**（`BuffId` 区分） |
| 眩晕抗性递增（unit_state）变化 | 复用 `BuffAppliedEvent`/`BuffRemovedEvent`（`class:unit_state`）或状态变更事件——**由实现选一，但必须可被事件流读出**（P17 可重建性） |
| 流血每回合跳伤（目标回合开始） | **`DamageEvent`**（带 `SkillId` + `Cause`/来源标注 bleed） |
| 暴击治疗（×2 + 目标 +4） | **`HealEvent`**（已补 `Source`·`SkillId`）+ 士气走 `MoraleChanged`；是否暴击由 `EffectEvent`/专用字段体现 |
| 被暴击情绪冲击（自身 −10 / 队友 50% −5） | **`MoraleChanged`**（来源标注 `physical_crit_hit_self/ally`）；随机写 `RngDraw` |
| 连续 miss 补偿 | **只在命中判定内部生效** → 仅新增 `RngDraw`；**不得**产生可显示字段（P18） |

### 8.4 分级（DevLog 默认只显示 1~2）

| 级 | 名称 | 内容 | 默认 |
|---|---|---|---|
| 1 | 叙事 | 回合开始 / 轮到谁 / 技能使用 / 命中伤害 / buff 施加·移除 / 士气 / 死门·崩溃·死亡 / 增援 / 胜负 | ✅ |
| 2 | 关键判定 | 暴击 / 位移（含失败原因）/ 抗性命中 / 敌方 AI 决策 / 靠齐 / 余烬 / **增援弹性** | ✅ |
| 3 | 机制细节 | 逐段伤害 / 属性减益逐项 / 障碍 | ⬜ 折叠 |
| 4 | 调试 | `RngDraw`（掷骰 + DrawCount）/ 序列号 / 状态快照 | ⬜ 隐藏（按键开） |

> 🔴 **级 4 与确定性**：`RngDraw` **必须记录**（回放/审计），只是**默认不显示**——**"不显示" ≠ "不记录"**。

### 8.5 消费者与只读红线

| 消费者 | 用途 | 红线 |
|---|---|---|
| UI DevLog | 开发/调试/复盘 | **只读**：开关/滚动/过滤/导出**不得**改变事件流内容与顺序（`Sequence` 连续）、不得产生任何抽取；**不得每帧重建全量文本**（增量 append + 文本缓存） |
| 回放 | 同 seed 逐事件比对 | 必须含**全部掷骰**与 `Round` |
| 统计（Monte Carlo） | KPI / 平衡数据 | 可程序化查询、字段完整（**技能使用率依赖 `SkillUseEvent`**） |

> 🔴 **统计口径（v0.60 实测锁定，必须遵守）**：**`SkillUseEvent` 覆盖双方**——敌方使用技能时 `CasterSlot = 0`。
> 因此 **「技能使用率」与「支援位技能次数」必须按 `CasterSlot > 0` 过滤**（只统计我方）；
> **直接把"事件流里 `SkillUseEvent` 的总数"当技能使用率会高估约 43%**（实测：事件流 **73** 条 vs 我方台账 **51** 条）。
> 同时**另断言"事件流确实含敌方使用"**（`CasterSlot == 0` 存在）——完整流程可读性不能靠丢掉敌方来换取。
> 门禁落点：`darkest/tests/G0aEventSourcingGateTests.cs`（3 条，锁死"两栏（技能使用率 / 待命次数）不许变空"）。

### 8.6 校验 **P17 · 事件字典完整性（#199，v0.50；测试阶段断言，不阻塞启动）**

| 断言 | 判定式 |
|---|---|
| 技能可读 | 对局中 `count(SkillUseEvent) == 实际技能结算次数`（含支援/无伤害技能）；且每次 `DamageEvent/HealEvent/DisplaceEvent` 均可回溯到所属 `SkillUseEvent` |
| buff 可读 | `BuffLedger` 每次 `Add/AddCharged/Remove/TickRounds` 均有对应 `BuffApplied/BuffRemoved`；仅凭事件流可**重建任一时刻 buff 集合与层数/次数** |
| Round 分组 | 每个事件 `Round` 已盖章且**单调不减**；按 `Round` 分组与 `RoundStartEvent` 完全一致 |
| 确定性 | 新增事件**不得引入抽取**：同 seed 同命令流 `DrawCount` 序列与事件流均**逐条一致** |
| 可重建性 | 仅凭事件流可重建终态（位置 / HP / 士气 / buff / 虚弱 / 死门） |
| **远征层可读（M7 追加）** | **完成率（6 场皆胜）/ 资源收支 / 扎营与食物档位 / Respite 花费 / 夜袭次数 / 撤退惩罚 / 回城后士气** 全部**可由事件流统计**（§3.11）——**不得为远征层单独造旁路计数器**；`PathChosen`/`AmbushTriggered`/事件结果**必须写 `RngDraw`**（同 seed 逐条一致） |

> 失败处理：CI 测试红（不进启动门禁）；修复前**不得**用"技能使用率/完整日志"类 KPI 作验收依据。

### 8.7 边界

| 模块 | 关系 |
|---|---|
| `ui_spec.md` §10 / §8 详情框 | 详情框读**投影**（当前状态）；DevLog 读**事件流**（历史）——数据源不同、互不替代 |
| `combat_math.md` §9 | 每次掷骰先 `RngDraw` 再业务事件（既有约定），级 4 可见 |
| `verification.md` | KPI 统计直接查事件流；**§8 是"KPI 能否算出来"的前提** |
| `Project_Memory.md` §0-5 | 事件流 = 战斗日志 = 回放源 = 统计源（一处写入、处处只读） |
