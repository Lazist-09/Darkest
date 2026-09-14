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

**角色已转正为【四角】**：`doc/windows/{策划,架构,主程序,UI设计师}窗口.txt`；**我（UI 设计师）是本项目 UI 部分的责任人**
（用户指令 2026-09-14：「现在UI部分你负责了，告诉主程序」）。技能已迁入**文件夹版**：
```
skills/darkest-architect/        ← 架构（+ references/architect_redlines.md）
skills/darkest-game-designer/    ← 策划（+ references/designer_spec_playbook.md）
skills/darkest-lead-programmer/  ← 主程序（+ references/lead_programmer_redlines.md）
skills/godot-ui-designer/        ← 🆕 第四角 = 我（+ references/darkest_ui_brief.md ← 本文件）
🔴 旧扁平路径（skills/架构师.md · 主程序.md · 策划师.md）已废弃（git 里显示为 D）⇒ 引用一律改指文件夹版
```

**投递模板（写对方窗口照此；来源 `architect_redlines §10`）**
```markdown
### 轮次：<日期> · <一句话主题>
- **我做了什么**：<≤3 行，镜像了什么 / 转了哪些 O-nn>
- 🔴 **需要你做什么**：<逐条、可执行>
- **阻塞 / 待裁定**：<有就写；无写「无」>
- **权威在哪**：<doc/ 具体文件 + 章节>
```
🔴 **写窗口的编码纪律（红线 20）**：统一 UTF-8 + **写完自读**；禁用依赖默认编码的读写（`Get-Content -Raw` / `Add-Content` / `>>` —— 追加一次毁两次）；**追加前必须先 `read` 全文**（防"用 `write` 整体替换"毁掉他人留言）。

**🔴 收件箱协议 —— 已按 `#321`（策划裁定）修正：各清各的，绝不代清**
```
① 读【自己的】窗口（`doc/windows/UI设计师窗口.txt`）= 我唯一的收件箱
② 读完【清空自己】（清前必须已转写进本 skill）
③ 干活 ④ 写【对方】窗口时【追加不覆盖】（先 `read` 全文 → 拼接 → 整体 `write` 写回）
🔴🔴 **不得"读别人的窗口 → 读完清空别人的窗口"**（`#321` 明令）—— 理由：
   你无法确认对方是否已处理；而"清空"是本项目【唯一会删东西的操作】⇒ 代清可能删掉对方**还没读**的消息
   ⇒ 信息丢失（本会话已发生 4 次）⚠️
✅ 他方窗口（主程序/策划/架构）**只读**作上下文，**只追加、绝不代清**
🔴 写入失败 = 未送达 ⇒ 当轮补投；返回 `file changed since it was read` 立刻重读补写
🔴 送达判据 = 【回读我的投递标记】（`Select-String <我的标题>`）—— 不得用【行数/字节数】（红线 20：
   实测 `Get-Content(...).Count` 报 448、实际 689，CRLF/LF 混用 ⇒ 分行口径不同）
```
> ⚠️ **历史**：用户首轮曾要求"读其他角色窗口 → 读完直接清空那些窗口"，我照做了一次（清空 `主程序窗口.txt` / `策划窗口.txt`，
> 原文可由 git 恢复）。`#321` 裁定该口径**会造成信息丢失、必须废止** ⇒ 本 skill 采用 **各清各的**；用户若要覆盖此裁定，需显式说明。

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

### 1.6 逐屏进度（🔴 **实测真读数**；最新提交 `e01dca1`）
| 界面 / 状态 | 入口 | 可见 Label | Panel+PC | 重叠对 | 透明框 | 状态 |
|---|---|---|---|---|---|---|
| **城池 Hamlet**（`HamletRoot`） | `--hamlet` | 11 | 5 | **0** | 0 | ✅ |
| **角色详情**（`HeroDetailPanel` 模态） | `--hamlet --hamlet-row=0` | 4（只审模态） | 0 | **0** | 0 | ✅（范围外 11 Label 确被不透明模态遮住） |
| **地图 Expedition** | `--topology` | **9** | 5 | **0** | **0** | ✅（`e01dca1`） |
| 地图 · **Curio 面板打开** | `--topology --click-map=1` | 1（只审 `CurioPanel` 模态） | 0 | **0** | 0 | ✅ |
| 地图 · **扎营技能面板打开** | `--topology --click-map=1 --camp` | 1（只审 `CampSkillPanel` 模态） | 0 | **0** | 0 | ✅ |
| **战斗 Battle**（`BattleUi`） | `--click-menu=0` | **59** | 35 | **391** | **22** | 🔴 未通过（最后一个，最大） |

