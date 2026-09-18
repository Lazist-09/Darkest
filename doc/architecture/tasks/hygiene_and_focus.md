# 任务卡：仓库卫生 × 重心转向（用户 2026-09-21 五条指令）

> **编号**：ARCH-T-HYGIENE-FOCUS-01 · **类型**：task · **状态**：🚧 进行中
> **来源**：**用户 2026-09-21 五条指令**（逐条取证后落本卡；🔴 **两条前提与事实不符，已按证据更正**）
> **硬约束**：`#307` 数值冻结（零数值改动）· 四角分工 · 红线 20 第三条（禁止索引/正则做结构性拼接）

---

## §1 命名空间统一（用户：`Darkest.Ui` 与 `Darkest.UI` 并存 ⇒ 统一 + 静态检查）

### 实测（架构取证 · 2026-09-21）
```
`namespace Darkest.Ui;`（小写 i）⇒ 🔴 **36 个文件**
`namespace Darkest.UI;`（大写 I）⇒ **2 个文件**（`BattleUi.cs` · `IBattleView.cs`）
⇒ ⚠️ **而两者【互相引用】**（`BattleUi.cs` 里写 `Darkest.Ui.BattleTopBarSkeleton` 等 ⇒ 跨命名空间）
```
### 🔴 定案（v1.73 · **用户拍板：统一为 `Darkest.UI`（大写 I）** —— 覆盖架构的成本建议）
```
🔴 **用户 2026-09-21 明确**：「**`Darkest.UI` 用这个**」⇒ ✅ **规范名 = `Darkest.UI`**（大写 I）
   ℹ️ 架构此前提过成本差（`Darkest.Ui` 35~36 文件 vs `Darkest.UI` 2 文件；反向 18×）⇒
      **用户选择付出该成本**（理由可推定：**.NET 命名惯例里两字母缩写应全大写** ⇒ `UI`）⇒ **按用户定** ✓
⇒ ⚠️ **架构已把"证据建议（`Darkest.Ui`）"撤下**，只留记录（不静默改，供后人理解为何动 33 文件）✓
```
### 🔴 改动面（架构实测 · 供执行者照做）
```
🔴 **A. 需要改 `namespace` 声明：33 个文件**（全部在 `darkest/scripts/ui/**`）
   `BattleBottomBarSkeleton` `BattleMiniMap` `BattleTopBarSkeleton` `BuildingNavButtonTemplate` `BuildingPopupSkeleton`
   `CampSkillPanel` `CurioPanel` `DdTheme` `ExpeditionListPanel` `FrameBudget` `HamletRoot` `HamletSkeleton` `HeroArt`
   `InventoryPanel` `LayoutAudit` `LightBarPanel` `LongTextProbe` `MainMenuRoot` `MainMenuSkeleton` `MfTabButtonTemplate`
   `PathChoicePanel` `PopupLineTemplate` `RosterRowTemplate` `ScoutMarkPanel` `SkillBoxTemplate` `SlotRowTemplate`
   `UiAuditHook` `UiMotion` `UiPalette` `UiSfx` `UnitCardTemplate` `WalkMapSkeleton` `WalkMapView`
🔴 **B. 需要改限定引用 `Darkest.Ui.` ⇒ `Darkest.UI.`：159 行**（分布：**`BattleUi.cs` 最多 ~100 行** ·
   `HamletRoot.cs` · `WalkMapView.cs` · `BattleMiniMap.cs` · `MainMenuRoot.cs` · `ScoutMarkPanel.cs` ·
   `LightBarPanel.cs` · `WalkMapSkeleton.cs` · **`gameplay/scene/DungeonRunDriver.cs`** · **`gameplay/scene/SmokeScript.cs`**）
   ⚠️ **同命名空间内的限定引用也必须改**（例：`BattleUi.cs` 里 `Darkest.Ui.DdTheme.X` ⇒ `Darkest.UI.DdTheme.X`），
      因为 **`Darkest.Ui` 将不再存在** ⇒ 不改就编译失败 ✓
🔴 **C. `using Darkest.Ui;` ⇒ `using Darkest.UI;`：1 行**（`DungeonRunDriver.cs:5`）
✅ **D. 不变**：`BattleUi.cs` / `IBattleView.cs`（已是 `Darkest.UI`）
✅ **E. 不受影响**：**`.tscn` 里 0 处 namespace 引用**（场景按 `path=` 引脚本）⇒ **但必须用冒烟证明没坏** ✓
```
### 🔴 执行（**跨两域 ⇒ 必须分工，禁止同文件并行编辑**）
| 谁 | 文件范围 | 内容 |
|---|---|---|
| **UI 设计师** | `darkest/scripts/ui/**` | A（33 个声明）+ B/C 中落在 `scripts/ui/**` 的部分 |
| **主程序** | `darkest/scripts/gameplay/scene/**` | `DungeonRunDriver.cs`（`using` + 2 处限定）· `SmokeScript.cs`（5 处限定） |
### 🔴 验证（缺一不可 · 归各自执行者）
```
① 构建 **0 错误** ② 全量测试绿（**当前 563/563**）③ 🔴 **`pwsh tools/dsh/smoke.ps1`：7 例非环境 ERROR = 0**
④ 终检：`git grep -c 'Darkest\.Ui' -- darkest` ⇒ **必须 0**（含注释；注释里的旧名也要清，避免误导）
⚠️ **手法纪律（红线 20 第三条）**：**这是"受认可的重命名"，不是"结构性拼接"** ——
   允许**精确 token 替换**（`Darkest.Ui.` → `Darkest.UI.`、`namespace Darkest.Ui;` → `namespace Darkest.UI;`），
   🔴 **但必须以【构建 + 测试 + 冒烟】三者通过为收口**（不许"改完就算"）；
   ⚠️ **禁止在 `.md` 文档上跑同一替换**（文档里的历史引用要保留/划线，不是替换）
```
### 🆕 门禁（**方向已随定案翻转** · 规格）
```
🔴 `tools/check_namespace_consistency.py`（落点 = `tools/check_godot_refs.py` 家族 · **禁用型**）
   判据 ①：`git grep -c 'namespace Darkest\.Ui;' -- darkest/scripts` ⇒ **必须 0**
   判据 ②：限定引用 `Darkest.Ui.` ⇒ **必须 0**
   ⇒ 非零 ⇒ **退出码 1**（**"这个拼写不存在"比"记得别用"强**，R3）✓
```

---

## §2 🔴 "两套 UI 构造策略"——**取证结论：不是两条路径**（用户前提需更正）

### 实测（架构取证）
```
✅ **6 个 Skeleton 全部【有调用点】**（不是"未接线"）：
   `BattleTopBarSkeleton` → `BattleUi.cs:1148`（`TryInstantiate`）+ 字段 `:1563`
   `BattleBottomBarSkeleton` → `BattleUi.cs:1179` + `:1564`
   `HamletSkeleton` → `HamletRoot.cs:97` · `MainMenuSkeleton` → `MainMenuRoot.cs:61`
   `BuildingPopupSkeleton` → `HamletRoot.cs:1112` · `WalkMapSkeleton` → `WalkMapView.cs:83`（**8 处**）
✅ **7 个模板场景全部各 1 个调用点**（`slot_row` / `unit_card` / `skill_box` / `roster_row` / `popup_line` / `mf_tab` / `building_nav_button`）
   —— 即 **UI 编辑器化 B（用户 2026-09-17 要求的方向）已经在用** ✓
```
### 🔴 真正的问题 = **过期注释**（这正是用户担心的"误导后人"）
```
`BattleTopBarSkeleton.cs:17` / `BattleBottomBarSkeleton.cs:20` / `BuildingPopupSkeleton.cs:19` /
`HamletSkeleton.cs:20` / `MainMenuSkeleton.cs:14` / `WalkMapSkeleton.cs:18` 里都写着：
   「⚠️ **接线状态：未接线**（…仍在代码里建这些容器；接线单独一轮做）」
🔴 **而实测它们已被 `TryInstantiate()` 接线**（见上）⇒ ⚠️ **注释在骗人**（红线 21 家族：不留死声明/误导文本）
```
### 架构裁定
```
🔴 **不是"两条 UI 路径"，是【一条路径的两层】**：
   · **骨架/模板** = `.tscn`（编辑器可改）· **数据填充与动态项** = C#（`Build()` / `Refresh()`）
   ⇒ ✅ **判据**：**结构布局来自 `.tscn`；内容/数据来自代码**（谁在哪层，注释必须写清）
🔴 **必须做的三件**（属 `scripts/ui/**` = UI 设计师域）：
   ① **清掉"未接线"过期注释** ⇒ 改成**如实状态**（已接线 / 部分接线：**哪一部分仍由代码建**）
   ② 🔴 **禁止再用"未接线/已接线"这种模糊二元** —— 改为**写明【当前哪一层来自 .tscn】**（红线 19 措辞纪律）
   ③ 若确实存在"代码与骨架都建同一容器"⇒ **那才是真问题**（重复构造）⇒ 按"一处构造"原则收口
⇒ 📌 **提请策划**把这条**单一路径口径**写进 `ui_spec`（权威），避免下一个人再问"到底哪条在用"
```

---

## §3 一键冒烟入口（用户：CLI 旗标只有写的人知道 ⇒ 收进 `tools/dsh` / 接 CI）

### 实测
```
`tools/dsh/` 原本只有 **`watch_inbox.ps1`**（我自己的窗口监视脚本）⇒ ⚠️ **用户以为的"一键冒烟"用途并不存在**
```
### ✅ 架构交付：🆕 `tools/dsh/smoke.ps1`（**已由主程序修到真能跑 · 10 用例**）
```
用法：`pwsh tools/dsh/smoke.ps1` · `-Case <name>`（单例）· `-List`（只打印用例表）
判据：每例打印「**真错误** / **引擎退出泄漏**」两列 + 行数 + exit；🔴 **只有【真错误】>0 才判红**；
   🔴 **行数 = 0 也判红**（**空输出 ≠ 无错误** —— 我原版在这里**假绿**过）✓
留档：`reports/smoke_summary_<stamp>.txt` + 每例 `reports/smoke_<case>_<stamp>.txt`
🔴 **CLI 用例 = 契约的一部分（10 例）**：
   `entry-main1` · `topology-auto` · `hamlet-next` · `e2e` · `map-mode` · `town-step` · `ui-audit`
   ＋ 🆕 `dungeon-in-scene` · `tile-walk` · `abandon`
🔴 **Godot 解析四级回退**：`$env:DARKEST_GODOT` → `PATH` → 常见安装位 → 递归找目录
   ⇒ 📌 **CI 里请设 `DARKEST_GODOT`**（最稳）✓

### 🔴 CI 配方（v1.77 · 主程序定 · **架构裁定采纳**）
```
① **确保没有别的 Godot 在跑** —— `smoke.ps1` 现在**拒绝并发**：已有实例 ⇒ **不启动 + 打印原因 + exit 2**
   ⇒ 📌 **exit 2 = "拒绝争用"，不是失败**（⚠️ 主程序实测：反复 Force-kill + 实例重叠**可能诱发原生崩溃**）
② `powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke.ps1`
③ 🔴 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/smoke_gate.ps1`   ← **CI 只看这一步**
🔴 **为什么不看 `smoke.ps1` 的退出码**：它**不可靠**（实测"绿色用例也退 1"）——
   真相是**主程序收尾段括号没配平 ⇒ 脚本跑不起来**（已修），但**判据不该依赖进程退出码**：
   ✅ **改用【机器可读判决行】**：`smoke.ps1` 收尾写 `RESULT: OK` ／ `RESULT: BAD n=<坏例数>` 进摘要，
      `smoke_gate.ps1`（**纯 ASCII**）读它 ⇒ `OK` 退 0；`BAD`/缺行/无摘要 退 1 ✓
   ⇒ 🎖️ **这正是 R3**：**把"不可靠的退出码"换成"可读的判决行"**（"让错误做法跑不通"）✓
   ⚠️ **对旧档不假装通过**：旧摘要无 `RESULT` 行 ⇒ 明确报"缺判决行，请重跑" + 退 1 ✓
🔴 **并已修**：`smoke.ps1` 的 **try/finally 回收自己起的 Godot 子进程**（中途失败也回收）·
   `SmokeScript` 的"放行提示"**每 0.2 秒刷一次（20+ 行）⇒ 改为只提示一次** ✓
```

### 🔴 门禁三态（v1.77 · 架构修到 v3 · 教训入 README 红线 20 ⑥）
```
`tools/dsh/check_ui_namespace.ps1`：**v1**（无 BOM + 不区分大小写 ⇒ 解析失败/假阳性）⇒
**v2**（ASCII 化，但**模式带回尾点** ⇒ **漏掉反引号裸名 ⇒ 假绿 exit 0**）⇒
🔴 **v3（架构修）**：**模式去尾点 + `-CaseSensitive` + `Get-ChildItem -Recurse`** ⇒ ✅ **如实抓到 2 处**
⇒ 📌 **判据：改判据/门禁后，先跑【注入 ⇒ 必须红】再跑【干净 ⇒ 必须绿】**；
   ⚠️ **"模式收紧"最容易在修订中悄悄丢失**（v2 = "改好了语言、改丢了判据"）✓
```
⚠️ **四处修复（**架构交的原版有 2 个真 bug，由主程序修**）**：
   ① **`--path .` ⇒ 必须显式给项目路径**（从仓库根跑会指错 ⇒ **0 行输出 ⇒ 我原版假绿**）❌
   ② **空日志（行数=0）判红**（同 ①，二者互为因果）❌
   ③ **mono 版 Godot 用 `& exe *> log` 抓不到输出** ⇒ 改 `cmd /c "… > log 2>&1"` ✓
   ④ **Godot 四级回退解析**（原版只找 PATH ⇒ 直接抛错）✓
⚠️ **实现坑（两条，已记红线 20 第六条）**：**含中文的 `.ps1` 必须带 BOM**（PS 5.1 按 ANSI 读 ⇒ 语法断）；
   **`Select-String` 在 PS 5.1 没有 `-Recurse`** ⇒ 参数报错非终止 ⇒ **会假绿** ✓

---

## §4 🔴 `reports/` 的"待判定 ERROR"清零（用户：要么修、要么标 N/A）

### 实测
```
`reports/p4final_summary_20260915_2343.txt` ⇒ **全 0** ✅（片 4 收尾那批是干净的）
`reports/p4final_summary_20260915_2311.txt` ⇒ entry-main1 **非环境ERROR=4**（旧留档）
🔴 `reports/regress_after_step_fix_20260916_1858.txt` ⇒ entry-main1=1 · topology-auto=1 · hamlet-next=1 ·
   **e2e=2** · map-mode=1 · ui-audit=1 · town-step=1 ⇒ ⚠️ **7 例里 7 例带 ERROR**（不是 1~2 个）
⇒ ⚠️ **那两份 summary 只有计数、【没有 ERROR 明细】**（原始日志已被清理/未入库）
   ⇒ 🔴 **所以无法"就地判定"** ⇒ 必须**重跑取明细**（用 §3 的新脚本）
```
### 架构裁定（三分类，一次清完）
```
🔴 **每条非环境 ERROR 必须落三分类之一，禁止"待判定"长期挂账**：
   ① **修**（真缺陷）⇒ 开 `O-nn` + 指派
   ② **标 N/A + 理由**（环境/已知噪声）⇒ **必须写进脚本的白名单并注明理由**（不许"心里知道"）
   ③ **归档**（历史留档、已被更新批覆盖）⇒ 在报告里标 `[归档]`
⇒ ✅ **执行**：主程序跑 `tools/dsh/smoke.ps1` 取**两份**报告（当前 HEAD 一份；如需要再复现旧批）
   ⇒ 每条 ERROR 出明细 ⇒ 三分类 ⇒ **清零后再入 CI（否则 CI 会长期红）**
📌 **判据**：`reports/` 里**不允许存在"非环境 ERROR 非零且未分类"的报告** ⇒
   建议在 §3 脚本里加 `-Audit` 模式：**扫描已有 summary、列出未分类项**（待加）
```

---

## §5 重心转向：**Hamlet / 养成闭环**（用户：战斗能跑了，但"养成→再出发"才是游戏本身）

```
🔴 **战略口径（架构采纳并落到路线）**：
   · **战斗内核已达可用**（556/556 · 内核纯净 · A1 判据齐）⇒ **不再是当前瓶颈**
   · 🎯 **当前瓶颈 = 养成闭环**：城池（名册/建筑/经济）→ 出发 → 回城 → **花钱有意义** → 再出发
   ⇒ 📌 **与既有 M8 路线一致**（`tasks/m8_roadmap.md`：经济闭环 / 减压出口 / 招募 / 个体化 / 端到端可玩）
     ⇒ 🔴 **建议：把 M8 从"计划"提为【当前主线】**，M7.6 剩余项（片 3.1 / 事件实现）作为**并行小件**
✅ **用户对英雄资产的评价我确认**：`HeroAssets` 的三条纪律（**显式标注来源 / 不发布 / 只从 PlaceholderRoot 读**）**做得对** ✓
   ⇒ ⚠️ **并登记一条发布前 gate**：**`borrow/`（856 文件）授权核对** —— 发布构建必须证明**不含**借用素材
     （`git ls-files | grep -i borrow` = 空 + 发行包无占位目录；已在 `O-90`/`P31` 有同类判据，此处**明确为发布前必检**）✓
```

---

## §6 分工与交付状态

| # | 事项 | 归属 | 状态 |
|---|---|---|---|
| 1 | 命名空间统一（**方向待用户拍板**） | UI 设计师（`scripts/ui/**`）+ 架构门禁 | ⏸ 待拍板 |
| 1b | 🆕 `tools/check_namespace_consistency.py` 门禁 | 架构出规格 · 主程序落 | ⬜ |
| 2 | 清"未接线"过期注释 + 写清单一构造口径 | **UI 设计师** | ⬜ |
| 2b | `ui_spec` 写单一路径口径 | 策划 | ⬜ |
| 3 | ✅ `tools/dsh/smoke.ps1` 一键冒烟 | 架构（已交）+ 主程序接 CI | ✅ / ⬜ |
| 4 | reports 的 ERROR 三分类清零 | **主程序**（跑脚本取明细）· 架构登记 | ⬜ |
| 5 | 重心转 Hamlet/养成闭环 | 策划（排期）+ 主程序/UI（实现） | ⬜ |
| 5b | `borrow/` 授权核对（**发布前 gate**） | 策划（授权口径）+ 架构（判据） | ⬜ |
