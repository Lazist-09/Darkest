# 日志与可观测性（模块）

> **为什么这份文档存在**：本作是**位置驱动 + 多层随机 + 多段结算**的系统，一次攻击可能产生
> 「命中 → 暴击 → 逐段伤害 → 士气 → 附加效果 → 位移 → 虚弱/死门 → 崩溃判定」近十个事件。
> **看不见中间步骤，玩家与测试都无法判断"结果是不是对的"**——日志不是附属品，是**可读性的基础设施**。
>
> 主文档：`GDD.md` §4（结算流程）；关联 `ui_spec.md` §10、`combat_math.md`、`Project_Memory.md` §0。
> 决策：**#199（日志系统）/ #201（敌方意图预留）**。架构落点见 `architecture/tasks/feat_pack_02_observability.md`。

---

## 1. 定位：三种消费者，一份事实来源

`CombatLog` 是 **append-only 不可变事件流**，`Sequence` 单调递增，**一处写入、处处只读**
（`blueprint` §6.2/§8.3 已立）。三类消费者读**同一份**：

| 消费者 | 用途 | 需求 |
|---|---|---|
| **UI（DevLog 控制台）** | 开发/调试/试玩复盘 | 完整、可过滤、可导出 |
| **回放** | 同 seed 逐事件比对 | 不可变、顺序确定、含全部掷骰 |
| **统计（Monte Carlo）** | KPI / 平衡数据 | 可程序化查询、字段完整 |

> 🔴 **纪律**：日志**不得**为某一类消费者单独造数据。任何"UI 要显示的数字"必须**先从事件流里能算出来**；
> 算不出来 → 说明**事件字典缺项**（本文档 §3 就是补这项）。

---

## 2. 现状缺口（核实于 2026-09-09）

事件族 `core/events/BattleEventTypes.cs` 现有 **18 类**，架构正确，但**读不出完整流程**：

### 2.1 🔴 缺失的事件

| 缺什么 | 后果 |
|---|---|
| **`SkillUseEvent`** | **没有任何事件记录"谁用了哪个技能"**——`HitEvent` 只有 Attacker/Target、**无 SkillId**。所以"谁对谁用了什么"读不出来 |
| **buff 生命周期** | `EffectEvent` 只记掷骰（类型/概率/是否触发）；`BuffLedger` 的 Add/Remove/Expire **零事件** → "谁给谁上了什么、几层、持续多久"全无 |
| **`TurnStartEvent`** | 只有 `RoundStartEvent` → 回合内"轮到谁"无事件 |
| **跳过行动** | 眩晕跳过、技能全不可用空过 → **无声消失** |
| **靠齐（CloseUp）** | 死亡后向中靠齐改位置 → **无事件** |
| **`BattleEndEvent`** | 胜负无事件，只在 `BattleRoot.EndGame` 里 `GD.Print` |
| **敌方 AI 决策** | 选了哪个技能/命中哪条规则 → 无事件（只有 15% 随机的 `RngDraw`） |
| **折磨 proc** | 恐惧拒绝技能/自私拒绝治疗/失控换目标（#193 将接入）→ 需新事件 |
| **属性减益** | 走 `UnitRuntime` 的 Mod 字段、**绕过 buff 台账** → 无事件 |
| **增援弹性调整** | #198 的 M 浮动 → 需记 M 旧值/新值/判定依据 |

### 2.2 ⚠️ 字段缺口

| 项 | 问题 |
|---|---|
| `BattleEvent` 基类 | **只有 `Sequence`，无 `Round`** → 日志无法按回合分组，只能靠 `RoundStartEvent` 反推 |
| `SwapEvent(Actor, FromPos, ToPos)` | **未记录被调动者 B**（#181 实际移动的是 B）→ 增援日志半盲 |
| `DamageEvent` | 无 `SkillId` → 多段伤害无法归属技能 |
| `EffectEvent` | 无 `Source`（施加者） |
| `DeathEvent` | 无 `Cause`（死因） |

---

## 3. 事件字典（补全后）

> 字段为**最小必需集**；实现可加，不可减。**"级"见 §4**。

