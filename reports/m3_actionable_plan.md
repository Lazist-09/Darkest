# M3 前置侦察（收口）：可执行清单 + 一处**我未挖到底**的点（2026-09-21）

> 🔴 本文把 M3 的**只读侦察**收口：给出**逐文件落点**与**明确的下一步**，
>    并**如实标注**我在哪一步停了、为什么停（不让"没做完"伪装成"做完了" ✓）

## 1. 逐文件落点（生产侧 **56 处 / 18 文件** · 实测）
| 文件 | 处数 | 在飞 | 备注 |
|---|---|---|---|
| `gameplay/sim/skill/SkillExecutor.cs` | **11** | **M** | 技能执行（态的主战场） |
| `data/SkillsConfig.cs` | **7** | **M** | 技能表解析（buff id 出现在技能数据里） |
| `gameplay/sim/director/BattleDirector.cs` | **6** | **M** | 回合/态驱动 |
| `gameplay/sim/director/BattleDirector.OvertimeAndRetreat.cs` | 3 | **M** | 加时/撤退 |
| `gameplay/sim/run/ExpeditionSession.CampAndBonuses.cs` | 3 | **M** | 营火增益 |
| `gameplay/sim/pipeline/ShieldGuard.cs` | 3 | clean | 护盾/护卫 |
| `data/TuningConfig.cs` | 3 | **M** | 数值件（**门禁超限 811**） |
| `gameplay/sim/pipeline/EffectsStep.cs` | 3 | clean | 效果判定 |
| `data/BuffPrimitiveTranslation.cs` | 3 | 我域 | 我本轮新加的分类器 |
| `gameplay/sim/run/RunSession.cs` | 2 | clean | 会话 |
| `core/events/CombatLogText.cs` | 2 | clean | 日志文案 |
| `gameplay/sim/pipeline/DamageStep.cs` | 2 | clean | 伤害步 |
| 其余 6 个文件 | 各 1 | 混合 | 见 `m3_consumption_points.md` |

```
📌 **含义（M3 排期的硬约束）**：Top-3 消费点（`SkillExecutor`/`SkillsConfig`/`BattleDirector` = 24/56 处）
   **全在飞 `M`** ⇒ 在它们落盘前动 buff 语义 = 与别人的改动**正面相撞** ⚠️
```

## 2. 我**未挖到底**的一点（如实交代）
```
卡里 ② 要求"机制态 ⇒ 换原版对应态"，其中隐含一步：**先看清原版把"态"写在哪** ✓
   · 我已确认：`shared/buffs/base.buffs.json`（2020 条）里的 `STUN*` **绝大多数是派生标记**（SKILL 12 / RESIST 9）✓
   · 🔴 **"根态"在哪，我没找到**：`shared/**` 搜 `.stun` 无命中；`heroes/<hero>/` 先命中的是 **art** 文件
     ⇒ **下一个落点应是 `heroes/<hero>/<hero>.info.darkest`**（技能/效果定义）—— 我**本轮停在这里** ✓
   · **为什么停**：它**不影响 M3 的排期结论**（排期已被"9 个消费点在飞"锁死 ✗），
     且继续深挖属"为完整而完整"⇒ **等你/策划要这条时我再挖**（避免把时间花在不会改变决策的信息上 ✓）
```

## 3. 可执行清单（等前置解除后，按此顺序动）
```
① **前置**：那 9 个在飞消费点落盘（或与作者协同）· M1（属性的"态"）与 M2（原语层）到位 ✓
② **分类**：22 条 buff 按卡里三类处置 —— ① 折磨/美德 → `traits.json`（`TraitsConfig` · 无 Ledger）
   ② 机制态 → 换原版对应态（**按语义匹配**，复用 `BuffPrimitiveTranslation` 的分类 ✓）
   ③ 自加（`taunt`/`bound` 等**原版表 0 命中**者）→ 废掉或按红线 30 移层 ✓
③ **搬家必须带行为**（纪律 AA）：**56 处一起改**（清单见 §1）＋ **前后读数**（士气/折磨）✓
④ **每步过门禁**：`check_file_size.py`（`TuningConfig` 是红件，别让它更红 ✓）· 全量测试同数 ✓
```

## 4. 结论（一句话）
```
M3 的**侦察已够用**：消费点、归属、在飞冲突、根态/派生的初步分层都量清了；
**唯一未挖到底**的是"原版根态的表位置"（已写明下一落点与停下的理由 ✓）；
**排期的真正约束**不是信息，而是**9 个在飞消费点** ⚠️
```
