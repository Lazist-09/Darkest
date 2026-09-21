# 复现 DD：三层改造清单与任务规划

> **核对口径**：以 **DD 官方 wiki**（`dd_reference.md` / `dungeon_view.md §1`）为需求基线；
> 以 **F 盘代码实际实现**为现状，**不以项目设计文档的自我描述为准**。
> 每条结论带**文件级证据**；判定"缺失"前均经**多形态交叉 grep 反证**。
>
> 🔴 **本轮更正两条此前的误判**（代码才 pl 是真值）：
> 1. **战斗实为 4v4** —— `data/formation.json`：`player.combat_slots = 4`，`support_slots = [5,6]`。
>    "我方 6 / 敌方 4"的**旧结论不准**：6 是总槽位，**替补槽 5/6 不参战**，与 DD 一致 ✓
> 2. **士气并非缺失** —— `sim/morale/` 目录空，**但逻辑实际住在** `pipeline/MoraleLedger.cs` +
>    `data/MoraleEventsConfig.cs` + `data/morale_events.json`（并有 `MoraleTests`）。
>    ⇒ 这是**组织位置问题（应归位），不是功能缺失** ✓

---

## 0. 一句话结论

> **战斗层已经很像 DD（灵魂机制齐全）；地牢层有骨架没压力；城池层有元框架没养成深度。**
>
> 复现 DD 的主战场**不在战斗**，而在 **① 地牢层的"探索压力"（回头代价 / 重访刷新）
> ② 城池层的"养成纵深"（装备·技能 0-4 阶 / 周循环）**。战斗层只需补零 + 归位。

---

## 1. 三层体检总表

| 层 | 代码体量 | 完整度 | 主缺口 | 主战场 |
|---|---|---|---|---|
| **战斗** | `sim/` 约 4.3k 行 | **★★★★☆ 最完整** | Stealth 缺失；`morale` 未归位 | ❌ 否 |
| **地牢** | `Dungeon*` 约 1.1k 行 | **★★☆☆☆ 有骨架** | 回头代价（方向还反了）/ 重访刷新 / 饥饿 / 隐藏房 | 🔴 **是** |
| **城池** | `run/` 约 5.9k 行 | **★★★☆☆ 有框架** | 装备·技能 0-4 阶 / 周循环 | 🔴 **是** |

---

## 2. 战斗层（改动最小）

### 2.1 已有且忠实（**不要动**）

| DD 机制 | 实现 | 证据 |
|---|---|---|
| **SPD 排序 + 速度浮动骰 + tiebreak** | `TurnSequencer`：固定枚举序抽取浮动骰（杜绝哈希漂移）、实际速度降序、同速编号小者先、跨阵营我方先手、眩晕跳过并清除 | `sim/turn/TurnSequencer.cs:11` 注释整段 |
| **4v4 阵型** | `formation.json`：`player.combat_slots=4` / `enemy.combat_slots=4` | `data/formation.json` |
| **Mark 标记**（DT void standard） | 施加 + AI 权重 ×2 + `bonus_vs_marked_percent` 加伤 | `EnemyAi.cs:221` `DamagePipeline.cs:240` `DamageStep.cs:46` |
| **护卫重定向 Guard** | `ShieldGuard` + `TuningGuardRedirect`（参数自 `data`） | `sim/buffs/ShieldGuard.cs` |
| **死门 + 死亡抗性** | `UnitStats.DeathsDoorResist` + `TuningDeathsDoor` + `DeathsDoorAfflictionPenalty` | `core/contracts/UnitStats.cs:19` |
| **美德/折磨崩溃判定** | `CollapseResultEvent{Kind∈Virtue/Affliction}` | `core/events/BattleEventTypes.cs:47` |
| **Mark/眩晕递增/DoT/暴击情绪链** | #202/#204/#205/#206/#209 已落地 | `dd_reference.md` §1–§2 验收 |
| **撤退/放弃** | `RetreatResolved`（场级）+ `ExpeditionAbandoned`（趟级），口径已分离 | `BattleEventTypes.cs:68,75` |
| **伏击 Ambush** | `AmbushChance` + 扎营 `ambush_immunity_once` | `CampSkillsConfig.cs:45` `TuningConfig.cs:417` |

### 2.2 缺口与改法

