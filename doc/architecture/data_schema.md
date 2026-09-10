# 数据层 Schema（data_schema.md）

> **#178/#179 目标语义派生表（2026-09-09 策划拍板）**：`target` 字段（scope+slots）= **可选目标池**，≠ 生效范围：

| 技能画像 | 生效目标 | 代表 |
|---|---|---|
| 有 `damage` 且 **无** `aoe` 标签 | 候选池内 **选一**（实机=玩家点选；headless=固定调用点随机） | 劈砍/突刺/精准射击/威吓箭/低语/震荡/盾击 |
| 有 `damage` 且 **带** `aoe` 标签 | 全范围（范围内全部占用非空位） | 横扫/精神震荡（P11 仅此二） |
| `heal_fixed`（any_ally） | 候选池内 **选一**（单体治疗） | 急救/战场鼓舞 |
| `scope=team` | 全体（逐成员施加） | 战吼/群体绷带/动员令 |
| `scope=self / adjacent_ally_and_self` | 自指/邻近 | 喘息/盾墙 |

- **P11 校验（新增，已入 SkillsConfig.Validate）**：带 `aoe` 标签的伤害技能仅允许 `warrior_sweep`、`caster_mental_shock`；其余伤害技能必须走"选一"路径。
- **备注 4（执行期生效值）**：units.json（敌方 60/60/46/41）与 skills.json（#173 主杠杆净值：cleave 0.9/lunge 0.8/收割 0.4/0.5/横扫 0.6/双段 0.5×2/charge 1.0）为**执行期生效值**；策划文档表（combat_math §7.1 速查、§7.3 实测行 v0.41）为旧口径引用，改数值只走这两份 JSON。
- 执行器落点：`SkillExecutor.Execute(skill, caster, …, chosenTargets)`（选一）；敌方选一按 **O-39 裁定**（候选槽序首个非空 + taunt 优先，无 RNG）。

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
| `skills.json` | skill_data.md（36 我方 + 7 敌方 = **43 条**）按 skill.md 13 字段模板 | 技能定义：灰显判定、命中/暴击、伤害、附加效果、位移、使用限制、士气、三轴标签 | 同上；**新增/改名 id、改枚举、改结构**须架构师评审并同步本文与 C# 模型 |
| `morale_events.json` | combat_math §5.2 士气增减表 / morale §2 | 系统级士气增减事件（delta / 对象 / 触发频次 / 出处） | 数值迭代按 §5.2 表；事件增减需同步 morale_events.json 与 morale.md §2 |
| `buff_defs.json` | buff.md §7/§7.1 / morale §5~§8 / combat_math §4 | 可挂状态（buff 与部分控制类）定义：类型/数值/持续/叠层/驱散/钩子 | 加新 buff = 加一条数据（buff.md §1 设计目标），不改代码 |
| `enemy_ai.json` | enemy.md §5.4 AI 优先级表（固定 + 随机） | 每敌人原型的技能优先级规则（切片关闭随机 #112） | 改 AI 规则只改 JSON；切片只 1 套编成（#89） |
| `formation.json` | GDD §1.1/§1.3/§1.6 / formation.md / character.md §1 / enemy.md §5.1 | 槽位常量（我方 6 = 4 战斗 + 2 支援、敌方 4）、初始编成、编号/边界/障碍规则标记 | 编成属内容配置，切片固定 1 套；改编成只改此文件 |
| `tuning.json` | combat_math / morale / GDD 各节（每字段单独标出处） | 系统级过程常量（钳制、公式系数、回合/次数参数、减益默认值） | 数值迭代按 verification.md；结构变更须架构师评审 |

