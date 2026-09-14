# Godot 内置工具利用率审计（架构侧 · v1.32）

> **触发**：用户指令「现有架构是否充分利用 godot 内置工具？将设计架构时充分利用 godot 内置工具写入 skill，并根据内置工具优化现有架构」
> **性质**：**架构侧专项**（不是 UI 专项）—— 回答三件事：**审计现状 / 写进 skill / 给出优化**。
> **配套**：`skills/darkest-lead-programmer/SKILL.md` **附 B**（程序侧的实测审计 B.1 + 纪律 B.2）；本文件是**架构侧独立复核 + 补充**，两者**互补不重复**。
> **方法论**：**用计数取证，不靠感觉**（下表每行都可用 `Select-String` 复现）；**内核零 Godot 是刻意设计**，故"未使用"要分成 **🔴 该用没用（缺陷）** 与 **✅ 刻意自研（正确）** 两类。

---

## 1. 独立复核（`darkest/scripts` 全量计数，2026-09-14）

| 引擎能力 | 计数 | 判定 |
|---|---|---|
| **场景 `PackedScene` / `Instantiate`** | **0** | 🔴 **该用没用** —— `scenes/` 只有 4 个 `.tscn`（每 Root 一个）；`scenes/battle/prefabs/` **目录已建但空** ⇒ **预制件这条路已预留、没走** |
| **容器（`VBox/HBox/Grid/Margin/Center`）** | **2** | 🔴 该用没用（布局靠硬编码坐标） |
| **锚点 `Anchors`** | **0** | 🔴 该用没用 |
| **中央 `Theme`（`.tres`）** | **0** | 🔴 该用没用（**逐节点 `AddTheme*Override` 20 处** ⇒ 样式散落） |
| **`Tween` / `AnimationPlayer`** | **0** | 🔴 该用没用（**无动效** —— 而 DD 的"手感"主要来自动效） |
| **`AudioStreamPlayer`** | **0** | 🔴 该用没用（**没有任何声音**） |
| **`Control` 焦点 / 手柄导航（`GrabFocus`/`FocusNeighbor`/`FocusMode`）** | **0** | 🔴🔴 **该用没用（本表新增项）** —— **`InputMap` 只做了一半**：键位可重映射了，但**焦点导航/手柄没做** ⇒ **"可重映射"的收益没兑现**（手柄玩家仍然点不了） |
| **`SceneTree.Paused` / `ProcessMode`** | **0** | ⚠️ **本表新增项**：暂停/结算弹窗缺一套"**逻辑停、表现仍跑**"的机制（与 Tween/Audio 同批） |
| **`Performance.GetMonitor`** | **0** | ⚠️ **本表新增项**：**没有帧预算基线**（表现层性能无观测手段） |
| **`TileMap` / `TileSet`** | **0** | ⚠️ 地图用 `_Draw()`；**表现层地图视图**可改内置（内核保持自研，见 §3） |
| **`AStar` / `AStarGrid2D`** | **0** | ⚠️ 同上（**只在表现层**可用） |
| **`ShaderMaterial` / `CanvasItemMaterial`** | **0** | ⚠️ **本表新增项**：DD 式**描边/暗角/受击闪白**本可由材质实现（`ui_spec` 已有"深色粗描边"要求） |
| **`GPUParticles2D`** | **0** | ⚠️ 命中/死亡的即时反馈（与 Tween 同批） |
| **`RichTextLabel`（BBCode）** | **0** | ⚠️ 日志/详情无法双色/图标 |
| **`[Export]` / `[Tool]` / `EditorPlugin`** | **0** | 🔴 **该用没用**：`data/*.json` **没有可视化编辑/校验面板**；**但数据文件在编辑器里不可见 = 设计侧改一次要手改 JSON** |
| **i18n（`Tr()` / `TranslationServer` / `.csv`/POT）** | **0** | ⚠️ **本表新增项**：**文本全硬编码中文** ⇒ **长线必做**（且它反过来影响"锚点/容器/文本长度"的优先级） |
| **`push_error` / `push_warning`** | **0** | ℹ️ 内核抛 C# 异常是对的（零 Godot）；**表现层/组合根**可补 `PushError`（能在调试器里点进去、CI 能抓日志） |
| **`Signal`（自定义）** | **13** | ✅ 已用好（`Pressed +=` 等） |
| **`CallDeferred`** | **17** | ✅ 已用好（`_Ready` 内切场景的坑已固化） |
| **`InputMap` / `IsAction`** | **5** | ✅ **已落地**（`dd_restart` / `dd_toggle_log`；Esc 走引擎内置 `ui_cancel`） |
| 🔴 **`System.IO` 读文件** | **3** | 🔴🔴 **本表新增项 + 正确性风险**（见 §2） |
| **`FileAccess.GetFileAsString`** | **20** | ✅ 已用好（**导出安全**的正解） |
| **内核零 Godot（`core`/`sim`/`data`）** | ✅ `tools/check_godot_refs.py` 守卫 | ✅✅ **刻意设计，继续保持** |

