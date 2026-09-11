# Project_Memory（项目记忆体 · 团队唯一事实来源）

> **定位**：团队共享工作记忆/向量库（架构师协议要求初始化；主程序每次写码后必须追加记录）。
> **规则**：只追加、不删除；改写历史条目须划线保留（策划纪律第 6 条同源）。禁止并行编辑。
> **坐标系**：本仓库的"策划文档层"在 `doc/`，"架构拆分层"在 `doc/architecture/`，"Godot 工程"在 `darkest/`（res://）。

---

## 0. 宏观架构原则（初始化时写入，行为级判据）

1. **确定性内核**：全部战斗规则/数值/随机收进引擎无关纯 C#（`scripts/core` + `scripts/gameplay/sim`），零 `using Godot`；headless 与实机共用同一 `BattleDirector`（M6 ≥300 场 Monte Carlo 的硬前提）——blueprint §4/§8。
2. **数据驱动**：起手值唯一落点 = `res://data/` 七个 JSON（units/skills/buff_defs/morale_events/tuning/formation/enemy_ai，字段以 data_schema.md 为唯一权威）；改数值只改 JSON / 走 override 覆盖集，不改代码（README §6-5）。
3. **五层单向依赖**：`Data(B1) → 内核(B2) → BattleDirector(B3) → 表现(B4) → UI(B5)`；出现 `scene→sim 写`、`ui→内核结算`、`sim→Godot` 任一即架构违规——blueprint §4。
4. **唯一随机出口**：`IRngProvider` + 每场单 seed + DrawCount 审计；9 层随机（combat_math §9）映射到固定调用点（blueprint §8.2），新增骰子必须回填。
5. **事件流 = 战斗日志 = 回放源 = 统计源**：UI 刷新/飘字/日志/Monte Carlo 全部订阅内核事件流，一处写入、处处只读——blueprint §6.2/§8.3。
6. **位移预览 = 内核同源 dry-run**：预览与结算逐事件一致（ui_spec 必显 #6；blueprint §5d），禁止 UI 自推"只推 1 格"。
7. **UI 是机制的一部分**：9 条必显示（ui_spec §2）缺一不可；物理 vs 精神必须视觉可分（#157）。
8. **测试钩子前置**：公式单测复算 combat_math §7.1 八样例；确定性回放（同 seed 同命令流逐字节一致）；模拟器直驱内核无场景树——blueprint §10。
9. **开放题纪律**：歧义挂 open_issues.md（O-nn），不静默拍板、不改设计；先后以 `doc/modules/` 与 `#N` 为准。
10. **先读后写**：改任何文件前 read 全文；同文件禁止并行编辑（本轮已踩过坑，策划纪律第 4 条）。
11. **命名**：C# 类型 PascalCase、文件/资源 snake_case、配置字段名以 data_schema.md 为准（_conventions §3）；术语用 glossary §9 建议（Morale/Resilience/Weak/DeathsDoor/…）。
12. **目标语义（#178/#179，v0.44）**：技能范围=候选池——单体/`any_ally` 池内**选一**（玩家③b / AI / 策略）、AOE 全中、`team` 全体、双段=同一目标两段；数据 JSON 零改动（data_schema §3.2 生效语义 + P11）；**旧"范围全命中"口径已废（O-38）**——改造中，完成前不重新基线。
13. **池外「移动」（#180，v0.45）**：每原型常备 1 条 `*_move`（`pool_external:true` + `target.scope=move_range` + `distance`：坦克 1 / 战医政 2）；仅战斗位 1~4、目标=自身±N 格内**被占用**战斗位并交换（空位不可选，#21）；**不过位移抗性、无伤害 → 不触发死门**；技能总量 **47**（36 池内 + 4 移动 + 7 敌）；增援=单按钮两步 `Reinforce(A,B,X)`（#181）；技能栏不内联要求文字、候选高亮必需（#182）。
    > ⚠️ **v0.48 已被 #190/#191 取代**：改为 **1 条通用 `move`**（不绑 `owner_unit`）+ `units.json` 的 `move_distance`（坦克 1 / 战医政 2），技能总量 **44**；过滤一律用 **`pool_external` 标志**，**禁止**再用 `_move` 后缀（可插拔契约）。
14. **文档回读纪律（数量／次数／频率专项）🔥 v0.48 新增**：
    **每完成一个里程碑、或合入一批改动后，实现方必须拿着文档当验收清单，逐条回读实现**——
    不是"读代码找 bug"，而是"**逐条核对文档写的规则，实现是否真的一样**"，并当场把不一致登记为 O-nn。
    **重点核 4 类规则**：
    ① **命中几个**（范围 = 池内选一 vs 全命中）
    ② **补几个**（超时增援 = 一波补齐全部空位 vs 每回合补 1 个）
    ③ **隔几回合**（波次间隔 M / CD / buff 持续回合 / 触发回合）
    ④ **判哪一方胜负**（胜 / 负是否**分别**判定，还是硬编码一方）
    > 🔴 **为什么单独立一条**：本项目已**连续 5 次**出现「**文档对、实现错**」——
    > O-38 目标语义（范围当全命中）、#177 换位增援口径缺失、O-48 交换链误用于增援/移动、
    > O-47 胜负硬编码 `"胜利：敌方全灭"`、O-52 超时增援每回合只补 1 个。
    > **这 5 次全部命中上述 4 类**。
    >
    > ⚠️ **为什么测试抓不到**：既有单测是**照着实现写的**，实现错了测试也跟着错，
    > 所以套件一直是绿的（最近一次 156/157），偏差只会让**设计意图静默失效**——
    > 直到**用户实机**发现（5 次里有 4 次是用户发现的）。
    > 故本条与第 8 条「测试钩子前置」互补：**第 8 条防"没测"，本条防"测错了对象"**。
    >
    > ✅ **策划侧对应动作**：验收/走查时**优先抽查这 4 类**规则，而不是抽查公式（公式有单测兜底）。

