# 任务卡：文件行数红线（≤600 行）与**拆分规范**

> **编号**：ARCH-T-FILE-SIZE-01 · **类型**：task · **状态**：✅ 硬线超限 **0**（2026-10-02 实测：**590** 个程序文件 ≤600 · 0 allowlist）· 预警面（401~600）**17 个**待拆
> **来源**：**用户 2026-09-18 指令**：「程序文件 ≤600 行」；主程序已拆其域 8 处（`partial class` 手法）。
> **架构评审**：v1.85（2026-09-18）—— 见 §2/§3/§4。

---

> 🔴 **本卡性质已升级为【红线 28】**（用户 2026-09-18 直接指令）：**所有程序文件 ≤600 行** ⇒ 见 `README` 红线 28（含**禁用假拆法** · **门禁 `tools/check_file_size.py`** · **白名单须带理由**）。
> 📊 **当前真实台账（实测）**：**590 个程序文件里超限 0 个**（`check_file_size.py` 0 allowlist · 预警面 17 个见 `check_file_budget.py --top`）⇒ ⚠️ **"超 600 共 10 个"是【拆前台账】，引用须区分【拆前/现状】**。

## §1 现状（实测）

> 🔴🔴 **2026-09-19 复查：红线 28 已【反弹】—— 门禁抓到 6 个超限文件**（**这就是"没有门禁会慢慢长回去"的实证**）：
> | 文件 | 拆后 | **现在** | 变化 |
> |---|---|---|---|
> | `gameplay/sim/run/ExpeditionFlow.cs` | 579 | 🔴 **1185** | **+606** |
> | `data/TuningConfig.cs` | 559 | 🔴 **811** | +252 |
> | `gameplay/sim/run/ExpeditionSession.cs` | 413 | 🔴 **670** | +257 |
> | `tests/HungerTests.cs` | —（新增） | 🔴 **648** | 新增即超限 |
> | `tests/BoardTests.cs` | 702 | 🔴 **630** | 仍超限 |
> | `gameplay/scene/BattleRoot.cs` | 562 | 🔴 **626** | +64 |
> ⇒ `python tools/check_file_size.py` ⇒ **FAIL（6 个）**；`tools/dsh/selfcheck.ps1` ⇒ 🔴 **FAIL（8/8 中 1 红 = file size）**
> ⇒ 📌 **判据**：**门禁已在，但【没接进 CI】⇒ 没人被挡** ⇒ ✅ **接 CI 是 P0**（`check_file_size.py` 退出码可用）✓
> ⇒ ⚠️ 并说明**为什么反弹**：**拆分只是"搬家"，不阻止原地继续长** ⇒ 需要 **① 门禁接 CI ② 每次提交前跑** ③ 新功能别再往大文件里塞 ✓

### 🔴 §1.2 拆分边界建议（**架构给出 · 2026-09-20** —— 执行者照做即可，不必再判断）

> 📌 **为什么由架构给边界**：**"按职责切"需要一个判断**（哪个方法属于哪个职责）——
>   而**那是架构的事**（本次反弹的根因之一就是"没有边界清单，只有行数目标"）✓
> ⚠️ **共同要求**（照 §3 的四条）：`partial` + **职责命名** + 头部四行（来源·职责·**依赖哪些私有状态**·只搬家+读数）+ **不许删注释**（红线 28 明令）

