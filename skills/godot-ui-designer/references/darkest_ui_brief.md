# Darkest 项目 · UI 设计师简报（收件箱转写）

> 🔴 **转写标记（回读这就是"已写进 skill"的证据，红线 20）**：`INBOX-TRANSCRIBED-DARKEST-UI-20260914`
> **转写来源**：`doc/windows/主程序窗口.txt`（508 行）· `doc/windows/策划窗口.txt`（950 行）· `doc/windows/架构窗口.txt`（空）
> **转写后处置**：三个来源窗口**已清空**（用户指定的收件箱协议）；全文可由 git 恢复，例：`git show HEAD:doc/windows/主程序窗口.txt`
> ⚠️ **本文件不是权威**：它是"UI 侧行动摘要 + 台账"。冲突时以 `doc/modules/ui_spec.md`、`doc/architecture/**`、`doc/state.md` 的决策号（`#3xx`）为准。

---

## 0. 角色 · 分工 · 收件箱协议

| 角色 | 定义什么 | 落点 |
|---|---|---|
| 策划 | **什么算对**（规格/判据/数值口径） | `doc/modules/*.md`、`doc/GDD.md`、`doc/state.md` |
| 架构 | **怎么核对**（契约/红线/分层） | `doc/architecture/**`、`doc/architecture/open_issues.md` |
| 主程序 | **实现 + 自测 + 取证** | `darkest/**` + 提交信息 |
| **UI 设计师（本 skill）** | **界面结构 / 视觉规范 / 可断言布局** | `darkest/scripts/ui/**`、`darkest/scenes/**`、`darkest/resources/**` |

**收件箱协议（本项目通用，四角色一致）**
```
① 读自己窗口 ② 读完【清空自己】（清前确认已处理/已转写） ③ 干活 ④ 写对方窗口【追加不覆盖】
   （先读→拼接→整体 write 写回）
🔴 写入失败 = 未送达 ⇒ 当轮补投；返回 `file changed since it was read` 立刻重读补写
🔴 送达判据 = 【回读我的投递标记】（`Select-String <我的标题>`）—— 不得用【行数/字节数】（红线 20：
   实测 `Get-Content(...).Count` 报 448、实际 689，CRLF/LF 混用 ⇒ 分行口径不同）
```
**本 skill 采用的收件箱口径（用户指定）**：把 `doc/windows/` 下**其他角色的窗口**当收件箱读 ⇒ 转写进本 skill ⇒ **读完直接清空**。

**分工边界（不代笔）**：UI 侧**不得**改 `darkest/scripts/core|data|gameplay/sim`（内核，零 Godot）、`darkest/data/*.json`、`doc/architecture/**`；发现别层错误 ⇒ **登记 + 交接**。

**当前冻结**：`#307` 数值冻结中 ⇒ 一切 UI 工作**零数值改动**；`#244` 纪律 = **一次一个轴**。

---

## 1. 🔴 最高优先级：`#319` 布局基建（用户原话）

> **用户原话**：「主要是**现有 UI 杂乱无章，文字各种重叠**，我只希望那种**自己在自己框里，
> **框不能是透明的**之类的」⇒ 🔴 **这不是美术问题，是【布局基建】问题**，**排在 §13 素材路线之前**。

### 1.1 两条根因（`ui_spec §14.1`）
| 现象 | 根因 |
|---|---|
| 文字各种重叠 | 每个 `Label` **手写 `Position`/`Size`（绝对坐标）** ⇒ 互相压、算不准、改一处崩一片 |
| 框是透明的 | **没有 `Panel`** —— 文字直接画在背景上 ⇒ 读不出边界 |

> 🔴 **"加美术素材"治不了它**：素材贴上去，文字照样重叠。

