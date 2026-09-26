# 「加载期校验」**确实存在** —— `Validate.ExpeditionSide.cs:550` fail-fast，且**理由写得极清楚**

> 🕒 2026-09-26 · 工具 `tools/dsh/find_affliction_threshold_validation.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `132_*.md §5` 的判据 193** ✓

---

## 1. 🎖️ 判决：**校验在最**（不是"注释只写了计划"）

```csharp
// TuningConfig.Validate.ExpeditionSide.cs:550
if (actOut.MoraleAfflictionThreshold != t.Morale.Start)
{
    throw new **InvalidDataException**(
        $"{ResPath}: dungeon_layer.exploration.morale_affliction_threshold = " +
        $"{actOut.MoraleAfflictionThreshold} 必须 == tuning.morale.start = {t.Morale.Start} —— " +
        "🔴 折磨**只**在士气 0 挂上、**只有**回到 `morale.start` 才解除（`MoraleLedger`）⇒ " +
        "阈值就是那条解除线；不同值会让「探索层判他不带折磨」与「战斗内他确实带着折磨」" +
        "**同时成立**（D-7）⚠️");
}
```
```
⇒ 🎖️ **即：校验【真的在】**，而且是 **fail-fast**（`throw InvalidDataException`）✓
   📌 **判据（第 193 条）**：**"注释说'加载期校验强制'——
      那个校验真的在吗？（第 190 条的教训：注释可能只写了计划）"** ✓
      ⇒ ✅ **这次【在】** ✓
```

## 2. 🎖️ 而校验代码的**注释解释了为什么必须是同值**（这段推理很值得记）

```
📊 **`L535-539` 的原文**：
   > ② 🔴🔴 **阈值必须 == `morale.start`** —— 这是本刀**最关键**的一条：
   >   折磨的**挂上点**（士气 == 0）与**解除点**（士气 == `morale.start`）都是
   >   `MoraleLedger` 的硬口径 ⇒
   >   **「士气 < 阈值」只有在阈值 == `morale.start` 时才与"带折磨"等价**。
   >   若策划把阈值调成 30，就会出现「**士气 40 时折磨实际已挂上（崩溃过），
   >   但探索层判他不带折磨**」⇒ **规则自相矛盾** ⚠️
   >   ⇒ 两者必须同值，改一个就改另一个（fail-fast，不给静默分歧）✓
⇒ 🎖️🎖️ **即：这条校验防的是【两套真值不一致】** ——
   · **战斗内**：折磨由 `MoraleLedger` 在士气 0 挂上 ⇒ **权威**
   · **探索层**：`ExplorationActOut.cs:58` 用 `morale < config.MoraleAfflictionThreshold` 判
     ⇒ **派生判断**
   ⇒ 📌 **两者必须等价** ⇒ ✅ **校验就是保证这个等价的** ✓
   🎖️ **判据（第 194 条）**：**"这条校验防的是什么？
      ⇒ 往往是'两处各判一次，可能不一致'"** ✓
```

## 3. 🎖️ 而代码里**注释与实现的三处互证**

```
📊 **同一个不变量在三处被写明**：
   | 位置 | 说法 |
   |---|---|
   | **`tuning.json:111`** | 「`morale_affliction_threshold` 必须 == `morale.start`（加载期校验强制）」|
   | **`TuningConfig.ExpeditionAndCombat.cs:194-197`** | 「折磨判据 = 士气 < 阈值 —— 该阈值**不是近似**…回到 `morale.start`（= 50）才解除（`MoraleLedger` 第 127~134 行）」|
   | **`Validate.ExpeditionSide.cs:535-556`** | **实际的 `throw`** ＋ 反例推理 ✓ |
⇒ 🎖️ **即：数据注释 · 配置类注释 · 校验代码【三处一致】** ✓
   📌 **判据（第 195 条）**：**"同一个规则被写在几处？
      ⇒ 多处都指向【同一个不变量】⇒ 说明它被认真对待过"** ✓
```

## 4. 🎖️ 而这次校验还**顺带验证了我 `132_*.md` 的结论**

```
📊 **`132_*.md` 我说**："`morale_full_100` 的'立即回 50'是**不变量的实现**
   （美德与折磨永不并存）" ✓
   ＋ **本件发现**：另有**第二条不变量**（阈值 == `morale.start`）也在被校验 ✓
⇒ 🎖️ **即：我方士气系统有【两条互斥不变量】** ✓
   · **美德 ↔ 折磨**（士气 100 vs 0 ⇒ 由"立即回 50"保证）✓
   · **探索层判据 ↔ 战斗内真相**（由阈值校验保证）✓
   ⇒ ✅ **两条都有实现 + 校验** ⇒ **设计完整** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`Validate.ExpeditionSide.cs:550` 的 `throw`（原文逐行）** ·
   **`L535-539` 的反例推理** · **`ExplorationActOut.cs:58` 的派生判断** ·
   **`MoraleLedger.cs:127` 的解除点** · **三处注释互证** —— **全部当场跑出** ✓
🎖️ 并把"注释可信度"从"待核"升级为"**已验，且校验质量高**" ✓
🔴 **不能验**：**`InvalidDataException` 在【启动时】真的会被抛**（即校验确实被调用）⇒
   📌 本件只见**校验函数里有它** ⇒ ⚠️ **未见调用链** ⇒ 记**未读调用者** ✓
   🎖️ **判据（第 196 条）**：**"校验函数存在 ≠ 它被调用；
      要再看【谁调它】"** ✓
🔴 **不能验**：**`MoraleLedger` 第 127~134 行的解除逻辑** ⇒
   📌 本件见 `L127` 一行（`newValue == _balance.MoraleStart`）⇒ 记**部分读** ✓
🔴 **不能验**：**`curio_refuse_percent`/`eat_refuse_percent` 的其余校验** ⇒
   📌 本件见 `L559-564` 的一条 ⇒ 记**未读全** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 无 scratch 依赖（工具本身入库）✓
```

## 6. 下一步

```
✅ **判据 193【闭环】**：加载期校验确实存在（`Validate.ExpeditionSide.cs:550` fail-fast）✓
🆕 **一条新待核**：🔴 **那个校验函数【被谁调用】**（判据 196 —— 存在 ≠ 被调用）
🆕 **可做**：① 🔴 **找 `Validate.ExpeditionSide` 的调用点**（确认启动时真跑）
   ② 读 `MoraleLedger` 的 127~134（解除逻辑）
   ③ 把第 194~196 条判据补进 `observe_list` D11
   ④ **把我方士气系统的两条不变量写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
