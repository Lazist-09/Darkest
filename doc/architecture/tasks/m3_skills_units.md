# M3 技能与角色（任务卡集）

> **编号**：ARCH-T-M3 · **类型**：task · **状态**：草案
> **上游**：[设计] doc/modules/skill.md（§1~§6）/ skill_data.md（§1~§6）/ character.md（§1、§2、§7）· doc/GDD.md §0.3、§1.2 · doc/modules/glossary.md §2、§7.2 · **决策引用**：#23、#127、#134、#136、#139、#140、#143、#149、#153、#156、#157、#165
> **依赖**：T-M2 系列（结算与属性，见 tasks/m2_combat_core.md）+ doc/architecture/data_schema.md（§3.1/§3.2/§3.5/§4）· **最近更新**：2026-09-09

> 本文只写任务卡，不写业务实现。数字、技能名与 `doc/modules/skill_data.md` **逐字一致**，禁止"顺手改数值"。

---

## 0. 里程碑目标与判据对齐

**一句话目标**：把 `skills.json` 的 **43 条技能**（36 我方 + 7 敌方，13 字段模板全量落模型）做成只读强类型数据送入确定性内核，落地「技能栏 9 选 5 → 可用性判定（灰显 + tooltip 原因）→ 目标部分非空结算 → 在 M2 DamagePipeline 上执行技能」，并保证**每角色每位置 ≥2 个可用技能（换位不废人）**。

**与策划 README M3 判据对齐**：

| 判据来源 | 原文判据 | 本任务卡落地 |
|---|---|---|
| doc/README.md §2（M3 行） | 4 个角色在每个位置都**至少 2 个可用技能** | T-M3-02 校验门禁 P3 + T-M3-08 覆盖率回归 |
| doc/architecture/blueprint.md §11（M3 行） | M3 = 13 字段数据结构、站位门槛/目标位/灰显；判据用 skill_data §6 覆盖校验表 + 灰显原因正确 | T-M3-01 / T-M3-03 / T-M3-05 |
| doc/README.md §2 M3 行"42 条技能数据" | 与 skill_data.md 36+7=43 不符，按 **O-16** 以 **43 条**为准（README 疑笔误，不阻塞） | T-M3-01 / T-M3-02 断言 43 |

> **前置假设**：`units.json`（data_schema §3.1：7 原型属性 + 技能池 id 列表，我方每原型 9、敌方 melee_soldier 2 / ranged_archer 3 / caster 2）的加载与注册表由 T-M2 系列/B1 数据任务交付；若未交付，须在 T-M3-01 前补一张 data_schema §3.1 的 units.json 导入子卡（不改变本文件卡片结构）。

---

## 1. 引用基线：开放题与口径裁定（open_issues.md §B 核对结果）

> 歧义一律引用 `doc/architecture/open_issues.md` §B 权威编号（本文件不复述全文，只取对 M3 生效的裁定基线）。

| 开放题 | 议题 | 归属 / 现状 | 本文档用法 |
|---|---|---|---|
| **O-12** | 一次技能内部步骤与 AOE 次序（附加/位移相对位置、中途死亡剔除、未命中子效果） | 策划拍板/架构裁定；蓝图 §5b/§8 有工作次序草案 | T-M3-06：骨架按"附加/位移在本技能全部目标结算后执行"组装，拍板后回填 |
| **O-13** | 多段倍率（0.55×2 / 0.5×2）各段是否分别过命中/暴击/护盾 | 架构裁定；建议"各段独立结算实例"，Monte Carlo 校 | T-M3-01 落库、T-M3-06 逐段、T-M3-07 标注 |
| **O-16** | 数据口径冲突：character.md 草案 vs skill_data.md 实值；README「42」疑笔误 | **架构裁定·已定**：以 skill_data.md/combat_math 为准；导入校验断言 43 | T-M3-01/02/07/08 全程遵循 |
| **O-23** | 属性减益"固定 −3"与技能韧性 −15/−10 并存口径 | 策划拍板；现状=专属减益（韧性 −15/−10）走技能标注值，通用 −3 只用于攻/防/速 | T-M3-02 校验不强制统一 |
| **O-24** | 无概率标注的属性减益/嘲讽是否过抗性 | **架构裁定·已定**：有概率才过抗性、无概率直挂 | T-M3-01 字段成对、T-M3-02 校验、T-M3-06 执行 |
| **O-29** | 战斗中能否换携带 / 配装硬约束 / 支援位技能池 9 选 5 | 策划拍板；现状按"**战前定 5、战中不可换**"实现 | T-M3-03、T-M3-04 |
| **O-21** | 威吓箭士气 −4 vs 精神伤害 −8 的关系 | 策划拍板（不阻塞数据录入） | T-M3-06/07 仅标注、不裁决 |

---

## 2. 任务卡总览

