# UI 还原计划：锚点 + 资产尺寸 → 同尺寸占位色块 → 后续换原创资产

> 版本 v3 · 2026-09-19（新增 §1.6 锚点性质分类、§1.7 菜单层级）
> 背景：本项目（Darkest）为 Godot 4.6 + C# 的类 DD 回合制战术 RPG。目标是 1:1 还原 Darkest Dungeon 的 UI 布局。
> 配套产物：`ui_spec.json`（v3-富化：每字段带「性质」标签 + 每 section 带「菜单层级」）、`ui_spec_table.html`（按 L1/L2/L3 分组可视化）。

---

## 0. 先回答：这样合规吗？

**合规。** 需要把三样东西分清楚：

| 对象 | 性质 | 能否使用 |
|---|---|---|
| **尺寸数据**（某张图是 720×224） | **度量事实**，不是版权表达 | ✅ 可用 |
| **锚点坐标**（面板挂在 946,712） | 布局/构图数据 | ✅ 可用 |
| **图像像素本体**（贴图、图集、字体文件） | 受版权保护的表达 | ❌ 不可用 |

**判定依据：**
- 版权保护的是**具体表达**（线条、色彩、造型），不保护"这张图宽 720"这种**事实性度量**。
- 类比：量了别人房间"宽 3 米"，自己做一面 3 米的墙，不侵权。
- **实际动作链**：读数字 → 用同尺寸色块占位 → 后续用**原创资产**填充。DD 的像素从头到尾**不进入我们的工程**。

**红线对照（本项目红线 27「学构图不抄素材」）：**
- 学构图（锚点 + 尺寸关系）→ ✅ 合规
- 抄素材（把 DD 的 PNG/Atlas 导入工程）→ ❌ 违规

**注意**：`borrow/` 目录下若存在 DD 的 `.atlas/.skel/.png`，**只能本地对照查看，不得 import 成项目资产、不得进发布包**。

---

## 1. 可行性验证结果（已完成）

### 1.1 E 盘资源结构

```
E:/SteamLibrary/steamapps/common/DarkestDungeon/
├── panels/          ← UI 面板（散装 PNG，非打包）
│   ├── panel_hero.png          720×224
│   ├── panel_monster.png       702×368
│   ├── panel_map.png           720×360
│   ├── panel_inventory.png     720×360
│   ├── panel_banner.png        754×136
│   ├── panel_transition.png   1920×20
│   └── ...
├── overlays/        ← 图标/按钮/特效（announcement_frame 619×136、tray_* 24×24、target_1..4 175/246/328/436×206 …）
├── shared/          ← 系统级 UI（controls/menu/dialog …）
├── fe_flow/         ← 主菜单流
├── campaign/town/   ← 主城（buildings/ 各建筑立绘与图标）
└── scripts/layout/  ← 战斗 HUD 布局
```

**关键结论：DD 的 UI 面板是散装 PNG，尺寸可直接读 PNG 文件头（26 字节），无需解码、无需解包。**

### 1.2 布局文件里的坐标格式（两种，都要支持）

**格式 A — section 缩进式**（主城、系统屏）：
```
town_screen_layout:
	.roster_list_pos		1550 0
	.panel_size 1550 1080
	.character_panel_size 1395 1080
```

**格式 B — 单行 key:value 式**（战斗 HUD 面板）：
```
status_bars: 	.char_x_offset -50 	.y_pos 698
				.health_bar_widths 100 200 300 400
panel.hero.darkest:
health_layout: .pos 130 11 .colour #c00000
stress_layout: .pos 130 40 .colour 150 150 150 255
```

**教训**：早期解析器只认格式 A 的缩进结构，导致 `panel.hero.darkest`、`status_bars.darkest` 被误判为"空文件"。**必须两种格式都解析。**

### 1.3 三类坐标，绝不能混用

| 类别 | 例子 | 空间 | 能否当屏幕像素 |
|---|---|---|---|
| **UI 层** | `roster_list_pos 1550 0`、`panel_size 1550 1080`、`panel.map` tab `672,252 48×90` | 逻辑画布 1920×1080 | ✅ 可以（用比例） |
| **角色站位** | `hero_start_pos 788 680`、`monster_start_pos 1050 680`、`hero_spacing -168` | 场景空间 | ⚠️ x 可参考，y 不是脚底 |
| **建筑/场景** | `pos3d 1420 0 -100`、`tavern .pos 550 10 -1` | **3D 世界坐标** | ❌ 必须经相机投影 |

