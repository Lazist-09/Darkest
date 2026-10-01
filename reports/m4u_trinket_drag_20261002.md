# 饰品装卸接线（`M4u` 收口 · 三条真手势 ＋ 拆片 ＋ 三处真值瑕疵 · 2026-10-02）

**上游**：`reports/m4u_trinket_ui_20261002.md`（`M4u` 显示半 · 其 §五 5 明写「拖放接线属后续」）· 契约 `doc/modules/trinkets.md`（§5 T3 ／ T4 ／ T6）· 施工单 `doc/architecture/tasks/dd1_workstreams.md §3`（`M4u`）

**用户裁定（2026-10-02 · 本件开工前）**：① 建筑详情选人 = **方块空洞 ＋ 把角色方块头像放进孔里**（本件据此把饰品格做成**孔**）② 装备阶金币价（750/1750/3000/6000）**保持不动** ⇒ 冒烟走**公开 API 播种** ③ 授权改组合根一行（`ExpeditionComposition.cs:146` 的 `ExpeditionContext.Gear ?? new HeroGearState()`）

**用户约束**：每文件 ≤600 行（目标 ≤400）· 可维护 / 可读 / 复用 · **能用引擎内建 / 开源轮子就不自己造** · **只允许最低限度的必要性测试**（⇒ 本件**不新增用例**，验证 = 筛选测试 ＋ Godot 冒烟）

⚠️ 如实标注：本环境**无外网**（沙箱）⇒「上网找开源轮子 / MCP 工具」这一步在本机**做不到**；本件能给的证据 = **新增依赖 0** ＋ 全部引擎内建 —— **那个「轮子」就是引擎自己**：拖放走 Godot 的 `_GetDragData` / `_CanDropData` / `_DropData` 协议（**不自造 DnD 管理器**）· 合成手势走 `Viewport.PushInput`（**不自造事件总线**）· 遍历走 `Node.FindChildren`（**不自造递归**）· 等布局走 `Callable.CallDeferred`（**不自造计时器**）✓

## 一、玩家可见行为（3 条玩家路径）

| # | 行为 | 引擎入口 | 内核落点 |
|---|---|---|---|
| ① | **把方块拖进孔 = 装上** | 孔 `GearHeroSlot`：`_CanDropData` / `_DropData` ⇒ `TryDropPayload` | `Roster.EquipTrinket` ⇒ `HeroTrinketEquippedEvent`（`Reason = drop`） |
| ② | **点方块 = 卸下** | 方块 `GearHeroSquare`：`_GuiInput` ⇒ `Pressed` | `Roster.UnequipTrinket` ⇒ `HeroTrinketUnequippedEvent`（`Reason = click`） |
| ③ | **把方块拖出孔外松手 = 卸下** | 没人收载荷 ⇒ 引擎 `NotificationDragEnd` ⇒ `DraggedOut` | 同上（`Reason = drag-out`） |

🔴 **孔「收下载荷」≠ 内核「接受」**（红线 21 (b)）：第二次投同一件 ⇒ 孔**收下**、内核**原样拒** ⇒ 详情把理由**整行**呈现，UI 一个字不加 ✓

🔴 **显示 == 真值**（红线 26）：槽位读数 = `Roster.TrinketsOf`（现读，不缓存）· 可装性 = `Roster.CanEquipTrinket`（**唯一判据落点**）· 事件里的 `Reason` = 玩家**真做的那件事** ✓

## 二、代码改动

| 文件 | 行数 | 类型 |
|---|---|---|
| `darkest/scripts/ui/HamletRoot.HeroDetail.Trinkets.cs` | 356 | 改：`Pressed` ⇒ `"click"` ／ `DraggedOut` ⇒ `"drag-out"`（理由由**调用方**给 ⇒ 事件记真手势） |
| `darkest/scripts/ui/HamletRoot.HeroDetail.Trinkets.Actions.cs` | 128 | 🆕 装卸收口：`EquipTrinketFromUi` ／ `UnequipTrinketFromUi(heroId, trinketId, reason)` ＋ 卸下成功时清同英雄旧拒绝读数 |
| `darkest/scripts/ui/HamletRoot.HeroDetail.Trinkets.Smoke.cs` | **395** | 🆕 冒烟族独立成片（落孔 ／ 合成真点击 ／ 合成真拖动） |
| `darkest/scripts/ui/HamletRoot.Build.cs` | **559** | 改：`HandleTrinketUiSmokeFlags(hamletArgs)` 排在 `--hamlet-hero-detail` **之后**（孔/方块是「详情打开那一刻」建的） |
| `darkest/scripts/ui/GearHeroSlot.cs` ／ `GearHeroSquare.cs` ／ `DragPayload.cs` | 112 ／ 103 ／ 54 | 上游轮已落（`Dropped` ／ `AcceptTag` ／ `SetItem` ／ `TryDropPayload`；`Payload` ／ `_GetDragData` ／ `NotificationDragEnd`） |
| `tools/deadfunc_allowlist.txt` | — | 改：**删** `UnequipTrinket` 那条（消费点已就位 ⇒ `deadfuncs = 0` 复绿） |

