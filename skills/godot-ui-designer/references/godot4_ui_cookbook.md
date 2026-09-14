# Godot 4 UI 详查手册（Cookbook）

> 配套 `godot-ui-designer` skill。本文是细节参考，日常工作流见 SKILL.md。
> 术语保留英文（Godot 节点名、属性名），解释用中文。

## 1. Control 节点族速查

### 根/布局类
- `Control`：所有 UI 基类，自带 anchor/margin/size_flags/theme。
- `CanvasLayer`：独立渲染层，不受相机影响，UI 标准宿主（z 层独立）。
- `Panel` / `PanelContainer`：背景面板；后者自动把唯一子节点铺满。
- `MarginContainer`：给子节点统一留边距（四个 margin），常用于 UI 最外层安全区。
- `CenterContainer`：把单一子节点居中（不拉伸，除非设 size flag）。

### 排列类（Container）
- `HBoxContainer` / `VBoxContainer`：水平/垂直堆叠，按子节点 `size_flags` 分配空间。
- `GridContainer`：网格，设 `columns`；列宽均分或按内容。
- `FlowContainer`：自动换行流式布局（Godot 4 新增，适合标签云/物品栏）。
- `ScrollContainer`：包住一个子节点提供滚动；子节点通常配 `VBox` + `size_flags = EXPAND_FILL`。设 `horizontal/vertical_scroll_mode`。
- `SplitContainer`：可拖拽分隔的左右/上下两栏。
- `BoxContainer` 的 `add_spacer(true/false)`：在 GDScript 中插入弹性间隔。

### 交互类
- `Button` / `TextureButton` / `LinkButton` / `BaseButton`：按钮；`disabled`、`flat`、`action_mode`、`shortcut`、`button_group`（单选）。
- `TextureButton`：用贴图做按钮，注意 `stretch_mode`。
- `CheckBox` / `CheckButton` / `RadioButton`（Button + `toggle_mode` + `button_group`）。
- `LineEdit`：单行输入；`placeholder_text`、`secret`、`max_length`、`right_icon`。
- `TextEdit`：多行富文本编辑。
- `Slider` / `HSlider` / `VSlider` / `SpinBox`：数值输入；`step`、`min_value`、`max_value`、`value`、`allow_greater`/`lesser`。
- `OptionButton`：下拉选择；`add_item(text, id)`。
- `PopupMenu` / `PopupPanel` / `Window` / `ConfirmationDialog` / `FileDialog`：弹层；Godot 4 用 `Popup` 体系，独立窗口用 `Window`（替代旧 `PopupDialog`）。

### 展示类
- `Label`：`text`、`horizontal_alignment`、`autowrap_mode`（自动换行）、`clip_text`（溢出裁剪）、`fit_content`（缩小字号适应）、`bbcode_enabled`（富文本）、`language`（强制 RTL/LTR）。
- `RichTextLabel`：复杂富文本、超链接、BBCode、内嵌控件，做对话框/日志首选。
- `TextureRect`：图片；`stretch_mode`（keep / scale / tile / keep_aspect / keep_covered 等）。
- `NinePatchRect`：九宫格拉伸贴图，做可伸缩面板边框零变形。
- `TextureProgressBar`：血条/进度条；`fill_mode`、`nine_patch_stretch`、`step`、`value`。
- `ProgressBar`：纯矢量进度条，配合 Theme 的 StyleBox。
- `ColorRect` / `ColorPicker` / `ColorPickerButton`：纯色块/取色。
- `ItemList` / `Tree`：高性能列表/树（上千项也不卡，优于手写 VBox+Label）。
- `GraphNode` / `GraphEdit`：节点编辑器类 UI（技能树、蓝图）。

### 复合/高级
- `TabContainer`：标签页；首个子节点即第一个 tab。
- `SubViewportContainer`：嵌入另一个视口（小地图、3D 预览）。
- `VideoStreamPlayer`：播放视频（过场/背景）。
- `AspectRatioContainer`：保持子节点宽高比。
- `Container` 自定义：继承 `Container` 重写 `_notification(NOTIFICATION_SORT)` / `get_minimum_size()` 做专属布局。

## 2. Anchors / Offsets