> 备注 1：README §2（M3 行）写"42 条技能数据"，与 skill_data.md 的"36 + 7"= 43 条不符；本文与 skill_data 以 **43 条**为准，README 数值疑为笔误，不阻塞。
> 备注 2：所有配置 JSON 存放于 `res://data/`（工程根 `darkest/data/`，_conventions §3）；C# 模型/加载/校验代码归属 `res://scripts/data/`；导入后的只读资源按蓝图约定放 `res://resources/`。
> 备注 3：切片 **没有**装备/营地/招募/存档等外部数据（GDD §15），故数据文件仅上表 7 个，不再新增。
> 备注 4：**执行期生效值 ≠ 策划表起手值**（#171/#173 已落地）：敌 HP **60/60/46/41**、横扫 0.6、cleave 0.9 / lunge 0.8 / doubleHit 0.5、冲锋令 1.0、失血收割系数 **0.4/0.5**（combat_math §7.2/§7.3）。`skill_data.md`/`enemy.md` 表保持起手值口径（README §6-5「数值不当即改」），**运行以 `units.json`/`skills.json` 实值为准**；改动仍走 override/数据迭代、不改代码。

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
| 技能目标范围 TargetScope | `slots` / `self` / `any_ally` / `team` / `adjacent_ally_and_self` | `Slots` / `Self` / `AnyAlly` / `Team` / `AdjacentAllyAndSelf` | 位置列表 / 自身 / 任意友方 / 本方全队 / 相邻友方 + 自身 | skill_data 目标列（"敌 1、2 / 自身 / 任意友方 / 我方全队 / 相邻友方 + 自身"） |
| 士气影响对象 MoraleScope | `self` / `targets` / `team` / `ally_targets` | `Self` / `Targets` / `Team` / `AllyTargets` | 本人 / 本次技能目标 / 全队 / 目标中的友方（盾墙"被守护者 +3"用） | skill_data 士气列（"自身 +3 / 全队 +5 / 目标 +15 / 被守护者 +3"） |
| 士气事件对象 EventScope | `self` / `team` / `per_target` | `Self` / `Team` / `PerTarget` | 本人 / 全队 / 每个被命中目标（AOE 精神 −5/目标） | combat_math §5.2 |
| 状态类别 StatusClass | `buff` / `unit_state` | `Buff` / `UnitState` | 数据驱动可挂 buff / 机制性单位状态（虚弱、崩溃余烬） | 见 §3.4 分类说明 |
| 状态极性 Polarity | `positive` / `negative` | `Positive` / `Negative` | 正面（不可驱散）/ 负面（可驱散） | buff.md §5.1 |
| 持续时间类型 DurationType | `rounds` / `action_skip` / `charges` / `until_morale_50` / `until_battle_end_or_morale_zero` / `next_attack_within_rounds` | `Rounds` / `ActionSkip` / `Charges` / `UntilMorale50` / `UntilBattleEndOrMoraleZero` / `NextAttackWithinRounds` | 回合数 / 跳过 1 次行动即结束（眩晕）/ 次数（护盾）/ 到士气回初始值（折磨）/ 战斗结束或士气再归 0（美德）/ 下次攻击且 ≤N 回合（突进增伤） | buff.md §6 / GDD §2.5 / combat_math §4 / morale §8 |
| 叠层规则 StackRule | `refresh` / `stack` / `none` | `Refresh` / `Stack` / `None` | 有时限 buff 再次获得=刷新时长 / 无限时 buff=叠层有上限 / 不叠层（美德上限 1） | buff.md §5 / #159 |
| 附加效果类型 EffectType | `stun` / `taunt` / `bleed` / `stat_mod` / `shield` / `guard_attach` / `next_attack_boost` | `Stun` / `Taunt` / `Bleed` / `StatMod` / `Shield` / `GuardAttach` / `NextAttackBoost` | 眩晕 / 嘲讽 / 流血 / 属性修正（增减益）/ 护盾（次数型）/ 护卫守护链接 / 下次攻击加成（敌人突进） | skill_data 附加列 + combat_math §4 + buff.md §7（整理所得，见 §3.2 效果表） |

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
| `hp` | int | >0 | ✅ | glossary §7.1 | 战士40/坦克55/军医32/政委33；近战小兵50/远程射手38/施法者34（#154 上调后） |
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
| `deaths_door_resist` | int?（百分数） | 0~100，**仅我方** | 我方 ✅ / 敌方 null | 同上 | 死门抗性（glossary §9 `DeathsDoor`）：70/75/60/60；**敌方为 null**（敌方无死门，enemy §1） |
| `skills` | string[] | 技能 id 列表 | ✅ | skill_data | 我方每原型 9 个 id；敌方：melee_soldier 2 个 / ranged_archer 3 个 / caster 2 个 |

> **禁止入库存派生量**：`精神减免 = 韧性/250（上限 40%）`、`美德率 = 10% + 韧性/2` 均为派生量，**不在 JSON 中**（glossary §7.1 注）。
> 敌方死门抗性必须为 `null`（不允许省略后按 0 处理，便于校验区分"敌方无此系统"与"数值漏填"）。
> 原型 vs 实例：6 人编成中重复原型（战士×2、军医×2、近战小兵×2）共享同一原型定义，初始位置由 formation.json 编成引用（§3.6），不在此文件复制。

