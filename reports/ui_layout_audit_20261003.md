# UI 布局审计与收口（2026-10-03）

> **用户问题**：「为什么开始游戏后 `credits_skeleton.tscn` 占满全屏且点不动？检查所有 UI 组件位置排布是否有不合理的地方」
> **结论**：两个**真根因已修** ——（A）主菜单占位骨架**常驻** ＋ 内部满屏 `Fill` 默认 `Stop` ⇒ 吞掉全部点击（实测 **9 ⇒ 0**）；
> （B）战斗屏「开始游戏后点不动」的**真凶** = **舞台带塌成 0 高**（`StageLayer` 非容器 ⇒ 最小高 0，被同层 `MidPadTop` 的 `ExpandFill` 吃光 ⇒ 卡片溢出 **233 px** 钻进底栏之下）。
> 本件另修两处：`MapCorner` 占位板不再压真内容（运行时隐藏 · 不删节点）· `VS` 居中并让开点击。
> **吞点击：主菜单 0 ／ Hamlet 0 ／ 战斗 0**（真玩家入口 `--click-menu=0` 亦 0）。
> **证据**：Godot 4.7.2 headless 四跑（`.tmp_ui_audit_menu.cmd`／`_hamlet`／`_battle`／`_battle_click`，全 **RC=0**）· 读数见 §4 · 代码位置见 §2/§3。

## §1 根因 A：`credits_skeleton` / `fe_flow_skeleton` 满屏吞点击（主菜单）

**现象**：开始游戏后主菜单**整屏点不动**（只能移动 / 关闭窗口）。

**根因**：`MainMenuRoot._Ready()` **无条件** `AddChild(FeFlowSkeleton)` ＋ `AddChild(CreditsSkeleton)` —— 两个「阶段 2 色块占位骨架」**常驻**压在主菜单上。
骨架根 `Control` 写了 `mouse_filter=2`（`Ignore`），但**内部满屏 `Fill`（`ColorRect`）没写** ⇒ 引擎默认 `Stop`(0) ⇒ 吞掉所有点击。

**关键事实（本件引擎实测）**：`mouse_filter` 是**逐控件**属性 —— 父节点 `Ignore` **不**豁免子节点。所以「骨架根写了 `Ignore`」≠「骨架不吃点击」。

**修法**（`darkest/scripts/ui/MainMenuRoot.cs`）：

- 新增 `MountPlaceholder` / `OpenPlaceholder` / `ClosePlaceholders`：占位骨架一律 **`Visible=false` 惰性挂载**（不再常驻），需要时才 `OpenPlaceholder`；父级挂 `_overlay?.ModalHost ?? this`。
- 🆕 `darkest/scripts/ui/PlaceholderBackButton.cs`：把占位骨架的「返回」色块**运行时升级成真按钮**（占位骨架不再是死色块）。

**读数**：主菜单吞点击 **9 ⇒ 0**（3 次判据全 `重叠 0 / 透明 0 / 吞点击 0` ✅）。

## §2 根因 B：战斗屏「开始游戏后点不动」—— 舞台带塌成 0 高

**现象**：进战斗后卡片**看不见 ＋ 点不动**，底栏面板却正常。

**推导**（`darkest/scripts/ui/BattleUI.Build.cs` 建树顺序）：

1. `StageLayer` 是**纯 `Control`（非容器）** ⇒ 最小高 = **0**；
2. 同层 `MidPadTop` 是 `ExpandFill`（`stretch 0.6297`）⇒ 中段可用空间被这个**空档**吃光 ⇒ **舞台带高度 = 0**；
3. `PlayerArea` / `EnemyArea` / `VS` 锚 `AnchorTop=0.6297 / AnchorBottom=0.95`（原本是「对满屏 1080」的 DD 语义）⇒ 现在是对 **0 高矩形**算锚点 ⇒ 卡片全部溢出 **233 px**，钻进底栏面板之下 ⇒ **看不见 ＋ 点不动**。

**修法（三处 · 全部零数值改动）**：

| # | 文件 | 改动 |
|---|---|---|
| 1 | `BattleUI.Build.cs`（:114-115） | `const float stageBandMinH = CardH + SupportH + 48f;`（112+86+48 = **246**）⇒ `midRow.CustomMinimumSize = new Vector2(0, stageBandMinH)` |
| 2 | `BattleUI.Build.Rows.cs`（:141-190） | `playerArea` / `vs` / `enemyArea` 纵向锚 `0.6297~0.95` ⇒ **`0~1`**；横向仍守 DD 1:1 带宽（`0.148/0.410` · `0.41/0.547` · `0.547/0.809` = 26.2%） |
| 3 | `BattleUI.Build.Rows.cs` | 4 个**纯展示件**逐件置 `Ignore`：`_actorRow`（`CurrentActorRow`）· `actorFrame`（`CurrentActorFrame`）· `_actorPortrait`（`CurrentActorPlaceholder`）· `((Control)_actorDetailBox)`（内部只有 1 个 Label ⇒ 纯展示；原 `Stop` 盖住 2 张支援位卡） |

