# MCP 工具链入库 · 探针清理 · 重复副本删除（2026-10-03）

> 一句话：`godot_mcp`（Godot MCP Native v1.0.8 · MIT · yurineko73）从【仓库根的错位置副本】搬正为
> `darkest/addons/godot_mcp`（99 文件）并**入公开仓库**；实测 38 个 MCP 工具可用（§二 两条 JSON 铁证）；
> 同批删除【被 MCP 覆盖的一次性探针 646 文件】＋【重复副本 90 文件】＝ 736 文件 / 15.4 MB，
> 与整项目重复副本 `darkest-(4.6)/`（118 MB / 1,750 文件）；保留 84 项「在用工具 / 前读数 / 基线 / 取证」。
> 决策号 `#547`。

## §一 四环链路（本机实测 · 可复跑）

| 环 | 实况 | 出处 |
|---|---|---|
| ① 插件本体 | `darkest/addons/godot_mcp/`（99 文件）· `plugin.cfg` = `Godot MCP Native` / `yurineko73` / `1.0.8` / `auto_start=true` `log_level=3` `security_level=1` `rate_limit=100` | 目录实况 ＋ `plugin.cfg` |
| ② 项目启用 | `darkest/project.godot:34-36` `[editor_plugins] enabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")` ＋ `:21` autoload `MCPRuntimeProbe="*uid://bsg12huaf1u5i"` | `git diff darkest/project.godot` |
| ③ 服务端 | `127.0.0.1:9080` **OPEN**（服务器活在**编辑器进程**内） | 端口探针 |
| ④ 客户端 | `~/.codex/config.toml` `[mcp_servers.godot] enabled=true url="http://localhost:9080/mcp"` | 配置实况 |

实测两条 JSON（本件现场）：

- `get_project_info` ⇒ `{"godot_version":"4.7.stable","project_name":"Darkest","project_path":"F:/GithubPro/Darkest/darkest/","main_scene":"res://scenes/main/MainMenu.tscn"}`
- `get_editor_state` ⇒ `{"editor_mode":"editor","active_scene":"","selected_count":0}`

⇒ **前提**：Godot 编辑器进程必须开着；关掉编辑器 ⇒ 全部 MCP 工具失效（回落到命令行探针）。

## §二 MCP 工具面（38 个 · 本会话可用）与被覆盖的旧探针

| 类别 | 个数 | 代表工具 | 覆盖的旧探针工作 |
|---|---|---|---|
| 节点 | 9 | `get_scene_tree` / `create_node` / `update_node_property` / `delete_node` | 读场景树、改节点、查属性（`.tmp_probe_*.py` 家族） |
| 脚本 | 9 | `read_script` / `write_script` / `list_scripts` | 读/写 `.gd`/`.cs`（`.tmp_read*.py` / `.tmp_write*.py`） |
| 场景 | 6 | `open_scene` / `save_scene` / `list_scenes` | 打开/落盘场景（`.tmp_*_spec.py`） |
| 编辑器 | 6 | `execute_editor_script` / `get_editor_logs` / `get_project_info` | 编辑器内跑命令、查日志（`.tmp_*probe.ps1` / `.tmp_logscan*.py`） |
| 调试 | 4 | 运行时探针 / 断点 / 运行控制 | 运行时观测（`.tmp_probe_run.ps1`） |
| 项目 | 4 | `list_project_resources` / 资源读写 | 资源清点（`.tmp_probe_fs10.py`） |

📌 插件入库的 99 文件里含 **11 个 `.uid`**（Godot 4.4+ 的 `uid://` 解析必需 —— autoload 就写成 `*uid://bsg12huaf1u5i`）⇒ **必须入库**，否则换机 clone 后 autoload 解析不到。

## §三 删什么 / 留什么（判据即代码：`.tmp_cleanup_20261003.py`）

### ① 被 MCP 覆盖的一次性探针：606 项 / 646 文件（`.tmp_*`）