> ⚠️ **历史上的"✅"有两类假绿**（都栽在判据口径上）：① 纯容器被当模态 ⇒ 审计范围被缩小（坑 ⑩）；
> ② `LightBarPanel`/`PathChoicePanel`/`InventoryPanel` **脚本根本没挂上**（坑 ⑪）⇒ 面板没内容 ⇒ "无重叠"。
> 🔴 **教训**：任何"✅"都要连【审计范围 + 全场景计数 + 范围外控件数 + 可见 Label 数是否合理】一起看。

**战斗屏真读数为什么这么差**：`BattleUi` 的控件**仍在"创建时加到 CanvasLayer"**（49 个 Label 级的孤儿 + 22 个引擎默认
`a=0.6` 的 Panel），新的 `_uiRoot + TopRow/MidRow/BottomRow` 只是**骨架** ⇒ 孤儿与容器树并存 ⇒ 391 对重叠。
⇒ 要做的是【把控件改成创建时进容器】（`#321`③ 的分区表），不是挪坐标。

### 1.7 已知坑（13 条，全部实机踩过）
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
⑨ 🔴 **`_Ready` 内不能直接 `Root.AddChild(node)`**（`Root` 正在 add_child 本场景 ⇒ busy）——
   引擎报 `Parent node is busy setting up children, add_child() failed` ⇒ **节点根本没进树**（还泄漏）
   ⇒ `--ui-audit` 一行都不输出就是这么来的（真因已取证，`7067c77`）⇒ 修法 = `Root.CallDeferred(AddChild, node)`
⑩ 🔴 **"模态覆盖层"判定只能认【能画底的 `Panel`/`PanelContainer` 类】**：
   纯布局容器（`MarginContainer`/`VBox`…）**遮不住任何东西**；实测它竟能解析出 `panel` 样式 `a=1`
   ⇒ 若用它做覆盖层判定，审计范围会被**悄悄缩小** ⇒ 报 ✅ 却是假通过（地图屏/战斗屏各中一次）
   ⇒ 并**强制打印**：覆盖层是谁+什么类+样式来源 ／ 全场景计数 ／ 范围外控件数 ✓
⑪ 🔴🔴 **`.tscn` 的 `type=` 必须与脚本基类一致**（`e01dca1`）：
   `Expedition.tscn` 把 `LightBarPanel`/`PathChoicePanel`/`InventoryPanel` 声明为 `PanelContainer`，
   而这三个类的 C# 仍是 `CanvasLayer` ⇒ 引擎报
   `Script inherits from native type 'CanvasLayer', so it can't be assigned to an object of type: 'PanelContainer'`
   ⇒ **脚本根本没挂上 ⇒ 光照条 / 选路 / 背包格子三块 UI 全死**（红线 18：玩家碰不到选路）⚠️
   ⇒ 修类后该屏可见 Label **4 → 9**（内容回来了）且判据仍 0/0 ✅
   📌 **判据查不出这类缺陷**（它只测重叠/透明）—— 必须靠【引擎错误清零】+【可见 Label 数是否合理】发现
⑫ **可见性要用 `IsVisibleInTree()`（有效可见性）**：面板 `Hide()` 后其内部 Label 的 `Visible` 仍是 `true`
   ⇒ 被算成"重叠"（**假红**，实测 8 对）—— 与假绿一样坏
⑬ 🔴 **父节点是 `Node2D` 时，Control 的 `FullRect` 锚点算不出尺寸**（`get_parent_anchorable_rect()` 为空），
   而"锚点不动、只设 `Size`"会被引擎覆盖（警告 *non-equal opposite anchors ⇒ size overridden after _ready()*）
   ⇒ 满屏模态会退化成 min size（内容挤到左上角、压住别的控件）⇒ 正解：先造一个【锚点相等 + 显式 `Size`=视口】
   的满屏 `Control` 宿主（`ModalHost`），模态再挂它下面 ✓（`CanvasLayer` 做根反而绕开了这坑：它不是 CanvasItem）
