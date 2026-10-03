# UI 主环收口：现有功能「能人工试玩」（2026-10-03 · 主程序）

> 用户原话：「现有UI如何了?能适配现有功能吗？能不能先完成现有功能有的UI，我想能人工测试玩法。游戏流程要对」
> 本件把主环「回城 → 养成 → 出发 → 走图 → 打一场 → 回城」上的**四处玩家路径真断点**补齐 ⇒ 现在可以完整手动玩一圈。
> 提交：`doc/state.md #548` · 8 个改码文件（+229 ／ −5 · 全部 ≤387 行 · LF 无 BOM）· 构建 0 错 · 两条冒烟 RC=0

---

## §0 一句话结论

主环此前有**四处真断点**，玩家走不完一圈；本件全部修掉并逐条复测：

| # | 断点 | 玩家感受 | 状态 |
|---|---|---|---|
| 1 | 进地牢**不建 UI** | 出发后**看不到地图**（只有背景） | ✅ 本件修（`#548`） |
| 2 | `_view` **全项目无人赋值** | 战斗卡片／技能栏／顺序条不刷新；E 区【序列】页直接 NRE | ✅ 本件修（`#548`） |
| 3 | 走进战斗格**不起战斗**；走到终点**不回城** | 站在地图上无路可走（流程断点） | ✅ 本件修（`#548`） |
| 4 | 相位门禁**把整张地图挡死** | 进地牢后选不了路／扎不了营／摸不了奇物 | ✅ 本件修（`#548`） |
| 5 | 每帧 **1799 条** `ObjectDisposedException` | 日志刷红 ＋ 每帧访问已释放节点 | ✅ **根因修** · 复测 **0** |

⇒ 加上此前已收口的 M4u（饰品装卸）／M5u（怪癖）／M6u（建筑升级树）／M7u（招募）／M12（补给买卖）／M14（任务选择），
**主环接线已闭合**：可以完整手动玩一圈（清单见 §5）。

---

## §1 改码 8 文件（行数 = 实际行数口径 · 硬线 600 ／ 目标 400）

| 文件 | 行数 | 改动 |
|---|---|---|
| `darkest/scripts/gameplay/scene/BattleRoot.Ready.cs` | 193 | 相位门禁：**进地牢不再被推成 `Battle`** |
| `darkest/scripts/gameplay/scene/BattleRoot.FlowBridge.cs` | 140 | 进地牢**必须建 UI**（`Callable.From(BindUi).CallDeferred()`） |
| `darkest/scripts/gameplay/scene/BattleRoot.PlayerActions.cs` | 372 | 战斗成立 ⇒ `_ui.SetBattleView(this)`（远征战斗 ／ 单场战斗两处入口） |
| `darkest/scripts/gameplay/scene/BattleRoot.EndGame.cs` | 166 | 【继续（回地图）】**先断视图**；终点房是战斗房 ⇒ 战后补结算回城 |
| `darkest/scripts/ui/BattleUI.Dungeon.cs` | 354 | 🆕 `TryStartBattleAtCurrentRoom` ／ `TryReturnToTownAtGoal` ／ `StandingInsideCurrentRoom` |
| `darkest/scripts/ui/BattleUI.MultiFunction.cs` | 387 | 瓷砖回调 ＋ 房间图回调**共用**两条判据；E 区三页**空态门**（修 NRE） |
| `darkest/scripts/ui/BattleUI.Lifecycle.cs` | 174 | 🆕 `SetBattleView` ／ 🆕 `ForgetLazyPanels`（12 个惰性字段 · 根因修复） |
| `darkest/scripts/ui/BattleUI.Refresh.cs` | 381 | 两行判据补 `GodotObject.IsInstanceValid`（保险侧） |

---

## §2 四条断点：根因与修法

### 2.1 进地牢不建 UI ⇒ 玩家看不到地图（`BattleRoot.FlowBridge.cs`）

**现象**（`.tmp_tile_smoke.txt` 修前）：进地牢后只有两条「地图模式请求：**UI 尚未构建** ⇒ 延后到 `Bind()` 之后」，
**没有** `[BattleUI] 暗黑地牢式排布就绪` ／ `[UI 模式] 进入【地图模式】` ／ `[UI 瓷砖]` ⇒ 地图永远不画。