| 卡 | 名称 | 一句话 | 主要产出（res://） |
|---|---|---|---|
| T-M3-01 | SkillTemplate 强类型模型与 skills.json 全量导入 | 43 条技能逐字段落模型、加载进只读注册表 | data/skills.json、scripts/data 模型+Loaders、resources/skills/ |
| T-M3-02 | 数据校验门禁 P1~P3 | 引用完整 / 技能数=43 / 覆盖每位置 ≥2 | scripts/data/Validators.cs、tests/DataGateTests.cs |
| T-M3-03 | 技能可用性判定 ISkillUseResolver | 四步判定顺序 + AvailabilityReason + 灰显 tooltip | scripts/core/contracts、scripts/gameplay/sim/skill/SkillUseResolver.cs |
| T-M3-04 | 战前配装 9 选 5 数据流 | 战前冻结携带 5、战斗中不可换 | scripts/core/contracts（SkillLoadout/BattleSetup） |
| T-M3-05 | 目标解析与"部分非空"结算 | 只打非空位置、障碍当目标参与 | scripts/gameplay/sim/skill/SkillTargetResolver.cs |
| T-M3-06 | 技能执行骨架 | 在 M2 DamagePipeline 上编排目标/段/效果/位移 | scripts/gameplay/sim/skill/SkillExecutor.cs、tests/SkillExecutionTests.cs |
| T-M3-07 | 特殊技能标注核对 | 每场 1 次 / 全部位兜底 / 多段 / 已失血% 逐字入库 | tests/SpecialSkillTests.cs + 数据标注核对 |
| T-M3-08 | 覆盖率回归测试 | 按 skill_data §6 表逐角色逐位置断言 ≥2 | tests/SkillCoverageTests.cs |

---

## 3. 任务卡

### T-M3-01 SkillTemplate 强类型模型与 skills.json 全量导入
- **上游**：[设计] doc/modules/skill.md §2（13 字段模板）/§3（三轴）/§4（使用限制）；skill_data.md §0~§5（43 条填实）· doc/architecture/data_schema.md §3.2（skills.json 字段表与记录索引）/§5（三条完整 JSON 示例）· 决策：#153、#156
- **依赖**：T-M2 系列（B1 加载管线与只读注册表就绪）+ data_schema.md §3.1（units.json 原型 id 全集，供 owner_unit 落库）
- **产出**：`res://data/skills.json`（43 条，逐字转写 skill_data §1~§5）；`res://scripts/data/` 下强类型模型（SkillTemplate 及子结构 record，字段名与 data_schema §3.2 逐 key 一致）与 Loaders（JSON→校验→绑定→只读快照）；`res://resources/skills/*.tres`（编辑器导入派生，blueprint §7）
- **要点**：
  1. **数据总量**：43 条 = 我方 4 原型（战士/坦克/军医/政委）各 9 + 敌方 3 原型 7 条（melee_soldier 2 / ranged_archer 3 / caster 2），行号对齐 skill_data §1~§5；记录 id 以 data_schema §3.2「记录索引」为唯一来源（43 个 id，如 `warrior_cleave`…`caster_mental_shock`）。
  2. **13 字段逐字段落模型**（JSON key 一律照 data_schema §3.2/§2.1，C# 类型 PascalCase，下行为契约清单非实现）：

     | skill.md 字段 | JSON key | 类型/语义 | skill_data 出处 |
     |---|---|---|---|
     | 1 技能名 | `name` | string（中文名逐字） | 各表行 1 |
     | 2 站位要求 | `self_slots` | `"all"`（任意位置）或位置数组；我方 1~6、敌方 1~4 相对本方 | 各表站位列 |
     | 3 目标位置 | `target` | TargetScope 五类：`slots`/`self`/`any_ally`/`team`/`adjacent_ally_and_self`（data_schema §2.1） | 各表目标列 |
     | 4 伤害倍率 | `damage` | `segments[]`（flat / missing_hp 二型，见下） | 倍率列 |
     | 5 命中修正 | `hit_mod` | int 百分点（对基础命中加减，+0 不省略） | 命中列 |
     | 6 暴击修正 | `crit_mod` | int 百分点 | 暴击列 |
     | 7 附加效果 | `effects` | Effect[]（type/probability/resist_axis/stat/delta/duration_rounds/charges/apply_to） | 附加列 |
     | 8 位移效果 | `displacement` | `{type: push/pull/self_forward/self_backward, count}` | 位移列 |
     | 9 使用限制 | `use_limit` | `{type: none/cooldown/per_battle/every_n_rounds, value}`；**切片 43 条仅用前三型**，`every_n_rounds` 为模板预留（data_schema §2.1/§3.2） | 限制列 |
     | 10 士气影响 | `morale_effects` | MoraleEffect[]（scope: self/targets/team/ally_targets + delta） | 士气列 |
     | 11 功能标签 | `tags` | FuncTag[]（output/control/displacement/support/heal/aoe/debuff，skill_data 标签列以"·"拆入数组） | 标签列 |
     | 12 距离轴 | `range_axis` | melee/ranged/none（无伤害类写 none） | 轴列 |
     | 13 伤害轴 | `damage_axis` | physical/mental/none | 轴列 |
     - 另含标识字段 `id`、`owner_unit`（units.json id）与两个补充字段：`heal_fixed`（治疗固定值，不吃攻击力，combat_math §8）、`self_damage_fixed`（自我伤害固定值）。
  3. **多段与公式型倍率**（data_schema §3.2 DamageSpec）：`0.55×2`/`0.5×2` → `segments` 两条 `{"type":"flat","multiplier":0.55}`（skill_data §0"两段各 0.55"）；「已失血%」公式段 `{"type":"missing_hp","base":1.0,"coefficient":0.8}` 语义 = `1.0 + 目标已失血% × 0.8`（致命注射），处决令 coefficient **0.9**；满血加成 0（skill_data §3 注）。倍率一律 double，百分比一律整数百分数（data_schema §2.2）。
  4. **附加效果概率/抗性成对**（O-24 已定口径）：带 `probability` 的效果必须带 `resist_axis`（眩晕→`stun_resist`、流血→`bleed_resist`、属性减益→`stat_debuff_resist`）；**无概率的效果（嘲讽、护盾、守护、stat_mod、突进增伤）不填 probability**——无概率=直挂、不过抗性。眩晕概率逐字：战士盾击 35 / 坦克盾击 40 / 麻醉针 30。
  5. **护盾次数型**（#156 按次数不按点数）：铁壁 → `effects:[{type:"shield", charges:2, apply_to:"self"}]` + `use_limit:{type:"cooldown", value:4}`（data_schema §5.2 完整示例即铁壁，可直接对照）。shield 生命周期/buff 记录归 buff_defs（M4 链），本卡只保证 `charges:2` 逐字入库。
  6. **士气衔接边界**：技能显式 `morale_effects`（战吼 team+5、喘息 self+8、战场鼓舞 targets+15 等）与"精神伤害派生士气"（mental 命中 −8/暴击 −12/AOE −5，由引擎按 morale_events 行结算，**不入技能数据**）两类，加载器**不得**要求精神技能必须自带士气字段（data_schema §3.2"士气扣减衔接"）；威吓箭 targets−4 是唯一同时显式声明 morale_effects 的精神伤害技能（#165），关系口径见 O-21，本卡只落数据。
  7. **同名技能独立记录**：战士喘息（heal_fixed 8）/坦克喘息（heal_fixed 10）、战士盾击（0.6）/坦克盾击（0.7）为不同记录，禁止按技能名合并、禁止串数值（data_schema §3.2 首注）。
  8. **敌方技能同模板**：owner_unit 指向敌方原型（melee_soldier/ranged_archer/caster），`heal_fixed`/`self_damage_fixed` 为 null；目标 side=player（skill_data §5"我 X"映射），范围 1~4 我方位（我方 6 槽中前 4 战斗位为射程常态，但 target.slots 合法值仍按 side=player→1..6 建模，具体取值照表）。
