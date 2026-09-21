# `TuningConfig.cs`（811 行）拆分 · **域段地图与执行清单**（2026-09-21 实测）

> 🔴 **为什么单独一份**：它是 **P0-1 六个待拆文件里唯一还没真拆的**（其余 5 个已拆完 ✓，见下）
> 🔴 **它的难点**：`TuningConfig` 是**单个 positional record**（67 属性）⇒ 只能 `partial` 拆**成员** ✓；
>   而 811 行里 **`Validate` 一个方法就占 ~618 行**（L193–811）⇒ 压到 ≤600 **必须拆 `Validate`** ✓
>   ⇒ ⚠️ 而 `Validate` 里有 **83 处 `throw`**、**跨段共享局部变量**、且**校验顺序/短路会改行为** ⇒ **高风险手术** ✓

## 1. 六个文件现状（P0-1 §1.2 清单 · 实测）
| 文件 | 原 | 现 | 拆出的 part |
|---|---|---|---|
| `ExpeditionFlow.cs` | 1185 | **357** | `.Topology 103` · `.TileWalk 153` · `.Traps 62` · `.Reveal 134` · `.Hunger 51` · `.Outcome 61` · `.BattleReturn 91` |
| `BoardTests.cs` | 630 | **295** | `.Movement 367` |
| `HungerTests.cs` | 648 | **557** | `.Spawn 117` |
| `ExpeditionSession.cs` | 670 | **378** | `.Survival 233` · `.Traps 97` |
| `BattleRoot.cs` | 626 | **532** | `.FlowBridge 116`（架构指定：**只抽流程驱动口** ✓） |
| 🔴 `TuningConfig.cs` | 811 | **811** | **未拆**（白名单 1 条 · 主程序认账 ✓） |

## 2. `Validate` 的域段地图（按**被校验的属性**反推 · 实测行号）
```
L193–206  横切：小 record 的校验（MentalReduction/Weak/Morale/StatDebuffDefault/Bleed/DeathsDoor/
          DamageFloat/HitClamp/Stun/Collapse/VirtueRate/SpeedFloat/WeakRecovery/GuardRedirect/
          OvertimeReinforcement/Retreat）≈ 14 行
L207–264  **远征域（一）** `t.Expedition`
L265–313  **远征域（二）** `t.Light` / `t.Scouting` / `t.Inventory`
L314–402  **集合类校验** `t.TryGetValue` / `t.Values` / `t.Resources` / `t.ContainsKey`
L403–435  **扎营与口粮** `t.Camp` / `t.Half` / `t.Full` / `t.Feast` / `t.Starve`
L436–495  （待判：该段 60 行的域归属 —— 需读一次 ✓）
L496–811  **战斗域**（`t.PhysicalMitigation` 起 ⇒ 含命中/伤害/暴击/士气/虚弱 等）
```
⇒ **切法建议（架构 §1.2 的 ②.Combat ③.Expedition ④.Hamlet）**：
```
① 主文件保留：10 个小 record + `TuningConfig` 位置参数 + `Parse` + `JsonOptions` + **`Validate` 变成薄调度**
② `TuningConfig.Expedition.cs`：`ValidateExpedition(t, raw)` = **L207–495 整体搬走**（≈ 289 行 ✓）
③ `TuningConfig.Combat.cs`：`ValidateCombat(t, raw)` = **L496–811 整体搬走**（≈ 315 行 ✓）
④ 主文件 `Validate` 里**在同一位置**依次调用 ②③ ⇒ 🔴 **校验顺序不变 = 零行为** ✓（786 用例兜底 ✓）
```
## 3. 🔴 动手术前必须先解决的三件（**别跳过**）
```
① **共享局部变量**：L207–495 / L496–811 用到的**段外局部**（如 `rawJson`、集合 `ids` 之类）⇒ 必须作为**参数**传入 ✓
   ⇒ 先量一次"每段引用了哪些段外局部"（一次 grep 即可 ✓）
② **`throw` 消息里的 `ResPath`** 等静态依赖 ⇒ 直接把 `ResPath` 作为参数或让 helper 自己读常量 ✓
③ **顺序与短路**：`Validate` 里若有"先算再判"的副作用（如构造 HashSet ✓ 无副作用；但 `TryGetValue` 填充呢）
   ⇒ 🔴 必须逐段比对 ⇒ **判据：789 个用例全绿 + `ShippedConfigs_Pass_AndValuesComeFromData` 不红** ✓
```
## 4. 为什么这一份**没有**顺手做掉（如实）
```
我本轮先做了 `ExpeditionSession` 与 `BattleRoot`（各一次成功 ✓）；到 `TuningConfig` 时已连续消耗大量轮次，
而它**必须改 `Validate` 的内部结构**（不是"搬成员"，是"拆方法"）⇒ 风险与其余 5 件不是一个量级 ⚠️
⇒ 我选择:**保留那条带理由的白名单（主程序认账 + 条件写在本文件）+ 把地图与清单留给下一次** ✓
   （这符合架构 §1.2 的明文许可，也符合"宁可如实豁免，不为数字冒险" ✓）
```