### 1.2 四条硬规则（`ui_spec §14.2`，照做即可）
```
① 移除所有手写 Position/Size —— 除顶层三类容器外，任何节点不得出现 Position = new Vector2(x,y)
② 每个信息块 = PanelContainer + StyleBoxFlat（不透明）：底深色 · BgColor.a = 1.0 · 1px 边框 · 圆角 0
③ 面板内部 = VBoxContainer（子项自动堆叠 ⇒ 物理上不可能重叠）
   横排 ⇒ HBoxContainer · 留白 ⇒ MarginContainer · 间距 ⇒ separation
④ 容器必须给【最小尺寸】—— 否则高度塌陷 ⇒ 又重叠（① 之外最常见的坑）
   文本类 ⇒ custom_minimum_size 或 Label.autowrap_mode = WordSmart + size_flags
```

### 1.3 顶层结构（`ui_spec §14.3`）
```
Root → MarginContainer（全屏留白）→ VBoxContainer（顶栏 / 主体 / 底栏）
  ├─ 顶栏 PanelContainer → HBoxContainer（左：状态 · 右：按钮）
  ├─ 主体 HBoxContainer
  │   ├─ 左 PanelContainer → VBoxContainer（角色面板 / 建筑区）
  │   └─ 右 PanelContainer → VBoxContainer（名册竖列 / 多功能框）
  └─ 底栏 PanelContainer → HBoxContainer（资源条 · Embark · 图标）
🔴 要点：每个分区一个 Panel ⇒ "各在各的框里"；面板之间用 separation 留白（不靠坐标）
```

### 1.4 统一 `Theme`（`ui_spec §14.4`）
```
落点：resources/theme/（§14.4 写作 darkest.tres；当前实现是 dd_theme.tres ⇒ ⚠️ 命名口径待统一）
内容：字体（开源衬线 OFL，§13.3）· 字号三档（标题/正文/小字）· 前景四色（正文近白/强调金/危险红/弱化灰）
      · 背景：不透明深底 · Panel 样式一处定义 ⇒ 取代 20 处 AddTheme*Override
🔴 纪律：颜色/字号不得在节点上硬写 ⇒ 必须走 Theme（否则"统一"是假的）
```

### 1.5 两条自动判据（`ui_spec §14.5` / `#319`⑤，已入验收）
```
🔴 判据 1：遍历当前界面【所有可见 Label】⇒ 两两不相交（Rect2.Intersects 全为假）
🔴 判据 2：每个 Panel 的 BgColor.a == 1.0（"框不能透明"直接可断言）
⚠️ 必须遍历【每个界面】：战斗 / 城池 / 角色详情 / 地图
🔴 价值：把"没有重叠"从"看起来还行"变成【可测】（与红线 25「动作 ≠ 意义」同路数）
```

### 1.6 逐屏进度（实测读数）
| 界面 | 现状 | 重叠对 | 透明框 | 状态 |
|---|---|---|---|---|
| **城池**（`HamletRoot`，提交 `9941c70`） | Root→Margin→VBox（顶栏/状态/主体［左操作/右名册］/底栏），**5 个 PanelContainer**，删尽手写坐标 | **2 → 0** | 0（Panel 0 → 5） | ✅ |
| **角色详情**（提交 `8f3a9df`） | `Node2D`+绝对坐标 → **满屏不透明 `PanelContainer`** + Margin→VBox（标题/左右两栏/返回行） | 0 | 0 | ✅ |
| **地图**（`Expedition.tscn`，提交 `b8d98b1`） | 5 个面板类改 `PanelContainer` + 组进 `MarginContainer→VBox`（唯一 UI 落点）；Theme 挂容器树根 | **9 → 0** | **3 → 0** | ✅ |
| **战斗**（`BattleUi`） | 已建满屏 `Control` 根（`_uiRoot`）+ `TopRow/MidRow/BottomRow` 三个 `PanelContainer` | 基线 **12** | 基线 **7** | 🔴 **未通过（最后一个）** |

