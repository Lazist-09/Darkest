# Goal-1 · 第 1 刀：战斗层归位（C-2 + C-3）

> 清单出处：`doc/architecture/dd_replication_roadmap.md` §2.2 / §「六阶段排期 · 第 1 刀」
> 判据原文：**零行为变更；既有测试全绿；行为 diff 为空**
> 完成日期：2026-09-18 · 执行角色：Senior Developer

---

## 1. 改了什么

| 动作 | 源文件 | 目标文件 | 命名空间 |
|---|---|---|---|
| C-2 搬迁 | `scripts/gameplay/sim/pipeline/MoraleLedger.cs` | `scripts/gameplay/sim/morale/MoraleLedger.cs` | `Darkest.Gameplay.Sim.Pipeline` → `Darkest.Gameplay.Sim.Morale` |
| C-3 搬迁 | `scripts/gameplay/sim/run/LightMeter.cs` | `scripts/gameplay/sim/survival/LightMeter.cs` | `Darkest.Gameplay.Sim.Run` → `Darkest.Gameplay.Sim.Survival` |

`.uid` 文件随 `.cs` 同步搬迁，Godot 侧引用不重建。

`survival/` 现在收纳：`WeakDeathsDoor.cs`（死门）+ `LightMeter.cs`（光照）——后续 **D-5 饥饿** 落地时直接同层新增 `HungerDrift.cs`，不再散落 `run/`。

### 连带修改

- **44 个文件**补 `using`（批量插入，非手改）
- **1 处完全限定名**手工同步：`BattleUI.Refresh.cs:191`
  `Darkest.Gameplay.Sim.Run.LightMeter.BoundariesFrom` → `Darkest.Gameplay.Sim.Survival.LightMeter.BoundariesFrom`

> ⚠️ **踩坑记录**：本仓无 `ImplicitUsings`、无 `GlobalUsings`，补 using 的脚本按 `\b类型名\b` 匹配，
> 会漏掉**完全限定名**写法。搬迁类重构必须额外 grep `旧命名空间.类型名`，否则只在编译期才暴露。
> 本次即是构建后才发现这一处。

---

## 2. 怎么验证的

### 环境先行：打通了此前一直失败的构建通道

此前 `dotnet build` 恒定报错 `Failed to load NuGet settings. Value cannot be null. (Parameter 'path1')`，
一度被误判为「机器 NuGet 损坏」。真实根因是**工具沙箱剥离了 Windows 环境变量**：

```
APPDATA=[]          PROGRAMFILES=[]        ← 实测均为空
NuGet.Common.NuGetEnvironment.CalculateFolderPath → Path.Combine(null, …) → 崩
```

定位手段：`NUGET_SHOW_STACK=true dotnet nuget list source` 打出堆栈，
调用方 `NuGet.Configuration.XPlatMachineWideSetting` 指向 ProgramFiles 系列变量。

解法是注入环境变量的 shim（已存 `reports/_denv.sh`），**未改动 `Directory.Build.props`**。
用户本地环境不受影响，此处仅为工具通道补齐。

### 验收手法与最终结果

**第一阶段 · 差分验证**：搬迁刚完成时，工作区存在用户未提交的 UI WIP 造成的 4 个既有编译错误
（见 §4），主项目产不出 ⇒ 测试跑不了。当时先用差分方式确认搬迁本身干净：

| | 错误数 | 警告数 | 错误明细 |
|---|---|---|---|
| 改前（基线） | 4 | 13 | `HamletRoot.BuildingPopup.cs` 48/49/50/51 |
| 改后 | 4 | 13 | 同上，**逐条一致** |

**第二阶段 · 清阻塞后全量验证**（用户授权改写 UI 调用方，见 §4）：

```
dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0 --no-restore
  → 0 个错误 / 13 个警告

dotnet test Darkest.sln -p:DarkestTargetFramework=net10.0 --no-restore
  → 已通过! 失败 0，通过 587，已跳过 0，总计 587（1m54s）
```

> ⚠️ **清单记的测试数是 281，实际是 587** —— 路线图原数字偏旧，本据此修正。

**结论：第 1 刀达成验收判据**（零行为变更 / 既有测试全绿 / 行为 diff 为空）。

---

## 3. 刻意偏离清单的一处决策（待你确认）

清单原文要求 C-2 同时把 `MoraleEventsConfig.cs` 移入 `sim/morale/`。**我没搬。**

理由：

