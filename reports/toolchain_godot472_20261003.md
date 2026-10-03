# 工具链收口 · Godot 4.7.2 实况 ＋ 构建链断裂修复 ＋ 编辑器/游戏窗口三问取证（2026-10-03）

> 一句话：本机编辑器已升到 **Godot 4.7.2 mono**；它保存 csproj 时把 Sdk 写成 `Godot.NET.Sdk/4.7.2`
> （并把 `TargetFramework` 实化为字面量，`O-114` 家族）⇒ 本机 NuGet 离线缓存里没有 4.7.2 包 ⇒
> **`MSB4236` ＋ `NU1301`** ⇒ 编辑器侧构建（按播放走的就是这条）**直接失败**。本件修复并实测
> **`dotnet build` RC=0**。用户三问（窗口点不动／启动闪退／`godot_mcp`）逐条取证见 §二。

## §一 现场（本机实况 · 可复跑）

| 项 | 实况 | 出处 |
|---|---|---|
| 本机编辑器 | `E:\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe` | 用户 2026-10-03 命令（附件 `已粘贴的文本.txt`） |
| 4.6.1 编辑器 | `E:\Godot_v4.6.1-stable_mono_win64\`（未删 · 可回退） | 目录实况 |
| `project.godot` | 编辑器已把 `config/features` 由 `"4.6"` 迁到 **`"4.7"`** | `git diff darkest/project.godot` |
| `Darkest.csproj` | 编辑器已把 Sdk 由 `Godot.NET.Sdk/4.6.1` 写成 **`Godot.NET.Sdk/4.7.2`**，并把 `TargetFramework` 实化 | `git diff darkest/Darkest.csproj` |
| 显示器 | 主屏 `2560x1440 @(0,0)` · 副屏 `1920x1200 @(-1920,0)` · 虚拟桌面 `(-1920,0) 4480x1440` · 2 屏 | `GetSystemMetrics(0/1/76..80)` 实测（本件探针 `.tmp_winprobe.ps1`） |

## §二 用户三问取证

### ① 编辑器「窗口内点不动」：保存的窗口矩形与当前双屏对不上（同族两条记录 · 一条已改观、一条仍越界）

`darkest/.godot/editor/editor_layout.cfg`（本地缓存 · 不入库）两次实测对照：

| 读取时刻 | `[EditorWindow]` | `[GameView]` 浮动窗 |
|---|---|---|
| 2026-10-03 17:36（编辑器当时在跑） | `screen=1 position=(2676,304) size=1152x1027` ⇒ **左边界越出桌面右界 2560 共 116 px** | `floating_window_rect=(2127,294,2203,1100) floating_window_screen=1` |
| 2026-10-03 18:2x（本件复核） | `screen=-1 mode="maximized" position=Vector2i(0,0) size=1152x1027`（已被改写为最大化） | 同上（**仍越界**：主屏只到 x=2560，而该窗从 2127 起宽 2203 ⇒ 大半在桌面外，只在主屏右缘留窄条） |

编辑器设置（`%APPDATA%\Godot\editor_settings-4.7.tres` 实测）：
`interface/editor/appearance/editor_screen=-5`（Screen 0）· `interface/editor/display/single_window_mode=false`（原生子窗）·
`run/window_placement/rect=1`（Centered）· `run/window_placement/screen=-5`（Screen 0）· `run/window_placement/game_embed_mode=0`（Auto）
⇒ **设置项本身没有错值，错的是被保存下来的窗口矩形记录**。

⇒ 症状（「能拖动/能关，但里面点不动」）的两个机制（都属「窗口/对话框落在看不见的位置」一族）：

1. **主窗恢复到桌面外**（17:36 那条记录；当时若被 Windows 夹回可见区，用户看到的是"夹回后的窗"，
   而它派生的子窗仍按旧矩形摆）；
2. **4.6→4.7 项目迁移会弹模态确认框**（"项目由旧版本编辑过"），模态窗口存在期间**主窗整窗不接受点击**，
   只剩 Windows 系统级的拖动/关闭可用 —— 若该模态窗以越界的主窗为锚，它就落在看不见的地方，表现正是「点不动」。

处置（本件已做 · 可逆）：把 `[GameView]` 的两行越界记录（`floating_window_rect` / `floating_window_screen`）
从该文件删掉（Godot 下次打开重建默认值；其余 dock 布局原样保留）。若复现：编辑器菜单
**窗口 → 重置窗口布局**；仍不行则用 `Alt+Tab` 找隐藏对话框，并看任务管理器里编辑器是否「未响应」。

### ② 「项目启动就闪退」**不是崩溃**（三条实测）

- 用户命令 **没有 `-e`** ⇒ 跑的是**游戏**不是编辑器（`--single-window` 是编辑器侧旗标，对游戏无意义）。
- 该次日志（`%APPDATA%\Godot\app_userdata\Darkest\logs\godot2026-10-03T17.40.34.log` 8.3 KB / `godot.log` 15.7 KB）
  尾部 = `[启动自检·调色板] ✅ …（40 项）`（`MainMenuRoot.cs:207`）⇒ 主菜单初始化**跑完**；之前的 C# 回溯是
  `CreditsSkeleton.TryInstantiate()`（`CreditsSkeleton.cs:19`）的**非致命**报错（`LayerTitle` 家族 · 父节点名不符），
  游戏继续运行；随后 `Unloading: Disposing tracked instances...` 是**正常退出清理**。
- 本件四条探针全 **exit 0**：`--headless --audio-driver Dummy --quit-after 200` ／ `--single-window --rendering-driver opengl3
  --fixed-fps 60 --quit-after 300 -v` ／ `--headless -e --quit-after 600` ／ **真窗口 12 秒存活探针**（`ALIVE=True PID=24796`）。
- 事件查看器近 3 天**无 Godot 崩溃记录**；`rg "GetTree().Quit"` 只命中 `SmokeScript.cs`（`--smoke=` 专用路径）。
- ⇒ 成因 = ① 的窗口落在看不见处 ＋ 命令行跑完自动退被当成闪退 ＋ `--single-window` 让游戏与编辑器共窗。
  验证动作：**去掉 `--single-window`** 再跑一次，应出现独立游戏窗口（Centered · Screen 0）。

### ③ `addons/godot_mcp` 当前**帮不上忙**（装错位置 ＋ 未启用）

- 目录在**仓库根** `F:\GithubPro\Darkest\addons\godot_mcp`，而 Godot 项目根是 `darkest/` ⇒ 正确位置是
  `darkest/addons/godot_mcp`；`darkest/project.godot` **没有 `[editor_plugins]` 段**（`rg` 零命中）⇒ 插件从未启用。
- `plugin.cfg`：`Godot MCP Native`（`yurineko73` · v1.0.8 · 纯 GDScript）· `[mcp] auto_start=true`。
- 能帮的：让 AI 客户端**直接读场景树/跑编辑器内命令**（可替代本项目大量 `.tmp_*.py` 探针）；**不能**修 ①
  （那是编辑器窗口/布局问题）。采纳 = 移进 `darkest/addons/` ＋ `project.godot` 注册 `[editor_plugins]` ＋ 决策登记
  （仓库公开 ＋ 红线 21「不留黑箱」）⇒ **是否入库待用户裁定**。

## §三 构建链断裂与修复（本件主交付）

**症状**：`dotnet build darkest/Darkest.csproj`（编辑器构建走的就是这条）⇒ **exit 1**：
`error MSB4236: 找不到指定的 SDK "Godot.NET.Sdk/4.7.2"` ＋ `NU1301 无法加载 api.nuget.org`（离线）。

**根因**：编辑器把 Sdk 写成 4.7.2，而本机 `~/.nuget/packages` 只有 `godot.net.sdk/4.5.1,4.6.1` 与
`godotsharp/4.4.0,4.5.1,4.6.1`；4.7.2 的 nupkg **只存在于** `E:\Godot_v4.7.2-stable_mono_win64\GodotSharp\Tools\nupkgs\`。

**修复（三处）**：

1. `darkest/Darkest.csproj`：Sdk 随编辑器 = `Godot.NET.Sdk/4.7.2`（沿用既有纪律「与编辑器实况有出入以编辑器为准」），
   并把被实化的 `<TargetFramework>` 还原为 `$(DarkestTargetFramework)`（`O-114`）。
2. 🆕 `darkest/nuget.config`（**不入库** · 已进 `.gitignore`）：把 Godot 安装目录自带 `nupkgs` 加为本机离线源。
3. 提权 `dotnet restore darkest/Darkest.csproj -v:q` ⇒ 4.7.2 三件包装进全局缓存（沙箱内写不进全局缓存）。

**实测**：`dotnet build darkest/Darkest.csproj -m:1 -nodeReuse:false -tl:off -v:q` ⇒ **RC=0（0 错）** ·
`Darkest.runtimeconfig.json` = `"tfm": "net8.0"`（`rollForward: LatestMinor`）· `…_console.exe --path darkest --headless
--audio-driver Dummy --quit-after 200` ⇒ **exit 0**（调色板自检 ✅）· `python tools/check_csproj_tfm.py` ⇒ **OK**（2 个 csproj 全走间接取值）。

**连带**：删 6 个 `darkest/Darkest.csproj.old*`（编辑器每次保存生成的备份）；`darkest/.gitignore` 的
`*.csproj.old` ⇒ **`*.csproj.old*`**（原写法漏掉带后缀的 `.old.1~.old.5`）。

## §四 工具链决策（登记 `#546`）

