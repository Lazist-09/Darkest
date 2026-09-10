# M2 结算核心 · 任务卡（tasks/m2_combat_core.md）

> **编号**：ARCH-T-M2 · **类型**：task · **状态**：草案 v0.1（待架构师审校）
> **上游**：[设计] doc/modules/combat_math.md（全文 §0~§10）/ doc/GDD.md §2.5 行动顺序 / §4 战斗结算流程
> **决策引用**：#37, #80, #117, #123, #152, #154, #157, #158, #163, #164
> **依赖**：doc/architecture/_conventions.md §6（任务卡模板）；blueprint.md（§4 B2 内核 / §5b 时序 / §8 确定性 / §9 契约 / §10 测试钩子）；data_schema.md（§3.1 units.json / §3.3 morale_events.json / §3.7 tuning.json）；open_issues.md（A/B 节）；tasks/m0_bootstrap.md 与 tasks/m1_formation.md（前序里程碑任务卡，规划中，见 _conventions §2）
> **最近更新**：2026-09-09

---

## 0. 里程碑目标、硬门槛与判据对齐

**一句话目标**：把 `combat_math.md` 全文公式（命中 / 暴击 / 物理与精神双减免 / 附加效果 / 位移 / 士气变动 / 虚弱 / 死门）落成"纯 C#、零 `using Godot`、注入 RNG、固定调用点"的确定性结算内核（blueprint §4 B2），并跑通**固定次序**的结算管线，使策划 README §2 M2 判据为真。

**硬门槛**：`M1+M2 是硬门槛`（doc/README.md §2：这两步没做完，M3 之后的技能数值没有意义）。M2 的全部完成判据必须在进入 M3 前关闭；blueprint §1.1 将 M2 公式实现限定在确定性内核，M6 的全部 Monte Carlo 都建立在这层确定性之上。

**与策划 README M2 判据对齐**（doc/README.md §2 M2 行）：

| 策划 M2 判据（原文） | 本文件落点 |
|---|---|
| "属性表、命中/暴击/双减免/附加概率/位移抗性、行动序列与破平局 \| `combat_math.md`" | T-M2-01（纯函数库）+ T-M2-03/04/05/06/07（行动序列与各判定调用点）；属性表数据就绪见 §1 前置 |
| "用文档 §7 的试算样例能复算出同样数字" | T-M2-10（FormulaTests 复算 §7.1 全 8 样例）+ T-M2-01/05 公式侧 |

> 判据口径：只要求**数字复算**一致（§7.1 单次伤害），**不要求** §7.2 回合数（那是 M6 Monte Carlo 的事，见 T-M2-10 要点 3）。

---

## 1. M2 边界与前置

### 1.1 范围（本里程碑做）

- 只做 **B2 确定性内核**（blueprint §4）：`scripts/core`（BattleMath / IRngProvider）+ `scripts/gameplay/sim`（管线 / 士气 / 生存 / 回合序列）。**不做 B4 表现层 / B5 UI**（零 `using Godot`，见 blueprint §3 依赖纪律与 §4 边界）。
- 全部公式常量读 `tuning.json`（BalanceTable 只读快照），数值**逐字**来自 combat_math / data_schema §3.7；士气增减值唯一来自 `morale_events.json`（data_schema §3.3）。
- 结算管线按 **§2 固定次序**装配（T-M2-04~09 各产出一步类，由 `DamagePipeline` 组装）。

### 1.2 前置数据就绪（M2 期必须可用）

| 数据 | 依据 | 说明 |
|---|---|---|
| `units.json` 属性表（7 原型：hp/attack/phys_def/speed/dodge/crit/resilience/三抗性/位移抗性/死门抗性） | data_schema §3.1；combat_math §0 | 敌方 `deaths_door_resist=null`（敌方无死门，enemy §1 / P7） |
| `tuning.json` 过程常量 | data_schema §3.7 | `hit_clamp`/`crit_multiplier`/`damage_float`/`damage_floor`/`mental_reduction`/`weak`/`deaths_door`/`morale`/`speed_float`/`stun`/`stat_debuff_default`/`bleed` |
| `morale_events.json` 全表 14 行 | data_schema §3.3；P9 | 启动校验须含 §5.2 全部 id（缺行=报错） |

> 技能结算所需字段（`hit_mod`/`crit_mod`/`damage.segments`/`effects`/`displacement` 等，data_schema §3.2）在 M2 期以**最小结算夹具**（按 §7.1 所需字段直接构造的只读模板 record）提供；`skills.json` **43 条全量导入与 P2/P3 覆盖校验归 tasks/m3**。M2 不依赖 M3。

### 1.3 M2 不做（明确挂 O 或移交后续里程碑）

| 不做 | 归属 | 说明 |
|---|---|---|
| 撤退结算 / 撤退按钮 | O-11 | 撤退公式未定，**M2 不实现**；`morale_events` 中 `retreat_success`/`retreat_fail` 两行仅随表绑定与对账（P9），引擎触发点归 M5/M6 |
| 士气完整生命周期：崩溃判定池解析（美德/折磨 buff、33%）、崩溃余烬、士气回升、支援位每回合 +3 回合钩子、满值 +10 美德、撤退行触发 | tasks/m4（morale.md §4~§9） | M2 只做"伤害派生士气 + 表绑定 + 钳制 + Apply 通路 + 事件记录"，触发点预留调用 |
| 护盾 / 护卫拦截（#156/#159，IShieldGuard §9.9） | tasks/m4 | M2 管线只留"受伤害前拦截口"空实现（精神/物理同路径直通），拦截语义与数据接入 M4 |
| 治疗固定值 / 自我伤害固定值链路（combat_math §8） | tasks/m3（技能数据） | 数据在 skills.json 导入后接入；护盾语义 #156 在 M4 |
| 技能可用性判定、43 条技能全量导入 | tasks/m3 | — |
| Monte Carlo ≥300 场、回合数节奏验证、手感 KPI | tasks/m6 | §7.2 回合数试算由 M6 验证 |

---

## 2. 结算管线固定次序（T-M2-04~10 的次序依据）