| ID | 缺口 | wiki | 改法（文件级） | 优先级 |
|---|---|---|---|---|
| **C-1** | **Stealth 潜行**缺失 | 🔴 **出处修正**：`wiki 4.6` 系**误引**（`dd_reference.md` 的 4.6 实为 **Riposte 反击**，属"刻意不抄"）。真实出处 = `darkestdungeon.wiki.gg/wiki/Stealth_(Darkest_Dungeon)` + Shieldbreaker DLC 补丁说明 + `borrow/` 原版数据（`.ignore_stealth` / `.stealth N .duration N` / `.unstealth N`） | ✅ **已完成**（2026-09-18）：<br>① `buff_defs.json` 新增 `stealth`（`state_flag` + `rounds 2` + `refresh`，⚠️ 数值 `placeholder: true` #307）；<br>② `SkillsConfig` 新增 `SkillEffectType.Stealth` + `FuncTag.IgnoreStealth`（对齐原版 `.ignore_stealth`）并补齐两个枚举映射；<br>③ `SkillTargetResolver.Resolve` 新增可选 `IBuffLedger?`：潜行者不可被直接指定 + 多目标技能需"至少 1 个非潜行目标"才放行；<br>④ `EnemyAi` 候选池过滤 + `SkillUseResolver`/`AvailabilityReason.TargetStealthed` 灰显 + `SkillExecutor` De-Stealth（`ClearStateFlag`）；<br>⑤ `tests/StealthTests.cs` 11 例。见 `doc/modules/dd_reference.md §潜行` | ✅ 完成 |
| **C-1a** | 潜行**数值与配发** | — | ✅ **已收口（2026-09-20）**：① 持续回合数 = **2**（原版一手实据：`.stealth` 效果 duration 分布 1×2/2×4/3×2，取最常见的 2；**原版高阶是 3，属技能阶维度 ⇒ M8.1**）② De-Stealth = **真·命中即解除**（读 `HitEvent`，未命中**保留**潜行；同目标多段只解除一次）③ 敌人开场潜行 = **刻意不配发**（我方只有 3 个通用原型，与 DD 的 6 种潜行敌人不是同一套单位概念；`enemy_ai.json` 也无开场 buff 字段 ⇒ 硬套等于编造，归 M8 内容层）④ `ignore_stealth` = 配给**敌方 AOE 输出**（`caster_mental_shock`，原版精神 = 泼洒型穿过潜行） | ✅ 已完成 |
| **C-2** | `sim/morale/` **空目录**，逻辑散在 `pipeline/MoraleLedger` | — | ✅ **已完成**：仅 `MoraleLedger.cs` 迁 `sim/morale/`（`namespace` → `...Sim.Morale`）。<br>🔴 **清单修订（用户 2026-09-18 确认）**：原案还要求搬 `MoraleEventsConfig.cs` ⇒ **裁定不搬**：实测被 32 个文件引用（`BattleDirector`/`SkillExecutor`/`DirectorBridge` + 20 余个测试），且与 `CuriosConfig`/`EconomyConfig` 同属 `scripts/data/` 配置层；搬入 `sim/` 会造成巨量 churn 并破坏「配置在 data、逻辑在 sim」分层 | ✅ 完成 |
| **C-3** | `sim/survival/` 仅 `WeakDeathsDoor.cs`(72行)，光照/饥饿散在 `run/` | — | ✅ **已完成**：`LightMeter.cs`（含 `LightTier`/`ILightMeter`）迁 `sim/survival/`，`namespace` → `...Sim.Survival` | ✅ 完成 |

| **C-4** | **敌人类型**（Human/Beast/Unholy/Eldritch）+ `vs 类型 +25% DMG` | wiki 4.7 | **刻意留到 M7**（现仅 1 套编成 3 种敌人）。登记，暂不做 | P3 |
| **C-5** | Riposte / Corpse 尸体 | wiki 4.6/4.1 | **刻意不抄**（项目已有"向中靠齐"占位 + 剖面 `dd_reference` 已裁）= 与决策一致，**不动** | — 不做的决定 |

> **✅ C-2 / C-3 已落地（2026-09-18）**
> - `MoraleLedger.cs` + `.uid` → `scripts/gameplay/sim/morale/`，`namespace` → `Darkest.Gameplay.Sim.Morale`。
> - `LightMeter.cs`（含 `LightTier` / `ILightMeter`）+ `.uid` → `scripts/gameplay/sim/survival/`，`namespace` → `Darkest.Gameplay.Sim.Survival`。
> - **44 个引用文件补 `using`**（脚本批量插入，非手改）；另有 **1 处完全限定名**
>   `Darkest.Gameplay.Sim.Run.LightMeter.BoundariesFrom`（`BattleUI.Refresh.cs:191`）同步改为 `...Survival...`。
>    ⚠️ 教训：**补 using 的正则会漏掉完全限定名**，搬迁类重构必须额外 grep `旧命名空间.类型名`。
> - **清单修订（用户已确认）**：`MoraleEventsConfig.cs` **不搬**——被 32 个文件引用，且与 `CuriosConfig`/`EconomyConfig`
>   同属 `scripts/data/` 配置层；搬入 `sim/` 会破坏「配置在 data、逻辑在 sim」分层。
> - **✅ 验收**：`dotnet build` **0 错误 / 13 警告**；`dotnet test` **587 通过 / 0 失败**
>   （清单原文记的"281"是旧数）。详见 `doc/architecture/goal1_combat_relocation.md`。