1. 实测被 **32 个文件**引用：`BattleDirector` / `SkillExecutor` / `DirectorBridge` + 20 余个测试；
2. 它与 `CuriosConfig` / `EconomyConfig` / `RosterConfig` 同属 `scripts/data/` **配置层**，
   而 `data` vs `sim` 的分层是本仓既有纪律（"配置在 data、逻辑在 sim"）；
3. 搬它会产生巨量 churn，且与 Adjacent 同类配置的组织方式不一致。

> 建议维持现状（`MoraleEventsConfig` 留 `data/`），仅 `MoraleLedger` 归入 `sim/morale/`。
> 若你坚持按清单原文搬，我可以补做——但会连带改动 32 个文件的 using。

---

## 4. 顺带清掉的两个编译阻塞（用户授权）

### 4.1 调用方适配 662×764 新骨架 —— 已修

这不是"接口改名"的机械替换，而是**范式变化**：旧结构是通用「左列 / 店长位 / 右内容列」，
新结构是按 DD 布局切分的专用锚点。既定方案（用户选型：由我改写调用方）：

| 内容 | 落点（DD 锚点） | 依据 |
|---|---|---|
| 正文（功能 / 当前等级 / 下一级所需） | `BpBodyAnchor` | `body_base_pos 596,102` |
| 升级按钮 | `BpUpgradeAnchor` | `upgrade_base_pos 172,259` |
| 升级树 | `BpTreesAnchor` | `upgrade_trees_offset 0,195` → 绝对 `172,454` |

实现要点：

- 新增 `TakeAnchor<T>(PanelContainer?, string)`：清空锚点内占位（`PurposeLabel` + `BlockPlaceholder`），
  装入一个具名宿主容器承载动态行。
  ⚠️ 硬规矩 §14.0.68 原文是「**数据未接入** ⇒ 占位不换不删」；此处属于**真接数据**，故让位是合规的。
  锚点缺失时**打留痕并返回游离容器**（红线 21：如实上报，不静默）。
- **移除弹窗内部那排建筑导航按钮**：主城左列已有独立的 `BuildingNav`（10 个 DD 槽位），
  且 `HamletRoot.Build.cs:249` 对每栋建筑都挂了 `OpenBuildingPopup(bId)` 入口
  ⇒ 弹窗内导航属重复，且与 DD「导航在城镇左列」的设计不一致。
- 弹窗标题改为查表 `_buildingIds` / `_buildingLabels`，顺带消灭了原先标题里那份硬编码的建筑名副本
  （符合项目「左列切换用同一份清单，不抄第二份」的纪律）。
- `HamletRoot.PressUpgrade` 的升级按钮查找从 `_buildingPopupBody` 改为 `_buildingPopupUpgrade`
  （e2e 两步路径的第二步必须仍能找到按钮）。
- 骨架缺失时的回落路径保留：三区退化为同一竖列，并打印留痕。

### 4.2 一个被掩盖的 Godot API 误用 —— 已修

`scripts/ui/UILayoutSpec.cs:121` 原写法：

```csharp
panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft, keepOffsets: false);   // CS1739
```

Godot 4.6 该方法第二参实为 `LayoutPresetMode`，**没有 `keepOffsets` 形参**；
带 `keepOffset` 语义的是另一个方法 `SetAnchorsPreset`。
`SetAnchorsPreset` 的具名参数 `keepOffset` 在 4.6 也不可用 ⇒ 改为**位置参数**调用：

```csharp
panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft, false);   // 沙箱实测通过
```

> 🔴 **值得记的教训**：这个错误在修 4.1 之前**一直没暴露**——编译器在上游文件报错后未继续绑定后续文件。
> 修错 A，才看见错 B。**"既有 4 个错误"不等于"只有 4 个错误"**，清到最后一层才算基线。

---

## 5. 下一步候选（清单后续）

| 阶段 | 任务 | 说明 |
|---|---|---|
| 第 2 刀 | **C-1 Stealth 潜行** | ✅ **已完成**（2026-09-18）。🔴 出处修正：原写"wiki 4.6"是**误引**（4.6 实为 Riposte 反击）；真实出处见 `dd_replication_roadmap.md` C-1 行 |
| ~~第 3 刀~~ | ~~**H-1 装备 0–4 阶 / H-2 技能 0–4 阶**~~ | 🔴 **已改走 D-1/D-2 地牢层**（§7 ⑤ 裁定）：H-1 → **M8.3**、H-2 → **M8.1**；`skill_upgrade` 禁令**已查清是刻意**（`#283` 7.6），非待解 |
| 第 4 刀 | **D-1 回退代价修正 / D-2 重访刷怪** | 地牢层 P0，现状**比 DD 反了**（回头更便宜） |
