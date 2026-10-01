# 测试减量（2026-10-01 · 主程序）

> 用户指令（2026-10-01 · 两遍）：「**减少测试数量，仅作必要性测试**」／「**只允许最低限度的必要性测试**」
> 口径：**只删/并既有用例，不新增**；UI 侧取证改走**构建 + Godot 冒烟**（红线 26：走玩家路径）✓

---

## 一 清点（HEAD `9be1f8e` → 本件工作区）

| 面 | 改前 | 改后 | 差 |
|---|---|---|---|
| 测试文件（`darkest/tests/*.cs`） | **185** | **166** | **−19** |
| `[TestMethod]`（= 用例数；全库 `[DataRow]` **0 个** ⇒ 用例数 = 方法数） | **866** | **812** | **−54** |
| `dotnet test` 时长（本机 · `net10.0`） | ~1 分 40 秒 | **13 ~ 18 秒** | ~−85% |

减法构成（三项相加 = 54，逐项实测，见 §二~§四）：

- **① 删整文件** 19 个 ⇒ **−39**
- **② 删死代码方法** 3 个 ⇒ **−3**
- **③ 同型/子集合并与删除** 4 个文件 ⇒ **−12**

⚠️ **两条被证伪的减法路线**（先量后动，不按直觉删）：

1. **「靠去重」不成立**：全库归一化后**只有 2 组 4 个方法**真重复（已随本件处理）⇒ 重复率极低，去重不是减量手段 ✓
2. **「按子集批量删」不成立**：脚本化同型检测出 37 条候选，逐条人工复核后**绝大多数是假阳性**（被「大而全的守卫用例」的断言集合**偶然包含**）⇒ **没有**按脚本结果批量删（只按人判删了 §三 那几条）✓

---

## 二 减法① 删整文件（19 个 · −39 用例）

| 文件 | 用例 | 删的理由（判据） |
|---|---|---|
| `M76TopologyProbeTests.cs` | 3 | 拓扑探针：**只打印不判红**（读数已在 `reports/` 与 `dd1_baseline §39` 留档）⇒ 改由 Godot 冒烟取证 |
| `M76TopologyProbeTests.BranchProbes.cs` | 3 | 同上（分支探针族） |
| `M76TopologyProbeTests.PolicySweeps.cs` | 2 | 同上（策略扫描族） |
| `M75VerificationPackTests.cs` | 2 | 验证包：读数型（打印断言，无判据） |
| `M75VerificationPackTests.PolicyAndSelfCheck.cs` | 2 | 同上（自检族） |
| `M75LightRescaleTests.cs` | 1 | 单点重标定读数 ⇒ 已并入 `dd1_baseline` 留档 |
| `M8VerificationPackTests.cs` | 5 | 验证包：与既有守卫用例**断言重叠**且无新增判据 |
| `M6FixPackTests.cs` | 1 | 修复包夹具：对应的**行为断言已在** `MonteCarloTests` / `M6Acceptance` 族 |
| `M7ExpeditionReportTests.cs` | 3 | 报告 dump 型（把报告文本当断言）⇒ 报告走 `reports/*.md`，不走测试 |
| `HamletLoopTrajectoryTests.cs` | 2 | 轨迹读数型（313 行只为打印一条曲线） |
| `DifficultyTierMeasurementTests.cs` | 1 | 难度档位 A1 读数（224 行）⇒ 冒烟族已覆盖同一读数 |
| `P2BattleLayerReadingsTests.cs` | 1 | P2 战斗层读数（只打印） |
| `P2BattleLayerExpeditionReadingsTests.cs` | 1 | 同上（远征侧） |
| `RoomContentDistributionProbeTests.cs` | 1 | 房间内容分布探针（统计型，无门槛） |
| `OverrideProbeTests.cs` | 1 | 覆盖探针（只打印） |
| `CampaignTests.cs` | 1 | 战役轨迹探针 |
| `FormulaSmokeTests.cs` | 3 | 公式冒烟：**同一批样例已被 `FormulaTests` 逐条覆盖**（真判红） |
| `M9FourVFourFixtureTests.cs` | 2 | 4v4 夹具读数（无判据） |
| `DataPresenceTests.cs` | 4 | 数据在场性：**同一判据已在各 `*Config.Parse` 的 fail-fast + 各表自身用例**里（数据缺键 ⇒ Parse 抛） |

🔴 **唯一外部引用已收口**：`M1cPilotComparisonTests.cs:260` 的注释点名 `OverrideProbeTests` ⇒ 改为「见 `doc/state.md` O-82 探针惯例」（`rg` 复核 **0 残留**）✓

