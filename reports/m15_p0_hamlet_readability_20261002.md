# M15-P0 · Hamlet 可读性（布局判据口径 ＋ 左列垫片 ＋ 成长对比行 ＋ 收尾 HP% 读数）· 2026-10-02

> 提交 `401d5f8`（9 文件 · 287 insertions ／ 15 deletions）· 上一棒 `0f10221`（M12 ②）
> 约束：**最低限度必要性测试** · 每文件 ≤600 行 · 数值只登记不动 · 红线 17（读数纪律）／21（不留黑箱）／26（显示 == 真值）／28（≤600 行）
> 冒烟日志：`.tmp_smoke/hamlet_next_audit4.txt`（378 行 · exit=0）· 基线对照：`hamlet_next_audit.txt` ／ `audit2` ／ `audit3`

---

## 一、交付清单（9 文件）

| 文件 | 行 | 内容 |
|---|---|---|
| `darkest/scripts/ui/LayoutAudit.cs` | 402 | 重叠判据改【裁剪后绘制矩形】＋ 越界判据补滚动口径 ＋ 跳过数重复计数更正 ＋ 口径行自证 |
| `darkest/scripts/ui/HamletRoot.NavScroll.cs` | 105 | 左列两块读数常量垫片（上 72 ／ 下 30）＋ 幂等建垫 |
| `darkest/scenes/ui/hamlet_skeleton.tscn` | 331 | `EstateCostHotSpot/PurposeLabel` 顶对齐 ＋ 不纵向填充（消 2px 相交） |
| `darkest/scripts/gameplay/scene/ExpeditionContext.cs` | — | `LastRunStartLines`（同源只读展示）＋ `LastRunEndHpPercent`（摘会话同刻缓存） |
| `darkest/scripts/gameplay/sim/run/RunStartSnapshot.cs` | — | `DefaultBuildings` 改指 `HeirloomConfig.AllowedBuildings` ＋ `AverageHpPercentOf`（唯一算法） |
| `darkest/scripts/gameplay/scene/ExpeditionComposition.cs` | — | `Built.SortieIds`（英雄 id 口径）＋ `[H-1]` 逐人取阶读数 |
| `darkest/scripts/gameplay/scene/BattleRoot.FlowBridge.cs` | — | 传 `sortieIds` ／ `hpPercentLastRunEnd`（两个值只有这里有） |
| `darkest/scripts/ui/HamletRoot.Refresh.cs` | — | 城池【成长对比】行（懒建 · 3 行 · 真值 = `LastRunStartLines`） |
| `darkest/tests/SnapshotHpPercentTests.cs` | — | ＋1 条口径守卫（加权 ／ 取整 ／ 空队 null） |

---

## 二、五处改动

### 1. 重叠判据 = 【裁剪后绘制矩形】（`clip_contents` 语义）
被祖先容器**裁到空的像素没画在屏幕上** ⇒ 不该算「文字重叠」。`ClippedRect()` 沿祖先链求交，空 ⇒ 跳过并留痕。
⇒ Hamlet 重叠对 **3 → 0** 的直接原因（上一棒的 3 对里有 2 对是「滚出 `ScrollContainer` 的假重叠」）。
🔴 留痕口径：报告行「跳过裁剪外元素 N 个」（**红线 17 ／ 21：例外必须可审计**）。

### 2. 越界判据补滚动口径
`ScrollContainer` 内部的子项**超出即可滚动**是设计如此 ⇒ 不计越界（复用既存 `InsideScroll`，与 `tooBig` **同源**）。
🔴 本棒左列加垫片后新出现的「越界控件 2 个」**全部是滚动内容**（`BuildingNav` 880 高 ／ `Locked_blacksmith_armour` y=1084）⇒ 补口径后 **2 → 0**。
⚠️ 归因：这 2 条是**本棒引入的读数噪声**（上一棒是 0），**不是布局变化**。

### 3. 读数更正：跳过瞬态元素重复计数
原式 = 覆盖层跳过数 ＋ 全场景跳过数 ⇒ **同一棵子树被数两遍** ⇒ 已扣一份（只报全场景口径，覆盖层内数另列）。