⑭ **重叠必须带矩形坐标**（`pos=/size=`）才能定位：本轮正是靠它才看清"真凶是 `size=(1232,620)` 的巨型 Label"
   与"模态其实没满屏（内容挤在 (22,22)）"⇒ 只报"谁压谁"分不清【放错位置】还是【容器被挤爆】
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
LayoutAudit.cs        两条判据 + 覆盖层口径 + **口径自证行**（覆盖层是谁/类/样式来源 · 全场景计数 · 范围外控件数）
                      Check(Node) ⇒ (bool, string)
UiAuditHook.cs        🆕 `--ui-audit` 的**跨场景取证钩子**（`7067c77`）：挂 `SceneTree.Root` + **`CallDeferred` 入树**
                      每屏跑满 5 次、以最后一次为准；回调不捕获会被释放的节点
MainMenuRoot.cs       `_Ready` 第一句 `UiAuditHook.InstallIfRequested(this)`；另有 `--theme-audit` / `--input-audit`
HamletRoot.cs         城池 + 角色详情（覆盖面板）
ExpeditionRoot.cs     远征/地图侧组合根：**MapRow**（地图视图进容器树）+ `MakeButton`（不再自己 AddChild）
                      + `MakeModal`/`ModalHost`（满屏不透明模态：Curio 面板 / 扎营技能面板）+ `SwitchTo`（deferred 切场景）
BattleUi.cs           : CanvasLayer（⚠️ 不是 Control）⇒ 已建满屏 _uiRoot + TopRow/MidRow/BottomRow（**骨架**，待填控件）
地图侧面板类（**已全部改成 PanelContainer**，`e01dca1`）：LightBarPanel · ScoutMarkPanel · PathChoicePanel ·
                      InventoryPanel · ExpeditionListPanel（⚠️ 前四个中三个原本是 CanvasLayer 且与 .tscn 的 type 不一致 ⇒ 曾全死）
BattleMiniMap.cs      战斗右下角地图
```
**资源 / 场景**：`darkest/resources/theme/dd_theme.tres`（323 B，⚠️ 与 §14.4 的 `darkest.tres` 命名不一致）·
`darkest/scenes/{battle/Battle,expedition/Expedition,hamlet/Hamlet,main/MainMenu}.tscn`

**取证命令（主程序实测口径，2026-09-14 交接信；以此为准）**
```powershell
# 构建 + 单测（当前 470 项全绿；net10.0 target）
dotnet build darkest\Darkest.sln -p:DarkestTargetFramework=net10.0 --no-restore -m:1 -nodeReuse:false -tl:off -v:q
dotnet test  darkest\Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --no-build -nodeReuse:false -tl:off -v:n
# 🔴 布局判据（每屏都要跑；判据 1 = 可见 Label 两两不相交；判据 2 = Panel/PanelContainer 的 a == 1.0）
& 'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe' --headless --path darkest --hamlet --ui-audit --quit-after 400 --audio-driver Dummy
& 'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe' --headless --path darkest --topology --ui-audit --quit-after 400 --audio-driver Dummy
# 焦点/手柄取证（审计清单③）：进场给焦点 + 单位卡可聚焦 + ui_accept 等价点击
& 'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe' --headless --path darkest --focus-audit --smoke=main:1,map:0,curio:bare,map:0 --quit-after 4000 --audio-driver Dummy
# 内核纯净门禁（两条规则 + 负向自检）
python tools\check_godot_refs.py --root darkest ; python tools\check_godot_refs.py --root darkest --selfcheck
# 其他入口/冒烟（全走【真实 Pressed】，红线 26）
--hamlet-row=0 · --hamlet-detail-back · --hamlet-hover=<building> · --hamlet-embark · --e2e · --expedition
--smoke=main:1,map:0,camp,finish,auto,map:0   （步骤器：每进场景消费一步 + 0.2s tick；未知步骤 ⇒ exit 2）
```

🔴 **我实测可用的跑法（2026-09-14，`7067c77`）** —— ⚠️ 三个坑，别重踩：
```powershell
$exe = 'E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe'   # ① 必须 console 版
$tmp = 'F:\GithubPro\Darkest\.tmp'; $env:APPDATA = $tmp                                  # ② 否则 user:// 写不出（沙箱/权限）
# ③ --fixed-fps 60：headless 帧率不受限 ⇒ 只给 --quit-after 会"还没到 0.4s 就退出" ⇒ 判据一行都不出
& $exe --headless --path darkest --click-menu=0 --ui-audit --quit-after 600 --fixed-fps 60 --audio-driver Dummy --log-file "$tmp\ui_audit_battle.log"
```
🔴 **读日志一律用【文件工具】（`read`）**：`Get-Content` 在 Windows PowerShell 默认 GBK 解码 UTF-8 ⇒
**内存里就已经是乱码**（红线 20 根因版：读坏 → 写坏，双程毁码）—— 我本会话已亲自踩过一次 ⚠️

---

## 6. 下一步（UI 侧执行顺序 · 参数已全部由 `#321` 定死）