**相机证据（`scripts/camera.darkest`）：**
```
m_CameraParameters:
    .RegularHorizontalFOV 75.0
    .AspectRatio 2.666666        ← 24:9
    .BattleRotationAngle 7.0
m_RoomCameraParameters:
    .Position 960.0 300.0 -1240.0   ← 3D，带 z
```

### 1.4 尺寸的三种来源（优先级从高到低）

1. **布局文件自带**（最可信）：`panel_size`、`character_panel_size`、`bbox_size`、`inventory_grid_size`、`button_size`、`icon_size 72 144` 等 —— 共 57 处。
2. **尺寸表/参数**：如 `health_bar_widths 100 200 300 400`、`health_bar_height 10` —— 由索引（队伍人数）查表，说明"尺寸随状态变化"。
3. **资产固有尺寸**（兜底）：布局文件没给 size 时，读 PNG 文件头。

### 1.5 已验证的坑（务必避免）

- ❌ **不要拿 wiki 截图像素和布局坐标直接叠加**：截图经过了未知缩放/信纸化（Hamlet 实为 1680×1050，内容区 1679×912，比例 1.841 ≠ 16:9）。
- ❌ **不要把"地面锚点"当"矩形左上角"**：`hero_start_pos 788 680` 的 y 是基准线，立绘从其上方向生长。
- ❌ **不要硬编码绝对值**：应使用逻辑基准 1920×1080 + 比例，交由 Godot 的 stretch/CanvasLayer 缩放。

### 1.6 锚点字段的「性质」分类（v3 新增）

**为什么需要**：字段名里既有 `*_pos`（绝对）也有 `*_offset`（相对），还混着 `*_scale`（倍率）。混着读必然出错——尤其 `*_offset` 出现负值时，容易被误当成"坐标错了"。**性质标签就是防止把偏移量当绝对坐标用。**

| 性质 | 判定依据 | 值含义 | Godot 落码 | 本表数量 |
|---|---|---|---|---|
| `absolute` 绝对锚点 | 尾缀 `_pos`/`_position`，或 `pos`/`flamepos`/`x_centre` | 1920×1080 画布坐标（原点左上） | `node.Position = new Vector2(a,b)` | 441 |
| `offset` 相对偏移 | 名含 `offset`/`spacing`/`margins`/`centre` | **父基准点 + (a,b)**；父基准点常落在元素中心/内部，故可为负 | `node.Position = parentAnchor + new Vector2(a,b)` | 602 |
| `scale` 缩放/翻转 | 名含 `scale`/`flip`/`mirror` | 倍率，非坐标；负值 = 该轴镜像 | `node.Scale = new Vector2(a,b)` | 10 |
| `offscreen` 超界/隐藏 | 名含 `offscreen`/`hidden` | 故意推到可视区外，作动画入/出场起点 | 同上（作为动画起始位） | 1 |
| `参数` | 颜色/时长/阈值/边距等 | 非坐标配置项 | 不进定位运算 | 17 |

**负值的三类语义**（回答"为什么会有负值锚点"）：
1. **相对偏移为负**（占 90%+）：说明父基准点在元素中心或右下，元素从基准点向外张开。例：`round_display.sprite_offset -70,-63` ≈ 140×126 精灵的一半 → 父基准点 = 圆盘中心。
2. **负 scale**：镜像翻转。例：`negative_controller_selected_icon_scale -1.0,1.0`。
3. **超界位**：故意推屏外。例：`town.button_navigation_offscreen_pos -40,230`。

**落码铁律**：负值**不是数据错误、不需要修正**。唯一要防的是把 `offset` 当绝对坐标用（如把 `hero_slot_offset -230,-100` 直接当画布坐标，会画到屏外）。

### 1.7 菜单层级标注（v3 新增）

DD 的 UI 是三层结构。给每个 section 打上层级，目的是**排落码顺序与可见性**：L1 常驻、L2 互斥、L3 模态。