- Anchor 是**归一化（0~1）**相对父级矩形四条边的位置；Margin 是锚点确定后到节点边的像素偏移。
- 预设（Anchor Preset）：Full Rect、Top Left/Right、Center、Bottom Wide 等；编辑器里点锚点图标快速设。
- 当左右锚点不重合（如 Full Rect），节点宽度随父级拉伸，margin 是固定偏移；当左右锚点重合（如 Center），节点宽度固定，水平位置随父级中心移动。
- 仅在**根级面板/装饰节点**用手摆锚点；内部一律交给 Container，避免 Anchor 与 Container 抢夺布局控制权。
- Godot 4 提供 `anchors_preset`、可在代码 `set_anchors_preset(Control.PRESET_FULL_RECT)`。

## 3. Size Flags（尺寸标志）

每个 Control 有 `horizontal_size_flags` / `vertical_size_flags`，组合位：
- `FILL`：占据分配给它的空间。
- `EXPAND`：在空间过剩时争取更多（可与其他 EXPAND 兄弟竞争，按 `size_flags_stretch_ratio` 分配）。
- `SHRINK_BEGIN / SHRINK_CENTER / SHRINK_END`：不填满时对齐方向。
- `IGNORE`：容器忽略其最小尺寸（慎用）。
常见组合：`SIZE_EXPAND_FILL`（占满并争取空间）、`SIZE_FILL`（占满不争）、默认（按内容）。
规则：需要"撑开"的滚动内容用 `EXPAND_FILL`；固定尺寸图标用默认；间隔用 `add_spacer`。

## 4. Theme 完整属性（ThemeType）

Theme 资源按 `ThemeType`（Button、Label、LineEdit、Panel、ProgressBar、Window…）组织。每个类型可覆盖：
- **StyleBox**：`normal`、`hover`、`pressed`、`focus`、`disabled`、`panel` 等（用 `StyleBoxFlat` 矢量或 `StyleBoxTexture` 贴图或 `StyleBoxEmpty`）。
- **Font**：`font`（动态字体，Godot 4 用 `FontFile`/`FontVariation`）。
- **Font Size**：`font_size`。
- **Color**：`font_color`、`font_hover_color`、`font_pressed_color`、`font_disabled_color`、`font_outline_color` 等。
- **Constant**：`outline_size`、`shadow_offset_*`、`border_width_*`、`corner_radius_*`、`content_margin_*`、`hseparation`、`vseparation` 等。
- **Icon**：`icon`、`press_icon`、`hover_icon`、`checkerboard` 等。

### StyleBoxFlat 关键属性（Godot 4 向量 UI 主力）
`bg_color`、`border_color`、`border_width_*`、`corner_radius_*`、`corner_detail`、`shadow_*`、`outline_*`、`anti_aliasing`。纯色圆角面板零贴图，性能佳。

### 字体方案（关键）
- Godot 4 动态字体：`FontFile`（加载 .ttf/.otf/.ttc），`FontVariation` 做变体（粗体/斜体/字号档），`fallbacks` 数组挂 CJK 字体实现中文回退。
- 旧位图字体（`BitmapFont`）在 4 中基本弃用，缩放模糊。
- 中文/日文项目务必设 fallback，否则方块乱码。

## 5. 响应式设置清单

`Project Settings > Display > Window`：
- `stretch_mode`：`canvas_items`（推荐）让 UI 与 2D 一起缩放；`viewport` 渲染到固定缓冲再拉伸（更可控但更重）；`disabled` 不缩放（不适合发布）。
- `stretch_aspect`：`expand`（容器自适应，最推荐）/ `keep`（保持比例黑边）/ `keep_width`/`keep_height`/`ignore`（拉伸变形）。
- `stretch_scale`：运行时按设置/DPI 调整，支撑"UI 缩放"功能。
- `viewport/width` & `height`：参考分辨率（设计基准）。
- 安全区：移动端用 `get_safe_area()` 或 `MarginContainer` 适配刘海。

验证方法：编辑器视口顶部下拉切换多种分辨率/宽高比；或运行后用 `DisplayServer.window_set_size()` 临时改窗大小目测。

## 6. 常见 UI 模式结构