| 文件 | 行数 | **建议边界** |
|---|---|---|
| 🔴 `sim/run/ExpeditionFlow.cs` | **1185** | 它现在同时管 **状态机核心 / 拓扑 / 结局 / 战斗回灌 / 奖励** ⇒ 拆 4~5 个（**`RoomInteractions.cs` 已存在，不重复**）：<br>① **`ExpeditionFlow.cs`**（核心：步骤类型 · 当前步骤 · `Advance` 驱动 · 只读面 `Meter/Bag/Session/Nodes/Tuning`）<br>② **`.Topology.cs`**（拓扑模式：`StepTo` · 邻接 · **已处理格**）<br>③ **`.Outcome.cs`**（**三类结局** `WalkedOut/Abandoned/Wiped` · **放弃远征** · 事件发射）<br>④ **`.BattleReturn.cs`**（`OnBattleFinished` · 撤退分支 · 战斗结果回灌）<br>⑤ **`.Rewards.cs`**（掉落/奖励/资源入账）—— 若 ① 仍 >600 才拆 |
| 🔴 `data/TuningConfig.cs` | **811** | 它本质是 **`tuning.json` 的只读 POCO 集合** ⇒ 按**数据域**拆（**不是按行数**）：<br>① **`TuningConfig.cs`**（根聚合 + 解析入口 + 顶层校验）<br>② **`.Combat.cs`**（士气钳制/减免除数/虚弱/死门/护卫/命中补偿/暴击治疗）<br>③ **`.Expedition.cs`**（光照 · 背包 · 扎营 · 回头威胁 · 超时增援）<br>④ **`.Hamlet.cs`**（养成 · 疗养院 · 经济 · 解锁 · 饥饿）<br>🔴 **特别提醒**：本文件大量 XML 注释是 **"数字外置（P29）的理由"** ⇒ **拆时逐条带走**（丢了注释 = 丢了"为什么"）⚠️ |
| 🔴 `gameplay/scene/BattleRoot.cs` | **626** | **只超 26 行 ⇒ 最小改动**：把 **`片 3 流程驱动口`**（`AdvanceHostFlow` 一族）抽成 **`BattleRoot.FlowBridge.cs`** 即可，别的别动 ✓ |
| 🔴 `sim/run/ExpeditionSession.cs` | **670** | 已有 `.CampAndBonuses` ⇒ 再抽 **`.Inventory.cs`**（背包/物品）或 **`.Scout.cs`**（侦察），二者取一即够 ✓ |
| 🔴 `tests/HungerTests.cs` | **648** | 按**测试主题**拆：**`.Spawn.cs`**（饥饿生成/触发）／**`.Consume.cs`**（消耗/结算）✓ |
| 🔴 `tests/BoardTests.cs` | **630** | 已有 `.Fixtures` ⇒ 再抽 **`.Movement.cs`**（推进/靠齐）或 **`.Skills.cs`**（技能落点）✓ |

> 🔴 **顺序建议**：**先 `ExpeditionFlow`**（最大、且它挡住内核门的可读性）⇒ 再 `TuningConfig`（纯 POCO，最安全）⇒ 其余四个都是小手术 ✓
> ✅ **每拆一个**：**构建绿 + 全量测试同一个数 + 门禁 `check_file_size.py` 复跑**（红转绿是可见进度）✓

### 🔴 §1.3 `ExpeditionFlow.cs` 边界**已实测扩为 8 件**（主程序 2026-09-20 · 架构已点头）

> 🎖️ **这一条是"先量再裁"的又一次胜利**：主程序**按行号量出**"+606 行 = **四个整块新功能**"（不是零散膨胀）⇒
>   🔴 **并指出：按架构 §1.2 的原 5 件切，切完【还会红】**（③④⑤⑥ 合计 **~574 行** —— **实测算术**）✓

| # | 文件 | 职责（实测行号） |
|---|---|---|
| ① | `ExpeditionFlow.cs` | **核心**（步骤类型 · 当前步骤 · `ctor` · `Advance` · `ResolveEvent` · 只读面 `Meter/Bag/Session/Nodes/Tuning`） |
| ② | `.Topology.cs` | **拓扑**（`BeginTopology` · `StepTo` · 邻接 · 已处理格 · 目标） |
| ③ | 🆕 `.TileWalk.cs` | **逐格走格**（`EnableTileWalk` · `TryStepTile`（单块 144 行）· 逐格光照分摊 · 回退计费） |
| ④ | 🆕 `.Traps.cs` | **陷阱**（`BindTraps` · `ResolveLandingTrap` · `TrapResistSourceDeclared`） |
| ⑤ | 🆕 `.Reveal.cs` | **秘密与揭示**（`RevealSecrets*` · `RevealScoutedTiles/Rooms` · `TileStateAt` · 侦察-揭示-访问三组读数） |
| ⑥ | 🆕 `.Hunger.cs` | **饥饿**（`ResolveHunger` · `HungerBuffer` · `CanEatForHunger` · `HasPendingHunger`） |
| ⑦ | `.Outcome.cs` | **三类结局**（`WalkedOut/Abandoned/Wiped`）＋ 🔴 **`Abandon()`【归此处】** ＋ 事件发射 |
| ⑧ | `.BattleReturn.cs` | `OnBattleFinished` ＋ 撤退分支 ＋ 结果回灌 |