---

## 3. 地牢层（主战场①）

### 3.1 已有（保持）

`DungeonGrid`(11 种 kind + 字符映射 + **P30 七条加载期校验**含 goal 可达 BFS) ·
`DungeonWalker`(四向/揭示/BFS) · **`WalkLightCost` 段内分摊+余数结转（总消耗守恒）** ·
`DungeonWalkLight` · `MapScouting`(base_pct+光照加成·1~3步) · `DungeonGridDeriver`(房间图→网格) ·
`DungeonGridConfig`(P30 契约) · `FlowPhase` 四相 · `LightMeter` ·
`CurioResolver` · **越暗⇒掉落补给更多**（`ExpeditionFlow.cs:439` 消费 `Light.Loot`）

### 3.2 缺口与改法（按 DD wiki §1 八条）

| ID | 缺口 | wiki # | 改法（文件级） | 优先级 |
|---|---|---|---|---|
| **D-1** | 🔴 **回头代价缺失 + 方向相反** | ⑦ | **现状**：`ExpeditionMapConfig.RevisitCost` 校验 `\|revisit\| < \|new\|`（**回头更便宜**），与 DD 相反。<br>**改**：① 旧房间图层**不动**（免返工）；② **网格层新增** `BacktrackCost`：`DungeonGridConfig` 加 `backtrack{ morale_cost: <placeholder> }`，`DungeonWalker` 进入**已 visited 格**时结算；③ UI 回头时弹一次红字读数 | **P0** |
| **D-2** | 🔴 **重访刷新威胁**缺失 | ⑧ | 新增 `RevisitSpawner`：`tuning.dungeon_layer.revisit`（每光照一档概率，先 `placeholder`，方向对齐 DD 的 ≤50⇒+2.5% / =0⇒+5%）；进入已访格掷骰 ⇒ 生成战斗/陷阱；**必写 `RngDraw`** | **P0** |
| **D-3** | ✅ **三态揭示**（2026-09-20 完成） | ⑥ | `_revealed:HashSet` ⇒ `Dictionary<pos, RevealState>`（`Unexplored/Scouted/Visited`）✓；`WalkMapView` 三态三配色 + `▒` 字形 ✓；`vision` 契约接线（`tuning.dungeon_layer.vision`，`dungeon_grid.json` 仍不存在）✓；加载期加"半径 ≥ 图边长 ⇒ 拒绝"上界 ✓；`scout_bonus` **只登记不消费**（避免第二份距离真值）⚠️ | P1 |
| **D-4** | ✅ **已完成**（2026-09-20） | ④ | ~~`Trap` 转真：踏中 ⇒ 伤害/状态/刷怪；可被侦察或 SPD 规避~~ ⇒ 🔴 **实测 wiki 原文后定口径**：踏中 ⇒ **随机一名**队员掉 **% 最大 HP** + 压力**恒定 +15**；**未侦察**的陷阱按该队员 **Trap Resist**（**不是 DODGE**）掷闪避，但**照样会踩中**（"看不见" ≠ "不触发"）；**已侦察** ⇒ 紫图标 + 可**拆除**（概率 = `Trap Resist + 40%`，🔴 **允许 > 100%**），拆成功 ⇒ **完全无害 + 回 8 压力**。<br>**落地**：① `TrapDefs`（`res://data/trap_defs.json`，**内容表**，与 `curios.json` 同族）+ 九条加载期 fail-fast；② `TrapResolver`（**唯一**抽取/判定面，`D-2` 的 `ThreatTrap` 必须复用它，`#325` D6）；③ 🔴🔴 **三态门禁** `TrapGate{Hidden,Disarmable,Consumed} ← RevealState` —— **这是 `D-3` 中间态 `Scouted` 唯一的玩法价值**（此前它只是视觉差异）；④ **门禁必须在 `TryStep` 之前算**（走完该格即 `Visited` ⇒ 现算恒得 `Consumed` ⇒ 整个机制静默失效 ⚠️）；⑤ `DungeonGridDeriver.ScatterTraps` —— 🔴 **`DungeonTileKind.Trap` 此前全仓无产出源**（派生器只挖房间与走廊）⇒ 补上"走廊格按 `tuning.dungeon_layer.traps` 概率撒 `^`"（**逐格掷**而非逐段，因派生图走廊段普遍只 1~2 格）；⑥ `ExpeditionSession.ResolveTrapByResist`（结算到名册；"随机一人"**也写 `RngDraw`**）；⑦ `BattleRoot`/`ExpeditionComposition` 生产接线（`BindTraps`/`SetRegion`/`BindTrapRng`）。<br>**测试**：`TrapTests` 15 例 + `TrapFlowTests` 12 例（含**端到端**"走到陷阱格 ⇒ 真的结算"）；全量 **709 通过 / 0 失败**（基线 683→709）；三扫 **0 处**。<br>**诚实缺口**：① `trap_resist` 在 `units.json` **不存在** ⇒ 退化解（全员 0），由 `TrapResistSourceDeclared` **自证**；② **地区 id 无入口**（主城没有"选地区"）⇒ `ExpeditionContext.RegionId` 默认 `null` ⇒ **陷阱抽取显式不做**（打印自证，不静默）；③ `weald_snare` 的 `hp_percent` 取 **5%**（DD 原文 0%，伤害全在 Blight）—— 本刀不落额外效果，照抄 0 会与"漏配键"不可区分，note 已写明"待 Blight 落地后改回 0 并放开校验" | ✅ |
| **D-5** | ✅ **已完成**（2026-09-20）| ④ | ~~新增 `HungerDrift`：按段/格累计~~ ⇒ 🔴 **实测 wiki 原文后改口径**：DD 的饥饿**不是"累计漂移"**，而是与 Fight/Obstacle/Curio 同类的**走廊格隐藏事件**，按**光照档**掷骰（`≤0→12.5%` / `≤50→10%` / `≤75→7.5%` / `≤100→7.5%`），带**缓冲**（开局/扎营 2 条、遇检查后 1 条、**只在前行时递减**）。<br>**落地**：`HungerSpawner`（纯函数，与 `RevisitSpawner` 同构）+ `HungerConfig` + `TuningConfig` 加载期校验（**末档必须覆盖满光照**，复制 D-2 防护）+ `ExpeditionFlow` 缓冲/触发 + `ExpeditionSession.ResolveHunger`（吃/不吃）+ `BattleUI.TryOpenHunger` 面板。<br>**测试**：`HungerTests` 26 例；全量 **660 通过 / 0 失败**；三扫 **0 处** | ✅ |
| **D-6** | ✅ **已完成**（2026-09-20） | ④ | ~~新增 `'*'` 字符 + 枚举，地图不显示，靠侦察揭示 ⇒ 给侦察一个**非信息类**回报~~ ⇒ **落地**：① `DungeonTileKind.Secret`（第 12 位）+ `DungeonTileMap` **`'*'` 双向登记**（P30② 护栏 `ToChar(FromChar(c)) != c` **自动拦**，`SecretTests` 有正/反两条用例）；② `TuningSecretScatter`（`corridor_chance_percent` / `reward_gold` / `max_rewards_per_run`）+ **三条加载期校验**，其中 🔴 **`reward_gold > 0` 是"意图检查"**（回报为 0 ⇒ 玩家理性地永不侦察 ⇒ 整条链路沦为装饰 ⚠️）；③ `ScatterTraps` ⇒ **`ScatterCorridorContent`**：陷阱与隐藏房**共用一趟走廊扫描**（`DungeonTileKind` 单值 ⇒ 分趟会互相覆盖，"陷阱密度"变成"隐藏房概率的函数"）；④ `ExpeditionFlow.RevealSecrets`（**唯一**揭示入口，挂在既有 Curio `scout` 效果上）+ `Economy.AwardContent`（金币的**唯一**记账通道）；⑤ `WalkMapView.FromTileWalk` **对未揭示的 `Secret` 格 `continue`**（🔴 **不能画成"未知格"** —— 那也会被数格子看穿）；⑥ `BattleRoot` 打印自证。<br>**测试**：`SecretTests` 18 + `SecretFlowTests` 12；全量 **739 通过 / 0 失败**（基线 709）；三扫 **0 处**。<br>**坑留档**：🔴 写入 `secrets` 段时用 `json.dumps(indent=2)` 重排整个 `tuning.json` ⇒ **19 个既有用例变红**（那批用例是**文本级拼接**，依赖原始紧凑格式与 `dungeon_layer` 的**物理位置**）⇒ 已按原风格 + 原键序重写；🔴 `ExpeditionSession.Gain` 会把 `"gold"` **当口粮加**（静默串账）⇒ 金币必须走 `Economy` | ✅ |
| **D-7** | ✅ **已落地**（2026-09-20）：折磨在探索层会**拒绝用道具（被迫空手）/ 拒绝进食（强制挨饿）**；判据 = **士气 < `morale.start`**（模型自身的解除线，见 §5 落地记录）| ⑩ | ~~`CurioResolver` 前置门禁：带特定 Affliction ⇒ 概率拒绝；拒绝**必须有文案事件**~~ ✅ 门禁挂在 `ExpeditionFlow.ResolveCurio` **之前**（比 `ResolveBare` 早）+ `ResolveHunger` 入口。依赖 D-5 ✅ | P2 |
| **D-8** | ⚠️ `dungeon_grid.json` **文件不存在**（仍在派生图） | ② | 阶段：先手绘 1~2 张**（含死路/环路/宽窄）**，再替 `DungeonGridDeriver`。派生图宽度恒定、无死路 ⇒ **做不出 DD 的关卡节奏** | P2 |
| **D-9** | 🟡 光照收益只有"补给"，非 DD 的"战利品掉率" | ⑨ | 现有 `Loot` 旁**并列**加 `loot_bonus_pct`（**不改现有语义**），作用于战斗结算的战利品掷骰 | P2 |