---

## 2. 🔴🔴 本轮唯一发现的【正确性风险】（不在程序侧 B.1 表里）

**现象**：**同一件事有两套读法** —— 表现层 **20 处**用 `FileAccess.GetFileAsString("res://data/…")`（**导出安全** ✅），**但 `BattleUi.cs` 的 `ReadData()` 用 `System.IO`**：

```csharp
var dir = new DirectoryInfo(AppContext.BaseDirectory);
while (dir is not null) {
    string candidate = Path.Combine(dir.FullName, "data", name);
    if (File.Exists(candidate)) return File.ReadAllText(candidate);   // ← 磁盘路径
    dir = dir.Parent;
}
throw new FileNotFoundException(...);
```

**为什么这是架构级问题（不只是"风格不一致"）**：
```
① 导出构建里 data/*.json 被打进 PCK（res:// 不是磁盘目录）
   ⇒ File.Exists 找不到 ⇒ 🔴 throw FileNotFoundException ⇒ 【单场战斗入口在发行版直接崩】
② 它同时是一条【不该存在的第二读法】：
   表现层读数据必须走 FileAccess/ResourceLoader（Godot 唯一的"打包内读取"通道）
③ 与红线 21 同族：不是"有 UI 展示无消费"，而是【同一职责有两个实现】
```
**处置（架构裁定）**：
| 项 | 规定 |
|---|---|
| 🔴 **表现层读数据** | **一律 `FileAccess.GetFileAsString` / `ResourceLoader`**（**导出安全**）；**禁止 `System.IO`** |
| **内核读数据** | **允许 `System.IO`**（零 Godot 的代价）—— 但**只能由组合根把"字符串"喂进去**（内核不接触路径） |
| **组合根（`DirectorBridge` 等）** | 它是**唯一**允许"Godot 读文件 + 内核解析"相接的地方 ⇒ 这段适配代码**必须有且只有一份** |
| **取证** | `Select-String 'File\.ReadAllText|DirectoryInfo'` ⇒ **表现层命中数必须为 0**（内核命中可豁免，但不得出现在 `scripts/ui`、`scripts/gameplay/scene`） |

⇒ 登记 **`O-84`**（表现层 `System.IO` 读数据 ⇒ 导出崩溃风险）；**建议作为 `(i1)` 之后的下一个"补欠账"小片**（改动面：1 个函数 + 1 条 grep 门禁）。

---

## 3. 分层边界（架构的"该用/不该用"判定，写死）