**单目标内固定次序**（[设计] combat_math §5.2 / §6；blueprint §5b / §8）：

```
命中 → 暴击 → 伤害(扣HP；含护盾/护卫拦截口空实现) → 士气变动 → 虚弱/死门
```

- **技能级次序**（blueprint §5b Note + open_issues O-12 工作草案，本文件予以**固化**）：本技能**全部目标逐个结算**上述单目标链后，再执行**附加效果（眩晕/流血/属性减益）与位移**结算。多目标遍历顺序 = 目标槽位编号升序（固定枚举序，blueprint §8.1）。
- **目标数量语义（#178/#179，v0.44，O-38）**：技能目标数**不经数据声明**，按 `scope` + `tags.aoe` 派生——单体（无 `aoe`）与 `any_ally` **恰 1 个**（调用方在候选池内选一后传入）；AOE（横扫/精神震荡）遍历范围内**全部**非空；`team` 全体；`adjacent_ally_and_self` 自身+相邻。**双段（0.55×2 / 0.5×2）= 同一目标两段、目标只选一次（#179）**，各段独立结算实例（O-13）。
- **M2 默认（O-12 范围内，策划可推翻）**：
  - 目标在伤害/士气阶段**真死** → 从后续目标列表与效果/位移候选中**剔除**（不对尸体结算）。
  - 未命中 → 该目标不结算伤害与伤害派生士气；**目标层效果**（眩晕/流血/减益、指向该目标的位移）不结算（命中是其宿主前提）；**技能层自我位移**（self_forward/self_backward，data_schema §3.2）不依赖命中，照常结算。
- 各步骤事件一律写 `CombatLog`（blueprint §6.2），含每一条 RNG 抽取的 `DrawCount` 与数值（blueprint §8.1 / §10）。

**本里程碑涉及的确定性随机调用点**（blueprint §8.2 表，本里程碑范围内）：命中、暴击、附加效果触发、位移过抗性、死门判定、速度浮动（每回合重掷）。崩溃判定调用点在 HP 归零处保留（见 T-M2-09），其完整解析归 M4；撤退判定（O-11）不实现；折磨 33% 钩子归 M4。

---

## 3. M2 引用的开放题一览（编号以 open_issues.md A/B 节为准）

| O | 议题（简） | M2 处理 | 涉及卡 |
|---|---|---|---|
| O-01 | 伤害浮动 0.9~1.1 是否开启 | 默认关闭（=1.0），`tuning.damage_float.enabled=false`；留开关，若开启=新增一次抽取并回填 blueprint §8.2 | 01 / 05 |
| O-02 | 速度浮动是否再收窄 / 档内随机 | 按 0~10% 区间均匀实现（#163），档内固定；不阻塞 | 03 |
| O-03 | 虚弱者挨打 −5 是否改"仅精神" | 保留**任意类型** + 每回合≤1，预留配置开关（combat_math §5.1 例外） | 08 |
| O-04 | 精神 −8 是否足以让士气触底 | 需 M6 Monte Carlo；M2 只实现数值，不判达标 | 08 / 10 |
| O-11 | 撤退成功率公式缺失 | **M2 不实现撤退**；仅绑表对账（P9） | 08 |
| O-12 | 一次技能内部步骤 / AOE 次序 / 未命中子效果 / 中途死亡剔除 | 本文件 §2 固化工作草案为 M2 默认；卡片标注可推翻 | 04~07 / 09 |
| O-13 | 多段倍率（0.55×2）各段是否分别过命中/暴击/护盾 | ✅ **已定形**：#179 **同一目标两段**（目标只选一次）+ 各段独立结算实例（架构裁定，O-13 收口） | 05 |
| O-14 | 士气团队性事件聚合口径（暴击+5/击杀+10/队友虚弱−8/死亡−15） | 默认"实例各自结算、全队性事件同动作内合并一次"（口径见 §6，已含 #166~#170 拍板核定） | 08 / 09 / 10 |
| O-15 | 虚弱者受伤害的 HP 算术 | [架构裁定·已定]：虚弱中伤害不改 HP（恒 1）→ 直接死门；存活留 1、失败归 0 | 09 |
| O-23 | 属性减益固定 −3 与技能韧性 −15/−10 并存 | −3 为攻/防/速通用默认（tuning.stat_debuff_default）；韧性减益走技能标注值（−15/−10），各为起手值并存 | 06 |
| O-24 | 无概率标注的效果（嘲讽/韧性减益）是否吃抗性 | [架构裁定·已定]：有概率才过抗性、无概率直接生效 | 06 |
| O-32 | 精神 AOE + 暴击的士气取值（AOE 折扣 −5 vs 精神暴击 −12） | ✅ **已定（#166，O-32 收口）**：折扣 −5 优先——AOE 精神每命中目标 −5，暴击**不改变** −5（暴击只放大伤害） | 08 |
| O-33 | 虚弱者受**精神**伤害：−8/−12 与虚弱 −5 是否叠加 | ✅ **已定（#167，O-33 收口）**：叠加——精神 −8/−12 独立结算，虚弱 −5 独立生效且受每回合≤1 限制 | 08 |
| O-34 | 跨阵营同速破平局先后（敌我同速且编号同为 1） | ✅ **已定（#168，O-34 收口）**：编号相同则固定阵营序，**我方先于敌方**；阵营内仍按 #80 编号小者先动 | 03 |

> 超出既有编号的新疑问（"精神 AOE + 暴击的士气取值""虚弱者受精神伤害时 −8 与 −5 是否叠加""跨阵营同速破平局先后"）已由策划 **#166~#168（O-32~O-34）拍板定案**；**目标数量语义（#178/#179，O-38）见 §2**。后续新疑问仍按 _conventions §5 登记（open_issues.md O-40 起）后回填。

---

## 4. 任务卡（T-M2-01 ~ T-M2-10）