---

## 4. 城池层（主战场②）

### 4.1 已有且忠实（保持）

| DD 机制 | 实现 | 证据 |
|---|---|---|
| **建筑扩建（多级）** | `HeirloomStock`：`UpgradePath` / `NextLevel` / `CanUpgrade` / `TryUpgrade` | `run/HeirloomStock.cs:87–106` |
| **减压双设施**（Tavern/Abbey **同价同效、风险不同**——照抄 DD） | `StressRelief.cs` + `EconomyConfig` **强制恰 2 栋**并校验 | `run/StressRelief.cs:14` `EconomyConfig.cs:156` |
| **马车招募 + 名册上限** | `Roster.Recruit(...)` + `HeirloomStock` 的 `stagecoach.RosterCapDelta` | `run/Roster.cs:333` `HeirloomStock.cs:145` |
| **疗养院** | `Sanitarium.cs`(169) | ✓ |
| **特质系统** | `HeroTraitConfig`（**术语 = Trait，非 DD 的 Quirk**，功能同类） | `data/HeroTraitConfig` |
| **英雄等级 / 阵亡留档** | `Roster.LevelOf` · `HeroProjection.ApplyLevel` · `Graveyard`（含 Level/Cause） | `run/Roster.cs:26,82` |
| **传承资源** | `HeirloomStock` 被 `Sanitarium`/`StressRelief` 引用 | ✓ |
| **跨趟解锁** | `RunProgress`：`runs` ⇒ `UnlocksConfig` ⇒ `building/curio/roster_cap` | `run/RunProgress.cs` |

