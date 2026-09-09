# M1 阵型骨架 · 任务卡（m1_formation.md）

> **编号**：ARCH-M1 · **类型**：task · **状态**：草案 v0.1
> **上游**：[设计] doc/modules/formation.md（全文：槽位三态/交换链/靠齐/边界/障碍/验收 §6）· doc/modules/glossary.md §1（位置与阵型黑话）· doc/GDD.md §1.1（编队/障碍）/§1.3（位移）/§1.6（靠齐）/§1.7（位移与替换边界）· doc/modules/ui_spec.md §2 必显 #6/#9、§6（位移逐格滑动）、§7（边界反馈）· doc/architecture/blueprint.md §5a/§5d/§9.1（IFormation）/§12 风险 6
> **决策引用**：#3、#20、#21、#22、#24、#39、#41/#41b、#73、#77、#78、#88、#105、#114、#115、#139（正文逐卡带出处）
> **依赖**：doc/architecture/tasks/m0_bootstrap.md（工程与内核骨架，已完成判据在前）· doc/architecture/data_schema.md（§2.1/§2.4/§3.6 字段与枚举为唯一权威）· doc/architecture/_conventions.md（写作规范）
> **最近更新**：2026-09-09

**一句话定位**：本里程碑对应策划 README §2 的 M1（依据 formation.md）：把「6+4 槽位 · 槽位三态 · 逐级交换链 · 向中靠齐 · 边界硬墙 · 预置障碍」落成确定性内核里的 **FormationBoard** 及其数据源 `data/formation.json`，并交付「位移预览 = 内核同源 dry-run」的 API（blueprint §5d/§12 风险 6）；可视化演出归 M5，本里程碑全部判据在 `dotnet test` 内核层可验证。

> 关联接口：[设计] blueprint §9.1 `IFormation`（本里程碑实现其全部成员）；开放题按 open_issues.md 权威编号：O-12（AOE/技能内部次序，涉及结算时挂）、O-16（数据口径冲突，编成数值涉及时挂）、O-08（障碍挡远程/胜利条件，不阻塞）。

---

## 0. 范围与承接

- **做**：board 数据结构与编成装载（T-M1-01）→ 逐级交换链（T-M1-02）→ 向中靠齐（T-M1-03）→ 预置障碍（T-M1-04）→ 位移预览 dry-run（T-M1-05）。全部为确定性内核（core/contracts + sim/board），零 `using Godot`。
- **不做**：伤害/士气/虚弱/死门结算（M2/M4）；灰显与可用性判定本体（M3，本里程碑只供真值）；UI 呈现/逐格动画（M5）；换人/增援完整操作流（M3/M5，见 T-M1-02 口径）。
- **编成数据形成期差**：`formation.json` 先于 `units.json`（M2/M3）成文，引用原型 id 仅作字符串；P1/P2 引用完整性校验待数据齐备后统一接入（data_schema §4.2），M1 期间 Board 冒烟与单测使用夹具编成与夹具障碍。

## 1. 里程碑完成判据（M1，可测）

### 1.1 formation.md §6 四条验收 → 判定式

| formation.md §6 原文 | M1 判定式（可测） | 责任 |
|---|---|---|
| ① 任意一次位移后，阵型长度不变、无空位产生（除非有单位离场） | 属性式：任意随机满阵（Occupied/Blocked 混合）上执行任意合法 `TrySwapChain` 成功后——占据槽位集合与三态类别不变、Empty 计数不增、实际交换步数 = distance | T-M1-02（M1 判） |
| ② 死亡后阵型在**同一时刻**完成靠齐，玩家能看清谁补上谁的位 | `CloseUp(fromPos)` **单次调用**原子返回整条 SlotChange 序列；按序回放该序列 == CloseUp 后板；空位只出现在队伍后段（虚弱/障碍语义见 §1.2） | T-M1-03（M1 判内核原子性；死亡事件与演出归 M2/M4/M5） |
| ③ 玩家释放技能前能明确看出「这个技能现在能打到谁」（灰显准确） | 支撑真值：`GetSlot/UnitAt/OccupiedPositions(includeObstacle)` 三态查询正确，障碍槽计入「范围内非空」（GDD §1.1 算技能目标）——灰显判定本体 M3（skill.md §5）、显示 M5 | T-M1-01/T-M1-04（终判 M3/M5） |
| ④ 敌方被位移后，其技能可用性在同一回合内发生可见变化 | 支撑：敌方侧板与玩家侧同构、可被推（board 层不区别对待，被推能力 #88 在调用层限制）——可用性随站位变化的判定本体 M3 | T-M1-02（终判 M3/M5） |