---

## 1. 文件注册表（截至 2026-09-09）

### 策划交付层（doc/，只读源，禁止改动）
| 文件 | 作用 | 备注 |
|---|---|---|
| README.md / GDD.md / state.md / CHANGELOG.md | 入口 / 主文档 / 决策表 **#1~#194** / 演进史（v0.48） | state.md = #N 溯源 |
| modules/combat_math.md | 全公式 + 9 层随机 + #157/#158/#163 口径 | 挡在所有代码前 |
| modules/skill_data.md | **44 条技能实值（36 池内 + 1 通用移动 + 7 敌方，#191）** | 数据抄录唯一来源 |
| modules/{glossary,skill,character,morale,formation,buff,enemy,ui_spec,verification,review}.md | 各系统规格 | modules 层为准 |

### 架构拆分层（doc/architecture/）
| 文件 | 作用 | 备注 |
|---|---|---|
| _conventions.md | 写作规范 / 任务卡模板 | 定稿 v1 |
| blueprint.md | 总体蓝图（五层/目录/接口/确定性/测试钩子/风险） | 草案 v0.1 |
| data_schema.md | 数据 Schema + P1~P12 校验 | 草案 v0.1→随实现滚动 |
| open_issues.md | 开放题 O-01~O-48（O-35 起为实现期登记；**O-49~O-52 为策划提案编号，待架构师确认/改号**） | 滚动维护 |
| README.md | 架构入口 + 看板 + 交付清单 | 本套入口 |
| tasks/m0~m6 共 7 个文件 + **`m6_fix_pack.md`（ARCH-T-FIX-PACK-01）** + **`feat_pack_01.md`（ARCH-T-FEAT-PACK-01）** | **54 张任务卡** + 两张**执行包卡**（修复包 P0~P4 口径链 / 功能包 F0~F3 点击·可插拔·士气 buff·增援） | 草案→随实现滚动 |