### 4.2 缺口与改法

| ID | 缺口 | wiki | 改法（文件级） | 优先级 |
|---|---|---|---|---|
| **H-1** | 🔴 **武器/护甲 0–4 阶**（装备升级）完全缺失 | wiki 5：武器 5 档 | ① `UnitStats` **补回 `WeaponLevel/ArmourLevel`(0–4)**（它是"压平成终值"的元凶）；② 新增 `Blacksmith.Upgrade(cost)`；③ 伤害/HP 走 `base × (1 + level系数)`，**不改既有数值口径** | **归属 M8.3**（不独立成刀；§7 ① 已裁定**不动 `core/contracts`**） |
| **H-2** | 🔴 **公会技能升级 0–4** 缺失（且 `RosterConfig:163` 显式**禁止** `"skill_upgrade"` key） | wiki 4.4 | ① 放行该 key（**已查清：`#283` 7.6/7.8 刻意禁 ⇒ 不是"待解"，见 §7 ②**）；② `SkillsConfig` 加每技能当前阶；③ 效果按阶缩放（ACC/DMG/副作用） | **归属 M8.1**（禁令**刻意**；放行前须先改 `#283` 7.6） |
| **H-3** | 🔴 **无"周"推进** —— 元层时间单位是 `runs`（出征趟数）而非 week | wiki 3 | 新增 `TimeCycle`：一趟结束 ⇒ 推进 N 周 ⇒ 触发 建筑升级完成 / 新英雄到站 /  idle 减压 −5。**这是 DD 养成 loop 的骨架** | **P1** |
| **H-4** | ⚠️ **无 Resolve Level ⇒ 抗性成长**（DD：每级 `Stun/Move/Bleed/Blight/Debuff +10%`） | wiki 5 | `HeroProjection.ApplyLevel` 扩展：抗性按 Level 线性加成 ⇒ **给养成一个"变强"的可感反馈** | P1 |
| **H-5** | ⚠️ 无**元层失败条件**（Stygian：12 名英雄死亡 / 86 周） | wiki 4.8 | `RunProgress` 加 `dead_heroes` / `weeks` 与失败阈值（**比数值放大更有戏剧性**） | P3 |
| **H-6** | ⚠️ 空闲一周减压 −5 缺失 | wiki 3 | 依赖 H-3 的周推进后，加 idle 自然减压 | P2 |

