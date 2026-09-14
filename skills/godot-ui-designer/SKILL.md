---
name: godot-ui-designer
description: 面向 Godot（Godot 4 为主，兼容 Godot 3）的专业 UI 设计 skill。当任务涉及用 Control 节点设计/搭建/主题化/排查 Godot 用户界面时触发——包括 HUD、主菜单、暂停菜单、设置面板、背包、对话框、血条、加载界面、响应式布局、Theme 主题系统、字体与可访问性（键盘/手柄导航、文本缩放、对比度）、以及 UI 与游戏状态绑定的 GDScript 代码。也适用于 Godot UI 性能优化、多分辨率适配、本地化（CJK/RTL）相关问题。
agent_created: true
---

# Godot UI Designer

## Overview

本 skill 把 Godot 的 UI 子系统（基于 `Control` 节点的即时模式/保留模式混合体系）的方法论、约定与可复用模板打包给deepseekharness。目标是产出**专业级、可维护、跨分辨率、可访问**的 Godot UI，而不是"把按钮摆上去能用就行"。

核心认知：Godot 的 UI 不是靠绝对坐标摆放，而是靠 **Anchor（锚点）+ Container（容器）+ Size Flags（尺寸标志）+ Theme（主题）** 四件套协同工作。任何跳过容器的"手摆像素"方案，都会在分辨率变化时崩坏。

## When to use

- 用户要求在 Godot 中新建/重做任意 UI（菜单、HUD、弹窗、背包、设置、对话框等）。
- 用户遇到 Godot UI 问题：布局错位、缩放抖动、字体模糊、控件不随屏幕变化、主题不生效、焦点乱跳。
- 用户要求做响应式/多分辨率适配、可访问性、本地化、UI 性能优化。
- 用户要求写 UI 与游戏逻辑解耦的 GDScript（事件/信号驱动、UI 管理器基类）。
- 用户要搭一套统一的 Godot 视觉主题（Theme 资源、StyleBox、字体方案）。

## 本项目（Darkest）：角色 · 收件箱协议 · 当前战线

**身份**：本工作区的第四个角色「UI 设计师」（与 策划 / 架构 / 主程序 并列）。我只动 `darkest/scripts/ui/**`、`darkest/scenes/**`、`darkest/resources/**`；**不改**内核（`scripts/core|data|gameplay/sim`，零 Godot）、`darkest/data/*.json`、`doc/architecture/**`（发现别层问题 ⇒ 登记 + 交接，不代笔）。

**🔴 收件箱协议（用户指定，强制）**
```
① 读 `doc/windows/` 下【其他角色的窗口】（`主程序窗口.txt` / `策划窗口.txt` / `架构窗口.txt`）
② 转写进本 skill —— 🔴 主落点是 `references/darkest_ui_brief.md`（含"投递台账"+ 行动摘要）
③ 🔴 读完【直接清空】那些窗口（清前必须已转写；全文可由 git 恢复 `git show HEAD:doc/windows/<文件>`）
④ 再干 UI 的活；需要回信时【追加不覆盖】写对方窗口（先读 → 拼接 → 整体写回）
🔴 送达判据 = 【回读我的投递标记】，不得用行数/字节数（README 红线 20）
```

**项目红线（UI 侧必守）**：零数值改动（`#307` 冻结中）· 一次一个轴（`#244`）· 不留不可解释的状态（红线 21：禁用必须说明原因）· **"看起来对了"不算验收，"能断言"才算**（红线 25）· 表现层读数据只走 `FileAccess`/`ResourceLoader`（硬边界 B7 / 红线 26）· **不得使用 DD 的任何素材**（红线 27）· **布局必须容器 + 锚点**（i18n 前提）。

**当前战线（`#319` 布局基建，最高优先）**：两条自动判据（可见 `Label` 两两不相交 ／ `Panel` 的 `BgColor.a == 1.0`）逐屏挂测 —— 城池 ✅ / 角色详情 ✅ / 地图 ✅ / **战斗 🔴 未通过**（基线 12 重叠 / 7 透明框，且审计钩子在 `81329e6` 修复后不再输出 ⇒ **先恢复取证**）。之后：字体（`§13.4①`）→ 动效（`§12.1` 4 个）→ 音效（`§12.2` 3 类 + 占位音）→ 材质描边（`§12.3`）→ 帧预算（⑨）。细节、读数、坑清单、取证命令见 `references/darkest_ui_brief.md`。

## 核心原则（非显而易见的关键点）