```
✅ 已完成：`7067c77` 取证恢复 + 判据口径修正 ｜ `e01dca1` **地图屏真绿**（含 3 个死面板复活 + Curio/扎营模态 + 口径 4~6 条）
✅ 四屏状态：城池 ✅ ／ 角色详情 ✅ ／ 地图 ✅（含两个模态）／ **战斗 🔴 59 Label · 391 重叠 · 22 透明**
① 🔴 战斗屏（最后一个，也最大）：按 `#321`③ 分区表把控件**改成【创建时进容器】**
   （A 顶栏 / B 战场 / C 左下角色面板固定宽 / E 右下多功能框 ExpandFill / 底栏；单位卡均分不 Expand；
    技能栏在 C 区内）—— 现有 `_uiRoot + TopRow/MidRow/BottomRow` 只是骨架，49 个 Label 级孤儿仍挂在 CanvasLayer 上
   手法照地图屏：① 类改 Control 系（若是 CanvasLayer/Node2D）② 控件创建时进容器 ③ Theme 挂容器根
            ④ 需要浮层就走 `MakeModal`（满屏模态）⑤ 每步跑 `--ui-audit` 到全绿
② 字体（§13.4①）：🔴 `Noto Serif SC`（含 CJK，OFL）+ fallback 链 `EB Garamond` → `Noto Serif SC`，落 `resources/theme/`
③ 配色（§14.4）：深底 + 强对比 + 金/红点缀，全部走 Theme（命名统一 `dd_theme.tres`）
④ 动效（§12.1 四个 + `#321`⑤ 常量，输入不得被吞） → 音效（§12.2 三类 + 占位音，触发点见 `#321`⑥）
⑤ ShaderMaterial 描边/暗角/闪白（视觉规范仍属 `ui_spec §1.4`） → ⑨ 帧预算基线 → ⑩ i18n（只做"布局先对"）
🔴 每改完一屏：跑 `--ui-audit` ⇒ 按架构 §⑥ 把读数（界面名／Label／重叠／透明／提交号）**追加到 `架构窗口.txt`**
🔴 另外建议（已投给架构）：**加一条静态检查** —— 扫 `.tscn` 的 `type=` 与脚本基类是否一致（坑 ⑪ 那类缺陷判据查不出来）
🔴 纪律：每轮一个轴（`#244`）· 零数值改动（`#307`）· 不改内核/数据（`#321`）· 一步一提交 · 读日志用文件工具（UTF-8）
```

---

## 7. 待他方口径 / 已知未决
```
· 线性模式战斗地图：卡写"隐藏"，主程序做成【说明"无地图（不在拓扑远征里）"】⇒ ⚠️ 仍待策划一句（红线 21：不留不可解释的空白）
· `ui_spec §12` 是否"已存在"：已由 `#318` + `#321` 结清 ⇒ UI 侧执行口径：【按 §12 照做，不必等】✅
· 解锁态 UI（已实现）：未解锁建筑「🔒 名称（第 N 趟后解锁）」；名册「8 / 8（上限 12）」
  ⇒ C1/C2/C3：`roster.cap` 硬上限 12、unlocks 控【当前可用上限】；消费点三个（建筑可见性 / Curio 池 4→6 / 名册上限）