### T-M2-01 BattleMath 纯函数库
- **上游**：[设计] combat_math.md §1 命中 / §2.1 物理 / §2.2 精神（#158）/ §2.3 取整下限 / §3 位移 / §4 附加概率 / §6 死门存活概率；data_schema.md §2.2 数值钳制、§3.7 tuning.json
- **决策**：#152（公式表）、#158（精神减免连续公式，推翻 #116 分档）
- **依赖**：T-M0 系列（tasks/m0_bootstrap.md：工程目录 `scripts/core/math` 与测试工程）；data_schema §3.7 `tuning.json`（BalanceTable 只读快照）就绪（§1.2）
- **产出**：`res://scripts/core/math/BattleMath.cs`（静态纯函数库，零 `using Godot`、零 I/O、不持状态）；`res://tests/FormulaTests.cs`（骨架；全量样例在 T-M2-10 收口）
- **要点**：
  1. 函数契约（伪代码级，交主程序实现）：`HitRate(dodge, hitMod)` 钳制 [55,100]（tuning `hit_clamp`）；`PhysicalMitigation(physDef)=physDef/(physDef+30)`（§2.1）；`MentalMitigation(resilience)=min(resilience/250, 40%)`（§2.2 / tuning `mental_reduction`）；`ActualEffectChance(labeled, resist)=labeled×(1−resist)`（§4）；`DeathDoorSurvive(resist, affliction)=resist−(affliction?10:0)`（§6 / tuning `deaths_door`）。
  2. 伤害主公式：物理 `round(攻击×倍率×(1−物防减免)×暴击倍率×伤害浮动)`；精神同构用精神减免（§2.1/§2.2）。`round()` 一律**四舍五入、远离零**（.NET 需显式 `MidpointRounding.AwayFromZero`，禁用默认银行家舍入——防跨平台 double 语义漂移，blueprint §8.1）。
  3. 任何来源伤害**最低 1 点**（tuning `damage_floor`，在取整后 `max(1,·)` 施加，§2.3）。
  4. 伤害浮动默认 `1.0`（tuning `damage_float.enabled=false`；O-01 按关闭实现，开关预留；若开启需一次抽取，由调用步注入随机，**纯函数内不自造随机**）。
  5. 全部数值经 `BalanceTable`（tuning 快照）读取，**禁止硬编码起手值**（blueprint §7 违规判据：任何拍死在内核代码里、脱离 tuning.json 的值都是架构违规）。
  6. 纯函数供 `tests/` 直连复算（blueprint §10"公式单测可复算"），与表现层零耦合。
- **完成判据（可测）**：FormulaTests 断言 §7.1 全 8 样例整数一致（其余样例卡在 T-M2-10）；边界：四舍五入 `round(0.5)=1`、`round(1.49)=1`；减免极值（高防 → 收敛不为负）；命中钳制 55/100；伤害下限 1（构造极小 raw 伤害断言）；精神减免 cap 40%。
- **风险/开放**：O-01（浮动开关）；§7.1 表中 `0.789`/`0.882` 等常数为文档截断示意，判据以"精确公式计算后取整的最终整数"为准，不比对截断常数。

### T-M2-02 IRngProvider 落地
- **上游**：[设计] combat_math.md 文档头通用约定（"骰子一律 `rand(0,100)`，比较用 `<`"）/ §9 随机层清点；blueprint §8 确定性 + §9.8 契约；data_schema §2.3 随机判定写法
- **决策**：—（确定性铁律见 blueprint §8，无新增决策）
- **依赖**：T-M0 系列（`scripts/core/rng` 目录与测试工程）；无 sim 依赖
- **产出**：`res://scripts/core/rng/IRngProvider.cs`；`res://scripts/core/rng/RngProvider.cs`（固定种子实现）；事件记录接入（DrawCount 字段随抽取事件写入 CombatLog）；`res://tests/DeterminismTests.cs`（骨架，T-M2-10 收口）
- **要点**：
  1. 接口逐字对齐 blueprint §9.8：`NextPercent()` 返回 [0,100)；`NextInt(minInclusive,maxExclusive)`；`ulong DrawCount`。
  2. 判定式写作约定：`rand(0,100) < 判定值` 算成功（阈值本身不命中，如命中率 90、roll=90 → miss）；**唯一例外** = 位移 `rand >= 位移抗性`（卡 07，data_schema §2.3）。RNG 只给均匀分布，判定比较写在调用点。
  3. **注入而非自造**：内核一律通过构造/组合根注入 IRngProvider，禁止 `new Random()` / Godot.RandomNumberGenerator（零 `using Godot`，blueprint §8.1）。每场 `BattleSession(seed)` 一个种子。
  4. **DrawCount 审计**：每次抽取单调递增，`(DrawCount, 数值)` 写进对应事件，CombatLog 可重放、可审计"谁消耗了随机"（blueprint §8.1）。抽取次数或顺序任何变更 = 确定性破坏。
  5. 实现算法**跨平台确定**（推荐自实现 SplitMix64 / Xoshiro256** 等种子化 PRNG，不依赖框架未契约化的 `System.Random` 内部实现），并用测试锁定首 N 个抽取值序列防未来漂移。
  6. 固定抽取时机（行动选择先抽、结算后抽——M6 半随机策略）；任何新增骰子必须落内核固定调用点并回填 blueprint §8.2 表，否则判架构违规。
- **完成判据（可测）**：DeterminismTests 同 seed 两次抽取序列逐值一致、不同 seed 结果不同；事件日志含逐条 DrawCount 且连续无跳号；`<` 判定边界用例（roll==阈值 → 失败）通过。
- **风险/开放**：无新增；抽取点扩展纪律见 blueprint §8.2。