1. **Control 优先，Node2D 不用于 UI。** 所有 UI 节点必须继承 `Control`。纯世界物体才用 Node2D/Sprite。
2. **优先用 Container，不要手摆坐标。** 除根节点外，绝大多数 UI 子节点应放在 `HBoxContainer / VBoxContainer / GridContainer / MarginContainer / CenterContainer` 内。Container 自动处理位置与尺寸，配合 Size Flags 实现弹性布局。只有极少数装饰性节点（纯背景图）才手动定锚点。
3. **Anchors 用于"根/区域"定位，Containers 用于"内部排列"。** 根 UI 面板用 Anchor Preset（如 Full Rect、Center）锚定到父级；面板内部内容用 Container 排列。两者职责不同，不要混用替代。
4. **Theme 必须集中管理，禁止分散硬编码颜色/字体。** 建立**一个项目级 `Theme` 资源**（见 `assets/game_theme.tres`），通过 `ThemeType`（Button、Label、Panel 等）统一 StyleBox/Font/Color/Constant。局部差异用节点的 `Theme Overrides`（Inspector 中的 Theme Override 段），而非改全局。
5. **在参考分辨率下设计，在多分辨率下验证。** 固定一个参考分辨率（如 1920×1080 或按目标平台选 1280×720），但最终必须在 Godot 视图顶栏切换多种 Aspect 验证。详见下方"响应式工作流"。
6. **UI 与游戏状态解耦：信号/事件驱动，禁止轮询。** UI 读取游戏状态应通过信号（`signal`）或事件总线，而不是每帧 `get_node(...).value` 轮询。提供 `assets/ui_manager.gd` 作为基类模板。
7. **可访问性是默认项，不是补丁。** 设计阶段就规划焦点顺序（Focus / Neighbor）、键盘与手柄导航、文本缩放、对比度与色盲友好配色、屏幕阅读器友好（给控件设 `tooltip`/`name`、隐藏装饰节点）。

## 推荐工作流

### 1. 配置项目级视口与拉伸（最先做，影响全局）
在 `Project Settings > Display > Window` 设置：
- `stretch_mode` = `canvas_items`（推荐，UI 与 2D 世界一致缩放）或 `viewport`。
- `stretch_aspect` = `expand`（最灵活，配合容器自适应）或 `keep`（保持比例留黑边）。
- `stretch_scale` 可由代码随 DPI/设置调整（用于文本缩放功能）。
- 在 `Project Settings > Gui > Theme` 设置 `default_theme` 指向项目 Theme 资源。

### 2. 建立项目 Theme 资源
- 用 `assets/game_theme.tres` 作为起点，定义 `default_font`（动态字体 TTF/OTF，含 CJK 回退）、主色板（Color 常量）、常用 StyleBoxFlat（按钮 normal/hover/pressed、面板、输入框）。
- 字体务必用**动态字体（`.ttf`/`.otf` + `.ttf`/`.otf` fallback）**，避免位图字体在大屏/缩放下模糊；CJK 需要单独字体资源做 fallback。
- 详细 Theme 属性清单见 `references/godot4_ui_cookbook.md` 的 Theme 章节。

### 3. 搭建 UI 场景（容器优先）
- 顶层结构：`CanvasLayer`（或 `Control` 根，Anchor = Full Rect）→ `MarginContainer`（留安全边距）→ 业务 Container 树。
- 菜单/列表用 `VBoxContainer`；工具栏/行用 `HBoxContainer`；网格用 `GridContainer`；需要滚动用 `ScrollContainer` 包住 `VBox`。
- 给需要伸缩的节点设 Size Flags：`horizontal_size_flags = EXPAND_FILL` 等，而非写死 `custom_minimum_size`（除非是图标等固定尺寸）。
- 可用 `assets/responsive_menu.tscn` 作为响应式主菜单骨架，删改即可。

### 4. 绑定游戏状态
- 游戏逻辑暴露 `signal`（如 `health_changed(new_value)`），UI 节点 `connect` 到更新函数。
- 复杂项目用事件总线单例（Autoload）解耦：UI 订阅事件，逻辑发事件。
- 用 `Tween` 做数值过渡（血条平滑、数字滚动），避免硬跳变。
- 详见 `assets/ui_manager.gd` 的基类模式（子 UI 注册到管理器，管理器统一连接信号）。

