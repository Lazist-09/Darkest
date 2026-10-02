# `--ui-audit` 全界面 19 对重叠 · 定性登记（2026-10-02）

> **一句话结论**：`--hamlet --hamlet-building=blacksmith.weapon --ui-audit`（**无覆盖层 ⇒ 审全界面**口径）读到的
> **19 对重叠**里，已能逐条看的前 6 对 = **5 对**「有明文出处的占位块 / DD 原设计溢出块」＋ **1 对**「分层遮挡」
> （下层状态栏被弹窗面板盖住）⇒ **不是布局缺陷**，而是**判据缺一层「被不透明祖先完全遮住 ⇒ 不算重叠」口径**。
> **本件只登记定性、不改码**；剩余 13 对（报告只打印前 6 对）**未逐对核** ⇒ **未收口**（§4 两条路待裁）✓

---

## §1 读数（两臂 · 可复跑）

### A 臂 · 全界面口径（本次读数）

```
E:\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe --path F:\GithubPro\Darkest\darkest --audio-driver Dummy --hamlet --hamlet-runs-seed=3 --hamlet-gold=5000 --hamlet-building=blacksmith.weapon --ui-audit --fixed-fps 60 --quit-after 400
```

（`$env:APPDATA` 指向隔离目录 `.tmp_godot_appdata` ⇒ 不动玩家存档 ✓）

- **16 次采样读数恒定**（`帧=24…384`）：可见 Label **59** ／ Panel+PC **72** ／ **重叠对 19** ／ 透明框 0 ⇒ 🔴 未通过
- 同轮其它判据全绿：越界控件 **0** ／ 跳过瞬态 **0** ／ 跳过子窗口 **0** ／ 跳过裁剪外 **0** ／ 内容需求超出相机 **0**
- ⚠️ **报告只打印前 6 对**（`LayoutAudit.cs:63` 的 `if (overlaps <= 6) // 报告前几条即可（防刷屏）`）⇒ 剩余 13 对**只有计数没有名字** ✓
- 证据：`.tmp_audit_bare.log`（223 行 · RC=0 · 未跟踪探针）

### B 臂 · 覆盖层口径（旁证 · **非严格对照**）

`.tmp_smoke_hamlet.cmd`（同族场景 ＋ `--hamlet-menu` ／ `--hamlet-popover` 等）⇒ 判据自动缩到覆盖层 `ProvisionPopup`：

- 读数：可见 Label 32 ／ Panel+PC 4 ／ **重叠对 0** ／ 透明框 0 ⇒ ✅ 通过（24 次采样）
- ⚠️ **两臂不是同一开集**：B 臂「全场景：可见 Label 91 ／ Panel+PC 77」≠ A 臂 59 ／ 72（B 臂多开了菜单/浮层）
  ⇒ 只能作「**范围一变，数就变**」的旁证，**不能**当作严格 A/B 对照 ✓
- 🔴 顺带留痕（既有口径自问）：B 臂口径行自己就打印「⚠️ 在审范围外还有 59 个 Label ／ 73 个 Panel（须判定：真被遮住 还是 漏审）」
  ⇒ 「被遮住 vs 漏审」**本来就是已承认的未决问题** ✓

## §2 前 6 对明细（A 臂 · 原样）

| # | 控件 A | 控件 B |
|---|---|---|
| 1 | `HamletRootCol/StatusBar/HamletStatus` (18,63) 1884×75 | `DialogCol/DialogTitleRow/DialogTitle` (619,121) 620×27 |
| 2 | `Overlay/RealmInventory/PurposeLabel` (887,294) 361×18 | `BpBodyAnchor/BpRuntimeBody/PopupLine` (1055,264) 212×49 |
| 3 | 同上 | `BpActivityLayout/BpActivityBase/PurposeLabel` (695,297) 606×18 |
| 4 | 同上 | `BpActivityBase/BpSlotListAnchor/PurposeLabel` (701,297) 600×18 |
| 5 | `BpBodyAnchor/BpRuntimeBody/PopupLine` (1055,264) | `BpActivityLayout/BpActivityBase/PurposeLabel` (695,297) |
| 6 | 同上 | `BpActivityBase/BpSlotListAnchor/PurposeLabel` (701,297) |

## §3 归因（三条 · 各自可复核）

**（1）5 对参战控件 = 有明文出处的占位块 / DD 原设计溢出块（不是玩家文字相撞）**