**拆片记录**：`HamletRoot.HeroDetail.Trinkets.cs` **284 ⇒ 356**（+72 · 装卸入口与工具方法）＋ 冒烟族**另起一片**（`…Trinkets.Smoke.cs` · **395 行**）⇒ 详情片**不背冒烟**，两片都在 400 目标内 ✓

## 三、冒烟读数（原文 · 一次跑完一条链）

**命令**（`cwd` = 仓库根 · `APPDATA` **必须**指到可写目录，否则 Godot signal 11）：

```
set APPDATA=F:\GithubPro\Darkest\.tmp_godot_appdata
"E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --path F:\GithubPro\Darkest\darkest --audio-driver Dummy --hamlet-trinket-seed=hero_warrior_1:crow_wingfeather --hamlet-hero-detail=0 "--hamlet-trinket-drop=hero_warrior_1:crow_tailfeather;hero_warrior_1:crow_wingfeather" --hamlet-trinket-unequip=hero_warrior_1:crow_wingfeather --hamlet-trinket-dragout=hero_warrior_1:crow_tailfeather --hamlet-next --ui-audit --fixed-fps 60 --quit-after 900
```

**读数**（`.tmp_smoke_m4u.log` · **652 行** · **EXIT=0**）：

```
[UI 英雄面板] ✅ 饰品 2 孔就位（真读数 · 同源 Roster.TrinketsOf；孔 = GearHeroSlot，只收 trinket 族载荷）✓
[M4u·饰品] hero_warrior_1：槽1=crow_wingfeather｜槽2=空｜【饰品】1/2 格已装　（装卸已接线：方块拖进空孔 = 装上 · 拖出孔外松手 ／ 点方块 = 卸下）（库 196 条）✓
[GearHeroSlot] 方块落孔（族 trinket）：crow_tailfeather（引擎 _DropData ⇒ Dropped）✓
[M4u·饰品] 落孔：hero_warrior_1 ⇐ 「crow_tailfeather」装上（现持 2/2）✓
[M4u·饰品] 装卸收口：最近事件 = HeroTrinketEquippedEvent { Sequence = 1, Round = 0, HeroId = hero_warrior_1, TrinketId = crow_tailfeather, Slot = 2, Reason = drop }（现持 2/2）✓
[M4u·饰品·冒烟] 落孔载荷「trinket:crow_wingfeather」⇒ 孔收下（现持 2/2）　事件=… Reason = drop …✓
[M4u·饰品] 落孔：hero_warrior_1 ⇐ 「crow_wingfeather」**被拒**：「老铁」已经装着「crow_wingfeather」—— 同件不重复装（内核原样 ⇒ 详情整行呈现，T4）✓
[M4u·饰品·冒烟] 第 1 次：方块「crow_wingfeather」矩形 (536, 283), (116, 35) ⇒ 点它的中心 (594, 300.5)（引擎 Viewport.PushInput：按下＋抬起 ⇒ 走引擎命中测试，不是直调回调）✓
[M4u·饰品·冒烟] 第 1 次没命中（现持仍 2）⇒ 再等一帧重试 ✓
[M4u·饰品·冒烟] 第 2 次：方块「crow_wingfeather」矩形 (542, 289), (116, 44) ⇒ 点它的中心 (600, 311)（同上）✓
[M4u·饰品] 卸下（click）：hero_warrior_1 ⇏ 「crow_wingfeather」（现持 1/2）✓
[M4u·饰品·冒烟] 点击后现持 1/2（点击前 2）　事件=HeroTrinketUnequippedEvent { … TrinketId = crow_wingfeather, Slot = 1, Reason = click } ✓
[M4u·饰品·冒烟] 方块「crow_tailfeather」第 1 次：矩形 (536, 283), (106, 35) 还没连续两帧不变（按下会拖错那件 ⇒ 等布局落定）✓
[M4u·饰品·冒烟] 方块「crow_tailfeather」第 2 次：矩形 (762, 289), (106, 44) 还没连续两帧不变（同上）✓
[M4u·饰品·冒烟] 方块「crow_tailfeather」第 3 次：矩形 (536, 283), (106, 35) 还没连续两帧不变（同上）✓
[M4u·饰品·冒烟] 方块「crow_tailfeather」第 4 次：矩形 (542, 289), (106, 44) 还没连续两帧不变（同上）✓
[M4u·饰品·冒烟] 第 5 次：从方块中心 (595, 311) 拖到孔外 (302, 311) 松手（引擎 Viewport.PushInput：按下 ＋ 越过阈值 ＋ 抬起 ⇒ 走完整拖放）✓
[GearHeroSquare] 开始拖动方块：trinket:crow_tailfeather（载荷 = 「族:id」串；引擎内建 drag-and-drop）✓
[GearHeroSquare] 方块「crow_tailfeather」拖出后未落在任何孔里 ⇒ DraggedOut ✓
[M4u·饰品] 卸下（drag-out）：hero_warrior_1 ⇏ 「crow_tailfeather」（现持 0/2）✓
[M4u·饰品·冒烟] 拖出后现持 0/2（拖前 1）　事件=HeroTrinketUnequippedEvent { … TrinketId = crow_tailfeather, Slot = 1, Reason = drag-out } ✓
[M4u·饰品] hero_warrior_1：槽1=空｜槽2=空｜【饰品】0/2 格已装 ✓
```