| 层级 | 定义 | 数量 | 典型 section |
|---|---|---|---|
| **L1 一级屏** | 独立整屏，各自有独立根 | 18 | `town`（主城）、`fe_flow`（主菜单）、`screen.raid`+`battle`+`status_bars`（战斗 HUD）、`raid_results`（结算）、`game_over`、`loading_screen` |
| **L2 二级面板** | 从 L1 点开的常驻功能面板 | 42 | `roster`（花名册）、`quest_select`（任务）、`shared/character`（英雄详情）、各建筑（blacksmith/guild/sanitarium…）、`shared/menu`（设置） |
| **L3 三级弹层** | 从 L2 再点开的模态/浮层 | 28 | `shared/tooltip`（悬浮提示）、`confirm_dialog`、`overlay.loot`（拾取）、`panel.monster`（怪物详情）、`panel.hero`（战斗分块） |

每个 section 还带 `所属场景`（主城/主菜单/战斗/结算/终局/系统/加载/通用）与 `层级说明`。

**落码含义**：
- L1 → 各挂一个 `CanvasLayer`，按场景切换显隐
- L2 → 挂 L1 的 `_uiRoot` 下，同一时刻只显示一个
- L3 → 挂在对应 L2/L1 之上，模态时拦截输入


---

## 2. 执行计划

### 阶段 1：抽取（数据层）

**目标**：生成一份机器可读的 `ui_spec.json`，包含全部面板的「锚点 + 尺寸 + 来源 + 类别」。

| 步骤 | 内容 | 产出 |
|---|---|---|
| 1.1 | 重新解析 `scripts/layout/*.darkest` 与 `campaign/**/*.darkest`，**同时支持格式 A 与格式 B** | 修正后的 `_ui_dist_raw.json` |
| 1.2 | 扫描全部 UI PNG 文件头，记录尺寸 | `_asset_sizes_raw.json`（已有，1589 条） |
| 1.3 | 建立「布局字段 → 资产文件」的**语义映射表**（人工确认，见 §4） | `ui_field_asset_map.json` |
| 1.4 | 为每个面板打标：类别（UI层/角色/建筑）、尺寸来源、可信度 | **`ui_spec.json`（核心交付）** |

### 阶段 2：占位（视觉层）

**目标**：在 Godot 里用色块把布局跑起来，尺寸与规格表一致。

| 步骤 | 内容 |
|---|---|
| 2.1 | 统一占位色：`PlaceholderFill = Color(0.62, 0.60, 0.58, 0.22)`（沿用既有规范，不换不删 §14.0.68） |
| 2.2 | 每个占位块严格按 `ui_spec.json` 的尺寸创建，命名 = 面板名 |
| 2.3 | UI 层：按逻辑坐标 1920×1080 布局，用比例换算 |
| 2.4 | 场景层：英雄/怪物用 `Sprite2D` 挂世界坐标，占位用同尺寸色块贴图 |
| 2.5 | **按 `性质` 决定落码方式**：`absolute` 直赋 position；`offset` 用「父锚点 + 偏移」；`scale` 走 Scale；`参数` 不进定位 |
| 2.6 | **按菜单层级搭建**：L1 各建 `CanvasLayer`；L2 挂 L1 的 `_uiRoot`；L3 叠在最上层 |
| 2.7 | 截图自查：用 Godot 自身截图（不是 wiki 图）验证色块位置 |

### 阶段 3：换资产（美术层）

| 步骤 | 内容 |
|---|---|
| 3.1 | 按 `ui_spec.json` 的尺寸清单，逐块绘制**原创哥特风**资产 |
| 3.2 | 尺寸必须与占位块一致（这样替换时零改动布局） |
| 3.3 | 接入时只替换贴图资源，不动节点结构和坐标 |

---

## 3. 验收标准

- [ ] `ui_spec.json` 覆盖全部面板，每条含：锚点、尺寸、来源、类别、**性质**、**菜单层级**
- [ ] 1071 个字段全部带 `性质` 标签，无"锚点却被判为参数"的错标
- [ ] 88 个 section 全部带 L1/L2/L3 层级与所属场景
- [ ] Godot 中每个占位块的尺寸与规格表**逐像素一致**
- [ ] `offset` 类字段一律用「父锚点 + 偏移」落码，无一处被当绝对坐标
- [ ] UI 层在不同窗口分辨率下比例正确（不出现偏移）
- [ ] 场景层英雄站位由世界坐标驱动，非硬编码屏幕像素
- [ ] 无任何 DD 原始资产进入工程（`grep` 校验）

---

## 4. 语义映射表（人工确认，节选）

