# `morale_effects` 与 `damage_axis` **是解耦的** —— 且**互斥**（显式覆盖派生）

> 🕒 2026-09-26 · 工具 `tools/dsh/check_morale_vs_axis_coupling.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `127_*.md §5` 的"`morale_effects` 与 `damage_axis` 有无耦合未核"** ✓

---

## 1. 🎖️ 答案是**有条件的耦合**，且**显式覆盖派生**

```csharp
// DamagePipeline.cs:144-159
// 士气：显式 morale_effects（如威吓箭 targets −4，O-21/#170）取代精神派生 −8/−12/−5（不叠加）；
// 否则按 damage_axis + 暴击 + aoe 派生（#157）。
if (skill.ExplicitMoraleEffects is { Count: > 0 } explicitEffects) {
    foreach (MoraleEffectRequest eff in explicitEffects)
        if (eff.Scope == "targets")
            _ledger.Apply(victim, eff.Delta, "skill_morale_effect", _log);   // 🔴 显式
} else {
    _ledger.ApplyIncomingDamageMorale(victim, **skill.Axis**, dmg.AnyCrit, skill.IsAoe, _log);  // 派生
}
```
```csharp
// MoraleLedger.cs:140-149
public void ApplyIncomingDamageMorale(UnitRuntime victim, string axis, bool crit, bool isAoe, CombatLog log)
{
    if (**axis != "mental"**) return;                       // 🔴 只有精神轴派生
    string id = isAoe ? "mental_aoe_hit" : crit ? "mental_crit_hit" : "mental_hit";
    Apply(victim, _events.Get(id).Delta, id, log);         // 值来自 morale_events 表
}
```
```
⇒ 🎖️🎖️ **三条读数**：
   ① 🔴 **两者【互斥】**：有 `morale_effects` ⇒ **只走显式**；否则才**按 `damage_axis` 派生** ✓
   ② ✅ **派生【只在 `axis == "mental"` 时发生】** ⇒ 所以**"精神轴 ⇒ 削士气"这条耦合是**真的**
   ③ 🔴 **但【显式存在时，`axis` 完全不参与士气】** ⇒ ✅ **两件事解耦** ✓
```

## 2. 🎖️ 于是 `ranged_intimidating_shot` 的疑问**有了机制解释**

```
📊 **它的两个字段**：
   · `damage_axis: mental` ⇒ **走 SpiritHit 公式**（用韧性减伤，而非护甲）✓
   · `morale_effects: [{targets, -4}]` ⇒ **显式削士气** ⇒ **走显式分支** ✓
⇒ 🎖️ **即：那条技能里，`mental` 的作用是【换伤害公式】，不是【触发削士气】** ✓
   📌 **削士气由 `morale_effects` 独立负责** ⇒ ✅ **两者不耦合** ✓
   ⇒ 🔴 **所以"`ranged` + `mental` 别扭"这个感觉的来源变了**：
      · 我原来担心"精神轴才该削士气" ⇒ ✅ **而它确实两者都有**（但走不同分支）
      · 🔴 **真正的问题只剩**：**"物理箭矢为何走 SpiritHit（韧性减伤）？"** ⚠️
      ⇒ 🎖️ **判据（第 176 条）**：**"两个字段有没有【耦合】？
          ⇒ 看它们是否在【同一段代码】里一起出现"** ✓
      ⇒ 🎖️ **判据（第 177 条）**：**"我发现'别扭'后，先查【机制上说不说得通】——
         说得通 ⇒ 存疑范围【缩小】（而非消失）"** ✓
```

## 3. 🎖️ 而这条**同时确认了我方士气系统的设计**

```
📊 **我方士气的两个来源**：
   | 来源 | 触发 | 值来自 | 代码 |
   |---|---|---|---|
   | **显式** | 技能有 `morale_effects` | **技能数据**（`skills.json`）| `skill_morale_effect` |
   | **派生** | **无显式** 且 `axis == "mental"` | **`morale_events` 表** | `mental_hit`/`_crit_hit`/`_aoe_hit` |
⇒ 🎖️ **即：两条路【都是数据驱动】**（一个来自技能 · 一个来自事件表）✓
   ＋ 🔴 **派生有 3 个变体**（普通/暴击/AOE）⇒ ✅ 而**显式没有变体**（就是 `delta`）✓
   🎖️ **判据（第 178 条）**：**"两条路的值【各自从哪来】？
      ⇒ 都数据驱动 ⇒ 那"硬编码"的担心可以排除"** ✓
```

## 4. 🔴 而 `-8/-12/-5` 这组数**在注释里**，需核它在表里

```
📊 **`DamagePipeline.cs:144` 的注释**：**"取代精神派生 −8/−12/−5（不叠加）"** ⚠️
   ⇒ 📌 **即：注释说派生值是 −8/−12/−5** ✓
   ＋ 而代码是 **`_events.Get(id).Delta`** ⇒ **从表读** ✓
   ⇒ 🎖️ **判据（第 179 条）**：**"注释里写死的数 ——
      与【表里的值】一致吗？（注释会过期）"** ✓
      📌 **这要查 `morale_events` 表** ⇒ ⚠️ **本件未查** ⇒ 记**待核** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`DamagePipeline.cs:144-159` 的互斥二选一** ·
   **`MoraleLedger.cs:140-149` 的 `axis != "mental"` 早退** ·
   **派生 3 个 ID（`mental_hit`/`_crit_hit`/`_aoe_hit`）** ·
   **两条路的值来源（技能数据 vs `morale_events` 表）** ·
   **`morale_effects` 的 4 种 scope**（`self`/`targets`/`team`/`ally_targets`）—— **全部当场跑出** ✓
🎖️ 并把"存疑"从"耦合问题"缩到"公式选择问题" ✓
🔴 **不能验**：**注释的 `−8/−12/−5` 与 `morale_events` 表是否一致** ⇒
   📌 **要读表** ⇒ 记**待核**（判据第 179 条）✓
🔴 **不能验**：**`ranged` + `mental`（物理箭矢走 SpiritHit）是否刻意** ⇒
   📌 **机制上说得通**（伤害公式与削士气分离）⇒ ⚠️ **但"为何用韧性减伤"仍是设计选择** ⇒
   记**待裁**（**窗口第 ⑪ 件的存疑范围已缩小**）✓
🔴 **不能验**：**`eff.Scope == "targets"` 之外的 scope 在伤害路径是否被忽略** ⇒
   📌 从 `L150` 看**只处理 `targets`** ⇒ ⚠️ **其余 3 种 scope 走支援路径**（`SkillExecutor.cs:467` 有注释）⇒
   记**已见注释，未读实现** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/morale_axis.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **`morale_effects` 与 `damage_axis` 的耦合【查清】**：互斥 · 显式覆盖派生 ✓
🆕 **存疑范围【缩小】**：从"耦合问题"变为"为何物理箭矢走 SpiritHit"
🆕 **可做**：① 🔴 **核注释的 `−8/−12/−5` 与 `morale_events` 表是否一致**（判据 179）
   ② 读 `SkillExecutor` 的支援路径（其余 3 种 scope）
   ③ 把第 176~179 条判据补进 `observe_list` D11
   ④ **更新窗口第 ⑪ 件**（存疑范围缩小，避免策划按旧描述判）✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