**根因链**：`EnterDungeonInScene()` **不调 `Bind()`**；而 `EnterMapMode()`（`BattleUI.Dungeon.cs`）在 `_bottomRow` ／ `_uiRoot`
为空时**只置 `_pendingMapMode = true` 就返回**，唯一解药 `Bind()` **只有 `BindUi()` 会调** ⇒ 纯进地牢路径永远不建 UI。

**修法**：`BattleRoot.FlowBridge.cs:30-41` 在 `_ui = GetNode<BattleUI>(...)` 之后插入 `Callable.From(BindUi).CallDeferred();`。两个约束写进注释：

· `Bind()` 只建 UI、**不起战斗**（起战斗仍在 `StartExpeditionBattleInScene` ／ `NewGame`）⇒ 不改任何规则；
· 必须 **deferred**：本方法在 `_Ready()` 期间被调，此时 `AddChild` 会撞「Parent node is busy adding/removing children」（实测踩过）。

### 2.2 `_view` 全项目无人赋值 ⇒ 刷新空转 ＋ 序列页 NRE（`BattleUI.Lifecycle.cs`）

**取证**：`rg` 搜 `_view` 赋值**零命中**（`BattleUI.cs:42` 声明后无人写）⇒ `Refresh()` 首行
`if (_host is null || _view is null) return;` **使整条刷新路径空转**（卡片 ／ 技能栏 ／ 行动顺序条 ／ 5·6 号位都不更新），
且 E 区【序列】页直接 `NullReferenceException`（`.tmp_battle_base.txt:259` 修前）。

**修法**：🆕 `public void SetBattleView(IBattleView? view)`（唯一写入口 ＋ 两行 `GD.Print` 自证）：

· **接**（战斗成立时）：`BattleRoot.PlayerActions.cs:49`（远征战斗）／ `:75`（单场战斗）；
· **断**（战斗结束时）：`BattleRoot.EndGame.cs:100-103`【继续（回地图）】回调最前面 —— 两条路（宿主内回地图 ／ 旧路径回远征场景）**都要断**，否则回地图后 E 区仍读**上一场**的读数（会撒谎 · 红线 21）；
· 地图模式 ／ 未起战斗 **保持 `null`** ⇒ 空转是预期（E 区三页走空态）。

### 2.3 走进战斗格不起战斗 ／ 走到终点不回城（`BattleUI.Dungeon.cs` ＋ `MultiFunction.cs`）

**现象**：两条地图回调（瓷砖主画面 `TryStepTile` ／ 房间图 `StepTo`）**此前都只挪位置** ⇒
走进 `battle` 房不起战斗、走到终点无人消费 `ReachedGoal`（只有 CLI ／ 冒烟会回城）⇒ 玩家只能干站在地图上。

**修法**：三个新判据收进 `BattleUI.Dungeon.cs`，**两条回调共用**（判据只写一处 ⇒ 不漂移）：

| 方法 | 判据 | 说明 |
|---|---|---|
| `TryStartBattleAtCurrentRoom` | `CurrentRoomType == battle` 且**未** `IsRoomResolved` 且**人确实还在房内** | 起战斗后 `ExitMapMode` ⇒ 调用方不再弹饥饿／刷面板 |
| `TryReturnToTownAtGoal` | `ReachedGoal` 且**人确实还在房内** | 复用 `DungeonRunDriver.ReturnToTown`（与 `--hamlet-next` ／ 冒烟 `town` 同一实现） |
| `StandingInsideCurrentRoom` | 站立格 → `TileRoom` 映射 == `CurrentRoomId` | **走廊格不更新房间 id** ⇒ 单看房间类型会在走廊上误触发 |

外加 `BattleRoot.EndGame.cs:113-126`：**终点房若是战斗房** ⇒ 打赢这一场后由【继续（回地图）】补结算回城
（只在 `PlayerVictory`；撤退留在图里，`#352` 口径不变）。

### 2.4 相位门禁把整张地图挡死（`BattleRoot.Ready.cs`）

**现象**：战斗场景里 `Phase` 补丁此前**无条件**执行 ⇒ **进地牢**（地图模式）也被推成 `Battle` ⇒
`CanShowPathChoice` ／ `CanShowCampUi` ／ `CanShowCurioUi` **三谓词全假** ⇒ 选不了路、扎不了营、摸不了奇物。