**战斗屏现状（窗口之外，git 取证）**：`81329e6` 记「真根因已定位并修好（**冒烟器同步切场景**触发引擎错误并连锁污染判据），
但**审计钩子在修复后不再输出** ⇒ 本屏仍未通过（如实）」⇒ 🔴 **当前第一件事不是"再排版"，而是让取证恢复**（见 §6）。

### 1.7 已知坑（8 条，全部实机踩过）
```
① CanvasLayer / Node2D 不是 Control ⇒ 🔴 Godot 的 Container【不排它】（Container 只管理 Control 子节点）
   ⇒ 既没有"框"（不在 Control 链上 ⇒ 不受 Theme 管）又根本进不了容器树 ⇒ **只挪坐标会掩盖结构问题**
② Theme 只沿 Control/Window 祖先链继承 ⇒ 子树没挂在带 Theme 的根下 ⇒ 拿引擎默认样式
   （实测 Panel 默认 a = 0.6 ⇒ 判据 2 直接抓到"框透明"）
③ PanelContainer 继承自 Container（不是 Panel）⇒ 判据 2 只收 Panel 会【假通过】（已修）
④ 满屏不透明浮层（模态，如角色详情）⇒ 判据必须【只审浮层内部】，否则浮层下被遮住的 Label 被算成重叠（实测 16 对假重叠）
⑤ 容器不给最小尺寸 ⇒ 高度塌陷 ⇒ 又重叠（实例：光照刻度宿主最小高度 46 → 66）
⑥ 锚点/尺寸要在【入树后】才算得对；`_Ready` 内直接切场景报 `Parent node is busy adding/removing children`
   ⇒ 必须 `CallDeferred`（`blueprint §9.14`）
⑦ 代码里新建的控件必须加进【同一容器】（`_uiRoot`/`uiMargin`）⇒ 否则逃出子树（实测"可见 Label 0"）且仍互压
⑧ 审计回调**不得捕获 `this`**（切场景被释放 ⇒ 静默无输出）；定时审计要等场景切完再审
📌 总纪律："通过了"之前先问【它到底检查了什么】—— 判据自身的口径也要自检
```

---

## 2. 表现层四规格（`ui_spec §12`，`#315`；主程序唯一剩余阻塞项）

### 2.1 动效（只做 4 个，`§12.1`）
```
🔴 原则：动效只表达【发生了什么】，不表达"多严重"（严重程度由【数字 + 颜色】表达）⇒ 短、克制、不抢信息
① 出现（伤害数字/事件条目）：短促上浮 + 淡出 0.3s
② 受击（单位）：短促位移抖动 + 闪白 0.15s
③ 士气崩溃（折磨/死门）：屏幕边缘暗角 + 该单位框变红 0.5s
④ 结算（面板出现）：淡入 0.2s
🔴 明确不做：常驻呼吸/待机 · 转场过场 · 技能特效（属美术）
🔴 并写死：动效【不得延迟玩家可操作的时间】（动画期间输入不得被吞）
```

### 2.2 音效（最小 3 类，`§12.2`）
```
① 命中（区分我方/敌方，伤害结算）② 受击·死门（受击/进死门/阵亡）③ 结算（胜/败）
🔴 没有音源时 ⇒ 用【占位音】（程序生成正弦/噪声短音），不是"静音"
   理由：静音会让"没做"和"做了但没声"无法区分（红线 21 同源）⇒ 且【音源缺失必须能跑】
```

### 2.3 描边 / 暗角 / 闪白（`§12.3`）：✅ **接受用 `ShaderMaterial` 统一实现**
视觉规范（描边宽度/颜色/暗角强度/闪白时长）**仍属 `ui_spec §1.4` 六条**，材质只是它的**一致实现**。

### 2.4 i18n（`§12.4`）：**M8.3 之后做，但现在【必须】用容器 + 锚点**
> 一句话：**i18n 的【文本】可以后补，但【布局方式】必须现在对**（硬编码坐标 + 多语言文本长度 ⇒ 必错位）
⇒ 因此 ② Theme+容器+锚点 与 ③ 焦点+手柄 同批排；"i18n 布局先对"**不必单列一轮**。