### 3.2 `skills.json` —— 43 条技能（36 我方 + 7 敌方）

**用途**：skill.md 13 字段模板的落盘实现（skill_data 为"填实版"）。技能按"施法者原型"归属；同名技能数值不同时是**独立记录**（如战士喘息 回 HP 8、坦克喘息 回 HP 10；战士盾击 0.6 / 坦克盾击 0.7）。

**记录索引（id / 中文名 / owner / skill_data 出处）**——数值列一律以 skill_data.md 对应行**逐字抄录**进 JSON，本文不重复展开 43 条数值（避免转录漂移），仅在 §5 给出三条完整示例。

| 原型 | 记录（id — 中文名） | skill_data 出处 |
|---|---|---|
| 战士 warrior | `warrior_cleave`劈砍 / `warrior_sweep`横扫 / `warrior_lunge`突刺 / `warrior_javelin`投掷短矛 / `warrior_shield_bash`盾击 / `warrior_war_cry`战吼 / `warrior_battle_fury`战意 / `warrior_catch_breath`喘息 / `warrior_last_stand`殊死一搏 | §1 行 1~9 |
| 坦克 tank | `tank_guard_wall`盾墙 / `tank_taunt`嘲讽 / `tank_shield_bash`盾击 / `tank_heavy_ram`重盾猛撞 / `tank_iron_wall`铁壁 / `tank_war_cry`战吼 / `tank_hunker`坚守 / `tank_catch_breath`喘息 / `tank_selfless_charge`舍身 | §2 行 1~9 |
| 军医 medic | `medic_scalpel`手术刀 / `medic_cross_slash`十字斩 / `medic_anesthetic`麻醉针 / `medic_medicine_flask`投掷药瓶 / `medic_double_hit`双连击 / `medic_field_strike`战地搏击 / `medic_lethal_injection`致命注射 / `medic_first_aid`急救 / `medic_group_bandage`群体绷带 | §3 行 1~9 |
| 政委 commissar | `commissar_command_blade`指挥刀 / `commissar_charge_order`冲锋令 / `commissar_pistol_shot`手枪射击 / `commissar_supervise`督战 / `commissar_battle_inspiration`战场鼓舞 / `commissar_mobilize`动员令 / `commissar_execution_order`处决令 / `commissar_burst_fire`连射 / `commissar_total_mobilization`总动员 | §4 行 1~9 |
| 近战小兵 melee_soldier | `melee_heavy_slash`重劈 / `melee_charge`突进 | §5 行 1~2 |
| 远程射手 ranged_archer | `ranged_precise_shot`精准射击 / `ranged_intimidating_shot`威吓箭（#165）/ `ranged_retreat`后撤 | §5 行 3~5 |
| 施法者 caster | `caster_fear_whisper`恐惧低语 / `caster_mental_shock`精神震荡 | §5 行 6~7 |

> 敌方技能归属字段仍写 `owner_unit`（敌方原型 id），目标是 AI 可用性过滤（enemy §5.4）与"该技能属于谁"的一致性校验。

**JSON 字段表（对应 skill.md / glossary §7.2 的 13 字段 + 标识字段）**：

