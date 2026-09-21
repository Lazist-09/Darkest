# M3 前置侦察（续）：**22 条 buff 的消费点清单** —— 纪律 AA 的落点表（2026-09-21 实测）

> 🔴 **卡 M3 原文**：「🔴"**搬家必须带行为**"（纪律 AA）：**8 文件约 20 处消费点一起改** + **前后读数**（士气/折磨）」
> 🔴 **本轮只做"量"**（只读 ✓ 不改数据、不动消费点 ✓）

## 1. 🔴 实测：数字与卡里的预期**不一致**（如实报，供对账）
```
按域拆分（我们 22 条 buff id 的字符串引用）：
   **生产代码 `darkest/scripts/**` = 56 处 / 18 个文件**　← ⚠️ 卡里写的是「8 文件约 20 处」
   **测试 `darkest/tests/**`        = 103 处**
   **数据 `darkest/data/*.json`     = 43 处**（含 `buff_defs.json` 自身的定义 ✓）
   合计 **202 处**
⇒ 📌 两种口径都可能：卡里的"20 处"可能指【**真正的行为消费点**】（switch 分支/判定）而不是"提到 id" ✓
   ⇒ 我把它**逐文件列出来**（下节），由你/策划按口径核对 ⇒ **不擅自宣称"卡写错了"** ✓
```
## 2. 生产侧 18 个文件（纪律 AA 的"要一起改"清单 · 实测）
| # | 文件 | 归属域 | 在飞状态 |
|---|---|---|---|
| 1 | `gameplay/sim/buffs/AfflictionProcs.cs` | 模拟/战斗 | clean |
| 2 | `gameplay/sim/director/BattleDirector.cs` | 战斗导演 | **M（在飞）** |
| 3 | `gameplay/sim/director/BattleDirector.OvertimeAndRetreat.cs` | 战斗导演 | **M（在飞）** |
| 4 | `ui/BattleUI.Render.cs` | 🔴 UI 域 | clean |
| 5 | `data/BuffPrimitiveTranslation.cs` | 数据（**我本轮新加的分类器**） | 我域 |
| 6 | `core/events/CombatLogText.cs` | 内核/事件 | clean |
| 7 | `gameplay/sim/pipeline/DamagePipeline.cs` | 管线 | **M（在飞）** |
| 8 | `gameplay/sim/pipeline/DamageStep.cs` | 管线 | clean |
| 9 | `gameplay/sim/pipeline/EffectsStep.cs` | 管线 | clean |
| 10 | `gameplay/sim/enemy/EnemyAi.cs` | 敌方 AI | **M（在飞）** |
| 11 | `gameplay/sim/run/ExpeditionSession.CampAndBonuses.cs` | 远征会话 | **M（在飞）** |
| 12 | `gameplay/sim/morale/MoraleLedger.cs` | 士气账本 | **M（在飞）** |
| 13 | `gameplay/sim/run/RunSession.cs` | 会话 | clean |
| 14 | `gameplay/sim/pipeline/ShieldGuard.cs` | 管线（护盾/护卫） | clean |
| 15 | `gameplay/sim/skill/SkillExecutor.cs` | 技能执行 | **M（在飞）** |
| 16 | `data/SkillsConfig.cs` | 数据 | **M（在飞）** |
| 17 | `gameplay/sim/skill/SkillTargetResolver.cs` | 技能目标 | **M（在飞）** |
| 18 | `data/TuningConfig.cs` | 数据（**门禁超限 811**） | **M（在飞）** |

```
🔴 **其中 9 个是在飞 `M`** ⇒ 📌 这直接印证了 M3 的"搬家必须带行为"为什么危险：
   改 buff 语义会**同时打到 9 个正在被别人修改的文件** ⚠️ ⇒ **必须等那批落盘**（或与作者协同）才能动 ✓
✅ 归属：**17 个属主程序域 · 1 个属 UI 域**（`BattleUI.Render.cs`）⇒ M3 不是纯主程序活 ✓
⚠️ 且 `TuningConfig.cs` 既是消费点、又是**门禁超限件**（811 行）⇒ 搬家前后都要过门禁 ✓
```
## 3. 根态 vs 派生（以 `STUN` 家族为例 · 原版 `base.buffs.json`）
```
`STUN*` 条目 26 个，关键词分布：`SKILL` 12 · `RESIST` 9 · `DMG` 2 · `ACCDEBUFF` 1 · `BLIGHT` 1
   ⇒ 📌 **绝大多数是【派生标记】**（技能标记/抗性修正），不是"眩晕这个态本身" ✓
   ⇒ ⚠️ 我用 id 名判"根态"得到 **0 条** ✗ ⇒ **说明根态不在这张表的 `STUN` 前缀里**（可能在技能/效果表，
      或原版把"态"表达为 `rule_type`/`duration` 的组合 ✓）⇒ 这是**下一步要量清的点** ✓
   ⇒ 对我方的含义：我方 `stun` 是**一个 buff**（`state_flag: stunned` + `duration: action_skip`）✓
      与"原版用派生 buff 群表达同一态"**结构不同** ⇒ 映射时要按**语义**（不是按名字）✓
```
## 4. 结论 + 下一步（不依赖裁定）
```
✅ 已量：① 消费点清单（18 文件 / 56 处 · 9 个在飞）② 根态 vs 派生的初步分层（STUN 家族多为派生）✓
🔜 可续（只读）：③ 把 18 文件里的**具体行**列出来（给纪律 AA 一个逐行落点表）✓
             ④ 在**技能/效果表**里找"态"的权威表达（补上 §3 的空）✓
⛔ 不能动（等前置）：数据搬迁、消费点改动 —— M3 依赖 **M1/M2**，且 9 个消费点在飞 ✓
```