· resources/theme 命名：✅ 已结清 —— 用 `dd_theme.tres`（代码已落地，改代码无收益）⇒ 规格侧由策划改 ✅
· 五件参数（分区/字体/动效/音效/命名）：✅ 全部由策划 `#321` 答完（见 §10 信 2）⇒ 无待裁
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

---

## 10. 我的收件箱台账（`doc/windows/UI设计师窗口.txt`，2026-09-14 两封信；转写后已清空自己）

> 🔴 **协议已定稿（用户 + `#321`）**：**写窗口一律【添加式写】（append-only）**；**只能清空自己的窗口**。

### 信 1 ·【来自主程序】欢迎 + 四屏现状交接 + 五件「可测参数」（送达标记 `四屏现状交接`）
```
· 分工：我 = 界面结构/视觉规范/可断言布局（scripts/ui/**, scenes/**, resources/**）；
        主程序 = 内核（零 Godot）+ 数据 + 玩法 + 自测取证。🔴 同文件并行编辑 = 明令禁止（一步一提交便于二分）
        冲突权威：ui_spec（策划）> doc/architecture/**（架构）> 本信/简报（可能滞后）
· 四屏读数（实测）：城池 重叠 2→0 / Panel 0→5（9941c70）· 角色详情 0/0（8f3a9df）
        · 地图 重叠 9→0 / 透明 3→0（b8d98b1）· 战斗 基线 49 Label / 30 Panel / 12 重叠 / 7 透明 🔴
· 地图屏根因（照它做少走两轮）：面板类原是 CanvasLayer / Node2D ⇒ 都不是 Control ⇒ 容器不排它
        ⇒ 正确顺序 ① 类改 PanelContainer（Control 系）并同步改 .tscn 的 type= ② 组进容器树 ③ Theme 挂【容器树的根】
· 战斗屏卡点：真根因已修 = MainMenuRoot.AddMenuButton 用【同步】ChangeSceneToFile ⇒ 冒烟在 _Ready 按菜单键
        ⇒ 引擎报 Parent node is busy adding/removing children ⇒ 场景树不一致 ⇒ 判据乱认浮层 ⇒ 读数不可信
        （已改 deferred：菜单键 + ExpeditionRoot.SwitchTo() + BattleRoot 两处 ⇒ 引擎错误消失）
        ⚠️ 但随之 --ui-audit 【一行都不输出】：已试定时器改直接调用 / 调用移到 _Ready 第一句 —— 仍未输出
        未证判断：切场景 deferred 后主菜单节点先被释放 ⇒ 挂其上的延后调用永不执行；或冒烟步骤在 _Ready 提前 return
        ⇒ 🔴 第一件事 = 恢复取证（取不到数不算通过，红线 25）
· 五件参数请求：① 战斗屏分区 ② 字体选型 + CJK 回退 ③ 动效数值常量 ④ 音效触发点 ⑤ Theme 命名（答案 = 信 2）
· 取证命令 + 三条"判据自身口径"（已并入本简报 §5 / §1.7）
```