| JSON key | C# 类型 | 单位·范围 | 必填 | 来源 | 备注 |
|---|---|---|---|---|---|
| `id` | string | ASCII，全局唯一 | ✅ | 【新命名】 | `<原型>_<动作>` 蛇形 |
| `name` | string | 中文名 | ✅ | skill_data | 13 字段之 1（技能名） |
| `owner_unit` | string | units.json id | ✅ | 【新命名】 | 归属原型（技能池归属 + 校验用） |
| `self_slots` | string \| int[] | `"all"` 或位置数组 | ✅ | 13 字段之 2 | **自身站位要求**。`"all"`=任意位置可用（战吼/急救/战场鼓舞等兜底）；数组元素：我方 1~6（含支援位 5、6）、敌方 1~4（相对本方编号） |
| `target` | object(TargetSpec) | 见下 | ✅ | 13 字段之 3 | **目标位置**：技能不直接选目标、只选位置（glossary §2） |
| `damage` | object? | 见下 | ⬜ | 13 字段之 4 | **伤害倍率**；治疗/纯增益类为 null |
| `hit_mod` | int | 对基础命中的加减（百分点） | ✅ | 13 字段之 5 | skill_data 命中列（+5 / −5 / +0 等） |
| `crit_mod` | int | 对基础暴击的加减（百分点） | ✅ | 13 字段之 6 | skill_data 暴击列 |
| `effects` | array(Effect[]) | 见下 | ✅（可为空数组） | 13 字段之 7 | **附加效果 + 概率**（眩晕/嘲讽/属性减益/护盾/守护等） |
| `displacement` | object? | 见下 | ⬜ | 13 字段之 8 | **位移效果**（推/拉/自移 + 格数） |
| `use_limit` | object(UseLimit) | 见下 | ✅ | 13 字段之 9 | **使用限制** |
| `morale_effects` | array(MoraleEffect[]) | 见下 | ✅（可为空数组） | 13 字段之 10 | **士气影响**（对象+数值）；精神伤害派生士气见下文"士气扣减衔接" |
| `tags` | string[](FuncTag) | 见 §2.1 | ✅ | 13 字段之 11 | **功能标签**（数组=组合，如 输出+AOE）；skill_data 标签列以"·"连接的词拆入数组 |
| `range_axis` | string(RangeAxis) | 见 §2.1 | ✅ | 13 字段之 12 | **距离轴** |
| `damage_axis` | string(DamageAxis) | 见 §2.1 | ✅ | 13 字段之 13 | **伤害轴**（决定减免轴、掉不掉士气、护盾/护卫挡不挡，#157） |
| `heal_fixed` | int? | ≥0，固定值 | ⬜ | combat_math §8 | **治疗固定值**：不吃攻击力。急救 12 / 群体绷带 5（每人）/ 喘息 8、10（自身）。对象随 `target` |
| `self_damage_fixed` | int? | ≥0，固定值 | ⬜ | combat_math §8 / glossary §3 | **自我伤害固定值**：殊死一搏 6 / 舍身 8。不被护盾吸收、可致死（走死门） |

**`target`（TargetSpec）子结构**：

| 字段 | C# 类型 | 说明 | 备注 |
|---|---|---|---|
| `scope` | TargetScope | `slots`/`self`/`any_ally`/`team`/`adjacent_ally_and_self` | skill_data 目标列逐条映射 |
| `side` | Side? | `scope=slots` 时必填：`player`/`enemy` | 敌方技能目标写我方（skill_data §5"我 X"）；我方技能目标可写敌方或己方 |
| `slots` | int[]? | `scope=slots` 时必填 | 取值须合法：`side=player` → 1~6；`side=enemy` → 1~4 |

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
>
> 范围内**全部为空 → 灰显不可释放**（NoTarget）；**部分非空 → 单体选其一、AOE 打全部非空**（不浪费、不落空）。**双段（0.55×2 / 0.5×2）= 同一目标两段，目标只选一次（#179）**。此为引擎规则、不入数据——**43 条技能 JSON 无需改动**。

**`damage`（DamageSpec）子结构 —— 多段倍率与公式型倍率的 JSON 表达**：

| 字段 | 类型 | 说明 |
|---|---|---|
| `segments` | DamageSegment[] | 伤害段列表；每段独立成伤。`0.55×2` = 两段各 0.55（skill_data §0"0.55×2 = 两段各 0.55"） |

段类型 `DamageSegment`（二选一）：

- 定值倍率段：`{"type":"flat","multiplier":1.0}` —— 段伤害 = 攻击 × multiplier。
- **"已失血%"公式段**（军医致命注射 / 政委处决令）：`{"type":"missing_hp","base":1.0,"coefficient":0.8}`，语义 `倍率 = base + (最大HP − 当前HP)/最大HP × coefficient`，即 skill_data 原文 `1.0 + 目标已失血% × 0.8`（处决令 0.9）；满血时加成 0（skill_data §3 注：`已失血% = (最大HP − 当前HP)/最大HP`）。

例：致命注射 → `{"segments":[{"type":"missing_hp","base":1.0,"coefficient":0.8}]}`；双连击 → `{"segments":[{"type":"flat","multiplier":0.55},{"type":"flat","multiplier":0.55}]}`。

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

结算语义：过目标位移抗性（`rand(0,100) >= 抗性`）后走逐级交换链（formation §2）；撞边界 = 位移失败不动（#78），撞障碍 = 交换（#22）；位移不产生空位、不产生伤害、不触发死门（#117）。敌人位移仅能由技能触发（#88）。

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
| `retreat_success` | 撤退成功 | −10 | team | normal（一次撤退结算） | #126/#43 | — |
| `retreat_fail` | 撤退失败 | −5 | team | normal（一次撤退结算；失败当回合不可再试 #118） | #126/#43 | — |
| `weak_hit_any_damage` | 虚弱状态下受伤（任意类型） | −5 | self | once_per_turn_max1 | #157 | §5.1 例外：物理不掉士气的唯一例外；每回合最多 1 次；可改"仅精神"（开放题 #README-3） |

