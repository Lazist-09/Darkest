# UI 图标资产需求清单（`#332`）

> **触发**：用户问「**UI 要的数据包呢？**」⇒ 核实工程现状：**`*.png = 0`，全工程没有任何图标资产** ⚠️
> 而 `ui_spec §13.3` 的资源清单里，**"图标"是唯一还标着"⬜ 未使用"的一档** ⇒ 🔴 **这就是缺的那个包**。
> 🔴 **纪律**：来源必须**授权清晰**（`ui_spec §13.3` / `assets_credits.md` A1~A4）—— **不得用 DD 的素材**（红线 27）。

---

## 0. 现状（**实测**）

| 类 | 工程内 | 状态 |
|---|---|---|
| **字体** | `EBGaramond.ttf` + 7 个 `NotoSerifSC-*.otf` | ✅ **已落地** |
| **着色器** | `vignette.gdshader`（暗角） | ✅ |
| **主题 / 调色板** | `dd_theme.tres` · `ui_palette.tres` | ✅ |
| **音效** | 程序生成占位音（`ui_spec §12.2`） | ✅ **不需资产** |
| 🔴 **图标 / 贴图** | 🔴 **`*.png = 0`** | 🔴 **缺** |

---

## 1. 🔴 需要的图标（**按界面分组，共 5 组 · 约 55 个**）

### 组① 状态 / buff 角标（**战斗屏，最紧急** —— 现在全是文字）

| # | 语义 | 建议来源（`game-icons.net` 搜索名） | 必需 |
|---|---|---|---|
| 1 | **虚弱** | `broken-bone` / `weakness` | ✅ |
| 2 | 🔴 **死门** | `death-skull` / `heart-beats` | ✅ **必显** |
| 3 | **折磨** | `psychic-waves` / `crying` | ✅ |
| 4 | **美德** | `holy-grail` / `sun` | ✅ |
| 5 | **流血** | `bleeding-wound` / `blood-drop` | ✅ |
| 6 | **瘟疫（Blight）** | `poison-bottle` / `virus` | ✅ |
| 7 | **眩晕** | `stun` / `dizzy` | ✅ |
| 8 | **减益** | `broken-shield` / `arrow-down` | ✅ |
| 9 | **位移** | `push` / `swap` | ⬜ |
| 10 | **护盾** | `shield` / `shield-reflect` | ✅ |
| 11 | **守护（guard）** | `shield-wall` / `body-swapping` | ✅ |
| 12 | **嘲讽（taunt）** | `shouting` / `bull-horn` | ✅ |

**技能/buff 增减（用同一套箭头 + 着色区分 ⇒ 省图标）**：
| 13 | 伤害 ± | `sword-wound` / `slash` | ✅ |
| 14 | 命中 ± | `targeting` / `crosshair` | ✅ |
| 15 | 暴击 ± | `critical-hit` / `explosion` | ✅ |
| 16 | 速度 ± | `wingfoot` / `running-shoe` | ✅ |
| 17 | 防御 ± | `shield` / `armor-vest` | ✅ |

### 组② 资源 / 物资（**全界面通用**）

| # | 语义 | 建议 | 必需 |
|---|---|---|---|
| 18 | **金币** | `two-coins` / `coins` | ✅ |
| 19~22 | **传家宝 ×4**（🔴 **具体 4 种见 `heirlooms.json`**） | `gem` / `cut-diamond` / `crown` / `family-tree` | ✅ |
| 23 | **柴火** | `campfire` / `torch` | ✅ |
| 24 | **口粮** | `meat` / `bread` | ✅ |
| 25 | **支援箱**（crate） | `wooden-crate` / `chest` | ✅ |
| 26 | 🔴 **支援包**（pack） | `backpack` / `knapsack` | ✅（`#329` 刚给它产出源） |

### 组③ 城池建筑（**Hamlet，3 个**）

| # | 语义 | 建议 | 必需 |
|---|---|---|---|
| 27 | **Tavern**（减压·快而不稳） | `beer-stein` / `wine-bottle` | ✅ |
| 28 | **Abbey**（减压·慢而稳） | `church` / `prayer` | ✅ |
| 29 | **Stage Coach**（招募） | `stage-coach` / `horse-head` | ✅ |

### 组④ 界面功能（**顶栏 / C 区 / E 区 / 底栏**）