```
🔴 **架构裁定（2026-09-20 · 回应主程序"请点头"）**：
   ① ✅ **点头**：**8 件成立**（③④⑤⑥ 是他实测发现的，**原清单确实漏了**）✓
   ② 🔴 **归属裁定（消歧）**：**`Abandon()` 归 `.Outcome.cs`**，**不留在 `.Topology.cs`** ——
      判据：**"这个方法的【语义归属】是【地图/拓扑】还是【一趟的结局】？"** ⇒ **放弃远征 = 结局/状态转移** ✓
      （⚠️ 否则"同一个概念落两处" = 我们反复防的"两处真值"家族）✓
   ③ 🔴 **核心文件【必须量】**：主程序估"核心+拓扑+尾部 ≈ 611 行" ⇒ ⚠️ **拆掉拓扑后，核心【有可能仍 >600】** ⇒
      ✅ **要求：拆完立刻跑 `check_file_size.py`；若核心仍 >600 ⇒ 再抽 `.ReadOnly.cs`**
      （只读面 `Meter/Bag/Session/Nodes/Tuning`）✓ —— 📌 **不许预估，必须量**（**纪律 AC**）✓
   ④ ✅ **`.Rewards.cs` 视①拆后行数决定**（**按量，不按猜**）✓ · ⚠️ **`.RoomInteractions.cs` 已在，不重复** ✓
   ⑤ ✅ **其余 5 个文件按 §1.2 原边界照做**（`TuningConfig` → `ExpeditionSession` → `BattleRoot` → `HungerTests` → `BoardTests`）✓
```

| 域 | 文件 | 拆前 → 拆后 | 状态 |
|---|---|---|---|
| **主程序** | `TuningConfig.cs` | 760 → **559** + 214 | ✅ |
| | `BattleDirector.cs` | 832 → **447** + 28 + 399 | ✅ |
| | `BattleRoot.cs` | 889 → **562** + 346 | ✅ |
| | `ExpeditionSession.cs` | 915 → **413** + 517 | ✅ |
| | `ExpeditionFlow.cs` | 937 → **579** + 372 | ✅ |
| | `M76TopologyProbeTests.cs` | 1176 → 463 + 434 + 313 | ✅ |
| **UI 设计师** | 🔴 `BattleUi.cs` | **2715**（未拆） | ⬜ **目标未达成** |
| | 🔴 `HamletRoot.cs` | **1663**（未拆） | ⬜ |

**手法**：`public (sealed) partial class` + **按职责命名**：
`BattleRoot.PlayerActions.cs` · `BattleDirector.OvertimeAndRetreat.cs` ·
`ExpeditionFlow.RoomInteractions.cs` · `ExpeditionSession.CampAndBonuses.cs`

---

## §2 ✅ 架构评审：**四条做对了**

```
✅ ① **手段与命名正确**：`partial` + **按职责命名**（不是 `Part2`/`File2`）⇒ 文件边界能表达职责 ✓
✅ ② **头部可追溯**：每个新文件开头写明「**从 X.cs 拆出（用户 2026-09-18 红线：程序文件 <=600 行）** ·
   本文件 = <职责> · **只搬家、零行为改动**」⇒ 下一个人知道它从哪来、该放什么 ✓
✅ ③ **语义零风险**：`partial` 是**同一个类型** ⇒ **字段布局/初始化顺序/确定性【不变】**
   ⇒ 佐证：**全量 583/583 未变** ✓（⚠️ 但见 §3 ②：这**还不够**当"零行为改动"的证据）
✅ ④ **边界没破 + 生命周期没踩坑**：
   · 内核面新 part（`ExpeditionFlow.RoomInteractions` / `ExpeditionSession.CampAndBonuses` /
     `BattleDirector.OvertimeAndRetreat`）**零 Godot** ✓ ⇒ `check_godot_refs.py` **仍 OK（0 hits）**，且门禁**覆盖新文件** ✓
   · `BattleRoot` 的 `_Ready`/`_UnhandledInput` 在主文件、`_Process` 在 part ⇒ 🔴 **无重复生命周期方法**
     （**这是 `partial` 最容易踩的坑：两处各写一个 `_Ready` ⇒ 编译错或互相覆盖**）⇒ ✅ 本轮没踩 ✓
```

---

## §3 ⚠️ 架构评审：**三条风险（需要补的）**