🔴 **两条手势都验「点得到吗」**（红线 26 功能级验收）：点击**不是**直调回调 —— 按方块 `GetGlobalRect().GetCenter()` 合成 `Viewport.PushInput`（按下 ＋ 抬起，`localCoords: true`）⇒ 命中测试由**引擎**做，UI 侧一句判据都不写 ✓

⚠️ **两处必须的延迟**：① 点击／拖动都要**延迟一帧**再取矩形（`GridContainer` 的 sort 是 deferred）② 拖动的核对必须**下一帧**（引擎的拖放收尾 `NotificationDragEnd ⇒ DraggedOut` **不在** `PushInput` 这一次调用里同步落地 ⇒ 立刻读数会读到假阴性）✓

## 四、三个真值瑕疵（全是实测逼出来的，不是猜的）

| # | 症状 | 根因 | 修法 |
|---|---|---|---|
| ① | 冒烟**找不到方块** | 方块嵌在**孔里**（`GearHeroSlot.SetItem` 把方块 `AddChild` 进孔）⇒ 只扫格的**直接子节点**必然漏 | 遍历改走引擎内建 `_detailTrinketGrid.FindChildren("*", string.Empty, true, false)`（⚠️ `owned: false` **必须给**：代码建的节点没有 owner，`owned: true` 会把它们全过滤掉） |
| ② | **事件理由假读数**（红线 26 违例） | 点方块也记 `Reason = drag-out` —— 回调没被告知玩家做的是哪件事 | 理由改由**调用方**传（`"click"` ／ `"drag-out"`）⇒ 事件记玩家**真做的那件事**（实测 `Reason = click` ✓） |
| ③ | **过期拒绝读数** | 卸下成功后，详情里仍留着上一次的「不可装备」行 | `UnequipTrinketFromUi` 里：`removed && _trinketRefusedHeroId == heroId` ⇒ 清空三个拒绝字段 ✓ |

## 五、🔴 顺手抓到的拖动专属缺陷（本件最重一笔）

**症状**（第一次复跑实测）：日志出现**自相矛盾的绿** —— 「拖出后现持 1/2（拖前 2）✓」而**事件是 `click`** ⇒ 也就是**另一件被卸掉了**（拖 `crow_tailfeather`、卸的是 `crow_wingfeather`）✓

**根因两条**：

1. **布局未落定那一帧按下的是旧实例** —— 上一步「点卸」触发的重建是 deferred ⇒ 拖动第 1 帧拿到的矩形属于**旧布局**（`(536,283),(106,35)`）⇒ 按在了**另一件**上 ✓
2. **旧判据只看「点数降没降」** —— 点数 `2 ⇒ 1` 成立 ⇒ **假绿**（降的是别人的点数）✓

**修法两条**：

1. 拖动加「**连续两帧读到同一矩形**」门（**只给拖动用**：点击点错位置最多是没命中、下一帧重试即可；拖动点错位置会**拖错那件**）✓
2. 点数判定加**事件归属**校验（`LastTrinketUnequipMatches`：读最近一条卸下事件，要求 `HeroId` **与** `TrinketId` **都**命中）✓

**复跑读数自洽**：拖动前等 4 帧布局（`第 1~4 次：… 还没连续两帧不变`）⇒ 第 5 次动手 ⇒ `拖出后现持 0/2（拖前 1）` ＋ 事件 `TrinketId = crow_tailfeather` ＋ `Reason = drag-out` ✓