| # | 语义 | 建议 | 必需 |
|---|---|---|---|
| 30 | **详情** | `info` / `magnifying-glass` | ✅ |
| 31 | **日志** | `scroll-quill` / `book` | ✅ |
| 32 | **序列** | `clockwise-rotation` / `hourglass` | ✅ |
| 33 | **编成** | `swap-bag` / `organigram` | ✅ |
| 34 | 🔴 **地图** | `treasure-map` / `folded-map` | ✅ **必显** |
| 35 | **背包** | `backpack` | ✅ |
| 36 | **扎营** | `campfire` | ✅ |
| 37 | **撤退** | `run` / `exit-door` | ✅ |
| 38 | 🔴 **提亮（火把）** | `torch` / `flame` | ✅ **光照计的核心操作** |
| 39 | **侦察** | `eye` / `binoculars` | ✅ |
| 40 | **攻击** | `crossed-swords` | ✅ |
| 41 | **治疗** | `healing` / `health-increase` | ✅ |
| 42 | **换位/移动** | `swap` / `arrows-left-right` | ✅ |
| 43 | **待命** | `pause` / `hourglass` | ✅ |
| 44 | **增援** | `reinforce` / `person` | ✅ |

### 组⑤ Curio（**6 个，地图格图标 + 交互面板**）

| # | 语义 | 建议 | 必需 |
|---|---|---|---|
| 45 | **补给箱** | `wooden-crate` | ✅ |
| 46 | **废弃营地** | `abandoned-camp` / `tent` | ✅ |
| 47 | **壁灯** | `wall-light` / `sconce` | ✅ |
| 48 | **书堆** | `bookshelf` / `book-pile` | ✅ |
| 49 | **圣坛** | `stone-altar` / `shrine` | ✅ |
| 50 | **骸骨堆** | `bone-pile` / `skull` | ✅ |

**地图格类型图标（`dungeon_view.md` §3，三期要用）**：
| 51 | **战斗格** | `crossed-swords` | ✅ |
| 52 | **障碍** | `wall` / `brick-wall` | ✅ |
| 53 | **陷阱** | `spider-web` / `wolf-trap` | ✅ |
| 54 | **隐藏房** | `star` / `secret-book` | ✅ |
| 55 | **队伍位置（火把）** | `torch`（同 38） | ✅ |

---

## 2. 🔴 来源与授权（**下载前必读**）

| 来源 | 授权 | 要求 | 备注 |
|---|---|---|---|
| 🔴 **`game-icons.net`** | **CC BY 3.0** | 🔴 **必须署名到【作者】**（不是只写站点） | **首选**（5000+ 图标，风格统一） |
| **`Kenney.nl`** | **CC0** | 无需署名 | 备选（风格偏卡通，与本作暗黑风**不太搭**）⚠️ |

🔴 **纪律（`ui_spec §13.3`）**：
```
① **每个图标必须登记【作者 + 授权】到 `doc/assets_credits.md`**（CC BY 要求署名到作者）⚠️
② **不得使用"授权不明"的图标**（包括"网上找的"）
③ 🔴 **不得用 DD 的任何素材**（红线 27）
📌 **`game-icons.net` 的署名格式**：`<图标名> by <作者名> (game-icons.net), CC BY 3.0`
   ⇒ 建议在 credits 里按【作者】分组列（一个作者可能贡献多个图标）✅
```

---

## 3. 🔴 放置与规格（**给 UI 设计师的实现口径**）

```
· **目录**：`darkest/resources/icons/<组名>/`（如 `icons/buffs/` · `icons/resources/` · `icons/ui/`）
· **格式**：🔴 **SVG 优先**（`game-icons.net` 原生就是 SVG ⇒ **矢量、任意缩放不糊、体积小**）✅
  ⚠️ Godot 4 需要 SVG 导入插件或用 `ImageTexture` 预渲染 —— 若走不通 ⇒ **PNG 512×512 透明底** ✅
· **颜色**：🔴 **单色（白）+ 运行时着色** —— 因为 `ui_spec §14.4` 的**四色**（近白/金/危险红/弱化灰）
  要能套在同一个图标上 ⇒ **不要下载彩色版** ✅
· 🔴 **命名**：**用语义名**，不用来源名（`icon_bleed.svg` 而不是 `bleeding-wound.svg`）
  ⇒ 理由：**换来源时不用改代码** ✅（与 UI 设计师那条"别按文件名猜资产"同一族）
```

---

## 4. 🔴 优先级（**按"玩家现在最看不到什么"排序**）

```
🔴 P1 = 组① 状态/buff 角标（12~17 个）—— 战斗屏现在【全靠文字】，而状态角标是"必显"的
🔴 P2 = 组② 资源图标（9 个）—— 底栏资源条现在【只有数字】
🔴 P3 = 组④ 界面功能（15 个）—— 页签/按钮现在【只有文字】
   P4 = 组③ 城池建筑（3 个）· 组⑤ Curio + 地图格（11 个）
```

---

## 5. 🔴 一句话

```
🔴 **UI 缺的是【图标资产】**（`*.png = 0`），而**字体/主题/着色器/音效都齐了** ✅
⇒ **下一步**：用户或 UI 设计师按 §1 的清单去 `game-icons.net` 取（**单色 SVG**），
   **放进 `resources/icons/`**，**并把作者/授权登记到 `assets_credits.md`** ✅
```
