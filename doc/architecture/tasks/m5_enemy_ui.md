# M5 敌人与 UI · 任务卡（m5_enemy_ui.md）

> **编号**：ARCH-T-M5 · **类型**：task · **状态**：草案
> **上游**：[设计] doc/modules/enemy.md、doc/modules/ui_spec.md、doc/GDD.md §1.5/§1.5.2/§2.5/§3.6（数值与技能逐字另据 skill_data.md §5、enemy.md §5.2、data_schema.md 分表）
> **决策引用**：#20~#24, #41a, #43, #46, #69~#71, #78, #88, #94, #96, #106~#113, #114~#118, #124, #126, #154, #156, #157, #160, #163~#165
> **依赖**：m2_combat_core.md（T-M2：行动序列/速度浮动/结算内核与回放）、m3_skills_units.md（T-M3：技能与角色数据导入/ISkillUseResolver）、m4_morale_survival.md（T-M4：士气/虚弱/死门/状态机/IBuffLedger）
> **最近更新**：2026-09-09

---

## 0. 本里程碑目标与口径

**里程碑一句话目标**：敌方用**固定优先级 AI + 第 6 回合超时增援**驱动；BattleUi 四分区落地，**9 条必显示**齐全，玩家**不看代码就能预判位移结果**（位移预览 = 内核同源 dry-run，blueprint §5d/§10、ui_spec 必显示 #6）。

本文件内编号规则：
- 任务卡 = `T-M5-01` ~ `T-M5-09`，遵守 _conventions.md §6 模板；里程碑内建议顺序：`01 → 02 → 03`（敌人侧数据与行为），`05 → 06 → 07 → 08`（UI 侧，可与 01~04 并行），`09` 收尾验收。
- **数据文件名一律按 data_schema.md §1 的 7 个 JSON**（enemy_ai.json / tuning.json / formation.json …；blueprint.md §3 目录图已同步对齐该清单，无旧草案名冲突）。
- **数值/技能名/优先级表与 enemy.md、skill_data.md 逐字一致**；拿不准回原文核对，禁止"顺手填值"（_conventions.md §7.1）。敌方数值均为起手值（#154 / O-06），Monte Carlo 只改 JSON 不改代码。
- 开放题一律引用 open_issues.md §B 权威编号：**O-11（撤退公式）/ O-19（AI 15% 随机开关）/ O-20（增援强度与 +攻/+速数值）/ O-21（威吓箭 −4 与点名目标）/ O-28（射手 AI 规则序与贴脸口径）/ O-30（敌人意图显示可选）**，另涉及 O-31（威吓箭伤害表达式）。

**里程碑级完成判据（可测）**：
| # | 判据 | 对应卡 | 判定式 |
|---|---|---|---|
| M-A | 位移预览 = 结算结果 | 06 / 09 | 对同一内核只读快照，dry-run 与确认结算输出的 SlotChange 序列逐条相等；实机命令流重放与 headless 事件日志逐事件一致（blueprint §10 镜像测试） |
| M-B | 9 条必显示逐条可达 | 06 | 每条在运行界面可被看到且数值与内核投影同源（ui_spec §2 表 9 行全过） |
| M-C | 撤退按钮显示成功率数字 | 04 | 按钮数字 == O-11 占位公式（tuning 配置）在当回合速度/存活快照下的输出；击杀最快敌人后数字实时变化 |
| M-D | 增援在第 6 回合触发 | 03 | 构造"打到第 6 回合未分胜负"用例：回合开始事件流出现 reinforcement 事件；5 回合内结束则无此事件 |

---

### T-M5-01 敌方编成数据落地（1 套 4 敌 + 7 敌技 + enemy_ai 配置源）

- **上游**：[设计] doc/modules/enemy.md §1/§3/§5.1/§5.2/§5.3、doc/GDD.md §1.5；data_schema.md §3.1/§3.2/§3.5/§3.6 · 决策：#106, #107, #110, #113, #124, #154, #165
- **依赖**：T-M3（units/skills 13 字段导入与校验管线，P1/P2/P7）+ m1_formation.md（formation.json initial_roster 编成骨架）
- **产出**：
  - `res://data/units.json`（敌方 3 原型行：`melee_soldier`/`ranged_archer`/`caster`）
  - `res://data/skills.json`（敌方 7 技能记录，13 字段逐字 skill_data.md §5）
  - `res://data/formation.json`（`initial_roster.enemy`：敌方 1~4 编成）
  - `res://data/enemy_ai.json`（新增：3 原型优先级配置源，结构见 data_schema §3.5）
  - `res://scripts/data/`（敌方行的加载与校验扩展）、`res://resources/` 派生 *.tres