```
┌─ 内核（scripts/core · scripts/gameplay/sim · scripts/data · tests）── 刻意【零 Godot】 ✅
│  理由（必须写进代码注释）：① 可 headless 跑蒙特卡洛 ② 可 xUnit 单测 ③ 可确定性复现
│  ⇒ 这里的"自研"（数学/事件流/RNG/解析/最短路/光照台账）**不是浪费，是分层的代价** ✅
│  ⇒ 🔴 例外条款：**若内核为"省事"而自研引擎已有能力（且不依赖 Godot）＝ 允许；
│      但若它【引用 Godot】＝ 立刻破坏分层**（`tools/check_godot_refs.py` 守着）
└─ 表现层（scripts/ui · scripts/gameplay/scene · scenes/）── 🔴 **必须优先用内置** ✅
   ⇒ 该用 Theme/Container/Anchors/Tween/Audio/PackedScene/RichTextLabel/TileMapLayer/
     ShaderMaterial/Particles/InputMap+Control 焦点/[Export] 而不用 ⇒ **算缺陷**（本审计 §1）
```

**五个判别问题（设计/评审时逐条问）**：① 引擎是否已有该能力？② 有没有**内置类型/节点/资源**可直接用？③ 有没有**内置编辑器工具链**（`[Tool]`/`[Export]`/`Theme`/`TileSet`/`EditorPlugin`）能让**非程序员**改？④ 我自研的版本能否被内置替代**而不破坏分层**？⑤ 若必须自研（内核为可测性/确定性）—— **理由是否写在注释里**？

---

## 4. 优化清单（按"收益/成本"排序，供排期）

| # | 优化 | 落点（Godot 惯例） | 收益 | 涟漪面 | 验收 |
|---|---|---|---|---|---|
| **①** | 🔴 **修 `BattleUi` 的 `System.IO` 读法** | `BattleUi.LoadNames()` → `FileAccess` | **消除导出崩溃** | **1 个函数** | `Select-String` 表现层命中 0 + 导出冒烟 |
| **②** | **中央 `Theme.tres` + 容器 + 锚点** | `resources/theme/*.tres`（**目录已存在、当前只有 `.gitkeep`**） | 分辨率/多语言无关；样式集中 | 全部 UI（**分批**） | 换分辨率/加长文本不错位 |
| **③** | **`Control` 焦点 + 手柄导航** | 各按钮 `FocusMode`/`FocusNeighborExplicit` | **兑现 `InputMap` 的收益**（手柄可玩） | 全部 UI | 纯键盘/手柄走通一条路径 |
| **④** | **`Tween`/`AnimationPlayer` 动效** | UI 出现/结算/受击 | DD"手感"① | UI + 战斗投影 | 逐条视觉验收（V11 视觉清单） |
| **⑤** | **`AudioStreamPlayer` 三类音效** | `resources/audio/` | DD"手感"② | 需美术/音源（**待策划**） | 命中/受击/结算有声音 |
| **⑥** | **预制场景（`PackedScene`）** | `scenes/battle/prefabs/`（**已建空目录**） | 复用件可视化编辑 | UI | 单位卡/技能格改为 `.tscn` |
| **⑦** | **`ShaderMaterial` 描边/暗角/闪白** | `resources/shaders/` | `ui_spec` 的"深色粗描边"由材质保证 | UI | 视觉 6 条中的描边/对比度 |
| **⑧** | **`[Tool]`/`[Export]` 数据校验面板** | `addons/` 或 `[Tool]` 脚本 | **非程序员可改数据 + 越界即时报错** | 低（只读 JSON） | 编辑器里改一个值立刻看到 Pxx 报错 |
| **⑨** | **`Performance.GetMonitor` 帧预算基线** | 调试面板（`F1` 已有 `dd_toggle_log`） | 表现层性能可见 | 低 | 冒烟里打印帧耗时 |
| **⑩** | **i18n（`TranslationServer`/`.csv`）** | `resources/i18n/` | 长线多语言；**反过来约束 ②** | 中（文本全量） | 至少一种语言切换可跑 |
| **⑪** | **`AStarGrid2D`/`TileMapLayer`（**仅表现层地图视图**）** | `scripts/ui` 地图视图 | 房间网格可视化/可达高亮 | 中（内核**不动**） | 地图视图与内核拓扑一致 |

