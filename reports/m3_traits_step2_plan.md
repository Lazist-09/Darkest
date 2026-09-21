# M3 第 2 步计划（**带行为的那一半** · 未执行）

> 🔴 卡里对 M3 的硬要求：「🔴 **"搬家必须带行为"（纪律 AA）** · 8 文件约 20 处消费点**一起改**」✓
> ⇒ 第 1 步（**安全的一半**）已完成：`traits.json` + `TraitsConfig`（**7 条** · 值原样 ✓ · 零行为 ✓）
> ⇒ 本文件列第 2 步（**高风险的一半**）要动什么、以及为什么现在**不该动** ✓

## 1. 第 1 步做了什么（已完成 · 读数）
```
🆕 `darkest/data/traits.json`（**7 条**：折磨 3 + 美德 4 ✓ 由 `tools/dsh/extract_ours_traits.py` **生成** ✓）
🆕 `TraitsConfig`（解析 + 三条 P 检查：id 唯一 / `kind↔ends_at` 自洽 / **可回溯 `source`** ✓）
📊 用例：7 条 ✓ · 与 `buff_defs.json` **逐字比对**（`ends_at` ✓）· 7/7 有 `source` + `origin: ours` ✓
🔴 **零行为**：`MoraleLedger` 等**仍读 `buff_defs`** ⇒ 行为一字不改 ✓
📌 边界如实记录：按"时长族"判会得到 **9** 条 ⇒ 多出的 `bound` / `pep_talk` 按卡里 **③ 自加**排除，
   理由写进生成器（`bound`：原版 buff 表 **0 命中** · `pep_talk`：**技能给的**）⇒ **7 与 9 的分歧不许藏** ✓
```

## 2. 第 2 步要动的（**逐条登记** · 待架构/策划点头后执行）
| # | 消费点（现状） | 改法 | 行为影响 |
|---|---|---|---|
| 1 | `MoraleLedger`：决心判定后**挂 buff**（`affliction_*` / `virtue_*`） | 改挂 **trait**（读 `TraitsConfig` ✓） | 🔴 有（但**语义等价**：同 7 条、同修正 ✓） |
| 2 | `AfflictionProcs`（`refuse_skill` / `refuse_heal` / `randomize_attack_target` 的 33%） | 改成读 trait 的修正 | 🔴 有（数值不变 ⇒ 只是来源变 ✓） |
| 3 | `SkillExecutor` 的自私 proc（`affliction_selfish` ✓） | 同上 | 🔴 有 |
| 4 | `MoraleLedger.SupportSlotRegen`（虚弱回升 ✓） | 同上 | 🔴 有 |
| 5 | UI 侧展示（`affliction` 名/图标 ✓） | 属 **UI 域** ⇒ 只提供 trait 名 ✓ | 🔴 UI 域 |
| 6 | 存档/重放（若 trait 与 buff 并存 ⇒ 双份真值 ✗） | 必须**同时删掉 buff_defs 里的 7 条** | 🔴 有 |
| 7 | 测试夹具（凡断言 `affliction_*` buff 的用例 ✓） | 跟改 | — |
| 8 | 冒烟/事件流（`BuffAppliedEvent` 的名字 ✓） | 决定是否新增 `TraitAppliedEvent` | 🔴 有 |

## 3. 🔴 为什么现在**不做**第 2 步（三条理由）
```
① **它不是"结构对齐"，是"行为搬家"**：7 条要**从 buff_defs 移除**并改 `MoraleLedger` 的挂载 ⇒
   按纪律 **AA**，必须"8 文件约 20 处一起改 + 前后读数" ⇒ 现在是**一次性的高风险动作** ✓
② **卡里 ②（机制态换原版对应态）与 ③（自加 5 条按红线 30 移层）会同时改到同一批文件** ✓
   ⇒ 三件事**一起做**才划算（否则同一批 20 处要改三遍 ✗）
③ 🔴 **架构/策划已裁的边界**：`bound` / `pep_talk` 的归属、`taunt` 的三条边界 ⇒ 第 2 步要用到 ✓
⇒ 📌 **建议**：等 ②③ 的处置口径一并确认后，**一次做完**（那时我按登记表逐条执行 + 附前后读数 ✓）
```