### 信 2 ·【来自策划】答五件参数 + 裁定收件箱口径 + 四角分工（`#321`，送达标记 `答五件参数`）
```
① 收件箱口径裁定：按通用协议【各清各的】；❌ 不得"读别人的窗口 → 读完清空那些窗口"
   （理由：无法确认对方是否已处理；清空是本项目唯一会删东西的操作；本会话已发生 4 次信息丢失）
   ⇒ 用户 2026-09-14 再次复述定稿：「写都是添加式写，只能清空自己的窗口」
② 四角分工：策划 doc/** ｜ 架构 doc/architecture/** ｜ 主程序 scripts/{core,data,gameplay/sim} + data/*.json（不碰 UI 文件）
   ｜ 我 darkest/scripts/ui/** + scenes/** + resources/**（不碰内核/数据）
   🔴 交界处 scripts/gameplay/scene/**（BattleRoot / ExpeditionRoot / HamletRoot）**归主程序**（流程持有者）
      ⇒ 我只改其中【呈现】部分，改前在窗口说一句
③ 战斗屏分区规范（`ui_spec §1` 早已定 + `#319` 容器结构）：
     A 顶栏：回合 · 支援点 ●●●○ │ 行动序列 │ 撤退
     B 战场：[辅6][辅5][战4][战3][战2][战1] ←→ [敌1][敌2][敌3][敌4]
     C 左下：当前轮次角色面板（常驻）      E 右下：多功能框（详情/日志/序列/编成，可切页）
     Root → MarginContainer → VBox（顶栏 / 主体 / 底栏）；顶栏 Panel+HBox（左状态·右按钮）
       主体 HBox：左 Panel = C 区（固定宽 ~30%，不 ExpandFill，🔴 技能栏在 C 区内）
                 右 Panel = E 区（🔴 唯一 ExpandFill，吃剩余宽度）
       底栏 Panel + HBox：§11.1 右下角地图（固定宽，右下 1/3）+ 顶部进度条
     🔴 单位卡：我方 6（战 1~4 + 辅 5~6）· 敌方 4 ⇒ 【均分、不 ExpandFill】（位置编号必须可见 ⇒ 均分才能稳定映射横坐标）
④ 字体（`§13.4①`）：🔴 用 `Noto Serif SC`（思源宋体 / Source Han Serif，**OFL**，**含 CJK** —— 决定性）
     IM Fell English / Cinzel / EB Garamond **都不含中文** ⇒ 会用出"换了字体但中文掉字"的假象
     fallback 链：`EB Garamond`（拉丁/数字）→ `Noto Serif SC`（CJK）；已写进 `ui_spec §13.3`
⑤ 动效常量（`§12.1`）：① 出现 = 上浮 8px + 淡出 0.30s ease-out ② 受击 = 抖动 ±4px · 0.15s · 2 次往返 · 闪白 #FFFFFF@60%
     ③ 士气崩溃 = 暗角 40%（四角径向）· 单位框 #C0202A · 0.50s ④ 结算 = 淡入 0.20s
     🔴 两条可测约束：动效期间【输入不得被吞】· 动效不得延迟【可操作时刻】
⑥ 音效触发点（用事件名，`§12.2`）：命中 = `DamageEvent`（我方/敌方两种音）· 受击·死门 = `DamageEvent`(我方被击) /
     `DeathDoorEvent` / 阵亡 · 结算 = 战斗结束（`PlayerVictory` / `EnemyVictory` / `DrawRetreat`）
     占位音：命中 = 方波短促（我方高音/敌方低音）· 受击 = 噪声 · 结算 = 下行正弦；🔴 音源缺失必须能跑
⑦ Theme 命名：用【已实现的】`resources/theme/dd_theme.tres`（改规格、不改代码）⇒ 策划把 `ui_spec §14.4` 的 `darkest.tres` 改掉
⑧ 策划把我三条"判据自身口径"写进 `ui_spec §14.5`
```

### 信 3 ·【来自架构】欢迎 + 四角落位 + 架构侧权威指针（送达标记 `四角落位 + 你需要的架构侧权威指针`）
```
① ✅ 协议已由架构改正在我的 SKILL.md（他做了，避免策划与我两处各改一遍）：读自己的窗口 → 转写 → 清空自己 → 追加对方
② 四角分工已写进【四份 skill】（含我的）；⚠️ 断链说明：策划 `#321` 提的 `skills/策划师/SKILL.md` §15 目录已不存在
   （重构后并入 `skills/darkest-game-designer/`）⇒ 权威 = 现存四份 skill 的表 + `#321`
③ 架构侧权威指针（别从窗口转述里找）：
   · 布局/顺序：`ui_spec §14 / §12 / §13`（策划）· `tasks/next_round.md §4 / §4.1`（验收判据，必须遍历四界面）
   · 顺序裁定：`godot_builtins_audit.md §4 / §4.1`（② Theme+容器+锚点 优先 → 焦点/手柄 → 动效 → 音效 → 材质/字体/图标）
   · 分层与跨层读取：`blueprint.md §9.16`（硬边界 B7）· 红线：`README` 25 / 26 / 27 · 台账：`open_issues`（`O-84` 已修 + 门禁）
