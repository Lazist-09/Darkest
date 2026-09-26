# 融合的**真实触发条件**：`StackEvents()` 按 **`SubEffect.Type`** 匹配 —— **跨 effect 也会融合**

> 🕒 2026-09-26 · 承接 `102_*.md §6`（记的"只查了同 effect 内"这个前提）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **本件把那个前提【查掉了】** ⇒ **结论要改** ✓

---

## 1. 🔴🔴 `StackEvents()` —— 融合在**单位层**做，按 **`Type`** 配对

```csharp
// Raid/Party/FormationUnit.cs:186
public void StackEvents()
{
    for (int i = EventQueue.Count - 1; i >= 0; i--) {
        if (EventQueue[i].SubEffect.**Fusable**) {
            var coreEffect = effectStackTemp.Find(item =>
                item.SubEffect.**Type** == EventQueue[i].SubEffect.**Type**);   // 🔴 按【Type】配对
            if (coreEffect == null) {
                effectStackTemp.Add(EventQueue[i]);
                EventQueue[i].**FuseSelf**();                                   // 先自算
            } else {
                coreEffect.**Fuse**(EventQueue[i]);                             // 累加
                EventQueue.**RemoveAt(i)**;                                     // 合并后删掉
            }
        }
    }
    effectStackTemp.Clear();
}
```
```
🎖️🎖️ **即：它配对的是【`SubEffect.Type`】**（如 `EffectSubType.Stress`）✓
   ⇒ 🔴 **不是"同一条 effect 内的两条"** —— 而是
      **"该单位 `EventQueue` 里所有 `Fusable` 且【同 Type】的事件"** ⚠️
   ⇒ ✅ **所以【跨 effect】的两条 stress 也会被融合** ✓
```

## 2. 🔴 于是 `102_*.md` 的**结论要改**（第 34 次同族）

```
🔴 `102_*.md` 我写：**"融合不会被触发"**（依据：952 条里 0 条有多条 `stress`）⚠️
   ⇒ 📌 那个依据**只覆盖"同一条 effect 内"** ⇒ ⚠️ **不足以支持结论** ✓
✅ **本件实测**：触发条件是 **"同一单位的 `EventQueue` 里有≥2 个 `Fusable` 且同 `Type` 的事件"** ✓
   ⇒ 📌 **而 `EventQueue` 是【单位级】的**（`FormationUnit.EventQueue`）✓
      ⇒ 🔴 **一个单位可以先后收到【来自不同 effect】的压力** ⚠️
      ⇒ ✅ **那样就会融合** ✓
   🎖️ **判据（第 95 条）**：**"这个配对是按【来源】还是按【类型】？
      ⇒ 按类型 ⇒ 【跨来源】也会配对 ⇒ 只查"同一条"是【不够的】"** ✓
```

## 3. 🎖️ 而这条**把 `102_*.md` 的"语法层保证"也削弱了**

```
🔴 `102_*.md §2②` 我论证："`Effects.txt` 没有组合语法 ⇒ 想加两条 stress 必须同行写两次"✓
   ⇒ 📌 **那是对的** —— **对"同一条 effect"成立** ✓
🔴 **但它【管不到】"同一单位先后收到两条不同 effect 的 stress"** ⚠️
   ⇒ 例：技能 A 施加 `Stress 2`，技能 B 施加 `Stress 3` ⇒
      **若两者在同一次结算前都进了该单位的 `EventQueue`** ⇒ **融合** ✓
   📌 **而 `StackEvents()` 正是在【执行前】做这一次合并** ⇒ ✅ **设计意图明确** ✓
```

## 4. 🎖️ 所以"要不要实现融合"的答案**回到了"要看"**

```
📊 **现在需要回答的是**：
   ① **同一单位的 `EventQueue` 里会不会【同时】有两条 stress？**
      ⇒ 📌 取决于**技能/效果的设计** ⇒ ⚠️ **本件未查**（要查"有没有两个 Effect 都带 stress
         且会在同一次结算前进同一队列"）✓
   ② 🎖️ **而 `StackEvents()` 的调用点**决定"什么时候合并" ⇒ **未读** ✓
⇒ 🎖️ **判据（第 96 条）**：**"我说'这个机制不会触发' ——
   我的依据覆盖了【所有触发路径】吗？"** ✓
   📌 **本件的教训**：**我查了"数据形状"，但触发条件是"运行时队列状态"** ⚠️
      ⇒ ✅ **两者不是一回事** ✓
      🎖️ **与第 76 条（"分支的触发条件在数据里出现过吗"）【同族，但这次我答早了】** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`StackEvents()` 的 20 行原文** · **`Find(item => item.SubEffect.Type == …)`** ·
   **`FuseSelf()` 先自算 · `Fuse()` 累加 · `RemoveAt(i)` 删源** ·
   **`Fusable` 的 3 处（基类 false + 2 个子类 true）** ·
   **`Fuse`/`FuseSelf` 的全部 3 个调用点** —— **全部当场跑出** ✓
🎖️ 并**发现 `102_*.md` 的结论下早了**，且**说清它错在哪** ✓
🔴 **不能验**：**同一单位的 `EventQueue` 会不会同时有两条 stress** ⇒
   📌 那要查**技能/效果的设计**（不是数据形状）⇒ **记为重点待查** ✓
🔴 **不能验**：**`StackEvents()` 的调用点**（何时合并）⇒ 记**未读** ✓
🔴 **不能验**：**`effectStackTemp` 的清空时机**（`L205` 每轮 `Clear()`）⇒
   📌 **说明合并是【每次调用内】的**，不跨调用 ⇒ 记**已知** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/fuse_calls.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **融合的真实触发条件【查清】**：按 `SubEffect.Type` 配对 ⇒ **跨 effect 也会融合** ✓
🔴 **并更正 `102_*.md` 的结论**（"不触发"下的太早）
🆕 **可做**：① 🔴 **查技能数据里有没有"两个带 stress 的 effect 会先后入同一队列"**
   ② 读 `StackEvents()` 的调用点（何时合并）
   ③ 把第 83~96 条判据补进 `observe_list` D11
   ④ **把"融合按 Type 配对"补进 `PLAN_adoption` 的 A6a 行** ✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