### Godot 工程（darkest/ = res://）
| 路径 | 状态 | 备注 |
|---|---|---|
| project.godot | 存在（Godot 4.6 .NET / assembly_name=Darkest，main_scene=Battle.tscn） | 只读实况 |
| scenes/ scripts/ data/ resources/ tests/ | ✅ **已实现（M0~M6 主程序迭代，最近一次 156/157 绿；判据 A 诚实红）** | 关键文件：`sim/skill/SkillTargetResolver.cs` / `SkillExecutor.cs`（O-38 已落地：候选池+选一）、`pipeline/DamagePipeline.cs`、`director/BattleDirector.cs`（**O-47 胜负判定待修**）、`turn/TurnSequencer.cs`、`enemy/EnemyAi.cs`（**O-46 目标偏好待接；`"tank"` 硬编码待去**）、`sim/buffs/{BuffLedger,ShieldGuard}.cs`（**buff `modifiers` 零消费者**）、`tests/MonteCarlo/{HeadlessDriver,Policies}.cs`（**O-42 策略待改造**） |
| darkest/data/*.json（7 个） | ✅ 生效值：#171/#173（敌 HP 60/60/46/41、收割 0.4/0.5、横扫 0.6 等） | 与 skill_data/enemy 起手值表并存（README §6-5 口径） |

---

## 2. 更新记录（追加式）

- `2026-09-09: [新增] doc/architecture/ 全套（_conventions / blueprint / data_schema / open_issues / README / tasks/m0~m6）。架构师初始化本记忆体。接口基线：IFormation / ITurnSequencer / ISkillUseResolver / IMoraleLedger / IDeathsDoor / IBuffLedger / IEnemyAi / IRngProvider / IShieldGuard / IPlayerPolicy（blueprint §9）。依赖：doc/ 策划 16 件；darkest/project.godot（Godot 4.6 .NET）。开放题基线：O-01~O-34。`

- `2026-09-09: [新增] M0 工程引导（T-M0-01~05，主程序）落地：darkest/Darkest.sln（dotnet 规范格式，含 Darkest 与 Darkest.Tests 两工程）、darkest/Darkest.csproj（Godot.NET.Sdk/4.6.1；TFM 经自定义属性 DarkestTargetFramework 间接取值，默认 net8.0；Compile Remove tests/** 与 .godot/**）、darkest/Darkest.Tests.csproj（MSTest 3.6.4 + Microsoft.NET.Test.Sdk 17.14.1，经典 VSTest 模式，EnableDefaultCompileItems=false，内核源码直编）；仓库根 Directory.Build.props（仅对 Darkest.Tests 把 obj/bin 重定位到 tests/ 下，防同目录双 csproj 生成物互吞）。res:// 目录骨架按 blueprint §3 建齐（空目录 .gitkeep）。新增内核文件：scripts/core/rng/IRngProvider.cs（签名对齐 blueprint §9.8）、scripts/core/rng/RngProvider.cs（SplitMix64 固定种子、DrawCount 单调审计）、scripts/core/events/BattleEvent.cs（不可变事件基）、scripts/core/events/RngDraw.cs（抽取审计记录）、scripts/core/events/CombatLog.cs（追加式、Append&lt;T&gt; 赋 0 基序号）、scripts/gameplay/sim/director/BattleSession.cs（BattleSession(seed[,IRngProvider]) 组合根雏形、零业务）、scripts/core/math/BattleMath.cs（PhysicalHit/SpiritHit 逐字 combat_math §2.1/§2.2/§2.3，System.Math 全限定防命名冲突）。测试：tests/FormulaSmokeTests.cs（§7.1 两样例复算 + 下限 1）、tests/RngSmokeTests.cs（同 seed 同序列 / DrawCount 单调 / 显式注入 / CombatLog 防篡改断言）。工具：tools/check_godot_refs.py（`using Godot` 白名单静态检查 + 负向自检内置）、.github/workflows/ci.yml（build→test→静态检查→自检）。暴露接口：IRngProvider{NextPercent,NextInt,DrawCount}、BattleMath.PhysicalHit/SpiritHit、CombatLog.Append&lt;T&gt;、BattleSession(seed[,rng])。依赖：Godot.NET.Sdk/GodotSharp 4.6.1、MSTest 3.6.4（NuGet 离线缓存）；本机仅 .NET SDK 10.0.400（离线）→ 冒烟用 -p:DarkestTargetFramework=net10.0 覆盖，net8.0 默认留给 CI/.NET 8 编辑器机器。验证实绩：dotnet build Darkest.sln 绿（Darkest.dll + Darkest.Tests.dll，0 警告 0 错误）；dotnet test 8/8 绿（20:33 复测；复验受本机「应用程序控制策略 0x800711C7」拦截，属本机执行策略非代码问题，CI 兜底）；白名单静态检查基线 0 命中 + 负向自检 PASS。开放：编辑器打开冒烟（判据 #1/#2）待装有 Godot 4.6 (.NET) 编辑器的机器；详情见 tasks/m0_bootstrap.md §5 冒烟记录。_`

> 主程序协议提醒：每次新建/修改 `.cs` 后，必须在本节追加一行 `_YYYY-MM-DD: [新增/修改] res://…/XXX.cs。暴露接口：…。依赖：…。_`

- `2026-09-09: [复核] M0 测试全量 8/8 最终复验绿（21:22，解除本机应用程序控制策略拦截后）。判据 #3 关闭；判据 #1/#2（Godot 4.6 (.NET) 编辑器打开）仍待编辑器机器。_`

- `2026-09-09: [新增] M1 阵型骨架（T-M1-01~05，主程序）落地：darkest/scripts/core/contracts/ 契约集（IFormation/SlotState/SlotKind/FormationSide/UnitId/SlotLayout/SlotChange/DisplaceResult/FormationRules，blueprint §9.1 + data_schema §2.1）；darkest/scripts/gameplay/sim/board/ 新增 UnitRuntime（Id/Side/Weak 最小占位）、ObstacleRuntime（hp 可空）、FormationBoard（槽位三态/逐级交换链 TrySwapChain/向中靠齐 CloseUp/障碍移除/只读快照 CreatePreviewSnapshot + DryRunSwapChain，行为开关读 FormationRules）、FormationBoardFactory（编成+障碍装载）；darkest/data/formation.json（§3.6 逐键：槽常量/center_outward/编成/obstacles=[]/rules 九键）；darkest/scripts/data/FormationConfig.cs（System.Text.Json snake_case 绑定 + fail-fast 校验）。测试 darkest/tests/BoardTests.cs 25 用例（T-M1-01~05 全判据：编成装载/交换链示例逐位一致/属性式无空位/边界不动/撞障碍交换/靠齐原子重放/虚弱跨越/全虚弱留空/障碍不阻挡/敌方对称/障碍建模/移除→靠齐/dry-run 与真实结算逐项一致+无副作用+可回放）。暴露接口：IFormation{GetSlot,UnitAt,OccupiedPositions,TrySwapChain,CloseUp,CanCloseUpIn}、FormationBoard{SlotKindAt,CreatePreviewSnapshot,DryRunSwapChain,RemoveObstacle,TryGetObstacleHp}、FormationConfig.Parse、FormationBoardFactory.CreatePlayerBoard/CreateEnemyBoard。依赖：M0 内核与测试工程。验证实绩：dotnet test 33/33 绿（0.88s）。踩坑：CloseUp 链式交换曾以槽位号直作数组下标（整体偏移 +1）导致死循环→步骤无限追加→testhost 内存暴涨 OOM（用户实机观测 memory 持续上涨）；已修复（位置→下标显式转换）并加收敛上限守卫（超限抛异常）；详见 m1_formation.md §5 冒烟记录。_`

- `2026-09-09: [复核] M0 判据 #1/#2（编辑器可用）本机补验绿：Godot 4.6.1 stable mono（E:\Godot_v4.6.1-stable_mono_win64）--headless --path darkest --import 通过（first_scan/update_scripts_classes/loading_editor_layout DONE，退出码 0；无项目导入错误/SCRIPT ERROR/error CS；仅 %APPDATA% 设置写与证书库告警属环境噪音）。编辑器侧 --build-solutions 因本机离线 net8 restore 失败（同 m0 §5.1），经 CI/.NET 8 机器复验；编辑器已能加载 .godot/mono/temp 的 Darkest.dll。手写 sln/csproj 与编辑器实况无出入（未被改写、无第二 csproj 误导入）。_`

- `2026-09-09: [新增] M2 结算核心（T-M2-01~10，主程序）落地：data/ 新增 units.json（7 原型，§3.1）、tuning.json（§3.7 全键）、morale_events.json（§3.3 全 14 行）；scripts/data/ 新增 UnitsConfig（P1/P4/P7）、TuningConfig、MoraleEventsConfig（P9 缺行报错 + 线性 Get）、BalanceTable（只读快照）。BattleMath 扩为完整公式库（HitRate/PhysicalMitigation/MentalMitigation/ActualEffectChance/DeathDoorSurvivePercent/SpeedFloatMultiplier/ApplyDamageRounding，T-M2-01）。contracts 新增 UnitStats、ITurnSequencer；UnitRuntime 扩属性/HP/减益修正/眩晕/流血/Morale/生效速度口；FormationBoardFactory 接入 units 属性、FormationBoard 增 UnitRuntimeAt/RemoveUnitAt/UnitAtPosition。管线：HitStep（T-M2-04）、DamageStep（多段独立实例，T-M2-05）、EffectsStep（乘法概率/无概率直挂/眩晕流血压根，T-M2-06）、DisplaceStep（唯一 >= 例外，T-M2-07）、MoraleLedger（表驱动派生/虚弱−5 每回合≤1/团队合并一次，T-M2-08）、WeakDeathsDoor（进虚弱/崩溃调用点/死门，T-M2-09）、DamagePipeline（§2 固定次序装配 + SkillFixture/SkillDisplace，O-12 默认）。core/events 增 BattleEventTypes（Hit/Crit/Damage/Morale/Effect/Displace/DeathDoor/WeakEnter/Death/CollapseRoll）。测试：FormulaTests（§7.1 八样例+边界）、DeterminismTests、TurnOrderTests、CombatResolutionTests（集成+同命令流确定性）。验证实绩：dotnet test 64/64 绿（0.92s）；using Godot 基线 0。踩坑：record 位置参数缺 [JsonPropertyName] 静默 null；查询缓存未初始化；默认编成 1 号位=坦克；靠齐会填槽——详见 m2_combat_core.md §7 冒烟记录。移交：护盾/流血钩子/崩溃池解析/撤退行 → M3/M4；组合行三默认已锁单测（AOE+暴击折扣优先/虚弱 −8 与 −5 叠加/跨阵营同速我方先手），建议登记 O-31+。_`

- `2026-09-09: [复核] 策划拍板 #166~#170 落点（主程序）：O-32（AOE+暴击→折扣 −5 优先）与 O-33（−8/−12 与 −5 叠加）、O-34（跨阵营同速我方先手）核对与 M2 实现一致，无需改动。O-11（#169）：tuning.retreat_formula + BattleMath.RetreatBaseRate/RetreatFinalRate（M5 撤退按钮用，含钳制用例）。O-21（#170）：DamagePipeline 增 SkillFixture.ExplicitMoraleEffects——显式 morale_effects 取代精神派生（威吓箭 −4 不叠加 −8），CombatResolutionTests 锁语义。dotnet test 66/66 绿。_`

- `2026-09-09: [新增] M3 技能与角色（T-M3-01~08，主程序）落地：data/skills.json（43 条逐字 skill_data §1~§5）；scripts/data/SkillsConfig.cs（SkillTemplateConfig 13 字段 + TargetSpec/DamageSegment(flat|missing_hp)/EffectSpec/DisplacementSpec/UseLimit/MoraleEffect + snake_case 词表转换器 + SelfSlots（'all'/数组）+ 内校验 43/原型计数/P2 越界/O-24 成对/every_n_rounds 禁用）；contracts 增 ISkillUseResolver/Availability/AvailabilityReason（tooltip 逐字 ui_spec §4）、SkillLoadout（恰 5 冻结）；sim/skill 增 SkillsTargetResolver（scope 五类→占用含障碍→槽升序）、SkillUseResolver（①携带②站位③目标部分非空④CD/次数，敌方跳过携带，零随机幂等）、SkillRuntimeState（CD/每场次数台账）、SkillExecutor（伤害路径：flat/missing_hp 逐目标实际倍率/动作级 AOE/逐目标 push 位移；支援路径：固定值治疗/stat_mod/士气；显式 morale_effects 钩子；use_limit 消耗）；core/events 增 HealEvent/SelfDamageEvent。测试：DataGateTests（抽样/反例/roundtrip）、SkillAvailabilityTests（8）、SkillExecutionTests（段/次序/未命中/威吓箭−4/战吼/急救/missing_hp/位移殿后）、SpecialSkillTests（per_battle 3/self_damage 6·8/all 4/missing 0.8·0.9/多段 2/我方 mental 0·敌方 3 + 构造反例）、SkillCoverageTests（4 角色 × 6 位置 ≥ 表声明数且 ≥2，换位不废人 + 急救 all→[1] 反例 + 敌方池引用）。暴露接口：SkillsConfig.Parse/Serialize、SkillTemplateConfig、ISkillUseResolver、SkillExecutor.Execute、SkillTargetResolver.Resolve、SkillRuntimeState。验证实绩：dotnet test 102/102 绿（1.56s）；using Godot 基线 0 + 自检 PASS。踩坑：snake_case 枚举词表、record 构造 ×2、根属性漏绑、多目标×段事件数、missing_hp 逐目标倍率——详见 m3_skills_units.md §9 冒烟记录。移交：shield/taunt/guard_attach/next_attack_boost 执行（buff 生命周期）与 self_damage 死门链归 M4；启动门禁统一装配归 M5。_`

- `2026-09-09: [新增] M4 士气与生存（T-M4-01~09，主程序）落地：data/buff_defs.json（16 条状态定义 + BuffDefsConfig 模型/词表/校验）；contracts/IBuffLedger（Add/AddCharged/Remove/Has/Buffs/Charges/StatModTotal/HasStateFlag/PercentMod/TickRounds/AnyOfKind）+ sim/buffs/BuffLedger（refresh/stack/none 叠层、护盾次数、回合递减）；MoraleLedger 扩：崩溃判定（事件触发跨 0 → 恰一次、HP 归零路径去重、余烬标记 #67）、RollCollapse（美德率=10%+韧÷2、折磨池三选一、美德上限 1 保留旧 #94、折磨回 50 结束、美德归零→消除→回 50→重判 §8）、满值处理（随机美德+全队+10 once+回 50，#55/#151/#100）、SupportSlotRegen（10+健康支援×5+韧÷20 钳 25，虚弱互不提供 #59）、Apply 返回净变动+余烬/折磨-结束维护；sim/buffs/ShieldGuard（护盾按次数挡物理整攻击无效、精神穿透、AOE 整挡；护卫每回合≤1 重定向按保护者防御、只物理 O-22）；WeakDeathsDoor.TryRecover 归队（#164 HP=10%）；DamagePipeline/SkillExecutor 注入 buffs+shield，士气步后崩溃/满值触发、伤害前拦截与重定向。测试：MoraleTests（钳制/派生/跨0/余烬/去重/美德率边界/满值保旧+once/回升/归队）、ShieldGuardTests（挡-穿-消耗-重定向-每回合≤1）、StateMachineTests（Afflicted→Normal/Virtuous→Afflicted/Virtuous→Virtuous）。验证实绩：dotnet test 121/121 绿（1.64s）；using Godot 基线 0。口径落点：O-22 护卫只挡物理、O-25 护盾刷新新满次数、O-26 护卫 3 回合占位、O-27 美德池仅勇猛、O-09 随机抽取、O-03 任意类型保留；恐惧/自私/失控 33% 动作钩子数据已录、执行归 M5 动作层。记录：m4_morale_survival.md 实施记录。_`

- `2026-09-09: [新增] M5 敌人与 UI（T-M5-01~09，主程序）落地：data/enemy_ai.json（3 原型优先级表 + random 开关 + #96 预留）+ EnemyAiConfig；sim/enemy/EnemyAi（谓词 self_slot_in/target_slots_occupied_min/player_morale_all_at_least、taunt 首目标、15% 次优先 default off、全不可用空过）；sim/director/BattleDirector（回合状态机：RoundStart 事件/增援[第 6 回合填首个空位+4 位上限/满编 +攻+速占位]/支援位+3/虚弱回升/行动序列固定点 BuildRoundOrder/玩家命令/敌方阶段/撤退[基础率展示无抽取+结算扰动+失败当回合禁用 #118]）；sim/director/BattleProjector（9 条必显只读投影 + dry-run 位移预览同源）+ scripts/ui/IBattleView（UI 唯一依赖面）；事件 RoundStart/Reinforcement/Retreat；UI 薄层骨架（Battle.tscn/BattleRoot/BattleUi，待 .NET 8 编辑器验证）。测试：EnemyAiTests（4 场景+确定性）、DirectorMilestoneTests（增援/满编增益/撤退成功·失败·当回合禁用·钳制/同 seed 同命令流镜像一致）、BattleProjectorTests（行动序列/撤退数字==公式/命中率/预览==结算/技能 tooltip）。验证实绩：dotnet test 134/134 绿（1.87s）；using Godot 基线 0。口径落点：O-19 random off、O-20 增援默认起手值+增益占位、O-28 表序直译（威吓箭③不可达已记录）、O-21 按 target 直译、O-31 按 0.5 倍率+攻击 13、O-11 展示=基础率无抽取。移交：BattleUI 四分区控件/演出/配色与战斗三态出口为 M5 UI 人工走查项，待编辑器构建核验。_`

- `2026-09-09: [新增] M5 编辑器可视补强（86fbbc3）：project.godot 设 run/main_scene=Battle.tscn；BattleRoot 演示驱动（每 1.2s 推回合+撤退按钮实时百分比+点击含 #118 拒绝）；全权限下 headless 运行 Battle 场景成功（回合 1 日志+撤退 47%）；此前 signal 11 二分定位=默认沙箱拦截 Godot 运行时 .NET 宿主初始化（全权限 0 崩溃，与 dotnet test 同源）；Darkest.csproj TFM 实化再还原；134/134 绿。_`

- `2026-09-09: [新增] M6 验收（T-M6-01~07，主程序）落地：…_`（见上段 M6 记录）

- `2026-09-09: [修订·架构师] v0.44 目标语义同步（#178/#179，O-38）：策划拍板"范围=候选池、单体选一"——旧实现（SkillTargetResolver 全命中 / SkillExecutor 全结算）与 #176 46% 基线、combat_math §7.3 实测全部作废。架构文档已对齐：data_schema P11（aoe 标签一致性）+ §3.2 生效语义、blueprint §5b 时序/§9.3/§9.10、m2 §2 目标数量语义 + O-32~34 收口（#166~#168）、m3 T-M3-05/06 改造清单（候选池+选一、双段同目标 #179）、m5 T-M5-02/06/07（AI 选一 O-39 / UI ③b）+ 撤退 #169、m6 T-M6-08 重新基线（含 SimSanity：劈砍·精准射击·急救恰 1 目标）、open_issues O-01~O-39 收口（O-11=#169、O-21=#170、O-31 关闭、O-36 ×2 否决）。主程序待办：SkillTargetResolver/SkillExecutor 改造 → M5 ③b/AI 选目标 → 重跑 300 场新基线（T-M6-08）。数据 JSON 零改动。_`

- `2026-09-09: [主程序·v0.45 改造完成] #180~#182 全链路落地（O-40/O-41）：skills.json 增 4 条 *_move（pool_external + move_range + distance 坦克1/战医政2；其余 43 条零改动）→ 47 断言 + P12 校验；SkillTargetResolver 支持 move_range（候选=自身±N 被占用战斗位、空位 NoTarget、障碍可交换）；SkillExecutor.ExecuteMovePath（交换、不过抗性、无伤害/士气/死门、DisplaceEvent 计入位移 KPI）；增援改单按钮两步 Reinforce(A,B,X)（#181 推翻双按钮：X 有人交换/空直入、发起者消耗行动、同回合≤1、UI 两步高亮 5/6→1~4）；「移动」常驻按钮（战斗位行动者、move_range 候选高亮复用 ③b）；技能栏去内联原因文字（#182，走悬停 tooltip、候选高亮保留）。测试：MoveSkill P12 抽样 / ReinforceTests / 47 断言 / P3 池内口径。**M6-08 v0.45 含移动复测（300 场）：胜率 20%、avg 17.48**（v0.44 27%/18.03 无移动口径作废）；151/152（判据 A 诚实红）。_`

- `2026-09-09: [修订·架构师] v0.45 同步（#180~#182，O-40/O-41）：策划新增通用「移动」（池外常备 `pool_external` + `move_range` + distance 坦克1/战医政2；目标=自身±N 格内被占用位并交换、空位不可选、不过抗性、不死门；技能总量 43→**47**）、增援改单按钮两步 `Reinforce(A,B,X)`（推翻双按钮）、技能栏去内联要求文字但候选高亮必需。架构落盘：data_schema §2.1/§3.2（`pool_external`/`move_range`/`distance`）+ **P12 校验** + §5.4 移动示例 + 47 口径；m2 T-M2-07（自我移动不过抗性）；m3 T-M3-01/02（47/P12）/05（move_range）/07（4 条移动）/08（P3 池内口径）；m5 T-M5-05/06/07（增援两步、去文字、候选高亮必需，判据 M-E）；m6 T-M6-02（SemiRandom 含移动 + 两步增援）/04（移动计入位移 KPI）/08（复测含移动；#180 平衡提醒——胜率预计略升，旧 27%/18.03 待复测）；open_issues O-40（落地指针）/O-41（移动细节）。`

- `2026-09-09: [语义修正·主程序] 增援/移动改为【两点直接互换】（d290e10，用户实测 bug）：曾用 TrySwapChain（逐级推移）实现 #41a 增援与 #180 移动 → 选 B=5/X=3 时原 3 位的人到 4（途经单位整体后移），不符「X 有人则交换」语义。**规则钉死：TrySwapChain（逐级交换链）只属位移技能（push/pull 推 N 格）；增援/移动是两点直接互换**——新增 `FormationBoard.SwapSlots(a,b)`（途经槽位不动；空/越界/相同→false），`Reinforce` X 有人→`SwapSlots(bSlot,x)`、`SkillExecutor.ExecuteMovePath`→`SwapSlots(from,to)`；X 空仍 Remove+Place 直入。测试：ReinforceTests 补「5→3 原 3 到 5、4 不动」场景；新增 MoveSkillTests（候选±N/空位 NoTarget/直接互换/不过抗性/无伤害死门/非法目标忽略）。M6-08 复测（300 场）：胜率 **24%**、avg **18.95**；156/157（判据 A 诚实红）。_`

- `2026-09-09: [fix-pack P0~P3 完成·主程序] 依 doc/architecture/tasks/m6_fix_pack.md 按序落地（一步一提交，红线：不调数值）：**P0/O-47**（6a1124e）BattleOutcome{Ongoing,Victory,Defeat}+Outcome 投影（我方优先判负）+RunFullRound 胜负前置守卫+BattleRoot 每轮先查结果+结算面板+BattleOutcomeTests(7)；**P1/O-46**（96bec90）敌人池内选一三层（taunt 加权抽取写 RngDraw → 原型偏好 lowest_hp/backmost/lowest_morale → 槽号兜底；AOE 全池；唯一候选不掷骰）+enemy_ai 新键 target_preference/taunt_weight=3+恐惧低语 slots[1]→[1,2,3,4]+buff_defs taunt 改加权+EnemyAiConfig P13 校验+EnemyTargetingTests(9)；🔴 顺带修 EnemyAi/玩家侧共 4 处 SkillTargetResolver **参数序颠倒**（敌方候选池按敌板解析恒打 1 位 = O-44 退化根因）；**P2/O-42**（2239916）策略规则化（集火最低 HP／治疗仅真伤员 HP%<60%／移动条件化／增援虚弱或 HP%<30% 拉起／控制打攻击力最高）+兜底标注 no_policy_fallback+PolicyTests(5)；**P3 复测**（bbea6c1）300 场数据零改动：终局 **敌灭 0 / 我灭 234 / 撤退成功 58 / 强切 8；胜率 19%、avg 18.25**；**口径健康度全绿**（敌方命中 1位17% 2位26% 3位11% 4位18% 5位15% 6位14%；移动使用 0/17368）→ P1 真生效；结论 **复测偏难**（正面 0 胜），按 P4.2 待拍板回补玩家输出；M6FixPackTests 固化诊断。全套 178/179（唯一红=判据 A 诚实红）。_`

- `2026-09-09: [reconcile·架构师·v0.46] 流程与镜像收口（#183~#187 / O-42~O-48）：① 立"写者与转写流程"——策划/主程序提交、架构师统一编号转写（O-42~O-47 原稿系直写，已复核：编号连续、归属格式统一）；② 反向指针：O-42→`tasks/m6_fix_pack.md` P2/P3、O-44/O-45/O-46→P1、O-47→P0；③ data_schema 镜像：§2.1 新增 TargetPreference 枚举、§3.2 生效语义补"增援/移动=两点直接互换 SwapSlots，逐级交换链仅 push/pull"（**O-48**/d290e10）、§3.4 taunt 改加权、§3.5 新增 `target_preference`+`taunt_weight` 与"池内选一"三层语义（taunt 加权抽 RngDraw → 原型偏好 → 槽号兜底）、§5.3 恐惧低语 `slots`→`[1,2,3,4]`（#186）、§4.2 新增 **P13** + P7 增补；④ 状态回写：O-38 已落地但其 27% 基线因 O-42 作废、O-39 被 #185/#187 修订（槽号最小降兜底）、O-40/O-41 v0.45 已落地；⑤ `tasks/m6_fix_pack.md` 追加 §7 架构镜像回执；⑥ 未代写项：glossary §2「池内选人」与 GDD §1.3.1 移动表述待策划同步（架构侧不代写策划文档）。接口基线不变。_`

