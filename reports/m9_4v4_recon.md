# M9（4v4 槽位）· **侦察报告与改动清单**（2026-09-21 实测）

> 🔴 **卡 M9 原文**：「`SlotLayout` **降级：`SupportSlots` ⇒ 通用扩展位（可为空）** · `formation.json` 我方 4 槽 ·
>   **SP 去掉"支援位"维度**（按技能声明；`Skill.SupportPointCost ?? 0`）· 保留 `P19 regen_per_round ≥ 1`（`#419`/`O-60′`）。
>   验收：**两侧对称 4 槽 · 契约仍能表达 6 槽（回归用例）· SP：未声明不扣 / 声明才扣**」

## 1. 🔴 第一发现：**代码已是"通用槽位"模型** ⇒ 改动面比"506 处"小得多 ✓
```
`SlotLayout(int slotCount, int combatSlots, IReadOnlyList<int> supportSlots)` —— **没有写死 4 或 6** ✓
`formation.json` 里两侧是**数据**：`player {slot_count 6, combat_slots 4, support_slots [5,6]}` ·
`enemy {slot_count 4, combat_slots 4, support_slots []}` ✓
⇒ 所以 M9 **不是结构改造，而是"数据 + 语义"两件事** ✓
（"506 处"这个数字我实测对不上：`support` 字面在 `scripts/**` 是 **169 处 / 33 文件**，
  真正依赖 support **语义**的只有 **10 个文件** ⇒ 见 §3；数字差异请你按口径核对 ✓）
```
## 2. 子任务 A：**数据**（`formation.json` · 我域 ✓）
```
现状：`player.slot_count = 6` · `combat_slots = 4` · `support_slots = [5,6]` ·
      `initial_roster.player` = **6 人**（tank/warrior/commissar/medic + 5:warrior + 6:medic）
目标（卡）：**我方 4 槽** ⇒ ① `slot_count 6 → 4` ② `support_slots [5,6] → []`
      ③ `initial_roster.player` 6 → **4 人**（保留前 4 个？⇒ ⚠️ **这是内容决定**，需策划点头 ✓）
⚠️ 且 `FormationConfig` 可能校验"我方人数 = slot_count"⇒ 改数据要**同批**改名单 ✓（一次只改一类 ✓）
```
## 3. 子任务 B：**`SupportSlots` 语义降级**（= 卡的"通用扩展位（可为空）"✓）——**10 个文件**
| # | 文件 | 现行（实测行） | 4v4 后应变成 |
|---|---|---|---|
| 1 | `board/SlotLayout.cs` | `SupportSlots` 列表 + 越界校验（L16/18/31-42/56） | **通用扩展位**（可为空；语义不再等于"支援位"） |
| 2 | `data/FormationConfig.cs` | L16 字段 · L107 敌方不得有支援位 · L180 遍历支援位 | 改成"扩展位"口径（敌方同理） |
| 3 | `board/FormationBoard.cs` | L19/33 `_supportSlots` HashSet · **L88 `SlotKind.Support`** | 位种类命名/判定改口径 |
| 4 | `board/FormationBoardFactory.cs` | L43 构造 `SlotLayout` | 跟数据走（应无需改 ✓） |
| 5 | `director/BattleDirector.cs` | **L134** 遍历支援位 · **L189** `skill.SupportPointCost ?? (IsSupportSlotActor ? SupportCostSkill : 0)` · L268 支援位判断 · L358 `IsSupportSlotActor` | 🔴 **SP 去掉支援位维度**（见子任务 C） |
| 6 | `director/BattleProjector.cs` | L78/80/85 同上（"#211 技能声明优先"） | 同上 |
| 7 | `morale/MoraleLedger.cs` | L312 遍历支援位 | 改口径 |
| 8 | `skill/SkillExecutor.cs` | L114 支援位判断 | 改口径 |
| 9 | `skill/SkillUseResolver.cs` | L23 `IsSupportSlotActor` · L120 SP 不足才拒 | 改口径（**契约字段名**可能也要顺带改 ✓） |
| 10 | `data/SkillsConfig.cs` | L93 `support_point_cost` 字段 | **保留**（"按技能声明"✓） |
```
🔴 注意：**没有一个是在飞文件**（我上次已把 252 项全部入库 ✓）⇒ 现在**可以动** ✓
   ⇒ 但 1~9 里有多处是**战斗行为**（谁被拒、扣不扣 SP）⇒ 必须走"**前后读数对照**"（解冻四件 ✓）
```
## 4. 子任务 C：**SP 去掉"支援位"维度**（行为改动 ⚠️）
```
现行（`BattleDirector.cs:189` / `BattleProjector.cs:80`）：
   `int spCost = skill.SupportPointCost ?? (IsSupportSlotActor(actor) ? SupportCostSkill : 0);`
   ⇒ 即：**技能没声明时**，站在支援位的人仍按 `SupportCostSkill` 扣 ✓
目标（卡）：**"未声明不扣 / 声明才扣"** ⇒ 上式退化成 `skill.SupportPointCost ?? 0` ✓
⚠️ 这是**行为改动**（会少扣 SP）⇒ 需要：① 前后读数 ② 受影响的既有用例（`SkillUseResolver` 的 `SupportPointsNotEnough` 路径 ✓）逐条改
✅ 保留：`P19 regen_per_round ≥ 1`（`#419`/`O-60′`）—— `BattleDirector.cs:106-113` 的回复逻辑**不动** ✓
```
## 5. 子任务 D：**契约仍能表达 6 槽**（回归用例 · 我域 ✓ 零行为）
```
卡的验收明写："**契约仍能表达 6 槽（回归用例）**" ⇒ 即：4v4 是**数据选择**，不是把模型砍到只能 4 ✓
⇒ 我会加：① `SlotLayout(6, 4, [5,6])` 仍可构造 ✓ ② `SlotKind` 对 6 槽的表达 ✓
  ③ **两侧 4 槽**的断言 ✓（读 `formation.json` 实测，而不是写死 ✓）
```
## 6. 结论与顺序（**建议**）
```
① **D（回归用例）先做** ⇒ 零行为，且把"契约仍能表达 6 槽"钉住 ✓
② **B（语义降级）其次** ⇒ 但会碰行为 ⇒ 先出**现状读数**（谁在支援位、扣多少 SP ✓）
③ **C（SP 维度）第三** ⇒ 同上，前后对照 ✓
④ **A（数据 6→4 槽）最后** ⇒ 因为它依赖 ①②③ 的口径，且名单改成 4 人**需策划点头** ✓
🔴 卡里 M9 的"数值"（SP 剂量）在**解冻清单**里（`O-95`）⇒ 我**不碰数值**，只改机制/口径 ✓
```