---

## 5. 任务排期（依赖驱动）

```
【第 1 刀】战斗层归位（半天，零行为变更）
   C-2  morale 归位  +  C-3 survival 归位
   ✅ 判据：281 个既有测试全绿；行为 diff 为空

【第 2 刀】养成纵深（触达 DD 的"长期目标感"）
   H-1 装备 0-4 阶   →  H-2 技能 0-4 阶   →  H-4 Resolve 抗性成长
   ✅ 判据：同一英雄从 Lv0 升到 Lv4 后，伤害/HP/抗性单调上升且可断言

【第 3 刀】地牢压力（触达 DD 的"探索紧张感"）★最重要
   D-1 回头代价(纠偏) ✅  →  D-2 重访刷新 ✅  →  D-5 饥饿 ✅
   ✅ 判据：① 回头必付代价 ✅ ② 反复走同一格会刷新威胁且全黑更频繁 ✅ ③ 每掷骰有 RngDraw ✅
   📌 落地（2026-09-20）：`DungeonGridConfig.Backtrack`（校验⑧）/ `DungeonWalker._visited` + `TryStep(out wasRevisit)`
      / `ExpeditionFlow.EnableTileWalk(segmentCost, backtrackCost)` + `BacktrackExtraFor` + `TileBacktrackCount`
      / `RevisitSpawner.Roll`（纯函数）/ `tuning.dungeon_layer`（`revisit_light_cost` + `revisit.tiers`）
      / `BattleRoot` 从数据接线；测试 `RevisitTests` 21 例全绿（全量 620 通过，基线 598）
   ⚠️ 踩坑留档：初版 `tiers` 末档只到 50 ⇒ 光照 51..100 **一档都不命中 ⇒ 完全不掷骰**，
      即"亮着走永远平安"，D-2 在最常见情形下静默失效。已由加载期校验「末档必须 ≥ `light.enter_value`」锁死。

【第 4 刀】养成的节奏
   H-3 周循环推进  →  H-6 idle 减压  （依赖 H-1/H-2 的建筑дь升级才有意义）

【第 5 刀】地牢内容层
   ~~D-3 三态揭示~~（✅ 2026-09-20）→ ~~D-4 陷阱转真~~（✅ 2026-09-20）→ ~~D-6 隐藏房~~（✅ 2026-09-20）→ ~~D-7 探索 act-out~~（✅ 2026-09-20）
   📌 `D-4` 落地（2026-09-20）：`TrapDefs`（`data/trap_defs.json`，九条 fail-fast）/ `TrapResolver`（唯一判定面）
      / `TrapGate{Hidden,Disarmable,Consumed} ← RevealState`（**`D-3` 中间态的玩法价值**）
      / `DungeonGridDeriver.ScatterTraps`（**`^` 的唯一产出源**）/ `ExpeditionSession.ResolveTrapByResist`
      / `BattleRoot` + `ExpeditionComposition` 生产接线；测试 `TrapTests` 15 + `TrapFlowTests` 12
      （全量 **709 通过 / 0 失败**，基线 683）
   ⚠️ 踩坑留档：① **门禁若在 `TryStep` 之后算 ⇒ 该格已 `Visited` ⇒ 恒 `Consumed` ⇒ 整个 D-4 静默失效**；
      ② `DungeonTileKind.Trap` 此前**全仓无产出源**（派生器只挖房间与走廊）⇒ `TrapResolver` 再完备也触发不了
      —— 是"库写完了没人接线"的典型（三扫的死函数清单**正是**为了抓这个）；
      ③ 三扫 `strip_code` 的**已知假阳性**：字符串里写 `//`（如 `"derived://..."`）会被当注释 ⇒ 该行后半段被当代码扫
      ⇒ 已把该标识改成不含 `//`（**不改扫描口径**：试过"先字符串后注释"会多报 9 处既有合法行）
   🔜 `D-4` 遗留（**有界、自证**，非静默）：① `trap_resist` 不在 `units.json` ⇒ 退化解（全员 0）；
      ② **地区 id 无入口**（主城没有"选地区"）⇒ `ExpeditionContext.RegionId` 为 `null` ⇒ 陷阱抽取**显式不做**；
      ③ `weald_snare` 取 5%（DD 原文 0%，待 Blight 落地改回）
   📌 `D-6` 落地（2026-09-20）：`DungeonTileKind.Secret` + `DungeonTileMap` `'*'` 双向登记（P30② 护栏自动拦）
      / `TuningSecretScatter` + 三条加载期校验（🔴 **`reward_gold > 0` 是"意图检查"**）
      / `DungeonGridDeriver.ScatterCorridorContent`（陷阱与隐藏房**共用一趟**走廊扫描 —— 单值枚举不得分趟覆盖）
      / `ExpeditionFlow.RevealSecrets`（唯一揭示入口，挂 Curio `scout`）/ `Economy.AwardContent`（金币唯一通道）
      / `WalkMapView` 对未揭示 `Secret` **连"未知格"都不给**（画了就会被数格子看穿）
      / `BattleRoot` 打印自证；测试 `SecretTests` 18 + `SecretFlowTests` 12
      （全量 **739 通过 / 0 失败**，基线 709）
   ⚠️ 踩坑留档：① 🔴 **用 `json.dumps(indent=2)` 重排整个 `tuning.json` ⇒ 19 个既有用例变红** ——
      那批用例是**文本级拼接**（`Replace("\"dungeon_layer\": {", ...)` 等），同时依赖**紧凑格式**与
      **键的物理位置**（`TuningJson` 把 `dungeon_layer` 插在 `"retreat_formula"` **之前**）⇒ 改 `tuning.json`
      前必须查"有没有测试在拼接它"；
      ② 🔴 **`ExpeditionSession.Gain` 会把 `"gold"` 当口粮加**（它的分支是 `firewood` / **else ⇒ Food**）
      ⇒ 金币必须走 `Economy`（`GoldChangedEvent` 是唯一通道）—— 这是**静默串账**，不是报错 ⚠️；
      ③ `vision.radius = 2` 会让"揭示 ⇒ `Scouted`"的用例读到 `Visited`（两条通道合成的**正确**结果）
   📌 `D-7` 落地（2026-09-20）：**折磨走出战斗** —— `ExplorationActOut`（纯函数内核：`IsAfflicted` /
      `RollCurioRefuse` / `RollEatRefuse` + 两条文案）/ `TuningExplorationActOut` + 四条加载期校验
      / `ExpeditionFlow.ResolveCurio` **最前面**的门禁（拒绝道具 ⇒ **被迫空手**）/
      `ExpeditionSession.ResolveHunger` 入口门禁（拒绝进食 ⇒ **强制挨饿**、口粮一点不扣）
      / `BattleUI.RoomInteractions` 玩家可见提示（不静默）/ `[片4·D-7]` 启动自证
      （全量 **763 通过 / 0 失败**，基线 739）
   🔴🔴 **`D-7` 的核心裁定：「带折磨」在探索层的判据 = `士气 < 50`** —— 它**不是近似，而是模型自身的边界**：
      折磨只在士气 == 0 挂上（`MoraleLedger.GrantAffliction`）、**只有回到 `morale.start`（=50）才解除**
      （`MoraleLedger` 第 127~134 行），美德走 100 并**立即回 50** ⇒ **美德与折磨永不并存**（连续变化必经 50）
      ⇒ 「士气 < 50」**⇔** 「带折磨」✓ 附带收益：**零新载体**（士气本就跨趟，`#245`/`#287`）⇒
      不需要给 `Retained` 加 affliction 字段（那是**第二份真值**，`#325` D6 禁止）✓
      🔴 加载期强制 `morale_affliction_threshold == tuning.morale.start`：若调成 30，会出现
      「士气 40 时折磨**实际已挂上**（崩溃过），但探索层判他**不带折磨**」⇒ 规则自相矛盾 ⚠️
      ⚠️ 判据取**队内最低士气**（只算存活者）—— 折磨是**个体**状态，取平均会把"一崩溃 + 五满状态"抹平
   ⚠️ 踩坑留档（`D-7`）：① 门禁必须挂在 `ResolveBare` **之前**（否则被拒的道具请求已消耗 `RngDraw`
      ⇒ 随机流与"接受"不同步 —— 与 `D-4` 的"门禁算晚"同族）；② **不带折磨时整个不掷**（`DrawCount == 0`）
      —— 否则每次摸 Curio 都多消耗一个随机数，会**污染整条随机流**；
      ③ 概率 **0 被加载期拒绝**（"配了但永远不生效"家族）⇒ 要关闭必须**整段删掉** `exploration`
   🔜 `D-7` 遗留（**有界、自证**）：① **首战前的暴露窗口** —— `Retained` 战后落账 ⇒ 开局到首战之间门禁不生效
      （与 `HungerCanApply`/`TrapCanApply` **同因同果**，非本刀引入）；② `curio.md §1.2 ⑤` 的**类型标签匹配**
      （只对某几类 Curio 生效）未做 —— 本刀对**所有** Curio 一律适用（更简单、更可预期），类型标签属内容层待 M8；
      ③ `Kleptomaniac`（私吞战利品）未做 —— 那是**怪癖**不是折磨，属另一条线
      ⇒ 测"揭示本身给什么态"必须**显式关掉 `vision`**；
      ④ 脆断言：用"全流程 `RngDraw` 总数"证明"关闭时不掷"会翻车（量级相近）⇒ 改**结构性判据**
      （同图同种子 关/开 两次 `Derive` 逐格比对：差别**只允许**是 `*` 覆盖 `C`）
   🔜 `D-6` 遗留（**有界、自证**）：① 隐藏房**只给金币**（"里面是哪件"属内容表 ⇒ `D-9` 同族）；
      ② 未注入 `Economy` ⇒ 揭示但金币不计 + 写 `secret_no_economy_gold_uncredited`（**不谎报**）；
      ③ 概率/回报量级全 `placeholder`

