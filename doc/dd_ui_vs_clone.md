# 原版 DD（E盘）↔ 克隆实现（F盘）UI 层对照

> 本文件是 `dd_original_vs_clone_crossref.md`（数据契约/系统逻辑）的**姊妹篇**，只比 **UI / 表现层**。
> 前置结论仍成立：**E 盘读不到原版引擎架构**（无 exe/DLL/`*_Data`），`scripts/layout/*.darkest` 是原版**唯一的 UI 布局契约**；F 盘是模仿项目，其 UI 体现"这个项目团队怎么重做 DD 的界面"，≠ 原版真身。

---

## 0. 一句话结论

原版 UI 是**数据契约驱动**：`scripts/layout/*.darkest` 用像素坐标声明每个 HUD 元素的位置/尺寸/动效，引擎在运行时消费这些契约 + 纹理资源拼出界面（逻辑不可读）。
克隆 UI 是**场景树 + 数据驱动主题**：Godot `.tscn` 场景定义骨架分区，C# "skeleton 分部类"桥接，颜色/字号/技能栏列数抽到 `.tres` 资源，并自带 `LayoutAudit` 审计。**美术皮肤高度复用原版（`borrow/`），差异集中在布局坐标系、信息密度与架构分层。**

---

## 1. 三层原料对照

| 层 | 原版（E盘） | 克隆（F盘） | 可信度 |
|---|---|---|---|
| **布局契约** | `scripts/layout/*.darkest`（像素坐标） | `.tscn` 场景 + `*Skeleton.cs` 分部类 | 原版✅ / 克隆⚠️（重做） |
| **美术资源** | `heroes/*/anim/*.png`、`borrow/panels`、`borrow/raid` 等纹理/atlas | 直接复用 `borrow/`（同批美术） | ✅ 一致 |
| **调色板/字体** | 颜色 baked 进 PNG/atlas；字体由引擎+`localization` 管 | `UiPalette`(.tres) + `DdTheme`(中央 Theme) | ⚠️ 克隆独有抽象 |
| **质量门** | 无（引擎+美术保证，源码不可读） | `LayoutAudit.cs`（Label 不重叠 / Panel 不透明 / 落相机内） | 🔴 克隆多出 |

---

## 2. 屏幕级映射表（原版契约 → 克隆落点）

| 原版契约文件 | 原版元素 | 克隆对应 `.cs` / `.tscn` | 备注 |
|---|---|---|---|
| `screen.raid.darkest` | 探索 HUD（火把/回合/任务/击杀/背包/篝火） | `BattleUI.Dungeon.cs` `BattleTopBarSkeleton` `BattleBottomBarSkeleton` `WalkMapView.cs` `CampSkillPanel.cs` | 最完整的一屏 |
| `screen.raid.battle.darkest` | 战斗 HUD（攻击覆盖层/入场/怪物面板位） | `BattleUI.Build.cs` `BattleUI.Cards.cs` `SkillBoxTemplate.cs` `BattleUI.StatusTray.cs` | 怪物面板位 `946 712` |
| `screen.raid.status_bars.darkest` | 状态条（血/压/图标托盘） | `BattleUI.StatusTray.cs` `UnitCardTemplate.cs` | 血条 `y_pos 698` |
| `panel.hero.darkest` | 英雄面板（血/压/属性/装备/饰品） | `UnitCardTemplate.cs` `HeroDetailSkeleton.cs` `HamletRoot.HeroDetail.cs` | 装备位 `238 0` 饰品 `453 0` |
| `panel.monster.darkest` | 怪物面板 | `UnitCardTemplate.cs`（怪物侧） | — |
| `pannel.inventory.darkest` | 背包（8 列网格，`start_pos 20 28`） | `InventoryPanel.cs` `slot_row.tscn` | 原版 8 列；克隆待核列数 |
| `camp_layout` | 篝火（4 英雄环绕） | `CampSkillPanel.cs` | — |
| （主城无独立 layout 文件） | 主城/建筑/名册/补给 | `HamletRoot.*.cs` `HamletSkeleton.cs` `BuildingNavButtonTemplate.cs` `ProvisionSkeleton.cs` `QuestSelectSkeleton.cs` `RosterRowTemplate.cs` | 克隆自建结构 |
| （标题屏） | 主菜单 | `MainMenuRoot.cs` `MainMenu.tscn` | — |
| `prop_interaction` / `curio` | 奇物交互 | `CurioPanel.cs` `PathChoicePanel.cs` `ScoutMarkPanel.cs` | — |

> 命名规律：克隆用 **`*Skeleton.cs`** = 编辑器可改的"分区骨架"（只放容器与占位 Label），**`*.Build*.cs`** = 代码往骨架里填真实内容。`LightBarPanel`（火把条本体）是 C# 类，不在骨架里，由 `BattleUI.BuildTopRow()` 以 `new LightBarPanel()` 注入 `TorchWrap` 容器。

---

## 3. 五个核心偏差

### 🔴 偏差 1：设计分辨率 / 视口不同 → 信息密度压缩
- 原版 `screen.raid.darkest`：作者空间 **宽 1920**（`x_centre 960`、`safe_left 240`..`safe_right 1680`），底栏锚定 `y≈698–712`，顶部 HUD 在 `y 20–200`。这是高密度 16:9 布局。
- 克隆 `LayoutAudit.cs` 实测读取项目设置：**视口 `1280×720`**（`viewport_width=1280`/`viewport_height=720`，`stretch=canvas_items`）。
- 影响：克隆在更矮的画布上放同样多的 HUD，必然**压缩纵向空间 / 字号**，且 `LayoutAudit` 把"控件外接矩形超出 1280×720"直接判为越界（用户规则①"UI 看不全"）。这是与原版最直观的保真度落差，**且是克隆自己的取舍，不是原版行为**。

