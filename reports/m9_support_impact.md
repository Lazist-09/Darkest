# M9 子任务 B/C · **影响面细读数**（2026-09-21 实测）

> 🔴 **本报告回答一个问题**：把 `SupportSlots` 降级成"通用扩展位"、并把 SP 的"支援位维度"去掉，
>   **到底是"改名"还是"改行为"**？✓
> 🔴 **结论先说**：**B 基本是改名（零行为）· C 是行为改动**，但 —— ⚠️ **支援位不只承载 SP**：
>   它还承载 **①一个目标规则** 与 **②一处士气逻辑** ⇒ **这两处必须由架构/策划裁定去留** ✓（我不代裁 ✓）

## 1. 全部站点（**10 个文件 · 32 处** · 逐行实测）
| 文件 | 处 | 关键行 | 性质 |
|---|---|---|---|
| `core/contracts/SlotLayout.cs` | 8 | L16/18/31-42/56 | 🟢 **纯模型**（构造 + 越界校验）⇒ 改名即可 |
| `data/FormationConfig.cs` | 3 | L16 字段 · **L107 敌方不得有支援位（校验！）** · L180 遍历 | 🟡 L107 是**规则**，不是名字 |
| `board/FormationBoard.cs` | 3 | L19/33 HashSet · **L88 `SlotKind.Support`** | 🟡 位种类枚举值（改名波及面） |
| `board/FormationBoardFactory.cs` | 1 | L43 构造 | 🟢 跟数据走 |
| `director/BattleDirector.cs` | 9 | 🔴 **L189 SP 兜底扣费** · 🔴 **L268 目标规则（用支援位）** · L335 `SupportCostSkill` · L358-359 `IsSupportSlotActor` · L134 遍历 | 🔴 **行为** |
| `director/BattleProjector.cs` | 3 | 🔴 L78/80/85（**投影里的同一条 SP 兜底**） | 🔴 **行为**（与 L189 必须同步改 ✓） |
| `morale/MoraleLedger.cs` | 1 | 🔴 **L312 遍历支援位** | 🔴 **行为**（支援位影响士气结算？⇒ 需裁 ✓） |
| `skill/SkillExecutor.cs` | 1 | 🔴 L114 `SupportSlots.Contains(casterPos)` | 🔴 **行为** |
| `skill/SkillUseResolver.cs` | 2 | L23 契约字段 · L120 SP 不足才拒 | 🟡 契约字段名 + 判定 |
| `data/SkillsConfig.cs` | 1 | L93 `support_point_cost` | 🟢 **保留**（卡："按技能声明"✓） |

## 2. 回归面（**12 个测试文件** · 按引用数）
```
30 处 SupportPointTests.cs　12 M9SlotContractTests.cs　10 SupportPackSpTests.cs　9 BoardTests.cs
 7 Policies.cs　6 BoardTests.Movement.cs　5 M6FixPackTests.cs　3 PassPenaltyTests.cs
 3 G0aEventSourcingGateTests.cs　2 M75VerificationPackTests.PolicyAndSelfCheck.cs
 1 AfflictionProcTests.cs　1 MoraleTests.cs
⇒ 其中真正会因 **C（SP 去支援位维度）** 需要改读数的，我估计集中在
   `SupportPointTests` / `SupportPackSpTests` / `Policies`（**SP 口径**）三件 ✓
```

## 3. 🔴 **要请架构/策划裁的三件事**（我不代裁）
```
① **`BattleDirector.cs:268` 的"目标规则"用支援位**：它决定了**谁能打到支援位**（或谁不能）✓
   ⇒ 4v4 后"通用扩展位"还要不要这条规则？⇒ **需要一句话** ✓
   （若要去掉 ⇒ 它是**行为改动** ⇒ 要前后读数 ✓）
② **`MoraleLedger.cs:312` 遍历支援位**：支援位参与士气结算 ✓
   ⇒ 同样：这条要不要保留？✓
③ **`FormationConfig.cs:107` 的校验**"敌方不得有支援位"
   ⇒ 若语义变成"通用扩展位"，这条校验是否还成立（敌方也能有扩展位？）✓
```

## 4. 我建议的**安全执行顺序**（在你答 ①②③ 之后）
```
① **B-改名**（零行为）：`SupportSlots → ExtensionSlots`（或你指定名）+ `SlotKind.Support → SlotKind.Extension`
   波及：`SlotLayout`/`FormationBoard`/`FormationBoardFactory`/`SkillUseResolver` 的**字段名**（纯改名 ✓）
   🔴 判据：**全量用例数不变 + 零行为**（797/797 ✓）
② **C-行为**（SP 去支援位维度）：`BattleDirector.cs:189` + `BattleProjector.cs:80` 的兜底一起删
   ⇒ 前后读数（谁被少扣多少 SP）+ 改 3 个 SP 用例的读数 ✓
③ **① ② 的"目标规则/士气"**：按裁定的结果处理（要留 ⇒ 改成"扩展位"口径；不留 ⇒ 单独一次行为改动 ✓）
④ **A-数据**（6→4 槽 + 名单 4 人）：等策划点头 + 与 ①②③ **同批**（因为人数=槽数的校验联动 ✓）
```

## 5. 一句话总结
```
**B 是改名（零行为，可安全做）· C 是行为改动（我可以做，但要前后读数 + 改 3 个用例）·
  真正的未知只有三处规则（目标规则/士气/敌方校验）⇒ 需要架构或策划一句话** ✓
⚠️ 且我**不碰数值**（SP 剂量在解冻清单 `O-95` 里 ✓）
```
