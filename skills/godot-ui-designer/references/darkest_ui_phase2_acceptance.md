# 阶段2 验收报告：按 DD 尺寸建同尺寸色块占位

日期 2026-09-21 ｜ 执行域：`darkest/scripts/ui/**` · `darkest/scenes/ui/**` · `resources/**` · `skills/**` · `tools/dsh/**`

## 一、目标与合规前提

目标：按 E 盘原游戏（`E:\SteamLibrary\steamapps\common\DarkestDungeon`）的**布局锚点**与**美术资产尺寸**，在我域建**同尺寸色块占位**；
阶段3 由美术出**同尺寸原创资产**替换，**不动锚点/尺寸** ⇒ 布局零改动。

合规（红线27「学构图不抄素材」）：
- 只读取**锚点/尺寸数字**与 **PNG 文件头尺寸**（= 度量事实，不构成抄袭）✓
- **DD 像素/图集/骨架零进入工程**：实测工程内 `.atlas`/`.skel` = **0**；8 个 `.png` 全在 `darkest/assets/heroes_placeholder/**`（我方占位素材）✓

尺寸来源优先级：① 布局自带 `size` ＞ ② 尺寸表参数 ＞ ③ 资产 PNG 固有尺寸（兜底）

## 二、交付物

1. `darkest_ui_asset_manifest.md` —— 阶段3 落位清单（A 栏=提及 DD 出处 / B 栏=我方自建），脚本只读可再生 ✓
2. 本报告 —— 覆盖范围、门禁读数、已知缺口 ✓

## 三、覆盖范围（已落）

| 批次 | 内容 | 关键 DD 依据 |
|---|---|---|
| 战斗批 ①~⑤ | 英雄检视面板锚点 · 怪物卡 3 位+2 色块 · 地图面板化(720×360+tab/home) · **新增 panel.banner(754×136)** · 库存 8 列 | panel_hero/panel_monster/panel_map/panel_banner/panel_inventory |
| 城池批 (1)~(5) | building_popup 6 锚点块 + 6 栋自带尺寸块 · town 全字段 · activity_log/town_event · realm_inventory 网格(560×525) | building.layout / town.layout / realm_inventory |
| 子流程批 (1)~(3) | provision 4 位 · quest_select 3 位 + 5 自带尺寸 · heirloom 3 位 · loot 3 位 | provision/quest_select/heirloom_exchange/overlay.loot |
| menu | 466×48 · 行距 8(=56−48) · 起点 510,240 · back 859,103 · tooltip 360×50 · 手柄热区 600×36 | shared/menu **base_layout** |
| 结算屏 | **新增 raid_results_skeleton**（9 + 5 位） | raid_results.layout |
| 英雄详情 | **基准修正**为城池角色面板(1395×1080) + 12 位 + 4 自带尺寸 | shared/character + shared/hero |
| 战斗屏其余 | screen.raid 自带尺寸 7 + 位置 19（含舞台互证 788/1050/680） | screen.raid |
| **新屏 3 个** | controls（按键提示）· fe_flow（存档槽/模式选择）· credits（制作人员） | shared/controls · fe_flow · shared/credits |
| 杂项尺寸 | inventory icon 72×144 · estate cost 100×50 | shared/inventory · shared/estate |

## 四、门禁读数（最近一次全量实测）

```
构建（dotnet build Darkest.csproj）............ 0 错误 ✓
tools/dsh/ui_sweep.ps1 ......................... 退出码 0，摘要 entries=24 failed=0 ✓
   每入口：spec14.5=ok ／ demand=0 ／ overlap=0 ／ transparent=0 ／ realERROR=0 ／ trace 断言命中 ✓
tools/dsh/check_dd_layout.ps1 .................. 46 条全命中（RESULT: all DD values have an implementation anchor）✓
tools/dsh/check_ui_namespace.ps1 ............... exit 0 ✓
冒烟 tools/dsh/smoke.ps1（早前读数）............. PROBE-EXIT bad=0 code=0（10 例真错误 0）✓
```

## 五、已知缺口 / "未取得"项（不谎报）

- **DLC（arena_mp，25 个布局文件）**：属**多人对战**模式，本项目无对应玩法 ⇒ **默认不做**（已留档 §14.0.115），待用户确认
- **相对偏移类字段**（roster 的 frame/scroll/sort · realm_inventory 的面板内坐标 · screen.raid 的若干相对位）：其**父基准 DD 未给** ⇒ **不硬套**（记录备查 ✓）
- **时序/动画/样式类段**（base.popup_text 43 类飘字 · credits_animation · status_bar_*_pulse · announcement_times）：**无几何** ⇒ 阶段2 不落（阶段3 可校准配色/类别 ✓）
- **screen.raid 部分段**（如 overlays 之外的 3D 场景位）：属场景非 UI ⇒ 不落 ✓
- **`panel.map` 屏幕位**、**building_popup 面板尺寸**、**realm_inventory 面板尺寸**：DD 未给 ⇒ 用"约定/取自 town 相关 pos"并**在 tooltip 注明** ✓

## 六、阶段3 接口（替换口径）

1. 美术按 `darkest_ui_asset_manifest.md` 的「节点 + 尺寸 + DD 出处」出**同尺寸原创资产** ✓
2. 替换时**只换贴图、不动锚点/尺寸**（`custom_minimum_size` 与 anchor 值保持）⇒ 布局零改动 ✓
3. 替换后判据：构建 0 错误 + `ui_sweep` 24 入口全绿（overlap/transparent 仍须 0）+ DD 门禁全命中 ✓

## 七、纪律沉淀（本轮新增，均已入库）

- 多 section 文件**必须直读 E 盘原文**（spec 会压平 section ✗）—— §14.0.100
- C# 插桩锚点**必须是完整语句且唯一** —— §14.0.108
- 核对留痕**必须查用例原始日志**（`reports/ui_<entry>_*.txt`），sweep 摘要不含 stdout —— §14.0.109
- 工具脚本写回**必须带 BOM**；写文件**禁用 `List.Add` 模式**（改用数组拼接） —— §14.0.111
- **自带尺寸必须先确认所属 section** —— §14.0.103