### 2.5 `O-84`（表现层读数据，已修完 = 合并包片 A）
`BattleUi.ReadData()` 原用 `System.IO` 磁盘路径 ⇒ 导出构建里 `data/*.json` 在 **PCK 内** ⇒ `File.Exists` 失败 ⇒ **单场战斗入口在发行版直接崩**。
✅ 裁定：**表现层读数据一律 `FileAccess` / `ResourceLoader`；`System.IO` 只允许在内核**（= 硬边界 **B7**，红线 26）。
✅ 门禁：`tools/check_godot_refs.py` 第二条规则禁表现层 `File.ReadAllText|File.Exists|DirectoryInfo|AppContext.BaseDirectory|Path.Combine` + `--selfcheck`；**实测表现层命中 0**。

---

## 3. 视觉资源与授权（`ui_spec §13`，`#318`）—— "像 DD" 但不抄 DD

```
🔴 不得使用：DD 的贴图 / 立绘 / 字体文件 / Spine（.atlas/.skel）/ FMOD 音频（.bank）
   —— 不存在"合法能拿的那部分"（红线 27）
✅ 可以抄【视觉语言】：深底 + 极强对比 + 深色粗描边 + 老式衬线 + 金/红点缀 + 暗角 + 受击闪白
🔴 判据：工程内不得出现任何来自 DD 安装目录的文件（取证 = 资源清单比对 + 命名来源可追溯）
🔴 纪律：① 每个外部资源记【来源 + 授权 + 是否需署名】② 需署名的（CC BY）必须有署名位置
        ③ 不得用"授权不明"的资源 ④ 来源清单落 doc/assets_credits.md
```
**实施顺序（"最小可感知"优先，`§13.4`）**：
```
① 字体（一个开源衬线，OFL：IM Fell English / Cinzel / EB Garamond）← 收益最大、成本最低，换字体气质立刻变
② 配色规范（深底+强对比+金/红点缀）⇒ Theme 统一
③ 描边 + 暗角（ShaderMaterial）⇒ 取代 20 处逐节点样式
④ 装饰线（金线/红分隔）⇒ Line2D / 九宫格，自己画
⑤ 图标（game-icons.net CC BY / Kenney.nl CC0）
⑥ 动效 + 音效（§12.1 / §12.2）
```
> 建议先做 ①+②+③：**零授权风险 + 零素材依赖 + 收益最大**。

---

## 4. Godot 内置工具清单（架构审计 `godot_builtins_audit.md`，红线 26）

**分层**：内核（core/sim/data/tests）**刻意零 Godot**（保持，不是浪费）；**表现层未充分利用**（该用没用）。

**11 项清单 · 执行顺序（`#319`⑦ 调整后，`next_round §4`）**：
```
② Theme + 容器 + 锚点（布局基建）【优先】  →  ③ 焦点 + 手柄（✅ 已完成）
→ ④ 动效 → ⑤ 音效 → ⑦ 材质描边 / 字体 / 图标（素材路线） → ⑩ i18n
🔴 纪律：②~⑪ 一律不得反向要求内核改；每轮 UI 【只挑一项】（一次一个轴）
✅ 已落地：① InputMap · ② Theme（继承已生效）+ 容器/锚点在城池/角色详情/地图 · ③ 焦点/手柄
⏳ 未做：④ 动效 · ⑤ 音效 · ⑥ 预制件 · ⑦ Shader 描边 · ⑧ [Tool] 数据面板 · ⑨ 帧预算（Performance.GetMonitor）
        · ⑩ i18n（只做"布局先对"）· ⑪ （仅表现层）AStarGrid2D/TileMapLayer 地图视图
```

---

## 5. 代码落点 · 取证命令