**修法**：判据改为「**本场景接下来要进地牢**（有请求 ／ `--dungeon-in-scene` ／ `--expedition` ／ `--e2e`）时**不推**；其余入口才推 `Battle`」。

### 2.5 🔴 每帧 1799 条 `ObjectDisposedException`（根因 · `BattleUI.Lifecycle.cs` ＋ `Refresh.cs`）

**现场**：`BattleUI.Refresh.cs:138`（`_mapModeList.Visible = false`）· 对象 = `ExpeditionListPanel` · 栈 = `BattleRoot._Process → Refresh`（**每帧**）。

**根因**：`Bind()` 里 `foreach (Node child in GetChildren()) child.QueueFree();` —— `QueueFree()` **本帧不销毁**（延到帧尾）
⇒ 惰性面板的判据 `is null || !IsInstanceValid` 在**本帧内仍为假** ⇒ 不重建；帧尾旧节点真死 ⇒ 字段**悬空** ⇒ `Refresh()` 每帧撞已释放节点。

**修法①（根因侧）**：🆕 `ForgetLazyPanels()`，在 `Bind()` 的 `QueueFree` 循环**之后**、`_cards.Clear()` **之前**调用，把 **12 个惰性字段**全部置 `null`
（都挂刚被 `QueueFree` 的子树）：`_dungeonHost` ／ `_mapModeList` ／ `_mapModeCamp` ／ `_walkMap` ／ `_walkHud` ／ `_mapModeCurio` ／ `_mapModeHunger` ／
`_statusTray` ／ `_banner` ／ `_mapModeScoutMark` ／ `_mapModeInventory` ／ `_mapModeLightBar`。方法注释写明纪律 ⚠️：**新增惰性面板必须登记到这里**。

**修法②（保险侧）**：`Refresh.cs` 两行判据补 `GodotObject.IsInstanceValid(...)`（与全项目其余惰性面板同口径）。

---

## §3 冒烟取证（两条命令 · 均 RC=0）

### 3.1 `.tmp_tile_smoke.cmd`（`--expedition --tile-walk --tile-step --fixed-fps 60 --quit-after 1800` · 7,845 行）

| 读数 | 修前 | 修后 |
|---|---|---|
| `ObjectDisposedException` | **1799** | **0** |
| `NullReferenceException` | 有 | **0** |
| `^ERROR` | — | **1**（既存离线噪声 `Failed to read the root certificate store.` · `os_windows.cpp:2582` ⇒ 非项目缺陷） |

关键 ✅ 行：

· `:242` `[BattleUI] 暗黑地牢式排布就绪（横排：我方 4321 ｜ 敌方 1234；支援位后排；底部技能栏）。`
· `:246` `[UI 瓷砖] ✅ 主画面（引擎内置 TileMapLayer）：格 89 ／ 连线 124　已揭示 10 ／ 89`
· `:257` `[UI 走格] 点格 (1,0) ⇒ TryStepTile(0,-1)=True（新格　现在 (1, 0)：Battle　已走 1 格　三态 Visited）✓`
· `:258` `[UI 地图] 🔴 走进**战斗格**（房间 0）⇒ 起战斗（同场景 · 本趟第 1 场 · 此前胜 0）✓`
· `:485` `[UI 视图] ✅ 已接入本场只读视图（卡片 ／ 技能栏 ／ 顺序条 ／ 5·6 号位恢复刷新）✓`
· `:488` ／ `:497` `[UI S1] ✅ S1 通过：切模式后骨架**未重建**`

### 3.2 `.tmp_battle_base.cmd`（`--click-menu=0 --battle-tab=2 --fixed-fps 60 --quit-after 900` · 3,862 行）

| 读数 | 修后 |
|---|---|
| `ObjectDisposedException` ／ `NullReferenceException` | **0** ／ **0** |
| `^ERROR` | **1**（同上证书噪声） |

关键 ✅ 行：

· `:256` `[UI 视图] ✅ 已接入本场只读视图`
· `:262` `[片③·页内容] 【序列】回合 0　行动顺序： ｜ 　（本回合还没有人行动）` ⇒ **修前此页 NRE**
· `:264` `[UI §8 用词] 撤退按钮文案=「撤退（退出这场战斗）」　可见=True　｜　放弃远征按钮文案=「放弃远征」　可见=False`

