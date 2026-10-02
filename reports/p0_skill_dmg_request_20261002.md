# 策划请求 · P0 数据请单【回一手复核】—— 4 职业池内 22 条技能 `dmg%`（现落库全 0）

> 🕒 2026-10-02 · 来自**主程序**（任务卡 P0 数据请单 · `M1c` 阶段 3 的硬前置）
> 🔴 **口径判据**：`doc/architecture/source_priority.md`（**一手 E 盘 = 第一来源** · 参考项目只兜底）
> 🧰 **读数可复跑（全只读）**：
>    `python -X utf8 tools/dsh/extract_dd1_hero_skills.py --check`（一手读取器 · ⚠️ 当前 E 盘原版目录**不在原位** ⇒ 见 §2 降级说明）
>    `python -X utf8 tools/dsh/reconcile_skill_dmg_edrive_vs_ref.py --check`（一手 ↔ 参考对账）
>    `python -X utf8 .tmp_p0_meta.py`（本件 22 条四分组表 · 探针留本地不入库）
> 📄 一手原始表：`reports/dd1_hero_skills_from_edrive.json`（525 行 · 15 英雄文件 + 28 共享文件）· 映射表：`reports/unity_ref/skill_dmg_mapping.json`
> 📌 **送达判据**：`Select-String -Path doc\windows\策划窗口.txt -Pattern 'DELIVERY-LEAD-SKILL-DMG-POOL-REQUEST-20261002' -SimpleMatch` 命中即送达。

---

## 0. 一句话

🔴 任务卡写「**23 个技能 `dmg%` 还没有**」= **2026-09-21 的过期读数**（当时 `dmg_pct` 0/44）。
   **今天实测：44 条全部有值**；真实缺口 = **4 职业池内 22 条「无出处」的技能（现落库值全 0）需要按一手口径逐条复核/裁定**。

| 口径 | 数 | 说明 |
|---|---:|---|
| 任务卡「23 条」 | 23 | `dd1_baseline §58` 的**原版技能之间**一手↔参考冲突表（`#490` 已裁「只登记不动」）⇒ **与本件不是同一件事** |
| **本件「22 条」** | **22** | 4 职业池内**无 `_dmg_pct_source`** 的技能（`#475` 裁定「30 条自加一律取 0」的落库结果）⇒ **回一手复核**，不是「补缺值」 |

⚠️ **两者不可互相替代**（纪律 AU：先定义再数）—— 本件按 **22 条**发；那 23 条**不重复请裁**。

## 1. 现况（实测 · 可复算）

| 项 | 读数 |
|---|---:|
| 技能总数（`darkest/data/skills.json`）| 44 |
| 有出处（`_dmg_pct_source`）| 14（`ref:` **6** + `dd1:` **8**）|
| 无出处（我们自加 · `#475` 取 0）| 30 |
| 4 职业池内 | 36 = 有出处 14 + **无出处 22** |
| 池内 22 条当前 `dmg_pct` | **全部 = 0**（实测 `POOL_NOSRC_NONZERO` = 空）|
| 池外 8 条（`melee_`2 / `ranged_`3 / `caster_`2 / `move`1）| 也在那 30 条里 · **不在本件** |

## 2. 口径（写清，免得答错题）

