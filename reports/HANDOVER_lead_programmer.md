# 主程序 · 交接文档（2026-09-21 · 末次刷新）

> 🔴 **给接手人（或下一个会话的我）**：本文只写**接手当天就能用**的东西 ✓
> 配套：`INDEX_lead_programmer.md`（交付索引 · 每项读数+提交号）· `final_verification.md`（终检读数）

## §1 三十秒上手
```
① 仓库：`F:\GithubPro\Darkest`（Godot 根 = `darkest/` = `res://`）
② 构建：`cd darkest; dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0 --no-restore -m:1 -nodeReuse:false -tl:off -v:q`
③ 测试：`dotnet test Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --no-build -nodeReuse:false -tl:off -v:n`
        ⇒ 末次读数 **809/809** ✓（**每次改完必须与这个数一致**，除非你在加用例 ✓）
④ 门禁：`python tools/check_file_size.py` · `check_no_external_assets.py` · `check_data_discipline.py` · `check_godot_refs.py`
⑤ 🎉 **本机能跑 Godot**（这是本会话最大的能力解锁）：
      `$env:DARKEST_GODOT='E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe'`
      `& .\tools\dsh\smoke.ps1`（**13 例** · 含 `e2e` 与 `ui-audit`）
      ⚠️ 本机外层是 **Windows PowerShell 5.1**（没有嵌套 `pwsh` ✗）⇒ 必须**点调用** `& .\tools\dsh\smoke.ps1`
      ⚠️ 全套要跑 **>10 分钟** ⇒ 用**后台任务**跑，别用会超时的前台调用 ✓
```

## §2 我的不变量（**这五条是我没出事的理由**）
```
① **git 一律显式路径**（不用通配符 `*` —— 我曾用它误提了别人的在飞文件 ✗）
② **git 提交一律用 `-F 消息文件`**（`-m` 带引号会被 shell 拆开 —— 我犯过 2 次 ✗）
③ **改前备份、改后复验**：备份在 `F:\GithubPro\Darkest-backup-*` ✓ 大手术前另做**专项快照** ✓
④ **中文句子里不许用 ASCII 引号**（用「」—— 我在 C# 里犯过 4 次，每次 16 个构建错 ✗）
⑤ **数值只在策划已给时改**；没给的**只登记、不动**（解冻四件：登记 + 证据 + 前后读数 + 回冻结 ✓）
```

## §3 本会话学到的**操作陷阱**（都是真金白银换的）
```
🔴 **拆分**：用**精确行号切片**（`$lines[0..n] + $lines[m..]`）✓
   绝不用花括号/缩进推断 ✗（那法**三次写坏文件**）· 绝不对 `Object[]` 用 `List.AddRange` ✗（曾写出 0 行文件）
🔴 **PowerShell**：`Select-Object -First N` 接在 `dotnet test` 后面会**伪造非零退出码** ✗
   `-Include` 必须配 `-Recurse` ✗ · `-replace` 遇正则元字符会崩 ⇒ 用 `.Replace()` ✓
   **贪婪正则改代码 = 危险**（我因此弄坏过 `BuffDefsConfig.cs` ✗）⇒ 用行号区间 ✓
🔴 **.ps1 文件**：**必须保 BOM**（含中文 ✗ 无 BOM 会被 GBK 读 ⇒ 解析崩 —— 我犯过 ✗）
   改完**立刻 `-List` 或 parse 一次** ✓
🔴 **JSON 数据**：**文本编辑**（`python json.dump` 会重排版 ⇒ 打断做字符串手术的用例 ✗）
🔴 **判据要"注释/代码分流"**：只 grep 原文会把**注释里的提及**当成消费点 ✗（我犯过 ✗）
🔴 **验证"我的路径跑了"**：冒烟用例通过 ≠ 我新写的那条路径被执行 ⇒ **必须抓自证行** ✓（我犯过 ✗）
🔴 **别忘了再跑一次冒烟**：改了治疗/防御这类**战斗路径** ⇒ 构建+单测绿**不能**代表运行时绿 ✓
```

## §4 现在**卡在哪**（接手人只需看这三行）
```
① **UI 域三件**：外壳加 `CanvasLayer`（B-2）· hamlet 等屏面板化（B-3 账本会说"还差几屏"）·
   `main_scene`→`ui_root.tscn` + **撤 autoload**（**必须同批做** ✗ 否则两份外壳实例）
   ⇒ 判据已备好一条命令：`--shell-status`（绘层 + 账本 + 实例数 一次看全 ✓ 会自动转绿 ✓）
② **等裁定的数值**：技能 `dmg%` 23 条 · `prot` 对齐（减伤→0）· 4v4 名单/待命位 · `def` 平衡
③ **M3 第 2 步**（带行为 · 8 消费点）⇒ 建议与 ②③ 一并口径确认后**一次做完** ✓
```

## §5 我留下的**可复用工具**（都在 `tools/`）
```
`tools/dsh/smoke.ps1`（13 例一键冒烟 ✓ 新增 `battle-panel` / `battle-panel-nav` ✓）
`tools/dsh/audit_split_integrity.py`（防"拆完丢内容"· **按成员名比** + 双向自检 ✓）
`tools/check_no_external_assets.py`（B6 · 白名单每条带理由+到期条件 ✓）
`tools/check_file_size.py`（≤600 行 · 白名单现 **0 条** ✓）
`tools/dsh/extract_ours_traits.py`（M3 第 1 步 · **从 buff_defs 投影** ⇒ 不手抄 ✓）
`--battle-panel` / `--shell-layer` / `--shell-count` / `--shell-status`（我域旗标 · **默认零行为** ✓）
```

## §6 一句话状态
```
**我域里"不需新裁定"的活基本做尽了**：六件拆分（白名单 0）· 形态 B 我域那一半（1 实现 + 4 判据 + 一键）·
DD1 的 M1a/M1c/M2/M3/M4/M5/M6/M7/M8/M9 全部落到"有读数、有提交号" ✓
剩下的是 **UI 域三件 + 等裁定的数值 + M3 第 2 步** —— 三者都**不在我能单方面推进的范围** ✓
末次总读数：构建 **0 错** · 全量 **809/809** · 门禁 **425 文件 ≤600 / 0 豁免** · B6 OK · 冒烟 **13/13** ✓
```
