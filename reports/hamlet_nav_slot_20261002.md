# P6 续做：Hamlet `nav` 槽位缺陷修复（显式映射 ＋ 骨架按 DD index 排序 ＋ 两类未命中留痕）· 2026-10-02

**本件来源**：`M6u` 施工单 §4 ①（nav 邻接缺陷）· 承接 `P4`（H-1 收尾）剩余项之一 · 上一棒 `01cafb0`（`observe_list` 357~359）。

> 约束：**最低限度必要性测试**（不新增用例 · 不跑全量）· 每文件 ≤600 行 · 数值只登记不动 · 红线 17（读数纪律）／21（不留黑箱）／26（显示 == 真值）
> 交付 2 文件：`darkest/scripts/ui/HamletRoot.Build.cs`（391 行）· `darkest/scenes/ui/hamlet_skeleton.tscn`（331 行）

---

## 一、实测取证（先复现 · 再动代码）

命令：`cmd /c .tmp_nav_probe.cmd`（Godot console · `--path darkest --headless --quit-after 240 --hamlet --ui-audit` · APPDATA 隔离）⇒ 基线 `.tmp_nav_probe_pre.log`（88 行）

两条回落留痕（实测原文）：

| 行 | 原文 |
|---|---|
| `:58` | `[HamletRoot] nav：stagecoach 无 DD 槽（骨架未留）⇒ 直接追加到 nav 末尾（如实留痕）✓` |
| `:59` | `[HamletRoot] nav：blacksmith.armour 无 DD 槽（骨架未留）⇒ 直接追加到 nav 末尾（如实留痕）✓` |

### 三层根因（比施工单多两层）

| # | 层 | 事实（可复核） |
|---|---|---|
| ① | **键不对** | 旧 `switch` 键写 `"stage_coach"`，而本清单 id 是 `"stagecoach"`（`HamletRoot.Build.cs:206` 的 `upgradable[]` ＋ `:237` 的 `bId == "stagecoach"`）⇒ `ddIdx` 恒 −1 |
| ② | **名不对** | 旧写法用 `$"DDNav{ddIdx}_{bId}"` **拼节点名**；骨架节点名却是 `DDNav0_stage_coach` ／ `DDNav1_blacksmith`（下划线与段名都与 id 不同）⇒ 就算键改对**照样找不到** |
| ③ | **静默** | 槽位名有、节点却缺（`GetNodeOrNull` 返回 null）时旧写法**不打印** ⇒ `blacksmith.weapon` 是**静默回落**（违反「不崩不静默」） |

🆕 **新发现（兄弟顺序 ≠ DD index）**：`hamlet_skeleton.tscn` 里 `DDNav0_stage_coach` ／ `DDNav4_tavern` ／ `DDNav5_abbey` 三个空槽被**追加在文件末尾**；`VBoxContainer` 的**兄弟顺序 = 屏幕顺序** ⇒ 驿站／酒馆／修道院渲染在 `DDNav2/3/6/7/8/9` 等未接线占位**之下** ⇒ 左侧建筑栏视觉顺序与 DD index 不一致。

---

## 二、修法（2 文件 · 逐字 diff 摘要）

### 2.1 `darkest/scripts/ui/HamletRoot.Build.cs`（−13 ／ ＋24 · LF 无 BOM）

- `int ddIdx` ⇒ **`string? ddSlotName`**（唯一真相 = 显式映射表 `:276-283`）：

| `bId` | 骨架节点名 |
|---|---|
| `stagecoach` | `DDNav0_stage_coach` |
| `blacksmith.weapon` | `DDNav1_blacksmith` |
| `tavern` | `DDNav4_tavern` |
| `abbey` | `DDNav5_abbey` |
| `blacksmith.armour` | `null`（原版一栋两页签 ⇒ 无第二个槽 · 设计如此） |

- 取槽 `:285`：`PanelContainer? ubSlot = ddSlotName is not null ? buildingRow.GetNodeOrNull<PanelContainer>(ddSlotName) : null;`
- **两类未命中都留痕**：`ddSlotName is null`（`:296`）⇒ 原「无 DD 槽」行；`ubSlot is null`（`:302`）⇒ 🆕「槽位名 `X` 在骨架里找不到节点 ⇒ 回落追加到 nav 末尾（不崩不静默 ⚠️）」
- 头注补三层根因 ＋ 本报告引用（`:270-275`）

### 2.2 `darkest/scenes/ui/hamlet_skeleton.tscn`（纯节点块搬移 · 属性零改动）

| 槽 | 旧位置 | 新位置（当前行号） |
|---|---|---|
| `DDNav0_stage_coach` | 文件末尾 | `DDNav1` 之前 ⇒ `:87` |
| `DDNav4_tavern` | 文件末尾 | `DDNav3` 与 `DDNav6` 之间 ⇒ `:148` |
| `DDNav5_abbey` | 文件末尾 | 同上 ⇒ `:152` |