【第 6 刀】收尾：不算错账款
   D-8 手写瓷砖图 → C-1 Stealth → D-9 战利品 → H-5 元层失败条件
```

### 每刀通行铁律（守既有纪律）
```
① 新数值先进 tuning + placeholder:true（#307），禁写死
② 新"格"枚举必须同登记 DungeonTileMap 往返 + P30②字符集（ToChar(FromChar(c))!=c 会自动拦）
③ 随机必写 RngDraw（MapScouting 是范式）
④ 加载期 fail-fast，不留静默默认（红线 21）
⑤ 先写失败测试（xUnit 零 Godot），再实现
```

---

## 6. 明确"不做"（已裁，避免返工）

| DD 机制 | 处置 | 依据 |
|---|---|---|
| Corpse 尸体占位 | **不抄** | 项目用"向中靠齐"，`dd_reference` 4.1 已裁 |
| Riposte 反击 | **不抄**（登记为候选） | `dd_reference` 4.6：会动平衡，非当前切片 |
| 敌人类型系统 | **延后 M7** | `dd_reference` 4.7：仅 1 套编成时无收益 |
| Stygian 难度模式 | **延后** | wiki 4.8：先做元层失败条件即可 |

---

## 7. 待你裁决（2026-09-20 复核：①③⑤ 已裁定，②④ 已关闭）

```
① H-1/H-2 是否现在就在 UnitStats 上"找回"装备/技能阶？（这会改 core/contracts 契约）
   ✅ 已裁定：**不动 `core/contracts`**。`m8_roadmap §1.3 ④` 明确「暂不做 Blacksmith（装备）· Guild（技能升级）」，
      故 H-1/H-2 现阶段**不碰契约**。归属已重标：
        · H-1（饰品/装备轴）→ 前置到 **M8.3**（饰品轴），不是独立一刀
        · H-2（技能 0–4 阶）→ 归入 **M8.1**，且必须先解 ②
