# `M6u` 建筑升级树全表 UI 落地（树 × 级 · 前置 · 多货币 · 可滚动 · 2026-10-02）

**施工单**：`doc/architecture/tasks/dd1_workstreams.md:51`（`M6u` 建筑升级树 UI —— 树 × 级（**`code` 不是数字 level**）· 前置显示 · 多货币 · 验收「每级 `prerequisite_requirements` 可见 ＋ `--ui-audit`」）

**用户约束**：每文件 ≤400 行目标 / 600 行硬线 · 可维护 / 可读 / 复用 · **尽量用 Godot 引擎内建工具** · **不造轮子** · 只允许最低限度的必要性测试

⚠️ 如实标注：本环境**无外网**（沙箱）⇒「上网找开源轮子 / MCP 工具」这一步在本机**做不到**；本件能给的证据 = **新增依赖 0** ＋ 用到的全是引擎内建（`ScrollContainer` / `VBoxContainer` / `Label` 的 `SizeFlags`）· **零自造控件** ✓

## 一、玩家可见行为（4 条玩家路径）

| # | 行为 | 入口 |
|---|---|---|
| ① | 城池 → 建筑区任意建筑弹窗 ⇒ 升级树位出现【树 × 级】全表：每树一行 `{tree_id}（N 级）`；每级一行 `· {code}｜花费 {type×amount ＋ …}｜前置 {tree_id·code ＋ …}` | `HamletRoot.BuildingLevels.cs::MountLevelTree`（**懒建 ＋ 幂等**） |
| ② | 行数多时可**滚动**（内建 `ScrollContainer`：水平 Disabled ／ 垂直 Auto） | `EnsureLevelTreeHost` |
| ③ | 无前置档位显式写「无（首档）」；数据无对应树 ⇒ 打印「没有对应树 ⇒ 空表（如实上报，不猜）」（红线 21） | `MountLevelTree` |
| ④ | 多货币花费逐条列出（`gold×0 ＋ portrait×2 ＋ crest×5` 之类） | 同 ① |

**所有数字从数据现算**：`darkest/data/buildings.json`（8 建筑 / 20 树 / 99 级）经 `BuildingsConfig.TreesFor` 直读 ⇒ UI **零写死**等级 / 花费 / 前置 ✓

## 二、代码改动（4 文件 · LF 无 BOM）

| 文件 | 行数 | 类型 |
|---|---|---|
| `darkest/scripts/ui/HamletRoot.BuildingLevels.cs` | 135 | 🆕 全表挂载 ＋ 滚动宿主（懒建） |
| `darkest/scripts/ui/HamletRoot.BuildingPopup.cs` | 317 | 改：树宿主 `HBoxContainer ⇒ VBoxContainer` ＋ `TakeAnchor` ＋ 回落 ＋ 挂载调用 |
| `darkest/scripts/ui/HamletRoot.cs` | 164 | 改：字段类型随宿主（`:73`） |
| `darkest/scripts/data/BuildingsConfig.cs` | 243 | 改：🆕 `TreesFor(id)`（先按建筑 id ⇒ 全树；否则按树 id ⇒ 单树；都没有 ⇒ `Array.Empty`） |

📌 **行数口径更正（2026-10-02 · 提交前复核 · 不静默改数）**：上表 4 个行数**初版按「LF+1」记**（136 ／ 318 ／ 165 ／ 244）⇒ 按项目口径【实际行数】更正为 **135 ／ 317 ／ 164 ／ 243**；口径依据 = `tools/check_file_size.py` 的 `count_lines`（`sum(1 for _ in fh)`）· 与门禁读数同源 ✓

`TreesFor` 的边界（`<summary>` 已写明）：**不发明 `code → 数字等级` 的映射**（红线 26）—— 只有 blacksmith 的 `a..d` 对 `Lv1..4` 有一手出处 ⇒ 其余按 `code` 原样展示 ✓

## 三、验收读数

### ① `--ui-audit`（`O-118` 同款命令 · 与基线对账）

```
E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe --path darkest --audio-driver Dummy --hamlet --hamlet-runs-seed=3 --hamlet-gold=5000 --hamlet-building=blacksmith.weapon --ui-audit --fixed-fps 60 --quit-after 400
```

- **16 次采样恒定**：可见 Label **62** ／ Panel+PC **72** ／ **重叠对 20** ／ 透明框 0
- 对账 `O-118` 基线（`#540`：59／72／19／0）⇒ **差 = +3 可见 Label ／ +1 重叠对**，其余判据全同（越界 0 · 跳过瞬态 0 · 跳过子窗口 0 · 内容需求超出相机 0）；`跳过裁剪外` 0 ⇒ **2**（滚动视口外的行）
- 全量臂 `--ui-audit-all`：20 对与已登记 19 对**逐对差 = 恰好 +1** ⇒ 新增 = `UpgradeLevelScroll/UpgradeLevelList/PopupLine (797,648) 192×23` ⟷ `UpgradePanel/BpUpgradeSlot/PurposeLabel (797,637) 90×18`（16 次采样均出现）
- **新增对定性 = 占位块 ⟷ 真内容同锚点**（同 `O-119` 家族 · **非本件引入的布局缺陷**）：`BpTreesAnchor` 180×120 @(172,454) 与 `BpUpgradeSlot` 102×72 @(172,454) 在 `building_popup.tscn:179/633` **同坐标**（`doc/UI_STRUCTURE_DECISIONS.md` 已登记两者同锚点）；后者 `PurposeLabel` 文本 =「升级图标 102x72」· tooltip 明标 DD `upgrade_requirement_layout.base_size`（**色块占位，不换不删**）⇒ 与 `O-118`／`O-119` 三条路裁决**一并收口**（本件不单独处置）✓
- 证据：`.tmp_m6u_acc1.txt`（前 6 条口径）／`.tmp_m6u_acc2.txt`（全量口径）· 解析器 `.tmp_parse_uiaudit.py`（未跟踪探针）✓