**UI 代码（`darkest/scripts/ui/`）**
```
DdTheme.cs            中央 Theme（语义色 7 个 · 字号三档 18/15/12 · 不透明 Panel 样式 bg α=1.0 + 1px 边框 + 圆角 0）
                      Apply(Control root) 向下继承；Audit() 读数字；DumpTo() 存 .tres
LayoutAudit.cs        两条判据 + 覆盖层口径；Check(Node) ⇒ (bool, string)
MainMenuRoot.cs       --ui-audit / --theme-audit / --input-audit 钩子；PrintUiAudit() 挂【场景树根的定时器】⇒ 跨场景存活
HamletRoot.cs         城池 + 角色详情（覆盖面板）
ExpeditionRoot.cs     远征/地图侧；组合根（内容表、Curio 选择）
BattleUi.cs           : CanvasLayer（⚠️ 不是 Control）⇒ 已建满屏 _uiRoot + TopRow/MidRow/BottomRow
LightBarPanel / ScoutMarkPanel / PathChoicePanel / InventoryPanel / ExpeditionListPanel.cs（地图侧面板类）
BattleMiniMap.cs      战斗右下角地图
```
**资源 / 场景**：`darkest/resources/theme/dd_theme.tres`（323 B，⚠️ 与 §14.4 的 `darkest.tres` 命名不一致）·
`darkest/scenes/{battle/Battle,expedition/Expedition,hamlet/Hamlet,main/MainMenu}.tscn`

**取证命令（本机 Godot 4.6.1 mono）**
```powershell
# 构建 / 单测（当前口径 470 项，以最近一次运行为准）
dotnet build darkest\Darkest.sln --nologo
dotnet test  darkest\Darkest.sln --nologo --no-build
# 门禁（两条规则 + 负向自检）
python tools\check_godot_refs.py --root darkest ; python tools\check_godot_refs.py --root darkest --selfcheck
# 🔴 布局判据（每个界面都要跑：判据 1+2 全绿才算过）
& 'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe' --headless --path darkest -- --hamlet --ui-audit
& 'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe' --headless --path darkest -- --topology --ui-audit
# 其他入口/冒烟（全走【真实 Pressed】，红线 26）
--hamlet-row=0 · --hamlet-detail-back · --hamlet-hover=<building> · --hamlet-embark · --e2e · --expedition
--smoke=main:1,map:0,camp,finish,auto,map:0   （步骤器：每进场景消费一步 + 0.2s tick；未知步骤 ⇒ exit 2）
```

---

## 6. 下一步（UI 侧建议顺序）

```
① 🔴 战斗屏（最后一个未过判据的界面）—— 先恢复取证，再排版：
   a. 查"审计钩子在修复后不再输出"（`81329e6`）⇒ 先让 --ui-audit 在战斗屏重新打印（`PrintUiAudit` 的定时器/
      `CallDeferred` 时序 / 回调不得捕获 this）—— 🔴 取不到数就不是"通过"（红线 25）
   b. 按 §14.2/14.3 把 49 Label / 30 Panel / 12 重叠 / 7 透明 改到判据 1+2 全绿（手法照地图屏：
      改类 → 组容器树 → Theme 挂容器根 → 每步跑 --ui-audit）
② 字体（§13.4①）+ 配色（§14.4）：OFL 衬线落 resources/theme/，顺手统一 darkest.tres / dd_theme.tres 命名口径
③ ④ 动效（§12.1 四个，不吞输入）  ④ ⑤ 音效（§12.2 三类 + 占位音）
⑤ ⑦ ShaderMaterial 描边/暗角/闪白（视觉规范仍属 ui_spec §1.4）
⑥ ⑨ 帧预算基线（Performance.GetMonitor）
⑦ ⑩ i18n：只做"布局先对"（随容器化已基本达成）
```

---

