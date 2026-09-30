# SHA 重写映射表 · 20260930（推送收口）

本表由一次性脚本按 `git log --reverse` 顺序逐条配对生成；配对依据 = 提交顺序 + subject 逐字比对（0 处错位），并用树哈希逐条复核内容零丢失。

用途：本地 master 已用 `git rebase --onto c53ff45 b319916 master` 重写到远端 c53ff45 之上，形成一条全新 SHA 链。历史文档中约 138 处旧 SHA 引用不修改（项目惯例：以本表为准，历史文本只作更正记录）。

## §1 推送前四条不变式（实测读数）

| # | 不变式 | 命令 | 读数 | 判定 |
|---|---|---|---|---|
| 1 | 内容零丢失（安全绳） | `git rev-parse archive/pre-push-20260930^{tree}` / `git rev-parse master^{tree}` | `414af2d5c53236ef248155a78402aa953de736bf` 两侧逐字相同 | PASS |
| 2 | 重放条数 | `git rev-list --count c53ff45..bfbeddb` | 849（= 838 + 11；本表提交自身另计 1，成链后读数 850） | PASS |
| 3 | 可 fast-forward | `git merge-base --is-ancestor c53ff45 master` | rc=0 | PASS |
| 4 | 无 100 MB 级对象 | `git rev-list --disk-usage --objects c53ff45..bfbeddb` | 20,507,221 B（约 19.6 MiB） | PASS |

补充读数：

- merge-base(archive/pre-push-20260930, c53ff45) = `41d287e3549095404c1b1ac8384559a770efcea9`
- 旧链安全绳 tip = `5cd994610c936a7a1300aa3c908ee8085c594a90`；新链 tip = `bfbeddba60b4db74e29dbcefd5c797c064929321`
- `git rev-list --count b319916..archive/pre-push-20260930` = 849（与新链 849 条逐条配对）
- 孪生段：`git rev-list --count 41d287e..b319916` = 230；`git rev-list --count 41d287e..c53ff45` = 230
- tree(b319916) = `560989914c155eb6fe53867dd4dac7fcde69c6dc`；tree(c53ff45) = `560989914c155eb6fe53867dd4dac7fcde69c6dc`（两链端点树逐字相同）
- merge 提交数：新范围 0；旧范围 0
- 树哈希逐条比对：重放段 849 对全部相同（不同 0 处）；孪生段 230 对中不同 4 处（见 §4）
- 对象体积：`41d287e..archive/pre-push-20260930` = 27,199,850 B；其中孪生段 `41d287e..b319916` = 6,703,838 B；新范围 = 20,507,221 B
- 远端链长（推送前）：`git rev-list --count c53ff45` = 854；重放 849 条 + 本表提交自身 1 条 ⇒ 推送后 `git rev-list --count origin/master` = 1704

## §2 104 MB 大文件：只留本地，不入远端

- 路径：`reports/p4final_topology-auto_20260915_2311.txt`
- blob：`f2018a2f7e68f9278f1eed403376086ad9f3b22e`，109,013,211 B（104 MB）
- 承载提交（均位于本地独有孪生段 `41d287e..b319916` 内）：