### 3.1 新增事件（14 类）

| 事件 | 字段 | 级 |
|---|---|---|
| `BattleEndEvent` | `Outcome`(Victory/Defeat/Retreat) · `Round` · `Reason` | 1 |
| `TurnStartEvent` | `Actor` · `Slot` · `EffectiveSpeed` | 1 |
| `TurnSkippedEvent` | `Actor` · `Reason`(`stunned`/`bound`/`no_usable_skill`) | 1 |
| **`SkillUseEvent`** | `Actor` · `CasterSlot` · `SkillId` · `TargetSlots[]` | 1 |
| `SkillRefusedEvent` | `Actor` · `SkillId` · `Reason`(`affliction_fear`/`no_target`/`cooldown`/`per_battle`) | 1 |
| **`BuffAppliedEvent`** | `Source` · `Target` · `BuffId` · `DurationRounds` · `Stacks` | 1 |
| **`BuffRemovedEvent`** | `Target` · `BuffId` · `Reason`(`expired`/`dispelled`/`consumed`/`morale_reset`/`death`) | 1 |
| `AfflictionProcEvent` | `Unit` · `AfflictionId` · `ProcKind`(`refuse_skill`/`refuse_heal`/`randomize_target`) · `Roll` · `Triggered` · `NewTargetSlot` | 1 |
| `CloseUpEvent` | `Moves[]`（`Unit` · `From` · `To`） | 2 |
| `MoraleEmberEvent` | `Unit` · `Kind`(`enter`/`exit`) | 2 |
| `EnemyDecisionEvent` | `Actor` · `SkillId` · `RuleIndex` · `RuleCondition` · `TargetSlots[]` | 2 |
| `ReinforcementElasticEvent` | `MFrom` · `MTo` · `Reason`(`not_all_out`/`recovered`) | 2 |
| `StatModEvent` | `Target` · `Stat` · `Delta` · `DurationRounds` | 3 |
| `ObstacleEvent` | `Slot` · `Kind`(`placed`/`destroyed`/`pushed`) | 3 |

### 3.2 补字段（既有事件）

| 事件 | 补 |
|---|---|
| `BattleEvent`（基类） | **`Round`**（导演 append 时盖章；`Sequence` 语义不变） |
| `SwapEvent` | `MovedUnit`（被调动的 B）+ `Kind`(`swap`/`reinforce`) |
| `DamageEvent` | `SkillId` |
| `EffectEvent` | `Source` |
| `DeathEvent` | `Cause`（`killed_by:<unit>` / `deaths_door` / `self_damage` / `bleed`） |
| `HealEvent` | `Source` · `SkillId` |
| `DisplaceEvent` | `SkillId` · `Source` |

---

## 4. 分级（默认视图只显示 1~2 级）

| 级 | 名称 | 内容 | DevLog 默认 |
|---|---|---|---|
| **1** | 叙事 | 回合开始 / 轮到谁 / 技能使用 / 命中与伤害 / buff 施加·移除 / 士气 / 死门·崩溃·死亡 / 增援 / 胜负 | ✅ 显示 |
| **2** | 关键判定 | 暴击 / 位移（含失败原因）/ 抗性命中 / 敌方 AI 决策 / 靠齐 / 余烬 | ✅ 显示 |
| **3** | 机制细节 | 逐段伤害 / 属性减益逐项 / 障碍 | ⬜ 折叠 |
| **4** | 调试 | `RngDraw`（每次掷骰 + DrawCount）/ 序列号 / 状态快照 | ⬜ 隐藏（按键开） |

> 🔴 **级 4 与确定性纪律的关系**：`RngDraw` **必须**在事件流里（回放/审计需要），
> 但**默认不显示**——它不是给玩家看的。**"不显示" ≠ "不记录"**。

---

## 5. 可读文本层

**规格**：纯函数 `事件 → 一行中文`，**零状态、零随机、零 Godot**（可被 headless 报告与 UI 复用）。
放在 `scripts/core/events/EventText.cs`（events 的只读消费者工具，不属内核规则）。