> 事件触发顺序（combat_math §5.2）：**先结算伤害 → 再结算士气 → 再判定虚弱/死门**，为引擎规则。
> 士气钳制 [0, 100]（morale §1）。技能自带的士气（战吼/喘息/战场鼓舞等）在 skills.json `morale_effects`，此处仅收录系统事件 + 战场鼓舞（用于一致性校验）。

### 3.4 `buff_defs.json` 与状态建模

**用途**：把 buff.md §7/§7.1、morale §5~§8、combat_math §4 里出现的**可挂状态**提炼为数据结构。设计目标 = buff.md §1："一个 buff = 一组修改器 + 一组钩子 + 一条生命周期"，加 buff 只加数据。

**分类裁定（buff vs 单位状态）**：

| 类别 | 收录位置 | 成员 | 依据 |
|---|---|---|---|
| **buff**（数据驱动、可挂可摘、走 buff.md §1 管线） | buff_defs.json | 眩晕、嘲讽、流血（预留）、护盾（次数型）、护卫/守护（链接）、捆缚（预留）、折磨×3、美德×4、突进增伤；属性增减益以技能内 `stat_mod` 效果承载（不单列 buff 记录） | buff.md §7 |
| **单位状态 unit_state**（机制状态机字段，**非 buff 数据**，不收录进 buff_defs） | tuning.json（数值）+ 士气/生存模块状态机 | 虚弱 Weak、崩溃余烬 CollapseEmber | buff.md §5.1（虚弱不可驱散）、morale §4.0（崩溃余烬） |

理由：虚弱与崩溃余烬没有"可驱散/可叠层/有图标周期"的 buff 语义（虚弱被驱散会跳穿整个虚弱/死门系统，buff.md §5.1 红线），放进独立状态机比混入 buff 管线更干净；其数值常量集中到 tuning.json（§3.7）。

**状态总表（每个可挂状态的数据结构）**：

