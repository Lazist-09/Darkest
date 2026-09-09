# M0 工程引导 · 任务卡（m0_bootstrap.md）

> **编号**：ARCH-M0 · **类型**：task · **状态**：草案 v0.1
> **上游**：[设计] doc/architecture/_conventions.md §3（res:// 顶层铁律与命名）· doc/architecture/blueprint.md §2（选型）/§3（工程目录与依赖纪律）/§10（测试钩子）/§12（风险 8）· 工程实况 darkest/project.godot（config/features 含 "4.6"、dotnet assembly_name="Darkest"）· doc/modules/combat_math.md §7.1（冒烟公式样例）
> **决策引用**：—（M0 为架构新增的工程引导里程碑，无玩法/数据决策；判据来自 blueprint §2/§3/§10/§12）
> **依赖**：doc/architecture/_conventions.md（§4 文档头 / §6 任务卡模板，写作必守）· doc/architecture/blueprint.md · 无前序任务文件（M0 为全套任务之首）
> **最近更新**：2026-09-09

**一句话定位**：在写任何玩法机制之前，把 Godot 4.6 (.NET/C#) 工程、测试工程、res:// 目录骨架、CI/静态检查一次搭对，消除 blueprint §12 风险 8「工具链/版本耦合」→ M1 之后每层返工的后顾之忧；本里程碑不含任何玩法规则，判据全部可机器验证。

---

## 0. 范围与承接

- **做**：Godot 4.6 .NET 工程可用性冒烟 → res:// 目录骨架（blueprint §3）→ 组合根雏形 + 确定性 RNG 接入点（blueprint §8.1/§9.8）→ CI 与 `using Godot` 白名单静态检查（blueprint §3 依赖纪律 / §10）→ 1 个公式单测冒烟（blueprint §11 M0 行：editor 打开 + 测试工程跑通）。
- **不做**（写作铁律 6，不扩大切片）：主场景/战斗节点装配（归 M5）、任何玩法机制（M1 起）、data/*.json 正式内容（M1+ 按 data_schema 各分表逐里程碑成文）。
- **本文件每条任务卡遵守 _conventions §6 模板；只给路径清单 / 契约签名 / 判定式，不写业务实现代码。**

## 1. 里程碑完成判据（M0，全部可测）

| # | 判据 | 判定式 / 验证方法 |
|---|---|---|
| 1 | 编辑器可用（MVP） | Godot 4.6 (.NET) 打开 `darkest/`：无导入错误、无 C# 编译错误；`.godot/` 无 import 失败记录 |
| 2 | 解决方案可构建 | `dotnet build Darkest.sln` 成功 |
| 3 | 测试工程跑通（MVP） | `dotnet test`（Darkest.Tests）通过，含 1 个公式单测复算 [设计] combat_math §7.1 任意 2 个样例（推荐 `战士→近战小兵 = 9` 与 `施法者→战士(精神) = 8`） |
| 4 | Godot 引用白名单 | 静态检查：`scripts/core/**`、`scripts/gameplay/sim/**`、`scripts/data/**`、`tests/**` 内 `using Godot` 命中 = 0；负向注入样例使脚本非零退出 |
| 5 | 最小确定性冒烟 | 同 seed 构造 `BattleSession(seed)` 两次 → 注入的 IRngProvider 抽取序列逐一相等、DrawCount 单调递增（blueprint §8.1/§9.8） |

> 对应 blueprint §11「M0 工程引导」行：`darkest/` 在 Godot 4.6 .NET 可打开，测试工程跑通冒烟（含公式 1 例）；风险缓解对应 §12 风险 8。

## 2. 任务卡总览

| 卡 | 内容 | 依赖 | 一句话判据 |
|---|---|---|---|
| T-M0-01 | Godot 4.6 .NET 工程可用性冒烟（sln/csproj + Darkest.Tests + 条件编译核对） | — | `dotnet build` 成功、editor 打开无错 |
| T-M0-02 | res:// 目录骨架（.gitkeep 占位） | T-M0-01 | 目录集合与 blueprint §3 树一致、无多余顶层 |
| T-M0-03 | 组合根雏形：BattleSession(seed) + IRngProvider + CombatLog 骨架 | T-M0-01/02 | 同 seed 同序列、DrawCount 单调、零 Godot |
| T-M0-04 | CI 与 `using Godot` 静态检查（白名单脚本化） | T-M0-01/02 | 基线 0 命中 + 负向用例红 |
| T-M0-05 | 冒烟单测：combat_math §7.1 两样例复算 | T-M0-01/02 | `dotnet test` 绿、期望值改错即红 |

---

## 3. 任务卡

### T-M0-01 Godot 4.6 .NET 工程可用性冒烟
- **上游**：[设计] doc/architecture/blueprint.md §2（技术选型·项目骨架一次搭对）/§3（工程结构）· 工程 darkest/project.godot（`config/features=PackedStringArray("4.6", ...)`、`dotnet/project/assembly_name="Darkest"`）· 决策：—
- **依赖**：无（M0 首卡）
- **产出**：
  - `darkest/Darkest.sln`、`darkest/Darkest.csproj`（Godot 4.6 .NET 生成/维护的主工程，Godot.NET.Sdk）
  - `darkest/Darkest.Tests.csproj`（.NET 测试工程；blueprint §3 根目录清单，源码归 `darkest/tests/`）
  - 冒烟核对结论（条件编译/构建上下文事实，记录于主 csproj 注释或 CI 步骤注释，注明出处）
- **要点**：
  1. 首次由 Godot 4.6 (.NET) 编辑器打开 `darkest/`，经编辑器/`dotnet build` 生成 `Darkest.sln` 与 `Darkest.csproj`；程序集名与 project.godot `assembly_name="Darkest"` 一致；生成物进版本库，禁止手工造出与编辑器不一致的工程文件。
  2. 新增 `Darkest.Tests.csproj`：测试框架任选其一并固定（记录于 csproj）；内核代码（`core/sim/data`）以**源码直编**方式纳入测试工程，使测试工程不依赖 Godot 程序集即可编译——对应 blueprint §3「tests/ 直连 sim+director 编译，不经过场景树」，CI 无 Godot 窗口也能 `dotnet test`。
  3. 主 csproj 对 `tests/**` 做 Compile Remove，防止测试源码混入游戏程序集；边界：若 Godot 编辑器对 res:// 下第二个 *.csproj 产生误导入/双编译，将 `Darkest.Tests.csproj` 下移到 `darkest/tests/`（_conventions §3 允许该位置）并同步本卡路径——以冒烟实测结论为准。
  4. 条件编译/构建上下文核对并记录：核实 Godot 4.6 .NET 生成 csproj 的符号集与 GodotSharp 引用方式（编辑器内 / 导出 / 纯 `dotnet build` 三种上下文，符号集以实际生成 csproj 为准），确认「目录白名单（静态检查）而非程序集隔离」足以控制 Godot 引用（blueprint §12 风险 8 缓解）；任何仅编辑器内可用的代码须显式条件编译并注释出处。
  5. 版本库卫生：`.gitignore` 覆盖 `.godot/`、`.mono/`、`bin/`、`obj/`（核对 `darkest/.gitignore` 是否齐全）。
- **完成判据（可测）**：`dotnet build Darkest.sln` 成功；Godot 4.6 (.NET) 打开 `darkest/` 无导入/C# 编译错误；`Darkest.Tests` 空工程 `dotnet test` 绿；条件编译核对结论有落点可查。
- **风险/开放**：blueprint §12 风险 8（工具链/版本耦合）——本卡即其第一步缓解；冒烟期间新增踩坑一律记回本卡所属里程碑。

### T-M0-02 res:// 目录骨架
- **上游**：[设计] doc/architecture/_conventions.md §3（顶层铁律与命名）· doc/architecture/blueprint.md §3（目录细化树）· 决策：—
- **依赖**：T-M0-01
- **产出**：在 `darkest/` 下按 blueprint §3 逐目录建立，空目录以 `.gitkeep` 占位：
  - `scenes/battle/`（含 `prefabs/` 子目录）
  - `scripts/core/{rng, math, events, contracts}/`
  - `scripts/gameplay/sim/{board, pipeline, morale, survival, buffs, skill, turn, enemy, director}/`
  - `scripts/gameplay/scene/`、`scripts/ui/`、`scripts/data/`
  - `data/`、`resources/`、`tests/`
- **要点**：
  1. 顶层目录只允许 _conventions §3 的业务目录（`scenes/scripts/data/resources/tests`）与工程根文件（project.godot / icon.svg / sln / csproj / .godot 等）；蓝图可细化子目录，不得新增顶层。
  2. 目录即分层地图：`core` + `gameplay/sim` + `data` 承载确定性内核，`gameplay/scene` + `ui` 是表现层——M0 起立界空目录，防止后续代码放错层（blueprint §4 B2/B4/B5 边界）。
  3. 空目录仅放 `.gitkeep`，不放任何占位 C#（避免被 Godot 误编译）。
  4. 命名纪律先立：C# 类型 PascalCase、文件/资源 snake_case、配置字段名以 data_schema.md 为准（_conventions §3）。
- **完成判据（可测）**：脚本化对照——目录集合与 blueprint §3 树逐项一致，顶层无多余目录；`.gitkeep` 使空目录可入库；`dotnet build` 后 `.godot` 导入清单无异常新条目。
- **风险/开放**：无。

### T-M0-03 组合根雏形与 IRngProvider 接入点
- **上游**：[设计] doc/architecture/blueprint.md §3（core/rng、core/events、sim/director 归属）、§4 B2/B3（确定性内核边界、导演不自造随机）、§6.2（事件流=日志单一数据源）、§8.1（每场一个 seed：`BattleSession(seed)`）、§9.8（IRngProvider 签名）· 决策：—
- **依赖**：T-M0-01、T-M0-02
- **产出**：
  - `darkest/scripts/core/rng/IRngProvider.cs`（接口签名照 blueprint §9.8）
  - `darkest/scripts/core/rng/RngProvider.cs`（固定种子确定性实现，抽取带单调审计序号 DrawCount）
  - `darkest/scripts/core/events/CombatLog.cs` + 事件基类型 / `RngDraw` 记录（骨架：不可变、追加式、含抽取审计位）
  - `darkest/scripts/gameplay/sim/director/BattleSession.cs`（雏形：`BattleSession(seed)` 为一场战斗唯一入口，由组合根显式注入 IRngProvider；不含业务方法）
- **要点**：
  1. `BattleSession(seed)` 是「每场战斗一个显式 seed」的确定性入口（blueprint §8.1）；seed 只进不出，回放/复现 = seed + 命令流。
  2. 组合根职责（注入关系）先定契约：实机侧 BattleRoot/TurnDirector（M5 装配）与 headless 直驱（M6）都从**同一个** BattleSession 构造点注入 RngProvider；本卡只立内核侧契约，不建任何场景树节点。
  3. 唯一随机出口 = IRngProvider（blueprint §8.1「导演不得自己造随机」）：签名逐字对齐 §9.8——`NextPercent()` 取 [0,100)、`NextInt(min,max)`、`DrawCount` 单调审计；内核任何一处不得 new 自己的随机源。
  4. CombatLog = 事件流容器骨架（§6.2）：记录不可变事件；`RngDraw{DrawCount, Value}` 占位，保证 M2 起每条抽取可审计、可重放；一次写入、处处只读。
  5. 零 Godot 铁律：上述文件全在 core/sim 目录，不得出现 `using Godot`（T-M0-04 静态检查覆盖）。
  6. 边界：本卡不实现任何战斗步骤；BattleSession 只保证「seed → RNG → 可注入」这一条最小确定性链路成立。
- **完成判据（可测）**：`new BattleSession(seed)` ×2（同 seed）→ 两个会话经注入 RNG 的前若干次抽取逐一相等、DrawCount 序列单调递增（如 0,1,2,…）；CombatLog 追加 `RngDraw` 后原内容不可变（防篡改断言）；内核三目录零 Godot 引用可编译。
- **风险/开放**：无（确定性随机算法由实现自选，判据 = 同 seed 同序列，替换算法不影响契约）。

### T-M0-04 CI 与 `using Godot` 静态检查
- **上游**：[设计] doc/architecture/blueprint.md §3（依赖纪律注：core/sim/data 不得出现 using Godot）、§10（镜像测试行：CI 静态检查白名单「只允许 scripts/gameplay/scene 与 scripts/ui」）· doc/architecture/_conventions.md §3（目录分层）· 决策：—
- **依赖**：T-M0-01、T-M0-02
- **产出**：
  - `F:\GithubPro\Darkest\tools\check_godot_refs.*`（静态检查脚本；tools/ 在仓库根、不在 res:// 内，不受 _conventions §3 顶层限制）
  - `F:\GithubPro\Darkest\.github\workflows\ci.yml`（若仓库托管为 GitHub；否则以等价平台配置承载同规则）
- **要点**：
  1. 白名单唯一规则：`using Godot` 只允许出现在 `scripts/gameplay/scene/**` 与 `scripts/ui/**`；`scripts/core/**`、`scripts/gameplay/sim/**`、`scripts/data/**`、`tests/**` 命中 = 0（blueprint §3 注 + §10 表，tests 直连内核故同样禁入）。
  2. 脚本化判定式：对禁入目录执行正则 `using\s+Godot`；任一命中 → 非零退出并输出 文件+行号；全绿 → 退出码 0。
  3. 负向测试必须内置（防检查脚本自身失效）：自检向临时文件在 `core/` 下注入一行 `using Godot;` → 断言非零退出，随后清理。
  4. CI 编排：checkout → 安装与 Godot 4.6 .NET 配套的 .NET SDK → `dotnet build Darkest.sln` → `dotnet test`（Darkest.Tests）→ 静态检查；任一失败阻断。
  5. 边界：白名单以**目录路径**为唯一判据，不用文件名/扩展名猜测归属。
- **完成判据（可测）**：本地执行：基线 0 命中；负向注入后非零退出并给出文件+行号；CI 上 build+test+检查全绿。
- **风险/开放**：无（本条即 blueprint §12 风险 1「结算被表现层污染」的静态防线第一道，行为级兜底是 §10 镜像测试，归 M5/M6）。

### T-M0-05 冒烟单测：公式样例复算
- **上游**：[设计] doc/modules/combat_math.md §2（伤害公式）/§2.3（round 四舍五入、下限 1）/§7.1（单次伤害速查样例表）· doc/architecture/blueprint.md §10（公式单测钩子）/§11（M0 行）· 决策：—
- **依赖**：T-M0-01（测试工程）、T-M0-02（core/math 目录）
- **产出**：
  - `darkest/scripts/core/math/BattleMath.cs`（最小入口：物理/精神单次伤害，公式逐字 combat_math §2.1/§2.2/§2.3；M2 扩充为完整公式库）
  - `darkest/tests/FormulaSmokeTests.cs`（≥1 个用例，复算 §7.1 中任意 2 行）
- **要点**：
  1. 样例逐字（[设计] combat_math §7.1），推荐组合：
     - `战士 → 近战小兵 = 12 × 1.0 × (1 − 8/38) = 9`（物理，最简路径）；
     - `施法者 → 战士(精神) = 12 × 0.8 × (1 − 20%) = 8`（精神，7.68 进位——用于断言 `round()` 四舍五入而非截断，combat_math §2.3；截断会得 7，此样例钉死口径）。
  2. 实现只在 `core/math` 纯 C#（零 Godot），每行公式以注释回溯 combat_math 对应 §。
  3. 本卡只落冒烟 2 例；§7.1 其余 6 行与 钳制/下限/减免极值/多段 用例归 M2（blueprint §10「公式单测可复算」行）。
  4. MVP 判据 = 里程碑判据 #1（editor 打开）+ #3（`dotnet test` 通过）并集（§1）。
- **完成判据（可测）**：`dotnet test`：上述 2 断言绿；将期望值改为错误值 → 测试红（防「假绿」）；`dotnet test` 全程不依赖 Godot 窗口/编辑器。
- **风险/开放**：无（样例数字冲突时以 [设计] combat_math §7.1 为准并回查 data_schema，不阻塞）。

---

## 4. 引用章节速查（供实现与评审核对）

- doc/architecture/blueprint.md：§2（技术选型）、§3（工程目录/依赖纪律）、§4（B2/B3 边界）、§6.2（事件流）、§8.1（BattleSession(seed)/唯一随机出口）、§9.8（IRngProvider）、§10（公式单测/镜像测试/CI 白名单）、§11（M0 行）、§12（风险 8）。
- doc/architecture/_conventions.md：§3（顶层铁律/命名）、§4（文档头）、§6（任务卡模板）。
- doc/modules/combat_math.md：§2（伤害公式）、§2.3（round/下限 1）、§7.1（样例表）。
- darkest/project.godot：引擎 4.6 / assembly_name="Darkest"（工程实况，只读不改）。

---

## 5. T-M0-01 冒烟记录（主程序回填 2026-09-09；判据状态与踩坑实录）

### 5.1 工具链与构建上下文事实（T-M0-01 要点4 出处落点）

1. 本机（Windows）仅装 .NET SDK 10.0.400（`dotnet --list-sdks`），无 .NET 8 SDK/运行时；NuGet 源不可达（离线，api.nuget.org TLS 失败）。→ net8.0 目标在本机无法离线还原（缺 Microsoft.NETCore.App.Ref 8.x，空工程 NU1301 探针实证），见 `darkest/Darkest.csproj` 注释。
2. Godot 4.6 .NET 配套包已缓存：godot.net.sdk / godotsharp / godotsharpeditor / godot.sourcegenerators = 4.6.1（GodotSharp lib 目标 net8.0，出处 `~/.nuget/packages`）；测试栈缓存：mstest.testframework 3.6.4 / mstest.testadapter 3.6.4 / microsoft.net.test.sdk 17.14.1 / Microsoft.Testing.* 1.4.3（xUnit/NUnit 未缓存 → 测试框架固定 **MSTest**，记录于 `Darkest.Tests.csproj`）。
3. **本机冒烟命令**（离网可用）：
   - `dotnet restore Darkest.csproj|Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 -p:NuGetAudit=false`（分工程 restore 均可用；**sln 级 restore 因 metaproj 内版本化 SDK 解析需联网而失败**——不作为本机判据，CI 在线环境执行 `dotnet build Darkest.sln` 全流程）；
   - `dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0 --no-restore -m:1`（✅ 0 警告 0 错误）；
   - `dotnet test Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --no-build`（✅ 8/8 通过，见 5.2）。
4. **TFM 覆盖机制**：NuGet restore 不接受 `-p:TargetFramework`（单数）全局覆盖（实证：assets 仍按项目内 net8.0 还原），统一改经自定义属性 **DarkestTargetFramework** 间接取值，restore/build/test 全部一致生效。

### 5.2 判据状态

| 里程碑判据 | 状态 |
|---|---|
| #1/#2 编辑器可用/无导入错误 | ⏸ 待装有 Godot 4.6 (.NET) 编辑器的机器复验（本机无编辑器可执行文件）；工程文件已按编辑器生成形状编写 |
| #2b 解决方案可构建 | ✅ `dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0 --no-restore -m:1` → Darkest.dll + Darkest.Tests.dll，0 警告 0 错误 |
| #3 `dotnet test` 含 §7.1 两样例复算 | ✅ 8/8 通过；**最终复验 2026-09-09 21:22 全绿**（解除本机应用程序控制策略拦截后；中途拦截见 5.3） |
| #4 `using Godot` 白名单静态检查 | ✅ 基线 0 命中；负向注入自检 PASS（`tools/check_godot_refs.py`，负向样例带文件+行号） |
| #5 最小确定性冒烟 | ✅ 同 seed 同序列、DrawCount 单调递增、显式注入可证、CombatLog 追加后原内容不可变（RngSmokeTests） |

### 5.3 踩坑实录（写回本卡所属里程碑）

- **同目录双 csproj 生成物互吞（CS0579）**：blueprint §3 把 Darkest.Tests.csproj 放 res:// 根，与其默认 obj 落在同目录 → 游戏工程默认 `**/*.cs` 通配把测试工程生成的 `obj/.../AssemblyInfo.cs` 编入 Darkest.dll → 特性重复。修复：仓库根 `Directory.Build.props` 仅对 **Darkest.Tests** 重定位 obj/bin 到 `tests/` 下（须在 Microsoft.Common.props 之前设置，否则 MSB3539），并在 Darkest.csproj 显式 `Compile Remove=".godot/**/*.cs"`。
- **`dotnet build Darkest.sln` 并行构建在本机沙箱出现 MSB5021（csc 被取消）**：`-m:1` 串行构建正常 → 本机判据以 `-m:1` 为准；CI（Linux 常规进程模型）不受影响。
- **VSTest testhost 需 OpenProcess 父进程句柄**：受本机进程策略限制（Win32Exception 5 拒绝访问），`dotnet test` 须在全权限上下文执行（已记录，CI 无此约束）。
- **全量 8/8 通过后复验被「应用程序控制策略 0x800711C7」拦截**：疑似本机 WDAC/执行策略按文件身份拦截新构建的测试程序集（同文件复制到其它目录同样被拦）；一次完整通过已留存于本节 5.2，后续以 CI/编辑器机器复跑为准。

### 5.4 边界/开放（不阻塞 M0）

- T-M0-01 要点1「首个 csproj/sln 由编辑器生成」：本机无编辑器，按编辑器生成形状手写并记录；编辑器打开后如有出入以编辑器实况为准（含 res:// 根第二 csproj 误导入/双编译时，按本卡边界把 Darkest.Tests.csproj 下移 `darkest/tests/` 并同步路径的预案）。
- 本里程碑不含玩法/数值决策；O-11（撤退公式）/O-21（威吓箭士气口径）/O-33/O-34 与 M0 无关，其默认口径自 M2 起按卡引用实现，不阻塞。