### 1.2 交换链示例与边界用例（判定式）

| 用例 | 判定式 | 责任 |
|---|---|---|
| 交换链示例逐位一致（formation.md §2） | 初始 `[1:A 2:B 3:C 4:D]`，`TrySwapChain(D, 4→1, distance=3)`：每步后逐槽断言 `[A,B,D,C] → [A,D,B,C] → [D,A,B,C]`，最终 `1:D 2:A 3:B 4:C`（= `4↔3→3↔2→2↔1` 全序列） | T-M1-02 |
| 最外侧被外推失败不动 | 我方 6 号位 / 敌方 4 号位（最外侧）被向外推 1 格 → 返回失败、板与调用前逐槽一致（#78） | T-M1-02 |
| 虚弱者被越过的靠齐 | 靠齐链上夹有虚弱者 → 虚弱者位置编号不减小（不参与前移），健康角色跨过它补位（换人保护，GDD §1.6） | T-M1-03 |
| 全虚弱留空 | 靠齐方向后方全虚弱 → 空位留空不补；再次调用 CloseUp 无新移动（#115，恢复后不自动前移） | T-M1-03 |

### 1.3 里程碑汇总

| # | 判据 | 判定式 |
|---|---|---|
| 1 | 数据源成立 | `data/formation.json` 可解析并构造 FormationBoard；player/enemy 槽常量、编成、rules 与 data_schema §3.6 逐键一致 |
| 2 | 交换链正确 | §1.2「交换链示例逐位一致」通过 |
| 3 | 靠齐原子 | §1.1 ② 判定式通过（含边界用例） |
| 4 | 障碍建模 | Blocked 三态、可被交换推动、靠齐不阻挡（#114）、不计入胜负（GDD §1.1/O-08） |
| 5 | 预览单源 | dry-run 输出与真实结算逐事件一致且不改板 |
| 6 | 工程承接 | `dotnet test` 全绿（含 BoardTests）；内核三目录零 `using Godot`（M0-04 检查不回归） |

> 对应 blueprint §11「M1 阵型骨架」行：推一个人能看到整条交换链结果且不产生空位；死亡靠齐同一时刻完成（formation.md §6）。

## 2. 任务卡总览

| 卡 | 内容 | 依赖 | 关键判据（一句话） |
|---|---|---|---|
| T-M1-01 | FormationBoard 数据结构与编成装载 | T-M0-03 | 6/4 槽 + 三态 + formation.json 可构造、非法槽报错 |
| T-M1-02 | 逐级交换链 TrySwapChain | T-M1-01 | D→1 示例逐位一致；无空位；边界失败不动 |
| T-M1-03 | 向中靠齐 CloseUp | T-M1-01 | 原子全量序列；虚弱/全虚弱/障碍语义对 |
| T-M1-04 | 预置障碍（Blocked 建模 + 数据） | T-M1-01/03 | obstacles 可读入、交换可推、不计胜负 |
| T-M1-05 | 位移预览 dry-run | T-M1-01/02 | 快照不污染、预览==结算序列 |

---

## 3. 任务卡

