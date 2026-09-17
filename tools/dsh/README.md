# `tools/dsh/` —— 一键冒烟与自检（**写给下一个人看**）

> 🔴 **为什么有这个目录**：用户 2026-09-21 原话 ——
> 「`DungeonRunDriver` 那套 CLI 旗标很聪明，但**只有你知道**…否则这套验证能力会**随人员变动丢失**」
> ⇒ 这里把「怎么跑 / 看什么 / 什么算红」**固化成脚本 + 文档**，不再依赖某个人的记忆 ✓

## 1. 一条命令：自检（**不需要 Godot**，最快）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/selfcheck.ps1
```

跑五项，**任一项非 0 ⇒ 整条非 0**：

| # | 检查 | 判据 |
|---|---|---|
| 1 | `tools/check_godot_refs.py` | 内核零 Godot（`scripts/core` · `data` · `gameplay/sim` · `tests`）+ `scripts/ui`/`scene` 不用 `System.IO` |
| 2 | `tools/check_data_discipline.py` | 数字纪律（**可疑数字 = 0**）· 死函数/死数据清单 |
| 3 | `tools/dsh/check_ui_namespace.ps1` | UI 命名空间统一为 `Darkest.UI`（**含小写限定引用**） |
| 4 | `tools/dsh/smoke_gate.ps1 -SelfTest` | CI 判据脚本自身可用（收 `OK` / 拒 `BAD` / 缺行报错） |
| 5 | `tools/dsh/selfcheck.ps1` 内联 **占位合规** | `borrow/` 与 `assets/heroes_placeholder/` **不入 git**、**有 gitignore 规则**、**未泄漏进 `resources/`**（`assets_credits.md` A1 家族 · **合规不变量**） |

## 2. 一键冒烟（**需要 Godot**）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke.ps1            # 全套（10 例）
powershell ... -File tools/dsh/smoke.ps1 -List                                     # 只列用例表
powershell ... -File tools/dsh/smoke.ps1 -Case e2e                                 # 只跑一例
```

**用例表**（CLI 旗标 = 契约的一部分；改这里要同步 `doc/architecture` 的"冒烟入口表"）：

| 用例 | 旗标 | 说明 |
|---|---|---|
| `entry-main1` | `--smoke=main:1` | 主菜单 → 单场战斗 |
| `topology-auto` | `--topology-auto` | 跑图到终点（远征主干） |
| `hamlet-next` | `--hamlet-next` | 回城 → 再出发（城池循环） |
| `e2e` | `--smoke=main:1 --e2e` | **完整回路**：跑图 → 回城 → 花钱 → 再出发（含 `[养成]` 读数） |
| `map-mode` | `--click-menu=0 --battle-map-mode` | 战斗 → 地图模式（骨架不重建） |
| `town-step` | `--hamlet` | 城池（含建筑入口/二级菜单） |
| `ui-audit` | `--ui-audit --fixed-fps 60` | 布局判据（0 重叠 / 0 透明） |
| `dungeon-in-scene` | `--dungeon-in-scene` | 宿主内进地牢（**片 3.1：进 Walking 不起战斗**） |
| `tile-walk` | `--smoke=main:1 --tile-walk` | 瓷砖主画面（走格开启 + UI 自证行） |
| `abandon` | `--smoke=main:1,abandon` | 放弃远征（行走模式按按钮 ⇒ 结束本趟回城） |

## 3. 🔴 ERROR 口径（**别再把"引擎退出"当成 bug**）

每例会打印：`行数 | 真错误 | 引擎退出泄漏 | exit`

| 分类 | 判据 | 是否判红 |
|---|---|---|
| **真错误** | 其余 `ERROR` / `SCRIPT ERROR` | ✅ **只有它判红** |
| **引擎退出泄漏** | `leaked at exit` · `RID allocations` · `resources still in use at exit` | ❌ 只作信息 |
| **已知噪声** | `AudioDriver` · `DisplayServer` · `OpenGL` · `Vulkan` · 缺失 `.wav` · `texture not found` | ❌ |
| 🔴 **空日志（行数 = 0）** | 启动失败 / 指错项目 | ✅ **判红**（防"0 错误"假绿） |

## 4. CI 三步（**只有第 ③ 步的退出码决定 CI**）

```powershell
# ① 确保没有别的 Godot 在跑（脚本会【拒绝并发】并退 2 —— 防两个实例争同一项目而崩溃）
# ② 跑冒烟（结果写进 reports/smoke_summary_*.txt）
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke.ps1
# ③ 读摘要里的机器可读判决行（RESULT: OK / RESULT: BAD n=<N>）
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke_gate.ps1
```

> 🔴 **为什么不看步骤 ② 的退出码**：实测 `powershell -File` 下它**不可靠**（绿色用例也会退 1）
> ⇒ CI 只信 **步骤 ③**（读 `RESULT` 行，确定性的）✓

## 5. 🔴 已知坑（都踩过，别再踩）

| 坑 | 症状 | 正解 |
|---|---|---|
| **含中文的 `.ps1` 没有 BOM** | PS 5.1 按 ANSI 解码 ⇒ **语法直接断**（`string is missing the terminator`） | 存成 **UTF-8 with BOM**（与 `.tres` 必须**不带** BOM 正相反 —— BOM 取舍取决于**读者**） |
| `Select-String -Recurse` | **无此参数** ⇒ 那行静默不生效 ⇒ **判据从没执行还报 ✅**（假通过） | 用 `Get-ChildItem -Recurse -Filter *.cs \| Select-String` |
| **中点 `·` 当输出标记** | 中文控制台编码会打乱 ⇒ 管道里 `grep ·` **抓不到** | 用 **ASCII 标记**（本仓用 `  > `） |
| **空日志当通过** | 启动失败被读成"0 错误" | **行数 = 0 ⇒ 判红** |
| **`exit $(if …)`** | 退出码不可靠 | 先 `$code = …` 再 `exit $code` |
| **Godot 进程残留** | 反复 Force-kill / 实例重叠 ⇒ 争用（甚至原生崩溃） | 脚本 **try/finally 回收自己起的子进程** + **并发保护** |

## 6. 读数提取（**不必人肉 grep**）

`smoke.ps1` 跑完会**自动提取关键读数**（控制台 + 摘要都写），覆盖：
`[养成]`（P0 养成对比）· `[片3.1]`（进地牢不起战斗）· `[片4] ✅`（走格开启/回地图）·
`[UI 瓷砖]` · `[UI 相位]` · `[M7] 🆕 V10` · `[P2]`（升级对照）