- **完成判据（可测）**：
  - Loaders 全量导入无异常；断言 `skills` 记录数 **= 43**，且 = skill_data §1~§5 行数合计（9+9+9+9+7）；
  - 抽样复算：data_schema §5.1（劈砍）/§5.2（铁壁）/§5.3（恐惧低语）三条完整 JSON 与导入结果**逐字段一致**；
  - 倍率 round-trip：致命注射 `missing_hp base 1.0 / coefficient 0.8`、处决令 `coefficient 0.9`、双连击/连射各拆两条 flat（0.55/0.5）与 skill_data 原文行一致；
  - 任一枚举越界（target.side=friend、range_axis=air、probability 无 resist_axis 等）导入即 fail-fast 报"文件+记录 id+规则"（data_schema §4.1），不带病进战斗。
- **风险/开放**：O-16（character.md §3~§6 草案表**不参与运行**、数值一律以 skill_data.md 为准）；O-13（多段逐段执行语义不影响落库，见 T-M3-06）

### T-M3-02 数据校验门禁 P1~P3（含技能数=43 断言）
- **上游**：[设计] doc/architecture/data_schema.md §4.2（P1~P3 判定式）；skill_data.md §6（覆盖校验表）；character.md §2（每位置可用技能硬要求）· doc/README.md §2 M3 判据 · 决策：#153；开放题 O-16、O-23、O-24
- **依赖**：T-M3-01（skills.json 导入）+ units.json 注册表（T-M2/B1）+ buff_defs.id 集合（引用完整用；记录本可先空，M4 前不得引用未定义 buff_id）
- **产出**：`res://scripts/data/Validators.cs`（P1~P3 + 43 断言，判定式照 data_schema §4.2）；`tests/DataGateTests.cs`（数据门禁用例，含扰动反例）
- **要点**：
  1. **P1 引用完整**：`∀f∈{units,skills,enemy_ai,buff_defs}: ids(f) 无重复`，且 `skills.owner_unit ∪ formation.initial_roster[].unit ⊆ units.id`（data_schema §4.2 P1 逐字）。
  2. **技能数=43 硬断言**（O-16）：导入校验断言 `count(skills)==43`；README §2 M3 行的"42"按笔误不采用；character.md 草案数值不参与任何校验输入。
  3. **P2 技能引用完整**：`target.side ∈ {player,enemy}`；`scope=slots ⇒ 0≤slot≤(side=player?6:4)`（敌方 4 位无支援，GDD §1.5）；`effects[].buff_id`（若有）∈ buff_defs.id；`owner_unit` ∈ units.id（data_schema §4.2 P2）。
  4. **P3 覆盖校验**：`∀u∈player_units: ∀pos∈1..6: count{s∈skills(u): s.self_slots="all" ∨ pos∈s.self_slots} ≥ 2`（位 1~4 战斗位与 5、6 支援位分别计入），且每角色 ≥1 个 `self_slots="all"` 兜底（战吼×2 / 急救 / 战场鼓舞）——判据基准 = skill_data §6 覆盖表。
  5. **口径差异处理（P3 注，勿误伤）**：skill_data §6 表声明数（战士 5/5/4/3、坦克 7/6/2/2、军医 6/6/4/4、政委 5/5/4/4）与"逐行自数"存在差异（把殊死一搏/战吼/总动员等计入后自数更多为合法），**只按 ≥2 硬校验、以表声明数为下限判据，禁止用"相等"断言**（否则战士位 1/2、坦克位 1/2、政委位 1/3 等格会误红）；细账不阻塞。
  6. **O-23 属性减益口径进校验**：`stat_mod` 的 `delta` 对韧性减益只允许 skill_data 标注值（投掷药瓶韧性 −15、督战/恐惧低语韧性 −10，各 2 回合），**不得**被 tuning `stat_debuff_default`(−3/2回合) 覆盖或强改；通用 −3 仅适用攻/防/速。检测到同技能 stat/delta 与 skill_data 行不符 → 启动报错。
  7. **O-24 成对规则进 schema 校验**：effects 条目 `probability` 与 `resist_axis` 必须同有同无；只有其一 → 报错（无概率效果代表直挂，见 T-M3-06）。
  8. 校验失败处理：启动报错含"文件 + 记录 id + 规则 + 期望 vs 实际"，阻断进战斗（data_schema §4.1 / blueprint §10"数据启动门禁"）。