### T-M1-01 FormationBoard 数据结构与编成装载
- **上游**：[设计] formation.md §1（镜头布局/编号）/§2（空位）/§6 · glossary.md §1（槽位三态/空/空气/换人）· GDD.md §1.1/§1.5 · data_schema.md §2.1（Side/SlotKind/SlotState 枚举）/§2.4（必显 #9 位置编号）/§3.6（formation.json）· blueprint.md §9.1（IFormation）· 决策：#3、#139
- **依赖**：T-M0-03（BattleSession(seed) 接入点：board 由会话/组合根构造）
- **产出**：
  - `darkest/scripts/core/contracts/` 下阵型契约集：`IFormation.cs` + `SlotState`/`FormationSide`/`UnitId`/`SlotChange`/`DisplaceResult` 等记录与枚举声明（签名照 blueprint §9.1；字段级以 data_schema §2.1 为准）
  - `darkest/scripts/gameplay/sim/board/FormationBoard.cs`（实现 IFormation）
  - `darkest/scripts/gameplay/sim/board/UnitRuntime.cs`（M1 最小：Id / Side / Weak 占位；M4 士气管线扩充，本卡不得超前实现）
  - `darkest/data/formation.json`（§3.6 结构：槽常量 / numeration / initial_roster / obstacles=[] / rules）
  - `darkest/scripts/data/` formation 绑定最小件（FormationConfig record + 解析，System.Text.Json，字段 snake_case，绑定约定 data_schema §4.1）
  - `darkest/tests/BoardTests.cs`（随各卡补用例）
- **要点**：
  1. 槽常量逐字（data_schema §3.6 / GDD §1.1/§1.5）：我方 `player = {slot_count:6, combat_slots:4, support_slots:[5,6]}`；敌方 `enemy = {slot_count:4, combat_slots:4, support_slots:[]}`（敌方无支援位）。编号 `numeration = "center_outward"`：我方 1~6、敌方 1~4，1 号最靠中（与敌方 1 隔「空气」对峙），编号沿「离中央越远越大」；空气不占槽位、不参与位移（formation.md §1 / glossary §1）。
  2. 槽位三态唯三（glossary §1 / data_schema §2.1）：`Occupied`（有角色）/`Empty`（空）/`Blocked`（障碍占据），JSON 值 `occupied/empty/blocked`；任一时刻一槽只落一态。区分「空」（三态之一）与「空位」（离场后留在队尾的连续空槽，glossary §1 辨析 2）。
  3. 初始编成（character.md §1 / #149；enemy.md §5.1）：我方 6 槽 = 1 `tank`、2 `warrior`、3 `commissar`、4 `medic`、5 `warrior`、6 `medic`；敌方 4 槽 = 1~2 `melee_soldier`×2、3 `ranged_archer`、4 `caster`；引用 units.json 原型 id（原型文件 M2/M3 成文，本卡只写引用关系）。
  4. 只读查询三件套对齐 §9.1：`GetSlot(pos)`/`UnitAt(pos)`/`OccupiedPositions(includeObstacle=true)`，供 M3 可用性判定与 M5 UI 高亮/编号显示（data_schema §2.4 行 9）。
  5. rules 标记随 formation.json 落库（data_schema §3.6 rules 表 9 键：boundary_as_hard_wall / displacement_only_via_swap_chain / obstacle_swaps_like_unit / close_up_on_death_immediate / close_up_ignores_obstacle / close_up_enemy_symmetric / swap_player_initiated / slot_three_state / deaths_and_close_up_separate_from_displacement，值均为 true）；board 行为开关读数据，禁止在代码里另拍一套常量（blueprint §7 起手值纪律）。
  6. 支援位是「位置不是职业」（#139）：槽位对任意角色开放；「支援位不能释放攻击类技能」属 M3 可用性判定（skill.md/glossary §1），本卡不做。
  7. 边界：非法槽位（敌方 5~6、编号 0 或越界、编成与障碍槽重叠）在构造/查询即报错或断言失败（口径与 data_schema §4.2 P2 同源）；板构造自 FormationConfig 快照，配置在战斗期只读（blueprint §7 管线）。
