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

---

## 1. 文件注册表（截至 2026-09-09）

### 策划交付层（doc/，只读源，禁止改动）
| 文件 | 作用 | 备注 |
|---|---|---|
| README.md / GDD.md / state.md / CHANGELOG.md | 入口 / 主文档 / 决策表 #1~#165 / 演进史 | state.md = #N 溯源 |
| modules/combat_math.md | 全公式 + 9 层随机 + #157/#158/#163 口径 | 挡在所有代码前 |
| modules/skill_data.md | 43 条技能实值（36 我方 + 7 敌方） | 数据抄录唯一来源 |
| modules/{glossary,skill,character,morale,formation,buff,enemy,ui_spec,verification,review}.md | 各系统规格 | modules 层为准 |

### 架构拆分层（doc/architecture/）
| 文件 | 作用 | 备注 |
|---|---|---|
| _conventions.md | 写作规范 / 任务卡模板 | 定稿 v1 |
| blueprint.md | 总体蓝图（五层/目录/接口/确定性/测试钩子/风险） | 草案 v0.1 |
| data_schema.md | 数据 Schema + P1~P10 校验 | 草案 v0.1 |
| open_issues.md | 开放题 O-01~O-34 | 滚动维护 |
| README.md | 架构入口 + 看板 + 交付清单 | 本套入口 |
| tasks/m0~m6 共 7 个文件 | 53 张任务卡 | 草案 |

### Godot 工程（darkest/ = res://）
| 路径 | 状态 | 备注 |
|---|---|---|
| project.godot | 存在（Godot 4.6 .NET / assembly_name=Darkest） | 只读实况 |
| scenes/ scripts/ data/ resources/ tests/ | 未建（M0 建立） | 顶层铁律 _conventions §3 |

---

## 2. 更新记录（追加式）

- `2026-09-09: [新增] doc/architecture/ 全套（_conventions / blueprint / data_schema / open_issues / README / tasks/m0~m6）。架构师初始化本记忆体。接口基线：IFormation / ITurnSequencer / ISkillUseResolver / IMoraleLedger / IDeathsDoor / IBuffLedger / IEnemyAi / IRngProvider / IShieldGuard / IPlayerPolicy（blueprint §9）。依赖：doc/ 策划 16 件；darkest/project.godot（Godot 4.6 .NET）。开放题基线：O-01~O-34。`

- `2026-09-09: [新增] M0 工程引导（T-M0-01~05，主程序）落地：darkest/Darkest.sln（dotnet 规范格式，含 Darkest 与 Darkest.Tests 两工程）、darkest/Darkest.csproj（Godot.NET.Sdk/4.6.1；TFM 经自定义属性 DarkestTargetFramework 间接取值，默认 net8.0；Compile Remove tests/** 与 .godot/**）、darkest/Darkest.Tests.csproj（MSTest 3.6.4 + Microsoft.NET.Test.Sdk 17.14.1，经典 VSTest 模式，EnableDefaultCompileItems=false，内核源码直编）；仓库根 Directory.Build.props（仅对 Darkest.Tests 把 obj/bin 重定位到 tests/ 下，防同目录双 csproj 生成物互吞）。res:// 目录骨架按 blueprint §3 建齐（空目录 .gitkeep）。新增内核文件：scripts/core/rng/IRngProvider.cs（签名对齐 blueprint §9.8）、scripts/core/rng/RngProvider.cs（SplitMix64 固定种子、DrawCount 单调审计）、scripts/core/events/BattleEvent.cs（不可变事件基）、scripts/core/events/RngDraw.cs（抽取审计记录）、scripts/core/events/CombatLog.cs（追加式、Append&lt;T&gt; 赋 0 基序号）、scripts/gameplay/sim/director/BattleSession.cs（BattleSession(seed[,IRngProvider]) 组合根雏形、零业务）、scripts/core/math/BattleMath.cs（PhysicalHit/SpiritHit 逐字 combat_math §2.1/§2.2/§2.3，System.Math 全限定防命名冲突）。测试：tests/FormulaSmokeTests.cs（§7.1 两样例复算 + 下限 1）、tests/RngSmokeTests.cs（同 seed 同序列 / DrawCount 单调 / 显式注入 / CombatLog 防篡改断言）。工具：tools/check_godot_refs.py（`using Godot` 白名单静态检查 + 负向自检内置）、.github/workflows/ci.yml（build→test→静态检查→自检）。暴露接口：IRngProvider{NextPercent,NextInt,DrawCount}、BattleMath.PhysicalHit/SpiritHit、CombatLog.Append&lt;T&gt;、BattleSession(seed[,rng])。依赖：Godot.NET.Sdk/GodotSharp 4.6.1、MSTest 3.6.4（NuGet 离线缓存）；本机仅 .NET SDK 10.0.400（离线）→ 冒烟用 -p:DarkestTargetFramework=net10.0 覆盖，net8.0 默认留给 CI/.NET 8 编辑器机器。验证实绩：dotnet build Darkest.sln 绿（Darkest.dll + Darkest.Tests.dll，0 警告 0 错误）；dotnet test 8/8 绿（20:33 复测；复验受本机「应用程序控制策略 0x800711C7」拦截，属本机执行策略非代码问题，CI 兜底）；白名单静态检查基线 0 命中 + 负向自检 PASS。开放：编辑器打开冒烟（判据 #1/#2）待装有 Godot 4.6 (.NET) 编辑器的机器；详情见 tasks/m0_bootstrap.md §5 冒烟记录。_`

> 主程序协议提醒：每次新建/修改 `.cs` 后，必须在本节追加一行 `_YYYY-MM-DD: [新增/修改] res://…/XXX.cs。暴露接口：…。依赖：…。_`

