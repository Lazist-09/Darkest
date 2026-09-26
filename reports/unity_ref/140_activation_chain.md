# 消费点**两头都全** —— 但 `SkillExecutor` 走的是 `Scale` 而非 `ScaleByCaster`（**内联了取数**）

> 🕒 2026-09-26 · 承接 `139_*.md §6` 的判据 218 ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **判据（第 218 条）**：**"消费点验证要两头：
>    【它读了那个名字】＋【有人调它】"** ✓

---

## 1. 🎖️ 判决：**两头都全**，但调用路径与注释说的**不完全一样**

```
📊 **实测调用点**（`SkillExecutor.cs:365-370`）：
   `int healPct = _buffs?.PercentModAny(caster, **"hp_heal_percent"**) ?? 0;`   // 🔴 取数【在这里】
   `int baseHeal = critHeal ? heal * 2 : heal;`
   `int scaledHeal = HealAmount.**Scale**(baseHeal, healPct);`                    // 🔴 调的是 `Scale`
   `if (healPct != 0) _log.Append(new EffectEvent(caster, "hp_heal_percent", healPct, true, caster));`
⇒ 🎖️ **即：**
   · ✅ **名字真的被读**（`PercentModAny(caster, "hp_heal_percent")`）✓
   · ✅ **有人调 `HealAmount`** ✓
   · 🔴 **但调的是 `Scale(baseHeal, healPct)`，【不是】`ScaleByCaster(...)`** ⚠️
⇒ 🎖️ **判据（第 218 条）**：**"消费点验证要两头：
      【它读了那个名字】＋【有人调它】"** ✓ ⇒ ✅ **两头都满足** ✓
   🔴 **而注释说"消费点 `HealAmount.ScaleByCaster`"** ⇒ ⚠️ **而实际调用的是 `Scale`** ✓
      📌 **即：`ScaleByCaster` 是【为可单测而准备的包装】，而生产路径【内联了取数】** ✓
      🎖️ **判据（第 219 条）**：**"注释点名的那个函数 ——
         是【生产路径】还是【为测试准备的便利包装】？"** ✓
```

## 2. 🎖️ 这**不是缺陷**，而是一种有意的分工

```
📊 **两层的实际分工**：
   | 层 | 在哪 | 做什么 |
   |---|---|---|
   | **取数** | **`SkillExecutor.cs:365`**（内联）| `_buffs?.PercentModAny(caster, "hp_heal_percent")` ✓ |
   | **算法** | **`HealAmount.Scale`**（纯函数）| `baseHeal * (1 + pct/100)`，`pct=0` 原样 ✓ |
   | **便利包装** | `HealAmount.ScaleByCaster` | **取数 + 调算法**（**供单测/其它调用方**）✓ |
⇒ 🎖️ **即：注释 `L12-13` 说的"算法在这里（可单测），`SkillExecutor` 只负责取修正 + 调这里"
   是【准确的】** —— **而 `ScaleByCaster` 只是把"取修正"也包了一层** ✓
   ＋ 🎖️ **而日志里有 `EffectEvent(caster, "hp_heal_percent", healPct, …)`**
     ⇒ ✅ **审计链完整**（改了多少都记下了）✓
   ⇒ 🎖️ **判据（第 220 条）**：**"同名的'包装函数'没人调 ——
      是【死代码】还是【测试便利】？⇒ 看有没有测试用它"** ✓
```

## 3. 🎖️ 而 `healPct != 0` 的那个日志分支**印证了"零行为"**

```
📊 **`L368-371`**：
   `if (healPct != 0) { _log.Append(new EffectEvent(caster, "hp_heal_percent", healPct, true, caster)); }`
⇒ 🎖️ **即：`healPct = 0` 时【连日志都不写】** ⇒ ✅ **现有战斗的日志逐位不变** ✓
   ＋ 而 `L364` 的注释也写：**"无该 buff ⇒ pct = 0 ⇒ 行为与激活前逐位相同"**
     （并指向 `reports/m2_activation_readings.md`）✓
   ⇒ 🎖️ **判据（第 221 条）**：**"这个'新功能'在【默认参数】下——
      连日志都不改吗？⇒ 不改 ⇒ 那才有'零行为'的保证"** ✓
      📌 **比 `139_*.md` 的判据 215 更强**：**不只输出相同，【副作用也相同】** ✓
```

## 4. 🎖️ 所以"1/25 激活链"**完整且可追**

```
📊 **完整链（5 环）**：
   ① **参考数据**：`JsonBuffs.json` 的 `stat_type`（25 种）✓
   ② **清单**：`BuffPrimitiveTranslation.Activated`（`hp_heal_percent`）✓
   ③ **加载校验**：`ValidateAgainst`（25 名单 == 参考全集）✓
   ④ **算法**：`HealAmount.Scale`（纯函数，可单测）✓
   ⑤ **生产调用**：`SkillExecutor.cs:365-367`（取数 + 调算法 + 记日志）✓
⇒ 🎖️ **即：从【参考数据】到【生产调用】五环全通** ✓
   📌 而 `PercentModAny` 的定义在 `IBuffLedger.cs:38`（接口）+ `BuffLedger.cs:177`（实现）✓
   ⇒ ✅ **这是本任务里追得最完整的一条链** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`SkillExecutor.cs:365-370` 的逐行原文** ·
   **`PercentModAny(caster, "hp_heal_percent")` 的真实调用** ·
   **调的是 `Scale` 而非 `ScaleByCaster`** · **`healPct != 0` 的日志分支** ·
   **`PercentModAny` 的定义位置** —— **全部当场跑出** ✓
🎖️ 并把判据 218 的两头都验完，且**发现注释与实现的细微出入** ✓
🔴 **不能验**：**`ScaleByCaster` 有没有被【测试】调用** ⇒
   📌 若没有 ⇒ 它可能是**未被使用的便利包装** ⇒ 记**未核** ✓
   🎖️ **判据（第 220 条）就是问这个** ✓
🔴 **不能验**：**`reports/m2_activation_readings.md` 的"前后读数"** ⇒
   📌 注释点名了它 ⇒ ⚠️ **本件未读** ⇒ 记**未核**（**那是不做假的关键证据**）✓
🔴 **不能验**：**`BuffLedger.PercentModAny` 的实现** ⇒ 记**未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/scale_caller.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **消费点两头【验完】**：名字真被读 · 有人调；**但走的是 `Scale` 不是 `ScaleByCaster`** ✓
🆕 **两条待核**：① `ScaleByCaster` 有没有测试用它（判据 220）
   ② **`reports/m2_activation_readings.md`**（"零行为"的关键证据）
🆕 **可做**：① 🔴 **读 `m2_activation_readings.md`**（前后读数 ⇒ 零行为的实证）
   ② 核 `ScaleByCaster` 的测试调用
   ③ 把第 214~221 条判据补进 `observe_list` D11（**本段 8 条**）
   ④ **把"1/25 激活链（5 环）"写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