④ 🔴 已定契约（可直接依赖）：
   · `darkest/resources/**` 是【架构给我定的落点】（`blueprint §9.16.5`）：theme→`resources/theme/` ·
     shaders→`resources/shaders/` · audio→`resources/audio/` · i18n→`resources/i18n/`；预制件→`darkest/scenes/**/prefabs/`（已建、空）
   · Theme 命名 = `resources/theme/dd_theme.tres` ✅（`#321`⑦）
   · 🔴 接口变化必须走窗口；已定接口形状：`ExpeditionFlow.StepTo(roomId)` · `PreviewOptions` = 当前位置的相邻未探索房间 ·
     `StepsDone` = 已走段数
   · 🔴 **规则数据里不得出现表现路径**（红线 27）：`data/*.json` 不写 `anim/fx/sfx` ⇒ 表现绑定走 **`id → 表现资源` 映射**
     （**映射归表现层**）；需要新映射表 ⇒ 在窗口提给架构，由他落进契约
   · i18n：文本可后补，但【布局方式】必须现在对（容器 + 锚点）
⑤ 两条架构提醒：🔴 "取不到数就不算通过"（红线 25，战斗屏现在卡在这，先恢复取证再谈判据）·
   判据自身会假通过（三条口径已挂进验收）
⑥ 🔴 **架构要我做的（长期）**：**改完任一屏 ⇒ 把【两条判据的读数】写回 `doc/windows/架构窗口.txt`**
   作为 `next_round §4.1` 的【跨四界面验收留档】；格式必须有：
   **界面名 ／ 可见 Label 数 ／ 重叠对数 ／ 透明 Panel 数 ／ 提交号**（现在只有 城池/角色详情/地图 ✅、战斗 🔴）
```

### 信 4 ·【来自主程序】收到接手信 ⇒ 停手 UI 确认 + 战斗屏交接现状（送达标记 `DELIVERY-LEAD-ACK-UI-TAKEOVER-20260914`）
```
① ✅ 他【没有未提交的 UI 改动】（`git status --porcelain -- darkest/scripts/ui darkest/scenes darkest/resources` = 空）
   ✅ 他【不在改 MainMenuRoot.cs】（最后的 `--ui-audit` 时序尝试已提交）⇒ 我可以直接接手，不会覆盖他的在飞工作
② ✅ 他确认：不再编辑 `scripts/ui/**`、`scenes/**`、`resources/**`；交界 `gameplay/scene/**` 流程归他，
   我要改其中【呈现】部分须先说一句；他继续内核/数据/内容层/单测/文档
③ 🔴 他给的完整现状（我据此复现并定位到真因）：
   · 已修三处引擎级问题：`MainMenuRoot.AddMenuButton` 同步 `ChangeSceneToFile`（报 busy）·
     `ExpeditionRoot.SwitchTo(path)` 统一 deferred（替换 4 处）· `BattleRoot` 两处改 deferred
   · 已试但没解决的：审计定时器从 `CallDeferred(nameof(PrintUiAudit))` 改直接调用；再把调用挪到 `_Ready` 第一句
   · 他两个**未证**假设：(a) 切场景 deferred 后主菜单节点先被释放 ⇒ 延后调用永不执行；
     (b) 冒烟步骤在 `_Ready` 提前 `return` ⇒ 整段被跳过
   · 🔴 他的建议（我采纳了）：别让审计依赖会被释放的场景根 ⇒ 挂 `GetTree().Root` + `CallDeferred` 入树
④ 他划走 UI 活：战斗屏排版 · 字体 · 动效 · 音效 · 描边 · ⑨ 帧预算 **全部归我**
⑤ 附：旧 skill 路径残留只在【架构 README】与【历史 CHANGELOG】（后者不改写）

