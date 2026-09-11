# 功能包 02：完整日志系统 + UI 详情框（可观测性）任务卡

> **编号**：ARCH-T-FEAT-PACK-02 · **类型**：task · **状态**：可开工
> **上游**：[设计] `doc/modules/logging.md`（全文）· `doc/modules/ui_spec.md` §8（详情框）/ §6（反馈）· `doc/Project_Memory.md` §0-5（事件流 = 唯一事实来源）
> **决策引用**：#199（日志系统）/ #200（详情框）/ #201（敌方意图预留）/ **#217（G0a/G0b 拆分 + 执行顺序，v0.59）**
> **开放项**：~~O-54（日志事件字典补全）/ O-55（详情框）/ O-56（敌方意图预留）~~ → ⚠️ **架构师已改号（v0.50）**：**O-55（日志事件字典补全 G0）/ O-56（详情框 G3）/ O-57（敌方意图预留 G4）**——原 O-54 与 v0.49「数值补偿与 M 校准契约」重号（先到先得，pack02 引用面小者顺延）。见 `open_issues.md` 表头重号说明与 §6 回执。
> **依赖**：T-M2~T-M5（事件族/投影/UI 骨架）；与 `m6_fix_pack` / `feat_pack_01` **互不冲突，可并行**
> **最近更新**：2026-09-09

---

## 0. 目标与范围

| 子包 | 要解决的问题 | 用户原话 |
|---|---|---|
| **G0a**🔴 | **复测必需事件**：`SkillUseEvent`（"谁用了什么技能"）+ `TurnSkippedEvent`（含待命 `passed`）+ **基类补 `Round`** | 「完整的日志系统…谁对谁用技能」 |
| **G0b** | 其余 **12 类事件** + 补字段 6 处（buff 生命周期 / 回合开始 / 靠齐 / 胜负 / 敌方 AI 决策 / 折磨 proc / 属性减益 / 增援弹性 …） | 同上 |
| **G1** | 事件 → 中文可读文本，没有这一层 | 同上 |
| **G2** | 日志在 UI 上没有呈现 | 「开发者模式那种日志呗」 |
| **G3** | 悬停技能/单位/buff 看不到详情 | 「ui上需要有一个详情框，技能鼠标悬停或者敌人悬停的时候能看到相关详情」 |
| **G4** | 敌方意图切片不做，但要为后续「侦察」预留 | 「目前切片不,但是后续可能会有侦察技能」 |

> 🔴 **总纪律（`logging.md` §1）**：任何"UI 要显示的数字"**必须先从事件流能算出来**。
> 算不出来 = **事件字典缺项**——**不得为某个消费者单独造数据**。

> 🔴 **顺序约束（v0.59 / #217 重写）：G0 拆两批，G0a 前置、G0b 后置**
> - **G0a（复测必需，🔴 前置）**＝ **`SkillUseEvent` + `TurnSkippedEvent` + 基类 `Round`** —— P3 v3 报告字段直接依赖：
>   **技能使用率 ← `SkillUseEvent`**、**待命次数 ← `TurnSkippedEvent:passed`**（SP 分布 ← `SupportPointEvent` 已在 `feat_pack_04` S1；实测 D/E ← `DamageEvent` 已有）。
>   ⚠️ **不得用"策略侧单独计数"顶替**——违反 `logging.md` §1（*任何要显示的数字必须先从事件流能算出来*），且**实机没有策略层 → 与实机日志必然对不上**。
> - **G0b（其余 12 类 + 补字段）与 G1~G4 → 后置**（在 P3 v3 之后）。
> - **理由：复测是当前瓶颈，应尽量少拦它。**
> - **修正后执行顺序**：`m6_fix_pack P0~P3`（✅ 已完成，勿重做）→ **`feat_pack_02` G0a** → `feat_pack_03` D0~D7 → `feat_pack_04` S0~S9 → `feat_pack_01` F0~F4 → **P3 v3 复测** → `feat_pack_02` **G0b + G1~G4**。
> - **依赖关系**：G1/G2 以 G0a+G0b 的事件为输入；G3/G4 与 G0 无依赖，可随时并行。