- **要点**：
  1. **编成 1 套（enemy.md §5.1 / data_schema §3.6）**：敌方仅 4 战斗位、无支援位（enemy §1 / #12）：enemy 1=近战小兵 A、2=近战小兵 B（前排贴脸高压）、3=远程射手（后排输出）、4=施法者（后排控制 + AOE）。**不得扩到 4 位以上**（GDD §1.5）。
  2. **属性起手值（enemy §5.2 表，逐字段入 units.json，敌方 `deaths_door_resist` 必须为 `null`——敌方无死门/士气/虚弱/崩溃系统，P7 校验）**：
     | 原型（id） | HP | 攻击 | 物防 | 速度 | 闪避 | 暴击 | 韧性 | 眩晕抗 | 流血抗 | 属性减益抗 | 位移抗 | 死门抗 |
     |---|---|---|---|---|---|---|---|---|---|---|---|---|
     | 近战小兵 `melee_soldier` | 50 | 12 | 8 | 8 | 10 | 5 | 55 | 30 | 30 | 25 | 55 | null |
     | 远程射手 `ranged_archer` | 38 | 13 | 4 | 12 | 15 | 8 | 60 | 25 | 30 | 25 | 25 | null |
     | 施法者 `caster` | 34 | 12 | 3 | 9 | 10 | 5 | 70 | 40 | 30 | 30 | 30 | null |
  3. **7 个敌方技能（skill_data.md §5，13 字段逐字；"我 X"= 我方位置；数据以 skill_data 为准）**：
     | 原型 | 技能 id / 名 | self_slots | target（slots, side=player） | damage | effects | displacement | use_limit | axis / tags | 备注 |
     |---|---|---|---|---|---|---|---|---|---|
     | 近战小兵 | `melee_heavy_slash` 重劈 | [1,2] | [1,2] | 1.0 | — | — | none | 近战·物理 / output | |
     | 近战小兵 | `melee_charge` 突进 | [3,4] | self | — | next_attack_boost +20%（2 回合内，buff_defs） | self_forward 1 | cooldown 2 | —·无 / displacement | #88 位移只能由技能触发 |
     | 远程射手 | `ranged_precise_shot` 精准射击 | [2,3,4] | [1,2,3,4] | 0.9 | — | — | none | 远程·物理 / output | |
     | 远程射手 | `ranged_intimidating_shot` 威吓箭 | [2,3,4] | [1,2] | 0.5 | morale_effects targets −4（#165） | — | cooldown 2 | 远程·精神 / output+debuff | 表达式张力 O-21/O-31（数据按 0.5 倍率 + 攻击 13） |
     | 远程射手 | `ranged_retreat` 后撤 | [1,2] | self | — | — | self_backward 1 | cooldown 2 | —·无 / displacement | |
     | 施法者 | `caster_fear_whisper` 恐惧低语 | [2,3,4] | [1] | 0.8 | stat_mod resilience −10（2 回合） | — | cooldown 1 | 远程·精神 / output+debuff | 固定目标"我 1 位" vs AI"点名最低韧性" → O-21 |
     | 施法者 | `caster_mental_shock` 精神震荡 | [2,3,4] | [1,2] | 0.7 | — | — | cooldown 2 | 远程·精神 / output+aoe | 精神 AOE −5/目标由 morale_events 派生 |
     - 士气衔接（引擎规则，不入技能表）：精神伤害按 morale_events 扣士气（单 −8 / 精神暴击 −12 / AOE 精神 −5）；威吓箭是唯一显式 `morale_effects targets −4` 且为精神伤害的技能，其与 −8 的关系见 **O-21**。
  4. **enemy_ai.json 为新增配置源**：按 data_schema §3.5 结构落 3 原型 rules（本卡只落结构骨架与"技能 id ∈ 该原型池"的引用完整性，规则语义归 T-M5-02）；顶层 `random: { enabled: false, fallback_probability: 0.15 }` 与 `#96` 标记权重预留位一并建好（见 T-M5-02）。
  5. **范围纪律**：大型敌人占多格、死亡补位型增援、3~4 套编成均不做（enemy §3 / GDD §15.1/#53/#89）；障碍只走关卡预置（enemy §5.6 / #39）；敌人无士气/虚弱/死门/崩溃（enemy §1/#87）。
- **完成判据（可测）**：
  - units.json 敌方 3 行与 enemy §5.2 逐字段一致（含速度 8/12/9、位移抗 55/25/30、HP 50/38/34），校验断言 `deaths_door_resist == null`（P7）通过。
  - skills.json 含敌方 7 条且 13 字段与 skill_data §5 逐字一致；导入期技能总数断言 = 43（36 我方 + 7 敌方，data_schema §1 备注）。
  - formation.json `initial_roster.enemy` = `[1:melee_soldier, 2:melee_soldier, 3:ranged_archer, 4:caster]`。
  - enemy_ai.json 每原型 rules[].skill_id 全部命中该原型技能池（P1/P2/P7 启动无报错）。
- **风险/开放**：O-06（敌方 HP 起手值可推翻，入表按 #154）；O-31（威吓箭 12×0.5 vs 射手攻击 13，疑笔误，数据按 skill_data 0.5 倍率落库不阻塞）；blueprint §3 数据文件名现已与 data_schema §1 对齐（见 §0）。

---

### T-M5-02 EnemyAi 固定优先级决策（3 原型优先级表）

- **上游**：[设计] doc/modules/enemy.md §2/§5.4、doc/GDD.md §1.5；data_schema.md §3.5；skill.md §5（可用性判定）· 决策：#86, #88, #96, #112, #113, #165
- **依赖**：T-M5-01（enemy_ai.json 配置源）+ m3（ISkillUseResolver 可用性顺序：②自身站位 ③目标部分非空 ④CD/每场次数，敌方无"战前携带"概念）+ m4（IBuffLedger：嘲讽 taunt / 眩晕 / 突进增伤 next_attack_boost）
- **产出**：
  - `res://data/enemy_ai.json`（3 原型 rules + random 开关 default off + #96 标记权重预留位）
  - `res://scripts/gameplay/sim/enemy/EnemyAi.cs`（实现 blueprint §9.7 `IEnemyAi.Choose(EnemyId, BattleSnapshot, IRngProvider)`）
  - `res://scripts/gameplay/sim/director/`（敌方行动回合：调用 EnemyAi 产出 `SkillChoice` → 翻译为 BattleCommand 注入 BattleDirector）
  - `res://tests/EnemyAiTests.cs`（原型 × 场景决策断言，见完成判据）