- **完成判据（可测）**：BoardTests——按 §3.6 编成构造板后，我方 Occupied 槽 = {1..6} 且 UnitId 逐槽一致（敌方 {1..4} 同理）；`SlotState` 与 JSON 值互转无歧义；formation.json 能被解析并构造板（槽常量逐字 §3.6、rules 键集合与 9 键一致）；非法构造样例（敌方 support、编号 0/7）抛异常。
- **风险/开放**：编成/原型数值若与草案表冲突，按 **O-16**（数据口径冲突源：以 data_schema/实值表为准，挂单不阻塞）；覆盖率校验 P3 待 M3。

### T-M1-02 逐级交换链 TrySwapChain
- **上游**：[设计] formation.md §2（逐级交换链/空位/障碍/方向/示例）+ §5 行 1（最外侧外推）与行 4（敌方位移）· GDD.md §1.3 · glossary.md §1（逐级交换链/边界硬墙）· combat_math.md §3（过抗性后走交换链）· data_schema.md §2.1（DisplacementType）/§3.6 rules · blueprint.md §9.1（TrySwapChain 签名）· 决策：#20、#21、#22、#78、#88
- **依赖**：T-M1-01
- **产出**：`FormationBoard.TrySwapChain`（§9.1 签名：`DisplaceResult TrySwapChain(UnitId mover, int fromPos, int toPos, int distance)`）+ 结果类型 `DisplaceResult`/步骤序列 `SlotChange`（含失败原因）+ BoardTests 位移用例
- **要点**：
  1. 位移唯一原语 = 逐级交换链（blueprint §9.1 注）：mover 沿 fromPos 朝 toPos 每步与「邻格当前占据物（角色或障碍）」交换一次，共 distance 步。推（向编号增大 = 本侧后排）/拉（向编号减小 = 1 号位侧）/自我前移/自我后移只是方向与调用方的换算，链内核同一套（formation.md §2 方向行）；数据层 `pull` 切片未用（data_schema §2.1/§3.2），内核一次实现、M3 数据启用。
  2. 位移永不写 Empty（#20/#21 角色不可走进空位）：任一步目标格为 Empty → 该步不可交换，判**整条链失败、板保持不动**——即「撞边界 = 失败不动」（#78/GDD §1.7）的泛化，覆盖「缩编后队尾空位挡推」与「最外侧被外推」两例。
  3. 撞障碍 = 照常交换（#22，障碍当角色）：障碍落到 mover 原位，链继续；障碍不因被推受损、不被位移清除（formation.md §5 行 5）。
  4. 多格 = 逐级交换：distance>1 必须逐步产出交换步骤（formation.md §2 示例 `4↔3→3↔2→2↔1`），不允许跨单位「瞬移」到目标格。
  5. 结果原子：成功返回整条步骤序列（每步 = 一次双槽交换，含槽位与单位标识，顺序即动画播放顺序，ui_spec §6）；失败返回失败原因且板与调用前逐槽一致（dry-run 与结算共用同一结果语义）。
  6. 位移 ≠ 靠齐（glossary §8 辨析 1 / data_schema rules `deaths_and_close_up_separate_from_displacement`）：TrySwapChain 成功**不得**触发 CloseUp；本卡不含 RNG/抗性判定——「过抗性 rand(0,100) >= 位移抗性」与「失败时伤害/其它效果照常」是 M2 结算（combat_math §3），board 只执行链。
  7. 发起权边界：敌方不能主动位移（#88，敌人位移只由技能触发），限制落在导演/调用层；board 层对双方同构支持被推（M1 判据 ④ 支撑）。换人/增援（#41/#41b，GDD §1.4.1）的「指定槽有人则交换」属玩家操作而非技能位移，完整操作流（消耗行动/支援位约束/入战斗位后技能切换）归 M3/M5；「无人则直接进入」分支只由换人触发，与 #20/#21 不冲突（GDD §1.4.1 澄清）。
- **完成判据（可测）**：① §1.2「交换链示例逐位一致」通过；② 属性式（§1.1 ①）通过；③ 边界用例：我方 6 号/敌方 4 号被外推 1 格失败不动；我方 1 号被拉（向 0）失败不动；缩编 2 人占 1、2 时推 2 号 1 格（目标格 Empty）失败不动；④ 撞障碍链：mover 与障碍交换、mover 抵达障碍原槽、步骤含该交换。
- **风险/开放**：O-12（仅当「位移 + 伤害/其它效果」同一技能的步骤次序问题——归属 M2 结算卡，本卡不裁决）；换人两槽「直接交换 vs 沿链逐级」的呈现口径在 GDD §1.4.1 内实现、由架构师在 M3 复核（本卡不新增开放题编号）。