📌 **我实测结论（与他的假设不同，已取证）**：真因是 **`_Ready` 内直接 `Root.AddChild(timer)` ⇒ Root busy ⇒ 定时器没进树**，
证据 = 引擎报错原文（`Parent node is busy setting up children, add_child() failed` + C# 栈指到 `MainMenuRoot.cs:63/75/157`）
+ 退出时 `ObjectDB instances leaked`（泄漏的就是这些没进树的定时器）⇒ 已修（`7067c77`）。
```

### 信 5 ·【来自主程序】🔴 接口变更通知（**我的文件需改一行**）：具名编成目录已建（送达标记 `DELIVERY-LEAD-ENCOUNTERS-INTERFACE-20260914`）
```
① 他新增（数据 + 内核，都是他的域）：`data/encounters.json`（encounter id ⇒ 敌方编成，**引用 units.json 原型 id**，两条占位样例）
   + `EncountersConfig.cs`（P28 校验：id 唯一 / enemy 非空 / 槽位不重复 / **unit 必须真实**）+ 3 条用例 ⇒ **473/473**
   🔴 **未接线**：出厂内容表 `encounter` 全 null ⇒ 战斗仍走 `formation.json` 模板（追加覆盖层，**不替换** = B5）
② 🔴 **要我定的一件事**：`ExpeditionRoot.cs`（我的域）里 `RoomContentsConfig.Parse(<room_contents.json>, _curiosCfg)`
   只传了 Curio 目录 ⇒ **任何非空 `encounter` 都会被判错**（P26 ④ 的 fail-fast；出厂无引用 ⇒ 无影响）
   接线写法 = 多读 `encounters.json` + `units.json` 的敌方原型集合，然后 `Parse(..., _curiosCfg, encIds)`（只多一个参数）
③ 若我接线 ⇒ 他做【战斗侧用哪套编成】（他的域）；若不接 ⇒ 他继续 `O-83`（`Retained.hp` 跨战斗缺口）

✅ **我的答复（已回投，标记 `DELIVERY-UI-ENCOUNTERS-ANSWER-20260914`）：暂不接线** ——
   · 与【架构 `O-88` 的建议同向】：现在接 = 出厂数据仍全 null ⇒ **"接了个空"**（红线 25：动作 ≠ 意义）
   · 一次一个轴（`#244`，我正在做 `#319` 布局）· 内容清单属策划（`O-85`/`O-88`）· 保持 null 才是诚实状态（红线 21）
   · 🔴 **接口不封锁**：策划给清单后，一行改完 + 一个提交 + 启动级门禁验证
```

---

## 11. 我方投递台账（outgoing · 追加式写）

| 日期 | 收件窗口 | 投递标记 | 主题 | 回读状态 |
|---|---|---|---|---|
| 2026-09-14 | `doc/windows/主程序窗口.txt` | `DELIVERY-UI-TAKEOVER-20260914` | UI 接手通知：请停止并行编辑 UI 文件 + 交接战斗屏取证 | ✅ 已投（主程序已回执并清空其窗口） |
| 2026-09-14 | `doc/windows/策划窗口.txt` | `DELIVERY-UI-RECEIPT-20260914` | 回执：五件参数已收到并落进 skill；UI 侧执行顺序；仍待裁 1 条 | ✅ 已投（回读命中） |
| 2026-09-14 | `doc/windows/架构窗口.txt` | `DELIVERY-UI-READINGS-20260914` | 🔴 **长期义务第 1 次**：四屏判据读数留档 + 两条"假通过"已打回 + `--ui-audit` 真因 | ⚠️ **首次回读未命中**（窗口被其主人清空）⇒ 已按"当轮补投"规则补投 |
| 2026-09-14 | `doc/windows/架构窗口.txt` | `DELIVERY-UI-READINGS-RETRY-20260914` | 同上（补投，`1365c9c`） | ✅ 已投（回读命中 L6；读数在 L18） |
| 2026-09-14 | `doc/windows/架构窗口.txt` | `DELIVERY-UI-READINGS-2-20260914` | 第 2 次：地图屏真绿 + 三个死面板取证 + 判据口径 4~6 + Node2D 锚点坑（`ddbbf7a`） | ✅ 已投（回读命中 L55） |
| 2026-09-14 | `doc/windows/主程序窗口.txt` | `DELIVERY-UI-ENCOUNTERS-ANSWER-20260914` | 答编成接线：**暂不接线**（与架构 `O-88` 同向）+ 四屏真读数 + 假通过更正 | ✅ 已投（回读命中） |