| 状态（id / 中文） | 类别 / 极性 | 数值 | 持续 | 叠层 | 驱散 | 施加来源 | 出处 |
|---|---|---|---|---|---|---|---|
| `stun` 眩晕 | buff / negative | 跳过本次行动（无伤害数值） | `action_skip`（1 次行动后结束） | refresh（上限 1，再次施加刷新） | ✅ 可驱散 | 盾击（概率 35/40）、麻醉针（30） | GDD §2.5 / combat_math §4 / buff.md §5.1 |
| `taunt` 嘲讽 | buff / negative | 改敌方 AI 优先攻击施法者（坦克） | `rounds` = 2 | refresh | ✅ 可驱散 | 坦克·嘲讽（CD 3） | skill_data §2 / buff.md §7 |
| `bleed` 流血 | buff / negative | 每回合 3 点固定伤害（回合结束结算） | `rounds` = 2 | refresh | ✅ 可驱散 | **切片无技能施加**（预留，定义随 combat_math §4 收录） | combat_math §4 / buff.md §7 |
| `shield` 护盾（次数型） | buff / positive | 抵挡 N 次物理攻击（铁壁 = 2 次） | `charges`（次数归 0 消失） | 待拍板（见 O-25）；不消耗流血/位移/自我伤害；AOE 消耗 1 次 | ❌ 不可驱散 | 铁壁（CD 4） | #156 / combat_math §8.1 / buff.md §7.1 |
| `guard_attach` 护卫/守护链接 | buff / positive（挂在保护者） | 伤害重定向到保护者自身（**只挡物理**，见 O-22）；被守护者 +3 士气（#136） | 未定（盾墙未标 → **O-26**） | 每回合最多重定向 1 次（#159） | ❌ 不可驱散 | 坦克·盾墙（目标=相邻友方） | #136/#159 / buff.md §7.1 |
| 属性增减益 `stat_mod` | buff（效果内嵌，不单列 id） | 攻/防/韧/速固定值增减（见 §3.2 effects） | `rounds` = 2 | refresh（buff.md §5 有时限→刷新） | 正面 ❌ / 负面 ✅ | 战意/盾墙/坚守/动员令/投掷药瓶/督战/恐惧低语 | combat_math §4 / buff.md §5.1 |
| `affliction_fear` 恐惧 | buff / negative | 使用技能时有概率（33%）拒绝释放；不消耗行动、技能灰掉必须重选（#61） | `until_morale_50` | 单折磨（自然不并存，见备注） | ✅ 可驱散 | 崩溃判定 | morale §4.1 / §5 / #57/#61 |
| `affliction_selfish` 自私 | buff / negative | 被治疗/鼓舞时有概率（33%）拒绝这一次 | `until_morale_50` | 同上 | ✅ 可驱散 | 崩溃判定 | morale §4.1 / §5 |
| `affliction_uncontrolled` 失控 | buff / negative | 攻击时有概率（33%）随机更换目标（战斗位）；支援位触发改为「捆缚」（#48） | `until_morale_50` | 同上 | ✅ 可驱散 | 崩溃判定 | morale §4.1 / §5 / #48 |
| `bound` 捆缚 | buff / negative | 无法行动（行动前钩子） | 见备注（O-17） | refresh | ✅ 可驱散 | 支援位失控的兜底（#48）；切片实际不触发（支援位无攻击动作） | morale §5.1 / buff.md §4 |
| `virtue_brave` 勇猛 | buff / positive | 造成伤害 +25%，免疫恐惧 | `until_battle_end_or_morale_zero` | **none（上限 1）** | ❌ 不可驱散 | 崩溃判定（小概率）/ 士气冲满 100 | morale §6 / buff.md §5 |
| `virtue_resolute` 坚韧 | buff / positive | 死门抗性 +20% | 同上 | 同上 | ❌ | 同上（切片美德池暂不含，见备注） | morale §6 |
| `virtue_inspired` 振奋 | buff / positive | 每回合给全队回士气（**数值缺失 → O-27**） | 同上 | 同上 | ❌ | 同上 | morale §6 |
| `virtue_focused` 专注 | buff / positive | 暴击率 +15% | 同上 | 同上 | ❌ | 同上（切片美德池暂不含） | morale §6 |
| `next_attack_boost` 突进增伤 | buff / positive | 下次攻击 +20% | `next_attack_within_rounds`（2 回合内） | refresh | ❌ | 敌人·突进 | skill_data §5 行 2 |

> **美德不叠层、上限 1**（#94/#159）：已有美德时再次冲满 100 → 保留旧美德、只结算全队 +10（#100）。美德持续到"战斗结束 **或** 士气再次归 0（先到为准）"；再次归 0 → 消除美德 → 重新判定（morale §8）。
> **折磨持续到士气回初始值 50**，结束点 = 恢复可判定资格点（morale §4.0）；崩溃余烬期间（士气停在 0）不再触发判定（事件触发，非状态触发）。
> 折磨降死门抗性 −10%（#123）→ tuning。折磨自然不并存：恢复判定资格时旧折磨已结束（回 50）。
> 美德池切片范围：morale §6 / GDD §3.2 建议切片先做勇猛 + 振奋；因振奋数值缺失（O-27），**切片美德池实际仅 [勇猛]**，待数值补齐后由数据扩展（池成员列表放 tuning.json `collapse`，见 §3.7）。

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
| `modifiers` | array | ⬜ | 修改器（buff.md §3）：`{kind: stat_mod|state_flag|damage_mod|prob_mod, ...}`；数值见状态总表 |
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

**切片 3 原型规则（逐字对应 enemy.md §5.4）**：