一次性、可复现、且功能已被 MCP 38 工具或内建工具覆盖：读场景树 / 改节点 / 落盘 / 跑编辑器内命令 / 扫日志 /
写报告 / 拆分与验证脚本 / 提交消息草稿 / 一次性读数 dump。

### ② 重复副本：90 文件

- **仓库根 `addons/godot_mcp`（88 文件）** —— **逐字节同源**比对（md5 全树）：`only_root = []`（根副本无任何独有文件）、
  `differing = []`（共有文件全同）⇒ **纯副本**。Godot 项目根是 `darkest/`，根副本**从未被引用**
  （`#546` 已实测「装错位置 ⇒ 未启用、帮不上忙」）。
- **根 `__pycache__/`（2 文件）** —— `.tmp_*_spec.py` 的编译产物。

### ③ 整项目重复副本：`darkest-(4.6)/`（118,357,583 B / 1,750 文件）

- 生成时刻 **2026-10-03 17:24:28~17:24:39**（11 秒 ⇒ 脚本级复制，非手工）
- 与 `darkest/` 逐文件 md5 比对（排除 `.godot/` `TestResults/` `Godot/` `.tmp_appdata/`）：
  **独有 5** 个（全是 `Darkest.csproj.old.1~5` = 编辑器备份垃圾）· **不同 4** 个（`.gitignore` / `Darkest.csproj` /
  `Darkest.csproj.old` / `project.godot` = 4.6 时代配置）· 全仓**零引用**（`rg` 源码/文档 0 命中）
- ⚠️ **诚实边界**：删除调用 `Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(..., SendToRecycleBin)`，
  但事后枚举 `F:\$RECYCLE.BIN\S-1-5-21-...` 的 `$I*` 元数据**未见该目录** ⇒ **实为永久删除**（未进回收站）。
  不涉源码损失（其源码 = 已入库的 `darkest/`，独有物仅 5 个 `.csproj.old` 垃圾）；若要 4.6 副本身份，重做一次复制即可。

### ④ 保留面（84 项 · 判据）

| 族 | 个数 | 为什么留 |
|---|---|---|
| 在用工具（`.tmp_rng.py` / `.tmp_parse_uiaudit.py` / `.tmp_gates8.cmd` / `.tmp_gates.cmd`） | 4 | MCP 替代不了：行区间打印 / ui-audit 日志解析 / 八门禁一键 |
| 前读数（`.tmp_state_pre517~522.md` / `.tmp_fs_pre*.md` / `.tmp_dd1_pre*.md` / `.tmp_hamlet_pre*.txt` / `.tmp_uiaudit_before\|after.txt`） | 22 | 判据 367：『前』读数必须在改动之前落定 —— 它们是**证据**不是探针 |
| 行数基线 `.tmp_baseline_*.cs` | 28 | 拆文件前基线（`#545` 家族取证） |
| 取证 `.tmp_m6u_*`（含 `armA`/`armB`/`shot.png`） | 22 | `#545` 已登记在案的截图与读数 |
| 报告分片 `.tmp_report_split4/5.md` ＋ `.tmp_save_backup.json` ＋ 清理留痕 | 8 | 报告组装源 / 本地存档备份 / 本件审计留痕 |

### ⑤ 清理留痕（本地 · 不入库）

`.tmp_cleanup_20261003.py`（判据即代码）· `.tmp_removed_manifest_20261003.txt`（逐文件清单）·
`.tmp_removed_20261003.zip`（**删除前打包**，含 734 文件；`darkest-(4.6)` 未入包）

## §四 同批仓库卫生（公开仓库）

- 🔴 **`O-114` 复发当场修复**：编辑器保存把 `darkest/Darkest.csproj:32` 由 `$(DarkestTargetFramework)`
  实化为 `net8.0` ⇒ 守门 `tools/check_csproj_tfm.py` **FAIL** ⇒ 还原 ⇒ **OK（2 csproj）**。
  （本机 net10 覆盖依赖这一行；CI 默认 net8.0 不受影响）