- **完成判据（可测）**：
  - 对当前 43 条数据跑 Validators：P1/P2/P3 全绿；43 断言输出 == 43；
  - 覆盖率输出（角色×位置×可用技能名清单）与 skill_data §6 表逐格对账：自数 ≥ 表声明数且 ≥2，4 角色 × 6 位置全部成立；
  - 扰动反例逐条触发阻断：删 1 条技能→"计数 42"；某技能 self_slots 含 7→"slot 越界（P2）"；把军医急救 self_slots 从 "all" 改成 [1] → 位 5/6 可用数 <2→"覆盖不足（P3）"；
  - grep 证据：Loaders/Validators 源码无任何对 character.md §3~§6 草案数值的引用。
- **风险/开放**：O-16、O-23、O-24（均已按现状基线执行，无新增阻塞）

### T-M3-03 技能可用性判定 ISkillUseResolver
- **上游**：[设计] doc/modules/skill.md §5（可用性判定顺序）；doc/architecture/blueprint.md §9.3（接口契约：判定顺序 = ①战前携带 5 内 ②自身站位 ③目标位部分非空(全空灰显) ④使用限制）；doc/modules/ui_spec.md §2 必显 #3、§4（灰显 tooltip 文案）· GDD.md §1.2 · 决策：#23、#41b；O-29、O-24（判定不掷随机，仅读态）
- **依赖**：T-M3-01（SkillTemplate 读入）+ T-M2/M1（IFormation 槽位三态快照、单位运行时台账）+ T-M3-04（携带 5 冻结集）
- **产出**：`res://scripts/core/contracts/`（`ISkillUseResolver` + `Availability`/`AvailabilityReason` 接口与枚举声明，**照 blueprint §9.3 逐字**）；`res://scripts/gameplay/sim/skill/SkillUseResolver.cs`（实现：只读快照判定，零随机、零写状态）；`tests/SkillAvailabilityTests.cs`
- **要点**：
  1. 判定顺序固定四步、不可调换（skill.md §5 编号 1→4）：① 技能 ∈ 战前携带 5 ② 施法者当前站位 ∈ `self_slots`（或 `"all"`）③ 目标范围内"部分非空"（全空 → NoTarget）④ 使用限制余量（CD 冷却中 / per_battle 次数用尽）。返回**首个**不通过原因。
  2. 返回值契约（blueprint §9.3 逐字）：`enum AvailabilityReason { Ok, NotCarried, BadStance, NoTarget, OnCooldown, UsesExhausted }`；`Availability Resolve(UnitId caster, SkillId skill, IFormation snapshot)`。
  3. **UI tooltip 映射**（ui_spec §4 / 必显 #3，文案逐字）：

     | AvailabilityReason | tooltip 文案（ui_spec §4） | 判定依据数据 |
     |---|---|---|
     | BadStance | 站位不符 | `self_slots` vs 施法者槽位 |
     | NoTarget | 范围内没有目标 | `target` 解析后占用槽为空（全空灰显） |
     | OnCooldown | CD 中 | `use_limit.cooldown` 运行时余量 |
     | UsesExhausted | 每场次数用尽 | `use_limit.per_battle` 运行时余量 |
     | NotCarried | （不可达的防御性结果）未携带 | 技能栏只渲染携带 5（O-29），正常交互不出现；仅 AI/越界查询可能命中 |
  4. **敌方复用同一解析器**：敌方**无"携带"概念**，跳过第①步；②③④ 与玩家一致（data_schema §3.5 rules 解析语义 / enemy.md §2）。
  5. 判定第③步与目标解析同源：障碍**计入非空位**（#73/#77/#105 障碍当敌人处理、算技能目标），空位不阻断同范围其它非空位的可用性（#23 部分非空）。
  6. Resolve 幂等且确定性：同快照重复调用结果一致，供 UI 悬停实时刷新灰显与 tooltip；不做任何随机抽取、不写 CD/次数（只读运行台账）。
  7. 边界：施法者处于虚弱/眩晕等状态时的行动资格由回合状态机（M2/M4）在**调用 Resolve 之前**裁决，本接口只管技能可用性本身，不叠状态判定。
