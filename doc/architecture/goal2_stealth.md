# 第 2 刀交付报告 — C-1 Stealth 潜行

**日期**：2026-09-18
**指令**：用户「进行」（紧接选型问答：**① 使用 `state_flag` ② 豁免**）
**验收**：`dotnet build` **0 错误** · `dotnet test` **598 通过 / 0 失败**（基线 587 + 新增 11）
**清单**：`doc/architecture/dd_replication_roadmap.md` C-1 → ✅ 完成

---

## 1. 交付内容

### 1.1 数据层

| 文件 | 改动 |
|---|---|
| `darkest/data/buff_defs.json` | 新增 `stealth` buff（第 22 项）：`polarity: positive` · `duration: rounds/2` · `stack: refresh` · `modifiers: [{kind: state_flag, effect: stealth}]` · `hooks: [{timing: before_enemy_ai, effect: …}]` · **`extra_rules` 含 `duration_rounds_placeholder: true`** · `source` 逐条标注原版出处 |

**为何走 `state_flag`**：`BuffDefsConfig.Validate` 的 `ConsumedEffectNames` 白名单**只约束 `DamageMod` / `ProbMod`**（`Validate` 内 `continue` 提前跳过其余 kind）⇒ 写 `state_flag: stealth` 不触发加载报错，且语义天然自洽（施加潜行的人 = 潜行者）。

### 1.2 代码层

| 文件 | 改动 |
|---|---|
| `scripts/data/SkillsConfig.cs` | `SkillEffectType` 增 `Stealth`；`FuncTag` 增 `IgnoreStealth`（对齐原版 `.ignore_stealth`）；**两个 `LowerEnumJsonConverter` 映射同步补齐**（成员必须给全，否则序列化 KeyNotFound） |
| `scripts/gameplay/sim/skill/SkillTargetResolver.cs` | 新增 `public const string StealthFlag = "stealth"`；`Resolve(...)` 新增**可选** `IBuffLedger? buffs = null`；新增私有 `IsStealthed(...)` |
| `scripts/gameplay/sim/enemy/EnemyAi.cs` | `ResolveTargets` / `IsSkillUsable` 传入 `buffs`（候选池过滤） |
| `scripts/core/contracts/ISkillUseResolver.cs` | `AvailabilityReason` 增 `TargetStealthed`；`TooltipFor` 增 `"目标处于潜行"` |
| `scripts/gameplay/sim/skill/SkillUseResolver.cs` | `SkillUseContext` 增可选 `IBuffLedger? Buffs = null`；③ 处区分 `TargetStealthed` / `NoTarget` 文案 |
| `scripts/gameplay/sim/skill/SkillExecutor.cs` | `Resolve` 传 `_buffs`；新增 **De-Stealth**（`ignore_stealth` 技能 → `ClearStateFlag(u,"stealth")` + `EffectEvent("unstealth")`）；`ExecuteSupportPath` 新增 `SkillEffectType.Stealth` 挂载分支 |
| `scripts/gameplay/sim/director/BattleProjector.cs` | `Skill(...)` 传 `Buffs: _director.Buffs`（D 栏灰显） |
| `scripts/gameplay/sim/run/SimplePlayerAuto.cs` | 3 处调用传 `director.Buffs` |
| `scripts/gameplay/scene/BattleRoot.PlayerActions.cs` | 4 处调用传 `Director.Buffs` |
| `scripts/ui/BattleUI.cs` | 1 处调用传 `d.Buffs` |
| `darkest/tests/StealthTests.cs` | **新增 11 个测试** |

**兼容性设计**：`IBuffLedger? buffs = null` 为**可选默认参数** ⇒ 22 个历史 `Resolve` 调用点与全部测试**一行未改**仍编译、行为逐字不变（`buffs == null` 时完全跳过潜行过滤）。这是本次没有产生 churn 的关键。

---

## 2. 🔴 我在侦察中发现的自身错误（如实上报）

**路线图与 goal1 文档里 C-1 的出处写的是「wiki 4.6」—— 这是误引。**

`doc/modules/dd_reference.md` 的 **4.6 实为 Riposte 反击**（属"刻意不抄"项），与 Stealth 无关。全仓多形态检索确认**改动前没有任何 stealth 规格文档**。

**已修正**（3 处）：
- `doc/architecture/dd_replication_roadmap.md` C-1 行 → 改标真实出处
- `doc/architecture/goal1_combat_relocation.md:145` → 同步修正
- `doc/modules/dd_reference.md` → **新增 §5.5 潜行**（规格 + 原版字段实据 + 未实现清单）

---

## 3. 🔴 测试跑出来的一个真实行为分歧（比原假设更准）

我最初把规则②实现成"**AOE 剔除潜行者但可穿过**"，测试**当场失败**（实际候选 `[2,3]`，期望 `[1,2,3]`）。

回查权威规格后发现**原版真实行为与我原先的假设不同**：

> Stealthed monsters **can be hit** with attacks that hit multiple targets.
> However, such multi-target abilities **must be targeting at least one non-Stealthed target**.
> Hitting a stealthed unit with an area-of-effect attack **will not de-stealth**, except for Rallying Flare.
> — `darkestdungeon.wiki.gg/wiki/Stealth_(Darkest_Dungeon)`