- 🧹 **根 `.gitignore` 补两条**（探针只在本机）：`/.tmp_*`、`/__pycache__/`。
  （`darkest/.gitignore` 已有 `*.csproj.old*` ＋ `/nuget.config` ⇒ 本机离线源不入库）
- 🧹 **追踪垃圾清理**：`.tmp_sweep_out.txt`（1,376 B · UTF-16LE PowerShell 报错文本 · `6f58cb1 清理commit` 误入库）
  ⇒ 本批删除（公开仓库里唯一的 `.tmp_*` **追踪**文件）
- 🧹 孤儿 `darkest/tests/M9FourVFourFixtureTests.cs.uid`（对应 `.cs` 已不存在）按既有约定留本地 ⇒ 本棒删除（不入库）

## §五 验证（最低限度必要性测试）

| 项 | 命令 | 读数 |
|---|---|---|
| 构建 | `dotnet build darkest/Darkest.csproj -m:1 -nodeReuse:false -tl:off -v:q` | **RC=0 · 0 错 / 73 警告**（警告均既存） |
| 启动冒烟（玩家路径 · 红线 18） | `Godot_v4.7.2-stable_mono_win64_console.exe --path darkest --headless --quit-after 240`（APPDATA 隔离） | **RC=0** · 尾部 `[启动自检·调色板] ✅（40 项）` · 日志 `mcp`/`autoload`/`SCRIPT ERROR` **0 命中** ⇒ 新 autoload 干净加载、无调试器时静默 |
| 门禁 8 项 | `cmd /c .tmp_gates8.cmd` | **RC1~RC8 = 0**（file_size 632/0/0 · file_budget 全 ≤400 · godot_refs 0 hits · data_discipline 可疑 0 · no_external_assets OK · csproj_tfm OK · ui_namespace OK · placeholders PASS） |
| 未跑全量 `dotnet test`（如实登记） | — | 本件**零 C# 逻辑改动**（csproj 一行还原 ＋ `project.godot` 配置 ＋ 第三方插件目录）⇒ 按用户约束「最低限度必要性测试」不跑全量 |

📌 两条既存非致命回溯仍在（`FeFlowSkeleton` / `CreditsSkeleton` 的 `LayerTitle` 父路径告警）—— `#546` 已取证，本件不涉。

## §六 安全边界（红线 21 不留黑箱）

- 插件 HTTP 服务**默认无鉴权**（`security_level=1`）· 只绑本机 ⇒ 仅开发期使用；**编辑器插件不进导出产物**。
- 但 autoload `MCPRuntimeProbe` **会**进导出产物（`project.godot [autoload]`）：实测
  `_ensure_debugger_capture_registered()` 在 `EngineDebugger.is_active()==false` 时**立即返回**
  （无调试器 = 空转节点，仅每帧一次 `is_active()` 判断）⇒ 处置 = **保留并登记**（零功能影响；去掉它会让本机插件失效）。
  若后续做导出流水线，可在 export preset 里排除 `addons/godot_mcp/`。
- 上游来源：`https://github.com/yurineko73/Godot-MCP-Native`（MIT · `LICENSE` 随包入库）。

## §七 下一件

`M10u B-1` 绘层：给 `darkest/scenes/ui/ui_root.tscn` 抬 `CanvasLayer`（判据「外壳三层都在当前场景之上」＋ S1 指纹），
⚠️ `UIRoot.cs:81-86` 的 `GetNodeOrNull<Control>("BaseLayer")` 路径需同步 —— **本件起改用 MCP 工具做**
（`get_scene_tree` ⇒ `create_node`/`update_node_property` ⇒ `save_scene`），正是「用现成轮子」的第一次落地。

---

📌 关联：`reports/toolchain_godot472_20261003.md`（`#546` 三问取证 · 插件装错位置）· `doc/state.md` `#547` ·
`reports/INDEX_lead_programmer.md §7`