### T-M2-03 行动序列 TurnSequencer
- **上游**：[设计] GDD §2.5 行动顺序；combat_math §9 速度浮动行；blueprint §9.2 契约；data_schema §2.4 UI#1（units.json `speed` × tuning `speed_float`）
- **决策**：#80（破平局：同速编号小者先动）、#163（速度浮动收窄 0~10%、每回合重掷）
- **依赖**：T-M2-02（RNG，速度浮动骰）；T-M1（tasks/m1_formation.md：FormationBoard / UnitRuntime 槽位枚举与 `OnRemoved`）；UnitRuntime"生效速度"只读口（T-M2-06 减益 / T-M2-09 虚弱修正接入）
- **产出**：`res://scripts/gameplay/sim/turn/TurnSequencer.cs`（实现 blueprint §9.2 ITurnSequencer）；`res://tests/TurnOrderTests.cs`
- **要点**：
  1. `BuildRoundOrder` 每回合开始调用一次，对本回合全部在列单位各掷一次速度浮动：`实际速度 = 基础速度 × (1 + 0~10% 随机)`（#163 / tuning `speed_float`）；浮动因子按单位**固定枚举序**（FormationBoard 槽位升序遍历，我方 1~6 后敌方 1~4）逐次抽取，杜绝字典/哈希序漂移（blueprint §8.1）。
  2. 排序按实际速度**降序**；实际速度相等 → **编号小者先动**（#80）。实际速度以固定精度（如保留 3 位小数定点）比较，实现自定但须固定并锁入单测。
  3. **眩晕** = 跳过本次行动、状态随即结束（GDD §2.5 / tuning `stun`）：`NextActor()` 出列时命中眩晕标记 → 跳过该次行动并清除标记（标记由 T-M2-06 登记；M2 只实现跳过逻辑）。
  4. `OnRemoved`：死亡/离场实时从序列剔除并更新（blueprint §9.2；敌方击杀在 T-M2-05、我方真死在 T-M2-09 产生）。
  5. "生效速度"口当前最小集合 = 基础速度 × 虚弱因子（tuning `weak.speed_mult=0.7`，T-M2-09 建立）；属性减益固定值（−3 速度，T-M2-06）与其它 buff 修改器后续接入同一只读口（加法先于乘法语义见 §9.6 FinalStats，M4 补全）。
  6. 行动序列结果须可被 UI 只读投影（ui_spec 必显 #1，显示本回合+下回合）——M2 只保证数据输出形态，渲染归 M5。
- **完成判据（可测）**：TurnOrderTests 用 stub RNG 断言：同 seed 全回合序列与手算一致；基础速度相同（军医 10 / 政委 10）且浮动因子相同时 → 编号小者（政委 3）先动；眩晕标记单位被跳过且状态随即清除；死亡单位不出现在后续；同 seed 两遍逐事件一致（DeterminismTests）。
- **风险/开放**：O-02（是否档内随机，待实测，不阻塞）；O-34 **已定（#168）**：同速跨阵营**我方先动**（M2 默认即此，锁单测）；眩晕 buff 数据位在 buff_defs（M3/M4），M2 只读标记接口预留。

### T-M2-04 命中判定
- **上游**：[设计] combat_math §1 命中；data_schema §2.2 钳制行 / §2.3 判定写法；units.json `dodge`、skills.json `hit_mod`
- **决策**：—（公式见上游，无新增决策）
- **依赖**：T-M2-01（HitRate 纯函数）、T-M2-02（命中骰）；T-M1 UnitRuntime（目标闪避）；技能 `hit_mod`（M2 用夹具 / M3 全量）
- **产出**：`res://scripts/gameplay/sim/pipeline/HitStep.cs`（命中步判定器）；`res://tests/CombatResolutionTests.cs`（命中用例，与卡 05~09 共用）
- **要点**：
  1. `命中率 = 100 − 目标闪避 + 技能命中修正`，钳制 **[55,100]**（combat_math §1 / tuning `hit_clamp`；下限防"必 miss"挫败，上限**不做必中**）。
  2. `命中 = rand(0,100) < 命中率`（`<`，阈值本身不命中）。
  3. 未命中处理按 §2 M2 默认（O-12）：该目标不结算伤害与伤害派生士气；目标层效果不结算，技能层自我位移照常。
  4. 命中/未命中写事件日志（含 DrawCount），供 UI 命中率显示（ui_spec 必显 #4，M5）与回放。
- **完成判据（可测）**：`HitRate(dodge=10, mod=0)=90`（对齐 §7.2"命中 90%"基线）；边界：负修正触发下限 55、正修正触发上限 100；roll==阈值（如 55）→ 未命中；未命中不产生伤害/士气事件（集成用例）。
- **风险/开放**：O-12（未命中子效果默认已固化，可推翻）。

### T-M2-05 伤害结算主链
- **上游**：[设计] combat_math §2.1 物理 / §2.2 精神（#158）/ §2.3 取整下限；data_schema §3.2 `damage.segments`；blueprint §8.2 暴击/伤害调用点
- **决策**：#152（公式表）、#158（精神减免连续公式）
- **依赖**：T-M2-01、T-M2-02（暴击骰）、T-M2-04（命中步，本卡接暴击+伤害）；技能段数据（data_schema §3.2，夹具先行）；伤害后接 T-M2-08（士气）、T-M2-09（弱转移）；击杀移除接 T-M1（FormationBoard / CloseUp）
- **产出**：`res://scripts/gameplay/sim/pipeline/DamageStep.cs`（暴击+伤害+击杀判定步，含护盾/护卫拦截口空实现）
- **要点**：
  1. **暴击步**：`rand(0,100) < 暴击率`（基础 + `crit_mod`）= 暴击 → 倍率 ×1.5（tuning `crit_multiplier`），未暴击 ×1.0（combat_math §2.1）。
  2. **物理**：`实际 = round( 攻击×倍率 ×(1 − 物防/(物防+30)) × 暴击倍率 × 浮动1.0 )`（§2.1；减免用递减公式，高防边际递减、永不出现 0/负伤害）。
  3. **精神**：`减免 = min(韧性/250, 40%)`，`实际 = round( 攻击×倍率 ×(1−精神减免) × 暴击倍率 × 浮动1.0 )`（§2.2 / #158；韧性 50 → 20%）。
  4. **段**：`damage.segments` 逐段独立成伤（`0.55×2` = 两段各 0.55）；默认各段独立结算实例（独立暴击与事件记录，O-13）；护盾粒度（按攻击 vs 按段）O-13 待拍，M4 接入前不影响（M2 无护盾）。
  5. 取整后**最低 1 点**（tuning `damage_floor`，§2.3）；扣 HP 在此步完成。
  6. **击杀判定**：目标 HP ≤ 0 且为敌方 → **直接死亡**（敌方无虚弱/死门，enemy §1，`deaths_door_resist=null`）→ 触发 `kill_enemy +10`（团队，聚合 O-14）+ 移除与靠齐（FormationBoard，M1）；目标 HP ≤ 0 且为我方 → 移交 T-M2-09 弱转移（**不死亡**）。
  7. 受伤害前拦截口（IShieldGuard §9.9）M2 为空实现直通（精神/物理同路径）；拦截语义与数据 M4 接入。