- **要点**：
  1. **固定优先级是配置不是逻辑（enemy §2）**：每原型一张表，rules 按数组顺序求值，选**第一个"条件满足且技能可用"**的规则；无条件规则 = 兜底默认项（data_schema §3.5 解析语义）。
  2. **3 原型优先级表（逐字对应 enemy.md §5.4 / data_schema §3.5，下表 = enemy_ai.json 内容）**：
     | 原型 | 序 | when（条件谓词） | skill_id | 对应策划原文 |
     |---|---|---|---|---|
     | melee_soldier 近战小兵 | 1 | `self_slot_in: [3,4]` | `melee_charge` 突进 | ① 若不在前排（被推到 3/4）→ 突进归位 |
     | melee_soldier | 2 | `{}`（默认） | `melee_heavy_slash` 重劈 | ② 否则重劈 |
     | ranged_archer 远程射手 | 1 | `self_slot_in: [1,2]` | `ranged_retreat` 后撤 | ① 若被近战贴脸 → 后撤拉开 |
     | ranged_archer | 2 | `{}`（默认） | `ranged_precise_shot` 精准射击 | ② 否则精准射击 |
     | ranged_archer | 3 | `player_morale_all_at_least: 40` | `ranged_intimidating_shot` 威吓箭 | ③ 我方士气整体偏高（无人 <40）且威吓箭 CD 就绪 → 压士气（#165） |
     | caster 施法者 | 1 | `target_slots_occupied_min: {side:player, slots:[1,2], min:2}` | `caster_mental_shock` 精神震荡 | ① 优先精神震荡打多人（AOE 收益高） |
     | caster | 2 | `{}`（默认） | `caster_fear_whisper` 恐惧低语 | ② 否则恐惧低语点名最低韧性单位 |
  3. **条件谓词词汇表（data_schema §3.5，支持 0~n 个、全部满足才命中）**：`self_slot_in`（施法者自身在这些位，例：突进 [3,4]、后撤 [1,2]）；`target_slots_occupied_min`（目标阵营指定位置"有人"数 ≥ min）；`player_morale_all_at_least`（我方所有人士气 ≥ 值 = 无人低于该值，威吓箭条件取 40）；空对象 = 无条件默认项。引擎层先过滤"技能可用"（self_slots、CD、目标全空），AI 表只决定"同类可选时优先谁"。
  4. **贴脸口径（O-28）**：射手规则①"被近战贴脸"实现谓词 = `self_slot_in: [1,2]`（射手自身被推/靠齐至敌方前排 1/2，与"后撤"技能 self_slots [1,2] 站位要求对齐，data_schema §3.5 直译）；策划口径"贴脸=敌方占我 1/2 位"尚待拍板（O-28），口径变更只改 when、不改引擎。
  5. **15% 次优先随机 = 数据开关 default off（O-19 / #112）**：`enemy_ai.json.random.enabled=false`（切片纯确定性）；开启时语义 = 15% 概率改用次优先级技能（enemy §5.4），抽取必须走注入 IRngProvider 的固定调用点并写入 CombatLog（blueprint §8.2）。
  6. **#96 标记权重预留位**：enemy_ai.json 每条规则预留（如 `"mark_weight": <int, 默认缺省/0>`），切片无"标记"技能、不参与决策，仅保字段（#96 需在 AI 配置留标记权重）。
  7. **目标选择与 taunt（嘲讽）**：技能只选位置不选目标（glossary §2）；敌技 target.slots 范围内按确定性顺序取非空位（槽位编号升序、不含 RNG）；若该敌身上有 `taunt` buff（data_schema buff_defs `taunt`，2 回合）且嘲讽者所在位在其技能范围内 → 该位优先（"改敌方 AI 优先攻击施法者"）。位移/自身类技能无目标选择。注：施法者"点名最低韧性"与固定 target "我 1 位"冲突 → **O-21**，在策划放开目标位前按 target 直译。
  8. **边界**：全部技能不可用（CD/站位不符/目标全空）→ 本回合不行动（data_schema §3.5 注）；施法者被推至敌方 1 号后全部技能站位不符（self_slots 2~4）→ 空过，是位置驱动预期张力，玩家可预判；敌人死亡不补位（死亡补位型增援不做，GDD §1.5.1/#218 延后）。
- **完成判据（可测）**：
  - 场景断言（EnemyAiTests，random.enabled=false 下同快照两次 Choose 结果相同）：近战小兵在敌方 3/4 且突进可用 → `melee_charge`；在 1/2 → `melee_heavy_slash`。
  - 远程射手在敌方 1/2 且后撤可用 → `ranged_retreat`；在 3/4 → 当前表序下 `ranged_precise_shot`（威吓箭因规则②无条件默认而不可达——**O-28 已记录，不静默改序**；策划裁定新序后补用例：全队士气 ≥40 且威吓箭 CD 就绪（CD 2，释放当回合与次回合不可用）→ `ranged_intimidating_shot`）。
  - 施法者在 2~4 且我方 1/2 位"有人"数 ≥2 → `caster_mental_shock`；否则 → `caster_fear_whisper`；被推至敌方 1 → 本回合不行动。
  - 技能可用性过滤走内核 ISkillUseResolver（skill.md §5），AI 用例含"CD 中不选突进/威吓箭"。
  - 开关测试：`random.enabled=true` 时开启 15% 分支的抽取带 DrawCount 且入 CombatLog；default off 下 100 次 Choose 无一走次优先。
- **风险/开放**：O-19（15% 开关 default off）、O-28（射手规则序/贴脸口径，阻塞威吓箭③可达性——实现前不另设规则）、O-21（施法者点名口径，固定 target 时不可达）。另：敌方决策与 M6 Headless 基线 AI 必须**同源**（blueprint §9.10），改动 enemy_ai.json 即同时影响实机与模拟。

---

### T-M5-03 第 6 回合超时增援

- **上游**：[设计] doc/GDD.md §1.5.2、doc/modules/enemy.md §4 · 决策：#65, #69, #70, #71（触发第 6 回合 / 强度随机只填空位 / 满编改上增益不扩编）
- **依赖**：T-M5-01（3 原型起手值模板）+ m2（BattleDirector 回合状态机/事件流）+ m4（IBuffLedger 施加 stat_mod）+ T-M5-02（新单位入阵后走同一 EnemyAi）
- **产出**：
  - `res://data/tuning.json`（`overtime_reinforcement`：`trigger_round:6`、`fill_or_buff:"fill_empty_then_buff_present"`、空位分支与 +攻/+速占位常量，见 data_schema §3.7）
  - `res://scripts/gameplay/sim/director/`（回合开始钩子：第 6 回合起"未分胜负"检测与增援结算）
  - `res://scripts/gameplay/sim/enemy/`（按原型起手值生成新敌方实例的工厂，无随机分布）
  - `res://tests/ReinforcementTests.cs`（触发/上限/满编分支用例）
- **要点**：
  1. **触发**：战斗**无硬性回合上限**；第 **6** 回合起、每个回合开始判定一次"仍未分胜负"（胜利 = 敌人全部死亡；失败 = 我方全灭，GDD §1.5.2/§3.5），未分胜负即结算本轮增援。触发回合常量落 tuning `overtime_reinforcement.trigger_round = 6`（#69，可调）。
  2. **有空位分支**：向敌方空位填入新单位，**只填空位、不打破 4 位上限、不扩编**（#70）。切片生成 = **从 3 原型按默认起手值填**（data_schema §3.1 起手值，顺序确定：敌方槽位编号升序取第一个空位）；**不做"强度随机分布"**——O-20 已裁定切片不走随机分布；若日后启用随机必须走 IRngProvider 固定调用点并回填 blueprint §8.2。
  3. **无空位分支**：敌方满编 → **不填人**，改为给【在场敌人】上增益（**+攻 / +速，具体数值缺失 → O-20**，占位落 tuning `overtime_reinforcement`，策划拍板后回填；数值就绪前以 stat_mod 施加、数值键可配，禁止拍死在内核代码）。
  4. **"有空位才填、无空位才上增益"是同一轮判定的两个分支**（GDD §1.5.2 用户补漏：#70 有漏洞 → #71 修正），判定顺序 = 先查空位、无空位走增益；两种结果都必须在 CombatLog 写增援事件（填了谁 / 增益挂在谁身上），供 UI 反馈与 M6 KPI。
  5. **与"死亡补位型增援"严格区分**：敌人死亡后立刻补新单位 = 切片**不做**（GDD §1.5.1"增援/召唤延后到切片之后"、GDD §15.1/§1.5.2、blueprint §1.2）；本卡只做超时触发（enemy §3"死亡补位 ❌、超时增援 ✅"）。
  6. **波次间隔口径**：GDD §1.5.2 另有"之后每隔 M 回合再来一波（M 待数值阶段定）"；切片按"第 6 回合起每回合检测"实现（enemy.md §4"第 6 回合起…时"直译），间隔 M 作为 tuning 可配置常量预留、回填前不阻塞，避免擅自替策划定 M（见 §0 口径）。
  7. **增援的敌人单位**：复用敌方无士气/虚弱/死门规则（enemy §1）；技能池同原型、AI 行为同 T-M5-02，无需新逻辑。