### 4. Hamlet 左列垫片（读数常量，引擎内建 `Control` ＋ `MouseFilter=Ignore` 不吞点击）
| 垫片 | 值 | 出处（一手读数） |
|---|---|---|
| 上 | **72** | DD `town_screen_layout.button_navigation_pos` = **70,230** ⇒ nav 应从 y=230 起；我方实测首行 y=158 ⇒ 差 72（顺带消掉与 `Overlay/ActivityLogAnchor`（DD `activity_log_pos` 144,132）的 1px 真重叠） |
| 下 | **30** | 让开 `Overlay/EstateSummary`（DD `estate_summary_pos` 0,975 ⇒ 盒顶 975.24） |

＋ `hamlet_skeleton.tscn`：`EstateCostHotSpot/PurposeLabel` 改**顶对齐** ＋ 不纵向填充 ⇒ 消掉与 `EstateSummary/PurposeLabel` 的 **2px 相交**（DD 未给该热点 `pos` ⇒ 位置本就是推定，**色块不动不删**）。

### 5. 成长对比行（真值只有一处）
- `ExpeditionContext.LastRunStartLines`：缓存**与进地牢 `GD.Print` 同一条**读数 ⇒ 城池**只读展示**，**不重算第二份快照**（红线 26）
- `RunStartSnapshot.DefaultBuildings` 改指 `HeirloomConfig.AllowedBuildings`（`heirlooms.json` **五条**升级路径）⇒ 铁匠铺两条树进对比（此前写死三栋 ⇒ 玩家在铁匠铺花的传家宝**在对比里看不见**）；**单一来源**
- `AverageHpPercentOf`：口径 = **ΣHp/ΣMaxHp**（按血量加权）＋ `AwayFromZero` ＋ 空队 ／ ΣMaxHp=0 ⇒ **null**（不假装）—— 它是 `HpPercentLastRunEnd` 的**唯一算法**
- `ExpeditionContext.End()`：**摘会话的同一刻**缓存 `LastRunEndHpPercent`（`PreviousSession` 会被 `ConsumePreviousSession` **认领即清** ⇒ 只有这里读得到）
- `Built.SortieIds`：**英雄 id** 口径（与 `HeroConfig.Id` 同源）——**不能**拿 `ExpeditionSession.Roster()` 顶替（那本台账的键是**战斗单位 id**）
- `[H-1]` 逐人取阶读数：容器被重置成空表时，旧读数照样报「N 人已取阶」⇒ 分不出「容器活着」与「被清零」⇒ 本行给**逐人真值**

---

## 三、重叠 9 → 0 归因表

| 轮次 | 读数（Label ／ 重叠 ／ 越界 ／ 裁剪外） | 说明 |
|---|---|---|
| `hamlet_next_audit.txt`（本棒前） | 38 ／ **9** ／ 0 ／ — | 打印上限 6 条 ⇒ 只归因已打印的 6 条 |
| `hamlet_next_audit2.txt`（改判据后） | 36 ／ **3** ／ 0 ／ 2 | 3 条**全真**（假重叠已被裁剪口径剔除） |
| `hamlet_next_audit3.txt`（加垫片后） | 35 ／ **0** ✅ ／ 2 ／ 3 | 越界 2 = 滚动内容假读数 |
| `hamlet_next_audit4.txt`（补越界口径后） | 35 ／ **0** ✅ ／ **0** ／ 3 | 全绿 |

**逐条归因**（9 = 3 真 ＋ 6 假）：

| # | 对（audit 打印顺序） | 真/假 | 归因 |
|---|---|---|---|
| 1 | `BuildingNav/DDNav1_blacksmith/PurposeLabel`(24,177) ⟷ `Overlay/ActivityLogAnchor/PurposeLabel`(150,160) | **真** | nav 首行高于 DD 位置 ⇒ 上垫片 72 修 |
| 2 | `Locked_blacksmith_weapon`(18,962) ⟷ `LeftCol/ReliefHint`(18,948) | 假 | 滚出 `ScrollContainer` ⇒ 裁剪口径剔除 |
| 3 | `Locked_blacksmith_weapon` ⟷ `LeftCol/LastRunStartLines`(18,977) | 假 | 同上 |
| 4 | `Locked_blacksmith_weapon` ⟷ `Overlay/EstateCostHotSpot/PurposeLabel`(6,991) | 假 | 同上 |
| 5 | `Locked_blacksmith_armour`(18,1006) ⟷ `BottomBar/BottomRow/HamletResourceBar`(18,1030) | 假 | 同上 |
| 6 | `Locked_blacksmith_armour` ⟷ `Overlay/EstateSummary/PurposeLabel`(6,1007) | 假 | 同上 |
| 7 | `LeftCol/LastRunStartLines`(18,977) ⟷ `EstateCostHotSpot/PurposeLabel`(6,991) | **真** | 未打印（**上限 6**）⇒ 由 audit2 的第 2 对**反证**；下垫片 30 ＋ 顶对齐修 |
| 8 | `EstateSummary/PurposeLabel`(6,1007) ⟷ `EstateCostHotSpot/PurposeLabel`(6,991) | **真** | 未打印 ⇒ 由 audit2 的第 3 对反证；同上 |
| 9 | `Locked_blacksmith_armour` ⟷ `EstateCostHotSpot/PurposeLabel` | 假 | 未打印 ⇒ **推断**（唯一剩余相交候选；裁剪口径下它必然消失，与 audit2「假重叠归零」一致） |