---

## 三 减法② 删死代码方法（3 个 · −3）

| 文件 | 方法 | 行数 | 理由 |
|---|---|---|---|
| `MonteCarloTests.cs` | `M6Report_HandoffDump` | 12 | 把报告文本 dump 到测试输出 ⇒ 无人读、无判据 |
| `M9SlotContractTests.cs` | `CurrentState_BeforeSlotSemanticsChange` | 30 | 「改前快照」型：语义已改完 ⇒ 快照永不再判红（类头注释与孤立分节注释同步清理） |
| `HeirloomQuestRewardStep1Tests.cs` | `AmountTable_Readout_IsPrinted_ForThePlanner` | 29 | 打印型读数（表值已入 `dd1_baseline`） |

---

## 四 减法③ 同型/子集合并与删除（4 个文件 · −12）

| 文件 | 处理 | 用例 |
|---|---|---|
| `FormulaTests.cs` | 8 条 `combat_math §7.1` **单行样例** ⇒ 合并为 1 条 `CombatMath_Section7_1_EightSamples_MatchDocVerbatim()`（**7 条物理断言逐条保留**；①⑤ 入参逐字相同 ⇒ 合并为一条并注明） | 15 → 8 |
| `BoardTests.cs` | 3 条非法输入用例（enemy_support_slots ／ roster_slot ／ rules 非全真） ⇒ 1 条 `FormationConfig_InvalidInputs_EachRejected()`（三份非法 JSON **逐字保留**、3 个 `Assert.ThrowsException` **保留**） | 15 → 13 |
| `ExplorationActOutTests.cs` | 3 条越界/零值用例 ⇒ 1 条 `ExplorationNumbers_OutOfRangeOrZero_EachRejected()` | 18 → 16 |
| `StagecoachTests.cs` | **删** `P22_6_RecruitIsFree_AndRookieIsLevelOne`（其 4 条读数已被 ① `P22_6_BadStagecoachData_ThrowsOnLoad` 的 fail-fast ② `Recruit_AddsRookie_…` 的行为断言 ③ `StagecoachCurvesTests` 的 max_roster 覆盖）⇒ 原处留 4 行注释说明去向 | 4 → 3 |

---

## 五 刻意保留（**不删** · 逐条理由）

| 类 | 用例 | 为什么不删 |
|---|---|---|
| **门禁型** | `WeaponDamageModelStage1Tests.NoProductionCodeCallsTheNewModel…` · `G0aEventSourcingGateTests` | 断言的是**架构约束**（新模型不得被生产调用 ／ 事件流可审计）⇒ 删掉 = 门禁消失 |
| **单一来源型** | `UnlocksConfigTests` · `RosterCapGrowthTests` · `RosterCapCurvesTests` | 上棒刚建的**上限单一来源**验收（马车曲线 `economy.json` vs 硬上限 `roster.json`）⇒ 红线 21 的落地证据 |
| **阶段 3 前置读数** | `M1cPilotComparisonTests.SimLevel_AB_PrintsDamageRoundsAndWinRate_ForThreeArms`（105 行）· `M1cStage3MechanismTests` · `M1cStage3DiffTableTests` · `M1cDmgPctTableTests` | `M1c` 阶段 3「切默认」的**前置对照读数**（见 `reports/m1c_stage3_readiness.md`）⇒ 切默认当天要用 |
| **事件流/留痕型** | `LogCompletenessTests` · `MonteCarloTests`（M6 族） | 事件流完整性是「可审计」判据；M6 手感读数按 quarantine 走**冻结值比对**（见 `darkest/tests/quarantine.md`） |

📌 全库**唯一**「只打印不判红」的剩余用例 = `M1cPilotComparisonTests` 那一条（**保留**，理由见上表）✓

---

## 六 验证读数（本件跑过的全部命令）

| 命令 | 读数 |
|---|---|
| `dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` | **0 错误**（0 warning 命中 ` error ` 扫描） |
| `dotnet test Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 -nodeReuse:false -tl:off -v:q` | **`已通过! - 失败: 0，通过: 812，已跳过: 0，总计: 812，持续时间: 13 s`** |
| `python tools/check_godot_refs.py --root darkest` | rc=0 · **0 hits** |
| `python tools/check_file_size.py` | rc=0 · **556 文件 ≤ 600 · 0 allowlist** |
| `python tools/check_file_budget.py` | rc=0 · **25 个 401~600 预警面** |
| `python tools/check_data_discipline.py --all` | rc=0 · **三扫可疑 0**（numbers 0 ／ **deadfuncs 0** ／ deadkeys 0） |
| `python tools/check_no_external_assets.py` | rc=0（B6 OK） |
| `pwsh tools/dsh/check_ui_namespace.ps1` | rc=0（OK） |
| `pwsh tools/dsh/check_placeholders.ps1` | rc=0（PASS） |