### T-M1-03 向中靠齐 CloseUp
- **上游**：[设计] formation.md §3（触发/方向/虚弱者/障碍/全虚弱兜底/敌方对称）+ §6 验收 ② · GDD.md §1.6（靠齐/换人保护）/§1.7（边界行）· glossary.md §1（向中靠齐/空位/换人保护）· data_schema.md §3.6 rules · blueprint.md §9.1（CloseUp 签名）· 决策：#24、#114、#115（虚弱数值/进入退出规则属 M4）
- **依赖**：T-M1-01
- **产出**：`FormationBoard.CloseUp(int fromPos)`（§9.1：`IReadOnlyList<SlotChange>`）+ 离场触发入口封装 + BoardTests 靠齐用例（夹具设 Weak）
- **要点**：
  1. 触发语义：角色死亡/离场（且无人替换）**瞬间**调用一次（formation.md §3 触发行 / GDD §1.6）；「一次调用完成全部移动」即 formation.md §6 验收 ② 的「同一时刻」。本卡不接死亡事件（导演/伤害管线 M2/M4 触发），只保证 `CloseUp(fromPos)` 本身原子正确。
  2. 方向：fromPos 之后（编号更大）的**健康角色**向 1 号位方向紧凑补位，空位只留在队伍后段（formation.md §3）；位移走交换链不产生空位、不触发靠齐（辨析 1）。
  3. 虚弱者不参与前移（换人保护，GDD §1.6/formation.md §3）：靠齐需要「谁虚弱」——读 `UnitRuntime.Weak`；虚弱者原地保留、位置编号不得减小，健康角色跨过它补位（被跨过时相对后移 = 交换）。Weak 的写入方是 M4 士气管线，本卡只实现读取语义并以夹具直接置位验证（不越界实现士气管线）。
  4. 全虚弱兜底（#115）：fromPos 之后到队尾全虚弱 → 空位留空不补；恢复后也不自动前移（靠齐只在死亡/离场瞬间触发一次，#24/#115）；再次对已收敛板调用 CloseUp 不得产生任何移动。
  5. 障碍不阻挡（#114/formation.md §3 障碍行）：靠齐推进遇障碍照常交换（障碍被推后、空位照样被填），与 §2「障碍当角色交换」一致；切片无真实障碍关卡，以夹具覆盖。
  6. 敌方对称执行（data_schema rules `close_up_enemy_symmetric` / formation.md §3）：敌方无士气/虚弱/死门（enemy.md §1），Weak 恒 false。
  7. 边界：同回合多单位死亡的「逐个结算 vs 批量合并」与演出次序归导演/M2/M4（ui_spec §7 逐格演出），本卡按「每次离场一次 CloseUp」的接口语义设计，不裁决批量次序（O-12 联动）。
- **完成判据（可测）**：① 原子性：单次调用返回即全部变更，按序回放该序列 == CloseUp 后板；② 普通用例：死亡槽=1、后方全健康 → 全队前移、空位落在队尾；③ 虚弱用例（§1.2）：虚弱者编号不减小、健康者补位、空位不落在两个健康角色之间；④ 全虚弱用例（§1.2）：无移动、空位保留、重复调用无新变更；⑤ 障碍用例：障碍在靠齐路径上 → 空位闭合、障碍相对后移、无单位被阻挡（#114）。注：空位与「不可移动的队尾障碍」相邻时的精确定位属实现期复核项（切片无障碍关卡，不阻塞）。
- **风险/开放**：O-12（多人死亡/中途死亡与靠齐的剔除时机，归属 M2/M4）；O-16（若虚弱/死亡状态数据口径涉及，以 data_schema 为准挂单）；其余「无」。