- **完成判据（可测）**：
  - SkillAvailabilityTests 构造快照逐项断言：未携带技能→NotCarried；战士在支援位 5 用劈砍（self_slots=[1,2]）→BadStance；目标位全空→NoTarget；盾击 CD 中→OnCooldown；殊死一搏第二次→UsesExhausted；正常→Ok；且失败原因顺序与 skill.md §5 的 1→4 一致（如"站位不符且 CD 中"返回 BadStance 而非 OnCooldown）；
  - UI 灰显 tooltip 文案与 ui_spec §4 四种原因逐字一致（站位不符 / 范围内没有目标 / CD 中 / 每场次数用尽），灰显状态与 Resolve 输出一一对应（必显 #3）；
  - 敌方 AI 用同一技能查询不因"未携带"误判（第①步对敌方跳过）；
  - 快照不变时重复调用 100 次全部同结果（幂等，无抽取审计行新增）。
- **风险/开放**：O-29（携带集合战斗期冻结，见 T-M3-04）；无新增

### T-M3-04 战前配装（9 选 5）数据流
- **上游**：[设计] doc/modules/skill.md §1（技能池 9 / 战前携带 5）、§6（待确认 3 项）；doc/GDD.md §0.3（技能栏决策）；doc/modules/character.md §1、§7（4 原型，技能池分布 7:2）· 决策：#139、#140、#41b；O-29
- **依赖**：T-M3-01（原型技能池 id 列表就绪）+ units.json（每原型 skills 9 id，data_schema §3.1）+ 6 人编成引用（character.md §1 / #149：支援位 5=战士、6=军医）
- **产出**：`res://scripts/core/contracts/` 中只读 record（如 `SkillLoadout(UnitId, IReadOnlyList<SkillId> carried)` 与 `BattleSetup` 构造入参契约）；配装合法性校验函数；BattleSession 对携带集的**冻结/拒绝**边界（导演层命令集不含"换携带"）
- **要点**：
  1. 数据流：`units.json skills（每原型 9）` → 战前 UI/策略选 **5** → `SkillLoadout` 冻结进入 `BattleSession` → 战斗中技能栏固定这 5 个（blueprint §5d SkillBar）。**战前定 5、战斗中不可换**（O-29 现状裁定；战斗中任何"更换携带"请求一律拒绝）。
  2. 合法性校验：carried 恰 **5** 个、无重复、全部 ∈ 该原型 units.json skills 池；6 人编成中重复原型（战士×2、军医×2）各实例持有**独立**携带集（同一池可选不同 5 个）。
  3. **位置限制是数据自洽而非职业判断**（#139 支援位是位置不是职业）：支援位能用的技能由 self_slots/target 数据保证（喘息/战吼/急救/战场鼓舞/动员令/群体绷带等不含攻击敌人类），校验可加"自我一致性"检查：任何 self_slots 覆盖 5/6 的技能，其 target 不得为敌方 slots。
  4. **不产生纯辅助职业**（#139/#140）：4 原型人人能打（伤害 7:2/#141），本数据流只做"携带子集"，不得因配装引入"职业只能增益"的规则或限制。
  5. 配装硬性约束（如"至少 1 个位移"）未拍板（skill.md §6 / O-29），本卡**不实现**任何配装约束，只记录待确认。
  6. per_battle 计数与 loadout 正交：BattleSession 内按 `use_limit` 初始化每场次数，随战斗结束丢弃（与携带集生命周期解耦）。
- **完成判据（可测）**：
  - 校验函数对 4 原型 × 池 9 的**任意 5 子集**通过合法性（恰 5、无重复、∈池），穷举组合无漏判；
  - 战斗中提交"更换携带"命令 → 被拒（返回拒绝原因，或命令枚举根本不含该类型，测试断言导演不接受）；
  - `SkillLoadout` 进入 BattleSession 后序列化只读：seed+命令流重放（DeterminismTests 镜像）不携带任何可变配装状态；
  - 边界用例：携带 6 个 / 携带池外 id / 携带重复 id → 校验各自报错。
- **风险/开放**：O-29（战前定 5 / 战中不可换 / 支援位技能池 9 选 5 一并记录，拍板后若改需回填本卡）

