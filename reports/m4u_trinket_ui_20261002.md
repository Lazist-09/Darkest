# 饰品（Trinket）落地（`M4u` · `M4` 内核 ＋ 详情显示 · 2026-10-02）

**施工单**：`doc/architecture/tasks/dd1_workstreams.md §3`（`M4u` `:49`）· 契约 `doc/modules/trinkets.md`（§1.3 槽位 = **2** · **§3 不要 `TrinketLedger`** · §5 T1~T6）

**用户裁定（2026-10-02 · 本件开工前）**：① 建筑详情选人 = **方块空洞 + 把角色方块头像放进孔里**（本件只做**显示 / 置灰 / 悬停**，**不抢拖放**）② 装备阶金币价（750/1750/3000/6000）**保持不动** ⇒ 冒烟走**公开 API 播种** ③ **授权**改组合根一行（`ExpeditionComposition.cs:126`）

**用户约束**：每文件 ≤400 行目标 / **600 硬线** · 可维护 / 可读 / 复用 · **尽量用引擎内建** · **不造轮子** · **只允许最低限度的必要性测试**（⇒ 本件**不新增用例**，只扩既有文件；验证 = **筛选测试 + Godot 冒烟**）

⚠️ 如实标注：本环境**无外网**（沙箱）⇒「上网找开源轮子 / MCP 工具」这一步在本机**做不到**；本件能给的证据 = **新增依赖 0** ＋ 全部引擎内建（`PanelContainer` · `GridContainer` · `Label.TooltipText` · `Label.AutowrapMode` · `Button.Pressed`）—— **不自造弹层、不自造 tooltip 组件** ✓

## 一、玩家可见行为（2 条玩家路径）

| # | 行为 | 入口 |
|---|---|---|
| ① | 名册点行（`--hamlet-row`）/ 右键头像 ⇒ 角色详情出现【饰品】区：**2 个孔**（`槽1=…｜槽2=…`）＋ `【饰品】N/2 格已装`；孔**空着就显示空**（不占位假数据） | `HamletRoot.HeroDetail.Trinkets.cs::RefreshTrinketSlots`（读数正文 `DescribeTrinketSlots`） |
| ② | **装不上的候选件**（职业不符 / 槽已满 / 同件重复）⇒ **整行灰字**（`BgColor.a = 1.0` 的全不透明灰 · 不是隐藏）＋ **悬停出全文理由**（理由**一个字不加**，原文来自内核） | 同上（`DescribeTrinketSlots` 的候选段 ＋ `TrinketTooltip`） |

**显示全部现算**（本件**不写死任何 id 与判定**）：槽位读数 = `Roster.TrinketsOf`（现读，不缓存）· 可装性 = `Roster.CanEquipTrinket`（**唯一判据落点**）· 库 = `TrinketsConfig`（**196 条** · `Get` fail-fast）⇒ 屏上 / 名册 / 内核**同一份**（红线 26）✓

🔴 **置灰不靠透明度**（`LayoutAudit` 判据 2：面板样式 `BgColor.a` 必须 1.0）：用**全不透明灰字 ＋ 理由整行**表达「装不上」⇒ 可断言、可读、不骗人 ✓

## 二、代码改动