- **完成判据（可测）**：FormulaTests 复算 §7.1 全 8 样例（含精神样例 施法者→战士 = 8：12×0.8×(1−20%)=7.68→8）；注入骰序固定非暴击、浮动 1.0 时断言；伤害下限 1；段独立事件记录；敌方 HP≤0 走击杀（无弱/死门事件）。
- **风险/开放**：O-01（浮动）、O-13（段粒度）、O-14（击杀 +10 聚合）；护盾/护卫空钩 M4。

### T-M2-06 附加效果判定
- **上游**：[设计] combat_math §4 附加效果；data_schema §3.2 `effects`（type/probability/resist_axis）+ §2.2 实际概率行；GDD §1.1 障碍免疫 debuff / §4 部分非空
- **决策**：—（眩晕按 GDD §2.5；数值与抗性见 §4 / units.json）
- **依赖**：T-M2-02（效果骰）；T-M1（槽位三态 / 障碍）；效果登记进 UnitRuntime 状态聚合（T-M2-03/05/09 消费）；眩晕联动 T-M2-03
- **产出**：`res://scripts/gameplay/sim/pipeline/EffectsStep.cs`（附加效果判定与登记）；UnitRuntime 状态聚合扩展（M1 基础上：stat_mod 修改器 / stun 标记 / bleed dot）
- **要点**：
  1. `实际触发率 = 技能标注概率 × (1 − 目标对应抗性)`（乘法，非减法，§4）；`触发 = rand(0,100) < 实际触发率`（blueprint §8.2）。抗性映射：眩晕→`stun_resist`、流血→`bleed_resist`、属性减益→`stat_debuff_resist`（data_schema §3.2 `resist_axis`）。
  2. **施加谓词（条件谓词）**：效果只对 `occupied`（有角色）槽生效；障碍（blocked）吃伤害（AOE 波及）但**免疫眩晕/流血/属性减益等状态**（GDD §1.1）；范围技能部分非空只作用于非空位（GDD §4 / data_schema §3.2 生效语义）。
  3. **眩晕**：持续 = 跳过本次行动、状态随即结束（GDD §2.5 / tuning `stun` / buff_defs `action_skip`），登记 stun 标记 → T-M2-03 出列时跳过。
  4. **流血**：每回合 3 点固定伤害、持续 2 回合、回合结束结算（tuning `bleed`）；切片无技能施加者（buff_defs 预留，P6 保证 tuning.bleed 与 buff_defs 一致），链路按 §4 实现备数据扩展。
  5. **属性减益**：固定值修正攻/防/韧/速（含速度固定值 −3，GDD §2.5），持续 2 回合；通用默认 −3/2（tuning `stat_debuff_default`）；技能自带韧性减益 −15/−10 与其并存（O-23 口径）；无概率标注的效果默认直挂不掷骰（O-24 [已定]）。
  6. 判定函数与生效分离：判定（概率/抗性/边界）为纯函数、不落状态，返回是否触发；由调用方按效果类型登记（stun 标记 / bleed dot / stat_mod 修改器），UI 显示的实际概率与引擎判定共用同一纯函数（对齐卡 01 的 `ActualEffectChance`）。
- **完成判据（可测）**：`ActualEffectChance(40, 30)=28`（反例证明乘法而非 40−30=10）；抗性 100 → 0（不触发）；标注 100% → 实际 = 100−抗性；roll==阈值 → 不触发；障碍目标不施加状态；无概率效果不掷骰；流血 3/2 与 tuning.bleed 一致（P6 对账）。
- **风险/开放**：O-12（时点/未命中/死亡剔除默认已固化）、O-23（−3 与 −15/−10 并存）、O-24（无概率直挂，已定）。

### T-M2-07 位移判定
- **上游**：[设计] combat_math §3 位移；formation.md §2 逐级交换链；data_schema §3.2 `displacement`、§3.6 `formation.json rules`
- **决策**：#117（位移不产生伤害 → 不触发死门）、#78（边界硬墙）
- **依赖**：T-M2-02（位移骰）；T-M1（IFormation.TrySwapChain / DisplaceResult / 预览 dry-run）；技能 `displacement` 字段（夹具先行）
- **产出**：`res://scripts/gameplay/sim/pipeline/DisplaceStep.cs`（位移步：过抗性判定 + 调 IFormation.TrySwapChain + 事件记录）
- **要点**：
  1. `过抗性 = rand(0,100) >= 目标位移抗性` 即成功（**唯一 `>=` 例外**，combat_math §3 / data_schema §2.3 / glossary §3）。
  2. 成功 → IFormation.TrySwapChain 交换链（**永不产生空位**，formation §2）；失败 → 位移不生效，**伤害与其它效果照常结算**（combat_math §3）——本步与卡 05/06 解耦（各自独立判定与事件）。
  3. 位移**不产生伤害** → **不触发死门**（#117）：本步不触碰 HP / 士气 / 死门（集成用例断言）。
  4. 撞边界 = 位移失败不动（`boundary_as_hard_wall`，#78）；撞障碍 = 交换（`obstacle_swaps_like_unit`，#22）——由 M1 FormationBoard 返回 DisplaceResult 承载，本步只读结果记事件。
  5. 敌人位移仅能由技能触发（#88）；`displacement.count` 切片=1（data_schema §3.2）；push / self_forward / self_backward 方向语义由技能记录提供。
  6. 位移步时点 = 技能全部目标结算后（§2 / O-12 默认）；位移预览 = 同一内核 dry-run（blueprint §5d）→ 本步须可在**只读快照**上调用（不写状态）。