即：**AOE 打到潜行者是"能命中、留在命中列表里、但不解除潜行"**，而非"剔除"。只有 **Rallying Flare 是唯一能 AOE 解潜行**的技能。

⇒ 实现已改为实证版本（多目标技能整池放行，仅在全潜行时整技能不可用），测试同步锁定。
**这条我原先想错了，是测试把它抓出来的 —— 记在报告里备查。**

---

## 4. 四条规则 → 落点对照

| # | 规则 | 权威出处 | 落点 |
|---|---|---|---|
| ① | 潜行者**不可被直接指定** | wiki + 补丁说明 | `SkillTargetResolver` 候选池（带台账时） |
| ② | **多目标技能可穿过**，但须"至少 1 个非潜行者" | wiki 原文（Grapeshot 对全体潜行 Swine Gorers 不可用） | 同处：`hits.Any(非潜行)` 为假 ⇒ 空 ⇒ NoTarget |
| ③ | **Bypass Stealth 可指定 + De-Stealth** | 补丁 + `.unstealth 1` 实据 | `FuncTag.IgnoreStealth` 豁免；`SkillExecutor` → `ClearStateFlag` |
| ③b | **AOE 不解除潜行** | wiki 修订（唯一例外 Rallying Flare） | De-Stealth 只挂 `ignore_stealth` 技能 ✓ |
| ④ | 持续 **2 回合** | wiki + 补丁 + `.stealth 1 .duration 2` 实据 | `buff_defs.json` `rounds/2`（⚠️ `placeholder: true`） |

**原版一手实据（`borrow/` 解包）**：
- `borrow/3440502939/effects/playwright.effects.darkest:26` → `.stealth 1 .duration 2`
- 同文件 `:5-6` → `.stun 1 .unstealth 1`（De-Stealth 与 stun 同挂）
- `borrow/3440502939/heroes/playwright/playwright.info.darkest` → `.ignore_stealth true`
- `borrow/3424145711/project.xml:126/139` → `"Stealth Self (2 Rds)"` / `"+30% DMG while Stealthed"`

---

## 5. ✅ C-1a 已全部收口（2026-09-20）

原先登记的 5 项**全部关闭**，本报告随之从"部分交付"转为**完整交付**：

| 项 | 结果 | 依据 / 实现 |
|---|---|---|
| **持续回合数** | ✅ **2**（定案） | 原版一手实据 `borrow/3440502939/effects/playwright.effects.darkest`：`.stealth` 效果 duration 分布 **1×2 / 2×4 / 3×2** ⇒ 取最常见的 2。🔴 **原版是技能级、随阶递增**（`flying knife combo` zero~two ⇒ 2，three/four ⇒ 3）⇒ 高阶 3 属**技能阶**维度，归 **M8.1**（`#283` 7.6 已禁本轮做），buff 上留 `extra_rules.duration_is_per_skill_tier` 标记该待办 |
| **De-Stealth 时机** | ✅ **真·命中即解除** | 新增 `SkillExecutor.ApplyDeStealthOnHits`：结算后扫**本次新增**的 `HitEvent`，只对 `Hit == true` 的目标清 flag。⇒ 未命中**保留**潜行；同目标多段只解除一次；纯支援/移动技能不会误解除（无 `HitEvent` 即不动） |
| **潜行期间增益** | ✅ **+30% DMG / +10 ACC** | `buff_defs.json` 补 `damage_mod: dealt_damage_mult 30` + `prob_mod: hit_mod 10`；两个 effect 名本就在白名单 ⇒ 无需改白名单代码 |
| **敌人开场潜行** | ⏸ **刻意不配发** | 不是遗漏：我方 3 个**通用**原型与 DD 的 6 种潜行敌人**不是同一套单位概念**（硬套 = 编造数据）；`enemy_ai.json` 也无开场 buff 字段。真正需要时属 **M8 内容层** |
| **我方 `ignore_stealth` 技能** | ✅ **已配发** | 按原版精神（泼洒型火器穿过潜行）配给**敌方 AOE 输出** `caster_mental_shock`。⚠️ `target.side` 是「打谁」不是「谁打」—— 归属看 `owner_unit` 阵营 |

**新增测试**：`tests/StealthFollowUpTests.cs`（7 例）—— 持续回合定案 + 命中/未命中/无标签/纯支援 四种 De-Stealth 分支 + 标签配发合法性 + 开场潜行"刻意不配发"锁死。

---

## 6. 验证证据

```
dotnet build darkest/Darkest.csproj          → 0 个错误（13 个既有警告，未新增）
dotnet build darkest/Darkest.Tests.csproj    → 0 个错误
dotnet test  darkest/Darkest.Tests.csproj    → 已通过! 失败 0，通过 628，总计 628
                                              （C-1 基线 587；C-1a 收口后 628，新增 StealthFollowUpTests 7 例）
dotnet test --filter "FullyQualifiedName~StealthTests"      → 13/13 通过
dotnet test --filter "FullyQualifiedName~StealthFollowUpTests" → 7/7 通过
```