📌 **反证链**：audit2 的 3 对**恰是**上表 3 条真重叠（R1 ／ R2 ／ R3）⇒ 「9 − 3 = 6 假」与「改判据后假重叠归零」**互相印证** ✓

---

## 四、读数

| 项 | 读数 |
|---|---|
| 编译 | `dotnet build Darkest.sln -p:DarkestTargetFramework=net10.0` ⇒ **0 错误 ／ 71 警告**（均既存） |
| 测试 | `dotnet test Darkest.Tests.csproj` ⇒ **814 通过 ／ 0 失败 ／ 0 跳过**（9 s） |
| 冒烟 | `--hamlet-next --ui-audit --fixed-fps 60 --quit-after 900` ⇒ exit=0 · 378 行 · 判据行 31 条 ⇒ **31 通过 ／ 0 未通过** ＋ 结论行「✅ 两条判据通过」 |
| 判据读数 | 可见 Label 35 ／ Panel+PC 51 · **重叠 0** · **透明框 0** · **越界 0** · **内容需求超相机 0** · 跳过瞬态 0 ／ 子窗口 0 ／ **裁剪外 3** |
| 功能读数 | `[HamletRoot] ✅ 成长对比行已显示（3 行；真值 = ExpeditionContext.LastRunStartLines，与远征日志同源）✓` · `[养成] 首趟基线` 行在 |
| 门禁 ×7 | 全 **rc=0**：`check_file_size` 564 ≤600 ／ `check_file_budget` **硬线 0** · **26 个 401~600 预警**（新增 `LayoutAudit.cs` **402**）／ `check_data_discipline --all` 三扫可疑 0 ／ `check_no_external_assets` OK ／ `check_godot_refs --root darkest` 0 hits ／ `check_ui_namespace` OK ／ `check_placeholders` PASS |

---

## 五、已知项与登记（如实 · 不改数值）

1. ⚠️ **`LayoutAudit.cs` 进入预警面（402 行）**：新增的口径注释 ＋ 判据自证行 ⇒ 已进 401~600 预警清单（600 硬线内）；下次动它优先拆「判据」／「口径 helper」两片。
2. ⚠️ **`--hamlet` ＋ `--hamlet-embark` 旗标组合会无限循环**（本棒未修 · 与 M12 同）；冒烟一律走 `--hamlet-next`。
3. ⚠️ **TextServer RID 泄漏 ／ `Failed to read the root certificate store`** = 引擎退出噪声 ／ 环境噪声（headless 既有，非本件引入）。
4. 📌 **裁剪外 2 → 3**：上垫片把 `Locked_blacksmith_armour` 推到 y=1084（视口外）⇒ 多裁 1 条；Label 35 = audit2 的 36 − 1，**数上自洽**。
5. 📌 **`O-113` 追加**：判据两处口径（裁剪 ／ 滚动）＋ 本棒读数更正 ⇒ 记入 `doc/architecture/open_issues.md`。

---

## 六、下一步（未做 · 不在本件范围）

- **M4u 饰品装 ／ 卸**（`O-112 ②`）：复用 `GearHeroSlot.cs` ／ `GearHeroSquare.cs` 的引擎内建拖放；内核 `Roster.Trinkets.cs` 接线后**必须**从 `tools/deadfunc_allowlist.txt` 拿掉。
- **P0 数据请单**：23 个技能 `dmg%`（一手 E 盘口径）——**零依赖**。
- **P1 ／ P2 ／ P3**：M1c 阶段 2 对照夹具 ／ 阶段 3 切默认（阻塞于数据请单）／ §39 解冻三项（阻塞于用户解冻）。
- **P6**：`state.md` → `doc/modules/*` → 报告三处同步（本件已做 `#506` ＋ `ui_spec §14.5.1` ＋ 本报告）。