### 🔴 偏差 2：战斗阵型 UI 的"几 v 几"未对齐
- 原版 `status_bars` 的血条档位 `health_bar_widths 100 200 300 400` / `health_bar_shared_widths 300 400 500 600`、怪物面板 `monster_panel_position 946 712`，指向 **4v4** 战斗（DD 标准）。
- 前一轮 crossref 已标记：克隆契约里出现过"我方 6 / 敌方 4"的设定。**若战斗 UI 按 6v4 排，则与原版 4v4 整屏布局冲突**——需回 `BattleUI.Build.cs` / `SlotRowTemplate.cs` 坐实是"名册 6 / 战斗 4"还是真的战斗 6v4。🔴 待核。

### ⚠️ 偏差 3：布局定义范式不同（数据契约 vs 场景树）
- 原版：纯数据（`key x y` 像素坐标），引擎消费；改 UI 改文本即可，但逻辑不可读。
- 克隆：`.tscn` 场景树 + C# 骨架分部类。多一层"编辑器可干预"的抽象（用户 2026-09-17 明确要"能在编辑器里直接干预"），内容仍由代码填。**这是克隆的工程化优势，但意味着它重构了原版的布局管线，不能直接当成原版读法。**

### ⚠️ 偏差 4：调色板 / 字体被抽成外部资源（原版无此层）
- 克隆 `UiPalette`（→ `ui_palette.tres`）：把四色、面板底/边、HP/压力/地图色、字号三档、`SkillBarColumns=4` 全外部化，且 `AuditFile()` 逐字段比对 `.tres` 与 `Default()` 防分叉（#325 D6）。
- 克隆 `DdTheme`：Godot 中央 `Theme` 资源，主字体 `Noto Serif SC`（思源宋体，因 UI 是**中文**）+ `EB Garamond` fallback；标题用 Bold 变体。
- 原版：颜色 baked 进纹理、字体由引擎+localization 管，**没有"可编辑调色板资源"这一层**。克隆这层是它自己的表现层架构，**学它等于学"DD-like 游戏怎么数据驱动皮肤"，不等于学原版**。

### ⚠️ 偏差 5：项目私有 UI 规则（原版没有）
- **规则①（2026-09-15）**：一切 UI 考虑相机大小 → 1280×720 视口 + `LayoutAudit` 越界读数。原版无此约束（原生分辨率渲染）。
- **规则②（2026-09-16）**：空闲/待填位置用**半透明占位**（`PlaceholderFill` a=0.22）。原版用具体美术占位，克隆用程序化半透明——视觉意图一致，实现不同。
- **中文 UI**：克隆主字体强制 CJK（Noto Serif SC）。原版多语言但 UI 文案在 `localization`，美术字是自研哥特衬线；克隆用思源宋体替代，风格意图（衬线/古典）一致但字形不同。

---

## 4. ✅ 高度忠实的部份（可直接当教材）
- **美术皮肤**：克隆直接吃 `borrow/` 里的原版 `panels/raid/heroes/trinkets` 纹理/atlas → 界面"长相"基本 1:1。
- **主城骨架比例**：`HamletSkeleton` 注释明确写"DD 1:1 3-3：建筑 nav 窄左列 / 右列 300 宽"——刻意对齐原版主城分区。
- **战斗顶栏分区**：`BattleTopBarSkeleton` 参考图② = 左上任务+撤退 / 正中火把条 / 右侧顺序+意图，与原版 `screen.raid.darkest` 的 quest_info(左上) / torch(中) / kill_count(右上) 分区一致。
- **背包网格**：原版 8 列 `offset 80 160`，克隆 `InventoryPanel` 沿用网格思路（列数待核）。

---

## 5. 怎么用这张表学原版 UI
1. **读原版契约当"规格"**：`scripts/layout/screen.raid.darkest`（探索）、`screen.raid.battle.darkest` + `status_bars.darkest`（战斗）、`panel.hero.darkest`（角色卡）、`pannel.inventory.darkest`（背包）是**原版亲生的 UI API 文档**，逐行像素坐标。
2. **在克隆里找对应实现当"一种重做"**：用 §2 映射表定位 `*Skeleton.cs` / `*.Build*.cs`，看它怎么把坐标翻译成 Godot 锚点/容器。
3. **每一条 🔴/⚠️ 都是探针**：优先去**原版游戏实跑**观察这些行为（尤其 1080p 下 HUD 密度、4v4 站位、背包 8 列），回克隆看它补得对不对——补错/补薄处即"原版另有其逻辑"的线索。
4. **想读真实 UI 架构**：仍须拿完整安装的 `DarkestDungeon_Data/Managed/Assembly-CSharp.dll` 用 dnSpy/ILSpy 反编译（本机 E 盘无）。克隆的 `LayoutAudit`/`DdTheme` 是它自己的工程化，不是原版。

---

## 6. 待补（下一步可做的三选一）
- ① 回 `BattleUI.Build.cs` / `SlotRowTemplate.cs` 坐实🔴偏差2：战斗到底 4v4 还是 6v4。
- ② 读 `InventoryPanel.cs` 确认克隆背包列数是否等于原版 8 列（偏差3 的具象化）。
- ③ 抽 `borrow/panels` 里原版 `panel_inventory.png` 等，做"原版皮肤 vs 克隆摆放"的逐控件像素比对。
