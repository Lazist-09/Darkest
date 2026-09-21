# 主程序产物索引（本会话 · 一页速查）

> 🔴 **用途**：让架构/策划/UI **按需取用**这些产物，而不必翻聊天记录或窗口历史 ✓
> 🔴 **每条都标了：用途 / 状态 / 谁该读 / 权威来源** —— 状态含"**已被取代**"（避免误用旧结论 ✓）

## A. 对齐数据（M1/M4/M5/M6/M8 · 阶段 A）
| 产物 | 用途 | 状态 | 谁该读 |
|---|---|---|---|
| `dd1_hero_tables_from_unity_ref.md` / `.json` | 15 英雄 × **8 抗性 + weapon/armour 5 阶 + 技能等级** | ✅ 可用（**参考件**，与 E 盘一致，策划已核 ✓） | 策划（M1a/M8 取数） |
| `darkest/data/units.json`（已落库） | 4 原型的 tier/三轴/`prot`（**按 `#448` 从参考件对齐**） | ✅ 已落库 + 用例验收 | 主程序/策划 |
| `dd1_trinkets_source.md` + `darkest/data/trinkets.json`（已落库） | Trinket 表 **196 条**（**E 盘一手** + 排除 `kickstarter`） | ✅ 已落库 + 验收（T1/T2/T5） | 策划/UI（商店接线） |
| ⚠️ ~~`dd1_trinkets_from_ref.json`~~ | ~~第三方参考件的 488 条~~ | 🔴 **已删除**（`#452` 裁定：E 盘才是一手 ⇒ 留着会误导） | —— |
| `dd1_quirks_source.md` + `darkest/data/quirks.json`（已落库） | Quirk **170 条**（E 盘一手）+ **互斥全对称**实测 | ✅ 已落库 + 验收 | 策划（M5）/UI（M5u） |
| `dd1_buildings_source.md` + `darkest/data/buildings.json`（已落库） | 建筑 **8/20 树/99 等级**（E 盘一手） | ✅ 已落库 + 三条 P 校验 | 策划（M6）/UI |
| `dd1_hero_upgrades_source.md` / `.json` | **15 职业 · 135 树 · 645 等级** + 新字段 `prerequisite_resolve_level` | ✅ 已量 · ⛔ **未落库**（卡明写依赖 M1/M2） | 策划（M8 排期） |
| `dd1_buff_primitives.md` / `.json` | 参考件 **1801 条 buff ⇒ 41 原语** 用量 | ✅ 已量（M2 映射的输入） | 架构/策划（M2 裁定） |

## B. 裁定与基线（给决策用）
| 产物 | 用途 | 状态 |
|---|---|---|
| `def_merge_baseline.md` | **合并 `def` 的前置基线**（7 单位 × 28 条命中/减伤矩阵 + 3 条钉死值） | ✅ **前置已满足** ⏳ 等裁定（甲乙丙） |
| `self_audit_unconsumed.md` | **我给自己做的"填了不消费"审计**（8 个符号待激活 + 逐条激活条件） | ✅ 已完成（含可检验承诺） |
| `m3_buff_table_recon.md` | M3: **原版落在哪张表**（buff 表 2020 / quirk 库 170；`taunt`/`bound` 0 命中） | ✅ 已量（含"id 名匹配不是判据"的方法论） |
| `m3_consumption_points.md` | M3: **22 条 buff 的消费点**（生产 56 处 / 18 文件，其中 9 个在飞） | ✅ 已量（与卡里数字不符，待对账） |
| `m3_actionable_plan.md` | M3: 逐文件落点 + 可执行清单 + **一处我没挖到底的如实交代** | ✅ 收口 |

## C. 工程与协调（给架构/UI 用）
| 产物 | 用途 | 状态 |
|---|---|---|
| `inflight_scan_summary.md` | **在飞改动全盘扫描**（210 项 / 域分布 / 4 条风险 / 建议批次） | ✅ 已交（用户裁定：只总结、不代提） |
| `commit_batches_for_inflight.md` | 在飞改动的**提交批次清单**（9 批 + 依赖顺序） | ✅ 已交（等逐批授权/派工） |
| `c4_navigation_inventory.md` | **C4 导航清单**（6 处调用点 + `project.godot`；回城两条路径要一起改） | ✅ 已交（归属：我域/UI 域/在飞） |
| `ui_layout_audit.json` | 静态布局审查的**结论数据**（覆盖率 0.3% ⇒ 静态不可为权威） | ✅ 结论已固化进工具 docstring |

## D. 我本会话新增的工具（都可重跑）
```
`extract_dd1_hero_tables.py`   ← 参考件 .bytes ⇒ 英雄 5 阶/抗性/技能等级（只读）
`extract_dd1_trinkets.py`      ← **E 盘一手** ⇒ trinkets.json（排除 kickstarter；每值 origin=dd1）
`extract_dd1_quirks.py`        ← **E 盘一手** ⇒ quirks.json（含互斥悬空/对称实测）
`extract_dd1_buildings.py`     ← **E 盘一手** ⇒ buildings.json（含悬空/成环实测）
`extract_dd1_hero_upgrades.py` ← E 盘 15 职业升级树（**只测量、不落库**）
`extract_dd1_buff_primitives.py`← 参考件 buff 表 ⇒ 原语用量（M2 输入）
`check_no_external_assets.py`  ← **B6 门禁**（E 盘来源/提取物；白名单需理由+到期；双向自检）
`check_ui_shell_singleton.py`  ← **形态 B 终态判据**（外壳实例必须 = 1；`--strict` 供 S4 后接 CI）
`ui_layout_audit.py`           ← 静态布局审查（**fail-closed**：覆盖率不足就 INCONCLUSIVE）
```
## E. 使用提醒
```
① **凡"参考件 vs E 盘"冲突** ⇒ **E 盘是一手**（`#452` 已立）⇒ 本索引里凡参考件来源的产物都标了来源 ✓
② 凡"未落库"的产物（`dd1_hero_upgrades_*`）⇒ 不是漏做，而是**卡的依赖未到位** ✓
③ 凡"待激活"的符号 ⇒ 见 `self_audit_unconsumed.md`（每条都有激活条件 ✓）
```