- **Sdk 随编辑器走 4.7.2**；`project.godot` 的 `features` 同步为 `"4.7"`（编辑器已迁移，本件收口）。
- **CI 不改**：`ubuntu-latest` + .NET 8 在线 restore 即可 —— 实测 `godotsharp/4.7.2/lib` 有 **`net8.0`** 目标，
  `godot.net.sdk/4.7.2/Sdk/Sdk.props` 无更高 SDK 要求（本机 .NET 10 SDK 下默认 net8.0 构建 RC=0）。
- **回 4.6.1 的路径**：csproj Sdk 改回 `4.6.1` ＋ `project.godot` features 回 `"4.6"`（4.6.1 包本机缓存已有）。

## §五 边界与未决

- `O-114`（编辑器保存 csproj 时实化 TFM）**仍会复发** —— 守门靠 `tools/check_csproj_tfm.py`（CI 已接）。
- `addons/godot_mcp` 是否搬入 `darkest/addons/` 并入库：**待用户裁定**。
- `LayerTitle` 家族三条实例化报错（`main_menu.tscn` / `fe_flow_skeleton.tscn` / `credits_skeleton.tscn` 的父节点名
  与代码期待不符）本件只取证、未改码。
- 探针（`.tmp_winprobe.ps1` 等）留本地不入库；本件零业务代码改动（只动 csproj／gitignore／project.godot／文档）。