**读数**：撑爆消除 —— 「内容需求超出相机 **0 个**（全部装得下 ✅）」。

## §3 本件新增两处

### (a) `MapCorner` 占位板压住真内容 ⇒ 运行时隐藏（`BattleUI.Build.cs:163-172`）

```csharp
if (_mfPanel is not null && overlay.GetNodeOrNull<Control>("MapCorner") is Control mapPlaceholder)
{
    mapPlaceholder.Visible = false;
}
```

- **依据**：`battle_overlay.tscn` 的 `MapCorner`（DD `panel.map` 占位 · 720×360 · 锚 `0.615~0.99 × 0.6367~0.97`）与底栏 **E 区多功能框是同一块 DD 区域**，而覆盖层画在底栏**之上** ⇒ 占位板半透明洗色（`UiPalette.PlaceholderFill` · alpha 0.22）＋「地图／地图页签／地图回中按钮」三个占位文字压在真内容上。
- **E 区已自带【地图】页**（`SetMultiFunctionPage(4)` · 格子主画面）⇒ 真内容在 ⇒ 占位板隐藏。
- ⚠️ **只改可见性、不删节点**：`tools/dsh/check_dd_layout.ps1:34` 按名字 grep `MapCorner`（DD 台账，不换不删）。
- **修复效果**：`BattleOverlay/MapCorner/PurposeLabel (1187,859)` 与 `MfContent` 的重叠对**已消失**。

### (b) `VS` 居中 ＋ `Ignore`（`BattleUI.Build.Rows.cs:164-166`）

```csharp
vs.HorizontalAlignment = HorizontalAlignment.Center;   // 矩形按设计吃满中缝（259×246），默认左对齐会把 VS 画到中缝左边缘
vs.MouseFilter = Control.MouseFilterEnum.Ignore;       // 纯展示：逐控件写（父级 Ignore 不豁免子节点）
```

## §4 读数（四跑 · 全 RC=0）

| 屏 | 命令 | 重叠对 | 透明框 | **吞点击** | 其他 |
|---|---|---|---|---|---|
| MainMenu | `--ui-audit-all` | **0** | **0** | **0** ✅ | 可见 Label 6 ／ Panel+PC 6 · 越界 0（3 次判据一致） |
| Hamlet | `--ui-audit-all` | **0** | **0** | **0** ✅ | 可见 Label 34 ／ Panel+PC 51 · 跳过裁剪外元素 3 |
| Battle（`--expedition --ui-audit-all`） | 同上 | 45 ⇒ **44** | 11 | **0** ✅ | Label 69 ／ Panel+PC 48 · 越界 3（DD 语义 · 见 §5）· 内容需求超出相机 **0** |
| Battle（真玩家入口 `--click-menu=0`） | 同上 | **48** | **9** | **0** ✅ | Label 69 ／ Panel+PC 57 · `ObjectDisposedException` **0** ／ `SCRIPT ERROR` **0** |

- 修复前基线（前棒取证）：主菜单**吞点击 9**；战斗屏吞点击 **7** ⇒ 舞台带修复后 **0**。
- `ERROR` 仅 2 条既存噪声（shader cache ／ root certificate），非本件引入。

## §5 误报与登记（**不修**）