- **完成判据（可测）**：抗性 60%：rand=60 → 成功、rand=59 → 失败（`>=` 边界）；位移失败不影响同技能伤害事件；位移成功**无** HP/士气/死门事件（#117 断言）；结果与 M1 BoardTests 交换链一致；同 seed 复现。
- **风险/开放**：O-12（时点/死亡目标剔除默认已固化）；`pull` 切片未用（data_schema §3.2）。

### T-M2-08 士气变动结算
- **上游**：[设计] combat_math §5.1 / §5.2 士气增减表；blueprint §9.4 IMoraleLedger；data_schema §3.3 morale_events.json + §2.2 士气钳制
- **决策**：#157（只有精神伤害降士气 + 虚弱者任意受伤 −5）、#155（士气冲满 +10 一次性）
- **依赖**：T-M2-05（伤害事件源）；T-M2-09（弱入口 / 死亡事件 / 每回合≤1 计数）；morale_events.json 只读注册表（§1.2）；崩溃判定调用点（完整解析归 M4）
- **产出**：`res://scripts/gameplay/sim/morale/MoraleLedger.cs`（Apply / 钳制 / 事件表绑定 / 伤害派生士气；其余成员如 RollCollapse / IsCollapseEmber / SupportSlotRegen 由 tasks/m4 按 blueprint §9.4 接口补齐）
- **要点**：
  1. 唯一写入口 `Apply(unit, delta, source)`（blueprint §6.2）；范围钳制 tuning `morale` [0,100]（含双向钳制）。
  2. **受击派生（本里程碑实现）**：物理伤害（damage_axis=physical）→ 士气不变（`physical_hit_no_effect` 0，行保留作口径记录）；精神伤害 → `mental_hit` −8；被精神暴击 → `mental_crit_hit` −12（覆盖同次 −8，不叠加）；精神 AOE（技能 tags 含 `aoe`）→ `mental_aoe_hit` −5 **per_target** 每个命中目标各 −5（AOE 折扣）。引擎按 damage_axis + 暴击 + aoe 标签自动落这三行，**不在技能数据里声明**（data_schema §3.2 士气衔接 / #157）。
  3. 虚弱者受任意伤害 → `weak_hit_any_damage` −5（self），occurrence `once_per_turn_max1`（每回合最多 1 次）；时序在死门判定**之前**（combat_math §6：先扣 −5 再死门；与 T-M2-09 联动）。
  4. **全表绑定 + P9 校验**：morale_events.json 须含 §5.2 全部 14 行 id（缺行=启动报错）；引擎只读表、数值逐字对照，**禁止硬编码**士气值（改 JSON 不改代码）。
  5. **团队性事件聚合**：暴击+5 / 击杀+10 / 队友虚弱−8 / 死亡−15 按 O-14 默认"实例各自结算、全队性事件同动作内合并一次"（如 AOE 一次命中 2 目标，`critical_strike_dealt +5` 只结算一次）；`mental_aoe_hit` 为 per_target 行，逐目标各自 −5（不合并）。Apply 需支持"动作级聚合上下文"。
  6. **本卡不做（M4 承接，避免重复实现）**：崩溃判定完整解析（美德/折磨 buff、33%、余烬）、士气回升、支援位每回合 +3 回合钩子、满值 +10 美德、撤退行触发、技能 `morale_effects`（M3 数据驱动，经 Apply 通路生效）。M2 只提供 Apply 通路与伤害派生；士气归 0（非虚弱入口）的崩溃判定触发点保留调用与事件记录。
- **完成判据（可测）**：物理伤害 → 士气 0 变化；精神 −8；精神暴击 −12（不叠加 −8）；精神 AOE 命中 2 目标 → 各 −5 且团队行合并一次；虚弱者挨打 −5 每回合仅 1 次（同回合第二次不扣）；钳制 0/100 边界；集成用例锁序"伤害→士气→虚弱/死门"；P9 14 行 id 齐备（缺行 → 启动失败）。
- **风险/开放**：O-14（聚合口径默认）、O-03（虚弱 −5 任意类型 + 配置开关）、O-04（−8 触底验证归 M6）；组合行见 §6（精神 AOE+暴击取值、虚弱者 −8 与 −5 叠加）。

### T-M2-09 虚弱 / 死门
- **上游**：[设计] combat_math §6 虚弱/死门；GDD §3.3 / §3.4；blueprint §5c stateDiagram + §9.5 IDeathsDoor；data_schema §3.7 `weak` / `deaths_door`
- **决策**：#123（折磨 −10%）、#117（位移不触发）、#37（虚弱减益值出处）
- **依赖**：T-M2-01/02/05/08；T-M1（CloseUp / 移除）；units.json 我方 4 原型死门抗性（70/75/60/60，敌方 null）
- **产出**：`res://scripts/gameplay/sim/survival/DeathsDoor.cs`；`res://scripts/gameplay/sim/survival/WeakState.cs`（弱转移状态机）；`res://tests/SurvivalTests.cs`
- **要点**：
  1. **HP 归零（非虚弱，仅我方）→ 不死亡**（combat_math §6 / GDD §3.3）：士气立即置 0 并触发一次崩溃判定（RollCollapse 调用点，blueprint §5c Normal→Weak；完整美德/折磨池解析与 buff 施加归 M4，本卡保证调用点顺序与事件记录）；随后进入虚弱：**伤害 −50% / 速度 −30% / HP 锁 1**（tuning `weak`：damage_mult 0.5 / speed_mult 0.7 / hp_lock 1）。
  2. **死门判定**（仅虚弱中再受伤时触发）：先扣 `weak_hit_any_damage` −5（卡 08，每回合≤1）→ `存活概率 = 死门抗性 − (折磨中 ? 10% : 0)`（#123 / tuning `deaths_door.affliction_penalty_percent`）；`存活 = rand(0,100) < 存活概率`（data_schema §2.3 / blueprint §8.2）。折磨标记经 IBuffLedger 检查（M4 提供 buff 语义；M2 测试用注入标记）。
  3. **HP 算术（O-15 [已定]）**：虚弱中伤害不改 HP（恒 1）→ 直接死门判定；存活 → HP 保持 1；失败 → HP 归 0、真死。
  4. 真死 → 移除 + 靠齐（CloseUp，M1 执行；虚弱者不参与前移 #114/#115）+ 全队 `ally_death −15`（团队，聚合 O-14）。
  5. 伤害来源：直接攻击、流血持续伤害、护卫替挡（buff.md §7.1）→ M2 覆盖直接攻击路径；流血（切片无施加者）与护卫（M4）只留判定入口。
  6. **位移不产生伤害 → 不触发死门**（#117）——卡 07 已断言。敌方无虚弱/死门：`deaths_door_resist=null`，HP 归零即死亡（卡 05 击杀路径；P7）。
  7. 事件顺序（blueprint §5b 内层）：受伤害 → 扣 HP → 士气 →（HP 归零且非虚弱 → 士气置 0 + 崩溃判定 → 进虚弱 | 已虚弱且受伤 → −5 → 死门）。