- **完成判据（可测）**：
  - 用例 1（第 6 回合触发）：同 seed 构造"打满 5 回合仍未分胜负"，回合 6 开始事件流出现一次增援事件且敌方至少一个空位被填；5 回合内结束战斗的对照组无增援事件。
  - 用例 2（4 位上限）：连续多回合增援后敌方数量恒 ≤4，且每轮只发生在空位或满编增益分支。
  - 用例 3（无空位分支）：满编场景（一敌未杀）进第 6 回合 → 无新单位，事件流出现"对在场 N 个敌人施加 +攻/+速 stat_mod"记录，增益数值读 tuning 占位常量。
  - 用例 4（确定性）：同 seed 同命令流两遍，增援事件（时机/填入原型与槽位）逐条一致；随机分布未启用时无 RNG 抽取新增。
- **风险/开放**：O-20（强度分布与 +攻/+速数值缺失，占位实现）；GDD §1.5.2 波次间隔 M 待数值阶段（tuning 预留键，未挂 O-nn，需统一时由架构师登记）；第 6 回合是否偏早属平衡问题走 verification 迭代，不在本卡拍板。

---

### T-M5-04 撤退结算与成功率显示

- **上游**：[设计] doc/GDD.md §3.6、doc/modules/ui_spec.md §2（必显 #2）/§4 · 决策：#36, #43, #46, #118, #126
- **依赖**：m2（BattleDirector 命令流/行动序列实际速度 #163）+ m4（撤退士气事件 retreat_success/retreat_fail 进 morale_events）+ T-M5-05（A 区按钮装配）
- **产出**：
  - `res://scripts/gameplay/sim/director/`（RetreatCommand：团队统一行动判定与结算，成功/失败事件写出）
  - `res://data/tuning.json`（`retreat`：success_morale/fail_morale + **O-11 占位公式常量与默认示例 f**）
  - `res://scripts/ui/RetreatButton.cs`（成功率数字 + 代价显示 + 二次确认 + 当回合禁点状态）
  - `res://tests/RetreatTests.cs`
- **要点**：
  1. **撤退 = 团队统一指令，全员一起撤**（#118/GDD §3.6），非单人脱离；由玩家方 A 区常驻按钮发起（ui_spec §1/§4）。
  2. **判定 = 混合式**：基础成功率 = f(我方存活单位速度 vs 敌方存活单位速度) + 随机 ±10% 内（#46，GDD §3.6）。**具体公式缺失 → O-11**：切片先落"**可配置公式 + 默认示例**"占位于 tuning（如默认示例 f = 我方平均实际速度 /（我方+敌方）平均实际速度，仅供数字管线自洽，**不代表策划拍板**）；正式公式【策划拍板】后回填 tuning，代码不写死。随机取数走 IRngProvider 固定调用点（blueprint §8.2 撤退层）。
  3. **士气代价（起手值，#126/#43）**：成功 → 全队 **−10**（morale_events `retreat_success`）；失败 → 全队 **−5**（`retreat_fail`）；成功则退出战斗（胜/负/撤退三种出口之一），失败则战斗继续。
  4. **失败限制（#118）**：撤退失败**当回合不可再发起**、下回合可再试；按钮状态由 BattleDirector 撤退状态机给投影（失败后置 disabled + tooltip"本回合不可再撤退"），UI 不做本地逻辑。
  5. **成功率数字实时刷新（ui_spec §9 建议实时）**：每回合速度重掷（#163）、任一单位死亡/离场后重算并刷新按钮数字——"先杀掉最快的敌人再撤"策略成立（O-11 公式对速度快照敏感，读同一只读投影）。
  6. **按钮交互（ui_spec §4）**：点开显示成功率数字 + 成功/失败士气代价（−10 / −5）→ 二次确认 → 发 RetreatCommand；不可撤销（确认后即结算）。
- **完成判据（可测）**：
  - 按钮数字 == tuning 占位公式在当回合实际速度/存活单位快照下的输出（单元测试直接调同一公式函数比对）；杀敌后数字单调变化（移走最高速敌方则提高，可断言方向）。
  - 成功路径：日志恰一次 `retreat_success` 且全队 −10（士气快照差），战斗以撤退出口结束。
  - 失败路径：全队 −5、当回合再点按钮被拒（投影 disabled / 命令被拒）、下一回合可再试；单回合最多一次撤退判定。
  - 撤退判定随机与 ±10% 范围断言：100 次模拟结果偏差落 [−10%, +10%] 且每次抽取带 DrawCount 入日志。
- **风险/开放**：O-11（公式缺失，本卡占位实现，阻塞 M6 撤退 KPI 的确定性口径前不视为完成——占位公式输出即"当前可测口径"）；ui_spec §9"撤退成功率是否实时"建议实时已采纳（显示层口径，非数值决策）。

---

### T-M5-05 BattleUi 四分区装配

- **上游**：[设计] doc/modules/ui_spec.md §1/§8、blueprint.md §5d（SceneTree）· 决策：#160
- **依赖**：T-M5-01（敌方单位运行时视图数据）+ m2/m3/m4（行动序列/技能可用性/士气与状态投影）+ T-M5-04（撤退按钮数据源）
- **产出**：
  - `res://scenes/battle/Battle.tscn`（主战斗场景，节点树 = blueprint §5d）+ `res://scenes/battle/prefabs/`（SlotView/UnitView/飘字/死门判定弹窗等预制）
  - `res://scripts/ui/BattleUi.cs`、`TopBar.cs`、`ActionOrderBar.cs`、`RetreatButton.cs`、`SkillBar.cs`、`CharacterCard.cs`、`BoardOverlay.cs`、`IBattleView.cs`
  - `res://scripts/gameplay/scene/BattleRoot.cs`、`FormationView.cs`、`SlotView.cs`/`UnitView.cs`、`CameraRig.cs`（表现层装配与输入翻译）