- `2026-09-09: [纪律新增·策划·v0.48] 新增 §0 原则第 14 条「文档回读纪律（数量／次数／频率专项）」：每完成一个里程碑或合入一批改动后，实现方须**拿着文档当验收清单逐条回读实现**，重点核 4 类规则——① 命中几个 ② 补几个 ③ 隔几回合 ④ 判哪一方胜负；不一致当场登记 O-nn。**立条依据**：本项目已连续 5 次「文档对、实现错」——O-38 目标语义（范围当全命中）、#177 换位增援口径缺失、O-48 交换链误用于增援/移动、O-47 胜负硬编码、O-52 超时增援每回合只补 1 个，**5 次全部命中这 4 类**；且**单测抓不到**（测试照错实现写，套件一直绿），偏差只会让设计意图静默失效，5 次里 4 次由**用户实机**发现。本条与第 8 条「测试钩子前置」互补：第 8 条防"没测"，本条防"测错了对象"。同时同步 §1 文件注册表（state.md 决策表 #1~#194、skill_data 44 条 = 36 池内 + 1 通用移动 + 7 敌、open_issues O-01~O-48 + O-49~O-52 待编号、tasks 增两张执行包卡、darkest/ 实测 156/157 与各待修项）。策划 v0.48 另落 #189~#194（强制目标点击／可插拔契约／通用 move+move_distance／嘲讽挂自己／士气 buff 效果+O-27 定值 +3／超时增援每 3 回合一波全补）+ 新任务卡 `tasks/feat_pack_01.md`。_`