- **完成判据（可测）**：普通归零 → 事件序 `hp_zero → morale_zero → collapse_roll → weak_enter`；已虚弱受伤 → 死门判定事件在 −5 士气事件之后；`rand<存活概率` 存活（HP 保持 1）、`rand≥存活概率` 失败 → 真死 → 移除 + `ally_death −15`；注入折磨标记 → 战士抗性 70→60（rand=60 → 死、rand=59 → 活）；敌方 HP 归零不走弱/死门；死门判定为固定调用点（DrawCount 单次记录，blueprint §8.2）。
- **风险/开放**：O-15（已定 HP 口径）、O-14（死亡 −15 聚合）；崩溃判定完整语义 M4；敌方 `deaths_door_resist=null` 校验 P7。

### T-M2-10 节奏与复算验收集
- **上游**：[设计] combat_math §7 节奏试算（§7.1 八样例 / §7.2 回合数）；blueprint §10 公式单测可复算 + DeterminismTests；doc/README §2 M2 判据
- **决策**：#154（敌 HP 起手值 50/50/38/34）、**#171/#173（v0.41 生效值：敌 HP 60/60/46/41、收割 0.4/0.5、横扫 0.6、cleave 0.9 / lunge 0.8 / doubleHit 0.5 / 冲锋令 1.0，已入 units/skills.json）**
- **依赖**：T-M2-01~09 全部；T-M0 测试工程；blueprint §10 测试钩子
- **产出**：`res://tests/FormulaTests.cs`（§7.1 全 8 样例断言表）；`res://tests/DeterminismTests.cs`（同 seed 同命令流日志一致）；`res://tests/SurvivalTests.cs` / `TurnOrderTests.cs` / `CombatResolutionTests.cs`（各卡判据汇总收口）
- **要点**：
  1. **§7.1 八样例全量断言**（判据字符串与 combat_math §7.1 逐字一致）：
     | 攻击者 → 目标 | 算式 | 期望 |
     |---|---|---|
     | 战士 → 近战小兵 | 12×1.0×(1−8/38) | **9** |
     | 军医 → 近战小兵 | 11×1.0×0.789 | **9** |
     | 政委 → 远程射手 | 11×0.95×(1−4/34) | **9** |
     | 坦克 → 近战小兵 | 8×0.9×0.789 | **6** |
     | 近战小兵 → 战士 | 12×1.0×0.789 | **9** |
     | 近战小兵 → 坦克 | 12×1.0×(1−12/42) | **9** |
     | 远程射手 → 军医 | 13×0.9×0.882 | **10** |
     | 施法者 → 战士（精神） | 12×0.8×(1−20%) | **8** |
  2. 用例默认**无暴击、无浮动**：注入骰序使暴击骰不命中、浮动恒 1.0（用 stub IRngProvider 固定序列）；输入 = units.json 属性 + §7.1 等价倍率夹具（M2 期，M3 用真实技能行回归同一断言）。
  3. **§7.2 回合数试算（~9 回合）不属 M2 验收**：由 M6 Monte Carlo（≥300 场，胜率 40~70%，verification §2）验证；M2 只提供公式确定性基础与敌人 HP **生效值（#171：60/60/46/41，units.json 现值；skill_data/enemy 表保持起手值口径 — README §6-5「数值不当即改」）**。
  4. **DeterminismTests**：同 seed + 同命令流跑两遍 → 两份 CombatLog 事件（含 DrawCount 序列）**完全一致**（blueprint §8 / §10）；覆盖至少一个完整回合 + 一次虚弱/死门场景。
  5. 另覆盖 blueprint §10 要求的边界：钳制 / 下限 / 四舍五入 / 减免极值。
- **完成判据（可测）**：`dotnet test` 全绿；§7.1 八样例逐字复算一致（对齐 doc/README §2 M2 判据"用文档 §7 的试算样例能复算出同样数字"）；同 seed 两遍事件日志一致；M6 镜像测试（实机 vs headless 逐事件一致）为 M5/M6 判据，M2 只留命令流 API。
- **风险/开放**：O-04（士气触底归 M6）；O-14（聚合口径影响士气分布 KPI，M6 校）。

---

## 5. 里程碑级收口说明

- **卡间次序**：T-M2-01/02 为基础设施（01 纯函数、02 RNG），T-M2-03~09 为结算各步（03 回合序列 / 04 命中 / 05 伤害 / 06 效果 / 07 位移 / 08 士气 / 09 虚弱死门），T-M2-10 为复算与确定性收口。各步类由 `DamagePipeline` 按 §2 次序装配。
- **集成判据**（blueprint §10）：删除整个表现层与 UI，headless 仍能凭同一内核跑完并产出 M6 全部指标；加回表现层不改任何内核代码。M2 只须保证内核本身确定性成立。
- **确定性红线**：同 seed 同命令流 → 逐位相同的事件日志；任何新增骰子必须落固定调用点并回填 blueprint §8.2。

---

## 6. 组合行（原"待确认"，三行已由策划 #166~#168 拍板，保留作血缘记录）