- `BuildingNav` 的 `theme_override_constants/separation = 12` 行尾加**改序留痕注释**（同文件既有惯例 · 说明「兄弟顺序 = 屏幕顺序」）
- 复核：搬移后 `DDNav0..DDNav9` 在文件里**升序**出现；`.tscn` `crlf 0 ／ lf 331 ／ bom none`；`.cs` `crlf 0 ／ lf 391 ／ bom none`；门禁 `tools/dsh/check_dd_layout.ps1` 的 `Pat = "DDNav9_statue"`（`:59` · `-SimpleMatch` **子串匹配** ⇒ 顺序无关）仍命中 ✓

---

## 三、读数（验收）

| 项 | 命令 | 读数 |
|---|---|---|
| 构建 | `cmd /c .tmp_build_full8.cmd` | **0 错 ／ 119 警告**（均既存）· 32.41 s |
| 冒烟 pre ／ post 差分 | `python -X utf8 .tmp_diff_logs.py .tmp_nav_probe_pre.log .tmp_nav_probe.log` | PRE 行种 **61** · POST 行种 **60** |
| 门禁 8 项 | `cmd /c .tmp_gates8.cmd` | **RC1~RC8 全 0** |

### 3.1 冒烟差分（归一化 `帧=N` 后比行集 · 差异只有两处且都是预期）

| # | 差异 | 判读 |
|---|---|---|
| ① | PRE `:58`「`nav：stagecoach 无 DD 槽`」**消失** | 驿站改走真槽 `DDNav0_stage_coach` ⇒ 回落行不再出现 ✓ |
| ② | 判据行（4 次 ＋ 1 条口径行）`可见 Label 34 ⇒ 33` | 酒馆改走真槽 ⇒ 其 `PurposeLabel` 占位被清掉 1 个 ⇒ **数上自洽** ✓ |

其余**全等**（仅 PRE 6 行 ／ 仅 POST 5 行 · 差异合计 2 类）。冒烟细节：

- `.tmp_nav_probe.log:57` 仍 `建筑 nav 套滚动宿主（内容最小高 668 > 可分配 ⇒ 纵向滚动；横向 Disabled）✓`（`HamletRoot.NavScroll.cs` 既有行为 · 未变）
- `:58` **仅剩** `nav：blacksmith.armour 无 DD 槽`（**设计如此**：原版一栋两页签 ⇒ 无第二个槽 · 见 `O-105`）
- `:59` `城池建筑：已解锁 5 ／ 5（起手只有 Stage Coach；已完成出征 3 趟；铁匠铺 = 2026-10-01 解冻窗口）`

### 3.2 门禁 8 项（`.tmp_g8_1..8.txt` · 本件重跑）

`file_size` **627 文件 ／ 0 超限 ／ 0 白名单** · `file_budget` **all files <= 400 lines** · `godot_refs` **0 hits**（含 `System.IO` 在 `scripts/ui`／`scripts/gameplay/scene` 0 命中）· `data_discipline --all` **三扫可疑 0**（内核 172 文件 ／ 字面量 1334 ／ 白名单 41 条）· `no_external_assets` **OK**（38 文本文件 ／ 0 豁免 token）· `csproj_tfm` **OK**（2 csproj 均取自 `$(DarkestTargetFramework)`）· `ui_namespace` **OK** · `placeholders` **PASS**（`out-of-viewport=0 ／ size-mismatch=0 ／ offset-at-root=0`）

### 3.3 未跑 `dotnet test`（如实登记 · 不是省略）

本件改动 = **UI 接线 ＋ 场景节点顺序**；`rg -n -- "HamletRoot" darkest/tests` 只命中 **2 条注释**（`HeirloomRunRewardWiringTests.cs:213` ／ `SmokeStepKindTests.cs:10`），`rg -n -- "nav" darkest/tests` **0 命中** ⇒ 无测试断言该路径。按用户约束「**最低限度必要性测试**」**不跑全量**；判据 = 构建 0 错 ＋ Godot 冒烟 pre ／ post 行集差分（§3.1）＋ 门禁 8 项全 0（§3.2）。

---

## 四、残留（既存债 · **非本次引入**）

`--hamlet --ui-audit` 判据仍 🔴（4 次全 🔴 · 重叠对 **1**）：

```
🔴 重叠：LeftColumn/LeftCol/ReliefHint pos=(18,1781) size=(140,23) ⟷ Overlay/EstateSummary/PurposeLabel pos=(6,1798) size=(410,18)
```

- **归因**：`ReliefHint` y=1781 落在**相机 1920×1080 之外的越界区**（`EstateSummary` 锚 0.903）⇒ `M15-P0` 的 30 下垫片不够。
- **处置**：登记 `O-115`（③ 项）· 待 `M15` 后续或解冻窗口。

---

## 五、下一件

- `P2`（`M1c` 阶段 3 切默认）／`P3`（§39 解冻三项）：**待用户解冻授权**（已提问 · 答复未到）。
- `P4`（H-1 收尾）：nav 邻接项**本件结清**；剩余 = 武器半（= `P2` 前置）。
- `observe_list` 下一批从 **(360)** 起（零依赖 · 范式 = `.tmp_observe357.py`）。