- **一手** = `E:\SteamLibrary\steamapps\common\DarkestDungeon\heroes\<name>\<name>.info.darkest` 的 `.dmg` 字段（`combat_skill:` 行 · L0..L4 恒定）
- **只做 4 职业池**（`warrior_` / `tank_` / `medic_` / `commissar_`）；池外 8 条不在本件
- **逐条带出处**（`file:line`，见 §3 表）
- **参考只兜底**：本表「参考」列只作对照，不作第一来源
- ⚠️ **降级事实（一并留痕）**：一手源 E 盘当前**不在原位** —— `E:\...\DarkestDungeon\` 只剩 `mods/` + `app.log`，`heroes/` 已不在 ⇒ `extract_dd1_hero_skills.py` 本次复跑 **RC=1（tree not found）**；本件一手读数**取自库内读取器产物** `reports/dd1_hero_skills_from_edrive.json`（`root` / `hero_files=15` / `shared_files=28` 都写在产物里，可审计；E 盘恢复后 `--check` 可复跑复核）✓
- ⚠️ **映射本身也请点头**：22 条里 **20 条映射置信度 =「候选」**、**2 条 =「无对应」** ⇒ **值有出处 ≠ 分配有出处**（纪律 BI）

## 3. 22 条 · 按处置分四组（机器核验：A4 / B5 / C8 / D5 = 22）

### A 组 · 一手有值且与参考一致（4 条）—— 建议：补非零值 + 挂 `dd1:` 出处

| 我方 id | 现落库 | 参考技能 / 值 | **一手值** | 一手出处 | 置信度 |
|---|---:|---|---:|---|---|
| `warrior_shield_bash` | 0 | `barbaric_yawp` −100 | **−100** | `heroes/hellion/hellion.info.darkest:23` | 候选 |
| `medic_field_strike` | 0 | `rampart` −60 | **−60** | `heroes/man_at_arms/man_at_arms.info.darkest:18` | 候选 |
| `commissar_charge_order` | 0 | `wicked_slice` +15 | **+15** | `heroes/highwayman/highwayman.info.darkest:13` | 候选 |
| `commissar_supervise` | 0 | `target_tag` −100 | **−100** | `heroes/bounty_hunter/bounty_hunter.info.darkest:18` | 候选 |

### B 组 · 一手与参考冲突（5 条）—— 请裁以哪个为准（我们建议一手）

| 我方 id | 现落库 | 参考 / 值 | **一手值** | 差 | 一手出处 |
|---|---:|---|---:|---|---|
| `warrior_javelin` | 0 | `thrown_dagger` 0 | **−10** | 一手更负 | `heroes/grave_robber/grave_robber.info.darkest:34` |
| `warrior_last_stand` | 0 | `heroic_end` +150 | **+50** | −100 | `heroes/jester/jester.info.darkest:23` |
| `tank_selfless_charge` | 0 | `heroic_end` +150 | **+50** | −100 | `heroes/jester/jester.info.darkest:23` |
| `medic_medicine_flask` | 0 | `gods_illumination` −50 | **−75** | −25 | `heroes/vestal/vestal.info.darkest:39` |
| `commissar_pistol_shot` | 0 | `pistol_shot` −25 | **−15** | +10 | `heroes/highwayman/highwayman.info.darkest:18` |

### C 组 · 一手 = 参考 = 0（8 条）—— 建议：值维持 0，但**补挂 `dd1:` 出处**（否则永远是无出处）

| 我方 id | 现落库 | 参考技能 / 值 | 一手值 | 一手出处 |
|---|---:|---|---:|---|
| `warrior_catch_breath` | 0 | `absolution` 0 | 0 | `heroes/abomination/abomination.info.darkest:28` |
| `tank_iron_wall` | 0 | `bolster` 0 | 0 | `heroes/man_at_arms/man_at_arms.info.darkest:44` |
| `tank_hunker` | 0 | `bolster` 0 | 0 | `heroes/man_at_arms/man_at_arms.info.darkest:44` |
| `tank_catch_breath` | 0 | `absolution` 0 | 0 | `heroes/abomination/abomination.info.darkest:28` |
| `medic_cross_slash` | 0 | `mace_bash` 0 | 0 | `heroes/vestal/vestal.info.darkest:13` |
| `commissar_mobilize` | 0 | `command` 0 | 0 | `heroes/man_at_arms/man_at_arms.info.darkest:39` |
| `commissar_execution_order` | 0 | `collect_bounty` 0 | 0 | `heroes/bounty_hunter/bounty_hunter.info.darkest:13` |
| `commissar_total_mobilization` | 0 | `bolster` 0 | 0 | `heroes/man_at_arms/man_at_arms.info.darkest:44` |

### D 组 · 参考答不上来（5 条）—— 不猜、不补，请裁

| 我方 id | 现落库 | 情况 |
|---|---:|---|
| `tank_taunt` | 0 | 映射表判「**无对应**」（参考侧与一手侧都找不到同族技能）|
| `medic_double_hit` | 0 | 映射表判「**无对应**」|
| `medic_first_aid` | 0 | 参考 `divine_grace` 与一手 `vestal.info.darkest:28` **两侧都无 `.dmg` 字段** |
| `medic_group_bandage` | 0 | 参考 `gods_comfort` 与一手 `vestal.info.darkest:34` **两侧都无 `.dmg` 字段** |
| `commissar_battle_inspiration` | 0 | 参考 `inspiring_cry` 与一手 `crusader.info.darkest:44` **两侧都无 `.dmg` 字段** |

## 4. 请你裁什么（3 问）

1. **A 组 4 条**：按一手补非零值（并挂 `dd1:` 出处）—— 可否？
2. **B 组 5 条**：以一手为准（参考值作废）—— 可否？
3. **D 组 5 条**：维持 0，还是另有口径？

（C 组 8 条若同意「补挂出处、值不动」，回信里带一句即可。）

## 5. 时效与影响（为什么现在改是零行为）

- 伤害路径**还没读** `dmg_pct`：`rg -n "DmgPct" darkest/scripts/` = 只有**声明**（`SkillsConfig.cs:93/107`）+ XML 注释 ⇒ **生产 0 读点**
- 守卫测试钉住：`darkest/tests/WeaponDamageModelStage1Tests.cs:76 NoProductionCodeCallsTheNewModel_SoOldReadingsCannotChange`
- ⇒ 本件落库**零行为**（游戏里一点不变）；**`M1c` 阶段 3 切默认**时才变成平衡改动 ⇒ 走解冻口径
- 按 `#486` / `#490` 先例：**回一手 = 对齐 ≠ 平衡数值改动** ⇒ 不入 `§39`

## 6. 本件边界

- 不动 `§39` · 不动 buff 原语层 · **不改任何代码**
- 池外 8 条 / `§58` 的 23 条 / Σ 段倍率（`§61`）**都不在本件**
- 数值只登记不动：**回信裁定前，`skills.json` 一行不改**

---

- **阻塞 / 待裁定**：上面 3 问（P0 是 `M1c` 阶段 3 的硬前置 ⇒ 卡着 `P2`）
- **权威在哪**：`doc/state.md #475`（30 条取 0）· `#490` / `#497`（回一手 2 条先例）· `doc/architecture/source_priority.md §1/§1b` · `doc/modules/dd1_baseline.md §58/§61` · `reports/m1c_stage3_readiness.md`（阶段 3 两个硬前置）· `reports/p0_skill_dmg_request_20261002.md`（本件）