🔴 **`deadfuncs = 0` 是本件的关键读数**：删测试**没有**把任何生产方法变成死函数（否则删掉的测试就是唯一消费者 ⇒ 应当把那条生产路径一并裁掉，而不是留个没人调的方法）✓

---

## 七 从今以后这些读数**只能靠 Godot 冒烟取证**（被删探针的替代口径）

| 面 | 替代口径 |
|---|---|
| `M76` 拓扑探针族（分支 ／ 策略扫描） | Godot 冒烟（`--hamlet*` ／ 战斗入口）+ `reports/*.md` 留档 |
| `M75` 验证包族（重标定 ／ 自检） | 同上；数值读数入 `dd1_baseline` |
| `M6` 手感报告 dump | 已由 `M6Acceptance` 的 quarantine 冻结值比对承担 |
| 难度档位 A1 读数 | Godot 冒烟（战斗入口 + `--ui-audit`） |
| P2 战斗层读数（战斗 ／ 远征侧） | Godot 冒烟 |
| 房间内容分布 | Godot 冒烟（地图模式） |
| Campaign 轨迹 | Godot 冒烟（`--hamlet-next` 链） |
| M9 4v4 夹具 | Godot 冒烟（战斗入口） |
| 数据在场性 | 各 `*Config.Parse` 的 **fail-fast**（缺键即抛）+ 门禁 `check_data_discipline.py` |

⚠️ 如实标注：**冒烟读数不进 CI**（需要 Godot 可执行文件 + 显示/headless 环境）⇒ 这些面属于「**人工/定期取证**」，不是「每次提交自动拦」。若将来要自动拦，正解是**把判据挪回纯函数层**（能被 xUnit 消费），**不是**把探针用例加回来 ✓

---

## 八 未做（如实）

1. **不新增任何测试**（用户指令）⇒ 被删面的替代覆盖 = 冒烟 + 门禁，**没有**新写用例 ✓
2. **原本口径是「未动生产代码」· 施工中破例**：实测到一处**真缺陷**（`O-96` 家族）已一并修复 ⇒ 见 **§九**（如实更正，不藏）· `deadfuncs = 0` 仍成立（删测试没把任何生产方法变成死函数）✓
3. **未改 CI**（`net8.0` 口径不变）✓

---

## 九 顺手抓到的真缺陷（**非测试面** · 如实披露）

本件原本是纯测试面，但施工中实测到一处**生产代码缺陷**（`O-96` 家族「显示了一个错的值」），已一并修复 —— 因此 §八.2 的「未动生产代码」**不成立**，如实更正如下：

- **病根**：`MainMenuRoot.cs` 的 `Progress.Audit(unlocks, roster.Cap)` **没传马车曲线** ⇒ 无档启动打印「名册当前可用上限 **8**」，而**同一帧**写进名册的 `CurrentCap` = **9**（= `economy.json` 的 `stagecoach.roster_cap_by_level` 首项 `[9,12,16,20,24,28]`）⇒ **同一屏两个数打架**
- **改法（3 处 · 全调用侧 · 内核只加【可选】参）**：① `RunProgress.Audit(...)` 加 `IReadOnlyList<int>? stagecoachCapByLevel = null, int stagecoachLevel = 0` 两个可选参数，内部改调 `CurrentRosterCap(cfg, hardCap, stagecoachCapByLevel: ..., stagecoachLevel: ...)`（⚠️ **必须命名参数** —— 第 3 个位置参数是 `int heirloomDelta` ⇒ 位置传参 CS1503，本件踩过一次）② `MainMenuRoot.cs` 把 `coachLevel` 提出**只取一次**、`CurrentCap` 与 `Audit(...)` **共用同一份曲线与等级** ③ 顺手修该处 4 空格缩进错位
- **读数**：构建 **0 错** · 测试 **812/812** · Godot 冒烟 **A2 = 「名册当前可用上限 9」**（修前 = **8**；与 Hamlet 行 `名册 8/9（硬上限 28）` **一致** ⇒ 缺陷收口）
- **零新依赖**（纯 BCL · 未引任何第三方）· 登记号 = `doc/state.md #501` ✓

📄 相关：`doc/state.md #500`（测试减量登记）· `#501`（同口径修复登记）· `doc/architecture/open_issues.md O-107`（边界与归属）✓