## 7. 待他方口径 / 已知未决（不阻塞 UI 侧）
```
· 线性模式战斗地图：卡写"隐藏"，主程序做成【说明"无地图（不在拓扑远征里）"】⇒ ⚠️ 待策划一句（红心 21：不留不可解释的空白）
· `ui_spec §12` 是否"已存在"：策划 `#318` 说早已写；架构一度复述"阻塞在 §12"⇒ 主程序已按 `#318` 认为不阻塞
  ⇒ UI 侧执行口径：【按 §12 照做，不必等】
· 解锁态 UI（已实现）：未解锁建筑显示「🔒 名称（第 N 趟后解锁）」；名册计数「8 / 8（上限 12）」
  ⇒ C1/C2/C3：`roster.cap` 硬上限 12、unlocks 控【当前可用上限】；消费点三个（建筑可见性 / Curio 池 4→6 / 名册上限）
· resources/theme 命名：`§14.4` = darkest.tres，实际 = dd_theme.tres（⚠️ 须一处定义，别留两个名字）
```

---

## 8. 本轮收件箱投递台账（清空前的全部消息，按窗口原文顺序）

**`doc/windows/主程序窗口.txt`（= 主程序的收件箱；发件人 = 策划/架构）**
| # | 发件人 | 主题（决策号） | UI 相关结论 |
|---|---|---|---|
| 1 | 策划 | Curio 规格取代"随机事件"（`#313`） | 事件房 UI = 物件 + **三按钮（空手／用道具／走开）** + 结果**描述文本** + "未接线不列出"（红线 21） |
| 2 | 架构 | Godot 内置工具利用率审计 | 表现层未充分利用；`O-84` 会崩；六项缺失（焦点=0／帧预算=0／Shader=0／i18n=0／Paused=0／prefabs 空）；11 项清单 |
| 3 | 架构 | 真机 DD 架构侦察 | 🔴 **UI 布局数据化对我方 = 倒退**；Godot 正解 = `Control` 树 + 容器 + 锚点 + `Theme`；红线 27（不抄素材） |
| 4 | 架构 | `#315` 落盘 | ⑦ 材质描边 ✅；⑩ i18n **M8.3 后做但现必须容器+锚点**；②③ 同批；④⑤ 归 `ui_spec §12` |
| 5 | 架构 | 合并执行包 A~E | 八条硬边界 **B7** = 表现层只走 `FileAccess` |
| 6 | 策划 | `#316` 三件全答 | 先三界面、再解锁（**同一块 `HamletRoot` 呈现**） |
| 7 | 策划 | `#317` next_round 五项 | ② 三界面收尾；③ 解锁（锁着的必须**说明何时解锁**）；④ 表现层顺序 **②→③→④→⑤→⑦→⑩**，②必须做 |
| 8 | 架构 | 合并包核过 + `next_round` 收编 | C1/C2/C3：**unlocks 三个消费点**（含城池建筑可见性） |
| 9 | 策划 | `#318` | ④动效/⑤音效规格**在 `ui_spec §12`**；**不得用 DD 素材**；风格化顺序（字体→配色→描边→…）；**行数不可当判据** |
| 10 | 策划 | `#319` 布局基建 | 🔴 **最高优先**：两条根因 / 四条硬规则 / 顶层结构 / Theme / **两条自动判据** / 实施顺序；素材路线往后放 |
| 11 | 架构 | `#319` 落盘 + 判据入验收 | 顺序确认；判据已入 `next_round §4.1`；**红线 20**（送达判据 = 回读标记）；建议 1=容器化推到其余界面+挂判据、2=帧预算、3=i18n 布局先对 |

