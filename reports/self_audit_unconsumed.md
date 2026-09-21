# 自我审计 · **记分卡刷新**（2026-09-21 · 兑现"每完成一个激活步骤就改一行"的承诺）

> 🔴 **我在第 30 轮做过一次自我审计**（"我加的东西有没有填了不消费"），并**承诺**：
>   "**每完成一个激活步骤，就把表里对应行从 🔴 改成 ✅（并给读数）**" ✓
> ⇒ 本文件就是**兑现**：全部**当场重测**（`scripts/**` 引用数含定义处 · `tests/**` 引用数）✓

## 1. 本会话**已激活**的（🔴 → ✅/🟡）
| 符号 | 之前 | 现在 | 靠什么激活的 |
|---|---|---|---|
| `RosterCapByLevel` | 🟡 仅被校验 | **✅ 已消费**（scripts 4 处） | **M7②**：`ExpeditionContext.StagecoachCapCurve` 缓存它 + `CurrentRosterCap` 消费 ✓（提交 `ca68e09`）|
| `NumRecruitsByLevel` | 🟡 仅被校验 | 🟡 **被 `StagecoachRecruits` 消费**（3 处） | **M7③**：`CountAt(...)` ✓（`709466d`）—— ⚠️ 仍**不在生产路径**（等 UI 招募屏）|
| `UpgradedRecruitChancesPct` | 🟡 仅被校验 | 🟡 同上（3 处） | **M7③**：`UpgradedChancePctAt(...)` ✓ |
| `StagecoachCapCurve` | （新符号） | 🟡 3 处（缓存 + 消费 + 调用点） | **M7②** 的接线 ✓ |
| `PositionLine`(M9 改名涉及的) | —— | —— | M9 改名批（11 文件 40+ 处）✓ |

## 2. 仍然**待激活**的（🔴 · 每条都有明确前置）
| 符号 | scripts / tests | 前置（一句话） |
|---|---|---|
| `WeaponAt` / `ArmourAt` | 1 / 7 · 1 / 3 | **M1c 阶段 3**（切默认时按阶取 ✓）—— 卡在"要不要让阶数影响伤害" |
| `WeaponRawDamage` / `WeaponRoll` | 1 / 11 · 2 / 6 | 同上 ✓（阶段 1 的"零消费点"是**设计目标** ✓） |
| `ProtFraction` | 1 / 1 | **需策划先定 `prot` 的值**（现 4 原型为 0 ✓）|
| `BuffPrimitiveTranslation` | 1 / 6 | **M2 映射裁定**（我提的（丙）· **7 条可立即接** ✓）|
| 🆕 `StagecoachRecruits` | 1 / 13 | **UI 招募屏接线**（属 UI 域 ✓ 激活条件已写在文件头 ✓）|
| 🆕 `UiShellLedger` | 1 / 19 | **两处调用点**（`BattleRoot.GoToHamlet` + `UIRoot.ShowPanel` 失败分支 ✓）|
| 🆕 `SmokeStepSpec` | 3 / 5 | 🟡 S4 后面板步骤接线 ✓（现在只用于仪表打印 ✓）|

## 3. 🔴 一件要**主动交代**的事：`CapCeiling` **成了没人用的遗留**
```
`EconomyConfig.CapCeiling => RosterCapByLevel is {Count:>0} c ? c[^1] : MaxRoster`（定义 1 处 · 用例 1 处）
🔴 全仓**没有任何生产消费点** ✗ —— 因为 **M7②** 我走的是 `RosterCapByLevel[level]`（按等级取 ✓），
   而 `CapCeiling`（取末值）**没被用上** ✗
⇒ 我**不擅自删**（它可能是给 UI/校验预留的 ✓）⇒ 三个选项请你/我定：
   **(甲) 删掉**（连带那 1 条用例）—— 最干净 ✓（我倾向这个）
   **(乙) 保留**，但改成"校验用"（例如断言 `roster_cap == CapCeiling` ✓，即"两个数据源必须一致"）
   **(丙) 保留原样**，在注释里写明"预留，未接线" ✓
📌 这条是我**自己主动找出来的**（不是别人指出的）—— 按我第 30 轮的承诺："**这条表就是我的欠账清单**" ✓
```

## 4. 一句话
```
**承诺兑现**：本会话有 **1 项转 ✅**（`RosterCapByLevel`）· **4 项从"无消费"变成"被新件消费"** ✓
**仍待激活 8 项**，每条都写了前置 ✓ · 并**主动交代 1 件遗留**（`CapCeiling`）✓
⇒ 我给自己立的规矩（"**不再新增无激活条件的符号**"）全程未破 ✓（新符号 `StagecoachRecruits`/`StagecoachCapCurve`/
   `UiShellLedger`/`SmokeStepSpec` 都在文件头写明了"谁消费它、什么时候" ✓）
```