- `2026-09-09: [修订·策划·v0.48] P3 复测结果解读与顺序约束修正：P0~P3 已由主程序执行（bbea6c1），结果 **胜率 19%（终局 敌灭 0／我灭 234／撤退成功 58／强切 8）、avg 18.25、口径健康度全绿**。策划解读两点：① 🔴 **「正面胜利 0 次」是最严重的平衡事实**——19% 全部来自撤退与强切，玩家从未靠打光敌人取胜；② 🔴 **该 19% 不得用于调数值**——`feat_pack_01` 的 F0~F3 尚未落地，其中 #193（士气 buff 效果＝纯玩家增益）与 #194（超时增援改一波全补＝改变敌方压力）**必然会改动结果**，落地后须重跑复测（P3 v3）。已在 `tasks/m6_fix_pack.md` §P3.2 追加该约束，并记录 P3.1.1 查明的新事实：**O-44 的真根因是 `SkillTargetResolver` 参数序颠倒（4 处，敌方候选池按敌板解析）**，"槽号最小"只是放大器——此类"参数序/方向写反"被归入第 14 条回读纪律的高危类（编译不报错、测试也照错实现写）。_`

- `2026-09-09: [reconcile·架构师·v0.48] feat_pack_01 三项回执 + 顺序约束（#189~#194 / O-49~O-53）：① **顺序约束双向确认**——`feat_pack_01` F0~F3 必须先于 `m6_fix_pack` P3 复测（P3 v2 的 19% 作废依据）；写入 README §4 红线/§6 交接语 + 两卡互挂（feat_pack §5 / m6_fix_pack §7）② **F1.4 嘲讽 self 字段裁定**（⇒ **O-53**）：`hit_mod=0`（self/team/adjacent 类占位、引擎不掷命中）、`range_axis="none"`（不保留 melee）、`polarity="positive"`+`dispellable=false`、`tags=["control"]`、`self_slots=[1,2]` 不变 ③ **O-49~O-52 编号确认采用**并转写（对应 F0/F1/F2/F3），新增 O-53；回写 O-20（M=3+每波补齐 #194）、O-27（振奋 +3/回合 #193）、O-39/O-46（taunt 来源 = 我方带 taunt buff 者，#192）④ **订正 `feat_pack_01` 3 处决策号错位**（通用 move=#191、嘲讽 self=#192、士气 buff=#193；以 state.md 为准）⑤ **data_schema 镜像**：§3.1 `move_distance`；§3.2 总量 **44**（36 池内 + 1 通用 `move` + 7 敌）、`owner_unit` 池内必填/池外勿填、`pool_external` 恰 1 条且按标志过滤、`target.distance` 切片不填（从单位读）、§5.4 示例改通用 `move`；§3.4 taunt 极性/驱散/self 施加 + 美德池 4 项（振奋 +3）+ modifiers 消费映射；§3.7 `overtime_reinforcement` 增 `wave_interval_rounds:3 / refill:all_empty_slots / full_branch:buff_all_each_wave`、`collapse.virtue_pool` 4 项；**P12 改写 + 新增 P14（move_distance/池外恰 1/distance 必须 null）、P15（士气 buff 白名单 + proc 唯一概率源 + 美德池 4 项 + RngDraw）**，P13 增补 taunt 来源禁依赖 ArchetypeId ⑥ m3/m5 加 v0.48 口径公告（44 条 / 强制点击 / taunt self / 增援 M3）。接口基线不变；未代写项（morale §6 振奋数值、buff.md 嘲讽极性、skill_data §4.5 通用 move）留策划。_`