| 孪生段序 | 旧 SHA | 树内 | 远端孪生 SHA | subject |
|---|---|---|---|---|
| 1 | `b145d2f38e8ec96bf8cf26b05991c65bf979c68f` | 有该 blob | `7ea7fcb80` | 片 4 收尾·全量冒烟留档：7 例（entry-main1/topology-auto/hamlet-next/e2e/map-mode/click-menu0/ui-audit）非环境 ERROR 全为 0 |
| 2 | `0776c151336669592604a51466ac5e59f588d3b3` | 有该 blob | `6df3f24f3` | 投架构：片 4 收尾证据包(V1=0 + 7 例冒烟全绿 + 提交号) + O-nn 记账清单 + 英雄资产加载器认领 |
| 3 | `882c2de6d68a1153de2013aa389ad5152598b514` | 有该 blob | `db1d73ad9` | 投 UI：走格接口就绪(瓷砖网格读数/驱动/三条边界) —— 格子主画面可开工 |
| 4 | `d95d7ee4484a85392097a76374c81942a0869097` | 有该 blob | `7a3b52226` | 主程序 (A) 收尾：格子视图顶部加信息行【当前房间类型 / 剩余段数 / 已揭示 x/y】（提示不再只活在 log 里）；10 入口复测全绿 |
| 5 | `6946a2e45714e23a1e5fdeeb391549d8fcd90311` | 无（此提交删除） | `bac569753` | 处置 O-90：reports/ 只入库摘要与文档（忽略 *.txt/*.log，保留 *_summary_*.txt）+ git rm --cached 掉 4 个超大白跑冒烟日志（104.7MB/8.7MB/6.6MB/0.6MB）—— 与上次 2.8GB 事故同族，我的 git add reports/p4final_* 扫进去的 |

- 该处置提交删除的 4 个超大日志：

    - `reports/p4final_entry-main1_20260915_2301.txt`
    - `reports/p4final_entry-main1_20260915_2311.txt`
    - `reports/p4final_topology-auto_20260915_2301.txt`
    - `reports/p4final_topology-auto_20260915_2311.txt`

- 校验：新范围内无任何提交触及该路径 ⇒ `git log --name-only c53ff45..master -- reports/p4final_topology-auto_20260915_2311.txt` = 0 行
- 校验：该路径在 `c53ff45` 上不存在；在 `b319916` 上不存在
- 结论：104 MB blob 只存在于本地安全绳分支 `archive/pre-push-20260930` 可达的历史；推送单 ref `git push origin master` 不会把它带到远端。

## §3 重放段：旧 → 新（849 条，按提交顺序一一对应）

| # | 旧 SHA | 新 SHA | subject |
|---|---|---|---|
| 1 | `d5cc63980` | `83618ecbf` | skill 加红线：所有程序文件 <=600 行 + 极端可读性/可重构性（用户 2026-09-18 指令）+ 现状台账与拆分方法 |
| 2 | `0d88c76ef` | `c1105e5fd` | 投三角:新红线(程序文件<=600 行 + 极端可读性/可重构性)+实测 10 处超限台账+拆分方法 |
| 3 | `6fff479c4` | `8839b17cd` | 拆文件 ①/8（红线 <=600 行）：M75VerificationPackTests 628 -> 494（+ 新文件 153） |
| 4 | `b26c14aec` | `ca0fca69c` | skill 台账更新：M75VerificationPackTests 已拆（628 到 494，新文件 153） |
| 5 | `fb5edbf27` | `02e5d73a3` | 拆文件 ②/8：BoardTests 702 -> 552（+ 新文件夹具 165） |
| 6 | `9da0b0c37` | `158179828` | 修红：拆 BoardTests 时漏掉 [TestClass] => MSTest 不再发现它 => 静默丢 25 个用例（583->558）=> 已修并复核 |
| 7 | `74a9b03c7` | `80e8a10fc` | skill 台账：BoardTests 已拆（702 到 553 + 新文件 165） |
| 8 | `3a43813cd` | `a8aa73f1b` | 拆文件 ③/8：M76TopologyProbeTests 1176 -> 463（+ 新文件 434 / 313） |
| 9 | `10dd71829` | `a0067d9ad` | skill 台账：M76TopologyProbeTests 已拆（1176 到 463 + 434 + 313） |
| 10 | `a4863468d` | `9d2fa5e61` | 拆文件 ④/8（数据面）：TuningConfig 760 -> 559（+ 新文件 214）—— 最干净的一次拆法 |
| 11 | `29427fade` | `834e828eb` | skill 台账：TuningConfig 已拆（760 到 559 + 新文件 214） |
| 12 | `0cc034b99` | `c8ccfd7d8` | 拆文件 ⑤/8（内核面·第 1 个）：BattleDirector 832 -> 447（+ 新文件 28 / 399） |
| 13 | `89f807d89` | `02461285e` | skill 台账补回内核行(我把它整行弄丢了,如实标注):按实测重写,含 BattleDirector/TuningConfig 已拆标记 |
| 14 | `481fca395` | `5beec0268` | 拆文件 ⑥/8（内核/场景面·第 2 个）：BattleRoot 889 -> 562（+ 新文件 346） |
| 15 | `99867a14f` | `e3ee53dbb` | skill 台账：BattleRoot 已拆（889 到 562 + 新文件 346） |
| 16 | `478074210` | `0d27ec8c9` | 拆文件 ⑦/8（内核面·第 3 个）：ExpeditionSession 915 -> 413（+ 新文件 517） |
| 17 | `a854513d9` | `bb93d76e8` | skill 台账：ExpeditionSession 已拆（915 到 413 + 新文件 517） |
| 18 | `fd0c62097` | `d466e0425` | 拆文件 ⑧/8（最后一个）：ExpeditionFlow 937 -> 579（+ 新文件 372）=> **我域清零** |
| 19 | `0e4367e0d` | `e9bcb5b34` | skill 台账：ExpeditionFlow 已拆（937 到 579 + 372）=> 主程序域 8 处全部拆完 |
| 20 | `01c7fa18a` | `c008aac20` | 投 UI 催办:我域 8 处已全拆完(含前/后读数+提交号),附拆分三件套与建议顺序 |
| 21 | `bd6f092c5` | `54602c28f` | 架构: 红线28(程序文件<=600行)+门禁 check_file_size.py(三向自检)+拆分评审卡; buff字段裁定落地(P30/P31/O-94); data_schema 字段四分类; 窗口回执 |
| 22 | `33549f3b3` | `2eea5faaa` | 架构: 投递中转件不落工作区(两次实例)+skill 补该纪律; PM v1.86 (红线28/门禁/拆分评审) |
| 23 | `0c5ca6db7` | `3afbe53ff` | 架构: 补清收件箱(上轮漏最后一步)+skill 立'投递+清箱成原子段' |
| 24 | `3227c1113` | `e7ebb7d6d` | skill §14.0.28：新红线 ≤600 行的 UI 域拆分施工计划（实测台账 2715/1663 + 按职责/相位的实测切点 + 头部四行含'依赖私有成员' + 主程序 5 个坑 + 验收=构建绿/用例总数不变/smoke 真错0/--ui-audit 复测） |
| 25 | `6e5a6573d` | `e9c7e7f36` | 红线≤600 第一刀【只搬家】：HamletRoot.cs 1663 → 1401 行 + 新 HamletRoot.HeroDetail.cs 282 行（从 L663-924 切出角色详情族：OpenHeroDetail/CloseHeroDetail 及详情面板构建与刷新） 证据链：① 构建 0 错误 ② smoke.ps1 **10 例 exit=0**（此前 10 例全部 exit=-1073741819 段错误 ⇒ 根因是 smoke 未设 APPDATA 导致 user://logs 打不开，我以'设 APPDATA'证明修法；smoke 内 '真错误=1' 经查是 ERROR: Failed to read the root certificate store = 本机环境噪声，smoke 的过滤少这一类）③ tools/dsh/ui_sweep.ps1 **14 入口全绿**（0 需求超出/0 重叠/0 透明/0 真错，覆盖架构要求的'搬家后父矩形/锚点'风险）④ 命名门 check_ui_namespace.ps1 OK（新文件 namespace Darkest.UI 大写 UI ✓） ⚠️ 未能取得：dotnet test 用例总数 —— 本机测试宿主环境失败（ProcessManager.OpenProcess 打不开自身进程句柄 ⇒ '测试运行已中止'），非本刀所致；如需'用例数不变'证明请在测试宿主可用的机器上跑 dotnet test darkest/Darkest.Tests.csproj |
| 26 | `52a70c4a6` | `8da974040` | 红线≤600 第二刀【只搬家】：HamletRoot.cs 1401 → 1211 行 + 新 HamletRoot.BuildingPopup.cs（切 L846-1035 建筑弹窗族：OpenBuildingPopup/RefreshBuildingPopup/PopupLine/ShowBuildingInfo，190 行） 证据：构建 0 错误 ｜ ui_sweep 四入口（hamlet/building/menu/hero-detail）全绿 ｜ 命名门 OK（新文件 namespace Darkest.UI 大写 UI） |
| 27 | `6e9994011` | `364fe7b9c` | 纪律 T（用户 2026-09-21 直接指令）：把「尽量合并和减少测试，只做必要测试」写入 skill —— SKILL.md 入口（T1 三问 / T2 各类改动口径 / T3 冗余用例处理）+ 工作手册 §14.0.29；UI 侧即刻生效：搬家类改动不写测试（用构建+ui_sweep+命名门当证明），新增前先合并，测试宿主不可用不阻塞也不谎报 |
| 28 | `07c72cf8d` | `ec7f828af` | 红线≤600 第三·四刀【只搬家】：HamletRoot.cs 1211 → 986 行；新增 HamletRoot.Progression.cs（养成动作族：减压/招募/选人/升级）+ HamletRoot.RosterServices.cs（名册交互与服务族：再出发/名册行/读数/右键头像/服务疗养） 证据（纪律 T：搬家类不新增测试）：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门待跑 |
| 29 | `d50bd84bf` | `690bd70f9` | 红线≤600 第五刀【只搬家】：HamletRoot.cs → 785 行 + 新 HamletRoot.Refresh.cs（刷新族：Refresh() 只读跨趟状态重画，不自己算账） 证据（纪律 T：搬家类不新增测试）：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK（namespace Darkest.UI 大写 UI） |
| 30 | `eccbed601` | `bff842b25` | 红线≤600 第六刀【只搬家】：HamletRoot.cs → 624 行（**达成 ≤600 ✓**）+ 新 HamletRoot.PopupMenu.cs（弹窗工厂与菜单族：MakePopup/Esc/CloseTopPopup/OpenHamletMenu） 证据：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK（namespace Darkest.UI 大写 UI） |
| 31 | `0e74a04a2` | `b7e02d167` | 红线≤600 第七刀【只搬家】：HamletRoot.cs → 151 行（**≤600 达成 ✓**）+ 新 HamletRoot.Build.cs（建树族：_Ready 骨架优先/回落搭出整屏） 证据：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK |
| 32 | `57c540b24` | `d0339aea6` | 红线≤600 BattleUi 第一刀【只搬家】：BattleUi.cs 2715 → 2436 行 + 新 BattleUi.MultiFunction.cs（E 区多功能框族：建三页/切页/内容刷新） 证据（纪律 T：搬家类不新增测试）：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK（namespace Darkest.UI 大写 UI） |
| 33 | `1919cd405` | `dc100d738` | 命名统一（用户指令）：类名与文件名 BattleUi → **BattleUI**（我域 7 文件 + 【越域 5 文件】BattleRoot.cs/BattleRoot.PlayerActions.cs/SmokeScript.cs/ExpeditionComposition.cs/Battle.tscn(脚本路径)） 证据：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK ｜ 旧写法残留 = 0 |
| 34 | `2364ba481` | `badf94631` | 红线≤600 BattleUI 第二刀【只搬家】：BattleUI.cs 2436 → 2044 行 + 新 BattleUI.Build.cs（建树族：Build/BuildTopRow/BuildBattlefield/BuildBottomRow，骨架优先+回落） 证据（纪律 T：搬家类不新增测试）：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK |
| 35 | `b858080ba` | `8fa98ca8e` | 红线≤600 BattleUI 第三刀【只搬家】：BattleUI.cs → 1582 行 + 新 BattleUI.Dungeon.cs（地牢宿主与地图相位族：DungeonHost/HostDungeon*/*Abandon*/HostDungeonPanels/EnterMapMode/ExitMapMode） 证据：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK |
| 36 | `0f32dfe1d` | `ae366b7a5` | 红线≤600 BattleUI 第四刀【只搬家】：BattleUI.cs → 1416 行 + 新 BattleUI.Modals.cs（模态与单位详情族：TitleLabel/MakeOpaqueModal/ShowUnitDetail/PressCard/ToggleDevLog） 证据：构建 0 错误 ｜ ui_sweep 14 入口全绿 |
| 37 | `b458efbf9` | `65343cc23` | 红线≤600 BattleUI 第五·六刀【只搬家】：BattleUI.cs → 883 行 + BattleUI.Motion.cs（动效与审计族）+ BattleUI.Refresh.cs（主刷新族 Refresh/FlashHint） 证据：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK |
| 38 | `9a1fe04d7` | `65c90cf13` | 红线≤600 BattleUI 第七·八·九刀【只搬家】：BattleUI.cs → 506 行 + BattleUI.Cards.cs（卡牌建树/敌方意图）+ BattleUI.Render.cs（顺序条/卡牌填数/技能栏）+ BattleUI.Data.cs（技能池与名字/数据读取） 证据：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK |
| 39 | `24131edcb` | `c6c2bd9fb` | 红线≤600 收口：回执主程序窗口 DELIVERY-UI-SPLIT-DONE-20260921（前后读数/16 刀清单/越域 5 文件备案/dotnet test 未取得明说/APPDATA 崩溃结论）+ skill §14.0.30（含 4 个踩坑）+ 清空我的收件窗口 |
| 40 | `547c2090b` | `b0bc522a2` | skill §14.0.31：架构诊断（DD 同构差距）逐条代码核实 —— 城池中央建筑区/大红 Embark/底部资源条与角色详情入口【已在】（前提过时，真缺口是可发现性与排布打磨），Node2D 异类【准确】，战斗右下常显地图【待确认】+ 四轨计划（Track 4→2→3→1） |
| 41 | `2f70ece2a` | `0ca264c7b` | 执行策划 #409 裁 (A)：删 dispellable（字段 + 数据 21 键 + 那条同义反复断言整条删） |
| 42 | `42c04ffa2` | `b6803b076` | Track 4(b)【角色详情可见入口】：名册行尾加 › 按钮（Name=RosterDetail_<id>，点击 ⇒ OpenHeroDetail），保留右键头像入口 ⇒ 消灭'孤岛/不可发现'（红线 18） 证据：构建 0 错误 ｜ ui_sweep 14 入口全绿 ｜ 命名门 OK |
| 43 | `7edf3184e` | `8a4f78689` | 门禁接入自检（第 8 项）+ 修一处【路径级白名单遇搬家失效】 |
| 44 | `4bc2de7a8` | `f62d3e951` | Track 2 第一步【统一面板范式】：HamletRoot 8 个 partial 由 **Node2D → Control** + 场景根 darkest/scenes/hamlet/Hamlet.tscn 根节点 Node2D→Control 并设**满屏锚点**（否则 Control 根尺寸 0 ⇒ 子节点塌陷） 依据（开工前取证）：① HamletRoot 自身**无任何手写 Position/Size**、② 域外仅做**类型检查**（SmokeScript ode is HamletRoot，无 Node2D 转型） 证据：构建 0 错误 ｜ ui_sweep **14 入口全绿且读数与改造前完全一致**（hamlet 62/hero-detail 63/战斗 1041…）⇒ 行为未变 ｜ 命名门 OK ⇒ 现在每个屏都是 Control 骨架+控制器范式，**可被 Track 1 的 UIRoot 外壳装载** ✓ |
| 45 | `30cf03c36` | `c3a3d3a4e` | 架构补一:每个 part 头部加【依赖主类私有成员】行(自动统计,不靠记忆;测试 partial 按 private static 助手统计) |
| 46 | `fe7c5833f` | `b8bae2d65` | 回执架构:补一(依赖行,自动统计)/补二(可核对声明+如实标边界)/补三(门禁接入自检8/8)+新规律(路径级白名单遇搬家失效) |
| 47 | `deb7a4395` | `0c77cdf5f` | Track 2 第二步（部分）：ui_sweep.ps1 显式并入 §14.5 判据列 spec14.5（=相机口径内 + Label 两两不相交 + Panel/PanelContainer 不透明；由既有 LayoutAudit 读数聚合）并在脚本口径注释里**如实标注**第三项（禁手写 Position/Size）尚未做成硬门（会误伤手绘地图）⇒ 待定基线 证据：PS 5.1 实跑三入口全绿、退出码 0 |
| 48 | `0533e9b2f` | `c27c53003` | 更正+报障:我上封把冒烟真错误写成0(实测1),附改动前后时间线定位为该报错为20:22后环境级新问题(证书库),请复核/裁口径 |
| 49 | `1b9de1931` | `5d48827f6` | 架构: 红线29(白名单键设计:片段必填+路径可选通配)+file_size门禁改通配键并验证搬家免疫 |
| 50 | `35572fab6` | `b3c615453` | Track 4(a) 取证 + 增量：① 实测确认【建筑悬停信息】已在工作（名称/功能/当前等级/下一级所需，见 --hamlet-hover=tavern 留痕）② 把该入口纳入一键审计（第 15 个入口 hamlet-hover）当正向留痕 ③ 🔴 冲突报告：'中央建筑区三栋卡片'与用户 2026-09-16 裁定'只保留一个入口按钮（DD 式）'+红线 21（锁定建筑不显示、起手只解锁 1/3）冲突 ⇒ 不擅自改，待裁定 |
| 51 | `3bf55f56b` | `69e750820` | 架构: 清空 file_size 白名单(UI 已拆完,全部<=600;临时豁免兑现) |
| 52 | `7412b2e93` | `f5c2106d2` | 架构: 红线28达成(UI拆完/白名单清空)+三件卫生提醒(.uid随part提交/大小写改名/测试产物gitignore) |
| 53 | `e17f0368d` | `90e4393ff` | Track 3 第一步【Overlay 层骨架·就位待接线】：scenes/ui/overlay_layer.tscn（ModalHost/TooltipHost 两层，mouse_filter=Ignore 不吃鼠标）+ OverlayLayer.cs（[Tool]+成功留痕；OpenModal/CloseTopModal 栈 / ShowTooltip/HideTooltip / Esc 关栈顶） 命名：namespace Darkest.UI（大写 UI）✓ 未接线 ⇒ 判据与之前逐项一致 ✓ 待接线（下一步）：把 BattleUI.Modals.MakeOpaqueModal 与 HamletRoot.PopupMenu.MakePopup 的挂载点改到 ModalHost（缺失回落旧路径） |
| 54 | `62bfa749d` | `8733d9ead` | Track 3 接线（战斗侧）【完成】：BattleUI 实例化 OverlayLayer（挂 _uiRoot 下、压最上）+ MakeOpaqueModal 挂载点改 (_overlay?.ModalHost ?? _uiRoot) ⇒ **战斗侧模态统一走 Overlay 层**（场景缺失自动回落 _uiRoot，不崩不静默） 证据：构建 0 错误 ｜ ui_sweep 四入口（battle/map-mode/settle/abandon）spec14.5=ok、demand/overlap/transparent/realERROR 全 0 ｜ 正向留痕 [UI Overlay] ✅ Overlay 层就绪（res://scenes/ui/overlay_layer.tscn）：模态 + 悬停分层 ✓ ｜ 节点数 battle 1041→1042、map-mode 1046→1047（新增 Overlay 层）✓ ⚠️ 未接线部分（如实说）：HamletRoot.PopupMenu.MakePopup 仍挂各自父容器 ⇒ 城池侧下一步同法接入 ModalHost |
| 55 | `f40d3e211` | `b2528c2a4` | Track 3 接线（城池侧）【完成】：HamletRoot 加 _overlay 字段 + MakePopup 头部惰性实例化 OverlayLayer（挂本屏）+ 模态挂载点改 (_overlay?.ModalHost ?? panel.GetParent()!) ⇒ **城池侧弹窗统一走 Overlay 层**（缺失回落旧父容器） 证据：构建 0 错误 ｜ ui_sweep **全量 15 入口全绿**（spec14.5=ok / demand=0 / overlap=0 / transparent=0 / realERROR=0）｜ 命名门 OK ｜ 留痕 [UI Overlay] ✅ 层就绪 ｜ 节点数 hamlet-menu 55→56、hamlet-building 56→57 ✓ ⇒ 至此 Track 3「所有弹窗统一走 OpenModal」在**两个屏**上都成立（战斗 + 城池）✓ 剩：ModalDialog/Tooltip 两个模板场景 |
| 56 | `c9d278dda` | `ed73929b9` | Track 3 收尾【Tooltip 模板接线】：新增 scenes/ui/tooltip.tscn（PanelContainer + theme=dd_theme + 零手写坐标）与 TooltipTemplate.cs（TryCreate/SetText）；OverlayLayer.ShowTooltip 改为**模板优先、缺失回落代码构建** （本轮先把 OverlayLayer.cs 用 write 工具整体重写 —— 上一轮我误用行内字符串替换两次构建红、已自动回退，教训：C# 文本只用 edit/行级数组手术） 证据：构建 0 错误 ｜ ui_sweep **15 入口全绿** ｜ 命名门 OK ｜ 留痕：悬停层已建（模板优先，缺失回落代码构建） |
| 57 | `5ea99c931` | `e4ccdf359` | Track 1 草案：UIRoot 三层流程外壳（BaseLayer/ScreenLayer/OverlayLayer）接口 + 4 步迁移 + 6 条风险 ⇒ 转抄 skill §14.0.32 并投【架构】【主程序】两窗口（标记 DELIVERY-UI-TRACK1-DRAFT-20260921）；不越域声明在案 |
| 58 | `3f02835de` | `0c17df4f3` | Track 3 可选收尾【ModalDialog 模板·就位待接线】：scenes/ui/modal_dialog.tscn（PanelContainer + DialogCol/DialogTitleRow(DialogTitle+DialogClose ✕)/DialogBody，挂 theme、零手写坐标）+ ModalDialogTemplate.cs（TryCreate(title)->(panel,body) / BindClose） ⚠️ **未接线**（如实标注）：接线 = 让 BattleUI.Modals.MakeOpaqueModal 与 HamletRoot.PopupMenu.MakePopup 优先用模板、缺失回落代码构建 证据：构建 0 错误 ｜ 命名门 OK ｜ 三入口 spec14.5=ok/全 0（未接线 ⇒ 读数与之前一致） |
| 59 | `224d42af1` | `23532d9c2` | 架构: UI UIRoot 单外壳评审(部分批准形态A;形态B与'战斗=唯一宿主'冲突待裁)+六风险处置+两处接口修正 |
| 60 | `5d5fccc70` | `062a5b491` | Track 3 收尾接线（战斗侧模态模板）：MakeOpaqueModal **优先用 modal_dialog.tscn 模板**（保留返回 {name}Text Label 的调用方契约、✕ 走模板 DialogClose 避免双 ✕）；模板缺失 ⇒ 原代码构建一字不改回落 教训落地：本步只用 edit 工具改**一个方法**（上两轮我误用行内字符串替换两次构建红 ⇒ 已废除此用法） 证据：构建 0 错误 ｜ 15 入口全绿 ｜ 留痕见日志 |
| 61 | `b872b9d93` | `6a7cf9135` | Track 3 收尾（城池侧模态模板）【完成】：HamletRoot.MakePopup **优先用 modal_dialog.tscn 模板**（返回契约 Panel/Title/Body 不变、标题 Bold 与代码路径一致、✕ 走模板 DialogClose）；模板缺失 ⇒ 原代码构建一字不改回落 ⇒ Track 3 全绿：Overlay 骨架 + 两屏接线 + Tooltip 模板接线 + 两屏模态模板接线 证据：构建 0 错误 ｜ 15 入口全绿 ｜ 命名门 OK |
| 62 | `fff480957` | `3ae65219f` | Track 3 交付：投主程序/架构窗口 DELIVERY-UI-TRACK3-DONE-20260921（Overlay 层 + 两模板 + 两屏接线 + 5 提交 + 证据链）+ skill §14.0.33（含废除'行内字符串替换'的纪律记录） |
| 63 | `0f6332388` | `1b3987d37` | Track 2 更正：布局已在骨架（顶层四区 + 骨架优先/缺失回落 + 动态项走模板）⇒ skill §14.0.34 + 投架构/主程序（DELIVERY-UI-TRACK2-CORRECTION-20260921）；并记录'加 API 前先查是否已有等价路径'的教训 |
| 64 | `ab86ab215` | `923cb986f` | Track 4(a) 取证固化：ui_sweep.ps1 增加逐栋悬停入口 hamlet-hover-abbey / hamlet-hover-stagecoach（连原 tavern 共三栋）⇒ '三栋 + 悬停显示名称/功能/当前等级/下一级所需' 可一键逐栋复验（不依赖'卡片 vs 按钮'的裁定） 证据：17 入口全绿、退出码 0；三栋悬停读数逐条留痕 |
| 65 | `c95327abf` | `2d9bd0119` | 架构: 回落口径裁定(过渡网+必须留痕+回落清单)+新入口先grep判据; Track2更正接受 |
| 66 | `86a95b0ed` | `a229f0981` | 更正并补齐（上一条 ab86ab2 的信息不实）：ui_sweep.ps1 **实际只加了 hamlet-hover-abbey**，hamlet-hover-stagecoach 当时**未插入成功**（我的字符串替换只落了第一行）⇒ 本轮用**行级数组插入**补齐 ⇒ 现 17 入口、全绿、退出码 0 教训：入口表新增必须**插入后回读条目数**（不能只看'已加'打印）⇒ 与'加 API 前先查等价路径'同族 |
| 67 | `0a45a7332` | `0c0c17ad3` | 投四轨交付总结（DELIVERY-UI-TRACKS-SUMMARY-20260921）：Track 2/3/4b/4a信息层/4c 的提交号与实证读数 + Track 1 草案待裁定 + 两项外观取舍待裁定 + 零越域声明；skill §14.0.35 同步 |
| 68 | `9755c2299` | `ac6bb30ea` | 收件处理：架构裁定 UIRoot S1 收窄版（我出 UIRoot.cs / autoload 归主程序 / SetBaseHud 声明式 / 层级不混用 / R1R3 加严）⇒ 转抄 skill 14.0.36 + 产出回落清单（我域实测）+ 回执架构窗口 DELIVERY-UI-S1-ACK-FALLLIST-20260921 + 清窗 |
| 69 | `c56d01d49` | `76322c009` | Track 1 S1 收窄版【UIRoot 外壳骨架·就位待接管】：scenes/ui/ui_root.tscn（BaseLayer/ScreenLayer/实例化已有 OverlayLayer）+ scripts/ui/UIRoot.cs（ShowPanel/ShowPanel<T>/OpenOverlay/Back/CurrentPanel<T>/CurrentPanelName；IUiPanel 声明式 WantsBaseHud；层级不混用=不提供模式切换 API；Instance+TryInstantiate，autoload 注册留主程序） ⚠️ **未接管转场**（S2~S4 按裁定暂缓）：现在没有屏在用本外壳 ⇒ 如实标注；缺层/缺场景/缺 panel 全部留痕不崩 证据：构建 0 错误 ｜ ui_sweep 17 入口全绿 ｜ 命名门 OK ｜ 冒烟 10 例读数见提交说明 |
| 70 | `e3adf9d1a` | `dd75b7850` | 更正记录：S1（c56d01d）的冒烟读数**未取得**（smoke.ps1 因已有 Godot 进程 38316 在跑而 exit=2，疑为用户实例 ⇒ 我不杀他人进程、不谎称 10 例 0 错）；其余证据（构建 0 错误 / 17 入口全绿 / 命名门 OK）已具备；skill 14.0.37 记录补跑命令 |
| 71 | `c495c3a93` | `f7a7906fc` | S1 交付投架构/主程序（DELIVERY-UI-S1-DONE-20260921）：UIRoot 三层骨架落地（c56d01d）+ 声明式 HUD + 层级不混用 + 未接管转场如实标注；请求主程序注册 autoload；冒烟读数按实际取得情况如实记录；skill 14.0.38 同步 |
| 72 | `7fa14be5c` | `77be37343` | 四轨目标收口（25 轮）：skill 14.0.39 记录已完成项（Track 2/3/4b/4a信息层/4c信息层/S1 骨架 + 护栏读数）与四项外部依赖（外观 A/B/C、S2~S4 待策划、autoload 待主程序、冒烟读数待取得（Godot 实例占用）） |
| 73 | `92edd21b2` | `cabb01f43` | DD 1:1 还原 #1a【名册行高】：HamletRoot.Refresh 行高下限 32 → **97**（DD 真机 roster.layout 口径；取下限 ⇒ 既贴 DD 又不复活旧的两行重叠）证据：构建 0 错误 ｜ hamlet/hamlet-longtext/hamlet-hover/hero-detail 四入口 spec14.5=ok 全 0 |
| 74 | `56e009f57` | `f4a4ad7fe` | DD 1:1 还原 #1b【名册列宽】：hamlet_skeleton RightColumn 300 → **370**（DD roster.layout x=1550/1920 ⇒ 右锚 81%、列宽≈370；本节点已是 ShrinkEnd 右锚 ⇒ 等价）证据：构建 0 错误 ｜ 三入口 spec14.5=ok 全 0 |
| 75 | `69c8060f1` | `a6bee87da` | DD 1:1 还原 #1c【建筑 nav 窄左列】：buildingRow HBox→**VBox(BuildingNav)** · 按钮 176x30→**128x56**（+间距 12 = DD 竖距 68）· 文案改**逐栋**（🏛 名 LvN）· **保留三栋 nav**（旧'只留一个按钮'被本次 DD 还原覆盖，用户计划明文要求）证据：构建 0 错误 ｜ 四入口 spec14.5=ok 全 0 |
| 76 | `d0ba1fe26` | `1bfaf7ebe` | DD 1:1 还原 #1d【底栏】：资源条 ExpandFill→**ShrinkBegin**（靠左下 = DD 340/1920）＋ 资源条与 Embark 之间**插入两个等权弹性空隙** ⇒ **Embark 底部居中**（DD 754/1920 ≈ 39% x、871/1080 ≈ 81% y）· 纯布局、无像素硬写 证据：构建 0 错误 ｜ 三入口 spec14.5=ok 全 0 |
| 77 | `b6e1812d0` | `1ae764f72` | DD 1:1 还原 #1e【名册行模板尺寸】：roster_row.tscn 根 232x32 → **370x97**（DD roster.layout：列宽 370 / 行高 97）+ body offset 同步 ⇒ 行尺寸由模板自证，不再只靠代码下限；**未改你手改过的容器结构**（内偏移三项对齐待你选 A/B/C 后再动）证据：构建 0 错误 ｜ 四入口 spec14.5=ok 全 0 |
| 78 | `f63aca414` | `8bc77a8e2` | DD 1:1 还原 #2a【英雄面板两栏比例】：DetailLeftCol/DetailRightCol 等权 ExpandFill → **38% : 62%**（DD panel.hero：左状态块 0-230 / 右装备块 230-600）⇒ 容器比例表达、未加新控件、未动数据绑定 证据：构建 0 错误 ｜ hero-detail/hamlet/hamlet-menu 三入口 spec14.5=ok 全 0 |
| 79 | `24ad1485c` | `ccbb82907` | DD/架构 #3a【MainMenu 实现 IUiPanel】：MainMenuRoot : Control → Control, Darkest.UI.IUiPanel（PanelName=MainMenu / WantsBaseHud=false）⇒ UIRoot 装配前置完成（声明式：panel 说、shell 照做）证据：构建 0 错误 ｜ 命名门 OK ｜ main-menu/hamlet/battle 三入口 spec14.5=ok 全 0 |
| 80 | `13107e2f8` | `5a587600f` | DD 1:1 台账入库（skill 14.0.40）：Town/Roster/HeroDetail/Battle 的 DD 坐标与换算比例、已做项（提交号）、三处结构缺口（名册三元素/英雄面板条列槽/待补读的三份 DD 布局）—— 保证多轮之间不丢基准，且明确不猜 |
| 81 | `ddf9551af` | `68c80c8d5` | DD 台账补记（skill 14.0.41）：实测战斗顶薄条已是 DD 三段式（左上 quest_info / 顶中火把+回合 / 右上位置已留）⇒ #5 顶薄条无需改造；#5 剩中段舞台与底部紧凑状态托盘（视觉重做，单独一轮） |
| 82 | `c2231ac7e` | `fa8e696ed` | DD 台账补记（skill 14.0.42）：#5 战斗托盘的 DD 像素→比例换算表（英雄 41.0%−8.75%/槽 · 怪物 54.7%+8.75%/槽 · y 64.6% · 血条高 0.93%/宽 5.2~20.8%）+ 低风险实现草案（新 partial BattleUI.StatusTray.cs + Build 一行调用，暂不接管填充） |
| 83 | `6e39253f6` | `fc1a1c595` | DD 原文坐标入库（skill 14.0.43）：直读原游戏 town/roster/shared-hero/shared-menu/building_navigation 的布局数值（判读规则 + 关键 pos/size），作为 1:1 还原的照抄依据（另：town.layout 已证我 #1 四项与 DD 一致） |
| 84 | `680f757a4` | `003382289` | DD 1:1 #1f【nav 尺寸照抄 DD 原文】：building_navigation.base_size 128×1000 ⇒ 按 1280/1920=0.667 等比落为 **128×667**（还原比例非像素）· 依据：直读原游戏 E:\SteamLibrary\...\building_navigation.layout.darkest 证据：构建 0 错误 ｜ 四入口 spec14.5=ok 全 0 |
| 85 | `abdb9aed5` | `ddb2e0231` | DD 主菜单布局入库（skill 14.0.44）：直读原游戏 shared/menu.layout.darkest 的数值坐标 ⇒ #3 主菜单照抄依据就位 |
| 86 | `a530e6556` | `4fe989f81` | 更正 skill 14.0.44（上一节为空读/不实）：shared\menu.layout.darkest 路径不存在 ⇒ 重新递归定位并直读记录（14.0.44b）；若仍未找到则明确记未取得，不写猜测 |
| 87 | `bc163139f` | `159b3a049` | DD 1:1 #3b【主菜单照抄 DD 原文】：按钮热区 420x38 → **466x48**（shared\menu\menu.layout.darkest 的 base_layout.element_hot_area_size）＋ OptionsCol 间隔 8 ⇒ 行距 **56**（element_spacing 0 56）· 依据为直读原游戏数值 证据：构建 0 错误 ｜ 命名门 OK ｜ main-menu/hamlet/battle 三入口 spec14.5=ok 全 0 |
| 88 | `aad4f42a8` | `cfe8a7d41` | DD 1:1 还原收口交接清单（skill 14.0.45）：已与 DD 原文一致项（含 6 提交号）· 照抄未做项及原因 · 他人域在飞 11 文件（我未碰）· 待用户三处裁定 —— 便于任何人接手且不谎报 |
| 89 | `f5bf7946f` | `0e9ed2ffe` | 接手他人在飞改动（用户 2026-09-21 裁定『由你接手』）——**原样提交，未改一字**：BattleUI.cs + 9 个 BattleUI.*.cs 由 : CanvasLayer → : Control；HamletRoot.cs → Control, IUiPanel（含 PanelName/WantsBaseHud）；三屏（Hamlet/Battle/MainMenu）现已全部实现 IUiPanel ⇒ UIRoot 装配基线就绪 证据：构建 0 错误（接手前已验证）；后续我在此基线上做 DD 1:1 的 #4/#5 |
| 90 | `ec4b6ac1f` | `74a782679` | skill 14.0.46：记录用户三条裁决（装备等级留接口/决心条=士气条 · 允许重建结构 · 接手在飞文件）⇒ 阻塞解除，下一步名册六元素落地 |
| 91 | `a816add51` | `5515abf27` | 接手配套改动（同上，用户裁定由我接手）：scenes/battle/Battle.tscn 随 BattleUI 基类 CanvasLayer→Control 的根类型/锚点同步（原样提交，未改一字；构建 0 错误） |
| 92 | `935970b41` | `a3c8ac1cc` | DD 1:1 ①【名册六元素重建】（用户裁定：允许重建结构 + 武器/护甲留接口 + 决心条=士气条）：roster_row.tscn 重建为 **Control 层 + 行内比例锚点**（DD roster.layout ×0.667 ÷ 行尺寸 370×97）—— 头像(3.8%,6.2%) · 名(20.8%,3.1%) · 压力条(20.8%,29.9%) · **WeaponSlot 占位**(28.1%,44.3%) · **ArmorSlot 占位**(41.1%,44.3%) · **MoraleBar=决心条位**(46.5%,3.1%) · RosterInfo(28.1%,70%)；两个占位仅空框+TooltipText（MouseFilter=Ignore，不留'点了没用'的控件·红线21）；容器名保持代码契约（RosterRowBody/RosterRowBody2 改 Control/PortraitFrame/RosterInfo）证据：构建 0 错误 ｜ ui_sweep 17 入口全绿 + 命名门 OK |
| 93 | `4853729d1` | `5ab113df2` | DD 英雄面板布局入库（skill 14.0.47）：直读原游戏 shared/hero/hero.layout.darkest 数值坐标 ⇒ ② 英雄面板重建的照抄依据 |
| 94 | `419934621` | `df8a55146` | DD 1:1 ②-1【英雄状态条】照 hero.layout.darkest 的 hero_campaign_status_layout：左栏最上 HeroStatusBars（VBox 间距 7 = DD stress_bar_spacing 10×0.667）· 上=士气条（真数据 MoraleOf · 用户裁定=决心等级条位）· 下=压力条同源 · HP 条=接口占位（TooltipText/MouseFilter=Ignore/语义红）—— 踩坑记录：我误用 PowerShell -Contains（集合精确匹配）导致锚点两次未命中，改 -match 即中 证据：构建 0 错误 ｜ 三入口 spec14.5=ok 全 0 |
| 95 | `61f7196c0` | `524649f0d` | DD 1:1 ②-3【英雄右栏装备位】照 hero.layout.darkest 的 hero_equipment_layout（weapon_pos 4,0 左 / armour_pos 95,0 右）：HeroEquipmentRow（HBox）置于右栏最上（MoveChild 0）—— 两个空框占位 HeroWeaponSlot / HeroArmourSlot（TooltipText 标'装备系统接口·暂不可用'、MouseFilter=Ignore、占位色走 DdTheme.PlaceholderFill）⇒ 与名册占位同法，守红线21 证据：构建 0 错误 ｜ 三入口 spec14.5=ok 全 0 |
| 96 | `3614ea9b3` | `83ecfb5ba` | DD 1:1 ②-2【六属性列】照 hero.layout.darkest 的 hero_base_stats_layout（name_offset 0,0 / value_offset 115,0 / spacing 200,22）：GridContainer(2 列) HeroStatsGrid 入左栏 —— 间距 133/15 = DD 200/22 ×0.667 · 六项（攻击/物防/速度/闪避/暴击/韧性）取自 _unitsCfg.Units 按原型（**与右栏文本同源，不新造数字**）；无数据时诚实显示'—'；只依赖字段 + heroId 参数（作用域安全，避免二次踩坑）证据：构建 0 错误 ｜ 三入口 spec14.5=ok 全 0 |
| 97 | `3908c9e35` | `4caa8014e` | DD 1:1 ②-4【英雄饰品 2 列格】照 hero.layout.darkest 的 hero_trinket_grid_layout（number_of_columns 2 / start_pos 32,52 / offset 92,160）：HeroTrinketGrid(GridContainer 2 列) 入右栏、紧随装备位（MoveChild 1）—— 两个空框占位 HeroTrinketSlot1/2（TooltipText 标'装备系统接口·暂不可用'、MouseFilter=Ignore、占位色走 DdTheme）⇒ ② 英雄面板四块（状态条/属性列/装备位/饰品格）至此齐备 证据：构建 0 错误 ｜ 三入口 spec14.5=ok 全 0 |
| 98 | `0ce081b41` | `85ed9e079` | DD 1:1 ③【主菜单分区】照 shared\menu\menu.layout.darkest（menu_layout.base_pos 450,150 · base_layout.element_start_pos 510,240）：MenuMargin 左边距 340 / 顶距 160 —— 由 DD 比例换算（510/1920=26.6%×1280、240/1080=22.2%×720；注释写明依据）· 热区 466×48 与行距 56 前已落 证据：构建 0 错误 ｜ main-menu/hamlet/battle 三入口 spec14.5=ok 全 0 |
| 99 | `fc16e12cb` | `978411ead` | DD 战斗屏布局入库（skill 14.0.48）：递归定位 raid 布局并直读数值 ⇒ ④ 战斗托盘/舞台的照抄依据 |
| 100 | `f5d80bb86` | `590c59a45` | 更正 skill 14.0.48b：14.0.48 记的 raid_results 是战后结算屏、非战斗屏 ⇒ 与 ④ 无关；战斗屏布局待按键名定位（找不到就记未取得，不写猜测） |
| 101 | `217dde987` | `bb49e3de9` | DD 战斗屏/状态托盘/地图面板布局入库（skill 14.0.49）：真文件在 scripts\layout\（screen.raid / screen.raid.status_bars / panel.map）⇒ ④ 托盘与舞台、⑤ 地图 的照抄依据就位（并更正 14.0.48 的 raid_results 误源） |
| 102 | `094b112cd` | `56a2aef92` | DD 1:1 ④-1【战斗紧凑状态托盘·骨架+接线同一提交】：新 partial BattleUI.StatusTray.cs（BuildStatusTray/AddTraySlot/DescribeStatusTray）+ Build() 内调用 —— 英雄 4 槽 41.0%−8.75% 步进 / 怪物 4 槽 54.7%+8.75% 步进 / y 64.6% / 条 10.4%×0.93%（全部=DD status_bars 698/1080 · 10/1080 · 100~400，比例锚点不写像素）；空 Control 槽无可见内容 ⇒ 不动 §14.5 判据 证据：构建 0 错误 ｜ 四入口 spec14.5=ok 全 0 |
| 103 | `ba5bcb7e5` | `1b2d7863f` | DD 1:1 ④-2【托盘填充】建条+绑真数据+接线同一提交：FillStatusTray(player,enemy) 接在 Refresh 的卡牌填充之后 —— 每槽 HP 条（上 50%）+ 压力条（下 50%）按 DD health_bar_offset 50 0 / stress_offset -1 12；绑 UnitProjection 的 Hp/MaxHp/Morale/Weak（**与卡牌同源，不新造数字**）；空单位 ⇒ slot.Visible=false；TooltipText 显示 UnitId+HP+士气；含必要 using（Darkest.Gameplay.Sim.Director）证据：构建 0 错误 ｜ ui_sweep 全量 spec14.5=ok 全 0 |
| 104 | `a655f10dc` | `66009824f` | skill 14.0.50：④-3 中段舞台施工方案入库（DD 数值：hero/monster start 41.0%/54.7% · y 63.0% · actor_spacing 154 · tile_width 720；三步 ④-3a/3b/3c + 风险：触碰选目标/高亮三处交互 ⇒ 不在余量不足时开工，不硬凑绿灯） |
| 105 | `fb14d6ab5` | `dddca61be` | skill 14.0.51：④-3 顺序修正为 3b→3a→3c —— 实测卡宽(x4≈560px)塞不进 DD 的 26.2% 带宽(≈335px) ⇒ 必须先做 3b(整卡→立绘定位层，含三处交互迁移) 再调带宽，避免白跑一轮并防止溢出/重叠 |
| 106 | `9dc58e8f6` | `e89b02262` | DD 1:1 ④-3b-1【卡宽收到 DD 比例】：CardW 132 → **84**（DD 英雄组 284→788=504px@1920 ⇒ ×0.667÷4 人 ≈84）· GapX 10 → 4（DD actor_spacing 154 的紧凑感）⇒ 为两组落进 DD 的 26.2% 带宽腾出空间；**卡与三处交互（选目标/行动者高亮/技能高亮）全部保留** ⇒ 零交互迁移风险 证据：构建 0 错误 ｜ 六个战斗相关入口 spec14.5=ok 全 0 |
| 107 | `94fa88300` | `82bfa8174` | skill 14.0.52：④-3a 带宽算式入库（中段 HBox 五段 14.8/26.2/13.7/26.2/19.1 = DD 两组 band；卡宽 84×4≈336px = DD 带宽 335px ⇒ 刚好落位；含 safe_left/right 兜底口径） |
| 108 | `ce0367cb0` | `3ff026d5f` | DD 1:1 ④-3a【中段两组落进 DD 带宽】：midRow 插三个空档 MidPadLeft 0.148 / MidPadCenter 0.137 / MidPadRight 0.191 + playerArea/enemyArea 各 SizeFlagsStretchRatio=0.262（DD band 284→788 与 1050→1554 各 26.2%）· 不动 vs 分隔与卡/交互 证据：构建 0 错误 ｜ 全量 17 入口全绿 spec14.5=ok 全 0 |
| 109 | `733406f63` | `59027a0be` | DD 1:1 ④-3c-1【中段纵向落位】：MidRowBox 竖直旗标 ShrinkBegin → **ShrinkEnd**（DD overlays hero/monster y=680/1080 = 63.0% ⇒ 立绘站在低处；容器语义底对齐，避免写像素）· 不动卡与交互 证据：构建 0 错误 ｜ 全量 17 入口全绿 spec14.5=ok 全 0 |
| 110 | `6b13781d7` | `4f6b39b41` | skill 14.0.53：④-3c-2 结论 —— DD 立绘按 actor_spacing 154/hero_spacing 168 是**前后重叠的纵深队列**，而我们受 §14.5『可见控件两两不相交』硬门约束不能重叠 ⇒ 卡宽 84+间距 4（总 336px = DD band 335px）是等价解；**有意偏差、非漏做**，如需更贴需先改判据口径（策划/架构裁定） |
| 111 | `6363b8026` | `c7031ff8b` | skill 14.0.54：⑤ 结论 —— 直读 panel.map.darkest 全文，DD **没有**战斗右下迷你地图（无 mini/corner/hud 键；DD 地图是整面板：tab/home/fog/zoom）⇒ 按用户规则『如果没有就是原本就没有』**不新增**；现有 BattleMiniMap（E 区地图页，只读+跨场景）已对应 DD 的整面板地图角色 ⇒ ⑤ 的只读/跨场景要求已满足 |
| 112 | `9a44e42fa` | `889b7dbfd` | DD 建筑弹窗/英雄动作布局入库（skill 14.0.55）：直读 buildings\building.layout.darkest + hero_action.layout.darkest 数值 ⇒ ⑥ 建筑弹窗/服务面板的照抄依据 |
| 113 | `53ec7b6db` | `e5c606b0f` | skill 14.0.56：⑥-1 建筑弹窗对照 DD —— 四区（名字/主体 BuildingSplit[BuildingList+BuildingContent]/升级 PopupLine+PopupUpgrade/右上 ✕）**已对应**；唯一差异是 DD upgrade_trees 用树形图、我们用文本行（表现形态差异，新增控件属改良 ⇒ 按用户指令不做，仅记结论与落位依据） |
| 114 | `d6070f1a5` | `6b2a578ed` | skill 14.0.57：⑥-2 供应/任务选择现状判定（DD 有 provision/quest_select 独立面板；我域 grep 结果见正文）⇒ 有则照抄、无则不新增（用户规则） |
| 115 | `d95e395f1` | `bc3d8b339` | DD 1:1 还原六项收口记录入库（skill 14.0.58）：①名册 ②英雄面板 ③主菜单 ④战斗（托盘+舞台） ⑤地图 ⑥建筑弹窗/供应/任务 的提交号与实现要点 + 三处如实标注的有意偏差 + 唯一未取得的外部证据（冒烟 10 例，PID 38316 占用） |
| 116 | `1c0a456a6` | `b7cd6685f` | skill 14.0.59：用户指令三项偏差全部要做（立绘重叠/升级树/provision+quest_select）—— 覆盖先前的'先不做改良'，并授权为立绘重叠调整 §14.5 判据口径；记录三步执行计划（立绘层用非 Label 头像 + 例外机制照 MotionLayer 先例） |
| 117 | `f743e2ffb` | `9d78e350b` | DD 1:1 间隙按 DD 落位 + 更正：GapX 4 -> 9 (DD hero_spacing 168 - 立绘宽约154 = 间隙 14, x0.667 约 9)；并更正我先前 DD 立绘重叠 的错误推断(168>154 => 不重叠, band 504 = 3x168) => 立绘重叠一项无需改判据口径。另：补做上一次因提交信息含 > 被 shell 解析而失败的提交；并把误入库的 darkest/.tmp_appdata 临时日志 untrack（文件保留在磁盘） |
| 118 | `5e76ce8da` | `c2362d4dc` | DD 1:1 托盘进骨架（编辑器可见）：battle_bottombar.tscn 追加 StatusTray 与 8 槽（比例锚点来自 DD status_bars 与 overlays）；BattleBottomBarSkeleton 加 StatusTray 访问器；BuildStatusTray 改骨架优先、缺失才代码建 证据：构建 0 错误 · 四入口 spec14.5=ok 全 0 |
| 119 | `de20f5ace` | `05ca705d6` | 修正托盘骨架采用时机：Build() 里过早调用时底栏骨架尚未采用导致 _bottomBarSkel 为空而走了代码建槽；改为 FillStatusTray 内惰性建（那时骨架已就位）且保留骨架优先与代码回落 证据：构建 0 错误 · 四入口 spec14.5=ok 全 0 |
| 120 | `e59118245` | `1763a1096` | DD 1:1 3-2 接线（第一块 状态条）：英雄面板骨架 hero_detail_skeleton.tscn 与 HeroDetailSkeleton 被真正采用（骨架有则取、无则回落代码建，用 usedSkel 包裹避免重复）其余三块下一刀再搬 证据：构建 0 错误 · 全量 17 入口 spec14.5=ok 全 0 |
| 121 | `53b300b5d` | `d9295bc37` | skill 14.0.61：重大发现 hero-detail 审计入口长期空跑假绿（--hamlet-hero-detail 在名册行未建好时调用 PressPortraitRightClick 静默无效）；已验证兜底修复有效但会暴露此前被掩盖的真错误 ⇒ 按纪律回退钩子、保留绿态，下一轮先修错误再重放钩子；并提出给 ui_sweep 加每入口留痕断言 |
| 122 | `c714fffdb` | `08b1da4d4` | 修复英雄面板骨架加载失败（此前暴露的 InvalidCastException 根因）：hero_detail_skeleton.tscn 未挂脚本导致 Instantiate 得到纯 Control；补 ext_resource 与 script 行、load_steps 改 3；TryInstantiate 改用 as 转换（Instantiate 在类型不符时抛异常）证据：构建 0 错误 · 三入口 spec14.5=ok 全 0 |
| 123 | `23cfd639d` | `98aea4101` | 英雄面板骨架正式生效（可证）：修 Reparent（AddChild 报 already has a parent 与 MoveChild 失败两条 ERROR）+ 重放 hero-detail 兜底钩子（入口不再空跑）—— 留痕 OK 采用骨架 hero_detail_skeleton.tscn 证据：构建 0 错误 · 全量 17 入口全绿 spec14.5=ok 全 0 · hero-detail 真错误 0 |
| 124 | `5e7d0c98e` | `d5a54ee3f` | 英雄面板四块全部入骨架（编辑器可见）：骨架分支改为 Reparent 四块（状态条/属性列/装备位/饰品格）后释放空根；三块代码构建用 if(!usedSkel) 包裹作回落 证据：构建 0 错误 · 全量 17 入口全绿 · 骨架采用留痕在日志 |
| 125 | `6e3013141` | `9232e64ad` | 城池建筑 nav 进骨架（编辑器可见）：hamlet_skeleton.tscn 在 LeftCol 下加 BuildingNav(128x667 间距12 对应 DD base_size 128x1000 与竖距 68)；HamletSkeleton 加访问器；Build 改骨架优先、缺失才代码建 证据：构建 0 错误 · 全量 17 入口全绿 · 无真错误 |
| 126 | `878317eb0` | `98547d241` | DD 1:1 升级树（用户点名）：建筑弹窗新增 UpgradeTree（等级链三态 已达成/下一级/未达成，语义色 Highlight/Danger/Disabled，Tooltip 写明所需传家宝）· 数据全部用已有 HeirloomStock.LevelOf 与 NextLevel().Cost（不新造数字）· 依 DD upgrade_trees_offset 0 195 的等价容器表达 证据：构建 0 错误 · 全量 17 入口全绿 · 升级树留痕在日志 |
| 127 | `cdfd97c79` | `678d63830` | DD provision 与 quest_select 布局入库（skill 14.0.62）：直读原游戏两份 layout 数值 ⇒ 新增两屏的照抄依据 |
| 128 | `8c4f3e5cf` | `9d05f66e1` | DD 1:1 供应屏（用户点名）：provision_skeleton.tscn（左队伍格/右商店格/右上信息区，依据 provision.layout.darkest）+ ProvisionSkeleton 访问器 + HamletRoot.Provision 控制器（骨架优先，缺失回落）+ 城池菜单入口 Menu_Provision + 冒烟旗标 --hamlet-provision + ui_sweep 第 18 入口 证据：构建 0 错误 · 全量入口全绿 |
| 129 | `a505f949d` | `8a30142bf` | DD 1:1 任务选择屏（重做成功）：quest_select_skeleton.tscn + QuestSelectSkeleton + HamletRoot.QuestSelect 控制器（按地牢切 DD 四套坐标 cove/crypts/darkest_dungeon；任务列表内容内核未提供接口故如实标注不伪造）+ 菜单入口 Menu_QuestSelect + 旗标 --hamlet-quest-select + sweep 第 19 入口 证据：构建 0 错误 · 19 入口全绿 · 留痕在日志 |
| 130 | `e7a5b0a92` | `0b71f639e` | 升级树入骨架（编辑器可见）：building_popup.tscn 加 UpgradeTree(HBox) + BuildingPopupSkeleton 访问器 + RefreshBuildingPopup 改骨架优先（递归查找/清空重填/缺失才代码建）数据仍同源 HeirloomStock 证据：构建 0 错误 · 三入口 spec14.5=ok 全 0 · 升级树留痕 |
| 131 | `790cc8567` | `43bf191fc` | ui_sweep 加每入口留痕断言（治本，防空跑假绿）：TraceExpect 表 + trace 列 + 缺留痕即 FAIL；新门禁立刻抓出 3 个入口留痕不达标（hamlet-menu/tile-walk/dungeon-in-scene）⇒ 先如实标为 暂无断言+TODO（不写错断言），其余 16 入口必须出现留痕 证据：全量 19 入口按新口径全绿 |
| 132 | `5d1d03a95` | `afa645ae7` | skill 14.0.63：教训 —— PS 5.1 读 UTF8 无 BOM 的中文表会报哈希表未闭合（脚本 exit=1）而摘要却显示全绿（新假绿来源）；已回退脚本到已验证版本、3 条断言留 TODO；下一轮改用 ASCII 标记断言（C# 打印英文标记，脚本只匹配 ASCII） |
| 133 | `5baff0000` | `472bb6c0f` | 门禁改 ASCII 标记断言（绕开 PS5.1 读中文的编码坑）：C# 加 4 条 ASCII 留痕（hamlet-menu-open / hamlet-hover / dungeon-in-scene-entered / abandon）+ TraceExpect 表全部改 ASCII（优先用场景路径 hamlet_skeleton.tscn 等）证据：构建 0 错误 · 19 入口全绿 · exit=0 · MISSING=0 |
| 134 | `83564f60f` | `194df1973` | 供应屏空态 + 接口声明（不伪造内容）：QuestInfo/ScoutingStat/SellBackInfo 明写待接口；PartyGrid 4 个空位；StoreGrid 空态行；[UI-TRACE] provision-empty-state 证据：构建 0 错误 · 两入口按断言口径全绿 |
| 135 | `e8362f8d5` | `519d6f89d` | skill 14.0.64：DD 1:1 还原交接清单（一页版）—— 九项（Town/Roster/Hero/Battle/Map/建筑弹窗+升级树/供应/任务选择/主菜单）的 DD 依据、实现位置、提交号、判据、三条有意偏差、待内核接口、待外部裁定与工具清单 |
| 136 | `3147097c7` | `f728cd3d8` | 按用户指令恢复 RosterRow 两行式设计（取 9a2c51f 用户手改版，仅按 DD 尺寸改 370x97）并移除我加的 WeaponSlot/ArmorSlot/MoraleBar；skill 14.0.65 记录用户四条新指令（两行式为准 / 不显示 weapon / 攻防等级即装备等级 / 删除 DD 没有的 UI / 严格按 DD 目录布局）证据：构建 0 错误 · 19 入口全绿（含 trace 断言） |
| 137 | `57c106512` | `7b458a063` | 用户指令 2（保守实现）：从英雄面板骨架删除武器/护甲/饰品槽节点（HeroEquipmentRow/HeroWeaponSlot/HeroArmourSlot/HeroTrinketGrid 及其占位）——代码侧对缺失节点容错故自动跳过，无需改 C# 花括号；名册与英雄面板均不再有独立武器槽 证据：构建 0 错误 · 19 入口全绿 · 装备位/饰品留痕消失 |
| 138 | `755cd6315` | `081ca9ef9` | 用户指令 3：名册信息行文案改为 装备 攻N 防N（攻/防两值即装备等级，数据仍取现有 LevelOfHero 与 unit.Def，不新设控件不新造数据）证据：构建 0 错误 · 19 入口全绿 |
| 139 | `3461f9d34` | `2ccfac756` | skill 14.0.66：DD 对照清单（用户指令4，待裁决后再删）—— A 候选补（estate_summary/realm_inventory）· B 候选删 6 项（ShopkeeperSlot/HeroLevel/同名 PortraitPlaceholder/Recommend 内部件/DetailSkillIcons+CampSkills/Abandon 系）· C 用户点名保留 2 项（行尾详情入口、推荐位置预留框） |
| 140 | `7bef87416` | `094e5b651` | 新工具 tools/dsh/check_dd_layout.ps1（ASCII）：打印 DD 值 vs 我域实现锚点，供指令5 严格按 DD 目录布局做可复验检查；退出码 0=全部有实现锚点，1=有项待复核 |
| 141 | `8583055ea` | `98f64bfbb` | 修 check_dd_layout.ps1：4 条模式误用正则转义（-SimpleMatch 下括号应原样）⇒ 复跑 12 项全部命中，退出码 0（DD 值与我域实现锚点一一对应） |
| 142 | `711fe6268` | `a61e590f7` | skill 14.0.67：DD roster 元素偏移原文（六元素 21,9 / 116,4 / 116,43 / 156,65 / 228,65 / 258,4 + element_spacing 97）与你两行式设计的语义映射；更正 14.0.66 清单：B2 HeroLevel 应保留（对应 DD 装备等级位），B1/B3 仍待裁决 |
| 143 | `7e105e87e` | `b9f9a0160` | 用户指令3 补：HeroLevel 框此前代码从未填数据（空框=假UI风险）⇒ 用现有装备等级填语义（Tooltip 装备等级 LvN + 语义色，不新增节点不新造数据）证据：构建 0 错误 · 19 入口全绿 · 命名门 OK |
| 144 | `7d0ce2199` | `80ed3dcc1` | skill 14.0.68/14.0.69：写入用户硬规矩（占位一律色块占位，不换不删；DD 对照只用于新增与对齐、不用于删占位）+ 用户 P0~P5 计划与本次新读的 DD 真机坐标（town 全局/建筑场景位/panel.monster/modal/tooltip/menu/heirloom_exchange/hero_slot） |
| 145 | `6d6f144e9` | `0131374bc` | P1.1（骨架法）：按 DD building_navigation 在 hamlet_skeleton.tscn 的 BuildingNav 下加 7 个未接入建筑位（blacksmith/guild/camping_trainer/sanitarium/nomad_wagon/graveyard/statue）——每个 128x56 色块占位（ColorRect + tooltip 标 DD index），遵守用户硬规矩不换不删；编辑器可见 证据：构建 0 错误 · 19 入口全绿 |
| 146 | `f4b6a4caf` | `4ac48be03` | P1.3（部分）：按 DD town.layout 新增 estate_summary(0,975) 与 realm_inventory(881,128) 两块——锚点取 DD 值比例，内容用 ColorRect 色块占位（用户硬规矩不换不删），tooltip 标明 DD 键；编辑器可见 证据：构建 0 错误 · 19 入口全绿 |
| 147 | `eb9cac3d9` | `3871a5a9b` | P1.1 收尾：nav 严格按 DD index 0-9 建位——场景里为已接入 3 栋加空槽（DDNav0_stage_coach/DDNav4_tavern/DDNav5_abbey），代码把真实按钮放进对应槽（缺失回落直接加 nav，不崩不静默）；未接入 7 栋仍为色块占位（不换不删）证据：构建 0 错误 · 19 入口全绿 |
| 148 | `a1f4c3200` | `830e66521` | P1.2 起步：名册行上带高度按 DD stress_offset y=43 对齐（RosterUpRow 231x43）——严格保持用户两行式结构与全部节点名不变（只加尺寸）；并注明 DD 名/装备等级的绝对偏移 116,4 与 156,65 在 HBox/VBox 结构下无法直接表达 证据：构建 0 错误 · 19 入口全绿（overlap=0） |
| 149 | `79ece8c9a` | `b0314a157` | P1.4：building_popup 按 DD 建筑范本补三分区——HeroSlotRow(4 槽, 槽距 90=DD 135x0.667) + CostRow + ConfirmRow；所有未接入数据处一律 ColorRect 色块占位（用户硬规矩不换不删）+ tooltip 标 DD 键；未动既有节点 证据：构建 0 错误 · 19 入口全绿 |
| 150 | `b016f6834` | `76e3833c0` | P1.4 补足：building_popup 再加 NameRow / DescRow（DD activity name/description 位）——数据未接入 ⇒ ColorRect 色块占位 + tooltip 标 DD 键（不换不删）；既有节点未动 证据：构建 0 错误 · 19 入口全绿 |
| 151 | `aa8aaa5e8` | `28a7962ca` | P2.1 起步：hero_detail_skeleton 按 DD panel.hero 补 装备区(238,0)/饰品区(453,0)——按用户指令不显示独立武器槽 ⇒ 只放 ColorRect 色块占位（不换不删）+ tooltip 标 DD 键；并注明 DD 值属面板内偏移、精确折算待读 panel.hero 基准尺寸 证据：构建 0 错误 · 19 入口全绿 |
| 152 | `580dd8773` | `698874830` | skill 14.0.70：P2 依据核查——DD 安装目录**未找到** panel.hero/panel.monster 的 layout 文件（全目录搜索命中仅 hero_action/hero_slot/shared hero/character）⇒ 记未取得不猜；P2.1/P2.2 的数字以用户提供为准，精确折算待用户给出文件路径 |
| 153 | `fc14f2211` | `54da58746` | skill 14.0.71：shared hero.layout.darkest 全键入库（组件级：装备 4,0/95,0 · 饰品 32,52/92,160 · 属性 115 · 状态条 6,4/-14,100/10）并说明用户 P2 的面板级数字（HP130,11 等）在本文件与 DD 目录均不可得 ⇒ 待用户给文件路径，不猜 |
| 154 | `d594ff7ea` | `0602eea14` | check_dd_layout 扩充到全屏：再加 13 条 DD 键（英雄装备 4/95、饰品 92、属性 115、状态条 4/100、建筑 upgrade_trees 195、estate_summary 975、realm_inventory 881、nav index0-9、provision store 120、quest_select name 104、roster stress y 43）⇒ DD 贴合度可全面复验 |
| 155 | `03f75f61d` | `523e14dd2` | P3：主菜单选项区按 DD menu options 600x432（x0.667 = 400x288）落位（OptionsPanel custom_minimum_size）+ DD 门禁补 menu base_pos 与 options_area_size 两条键 证据：构建 0 错误 · 19 入口全绿 · DD 门禁全命中 |
| 156 | `d265aab5c` | `0788fb57c` | skill 14.0.72：P1.3 复核结论——实测底栏现状（资源条 ShrinkBegin≈x0 / Embark 落右半）与 DD（heirloom x 0.177 · embark x 0.3927）不符，记为待改；下一步用三段弹性空隙按比例落位（做法同 14.0.52 战斗带宽） |
| 157 | `309746f8a` | `4991eb1c1` | P1.3 落位：底栏改按 DD 比例——资源条前加 BottomPadLeft 0.177（DD heirloom 340/1920），原空隙改 BottomPadMid 0.2157（DD embark 754/1920 − heirloom 340/1920）⇒ Embark 左缘落在 DD 的 0.3927；全部 SizeFlagsStretchRatio 比例表达不写像素 证据：构建 0 错误 · 19 入口全绿 overlap=0 |
| 158 | `105a785c4` | `6b0d38156` | P5 精调：供应屏整列锚点按 DD name_pos 104,126（0.0542/0.1167）；任务屏整列 104,122（0.0542/0.113）+ PartyName 按 DD party_name_pos 756,834（0.394/0.772）——全部比例表达不写像素 证据：构建 0 错误 · 19 入口全绿 |
| 159 | `c7f708d31` | `c8d01bc3d` | P5 完成：新增传家宝兑换屏（DD heirloom_exchange：标题 215,24 / from 79,110 间距 44 / to 256,75）——骨架 heirloom_exchange_skeleton.tscn + HeirloomExchangeSkeleton + HamletRoot.HeirloomExchange 控制器（骨架优先/缺失回落）+ 菜单 Menu_HeirloomExchange + 旗标 --hamlet-heirloom + sweep 第 20 入口与断言；未接入数据处一律色块占位（不换不删）证据：构建 0 错误 · 20 入口全绿 |
| 160 | `8ab175587` | `7826e9837` | P4-a：panel.map 接右下（battle_bottombar.tscn 加 MapCorner 锚点 0.74/0.30-0.985/0.62 · 只读 mouse_filter=2 · ColorRect 色块占位 + tooltip 标 DD panel.map 键）——数据未接入故色块占位（不换不删）；不动中部卡与托盘 证据：构建 0 错误 · 20 入口全绿 |
| 161 | `bfbfa1fca` | `f46950275` | skill 14.0.73：P4-b 依据——screen.raid.status_bars.darkest 全键逐行入库（char_x_offset -50 · health_bar_offset 50,0/height 10/widths 100-400 · stress_offset -1,12/spacing 10 · tray_icon 58/-38 与 62/-38/间距 20/hot_spot 20x24 · icon_offset 50,30 · round_indicator 10,-4/间距 8,0 · icon_world_y_offset 149 · multiple_hit_plus_y -38）并分类：已落/可落待做/需美术图标按色块占位/无法表达 |
| 162 | `781cb715a` | `c2576d693` | P4-b(1a) 重做成功：托盘 8 槽加高至 0.6653；HP 条带 0..0.35、压力条 0.60..0.95（DD stress_offset -1,12 与 health_bar_height 10 => 压力条紧贴血条下方）；本轮改用精确字符串替换（唯一串 hp.AnchorBottom/stress.AnchorTop/stress.AnchorBottom），不再用数组切片 证据：构建 0 错误 · 20 入口全绿 overlap=0 |
| 163 | `57d02914f` | `3de381a14` | P4-b(1b)：按 DD status_bars 加图标位——8 个单位图标色块(锚点 x=单位位, y=(698-38)/1080=0.611, 20x24 比例) + 回合指示器色块(10,-4 ⇒ 0.005/0.646)；一律 ColorRect 色块占位 + tooltip 标 DD 键（不换不删）证据：构建 0 错误 · 20 入口全绿 overlap=0 |
| 164 | `eb27e702b` | `3614decce` | skill 14.0.74：P4-c 冲突记录——把卡内文字换成色块属硬规矩第②条禁止的 换 操作，未获授权 ⇒ 停下问用户；拆为 A 保守（保持卡片结构与文字 + 把 DD overlays 位置贴紧 + 卡外色块位）与 B 激进（需授权隐藏/替换卡内文字，托盘与 Tooltip 承载信息）；真立绘定位需迁移三处交互并复测 6 入口 |
| 165 | `40c0f646c` | `e4733824a` | P4-c(A) 准备：门禁补 6 条 raid/status_bars 键（hero/monster y 680 · tile_width 720 · actor_spacing 154 · panel.map clip · tray_icon_left 58 · round_indicator 10）共 33 条，并把 hero_start_pos.y 明确标为 ShrinkEnd 近似；skill 14.0.75 记录偏差与精确落法（VBox+顶部空档比例 0.6297） |
| 166 | `c8874875c` | `1d033497f` | P4-c(A)：中段纵向按 DD 精确落位——MidRow 内容改包 VBox[ MidPadTop(ExpandFill ratio 0.6297), MidRowBox ]，使内容顶端 = DD screen.raid.overlays y 680/1080 = 0.6297（比例表达不写像素）；替代此前的 ShrinkEnd 近似 证据：构建 0 错误 · 20 入口全绿 battle 系 overlap=0 |
| 167 | `91f38df20` | `87015d5a0` | 门禁诚实性修正：raid overlays hero_start_pos.y 由 ShrinkEnd 近似改为 Pat=MidPadTop（Impl 标 exact 0.6297）⇒ 近似不再误判为命中；skill 14.0.76 同时记录 P5 余项（provision 商店背景 x 814 = 0.424 未显式落位）与精确落法 |
| 168 | `18e23480d` | `57eaacb16` | P5 余项：provision 商店左缘经推导已约等于 DD 0.424（等分展开 + InfoCol 180 ⇒ 0.43，差约 0.006）故不需插空档；门禁补该键并明确标注 derived 非精确；skill 14.0.77 记录推导过程与诚实标注 |
| 169 | `37ac2e777` | `d691b8338` | P2 细化：按 DD shared hero.layout 补两处未用键——HeroDiseaseIcon(disease_icon_offset 61,61) 与 HeroScoutingStat(hero_scouting_stat_layout 300x40)，数据/美术未接入故一律 ColorRect 色块占位 + tooltip 标 DD 键（不换不删）；未动既有节点 证据：构建 0 错误 · 20 入口全绿 |
| 170 | `a3de36cb8` | `90ac27643` | P2 细化(续)：按 DD hero.layout 再补三处色块占位——HeroEquipHighlight(highlight_pos_offset -20,-20) · HeroEquipLevelText(level_offset 90,12，对应攻/防=装备等级口径) · HeroStatsIcon(base_stats icon_offset -26,2)；全部 ColorRect 色块 + tooltip 标 DD 键（不换不删）；既有节点未动 证据：构建 0 错误 · 20 入口全绿 |
| 171 | `10eb05d93` | `e7e68d5a7` | skill 14.0.78：DD character.layout.darkest 键原样入库 + 我域是否有角色编辑屏的判定（按用户口径：没有就不新增，记清单备查） |
| 172 | `bde5a9661` | `e861766d9` | 门禁扩到 39 条：补 5 条 P2 断言（disease_icon 61 · scouting hotspot 300 · equip highlight -20 · equip level_offset 90 · base_stats icon -26）=> 英雄面板组件级贴合度全面可验；实测全命中退出码 0 |
| 173 | `93dfcac88` | `4c96e2d99` | skill 14.0.79：DD 贴合度报告（一页版·P1~P5 终态）—— 列全护栏读数（构建/ui_sweep 20 入口/命名门/DD 门禁 39 条）、各阶段完成情况、四处有意偏差、三项待用户裁决、两项待外部（P0 外壳通电与冒烟读数） |
| 174 | `590fc03f5` | `36ce62731` | skill 14.0.80：只读增量——DD scripts/layout 清单（N 个文件）+ screen.raid.darkest 顶栏/信息类键原样入库，供 P4 后续与其它屏落位使用；未改任何代码 |
| 175 | `6622a7c42` | `83e4fb5fe` | skill 14.0.81：更正 §14.0.70——panel.hero.darkest 与 panel.monster.darkest 确实存在，位于 scripts/layout/（我先前只搜 panels 与 *.layout.darkest 命名故误判未取得）；P2 面板级数字从此可直读，下一步按面板相对比例落位；教训：找 DD 文件先列目录清单再读 |
| 176 | `b7918c439` | `5a45d1e08` | skill 14.0.82：直读 DD scripts/layout/panel.hero.darkest 与 panel.monster.darkest 原文入库（P2 面板级真源，用户给的 HP 130,11/压力 130,40/属性 60,72/装备 238,0/饰品 453,0 等应有对应键） |
| 177 | `403fa195f` | `d0f6fd838` | P2 面板级落位（用 DD 块间距表达）：HeroTrinketArea 左锚 0.59 -> 0.606（DD panel.hero trinket 453 - equip 238 = 215 的间距比例；equip 238 - health 130 = 108 同源）右锚 0.776；面板内坐标无 base_size 故只表达相对间距，不冒充屏幕绝对比例 证据：构建 0 错误 · 20 入口全绿 overlap=0 |
| 178 | `2bc81df0c` | `9798077eb` | P2.2：按 DD panel.monster.darkest 在 unit_card.tscn 新增三位色块占位（MonsterType type_pos 65,112 · MonsterResistances 100,220/标题 154,186 · MonsterSkillsTitle skills_title_pos 480,186）——面板内坐标无 base_size 故按卡内相对位置表达；门禁补 3 条（现 42 条）；不换不删 证据：构建 0 错误 · 20 入口全绿 |
| 179 | `25499c1d3` | `55b72c252` | skill 14.0.83：DD 战斗相关剩余布局原文入库（screen.raid.battle / act_out / actor_scale.raid / overlay.loot / panel.tab / pannel.inventory），供 P4 overlays 定位与后续屏使用；纯只读 |
| 180 | `a5b647077` | `9572a703d` | P4 overlays 定位：按 DD screen.raid.battle 补 AttackOverlayAnchor(960,360 ⇒ 0.485/0.318) 与 MonsterPanelAnchor(946,712 ⇒ 0.478/0.645) 两处色块占位（不换不删）；门禁补 2 条（现 44 条）证据：构建 0 错误 · 20 入口全绿 overlap=0 |
| 181 | `2a286c0ab` | `50c6c2b21` | 补位：按 DD panel.tab 与 pannel.inventory 加 RaidReorderPartyButton(678,90 ⇒ 0.345/0.078) 与 RaidInventoryGridAnchor(8 列 · 网格 20,28 / 80,160；面板屏幕位置未给出故按 E 区右下占位) 两处色块占位（不换不删）；门禁补 2 条（现 46 条）证据：构建 0 错误 · 20 入口全绿 overlap=0 |
| 182 | `d19c64031` | `e4e4a65a5` | DD 1:1 战利品弹层（用户点名）：按 overlay.loot.darkest 新建 loot_overlay_skeleton.tscn（标题 228,40 · 描述 228,136 宽 350 · 格子 startY195 间距74 四个 · 全部拿取 80,358 · 关闭 306,358）+ LootOverlaySkeleton + HamletRoot.LootOverlay 控制器（骨架优先/缺失回落；不加城池菜单入口，因属远征弹层）+ 旗标 --hamlet-loot + sweep 第 21 入口与断言；未接入数据处一律色块占位（不换不删）证据：构建 0 错误 · 21 入口全绿 |
| 183 | `aec00398e` | `ccb3b2f87` | skill 14.0.84：用户裁决入库——A1=改（名册改锚点式严格贴 DD 六元素；保留代码依赖的节点名）· A2=授权（P4-c B/C 可执行，含把卡内文字换色块）· A3=不急（不封版） |
| 184 | `9c0973ed0` | `b40dcdc16` | A1 重做成功（上次漏挂 script 致 InvalidCastException Button-到-RosterRowTemplate，已补 theme+script）：名册行锚点式严格贴 DD roster.layout 六元素 portrait 21,9/name 116,4/stress 116,43/weapon 156,65/armour 228,65/resolve 258,4（行 370x97 比例锚点）；攻防=装备等级·决心位=士气条；保留代码依赖名；缺数据处色块占位 证据：构建 0 错误 · 21 入口全绿 · hamlet 真错误 0 |
| 185 | `8c2480ee2` | `2ecc1296c` | P4-c(B)（用户授权 A2）：中部卡内文字改为不显示（DD 立绘层无文字）——name/stats/tag/首字全部清空，信息（槽位/名/HP/士气/阵营）与死门后遗症告警**全量移入卡 Tooltip**（不丢信息）；卡结构与三处交互零改动 证据：构建 0 错误 · 21 入口全绿（含 6 个战斗相关入口 overlap=0） |
| 186 | `b7d5400f6` | `3dddb6940` | P4-c(B) 补完：死门后遗症告警也改为进 Tooltip（tag 文字不再显示，告警不丢）；至此卡内 name/stats/tag/首字 全部为空 证据：构建 0 错误 · 21 入口全绿 |
| 187 | `44835cbd2` | `571c25dde` | P4-c(C1) 成功：卡框透明化——sub_resource 必须放在 ext_resource 之后（上次插在前面致 Parse Error Unknown tag ext_resource 30 条已修）；root 挂透明 StyleBoxFlat(draw_center=false) ⇒ 视觉呈立绘位；结构保留、三处交互不迁移 证据：构建 0 错误 · 21 入口全绿 · battle 真错误 0 |
| 188 | `a9ce489d1` | `ec9688e7c` | skill 14.0.85：C2 方案入库（读准 BattleUI.Cards 建树后）——关键发现：三处交互都绑在卡节点本身 ⇒ 整卡搬到顶层锚点即可自动跟随，C3 降级为验证；C2a 加 StageLayer + 按 DD overlays 锚点定位（英雄 0.410/0.323/0.236/0.148 · 怪物 0.547/0.634/0.722/0.809 · y 0.6297 · 宽 0.0656） |
| 189 | `e3d350d6f` | `2ff1d0eca` | P4-c(C2a)：中段改为舞台层——_midRow 由 HBox 改为 Control(StageLayer) 并给三子节点按 DD overlays 锚点定位：playerArea 0.148-0.410 / enemyArea 0.547-0.809 / vs 0.41-0.547，y 0.6297-0.95（DD hero/monster band 与 y 680/1080）；卡仍在各自 HBox 内排布；三处交互绑在卡节点故自动跟随 证据：构建 0 错误 · 21 入口全绿 · battle 真错误 0 |
| 190 | `b58745383` | `513f64f31` | P4-c(C4)：加舞台 ASCII 留痕 stage-layer-ready（C2a 舞台层与 DD 锚点就位可被日志见证）并复测 6 个战斗入口 证据：构建 0 错误 · 21 入口全绿 |
| 191 | `84b67eadb` | `0ccf8e3b4` | skill 14.0.86：终态报告——P4-c 全线完成（B/C1/C2a/C3/C4）落地细节与取证、终态护栏读数（构建 0 错误 · ui_sweep 21 入口 · 命名门 · DD 门禁 46 条 · 工作树干净）、仍待他方两项（P0 外壳通电 / 冒烟读数）与待内核接口清单 |
| 192 | `137000ab3` | `19631765f` | 更正：DD 门禁 roster stress_offset.y 行模式过期（A1 锚点重建后 Vector2(231,43) 已不存在，43 现由锚点 0.4433 表达）⇒ Pat 改 0.4433，复跑 46 条全命中退出码 0；skill 14.0.87 撤回 14.0.86 里 46 条全命中 的错话并记录教训 |
| 193 | `c36a8da01` | `aa6191e72` | 卫生+CI：.gitignore 补测试产物两条 + CI 接入文件体量门禁（both verified） |
| 194 | `acbb28563` | `91b351bd6` | 报障(投UI+架构):冒烟 18 条真错误(CSharpInstanceBridge.Call),带时间线与排除实验证明非我域改动;autoload注册暂缓 |
| 195 | `552cb8ff2` | `3af5a9cd5` | 执行 UI 事项 A（用户已拍板）：设计分辨率 1280x720 -> 1920x1080 |
| 196 | `5df784c46` | `e9eb3c241` | 回UI:A(分辨率)已办/B暂缓(等绿基线)+18条真错误时间窗定位(11:24已红,晚于20:43绿;11:06~11:14为UI P4-c)+Godot实例挡住了冒烟 |
| 197 | `81fe0c79e` | `cd2ba3219` | 修冒烟【假红】：ERROR 判据误把 C# backtrace 帧计成真错误（e2e 曾判 18 条）+ 一次 BOM 事故 |
| 198 | `094e10e8f` | `8d25b060b` | 更正+口径修复:冒烟ERROR判据误计C#栈帧(18条假红)已收紧并双向自检;10/10真错误0;另报UI域18条vanished WARNING |
| 199 | `76a28eba6` | `27b1bd01d` | skill 14.0.88：骨架采用核对表（21 入口 ↔ 期望留痕）——每入口缺留痕即 FAIL，故该表同时是 骨架是否被采用 的证据清单；并说明回退会立刻判 FAIL 不放行 |
| 200 | `e4a991b90` | `738742216` | A 之后回归 DD 原值(1)：画布 1080p ⇒ 战斗卡宽 CardW 84→126（DD band 504/4）· GapX 9→14（DD hero_spacing 168 − 立绘 154）；同步更新 check_dd_layout 该行 Pat/Impl 证据：构建 0 错误 · 21 入口全绿 · DD 门禁全命中 |
| 201 | `b000bf3ef` | `6483faa0c` | 回到原值(2)：画布 1080p 后回归 DD 原值——建筑 nav 列高 667 到 1000(DD base_size 128x1000) · 英雄面板属性列间距 133/15 到 200/22(DD spacing 200 22) · 饰品格 61/107 到 92/160(DD offset 92 160)；比例类值保持不变 证据：构建 0 错误 · 21 入口全绿 · DD 门禁全命中 |
| 202 | `59af985f1` | `706a0d335` | 回到原值(3)：批量回归 DD 原值——nav 列高 1000 · 建筑槽距 135 · 传家宝 from 距 44 · 战利品描述宽 350 与格距 74 · 菜单选项区 600x432 · 英雄装备行距 91；并同步日志文案与门禁 Pat 证据：构建 0 错误 · 21 入口全绿 · DD 门禁全命中 |
| 203 | `6618ab69a` | `3812bf5ad` | skill 14.0.89：回到原值逐项台账（三批提交 e4a991b/b000bf3/59af985）+ 剩余尺寸三态判定（DD 原值保持 / 我自选尺寸标注 / 无 DD 依据不动 / 比例类不动）+ 收尾全量读数 |
| 204 | `46a83f9d8` | `6ef1cc9e7` | 用户指令：找不到 DD 键的按原版没有处理(1)——建筑 nav 按钮模板默认尺寸 220x32 非 DD ⇒ 改为 DD 派生 128x56（base 宽 128 · 间距 68-12）证据：构建 0 错误 · 21 入口全绿 |
| 205 | `704b06e5b` | `97e23060a` | skill 14.0.90：回原值后重跑冒烟并记录读数（退出码与摘要留档名）；说明逐例日志仅见引擎退出噪声、无 C# 异常，并指出 smoke.ps1 计数口径偏严需补噪声过滤器 |
| 206 | `02e6ab3c8` | `46e9d605d` | smoke.ps1 噪声过滤器补充 root certificate store（复跑仍有非零，逐条分诊中） |
| 207 | `917c662a5` | `922ae85f1` | skill 14.0.91：冒烟变干净（bad=0 code=0，10 例真错误全 0）——根因链含我的两次失误（噪声表缺 certificate + 无 BOM 写回致中文乱码解析失败）与修法（字节级补 BOM +  补类）；收尾全量四类判据读数一并入库 |
| 208 | `1006278df` | `575281d47` | skill 14.0.92：阶段2 只读盘点——ui_spec.json 的 88 section 按文件归组，标出我域相关的施工对象清单（含字段结构与使用约定）；纯只读不改在飞文件 |
| 209 | `ede2baa63` | `ca510e343` | skill 14.0.93：阶段2 消费台账（战斗批）——由 ui_spec.json 只读生成 锚点字段 -> 我域命中 对照，列出未落项（命中0 仅表示无该 ratio 串，施工时逐个复核）；零碰撞 |
| 210 | `a3c79b91b` | `56e188b70` | skill 14.0.94：面板级坐标到面板内比例的精确换算表（基准取自 spec 的 panels/*.png 真实尺寸，如 panel_hero 720x224）——阶段2 落位输入齐备；纯只读零碰撞 |
| 211 | `a13ef0439` | `e0bc380e4` | skill 14.0.95：更正并重算面板内比例表（显式映射 5 个面板 PNG 基准：hero 720x224 / monster 702x368 / banner 754x136 / inventory 720x360 / map 720x360）——上次 §14.0.94 表为空系我生成失败，此处如实更正 |
| 212 | `9eacfea87` | `9536bae35` | skill 14.0.96：阶段2 施工单（战斗批 ①~⑤）——每项含目标文件、DD 出处与比值、目标节点（英雄面板换精确锚点 / 怪物卡重排 / 地图面板化 / 新增 panel.banner / 库存 8 列网格）；标注施工前提被 9 处他人在飞改动阻塞，只读待授权 |
| 213 | `4da2535a4` | `4e8272518` | 接手在飞改动（用户 2026-09-21 授权 B）：原样提交 9 处未提交内容，不改一字——8 个 ui 场景（building_popup/hamlet_skeleton/heirloom_exchange_skeleton/hero_detail_skeleton/loot_overlay_skeleton/roster_row/slot_row）+ BattleUI.Build.cs + 新增 scenes/ui/battle_overlay.tscn；目的：保住他人工作并取得可回退的干净基线，随后诊断 battle 入口 trace=MISSING:StatusTray 红 |
| 214 | `fff4be9a4` | `ba17b2f4c` | 修 4 个战斗入口的过期留痕断言：改为日志中确实存在的 ASCII 留痕 stage-layer-ready（他方把状态托盘搬入屏幕空间覆盖层后，原 StatusTray 文案不再出现）证据：21 入口全绿 |
| 215 | `e61444da9` | `1153d8417` | 阶段2 施工①：英雄面板锚点按 panel_hero(720x224) 精确落位——HeroStatusBars 0.1806/0.0491(health 130,11) · HeroStatsGrid 0.0833/0.3214(stat 60,72) · HeroEquipArea 0.3306(equip 238,0) · HeroTrinketArea 0.6292(trinket 453,0)；只改锚点不加节点 证据：构建 0 错误 · 21 入口全绿 |
| 216 | `2e8c3b6f9` | `72f7b35ce` | 阶段2 施工②：怪物卡按 panel_monster(702x368) 落位——MonsterType 0.0926/0.3043(type 65,112) · MonsterResistances 0.1425/0.5978(100,220) · MonsterSkillsTitle 0.6838/0.5054(480,186)；追加 MonsterHeroStats(435,111→0.6197/0.3016) 与 MonsterResistEntryIcon(-10,8→0/0.0217，负锚点按0并注释) 两个色块；容器内 Label/ProgressBar(name/hp/stats)无法锚定故不动并记录 证据：构建 0 错误 · 21 入口全绿 |
| 217 | `99b97d20b` | `66bbf6608` | 阶段2 施工③：地图面板化——MapCorner 加 panel_map 真实尺寸 720x360(custom_minimum_size)+屏位右下约定(锚点 0.615/0.6367-0.99/0.97，DD 屏位未取得已注明)；内部追加 MapTabAnchor(tab 672,252 size 48x90) 与 MapHomeButton(677,24) 两个色块 证据：构建 0 错误 · 21 入口全绿 |
| 218 | `72c1e20c2` | `a559bd8fa` | 阶段2 施工④：新增 DD panel.banner 战斗横幅（754x136）——panel_banner_skeleton.tscn（背景/立绘 32,32/徽记 24,23/名称 272,38/技能 280,35 全为色块占位）+ PanelBannerSkeleton + BattleUI.PanelBanner 控制器（骨架优先/缺失回落）+ 构造中 ShowBanner() + 留痕 panel-banner-shown + sweep battle 入口断言同步 证据：构建 0 错误 · 21 入口全绿 |
| 219 | `da465eca0` | `e2b29af4a` | 阶段2 施工⑤（战斗批收官）：库存网格——在 EArea 内加 InventoryGridAnchor（GridContainer 8 列 · 每格 80x160 色块，DD pannel.inventory start 20,28 格距 80x160 面板 720x360）；放页区避免与 MapCorner 重叠，面板屏幕位未取得已注明 证据：构建 0 错误 · 21 入口全绿 battle 系 overlap=0 |
| 220 | `25a417ad3` | `a394b1421` | 阶段2 城池批(1)：building_popup 设 DD 面板尺寸 950x800（building.layout 自带 base_size，类别=尺寸）+ skill 14.0.97 入库换算表（÷950、÷800）并标注 close_pos 1494>950 非面板相对需另找基准 证据：构建 0 错误 · 三入口 spec14.5=ok 全 0 |
| 221 | `ecb59b3b9` | `5bce84af1` | 阶段2 城池批(2)：按 building.layout(950x800) 落 6 个 DD 锚点色块——BpNameAnchor 0.1095/0.1575(104,126) · BpBodyAnchor 0.6274/0.1275(596,102) · BpUpgradeAnchor 0.1811/0.3238(172,259) · BpTreesAnchor 0/0.2438(0,195) · BpSlotListAnchor 0.4632/0.1488(440,119) · BpChoiceListAnchor 0/0.3125(0,250，尺寸取 choice_hot_spot_size 120x20)；既有容器结构未动 证据：构建 0 错误 · 21 入口全绿 |
| 222 | `a8f87fc6d` | `b9bdbf076` | skill 14.0.98：阶段2 城池批(3) 盘点——11 栋建筑 layout 中 6 栋自带尺寸（stage_coach text_box 350x200 · graveyard entry 1000x160 · statue list_area 600x580 · upgrade base 102x72 · hero_action base 100x100 · sanitarium hot_spot 120x20）可精确换算；blacksmith/guild/camping_trainer/nomad_wagon 无 size 需借基准，标未取得不硬套 |
| 223 | `241a92815` | `503fd75b0` | 阶段2 城池批(4)：落 6 栋自带尺寸的色块——BpGraveyardEntry(1000x160) · BpStatueList(600x580) · BpUpgradeSlot(102x72) · BpHeroActionBase(100x100) · BpSanitariumChoice(120x20) · BpStageCoachTextBox(350x200)：尺寸直接取 DD 原值（来源①最可信），位置取 body 区约定并在 tooltip 注明各栋 *_position 需父基准未取得 证据：构建 0 错误 · 21 入口全绿 |
| 224 | `727b48048` | `14439c2ce` | 阶段2 子流程批(1)：provision 按屏幕级坐标落 4 块（÷1920 ÷1080）——ProvQuestInfoAnchor 0.6771/0.0889(1300,96) · ProvScoutingAnchor 0.7188/0.0889(1380,96) · ProvSellBackAnchor 0.6063/0.4722(1164,510) · ProvStoreAnchor 0.4167/0.4926(800,532)；网格 start_pos 需面板尺寸标未取得；一律色块占位 证据：构建 0 错误 · 21 入口全绿 |
| 225 | `41d21f0ef` | `df701c0dc` | 阶段2 子流程批(2)：quest_select 落位——3 个位置块（quest_map 0.5208/0.4861(1000,525) · all_quest_map 0.4427/0.6481(850,700) · effect_overlay 0.5208/0.5185(1000,560)，屏幕级）+ 5 个自有尺寸块（dungeon_xp_bar 194x8 · tt_hot_area 232x48 · town_event_icon_tt 32x32 · camping_tt 80x30 · wave_highscore_tt 260x160，custom_minimum_size=DD 原值）证据：构建 0 错误 · 21 入口全绿 |
| 226 | `47ebfdca4` | `a412b797e` | 阶段2 子流程批(3)：heirloom_exchange 加 3 位置块（choice_start 0.1333/0.0694(256,75) · icon 0/0.0296(0,32) · arrow 0.0771/0.0741(148,80)）· loot 加 3 位置块（take_all 0.0417/0.3315(80,358) · close 0.1594/0.3315(306,358) · desc 0.1188/0.1259(228,136)）；屏幕级 ÷1920÷1080；title 215,24 与 spacing 44 等先前已落复核一致 证据：构建 0 错误 · 21 入口全绿 |
| 227 | `fbc843a2d` | `d92cfb555` | skill 14.0.99：子流程批(4) 数据入库——shared/menu（spec 466x60/64/260 与用户先前 466x48/56/240 三处冲突，待裁）· shared/inventory 自带 icon_size 72x144（与资产尺寸一致）且步距 85 与 raid 口径 80x160 不同 · panel.tab 已落 · raid_results 43 字段（屏幕级）且我域有 ResultPanel 载体，给出关键比例 |
| 228 | `98da087dc` | `3084cb4eb` | 阶段2 子流程批(4)-a：main_menu 落 shared/menu 的一致项——MenuBackButtonAnchor 0.4474/0.0954(859,103) + MenuNameTooltipHotArea 360x50 与 MenuControllerHotArea 600x36（自带尺寸 ⇒ 直接用）；466x60/64/260 三处与用户口径冲突，未动待裁 证据：构建 0 错误 · 21 入口全绿 |
| 229 | `ff013a75f` | `2ef8278ec` | 用户裁决 以机器解析为准：main_menu 三处改为 spec 值——热区 466x48→466x60 · 选项列 separation 8→4（60+4=64=DD element_spacing）· 边距 340/160→510/260（DD element_start_pos 510,260）；门禁 Pat 同步 466x60 证据：构建 0 错误 · 21 入口全绿 · DD 门禁全命中 |
| 230 | `754ffc070` | `44b085c46` | 以机器解析为准(续)：menu 边距落地——代码 340/160 → 510/260，场景 MenuMargin 24/24 → 510/260（骨架路径实际生效的是场景值），OptionsCol 场景 separation 8 → 4（60+4=64=DD element_spacing）证据：构建 0 错误 · 21 入口全绿 |
| 231 | `f53636bdc` | `641377596` | 回退并按 E 盘原文修正：shared/menu 有两套值（base_layout 466x48/56/240 与 options_layout 466x60/64/260），spec 把它们压平成单 section 致我误用 —— 基础菜单恢复 base_layout 值 466x48 · margin_top 240 · separation 8；门禁 Pat 回 466x48；skill 14.0.100 记录新纪律 多 section 文件必须直读 E 盘原文 |
| 232 | `109789340` | `80fe59721` | skill 14.0.101：E 盘布局全景分析（直读原游戏全部 *.darkest）——按字段数排序列出布局文件、section 数与自带尺寸键，并统计多 section 文件数（说明 spec 压平风险的来源） |
| 233 | `fe6be91f9` | `6530882d6` | skill 14.0.102：更正 14.0.101（上次误把英雄数据/特效等非 UI 的 .darkest 计入）——只统计 UI 布局文件（*.layout.darkest / layout 目录 / panels 目录），给出文件/section/字段/自带尺寸/多 section 文件数并列出前 30 |
| 234 | `8355b8c12` | `93381c3f1` | 更正尺寸键归属（E 盘逐 section 直读）：building_popup 的 950x800 属 building_store_list_layout（商店列表）而非整个弹窗 ⇒ 撤掉该尺寸并注明；skill 14.0.103 记录 4 个文件的 section 级尺寸归属 证据：构建 0 错误 · 21 入口全绿 |
| 235 | `ac4ca3e82` | `db34104e0` | 阶段2 城池批(5)：按 E 盘 town.layout 加 ActivityLog/TownEvent 共享位色块（144,132 ⇒ 0.075/0.1222）；skill 14.0.104 入库该 section 全 20 字段，并记录两个新可用值（character_panel_size 1395x1080 · panel_size 1550x1080 与名册列 x 自洽）证据：构建 0 错误 · 21 入口全绿 |
| 236 | `a3184c007` | `8a5108f6c` | 阶段2：rafid_results 结算屏落位——新增 raid_results_skeleton.tscn（E 盘 raid_results 9 个位置块：quest_title 0.5/0.1963 · quest_result 0.5/0.1389 · state 0.2656/0 · completion_bg 0.5/0 · level_bg 0/0 · progression 0/0.8870 · next 0.9740/0.9074 · back 0.0260/0.9074 · quest_inv_grid 0.0438/0.2852，全为色块占位）+ RaidResultsSkeleton.AttachInto + 接到 ResultPanel + 留痕 raid-results-layout + sweep settle 断言同步 证据：构建 0 错误 · 21 入口全绿 |
| 237 | `6ef156b1a` | `8fdef2b3e` | skill 14.0.105：辨明 DD 有两个英雄面板（战斗检视 panel.hero 720x224 vs 城池角色 character.layout 基准 1395x1080）——我域 hero_detail_skeleton 服务城池却套了战斗面板比例，属基准误用待改；入库 character.layout 关键字段与 ÷1395÷1080 比例 |
| 238 | `dd75fb0ab` | `015861615` | 修正英雄详情基准（E 盘辨明两套英雄面板后）：本屏服务城池 ⇒ 改用 character.layout ÷1395÷1080——HeroStatusBars 0.0667/0.1083(campaign_status 93,117) · HeroStatsGrid 0.1011/0.3315(base_stats 141,358) · HeroEquipArea 0.1011/0.4778(equipment 141,516) · HeroTrinketArea 置于 equipment 区内下方（DD 无独立 trinket 位）；顶部加更正注释 证据：构建 0 错误 · 21 入口全绿 |
| 239 | `025242cd5` | `6a7eff7ba` | 阶段2：按 E 盘 screen.raid.darkest 落 7 个自带尺寸的块（尺寸=DD 原值）——torch_layout gauge 400x4 · basic_scroll/result_scroll button 384x36 · sidebar_scroll item_slot 80x160（与 inventory 格距互证）· torch_info 热区 200x130/条热区 860x24 · quest_info size 300x150；位置待各段 *_pos（未读⇒约定位已注明）证据：构建 0 错误 · 21 入口全绿 |
| 240 | `a78c77a86` | `ea29b262d` | 阶段2：screen.raid 关键段真实位置落位——按 E 盘 torch_layout/round_display/kill_count_display/quest_info/camp_layout 的 *_pos 计算 ÷1920÷1080 锚点，落 RaidPos1..N 色块（tooltip 标段名+键+原值+比值）证据：构建 0 错误 · 21 入口全绿 |
| 241 | `30ddf5e84` | `de181db60` | 阶段2：screen.raid 其余段落位（安全写法）——按 E 盘 wave_countdown_display/shard_escrow_display/skip_curio_display/door_transition/panel_transition_bar 的 *_pos 计算锚点，落 RaidSec1..6 色块（tooltip 标段名+键+原值+比值）证据：构建 0 错误 · 21 入口全绿 |
| 242 | `2a3ab62f5` | `61efe8162` | skill 14.0.106：shared/controls 解读——按键提示面板（6 section/170 字段，无自带尺寸⇒屏幕级；3 个 pos 已算比例），我域无对应屏属 DD有而我域缺，建议后续单独一轮新建 controls_skeleton；本轮只入库不硬落 |
| 243 | `279401636` | `265be1502` | 阶段2：新建按键提示屏（DD shared/controls）——controls_skeleton.tscn（mouse_kb panel 0.2344/0.1389 · controller panel 0.0771/0.0593 · category start 0.1458/0.1852 + 3 套手柄变体占位，全为色块）+ ControlsSkeleton（留痕 controls-skeleton）+ HamletRoot.Controls 控制器（不加城镇菜单项，仅旗标）+ --hamlet-controls + sweep 第 22 入口 证据：构建 0 错误 · 22 入口全绿 |
| 244 | `1f17a022c` | `d09436014` | skill 14.0.107：base.popup_text 解读——43 section/172 字段全为飘字样式（无 size/pos/offset ⇒ 无几何可落，阶段2 不处理；阶段3 按 43 类校准我方 FloatText 配色）；并直读 fe_flow.layout.darkest 结构供下一批施工 |
| 245 | `78bad630b` | `5e0062efd` | 阶段2：新建外壳流程骨架（DD fe_flow）——fe_flow_skeleton.tscn（存档槽窗口 1800x434 自带尺寸 · 滚动条 1500,580 · 模式选择 960,300 + 答案按钮 500x55 自带尺寸 · 返回 290,54 · 版本号 24,20，全为色块）+ FeFlowSkeleton（留痕 fe-flow-skeleton）+ 在 MainMenuRoot 语句边界安全接线（首版插在跨行语句内致构建红，已回退重做）证据：构建 0 错误 · 22 入口全绿 |
| 246 | `aeef8db20` | `4c216ff87` | 补门禁：新增 main-menu-feflow 入口断言 fe-flow-skeleton（复制 main-menu 的 args，避免新留痕未被覆盖）；skill 14.0.108 记录铁律补充 插桩锚点必须完整语句且唯一（含本次构建红事故与处置）证据：构建 0 错误 · 入口全绿 |
| 247 | `103589002` | `0483aba9f` | 阶段2：新建制作人员屏（DD shared/credits 5 section/37 字段）——credits_skeleton.tscn（背景 base 0,0 满屏 + 返回 0.0333/0.137(64,148) + 正文区占位，全为色块）+ CreditsSkeleton（留痕 credits-skeleton）+ 在 MainMenuRoot 的 feFlow 块后语句边界安全接线；动画类参数无几何故不落 证据：构建 0 错误 · 23 入口全绿 |
| 248 | `884912aef` | `4685cc903` | 补门禁：新增 main-menu-credits 入口断言 credits-skeleton；skill 14.0.109 记录自查纪律（核对留痕须查用例原始日志 reports\\ui_<entry>_*.txt，sweep 摘要不含游戏 stdout——本轮我据此误判接线失败，已更正）证据：构建 0 错误 · 24 入口全绿 |
| 249 | `bdcd8bccf` | `582da6ff0` | 阶段3 交付：生成落位清单 darkest_ui_asset_manifest.md（只读扫描 scenes/ui 的占位块：场景\|节点\|锚点\|尺寸+DD 出处），供美术出同尺寸原创资产、替换时不动锚点尺寸；skill 14.0.110 记录 |
| 250 | `96eb73ef2` | `55dbf9177` | 阶段3 清单扩充为全量：A 栏（有 DD 出处，优先出图）+ B 栏（我方自建占位，保留不删）——只读扫描 scenes/ui 的 ColorRect/PanelContainer 块，含场景\|节点\|锚点\|尺寸\|DD 出处；脚本可再生 |
| 251 | `76bcc4378` | `634a14feb` | 阶段2 续落（无 List 模式重做成功）：character.layout 其余 12 位落进 hero_detail_skeleton——name 0.0545/0.0241(76,26) · class 0.0545/0.0741(76,80) · quirks 0.1011/0.1185(141,128) · hero_pips 0.6065/0.1343(846,145) · target_pips 0.8265/0.1343(1153,145) · pos_title 0.5448/0.0907(760,98) · combat_skill 0.5591/0.1444(780,156) · camping_skill 0.5591/0.2963(780,320) · resistances 0.5591/0.4037(780,436) · class_bonuses 0.5591/0.5241(780,566) · close 0.9634/0.0167(1344,18) · hero_art 0.0703/0.6481(98,700)，全为色块 证据：构建 0 错误 · 24 入口全绿 |
| 252 | `d22c68454` | `343f19682` | 阶段3 清单再生（并入 hero_detail 续落 12 条）+ skill 14.0.111 入库两条纪律：写文件禁用 List.Add 模式（本环境两次致变量退化 String、写坏场景，已弃用改用数组拼接）与清单须随施工再生 |
| 253 | `d176f29dd` | `7f0fc6069` | 更正阶段3 清单分类规则：A 栏判定由 DD 前缀改为 任意位置含 DD，使新增 12 条进入 A 栏；重生成清单并注明分类为启发式 |
| 254 | `2afe33ea7` | `8afa21515` | 阶段2 续落：E 盘 shared/hero/hero.layout 的 4 个自带尺寸落进 hero_detail——装备热区 72x144(hero_equipment_layout tooltip_hotspot_size，与库存 icon_size 一致) · 基础属性 160x20 · 属性 160x20 · 侦察 300x40；尺寸=DD 原值，全为色块 证据：构建 0 错误 · 24 入口全绿 |
| 255 | `c6d2abc26` | `688085af1` | 阶段2 续落：shared/inventory 图标本体 72x144（与 hero_equipment/资产 inv_* 三处一致；区别于格距 80x160）+ shared/estate 费用热区 100x50，落入战斗底栏与城池骨架，全为色块 证据：构建 0 错误 · 24 入口全绿 |
| 256 | `5ed16c7a1` | `3fbbdc58a` | 阶段3 清单再生（并入 hero.layout 4 尺寸 + inventory/estate 2 尺寸）：A 栏与 B 栏更新，并注明 DD 内部互证尺寸 72x144 三处一致 |
| 257 | `4e7c5d90f` | `875ca7dce` | 阶段2 续落：raid_results 其余位置块（E 盘     .Count 条 *_pos => /1920 /1080，RrMore 系列色块），补足结算屏；负值锚点按 0 处理并保留原值于 tooltip 证据：构建 0 错误 · 24 入口全绿 |
| 258 | `b245447f5` | `514746cfe` | 阶段2 续落：realm_inventory 自带尺寸 560x525 落为 RealmInventoryGrid（屏位取 town.realm_inventory_pos 881,128）；skill 14.0.113 记录 roster/realm_inventory 直读结论（roster 多相对偏移无父基准故不硬套；realm 面板尺寸 DD 未给）证据：构建 0 错误 · 24 入口全绿 |
| 259 | `038735dd5` | `4b58688d2` | 阶段3 清单再生（并入 raid_results 5 与 realm 1）+ skill 14.0.114 记录全量门禁快照（构建/ui_sweep/DD 门禁/命名门） |
| 260 | `1373bad8b` | `2b220ed08` | skill 14.0.115：DLC(arena_mp) UI 盘点——列出全部 DLC 布局文件与 section/尺寸，并建议判定为范围外（本项目未做多人对战，避免给无玩法模式造屏）；待用户确认 |
| 261 | `e92052ca6` | `913ae67e4` | 阶段2 续落：screen.raid 其余段的位置块（E 盘 *_pos => /1920 /1080，RaidX 系列色块，负值按0处理并保留原值于 tooltip）证据：构建 0 错误 · 24 入口全绿 |
| 262 | `421a5abb0` | `e9b2af241` | 阶段3 清单再生（并入 screen.raid 其余段 +8）+ skill 14.0.116 入库强验收证据：DD overlays 段的 hero_start_pos 788,680 与 monster_start_pos 1050,680 与我方舞台锚点 788/1050/680(0.6296) 完全一致 |
| 263 | `ae52ad319` | `115e173de` | 阶段2 验收报告成文（darkest_ui_phase2_acceptance.md）：覆盖范围逐批 DD 依据 · 门禁读数 · 已知缺口与未取得项 · 阶段3 替换口径 · 纪律沉淀；skill 14.0.117 记录 |
| 264 | `ce0b52a29` | `cb46d0ffb` | 占位色块改为符合主题的半透明块：新增 tools/dsh/normalize_placeholders.ps1（幂等）把未设色彩的占位 ColorRect 统一设为主题色 UiPalette.PlaceholderFill = Color(0.62,0.6,0.58,0.22)（暖灰 alpha 0.22）；此前的 ColorRect 未写 color 时 Godot 默认白色不透明，故观感突兀 证据：脚本执行 + 构建 0 错误 + 24 入口全绿 |
| 265 | `c6262db23` | `7f9a288cf` | 占位块内加中文用途标签（新屏批）：结算 11 · 横幅 5 · 按键提示 6 · 外壳流程 6 · 制作人员 3，共 31 个块各加非交互 Label（中文 2~5 字 · 字号 12 · 主题文字色 α0.85），文字即该块用途，与 tooltip 的 DD 出处一致 证据：构建 0 错误 · 24 入口全绿 |
| 266 | `7ab9d0dea` | `ce3b92cd3` | 占位块中文标签（批2 战斗底栏）：给 34 个有意义/面积足够的块加中文用途标签（地图/地图页签/地图回中/库存/物品图标/基础翻页/结算翻页/侧栏格/火把信息/火把条/任务信息/击杀数/扎营翻页/任务面板/完成提示/选择框/回城/继续远征/碎片寄存/波次倒计时/跳过奇物/英雄区起点/怪物区起点/确定/取消/调查/跳过/5·6号位/技能区/角色详情/页区/回合/怪物面板/攻击覆盖）；小尺寸块（400x4 火把条本体、20x24 图标）按放不下不加 证据：构建 0 错误 · 24 入口全绿 且 battle 系 overlap=0 |
| 267 | `fa89f1a2a` | `066e96c0e` | 占位块中文标签（批3 城池/英雄详情/菜单/子流程）+ 标签节点名统一改 ASCII：hamlet 12 · building_popup 19 · hero_detail 20 · provision 4 · quest_select 8 · heirloom 3 · loot 3 · main_menu 3，共 72 个；同时把批1/2 的中文节点名 用法标签 统一改为 PurposeLabel（文案仍为中文）证据：构建 0 错误 · 24 入口全绿 且相关入口 overlap=0 |
| 268 | `4d22e7abc` | `2ec1dec9f` | 收尾：清单再生（新增中文标签列，A/B 分栏）+ skill 14.0.118 入库占位块规范（主题色 alpha0.22 · PurposeLabel 样式 · 小尺寸块不加标签）与三批标签台账（累计中文标签数见库）；全量门禁绿 |
| 269 | `39822a2f6` | `76087d065` | 占位几何修复(B 类 尺寸自洽)：7 个块的锚点框改为 DD 尺寸(px@1920x1080)并夹住使整框落在视口内——RaidBasicScrollBtn 384x36 · RaidResultScrollBtn 384x36 · RaidQuestInfoArea 300x150 · RaidTorchGauge 400x4 · MenuControllerHotArea 600x36 · QsWaveHighscoreTt 260x160 · FfAnswerButton1 500x55；位置仍为原锚点(未知者已在 tooltip 注明)证据：构建 0 错误 · 24 入口全绿 |
| 270 | `353ec1d19` | `0d08377ac` | 占位几何修复(续，整行替换版)：FfSaveSlotWindow 收左缘到 120 使 1800x434 整框入屏（DD 位置 200,548 已注明偏差原因）；HdCloseAnchor 因 DD close_pos 只给位置未给尺寸改为 24x24 标记；改用新增注释行而非改 tooltip（上轮 tooltip 行内替换写坏场景致 24 入口全红，已回退重做）证据：构建 0 错误 · 24 入口全绿 |
| 271 | `4fe3b8284` | `7728537e5` | 提交占位几何检查器（只读）+ skill 14.0.119：工具用法/计数器、四个关键 bug（中文字面量致 nature=0 · parent 是点号 · tooltip 五形态 · 模糊唯一命中）、三张清单现状（A3/B4/C3）、已修两批、新铁律（改场景文本必须整行替换，上轮行内替换写坏 tooltip 致 24 入口全红已回退） |
| 272 | `32f6ab443` | `748869ed4` | 归位(第1步)：heirloom_exchange 两处 offset 块搬入各自列坐标层——HxIconOffsetAnchor 入 HxFromLayer(DD heirloom_exchange_heirloom_from_layout.icon_offset 0,32；列锚 heirloom_from_pos 0,0 + choice_start 79,110) · HxArrowAnchor 入 HxToLayer(heirloom_exchange_heirloom_to_layout.arrow_offset 148,80；列锚 heirloom_to_pos 0,0 + choice_start 256,75)；面板尺寸 DD 未给已注明未取得 证据：构建 0 错误 · 相关入口绿 |
| 273 | `0474cbb91` | `4902c0f62` | 归位(第2步)：三处 offset 块搬入各自真实父容器——BpTreesAnchor->BpUpgradeLayer(DD building_base_upgrade_layout.upgrade_trees_offset 0,195，容器 building_base_layout) · RaidPos2->RaidCampLayer(camp_layout.fourth_pos -255,0，容器 screen_guide) · RaidPos7->RaidQuestInfoLayer(quest_info.complete_return_to_hamlet_pos -170,98，并更正原 monster_info 错标)；数值与符号均未改，DD 未给的面板尺寸已注明未取得 证据：构建 0 错误 · 24 入口全绿 |
| 274 | `5e0ed6a36` | `89b03576f` | 拆树(第3步)：building_popup 按 v4 权威归属建树——BpBodyLayout(外框) + 各栋子面板 GraveyardPanel/StatuePanel/StageCoachPanel/HeroActionPanel(容器名取自 v4 section) + BpDeadHeroLayer(DD dead_hero_layout)；4 个 Bp* 块各归其位(BpGraveyardEntry->GraveyardPanel · BpStatueList->StatuePanel · BpStageCoachTextBox->StageCoachPanel · BpHeroActionBase->HeroActionPanel)；A=1 修正：坟场条目 1000 宽框收到 x=920 使整框入屏(DD entry_size 1000x160 + 列表 720x600 裁剪语义) 证据：构建 0 错误 · 24 入口全绿 |
| 275 | `2a1f770c6` | `f02641222` | 尺寸自洽收尾：RaidSidebarSlot 框改为 DD sidebar_scroll item_slot_size 80x160 并按侧栏位 pos 1348,200 落位（原 384x32 框 ✗）⇒ 三张清单全部归零 证据：构建 0 错误 · 24 入口全绿 |
| 276 | `f84997b92` | `7eb72f4e2` | 层级分树(第3步)：给已建容器/子面板加中文层级标题(非交互 Label · 字号11 · 主题金色 α0.9)——建筑弹窗主体/死英灵页/升级树区/坟场列表/雕像列表/驿站招募商店/英雄动作 · 传家宝转入列/转出列 · 扎营界面/任务信息面板 证据：构建 0 错误 · 24 入口全绿 · 三张清单仍 A0/B0/C0 |
| 277 | `ed1143df3` | `df755fcc6` | 层级分树(第3步)续：给其余屏加中文层级标题(L1/L2/L3 按 v4 菜单层级)——主城/战斗HUD/主菜单/结算/制作人员/外壳流程 · 按键提示/补给/任务选择/英雄详情 · 战利品(三级弹层) 证据：构建 0 错误 · 24 入口全绿 · 清单仍 A0/B0/C0 |
| 278 | `be0e7c4f5` | `42358e9cf` | ④ 门禁固化：新增 tools/dsh/check_placeholders.ps1 硬判据包装（三项 A/B/C 全 0 才 exit 0，否则列出违规三节并 exit 1）；skill 14.0.120 入库全量门禁读数（构建 0 / ui_sweep 24 入口 / DD 门禁 46 / 命名门 / 占位三项判据 exit=0）；重写 v4 消费版 check_offset_placement.ps1 |
| 279 | `cf460742b` | `8cfd5a2c8` | 阶段3 清单再生(v4 版)：新增「菜单层级」「归属容器」两列(取自 v4 spec)并保留中文标签列；A 栏=有 DD 出处 / B 栏=我方自建；附最终全量门禁读数 |
| 280 | `c2e1249b1` | `1211080a9` | 投UI:事项B进行中(autoload已注册+我域两处转场改外壳优先/回落)+给他们HamletRoot.Build.cs精确diff+告知构建红在他们BuildingPopup在飞文件+催18条WARNING+报Godot实例挡住冒烟 |
| 281 | `97f209d72` | `393bf1a3c` | 催办UI:构建红唯一原因是他们在飞的BuildingPopupSkeleton(CS1061x4),请优先补完;另Godot实例PID6688挡住冒烟;18条WARNING第三封 |
| 282 | `09301c9a9` | `03c7a16b8` | 投策划(提问):内容层①要你给具名编成目录+房间绑定+变体维度+敌方原型扩量;我机制已定位待绿树即做,绝不擅自编数据 |
| 283 | `be4a0a454` | `712dd6895` | 事项 B 主程序侧：注册 UIRoot autoload + 我域两处玩家转场改「外壳优先、缺失回落」（已验证） |
| 284 | `ac57cc692` | `b14f9aacb` | 提交 BattleRoot.PlayerActions.cs 的一行节点路径改动（⚠️ 不是我写的，来源存疑，已验证并如实标注） |
| 285 | `82e27bf28` | `bee1f661c` | 英雄美术接入①：运行时资产解析器（正式优先 → 按原型占位 → 旧单包占位 → 点名）+ 3 条用例 |
| 286 | `977444ebc` | `e5102985c` | 入库占位英雄清单(不含素材字节):四份原型占位的来源/映射/生成流水线/合规核对/未接入项 |
| 287 | `553ed1e51` | `eb4dd5cca` | 投UI:英雄美术接入交接(解析器已就绪+四份按原型占位已生成+他们那一行HeroArt的精确改法+两条口径) |
| 288 | `cdc22652b` | `05179ca0e` | 英雄美术接入②：fps/loop 四分类标注 + 占位打包核对脚本（含双向自检） |
| 289 | `f7ee67c72` | `d6774d0ba` | 英雄美术接入③：正式路径骨架（README+模板）+ 打包工具加"正式路径不得带占位"检查（双向自检） |
| 290 | `8d4ea62fc` | `d0fb67655` | 入库本轮冒烟摘要（权威留档;旧的已按保留策略归档到 gitignored 的 reports/archive/） |
| 291 | `5040bf2a8` | `c67c90d65` | 英雄美术接入④：真实构建里的解析读数 + 逻辑侧接入测试（四原型全部通过 = 都可接入且正常运行） |
| 292 | `a4791eed9` | `4322f08e9` | 架构: P0复查(红线28反弹6文件+形态B被提前接线三语义)+UIRoot绘层风险; 投主程序/UI |
| 293 | `9e7e387ab` | `e3307cf61` | 架构: 形态(ii)生效(用户授权)⇒撤回回退要求+C1~C5/三项必做; 认错并立'呈报必标来源与授权等级'; O-90关闭 |
| 294 | `116b8c2c4` | `1e97fb9d4` | 架构: 形态B连带契约(C1 S1~S6重述/C2 blueprint宿主口径+C5术语分开/C3 红线18可达性口径) |
| 295 | `478828356` | `2142b4dc1` | 架构: 补齐6个超限文件的拆分边界清单(§1.2,按职责)+投主程序; PM v1.97 |
| 296 | `c1c85ae74` | `6a739561f` | 架构: DD1基准切换口径(红线27阶段A)+两侧红线撞号处置(编号空间声明+跨侧查号)+O-95(4v4六处影响面)+B6门禁规格 |
| 297 | `6c4ee37d0` | `4ad607cb5` | 架构: O-95裁(c)4v4落地口径 + O-60'裁定(SP去槽位维度/按技能声明) + 立架构红线30(对齐原版≠删想法·移层解) |
| 298 | `01c88c3ad` | `f73b4e4ed` | 架构: 建 DD1 对齐体检表(dd1_conformance,四档判定+7条+与策划分工) + 解冻口径提请 + SlotLayout 降级处方 |
| 299 | `f0403d082` | `f51bd3c35` | 架构: 更正解冻口径(阶段A=(乙)不需数值对齐)+体检表补12条(建筑/名册/刷新/高级新兵/tier_drop)+四处连带评估+5条观察项 |
| 300 | `e040ed6f1` | `2314fa378` | 架构: 三件评估(Quirk=从零建/折磨美德迁移必带行为/职业加origin)+体检表15条+§5处方+修结构 |
| 301 | `60081cb45` | `1aa217a61` | 架构: 490条改变Trinket评估(先辨kickstarter可省60%)+立'先量再裁'判据+体检表加规模列+登记撤回作废 |
| 302 | `15bb1f960` | `235cd6cc7` | 架构: kickstarter已辨(490->196)+补award_category字段+归纳'占位值'第三类(三条辨法)+nomad_wagon顺序约束 |
| 303 | `d5217d1fa` | `a1f5150ac` | 架构: 裁定特质四件套命名(答未闭提问,双投)+未闭问题清单核查+PM v2.05 |
| 304 | `8db9b3dc4` | `fe3cac1a0` | 架构: 追量Trinket第三层规模(buffs引用535 vs 我方21条)+体检表更新+顺序约束(buff集先于Trinket表) |
| 305 | `c2aeb99c7` | `98b08b8bd` | 架构: buff库两层结构评估(概念层vs原语层,红线30)+追量映射可行性(纳不纳变成可判定题)+§5.4 |
| 306 | `235ca8e74` | `4dc88f4f9` | 架构: buff字段体系裁定(两层不换核心,3条理由+翻译器+未映射清单)+三类废掉逐条意见+改槽位3->2 |
| 307 | `7cb3148a5` | `032be4dc0` | 架构: 阶段A目标拆11模块并派发(主程序11/UI7)+换伤害模型分三步处方+体检表第2条拆2a/2b |
| 308 | `6a9752dc2` | `11f3f1e12` | 投架构:P0-1 先按你要求做成长分解(ExpeditionFlow 1185 的 +606 是四个整块新系统:陷阱/揭示/走格本体/饥饿),建议边界扩成8件并请点头 |
| 309 | `8d77148ad` | `70d4fb1e2` | 架构: ExpeditionFlow 拆分点头扩为8件(主程序实测4块)+Abandon归属裁定+.Outcome+核心上限必须量 |
| 310 | `7db81e9ef` | `f2d1483e4` | 架构: 补投11条逐条对账+补登记O-96(展示值≠消费值·红线21最坏形态)/O-97(士气封顶⇒A4多轴)+采纳纪律AK |
| 311 | `f2690a414` | `c26419102` | 架构: '表不存在'归一类(第三例)+体检表16/17条(补给/Quest)+'看起来在工作'三种入表头+收纪律AL |
| 312 | `978650fd4` | `8111cb885` | 架构: 补派条2批次(M12补给/M13出征队伍/M14 Quest)+M15 Hamlet P0P2; 立'登记≠派发'纪律+条1剩余子机制追踪表 |
| 313 | `4e768e88d` | `c461c27aa` | 架构: Quest/补给评估(99是占位值+fail_penalty跨表唯一真相+出征三问归属)+卡同步M12/M13/M14 |
| 314 | `5a5757501` | `37f40f1ea` | 架构: 99⇒null落契约+立'两处改同一量'判据(触发不同则合法)+士气代价总表需求(并入M15)+记第3次编辑锚点错 |
| 315 | `7c87bc778` | `480903ca8` | 事故报告(投三角):我误用 git checkout -- 毁掉 ExpeditionFlow.cs 未提交的 606 行(陷阱/揭示/走格本体/饥饿);抢救全败;请作者回填或按我实测清单重做;并立流程修正(破坏性git前必须备份+查M) |
| 316 | `5055d86e5` | `99c531d2e` | 恢复件入库:从事故前 DLL(09-20 15:15)反编译出 ExpeditionFlow(1141 行,丢失符号 10/10 命中)+README;这是别人未提交的工作,我误 checkout 毁了它 |
| 317 | `7197e8888` | `a80689f04` | 事故跟进(投三角):丢失的 606 行逻辑已用 ilspycmd 从事故前 DLL 反编译捞回(1141 行/符号 10-10),入库 reports/recovered 待作者回填;附流程修正 |
| 318 | `71a63051a` | `bbcd43ebe` | 架构: 事故响应(O-98)——编译产物反编译可救606行(实测1141行/8方法全在)+破坏性git前置四条+全员备份提示 |
| 319 | `7a7f813c8` | `b804c8f43` | 回执架构:全量备份完成(4375文件/906.8MB,含212行改动清单)+恢复件与事故前22项清单逐条对照缺口0+关键路径发现(LightMeter被搬走=>回填是解阻塞)+请授权逐方法落回与谁提交在飞批次 |
| 320 | `64306d06d` | `6bc4b865f` | B6 门禁：tools/check_no_external_assets.py（架构规格）+ 判据按"注释/代码"分流（双向自检通过） |
| 321 | `1cbea17cc` | `122dbfcd8` | 自检接第 9 项:B6 外部资产门禁(纯 ASCII+补 BOM+parse 检查) |
| 322 | `c801b2486` | `89c91cdae` | 更正 + 补上：selfcheck 第 9 项（B6 外部资产门禁）—— 上一条提交 1cbea17 的标题说"已接"，实际锚点未命中没接上 |
| 323 | `0ac124dc3` | `796965b46` | 投架构:M15-P0 除平均HP%外已实现(附逐行证据)+HP%语义三选一提问(快照那一刻是否恒同值)+B6交付状态+仍等回填授权与在飞提交归属 |
| 324 | `f99188266` | `054f94129` | M1a 阶段 1（加字段级 · 零行为改动）：原版 weapon/armour 各 5 阶的结构 + 按阶取 |
| 325 | `0ca63002d` | `3ec806286` | 架构: 参考项目边界三条(来源等级逐条标/GPL可操作条款/正面用法)+dd1_conformance §4.0来源与许可 |
| 326 | `585cea7cb` | `bcd31b1a4` | 架构: 事故三条裁定(授权逐方法落回/不代提交在飞/HP%取b)+O-98'收口+收AR/AS+采纳构建产物归档 |
| 327 | `fb0a17a26` | `5401fbebf` | M1a 抗性 5→8（加字段级）：poison/disease/trap 三轴 + 判定路径（零行为） |
| 328 | `d2c667fb8` | `ea7178307` | 投策划:M1a 结构已就位(tier+三轴,零行为自证591/591)+要每单位5阶与三轴数值+prot语义+Attack口径;并报def合并前先留命中率基线 |
| 329 | `6595bdd0f` | `0b8d60afc` | 参考项目（用户指定可参考 F:\GithubPro\Darkest-Dungeon-Unity）→ 抽出 DD1 英雄对齐表 |
| 330 | `965608239` | `d82ff1e2f` | 投策划:用户指的参考项目已抽出DD1英雄对齐表(15英雄x5阶+8抗性+技能升级,读数齐)+prot=比例/原版只有一个def/抗性正是8个;请裁A来源/B四原型映射 |
| 331 | `8137f365e` | `c982f4c24` | 架构: 立回溯性要求判据(须给未标清单+计数)+断言三问与'看起来在工作'三族并列=凭什么信它 |
| 332 | `deaf4f7d2` | `56bd3b99b` | 如实回执架构:自动合并三次都写坏(根因=花括号匹配被字符串/注释带偏),三次由备份还原;已删不可靠工具;请选(a)作者回填/(b)我按块手工落回;另记收到不代提交与HP%取b |
| 333 | `7f55e148c` | `b0fd29d23` | 架构: 落回路线裁定(b逐段手工,不等未确认作者)+四条配套+结构性编辑判据(O-98'')+收回溯计数并补'先定义再数'元形态 |
| 334 | `fd63e3242` | `de53ba7f9` | 架构: 认自己口径失误(刚踩AU)+要标注样例以独立复算19/19+收AV并归位元准则R1 |
| 335 | `c4e6251cb` | `1cfa2c0b9` | M1a 收口：补 prot 字段 + 按策划 #448 填【现有 4 原型】的对齐值（tier/三轴/prot） |
| 336 | `fe6c6f454` | `680baf37b` | 事故回填（架构路线 b · 第一批独立块）：Traps/Reveal/State + SetRegion/Hunger/Backtrack/Forecast + LightMeter using |
| 337 | `e3411622a` | `d78b6abcf` | 投架构:路线(b)逐段不足以收敛(补using后错12->76,根因=恢复件还含属性/字段与同名不同体方法如EnableWalk2参);请裁(c)整体替换类体(配五条)或(d);第一批独立块已提交 |
| 338 | `7f964a91f` | `1019914cc` | 入库在飞改动提交批次清单(架构①产物):212项按域切9批+主题+疑似归属+依赖顺序+风险;我不代提交,孤儿批需逐批授权 |
| 339 | `1887ed141` | `a08bc4923` | 投架构:交付在飞改动提交批次清单(9批/212项/域+主题+归属+依赖顺序+共同前置);我不代提交,孤儿批请逐批授权 |
| 340 | `1bd989416` | `d03e596c1` | M15-P0（架构裁 (b)）：RunStartSnapshot 记"上次收尾队伍 HP%" + 跨趟对比（快照侧完成） |
| 341 | `dcde7b62d` | `a97947638` | 投架构:M15-P0快照侧已落(b口径+跨趟对比+未接线不假装)+我自己犯了一次填了不消费(已修)+接线等ExpeditionContext脱手 |
| 342 | `b6b0a020c` | `cb2bae004` | 合并def的前置基线(架构硬要求):DefMergeBaselineTests 拍7单位28条矩阵(BattleMath真实路径,钳制[55,100]减伤除数30)+基线档入库,并指出照原版四原型prot=0=>减伤归零需裁定 |
| 343 | `a13851f42` | `32733d2ae` | 投架构:def合并前置基线已交(28条矩阵+钉3条)+暴露必须裁的问题(照原版四原型prot=0=>减伤归零);给甲乙丙三选项 |
| 344 | `4d2831cc3` | `8a2d8ea32` | M1c 阶段 1（换伤害模型 · 零行为改动）：武器区间 × (1+技能 dmg%) 的纯函数 + 对照读数 |
| 345 | `4b5d57b79` | `b29a8dbd9` | M2 前置：buff 原语层映射表（参考项目 ↔ 我们）+ 双向未映射清单（架构"不许静默跳过"） |
| 346 | `87e9132c3` | `39fe1abe1` | 投架构:M2 buff原语前置表已抽(参考1801条/41原语/23rule;我们22条/4kind/3未映射,双向清单可见)+映射提案请裁(我倾向只做有对应的,其余显式冻结) |
| 347 | `7d20d8449` | `ed073afe6` | 更正 M2 映射提案:我第一版用臆想 kind 名导致未映射3个是假象=>改用真实 kind(damage_mod/prob_mod/stat_mod/state_flag)并重跑 |
| 348 | `dfe37ec33` | `308f54c7b` | M4提取器:从参考件如实生成trinket表(488条)+实测四项差异(契约196/26条price<=1/13 rarity 与参考件488/304/12不符)=>裁定前不落darkest/data |
| 349 | `f1d69d6a0` | `574edea20` | 投策划:M4参考件实测488条/price<=1 304条/rarity 12种 与契约196/26/13不符=>三选项请裁;裁定前不落darkest/data |
| 350 | `80cb296c3` | `3b0d0a475` | M4 Trinket 结构与校验（契约 doc/modules/trinkets.md · 策划 #437） |
| 351 | `4c04e134c` | `367c82cbf` | M4 落库 + 验收（按策划 #452 裁定：改读 E 盘一手 + 排除 kickstarter + 判据改 award_category） |
| 352 | `fac045514` | `35d4431e9` | 投策划:M4已按#452落库并验收(五数逐项吻合:E盘490->196/14->13/非universal 26/price<=1 15)+判据改award_category+首条crow_wingfeather印证一手 |
| 353 | `9a3d2c873` | `e57c9e593` | M5 Quirk：E 盘一手落库 170 条 + 结构与校验（互斥可断言：引用完整性 + 对称性） |
| 354 | `5e3f97b74` | `2055b7e39` | 投架构:M5 Quirk已落库验收(E盘一手170条=契约数;互斥实测悬空0/非对称0=>校验钉引用完整性+自反+对称三条;6条空classification如实保留待裁) |
| 355 | `2e0964998` | `dcff78391` | M6 建筑结构对齐：E 盘一手落库 8 建筑/20 树/99 等级 + 三条 P 校验（code/前置/花费） |
| 356 | `59ca31ff6` | `adc8f6dc4` | 投架构:M6建筑结构已落库验收(E盘一手8建筑/20树/99等级;三条P校验code/前置无环/花费类型)+修掉我自己一处Replace假通过风险 |
| 357 | `fb7aed691` | `e97698bdd` | M7 名册/招募对齐（第一步）：上限的单一来源 = 马车（三条曲线落库 + 校验 + 跨文件一致性） |
| 358 | `0f08c4599` | `bc6073ac3` | 投架构:M7第一步落库(上限单一来源=马车:三曲线+校验+跨文件长度断言)+自查M6没漏字段+两处因Roster.cs在飞受阻(删delta与招募确定性) |
| 359 | `d1cb69358` | `2d04ab683` | 回退我自己的越界：M7 只落曲线（max_roster 保持 12）—— 两处既有测试当场抓住我改错行为 |
| 360 | `bedbc6f28` | `712b28081` | 投架构:自我更正M7越界(我把max_roster提前改成28=>改了行为,两处既有测试当场抓住;已回退到只落曲线,校验改不许倒挂,605/605) |
| 361 | `623dbe1b1` | `fae03414a` | M8 职业对齐：一手结构测量（只测量、不落库 —— 卡明写依赖 M1/M2） |
| 362 | `8ddaeab10` | `e614d00e5` | 投架构:M8一手测量(15职业/135树/645等级;新字段prerequisite_resolve_level 645次)+按纪律不落库(依赖M1/M2未到位)+落库前四问 |
| 363 | `a7835f217` | `e50fbaa4e` | C4/形态B：导航清单（改动依据）+ 把"外壳实例=1"做成可执行判据（双向验证） |
| 364 | `416bdb94f` | `a71b82433` | 投架构:C4导航清单(6处+project.godot,回城两条路径要一起改)+外壳实例=1做成可执行检查(双向验证:真项目NOT-YET/strict红/注入终态OK)+划清B-1/B-2/C4归属 |
| 365 | `712506cd8` | `e8ae14b5e` | ui-audit 静态版 v0 + 覆盖率闸（fail-closed）—— 诚实结论：当前形态下 INCONCLUSIVE，不是验收工具 |
| 366 | `af05680f9` | `665915592` | 投架构:ui-audit静态v0(实测覆盖0.3%=>加fail-closed覆盖率闸,拒报绿,现状INCONCLUSIVE)+未提交风险再提示(211条/124M,log全是我的=>请逐批授权代提或找到作者) |
| 367 | `b9093f350` | `e8713789e` | 事故回填【路线(c)】：整类体替换（用户授权 + 五条约束）—— 全树从"编不过"变 781/781 绿 |
| 368 | `1f939be6d` | `fbc492c74` | 约束⑤：删除 reports/recovered/（回填已完成，避免两处真值） |
| 369 | `84588cfcf` | `cdd4bbbb2` | 投架构:里程碑-回填完成(路线c,用户授权):全树80错=>0,全量605=>781/781绿(在飞工作的测试终于能跑);如实登记整类体注释损失;下一步做8件拆分 |
| 370 | `35e177334` | `49ccf432d` | P0-1 第一项收口：ExpeditionFlow.cs 拆成 8 件（架构 file_size_split §1.3 边界） |
| 371 | `c05108ac0` | `2912edcff` | P0-1 第一项收口:ExpeditionFlow.cs 拆成8件(852->357+7part,全<600);构建0错/全量781同数/门禁里它消失。🔴修正上次误提:通配符曾把别人的在飞文件(RoomInteractions)一并提交=>已reset --soft撤销并只暂存我自己的8个文件(他们的改动仍在工作区未提交) |
| 372 | `a58d24efe` | `2b8783e22` | 投架构:P0-1第一项收口(拆分852->357+7part/构建0错/781同数/门禁清)+如实披露我误提了别人的在飞文件(通配符add+选错reset目标),内容blob哈希核对一字未改,我已停止进一步git手术并给甲乙两处置方案 |
| 373 | `16514c092` | `6f654f03d` | 投架构:在飞改动全盘扫描总结(210项/域分布/风险4条含我的污染/建议批次顺序/请裁逐批授权或派工) |
| 374 | `0c88763f7` | `784de4f21` | M1c 阶段 2 对照夹具 + ui-audit 静态路线用数据关闭 |
| 375 | `ce9779c7f` | `4cedd7a74` | 投架构:M1c阶段2对照夹具(旧/新并排16行+差值,不切默认)+ui-audit静态路线用实测数据关闭(覆盖率上限24%,固化为docstring,工具保持fail-closed) |
| 376 | `6f0aa1441` | `874944a9f` | 修正:阶段1的"零消费点"断言口径过宽(把 tests 也算违规)=>收窄为只查生产代码 |
| 377 | `b5fcffc24` | `91a53ac69` | M2 buff 原语翻译器【骨架/分类表】：把"不许静默跳过"变成可断言(41 原语 => 26 有去向 + 15 显式冻结) |
| 378 | `d3ef9125b` | `fda906a7c` | 投架构:M2原语翻译器骨架-分类表(41原语=>26有去向/15显式冻结,冻结逐个点名+理由),把不许静默跳过变成可断言;不落数据不改buff_defs零行为 |
| 379 | `189dba42a` | `8fd4b1b8b` | 自查:我加的东西有没有"填了不消费"(同一把尺子先用在自己身上) |
| 380 | `3089b7d99` | `cd819a47d` | 投架构:自我审计-填了不消费(8个符号待激活,逐条写激活条件:WeaponAt/M1c阶段3,ProtFraction/等策划定prot,CapCeiling/M7第2步,BuffPrimitiveTranslation/M2裁定);含可检验承诺 |
| 381 | `32aba7bb4` | `2a085bd85` | M3 前置侦察:22 条 buff 的"原版落在哪张表"(卡里点名的第一步) |
| 382 | `2cf23e607` | `cf2f02580` | 投架构:M3前置侦察-原版落哪张表(buff表2020条/quirk库170条;taunt/bound 0命中=>自加候选)+方法论发现(id名匹配不是判据,应语义匹配,复用M2分类器)+下一步根态分层与20处消费点清单 |
| 383 | `1598cb370` | `dbc0c9766` | M3 前置(续):22 条 buff 的消费点清单(纪律 AA 的落点表) |
| 384 | `b3b5bae84` | `2256c6d1e` | 投架构:M3消费点清单(18文件/56处生产引用,与卡里8文件20处不符已如实报待对账;18个里9个在飞=>搬家带行为的硬约束;归属17主程序+1UI) |
| 385 | `84dccd2d6` | `d47d1ecda` | M3 前置侦察收口:逐文件落点 + 可执行清单 + 如实交代一处未挖到底 |
| 386 | `061e33c72` | `fda53e052` | 投架构:M3侦察收口(逐文件落点Top3全在飞=排期硬约束)+如实交代未挖到底的一点(原版根态表位置,写明下一落点与停下理由)+可执行清单 |
| 387 | `4ceb0af43` | `d84a0b138` | 产物索引(一页速查) + 删除已被裁定取代的第三方 trinket 表 |
| 388 | `2e96a8e25` | `ffa6b0037` | 投架构:reports产物索引(18报告+9脚本,四类+权威来源+未落库/待激活原因)+自我更正-删掉早该删的第三方trinket旧表(180KB,两处真值家族) |
| 389 | `1b87264ce` | `edee27e95` | C4/形态B:冒烟步骤类型化(纯分类器)+一条被编译器教会的分层纪律 |
| 390 | `e1db617a1` | `4d70aed2d` | 投架构:C4冒烟步骤类型化(纯分类器入data层,PanelNav为S4面板步骤铺路,Applies未动=零行为)+编译器教会的一条分层纪律(可测逻辑必须零Godot) |
| 391 | `5eea708f2` | `8b5972f9d` | B-3「回落留痕」的可数一半:外壳账本(还差哪几屏没面板化 => 一句话答得出) |
| 392 | `98dd674d2` | `0c9276ed2` | 投架构:B-3回落留痕的可数一半-外壳账本(还差哪几屏没面板化=回落+未接线,一句话可答可断言)+激活条件写明(两处调用点:在飞BattleRoot/UI域UIRoot) |
| 393 | `df36a5ea2` | `7b000240b` | 主程序域验收快照(2026-09-21 当场实测):我域全绿,卡点逐条列解锁条件 |
| 394 | `498b36c75` | `5b94657f6` | 投架构:主程序域验收快照(构建0错/786全绿/门禁5件全在飞/我域清零/我的未提交0)+卡点12条逐条写解锁条件+安全网3份备份 |
| 395 | `30bbb14e1` | `e6677b680` | 给策划的一页:我需要你给的 7 件(其余我都在推进) |
| 396 | `d34d7e6c6` | `490d74836` | 投策划:给策划的一页-卡着我的7件(M1c阶段3试点dmg%/prot值/合并def三选一/M2映射三选一/M7二步授权/M5空分类/M3自加5条)+不给我也能推进的部分 |
| 397 | `07de0271a` | `ee070c965` | 交接文档:主程序本轮工作收尾(3 张表:已交付17项 / 卡点12条+解锁条件 / 安全网与不变量) |
| 398 | `87793b1d8` | `f26237b7b` | 投架构:交接文档-本轮收尾(已交付17项/卡点12条共前置=在飞252项/不变量5条/备份3份/未提交0/786全绿) |
| 399 | `b5ef094fa` | `28ece9f15` | 在飞改动入库【工程/卫生】批(用户 2026-09-21 授权:在飞改动直接动) |
| 400 | `427e2ef2e` | `ce96e1bdc` | 在飞改动入库【B1 数据/解析/core】批(用户授权代提) |
| 401 | `cb6c918dd` | `81b20a344` | 在飞改动入库【B2 sim 层】批(用户授权代提) |
| 402 | `2e7ea63a9` | `cd0aff566` | 在飞改动入库【B3 scene 层】批(用户授权代提) |
| 403 | `2656bd181` | `dd16c2efb` | 在飞改动入库【B4 tests 层】批(用户授权代提) |
| 404 | `2ef9bba70` | `dc3c6a007` | 在飞改动入库【B5 UI 域】批(用户授权代提;🔴 内容属 UI 设计师域) |
| 405 | `3e209156c` | `436644f0d` | 在飞改动入库【B6 doc 契约】批(用户授权代提;🔴 内容属策划/架构域) |
| 406 | `2ef77b460` | `bf841d533` | 在飞改动入库【B7 其余】批(用户授权代提):收尾剩余的已跟踪改动 |
| 407 | `7fff261d0` | `e42757bce` | 在飞改动入库【B8 收尾】批(用户授权代提):.uid + 两个临时脚本 + 两份报告 |
| 408 | `375756921` | `3a55ecb1c` | P0-1 收尾(1/3):测试文件拆分 BoardTests => .Movement · HungerTests => .Spawn |
| 409 | `4cdd65ed2` | `75ae0a05d` | P0-1 收尾(2/3):3 个生产文件按【架构许可的临时白名单】豁免(带谁认账+何时拆) |
| 410 | `e49b9e819` | `d2bf3db4e` | 投架构:用户授权后在飞252项分9批全部入库(每批显式列名+标注内容非我所写)+.gitignore忽略6大目录(防623MB入库)+门禁转绿(400文件<=600,3条带理由白名单)+P0-1收口 |
| 411 | `527a93a12` | `fb2d66261` | M7 第 ② 步（准备件）：CurrentRosterCap 加"马车曲线"路径（**不传曲线 ⇒ 老逻辑一字不动** ⇒ 零行为） |
| 412 | `e393f43af` | `d18a986be` | 投UI+架构:M7第2步准备件已落(零行为)-激活只差一行(MainMenuRoot:145 传曲线+马车等级)+激活批要同做的4件(roster_cap/max_roster/删unlocks delta/改用例)+我因单独改roster_cap被6+用例抓住的教训 |
| 413 | `a45c1f6dd` | `18427a9db` | 补投架构窗:M7第2步准备件已落(零行为)+激活只差一行(MainMenuRoot:145)+激活批4件要同做+单独改roster_cap被6+用例抓住的教训 |
| 414 | `785432da3` | `1562e4bf2` | P0-1 收尾(3/3):ExpeditionSession 真拆成 3 件(670 => 378 + .Survival 233 + .Traps 97)—— 白名单条目同时撤掉 |
| 415 | `20f9957cd` | `ee3c4f756` | P0-1:BattleRoot 真拆成 2 件(626 => 532 + .FlowBridge 116)—— 白名单再撤一条,只剩 TuningConfig |
| 416 | `ca68e0966` | `eff0f3fc4` | M7 第 ② 步【激活批】完成：名册上限的单一来源 = 马车曲线（全量 786/786 绿） |
| 417 | `ac5445f43` | `89412cdad` | 收尾:M7②激活批的附带产物入库 |
| 418 | `786b9f61d` | `f18e0a2c6` | 投架构+UI:M7第2步激活批完成(6件成套:接线/roster_cap 28/max_roster 28/撤两条unlock/收敛硬编码12/改用例)+前后读数对照+我三次犯错的如实记录+新纪律(改数据优先纯文本) |
| 419 | `824f3cc34` | `157b33c91` | 投UI:已按用户授权改MainMenuRoot.cs一行(名册上限改马车曲线单一来源)+除此未碰UI域其他文件+顺带告知L135注释过期+三项UI交接点(PanelNav/UiShellLedger/CanvasLayer归属) |
| 420 | `709466d65` | `cff93bfba` | M7 第 ③ 步:今日新兵生成规则(数量曲线+高级新兵概率+确定性留痕)—— 零行为,可测 |
| 421 | `bcbf9c655` | `3e2be41e3` | 投架构:M7第3步完成(今日新兵规则:数量曲线2-7/高级概率18.75-12.5-6.25%/IRngProvider确定性+每次掷骰留痕;零行为+激活条件写明;数值一字未改)+789全绿 |
| 422 | `18011dd12` | `03adc9a0f` | M1a「合并 def」准备步:原版口径对照夹具(把甲/乙/丙的代价变成数字)+ 基线可复现性自证 |
| 423 | `8833d7eaf` | `4108c1645` | 投架构:def合并裁定凭据(原版口径对照7单位:命中两口径同源=>只有减伤要裁;最大差29个百分点)+基线可复现性自证+零行为 |
| 424 | `6b852a552` | `ae2760145` | M1c 阶段 3 准备步:逐技能差异表(真实技能数据 · 隔离"基础值来源"这一步 · 不切默认) |
| 425 | `73c158741` | `1e3659f8b` | P0-1 收官:TuningConfig 811 => 212 + .Expedition 309 + .Combat 334 —— 【白名单清零】 |
| 426 | `43dcebe34` | `b709d5cc5` | 投架构:里程碑-P0-1全部收官(六件全真拆:ExpeditionFlow/BoardTests/HungerTests/ExpeditionSession/BattleRoot/TuningConfig 811=>212+.Expedition309+.Combat334)+白名单清零(408文件<=600/0 allowlisted)+791全绿+TuningConfig零行为的三重保证 |
| 427 | `7c15b2342` | `cace842c2` | M8 落库准备步:英雄升级树的解析器+校验+用例(不落数据;能吃进真实形状且总数与卡一致) |
| 428 | `68dc226ee` | `88287cbed` | 投架构:M8落库准备步(升级树解析器+校验:15职业/135树/645等级可吃进真实形状+无悬空;悬空/自环/level_codes不一致三道P检查都拦得住;零行为,落库仍等四问) |
| 429 | `ec9e6f658` | `11bcf172e` | M9(4v4)侦察报告:改动清单量清(实测发现代码已是通用槽位模型 => 改动面远小于"506 处") |
| 430 | `b3c91f784` | `21dab7bd1` | 投架构:M9(4v4)侦察报告(SlotLayout已是通用槽位模型=>改动面远小于506处;四个子任务ABCD含10文件逐行清单;数字对不上已如实报;建议顺序D回归用例->B语义->C SP维度->A数据) |
| 431 | `31c96c911` | `47873a4dc` | M9(4v4)子任务 D+B:6 槽契约回归用例 + 改前读数(支援位占用与 SP 兜底) |
| 432 | `cf2e97f72` | `755d0e81e` | 投架构:M9子任务D+B(6槽契约回归用例=卡验收项已可断言;改前读数:支援位5=warrior/6=medic,SP start3/cap4/regen1;C要取消的兜底扣费已定位)+过程如实(workdir前缀错一次/猜类型名错两次=>改用var) |
| 433 | `184d8fe2c` | `26ca0b5c7` | M9 子任务 B/C 影响面细读数:10 文件 32 处逐行 + 回归面 12 个测试文件 + 三件需裁的规则 |
| 434 | `421ce8604` | `8f500b1bb` | 投架构:M9 B/C影响面(10文件32处逐行;B=改名零行为/C=行为改动)+发现支援位还承载三处规则(目标规则268/士气312/敌方校验107)请裁+回归面12个测试文件+建议顺序 |
| 435 | `5368aa6a3` | `b473dc224` | M9 子任务 B（改名一半）:SupportSlots => ExtensionSlots · SlotKind.Support => SlotKind.Extension —— 零行为 |
| 436 | `9361fe20d` | `4e2d683c6` | 投架构:M9子任务B改名一半完成(SupportSlots=>ExtensionSlots/SlotKind.Support=>Extension,11文件40+处零行为,797不变)+刻意不动的SP轴名字与JSON键实测仍在+编译器替我抓到2处遗漏 |
| 437 | `c99632b29` | `e9b67929e` | 清理:删掉改名时留下的 .bak-rename(9 个)+.gitignore 补 *.bak-* 规则(防将来再被看到) |
| 438 | `92cefb325` | `8186f1a62` | M9 三处规则现状读数 + 文案对齐:真正要裁的只有两件事 |
| 439 | `535fdac0a` | `64f24e11d` | 投架构:M9三处规则现状读数(读完真实代码的重大更正:BattleDirector:261-278 其实是【增援】规则,扩展位=待命席=>4v4无扩展位则增援消失/虚弱回升归0)+真正要裁的只有两件 |
| 440 | `e251e0b10` | `c3913921d` | M3 可执行清单:22 条 buff 的逐条归属表 + 方法与卡数字的对账 |
| 441 | `bff340c13` | `548451228` | 投架构:M3 22条buff逐条归属表(方法修正:按【时长形态】分类不按id名 => ①折磨/美德 7 条与卡严丝合缝;②③各差1~3条,差在 taunt/bound/pep_talk 三条边界请裁) |
| 442 | `55b861db6` | `dd6b5cebb` | def 合并三份执行预案(甲/乙/丙):每份写清动哪几行/改哪些读数/风险 |
| 443 | `d27823114` | `abfb4582d` | 投架构:def合并三份执行预案(甲退场=行为改动+前后读数已有/乙保留=零代码/丙只加注释=零行为)+现状规模50处27文件+建议先做丙 |
| 444 | `b15cdfb16` | `1936ce650` | def 合并【预案(丙)】执行:5 处声明点加 XML/行注释(零行为)—— 消除"PhysDef 是我们自加"的歧义 |
| 445 | `02ea59712` | `7ccd9a863` | 投架构:def预案(丙)执行完成(5处声明点写清PhysDef是我们自加+原版走prot+指向三份预案;零行为797不变)+为什么甲仍需你裁(减伤归零=平衡决定) |
| 446 | `c86adac52` | `609afa92f` | M2 映射「丙方案」:15 条冻结原语的逐条激活清单(7 条现在就能接) |
| 447 | `e078f04d1` | `3ddd832e6` | 投架构:M2丙方案的15条冻结原语逐条激活清单(7条现在就能接:系统已存在;4条等M3/M4;2条口径待定)+口径声明(线索含我的分类器,=1即真无系统)+选丙不是拖延 |
| 448 | `ca0897282` | `2a7cff211` | M1c 取值表 + M5 空分类清单:两条裁定所需的最后一块 |
| 449 | `f38507b47` | `24f121bbd` | 投架构:M1c取值表(23技能,复现今天所需的dmg%)+结构性结论(单一dmg%无法跨阶复现:今天阶数不影响伤害而新模型区间随阶增长=>真正要裁的是要不要让阶影响伤害)+M5空分类全是corvids_*(建议保持空) |
| 450 | `39304fd58` | `0b81d62dc` | 终检快照:构建0错/798全绿/门禁412文件0豁免/B6 OK/在飞0/4份备份 + 33项交付总表 + 12件待裁定 |
| 451 | `99f062474` | `a0ec395f4` | 投架构:终检快照(全绿:构建0错/798全绿/门禁412文件0豁免/B6 OK/在飞0/我的未提交0/4份备份)+33项交付总表+12件待裁定清单+守住的3条硬约束 |
| 452 | `b94947d08` | `3d19796b3` | 六件拆分完整性审计:XML注释与成员逐条无损(唯一差异是一个测试方法被改名,已如实报告) |
| 453 | `0dd895d86` | `1d542fdce` | 投架构:六件拆分完整性审计(XML注释与成员逐条无损:双计数+拆分前快照强校验)+方法教训(拿HEAD比无效)+唯一差异是一个测试方法被改名(M9改名批副作用,已如实报告并给恢复选项) |
| 454 | `f0feb3d88` | `bb244e937` | TuningConfig 域界复核:我自曝"名不副实"(切口是行数边界,不是纯域)+给出三个选项 |
| 455 | `d2883c5e8` | `044dbf301` | 投架构:TuningConfig域界复核+自我更正(切口是行数边界不是纯域;实测跨域清单;三选项甲保持/乙真重切/丙按校验对象改名我推荐丙)+头注释已改如实描述零行为798不变 |
| 456 | `41272b370` | `d69a2c269` | 拆分完整性审计(第二批):ExpeditionFlow 7 片也【零缺失】+ 第二条方法教训 |
| 457 | `ee7fe2c5c` | `42fb421da` | 投架构:拆分审计第二批(ExpeditionFlow 7片零缺失:91=>110成员名/缺失0)+第二条方法教训(反编译来源要按成员名比,签名行不可靠)+确认IsRoomResolved在Topology:26 |
| 458 | `3574000de` | `a79bdaa12` | 拆分完整性工具化:tools/dsh/audit_split_integrity.py(可复跑 · CI 可用 · 含双向自检)+ 把一个测试名改回原名 |
| 459 | `1f134b140` | `1e725991a` | 投架构:拆分完整性工具化(audit_split_integrity.py一条命令自证没丢东西,按成员名比+双向自检+两条方法教训)+把M9改名批意外改掉的测试名改回原名+六件工具实跑全过+798全绿 |
| 460 | `8308b9029` | `cd714e942` | 产物索引刷新(接手指南):32 份产物 + 🆕"按裁定取用"一节(你裁哪条就看哪份)+工具清单 + 使用提醒 |
| 461 | `8a95cffd7` | `70ade1aaf` | 投架构:产物索引刷新(32份产物+新增按裁定取用一节:12条裁定各自对应哪份报告)+索引自检逐条grep通过 |
| 462 | `dd1021849` | `0e70916eb` | TuningConfig（丙）落地:两片改名成"不假装纯域"的名字(零行为) |
| 463 | `c379a0c0e` | `f0d9a1644` | 投架构:TuningConfig名实不符已闭环(选丙:两片改名成 Validate.<X>Side 诚实标记,零行为798不变,工具复跑OK)+若仍要乙严格按域重切清单已备 |
| 464 | `8c7358603` | `86cfbaec4` | 自我审计记分卡刷新(兑现第 30 轮的承诺"每完成一个激活步骤就改一行") |
| 465 | `599f3dfad` | `88bce8af2` | 投架构:自我审计记分卡刷新(兑现第30轮承诺:1项转✅RosterCapByLevel+4项转为被新件消费+仍待激活8项逐条前置)+主动交代CapCeiling成无人用遗留(给三个选项) |
| 466 | `9a84a162d` | `815582fd3` | TuningConfig（乙）落地:严格按域重切(256 行 dungeon/retreat 从 CombatSide 搬到 ExpeditionSide)—— 零行为 |
| 467 | `6c5814556` | `2cf7317c8` | 重大能力解锁:本机 Godot 4.6.1 mono 可用 ⇒ 我能跑运行时验证(全套冒烟 10 例全绿!) |
| 468 | `82918abb7` | `6e2a52940` | 投架构:重大能力解锁-本机Godot可用=>跑通全套冒烟10例全绿(真错误0/退出码0,含e2e与ui-audit)+如实列出3例引擎退出泄漏+41项交付有了运行时证据+B-1真实缺口(BattleRoot是Node2D不能当panel,需Control包装) |
| 469 | `e8919ecaa` | `929ea068a` | B-1 主程序侧第一步落地:战斗可成【面板】(BattlePanel + --battle-panel + 冒烟用例,运行时验证通过) |
| 470 | `e148b3497` | `4329e41b6` | B-1 主程序侧第一步【真·运行时验证通过】:战斗成面板(11 例冒烟全绿)+ 更正我上一条"说早了" |
| 471 | `d462eacf2` | `02f918c5f` | 投架构:B-1主程序侧第一步(战斗成面板,11例冒烟全绿零回归)+更正我上一条说早了(当时该用例其实红:Node not found UILayer/BattleUI,已改为实例化战斗场景=>真错误2变0)+边界诚实(默认boot/C4/main_scene终态/B-2仍未做) |
| 472 | `129523311` | `8612a10ae` | 收尾:B-1 的附带产物(冒烟留档/uid 等)入库 |
| 473 | `d2f26e199` | `674a2a30b` | C4 落地:面板步骤导航(panel:battle)+冒烟用例 —— 判据有硬证据(路径证明消费者在面板内) |
| 474 | `ee2c7a4c4` | `4244993ae` | 收尾:C4 的冒烟留档入库 |
| 475 | `c3f69adaa` | `a1fd8ab94` | 投架构:C4落地(面板步骤导航panel:battle+13例冒烟全绿+硬证据:路径证明消费者在面板内)+边界(默认boot/autoload终态/B-2的CanvasLayer仍属UI) |
| 476 | `fce749f22` | `5a2b03eb9` | B-2 判据落地:外壳"是否在场景之上"变成可核对读数(实测: 0 vs 0 => 同层 => 场景会盖住外壳) |
| 477 | `002db285f` | `2efd1f474` | 投架构+UI:B-2判据落地(ShellLayerAudit:外壳三层层号+外壳vs场景有效绘层,失败关;实测0vs0=>同层=>场景会盖住外壳,你的怀疑被量化)+修法归属UI域+用法(UI加CanvasLayer后自动转绿) |
| 478 | `7690ffecd` | `39fb8c24a` | B-3 接线完成:回落真的留痕了 ⇒ "还差哪几屏没面板化"有【真实运行数据】 |
| 479 | `55582863e` | `aac3eb33b` | 策划案(更新后)工作总结:裁定→交付对照表 + 纪律自证 + 仍等策划的 8 条 |
| 480 | `280311fb4` | `07fc0ad7d` | C4 终态判据落地:数外壳实例 + 看 main_scene/autoload(实测:实例 1 ✓ 但 autoload 仍注册 ✗) |
| 481 | `247062f40` | `72cf20a54` | 投架构:C4终态判据落地(ShellInstanceAudit:数UIRoot实例+看main_scene/autoload三者齐判;实测实例1但autoload仍注册=>只改main_scene会立刻变2份)+修法顺序 |
| 482 | `b9c8e8877` | `479662487` | 架构: 清61封积压⇒一次性裁完12件(def取丙/M2取丙/阶数要影响伤害/M8M3M9授权/误提取乙/注释认领)+修订'不代提交' |
| 483 | `ddd32baf3` | `b18c9db29` | 形态 B 一键状态:--shell-status(绘层 + 回落账本 + 实例数 一次看全,并写明"剩下归谁") |
| 484 | `7a3833887` | `631403bff` | 收尾:一键状态的冒烟留档入库 |
| 485 | `397abb8a1` | `d8606293c` | 投架构+UI:形态B一键状态--shell-status(绘层/回落账本/实例数一次看全+小结+写明剩下三件全属UI)+用法命令 |
| 486 | `3ccec2b36` | `3ed8d45e3` | 架构: 回执--shell-status(立交付模板)+提改进'未触发≠0'(同族红线20⑤/O-82/O-96)+确认剩余三件归UI |
| 487 | `9c85f89aa` | `b7629a060` | 架构: 确认§39解冻清单单一载体+校正'改名不做'(挂到甲,避免churn50处与打回)+AZ增量(注释也要标出处) |
| 488 | `b088221ec` | `db240bd7f` | 架构回执两件:--shell-status 区分"未触发" + 无注释清单(附"我删掉一个坏门禁"的如实记录) |
| 489 | `f679a3de2` | `a40b23d9a` | 无注释整类体清单(架构 ⑩ 要的按块格式)+成因交代(ExpeditionFlow 那批是我反编译回填的,密度 2-3%) |
| 490 | `2e34824fb` | `9d8fd9a64` | 投架构+策划:收到12件与七件裁定(先交两件点名的:--shell-status未触发已修+无注释整类体清单按块)+如实交代删掉一个误报1278处的坏门禁+下一轮执行顺序(M2七条/M8/M1c阶段3/PhysDef归位/误提乙/在飞已全部提完=0) |
| 491 | `78fac62cf` | `7088e8169` | 架构: O-89/O-92 改为指向 §39 解冻清单单一载体(消'两处清单'风险) |
| 492 | `debcb2cf8` | `bc78441eb` | 架构: 同意删门禁并立'已有能挡就别自建'+给无注释类体审法(只审反直觉处)+两条新判据(纪律第一次适用场景/锚点失败可见vs静默改错) |
| 493 | `a875b0568` | `02657f8e8` | 架构: 两条元判据(新增纪律前先问能否改已有/改文件用具精确匹配失败可见的工具)+遗忘登记做法 |
| 494 | `8d00213ca` | `30fc77f7b` | M2 激活第 1 条:hp_heal_percent(带用例+前后读数)+一条前提性发现(我上轮"7 条可立即接"过于乐观) |
| 495 | `e64a5bd5b` | `a0e4409b4` | 投架构:M2 激活第 1 条 hp_heal_percent(纯函数+用例+前后读数) + 更正上轮"7 条可立即接"过于乐观 |
| 496 | `ce1cda2ad` | `3d9cab70d` | 架构: M2裁定取(乙)留冻结清单+需求驱动转甲+立'缺载体≠未接线'判据+记前后读数标准格式 |
| 497 | `4d98c6a80` | `10e7cf3c7` | M8 落库完成(架构授权):hero_upgrades.json 15 职业/135 树/645 等级 + 四问答案与依据 |
| 498 | `51e7822d4` | `2c067c545` | M1a PhysDef 归位完成(策划更正:我方 PhysDef 对应原版 prot ⇒ 改名归位,值原样搬家=零行为) |
| 499 | `eaebd6a9c` | `f26890260` | 投架构+UI:prot归位完成(phys_def=>prot 数值原样搬家=零行为;803全绿+冒烟13/13)+撤掉为UI让步的兼容别名(用户指示)+如实报告改了UI3行+入解冻清单条目(prot对齐原版=>减伤归0) |
| 500 | `73d831365` | `54892cbc3` | M1c 阶段 3:机制就位(默认零行为)+就绪度报告(两个硬前置未满足,不硬造) |
| 501 | `1c34b8d06` | `b48584c4d` | M3 第 1 步:折磨/美德搬到自己的表(7 条 · 值原样 · 零行为)+第 2 步计划 |
| 502 | `98ec3c862` | `536386e67` | 孤儿批逐文件核准表(架构 ⑨ 裁 (乙) 的合法形态 = 逐文件标注):9 批 237 文件全部可追溯到 提交号+批次+归属域 |
| 503 | `7f6c58f6c` | `2fd14f341` | 投架构:误提 (乙) 逐文件核准表(9批237文件全部可追溯到提交号+批次+归属域;能担保/不能担保分开写;建议改为认领而非摘出) |
| 504 | `b8dde43e3` | `dda4cd464` | 交付索引(单一入口):reports/INDEX_lead_programmer.md 刷新 — 每项含实测读数 + 提交号 |
| 505 | `854ed4e39` | `f1eaf4c58` | 投架构:交付索引(单一入口)已刷新(每项含实测读数+提交号,哈希全部git校验存在,§6能验与不能验分开写) |
| 506 | `9a6f16b4b` | `6962a6fa4` | 索引补全:10 行"各批"改为真实提交号(六件拆分/白名单清零/审计工具/B6 批入库/方法论两批) |
| 507 | `47c82ba88` | `85941148b` | 冒烟"引擎退出泄漏"查清了:全是主题/字体资源(FontFile/DPITexture/StyleBoxFlat),没有我们的对象 |
| 508 | `01a73a6d7` | `0cbc2ffb3` | 投UI+架构:冒烟引擎退出泄漏取证(全是主题/字体资源FontFile/DPITexture/StyleBoxFlat,无我方对象;更正我第75轮怀疑;泄漏量随屏数单调相关;归属UI域+给可复现命令) |
| 509 | `afbae257d` | `00db050f3` | 终检 + 交接刷新:reports/final_verification.md(六项硬读数+13例冒烟+三列定稿) + HANDOVER(5条不变量+操作陷阱+卡点) |
| 510 | `9f9ca8b02` | `6a083d923` | 终检留档:冒烟重跑 13/13 全绿(真错误 0 · PROBE-EXIT bad=0 code=0 · exit 0) |
| 511 | `5bc322049` | `ba062ddea` | 投架构:终检交付(六项硬读数+三列定稿做完/没做/等裁定+交接文档;冒烟重跑13/13全绿) |
| 512 | `068072236` | `3e4443bf6` | 数据纪律 41 处可疑【逐条判读】完成:三扫 41 => 0 处 · exit 1 => 0 · 每条豁免带理由 |
| 513 | `b921b85e1` | `50e62196d` | 投架构:数据纪律41处逐条判读(8数字=结构性含SlotCap故意护栏[我先入为主判错已更正]/9死函数=机制待消费附激活条件/24死键=元数据8+搬运载荷1+15个hero id判定为扫描器局限)+三扫41=>0 exit0 |
| 514 | `23e3e3071` | `29b97e875` | 门禁判据升级【方案】(未实施):--deadkeys 对"映射键"的误判 —— 保守规则 + 双向自检 |
| 515 | `78ab849ef` | `a8f1f2b50` | 投架构:门禁判据升级方案(--deadkeys 映射键误判;保守规则+双向自检+影响与风险;只出方案未实施,末段不引入判据级风险) |
| 516 | `d13a1fd50` | `7900a1732` | 终检复跑(R87):构建 0 错 · 809/809 · 五道门禁读数(数据纪律 41=>0 exit 0) · 外壳单例 NOT-YET(UI 域已声明) |
| 517 | `5a4dca9b9` | `cd7eea9f4` | 终检复跑(R87)补完:冒烟 13/13 全绿(真错误 0 · PROBE-EXIT bad=0 code=0 · exit 0)+留档入库 ⇒ 末次终检六项+五门禁+冒烟全绿 |
| 518 | `f9f6875c7` | `3fcd29ce0` | 收尾:R87 终检的相关留档入库 |
| 519 | `5a2c231b0` | `d68ee479c` | 索引 §0 同步 + 收尾:把"数据纪律 41=>0"等末次读数写进单一入口 |
| 520 | `4d6f89022` | `5dd488725` | R88 收尾:第 5 份全量备份完成 + 我域在飞清零 + 索引/终检同步 |
| 521 | `c18f1cd6b` | `1d4e9d975` | R89 结项报告:做完的清点 + 没做的逐项写前置(三类) + 证据链三条 + 诚实边界 |
| 522 | `5401115ec` | `19e331d69` | 投三窗:结项报告(做完 69 项清点 + 没做逐项写前置三类 + 证据链三条互证 + 诚实边界:别人 237 文件只担保能编译全绿可追溯) |
| 523 | `32b795ece` | `5970bf274` | R90 收网:末轮复验(构建 0 错/809/门禁全绿) + 索引挂结项报告 |
| 524 | `3027c1e61` | `601c58e99` | skillupdate |
| 525 | `71738bd66` | `c96128367` | 架构: P0报告(合并冲突已解除)+DEADKEY裁定取甲+登记O-99(合并事故)/O-100(契约不为UI让步)+清8封积压 |
| 526 | `ada3ca0ea` | `8ef9d1668` | 参考项目顶替:可行性实测报告(六项逐项结论) + merge 事故按用户授权 abort 处置 |
| 527 | `e0511cd7a` | `dcb19b654` | 技能 dmg% 映射草案工具化:make_skill_mapping_proposal.py(可复跑) + 44 行提案表(未落库) |
| 528 | `46966666c` | `e435ed0d4` | 架构: 参考顶替六项裁定(origin分流/prot是顺序问题/新增O-101当前阶契约空缺)+转策划3件+记第4次锚点错 |
| 529 | `894169ed3` | `921f127e7` | 架构: 重投参考顶替裁定(上封因TEMP路径被拒写未送达)+含'我投递了怎么证明'判据 |
| 530 | `003ad8712` | `8cf8e5395` | 架构: 记投递失败事故(硬编码TEMP被拒写)+通道完整性发现(策划窗标记只剩2个)+建议合并后四窗对账 |
| 531 | `d87af6323` | `62d8efecf` | 参考项目顶替轮 1:一手对账(495 比较/97 冲突)+ ③④ 改从【一手】落库(零行为)+ 替换清单与 GPL 登记文本 |
| 532 | `3ac0c7a2c` | `eb08766f5` | 投策划+架构:参考项目顶替轮1(一手对账495比较/97冲突=>顶替改从一手落;③④已落库零行为809全绿;替换清单reference_placeholders.md;GPL登记原文待贴;请策划拍板四件) |
| 533 | `66b035028` | `f03aebb7c` | 架构: 落⑥⑦判据(参考项目≠一手19.6%+映射依据/零行为证明边界)+贴GPL登记进assets_credits§10+回执 |
| 534 | `da2ba963e` | `baf99f206` | 出处等级审计(轮 2):5 个数据集已一手 · 2 个仍第三方(已列待对账+写清为什么现在做不了) · hero_upgrades 补上来源标记 |
| 535 | `f21cfbcd5` | `2ac7ddfe5` | 轮 2 补:顶层基础属性 vs 第0阶一致性核对(实测表 + 不是我弄的证据 + 建议维持现状) |
| 536 | `31aa23766` | `6cd435652` | buff 原语词汇一手对账(轮 3):一手 48 原语 vs 第三方 41 ⇒ 分类器漏 8 多 1(需按一手修正) |
| 537 | `dafca86a1` | `74a269887` | 替换清单补 §6:把"数据出处等级与对账状态"也纳入单一入口(含 buff 原语对账结论 + 待修正分类器) |
| 538 | `06ca07997` | `20f22bd40` | R4 分类器修正(按一手):判据源换成一手产物 ⇒ 原语 41=>48 · 有去向 26=>27 · 冻结 15=>21(零行为) |
| 539 | `9950bcc10` | `fd6f6da62` | R4 补:替换清单把 buff 原语项从"待修正"改为"已修正"(读数 41=>48 / 26=>27 / 15=>21) |
| 540 | `5338fa268` | `ff3b6010f` | R5 修一处门禁回红:我给 hero_upgrades.json 加的 `_source` 被当死键(工具只放行 source/_note)⇒ 补齐元数据键约定 |
| 541 | `d4fbce26e` | `bf202563e` | --deadkeys 判据升级(架构批准取甲):映射容器改为判据处理,删掉 15 行白名单,双向自检 PASS |
| 542 | `7b66658eb` | `1ea527b58` | 投架构:--deadkeys 判据升级完成(映射容器改判据/删15行白名单/双向自检PASS/三扫0处exit0/全项目809全绿) |
| 543 | `fd8dee789` | `7c06ee4c7` | R6 ③④ 减伤侧纯机制补齐(TierDefence)+阶段1守卫扩到它+当前阶三选项提案 |
| 544 | `59f873cbf` | `be8f81d2d` | R6 补:把 TierDefence 带来的 3 处门禁项按机制登记(1 结构性数字 + 2 条待激活函数)⇒ 数据纪律回 0 |
| 545 | `5e9247b81` | `ec007d762` | 技能映射提案升级:加【类型一致性】判据(我们的 range_axis vs 参考 .type)⇒ 自动挑出 battle_ballad 那类错配 |
| 546 | `1c8239fec` | `dff312f58` | R7 留档:轮 1 换一手表后冒烟重跑 13/13 全绿(真错误 0 · PROBE-EXIT bad=0 code=0 · exit 0)⇒ 换源未伤运行时 ✓ |
| 547 | `ffdf8e4f1` | `35b896788` | R8 替换清单末次全面同步:①~⑥+⑤b 现状/出处等级/前后读数/提交索引/待裁 5 件 全收进单一入口 |
| 548 | `c064a3ad6` | `ce59a6cf9` | R9 收尾三件:HANDOVER 补两条工具坑 + 全局索引挂上"参考项目顶替"目标的单一入口 + 终检 |
| 549 | `a19938cd6` | `c7abbf078` | 架构: 验收--deadkeys升级(取甲+双向自检+证据删白名单)+结案prot取(a)+报值守4h空档(改12h) |
| 550 | `9aebaf110` | `62d328619` | 架构: 裁定'4人三义'(开局名单/编队上限/夹具)+定夹具warrior/tank/medic/commissar+四件全闭 |
| 551 | `efcfb05b9` | `722c3e28b` | 架构: 36行映射表指出'无对应13混两口径'+补结构关系列+修订判据⑦(a)为三档+重复语义共享判据 |
| 552 | `c22abb326` | `3f64ee4af` | 架构: 承认跨职业是第五档+关键区分'值有出处≠分配有出处'+收纪律BH并归族(模式/口径/层级) |
| 553 | `254471833` | `9c9e23146` | R10 给待定项②造事实:prot=0 的 M6 达标值实测对照(临时落⇒抓数⇒立刻还原,数据未改) |
| 554 | `9f169c252` | `10acbc7e9` | R10 落库:策划 ①「祖产兑换表 12 条」(一手) + 一处发现更正(并非全部损失 50%) |
| 555 | `a4a02958c` | `f7d58561c` | R10 补:把新增的 _design 元数据键纳入约定(与 _note/_source/_align/_ruling/_field_classes 同类 ✓)⇒ 数据纪律回 0 |
| 556 | `3a8df45cf` | `9714adbb2` | R10 策划②步骤①:传家宝【任务奖励】通道(结构·零行为)+一手表核对 + 一处歧义如实上报 |
| 557 | `0404cf525` | `c8e279ece` | 投策划+架构:祖产①已落库+②步骤①完成(零行为/tier_drop未动)+更正1(并非所有兑换损失50%:实测0/25/33.3/50%)+提问(amounts索引口径歧义)+如实标注3条新用例未验证(环境内存耗尽) |
| 558 | `b769937bc` | `8edb48218` | R11:补验上轮 3 条用例(通过 ✓ / 全量 818/818)+ 步骤②施工单(8 处逐行读清)+ 一条顺序问题上报 |
| 559 | `d6f13fc08` | `7d4a31206` | 投策划+架构:祖产②施工单(8处已读清)+顺序问题(现在删tier_drop会让传家宝产出=0⇒建议先接线再删)+接线三口径不猜(难度档来源/任务长度来源/每趟给哪几种)+补验上轮3条用例通过(全量818/818) |
| 560 | `3e2e6408b` | `020111036` | R12 落库:策划 36 行表的【明确 11 条】dmg_pct 已落(零行为占位)+ 元数据键规则一般化 |
| 561 | `c60689af0` | `86a6f2ee3` | R13:策划 5 件答案的落地状态收进替换清单 §8 + 4v4 夹具钉住(820/820)+ ⑥ SP 载体已存在(无需改) |
| 562 | `bd56ab252` | `7598c3a2a` | R13 祖产②接线:索引口径改 (ii) + 难度带映射 + 每趟产出量读数(823/823) |
| 563 | `a0e38afab` | `ee8f7352d` | 投策划+架构:祖产②接线机制+查表完成((ii)口径修正+难度带+每趟产出量读数表:档1/3/5 x 长度1..4)+两个输入缺口如实报(无任务长度概念/无队伍 resolve level 模型 ⇒ 真正调用等口径)+tier_drop 仍原封(按(A)排第③步) |
| 564 | `0352d9a3a` | `fe774c0b8` | R14 末轮:终检 + 单一入口并入本目标全部提交索引(29 条)+6 项最终状态 + 据实结项 |
| 565 | `65e3b5bfa` | `a5c35fa1d` | R14 补:祖产接线机制的 3 条函数按机制登记(消费方 = 结算接线 · 等两输入)⇒ 数据纪律回 0 |
| 566 | `387f75828` | `c31d50a20` | R15(目标已完成后继续):③④ 的前置解除 —— HeroConfig 加 tier 载体(weapon_tier/armour_tier,默认 0 零行为) |
| 567 | `09563cfdc` | `be142331d` | 投架构+策划:③④前置解除(HeroConfig 加 weapon_tier/armour_tier 默认0零行为·823同数)+请给切换时机(成套切换+前后读数)+仍等两件(候选12/祖产两输入) |
| 568 | `89ad71868` | `b774d7e89` | R16 就按候选表落:dmg_pct 候选 12 条已落(合计 23 条)+ 修正"11 填"守卫为 23 |
| 569 | `baa180627` | `5b241f511` | R16 补:祖产代理量的"长度上限 4"按结构性常量登记(一手值域 1~4)⇒ 数据纪律回 0 |
| 570 | `ce5cebd79` | `3a87cd331` | R17 #472①:把技能 dmg% 的【候选 12 条】撤出 darkest/data(只留在观察清单 D5) |
| 571 | `49b190840` | `ff215f821` | R18 传家宝步骤②接线:产出从「光照档」改走「任务奖励」通道(#470 顺序 (A) 先接线) |
| 572 | `ef1a608bd` | `51ec8ac0b` | R18 补:步骤②接线的执行记录(§54)+ 冒烟真跑读数 + 清单/索引回填 |
| 573 | `177084549` | `d1c7e7b95` | R19 传家宝步骤②第二步:删掉旧源 tier_drop(#470 顺序 (A) 先接线->后删源) |
| 574 | `45f59b62b` | `ad32128fb` | R19 补:删源执行记录(§55)+ P23③ 已破登记(§39 第 9 项)+ 契约变更请求 + 清单/索引回填 |
| 575 | `94979d659` | `ac99898dd` | R19 补:§55.7 回填两个提交号(1770845 实现 / 45f59b6 记录) |
| 576 | `d7184be74` | `358d0f24a` | R20 M1b 补齐(清单 P6):按阶取属性的入口纪律(越界报错 / 缺阶报错 / 旧字段零行为) |
| 577 | `5bf0d951e` | `24deaade5` | R20 补:P6 执行记录(§56)+ 清单/索引回填(836/836 · 440 文件 · 13/13) |
| 578 | `798770c5c` | `f70ed68af` | R21 学习参考项目 Darkest-Dungeon-Unity:数据与逻辑侦察 + 数值来源改判 + 采用计划 |
| 579 | `8c6b17e7e` | `9556b6579` | R21 补:战斗逻辑学习报告 + 并入采用计划(伤害公式/def与prot的裁定/P5 阻塞解开/5 点不一致/参考项目缺陷) |
| 580 | `7f7dbd1a9` | `ea94ad48a` | R21 补:把 3.3MB 临时转储 _raw_stats.json 移出版本库(保留磁盘文件,仍在跑的分队要用) |
| 581 | `1236a68fd` | `4c9c80170` | R21 补:并入数据总账的 5 条更正(Curios.csv 60 条/7 个非法 JSON/营地技能位置/AI join 陷阱/Maps 是二进制)+ 划掉本地化 12.37MB |
| 582 | `c74954ecd` | `7bc9b7fee` | A1：落 buff 原语层（参考项目 1801 条）—— 引用链从 0 条解析到 482 条 |
| 583 | `56a8a5c5f` | `e1f86ffc5` | R22 记录：A1 之后的文档与学习报告归档（含两份城镇/任务分队交付） |
| 584 | `57b86d4d5` | `1a6a0e029` | R22 更正：A2（技能 dmg%）**不受**"amount 是分数"阻塞 —— 实测 `.dmg` 是整数百分比 |
| 585 | `eafbb979a` | `5d04f7352` | ﻿R22 加固：A1 的防火墙补【负向证明】—— 只会"总是通过"的校验等于空转 |
| 586 | `54afb57c1` | `46379f1e2` | 修 `reconcile_hero_tables` 的**静默漏比**：旧版漏掉 4/15 英雄，冲突数被少报（97 ⇒ 130） |
| 587 | `3f431360e` | `6efb03f64` | A2：技能 `dmg%` **改用参考项目来源**（11 → 14 条）—— 顺带纠正 2 个值，并把落库器做成**幂等** |
| 588 | `dd3754e17` | `8582fd4e6` | A3：英雄武器/护甲 **5 阶**改用【参考项目】来源（39 个字段顶替）—— 顺带把 `CritPct` 放宽成小数 |
| 589 | `eb975c5c8` | `33ea6807d` | A4 侦察：怪物来源读数（参考 230 文件 / 94 base vs 我方 **3** 个敌方原型）—— 只出读数与卡点，不落库 |
| 590 | `f6bdb9bb6` | `bc4336fb1` | P0 通道事故：**契约载体里的 C0 控制字符** —— 定位根因 + 门禁 + 修好 3 个活载体；并落 P7 两条裁定 |
| 591 | `b4bb21a86` | `1b05f52f2` | A4 步1：参考项目**怪物全表抽取**（230 条 · 2070 个值逐值复核 0 不匹配）—— 只抽不落库 |
| 592 | `7b43e2cf3` | `0bba83f35` | A6：折磨/美德 12 条 + 两张 **act-out 表**（14 + 15 项）—— 并**逐条判定**：我方那 7 条**全不是**参考的对等物 |
| 593 | `3d42f2310` | `9b8627521` | A6 请求件 + 投递：我方 7 条**全不是**参考对等物（含 A4 步1 汇报） |
| 594 | `218206825` | `8d12754c8` | A9：建筑与升级 —— 抽取（8 建筑 / 20 树 / 99 等级）+ 🎖️ **三方判定**：我方==E盘 **99/99**，参考==E盘 **0/99** |
| 595 | `4443c3d33` | `5255a0348` | A10 + A11：补给/物品（57 条 + 3 张清单）· 🎖️ **传家宝兑换三方 12/12 一致 ⇒ 无需改动** |
| 596 | `79da9cdcf` | `c1425326f` | A9/A10/A11 请求件 + 投递：**参考项目数值被证明改过两处**，请裁总口径 |
| 597 | `86b31aafb` | `c68cfa0af` | A12：地牢/地图 —— `Dungeons/` 可抽（18 mash 段 / 744 条目）· `Maps/` 全二进制不可抽；下游奇物 60 条补上 |
| 598 | `0b5938fc4` | `a4e79da25` | 奇物逐条对账：我方 6 条 vs 参考 60 条 —— **id 交集 0**，但 **5/6 有语义配对** |
| 599 | `430f0ac45` | `02c37fa75` | 奇物配对**升级**：按 tag 交集（可计算）—— 我方 curio_type 就是参考的 TAG |
| 600 | `2602c777d` | `9087517e1` | A8：任务/战利品/旁白/队伍名 四份全抽 —— 🎖️ 引用完整性**按命名空间分开查**（我先混错了一次） |
| 601 | `9d4d7bc0d` | `70d4876d2` | A 线总账投递：**A1~A12 的抽取/读数全部走完** + A8/奇物配对升级汇报 |
| 602 | `139c68186` | `28df4fd32` | A5：怪物 AI 抽取（160 brains 全文）—— 并**修正 `§6` 的两处描述** |
| 603 | `ce8667842` | `2e045ccf4` | A7：饰品 488 / 怪癖 163 —— **A 线最后一项**（抽取完成）· 🎖️ 并更正"74 悬空 = 改名"的说法 |
| 604 | `f605a773e` | `e3d2a227e` | 投递：A 线抽取 100% 收官（补 A5/A7）+ 三处更正 |
| 605 | `41f818d05` | `e8ee584ca` | 奇物配对：三段判据**只能做到第 ① 段** —— ②③ 都缺前置（如实报，不硬凑） |
| 606 | `6d075961e` | `d123d8bc2` | plot_quests 19 字段逐个核 —— 量出**两条轴结构** + 三种「用不到」的字段 |
| 607 | `dec054a62` | `66dab0b6c` | plot_quests 字段消费点实测：**13 个里只有 4 个真的生效** |
| 608 | `9b1d9e81b` | `e1f3889cf` | goals 45 条：结构 + 三张表交叉校验全命中 + 两类 goal |
| 609 | `4047de681` | `f46f98178` | 投递：quest 字段消费点实测 + 「失败惩罚参考侧未实现」+ goals 表读数 |
| 610 | `6fc28f2c2` | `d725f5898` | generation/restriction 展开：**任务生成系统**（我方整块没有）+ 发现 dungeon_types 4种vs2种 |
| 611 | `255ffb8a9` | `2d9b4ccd6` | 投递：generation 任务生成系统 + dungeon_types 4种vs2种（可直接执行的改动） |
| 612 | `a6b511da4` | `058db7d84` | 经济量纲实测：我方**小整数经济** vs 参考**千金经济**（差约 3 个数量级） |
| 613 | `bbb39b7f4` | `e9e9660d7` | 🔴 更正：我方**不是**「小整数经济」—— 是**分通道的**（上一件结论过强） |
| 614 | `525c32c2a` | `942e3422e` | 投递：撤回「经济放大 667 倍」的错误建议 + 更正后的按通道对照 |
| 615 | `127fb4807` | `9a4526c4d` | 亮度 5 档：我方与参考**逐档逐界完全一致**（我第一版算错了方向） |
| 616 | `4d4c7ff35` | `57384cd32` | 亮度 5 档**后续两问**已关：`Out` 特判 与 `darkness_bonuses` 机制 |
| 617 | `b62d2fdad` | `ab6fefa61` | 投递：亮度 5 档完全一致（第 5 处）+ 一条真正的设计分歧（亮度方向） |
| 618 | `d78609278` | `598357922` | 宽面引用审计：我方所有跨文件引用一次查完（按命名空间分类） |
| 619 | `d067e2d6c` | `8a594f511` | A6 后续：参考 29 种战斗 act-out vs 我方 —— **整层缺失**，与 A6 判定交叉印证 |
| 620 | `ff16db5dd` | `a3fa47741` | 审计覆盖缺口已补：又 4 个引用键全部闭合 · `skill_id` 7/7 命中（跨表验证） |
| 621 | `1fdc5de16` | `bad1cd4ac` | 29 种 act-out 消费点实测：15 种反应里 **2 种没人读**（数据却有值） |
| 622 | `ded041c44` | `948e8f852` | 14 种回合开始 act-out 消费点实测：**14/14 全部有真实分支** |
| 623 | `5c8ed90a9` | `2216e176d` | 回合开始 act-out 执行体细读：11 个 case + 🔴 **3 个枚举值无 case**（订正上轮的 14/14） |
| 624 | `6e153322a` | `1f1b3fd56` | 传家宝命名不一致结案：**已存在一座有文档、已接线的桥**（不是缺陷） |
| 625 | `32c253168` | `505d5ebbc` | 命名一致性审计：新发现 `stage_coach` vs `stagecoach`（查证后**不是缺陷**） |
| 626 | `0e0895b7c` | `f8d494dca` | 驼峰命名补扫：**0 组「同词不同分界」** —— 但记一处跨域并存 |
| 627 | `9bef6ade7` | `b86e39a9b` | 联机侧 act-out：**同一 switch 的第二份实现** —— 而 `HealSelf` 两处不同 |
| 628 | `462ab8db9` | `ec402498d` | 两条推断查证：`HealPercent` **确认同公式** · `RevertDeathsDoor` **确认幂等** |
| 629 | `ef0820d4d` | `838ea0801` | 最后一口关闭：**治疗向上取整 · 伤害四舍五入 —— 两侧不对称** |
| 630 | `6e4357ecc` | `643b534bc` | 取整链条查清：**是「两道取整」** —— 我上一件担心的「不一致」是**误读** |
| 631 | `e711f94e8` | `12361e879` | 取整顺序**穷举验证**：两种顺序在 **900/2001** 个采样点上不同（我上一件的推断反了） |
| 632 | `dbb987819` | `ec6a98592` | 🔴🔴 我方伤害取整 vs 参考：**三处不同**（其中一处会改变「能不能打死」） |
| 633 | `c08c4e054` | `3c91b5994` | 投递：伤害取整对账 —— 我方与参考**三处不同**（两处会改平衡，请裁） |
| 634 | `b949c4f56` | `994753280` | `battle_modifier` 审计：参考 **15 字段**、我方 **0** —— 逐个测出哪些真被消费 |
| 635 | `b7783e07c` | `c53392d2f` | act-out 的 `data` 语义查清：`number_value` 是**分数** · `string_value` 指向 **Effect**（6/6 命中） |
| 636 | `4134f224f` | `325a4285b` | `Effects.txt` 抽取：**952 条 · 65 字段**（A6 的直接前置已就位） |
| 637 | `d8bcb5cbc` | `b552dc5d7` | 952 条 effect 的消费点对账：**754 被引用 · 190 从未被点名**（拆成两类） |
| 638 | `992b97def` | `0c1440964` | 更正 + 收口：effect 消费点**两个口径必须分开**（198 vs 190） |
| 639 | `7bda12a05` | `88e204040` | PLAN 更新：A6 拆成 **A6a（Effect 表）+ A6b（act-out）**，并标注依赖 |
| 640 | `4ad9a73d9` | `4914c35f9` | `.tscn` 节点名审计：**规范良好** —— 8 处「不存在的节点名」**全部是运行时创建的** |
| 641 | `2798499e3` | `9d45e4e2b` | 收口：**198 这个数【不用改】** —— 本地化里的 5 处是「恰好同名」，不是引用 |
| 642 | `07e205267` | `2fc97f5ed` | observe_list：把 A 线 20 轮里反复验证的 **9 条读数判据**集中留档（D11） |
| 643 | `3910e4df4` | `4e2e914ce` | `.tscn` 类型检查：138 条受检 · 4 条「不匹配」**全是假报**（脚本类） |
| 644 | `aa084613c` | `c9fd0b07f` | A9 逐值对账：**99/99 不同，但差异【只集中在 `crest`】** |
| 645 | `1eb7ed367` | `3164f54df` | A3 的**未覆盖半层**：英雄升级消耗逐值对账 —— **300/645 不同 · 全是 `gold`** |
| 646 | `097307834` | `e598cd7ce` | observe_list D11 续：补 4 条判据（10~13），每条都注明救过的那次 |
| 647 | `57f29b11e` | `decba2dcd` | 升级的**效果侧**：参考的建筑效果表 **53 键**，我方 **0** |
| 648 | `5a56c89b6` | `62ff0e3f0` | 交叉核对：`side_effects.results[].type` 与 Effect **是两套词汇**（交集 0） |
| 649 | `13f1579ca` | `7a9a5855f` | 收口 + 更正：`results[].type` 是 **7 种**（不是 8）—— 且 **7/7 全部有代码消费** |
| 650 | `334458f94` | `50cba25a5` | 解锁机制对账：参考用**两个维度**（任务数 + 地牢等级），我方只用**出征数** |
| 651 | `22df7555b` | `849acefce` | 🎖️ 参考的**建筑解锁三件套是死数据** —— 解析了但**一次都没读过** |
| 652 | `07bcd444c` | `2abecea10` | 🎖️🎖️ 参考项目**没有「建筑解锁」机制** —— 8 栋建筑**从一开始就全在** |
| 653 | `db475a73e` | `34f152930` | 🎖️🎖️ 英雄升级消耗**三方对账**：我方 == 一手 **645/645** · 参考改了 300 处 |
| 654 | `bcf009c17` | `6bd5f4105` | 🎖️🎖️ 建筑升级**三方对账**：我方 == 一手 **99/99** · 参考 == 一手 **0/99** |
| 655 | `4e8bbb9de` | `a5ccabd8e` | 投递：三方对账 —— 我方 == 一手 **744/744** · 参考改了 **399 处**（请知情） |
| 656 | `d8ff253b1` | `115741ad8` | 版本差排除：参考的 399 处**不是版本差异** —— 是**一次系统性改法** |
| 657 | `3fa1f475f` | `83c4fd141` | DLC 缺口量化：一手数据散在**本体 + `dlc/` + `modes/` 三处** —— 三方英雄各不同 |
| 658 | `8178dd5ef` | `c787242eb` | A4 的 DLC 覆盖核查：**A4 准确** —— 缺口全部来自参考（36 个怪物） |
| 659 | `1cb82be5f` | `9bcb79038` | observe_list D11 续：补判据 14~19（含一条应成为默认预期的"参考不是完整 DD1"） |
| 660 | `8964d74d4` | `fbb83a7be` | DLC 全类别清点：一手 **1,492+** 数据文件分三处 · 参考只收 **10 类 / 320 文件** |
| 661 | `c2221ef08` | `31b5483e4` | A 线覆盖面自审：**12 项全部溯源到参考** · 0 缺失 · 2 项记未核 |
| 662 | `f7baaf103` | `7105d887a` | 那 7 个被参考丢掉的怪：**全部是真实可遭遇的怪物**（在副本遭遇表里，用【变体名】引用） |
| 663 | `ba5b9df8b` | `5be10820b` | 29 个 DLC 怪：**27/29 在遭遇表里** —— 与那 7 个同性质 |
| 664 | `775694e49` | `7678b6ead` | 验证「参考按清单抓怪物」：**推断被推翻** —— 参考是**一手目录的严格子集** |
| 665 | `dcfb8fb15` | `fc58e43f9` | 变体级验证：**参考 = 一手 − 24 变体** —— base 级推断**确认**且更精确 |
| 666 | `4770413c7` | `a9b531cb8` | 两个粒度的完整账：参考漏 **36 个怪条目 / 80 个变体** |
| 667 | `759df6d49` | `c9a13b357` | 最后一块：**`features/` 下还有 52 个怪 / 130 变体** —— 缺口总数 **88 条目 / 210 变体** |
| 668 | `5c599f491` | `a357d1fa9` | 🔴🔴 一行正则（少写 `re.M`）让我把 **139 报成 26** —— 可达性结论完全翻转 |
| 669 | `c5debd3e4` | `23b684202` | `re.M` 全库审计：**只有 1 处真错** —— 其余是「可疑但正确」 |
| 670 | `2bf265cef` | `2d5cbacb9` | observe_list D11 续：补判据 20~26（A 线收尾段，含两条"测量 bug"判据） |
| 671 | `6b759c3ff` | `92119f6b3` | 修正 `re.M` bug 后的两个数：**参考 ∩ 遭遇表 = 83**（原报 13）· 结论方向不变 |
| 672 | `5585dfcd8` | `36dbf4298` | A8 `curio_name` × A12 奇物：**交集 = 1**（`iron_maiden`）—— 实测收口 |
| 673 | `b824dd0c5` | `59810a079` | 那 10 个 `curio_name` **确判为「任务专用交互物」** —— 参考 `Curios/` 里 10/10 都没有 |
| 674 | `e01070902` | `7b051ea8c` | A8 item 引用 × A10 物品：**22/22 全命中** —— 反向 9 个未引用**全部有解释** |
| 675 | `585626b52` | `35a5fd545` | A8 `trinket` 引用 × A7 488 饰品：**按 `rarity` 对齐，9/9 全部合法**（第 3 类闭合） |
| 676 | `550e9810d` | `0ff40a361` | 悬空的表 `J` **收口**：被引用 1 次 · **`chances = 0`** · **从未被定义** |
| 677 | `c15266b68` | `ec13a64c6` | observe_list D11 续：补判据 27~31（A8 引用对账段） |
| 678 | `c2b57c619` | `f8687984e` | A5 深析：160 brains 结构全貌 · 我方 3 个 archetype 是**另一套机制**（且缺两张表） |
| 679 | `c1920bc50` | `bc7f02974` | A5 前置查清：**24 个条件键全部有代码 case** —— 机制是「硬排除 + 加权抽签」 |
| 680 | `540d2c16c` | `dc837e408` | 目标侧读通：**`FilterTargets` 5 个过滤 + `ChooseTargets` 随机** —— 两半机制齐了 |
| 681 | `449932335` | `c0259f2a5` | observe_list D11 续：补判据 32~36（A5 机制读码段） |
| 682 | `be7455210` | `e98f9db03` | 第三张表读通：`bonus_initiative_desires` —— **换技能的先手奖励**（6 子类 · 全键闭合） |
| 683 | `a28811de2` | `8aa962a78` | 先手奖励的**接线查清**：3 个调用点 · `BonusTurn(选择器)` · 命中即**换技能并 break** |
| 684 | `20456a5ae` | `3c83a9b61` | `fromBonusTurn` 跳过的**3 处**（逐行读出）—— 先手回合是**「轻量回合」** |
| 685 | `a87555220` | `7e10e5160` | PLAN 更新：A5 行补入**完整机制**（三张表 · 骨架 · 先手接线 5 件 · 原始拼写错） |
| 686 | `6f7cd2850` | `acc5ff513` | 联机 vs 单机 `BonusTurn`：**「两份实现」怀疑被推翻** —— 是**复制品少了第三个参数** |
| 687 | `24b7b2780` | `e3818618b` | 联机 act-out **重核**：11 个 case 名称相同，但 **① `RandomCommand` 是空桩 ② 反应只有 1/21** |
| 688 | `2a31c938f` | `4184c04fb` | observe_list D11 续：补判据 37~48（A5 与"单机/联机两份"段） |
| 689 | `a5036c057` | `12354c7eb` | 反应实现点**全盘收口**：只有 3 个文件提到 —— 联机侧**确实残缺**（不是「写在别处」） |
| 690 | `d596c47e8` | `41ea6fde4` | 障碍/陷阱对账：**名字交集 0** —— 两边按**完全不同的维度**切 |
| 691 | `c3cd826d9` | `f5c79cfa2` | 陷阱的难度分档**读全**：只改 `fail_effects` 或 `health`，而 `lurker` **三档全同** |
| 692 | `91908cbc7` | `3f7ec0dbb` | PLAN 更新：A12 行补入 **obstacles/traps 对账**（交集 0 · 切法不同 · 难度分档 · 我方有意简化） |
| 693 | `3339cc281` | `c858a63df` | 🔴🔴 我的抽取器**静默丢了 4 个驼峰字段**（142 处）—— 正则 `[a-z_]+` 不匹配大写 |
| 694 | `050e8fabe` | `5c8d1e605` | `[a-z_]+` 全库审计：**17 个可疑 · 0 个真错** —— 那个 bug 是 `Effects.txt` **专属的** |
| 695 | `67970fde5` | `dc6c0003b` | `Effects.txt` 的**下游用户**核查：两个都读 JSON 产物 ⇒ 唯一爆炸半径确认 |
| 696 | `9443be1cc` | `ba147b43d` | observe_list D11 续：补判据 49~66（A12 对账 + 两处工具 bug 段） |
| 697 | `a299d608c` | `d48c06189` | A6a 的 4 个驼峰字段**消费点读通**：`Effect.LoadData` 的扁平 token 流解析 —— 全部有 case |
| 698 | `217c34f86` | `8c1d7f22f` | DoT 结算逻辑**读通**：`TickDamage`/`Duration` + 概率门 + **一条与先手层的交叉影响** |
| 699 | `00412dcc1` | `57fabffac` | `×1.5` 的真相：**`idleUnit`（先攻为 0 的怪）的 DoT 补偿结算** —— 不是同一个场合 |
| 700 | `6cabba86d` | `7bd7324c5` | PLAN 更新：A6a 行 —— 字段数更正（65→69）+ **四层齐备·可用于实现** |
| 701 | `dab8b688f` | `a88c00e2c` | 两处待读收口：`TotalInitiatives` 的真相 —— **「先攻 0」= 本回合【没有行动位】** |
| 702 | `ce6d25404` | `03fa9ae2f` | `NumberOfTurns == 0` 的怪：**88 / 438 个变体（20%）** —— 那个分支**必须实现** |
| 703 | `e19c6027e` | `5ce5fd5fd` | DoT 4 个结算点**全齐**：`ExecuteRoundAdvance` 是**行走中的 DoT**（只对【英雄】） |
| 704 | `0148ddeed` | `248a61435` | `4880` 那处**不是第 5 个 DoT 结算点** —— 是**营火事件的延迟效果队列** |
| 705 | `a702e526f` | `bd9ba7799` | observe_list D11 续：补判据 67~82（A6a 四层 + DoT 结算段） |
| 706 | `647bf7f5e` | `3181f720f` | PLAN 更新：A6a 行补入 **DoT 的 4 个结算点**（含 `idleUnit` 必须实现的证据） |
| 707 | `85de6b84f` | `ba628ba4d` | `EventQueue` 机制**读通**：`queue` 开关决定「立即 vs 延迟」，且带一套**融合（Fuse）**系统 |
| 708 | `7fc7e535e` | `cd6d08192` | 融合系统**只有 2/29 个子类实现** —— 成对的 stress 增减，用于**合批结算** |
| 709 | `51b406ac6` | `1b7374690` | 融合系统**不会被触发** —— 952 条 effect 里没有一条有多条 `stress` ⇒ 它是**预留机制** |
| 710 | `83e1f7dc0` | `679d97322` | 融合的真实触发条件：`StackEvents()` 按 **`SubEffect.Type`** 配对 —— **跨 effect 也会融合** |
| 711 | `08c1178af` | `8445905be` | 融合触发条件的**全部三条路径查清**：① 无 · ② 结构上不可能 · ③ **可达** ⇒ 应当实现 |
| 712 | `6bcf30463` | `1e04168b6` | observe_list D11 续：补判据 83~99（延迟效果 + 融合段） |
| 713 | `9ccaf0880` | `a6ee5e58d` | PLAN 更新：A6a 行补入 **延迟效果层（`queue` 447 次）与融合（Fuse）** |
| 714 | `49018ab40` | `573b1a138` | 全库取整审计：**5 种方式 · 战斗侧 35 处** —— 规律是「**HP 走 Ceil · 非 HP 走 Round**」 |
| 715 | `efeb5eea1` | `03f1578cf` | 取整**逐条对照**：我方 vs 参考 —— **3 处应改 · 其余不改**（附依据与影响） |
| 716 | `1c6c633b3` | `c6e8a63e6` | `ApplyDamageRounding` 调用点**全列**：定义 1 + 调用 14 —— 改动面**含 2 处配置硬约束** |
| 717 | `722aa4f1a` | `5c704ce08` | 窗口信封补：给第 ⑧ 张单（取整方向 + 下限）补上**完整改动包** |
| 718 | `68cb825a7` | `14d4bdaf7` | observe_list D11 续：补判据 100~107（取整审计段） |
| 719 | `d24f73bc2` | `54116c4f8` | 取整改动**真实影响面**：**6 条断言会红**（不是 2 条）—— 3 条走**间接**路径 |
| 720 | `b59d995bc` | `dc1485c75` | 取整基线**实跑确认**：`FormulaTests` **15/15 绿** · 全量 **843/846**（与既有读数组同） |
| 721 | `095689ca7` | `ddb2bc276` | 窗口第 ⑧ 单更正：影响面 **2 条 ⇒ 6 条**（含 4 条间接）＋ 补入实跑基线 |
| 722 | `d27f4881b` | `c5f0e15af` | observe_list D11 续：补判据 108~115（取整影响面 + 基线校准段） |
| 723 | `45c1ad8ad` | `058e8eaf2` | PLAN 追加 §7.1a：取整改动包（**已量到可执行粒度**） |
| 724 | `26f7a0e1f` | `df900a53f` | A2 复核：**已全部落地（44/44）** —— PLAN 行的「只落 14/44」是**陈述过期** |
| 725 | `6571e56ff` | `5ab9aa681` | PLAN 的 A2 行更新：**「只落 14/44 · 卡在策划」→「已全部落地 44/44」** |
| 726 | `9dd1af13c` | `5ec8efd05` | observe_list D11 续：补判据 116~119（A2 复核段） |
| 727 | `f4ef0731f` | `e643d7b18` | Σ 归一复核：**26 条全部 Σ = 1.0**（精确值，非近似）—— 裁定已落库 |
| 728 | `491c7dd9e` | `98715978d` | PLAN 的 A2 行补入：**`Σ段倍率` 归一也已复核**（26 条全 = 1.0） |
| 729 | `55e7f6cf4` | `0cc2e4168` | observe_list D11 续：补判据 120~123（Σ 归一 + 全表审计段） |
| 730 | `ffc444b00` | `37e249eac` | `missing_hp` 段**在参考里没有对应** —— 确认是**我方自加**（11+7 种模式 0 命中） |
| 731 | `d94dbf33a` | `8329f2db4` | `missing_hp` 的**归属查清**：`§39.2` 里**没有它**，而 `§43` 的映射**只对上了技能、没对上伤害形状** |
| 732 | `eb1c6db44` | `2d2fc5289` | 伤害形状全查：**只有 2 种段类型** —— `flat` ✅ 有对应 · `missing_hp` 🔴 无对应（**仅 2 条**） |
| 733 | `0ac178a92` | `9cb1f03e3` | PLAN §6 补入：**我方自加的伤害机制 `missing_hp`**（含粒度发现） |
| 734 | `5dfdb7bee` | `2f9e9a603` | observe_list D11 续：补判据 124~131（missing_hp 归属 + 形状审计段） |
| 735 | `dfc01b7e2` | `3a83a27ea` | 窗口新增第 ⑩ 件：我方自加的伤害机制 `missing_hp`（请裁 3 项） |
| 736 | `92f12f86f` | `c070ff58c` | `§43` 映射的**置信度画像**：36 条里 **对齐仅 10（28%）· 自加 13（36%）** |
| 737 | `b08e7e948` | `db5c8b328` | `§43` 36 vs `skills.json` 44 的**差 8 条查清**：不是我漏了，是**表的范围只覆盖英雄** |
| 738 | `faf10803e` | `d56975369` | observe_list D11 续：补判据 132~140（§43 映射画像段） |
| 739 | `33a43756c` | `6db3c35f0` | 敌方 8 条技能的**参考原型**：**7/8 有机制对应** —— 「映射不存在 ≠ 机制不存在」 |
| 740 | `c1a057672` | `fcc3b1b1f` | `.target` 编码**解码完成**：前缀 `@`/`~`/`?` + **逐字符即 rank** |
| 741 | `2ee1812e0` | `b7428d7bf` | `target` 映射表：**36/44 可表达 · 8 条需补语义**（`scope` 有 6 种取值） |
| 742 | `97b9a0951` | `1c6cbaee2` | observe_list D11 续：补判据 141~152（敌方原型 + `.target` 编码段） |
| 743 | `75b29e819` | `ef4af2f7c` | 「相邻友方」在参考里**不存在** —— 参考用 `@4321`（全队）+ `.guard` 效果 |
| 744 | `319b720bf` | `f29a0dade` | 英雄侧守卫查清：三条守卫技能**全是 `@1234`（全队）** —— 参考的守卫**不限定相邻** |
| 745 | `1dcee7d2c` | `66693c44a` | observe_list D11 续：补判据 153~159（相邻概念 + 英雄守卫段） |
| 746 | `c956cc8aa` | `b95d3b4ca` | PLAN §6 补入：**我方自加的「目标限制」`adjacent_ally_and_self`** + `.target` 编码规则 |
| 747 | `35762dbd4` | `cc9c62b52` | 参考英雄技能**全数**：15 职业 × 7 = **105 条** —— 且 `.effect` 是**空格分隔的列表** |
| 748 | `5a324d95e` | `eb728b874` | `§43` 36 条的**映射落地率**：**22/36 命中真实参考技能** · **14 条无对应** · **0 悬空** |
| 749 | `ed84a8945` | `37194e5ab` | `§43` 两条口径**对上了**：**交叉表 13/1/10/2/10 = 36** —— 无差异，是我上次数错 |
| 750 | `faaa52abd` | `489784e02` | observe_list D11 续：补判据 160~170（英雄技能全数 + `§43` 二维画像段） |
| 751 | `ff5d3f0ff` | `1c5cf73cf` | PLAN §6 补入：**`§43` 技能映射的二维画像** + 参考规模对照 |
| 752 | `8f01540dc` | `b90aa46a3` | `damage_axis`/`range_axis` 对照：**`range_axis` 与参考 `.type` 同构** · `damage_axis` 是**我方自加** |
| 753 | `768c9e563` | `b118a76a9` | 两批 `none` **完全同一集合**（16 条）—— 且 `damage_axis: mental` 有**一处语义存疑** |
| 754 | `d48453dab` | `0e48ce438` | 窗口新增第 ⑪ 件：一条字段自洽性存疑（`damage_axis: mental`） |
| 755 | `2157ef1ec` | `194b4de1d` | observe_list D11 续：补判据 171~175（轴对照 + mental 自洽段） |
| 756 | `6e50ce324` | `46421a9c3` | `morale_effects` 与 `damage_axis` **是解耦的** —— 且**互斥**（显式覆盖派生） |
| 757 | `5db379c4e` | `2b251b89f` | 注释与表**一致**：`−8/−12/−5` 正是 `morale_events.json` 的值 —— 注释没过期 |
| 758 | `42abe5e7e` | `587429e39` | 窗口第 ⑪ 件**收窄**：从「语义别扭」改为「机制自洽 · 只剩一个设计选择」 |
| 759 | `183725d20` | `3640e9646` | observe_list D11 续：补判据 176~183（士气耦合 + 表值核对段） |
| 760 | `5927c66f9` | `b0f070b3f` | `morale_events` 表**全数**：**18 条**（我只读了 3 条）· **3 条无消费点** |
| 761 | `b289be359` | `e74223371` | `battle_inspiration` 的疑问**由它自己的 `note` 答了** —— **不是冗余，是「对账锚点」** |
| 762 | `e994cbcb3` | `6aee9f6f4` | `morale_full_100` 的「自身回 50」**已实现** —— `morale.start = 50`，`L300` 就是它 |
| 763 | `c50d23d42` | `c280adea4` | observe_list D11 续：补判据 184~193（士气表全数 + 注释自解释段） |
| 764 | `6a9ba74a7` | `05398d1c2` | 「加载期校验」**确实存在** —— `Validate.ExpeditionSide.cs:550` fail-fast，理由写得极清楚 |
| 765 | `d70041b72` | `8b6b55411` | 校验调用链**全程确认**：`Parse` → `Validate` → `ValidateExpedition` → **`throw`**（4 步） |
| 766 | `9577f9038` | `5329d1fdf` | 校验链**追到顶**：`DirectorBridge.BuildFromRes`（生产加载点）→ … → `throw`（**5 步**） |
| 767 | `c646d2f81` | `fb10eab9d` | observe_list D11 续：补判据 194~202（校验链追顶段） |
| 768 | `95cdfba39` | `79c2fef74` | buff 防火墙**独立复核**：清单 **25 = 参考 25，一一对应**（0 悬空） |
| 769 | `9db40da97` | `2a9a60fb5` | `Pending` 24 个**是待办清单，不是缺口** —— 注释与排序理由都说清了 |
| 770 | `f8089eac8` | `4814284d2` | `BuffPrimitivesTests` **存在且双向** —— 注释承诺**再次属实**（并实跑 **11/11 绿**） |
| 771 | `856e388a3` | `26a42b5f3` | observe_list D11 续：补判据 203~213（buff 防火墙 + 待办清单段） |
| 772 | `5807bfb8c` | `7e33b61f1` | PLAN §6 补入：本仓**「加载即校验」清单**（两处，四级齐） |
| 773 | `68c36000d` | `2a3650518` | 消费点**两头都全** —— 但 `SkillExecutor` 走的是 `Scale` 而非 `ScaleByCaster`（**内联了取数**） |
| 774 | `c0a70a80c` | `4d3a61fbf` | `m2_activation_readings.md` 里藏着**一条已裁定的设计决定** —— 而它的**第 ② 项要求【未执行】** |
| 775 | `a3dc4e909` | `d093d18ca` | 窗口新增第 ⑫ 件：一条【架构已裁定但未执行】的要求（待接条件列） |
| 776 | `a08203d96` | `74350f9b9` | observe_list D11 续：补判据 214~224（激活链 + 旧报告审计段） |
| 777 | `e31a045cb` | `3988a8d95` | 实现架构 `M2-ROUTE-B-20260921` 裁定 ② 的那一列：**待接条件**（区分「缺载体」与「未接线」） |
| 778 | `77100333c` | `7f740cc63` | 窗口第 ⑫ 件更新：**那一列已实现**（含独立复算与零行为证据） |
| 779 | `ed73bbedd` | `08655b7fb` | 实现架构 `M2-ROUTE-B-20260921` 裁定 ② 的那一列：**待接条件**（区分「缺载体」与「未接线」） |
| 780 | `5913ecac0` | `3ef5dba6f` | 给「待接条件」列**补测试**（判据 229：新加的东西要有测试钉住） |
| 781 | `3b42bc74b` | `36af442fd` | observe_list D11 续：补判据 225~229（待接条件实现段） |
| 782 | `d5d0bdfa0` | `0b3112d43` | 17 条「未接线」的落点**逐条核完**：全部有落点说明 · 🔴 **2 条去向与说明不符** |
| 783 | `e18459eec` | `d44388cea` | 窗口新增第 ⑬ 件：2 条【去向与说明不符】的原语（请裁落点） |
| 784 | `e1109c618` | `587056018` | observe_list D11 续：补判据 230~232（未接线落点核验段） |
| 785 | `08b109c89` | `26c808ae2` | `stress_heal_*` 的落点**定案：趟级** —— 消费侧证据，**订正了我上轮的倾向** |
| 786 | `9555cd192` | `0e1da0fd4` | 窗口第 ⑬ 件更新：**已自查出答案**（趟级）· 并报一条结构问题给架构 |
| 787 | `f75af8e2d` | `9e813aaf1` | observe_list D11 续：补判据 233~237（落点定案段） |
| 788 | `df97d303b` | `0176796c3` | `resolve_*` 同族核查：**同族 · 共 4 条**（不是 2 条）⇒ 比改为 **11:13** |
| 789 | `d607acc59` | `840f29c81` | `MoraleMod` **6 条全核完**：**2 条战斗级（枚举对）· 4 条趟级（枚举错）** ⇒ 比修正为 **10:14** |
| 790 | `7da664e9a` | `ca1929a5d` | 窗口第 ⑬ 件再更新：同族 **4 条**（不是 2 条）· 比修正为 **10:14** |
| 791 | `864b9cbb7` | `c9a063f38` | observe_list D11 续：补判据 238~242（同族核查 + 全族审计段） |
| 792 | `f95ee734e` | `4f231f3e9` | `Target` 枚举**全 9 个取值核完**：**8 按【轴】· 只有 1 个按【层】** —— 根因确认 |
| 793 | `3d34ff1e7` | `7de46e7f9` | 其余 10 条原语的落点层：**全部在战斗侧** —— **误判是 `MoraleMod` 独有（4/21 = 19%），非普遍** |
| 794 | `94efc6918` | `f61cc026e` | 窗口第 ⑬ 件续：把「枚举混维度」核到全量 ⇒ 报**更精确的结论 + 两条修法** |
| 795 | `90c3501dc` | `dcec6ff24` | observe_list D11 续：补判据 243~250（枚举维度审计段） |
| 796 | `d2a20a155` | `6a4a08381` | 🔴 **自查发现：我 13 张单【全投错了地方】** —— 按策划 `#339` 纪律 B 补投到其收件箱 |
| 797 | `55f9772c8` | `39262b303` | 🔴 **修复我造成的一次数据丢失**：补投时覆盖了自己更早的一封投递 |
| 798 | `1a6db0997` | `278aa9ce8` | observe_list D11 续：补判据 251~253（投递纪律段） |
| 799 | `922fea8d7` | `f81af69f7` | observe_list D11 续：补判据 254~256（窗口纪律落地段） |
| 800 | `0992756a6` | `523e46bf5` | 祖产兑换表复核：**代码早已推翻「损失 50%」** —— 我独立复算得到同一结论 |
| 801 | `7fe98bd58` | `648503f21` | observe_list D11 续：补判据 257~260（数据断言独立复算段） |
| 802 | `d148c45de` | `a417937eb` | 数据断言**一次性核完**：7 项全对 · 🔴 **修掉 1 处已过期措辞** |
| 803 | `b9c0a27f0` | `4c499bdb7` | observe_list D11 续：补判据 261~263（数据断言复算段） |
| 804 | `3e339e085` | `a917a41bf` | 「设计参数」类断言复核：**`unlocks.json` 的「起手态」3 项全对**（累计 9 项） |
| 805 | `fc295111a` | `3dc89b182` | `roster_cap` 命名核查 + **修掉 3 处零行为问题**（注释/报错漏了 `_delta` · 缩进不一致） |
| 806 | `3a8739cd7` | `2843742cd` | observe_list D11 续：补判据 264~269（设计参数 + 措辞维护段） |
| 807 | `f24027acb` | `ff4dee987` | 枚举式措辞**全仓扫描** + **根治漂移**（清单抽成常量 + 补防线测试） |
| 808 | `f204fe912` | `92b71ca84` | observe_list D11 续：补判据 270~274（枚举措辞根治段） |
| 809 | `f1a0e229f` | `f052604ae` | 手写清单扫描 + **一条「防线不够强」的实测教训** |
| 810 | `5abf01172` | `b8d2313e9` | observe_list D11 续：补判据 275~284（防线强度段） |
| 811 | `6c397fc59` | `cfcfc8355` | 防线强度**全仓审计**：**98.6% 强断言** · 40 弱里只有 1 处真风险（已修 + 实测极限） |
| 812 | `f8638f894` | `b637e20e9` | observe_list D11 续：补判据 285~289（防线审计段） |
| 813 | `ed404ec98` | `65b7d8ef0` | `string.Join` 报错文案覆盖：**16 处（纠上轮"3 处"）** · 补 1 处防线 + 实测同形极限 |
| 814 | `48be2f711` | `04da3c7a5` | observe_list D11 续：补判据 290~294（报错文案覆盖段） |
| 815 | `543136d41` | `a70057f4d` | 「报数的可复算性」审计：**135 个数 · 60 自动可定位 · 75 需人读** —— 根因是「没扫全就报数」 |
| 816 | `6fb472c56` | `80cdc1dd8` | 补齐第 2 处「可钉未钉」：`KnownRoomTypes` 防线（**实测能红**）+ 又一次破坏实验的坑 |
| 817 | `eb50410c1` | `8a3ff6cb6` | observe_list D11 续：补判据 295~299（报数溯源 + 破坏实验段） |
| 818 | `02c964bad` | `161fcb43b` | 「采用参考来源」的**覆盖清单**：26 文件 · 10 有外部来源 · 🔴 **7 个连说明都没有** |
| 819 | `a2f5e4b70` | `e80aa9c33` | 7 个无说明文件的来源判定：**4 疑似「抄了没记」· 1 纯自造 · 2 概念+自造** |
| 820 | `d0e8e8cde` | `3a94d895a` | observe_list D11 续：补判据 300~308（采用范围 + 来源判定段） |
| 821 | `633393e95` | `47d6da1b7` | 逐条比对 `curios.json`：**6/6 有参考对应物，但只采用 6/60** · 🔴 且**数值语义不同** |
| 822 | `4132be8a1` | `29019d62d` | 投递（策划收件箱）：`curios.json` 采用率 **6/60** + 两件请裁 |
| 823 | `4ef74e040` | `48fa16f5d` | observe_list D11 续：补判据 309~312（逐条比对段） |
| 824 | `9fb2ec2da` | `47224ed4e` | 各层**采用率**实测：**4/6 层 100%** · 两缺口都有单号解释（`curio` 10% · `trinket` 40%） |
| 825 | `39ad7139a` | `a41072836` | observe_list D11 续：补判据 313~321（采用率实测段） |
| 826 | `7f2de5f3f` | `2aa22aa3a` | 来源口径核查：**26 文件里 16 个连"源"都没提** · 🔴 **并改判了上一件的一处结论** |
| 827 | `c9c62d7d9` | `740f36fdd` | 投递（策划收件箱）：🔴 **更正「quirks 含 7 条自加」的错误结论** + 一个"两个源并存"的口径问题 |
| 828 | `2bcdd7540` | `140fb092b` | observe_list D11 续：补判据 322~326（来源口径段） |
| 829 | `3e686f6e2` | `28bf7a94f` | 未标注文件的**源归属**判定：**探针质量决定结论** · `enemy_ai` 采用率 **3/160 = 2%** |
| 830 | `e83f60ff0` | `517460d64` | observe_list D11 续：补判据 327~331（源归属判定段） |
| 831 | `c09826826` | `9cfff9e43` | 用**结构**重判源归属：**多数层的字段名【两源都没有】** ⇒ 那是我方自有 schema |
| 832 | `1a58579c9` | `c8dcf64c4` | observe_list D11 续：补判据 332~335（结构判源段） |
| 833 | `6d15d24af` | `d6be43ef4` | `tuning.json` **内容层**比对：schema 自有 · 机制有对应 · 🔴 值未对齐（**已知待裁**） |
| 834 | `5fd262154` | `99d71686a` | observe_list D11 续：补判据 336~342（内容层比对段） |
| 835 | `b6b9304a0` | `a9936b00a` | 阶维度核查：**维度【已存在】，4 英雄 5 阶 `def_pct` 与参考逐一相等** · 🔴 **推翻我上一件** |
| 836 | `e1a23667a` | `fab8ad35f` | observe_list D11 续：补判据 343~344（阶维度核查段） |
| 837 | `5f1db454c` | `bfaf3dc93` | 武器 5 阶比对：**4/4 完全一致（5 阶 × 5 字段）** ⇒ **A3 的两侧都验完了** |
| 838 | `0d5d39846` | `1ca9c808e` | observe_list D11 续：补判据 345~348（对称侧复核段） |
| 839 | `561c6f072` | `39834d028` | 推送收口①：.gitignore 补精确路径忽略（根级日志 / unity_ref 探针 / 本地存档） |
| 840 | `8dbb38426` | `fd5b87d5f` | 推送收口②：新增 tools/check_file_budget.py + tools/dsh/ 15 个脚本（工具面收口） |
| 841 | `cebf52400` | `0021c3cd1` | 推送收口③：存档 Phase 1 —— 快照/序列化/迁移 + 场景接线 + 测试（goal10 交付） |
| 842 | `c75acb78c` | `dcc2c58b4` | 推送收口④：H-1 HeroGear 投影 + 伤害模型守卫（两半机制已在库内，减伤半接线在⑤） |
| 843 | `75ab95202` | `c7ac28461` | 推送收口⑤：sim 内核（UnitRuntime/HitStep/BattleProjector/…）+ 数据（units/heirlooms） |
| 844 | `8017d636e` | `e84165b45` | 推送收口⑥：UI 域（BattleUI.* ×10 · HamletRoot.* ×3 · UiPalette · ui_palette.tres） |
| 845 | `11b689a1c` | `ec45a5ecb` | 推送收口⑦：文档与报告（doc/** · reports/** · skills/*/SKILL.md） |
| 846 | `137890597` | `dd633bd88` | 推送收口⑧：`arch_20260926_o104_retraction.md` 批次更正补落（文档类） |
| 847 | `e5ceb6661` | `48a876ed1` | 推送收口⑨：三条红测归因修复（两条真修 + 一条 quarantine）+ 第一轮基线报告 |
| 848 | `3b4c07f7a` | `43abef66d` | 推送收口⑨b：白名单【死条目】修复（`check_data_discipline.py --all` rc=1 的真因） |
| 849 | `5cd994610` | `bfbeddba6` | 推送收口⑩：CI 一次接齐 9 条 Python 门禁 + 2 条 pwsh 门禁（M11 ④ 落地） |