| 原型 | 顺序 | when | skill_id | 对应策划原文 |
|---|---|---|---|---|
| melee_soldier | 1 | `self_slot_in:[3,4]` | `melee_charge` 突进 | ① 若不在前排（被推到 3/4）→ 突进归位 |
| melee_soldier | 2 | （默认） | `melee_heavy_slash` 重劈 | ② 否则重劈 |
| ranged_archer | 1 | `self_slot_in:[1,2]` | `ranged_retreat` 后撤 | ① 若被近战贴脸（敌方在 1/2）→ 后撤拉开（口径歧义见 O-28；与后撤站位要求 [1,2] 对齐） |
| ranged_archer | 2 | （默认） | `ranged_precise_shot` 精准射击 | ② 否则【精准射击】 |
| ranged_archer | 3 | `player_morale_all_at_least:40` | `ranged_intimidating_shot` 威吓箭 | ③ 我方士气整体偏高（无人 <40）且【威吓箭】CD 就绪 → 用威吓箭压士气（#165）；按首条命中语义与第 2 条存在互斥问题 → **O-28** |
| caster | 1 | `target_slots_occupied_min:{player,[1,2],min:2}` | `caster_mental_shock` 精神震荡 | ① 优先【精神震荡】打多人（AOE 收益高） |
| caster | 2 | （默认） | `caster_fear_whisper` 恐惧低语 | ② 否则【恐惧低语】点名最低韧性单位（"点名最低韧性"与技能目标"我 1 位"冲突 → **O-21**） |

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
| `displacement_only_via_swap_chain` | true | 位移只走逐级交换链，永不产生空位，角色不可走进空格 | #20/#21 / formation §2 |
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
| `retreat` | object | `{success_morale:-10, fail_morale:-5}` | #126/#43 | 撤退成功/失败士气代价（数值同 morale_events，供展示与对账） |
| `support_slot_morale_per_turn` | int | `3` | #60 | 站支援位每回合 +3（本人） |
| `affliction_proc_percent` | int | `33` | #57 | 折磨各状态统一触发概率 33%（自私/恐惧/失控；失控建议后续最低档） |
| `guard_redirect` | object | `{max_per_turn:1, physical_only:true}` | #159 / buff.md §7.1 | 护卫每回合最多重定向 1 次；"只挡物理"见 O-22 |
| `overtime_reinforcement` | object | `{trigger_round:6, fill_or_buff:"fill_empty_then_buff_present"}` | GDD §1.5.2 / #69/#71 | **超时增援第 6 回合**起：有空位填人，无空位给在场敌人上增益（+攻/+速，具体数值缺失 → O-20） |
| `bleed` | object | `{per_round_damage:3, rounds:2}` | combat_math §4 | **流血每回合 3 / 2 回合**（回合结束结算）；须与 buff_defs `bleed` 一致（校验 P6） |
| `stat_debuff_default` | object | `{delta:-3, rounds:2}` | combat_math §4 / §10 | **属性减益固定 −3 / 2 回合**（速度也走固定值）；与技能韧性 −15/−10 的张力见 O-23 |
| `stun` | object | `{effect:"skip_own_action"}` | GDD §2.5 | 眩晕跳过本次行动，随后状态结束 |
| `damage_floor` | int | `1` | combat_math §2.3 | 任何来源伤害最低 1 点 |
| `hit_clamp` | object | `{min:55, max:100}` | combat_math §1 | 命中率钳制 [55, 100] |
| `crit_multiplier` | double | `1.5` | combat_math §2.1 | 暴击倍率（未暴击 1.0） |
| `damage_float` | object | `{enabled:false, min:0.9, max:1.1}` | combat_math §2.1 / README 开放题 1 | 伤害浮动默认 1.0 关闭 |
| `speed_float` | object | `{enabled:true, percent:10}` | #163 / GDD §2.5 | 速度浮动 0~10%，每回合重掷 |
| `collapse` | object | `{affliction_pool:["affliction_fear","affliction_selfish","affliction_uncontrolled"], virtue_pool:["virtue_brave"], proc: "morale drop >0→0 event"}` | morale §4~§6 | 崩溃判定：事件触发（士气从 >0 降到 0）；美德池切片默认仅勇猛（振奋数值缺失 O-27）；判定后士气留在 0（#67） |

