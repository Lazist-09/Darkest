# 在飞改动 → **提交批次清单**（架构 `DELIVERY-ARCH-INCIDENT-RULINGS` ① 的产物）

> 🔴 **用途**：架构裁定「**不代提交**」——正解 = 我按域切出批次清单 ⇒ **作者认领，谁的域谁提** ✓
> 🔴 **本清单不含任何提交动作**：它只是**清单 + 主题 + 疑似归属**（结构由架构定，我照办）✓
> 📌 **取样时点**：2026-09-21（当场实测 `git status --short`；**总数 212 条**：M/D **126** · 未跟踪 **86**）✓
> 🎖️ 并附**疑似作者**判据：`O-98 ⑤` 的"并行写入者"**尚未找到** ⇒ 我按"**域**"猜归属，**不点名个人** ✓

## 0. 为什么必须分批（而不是一次全提）
```
🔴 一次全提 = 把**一批可能未完成的半成品**固化成历史 ⇒ 之后无法归因（"这条改动是谁的、完成了没"说不清）✓
✅ 分批 = 每批**自带主题** ⇒ 出问题能退回**单批**；且**同域改动互不阻塞** ✓
```

## 1. 批次表（按域切 · 每批一行主题 + 归属 + 依赖）

| 批次 | 域 | 条目数 | 主题（从文件名与内容判读） | 疑似归属 | 备注（依赖/风险） |
|---|---|---|---|---|---|
| **B1** | `darkest/data/**` | 4 | **内容数据**：`buff_defs.json` · `skills.json` · `tuning.json` · `trap_defs.json` | 数据域（疑似并行写入者） | ⚠️ `tuning.json` 属**数值** ⇒ 提交前应确认"是否有裁定依据"（`#307`） |
| **B2** | `darkest/scripts/core/**` + `darkest/scripts/data/**` | ~10 | **解析/契约**：`ISkillUseResolver` · `BuffDefsConfig` · `CampSkillsConfig` · `CuriosConfig` · `SkillsConfig` … | 主程序域 | ✅ 这批是 B1 的**前置**（解析先于数据） |
| **B3** | `darkest/scripts/gameplay/sim/survival/**` + `run/TrapDefs.cs` + `run/ExpeditionSession*` | ~12 | **生存/陷阱**：`LightMeter`（搬入）· `TrapDefs` · 饥饿/揭示/走格相关 | 主程序域（**疑似并行写入者**） | 🔴 与 `ExpeditionFlow.cs` 的 603 行**强耦合** ⇒ 回填完成前**不建议单独提**（提了也编不过） |
| **B4** | `darkest/scripts/gameplay/sim/**`（其余 41 项中的大头） | ~30 | **战斗/流程**：`UnitRuntime` · `BattleDirector*` · `BattleProjector` · `FormationBoard` … | 主程序域 | 同上：依赖 B3 |
| **B5** | `darkest/scripts/gameplay/scene/**` | 5 | **场景桥**：`BattleRoot*` · `ExpeditionComposition` · `ExpeditionContext` | 主程序域 | 🔴 含**我的**改动（autoload/转场/英雄读数）⇒ **可与我的提交合并**，但**必须与 B3/B4 同批**（否则跨批编不过） |
| **B6** | `darkest/tests/**` | 57 | **用例群**：`CampSkill*` · `Curio*` · `Trap*` · `Hunger*` · `Exploration*` · `BoardTests` … | 主程序域 | ⚠️ **强依赖 B1~B4** ⇒ 最后提（否则测试引用不存在的成员） |
| **B7** | `darkest/scripts/ui/**` + `darkest/scenes/**` + `darkest/resources/**` | 39 | **UI 域**：`BattleUI.*` · 各骨架 `*.tscn` · `ui_palette.tres` | 🔴 **UI 设计师**（**他的域 ⇒ 他提**） | ✅ 与 B1~B6 弱耦合（可独立提） |
| **B8** | `doc/**` | 35 | **契约文档**：`README` · `data_schema` · `hamlet_loop` · `retreat` · `ui_spec` … | 🔴 **策划 / 架构**（契约域） | ✅ 可独立提 |
| **B9** | `tools/**` + `darkest/project.godot` + 杂项 | 31 | **工程**：门禁脚本 · `project.godot`（含**我**的 autoload 注册）· `.uid` 等 | 主程序域 + **UI（`.uid`）** | ✅ 可独立提；⚠️ `.uid` 必须与**其场景/脚本同一批**（本项目已跟踪 265 个 `.uid`） |

## 2. 建议顺序（**先能编过，再谈完整**）
```
🔴 **前置**：`ExpeditionFlow.cs` 的 603 行回填（**等架构裁 (c)/(d)**）—— 它是 B3/B4/B5/B6 的**共同前置** ✓
⇒ 之后：**B2 → B1 → B3 → B4 → B5**（解析 → 数据 → 生存 → 战斗 → 场景：每步都保证"能编过 + 全量同数"）
⇒ 可**并行**：**B7（UI 域）· B8（契约域）· B9（工程）**（互不阻塞）✓
⇒ 最后：**B6（用例群）**（它依赖前面全部）✓
```
## 3. 我不做的事（明确边界）
```
🔴 **我不代提交任何一批**（架构裁定 ✓）—— 包括我"猜"出的归属：**猜归属 ≠ 取得授权** ✓
✅ 我**能**做且**已做**的：本清单（按域/主题/依赖切好）+ 每批的**前置与风险** ✓
✅ 若某批**完全无人认领**（真孤儿）⇒ 请架构明确"授权我提这一批"，我再动（**逐批授权**，不一次性）✓
```
## 4. 可核对（本清单的数字口径）
```
`git status --short | Measure-Object` ⇒ **212** 条（M/D 126 · 未跟踪 86）
分组计数：sim **41** · data **4** · scene **5** · UI 域 **39** · tests **57** · doc **35** · 其它 **31**（合计 212 ✓）
```