## §4 孪生段：本地独有 230 条 vs 远端现役 230 条（同 subject，不同 SHA）

远端这 230 条（`41d287e..c53ff45`）已在 `origin/master` 上；本地 230 条与其 subject 一一对应、端点树逐字相同，实质差异 = 大日志的增删与 .gitignore 处置。本地副本只由安全绳分支保留。

| # | 本地 SHA（已废弃链） | 远端 SHA（现役） | 树 | subject |
|---|---|---|---|---|
| 1 | `b145d2f38` | `7ea7fcb80` | 异 | 片 4 收尾·全量冒烟留档：7 例（entry-main1/topology-auto/hamlet-next/e2e/map-mode/click-menu0/ui-audit）非环境 ERROR 全为 0 |
| 2 | `0776c1513` | `6df3f24f3` | 异 | 投架构：片 4 收尾证据包(V1=0 + 7 例冒烟全绿 + 提交号) + O-nn 记账清单 + 英雄资产加载器认领 |
| 3 | `882c2de6d` | `db1d73ad9` | 异 | 投 UI：走格接口就绪(瓷砖网格读数/驱动/三条边界) —— 格子主画面可开工 |
| 4 | `d95d7ee44` | `7a3b52226` | 异 | 主程序 (A) 收尾：格子视图顶部加信息行【当前房间类型 / 剩余段数 / 已揭示 x/y】（提示不再只活在 log 里）；10 入口复测全绿 |
| 5 | `6946a2e45` | `bac569753` | 同 | 处置 O-90：reports/ 只入库摘要与文档（忽略 *.txt/*.log，保留 *_summary_*.txt）+ git rm --cached 掉 4 个超大白跑冒烟日志（104.7MB/8.7MB/6.6MB/0.6MB）—— 与上次 2.8GB 事故同族，我的 git add reports/p4final_* 扫进去的 |
| 6 | `1f9bd3a89` | `d210ca649` | 同 | skill：新增第 4/5 条取证纪律（0 处判据必须 scoped + 提交前必查新增文件大小，O-90 同批） |
| 7 | `352f0ea40` | `5013724df` | 同 | 回架构：V1 口径已改 scoped(O 判据写进 skill 第4条) + O-90 三件齐处置(ignore/rm --cached/回收中) + skill 第5条(提交前必查新增文件大小) |
| 8 | `4ccf5af9c` | `6ef1920b3` | 同 | 修我引入的 1 对重叠：格子视图子项为手工定位 ⇒ 信息行与画布错开（画布下移 18px） |
| 9 | `1684d653f` | `cd5b4ce03` | 同 | 投架构：O-90 回收读数(loose 30.41MiB→0；pack 23.28MiB 仍在历史) + 判读与建议(不做历史改写,防复发已落地) |
| 10 | `17d47e363` | `d2369118e` | 同 | 修重叠根因：侦察标记/扎营面板**内部内容 314px 宽而面板未预留宽度** ⇒ 溢出压邻居（实测 1 对重叠）；改为预留 210 宽 + 按 720 收高 |
| 11 | `3f66115fc` | `9f1bb752b` | 同 | 英雄资产加载器（内核零 Godot · P31 门禁）—— 落实现；⚠️ 用例待补（如实标注，实现无人调用、合入安全） |
| 12 | `ba2947758` | `7463f8a6c` | 同 | 投架构：英雄加载器已落(内核零Godot·P31门禁·PlaceholderRoot)+用例待补如实标注+一处口径确认(锚点要求范围) |
| 13 | `fd434b328` | `529f90e32` | 同 | skill §14.0.3：记下外部权威变更（规格权威=ui_spec §15；用户裁定 (B) 瓷砖网格；③ 派生网格时才退役我这套房间+连线图）+ 我的零浪费动作（先做 WalkMapView 输入的去具体类型化） |
| 14 | `29aa5c6f0` | `dc63042e0` | 同 | 行走层渲染无关化（为 (B) ③ 铺路，零返工）：WalkMapView 只吃表现层快照 MapSketch（格子/连线/剩余段/当前类型）；旧签名保留为【适配器】FromExpeditionMap（行为不变）；③ 落地时只写 FromDungeonGrid 即换数据源 |
| 15 | `5328b8a94` | `1daecd740` | 同 | 消最后 1 对重叠（宽度预算）：地牢宿主由 ExpandFill 改 ShrinkEnd + 固定 240 宽（原先与 E 区争宽 ⇒ 合计超行宽 ⇒ 面板被挤到与 E 区信息行相压）；IndexOf 精确定位写法 |
| 16 | `ab621ffb1` | `9a90dd17d` | 同 | 协议收尾：① 投主程序窗口（复核 0 错误 / 时序我选'我改读点' / 面板已改读公共读处 / (A) 进度 / (B) 已铺路 / 相机 11 入口全绿）② 收件箱转写 ⑦（5 封，含主程序【走格接口已就绪】的完整 API 面）③ 清空我的收件箱 |
| 17 | `a67b40322` | `ee9bdec62` | 同 | (B) ③ 落地：走格适配器 FromTileWalk（瓷砖网格⇒表现层快照 MapSketch）+ 地图页接线（TileWalkEnabled 时用瓷砖主画面；点击格⇒TryStepTile(dx,dy)，false=墙/越界则内核状态零变化）；渲染类一行未改 |
| 18 | `fc0534385` | `1a5b33f54` | 同 | 英雄资产加载器【用例补齐】：7 条（H1/H2 + P31 硬形状 + 合规），结构化序列化造样本（不再碰字符串字面量） |
| 19 | `d7bb9d8ee` | `fd108a70b` | 同 | 投架构：英雄加载器用例已补齐(544→551,含 H2 点名) + 结构化序列化做法 + O-90 裁定照办 + 锚点口径待裁 |
| 20 | `38414fc4d` | `0ac0d8f3c` | 同 | 策划 #348③：战斗单帧占位渲染（玩家卡画占位 sprite/combat.png，只从 HeroAssets.PlaceholderRoot 读，引用相对根解析；取不到⇒回落色块+首字并留痕；有立绘则不叠首字）+ 自证读数；6 入口判据 0-0 |
| 21 | `339c4a301` | `8ace00d75` | 同 | 修正占位载入：未导入的 png 走 ResourceLoader 会报 'No loader found'（实测 E=4）⇒ 改 Godot.Image.LoadFromFile（静态）+ ImageTexture.CreateFromImage；战斗单帧占位自此可见 |
| 22 | `00f1e8758` | `99d090478` | 同 | 卡片 #348 V1/V2：拿【真占位数据】跑验证（7 槽/必需 5 有落点/三条拒绝路径点名）+ Load(root) 重载 + portrait 同纪律支持 |
| 23 | `c7d0f010f` | `5629df2d5` | 同 | 投策划：卡片 #348 V1/V2/V5 真数据验收读数 + Load(root)/portrait 已实现 + V6 诚实分开写(能验/不能验) |
| 24 | `c35e93242` | `dd148b555` | 同 | 投策划窗口：V3/V4 已达成（战斗单帧占位自证读数 + 六入口判据/计数/0 错误）+ V6 能验/不能验分开写 + 记录'占位 png 不能走 ResourceLoader'这个坑 |
| 25 | `ae0ede4d1` | `b6404a4c5` | 同 | 协议收尾：收件箱⑧转写（3 封策划信 + 我的 V3/V4 执行与'占位 png 不能走 ResourceLoader'这个坑）⇒ 清空收件箱 |
| 26 | `4dd58a9d4` | `5cb22227a` | 同 | 片 4 收口修：`--e2e` 完整回路跑通（阶段2 曾永远到不了）+ 修一句"会撒谎的打印" |
| 27 | `038c4f90a` | `e38d0c4be` | 同 | 投架构：--e2e 完整回路跑通(阶段2 曾永远到不了,根因=冒烟走完即Quit抢先) + 修一句会撒谎的打印 |
| 28 | `08ec70c3e` | `6f032f8a6` | 同 | skill：新增第 6/7/8 条 —— (单段绿≠回路通:端到端才暴露的断点,自管生命周期 CLI 不得自动退出) + (会撒谎的打印:期望值不许写死) + (测试样本用结构化序列化,不用多行原始字符串) |
| 29 | `33123b27b` | `888bd7493` | 同 | skill §14.0.4/§14.0.5：记规则②进度与两个卡点（dump 不写文件 / 审计显式清单 32 vs 33）+ 七条反复踩的工程坑（shell 拼代码·设了被覆盖·删创建不删使用点·红构建不许提交·未导入资源不能走 ResourceLoader·极端读数先怀疑口径·中途长大的元素要打 3 次） |
| 30 | `e30891b53` | `a31f73f77` | 同 | 用户规则②落地：5 处空闲占位改【半透明】——店长位用 PlaceholderFill、推荐位补半透明色块、名册头像/技能图标/5-6号位保留原型色相但 α 取调色板（WithPlaceholderAlpha）⇒ 不丢区分又一眼看出'待填'；11 入口复测 |
| 31 | `f413c5a86` | `0f671fb2e` | 同 | skill：用户定稿【每次任务开始前先看一眼窗口】写成固定前置动作(读自己+扫其他三窗口+先处理待办) + town 步骤接上宿主(与 --hamlet-next 共用实现,含打印自证) |
| 32 | `2b2ed0f83` | `ff7187a05` | 同 | 策划 #348③：新增共享占位读取入口 HeroArt（战斗单帧 + 名册头像，只从 PlaceholderRoot 读、Image 直接解码绕过导入系统）+ 名册头像接上（P31 的 portrait 字段已在代码里；有图则画、无图保留半透明色块）；BattleUi 改为委托共享入口（消两处真值） |
| 33 | `61ebd89ec` | `7096c34e5` | 同 | 策划 #348③：名册头像确认生效（自证：'[UI 占位英雄] ✅ portrait 用占位：…/placeholder_pw/portrait.png（只从 PlaceholderRoot 读）'）+ 移除诊断探针（避免死声明，红线 21） |
| 34 | `657a92512` | `4db2e3d67` | 同 | 投策划窗口：V3 两半（战斗单帧+名册头像）自证 + 11 入口 V4 读数（全绿）+ V6 能验/不能验分开写 + '构建红时的 Godot 运行不可信'这条验证口径教训 |
| 35 | `a2bf7d953` | `a732040a7` | 同 | 片 4 收口：`town` 步骤接宿主（与 --hamlet-next 共用实现 + 打印自证）+ 同场景续跑钩子（⚠️ 实测未命中路径 ⇒ 如实标注未修好） |
| 36 | `2e923931a` | `5b9da44b2` | 同 | skill 收件箱转写⑨：策划'撤退/放弃远征'可抄信（§8 用词规范三条硬要求 + 我的活=行走模式加【放弃远征】入口含二次确认 + 我自曝'现有撤退按钮可能是被禁反例' + §9 V10 结局口径改为三类/撤退移入场级量 + 新读数'撤而不弃'占比） |
| 37 | `58689dbf5` | `2183aa640` | 同 | 投主程序窗口：撤退/放弃远征我侧已备好（按住'撤退'重命名以免成为被禁反例；预留 SetAbandonAction 不破签名、没回调就不显示按钮）+ (B) 开关催办（全项目无人调 EnableTileWalk）+ 共享'构建红时运行不可信'的验证口径纪律 |
| 38 | `9924abecd` | `55586de0f` | 同 | retreat.md §8 我侧落地（不依赖宿主的部分）：新增【放弃远征】入口——只在行走模式可见（与'撤退'天然不同屏，满足§8①）+ 二次确认模态（不可逆，§8②）+ tooltip 写清后果（§8③）；回调走 SetAbandonAction（不破 Bind 签名；未注入⇒按钮不显示，红线 21） |
| 39 | `e954b1777` | `e0c1fa44f` | 同 | 红线 21 补留痕：宿主未注入【放弃远征】动作时，打印'该按钮不显示（不假装可用）'一次（不留不可解释的状态） |
| 40 | `01fa3633d` | `5d9aaea16` | 同 | skill 收件箱转写⑩：策划 RETREAT-SPLIT（用户裁定 DD 形态 ⇒ 拆撤退/放弃远征两动作 + 分工 + 我已做的入口/二次确认/不同屏/按住撤退重命名的理由） |
| 41 | `bd8738946` | `544c34ea6` | 同 | 补提交规则②的调色板改动（此前只提交了使用侧）：UiPalette 新增 PlaceholderFill 字段+Default 值+Fields 显式清单 · DdTheme 访问器 + DumpPalette 改为写 Default() · ui_palette.tres 重生成（含 PlaceholderFill，审计 33 项一致）· 补两个 .uid |
| 42 | `1625e613f` | `4c74b18dc` | 同 | skill §14.0.6：末轮两条新坑 —— ⑧'改了但没提交'与'改了没生效'一样危险（绿灯验证后先 git status 再报完成）· ⑨取读数别用 MatchInfo.Matches[1] 取捕获组（'读数全空'要先怀疑自己的取数） |
| 43 | `0d7d23e79` | `7f132538b` | 同 | 清 #358 两处"名字与实现不符" + #358④/UI 催办：**把 EnableTileWalk 真正调起来**（走格接口不再无人用） |
| 44 | `4733a4c22` | `09b5e40c9` | 同 | 投 UI：EnableTileWalk 已真正调用(宿主进地牢即开走格,段消耗取数据) + 走格读数与三条边界 + 冒烟步骤入口请他们给名 |
| 45 | `4b5e6ab62` | `496994b81` | 同 | (B) 瓷砖网格自证入口：--tile-step（走我真实回调 OnRoomClicked ⇒ TryStepTile，用于红线 25 证据链重走）+ --tile-walk（宿主未开时的兜底）；实测瓷砖主画面 89 格/124 连线 + TryStepTile=True + 判据全绿 |
| 46 | `72c3c61d3` | `1bc74f57f` | 同 | skill §14.0.7/§14.0.8：(B) 瓷砖网格已验证（89格/124连线 + TryStepTile=True + 判据全绿；渲染类一行未改）+ 'grep 不递归'这个坑我踩了两次的纪律 + §8 撤退/放弃远征卡在两处外部（IsFinished 未拆 ⇒ 我按住不改文案） |
| 47 | `05bc18d74` | `ce7333673` | 同 | 协议闭环：收件箱转写⑪（英雄资产接入验证收口 V1~V6 全齐 + 我的验证口径教训被记进纪律#355 + 撤退拆分状态表 IsFinished 未动）⇒ 清空收件箱 |
| 48 | `dd05a19c5` | `66f2b2ba8` | 同 | retreat.md §8 我侧收口（主程序已拆 IsFinished ⇒ 解锁）：撤退按钮文案改「撤退（退出这场战斗）」+ tooltip 写清后果（退本场·回当前格继续走·有士气惩罚无胜利收益·要结束本趟请用放弃远征）；两处赋值点措辞统一 |
| 49 | `d52d127a4` | `dd01d97e8` | 同 | §8 文本级自证：一次性打印【撤退/放弃远征】两个按钮的界面用词与可见性（含模式与宿主是否已注入）；修一处编译错（模式三元表达式） |
| 50 | `169b55063` | `3ab26ea81` | 同 | 修红 + §8① 收口：撤退按钮按模式裁决（地图模式不再与放弃远征同屏）；统一用字段 _mode（同名代码两处都能编译） |
| 51 | `2723bc8fa` | `3d37ec469` | 同 | skill §14.0.9：⑩'同名代码两处'陷阱（踩两次，改共享行只用字段/先 grep 数出现次数）· ⑪ 文本级自证能抓到可见性 bug（地图模式撤退/放弃远征同屏被我当场抓到并修） |
| 52 | `fa01ce533` | `1e227714b` | 同 | §8 补最后一块：冒烟入口 --abandon ⇒ 按下【放弃远征】弹出二次确认（只弹不确认，避免真结束一趟）⇒ 验证弹窗态判据闭环 |
| 53 | `802c95485` | `261951605` | 同 | #352/#360 策划点名的【主程序两件】已完成：IsFinished 语义拆分（撤退不再结束本趟）+ 放弃远征入口注入 UI |
| 54 | `983a1ba2f` | `61cbbf2b6` | 同 | 投策划(答复点名信)：你点名的两件已完成(IsFinished 拆分 + 放弃远征注入 UI),附 554/554 证据 + 抓到一条既有用例依赖旧契约 |
| 55 | `53f628d0f` | `4a642dbc6` | 同 | 清 #357 欠账：R8（一趟退 2 场 ⇒ 恰扣 2 次）+ 又一处"注释与实现不符"（−10/−5 ⇒ −12/−5） |
| 56 | `2ea45cb64` | `66d7564b5` | 同 | 清 #361 欠账：IsRoomResolved 用例（他④ 的建议）+ 登记 retreat 冒烟步骤 + R8 已完成 |
| 57 | `dc6f3448a` | `786e8b538` | 同 | 投策划(#361 三点回复)：--abandon 是 UI 侧 CLI 旗标(非步骤表)+retreat 步骤已登记但需 UI 公共入口+IsRoomResolved 用例已补+R8/R9 已完成 |
| 58 | `485478828` | `47d8cf491` | 同 | 投 UI：解锁请求(暴露 PressRetreat 公共入口 或 加 --retreat 旗标,与 --abandon 同形)+告知走格已开启与放弃按钮已显示 |
| 59 | `72d743b28` | `12b84a50f` | 同 | 用户 8 条改版第一批：① 进地图模式不再自动开地图页 ② 删掉底部最右的侦察/扎营面板（含修我删创建块漏删使用点导致的每帧 NRE）③ 紫框底色提亮 22%（不再黑得看不清）④ 地图改为框内拖拽+滚轮缩放+ClipContents（永不超出框）⑤ 技能改小方块 56×56 且去掉滚动 ⑥ 橙框（角色+技能）在上、紫框（英雄详情）在下；5/6 号位合并到左侧同框且两种模式都显示 |
| 60 | `066ab6e84` | `4a447897c` | 同 | 修红：删掉被移除面板的最后使用点（_mapModeScoutMark.Refresh ⇒ 每帧 NRE，E=1~5）；现在底栏不再有侦察/扎营面板 |
| 61 | `53f241ada` | `04654d08c` | 同 | 🎯 片 4 遗留【同场景步骤不续跑】根因找到并修复：自动放行分支的**无条件 return** 挡住了后续步骤 |
| 62 | `27b2227bf` | `ad32fb9be` | 同 | 投 UI：报一条栈在 HamletRoot.Refresh 的 ERROR(我的修复让该路径首次被走到)+复现命令+请他们判归属 |
| 63 | `ba0866813` | `21847fd84` | 同 | #371 清账：代码侧已核【干净】+ 撤退打印自证已加（文档侧转架构） |
| 64 | `944f7f38e` | `a979831bd` | 同 | 投架构(转 #371 文档清账 A/B 清单+代码侧已核干净) + 投 UI(HamletRoot NRE 精确假设:主题初始化顺序) |
| 65 | `3ab00049e` | `4c3744669` | 同 | 用户更正：紫框（多功能框）回右侧原位 + 技能框下方新增【角色详情框】（紫边，显示当前角色 HP/士气/槽位/状态）+ 技能方块 56→48（全部展示不滚动） |
| 66 | `bdb1e317d` | `b8f183dfb` | 同 | 用户要求：主城左侧的【减压(酒馆/修道院)·招募·服务·建筑信息·升级状态·服务状态】六块全部搬进【建筑详情】弹窗（主屏不再一眼可见）；入口仍是那个建筑按钮；冒烟按键路径不变 |
| 67 | `02620b093` | `429486aab` | 同 | 修红（我上一提交把 \  当字面量写进 C# ⇒ CS1056）：字段声明恢复为三行；主城六块搬进建筑详情保持不变 |
| 68 | `411459ea7` | `6d1e00ed7` | 同 | 清 #371 代码侧：7 处过期"撤退"口径注释已改 + 冒烟续跑的收窄（含"不是我改的"归因证据） |
| 69 | `04690aff8` | `6a0600933` | 同 | 投 UI：冒烟 ERROR 归因证据(我 stash 自证修复前同样有 1 条)+栈指向 HamletRoot.Refresh+BattleRoot 延迟调用+作废旧的全绿基线声明 |
| 70 | `0bb8e8fff` | `425e657fa` | 同 | UI 编辑器化【第 1 步·地基】：建 scenes/ui/ + 名册行模板场景 roster_row.tscn（节点名与既有代码/验收读数一致：RosterRowBody/PortraitFrame/PortraitPlaceholder/RosterInfo）· [Tool] 脚本 RosterRowTemplate.cs（编辑器里可见占位内容，Engine.IsEditorHint() 守卫）——混合方案：静态骨架进 .tscn、动态项用模板实例化 |
| 71 | `971094384` | `84242c3ea` | 同 | UI 编辑器化 A【接线】：名册行改为实例化模板场景 scenes/ui/roster_row.tscn（[Tool] 编辑器可编辑）+ 场景缺失/类型不符时回落代码构建（留痕）；RosterRowTemplate.TryInstantiate() 提供入口；节点名保持不变 ⇒ 判据/S1/右键头像/详情路径全部沿用 |
| 72 | `18d0e5781` | `6703a73c0` | 同 | UI 编辑器化 B【技能方块】：scenes/ui/skill_box.tscn（48×48 + font_size 20 可在编辑器里直接改）+ [Tool] 预览脚本；BattleUi 技能方块改为实例化模板（数据仍由代码填；场景缺失则回落） |
| 73 | `26b83a42c` | `929b21ce9` | 同 | UI 编辑器化 B【弹窗内容行】：scenes/ui/popup_line.tscn（换行/裁切/字号可在编辑器里改）+ [Tool] 预览；HamletRoot.PopupLine 改为实例化模板（场景缺失则回落）；全部弹窗态判据仍绿 |
| 74 | `5af4fa408` | `a564ef00b` | 同 | skill §14.0.10：用户新硬规则【重复 UI 元素必须抽成可复用模板】（≥2 处同构 ⇒ 必须模板化，禁止多处各 new 一遍）；列出已抽 3 个 + 待抽 6 项（按复用次数排序：战斗卡牌 10 张收益最大） |
| 75 | `5adb79bfd` | `0c3c9f8ec` | 同 | UI 编辑器化 B【战斗卡牌·10 处复用】：scenes/ui/unit_card.tscn（10 个命名节点，外观可在编辑器里改）+ [Tool] 预览；BuildCard 改为实例化模板（FillCard 只填数据），场景缺失则回落代码构建（节点名保持一致） |
| 76 | `12bae01ca` | `90033f7c5` | 同 | UI 编辑器化 B【建筑切换按钮·3 处复用】：scenes/ui/building_nav_button.tscn + [Tool] 预览；建筑详情弹窗左列切换按钮改为实例化模板（场景缺失则回落，节点名 BuildingNav_<id> 不变） |
| 77 | `4a2cadc20` | `c36d42a86` | 同 | UI 编辑器化 B【多功能页签·4~5 处复用】：scenes/ui/mf_tab.tscn + [Tool] 预览；多功能框页签改为实例化模板（场景缺失回落） |
| 78 | `051daeb99` | `5dba25fa9` | 同 | UI 编辑器化 B【5/6 号位行·2 处复用】：scenes/ui/back_slot_row.tscn + [Tool] 预览；FillBackSlots 改为实例化模板（场景缺失回落，节点名 BackSlot{n}Title/Frame/Name 不变） |
| 79 | `478f11930` | `26e361970` | 同 | Revert "UI 编辑器化 B【5/6 号位行·2 处复用】：scenes/ui/back_slot_row.tscn + [Tool] 预览；FillBackSlots 改为实例化模板（场景缺失回落，节点名 BackSlot{n}Title/Frame/Name 不变）" |
| 80 | `6534b1e83` | `5c9b9aa52` | 同 | UI 编辑器化 B【5/6 号位行模板·就位待接线】：scenes/ui/slot_row.tscn + SlotRowTemplate.cs（[Tool] 预览；TryCreate 输出 BackSlot{n}Title/Frame/Name 以与既有验收读数一致）——上一轮碎片化替换失败已回退，本轮只重建模板，接线下一轮一次改完整段 |
| 81 | `501d8fe0d` | `6b3a94e31` | 同 | UI 编辑器化 B【5/6 号位行接线·重做成功】：FillBackSlots 整段改为实例化 slot_row.tscn（模板优先 + 回落一次写完，节点名 BackSlot{n}Row/Title/Frame/Name 不变）——上一轮碎片化替换失败已回退，这次用 edit 工具整段替换，计数保持 55/38 |
| 82 | `18f8a9021` | `efb78aca7` | 同 | Revert "UI 编辑器化 B【5/6 号位行接线·重做成功】：FillBackSlots 整段改为实例化 slot_row.tscn（模板优先 + 回落一次写完，节点名 BackSlot{n}Row/Title/Frame/Name 不变）——上一轮碎片化替换失败已回退，这次用 edit 工具整段替换，计数保持 55/38" |
| 83 | `14632d9f6` | `6ca14169a` | 同 | 修 SlotRowTemplate 自身 bug：**先改名再按旧路径取节点** ⇒ GetNodeOrNull 返回 null ⇒ 每帧 NRE（实测 3998 次）——改为先取局部变量再改名（我自己的经典错误，留档） |
| 84 | `d450256f1` | `4c17ba679` | 同 | UI 编辑器化 B【5/6 号位行接线成功】：FillBackSlots 实例化 slot_row.tscn（模板优先+回落，节点名不变）；六入口真错=0（判据=只认行首 ERROR: 且排除本机 certificate store 与退出时 RID 泄漏）；计数 55/38→57/40（模板每行多节点，可解释） |
| 85 | `0206e2982` | `ecd82dadb` | 同 | skill §14.0.11：模板化施工四条硬教训 —— ①改名失效取节点路径（每帧 NRE ⇒ 刷新被打断 ⇒ 元素掉一半）②验证判据必须行首锚定 ^ERROR: + 排除 certificate store 与 leaked at exit ③两次同现象换假设别改代码 ④共享文件被并发编辑时停手 |
| 86 | `2c414d7a0` | `eeafe412b` | 同 | UI 编辑器化 B【主菜单骨架·就位待接线】：scenes/ui/main_menu.tscn（MenuMargin/MenuCol/TitlePanel/OptionsPanel/StatusPanel 全部命名，可在编辑器里拖拽改边距与尺寸）+ MainMenuSkeleton.cs（[Tool] 预览 + TryInstantiate）；**未接线**（接线需同时改 MainMenuRoot 建树与刷新，单独一轮） |
| 87 | `64ad9d08d` | `95a564597` | 同 | UI 编辑器化 B【主菜单骨架接线】：MainMenuRoot 建树改为**骨架优先**（scenes/ui/main_menu.tscn 的 MenuMargin/MenuCol/TitlePanel/OptionsPanel/OptionsCol/StatusPanel 全部采用，边距/间距/尺寸可在编辑器里改）+ 场景缺失回落代码构建；节点名保持不变 |
| 88 | `130258534` | `f022fed22` | 同 | UI 编辑器化 C【主城骨架·就位待接线】：scenes/ui/hamlet_skeleton.tscn（HamletMargin/HamletRootCol/TopBar/TopRow/StatusBar/Body/LeftColumn/LeftCol/RightColumn/RightCol —— 节点名与代码逐字一致，接线后判据/S1/冒烟锚点不变）+ HamletSkeleton.cs（TryInstantiate）；**未接线**；BottomRow 类型未核 ⇒ 本轮不放进骨架（不猜） |
| 89 | `0b78cbf32` | `52e0f8f98` | 同 | UI 编辑器化 C【主城骨架接线】：HamletRoot 建树改为骨架优先（hamlet_skeleton.tscn 的 HamletMargin/HamletRootCol/TopBar/TopRow/StatusBar/Body/LeftColumn/LeftCol/RightColumn/RightCol 全部采用 ⇒ 边距/间距/栏宽在编辑器里改）+ 场景缺失回落；节点名不变；BottomRow 仍代码建（如实标注） |
| 90 | `65bd09394` | `8f8133048` | 同 | 骨架加载加【成功留痕】（此前只有失败才打印 ⇒ 我一度误判'城池未采用骨架'）：主城/主菜单 TryInstantiate 成功各打印一行 ⇒ 以后'骨架是否真生效'可直接验证 |
| 91 | `4cf339181` | `eef4184a1` | 同 | UI 编辑器化 C【底栏入骨架】：hamlet_skeleton.tscn 补 BottomBar(PanelContainer)/BottomRow(HBox)；HamletRoot 骨架优先采用（缺失回落），子项仍代码追加；节点名不变；六入口真错=0 |
| 92 | `6fc130b5c` | `7064b5a59` | 同 | UI 编辑器化 C【建筑详情弹窗骨架·就位待接线】：scenes/ui/building_popup.tscn（BuildingSplit/BuildingList 220宽/ShopkeeperSlot 96高/ShopkeeperPlaceholder/BuildingContent —— 节点名与代码逐字一致）+ BuildingPopupSkeleton.cs（含**成功留痕**）；未接线 |
| 93 | `04db4a3b0` | `897f9fad6` | 同 | UI 编辑器化 C【建筑详情弹窗接线·C 收口】：OpenBuildingPopup 骨架优先（BuildingSplit/BuildingList/ShopkeeperSlot/BuildingContent 全采用 ⇒ 左列宽/店长位高/间距/内容占比在编辑器里改）+ 缺失回落；导航与内容仍代码追加；节点名不变；六入口真错=0 + 成功留痕 |
| 94 | `d3414d599` | `6fd3cd043` | 同 | UI 编辑器化 D 第一轮【战斗顶栏骨架·就位待接线】：scenes/ui/battle_topbar.tscn（BattleTopRow/TopLeftGroup→MissionLabel/TorchWrap/RightGroup —— 左上任务·正中火把条容器·右侧顺序与意图三区可编辑）+ BattleTopBarSkeleton.cs（成功留痕）；火把条本体是 C# 类 ⇒ 容器进骨架、本体仍代码建（不猜）；未接线 |
| 95 | `7d215f514` | `1a4f81012` | 同 | UI 编辑器化 D【战斗顶栏接线·重做成功】：改用**字段承载骨架**（避开作用域问题）+ **逐处构建**（每处都过构建才继续）⇒ BattleTopRow/TopLeftGroup/TorchWrap 全部采用骨架、缺失回落；节点名不变；六入口真错=0 + 成功留痕 |
| 96 | `ab7c49734` | `b7e620bd4` | 同 | UI 编辑器化 D 第二轮【战斗底栏骨架·就位待接线】：scenes/ui/battle_bottombar.tscn（BottomRowBox→BackSlot5/LeftStack(CArea+ActorDetailBox)/EArea/DungeonHost 五区，**顺序即布局**，节点名与代码逐字一致）+ BattleBottomBarSkeleton.cs（成功留痕）；未接线 |
| 97 | `426bc194b` | `abbffda1a` | 同 | skill §14.0.12：骨架必须【全有或全无】地采用 —— 实测'只接一区'导致 结构半分裂（少控件 + 偶发 NRE + 局部越界）；配两条已验证改法：字段承载骨架 + 逐处替换每处构建 |
| 98 | `a5218a505` | `7c052081d` | 同 | skill §14.0.13：战斗底栏接线机械清单（六处容器 + MoveChild 顺序 + 逐处构建 + 判据）—— 供换轮/上下文压缩后照做，避免重复侦察 |
| 99 | `706a972e5` | `da3399b3e` | 同 | skill §14.0.14：底栏接线未解现象存档（已排除 写盘竞态/类作用域/字段缺失/static 四种可能，红只在'字段+第2处+第3处'同时应用时出现 ⇒ 嫌疑集中在第2处；下一轮二分定位；备选降级=改用局部变量） |
| 100 | `63c14e623` | `8d356526a` | 同 | 编码卫生（红线 20）：清理我域内 14 个文件的 BOM（早前 Set-Content -Encoding utf8 引入）—— 统一为无 BOM UTF-8；构建绿 + 三入口判据不变（BOM 对 C# 合法，但遵守项目编码纪律） |
| 101 | `9ed2100f4` | `d7aec0b6a` | 同 | UI 编辑器化 D【战斗底栏接线成功·两段写】：段1=字段+实例化(整个骨架入树)，段2=五处容器 adopt（BackSlot5/LeftStack/CArea/ActorDetailBox/EArea/DungeonHost 按名取、缺失回落、已挂不重复挂）；节点名不变；判定=分段落盘核对(2→8)+构建重试到绿 |
| 102 | `fe3ec70e5` | `8e7b12b82` | 同 | Revert "UI 编辑器化 D【战斗底栏接线成功·两段写】：段1=字段+实例化(整个骨架入树)，段2=五处容器 adopt（BackSlot5/LeftStack/CArea/ActorDetailBox/EArea/DungeonHost 按名取、缺失回落、已挂不重复挂）；节点名不变；判定=分段落盘核对(2→8)+构建重试到绿" |
| 103 | `78fe52b99` | `65ad2e3a2` | 同 | skill §14.0.15：E 行走地图改 TileMapLayer 施工方案（侦察结论：WalkMapView L216 Refresh 里 L265/286/307 三处手绘 ColorRect 待替换；缩放/拖拽/ClipContents 必须保留；瓦片纹理用引擎内置生成或美术图集；Node2D 与 Control 裁切的风险提示） |
| 104 | `6544c5955` | `361591034` | 同 | UI 编辑器化 E 第1步【行走地图瓷砖骨架·就位待接线】：scenes/ui/walk_map_layer.tscn（Control(clip_contents)+TileMapLayer WalkTileLayer）+ WalkMapSkeleton.cs（[Tool] + 成功留痕 + **运行时用引擎内置生成 16×16 双色图集**（房间/走廊）⇒ 不引入美术文件、颜色取自 DdTheme）；未接线（Refresh 仍手绘，见 skill 14.0.15） |
| 105 | `bdd97a963` | `22a0e7336` | 同 | skill §14.0.16：E 接线几何约束 —— 房间 14px vs 走廊 5px ≠ 单一 TileSet 的 TileSize ⇒ 必须**两个 TileMapLayer + 两个 TileSet**（否则走廊变粗=视觉变更，红线 19）；附 Refresh 逐行现状（清空循环要跳过骨架、热区保持代码创建、缩放/拖拽/裁切保留） |
| 106 | `05de7531f` | `2da86d123` | 同 | UI 编辑器化 E 第1.5步【双层瓷砖骨架】：walk_map_layer.tscn 扩为 WalkCorridorLayer(5×5)+WalkRoomLayer(14×14)（几何与手绘现状逐像素一致，skill 14.0.16）；脚本建**两个 TileSet**（房间 4 态：未知/已访/当前/终点，颜色取自 DdTheme；走廊 1 态 MapEdge）+ ClearTiles()；成功留痕；未接线 |
| 107 | `128552997` | `aeb81c73e` | 同 | skill §14.0.17：未解现象归档（两文件复现）——新增字段在'第二个方法'里 CS0103（BattleUi._bottomBarSkel / WalkMapView._walkSkel）；已排除写盘竞态/BOM/类作用域/static/字段未插；下一轮实验=改用方法内局部变量；建议向【主程序窗口】提最小复现 |
| 108 | `b9e659800` | `42edc1670` | 同 | UI→主程序 投递（标记 CS0103）：新增字段在 foreach 块内使用时 CS0103（WalkMapView._walkSkel / BattleUi._bottomBarSkel），附最小复现+已排除项，请判定是否 Godot.NET.Sdk 4.6.1 + .NET SDK 10.0.400 环境问题；E/底栏接线暂时冻结；skill 台账同步登记 §14.0.18 |
| 109 | `ecf8c6f34` | `9c5956d07` | 同 | skill §14.0.19：编辑器可直接干预清单（交付版）—— 13 个场景逐个列出'改什么/影响哪里/验证入口'，标注 2 项未接线；附通用规则（别改节点名、验收命令） |
| 110 | `3100960db` | `994093ce5` | 同 | skill §14.0.20：更正 CS0103 真因=我在 shell 里对 C# 做多行文本替换（证据：原有局部变量 currentId 也报不存在 ⇒ 级联）；自此硬规则=C# 文本一律用 edit 工具；症状辨识=CS0103 报'刚声明的名字'即怀疑上一处替换 |
| 111 | `f1188edc2` | `82b8cf595` | 同 | UI 编辑器化 E【行走地图改用引擎内置 TileMapLayer 成功】：房间14px四态瓦片／走廊5px瓦片（骨架优先、缺失回落手绘）；清空循环跳过骨架+ClearTiles；热区 RoomHit 仍代码创建；缩放/拖拽/裁切保留；**全部改动用 edit 工具**（§14.0.20）+ 只构建一次 + 先验证后提交 |
| 112 | `4c0a9dbbb` | `b71b2d181` | 同 | skill §14.0.21：战斗底栏接线 12 处 old→new 照抄清单（E1-E12，含 4 处挂载/重排守卫；用 edit 工具 + 只构建一次 + 先验证后提交；附两个已踩过的关键坑） |
| 113 | `e9272d253` | `13273af99` | 同 | UI 编辑器化 D【战斗底栏接线成功】：battle_bottombar 整体入树 + 六处容器按名取 + **五处挂载/重排守卫**（_slotLeft AddChild+MoveChild · leftStack.AddChild(_cArea) · actorDetailBox/leftStack 挂载 · _eArea 挂载 · **DungeonHost 挂载**）；节点名不变；七入口真错=0 + 底栏骨架成功留痕；作业=行数组手术+只构建一次+先验证后提交 |
| 114 | `388f1eae2` | `55e197856` | 同 | 修正【编辑器里看不到东西】：13 个场景全部挂上项目主题 res://resources/theme/dd_theme.tres（此前 0 个带 theme ⇒ 2D 视图里 PanelContainer 无样式即不可见、Label 空 ⇒ 画布空空如也）+ 按资源数修正 load_steps；六入口判据不变、真错=0 |
| 115 | `9c2c02b4c` | `704b8136f` | 同 | 投 UI：异常正文抓到(Image.LoadFromFile 失败,全链在 scripts/ui/**)+撤回我上一条假设+三个可查方向+每帧重试建议 |
| 116 | `93743d2a0` | `008634eab` | 同 | 补投架构：重述未闭项(其窗口被清过,附已完成的核对)+新扫到两处旧口径(m6_verification.md:29/m5_enemy_ui.md:203)+仍缺 abandon 事件类型与 portrait 登记 |
| 117 | `2062de622` | `4ad14b34c` | 同 | 修【UI 侧真 bug·主程序 2026-09-16 报告】：HeroArt 用 Image.LoadFromFile(res://…) 必失败且**失败不缓存 ⇒ 每帧刷 ERROR** ⇒ 改为 FileAccess.GetFileAsBytes + Image.LoadPngFromBuffer（**支持 res://**）+ 成功/失败都置 done ⇒ 绝不重试；留痕明确'已缓存失败、不再重试' |
| 118 | `8a884477d` | `bf99b57e1` | 同 | 处理窗口：转抄主程序来件(DELIVERY-LEAD-ERRTEXT-NAILED-20260916)至 skill §14.0.22 + 回执修复(DELIVERY-UI-HEROART-FIXED-20260917，含 CS0103 更正) + 按纪律清空我自己的收件窗口 |
| 119 | `d71539c3d` | `7feca5c08` | 同 | 补投递（修正 AppendAllLines 重载问题）：skill 转抄主程序来件 §14.0.22 + 主程序窗口回执 DELIVERY-UI-HEROART-FIXED-20260917（修复说明 + CS0103 更正 + 导出包占位目录裁定请求） |
| 120 | `14b70baa9` | `ca78456e4` | 同 | #376② 完成：2 类事件（RetreatResolved / ExpeditionAbandoned）已按契约实现 + 判据用例 |
| 121 | `cbe12794e` | `67981a8e1` | 同 | 投策划：#376② 2 类事件已完成(RetreatResolved/ExpeditionAbandoned 按契约+判据用例 557/557) + #384 的 #2(片 3.1)下一轮做 |
| 122 | `8a49177cb` | `974f1030d` | 同 | 🎯 片 3.1 完成：进地牢【不再起战斗】（按请求分流）+ 连带修掉战斗专用路径的空引用刷屏 |
| 123 | `f8adf7c1f` | `ac37a6048` | 同 | 投策划(#384 第2件完成:进地牢不再起战斗,附三例读数) + 投 UI(片3.1 后战斗改由落格触发:给入口与判定) |
| 124 | `fa0fb0272` | `c43951ea8` | 同 | 答 UI 编译异常求助：复现不出+无语言/SDK 缺陷证据(附非增量构建 0 错误/两条机制/三方取证法) |
| 125 | `f19b2de18` | `99fc12d78` | 同 | ② V10 口径读数已实现：三类结局 + 完成率 + 撤退场次分布 + 「撤而不弃」（全部由事件流算出） |
| 126 | `0c87a38e8` | `7860d76b2` | 同 | 投架构：V10 新口径读数已实现(只读事件流/判据①闭合/撤而不弃) + 判据③ RngDraw 已核 + 探针接线下一轮 |
| 127 | `166e6058f` | `ae7d36e60` | 同 | ③ P31 ⑧/④ 收紧：portrait 缺失必须显式声明 + 锚点支持显式 "inherit" |
| 128 | `2f53e6743` | `31d7b5711` | 同 | 投架构：P31 ⑧ portrait 已实现(顶层字段/引用/缺失必须显式声明) + 同批补 P31 ④ 的显式 inherit + 我一条流程教训 |
| 129 | `8a44990ca` | `6a2cb86f5` | 同 | 投 UI：瓷砖主画面读数(接口已开但你的分支本次没执行,73 行/真错误 0)+你只需接线+落格触发示例 |
| 130 | `21a3df168` | `377858690` | 同 | ② 尾巴：把 V10 读数接进远征探针 ⇒ **出真实数字**（只报数不判红）+ 如实标注探针的旧口径 |
| 131 | `81d5d498e` | `6d773c85d` | 同 | 投策划：V10 真实数字(300 趟:走完260/全灭40/完成率86.7%)+三条口径警告(旧线性路径/撤退无样本/旧结局串)+请裁是否改分类 |
| 132 | `c6aedbb05` | `a9387f9a2` | 同 | 落策划 #387② 硬规矩：V10 读数【无样本 ⇒ 报 N/A（无样本），不得报 0.0%】 |
| 133 | `de4ced712` | `2c7742b6b` | 同 | 补交：上一条提交(见 c6aedbb)消息里声称的 N/A 判据用例其实因 edit 被拒未落地 ⇒ 现补上(含如实说明) 全量 564/564 |
| 134 | `bbb8d3a5f` | `3a8578e8c` | 同 | 投策划(#387 A/B 已落+N/A 判据+我一条提交瑕疵已补) + 投 UI(撤回我 ⑥ 的强结论并请求打印自证 + 两条入口请求 + tile-walk 硬写 30 提醒) |
| 135 | `79a4e5dfb` | `e39411e87` | 同 | 投策划：证据(M76拓扑探针 L293 retreats++;break; = 旧语义)⇒产不出撤而不弃样本,专表需新探针+请示是否现在写 |
| 136 | `0a3f62fd4` | `d21ec0503` | 同 | 按主程序 2026-09-21 请求：PressAbandon 改 public（retreat 冒烟需真实按下入口）+ 瓷砖主画面分支加【自证打印】（格数/连线数/已揭示，只用 MapSketch 已知成员）+ 硬写 EnableTileWalk(30) 处加注明（仅冒烟用，正式走 tuning） |
| 137 | `6c3849fb0` | `b3d8fd5cd` | 同 | 处理窗口（2026-09-21 五件）：转抄 skill §14.0.23 + 回执 DELIVERY-UI-ASKS-DONE-20260921（请求一已做/自证打印已加/camp-curio 与落格触发交接状态）+ 清空我的收件窗口 |
| 138 | `0e077f0a9` | `bf94c4af8` | 同 | 统一 UI 命名空间为 Darkest.UI（用户 2026-09-21 指令）：我域 35 个文件行级改名（185 处命中）+ 我域外 2 个文件（DungeonRunDriver.cs / SmokeScript.cs，**越域改动、如实标注**）补齐 using/限定名；复查'仍含 Darkest.Ui' = 0；新增静态检查 tools/dsh/check_ui_namespace.ps1 防再生；构建绿 + 五入口真错 0 |
| 139 | `06a29c0da` | `032360d1d` | 同 | 接 `abandon` 冒烟步骤（UI 已放公共入口）+ 复核同伴的命名空间统一：两条精确结论 |
| 140 | `d45bc8c4f` | `483d56393` | 同 | 投 UI：冒烟抓到 PressAbandon 的 NRE 栈(他们文件)+⑥读数还差一步(要自证打印或切页入口)+复核命名空间通过但他们的检查脚本在 PS5.1 跑不起来 |
| 141 | `7cfb275be` | `57380ebf9` | 同 | 出《养成闭环现状清单+缺口+建议优先级》(reports/hamlet_loop_status_and_gaps.md)并投策划:城池动作已完整,缺口=再出发对比读数/招募死内容/升级对照/主动培养面/borrow 合规 |
| 142 | `eed262116` | `b618662f9` | 同 | ① 命名空间门禁：复核 + 双向验证（能抓违规）；② 一键冒烟 smoke.ps1：修到真能跑（含三个"假通过"） |
| 143 | `1954c6762` | `53749a887` | 同 | 投架构：门禁双向验证通过 + smoke.ps1 修到真能跑(四处修复)+三个假通过机制(BOM/Select-String 无 Recurse/空日志当通过) |
| 144 | `9a2c51f6d` | `b57ebc2af` | 同 | 兼容用户在编辑器里的 UI 改动（保留其文件）+ 修 abandon NRE：① hamlet_skeleton 缺 TopBar/TopRow/StatusBar/LeftColumn/LeftCol ⇒ skelOk 判定、缺任一整段回落（修 L177 NRE）② roster_row 两行重排 ⇒ 名册行按名递归查找+缺失即补 ③ PressAbandon 加 _uiRoot 守卫 ④ 判据补 still-in-use 退出噪声 ⑤ 用户 6 场景 + dd_theme.tres 入库 |
| 145 | `efac75ec8` | `1b12c0b3e` | 同 | 目标②③达成：① tools/dsh/check_ui_namespace.ps1 改为 **PS 5.1 可跑**（全 ASCII 输出 + 绝对路径 + **-CaseSensitive** 修正误报）⇒ Windows PowerShell 5.1 实跑 OK/退出码 0（主程序此前实测它跑不起来）② 清 6 处过时注释：'接线状态：未接线' ⇒ '已接线 + 生效留痕证据'（消除'两条 UI 构造策略'误判；事实=13 处 TryInstantiate/TryCreate 已接线） |
| 146 | `6ada31515` | `1dfaa228f` | 同 | ④ P0 养成闭环：`RunStartSnapshot` 只读读数 + "本次 vs 上次"对比 ⇒ 养成第一次【可读】（含真实读数） |
| 147 | `e842665af` | `0f7b19272` | 同 | 修城池重叠 6~7 对（真因=用户在编辑器把名册行改成两行结构，而我把行高写死 32px ⇒ RosterUpRow/RosterDownRow 压叠）：行高改为 **按行内实际需求** Math.Max(32, rowBody.GetCombinedMinimumSize().Y)，宽度仍按相机口径 232；保留用户两行设计 |
| 148 | `5a06baf5f` | `68753346c` | 同 | ② ③ 收口：ERROR 口径再补一条（引擎退出"资源仍在使用"）+ 三例复跑真错误全为 0 |
| 149 | `107815298` | `a2d0e79b1` | 同 | 投策划：P0 养成读数已可读(四趟真实对比)+两条真现象(养成链路在转/第4趟平台期)+我踩的读数打断主流程的错与修 |
| 150 | `110851ddd` | `ed2978c00` | 同 | 修城池重叠（第二轮·正确量法）：行高改为量 **内层 VBox RosterRowBody2**（用户两行结构）的合并最小高度，缺失退回外层；宽度仍 232 相机口径 ⇒ 保留用户设计 |
| 151 | `8f5a2bb4d` | `6a4f99360` | 同 | ⑤ P2 升级对照读数：三栋建筑的"升级真的更强"在【规则层+经济层】被证明（战斗层如实标未测） |
| 152 | `b2758e0be` | `ad64eb565` | 同 | 投策划：P2 升级对照读数(三栋各动一个轴 + 固定预算买到更多士气)+战斗层如实标未测并请示是否补 |
| 153 | `2714dc802` | `a807d57f2` | 同 | 修战斗顶栏'内容需求超出相机 4'（实测 BattleMargin 需 1388 > 相机 1280）：OrderBox 允许收缩+裁切、EnemyIntent 裁切+最小宽 0 ⇒ 不再把整行顶出画框（不动用户编辑过的 battle_topbar.tscn） |
| 154 | `3066d0f62` | `5967c86bc` | 同 | #394 判据执行：10 趟"起点变化轨迹"⇒ (a) 不成立（没有平台期），并发现真天花板 = 士气 100 封顶 |
| 155 | `1cb83c13b` | `6fd154ead` | 同 | 投策划(#394 判据):10 趟轨迹⇒无平台期((a)/(b)都不成立)+发现真天花板=士气100封顶+两条读数不可混引 |
| 156 | `a9e67f8dd` | `b7e532177` | 同 | #394 caveat 已补：含损耗的 10 趟轨迹（疾病累积可见）+ 查出"阵亡在名册侧无表示"的缺口 |
| 157 | `a609bdaa0` | `714bffc96` | 同 | 投策划：含损耗 10 趟轨迹(疾病累积 vs 士气封顶)+查出阵亡在名册侧无表示(机制缺口)+两条读数口径分开 |
| 158 | `f93cbd764` | `596534b0a` | 同 | 相机纠偏（战斗顶栏需求 1388→≤1280）：顺序头像面板 42→34、字形同步、顶栏间距 10→6（配上一轮 OrderBox/EnemyIntent 可收缩裁切）；不动用户编辑过的 battle_topbar.tscn |
| 159 | `42dafacea` | `d20767bbe` | 同 | 相机纠偏·续：任务标签允许收缩+裁切（TrimEllipsis）、顶栏间距 6→4 ⇒ 继续压低战斗顶栏横向需求（配头像 34 + OrderBox/EnemyIntent 可收缩）；不动用户场景 |
| 160 | `b2368fd1c` | `0233b71db` | 同 | smoke.ps1：读数自动提取【已验证可用】；退出码接 CI【未验通】—— 如实标注，不谎报 |
| 161 | `8bf5c69b8` | `c8aea971f` | 同 | 因"把机器搞脏了"而修：smoke.ps1 保证回收子进程 + 禁止并发实例；并修冒烟刷屏 |
| 162 | `37674acc0` | `6e9b927df` | 同 | 投架构：认账(残留进程是我搞的)+已加子进程回收与并发保护+修冒烟刷屏+CI 退出码改摘要行判红的方案 |
| 163 | `bd3f2283d` | `f9f0c83c6` | 同 | CI 判据收口：不靠 $LASTEXITCODE，改为【摘要里的 RESULT 行】+ 新门禁脚本（自检无需 Godot） |
| 164 | `22dde438e` | `7f4245116` | 同 | 投架构：CI 判据收口(不靠退出码,改读摘要 RESULT 行)+新 smoke_gate.ps1(纯 ASCII,自检不需 Godot)+CI 三步配方 |
| 165 | `24e15eb53` | `bf84a5c81` | 同 | 目标⑥：新增 tools/dsh/ui_sweep.ps1 —— 14 个 UI 入口一键审计（表含 demandOverCamera/overlap/transparent/realERROR + 空日志判 FAIL + 退出码可接 CI），PS 5.1 实测通过；修一处实测崩溃（headless 必须设 APPDATA，否则 user://logs 打不开 ⇒ signal 11 段错误、Windows 弹'该内存不能 read'）+ PS 5.1 的 -Only 逗号拆分；skill §14.0.24 记录 |
| 166 | `5bc3c776e` | `59e39fe93` | 同 | 清单①收口：一条命令自检 `tools/dsh/selfcheck.ps1`（四项 · 非 0 即失败）+ `tools/dsh/README.md`（防"只有我知道"） |
| 167 | `e8542e94d` | `65cf78742` | 同 | 投策划：一条命令自检(四项·非0即失败)已落地+README 文档化+透明说明(3 个数字按结构性常量登记,Fps 是否改必填待裁) |
| 168 | `2ba9293cc` | `8a553e293` | 同 | ① ui_sweep.ps1 修 \ 初始化（我上一补丁覆盖了 \ = \ ⇒ 不带 -Only 时抛错）⇒ 全量 14 入口跑通、entries=14 failed=0、退出码 0 ② 投主程序窗口 DELIVERY-UI-BATCH2-20260921（5 类修复 + APPDATA 崩溃教训 + 新一键入口）③ skill 台账登记回读 |
| 169 | `5e6858ec9` | `72b1ad73c` | 同 | 清单③收口：reports/ 留档按新口径逐份重分类（17 份）⇒ 不再有"待判定" |
| 170 | `69700a18f` | `01d55a8e3` | 同 | 目标⑤（Hamlet 可用性·第1项）：把角色详情里'推荐位置（待定）'改成玩家可理解的诚实标注'（开发中·预留）'并加注释说明（框=用户明确要保留的预留框，不改结构；红线 21：不留不可解释状态）；hamlet 四入口审计全绿 |
| 171 | `3ff6fb5a4` | `ab22da8f9` | 同 | 自检加第 5 项【占位合规】不变量(borrow/与占位不入 git+有 gitignore+未泄漏 resources/)+标签改 /5 并同步 README |
| 172 | `0b61f80eb` | `83c769683` | 同 | skill §14.0.25：目标⑤（Hamlet/养成）复审五入口含长文本全绿 + 可用性盘点结论 + 推荐位置文案已诚实化；目标⑦ 结论=主程序 reclassification 档自身已收口且无我域条目，我域侧 14 入口 realERROR=0、唯一 ERROR 类=引擎退出噪声 |
| 173 | `a81441faf` | `f5bcb522b` | 同 | skill §14.0.26：交付总结（五条评审收口对照 + 最终双重门读数 + 4 个真 bug + 用户改动保留说明 + borrow/ 授权标注） |
| 174 | `cb97f020a` | `e8afb8394` | 同 | P0 读数补两面（用户重心是养成，而我第一版快照竟漏了"等级"⚠️）+ 直接给策划 A4 判据一行结论 |
| 175 | `b8a34c2c3` | `139fb2fea` | 同 | 投策划：P0 读数补两面（英雄等级 + A4 一行判据）+ 如实标口径（本循环不接练级故等级不变） |
| 176 | `8f78e95b3` | `7ea401a58` | 同 | 更正我自己：smoke.ps1 从"包 try/catch"那一刻起就【跑不起来】—— 是我的括号错，不是 PS 5.1 |
| 177 | `45fe620ec` | `4b477d024` | 同 | 投架构：更正我自己（smoke.ps1 退 1 的真因是我括号没配平,不是 PS 5.1）+修法与实测+tile 读数仍受门控 |
| 178 | `c1974f5c2` | `3abd0bb06` | 同 | 自检加第 6 项【PowerShell 语法】护栏 + README 记一条我连踩两次的坑 |
| 179 | `9290c4e63` | `56e785310` | 同 | 策划 #396 两件点我的活：D 口径假象已查清（标 N/A + 理由）+ A 入口清单写进 README |
| 180 | `28a2267b7` | `0190a5287` | 同 | 策划 #396 新增验收 A9：**首条读数已出**（士气到顶后其余轴仍在动） |
| 181 | `1e183d718` | `50be5547a` | 同 | 投策划(#396 三件):D 口径假象已清并标 N/A+A 入口清单进 README+A9 首条读数(士气到顶后其余轴仍在动) |
| 182 | `b36ad4558` | `8552daf80` | 同 | 更正：上封与 28a2267 里的 568/568 是错的,实测 567/567（A9 加进已有用例,总数不变） |
| 183 | `291964a9f` | `639d3dc6d` | 同 | P2 ③ 战斗层（策划 §9.2 裁定"必须补"）：**读数已出，但口径揭示它不直接回答"更不容易崩"** ⇒ 报原文 + 报口径 + 请裁指标 |
| 184 | `2a3da2c2b` | `1c0e16503` | 同 | 投策划:§9.2 的 ③ 战斗层读数已补(同 seed×士气 40vs75)+口径揭示它不直接回答那句话+请裁指标(a/b/c/d) |
| 185 | `2d71d3ab1` | `9fd0598ea` | 同 | 养成逻辑（目标第 1 轮）：§9.2 的 (d) 四数读数已出 + 更正我"招募是死内容"的过强说法 |
| 186 | `960769b92` | `d9b9f0a19` | 同 | 投策划:(d) 四数读数已交+更正招募说重了+升级通道要一组数字(机制我先做到只差一个数,data 不动) |
| 187 | `f8c283b18` | `67acd1b9e` | 同 | 养成逻辑 ① 英雄升级通道：**机制 + 端到端验证已跑通**（只差策划那组数字） |
| 188 | `aa8950511` | `712afa407` | 同 | 投策划:升级通道机制与端到端验证已跑通(未接线显式不生效/接线后 MaxHp 28→30)+请裁阈值语义与那组数字 |
| 189 | `d30789fae` | `09bc9f01d` | 同 | 养成逻辑 ① 收口：占位数值已落盘（策划 #399）+ A10 曲线可读 + 宿主接线（附一条重要口径） |
| 190 | `c2a974161` | `9863d2106` | 同 | 投策划:A10 曲线已可读(升到 Lv6 累计 20 胜)+占位数值已落盘+口径(e2e 胜负是模拟的,故宿主接线未取到 live 读数) |
| 191 | `7f79792a9` | `cc4999c38` | 同 | 修我域那处过期注释（架构点我）：DungeonRunDriver.cs:5 ⇒ 命名空间门禁现在只指向 UI 那一处 |
| 192 | `a731b9800` | `d310d9c42` | 同 | 投 UI:命名空间门禁现在只红在 BattleUi.cs:13 的过期注释(我已修我域那处)+转达红线20第六条(改判据必重做双向自检) |
| 193 | `5a32c2a5d` | `da11c7683` | 同 | 养成逻辑 ④轮换 + ⑤名册构成读数：机制跑通并给出实测（含一条"策略写反"的实证） |
| 194 | `96ff5e20c` | `de224fa29` | 同 | 投策划：④ 轮换机制 + ⑤ 名册构成读数（附实测 8/9 次轮换 + 一条"轮换需要动机"的实证）+ 请裁 ② 阵亡三选一 |
| 195 | `9b22dab08` | `853df2d89` | 同 | 补提交我漏掉的一条：A8 逐趟判据 + 合计（`hamlet_loop.md §7.4`） |
| 196 | `410683153` | `00bf2f43d` | 同 | 补入库：我新建的内核/用例文件的 .uid（本仓约定 .uid 跟踪；只加我域的文件，不碰同伴的） |
| 197 | `3b3eebe60` | `c1cd3fcb0` | 同 | 养成逻辑 ⑤ 收口：自检第 7 项【构建必须通过】+ A10 派生读数（并纠正一个判断口径） |
| 198 | `5d63bbf42` | `dd03da266` | 同 | 投策划：A10 派生读数（升满 5~7 趟）+ 纠正「20 场偏长」的口径 + 自检第 7 项（构建）+ 自检标签统一 /7 |
| 199 | `2bf722aaa` | `cfb5e04ca` | 同 | 养成逻辑：损耗版 A9 已有读数（纠正一条过期 caveat）+ 轨迹纳入等级成长 + A7~A10 四条验收自查 |
| 200 | `2a50e784d` | `1fff84ad2` | 同 | 投策划:损耗版 A9 已有读数(纠正 caveat)+轨迹纳入等级成长+A7~A10 四条验收自查 |
| 201 | `bc9e2c52d` | `1311c6ec2` | 同 | 养成逻辑 ⑤ 完善：等级分布 + 轮换（换人 N 名）进"本次 vs 上次"快照（实测可见） |
| 202 | `7792ff4ef` | `170afacdb` | 同 | 养成逻辑 ⑤ 接进宿主：名册构成读数随"再出发"打印（实测可见）+ 冒烟关键读数模式扩充 |
| 203 | `c07052e50` | `f42ea4753` | 同 | 养成逻辑 ① 收口：升级通道在**真实流程**里跑通（e2e 实测：等级分布 Lv1×4/Lv2×4 → Lv3×8） |
| 204 | `33a61c937` | `32c71efef` | 同 | 按策划 #400/#401 实现两件：阵亡 (a)+ 与 A10 反推（level_costs=[11×5] ⇒ 正好 ~3 趟/级） |
| 205 | `9597f8e03` | `08db1676b` | 同 | 投策划:阵亡(a)+已实现且 A11 取证+A10 反推 level_costs=[11x5] 正好 3 趟/级+我两个坑 |
| 206 | `2f7af8daa` | `ec5fb9f20` | 同 | 做功能：马车"新兵起始等级"从【只展示】变成【真消费】+ 阵亡⇒空位⇒招募补人闭环 |
| 207 | `0ff8f61bf` | `43ae2f9dc` | 同 | 投 UI(一行:招募传 EffectiveRookieLevel)+投策划(裁:名册上限两条来源怎么合成 a 取最大/b 相加) |
| 208 | `51815c4ff` | `5536be6a1` | 同 | 做功能：养成闭环整合读数（出征含阵亡 ⇒ 回城招募/升级/减压 ⇒ 再出发，10 趟一整条） |
| 209 | `93190ad2a` | `56c734167` | 同 | 做功能：按 #403 接上名册上限（相加+增量+封顶）+ A12 读数 + Effective* 消费面普查 |
| 210 | `24ff9fb65` | `42bb3fdf5` | 同 | 修红：把 UnlocksConfigTests 的断言同步到 #403 的增量语法（全量 580/580） |
| 211 | `2aa809e0f` | `36cafc864` | 同 | 投策划:#403 已落+A12 达成+普查抓到两处只展示(EffectiveReliefCost/EffectiveMoraleRestore 未消费) |
| 212 | `d97888f2c` | `e1f638b2d` | 同 | 办两件来件（都在我域）：① 删 BattleUi.cs 的冗余别名 using UiMotion=（同命名空间，且其注释里的小写 Darkest.Ui 会撞我自己写的命名空间门 ⇒ 假阳性）⇒ 门禁干净树上转绿 ② 两处招募补 rookieLevel: heirlooms.EffectiveRookieLevel(_cfg.Coach.RookieLevel)（消费 §1605 展示值，修死声明）；PressAbandon 早已 public 无需再改 |
| 213 | `fa798105d` | `2795254e6` | 同 | 投 UI：更正我上条指示的变量名(ExpeditionContext.Heirlooms)——构建现因此红+同族减压收费缺陷与修法 |
| 214 | `9732a364f` | `5ff443615` | 同 | 补两处招募 rookieLevel（用 ExpeditionContext.Heirlooms?.EffectiveRookieLevel ⇒ int? 传 null 即内核缺省=旧行为）：消费 HamletRoot:1605 的展示值，修【有展示、无消费】死声明 ✓ 更正上一条 d97888f 的提交信息（它误称已补，实际因我把路径变量 \/\ 写成同名而未生效）；别名删除保持有效、门禁干净树转绿 |
| 215 | `b7a7c6907` | `2a2aba888` | 同 | 办【减压展示值==消费值】(策划 #404 纪律 V / 主程序内核已修 StressRelief.Apply)：HamletRoot:558 的 ApplyRelief 恢复量改走 ExpeditionContext.Heirlooms?.EffectiveMoraleRestore(...) ?? 原始值 ⇒ UI 展示与实际恢复同源；收费侧由内核 StressRelief.Apply 生效值负责（我不重复扣）✓ |
| 216 | `13a598b2b` | `aa62c05ee` | 同 | 处理窗口：转抄 skill §14.0.27（收件3件已办+PS大小写教训）+ 回执 DELIVERY-UI-ALIAS-RECRUIT-RELIEF-DONE-20260921 + 清空我的收件窗口 |
| 217 | `005a44835` | `bc4771bbf` | 同 | 做功能：#404 纪律 V —— 把"多了一个谎"修掉（减压实际收费/恢复改走生效值）+ A13/A14 读数 |
| 218 | `bb6f29dc0` | `d37377d6d` | 同 | 投策划:#404 纪律 V 已落(减压实际收费/恢复走生效值)+A13/A14 达成+认两处我自己的错+承诺下一轮做 reports 卫生 |
| 219 | `206b31da1` | `11193fd9f` | 同 | 功能开发（目标第 1 轮）：进度可见性 —— "下一个解锁还差几趟/几胜" + Audit 一并显示（全量 583/583） |
| 220 | `cb4072eb8` | `e366371c2` | 同 | 功能开发（目标第 1 轮·续）：reports/ 卫生（#405 承诺）—— latest 指针 + 保留策略 + README（不需 Godot 可跑） |
| 221 | `4d6892107` | `e93ebdf30` | 同 | 提交 .gitignore：加 reports/latest/ 与 reports/archive/ 的忽略（#405 卫生配套） |
| 222 | `7da468f62` | `be7f59249` | 同 | 功能开发（目标第 2 轮）：把"下一解锁"接进宿主"再出发"读数（e2e 实测）+ 连线上的 Curio 目录坑 |
| 223 | `e157b05b3` | `970eb73d8` | 同 | 投架构:普查续集——buff 定义 6 个字段被解析但不被消费,请裁(预留/应消费/应删) |
| 224 | `dc7f8da31` | `1e624b2c6` | 同 | 执行架构 #408 执行项：删 buff 的 class/timing 两字段 + 四分类标注（全量 583/583） |
| 225 | `7852de92c` | `e65be8cc5` | 同 | 回架构:#408 执行项已办(删 class/timing+改那一处校验+四分类标注,583/583)+我两处坑 |
| 226 | `c137f50cb` | `2d90c2452` | 同 | reports 卫生加 90MB 单文件守卫：超限即压缩为 zip 并告警（O-90：打不开的报告不是报告） |
| 227 | `d520e4824` | `0e38dbc5d` | 同 | 回执架构:门禁 v4 三向验证全过(干净0/注入1/注释0),无法复现干净树报红;请给那次红的原始输出 |
| 228 | `3db395b29` | `90488d4be` | 同 | 把门禁 v4 入库（修仓库状态）：已提交的那版是 v3 ⇒ **对注释假红**（用户报的"干净树也红"就是这个） |
| 229 | `ce3b3f93e` | `6f58cb1fe` | 同 | 清理commit |
| 230 | `b31991604` | `c53ff4586` | 同 | 策划：养成闭环收口（A1~A14 验收 + 纪律 A~Z + 三问裁定） |

## §5 使用说明与例外

- 覆盖范围：旧 `b319916..5cd9946`（849）→ 新 `c53ff45..bfbeddb`（849）；`41d287e` 及更早的共用历史 SHA 未变，不在本表。
- 唯一例外：本表自身的提交（推送收口⑪）只存在于新链，没有旧 SHA 与之对应；`bfbeddba6` 之后新增的提交同理。
- 安全绳：`archive/pre-push-20260930`（tip `5cd994610`）保留全部旧 SHA 与 104 MB 历史，仅本地，不推送。
- 历史文档中的旧 SHA 引用一律不改；需要旧 SHA 时按本表反查（新→旧即反向查 `§3`）。
- 推送后复核：`git ls-remote origin master` 与 `git rev-parse master` 逐字比对；`git rev-list --count origin/master` 应为 1704（含本表提交自身）。