| 面板 | 布局锚点 | 布局尺寸 | 资产 | 资产尺寸 | 类别 |
|---|---|---|---|---|---|
| 英雄战斗面板 | `panel.hero` 相对坐标 | 无 | `panels/panel_hero.png` | 720×224 | UI层 |
| 怪物信息面板 | `monster_panel_position` | 无 | `panels/panel_monster.png` | 702×368 | UI层 |
| 地图面板 | `tab pos 672,252` | `tab size 48×90` | `panels/panel_map.png` | 720×360 | UI层 |
| 转场条 | `panel_transition_bar 0,710` | 无 | `panels/panel_transition.png` | 1920×20 | UI层 |
| 状态血条 | `y_pos 698` | `健康条宽 100/200/300/400` | （程序绘制） | — | UI层 |
| 主城面板 | 屏幕级 | `panel_size 1550×1080` | — | — | UI层 |
| 角色详情 | 屏幕级 | `character_panel_size 1395×1080` | — | — | UI层 |
| 铁匠铺 | `pos 1460,-30` | `bbox_size 700×300` | `blacksmith.character.png` | 865×760 | 建筑 |
| 修道院 | `pos 1070,220` | `bbox_size 324×362` | `abbey.character.png` | 811×757 | 建筑 |
| 英雄站位 | `hero_start_pos 788 680` | 无 | 立绘资源 | — | 角色 |

---

## 5. 风险与对策

| 风险 | 对策 |
|---|---|
| 解析格式不全导致漏字段 | 双格式解析 + 抽样人工核对 |
| 资产尺寸 ≠ 屏幕显示尺寸（缩放） | 以布局文件自带 size 为准，资产尺寸仅兜底 |
| 场景层投影算不准 | 不在设计阶段硬算；运行时交给相机 |
| 误引入 DD 资产 | 独立 `assets/original/` 目录隔离 + 提交前 grep 校验 |

---

## 6. 立即可执行的下一步

1. ~~按 §1.2 双格式重写解析器 → 生成 `ui_spec.json`~~ ✅ 已产出
2. ~~补 `性质` 标签 + 菜单层级~~ ✅ 已产出（`_enrich_spec.py` + `_gen_spec_table.py`）
3. ~~修复解析器丢失 section 名/父级的缺陷~~ ✅ v3 解析器 → **432 section / 2456 字段**（`_parse_layout_v3.py` + `_rebuild_spec_v4.py`）
4. 按 `UI_STRUCTURE_DECISIONS.md` 落 §4 修复清单（建筑拆分 / 父容器重挂 / 层级自动标注）
5. 按 §4 补全语义映射表（人工逐条确认）
6. 在 Godot 中生成占位色块（阶段 2），落码时严格遵守 §1.6 的性质规则与 §1.7 的层级结构

---

## 7. 重大修订：解析器缺陷修复（v4）

> 详见 `UI_STRUCTURE_DECISIONS.md`

**v2 解析器致命缺陷**：格式A（缩进式）的顶层 section 名一律写成 `_root`，导致 54 个文件的 section 名丢失、字段被拍平、父容器归属不可恢复。直接后果：

- `heirloom_exchange_*_layout` 三个 section 合并 → 5 处 offset 找不到父
- `building.layout.darkest` 11 个 section 合并 → 建筑屏结构被抹平
- `monster_info` / `quest_info` 张冠李戴 → 施工挂错父节点

**v3 修复后对比**：

| 项 | v2 | v3/v4 |
|---|---|---|
| 带字段 section | 88 | **432** |
| 字段总数 | 1071 | **2456** |
| 真实命名 section | 34 | **432（零匿名）** |
| 父级信息 | ❌ | ✅ parent / depth / line / 归属容器 |

**关键发现**：`town.layout.darkest` 含 19 个 section，11 栋建筑的 `*_layout`（各带 `pos3d`+`pos`+`bbox_size`）**全在同一文件内**。

**三处结构判定结论**（`UI_STRUCTURE_DECISIONS.md`）：
1. 建筑屏 → 按 A 建独立 L2 子面板，用 `building_base_body_layout` 做统一外框
2. 5 处 offset → 父容器全部还原（含 `RaidPos7` 应从 `monster_info` 改挂 `quest_info`）
3. "层级为空" → 误会，那是工程节点不是 spec section；已给机械推导规则