- `2026-09-09: [复核] M0 测试全量 8/8 最终复验绿（21:22，解除本机应用程序控制策略拦截后）。判据 #3 关闭；判据 #1/#2（Godot 4.6 (.NET) 编辑器打开）仍待编辑器机器。_`

- `2026-09-09: [新增] M1 阵型骨架（T-M1-01~05，主程序）落地：darkest/scripts/core/contracts/ 契约集（IFormation/SlotState/SlotKind/FormationSide/UnitId/SlotLayout/SlotChange/DisplaceResult/FormationRules，blueprint §9.1 + data_schema §2.1）；darkest/scripts/gameplay/sim/board/ 新增 UnitRuntime（Id/Side/Weak 最小占位）、ObstacleRuntime（hp 可空）、FormationBoard（槽位三态/逐级交换链 TrySwapChain/向中靠齐 CloseUp/障碍移除/只读快照 CreatePreviewSnapshot + DryRunSwapChain，行为开关读 FormationRules）、FormationBoardFactory（编成+障碍装载）；darkest/data/formation.json（§3.6 逐键：槽常量/center_outward/编成/obstacles=[]/rules 九键）；darkest/scripts/data/FormationConfig.cs（System.Text.Json snake_case 绑定 + fail-fast 校验）。测试 darkest/tests/BoardTests.cs 25 用例（T-M1-01~05 全判据：编成装载/交换链示例逐位一致/属性式无空位/边界不动/撞障碍交换/靠齐原子重放/虚弱跨越/全虚弱留空/障碍不阻挡/敌方对称/障碍建模/移除→靠齐/dry-run 与真实结算逐项一致+无副作用+可回放）。暴露接口：IFormation{GetSlot,UnitAt,OccupiedPositions,TrySwapChain,CloseUp,CanCloseUpIn}、FormationBoard{SlotKindAt,CreatePreviewSnapshot,DryRunSwapChain,RemoveObstacle,TryGetObstacleHp}、FormationConfig.Parse、FormationBoardFactory.CreatePlayerBoard/CreateEnemyBoard。依赖：M0 内核与测试工程。验证实绩：dotnet test 33/33 绿（0.88s）。踩坑：CloseUp 链式交换曾以槽位号直作数组下标（整体偏移 +1）导致死循环→步骤无限追加→testhost 内存暴涨 OOM（用户实机观测 memory 持续上涨）；已修复（位置→下标显式转换）并加收敛上限守卫（超限抛异常）；详见 m1_formation.md §5 冒烟记录。_`

- `2026-09-09: [复核] M0 判据 #1/#2（编辑器可用）本机补验绿：Godot 4.6.1 stable mono（E:\Godot_v4.6.1-stable_mono_win64）--headless --path darkest --import 通过（first_scan/update_scripts_classes/loading_editor_layout DONE，退出码 0；无项目导入错误/SCRIPT ERROR/error CS；仅 %APPDATA% 设置写与证书库告警属环境噪音）。编辑器侧 --build-solutions 因本机离线 net8 restore 失败（同 m0 §5.1），经 CI/.NET 8 机器复验；编辑器已能加载 .godot/mono/temp 的 Darkest.dll。手写 sln/csproj 与编辑器实况无出入（未被改写、无第二 csproj 误导入）。_`

- `2026-09-09: [新增] M2 结算核心（T-M2-01~10，主程序）落地：data/ 新增 units.json（7 原型，§3.1）、tuning.json（§3.7 全键）、morale_events.json（§3.3 全 14 行）；scripts/data/ 新增 UnitsConfig（P1/P4/P7）、TuningConfig、MoraleEventsConfig（P9 缺行报错 + 线性 Get）、BalanceTable（只读快照）。BattleMath 扩为完整公式库（HitRate/PhysicalMitigation/MentalMitigation/ActualEffectChance/DeathDoorSurvivePercent/SpeedFloatMultiplier/ApplyDamageRounding，T-M2-01）。contracts 新增 UnitStats、ITurnSequencer；UnitRuntime 扩属性/HP/减益修正/眩晕/流血/Morale/生效速度口；FormationBoardFactory 接入 units 属性、FormationBoard 增 UnitRuntimeAt/RemoveUnitAt/UnitAtPosition。管线：HitStep（T-M2-04）、DamageStep（多段独立实例，T-M2-05）、EffectsStep（乘法概率/无概率直挂/眩晕流血压根，T-M2-06）、DisplaceStep（唯一 >= 例外，T-M2-07）、MoraleLedger（表驱动派生/虚弱−5 每回合≤1/团队合并一次，T-M2-08）、WeakDeathsDoor（进虚弱/崩溃调用点/死门，T-M2-09）、DamagePipeline（§2 固定次序装配 + SkillFixture/SkillDisplace，O-12 默认）。core/events 增 BattleEventTypes（Hit/Crit/Damage/Morale/Effect/Displace/DeathDoor/WeakEnter/Death/CollapseRoll）。测试：FormulaTests（§7.1 八样例+边界）、DeterminismTests、TurnOrderTests、CombatResolutionTests（集成+同命令流确定性）。验证实绩：dotnet test 64/64 绿（0.92s）；using Godot 基线 0。踩坑：record 位置参数缺 [JsonPropertyName] 静默 null；查询缓存未初始化；默认编成 1 号位=坦克；靠齐会填槽——详见 m2_combat_core.md §7 冒烟记录。移交：护盾/流血钩子/崩溃池解析/撤退行 → M3/M4；组合行三默认已锁单测（AOE+暴击折扣优先/虚弱 −8 与 −5 叠加/跨阵营同速我方先手），建议登记 O-31+。_`