### 🔴 §4.1 策划裁定（`#315` · `#319` · 2026-09-14）—— 四项口径已给 + 顺序调整

| 项 | 裁定 |
|---|---|
| **⑦ 材质描边** | ✅ **接受"用 `ShaderMaterial` 统一实现"**（理由：现 20 处逐节点覆盖 ⇒ **改一处要改 20 处**；而"深色粗描边"是**全局一致的视觉语言** ⇒ 材质正好）—— 🔴 **但"视觉规范"（描边宽度/颜色/暗角强度/闪白时长）仍属 `ui_spec §1.4`** ⇒ **材质只是它的一致实现**（不是新规范） |
| **⑩ i18n** | 🔴 **"M8.3 后做，但现在【必须】用容器 + 锚点"** ⇒ 📌 **一句话：i18n 的【文本】可以后补，但【布局方式】必须现在对**（多语言文本长度不同 ⇒ 硬编码绝对坐标必错位）⇒ **②③ 与 i18n 一起排 = 正确顺序** ✅ |
| **② ③ 顺序** | ✅ 确认（**Theme + 容器 + 锚点** 与 **焦点 + 手柄** 同批，因为都与"布局/可 i18n"耦合） |
| **④ 动效 / ⑤ 音效** | ⏸ **规格归 `ui_spec §12`**（策划侧文档）；架构侧照此实现（落点见本表 ④⑤） |
| 🔴🔴 **顺序调整（`#319` ⑦）** | **② Theme + 容器 + 锚点（布局基建）【优先做】** → 再 **④ 动效 / ⑤ 音效** → 最后 **⑦ 材质描边 / 字体 / 图标（素材路线）**；<br>**理由（策划）**：**① 它治的是"重叠"**（素材治不了）· **② 零授权风险、零素材依赖** · **③ 它是素材路线的前提**（**框都没立起来，贴素材只会更乱**）⚠️ |
| 🔴🔴 **可自动化验收（`#319` ⑤ · 架构记为【判据】）** | **判据 1**：遍历当前界面**所有可见 `Label` ⇒ 两两不相交**（`Rect2.Intersects` 全为假）· **判据 2**：**每个 `Panel` 的 `BgColor.a == 1.0`**（**"框不能透明"直接可断言**）⇒ ⚠️ **两条要遍历【每个界面】**（战斗 / 城池 / 角色详情 / 地图）⇒ 📌 **"没有重叠"从"看起来还行"变成【可测】** ⇒ 落 `tasks/next_round.md` §4 与 `tasks/ui_three_screens.md` 的验收 |

🔴 **纪律**：**②~⑪ 属"表现层补课"，一律不得反向要求内核改**（内核的最短路/连通性/光照台账**留在内核**）；**每轮做 UI 相关工作时，从本表挑一项落地**（一次一个轴，`#244`）。

---

## 5. 与程序侧 `附 B` 的关系

| | 程序侧 `skills/darkest-lead-programmer/SKILL.md` 附 B | **架构侧（本文件 + `blueprint` §9.16 + `skills/darkest-architect/SKILL.md`）** |
|---|---|---|
| 视角 | **实现**：怎么改代码、优先级 ①~⑥ | **架构**：**分层边界**、**该用/不该用的判定**、**红线**、**跨层风险**（导出安全） |
| 独有内容 | B.2 的落地优先级、`--input-audit` 取证 | 🔴 **`System.IO` 导出风险（`O-84`）** · **Control 焦点/手柄** · **`Performance`/Shader/Particles/i18n** · **预制件目录约定** · **五个判别问题** |
| 共同 | 同一张 B.1 表（我已独立复核一致） | 同左 |