### T-M3-05 目标解析与"部分非空"结算
- **上游**：[设计] doc/GDD.md §1.2（技能不选目标、只选位置）/§1.5（敌方 4 位无支援、空位不打空）/§4（部分非空只打非空位置）；doc/modules/glossary.md §2（部分非空/技能灰显）；doc/architecture/data_schema.md §3.2 target 生效语义 · 决策：#23、#73、#77、#105
- **依赖**：T-M3-01（target 五类 scope 数据）+ T-M3-03（NoTarget 判定同源，全空灰显）+ M1（IFormation 槽位三态快照：角色/空/障碍）
- **产出**：`res://scripts/gameplay/sim/skill/SkillTargetResolver.cs`（scope→候选槽位→占用过滤→**有序**目标列表）
- **要点**：
  1. scope 五类解析规则：`slots` → side+slots 展开；`self` → 施法者当前槽；`any_ally` → 任意友方（结算指向被选友方所在槽）；`team` → 本方全队（含支援位 5、6，战吼/群体绷带/动员令）；`adjacent_ally_and_self` → 施法时刻按 formation 相邻语义展开"相邻友方 + 自身"（坦克盾墙）。
  2. **"部分非空"只作用于 slots 型多格目标**：对候选槽按占用过滤（`Occupied` 含**角色与障碍**，IFormation.OccupiedPositions(includeObstacle=true)）；全部为空 → 可用性 NoTarget（T-M3-03，UI 灰显"范围内没有目标"）；部分非空 → **只对非空位置生效，不浪费、不落空**（#23 / GDD §4）。
  3. **障碍当目标参与**（#73/#77/#105 已定）：障碍算非空位、吃伤害（可被打掉→血尽消失→触发向中靠齐，M1 语义）；但障碍**免疫一切 debuff/附加状态**（眩晕/减益/嘲讽/流血等不落于障碍，glossary §1/GDD §1.1），效果阶段按目标类型跳过；障碍不计入胜利条件（导演层，M5/M6 验收项，本卡只管目标列表含障碍）。
  4. 槽位边界：`side=enemy` 只有 1~4（敌方无支援位，GDD §1.5 / enemy.md §1）；越界槽（如 enemy slots=[5]）在数据层被 P2 拦截，运行时 Resolve 亦须防御（不静默放行，按 fail-fast 报错）。
  5. 我方全队/任意友方目标含支援位角色：治疗与鼓舞可打 5、6 位（急救/战场鼓舞 target=任意友方；战吼 target=我方全队含支援位）。
  6. **确定性**：目标列表一律按槽号**升序**输出（blueprint §8.1 遍历顺序固定铁律），供 T-M3-06 逐目标结算；本解析器零随机、零写状态。
- **完成判据（可测）**：
  - 构造快照"敌 1 空 / 敌 2 有角色 / 敌 3 障碍 / 敌 4 角色"，范围技能目标 敌 1~3 → 输出 [2,3]（跳过空 1）；目标仅敌 1 且空 → NoTarget；
  - 战士战吼/军医群体绷带在 6 人满编含支援位快照下 → 我方全队 6 个占用位全在目标列表；
  - AOE 命中障碍 → 障碍掉血并走消失/靠齐联动用例通过；障碍不挂任何附加状态（断言目标状态列表为空）；
  - 越界目标（side=enemy slots=[5]）→ 校验期 P2 报错 + 运行时 Resolve 失败，不得进入结算。
- **风险/开放**：O-12（AOE 逐目标中中途死亡/靠齐的剔除时机，T-M3-06 骨架一并承接）；O-08（障碍挡远程/胜利条件未定，**不在本卡实现**，障碍仅按 #73/#77/#105 当目标处理）

### T-M3-06 技能执行骨架（在 M2 DamagePipeline 之上组装）
- **上游**：[设计] doc/modules/combat_math.md §5.2（先伤害→再士气→再虚弱/死门）；doc/architecture/blueprint.md §5b（工作次序草案：附加/位移在本技能全部目标结算后执行）/§8（管线次序）；data_schema.md §2.3（随机判定写法）/§3.2（段/效果/位移子结构）· 决策：#23、#117；O-12、O-13、O-24、O-21
- **依赖**：T-M2 系列（DamagePipeline：命中→暴击→伤害→士气→虚弱/死门已落地）+ T-M3-01（段/效果/位移数据）+ T-M3-05（有序目标列表）
- **产出**：`res://scripts/gameplay/sim/skill/SkillExecutor.cs`（技能动作编排骨架，非公式实现）；`tests/SkillExecutionTests.cs`（次序与未命中用例）
- **要点**：
  1. **骨架固定形状**（伪代码级契约，具体实现归主程序）：
     ```
     ExecuteSkill(snapshot, skillId, 目标选择):
       复核可用性（T-M3-03，防御性，正常路径为 Ok）
       targets = SkillTargetResolver.Resolve(...)          // T-M3-05，槽号升序、含障碍
       foreach t in targets:                                // 逐目标
           DamagePipeline.Execute(施法者, t, skill.damage)  // M2：命中→暴击→(多段逐段)→伤害→士气→虚弱/死门
       全部目标结算完成后:
           EffectsPhase     // 附加效果/治疗/buff（眩晕/减益/守护/护盾/突进增伤…）
           DisplacementPhase// 位移：过目标位移抗性 → 交换链（#117：无伤、不死门）
       记录该技能 use_limit 消耗（CD 置位 / per_battle 计数减一）
     ```
     次序依据 blueprint §5b 尾部 Note（"技能附带效果（眩晕/流血/属性减益）与位移结算在本技能全部目标结算后执行"），O-12 拍板后如有调整回填本卡。
  2. **多段逐段**（O-13 建议口径）：双连击/连射每段 = 一次独立"伤害实例"，各段分别走命中/暴击/减免/护盾判定（架构裁定方向，Monte Carlo 校正）；段间不重建目标列表；每段抽取经 IRngProvider（blueprint §8 唯一随机出口 + DrawCount 审计）。
  3. **未命中语义**：命中失败 → 该目标无伤害、无士气变化；同技能其它子效果（附加/位移）对未命中目标是否照常属 O-12 范围——先按蓝图 §5b alt 分支口径（该目标不结算伤害，子效果语义随 O-12 草案实现并标注）落地，**不静默发明规则**。
  4. **士气衔接两路**：技能显式 `morale_effects` 在技能层结算（战吼 team+5 等）；精神伤害派生士气（−8/暴击−12/AOE −5）由引擎按 morale_events 行自动扣，数据不得重复声明（威吓箭 targets−4 例外，O-21 标注不裁决）。团队性事件聚合口径（暴击+5/击杀+10/队友虚弱−8/死亡−15）归 M4 士气任务（open_issues §B O-14），本卡不实现。
  5. **附加效果两步语义**（O-24 已定）：带概率 → `rand(0,100) < 标注概率 × (1 − 目标对应抗性)`（combat_math §4 / data_schema §2.3）；无概率（嘲讽/护盾/守护/stat_mod/突进增伤）→ **直挂、不掷骰、不过抗性**。
  6. **自我伤害/治疗固定值**：殊死一搏（自身受 6 伤）/舍身（自身受 8 伤）走 `self_damage_fixed` 固定值入口，**可致死（走死门）**；急救 12 / 群体绷带 5 / 喘息 8、10 走 `heal_fixed` 固定值，不吃攻击力（combat_math §8 / glossary §3）。护盾"次数不耗于流血/位移/自我伤害"（#156）的裁决在 M4 护盾生命周期，本卡只保证数值入口正确。
  7. 位移在技能全目标结算后统一执行，不产生伤害、不触发死门（#117）；过抗性判定 `rand(0,100) >= 目标位移抗性` 即成功（**大于等于**，data_schema §2.3 唯一例外）；逐级交换链/边界失败不动（#78）由 M1 交换链承接。