### 3.3 判读口径（写下来免得下一个人踩）

· ⚠️ **日志里有 C# backtrace ≠ 有缺陷** —— Godot 会把**已捕获异常**也打栈；本件两条日志的 `:1-241` ／ `:220-226` 就是启动期 `NewGame → Bind` 栈的既存记录。**判据看三条计数与 ✅ 行**。
· ⚠️ 日志尾部数千行 `[UI 敌人信息·悬停]` 每帧重复打印 = **既存噪声**（非缺陷；可后续做去重）。

---

## §4 登记（新增 5 条开放题）

| 号 | 内容 | 处置 |
|---|---|---|
| `O-120` | **战斗格重复触发**：打赢**不写** `_resolvedRooms`（内核只在 `DrawRetreat` 写）⇒ 走回已打过的战斗格会再打一场 | 内核写入口 `MarkRoomResolved` 留后续件 |
| `O-121` | **起点房 `battle` 类型** ⇒ 第一步即开打，是否合理 | 策划裁 |
| `O-122` | 骨架 `has vanished`（36 ／ 14 条）：`main_menu` ／ `fe_flow_skeleton` ／ `raid_results_skeleton` 的 `LayerTitle` 相对父路径 | 疑编辑器 Editable Children 另存产物 · 只取证 |
| `O-123` | 重入 `Bind()` ⇒ 骨架被重建（S1 已知迁移目标） | 非本件引入 · 登记 |
| `O-124` | `_mapModeInventoryBag` 从未使用（`CS0169`） | 死字段 · 登记 |

---

## §5 UI 现状（答复用户「现有 UI 如何了」）

**能用（可手动试玩）**：

· 回城：建筑 nav ／ 名册 ／ 英雄详情 ／ 酒馆·修道院减压 ／ 疗养院治病 ／ 传家宝兑换
· 招募（M7u）· 补给买卖（M12）· 任务选择（M14 · 真读 `darkest/data/quests.json`）
· 建筑升级树（M6u）· 装备阶升阶（方块拖孔选人）· 饰品装卸（M4u）
· 地牢走格：瓷砖主画面 ＋ 点击走格 ＋ 三态（已看清 ／ 只有轮廓 ／ 未知）· 扎营 ／ Curio ／ 饥饿 ／ 陷阱接线
· 单场战斗 · 战后结算面板 · 继续回地图

**占位 ／ 未接线（登记在案）**：

· 饰品来源（库存 ／ 商店）· buff 原语层（`#307` 数值冻结 ⇒ 饰品不改属性）
· 补给屏任务信息位只放「—」· 队伍拖动换位（`O-113`）· 英雄详情 HP 条 ／ 装备位空框（`O-112`）
· 多处色块占位骨架（`§14.0.68` 家族 · 受「色块占位，不换不删」约束）

**覆盖度读数**：`darkest/scripts/ui` **110** 个 `.cs`（15,767 行 · 最大 395 行）· `darkest/scenes/ui` **31** 个 `.tscn` ·
主场景 `res://scenes/main/MainMenu.tscn`（菜单三项：单场战斗 ／ 出发远征 ／ 回城）。

**手动试玩路线（建议）**：主菜单「回城」⇒ 城里养成（招募 ／ 买补给 ／ 升级建筑 ／ 装备阶）⇒ 「出发远征」⇒ 点格走图 ⇒
走进战斗格开打 ⇒ 打赢 ⇒ 走到终点回城 ⇒ 战后结算面板【继续】。

---

## §6 验收与边界

· 构建：`dotnet build darkest/Darkest.csproj -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` ⇒ **0 错**（`NU1900` 离线告警为既存 · 本机口径 net10.0）
· **最低限度必要性测试（用户约束）**：本件为**纯 UI 接线 ＋ 判据补强**（零数值 · 零内核规则改动）⇒ **不跑全量 `dotnet test`**；判据 = 两条 Godot 冒烟（玩家路径）＋ 构建 0 错
· 8 个改码文件全部 **≤387 行**（硬线 600 ／ 目标 400）· LF 无 BOM（`darkest/scripts/**` 口径）
· 遗留：`O-120`~`O-124`（见 §4）· `P0` 请单等策划回件（解锁 `P2`）· `P3` 待解冻