**`doc/windows/策划窗口.txt`（= 策划的收件箱；发件人 = 主程序/架构）**
| # | 发件人 | 主题 | UI 相关结论 |
|---|---|---|---|
| 1 | 主程序 | 片① 城池 / 片② 角色详情 已交（428/428） | 城池横幅/名册/资源条/Embark；角色详情左右两栏；**扎营技能只列已接线** |
| 2 | 主程序 | 片③ 前置：跨场景步进冒烟器 | `--smoke=步骤`，全走真实 `Pressed`，未知步骤 `exit 2` |
| 3 | 主程序 | Curio 首版完工 V1~V7 | UI 级真实 `Pressed`：三按钮 / 描述文本 / 阶段二显式标"未接线·不生效" |
| 4–5 | 主程序 | 接线后重跑读数（V10/A1/A2）+ 每档三项 | 非 UI（平衡读数） |
| 6 | 主程序 | Curio 全量收口 9 kind（446/446） | 三界面 ③ 战斗 E 区多功能框（详情/日志/地图）✅ |
| 7 | 架构 | 内置审计：**四项需策划补规格** | ④动效 ⑤音效 ⑦描边材质 ⑩i18n（后由 `#318` 答） |
| 8–10 | 架构 | 真机三条形态候选 / `#315` 落盘 / 合并包合成 | `O-85`/`O-86`/`O-87`（数据层，非 UI） |
| 11–14 | 主程序 | 合并包片 A/B/C/D/E | 片 A = `O-84` 修 + 门禁 0 命中；"读数不变 ≠ 生效，而是口径不覆盖" |
| 15 | 架构 | `#316`/`#317` 落盘 + C1/C2/C3 + P26/P27 | 判读自检升格为判据 |
| 16 | 主程序 | 判读逻辑真 bug 已修（463/463） | 表现层状态：① InputMap ✅ ／ ③ 焦点·手柄 ✅ ／ ② Theme ✅ + 容器/锚点 E 区已改 |
| 17–18 | 主程序 | ③ 解锁：内核 + UI + 玩家路径验收 | 城池三栋按解锁显示 + 「🔒 名称（第 N 趟后解锁）」；名册「8 / 8（上限 12）」；**C2 内核也要拦** |
| 19 | 主程序 | ② 三界面收尾玩家路径复核 | 战斗 E 区五页（拓扑显示地图/线性用【说明】）；顶部进度条（线性如实标"无段数口径"） |
| 20 | 主程序 | ⑤ P26 `$pool:` 已交（470/470） | ④ 表现层唯一剩项 = 动效/音效（等规格）；附行数口径教训 |
| 21 | 架构 | `#319` 落盘 + 判据精确化 | 仅 §12 两条规格（架构视角）被视为阻塞 |
| 22 | 主程序 | `#319` 第 1 步：`LayoutAudit` + 不透明 Panel 样式 + `--ui-audit` | **基线**：城池 11 Label／0 Panel／2 重叠／0 透明；战斗 49／30／**12**／**7**；两个坑（切场景要等切完、回调不得捕获 `this`） |
| 23 | 主程序 | 城池 ✅ + 角色详情 ✅ | 详情**必须显式挂 Theme**（否则 a=0.6）；两次修判据口径（`PanelContainer` 漏检 / 模态只审浮层内部） |
| 24–25 | 主程序 | 地图屏基线 + 第 1 步 | 根因：5 个面板类是 `Node2D`/`CanvasLayer` + 手写坐标 ⇒ `Panel+PC = 0`；**只挪坐标会掩盖结构问题** |
| 26 | 主程序 | 地图屏改完 ⇒ 判据 1+2 全绿（`b8d98b1`） | 🔴 根因：**`CanvasLayer` 不是 `Control` ⇒ 容器不排它**；Theme 必须挂**容器树根**；四屏进度：只剩战斗 |

**窗口之外（git 取证，最新）**：`81329e6` 战斗屏真根因 = 冒烟器同步切场景 ⇒ 修好后**审计钩子不再输出** ⇒ 本屏仍未通过。

---

## 9. 与主程序的操作约定（2026-09-14 交接，主程序投递 `doc/windows/UI设计师窗口.txt`）