- **完成判据（可测）**：
  - SkillExecutionTests 用构造快照断言事件日志次序 = `目标1[命中→暴击→伤害→士气→状态] → 目标2… → 附加效果 → 位移`（blueprint §5b），事件流可回放；
  - 双连击对单目标产出**两段**伤害实例事件（每段独立抽取序号），AOE 对多目标逐目标发事件（槽号升序）；
  - 未命中目标无伤害/士气事件；位移过抗性失败与成功用例分别按 data_schema §2.3 判定式成立；
  - 全技能动作期间新增抽取全部经 IRngProvider，DrawCount 单调连续可审计（blueprint §8）。
- **风险/开放**：O-12（步骤次序/未命中子效果/中途死亡剔除时机）、O-13（多段逐段语义）、O-21（威吓箭士气 −4 与 −8 关系，M5 敌技挂起，数据已录入）；无新增

### T-M3-07 特殊技能标注核对
- **上游**：[设计] doc/modules/skill_data.md §1~§5（殊死一搏/舍身/总动员；战吼×2/急救/战场鼓舞；双连击/连射；致命注射/处决令）；doc/modules/glossary.md §3（自我伤害/治疗/护盾口径）；data_schema.md §3.2（use_limit/heal_fixed/self_damage_fixed/missing_hp 段）· 决策：#153、#156、#165；O-13、O-16、O-21
- **依赖**：T-M3-01（已入库）+ T-M3-02（已过 P1~P3 门禁）
- **产出**：`tests/SpecialSkillTests.cs`（特殊技能逐条断言）+ 数据标注核对清单（下表为断言基线，数值逐字，禁止改动）
- **要点**：
  1. **每场 1 次（use_limit.per_battle=1，恰 3 条）**：殊死一搏（战士 #9：倍率 2.0 / 命中 +10 / 暴击 +10 / 自身受 6 伤 / 站位 1~4 / 目标 敌 1、2）；舍身（坦克 #9：1.8 / +5 / +5 / 自身受 8 伤 / 站位 1~4 / 目标 敌 1、2）；总动员（政委 #9：1.5 / +10 / +10 / **全队 +10 士气**（morale_effects team+10）/ 站位 1~4 / 目标 敌 1、2）。
  2. **自我伤害固定值**：`self_damage_fixed` 殊死一搏 **6** / 舍身 **8**（glossary §3 / combat_math §8），逐字入库、可致死走死门；总动员无自我伤害。注：glossary §3"可被护盾吸收"与 data_schema §3.2"不被护盾吸收"存在文字口径差异，吸收语义属护盾生命周期（M4/O-15 叠层议题），**本卡只保证 6/8 固定值落库、不裁决吸收**。
  3. **"所有位置"兜底（self_slots="all"，恰 4 条）**：战士战吼、坦克战吼（CD 2 / 全队 +5）；军医急救（治疗 **12 HP** / CD 1 / 任意友方）；政委战场鼓舞（**无限制** / 目标 +15 / 任意友方）——4 原型各 ≥1 兜底（skill_data §6 末注），是"换位不废人"的最后保险。
  4. **多段（恰 2 条）**：双连击（军医 #5，0.55×2）、连射（政委 #8，0.5×2）→ segments 各拆两条 flat（skill_data §0"两段各 0.55/0.5"），执行语义见 T-M3-06（O-13）。
  5. **公式型"已失血%"（恰 2 条）**：致命注射（军医 #7，missing_hp base 1.0 / coefficient **0.8**）；处决令（政委 #7，base 1.0 / coefficient **0.9**）；语义 = `base + (最大HP−当前HP)/最大HP × coefficient`，满血加成 0（skill_data §3 注）；0.8/0.9 逐字，禁止换算或调整。
  6. **精神轴抽查（士气压力源清单）**：我方 36 技能 `damage_axis=mental` **恰 0 条**；敌方 7 条中 mental 恰 3 条 = 威吓箭（射手，士气 targets−4、CD 2、#165）、恐惧低语（施法者，韧性−10、CD 1）、精神震荡（施法者，AOE）——防止 13 字段抄串轴。
  7. 上述标注只为"数值/轴/标签/限制逐字入库且可被断言引用"，**不新增运行逻辑**（执行语义在 T-M3-05/06 及其它里程碑）；任一断言失败即阻断（防"顺手改数字"）。