```
🔴 ① `partial` 的【本质局限】：它让"行数"合规，但**不建立类型边界** ——
      **任一 part 都能读写主类的全部私有成员** ⇒ ⚠️ **封装在文件级失效**、**类的认知复杂度没降**
   ⇒ 🔴 **判据（架构立）**：**"拆出来的 part 能不能【独立命名 + 独立持有状态 + 独立测试】？"**
      · **能** ⇒ 应抽成**真正的独立类型**（有自己的不变式）——**`partial` 是最弱的一种拆法**
      · **不能**（必须直接读写主类私有状态）⇒ `partial` 合理 ✅ **但必须声明依赖**
   ⇒ ✅ **要求（低成本、高价值）**：**每个 part 头部再补一行【它依赖主类的哪些私有成员/状态】**
      ⚠️ 现状：头部只写了"职责"与"零行为改动"，**没写依赖** ⇒ 下一个人**无法判断改动影响面** ⚠️
   ✅ **例外（可以接受 `partial` 的判据）**：**"按【已有状态机的相位】切"** ——
      如 `BattleUi` 应按 `Battle / Map / Curio / Camp` 切（**与 §5 的片 3.1 边界对齐**）✓

🔴 ② **"零行为改动"必须可核对，不能只靠声明** ⚠️
   ⇒ 判据（架构给，用于每个拆分提交）：**① 全量测试数不变** ＋ **② 冒烟"真错误"= 0** ＋
      🔴 **③ 关键读数前后一致**（**A1 单场 / V10 四档 / A2** —— 它们是"行为"的直接观测）✓
   ⚠️ 只给"行数前后"（如 937 → 579+372）**不足以证明"零行为改动"** ⇒ 🔴 **建议补读数对照** ✓

🔴 ③ **没有门禁 ⇒ 会慢慢长回去** ⚠️（我们已有先例：命名空间门禁 `check_ui_namespace.ps1`）
   🆕 **建议**：`tools/check_file_size.py`
   · 判据：`darkest/scripts/**/*.cs` 与 `darkest/tests/**/*.cs` 的**行数 ≤ 600** ⇒ 非零 ⇒ **exit 1**
   · 🔴 **白名单机制**：例外**必须登记理由**（如"拆分进行中"），**且白名单本身要被 review**（不许悄悄加）
   · 📌 **文档（`doc/**/*.md`）不受此限**（长表格/长清单是合法的）✓
   ⇒ 📌 **一句话**：**"≤600 行"若没有门禁，就是【一次性动作】而不是【工程约束】** ✓
```

---

## §4 UI 域：两个最大文件（**且它们不是"行数问题"那么简单**）

```
🔴 `BattleUi.cs` **2715 行**（已是 `partial class`）· `HamletRoot.cs` **1663 行**
⇒ 🔴 **架构建议：不要"切 N 份"**，而应**按职责/按已有状态机切**：
   · `BattleUi`：**`Battle` / `Map` / `Curio` / `Camp`（现有模式状态机的相位）** ＋ `Build`骨架 ＋ `Audit`/`Sfx`/`Motion` 辅助
     ⇒ ✅ **并与片 3.1「宿主按请求分流」的边界对齐** —— 这样**一次拆出可复用的结构**（不是单纯减行数）✓
   · `HamletRoot`：**名册 / 建筑弹窗 / 详情 / 菜单**（它已有这些分工的段落，按段落切）✓
   ⚠️ **同一条要求**：每个 part 头部写「来源 · 职责 · **依赖哪些私有状态** · 零行为改动 + 读数对照」✓
```

---

## §5 验收（本卡）

```
F1 🔴 **主程序域**：8 处拆完 ⇒ ✅ 已完成（另有 2 处待其确认是否也 >600）
F2 🔴 **UI 域**：`BattleUi.cs` / `HamletRoot.cs` ≤600（**按职责/按相位切**，不是按行数）
F3 🔴 **每个 part 头部含四行**：来源 · 职责 · **依赖的私有状态** · 零行为改动（+ 读数对照）
F4 🔴 **零行为改动有据**：全量测试数不变 + 冒烟真错误 0 + **A1/V10/A2 读数前后一致**
F5 🔴 **门禁**：`tools/check_file_size.py`（≤600 · 白名单须登记理由 · 文档不限）
F6 🔴 **内核门禁仍 OK**：`check_godot_refs.py` 0 hits（**新 part 不得引 Godot**）
```
