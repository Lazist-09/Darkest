# 「无注释整类体」清单（架构 ⑩ 点名要的 · 2026-09-21 实测）

> 🔴 **架构原话**：「**请把【无注释的整类体清单】给我**（按块：类/方法 + 行数）⇒ 我按块审，
>   优先审『**为什么这么写**』的那类」
> 🔴 **本清单用【注释密度】排序**（`^\s*//` 行数 ÷ 总行数）⇒ 密度最低的 = 最可能"反编译/无注释"来源 ✓
> 🔴 **成因我交代清楚**：排在最前的 `ExpeditionFlow*` 那批，是**我按你裁的"路线 c"回填**的产物 ——
>   整类体来自 **`ilspycmd` 反编译**（当时原文件被我误 `checkout` 覆盖 ✗）⇒ **反编译不带注释** ⇒ 密度 2~3% ✓
>   ⇒ 所以这份清单里**真正需要你审的、也是最有价值的**，就是它们 ✓

## 1. 注释密度最低的 11 个文件（≥120 行 · 实测）
| 文件 | 行数 | 注释行 | 密度 | 归属 |
|---|---|---|---|---|
| `gameplay/sim/run/ExpeditionFlow.TileWalk.cs` | 153 | 3 | **2.0 %** | 🔴 **我的回填产物** |
| `gameplay/sim/run/ExpeditionFlow.Reveal.cs` | 134 | 3 | **2.2 %** | 🔴 同上 |
| `gameplay/sim/run/ExpeditionFlow.cs` | 357 | 10 | **2.8 %** | 🔴 同上（主体） |
| `gameplay/sim/run/ExpeditionFlow.Topology.cs` | 103 | 3 | 2.9 % | 🔴 同上 |
| `gameplay/sim/buffs/BuffLedger.cs` | 253 | 8 | 3.2 % | 并行写入者（已入库） |
| `gameplay/sim/run/ExpeditionFlow.BattleReturn.cs` | 91 | 3 | 3.3 % | 🔴 我的回填产物 |
| `data/MoraleEventsConfig.cs` | 175 | 7 | 4.0 % | 并行写入者 |
| `data/SkillsConfig.cs` | 504 | 23 | 4.6 % | 并行写入者 |
| `gameplay/sim/run/ExpeditionFlow.Traps.cs` | 62 | 3 | 4.8 % | 🔴 我的回填产物 |
| `gameplay/sim/run/ExpeditionFlow.Outcome.cs` | 61 | 3 | 4.9 % | 🔴 我的回填产物 |
| `gameplay/sim/run/ExpeditionFlow.Hunger.cs` | 51 | 3 | 5.9 % | 🔴 我的回填产物 |

## 2. 建议的**审阅顺序**（按"为什么读不出"的程度）
```
① **`ExpeditionFlow.cs`（357 行 · 主体）+ 7 个 part（共 ~950 行）** —— 反编译来源，**"为什么"全丢** ✗
   ⇒ 这 8 个文件的注释**只能由作者（我）或你补**，而我**没有原始注释**（它们在事故里丢了 ✗）
   ⇒ 📌 **我能补的是"行为说明"（怎么写的），补不了"当初为什么这么写"** ⇒ 所以**请你优先审这批** ✓
② **`BuffLedger.cs` / `MoraleEventsConfig.cs` / `SkillsConfig.cs`** —— 并行写入者的产物（已入库 ✓）
   ⇒ 它们的"为什么"作者可能还在（或在窗口里）⇒ 建议**先问作者**，你再审 ✓
```
## 3. 我这边能立刻配合的
```
· **逐块方法签名 + 行号**：任何人要，我用一条命令就能出（本清单就是那么生成的 ✓）
· **行为性注释**：我可以给 `ExpeditionFlow` 那批补"**这段在做什么**"（从代码读得出来的部分 ✓）
  ⇒ 🔴 但我会**明确标注**"这是行为说明，不是作者原意"（不冒充原始注释 ✗）
```