---

# G0 · 事件字典补全（`logging.md` §3）

## G0.0 🔴 拆分：G0a（前置）/ G0b（后置）—— v0.59 / #217

| 批次 | 内容 | 为什么 | 时机 |
|---|---|---|---|
| **G0a** | ① **`SkillUseEvent`**（`Actor`·`CasterSlot`·`SkillId`·`TargetSlots[]`）② **`TurnSkippedEvent`**（`Reason` 含 **`passed`** = 待命）③ **基类 `BattleEvent` 补 `Round`** | **P3 v3 报告字段直接依赖**：技能使用率 ← `SkillUseEvent`；待命次数 ← `TurnSkippedEvent:passed` | 🔴 **前置**（P0~P3 之后立刻做，早于 D/S/F） |
| **G0b** | 其余 **12 类事件** + **补字段 6 处**（`SwapEvent`/`DamageEvent`/`EffectEvent`/`DeathEvent`/`HealEvent`/`DisplaceEvent`） | 完整日志与 DevLog 需要，但**不拦复测** | 复测后（G0b + G1~G4 一批） |

> ⚠️ **禁止用"策略侧单独计数"顶替 G0a**：违反 `logging.md` §1（*任何 UI 要显示的数字必须先从事件流能算出来；算不出来＝事件字典缺项，不得为某消费者单独造数据*）——且**实机没有策略层，单独计数与实机日志必然对不上**。
> ⚠️ **基类 `Round` 属于 G0a**：没有它，技能使用与待命统计**无法按回合归一**（技能使用率/待命次数都是"每回合"口径）。
> ✅ **验收（G0a 单列）**：任意一场对局，`count(SkillUseEvent) == 实际技能结算次数`（含支援/无伤害技能）；待命走 `TurnSkippedEvent(Reason:"passed")` 且**不消耗 SP、不是技能**（与 #189 不冲突）；按 `Round` 分组与 `RoundStartEvent` 完全一致；**新增事件不引入抽取**（同 seed 同命令流 `DrawCount` 一致）。

## G0.1 现状缺陷（代码依据）

| 缺陷 | 位置 |
|---|---|
| 🔴 **无 `SkillUseEvent`**——"谁用了什么技能"**无事件** | `core/events/BattleEventTypes.cs`（全 18 类无此项）；`HitEvent` 仅 `(Hit, HitRate, Attacker, Target)`、**无 SkillId** |
| 🔴 **buff 生命周期零事件** | `sim/buffs/BuffLedger.cs` 的 `Add/AddCharged/Remove/TickRounds` 全无 `_log.Append`；`EffectEvent` 只记掷骰 |
| 无 `TurnStartEvent` | 只有 `RoundStartEvent(Round)` |
| 跳过行动无声 | `TurnSequencer.NextActor` 对眩晕 `continue`，无事件 |
| 靠齐无事件 | `FormationBoard.CloseUp` 改位置但无事件 |
| 胜负无事件 | `BattleRoot.EndGame` 只 `GD.Print` |
| 敌方 AI 决策无事件 | `EnemyAi.Choose` 返回 `SkillChoice` 后直接执行 |
| 属性减益无事件 | 走 `UnitRuntime.AttackMod/ResilienceMod/SpeedMod`，**绕过 buff 台账** |
| 基类缺 `Round` | `BattleEvent` 只有 `Sequence` → 无法按回合分组 |
| `SwapEvent` 缺被调动者 | `SwapEvent(Actor, FromPos, ToPos)`——#181 实际移动的是 B |

## G0.2 规格

**新增 14 类事件**（字段为最小必需集，实现可加不可减；**批 = G0a/G0b**）：