- `2026-09-09: [reconcile·架构师·v0.49] F4 数值补偿 + 增援 M 公式化 镜像（#195/#196/#197 / O-54）：① **修正上一条 v0.48 记录里的旧口径**——`data_schema §3.7` 原写 `wave_interval_rounds:3`（#194 旧版），现改为 **公式导出**：`{trigger_round:6, refill:"all_empty_slots", full_branch:"buff_all_each_wave", safety_factor:0.8, wave_interval_min:3, wave_interval_rounds:"导出值（非手填常数）"}`，语义 `M = max(3, ceil(敌满编总HP ÷ (D×0.8)))`、`D`=我方每回合对敌总伤害**实测值**；当前推导 **M≈9**（敌总 166、D≈24；第二波第 15 回合 > 战斗约 10 回合 → 实际只有首波生效）。**任何改敌 HP/我方输出都必须重算 M**。② **§3.1 敌 HP 两档**：现生效 60/60/46/41（#171/#173）→ **F4 目标 48/48/37/33（总 166，#195）待落地**。③ **§3.2 倍率**：`missing_hp` 系数 **0.4/0.5 → 0.6/0.7**、劈砍 **0.9 → 1.0**、**横扫保持 0.6（不得顺带改）**。④ **新增 P16**：启动须有 `safety_factor>0`/`wave_interval_min≥1` 且**禁止把 M 当手填常数**；运行时 `M == max(min, ceil(enemy_full_hp ÷ (D×safety_factor)))`；**复测报告缺实测 D（与口径健康度 1 号位<50%）即判基线无效**。⑤ `m6_verification` 镜像：T-M6-01 日志增「实测 D + 口径健康度」；T-M6-03 前置升为 **F0~F4**、反推表 HP 行更新、完成判据增 D/M；§4 增 checkbox；§7 增 O-54。⑥ open_issues：**O-43 由待定→已定（P4.1 推荐 (c)+(d)：承认 6v4、按 6v4 平衡敌人数值 + 难度靠 AI/时间压力）**；O-52 改「M 公式化」；O-06 改两档 HP；O-20 同步；新增 **O-54（数值补偿与 M 校准契约：F4 + 重算 M + 输出 D + 判定分叉：落 [40,70] 不做第二轮，<40% 才 Step 3）**。⑦ `feat_pack_01` 加 **§5.1 v0.49 回执**、§4 数值红线改为「仅限 F4 与 F3 tuning 键」；`m6_fix_pack` §7 加 P4 落地回执、**顺序升级为 F0~F4 → P3 v3**；README 红线第 10 条与交接语同步（三条流水线）。接口基线不变。_`