> 士气满 100 的系统行为：给自己上美德（随机抽取 #55）+ 全队 +10（morale_events `morale_full_100`）+ 自身回 50——复位值属行为常量，随 `morale.start` 语义（50）执行，不单列键。

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
| P3 | **覆盖校验（skill_data §6"每角色每位置 ≥2 可用技能"）** | `∀u∈player_units: ∀pos∈1..6: count{s∈skills(u): s.self_slots="all" ∨ pos∈s.self_slots} ≥ 2`（位置 1~4 战斗位与 5、6 支援位分别计入）；且每角色至少 1 个 `self_slots="all"` 兜底技能（战吼/急救/战场鼓舞） | skill_data §6 / character §2 | 启动报错；§6 表内"位 1、2"声明数字与逐行自数存在口径差异（含殊死一搏/战吼后自数更多），仅按"≥2"硬校验，细账不阻塞 |
| P4 | 派生量不入库 | `∀u∈units: u 不含 mental_reduction 与 virtue_rate 字段`（schema 级白名单校验，反序列化模型无此属性即可） | glossary §7.1 注 | 结构校验（建模即满足） |
| P5 | 士气事件 ↔ 技能对账 | `∀e∈morale_events: e.source 指向技能 ⇒ skills 中对应技能 morale_effects 含同 delta/scope`（战场鼓舞 +15 与 morale_events `battle_inspiration`） | morale §2 与 skill_data §4 | 启动报错（防止双源漂移） |
| P6 | tuning ↔ buff/技能常量一致 | `tuning.bleed.per_round_damage == buff_defs(bleed).modifiers.damage` 且 `tuning.bleed.rounds == buff_defs(bleed).duration.value`；`tuning.weak.*` 与相关技能/状态无矛盾 | combat_math §4 与 §3.4 | 启动报错（同一数值两处定义，改动须同步） |
| P7 | 敌人数据边界 | `∀e∈units(side=enemy): e.deaths_door_resist == null`；enemy_ai 的 rules[].skill_id 属于该原型且 `self_slots` 与 AI 触发位置不矛盾（如后撤 self_slots=[1,2] 与 ranged rule1 一致） | enemy §1 / §5.4 | 同上 |
| P8 | 数值范围 | 百分比 ∈ [0,100]；`resilience ∈ [0,100]`；`hp/attack/speed > 0`；`damage.segments` 非空（当 damage 非 null）；`use_limit.cooldown.value ≥ 1` | glossary §7.1 / 通用约定 | 同上 |
| P9 | 士气事件表完整性 | morale_events 必须包含 §5.2 全部 14 行 id（缺失=实现期士气数值静默丢来源） | combat_math §5.2 | 启动告警/报错（缺失即失败） |
| P10 | 派生一致性抽查 | 运行时复算样例（combat_math §7.1 单次伤害表）应复现文档数字（如战士→近战小兵 = 9）；该样例验证属 M2 验收而非启动校验 | verification §1 / README M2 | 测试阶段断言 |
| P11 | **目标语义一致性（#178/#179）** | `∀s∈skills: (tags 含 aoe) ⇒ scope=slots`（aoe 只允许"范围型多格目标"）；切片 aoe 集 = {`warrior_sweep`, `caster_mental_shock`}；非 aoe 的 `slots`/`any_ally` 技能在执行层必须走**选一**路径（SkillTargetResolver 输出候选池 + 调用方选择，禁止"范围内全命中"） | GDD §1.2 / skill.md §2 字段3 / O-38 | 启动报错（aoe 标签越界）+ 执行层行为断言（单体技能单次结算） |

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
  "target": { "scope": "slots", "side": "player", "slots": [1] },
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

> 对应表：站位 2、3、4；目标 我 1 位；倍率 0.8；附加 = 目标韧性 −10（2 回合）；限制 CD 1（#165 后持续压力源）；轴 远程·精神；标签 输出·减益。
> 士气衔接：`damage_axis=mental` 单目标命中 → 按 morale_events `mental_hit` 对目标 −8（精神暴击 −12）；韧性 −10 使精神减免与美德率同降（#158 双威胁）。"点名最低韧性单位"（enemy §5.4）与固定目标"我 1 位"冲突见 **O-21**。

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
| 槽位三态 | `occupied` / `empty` / `blocked`（SlotState） | GDD §1.1【新命名】 |
| 空气（间隙） | `air_gap` | glossary §1【新命名】 |
| 换人 / 增援 | `reinforce`（战斗位发起，GDD §1.4.1） | #41【新命名】 |
| 覆盖校验 / 灰显 | `coverage_check` / `grayed_out_reason`（运行时） | skill_data §6 / ui_spec §4【新命名】 |
| 附加效果实际概率 | `actual_prob = labeled_prob × (1 − resist)` | combat_math §4【新命名】 |

---

## 7. 开放问题（已并入 open_issues.md B 节，编号与其一致）

> 按 _conventions §5：O-01~O-10 已被策划 README §5 的 10 条占用；以下为 schema 侧新增疑问，**编号即 open_issues.md B 节的权威编号（O-11~O-39）**，标注【策划拍板】/【架构裁定】/【实现期待定】与阻塞范围。**本文不改 open_issues.md**，以下仅供架构师归档与任务卡引用。

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

> 非阻塞说明（不挂开放题，仅记录）：README §2 M3 行"42 条技能"应为 43 之笔误（以 skill_data 36+7 为准）；skill_data §6 覆盖表"位 1、2"的计数与逐行自数存在口径差异，校验按 ≥2 硬规则执行（P3）。