### 5. 验证响应式与可访问性
- 在编辑器视图切换 16:9 / 16:10 / 4:3 / 竖屏模拟，确认无溢出、无重叠、文本不截断。
- 用 `Project Settings > Gui > Common > Default Focus` 与节点 `focus_mode` 规划 Tab/手柄导航顺序；用 `focus_neighbor_*` 修正跳序。
- 开启文本缩放（临时调 `stretch_scale`）确认布局不破。
- 检查对比度（前景/背景 ≥ 4.5:1 给正文），避免仅靠颜色传达状态（加图标/形状）。

### 6. 性能与收尾
- 控制 Control 节点总数；过量节点（尤其每帧重建的列表）改用 `ItemList`/`Tree` 或对象池 + `visible` 切换。
- 多用 `StyleBoxFlat`（矢量，零贴图开销）与 `NinePatchRect`；少用量大且重叠的透明贴图。
- 给 `Label` 设 `autowrap_mode` 处理长文本/本地化；用 `clip_text` 防溢出。
- 本地化：所有用户可见文本走 `tr()` + CSV 翻译文件；CJK 与 RTL 单独验证。

## 资源索引

- `references/godot4_ui_cookbook.md` — 详细参考：Control 节点族、Anchors/Containers/Size Flags 速查、Theme 完整属性、响应式设置清单、常见 UI 模式结构（HUD/菜单/背包/对话框）、可访问性清单、性能要点、Godot 3 vs 4 差异。
- `references/darkest_ui_brief.md` — 🔴 **本项目（Darkest）UI 设计师简报**：收件箱投递台账（每次清空窗口前必写）· `#319` 布局基建（两根因/四硬规则/顶层结构/Theme/两条自动判据）· 逐屏进度与实测读数 · `ui_spec §12/§13/§14` 摘要 · 代码落点与取证命令 · 8 条已知坑 · 下一步清单。
- `assets/ui_manager.gd` — GDScript UI 管理器基类模板：子 UI 注册、信号自动连接、Tween 过渡、安全获取节点。
- `assets/responsive_menu.tscn` — 响应式主菜单场景骨架（MarginContainer + VBox + 按钮），可直接复制改造。
- `assets/game_theme.tres` — 起步用项目 Theme 资源（字体占位、StyleBoxFlat 预设、配色常量）。

## 常见陷阱清单（交付前自查）

- [ ] 是否存在"手摆绝对坐标、无容器"的面板？→ 改用 Container。
- [ ] 颜色/字体是否散落在各节点而非统一 Theme？→ 收敛到 Theme + Override。
- [ ] 是否用位图字体导致缩放模糊？→ 换动态字体 + CJK fallback。
- [ ] 是否每帧轮询游戏状态更新 UI？→ 改为信号/事件驱动。
- [ ] 是否在单一分辨率下设计、未测其他 Aspect？→ 多分辨率验证。
- [ ] 键盘/手柄焦点顺序是否乱跳？→ 设 `focus_mode` 与 `focus_neighbor_*`。
- [ ] 是否仅靠颜色区分状态（如红绿血条）？→ 加图标/形状，保证色盲可辨。
- [ ] 长文本/本地化是否溢出或截断？→ `autowrap_mode` / `clip_text` / 容器最小尺寸。
- [ ] Control 节点是否过多导致卡顿？→ 合并、用原生列表控件或对象池。
- [ ] 🔴 面板/容器类是否误用 `CanvasLayer` / `Node2D`？→ 它们**不是 `Control`**：Godot 的 `Container` 只排 `Control` 子节点 ⇒ 既没有"框"、也不受 `Theme` 管、**根本进不了容器树**（本项目地图屏的真实根因：只挪坐标会掩盖它）。→ 改成 `PanelContainer` / `Control` 并组进容器树。
- [ ] `Theme` 是否挂在了**容器树的根 `Control`** 上？→ `Theme` 只沿 `Control`/`Window` 祖先链继承；子树漏挂 ⇒ 拿引擎默认样式（实测 Panel 默认 `BgColor.a = 0.6` ⇒ 判据 2 报"框透明"）。
- [ ] 代码里**动态新建**的控件是否也加进了同一容器？→ 否则逃出子树（审计看不到、且仍互相压）。
- [ ] 审计/判据**自身的口径**是否可信？→ `PanelContainer` 继承自 `Container` 而非 `Panel`（漏检 = 假通过）；满屏不透明模态浮层必须只审浮层内部（否则被遮住的 Label 算成假重叠）。**"通过了"之前先问"它到底检查了什么"**。