### T-M1-04 预置障碍（Blocked 建模 + formation.json obstacles）
- **上游**：[设计] GDD.md §1.1（障碍本质：不会行动、只有血量的占位角色；AOE 波及、debuff 不生效、血量归零→消失→立即靠齐）· formation.md §2（障碍当角色）/§5 行 5（可被交换推动、不可主动清除）与行 7（来源 #39）· data_schema.md §3.6（obstacles：`{side, slot, hp?}`）· blueprint.md §1.2（障碍只用关卡预置 #39/#73/#77/#105；敌人造障碍技能不做）· 决策：#39、#73、#77、#105、#22
- **依赖**：T-M1-01、T-M1-03（障碍消失 → CloseUp）
- **产出**：FormationBoard 障碍表达（`Blocked` 三态 + 占位实体：hp 与 debuff 免疫查询位）；formation.json `obstacles` 编写规范与读入支持；BoardTests 障碍夹具样例（真实关卡数据切片默认 `obstacles: []`，data_schema §3.6 注）
- **要点**：
  1. 障碍 = 「不会行动、只有血量的占位角色」（GDD §1.1 本质 ✅）：占槽位三态的 `Blocked`（data_schema §2.1）；一槽不可同时有角色与障碍。
  2. 数据落点：`formation.json → obstacles: [{side, slot, hp?}]`（§3.6），slot 取值 player 1~6 / enemy 1~4；`hp` 缺省 = 不可被摧毁占位。切片只做关卡预置（#39/#73/#77/#105），敌人造障碍技能不做、大敌人占多格不做（GDD §15.2/#53）。
  3. 参与位移与靠齐：撞障碍即交换（T-M1-02，#22）；靠齐不阻挡、交换推进（T-M1-03，#114）。
  4. 结算语义预埋（本卡只建模、不结算）：障碍可被攻击、AOE 会波及（范围技能打到它掉血）、debuff 不生效（免疫眩晕/流血/属性减益等状态，GDD §1.1 用户拍板）；障碍计入「技能目标/范围内非空」判定（GDD §1.1 完全当敌人处理）。伤害/状态结算归 M2/M4 管线：血量归零 → 移除 → 立即触发向中靠齐（GDD §1.1 表），board 提供移除入口（mock 可触发）。
  5. 不计胜负：胜利条件 = 敌方单位全部死亡，障碍不计入（GDD §1.1；[待确认：写进胜利条件] → O-08，本卡不裁决，但障碍不得混入「单位存活数/覆灭」判定）。
  6. 玩家不能以位移或「主动清除」操作移除障碍：位移只交换位置、切片无主动造/清障碍技能（formation.md §5 行 5 口径；「打掉」= 血量归零属伤害路径）。
- **完成判据（可测）**：① formation.json 合法 obstacles 夹具可读入构造板：`GetSlot=Blocked`、`UnitAt=null`、`OccupiedPositions(true)` 含该槽而 `(false)` 不含（口径区分）；② 障碍槽被「范围内非空」计数正确（M3 灰显判据支撑）；③ 障碍随交换链被推动后位置正确；④ 非法 obstacles（越界/与编成重叠）→ 构造报错；⑤ mock 移除障碍 → 触发 CloseUp 且空位收敛（GDD §1.1「立即向中靠齐填补」）。
- **风险/开放**：O-08（障碍是否挡远程/是否写进胜利条件——不阻塞 M1）；O-12（AOE 逐目标顺序含障碍在场时的结算次序——归 M2，涉及即挂）；O-16（编成/障碍数据口径冲突以 data_schema §3.6 为准）。