- **要点**：
  1. **四分区（ui_spec §1）**：A 顶栏 = 回合数 + 行动序列条 + 撤退按钮；B 战场 = 我方 6 槽 + 敌方 4 槽渲染（含槽位三态：角色/空/障碍，GDD §1.1）且**位置编号必须可见**；C 角色卡 = 当前行动单位（高亮，可悬停切换任意单位）；D 技能栏 = 当前单位 5 个携带技能（9 选 5 战前已定，skill.md §1）。
  2. **装配纪律**：BattleUI(CanvasLayer) 只做"读 IBattleView 投影 + 发命令门面"，一切状态真值在确定性内核（blueprint §4 B5/B4/B3）；BattleRoot 组合根持有 BattleDirector 并注入种子，只装配/转发/订阅（blueprint §5d/§6.1）。
  3. **A 区行动序列条**：本回合完整序列 + 下回合预览；死亡/离场单位实时划掉或移除（ui_spec §7 边界）；眩晕跳过的显示口径由 ITurnSequencer 投影给出（GDD §2.5/#163）。敌人意图显示（可选）见 T-M5-06 注（O-30）。
  4. **B 区**：10 槽全部常驻可读；支援位 5/6 平时不在镜头中心、被使用/被攻击时镜头短暂移过去（GDD §1.1/ui_spec §7）；本卡至少保证"信息不落在屏幕外"——若镜头运镜延后（ui_spec §8 可延后项），受击反馈（飘字/状态）必须仍可见。
  5. **D 区技能栏 + 灰显**：技能可用性由 ISkillUseResolver 投影（AvailabilityReason：NotCarried/BadStance/NoTarget/OnCooldown/UsesExhausted），灰显技能不可点、tooltip 给原因（站位不符/范围内无人/CD 中/次数用尽，ui_spec §4）；技能栏旁放**换位/增援入口**（GDD §1.4.1/#41a：战斗位角色发起，非技能，ui_spec §4）——本卡只装配入口与命令转发，执行链路依赖阵型命令流（m1/m3 已建的命令通道）。
  6. **可延后项按 ui_spec §8 封锁**：buff 详情面板、悬停属性面板、镜头运镜美术、音效、动画精修不做/占位；MVP 最小集逐项可勾选（位置编号+站位 / 行动序列条 / 撤退按钮+数字 / 技能栏+灰显 tooltip / 目标范围高亮+位移预览 / HP-士气-虚弱-死亡 / 物理 vs 精神视觉区分 / 死门演出 / buff 图标 ≤4+折叠）。
- **完成判据（可测）**：
  - 场景启动后节点树与 blueprint §5d 所列一致；四分区齐备：A 显示回合数与行动序列（本回合+下回合）与撤退按钮（成功率数字），B 渲染 10 槽且编号可见（我方 1~6、敌方 1~4），C 显示当前行动单位卡并高亮，D 列出该单位 5 个携带技能。
  - 初始高亮/灰显状态与内核可用性判定一致（抽 3 个技能断言 Reason 相同）。
  - ui_spec §8 MVP 勾选清单全过；可延后项不出现或仅占位。
- **风险/开放**：O-30（敌人意图/伤害预估为可选项，非 9 条必显，见 T-M5-06）；支援位入镜运镜可延后时须有"屏幕外不掉血"兜底（本卡要点 4）。

---

### T-M5-06 9 条必显示落地

- **上游**：[设计] doc/modules/ui_spec.md §2/§3/§4、data_schema.md §2.4（必显 → 数据来源）· 决策：#160, #163, #156, #157
- **依赖**：T-M5-05（四分区装配）、T-M5-04（撤退数字）、T-M5-02（敌人意图可选数据源）
- **产出**：`res://scripts/ui/` 各分区控件内实现（TopBar/ActionOrderBar/RetreatButton/SkillBar/CharacterCard/BoardOverlay 等）；位移预览复用 `scripts/gameplay/sim` 内核 dry-run 只读接口（IFormation.TrySwapChain/CanCloseUpIn，blueprint §9.1）；`res://tests/` 逐条断言
- **要点（9 条必显示逐条落地：实现要点 + 出处 + 观测判据）**：

  | # | 必显信息 | 数据源与实现要点 | 出处 | 观测判据（可测） |
  |---|---|---|---|---|
  | 1 | **行动序列**（本回合 + 下回合预览） | ITurnSequencer 投影：实际速度 = 基础×(1+0~10%) 每回合重掷（#163）；同速编号小者先动（#80）；A 区条状显示。**下回合预览不得新增 RNG 抽取**（回放 DrawCount 一致性铁律 blueprint §8.1）——以当前速度快照外推并标注"预演，回合开始重掷后可能变动"；实现口径若需"同速档内随机"待实测（O-02） | GDD §2.5 / ui_spec 必显 #1 | 界面同时见本回合全序 + 下回合预览；死亡后序列实时更新、离场单位划掉（ui_spec §7） |
  | 2 | **撤退成功率具体数字** | tuning 占位公式（O-11）+ 当回合实际速度快照，A 区按钮常驻显示（如"撤退 68%"）；实时刷新（T-M5-04 要点 5） | GDD §3.6 / ui_spec 必显 #2 | 数字 == 占位公式输出；杀敌后刷新 |
  | 3 | **技能灰显 + 原因** | ISkillUseResolver.Availability（skill.md §5）：灰显禁点 + tooltip 原因（BadStance/NoTarget/OnCooldown/UsesExhausted/NotCarried） | ui_spec 必显 #3 / GDD §1.2 | 每种 Reason 各造 1 例可见 tooltip 文案正确 |
  | 4 | **命中率** | 命中 = 100 − 目标闪避 + 技能 hit_mod，钳制 [55,100]（combat_math §1 / data_schema §2.2）；悬停/选中技能时显示（AOE 逐目标同值） | ui_spec 必显 #4 | 悬停技能显示数字与内核公式复算一致 |
  | 5 | **目标范围高亮** | skills.json `target`（slots 语义）+ 位置编号映射；范围内**部分非空只高亮非空位**、全空灰显（GDD §4） | ui_spec 必显 #5 | 选中技能后高亮位置 == target.slots ∩ 非空位 |
  | 6 | **位移预览（整条交换链）** | **内核同源 dry-run**：UI 请求"对当前只读快照跑同内核交换链计算"，得到完整 SlotChange 序列（含中间单位各退一格），BoardOverlay 把整条链画成逐格预览；**禁止只画"目标被推 1 格"**（ui_spec §6 警告） | ui_spec 必显 #6 / blueprint §5d/§9.1 | 预览 SlotChange 序列 == 确认结算后的实际位移序列（dry-run 与结算同函数） |
  | 7 | **士气条（0~100 + 数值）** | 运行时士气（钳制 [0,100]、起手 50，data_schema §2.2）；横向条 + 数字；≥85 金 / ≤15 红预警（ui_spec §5） | ui_spec 必显 #7 / morale §1 | 条与数值随 MoraleChanged 事件同步，变色边界正确 |
  | 8 | **HP / 护盾次数 / buff 图标** | units hp + 护盾按次数（#156，盾形图标 + **剩余次数数字**，非吸收量）+ buff 图标（≤4 个，超出折叠为 +N，点开看全部，ui_spec §3/§5）；图标优先级 = 死门/虚弱 > 折磨 > 美德 > 眩晕/捆缚 > 流血 > 属性增减益 > 护盾 | ui_spec 必显 #8 / buff.md §8.1 | 铁壁（2 次）被物理攻击挡 1 次后次数显示 1；6 个 buff 时第 5 个起折叠 |
  | 9 | **位置编号** | formation.json numeration（我方 1~6、敌方 1~4，中央向两侧递增，GDD §1.1）；每槽编号**常驻可见**（信息层级 1 级） | ui_spec 必显 #9 / data_schema §3.6 | B 区每槽显示编号；技能描述可对照编号读懂 |

  > **注（O-30 可选项，非 9 条必显）**：敌人意图（下一步要放什么）与伤害预估区间，按 ui_spec §9 建议"做敌人意图"——A 区行动序列条或 B 区敌方格显示 EnemyAi 对下一行动的确定性结论（固定优先级可预判）；不阻塞 M5 主判据，见 O-30。
