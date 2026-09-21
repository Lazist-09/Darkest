# 士气代价总表（**跨系统的"谁在什么时候改士气"**）

> 🔴 **为什么需要这份**（架构提 `#445` · 我据实测落）：
> **两条代价分居两个系统 ⇒ 玩家只能看到"掉了"、看不到"为什么掉、还会不会再掉"** ⚠️
> —— 而那正是 **"缺可读性"**（**不是缺机制**）✅
> 🔴 **触发**：`retreat.md` 的**撤退代价（−12）** vs Quest 表的 **`fail_penalty`（−20）** 看起来像"两处真值"
> ⇒ ✅ **纪律 AP**：**"两处都改同一个量" ⇒ 合法【当且仅当】【触发不同】** ✅

---

## §1 🔴 实测：士气代价散布在 **13 张表 · 32 处代码**

```
🔴 **数据侧（13 张表）**：
   `skills.json` **44 处** · `tuning.json` **19** · `buff_defs.json` **20** · `expedition_nodes.json` **12** ·
   `roster.json` **10** · `economy.json` **8** · `curios.json` **5** · `morale_events.json` **5** ·
   `camp_skills.json` **4** · `heirlooms.json` **3** · `sanitarium.json` **3** · `trap_defs.json` **3** ·
   `enemy_ai.json` **1**
🔴 **代码侧（32 处 · 16 文件）**：
   `ExpeditionSession.CampAndBonuses` 4 · `BattleDirector` 3 · `TrapDefs` 3 · `UnitRuntime` 3 ·
   `BattleEventTypes` 3 · `SkillExecutor` 2 · `SanitariumConfig` 2 · `tuning.json` 1 · …
⇒ 🎖️ **即：士气是【跨系统共享量】** —— 而**目前没有一处能看到全貌** ⚠️
```

---

## §2 🔴 士气代价总表（**按触发分类 · 那是纪律 AP 的判据**）

### 2.1 远征级（**一趟的开始/结束**）

| 触发 | 值 | 表 | 范围 |
|---|---|---|---|
| **一趟开始** | 基准 **50**（`morale.start`）| `tuning.json` | 全队 |
| 🔴 **放弃远征 / 全灭**（`fail_penalty`）| **−20** | 🔴 **Quest 表**（`quest.exit_penalty.json`）| 全队 |
| **回城封顶** | 上限压到 **100**（低于 100 原样保留）| `dd_reference.md` | 全队 |

### 2.2 战斗级（**单场之内**）

| 触发 | 值 | 表 |
|---|---|---|
| 🔴 **撤退成功** | **−12** | 🆕 `tuning.json` `retreat.success_morale`（**本档 `#357` 裁定**）|
| 🔴 **撤退且有人死** | **−15** | `tuning.json` `retreat.with_death_morale` |
| 🔴 **撤退失败** | **−5** | `tuning.json` `retreat.fail_morale` |
| **打出暴击**（`critical_strike_dealt`）| **+5** | `morale_events.json` |
| **击杀敌人**（`kill_enemy`）| **+10** | `morale_events.json` |
| **队友进入虚弱**（`ally_enters_weak`）| **−8** | `morale_events.json` |
| **目睹暴击（shock）** | **50% 概率**触发 | `tuning.json` `morale.witness_crit_shock_chance_percent` |
| **技能/状态**（`skills.json` 的 `morale_effects`）| 逐条 | `skills.json`（44 处）|

### 2.3 探索级（**地牢之内**）

| 触发 | 值 | 表 |
|---|---|---|
| **陷阱**（`stress_damage`）| **15** | `trap_defs.json` |
| **拆陷阱成功**（`disarm_stress_heal`）| **+8** | `trap_defs.json` |
| **Curio 交互**（`morale_team`）| **±5 ~ ±15** | `curios.json` |
| **节点抉择**（`morale`）| **−5** 等 | `expedition_nodes.json`（12 处）|
| **扎营技能**（`morale_plus_8` 等）| **+5 / +8** | `camp_skills.json` |
| **疾病**（`morale_delta`）| **0 ~ −5** | `sanitarium.json` |

### 2.4 城镇级（**Hamlet 之内**）

| 触发 | 值 | 表 |
|---|---|---|
| **减压（Tavern / Abbey）** | **+30 基准**（`stress_relief_cost = 3` 金）| `economy.json` |
| **Abbey 升级**（`morale_restore_delta`）| **+5 / 级**（3 级）| `heirlooms.json` |
| **特质**（`morale_damage_pct`）| **−10%** 等 | `roster.json` |

---

## §3 🔴 叠加规则（**玩家最需要知道的**）

```
🔴 **一场之内**（撤退 + 目睹暴击 + 陷阱 …）⇒ **累加** ✅
🔴 **一趟之内**（多场撤退）⇒ **累加**（**`#364` 的 R8：一趟退 2 场 ⇒ 恰扣 2 次**）✅
🔴 **撤退（−12）＋ 放弃远征（−20）⇒ 会叠加**（**那是有意的** —— `#444` 裁定）✅
   ⇒ 📌 **即：一趟里"退了一场再放弃" ⇒ 比"直接放弃"多扣 12** ✅
🔴 **下限 0 · 上限 100**（`tuning.morale{min,max}`）⇒ **触顶后不再增**（**`A9` 多轴读数因此必要**）✅
```

---

## §4 🎖️ 而这份表本身是【纪律 AP 的产物】

```
🆕 **纪律 AP**：**"两处都改同一个量" ⇒ 合法【当且仅当】【触发不同】**
   ⇒ ✅ **本表按【触发】分类**（远征 / 战斗 / 探索 / 城镇）——
      而**同类里若有两处写同一个触发 ⇒ 那就是两处真值**（**必须收敛**）⚠️
⇒ 🎖️ **一句话**：**"这两处，是【同一个触发】还是【两个触发】？"** ✅
🔴 **并可核对（判据）**：**本表任一行 ⇒ 都能回答"谁 / 何时 / 多少"** ✅
```

---

## §5 ⚠️ 而不在本表的（**有意排除**）

```
⚠️ **`roster.json` 的"当前士气值"**（`morale: 45` 等）⇒ **那是【状态】，不是【代价】** ✅
⚠️ **`skills.json` 的 `morale_effects: []`**（44 处）⇒ **多数为空**（**其余是逐技能的士气效果**）✅
   ⇒ 📌 **若逐技能展开 ⇒ 本表会长 44 行** ⇒ ✅ **建议按需展开**（**先留指针**）✅
```