- `2026-09-09: [reconcile·架构师·v0.50] 事件族/事件流/可观测性 镜像（#198~#201 / O-55~O-57）：① **data_schema 新增 §8 事件族清单**（运行时契约、非 JSON 配置；来源 `modules/logging.md` + 落地卡 `feat_pack_02` G0）：§8.1 现状 18 类与硬缺口（**无 `SkillUseEvent` → 「技能使用率」KPI 为空**、buff 生命周期零事件、基类无 `Round`、`SwapEvent` 未记 B）；§8.2 **新增 14 类**（含 `SkillUseEvent`/`BuffApplied`/`BuffRemoved`/`TurnSkipped`/`BattleEnd`/`EnemyDecision`/`ReinforcementElastic`/`StatMod`/`Obstacle` 等，字段+级）；§8.3 **补字段 7 处**（`BattleEvent.Round`、`SwapEvent.MovedUnit+Kind`、`DamageEvent.SkillId`、`EffectEvent.Source`、`DeathEvent.Cause`、`HealEvent.Source+SkillId`、`DisplaceEvent.SkillId+Source`；尾部追加+默认值）；§8.4 **4 级分级**（级 4 `RngDraw` 必须记录、默认隐藏）；§8.5 消费者只读红线（DevLog 不得反向写/产生抽取/改变 Sequence；不得每帧重建全量文本）；**§8.6 新增 P17 事件字典完整性**（技能可读/buff 可读/Round 单调/不引入抽取/可重建终态）；§8.7 与 ui_spec·combat_math·verification·Project_Memory 边界。② **§3.7 增 #198 自适应 M**：`elastic{enabled,k_rounds:2,idle_output_slots:3,max_bonus:3}`，`M ∈ [M_base, M_base+3]`，K=2 窗口内 ≥3/4 存活战斗位未用 `output` 技能 → M+1、达标回落、**首波固定第 6 回合**、满编 +攻/+速每波叠加无上限（防苟活兜底）；**P16 扩展为 #196+#198**（弹性参数范围/M 区间/首波不受弹性影响/每次变动写 `ReinforcementElasticEvent`）。③ **blueprint §6.2** 增事件流说明：事件字典指向 data_schema §8、🔴 纪律（UI 要的数字必须先从事件流算出、不得为消费者造数据）、基类 `Round`、`SkillUseEvent` 为统计前提、只读红线、字段尾部追加兼容性、**投影链 vs 事件流链互不替代**。④ **open_issues**：O-54 并入 #198；新增 **O-55（事件字典 G0 / #199）、O-56（详情框 G3 / #200）、O-57（敌方意图预留 G4 / #201）**；并处理 **pack02 原 O-54~O-56 重号**（与 v0.49 O-54 冲突）→ 按"先到先得+引用面最小"**顺延为 O-55~O-57**，表头记录。⑤ **feat_pack_02** 卡头改号 + **G0 优先**顺序约束（G1/G2 以其为输入、M6 技能使用率 KPI 依赖）+ **§6 架构镜像回执**（含未代写项：ui_spec §9 敌方意图改"预留不启用"、verification 技能使用率注明依赖 O-55）。⑥ **m6_verification** T-M6-01/§7 挂"技能使用率 ← `SkillUseEvent`（O-55）"依赖，明确"缺它时 KPI 不得以空值通过"。⑦ README 增红线第 11 条（可观测性优先序）与包清单（3 个包）、交付清单 v0.50。接口基线不变。_`