- **完成判据（可测）**：
  - 上表 9 行观测判据逐行可复现（自动化：投影字段断言；人工：每行一次走查记录）。
  - **位移预览一致性专项**：选突进/推类技能 → dry-run 输出与确认结算后 FormationBoard 实际变化逐槽一致（含撞边界 = 位移失败不动 #78、撞障碍交换 #22、交换链中间单位各退一格）。
  - 行动序列"本回合 + 下回合"同屏可见（M-A）。
- **风险/开放**：O-30（敌人意图/伤害预估可选项）；"下回合预览"与 #163 每回合重掷的展示口径为实现期裁定（不新增 RNG 调用，见要点 1）。

---

### T-M5-07 操作流与结算反馈

- **上游**：[设计] doc/modules/ui_spec.md §4/§6、blueprint.md §5b/§6（事件流驱动反馈）· 决策：#157（物理 vs 精神视觉可分）、#117（位移不触发死门）
- **依赖**：T-M5-05（四分区）+ T-M5-06（预览/高亮/命中率）+ m2（结算事件批：伤害/位移/士气/状态/死门事件类型）
- **产出**：
  - `res://scripts/gameplay/scene/EffectsPlayer.cs`（飘字/逐格滑动/倒地/死门判定演出，tween 仅表现层）
  - `res://scripts/ui/SkillBar.cs`、`BoardOverlay.cs`（②~④ 交互态）、`CharacterCard.cs`（状态变化）
  - `res://tests/`（事件 → 反馈元素映射断言）
- **要点（操作流 ①~⑥，ui_spec §4）**：
  1. ① 轮到我方单位 → 该单位高亮 + 镜头轻微推近（镜头以固定全景为默认，ui_spec §9 建议）。
  2. ② 点技能：可用 → ③；灰显 → 禁点 + tooltip 原因（站位不符/范围内无人/CD 中/次数用尽）。
  3. ③ 显示目标范围高亮 + 位移预览（整条交换链）+ 命中率（T-M5-06 #4/#5/#6）。**可取消重选不消耗行动**（④ 确认前任意取消/换技能，不发任何 BattleCommand）。
  4. ④ 确认 → 发 UseSkill BattleCommand → BattleDirector 复核可用性并结算（skill.md §5 / blueprint §5b）；**确认后不可撤销**。
  5. ⑤ 结算反馈按事件批渲染，**固定次序**：伤害飘字 → 位移动画 → 士气飘字 → 状态变化（虚弱/死门/死亡）（ui_spec §4）；内核内部"先伤害→再士气→再虚弱/死门"次序（combat_math §5.2）由 m2 保证，本卡只消费 BattleEventStream，**不得反向订阅或篡改**（blueprint §6.1/§6.3）。
  6. ⑥ **死门判定必须给一次明确的判定演出**（全作最紧张一刻，不能悄悄结算，ui_spec §4/§5/blueprint §5b 时序末端）：结算时弹出大字结果（如"死门 62% → 扛住了"）。
  7. **物理 vs 精神必须视觉可分（#157/ui_spec §6 ⚠️）**：精神伤害用区别于物理的颜色（如紫/蓝），因只有精神降士气——两者不可同视觉；飘字规范：伤害飘字（暴击更大字号 + 不同颜色 + 全队 +5 可见提示）、士气飘字上浮绿/下沉红、位移沿交换链**逐格滑动**（不瞬移）、死亡倒地 + 立即触发向中靠齐补位动画（formation §6）、士气满 100 有明显"士气高涨"演出、士气归 0 崩溃判定演出并显示折磨名（ui_spec §6）。
  8. **边界与异常（ui_spec §7）**：支援位被攻击不得"屏幕外掉血"（见 T-M5-05 兜底）；位移撞边界 → "撞墙"抖动 + 「位移失败」提示；战斗位全空 → 失败结算面板。
- **完成判据（可测）**：
  - 手工走查 ①~⑥ 全链一次（选可用技能 → 预览 → 确认 → 五类反馈依次出现）；③ 阶段取消 3 次后再确认，BattleDirector 收到的命令数 = 1（不耗行动断言）。
  - 死门触发用例：虚弱单位被精神攻击 → 大字判定演出事件出现（日志含 deaths_door 判定数值，如抗性 − 折磨 10%）。
  - 精神 vs 物理视觉断言：`DamageAxis.Mental` 与 `Physical` 事件映射到不同反馈样式（静态检查/单测映射表）。
  - 多目标死亡靠齐：逐格演出不串场（事件批顺序驱动，每死亡一次靠齐演出一次）。