**测试覆盖**：
- 数据层：buff 定义合法性 + 枚举往返序列化
- ① 唯一潜行目标 ⇒ 无候选；未传台账 ⇒ 原行为
- ② 多目标穿过（含潜行者在命中列表）+ 全潜行 ⇒ 整技能不可用
- ③ `ignore_stealth` 在**全体潜行**下仍可用
- ④ 障碍位不受潜行过滤影响（#73/#77/#105）
- 可用性：`TargetStealthed` 文案 + 真·空池仍报 `NoTarget`（不误报）
- 台账原语：`HasStateFlag`/`ClearStateFlag` + 2 回合自动到期

---

## 7. 下一刀（已改排）

~~H-1 装备 0–4 阶 / H-2 技能 0–4 阶~~ ⇒ 🔴 **已改走 `D-1`/`D-2` 地牢层**（§7 ⑤ 裁定）。

原因：`m8_roadmap §1.3 ④` 明确「暂不做 Blacksmith/Guild」，且 H-2 要放行的 `skill_upgrade` 正是 `#283` 7.6 **刻意禁**的
⇒ 养成两项**受上游制约反而不能先做**。`skill_upgrade` 的"待解"已被查清为**误标**，非占位。

---

## 8. 第 4 刀追加：数据纪律清算（2026-09-20 同日补）

C-1a 收口留下的**最后一条自身尾巴**（`duration_is_per_skill_tier` 被判疑似死数据）按用户指令
**「能解决的一起解决了,不要再留了」** 扩展成一整轮清算：

| 指标 | 清算前 | 清算后 |
|---|---|---|
| 死函数 `--deadfuncs` | 25 | **0**（豁免 3，逐条理由） |
| 死数据 `--deadkeys` | 7 | **0**（文档键放行 47） |
| 内核魔法数 `--numbers` | 0 | 0 |
| 测试 | 628 | **634**（+6 障碍用例） |

### 🔴 8 条"死函数"**全是真的生产缺陷**（不是误报）
判据 = 「**写了、注释说会生效、但生产路径没人调**」⇒ **语义静默退化**，编译过、测试绿、肉眼看不出：

| # | 死函数 | 退化后的实际行为 |
|---|---|---|
| 1 | `MoraleLedger.ResetTurnCounters` | 「虚弱受击 −5（**每回合**≤1）」→ 实际「**整场**≤1」 |
| 2 | `FormationBoard.TryGetObstacleHp` / `RemoveObstacle` | **障碍 = 纯无敌墙**（`DamagePipeline` 遇 `Blocked` 直接 `continue`，全仓无扣血点）⇒ 与 `ObstacleRuntime`「只有血量的占位角色」矛盾 |
| 3 | `BattleProjector.DisplacementPreview` / `DryRunSwapChain` | 「必显 #6 位移预览」实机**根本没显** |
| 4 | `ExpeditionSession.CarryOverFrom` | `#245` 跨趟士气累积生产路径落不了地 |
| 5 | `StressRelief.NextRunOpeningMorale` | 减压副作用「下趟 −N」只显示、从不扣 |
| 6 | `BattleDirector.EnemyPhase` | `enemy_actions_per_round > 1` 保留开关形同虚设 |
| 7 | `ExpeditionFlow.BeginAmbushBattle` | 「扎营被夜袭」流程上从未发生 |
| 8 | `DungeonMap.IsConnected` | 不连通地图**静默产出**（走不到终点）而非 fail-fast |

> ⇒ **教训入库**：「只被测试调用也算死函数」这条扫描规则**是对的**。以后见到死函数**默认按缺陷查**，先别想豁免。

### 本刀新增（障碍受击闭环）
`ObstacleRuntime.TakeDamage`（**返回新实例**，record 不可变 ⇒ 快照不污染）· `FormationBoard.DamageObstacle`
（**复用**读口 + 移除口，不写第二份）· `SkillExecutor.ApplyObstacleDamage`（**只读**本次区间 `HitEvent`、
**不重掷骰** ⇒ 纪律 V 保住；伤害量优先取实际 `DamageEvent.Amount`）· `BattleProjector.Detail` 障碍槽如实报血量 ·
`BattleRoot.OnCardClicked` 移动前 dry-run（**被拒就不提交**）· **消重**：`DisplacementPreview` 委托 `DryRunSwapChain`
（消灭 `#325` D6 同族的**两份真值**）。

### 🔴 刻意不接的（唯一 1 项，已登记）
`LightMeter.ApplyEventChoice`：`expedition_nodes.json` **无 `torch`/`dark` 选项键** ⇒ 接线 = 代码替策划
**发明规则** ⇒ **造数据**，违反 `#307`。**归内容层待办**，等数据补上再连（它不是漏接，用例是契约载体）。

### 验证
```
dotnet build darkest/Darkest.csproj       → 0 个错误
dotnet test  darkest/Darkest.Tests.csproj → 已通过! 失败 0，通过 634，总计 634
python tools/check_data_discipline.py --all → 三扫合计可疑 0 处
```