| 文件 | 行数 | 类型 |
|---|---|---|
| `darkest/data/trinkets.json` | 3111 | **既有**（一手 196 条 · 本件一行未改） |
| `darkest/scripts/data/TrinketsConfig.cs` | — | **既有**（本件一行未改 · `ResPath`/`Parse`/`Get` fail-fast/`AwardCategoryOf`/`IsPurchasable`：**170 可买 / 26 不可买**） |
| `darkest/scripts/gameplay/sim/run/Roster.Trinkets.cs` | 157 | 🆕 **状态半**（`Roster` partial 第四片：状态 ／ 存档 ／ 怪癖 ／ **饰品**） |
| `darkest/scripts/gameplay/sim/run/Roster.Save.cs` | 151 | 改：`_trinkets` 入档 / 出档（+15） |
| `darkest/scripts/gameplay/sim/run/Roster.cs` | 482 | 改：阵亡者饰品一并清掉（+1） |
| `darkest/scripts/core/events/EconomyEventTypes.cs` | 73 | 改：🆕 `HeroTrinketEquippedEvent` / `HeroTrinketUnequippedEvent`（+7 · `Slot` 从 1 起） |
| `darkest/scripts/gameplay/sim/save/SaveSnapshot.cs` | 96 | 改：`RosterSnapshot.Trinkets`（**无默认值** ⇒ 构造点被编译器点名） |
| `darkest/scripts/gameplay/sim/save/SaveMigrator.cs` | 140 | 改：`CurrentVersion 3⇒4` ＋ 🆕 `TrinketFieldSinceVersion` ＋ **逐级迁移 v3⇒v4** |
| `darkest/scripts/gameplay/sim/save/SaveSerializer.cs` | 143 | 改：**按版本分档**判「缺 = 损坏」（+10） |
| `darkest/scripts/ui/HamletRoot.HeroDetail.Trinkets.cs` | 284 | 🆕 详情【饰品】分片（2 孔懒建 / 每开详情现读 / 被拒候选整行灰字 / 悬停全文 / `--hamlet-trinket-seed` 播种） |
| `darkest/scripts/ui/HamletRoot.HeroDetail.cs` | 517 | 改：**逐块**骨架布尔（`skelBarsUsed`/`skelStatsUsed`/`skelEquipUsed`/`skelTrinketUsed`）＋ 骨架报表打点 ＋ 接线调用 |
| `darkest/scripts/ui/HamletRoot.Build.cs` | **546** | 改：解析饰品库 ＋ 冒烟旗标收口（**603 ⇒ 546** · 见 §五 ②） |
| `darkest/scripts/ui/HamletRoot.Build.SmokeFlags.cs` | 84 | 🆕 **从 `Build.cs` 抽出的冒烟旗标族**（**只搬家、零行为改动**） |
| `darkest/tests/SaveSystemTests.cs` | 368 | 改：存档 **v4** 夹具 ＋ 饰品断言（+26/−…） |
| `tools/number_allowlist.txt` | — | 改：＋1 条（迁移链版本判断 = **结构性**） |
| `tools/deadfunc_allowlist.txt` | — | 改：＋1 条（`UnequipTrinket` = **契约先就位** · 消费方 = M4u 拖放接线） |

`git diff --stat`（11 个已跟踪文件）= **129+/89−**；**3 个新文件**未跟踪（`Roster.Trinkets.cs` ／ `HamletRoot.HeroDetail.Trinkets.cs` ／ `HamletRoot.Build.SmokeFlags.cs`）。**全部 LF · 无 BOM** ✓

## 三、内核三件（`M4` · 本件收口）

### ① 状态半（🆕 `Roster.Trinkets.cs` · 157 行）

- **为什么住在名册**：契约 §3 明写 **不要 `TrinketLedger`** ——「哪个英雄身上有哪几件」是**跨趟玩家状态**（入档），与士气 / 经验 / 疾病 / 怪癖**同层同型** ✓
- 每英雄 **2 槽**（`public const int MaxTrinketSlots = 2` · 契约 §3 · 策划 `#437`）；槽位**从 1 起**（与事件字段同口径）；内部 `List<string>` **保序**（= 槽位稳定 ⇒ 存档可 diff）
- 🔴 **拒绝理由只在本文件生产**（红线 21 (b)）：`CanEquipTrinket` 是**唯一判据落点**，`EquipTrinket` 与 UI **共用它** ⇒ 不存在「UI 自己再判一遍」的第二套真值。返回值契约：**`null` = 可以装 / 已装上；非 null = 拒绝理由**（人类可读，UI 原样显示）
- 三条拒绝理由：① 职业不符（`hero_class_requirements` 非空且不含本职业）② 同件重复 ③ 槽满
- 🔴 **只读口为什么必要**：UI 要把「装不上的」**置灰 + 显示理由**（T4），而「试着装一下看返回」会**真改状态** ⇒ 必须有一个**不写状态**的同一判据 ✓
- 🔴 **成功 ⇒ 必写事件**（`HeroTrinketEquippedEvent` / `HeroTrinketUnequippedEvent`）—— 与怪癖 / 疾病同一条「变更必留痕」纪律；卸下到空表 ⇒ **删字典条目**（空表不留孤儿 ⇒ 存档不涨）✓
- ⚠️ `EquipTrinket` **不管「能否购买」**（`IsPurchasable` 只服务商店列表）：战利品 / 任务奖励 / 事件给的**不可购买件照样能装** ✓