### ② 🔴 本件抓到并修的真缺陷：树表内层塌成 1px（文字被压成竖条）

- **症状**：首版读数 61／72／20／0（16 次采样恒定），新增对是 `UpgradeLevelScroll/UpgradeLevelList/PopupLine (797,648) size=(1,75)`
- **根因链**：`popup_line.tscn` **保留按词换行**（`autowrap_mode = 2`）而 **`clip_text` ／ `text_overrun_behavior` 已在 2026-10-02 的 1px 行高修复中删掉** ⇒ 引擎语义下 Label **最小宽 = 1**；`ScrollContainer` **不自动展开子节点** ⇒ 内层 VBox 只按**最小宽**被安置 ⇒ 实测 `list=(1,6919)`、每行 `size=(1,75)`（文字压成 1 列竖条）
- **修法（一行 · 引擎语义）**：`box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;`（`HamletRoot.BuildingLevels.cs:163` · 含根因注释）
- **三行读数（同一 tavern 臂 21 行）**：修前 `list=(1,6919)` · first=(1,75) ⇒ 修后（同帧）`list=(192,6919)` ⇒ **settled** `list=(192,1277)`、行高 23（单行）／49（两行）；删探针后终测（`blacksmith.weapon`）审计行 `PopupLine size=(192,23)` ✓
- 归因边界：`跳过裁剪外` 由历史 0 ⇒ **2** 是**同一条修法的副产品**（滚动视口外的行按 `clip_contents` 口径跳过 ⇒ 不再进重叠判据），**不是**独立缺陷 ✓

### ③ 多树臂（`--hamlet-building=tavern`）

- `M6u 升级树全表就位：tavern ⇒ 3 树 / 18 级`（与既有 DD 树行「节点 **3** 个」同数 ⇒ 交叉核对一致）· RC=0 · 无脚本错 ✓
- 证据：`.tmp_m6u_smoke2_tavern.txt`（未跟踪探针）✓

### ④ 构建 ／ 门禁 ／ 最低限度测试

- **构建**：`dotnet build darkest/Darkest.csproj -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` ⇒ **0 错**（73 条既有警告不变）
- **门禁 8 项全绿**（一键 `cmd /c .tmp_gates8.cmd` ⇒ RC1~RC8=0）：`file_size` **632 / 0 allowlist** · `file_budget` all ≤400 · `godot_refs` **0 hits** · `data_discipline --all` 三扫可疑 **0** · `no_external_assets` OK · `csproj_tfm` OK · `ui_namespace` OK · `placeholders` PASS
- **最低限度测试**（唯一消费点）：`dotnet test darkest/Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --filter "FullyQualifiedName~BuildingsConfig"` ⇒ **失败 0 ／ 通过 2 ／ 总计 2** ✓

## 四、偏差（deviation）

1. 🆕 **新增 API** `BuildingsConfig.TreesFor`（243 行内）—— 施工单没点名该 API，属实现选择；消费点 = `HamletRoot.BuildingLevels.cs`（**唯一**）✓
2. 🆕 **滚动宿主**（内建 `ScrollContainer` ＋ 内层 VBox）—— 场景 `building_popup.tscn` **未动**（`BpTreesAnchor` 原样承接）；宿主**懒建 ＋ 幂等 ＋ 自愈**（不持野引用：被拆 ⇒ 重建）✓
3. 🔴 **与 `O-119` 的交集**：`BpTreesAnchor` ⟷ `BpUpgradeSlot` 同坐标 ⇒ 新增 1 对重叠（定性见 §三①）；**不删场景占位**（受「色块占位，不换不删」约束）⇒ 待三条路裁决一并收口 ✓
4. 📌 **未逐建筑全跑 `--ui-audit`**：只跑 `blacksmith.weapon`（对账臂）＋ `tavern`（多树臂）⇒ 其余建筑按同一条挂载路径（`TreesFor` 唯一分支）推断，**未取证的部分如实标注** ✓

## 五、边界（本件动了什么 ／ 没动什么）

- **动了**：4 个代码文件（§二）＋ 本报告 ＋ `doc/state.md #545` ＋ `reports/INDEX_lead_programmer.md §7` ＋ `doc/modules/observe_list.md` 判据 (372)~(374) ＋ `doc/architecture/open_issues.md`（`O-118` 读数更新 ＋ `O-119` 补一行）
- **没动**：场景 `building_popup.tscn`（零改动）· 数据 `buildings.json`（零改动）· **数值只登记不动**（红线 17）· 无新增第三方依赖（不造轮子）✓
- 探针与日志（`.tmp_m6u_*`）**留本地不入库**（项目惯例）✓

## 六、提交与下一步（`#545`）

- 提交：4 文件**显式路径**（不用通配）· 消息写文件后 `-F`（LF 无 BOM）
- 下一步：`O-118`／`O-119` 三条路待裁（本件交集已登记）· `P5` 剩余 `M8u~M10u` · `P0` 等策划回件（解锁 `P2`）· `P3` 待解冻 · `observe_list` 从 **(375)** 起 ✓