- `Overlay/RealmInventory/PurposeLabel`：`darkest/scenes/ui/hamlet_skeleton.tscn:261-279`（`tooltip_text` 明写「色块占位，**不换不删**」）
- `BpActivityLayout/BpActivityBase/PurposeLabel` ／ `BpActivityBase/BpSlotListAnchor/PurposeLabel`：`darkest/scenes/ui/building_popup.tscn:232` ／ `:259`
- 同族 `BpBodyAnchor/BpRuntimeBody/PopupLine`（同一 DD 版式块）
- 出处：`doc/UI_STRUCTURE_DECISIONS.md:303-306` —— `BpActivityBase 800x200 @ (70,50) ← base_size 800x200（溢出裁剪）`、`BpSlotListAnchor 135x80 @ (440,119) ← slot_list_pos`，并明写「**4 个溢出块均已确认是 DD 原设计（被面板裁剪）**」✓

**（2）第 1 对 = 分层遮挡（模态弹窗盖住下层状态栏）**

- 下层：`HamletStatus` 由 `darkest/scripts/ui/HamletRoot.Build.cs:167-172` 建（`AutowrapMode=WordSmart` ＋ 最小高 56 ⇒ 实测 **75** 高，文字可到 y≈138）
- 上层：`DialogTitle` 来自 `darkest/scenes/ui/modal_dialog.tscn:20`（弹窗层）；相交区 x619~1239 ／ y121~138 **落在弹窗自己的面板内**
- 本场景「透明框 0」⇒ 面板全不透明 ⇒ 玩家实际看到的是**弹窗标题**，状态栏那一段被弹窗盖住 = **模态弹窗的预期行为** ✓

**（3）判据缺口（代码坐标 · 未改）**

- `darkest/scripts/ui/LayoutAudit.Traversal.cs:83-128`（`Collect`）现有**三条跳过**：`MotionLayer` 子树 ／ `Window` 子树 ／ `clip_contents` 裁到空 —— **没有**「被不透明祖先完全遮住 ⇒ 跳过」✓
- `darkest/scripts/ui/LayoutAudit.Caliber.cs:35-66`（`FindOpaqueFullScreenOverlay`）**只认「恰好一个满屏不透明 Panel」**✓
- `darkest/scripts/ui/LayoutAudit.cs:34-40`：找不到覆盖层 ⇒ `scope = root`（审全场景）✓
- 本场景 = 「建筑详情 ＋ ProvisionPopup **两个局部浮层**」⇒ 两条都不满足 ⇒ 回落全界面 ⇒ 下层被遮住的 Label 也参与两两比较 ⇒ **19 对的来源** ✓

## §4 未收口 · 两条路（待裁）

| 路 | 动作 | 代价 / 约束 |
|---|---|---|
| **(a) 判据侧**（推荐） | `Collect` 增「被不透明祖先完全遮住 ⇒ 跳过并留痕」 | 改判据口径 ⇒ 必须给**前后读数两臂对照**；红线 21：跳过的数量必须打印（不许静默） |
| **(b) 场景侧** | 按 DD 真值收敛占位块的可见性 / `mouse_filter` | 与既有决定「色块占位，**不换不删**」冲突（`O-105` 家族）；且已定性的都不是布局缺陷 ⇒ 等于**用场景迁就判据** |

**两路共同前置**：先把 **19 对全量**打印出来逐对核（现只打印前 6 对 ⇒ 需加开关或提高上限 ⇒ **属改码，本件未做**）✓

## §5 与 `O-115` 的关系（不同源 · 分开处置）

- `O-115` 记的是**不带** `--hamlet-building` 时的 1 对：`LeftCol/ReliefHint (18,1781)` ⟷ `Overlay/EstateSummary/PurposeLabel (6,1798)` —— `ReliefHint` 落在相机 1920×1080 **之外**（30 下垫片不够）
- 本件 A 臂读数里**没有** `ReliefHint`（开了建筑详情后布局不同）⇒ **两条不要混成一条** ✓

## §6 边界（本件动了什么）

- 只动文档：本报告 ＋ `doc/architecture/open_issues.md`（`O-118`）＋ `doc/state.md`（`#540`）；**零代码 · 零数值**（红线 17）
- **未跑测试 / 未跑门禁**：纯文档改动不触代码面（用户约束「最低限度必要性测试」）⇒ 判据 = 改动文件的 CRLF 无 BOM ＋ md5 记账 ✓
- 探针与日志（`.tmp_audit_bare.log` ／ `.tmp_smoke_hamlet.log` ／ `.tmp_smoke_hamlet.cmd`）**留本地不入库**（项目惯例）✓
