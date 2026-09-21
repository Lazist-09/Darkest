# M2 激活（架构裁 **(丙)**）· **读数与前提性发现**（2026-09-21）

> 🔴 **架构的要求**：「**先激活"对应系统已存在"的 7 条** … **激活一条 ⇒ 一条用例 + 前后读数**（否则"激活了"只是声称）
>   ＋ **未映射清单保持可打印**」✓

## 1. ✅ 已激活：**`hp_heal_percent`**（第 1 条）
```
落点：`SkillExecutor.ExecuteSupportPath`（`skill.HealFixed` 的治疗量计算 ✓）
语义（原版）：**施法者**的"治疗量"百分比修正 ⇒ `治疗量 ×(1 + pct/100)` ✓
实现：
  · 🆕 `HealAmount.Scale(baseHeal, pct)`（**纯函数** ⇒ 可单测 ✓）+ `ScaleByCaster(buffs, caster, base)` ✓
  · 🆕 `BuffLedger.PercentModAny(u, effect)`（跨 kind 求和 · **附加式**：`PercentMod` 语义未动 ✓）
  · 🆕 `IBuffLedger.PercentModAny`（接口 + 唯一实现者 ✓）
  · `SkillExecutor` 改为调纯函数（**取修正 + 调算法**两行 ✓）
📊 **前后读数**（用例实跑 · `M2HealPercentActivationTests` 3/3 ✓）：
   **前**：`pct = 0 ⇒ 原样`（1/5/12/15/100/999 全等 ✓）＋ **全量 798/798 不变** ✓
   **后**：`12 +25% ⇒ 15` · `12 −40% ⇒ 7` · `12 +100% ⇒ 24` · `5 −1% ⇒ 5` · `15 −33% ⇒ 10` ✓
   **清单**：`已激活 1 条（hp_heal_percent）· 仍冻结 13 条`（**可打印** ✓ 架构要求 ✓）
全量：**801/801** ✓（798 + 本件 3 条 ✓）
```

## 2. 🔴 **前提性发现（我上轮的"7 条可立即接"过于乐观 ⚠️）**
```
我把 7 条的**落点档位**逐一查了（实测）：
   `BuffLedger` 的持有者 = `ShieldGuard` · `BattleDirector` · `MoraleLedger` · `DamagePipeline` · `SkillExecutor`
   ⇒ **全部是【战斗级】** ✓
| 原语 | 落点 | 档位 | 今天能接吗 |
| `hp_heal_percent` | 战斗内治疗（`SkillExecutor`） | **战斗级** | ✅ **能**（本条已做 ✓） |
| `party_surprise_chance` | `ExpeditionSession.RollAmbush` | **趟级** | ✗ **buff 修正到不了** |
| `monsters_surprise_chance` | 同上 | 趟级 | ✗ 同上 |
| `scouting_chance` | 侦察（趟级） | 趟级 | ✗ 同上 |
| `starving_damage_percent` | `HungerSpawner.StarveDamage` | 趟级 | ✗ 同上 |
| `food_consumption_percent` | `HungerSpawner.FoodRequired` | 趟级 | ✗ 同上 |
| `remove_quirk_chance` | 怪癖移除 | **城池级** | ✗ 同上 |
⇒ 📌 **真正的前置不是"系统存在"，而是「趟级/城池级也要能读到修正」** —— 那需要**一层新结构** ✗
   （例如：run 级修正载体 / 把战斗级台账上提 / 或"趟级 buff"概念 ✓）⇒ 🔴 **这是设计决定 ⇒ 请你裁** ✓
```
## 3. 我建议的两条路（**都不擅自做**）
```
**(甲) 加"趟级修正"载体**：`ExpeditionSession` 持一个 `RunModifiers`（与 `BuffLedger` 同形但生命周期 = 一趟 ✓）
   ⇒ 6 条一起可接 ✓ 但要定"趟级 buff 从哪来"（营火/Curio/饰品 ✓）
**(乙) 只做战斗级**：那就**只激活 `hp_heal_percent`**（已完成 ✓），其余 6 条**留在冻结清单**（可打印 ✓）
   ⇒ 等 M3/M4（它们本来就要引入压力/决心/怪癖轴 ✓）
📌 **我的倾向**：**(乙) 先** —— 因为那 6 条里有 4 条（伏击/侦察/饥饿）**语义上本来就是"趟级"** ✓，
   而 M3/M4 会带来"修正轴"的正式设计 ⇒ **现在硬造一层可能白做** ✓
```