- **风险/开放**：O-12（技能内部步骤次序的 AOE/附加效果相对位置由 m2/m3 已定，本卡不重定，只按事件批消费）；物理/精神配色为视觉口径，具体色值不属切片规格。

---

### T-M5-08 状态显示规格

- **上游**：[设计] doc/modules/ui_spec.md §3/§5、doc/modules/morale.md §5.1（失控标注缓解建议 2）· 决策：#123, #136, #156, #157, #164
- **依赖**：T-M5-05/T-M5-06（C 卡与单位视图装配）+ m4（状态机事件：虚弱/崩溃余烬/折磨/美德/护盾/守护/嘲讽投影）
- **产出**：`res://scripts/ui/CharacterCard.cs`、`UnitView.cs`、`BoardOverlay.cs` 内状态渲染；`res://scenes/battle/prefabs/`（角标/连线/箭头/弹窗预制）；视觉走查用例表
- **要点（逐状态，ui_spec §5 表）**：
  | 状态 | 显示方式（ui_spec §5 逐字） | 备注 |
  |---|---|---|
  | 士气 | 横向条 + 数字；**接近 100 或 0 变色预警：≥85 金色 / ≤15 红色** | 见 T-M5-06 #7 |
  | 虚弱 | 单位半透明 + 灰化 + 「虚弱」角标；HP 显示为 `1` 并加**删除线**示意"锁 1"（GDD §3.3/#37） | 虚弱不可驱散（buff.md §5.1），无解除图标 |
  | 护盾 | 单位外圈盾形图标 + **剩余次数数字**（#156，不是吸收量） | buff 优先级：护盾最低 |
  | 守护 | 保护者与被守护者之间**画一条连线**（位置系统可视化——"推开就断开"看得见，#136 被守护者 +3） | 连线随交换链/位移重算 |
  | 嘲讽 | 敌人头顶「嘲讽」标记 + **指向坦克的箭头**（buff_defs taunt，2 回合） | 与 AI 目标选择联动（T-M5-02 要点 7） |
  | 折磨 / 美德 | 不同**底色角标**；**美德要有明显正反馈**（光效/音效，切片可用色块/脉冲占位） | 折磨 = 恐惧/自私/失控（角标细分）；崩溃余烬期不再判定（morale §4.0） |
  | 失控 | ⚠️ **必须显著标注**（morale.md §5.1 缓解建议 2）：否则玩家不知道他会乱打队友 | 支援位失控 → 捆缚（#48，切片实际不触发） |
  | 死门判定 | 结算时弹出**大字判定结果**（如"死门 62% → 扛住了"），不能只飘小数字 | 抗性 60~75% + 折磨 −10%（#123） |
  1. **buff 图标上限 4 个**（信息层级 2 级常驻小图标），超出折叠为「+N」、点开看全部（ui_spec §3，对应 buff.md §8.1）；图标优先级：死门/虚弱 > 折磨 > 美德 > 眩晕/捆缚 > 流血 > 属性增减益 > 护盾。
  2. **信息层级**：1 级（永远可见）= 位置编号 · 存活 · HP · 士气；2 级 = buff/debuff 小图标 · 护盾剩余次数 · 虚弱标记；3 级 = 悬停精确数值/命中率/伤害预估/目标范围/位移预览；4 级 = 结算飘字与死门大字（ui_spec §3）。
  3. 死门/崩溃等"结果"均为**事件流驱动的一次性演出**（来自内核事件，UI 只负责表现映射），状态角标为事件后的常驻投影；二者数据源同一，不得各自维护状态副本（blueprint §6.2：收到事件批 → 重算只读投影 → 分区重绘）。
- **完成判据（可测）**：
  - 视觉走查表：上述 8 种状态各造一例，逐条对照 ui_spec §5 渲染方式通过（含士气 ≥85 金 / ≤15 红、虚弱删除线"1"、护盾次数数字随抵挡递减、守护连线随位移断开）。
  - 事件 → 状态一致性：状态角标出现/消失时刻与内核事件日志（weak/virtue/affliction/taunt/guard/shield 事件）严格一致。
  - 失控标注用例：角色进入失控 → 单位有显著标注（角标 + 警示色，区别于普通 debuff），否则判不通过。
  - buff 折叠：同单位 ≥5 个图标时显示 ≤4 + 「+N」，展开可见全部且顺序 = ui_spec §3 优先级。
- **风险/开放**：无（状态数值口径（折磨 −10% 死门等）归 m4，本卡只做显示）；视觉符号/配色细则为表现层自由项，不属本卡规格。

---

### T-M5-09 UI↔内核解耦验收（镜像测试）

- **上游**：[设计] blueprint.md §4/§6.3/§8/§10（UI 解耦判据与镜像测试）、doc/modules/ui_spec.md §1（UI 是机制一部分）· 决策：#160
- **依赖**：T-M5-02 ~ T-M5-08 全部 + m2（回放：seed + 命令流 / DeterminismTests 基建）
- **产出**：
  - `res://scripts/ui/IBattleView.cs`（UI 唯一依赖面：只读投影 + 命令门面，blueprint §3/§12 风险 3）
  - `res://tests/MirrorTests.cs`（实机录制命令流重放 vs headless 事件日志逐条比对）
  - CI 静态检查配置（`using Godot` 白名单：只允许 scripts/gameplay/scene 与 scripts/ui，blueprint §10）
  - headless 冒烟脚本（删表现层后直驱 BattleDirector 跑通一局）
- **要点**：
  1. **UI 唯一依赖面 = IBattleView**：只读投影（Round/行动序列/单位位置·HP·士气·buff/技能可用性与命中率/撤退数字/预览请求结果）+ 命令门面（UseSkill/Retreat/Reinforce 等 BattleCommand 单向转交 BattleRoot→TurnDirector→BattleDirector）；**UI 不得触内核结算 API、不得持有可写引用**（blueprint §4 B5/B3、§12 风险 3）；"UI 绕过导演直连内核"即架构违规。
  2. **预览与结算同源**：位移/范围预览请求走内核 dry-run（对只读快照跑同一计算），因此预览永不与结算漂移（blueprint §5d/§10/§12 风险 6）——本卡把这条立为镜像测试的一部分。
  3. **镜像测试（blueprint §10）**：实机战斗以"录制命令流"重放，与 headless 事件日志**逐条比对**（逐事件一致，含 DrawCount 抽取序号序列）；同 seed + 同命令流 → 两份 CombatLog 完全一致（DeterminismTests）。
  4. **表现层零规则**：B4/B5 不得实现任何战斗规则（唯一事实在内核）；CI 静态检查 `using Godot` 白名单只放行 scripts/gameplay/scene 与 scripts/ui；`scripts/core`、`scripts/gameplay/sim`、`scripts/data` 零 `using Godot`（blueprint §3 依赖纪律）。
  5. **删表现层仍可跑（blueprint §10 一句话判据）**：删掉整个表现层与 UI，headless 仍能凭同一内核跑完并产出 M6 全部指标（胜率/回合/士气直方图/死门致死数/技能使用率/系统触发 KPI）；**加回表现层不改任何内核代码**。
  6. **命令来源三态一致**：实机输入 / 录制回放 / headless IPlayerPolicy 只差"命令来源"，均驱动同一个 BattleDirector 与同一份 JSON 数据（blueprint §8.3）；M6 基线 AI 与实机 EnemyAi 同源（T-M5-02）。