### T-M1-05 位移预览 dry-run（内核侧）
- **上游**：[设计] ui_spec.md §2 必显 #6（位移预览：整条交换链的结果，不是只推 1 格）与 §6（位移沿交换链逐格滑动，不能瞬移）、§7（位移撞边界 → 「撞墙」抖动 + 「位移失败」提示）· blueprint.md §5d 注（位移预览 = 内核同源 dry-run）/§12 风险 6（位置/预览两套坐标漂移）· 决策：—（机制已定，见 formation.md §2）
- **依赖**：T-M1-01（只读快照）、T-M1-02（TrySwapChain）
- **产出**：FormationBoard 只读快照接口 + dry-run 入口（对快照执行同一 TrySwapChain）+ BoardTests 一致性用例 + SlotChange 序列契约说明（供 M5 FormationView/BoardOverlay 消费）
- **要点**：
  1. 唯一预览实现（blueprint §5d 注）：对「当前板的只读快照」跑**同一个** `TrySwapChain` 内核；快照深拷贝板数据、与原板零共享可变状态 → dry-run 永不污染真实板。
  2. 单源原则（blueprint §12 风险 6 缓解）：预览输出与确认后结算必须逐事件一致；UI 只把同一份序列画成逐格滑动（ui_spec §6），**不得**自行推导「只推 1 格」的简化预览。
  3. 序列契约：每条 SlotChange 含 槽位对/单位标识/结果态，顺序即动画播放顺序（逐格滑动）；失败返回「位移失败」信号供 UI 抖动反馈与「位移失败」文案（ui_spec §7）。
  4. 幂等与副作用：任意合法命令 dry-run 后真实板逐槽不变；同一输入两次 dry-run 输出一致。
  5. 边界：本里程碑只交付内核 API 与判据；预览 UI/高亮/镜头（FormationView/BoardOverlay/B 区）属 M5，此处只留接口与序列定义。
- **完成判据（可测）**：① 一致性：同一板上同一命令 → dry-run 序列 == 真实执行 TrySwapChain 的序列（逐条 槽位/单位/次序相同）；② 无副作用：dry-run 后真实板逐槽快照不变；③ 失败样例：最外侧外推 dry-run 返回失败 + 空步骤、真实板不动；④ 序列可驱动动画：回放 dry-run 序列可无歧义还原最终板。
- **风险/开放**：无（序列契约收口；可视化呈现留 M5 验收，ui_spec 必显 #6）。

---

## 4. 引用章节速查（供实现与评审核对）

- doc/modules/formation.md：§1（镜头布局/编号）、§2（位移规则/交换链示例/设计后果）、§3（靠齐/虚弱/障碍/全虚弱/敌方对称）、§5（边界条件 #78/#88/#114/#115/#39/#41/#22）、§6（验收四条）。
- doc/modules/glossary.md：§1（位置与阵型黑话：战斗位/支援位/位置编号/空气/槽位三态/障碍/空/空位/向中靠齐/逐级交换链/边界/换人）、§8 辨析 1~2、§9（CombatSlot/SupportSlot/CloseUp/SwapChain/Weak 命名）。
- doc/GDD.md：§1.1（编队与位置/障碍本质）、§1.3（逐级交换）、§1.4.1（换位/增援 #41/#41b）、§1.5（敌方 4 战斗位）、§1.6（向中靠齐）、§1.7（位移与替换边界）。
- doc/modules/ui_spec.md：§2 必显 #6/#9、§6（位移逐格滑动）、§7（撞墙/同回合多人死亡演出）。
- doc/modules/combat_math.md：§3（位移判定：过抗性后走交换链；失败时其它效果照常；位移不触发死门 #117）——抗性/结算本体归 M2。
- doc/architecture/blueprint.md：§1.2（障碍只用关卡预置）、§3（目录归属）、§4（B2/B3 边界）、§5a（FormationBoard/UnitRuntime）、§5d（dry-run 预览注）、§9.1（IFormation 契约）、§12 风险 6（预览两套坐标漂移）。
- doc/architecture/data_schema.md：§2.1（Side/SlotKind/SlotState/DisplacementType 枚举）、§2.4（必显 #6/#9 数据来源）、§3.6（formation.json 全字段与 rules 表）、§4.1（绑定约定）、§4.2（P1/P2 校验——引用完整性待 M3）。
- 开放题（open_issues.md 权威编号）：O-08（障碍，不阻塞）、O-12（AOE/技能内部次序，结算涉及挂）、O-16（数据口径冲突源）。