### ② 存档 v4（`M4u` 是 v4 的第一个字段持有者）

- `RosterSnapshot.Trinkets` **不给默认值**（构造点被编译器点名 —— 沿用本项目存档纪律）· `CurrentVersion 3⇒4` · 🆕 `TrinketFieldSinceVersion`（`Validate` 与迁移**同源常量**）
- **逐级迁移 v1⇒v2⇒v3⇒v4**（每级如实回报补了什么）；`Validate` **按版本分档**判「缺 = 损坏」（一刀切会把老档误判损坏）✓

### ③ 组合根缝桥（用户裁定 ③ · 上游 `M6u` 轮已落）

`ExpeditionComposition.cs:126` 原为**无条件** `new HeroGearState()` ⇒ 玩家在 Hamlet 买了阶、**一出征就被覆盖清零**，回城自动存档再把空表写盘（验收 ③ 在真实路径上不成立）。现为 `ExpeditionContext.Gear ?? new HeroGearState()` ✓

## 四、验收读数

### ① 构建 ＋ 测试（用户指令：**最低限度必要性测试**）

| 命令 | 读数 |
|---|---|
| `dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` | **0 错误** |
| `dotnet test Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --filter "FullyQualifiedName~SaveSystem|FullyQualifiedName~Roster|FullyQualifiedName~Trinket"` | **37 / 37 通过 · 0 失败 · 967 ms**（**只跑本件触到的三个面 · 不跑全量**） |

### ② 八门禁（全 rc=0）

| 门禁 | 读数 |
|---|---|
| `python tools/check_godot_refs.py --root darkest` | rc=0 · **0 hits**（内核四目录无 `using Godot`；`scripts/ui`/`scene` 无 `System.IO` · O-84） |
| `python tools/check_godot_refs.py --selfcheck` | rc=0 · 两条探针都抓到（负例 ＋ O-84 例）· **跑完自动清除**（复跑 `--root` 仍 0 hits ✓） |
| `python tools/check_file_size.py` | rc=0 · **560 文件 ≤ 600** · 0 allowlist |
| `python tools/check_file_budget.py` | rc=0 · **硬线超限 0**（本件把 `HamletRoot.Build.cs` 从 603 拉回 **546**）· 25 个 401~600 预警面 = **跨轮既有基线** |
| `python tools/check_data_discipline.py --all` | rc=0 · **三扫可疑 0**（numbers 0 ／ deadfuncs 0 ／ deadkeys 0）· 白名单 41 ＋ 死函数豁免 18 |
| `python tools/check_no_external_assets.py` | rc=0（B6 OK · 0 allowlist token） |
| `pwsh -NoProfile -File tools/dsh/check_ui_namespace.ps1` | rc=0（OK · `Darkest.UI` 无残留） |
| `pwsh -NoProfile -File tools/dsh/check_placeholders.ps1` | rc=0（PASS · 三判据全 0） |

### ③ Godot 冒烟（`--hamlet` · 玩家路径 · 4 组）

**① 基线**（`--hamlet-hero-detail=0` · 空槽）：

```
[UI 英雄面板] 骨架 hero_detail_skeleton.tscn 提供：状态条=True 属性列=True 装备行=False 饰品格=False（缺的块走代码建 · 不静默）✓
[UI 英雄面板] ✅ 饰品 2 孔就位（真读数 · 同源 Roster.TrinketsOf；拖放未接线）✓
[M4u·饰品] hero_warrior_1：槽1=空｜槽2=空｜【饰品】0/2 格已装　（装备/卸下入口未接线 · 拖放属 M4u 后续；buff 原语层未接线 #307）（库 196 条）✓
```

**② T3 槽满**（`--hamlet-trinket-seed=hero_warrior_1:crow_wingfeather,crow_tailfeather,crow_talon`）：前 2 件装上，第 3 件**被拒**