- **完成判据（可测）**：
  - MirrorTests：录制 ≥1 整场实机命令流（含我方技能/撤退/敌方 AI 决策），headless 重放后事件日志与实机日志**逐事件一致**（含每一条 RNG DrawCount 与取值）。
  - DeterminismTests：同 seed 同命令流跑两遍 → CombatLog 完全一致（m2 基建上扩展 M5 场景：敌方 AI 决策 + 增援 + 撤退参与的命令流）。
  - CI 白名单检查通过：`scripts/core|sim|data` 无 `using Godot`；scene/ui 之外的目录无规则实现。
  - 冒烟：删除 scenes/ 与 scripts/ui、scripts/gameplay/scene 后，HeadlessDriver 仍完成一整场（含增援/撤退/敌方 AI）并输出统计；再挂回表现层，内核文件 diff = 0。
- **风险/开放**：无新 O；若镜像测试暴露"预览阶段 RNG 抽取"问题（T-M5-06 注），属于实现口径缺陷，按"预览不新增抽取"铁律修复（O-30 可选项不参与命令流）。

---

## 附：M5 相关开放题速查（open_issues.md §B）

| ID | 议题 | 本文件落点 |
|---|---|---|
| O-11 | 撤退成功率公式缺失（f 未定，±10%） | T-M5-04：tuning 占位公式 + 默认示例，按钮数字与占位一致（M-C） |
| O-19 | AI 15% 次优先随机 vs #112 | T-M5-02：`random.enabled=false` 数据开关 default off |
| O-20 | 超时增援强度分布缺失 / 无空位 +攻/+速数值缺失 | T-M5-03：按 3 原型起手值生成、不走随机分布；增益数值占位 tuning |
| O-21 | 威吓箭 −4 vs 精神 −8 / 施法者点名最低韧性 vs 固定"我 1 位" | T-M5-01/02：威吓箭按显式 −4；点名按 target 直译待拍板 |
| O-31 | 威吓箭伤害表达式 12×0.5 vs 射手攻击 13 | T-M5-01：数据按 0.5 倍率 + 攻击 13，疑笔误不阻塞 |
| O-28 | 射手 AI 规则②使③威吓箭不可达 / 贴脸口径 | T-M5-02：按 data_schema §3.5 表序直译，不静默改序 |
| O-30 | 敌人意图 / 伤害预估显示可选 | T-M5-06 注：可选项，建议做敌人意图，非 9 条必显 |

---

## 实施记录（主程序回填 2026-09-09；判据状态与口径落点）

### 内核侧判据（T-M5-01~04 + T-M5-06/09 数据侧）全部关闭

| 判据 | 验证 |
|---|---|
| M-A 位移预览=结算 | BattleProjectorTests：dry-run 与真实 TrySwapChain 逐条一致、无副作用（快照板不变） |
| M-B 9 条必显数据可达 | BattleProjectorTests：Support（行动序列/撤退数字/占用）、HitRateFor 公式（90/55/100）、Skill tooltip 逐字、Units 槽升序含编号 |
| M-C 撤退按钮数字 | Support().RetreatRatePercent == director.CurrentRetreatRate()（纯公式无抽取）；钳制 [15,85] |
| M-D 增援第 6 回合 | DirectorMilestoneTests：第 5 回合前无事件、第 6 回合 Fill 首个空位、满编改 Buff（+攻/+速占位 3/3）、4 位上限 |
| 敌方 AI | EnemyAiTests：近战 3/4→突进、射手 1/2→后撤、施法者 AOE/低语、推至 1 空过、random off 确定性 0 抽取 |
| 撤退 | RetreatTests 子集：成功 −10×6/失败 −5×6、当回合禁用、下回合可试、钳制 [5,95] |
| 镜像/回放 | DirectorMilestoneTests.Mirror：同 seed 同命令流（含敌方 AI/增援钩子）两份 CombatLog 逐条一致 |

工程承接：`dotnet test` **134/134 绿（1.87s）**；`using Godot` 静态检查基线 0。

### 落地文件
- data/enemy_ai.json（3 原型优先级表 + random 开关 + #96 mark_weight 预留）+ EnemyAiConfig 模型/校验
- sim/enemy/EnemyAi.cs（谓词/taunt 优先/15% 分支 default off/空过）
- sim/director/BattleDirector.cs（回合状态机：增援/支援位+3/虚弱回升/行动序列固定点 BuildRoundOrder/玩家命令/敌方阶段/撤退 + 失败禁用 #118）
- sim/director/BattleProjector.cs（9 条必显只读投影 + dry-run 预览）、IBattleView.cs（UI 唯一依赖面）
- 事件：RoundStartEvent/ReinforcementEvent/RetreatEvent
- UI 薄层骨架（待 .NET 8/编辑器验证）：scenes/battle/Battle.tscn、BattleRoot.cs（组合根）、BattleUi.cs、IBattleView；tuning.overtime_reinforcement 增 buff_attack_delta/buff_speed_delta 占位

### 口径落点与移交
- O-19 random default off、O-20 增援走默认起手值（无随机分布）+ 增益占位数值、O-28 射手表序直译不静默改序（威吓箭③不可达已记录）、O-21 施法者按 target 直译、O-31 数据按 0.5 倍率+攻击 13、O-11 撤退展示=基础率（无扰动无抽取）、结算带扰动。
- 战斗出口（胜/负/撤退三态）与 BattleUI 四分区控件、死门演出、物理/精神配色、A 区 ActionOrderBar 等为 M5 UI 人工走查项，待编辑器（.NET 8）构建后逐项核验（见 §0 判据 M-B）。
