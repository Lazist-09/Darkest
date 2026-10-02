# P6 · `--hamlet-popup-close` 判据误报修复（2026-10-02）

## 结论

`--hamlet-popup-close：关闭后 BuildingPopupOpen=True（应 False）` **是判据误报，不是缺陷**。
`CloseTopPopup()` 关的是**栈顶**那一层；冒烟却拿 `BuildingPopupOpen`（只反映建筑弹窗**自己**）判「关掉成功」——
当栈顶是别的层（例如 `HamletMenu`）时，关掉栈顶后建筑弹窗**仍在模态栈里且 Visible=true** ⇒ 读成「没关掉」（假红）。

## 证据（修复前 · `.tmp_smoke_hamlet.log:91-96`）

```
[UI Overlay] OpenModal：`HamletMenu`（栈深 3，遮罩 开）✓
[UI Overlay] CloseTopModal：`HamletMenu`（余 2）✓
[HamletRoot] --hamlet-popup-close：关闭后 BuildingPopupOpen=True（应 False）
```

⇒ 关的是 `HamletMenu`（**正确**）；`BuildingPopupOpen=True` 是**真值**（建筑弹窗在栈底第 1 层且可见）。
判据错在「拿非栈顶面板当栈顶探针」（违反红线 26：判据必须反映真值）。

## 修复（3 文件 · 最小改动 · 旧签名全保留）

| 文件 | 改动 |
|---|---|
| `darkest/scripts/ui/OverlayLayer.cs`（216 行） | `CloseTopModal()` 保留 ⇒ 新增 `CloseTopModal(out string? closed)`：**带出实际关掉的那一层名字**（`closed=null` ⇒ 栈空没关任何层） |
| `darkest/scripts/ui/HamletRoot.PopupMenu.cs`（318 行） | `CloseTopPopup()` 保留 ⇒ 新增 `CloseTopPopup(out string? closed)`：栈路径透传；回落路径回填 `BuildingPopup` / `HeroDetail` |
| `darkest/scripts/ui/HamletRoot.Build.SmokeFlags.cs`（87 行） | `--hamlet-popup-close` 打印改**真值口径**：关闭前/后 **栈深** ＋ **关掉的层名** ＋ 建筑弹窗是否仍开 |

旧签名保留 ⇒ 既有调用点**零改动**：`HamletRoot.PopupMenu.cs:178`（Esc 分支）· `OverlayLayer.cs:193`（Esc 兜底）· `UIRoot.cs:189`。
三文件 LF 无 BOM（与 `darkest/**` 纪律一致）· 全部 ≤600 行（远低于硬线）· 未新增 `.cs` ⇒ 无需生成 `.uid`。

## 验收（最低限度：构建 ＋ 两条玩家路径冒烟 · 不跑全量测试）

命令（真实玩家路径 · 红线 18）：`--hamlet --hamlet-building=blacksmith.weapon [--hamlet-menu] --hamlet-popup-close --fixed-fps 60 --quit-after 300`

| 臂 | 关闭前 | 关掉的层 | 关闭后 | 建筑弹窗仍开 |
|---|---|---|---|---|
| A（building ＋ menu ⇒ 栈顶 = `HamletMenu`） | 栈深=2 · 建筑弹窗开=True | `HamletMenu` | 栈深=1 · 关掉=True | **True**（真值：它在栈里且可见，只是不在栈顶） |
| B（只有 building ⇒ 栈顶 = `BuildingPopup`） | 栈深=1 · 建筑弹窗开=True | `BuildingPopup` | 栈深=0 · 关掉=True | **False** |

⇒ 两臂读数与真值一致（红线 26）；A 臂的 `True` 不再被误读为「关不掉」。

- 构建：`dotnet build darkest/Darkest.csproj -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` ⇒ **RC=0 · 0 错误**（73 警告 · 与基线同类）
- 冒烟：CASE A / CASE B 均 **RC=0**（探针 `.tmp_p6_caseA.log` / `.tmp_p6_caseB.log` · 脚本 `.tmp_smoke_p6.cmd`）
- 未跑全量测试：本件只改**判据口径**（无内核/数值行为改动）⇒ 按用户约束「只允许最低限度必要性测试」执行

## 更正记录（不改历史文本）

三处历史文本把它记为「既存不良读数 / 既存挂账」；按项目惯例（映射表为准 · 历史文本只作更正记录）**不回改**，本报告即更正记录：

- `reports/m14_quest_20261002.md:168`
- `reports/p6_m11_split3_20261002.md:30`
- `reports/p6_m11_split11_20261002.md:39`

## 红线对照

- **红线 26**（判据 == 真值）：判据从「猜哪个面板」改成「栈深 ＋ 实际层名」✓
- **红线 21**（不留黑箱）：关闭动作与层名同日志留痕 ✓
- **红线 18**（玩家路径）：冒烟走 `--hamlet` 真实入口（`OpenBuildingPopup` / `OpenHamletMenu` / `CloseTopPopup`）✓