② H-2：当初为何在 RosterConfig:163 显式禁止 "skill_upgrade"？是刻意还是占位？
   ✅ 已结案：**刻意禁用**（非占位）。证据链：`#283` 的 7.6 / 7.8 已裁定「等级只给属性、**不升技能**」，
      技能升级留 **M8.1**；`m8_roadmap §1.3 ④` 与 `doc/architecture/README.md` 对齐。
      ⚠️ 旧文里把 `skill_upgrade` 标成"待解"是**误标**，已在本文件与 open_issues O-75 同步更正。
③ H-3：元层时间单位要从 runs 改成 week 吗？
   ✅ 已裁定：**改**（`week` 是 DD 养成 loop 的骨架），但**排在 H-1/H-2 之后**做（第 4 刀），
      因为"周"的意义来自建筑 + 装备/技能成长；先改单位会得到一个空壳循环。
④ D-1：旧"回头更便宜"口径是否接受"两层并存"？
   ✅ 已实施（按本条建议）：**旧层不动、新网格层用 DD 口径** —— 两层各自自洽、互不覆盖。
        · 旧层 = `expedition_map.json` 的 `move.revisit_cost`（按段移动，`MapTraversal`），保持"回头更便宜"原样；
        · 新层 = `DungeonWalker` / `TryStepTile`（瓷砖网格，D-1 落点），回头**必付** `dungeon_layer.revisit_light_cost`，
          且走廊格**只补差额** `max(0, revisit − perTile)`，避免与段守恒重复计费。
⑤ 排期顺序：建议按" §5 战斗归位 → 养成 → 地牢压力"，还是优先把地牢压力做完？
   ✅ 已裁定：**改走 D-1/D-2 地牢层优先**（第 3 刀 = 地牢压力，已按 `m8_roadmap §0` 的"可立即开工"执行）。
      H-1/H-2 受 ① 制约（不动契约）⇒ 反而**不能**先做 ⇒ 顺序改为：地牢层 → M8.1 传家宝+建筑 → 地牢内容层。
```