| 项 | 判定 |
|---|---|
| 透明框 11（`UnitCard` ＋ `@PanelContainer@38..44` 等 `a=0`） | **误报**：`unit_card.tscn` 用 `StyleBoxFlat_noframe` ⇒ 无底色是**设计** |
| `BattleBg`（`a=0.6`） | **误报**：世界层背景按设计半透明 |
| `building_popup` 裁剪 | **设计**：弹窗裁剪到内容 |
| 4 个「零尺寸关闭按钮」 | **误报**：真实最小尺寸 `58×31` |
| 越界 `RaidCampLayer#RaidPos2 (-255,0)` ／ `RaidQuestInfoLayer#RaidPos7 (-170,98)` | **登记**：DD 负值语义（`fourth_pos` / `complete_return_to_hamlet_pos`，**未钳值未改符号**） |
| 越界 `BattleBg (-320,-180)` | **登记**：世界层坐标，与相机口径不可比 |
| Battle 重叠 44 主体（`@Control@28/*` 的 `PurposeLabel`/`LayerTitle` 全在 `(0,0)` 附近） | **登记**：**S1 重入**产生第二份 `UiRoot`（`O-123`／§9.17 已知迁移目标） |
| `RaidX*/PurposeLabel` 同格互叠 | **登记**：占位族设计 |
| `MidCol/StageLayer/@Label@36`（= `VS` 259×246） | **登记**：矩形按设计吃满中缝（本件已居中） |
| `BottomRowBox/EArea/PurposeLabel` ⟷ `MultiFunctionBox/MfColumn/MfContent` | **登记**：E 区**占位标题**（`mouse_filter=2`）与**真内容 Label**（`a=0` · `Ignore` 家族）互叠 ⇒ **零点击影响** |
| `WARNING: Parent path './CArea'|'./ActorDetailBox' for node 'PurposeLabel' has vanished`（2 处） | **登记**：`O-122` 家族（`battle_bottombar.tscn` 实例化时） |
| `ERROR: Failed to read the root certificate store` ／ `Can't create shader cache folder` | **登记**：headless 噪声（Windows / APPDATA 隔离），无害 |

## §6 引擎实测：`mouse_filter` 默认值（Godot 4.7.2）

| 默认值 | 控件类型 |
|---|---|
| `0` = `Stop`（吃点击） | `Control` · `ColorRect` · `Panel` · `PanelContainer` · `RichTextLabel` · `Button` · `ItemList` |
| `1` = `Pass`（冒泡给父级） | `MarginContainer` · `VBoxContainer` · `HBoxContainer` · `GridContainer` · `ScrollContainer` · `TextureRect` · `CenterContainer` · `TabContainer` |
| `2` = `Ignore`（完全不吃） | `Label` |

⇒ **逐控件**语义：父级 `Ignore` 不豁免子节点；纯展示件必须**自己**写 `Ignore`。

**本件按此补齐 76 处**：`battle_overlay` 19 · `hero_detail_skeleton` 19 · `battle_bottombar` 8 · `quest_select_skeleton` 8 · `controls_skeleton` 6 · `provision_skeleton` 6 · `unit_card` 3 · `loot_overlay_skeleton` 3 · `main_menu` 3 · `panel_banner_skeleton` 1（**未碰**任何 `Button` 及其子树）。
配套：🆕 `darkest/scripts/ui/LayoutAudit.Clicks.cs` 判据 3 新增「`Pass` 冒泡豁免」（被盖的是它自己的祖先时不算吞）· `LayoutAudit` 源码分工 = 主流程 ＋ `Traversal`／`Caliber`／`Clicks` 三片（每片 ≤276 行）。

## §7 门禁与验证（全绿）

```
python -X utf8 tools/check_file_size.py              → rc=0  OK: all 634 scanned program files <= 600 lines (0 allowlisted)
python -X utf8 tools/check_file_budget.py            → rc=0  仅 1 条预警：warn 453 scripts\ui\MainMenuRoot.cs
python -X utf8 tools/check_godot_refs.py --root darkest → rc=0  0 hits
pwsh -NoProfile -File tools/dsh/check_placeholders.ps1 → rc=0  PASS（out-of-viewport=0 / size-mismatch=0 / offset-at-root=0）
pwsh -NoProfile -File tools/dsh/check_ui_namespace.ps1 → rc=0  OK: UI namespace unified as Darkest.UI
dotnet build darkest\Darkest.csproj -p:DarkestTargetFramework=net10.0 → rc=0  0 错 / 72 警告
```

**最低限度必要性测试**（用户硬约束）：本件为 UI 接线与可见性改动 ⇒ **不跑全量 `dotnet test`**；功能验收走**玩家路径**（三屏冒烟 ＋ `--click-menu=0` 真入口），不以「编译通过 ＋ 测试全绿」当功能验收。

## §8 边界与下一件

- `MapCorner` 的 `CustomMinimumSize` 720×360 与锚点冲突 ⇒ 本件只做**运行时隐藏**，静态处置（清 `cms` 或改锚）待裁（`O-126`）。
- Battle 重叠 44 的**根治**在 S1（`Bind()` 重入重建骨架 · `O-123`／§9.17），不在本件。
- 新增占位骨架**必守三条**（`O-125`）：① 默认 `Visible=false` 惰性挂载；② 内部满屏 `Fill`／装饰件**逐件**显式 `Ignore`；③ 纯展示件同律。
- 下一件：`observe_list` 从 **(380)** 起（本件已续编 (378)／(379)）· `O-120`~`O-126` 待裁 · `P0` 等策划回件（解锁 `P2`）· `P3` 待解冻。