**命名约定**：`我1` = 我方槽位 1；`敌·施法者` = 敌方原型中文名；技能用「」（配 `unit_names` / `skill_names` 解析）。

**目标效果（验收样例）**：

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

> 规格要求：**每一行必须自带"谁·对谁·什么·数值·前后值"**，不允许只有"发生了某事"。

---

## 6. DevLog 控制台（UI 形态）

**形态**：开发者模式日志——**按键呼出的覆盖层**，不做美术、不做玩家向叙事化。

| 项 | 规格 |
|---|---|
| 入口 | 按键开关（建议 `F2`）；默认隐藏 |
| 呈现 | 半透明覆盖层 + **等宽字体** + 纵向滚动 + 自动滚到底 |
| 分组 | 按**回合**分组（靠基类 `Round`），回合标题可折叠 |
| 过滤 | ① **分级**（1~4，默认 1~2）② **关键字**（模糊匹配）③ **单位**（只看某单位相关） |
| 冻结 | 「暂停跟随」开关：滚动后停止自动滚底，便于阅读（⇧ 键或按钮） |
| 配色 | 按类别：伤害（物理/精神**必须可分**，见 `ui_spec.md` §6）/ 士气（升绿降红）/ 状态（黄）/ 系统（灰）/ 掷骰（暗） |
| 导出 | ① 复制全文到剪贴板 ② 写 `user://logs/battle_<seed>.log`（供试玩报告与 bug 单） |
| 性能 | **增量追加**（只渲染新增事件）；上限 5000 条，超出丢弃最旧（或虚拟化） |
| 零影响 | **只读事件流**，不得反向调用导演/内核（不得因此产生任何抽取或状态变更） |

> 🔴 **性能红线**：DevLog **不得**在每帧重建全量文本。事件到达时增量 append；文本用 §5 的纯函数生成并**缓存**。

---

## 7. 与其它模块的边界

| 模块 | 关系 |
|---|---|
| `ui_spec.md` §10 详情框 | 详情框读的是**投影**（当前状态），DevLog 读的是**事件流**（历史）。两者数据源不同、互不替代 |
| `combat_math.md` §9 随机 | 每次掷骰先 `RngDraw` 再业务事件（既有约定），级 4 可见 |
| `verification.md` | KPI 统计**直接查事件流**；本文档是"统计能否算出来"的前提 |
| `Project_Memory.md` §0-5 | 事件流 = 战斗日志 = 回放源 = 统计源（一处写入、处处只读） |

---

## 8. 验收

| 用例 | 期望 |
|---|---|
| **完整性** | 任意一次战斗的日志，能**逐事件重建**出最终盘面（位置/HP/士气/buff/虚弱/死门） |
| **技能可读** | 日志里能找到"**谁·用了什么技能·对哪些槽位**"（`SkillUseEvent` 存在且被引用） |
| **buff 可读** | 任一 buff 的**施加者、目标、持续、移除原因**都能查到 |
| **掷骰齐全** | 每次随机都有对应 `RngDraw`，且 `DrawCount` 单调（确定性审计） |
| **分级正确** | 默认视图不含级 3/4；切到级 4 能看到全部掷骰 |
| **文本规格** | §5 的每一行都含"谁·对谁·什么·数值·前后值" |
| **导出** | 能导出完整日志且**与内存事件流逐条一致** |
| **只读性** | 打开/关闭 DevLog、滚动、过滤，**不改变事件流内容与顺序**（Sequence 连续） |
| **性能** | 500 场 headless 不受影响；实机 DevLog 关闭时零开销 |

---

## 9. 待确认（不阻塞）

| 议题 | 说明 |
|---|---|
| 玩家向的"战斗记录"（叙事化） | 用户已定**切片不做**（开发者模式日志即可）；后续若要，复用同一事件流另做一套文本模板即可 |
| 日志中文化 vs 双语 | 现在全中文；若未来上 Steam 英文，`EventText` 应为模板 + 参数（便于替换语言包） |
| 是否记录"敌方内部状态快照" | 级 4 可选，用于排查 AI 决策 |