- **完成判据（可测）**：
  - SpecialSkillTests 逐条断言（对已导入数据）：per_battle=1 恰 3 条（殊死一搏/舍身/总动员）；self_damage_fixed=6/8 恰 2 条且绑定上述技能；self_slots="all" 恰 4 条（战吼×2、急救、战场鼓舞）；missing_hp 段恰 2 条（0.8/0.9）；0.55×2/0.5×2 多段恰 2 条；我方 mental 0 条 / 敌方 mental 3 条；
  - 与 skill_data §1~§5 原文行逐条比对输出一致（可复算）；
  - 构造性反例：把殊死一搏 per_battle 改成 2 → 断言红，证明测试真实覆盖。
- **风险/开放**：O-13、O-16、O-21（威吓箭士气口径不阻塞数据）；glossary §3 vs data_schema §3.2 自我伤害×护盾文字差异 → 归 M4 护盾生命周期（相关 O-15），非本卡裁决范围

### T-M3-08 覆盖率回归测试（换位不废人）
- **上游**：[设计] doc/modules/skill_data.md §6（覆盖校验表：每角色每位置 ≥2 的判据基准）；character.md §2（每位置可用技能硬要求）；doc/README.md §2（M3 完成判据）；data_schema.md §4.2 P3（运行口径）· O-16
- **依赖**：T-M3-01/02（skills.json + units.json 导入与 P3 门禁）+ T-M3-03（ISkillUseResolver 快照判定，供场景级回归）
- **产出**：`tests/SkillCoverageTests.cs`（数据驱动：读 skills.json/units.json → 覆盖率矩阵断言）
- **要点**：
  1. **数据驱动矩阵**：对 4 个 player 原型 × 位置 1~6，计算可用技能数 = `count{s ∈ 池: s.self_slots="all" ∨ pos ∈ s.self_slots}`，期望下限取 skill_data §6 覆盖表：

     | 角色 | 位 1 | 位 2 | 位 3 | 位 4 | 位 5、6 |
     |---|---|---|---|---|---|
     | 战士 | 5 | 5 | 4 | 3 | 2（战吼、喘息） |
     | 坦克 | 7 | 6 | 2 | 2 | 2（战吼、喘息） |
     | 军医 | 6 | 6 | 4 | 4 | 2（急救、群体绷带） |
     | 政委 | 5 | 5 | 4 | 4 | 2（战场鼓舞、动员令） |

  2. **断言口径（P3 注，勿用相等）**：每格"逐行自数 ≥ 表声明数"且 **≥2**；表声明数为**判据下限**（把殊死一搏/战吼/总动员计入后自数更多为合法，O-16）；同时每角色 ≥1 个 `self_slots="all"` 兜底技能。
  3. **换位不废人场景回归**：把每个原型放到位 1~6 的任意站位（模拟交换/位移/增援后的落位），断言其技能池可用技能 ≥2（任何站位都有事可做），与 P3 共享同一计数函数（防"校验与回归两套数"）。
  4. 敌方不套用覆盖校验（敌方技能池小且固定，无换位自由——enemy 覆盖无此设计硬要求）；但可加辅助断言：敌方每原型技能 id ∈ units.json skills 列表（引用完整补强）。
  5. 每次改动 skills.json/units.json 自动重跑（CI 门禁），失败信息输出 角色/位置/期望下限/实际/缺失技能名清单。
- **完成判据（可测）**：
  - 用例全绿 = 4 角色 × 6 位置可用技能自数均 ≥ skill_data §6 表声明数且 ≥2；
  - 覆盖率矩阵输出可与人读 skill_data §6 表逐格对账（自数 ≥ 表值，表格每格成立）；
  - 构造性反例：把军医急救 self_slots 从 "all" 改为 [1] → 位 5/6 自数降为 1 → 用例红；把战士投掷短矛 self_slots 改为 [1,2] → 位 3 自数 <3 → 用例红（证明测试真实反映换位覆盖）；
  - 与 T-M3-03 场景回归同源：Resolve 快照判定结果与矩阵计数在任意（原型, 位置）组合上一致。
- **风险/开放**：O-16（表声明数 vs 自数口径，仅 ≥2 为硬校验，细账不阻塞）
