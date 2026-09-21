# 自我审计：**我加的东西，有没有"填了不消费"**（2026-09-21 实测）

> 🔴 **动机**：我这两轮一直在审计别人的"填了不消费"（有字段、无消费点）⇒ **同一把尺子必须先用在自己身上** ✓
> 🔴 **方法**：对我在本会话里新增的每个符号，数 `darkest/scripts/**` 里的引用（含**定义处**）与 `darkest/tests/**` 里的引用 ✓
>   ⇒ `scripts == 1` 即 **只有定义、生产代码无人读** ✓ ⇒ 逐条给出**激活条件**（何时才该有人读它 ✓）

## 1. 实测表（`scripts` 含定义处 · `tests` 为用例引用）
| 符号 | scripts | tests | 判断 | **激活条件**（何时该有生产消费者） |
|---|---|---|---|---|
| `WeaponTier` / `ArmourTier` | 4 / 4 | 0 / 0 | ✅ 结构已被解析层与 `UnitStats` 携带 | —— （数据载体，恒被解析器读 ✓） |
| `WeaponAt` / `ArmourAt` | **1** / **1** | 7 / 3 | 🔴 **仅定义 + 测试** | **M1c 阶段 3**（切默认伤害模型时按阶取 ✓） |
| `WeaponRoll` / `WeaponRawDamage` | 2 / **1** | 6 / 9 | 🔴 **仅定义 + 夹具** | **M1c 阶段 3**（阶段 1 的"零消费点"是**设计目标** ✓） |
| `ProtFraction` | **1** | 0 | 🔴 **仅定义** | 「`prot` 接结算」⇒ **需策划先定 `prot` 的值**（现 4 原型为 0 ✓） |
| `PoisonResist` / `DiseaseResist` / `TrapResist` | 5 / 5 / 5 | 5 / 2 / 5 | ✅ 已被 `EffectsStep` 的判定轴消费 | —— ✓ |
| `HpPercentLastRunEnd` | 5 | 4 | ✅ 已被 `Describe`/`DiffLines` 消费 | —— ✓（接线宿主待 `ExpeditionContext` 脱手） |
| `CapCeiling` | **1** | 1 | 🔴 **仅定义 + 测试** | **M7 第 ② 步**（`Roster` 改读曲线 + `max_roster` 提到 28 + 撤 `unlocks.roster_cap_delta`）⇒ **卡在在飞 `Roster.cs`** ✓ |
| `RosterCapByLevel` / `NumRecruitsByLevel` / `UpgradedRecruitChancesPct` | 3 / 2 / 2 | 3 / 2 / 2 | 🟡 **仅被校验与用例读** | 同上（M7 ②）✓ |
| `TrinketsConfig` | 5 | 7 | 🟡 解析 + 校验 + 用例 | 商店接线（`price`/`award_category` 的消费方）✓ |
| `QuirksConfig` | 5 | 5 | 🟡 解析 + 校验 + 用例 | 名册/英雄详情的 Quirk 显示与互斥判定接入 ✓ |
| `BuildingsConfig` | 5 | 8 | 🟡 解析 + 校验 + 用例 | 城建/升级消费方（读 `code`/`currency_cost`/`prerequisites`）✓ |
| `BuffPrimitiveTranslation` | **1** | 6 | 🔴 **仅定义 + 用例** | **M2 映射裁定 (甲)/(乙)/(丙) 之后** ✓ |
| `AreIncompatible` / `IsPurchasable` | 2 / 2 | 3 / 2 | 🟡 单一落点已就位 | 各自的消费方（互斥判定 / 商店）✓ |

## 2. 结论（区分三类，不含糊）
```
✅ **有生产消费者**（3 项）：三轴抗性 · `HpPercentLastRunEnd` · tier 数据载体
🟡 **有"结构消费者"**（校验器/解析器读它 ⇒ 不是死字段）：三个 M4/M5/M6 结构件 + M7 三条曲线
🔴 **暂无可避免地无消费者**（5 类）：`WeaponAt/ArmourAt` · `WeaponRoll/WeaponRawDamage` · `ProtFraction` ·
   `CapCeiling` · `BuffPrimitiveTranslation`
   ⇒ **它们全是"阶段 1 / 骨架"的产物**，且**每一条都写明了激活条件**（上表右列）✓
   ⇒ 这正是架构的 M1c 三步走与 M7 分两步所**要求**的形态：**先建结构、后接线** ✓
```
## 3. 我的承诺（可被检验）
```
① **不再新增"无激活条件"的符号** —— 若我下次要加只有定义的东西，必须同时写下**谁会消费它、什么时候** ✓
② 每完成一个**激活步骤**，我会把上表对应行**从 🔴 改成 ✅**（并给出读数）✓
③ 这份表就是我的**欠账清单**：5 类 8 个符号待激活 —— 它们不是我"忘了接"，而是**按计划等前置** ✓
```