⚠️ **帧预算口径**：`TrinketClickAttempts` **6 ⇒ 10**（点击与拖动**共用**这份帧预算；实测一次拖动前等了 4 帧 ⇒ 6 帧只剩 1 帧余量 ⇒ 放宽并把口径写进注释）✓

## 六、验收读数

| 命令 | 读数 |
|---|---|
| `dotnet build darkest\Darkest.sln -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` | **0 错误**（71 警告均既存） |
| `dotnet test darkest\Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --filter "FullyQualifiedName~SaveSystem\|FullyQualifiedName~Roster\|FullyQualifiedName~Trinket"` | **37 ／ 37 通过 · 0 失败 · 658 ms**（**只跑本件触到的三个面 · 不跑全量 · 不新增用例**） |
| `tools/dsh/selfcheck.ps1` | **9 ／ 9 OK**（内核无 Godot ／ 数字纪律三扫可疑 0 ／ UI 命名 ／ 门禁自检 ／ 占位符 ／ pwsh 语法 ／ 构建 0 错 ／ **567 文件 ≤600** ／ 无外部素材） |
| `python tools/check_file_budget.py` | rc=0 · **硬线超限 0** · 26 个 401~600 预警面（跨轮既有基线） |
| Godot 冒烟（§三 命令） | **EXIT=0** · 652 行 · 三条真手势读数见 §三 |

## 七、deviation（如实标注 · 数值一行未改）

1. ⚠️ **按 A 落地**（「装」的方块**从哪来** · A ／ B ／ C 三选一的答复未到 ⇒ 按**推荐项 A** 施工，可扩展）：本件只做「**孔 ＋ 卸下**」＋ 落孔入口；「饰品方块**从库里挑**、拖进孔」（B ／ C）留后续 ✓
2. ⚠️ **T6 验不了**：buff 原语层（`M2`）未接线（`#307` 冻结）⇒「装 2 件后属性 / 行为真的变了」**没有消费点** ⇒ 详情里如实标注，**不假装生效** ✓
3. ⚠️ **`O-112` ① ／ ③ 原样不动**：`hero_detail_skeleton.tscn` 缺两块节点（编辑器动作）· `limit` 无库存概念（待策划）✓
4. ⚠️ **`O-111` 职业名口径**（策划域 · **不自造映射表**）：真实名册上 **117 条通用件可装 ／ 79 条专属件整体不可装** ⇒ 本件读数里 T4 用的是**同件不重复**那一支（职业不符那支的读数来自上游轮）✓
5. ⚠️ **无外网**（沙箱）⇒「上网找开源轮子 / MCP 工具」做不到 · 如实标注；证据 = **新增依赖 0** ＋ 全引擎内建（`Viewport.PushInput` ／ `_GetDragData` ／ `_CanDropData` ／ `_DropData` ／ `FindChildren` ／ `Callable.CallDeferred`）✓
6. ⚠️ **冒烟噪声既有（非本件）**：`Failed to read the root certificate store.` ＋ `RID of type "CanvasItem" were leaked.` 等引擎退出噪声 ✓
7. ⚠️ **未跟踪杂物不入库**：`.tmp_*` ／ `.tmp_godot_appdata/` ／ 一批孤儿 `.uid`（`O-113` 登记：**不提交不删**）✓

## 八、边界与后续

- 🔴 **数值一行未改**：`trinkets.json` 只读（`#307` 冻结不破）· UI **不写死**任何 id 与判定 ✓
- 🔴 **单一真值**：饰品状态 = `Roster._trinkets` · 拒绝判据 = `Roster.CanEquipTrinket` · 槽位数 = `Roster.MaxTrinketSlots`（UI 读它，不写 2）· 理由串**只在内核生产** ✓
- ⚠️ **仍断的**：饰品**效果**（buff 原语层 `#307`）· `.tscn` 两块节点 · 专属件可用性（`O-111`）✓
- 下一件：`P6` 三处同步（`state.md` → `doc/modules/*` → 报告）／ `P0` 数据请单（23 条 `dmg%`）／ `P1` `M1c` 阶段 2 对照夹具 ✓

📄 关联：`reports/m4u_trinket_ui_20261002.md`（同族上一棒）· `doc/modules/trinkets.md §5.1` · `doc/state.md #507` · `doc/architecture/open_issues.md`（`O-111` ／ `O-112` ／ `O-113`）· `doc/architecture/tasks/dd1_workstreams.md §3` ✓