> 🔴 **送达标记**：`Select-String -Path doc\windows\UI设计师窗口.txt -Pattern '四屏现状交接'`（回读命中即送达；不得用行数/字节数）

### 9.1 分工与"不代笔"
| 角色 | 定义/实现什么 | 落点 | 禁止 |
|---|---|---|---|
| **UI 设计师（本 skill）** | 界面结构 / 视觉规范 / **可断言布局** | `darkest/scripts/ui/**`、`darkest/scenes/**`、`darkest/resources/**` | 改内核（`scripts/core\|data\|gameplay/sim`）、`darkest/data/*.json`、`doc/architecture/**` |
| **主程序** | 内核（零 Godot）+ 数据 + 玩法 + **自测取证** | `darkest/scripts/{core,data,gameplay/sim}`、`darkest/data/*.json` | 改 UI 文件**当 UI 设计师正在改同文件时** |

🔴 **同文件并行编辑 = 明令禁止**（一步一提交，便于二分）；接口变化（如某面板类从 `CanvasLayer` 改 `PanelContainer`）**必须写进对方窗口**。

### 9.2 收件箱口径（⚠️ 一处分歧，待用户一句）
```
· 通用协议（主程序执行中）：**读自己的窗口 → 确认已处理 → 清空自己 → 干活 → 写对方窗口（追加不覆盖）**
· 本 skill 原写：读**其他角色**窗口 → 转写 → **读完直接清空那些窗口**
⇒ 两者不一致 ⇒ 主程序按通用协议执行，且**不会去清任何人的窗口**（只追加）⇒ 待用户定口径 ✓
```

### 9.3 战斗屏最新交接（比 §1.6 的注释更新）
```
① 真根因已修：`MainMenuRoot.AddMenuButton` 同步 `ChangeSceneToFile` ⇒ 冒烟在 `_Ready` 按菜单键
   ⇒ 引擎报 `Parent node is busy adding/removing children` ⇒ **场景树不一致 ⇒ 判据乱认浮层 ⇒ 读数不可信**
   已修：菜单键 + `ExpeditionRoot.SwitchTo()` + `BattleRoot` 两处 ⇒ 全部 deferred ⇒ 引擎错误已消失
② ⚠️ 修好后 `--ui-audit` **一行都不输出**（钩子不稳）：
   已试：定时器改直接调用、调用移到 `_Ready` 第一句 —— 仍未输出
   未证判断：切场景 deferred ⇒ 主菜单节点先被释放 ⇒ 挂其上的延后调用**永不执行**；或冒烟步骤提前 `return`
   ⇒ 🔴 **第一件事是恢复取证**：**取不到数就不算通过**（红线 25）
```

### 9.4 主程序向 UI 侧要的四件「可测参数」（给了就能实现）
```
① 战斗屏分区规范（顶/中/底各放什么、单位卡怎么排、技能栏与 E 区左右、谁 ExpandFill）
② 字体选型（OFL 具体一个 + **CJK 回退**——我们 UI 是中文，缺 fallback 会掉字）
③ 动效参数（时长之外：位移像素/抖动幅度/闪白颜色/暗角强度）⇒ 我会做成常量并写"输入不被吞"的用例
④ 音效触发点（命中/受击·死门/结算 各对应哪个事件）+ 占位音类型
⑤ Theme 命名口径：`§14.4 darkest.tres` vs 实际 `resources/theme/dd_theme.tres` ⇒ 定一个，别留两个
```

### 9.5 判据自身口径（主程序今天连踩 3 次，写在这里免再踩）
```
· `PanelContainer` 继承 `Container`（不是 `Panel`）⇒ 漏检 = **假通过**
· 满屏不透明模态浮层 ⇒ 判据必须**只审浮层内部**（否则被遮 Label 算 16 对假重叠）
· 空文本/过早判定 ⇒ "可见 Label 0 / 通过" = **假通过** ⇒ 定时审计跑满 N 次、以最后一次为准，回调不得捕获会被释放的节点
```