| 组合行 | 现状 | M2 默认（可推翻） |
|---|---|---|
| 精神 AOE + 暴击 的士气取值 | combat_math §5.2 未给组合行（−5 AOE 折扣 vs −12 暴击） | 按折扣行优先：AOE 精神命中即 −5/目标，暴击不改变 −5 |
| 虚弱者受精神伤害：−8（或 AOE −5）与 `weak_hit −5` 是否叠加 | §5.1 只写"任何伤害仍 −5（每回合≤1）"，叠加未明示 | 叠加：受击派生行各自结算 + 虚弱 −5（once_per_turn_max1） |
| 跨阵营同速破平局先后 | #80 只定义"编号小者先动"，编号体系各自 1 起，跨阵营未定义 | 编号相同则固定阵营序（我方先于敌方），锁单测 |

> ✅ **三行已由策划拍板（#166~#170，2026-09-09）并经主程序核对**：精神 AOE+暴击 → **折扣 −5 优先、暴击不改变 −5**（#166 / O-32）；虚弱者 −8/−12 与 −5 **叠加**（#167 / O-33）；跨阵营同速 **我方先动**（#168 / O-34）——与 M2 默认一致，M2 §7.3 已核对无需改动。另：**目标数量语义（#178/#179，O-38）见 §2**——单体技能的"多目标遍历"在新口径下目标数=1。

---

## 7. 冒烟记录（主程序回填 2026-09-09；判据状态与踩坑实录）

### 7.1 判据状态（M2 硬门槛全部关闭）

| 收口项 | 验证 |
|---|---|
| §7.1 八样例复算（README M2 判据"用文档 §7 复算同样数字"） | ✅ FormulaTests 8/8（含精神 7.68→8 钉死四舍五入） |
| 命中/暴击/双减免/附加概率/位移抗性调用点 | ✅ Hit/Effects/Displace 边界用例（`<` 与位移 `>=` 例外、乘法实际率、钳制) |
| 行动序列与破平局 | ✅ TurnOrderTests（#80 编号小者先动 + 跨阵营我方先手锁死） |
| 固定次序管线（§2） | ✅ DamagePipeline 装配：命中→暴击→伤害→士气→虚弱/死门；效果与位移殿后（O-12 默认） |
| 同命令流确定性 | ✅ CombatResolutionTests.Determinism：同 seed 两份事件日志逐条一致（含 RngDraw 序列）|
| 工程承接 | ✅ `dotnet test` **64/64 绿（0.92s）**；`using Godot` 基线 0 命中 |

### 7.2 踩坑实录（写回本卡）

- **record 位置参数缺 `[JsonPropertyName]` → 静默 null/默认值**：MoraleEventConfig 仅 id/name 带属性，delta/scope/occurrence/source/note 大小写不匹配全部落默认（首行校验即报"name/source 必填"误导性错误）；已全字段显式标注。同类教训已在 M1 §5.2 记录，本次再次钉死：**凡 snake_case JSON 必逐字段 `[JsonPropertyName]`**。
- **数据模型"查询缓存"必须初始化**：MoraleEventsConfig 曾以空字典字段作 `Get` 缓存（从未填充）→ 运行期"引用不存在事件"。改为小表线性查找（≤14 行），消除未初始化路径。
- **测试对默认编成的槽位假设**：formation.json 我方 1 号位是**坦克**（hp55、韧55←精神减免22%、死门75），非战士；多例按"1 号位=战士"断言吃瘪。已统一改用 2 号位（战士）或按实况断言。
- **靠齐会填槽**：死亡移除后 CloseUp 会把后方单位补上来，断言"原槽 Empty"在非队尾槽位不成立；改断言"占据数 −1 / 原单位不再在该槽"。

### 7.3 边界/移交（不阻塞 M2，挂后续里程碑）

- 护盾/护卫拦截口、流血回合钩子、崩溃判定池解析、撤退行触发、技能 `morale_effects`（M3 全量数据）→ M3/M4。
- 组合行三默认（AOE+暴击 → 折扣优先 / 虚弱 −8 与 −5 叠加 / 跨阵营同速我方先手）已锁单测。**策划拍板 #166~#170（2026-09-09）核对**：与实现一致，无需改动；O-32 的 open_issues 旧登记（"−12 覆盖 −5"）已由策划纠正为折扣优先——主程序实现自始按 m2 §6 组合行默认（折扣优先）落地，未照旧登记做过错判例。
- **O-11（#169）已落盘**：`tuning.json → retreat_formula`（base 50 / ±4%×速度差 / 钳 [15,85] / ±10 / 钳 [5,95]）；`BattleMath.RetreatBaseRate / RetreatFinalRate` 纯函数 + FormulaTests 边界用例（M5 撤退按钮按此实现，tuning 默认即上值）。
- **O-21（#170）钩子已落**：`SkillFixture.ExplicitMoraleEffects`——显式 morale_effects（威吓箭 targets −4）**取代**精神派生 −8/−12/−5（不叠加）；CombatResolutionTests 已锁"−4 替换、无 mental_hit"语义。M3 全量 skills.json 接入时启用。

### 7.4 v0.44 收口（#178/#179 目标语义，2026-09-09）

- **§2 目标数量语义（更新 T-M2 产出口径）**：管线按"已解析目标"执行；目标数量由 `SkillExecutor` 在 M3 侧按派生表决定（见 data_schema 顶部 #178/#179 表）：单体伤害/any_ally = 候选池**选一**（实机玩家点选、headless 固定调用点随机、敌方 O-39 裁定槽序首个+taunt）、AOE = 全范围、team = 全体、双段 = 同一目标两段。本卡原"范围内全部非空"描述作废。
- **开放题收口**：O-32/33/34（#166~#168）已按 §6 组合行默认锁测（折扣优先/叠加/我方先手）；O-13（#179 双段同目标两段）已由"两段独立实例"用例覆盖（MultiSegment 断言）。
- **T-M2-10 对外数值 = 执行期生效值**：units.json 敌 HP **60/60/46/41**、skills.json #173 净值（cleave 0.9/lunge 0.8/收割 0.4/0.5/横扫 0.6/双段 0.5×2/charge 1.0）；§7.1 样本算式为旧口径仅供推导参考。