- **主菜单**：`CanvasLayer > MarginContainer(Full Rect) > VBoxContainer(居中) > Title(Label) + Buttons(开始/设置/退出)`。用 `responsive_menu.tscn` 起步。
- **HUD**：`CanvasLayer > MarginContainer > (左上血量/右上小地图/底部技能栏)`，各区域用 Anchor 或独立 Margin 分区；HUD 只读游戏状态信号。
- **暂停菜单**：`ConfirmationDialog` 或自定义 `Window`，`pause_mode = PROCESS`（或自身 `process_mode`），呼出时 `get_tree().paused = true`。
- **设置面板**：`TabContainer`（画面/音频/控制）+ 各页 `ScrollContainer > VBox`（Slider/CheckBox/OptionButton）。设置写 `ProjectSettings` 或自定义 `SaveManager`。
- **背包**：`GridContainer`（列数固定）+ 物品 `Button`/`TextureButton`；量大用 `ItemList`。拖拽用 `_get_drag_data`/`_can_drop_data`/`_drop_data`。
- **对话框**：`RichTextLabel` + 头像 `TextureRect` + 名字 `Label` + 选项 `VBox` 按钮；用信号推进剧情节点。
- **血条/资源条**：`TextureProgressBar`（贴图九宫）或 `ProgressBar`+Theme；数值变化经 Tween 平滑，配 `Label` 同步。
- **加载界面**：`CanvasLayer > ColorRect(全屏) + ProgressBar + Label(提示)`，配合 `ResourceLoader`/`SceneTree.change_scene_to_packed` 的 `progress` 信号。

## 7. 可访问性清单

- 焦点：`focus_mode`（ALL/CLICK/NO）；用 `grab_focus()` 在菜单打开时聚焦首项；`focus_neighbor_left/right/top/bottom` 修正手柄/Tab 跳序；`Control` 的 `gui_focus_changed` 信号做高亮。
- 键盘/手柄：所有可操作项支持 Enter/Space 激活；菜单支持方向键；`shortcut` 绑快捷键。
- 文本缩放：`stretch_scale` 或 Theme `font_size` 倍增；布局需随字号不破。
- 对比度：正文前景/背景 ≥ 4.5:1；状态不只靠颜色（加图标/形状/纹理）。
- 色盲友好：避免红/绿单靠区分；用形状+文字标签。
- 屏幕阅读：给控件有意义 `name`/`tooltip`；装饰节点 `mouse_filter = IGNORE` 且 `focus_mode = NO` 并 `hide` 或 `visible = false` 避免误读。
- 减少动态：尊重系统"减少动态效果"偏好，弱化 Tween/粒子。

## 8. 性能要点

- Control 节点便宜但数量过千仍卡；列表用 `ItemList`/`Tree`/虚拟列表。
- 避免每帧重建 UI 子树；用对象池 + `visible` 切换。
- `StyleBoxFlat` 矢量优先；慎用大量重叠半透明贴图（破坏批处理）。
- `Label` 的 `autowrap` 在长文本时重排成本高，静态文本预排版。
- 离屏 UI 设 `visible=false` 或 `process_mode=DISABLED` 减少绘制/通知。
- 合理使用 `CanvasLayer`（独立层，但每层有额外绘制开销，不宜过多）。

## 9. Godot 3 vs Godot 4 差异（迁移注意）

- 字体：3 用 `DynamicFont`（.tres）+ `BitmapFont`；4 用 `FontFile`/`FontVariation`/`ThemeDB`，且 Theme 体系重写，3 的 Theme `.tres` 需重做。
- 弹出层：3 用 `Popup`/`PopupDialog`/`PopupMenu`；4 用 `Window` 统一窗口体系（`Popup*` 仍存在但实现变）。
- 富文本：`RichTextLabel` 4 用 BBCode 改进、`meta_clicked` 信号。
- 容器/锚点行为基本一致；4 新增 `FlowContainer`、`AspectRatioContainer`、`Container` 排序通知更稳定。
- ColorPicker、FileDialog 等内置对话框 4 重构为 `Window` 子类。
- 主题默认：4 有 `ThemeDB` 全局默认主题，项目级 `default_theme` 覆盖更干净。

## 10. 自检：何时该用哪种方案

| 需求 | 首选 |
|---|---|
| 固定边距外壳 | MarginContainer |
| 纵向菜单/列表 | VBoxContainer (+ ScrollContainer) |
| 工具栏/行 | HBoxContainer |
| 网格物品 | GridContainer / ItemList |
| 可拉伸圆角面板 | Panel + StyleBoxFlat |
| 九宫贴图边框 | NinePatchRect |
| 进度/血条 | ProgressBar / TextureProgressBar |
| 富文本/对话 | RichTextLabel |
| 大量列表 | ItemList / Tree |
| 标签页 | TabContainer |
| 独立弹窗 | Window / ConfirmationDialog |
| 统一视觉 | 项目 Theme 资源 |
