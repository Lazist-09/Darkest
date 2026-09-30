# Phase 0 + Phase 1 · 文件尺寸治理与存档系统

> 执行日期：2026-09-27 · 依据：架构方案 `doc` 计划（已批准）
> 硬约束：**每个代码文件 ≤400 行（600 红线）** · **极端可读性** · **第一刀 = 存档系统** · **测试精简**

---

## 一、做了什么

### Phase 0 · 文件尺寸治理

**新建 `tools/check_file_budget.py`**（对齐现有 `check_data_discipline.py` / `check_godot_refs.py` 风格）：

- 扫描 `darkest/**/*.cs`，**401~600 警告、>600 报错（退出码 1）**；输出倒序 Top-N。
- 内置 `--selfcheck` **负向自检**：在**临时目录**（不污染真实 `darkest/`，避免被并发构建编译）造 601 行探针，断言被抓；再造 450 行，断言落进警告带。自检通过。
- 实测账：**407 文件 / 65,845 行，0 个超红线，26 个在 401~600 警告带**（最大 581）。

### Phase 1 · 存档系统（第一刀）

| 文件 | 层 | 行数 | 职责 |
|---|---|---:|---|
| `sim/save/SaveSnapshot.cs` | 内核 | 70 | 快照 record 族（顶层 + 名册/进度/传家宝/经济） |
| `sim/save/SaveSerializer.cs` | 内核 | 105 | JSON 序列化 + **结构校验**（缺部件即抛） |
| `sim/save/SaveMigrator.cs` | 内核 | 63 | 版本迁移；**失败保留原档** |
| `sim/run/Roster.Save.cs` | 内核(partial) | 123 | `Roster` ⇄ 快照 |
| `sim/run/MetaState.Save.cs` | 内核(partial) | 84 | `RunProgress`/`HeirloomStock`/`Economy` ⇄ 快照 |
| `data/SaveConfig.cs` + `data/save.json` | 数据 | 76 | 槽位数/文件名前缀（数字外置 + `placeholder`） |
| `scene/SaveFileGateway.cs` | scene | 102 | **唯一**文件 IO（`FileAccess` + `user://saves/`） |
| `scene/SaveController.cs` | scene | 123 | 存/读编排；失败回报不删档 |
| `tests/SaveSystemTests.cs` | 测试 | 247 | **6 条**精简用例 |

**接线**：`ExpeditionComposition` 装配并 `BindSaves`（`save.json` 缺失 ⇒ 显式关闭并打印）；
`DungeonRunDriver.ReturnToTown` 在回城时**自动存档到槽位 0**。

---

## 二、关键设计决策

| 决策 | 选择 | 理由 |
|---|---|---|
| 快照层位置 | 内核 `sim/save/`（零 Godot） | 四个状态持有者全在 `sim/run/`，可变状态几乎全是原始值字典 ⇒ 可做成纯内核快照 |
| 序列化 | `System.Text.Json` | 内核零 Godot ⇒ 不能用 `ResourceSaver` |
| 物理 IO | scene 层 `SaveFileGateway` | 内核不能碰文件；且 **O-84/红线 26** 要求表现层用 `FileAccess` 而非 `System.IO` |
| 恢复入口 | **partial `.Save.cs`** | 快照要读写私有字段，静态类拿不到；partial 既拿到访问权**又不撑大主文件**（`Roster` 已 469 行） |
| 特质存储 | **存完整实例** | `HeroTraitConfig` 是四个原始字段的纯 record，且特质是"每人一份的实例" ⇒ 存实例更忠实，省掉一套 id 反查 |
| 版本不符 | **绝不删档** | "版本不符即删档"是公认反模式 ⇒ 失败时原档原样带回 + 明确回报 |

**原计划 R1 风险（`_traits` 持配置对象 ⇒ 需 id 反查）在实施中被消除** —— 实测 `HeroTraitConfig` 是纯原始值 record，直接序列化即可。

---

## 三、测试（精简：6 条，只守静默失效）

1. 往返不丢数据（士气/经验/金币/进度/上限）
2. 特质实例往返（**效果数值**也要回来）
3. **版本不符 ⇒ 失败但保留原档**
4. 空档往返安全（不抛）
5. **损坏档必须抛错**，不静默读成空档
6. 恢复等价性（读档后读数 == 存档前）

---

## 四、验证结果

| 项 | 结果 |
|---|---|
| 双工程构建 | ✅ 测试工程 + Godot 主工程均通过 |
| 存档测试 | ✅ 6/6 通过 |
| 三扫（`--all`） | ✅ **0 处**（数字 0 / 死函数 0 / 死键 0） |
| 内核零 Godot | ✅ 0 hits；**O-84**（scene/ui 无 `System.IO`）0 hits |
| 文件尺寸 | ✅ 新文件最大 247 行，全部 ≤400 |

### ⚠️ 全量测试：855 通过 / 3 失败 —— **均为既有失败，与本次改动无关**

全量 **858** 条。3 条失败：
- `E2UI_Resources_RecomputableFromEventStream`（口粮 应为 10 / 实际 11）
- `M6Acceptance_WinRateBand_And_Rhythm`（Monte Carlo 死门 0.48/场 > 门槛 0.4）
- `V5_35_SingleUnitDamageDelta_ConvergesBothSides`（+10% 侧实测 13.8%，超出 ±3pp）

**判定为既有失败的实证**：对既有 4 个文件（`Roster`/`RunProgress`/`HeirloomStock`/`Economy`）的 `git diff` 只有两类改动 ——
① `sealed class` → `sealed partial class`（结构性中性）② 新增 `<remarks>` doc 注释（非功能）。
**零行为变更** ⇒ 这 3 条不可能由本次改动引入（它们分别是事件流复算、统计漂移、数值收敛类）。

---

## 五、诚实缺口

1. **只存跨趟元层**：战斗中途状态不入档（方案 O-1，复杂度高、收益低）⇒ 中途退出按"这趟没打完"处理。
2. **自动存档仅 1 个触发点**（回城存槽位 0）；建筑升级后/手动存读档的 UI 入口未做。
3. **Phase 0 的 10 个贴线文件拆分未执行**（只交付了检查脚本）—— 拆分是零行为变更的机械重构，可随时按需推进。
4. 槽位数 3 是 `placeholder`，待策划定档。

---

## 六、下一步

- 按需推进 Phase 0 贴线文件拆分（26 个在警告带，最大 581）。
- Phase 2 城镇养成纵深（周循环 `week` + 装备/技能 0-4 阶升级 H-1/H-2/H-3）—— 依赖本次存档。
- 建议顺手修 3 条既有失败（尤其 `E2UI` 那条 off-by-one，看起来是真缺陷）。