| 事件 | 字段 | 级 | 批 |
|---|---|---|---|
| `BattleEndEvent` | `Outcome` · `Round` · `Reason` | 1 | G0b |
| `TurnStartEvent` | `Actor` · `Slot` · `EffectiveSpeed` | 1 | G0b |
| `TurnSkippedEvent` | `Actor` · `Reason`(`stunned`/`bound`/`no_usable_skill`/**`passed`**) | 1 | 🔴 **G0a** |
| **`SkillUseEvent`** | `Actor` · `CasterSlot` · `SkillId` · `TargetSlots[]` | 1 | 🔴 **G0a** |
| `SkillRefusedEvent` | `Actor` · `SkillId` · `Reason`(`affliction_fear`/`no_target`/`cooldown`/`per_battle`) | 1 | G0b |
| **`BuffAppliedEvent`** | `Source` · `Target` · `BuffId` · `DurationRounds` · `Stacks` | 1 | G0b |
| **`BuffRemovedEvent`** | `Target` · `BuffId` · `Reason`(`expired`/`dispelled`/`consumed`/`morale_reset`/`death`) | 1 | G0b |
| `AfflictionProcEvent` | `Unit` · `AfflictionId` · `ProcKind` · `Roll` · `Triggered` · `NewTargetSlot` | 1 | G0b |
| `CloseUpEvent` | `Moves[]`(`Unit` · `From` · `To`) | 2 | G0b |
| `MoraleEmberEvent` | `Unit` · `Kind`(`enter`/`exit`) | 2 | G0b |
| `EnemyDecisionEvent` | `Actor` · `SkillId` · `RuleIndex` · `RuleCondition` · `TargetSlots[]` | 2 | G0b |
| `ReinforcementElasticEvent` | `MFrom` · `MTo` · `Reason` | 2 | G0b |
| `StatModEvent` | `Target` · `Stat` · `Delta` · `DurationRounds` | 3 | G0b |
| `ObstacleEvent` | `Slot` · `Kind` | 3 | G0b |
| **`SupportPointEvent`** | `Delta` · `NewValue` · `Reason` | 2 | **`feat_pack_04` S1（不属本包，已在 SP 包实现）** |

**补字段 7 处**（`BattleEvent.Round` = **G0a**，其余 6 处 = G0b）：

| 事件 | 补 | 批 |
|---|---|---|
| `BattleEvent`（基类） | **`Round`**（导演 append 时盖章；`Sequence` 语义不变） | 🔴 **G0a** |
| `SwapEvent` | `MovedUnit` + `Kind`(`swap`/`reinforce`) | G0b |
| `DamageEvent` | `SkillId` | G0b |
| `EffectEvent` | `Source` | G0b |
| `DeathEvent` | `Cause` | G0b |
| `HealEvent` | `Source` · `SkillId` | G0b |
| `DisplaceEvent` | `SkillId` · `Source` | G0b |

> ⚠️ **兼容性**：基类加 `Round`、位置参数加尾参**不得破坏既有测试**（新增字段一律**默认值 + 追加在末尾**）。

## G0.3 改动点

| 文件 | 改动 |
|---|---|
| `core/events/BattleEventTypes.cs` | 新增 14 类 record |
| `core/events/BattleEvent.cs` | 加 `Round { get; init; }` |
| `core/events/CombatLog.cs` | `Append` 时盖章 `Round`（由导演传入当前回合，或 `CombatLog` 持有 `CurrentRound`） |
| `sim/buffs/BuffLedger.cs` | `Add/Remove/TickRounds` 增 `_log.Append(BuffApplied/Removed)` |
| `sim/pipeline/EffectsStep.cs` · `SkillExecutor.cs` | `stat_mod` 增 `StatModEvent` |
| `sim/director/BattleDirector.cs` | `SkillUseEvent`（玩家/敌方两路）、`TurnStartEvent`、`BattleEndEvent`、`SwapEvent.MovedUnit`、`ReinforcementElasticEvent` |
| `sim/turn/TurnSequencer.cs` | 眩晕/捆缚跳过 → `TurnSkippedEvent`（需注入 log） |
| `sim/board/FormationBoard.cs` 或导演侧 | `CloseUpEvent`（靠齐的逐槽 From→To） |
| `sim/enemy/EnemyAi.cs` | `EnemyDecisionEvent`（含 RuleIndex/条件/目标位） |
| `sim/pipeline/MoraleLedger.cs` | `MoraleEmberEvent` |
| `sim/survival/WeakDeathsDoor.cs` | `DeathEvent.Cause` |

## G0.4 验收

| 用例 | 期望 |
|---|---|
| **完整性** | 任意一次战斗的日志能**逐事件重建**最终盘面（位置/HP/士气/buff/虚弱/死门） |
| **技能可读** | 能查到"**谁 · 用【什么技能】· 对哪些槽位**" |
| **buff 可读** | 任一 buff 的**施加者 / 目标 / 持续 / 移除原因**均可查 |
| **Round 分组** | 每个事件都有正确 `Round`；按回合分组与 `RoundStartEvent` 完全一致 |
| **掷骰齐全** | 每次随机都有对应 `RngDraw`，`DrawCount` 单调 |
| **回归** | 既有 **178/179** 测试全绿（加字段不破坏既有断言） |
| **确定性** | 同 seed 同命令流 → 事件流**逐条一致**（含新事件） |

---

# G1 · 可读文本层

## G1.1 规格

- **纯函数** `事件 → 一行中文`，**零状态、零随机、零 Godot**，可被 headless 报告与 UI 复用。
- 位置：`scripts/core/events/EventText.cs`（events 的只读消费者工具，**不属内核规则**）。
- 命名：`我1` = 我方槽 1；`敌·施法者` = 敌方原型中文名；技能用「」。
- 🔴 **每行必须自带「谁 · 对谁 · 什么 · 数值 · 前后值」**，不允许只有"发生了某事"。

**验收样例（逐行照抄作为测试基准）**：

```
[回合 4] ── 轮到 政委（有效速度 10.3）
[回合 4] 政委 使用【战场鼓舞】→ 战士
[回合 4]   战士 士气 +15 → 78
[回合 4] 敌·施法者 使用【精神震荡】→ 我1、我2（AOE·精神）
[回合 4]   我1 命中｜精神伤害 9（精神减免 22%）｜士气 −5 → 45
[回合 4]   我2 命中｜精神伤害 9｜士气 −5 → 38
[回合 4] 我2 士气跨过 0 → 崩溃判定（33%）
[回合 4]   掷骰 27 → 【折磨·恐惧】；获得状态「恐惧」（持续到士气回 50）
[回合 4] 我2 使用技能被【恐惧】拒绝（掷骰 21 < 33）——不消耗行动
[回合 4] 敌·近战小兵 使用【重劈】→ 我1
[回合 4]   护盾格挡 → 本次物理完全无效（#156）
[回合 4] 我方 撤退成功（判定 68% → 掷骰 41）；全队士气 −10
[回合 4] ── 战斗结束：胜利（敌方全灭）
```

## G1.2 验收

| 用例 | 期望 |
|---|---|
| 覆盖 | 14 类新事件 + 既有事件**全部**有文本模板（无 "TODO"/裸类型名） |
| 规格 | 每行含"谁·对谁·什么·数值·前后值" |
| 纯函数 | 同输入同输出；无副作用；可脱离 Godot 单测 |
| 缺名回退 | 未知 id → 显示原始 id 而非抛异常 |

---

# G2 · DevLog 控制台（`ui_spec.md` / `logging.md` §6）

## G2.1 规格

| 项 | 规格 |
|---|---|
| 入口 | **按键开关（建议 `F2`）**，默认隐藏 |
| 呈现 | 半透明覆盖层 + **等宽字体** + 纵向滚动 + 自动滚到底；**不做美术** |
| 分组 | 按**回合**分组（靠基类 `Round`），回合标题可折叠 |
| 过滤 | ① 分级（1~4，**默认 1~2**）② 关键字 ③ 单位 |
| 冻结 | 「暂停跟随」：滚动后停止自动滚底 |
| 配色 | 伤害（**物理 vs 精神必须可分**）/ 士气（升绿降红）/ 状态（黄）/ 系统（灰）/ 掷骰（暗） |
| 导出 | ① 复制全文到剪贴板 ② 写 `user://logs/battle_<seed>.log` |
| 性能 | **增量 append**（只渲染新增）；上限 5000 条，超出丢最旧；**关闭时零开销** |

## G2.2 红线

- 🔴 **只读**：DevLog 不得反向调用导演/内核，**不得产生任何抽取或状态变更**。
- 🔴 **不得每帧重建全量文本**：文本用 G1 纯函数生成并**缓存**，事件到达时增量 append。

## G2.3 改动点与验收

| 文件 | 改动 |
|---|---|
| `scripts/ui/DevLogPanel.cs`（新） | 覆盖层 + 滚动 + 过滤 + 导出 |
| `scripts/ui/BattleUi.cs` | 挂载 + 按键绑定 |

| 用例 | 期望 |
|---|---|
| 开关 | `F2` 开关；默认隐藏 |
| 分级 | 默认不含级 3/4；切到级 4 能看到全部掷骰 |
| 只读性 | 开关/滚动/过滤后，事件流内容与 `Sequence` **不变** |
| 性能 | 500 场 headless 不受影响；关闭时零开销 |
| 导出 | 导出内容与内存事件流**逐条一致** |

---

# G3 · 详情框（`ui_spec.md` §8）

## G3.1 规格

**三类悬停共用一套组件**：

| 对象 | 内容 |
|---|---|
| **技能按钮** | 名称 · 距离轴 · 伤害轴 · 倍率 · 命中/暴击修正 · 段数 · 附加效果+概率 · 位移 · 使用限制 · 士气影响 · 标签 · **可用位置** · **当前可用性及原因** · **对候选池每个目标的命中率 + 伤害预估** |
| **单位（我方）** | 名称 · HP · 士气 · 速度 · 攻击 · 物防 · 韧性 · 四类抗性 · buff 列表（可继续悬停）· 虚弱/死门 · 折磨或美德 · 技能池 |
| **单位（敌方）** | 🔴 **全暴露**：同上 + **完整技能表**（**默认折叠**，点开展开） |
| **buff 图标** | 名称 · 极性 · 来源（含技能名）· 剩余回合/层数/**次数** · **数值化效果** · 可否驱散 |

**交互**：触发延迟 **150~250ms** 防抖 · 跟随鼠标 + **边界避让** · **层级最高** · 内部滚动（≤70% 屏高）· 移开即关（可选点击锁定）。

## G3.2 改动点与验收

| 文件 | 改动 |
|---|---|
| `scripts/ui/DetailTooltip.cs`（新） | 三类内容渲染 + 防抖 + 边界避让 |
| `scripts/gameplay/sim/director/BattleProjector.cs` | 补投影：**单位完整属性**（含四类抗性）、**敌方技能表**、**buff 明细**（来源/剩余/数值） |
| `scripts/ui/BattleUi.cs` | 技能按钮 / 单位卡 / buff 图标挂 hover 事件 |

| 用例 | 期望 |
|---|---|
| 技能悬停 | 含倍率/命中/暴击/附加+概率/位移/限制/可用位置/可用性原因；攻击技能给出**每个候选目标**的命中率+伤害预估 |
| 单位悬停（我方） | 全属性 + 士气 + buff + 虚弱/死门 + 折磨/美德 |
| 单位悬停（敌方） | **全暴露**（精确 HP/抗性）+ 技能表**默认折叠**、可展开 |
| buff 悬停 | 名称/极性/来源/剩余/数值化效果/可否驱散 |
| 防抖 | 快速扫过**不闪**；停留 150~250ms 才出现 |
| 边界 | 贴屏幕边缘时详情框**自动翻转**不越界 |
| 只读 | 悬停**不改变任何状态**（无抽取、无写入） |

---

# G4 · 敌方意图预留接口（#201）

> 🔴 **切片不显示意图**，但架构必须预留——否则后续加「侦察」技能要动内核。

| 项 | 规格 |
|---|---|
| ① `EnemyAi` | 提供**不掷骰的预览路径**（只读快照 + 同源决策）。**否则预览会偷走随机数、破坏确定性**（`blueprint` §8 铁律） |
| ② 投射层 | 预留 `IntentProjection(UnitId) → (SkillId, TargetSlots) \| Unknown` |
| ③ 单位视图 | 预留 `IntentRevealed` 标记（默认 `false`） |
| ④ UI | 意图区块默认显示「**未知（需侦察）**」；`true` 时显示下一步技能 + 目标位 |

**验收**：

| 用例 | 期望 |
|---|---|
| 预览不掷骰 | 调用 `IntentProjection` **前后 `rng.DrawCount` 不变**，事件流不新增 |
| 默认隐藏 | `IntentRevealed=false` → UI 显示「未知（需侦察）」 |
| 可解锁 | 测试夹具置 `true` → 显示正确的下一步技能与目标位 |
| 同源性 | 预览结果与"真到它行动时实际选的技能"**在状态未变时一致** |

---

# 5. 红线与协作

| 红线 | 说明 |
|---|---|
| **日志只读** | 任何消费者（UI/导出/统计）**不得反向写内核或产生抽取** |
| **不显示 ≠ 不记录** | 级 4（`RngDraw`/DrawCount）必须记录，仅默认不显示 |
| **不为消费者造数据** | UI 要的数字必须能从事件流算出；算不出＝补事件字典 |
| **确定性不受影响** | 新增事件**不得**引入新抽取；预览路径必须无 rng |
| **加字段向后兼容** | 一律尾部追加 + 默认值，不破坏既有 178/179 测试 |
| **一步一提交** | **`G0a` / `G0b` / `G1` / `G2` / `G3` / `G4` 独立提交**（v0.59：G0 拆两批，便于"复测前 / 复测后"二分） |
| **架构侧需镜像** | ✅ **已完成（v0.50，见 §6）**：`data_schema` **§8 事件族清单**（14 新事件 + 补字段 7 处 + 4 级 + 消费者红线）+ **P17 事件字典完整性校验**；`blueprint` **§6.2 事件流说明**（事件字典指向 + 只读/不造数据/`Round`/`SkillUseEvent` 统计前提 + 兼容性）；`open_issues` **O-55/O-56/O-57**（原 O-54~O-56 顺延，见卡头改号说明） |

---

# 6. 架构镜像记录（v0.50，架构师）

> 回执本包 §5「架构侧需镜像」+ 编号/排序事项。**本包 G0~G4 规格未改动**，仅改号、补排序约束与回执。

| 项 | 回执 |
|---|---|
| **编号改号（重号处理）** | ⚠️ 本包原提案 **O-54/O-55/O-56** 与 v0.49 已发布的 **O-54「数值补偿与 M 校准契约」重号**（后者已被 data_schema P16 / m6 卡 / README / feat_pack_01 §5.1 引用）→ 架构师按"先到先得 + 引用面最小者改号"原则**顺延为 O-55（事件字典 G0）/ O-56（详情框 G3）/ O-57（敌方意图 G4）**；`open_issues.md` 表头已记录重号说明 |
| **data_schema 镜像** | ✅ 新增 **§8 事件族清单**（运行时契约，非 JSON 配置）：§8.1 现状与缺口 / §8.2 **新增 14 类**（字段+级）/ §8.3 **补字段 7 处**（尾部追加+默认值）/ §8.4 **4 级分级**（级 4 必须记录、默认隐藏）/ §8.5 消费者与只读红线 / §8.6 **P17** / §8.7 与 ui_spec·combat_math·verification·Project_Memory 的边界 |
| **blueprint 镜像** | ✅ **§6.2** 增：事件字典指向（data_schema §8）、🔴 纪律（UI 要的数字必须先从事件流算出；不得为消费者造数据）、基类 `Round` 必带、`SkillUseEvent` 是统计前提、只读红线（不得反向写内核/产生抽取/改变 Sequence）、新增字段尾部追加兼容性；并明确**投影链 vs 事件流链互不替代**（详情框读投影、DevLog 读事件流） |
| **open_issues 镜像** | ✅ 新增 **O-55/O-56/O-57**；**O-54 扩展** —— 并入 **#198 自适应 M**（`M ∈ [M_base, M_base+3]`、K=2 窗口、≥3/4 未用 `output` → M+1、首波固定、满编增益每波叠加无上限）；周期说明更新为 O-35~O-57 |
| **data_schema 连带** | ✅ §3.7 `overtime_reinforcement` 增 `elastic{enabled,k_rounds:2,idle_output_slots:3,max_bonus:3}` 与 `ReinforcementElasticEvent`（级 2）；**P16** 扩展为"#196+#198"（弹性参数范围、`M ∈ [M_base, M_base+max_bonus]`、首波不受弹性影响、每次变动写事件） |
| **G0 优先（采纳）** | ✅ 已写入本包卡头顺序约束 + README §4 红线/§6 交接语：**G0 → G1/G2**；理由 = G1/G2 以事件为输入 + **M6「技能使用率」KPI 依赖 `SkillUseEvent`**；`m6_verification` T-M6-01/04 已挂该依赖 |
| **未代写项（留给策划）** | `ui_spec.md` §9「强烈建议做敌方意图」需改为「预留不启用」（#201）；`verification.md` 的「技能使用率」KPI 建议注明"依赖 `SkillUseEvent`（O-55）"；玩家向叙事化战斗记录切片不做（已定） |

---

# 7. 架构镜像记录（v0.59 增补，架构师）

> 回执 `CHANGELOG v0.59` / `state.md` **#217**（G0 拆 G0a/G0b + 执行顺序）。

| 项 | 回执 |
|---|---|
| **拆分落点** | ✅ 卡头**顺序约束已重写**（G0a 前置 / G0b 后置）；**§0 范围表** G0 → **G0a/G0b** 两行；新增 **§G0.0 拆分表**（内容 / 理由 / 时机 / 单列验收）；**§G0.2 事件表加「批」列**（`SkillUseEvent`、`TurnSkippedEvent` = **G0a**，其余 12 类 = G0b；并补记 `SupportPointEvent` 属 `feat_pack_04` S1，不重复实现）；**§G0.2 补字段表加「批」列**（**`BattleEvent.Round` = G0a**，其余 6 处 = G0b） |
| **为什么 `Round` 属 G0a** | 技能使用率与待命次数都是**"每回合"口径**；无基类 `Round` 只能靠 `RoundStartEvent` 反推，**统计无法按回合归一** → 与复测报告字段直接相关 |
| **G0a 的真实工作量（重要澄清）** | 代码侧 **G0 第一批/第二批已提交**（`cd90cca` / `63139de`，随后 `008eacf` G1、`554c7ae` G2~G4）。故 **G0a 的实质 = 确认这三件事成立**：① `SkillUseEvent` 已由内核发射且**报告读事件流**（此前报的"技能使用事件 17522"**疑为策略侧单独计数** → 必须排除，否则实机对不上）；② 基类 `Round` 已盖章且按回合分组一致；③ `TurnSkippedEvent` 的 **`Reason:"passed"` 发射点随 `feat_pack_04` **S5.2（待命）** 落地** —— 事件类型/枚举属 G0a，**发射点属 SP 包**，两边文件不同**可并行、不得互相等待** |
| **镜像同步** | `data_schema` **§8.2** 已把 `TurnSkippedEvent.Reason` 记为含 `passed`、`SupportPointEvent` 为第 15 类；**P17** 的"技能可读/buff 可读/Round 单调/不引入抽取"即 G0a 的校验落点；`m6_verification` T-M6-01 已注明"**技能使用率 ← `SkillUseEvent`**、**缺它时 KPI 不得以空值通过**" |
| **执行顺序（已写入 README）** | `m6_fix_pack P0~P3`（✅ 已完成，勿重做）→ **`feat_pack_02` G0a** → `feat_pack_03` D0~D7 → `feat_pack_04` S0~S9 → `feat_pack_01` F0~F4 → **P3 v3 复测** → `feat_pack_02` **G0b + G1~G4** |
| **红线补充** | 本包 §5「一步一提交」由 `G0/G1/G2/G3/G4` 细化为 **`G0a / G0b / G1 / G2 / G3 / G4`** 独立提交（便于复测前后二分） |