```
[HamletRoot] 饰品播种：hero_warrior_1 ⇒ 「crow_wingfeather」装上（现持 1/2）✓
[HamletRoot] 饰品播种：hero_warrior_1 ⇒ 「crow_tailfeather」装上（现持 2/2）✓
[HamletRoot] 饰品播种：hero_warrior_1 ⇒ 「crow_talon」**被拒**：「老铁」的饰品格已满（2 格）—— 先卸下一件再装「crow_talon」✓
[M4u·饰品] hero_warrior_1：槽1=crow_wingfeather｜槽2=crow_tailfeather｜候选「crow_talon」=不可装备｜【饰品】2/2 格已装｜🔴 不可装备：「crow_talon」—— 「老铁」的饰品格已满（2 格）—— 先卸下一件再装「crow_talon」（库 196 条）✓
```

**③ T4 职业不符**（`--hamlet-trinket-seed=hero_warrior_1:sacred_scroll`）：

```
[M4u·饰品] hero_warrior_1：槽1=空｜槽2=空｜候选「sacred_scroll」=不可装备｜【饰品】0/2 格已装｜🔴 不可装备：「sacred_scroll」—— 「老铁」（warrior）职业不符：「sacred_scroll」限定 vestal（库 196 条）✓
```

**④ 拆片后回归**（本件把冒烟旗标族搬出 `Build.cs` 之后**原样复跑 ②**）：

```
[t3_after_move] exit=0 行数=300 真错误=3（= 既有环境噪声：根证书 1 条 ＋ RID/texture 泄漏 2 条）
line 与 ② 逐字相同 ⇒ 搬家零行为改动（旗标链未断）✓
```

## 五、deviation（如实标注 · 数值一行未改）

1. 🔴 **职业名口径冲突（本件最大偏差 · 待裁）**：我方名册实际职业 = `warrior / tank / medic / commissar`（8 人），而 `trinkets.json` 的 `hero_class_requirements` 用的是 **DD1 的 15 个职业名**（`vestal` / `houndmaster` …）⇒ **117 条通用件（要求为空）可装 · 79 条专属件在真实名册上整体不可装**。🔴 **不自造映射表**（那会成第三套 truth）⇒ 登记 🆕 `O-111` 待策划裁（选项：① 补职业名对照表 ② 专属件改按我方职业名重制 ③ 专属件暂不入池）✓
2. ⚠️ **骨架缺两块 ⇒ 本件已修「沉默」**：`hero_detail_skeleton.tscn` **没有** `HeroEquipmentRow` / `HeroTrinketGrid` 节点 ⇒ 原来「骨架给了没有」与「读没读到」**分不清**（本件实测读数里两块恒 `False` 且无打点）。改法 = **逐块布尔**（`skelBarsUsed`/`skelStatsUsed`/`skelEquipUsed`/`skelTrinketUsed`）＋ 骨架报表打点 ⇒ 缺哪块、走没走代码建，**屏上可读**。⚠️ **`.tscn` 仍未补节点**（在编辑器里补即可 · 属后续美术/编辑器动作）✓
3. ⚠️ **buff 原语层（`M2`）未接线** ⇒ 契约 T6「装 2 件后属性变了」本件**验不了**：详情里以「buff 原语层未接线 · `#307`」**如实标注**，**不假装生效** ✓
4. ⚠️ **`limit`（同件持有上限）本件不判**：我们还没有「饰品库存 / 副本」概念（入手路径未接线 ⇒ 冒烟走公开 API 播种）⇒ 凭空判 `limit` 只会造一条**玩家永远触发不到的规则** ⇒ 登记待策划（**不预造 API**）✓
5. ⚠️ **拖放接线属 M4u 后续**：本件只做**显示 / 置灰 / 悬停**；「把方块头像 / 饰品方块拖进孔」= 下一步（`UnequipTrinket` 已就位并入了 `deadfunc_allowlist` 说明「契约先就位 · 消费方 = 拖放」）✓
6. ⚠️ **无外网**（沙箱）⇒「上网找开源轮子 / MCP 工具」做不到 · 如实标注；证据 = **新增依赖 0** ＋ 全引擎内建（`PanelContainer` 孔 · `GridContainer` 2 列 · `Label.TooltipText` 悬停 · `Label.AutowrapMode` 折行）—— **不自造控件** ✓
7. ⚠️ **`HamletRoot.Build.cs` 603 ⇒ 546（本件拆片）**：新增旗标把该文件推过 **600 硬线**（`check_file_size` 当场红）⇒ 把**冒烟旗标族**（悬停 ／ 弹窗 ／ 二级屏 ／ 播种：`--hamlet-hover/building/controls/loot/heirloom/quest-select/provision/menu/popup-close` ＋ 怪癖/饰品播种）搬进 🆕 `HamletRoot.Build.SmokeFlags.cs`（84 行），**只搬家、零行为改动**（逐条同序 · 冒烟 ④ 逐字复验）⇒ `--hamlet-hero-detail` / `--hamlet-row` 两族**留在原地不动** ✓
8. ⚠️ **冒烟噪声既有（非本件）**：`Failed to read the root certificate store.`（1 条）＋ `Parent path './X' …has vanished` ／ `RID of type "CanvasItem" were leaked.`（引擎退出行为 · 2 条）⇒ 冒烟脚本单列 `真错误=3` 就是这三条 ✓
9. ⚠️ **孤儿文件不入库**：`darkest/tests/M9FourVFourFixtureTests.cs.uid` 仍在工作区（未跟踪）⇒ **不提交、不删**（与本件无关）✓

