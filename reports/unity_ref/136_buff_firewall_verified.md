# buff 防火墙**独立复核**：清单 **25 = 参考 25，一一对应**（0 悬空）

> 🕒 2026-09-26 · 工具 `tools/dsh/verify_buff_checklist.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `135_*.md §6①` 的"读 `ValidateAgainst`"** ✓

---

## 1. 🎖️ 而这次我**不靠代码**，直接拿参考全集独立复算

```
📊 **做法**：抽 `BuffPrimitiveTranslation.cs` 的 `Checklist`（`Activated` + `Pending`），
   再抽参考 `JsonBuffs.json` 的 `stat_type` 全集，**两边对照** ✓
   🎖️ **判据（第 203 条）**：**"这个校验的目标是什么？
      ⇒ 拿【参考的真实全集】独立复算一遍，别只信校验通过"** ✓
```

## 2. 🎖️🎖️ 结果：**25 = 25，一一对应，差集两空**

```
📊 **`Checklist`（Activated + Pending）⇒ 25 个名字** ✓
   （`Activated` = `["hp_heal_percent"]` 1 个 · `Pending` = 24 个）
📊 **参考 `stat_type` 全集 ⇒ 25 种**（**全部来自 `JsonBuffs.json`**）✓
   `bleed_chance` · `combat_stat_add` · `combat_stat_multiply` · `damage_received_percent` ·
   `debuff_chance` · `food_consumption_percent` · `hp_heal_amount` · `hp_heal_percent` ·
   `hp_heal_received_percent` · `monsters_surprise_chance` · `move_chance` ·
   `party_surprise_chance` · `poison_chance` · `remove_negative_quirk_chance` · `resistance` ·
   `resolve_check_percent` · `resolve_xp_bonus_percent` · `scouting_chance` ·
   `starving_damage_percent` · `stress_dmg_percent` · `stress_dmg_received_percent` ·
   `stress_heal_percent` · `stress_heal_received_percent` · `stun_chance` · `upgrade_discount` ✓
📊 **对照**：
   · 🔴 **清单有而参考无**：**`[]`** ✓
   · 🔴 **参考有而清单无**：**`[]`** ✓
⇒ 🎖️🎖️ **即：完全一致 ⇒ 清单【就是】参考的 25 种全集** ✓
   📌 **意义**：**防火墙的目标达成** —— 且**清单与参考【同规模】**，
      说明**"参考的 stat_type 共 25 种，我方已全部登记"** ✓
```

## 3. 🎖️ 而注释讲的**三个历史错名**，现在是**真名**

```
📊 **`L183-187` 的原文**：
   > 🔴 **加载即校验（红线的防火墙）**：清单里每个名字**都必须**是参考项目真有的 `stat_type`。
   > WHY 必要：实测踩过 —— 旧清单里有 **`resolve_xp_percent` / `remove_quirk_chance` /
   > `dmg_received_percent`** 三个名字上游根本不存在（**真名是 `..._bonus_percent` /
   > `remove_negative_...` / `damage_...`**）⇒ 那 3 条**永远接不上**，而清单上却写着"待接" ⚠️
   > （`#290` **红线 21："写了但没接上"**）⇒ 从此**加载时就报**，不再靠人眼 ✓
⇒ 🎖️ **实测三个真名都在清单里**：`resolve_xp_bonus_percent` · `remove_negative_quirk_chance` ·
   `damage_received_percent` ✓
   ⇒ ✅ **即：三个都【已改名到位】** ✓
```

## 4. 🔴 而我自己**踩了一次子串误报**（第 37 次同族）

```
🔴 **我第一版判**："错名 `dmg_received_percent` ⇒ 在文件里 **仍在**" ⚠️
   ⇒ 📌 **查了行号才发现**：`L70`/`L126`/`L170` 那三处其实是
      **`stress_dmg_received_percent`**（**包含** `dmg_received_percent` 作为子串）⚠️
   ＋ 而唯一真出现 `dmg_received_percent` 的地方是 **`L185` 的注释**（讲历史）✓
⇒ 🎖️ **即：`dmg_received_percent` 【没有】作为独立名字存在** ✓
   🎖️ **判据（第 204 条）**：**"我搜到一个名字 ——
      它是【独立出现】还是【某个更长名字的子串】？⇒ 加词边界"** ✓
      📌 与第 26/66/83/125 条**同族（第 5 次）**：**"命中 ≠ 是它"** ✓
```

## 5. 🎖️ 所以"加载即校验"这一处也**四级齐**（与 `tuning` 那条同形）

```
📊 **两处对照**：
   | | `tuning.json` 的阈值校验 | **`buff_primitives` 的清单校验** |
   |---|---|---|
   | **存在** | ✅ `throw InvalidDataException` | ✅ `throw InvalidOperationException` |
   | **被调用** | ✅ 4 步链 | ✅ **`DirectorBridge.cs:55` 直接调** |
   | **路径必经** | ✅ 组合根 | ✅ **同一组合根** |
   | **负向测试** | ✅ `ConsecutiveMissDataTests` | ⚠️ **未核** |
⇒ 🎖️ **即：两处都是"加载即校验"，且都在同一组合根上** ✓
   ＋ 而注释明说 **"生产消费点：`DirectorBridge`（战斗装配时调用）"** ✓
   ⇒ 🎖️ **判据（第 205 条）**：**"同一个项目里有几处'加载即校验'？
      ⇒ 列成表 ⇒ 看是否【都】接在同一个组合根上"** ✓
```

## 6. 诚实边界

```
✅ **能验**：**`Checklist` 的 25 个名字（逐个）** · **参考 `stat_type` 的 25 种（逐个）** ·
   **两个差集都空** · **参考 25 种全部来自 `JsonBuffs.json`** ·
   **三个历史真名都在清单里** · **`dmg_received_percent` 只作子串/注释出现** ——
   **全部当场跑出** ✓
🎖️ 并**独立复算了那个校验的目标**（不只信"校验通过"）✓
🔴 **不能验**：**`ValidateAgainst` 有没有【负向测试】** ⇒ 📌 本件未搜 ⇒ 记**未核** ✓
   🎖️ **判据（第 206 条）**：**"两处同类校验 —— 一处的测试覆盖能推出另一处吗？
      不能 ⇒ 要各自查"** ✓
🔴 **不能验**：**`Activated` 只有 1 个（`hp_heal_percent`）而 `Pending` 24 个** ⇒
   📌 那说明**翻译进度 1/25** ⇒ ⚠️ **而这不是缺陷**（清单是"待办表"）⇒ 记**已知** ✓
🔴 **不能验**：**`Pending` 的 24 个在 `BattleMath`/`MoraleLedger` 里接上了没有** ⇒
   📌 那是**另一层**（清单校验只管"名字存在"）⇒ 记**未核** ✓
   🎖️ **判据（第 207 条）**：**"这个校验保证的是【名字存在】——
      那【接上了没有】是另一层，谁保证？"** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{checklist,stat_types,three_names,where_bad}.py`
   **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **buff 防火墙【独立复核】**：25 = 25 一一对应 · 0 悬空 · 3 个历史错名已改 ✓
🆕 **两条待核**：① `ValidateAgainst` 的负向测试 · ② `Pending` 24 个"接上了没有"
🆕 **可做**：① 🔴 **查 `ValidateAgainst` 的负向测试**（判据 206）
   ② **核 `Pending` 24 个的落地进度**（判据 207 —— 名字存在 ≠ 已接上）
   ③ 把第 203~207 条判据补进 `observe_list` D11
   ④ **把"加载即校验"的两处列成表写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
