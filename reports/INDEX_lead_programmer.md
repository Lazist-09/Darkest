# 主程序产物索引（接手人入口 · 2026-09-21 刷新）

> 🔴 **本会话我域的产物共 32 份**（外加 2 份是我代提的别人域报告 ✓）
> 🔴 **怎么用**：先看 §0「按裁定取用」⇒ 你手上是哪条裁定，就翻哪份报告 ✓；需要背景再看 §1~§4 ✓
> 🔴 **权威来源**：凡"参考件 vs E 盘"冲突 ⇒ **E 盘是一手**（策划 `#452`）✓

## 0. 🔴 按裁定取用（**你裁哪条，就看哪份**）
| 你手上的裁定 | 看这份 | 里面有什么 |
|---|---|---|
| **要不要让【阶数】直接影响伤害** | `m1c_dmg_pct_*.md`?→ 见 `M1c` 取值表（用例 `M1cDmgPctTableTests`） | 23 技能"复现今天所需 dmg%"两列（tier0/tier4）⇒ **两列差异巨大**就是这个问题的证据 |
| **M1c 的 dmg% 值** | 同上 + `M1cPilotComparisonTests` | 16 行并排对照（阶段 2）✓ |
| **`def` 合并 (甲)/(乙)** | `def_merge_three_plans.md` + `def_merge_baseline.md` | 三份预案（动哪几行/改哪些读数）+ 28 条前置基线 + 原版口径对照（最大差 **29 pt**）|
| **`M2` 映射 (甲)/(乙)/(丙)** | `m2_activation_plan.md` | 15 条冻结原语逐条：**7 条现在就能接** · 4 条等 M3/M4 · 2 条口径待定 |
| **`M8` 四问** | `dd1_hero_upgrades_source.{md,json}` + 用例 `HeroUpgradesConfigTests` | **15 职业/135 树/645 等级** 可吃进真实形状（无悬空）+ 三道 P 检查 |
| **`M5` 的 6 条空分类** | `dd1_quirks_source.md` | 6 条**全是 `corvids_*`**（建议保持空 ✓）|
| **`M3` 三条边界** | `m3_buff_classification.md` | 22 条逐条归属表（**①7 条与卡严丝合缝**）+ `taunt`/`bound`/`pep_talk` 的判据冲突 |
| **`M9` 名单 4 人 / 是否保留待命位** | `m9_4v4_recon.md` + `m9_rules_readings.md` + `m9_support_impact.md` | 四个子任务 + **"增援=待命席"的更正** + 10 文件 32 处影响面 |
| **拆分到底有没有丢东西** | `split_integrity_audit.md` + 工具 `tools/dsh/audit_split_integrity.py` | 六件 + `ExpeditionFlow` 7 片 = **成员零丢失**（一条命令可复跑 ✓）|
| **`TuningConfig` 名字与内容不符** | `tuningconfig_domain_recheck.md` + `tuningconfig_split_map.md` | 真实跨域清单 + 三个选项（甲保持/乙真重切/丙按校验对象改名）|
| **整体收尾与卡点** | `final_verification.md` + `HANDOVER_lead_programmer.md` | 终检读数 + 37 项交付总表 + 12 件待裁定 + 我的不变量 |

## 1. 对齐数据（M1/M4/M5/M6/M8 · DD1 阶段 A）
| 产物 | 用途 | 状态 |
|---|---|---|
| `dd1_hero_tables_from_unity_ref.{md,json}` | 15 英雄 × 8 抗性 + weapon/armour 5 阶 | ✅ 可用（**参考件**，与 E 盘一致） |
| `dd1_trinkets_source.md` + `darkest/data/trinkets.json` | Trinket **196 条**（E 盘一手，排除 kickstarter） | ✅ 已落库 + 验收 |
| `dd1_quirks_source.md` + `darkest/data/quirks.json` | Quirk **170 条**（互斥悬空 0 / 非对称 0） | ✅ 已落库 + 验收 |
| `dd1_buildings_source.md` + `darkest/data/buildings.json` | 建筑 **8/20/99** | ✅ 已落库 + 三条 P 校验 |
| `dd1_hero_upgrades_source.{md,json}`（286 KB） | **15 职业/135 树/645 等级** | 🟡 **解析器+校验已就绪** · ⛔ 落库等 `M8 四问` |
| `dd1_buff_primitives.{md,json}` | 参考件 1801 条 buff ⇒ **41 原语** | ✅ 已量（M2 的输入） |

## 2. 裁定与基线
`def_merge_baseline.md`（28 条命中/减伤基线）· `def_merge_three_plans.md`（三预案）· `m2_activation_plan.md` ·
`m3_buff_table_recon.md` · `m3_consumption_points.md` · `m3_actionable_plan.md` · `m3_buff_classification.md` ·
`m9_4v4_recon.md` · `m9_support_impact.md` · `m9_rules_readings.md` · `tuningconfig_split_map.md`（域段地图，已用掉 ✓）·
`tuningconfig_domain_recheck.md` · `self_audit_unconsumed.md`（我的欠账清单：8 符号待激活 + 条件）·
`split_integrity_audit.md` · `final_verification.md` · `HANDOVER_lead_programmer.md`

## 3. 工程与协调
`inflight_scan_summary.md`（在飞全盘扫描）· `commit_batches_for_inflight.md`（9 批清单 · **已执行完** ✓）·
`c4_navigation_inventory.md`（C4 6 处调用点）· `ui_layout_audit.json`（静态审查结论：覆盖率上限 24% ⇒ 必须走 runtime）

## 4. 我本会话新增的工具（都可复跑）
```
`extract_dd1_hero_tables.py`   ← 英雄 5 阶/抗性/技能等级（参考件）
`extract_dd1_trinkets.py`      ← **E 盘一手** ⇒ trinkets.json
`extract_dd1_quirks.py`        ← **E 盘一手** ⇒ quirks.json
`extract_dd1_buildings.py`     ← **E 盘一手** ⇒ buildings.json
`extract_dd1_hero_upgrades.py` ← E 盘 15 职业升级树（只测量 ✓）
`extract_dd1_buff_primitives.py` ← 参考件 buff ⇒ 原语用量
`check_no_external_assets.py`  ← **B6 门禁**（E 盘来源/提取物 · 白名单需理由+到期 · 双向自检）
`check_ui_shell_singleton.py`  ← 形态 B 终态判据（外壳实例 = 1 · `--strict` 供 S4 后接 CI）
`ui_layout_audit.py`           ← 静态布局审查（**fail-closed**：覆盖不足即 INCONCLUSIVE）
🆕 `audit_split_integrity.py`  ← **拆分完整性**（按成员名比 · 缺一个即 exit 1 · 含双向自检 ✓）
```
## 5. 使用提醒
```
① 凡"参考件 vs E 盘"冲突 ⇒ **E 盘是一手** ✓
② 凡"未落库/待激活"的产物 ⇒ **不是漏做**，是**按计划等前置**（每条都有前置写清 ✓）
③ 本索引里凡标 🟡/⛔ 的 ⇒ 都是"等裁定"；标 ✅ 的 ⇒ **有读数 + 提交号** ✓
```