## 六、复现命令（原文 · cwd = 仓库根）

```powershell
$env:APPDATA = '<可写沙箱目录>'   # Godot 要写 user:// ⇒ 不设会 signal 11
$EXE = 'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe'

# ① 基线（空槽 · 骨架报表）
& $EXE --path darkest --headless --audio-driver Dummy --hamlet --hamlet-hero-detail=0 --quit-after 900

# ② T3 槽满（前 2 件装上 · 第 3 件被拒 ⇒ 整行灰字 + 悬停全文）
& $EXE --path darkest --headless --audio-driver Dummy --hamlet --hamlet-hero-detail=0 `
  --hamlet-trinket-seed=hero_warrior_1:crow_wingfeather,crow_tailfeather,crow_talon --quit-after 900

# ③ T4 职业不符（warrior 撞 vestal 专属件）
& $EXE --path darkest --headless --audio-driver Dummy --hamlet --hamlet-hero-detail=0 `
  --hamlet-trinket-seed=hero_warrior_1:sacred_scroll --quit-after 900

# 存档 v4（读档行 · 每次冒烟都会打）
& $EXE --path darkest --headless --audio-driver Dummy --hamlet --quit-after 900
```

（冒烟日志留档 = 本次会话 scratch 目录 `probe_holding/runs/*.txt`，**不入库** ✓）

## 七、边界与后续

- 🔴 **数值一行未改**：`trinkets.json` 只读（`#307` 冻结不破）· **UI 不写死任何 id 与判定**（名字 ／ 职业要求 ／ 悬停全文全走 `TrinketsConfig`；可装性全走 `Roster.CanEquipTrinket`）· **不自造理由串** ✓
- 🔴 **单一真值**：饰品状态 = `Roster._trinkets`（唯一）· 拒绝判据 = `Roster.CanEquipTrinket`（唯一）· 槽位数 = `Roster.MaxTrinketSlots`（唯一 · UI 读它，不写 2）✓
- ⚠️ **仍断的**：饰品**效果**（buff 原语层 `#307`）· **装/卸 UI 入口**（拖放接线）· **专属件可用性**（`O-111` 职业名口径）· `.tscn` 的两块节点
- 下一件（UI 域并行）：`M6u` 建筑升级树 ／ 回到主线 `P0` 请单（23 条 `dmg%`）＋ `P1` `M1c` 阶段 2 对照夹具（`dd1_workstreams §3`）✓

📄 关联：`doc/architecture/tasks/dd1_workstreams.md §3`（`M4u` `:49`）· `doc/modules/trinkets.md` · `doc/state.md #504` · `doc/architecture/open_issues.md`（🆕 `O-111` / `O-112`）· `reports/m5u_quirk_ui_20261001.md`（同族前一棒）✓
