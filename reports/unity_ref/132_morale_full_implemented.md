# `morale_full_100` 的"自身回 50"**已实现** —— `morale.start = 50`，`L300` 就是它

> 🕒 2026-09-26 · 承接 `131_*.md §5` 的"`morale_full_100` 的附带效果实现没有" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 判决：**已实现**（不是"只写了计划"）

```csharp
// MoraleLedger.cs:291-302
public void HandleMoraleMax(UnitRuntime unit, UnitRuntime[] team, IRngProvider rng, CombatLog log) {
    **GrantVirtue**(unit, rng, log);                       // ① 授予美德
    if (!_moraleFullTeamBonusDone) {
        _moraleFullTeamBonusDone = true;
        ApplyTeamOnce(team, "morale_full_100", log);       // ② 全队 +10（once_per_battle）
    }
    **unit.Morale = _balance.MoraleStart**;                // ③ 自身回 50（morale §7）
    unit.CollapseEmber = false;                            // ④ 清余烬
}
```
```
📊 **`tuning.json:2`** ⇒ `"morale": { "min": 0, "max": 100, "**start": 50**, … }` ✓
⇒ 🎖️ **即：`_balance.MoraleStart` = 50** ⇒ ✅ **`L300` 正是 note 说的"自身回 50"** ✓
   📌 **判据（第 190 条）**：**"`note` 里提到的【附加效果】——
      代码里找得到吗？（`note` 可能只写了计划）"** ✓
      ⇒ ✅ **这次【找得到】** ✓
```

## 2. 🎖️ 而 `HandleMoraleMax` 一共做了**四件事**（比 note 说的多）

| # | 行 | 做什么 |
|---|---|---|
| ① | `L293` | **`GrantVirtue`** ⇒ **授予美德**（note **没提**）✓ |
| ② | `L297` | `ApplyTeamOnce(team, "morale_full_100")` ⇒ **全队 +10**（**一次性**）✓ |
| ③ | **`L300`** | **`unit.Morale = MoraleStart`（50）** ⇒ **note 说的"自身回 50"** ✓ |
| ④ | `L301` | **`CollapseEmber = false`** ⇒ 清余烬 ✓ |

```
⇒ 🎖️ **即：note 只写了"自身回 50"，而实现【还做了美德 + 全队 +10 + 清余烬】** ✓
   📌 **判据（第 191 条）**：**"`note` 的粒度与代码的粒度【对得上】吗？
      可能 note 只提了最显眼的那一条"** ✓
      ⇒ ✅ **这不是缺陷**（note 不是规范）⇒ 但**要记下"note 更粗"** ✓
```

## 3. 🎖️ 而 `TuningConfig` 里的一处注释**解释了整个设计意图**

```
📊 **`TuningConfig.ExpeditionAndCombat.cs:198`**：
   > · 美德走士气 == 100（`HandleMoraleMax`）且**立即把士气拉回 50** ⇒
   >   **美德与折磨【永不并存】** ✓
⇒ 🎖️🎖️ **即：③ 的作用是【互斥保证】** ——
   · **折磨**：士气 0 挂上 · 回到 `morale.start`（50）解除 ✓
   · **美德**：士气 100 挂上 · **立即回 50** ⇒ ⇒ **不可能同时"满"和"零"** ✓
   ＋ 而 `tuning.json:111` 的注释也写：
      > `morale_affliction_threshold` **必须 == `morale.start`**（加载期校验强制）——
      > 折磨**只**在士气 0 挂上、**只有**回到 `morale.start` 才解除
      ⇒ ✅ **两处注释互证** ✓
   🎖️ **判据（第 192 条）**：**"这个'立即回 50'看起来多余 ——
      但它可能是【不变量】的实现（如互斥）；找注释"** ✓
```

## 4. 🎖️ 所以本轮的两条待核**都闭环了**

```
📊 **`130_*.md` 提出的两条**：
   ① **`battle_inspiration` 是否由技能侧给** ⇒ ✅ **`131_*.md` 已答**（note 说是"对账锚点"）✓
   ② **`morale_full_100` 的"自身回 50"实现没有** ⇒ ✅ **本件已答**（`L300` 就是）✓
⇒ 🎖️ **即：`130_*.md` 的"3 条无消费点"里，2 条已自解释，
   剩 `retreat_success_with_death` 一条** ✓
   📌 **而那一条【无 note】** ⇒ ⚠️ **无法自解释** ⇒ ✅ **保持"待报"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`HandleMoraleMax` 的 291-302 全文（四件事）** ·
   **`tuning.json:2` 的 `morale.start = 50`** ·
   **`TuningConfig.ExpeditionAndCombat.cs:198` 的"永不并存"注释** ·
   **`tuning.json:111` 的"阈值必须 == `morale.start`"注释** —— **全部当场跑出** ✓
🎖️ 并**把"待核"清到只剩 1 条** ✓
🔴 **不能验**：**`GrantVirtue` 的实现**（如何选美德）⇒ 📌 本件只见调用 ⇒ 记**未读** ✓
🔴 **不能验**：**那两条注释所说的【加载期校验】是否真的存在** ⇒
   📌 注释说"必须 == `morale.start`（加载期校验强制）" ⇒ ⚠️ **本件未找到该校验的代码** ⇒
   记**待核**（**可能又是"注释写了但没实现"**）✓
   🎖️ **判据（第 193 条）**：**"注释说'加载期校验强制'——
      那个校验【真的在】吗？（我上一件的教训：注释可能只写了计划）"** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{heal50,morale_start}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **本轮两条待核【闭环】**：`battle_inspiration`（对账锚点）· `morale_full_100`（已实现）✓
🆕 **一条新待核**：🔴 **注释说的"加载期校验"（阈值必须 == `morale.start`）真的存在吗？**
   （判据 193 —— 直接套用第 190 条的教训）
🆕 **可做**：① 🔴 **找那个加载期校验**（`morale_affliction_threshold == morale.start`）
   ② 读 `GrantVirtue`（美德怎么选）
   ③ 把第 183~193 条判据补进 `observe_list` D11（**本段 11 条**）
   ④ **把我方士气系统（18 条表 + 两条路 + 互斥不变量）写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
