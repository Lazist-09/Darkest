# `EventQueue` 机制**读通**：`queue` 开关决定「立即 vs 延迟」，且带一套**融合（Fuse）**系统

> 🕒 2026-09-26 · 工具 `tools/dsh/read_event_queue.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `99_*.md §6①` 的"`EventQueue` 入队点未读"** ✓

---

## 1. 🔴 先纠一处**同名混淆**（第 33 次同族）

```
🔴 **我第一版**搜 `EventQueue` ⇒ 命中 **30 处**，其中 28 处是 **`UnitEventQueue`**
   （**另一个东西**：`List<FormationUnit>`，单位队列）⚠️
✅ **实测区分**：
   · 🔴 **`UnitEventQueue`**（`RaidSceneManager.cs:104`）= `List<FormationUnit>(8)`
     ⇒ **"待处理的【单位】列表"**（DoT/事件循环用它遍历单位）✓
   · 🎖️ **`.EventQueue`**（**带点** · `FormationUnit.EventQueue`）= `List<EffectEvent>`
     ⇒ **"该单位待执行的【效果】列表"** ✓
⇒ 🎖️ **判据（第 83 条）**：**"这两个名字相似 —— 是【同一个】还是【前缀撞了】？
   ⇒ 看【类型】与【拥有者】"** ✓
      · `UnitEventQueue` ⇒ **控制器拥有**（`RaidSceneManager` 的字段）✓
      · `EventQueue` ⇒ **单位拥有**（`target.EventQueue`）✓
   📌 与第 66 条（"提到 vs 解析"）· 第 82 条（"同一容器不同用途"）
      **同族：名字/形状相近 ≠ 同一个东西** ✓
```

## 2. 🎖️🎖️ 而入队点是 **`SubEffect.Apply`**（**41 行的基类**）

```csharp
// Mechanics/Skills/SubEffect.cs:6
public virtual void Apply(FormationUnit performer, FormationUnit target, Effect effect)
{
    if (effect.BooleanParams[**EffectBoolParams.Queue**].HasValue) {
        if (effect.BooleanParams[EffectBoolParams.Queue] == **false**)
            **ApplyInstant**(performer, target, effect);                    // 🔴 立即
        else
            target.EventQueue.Add(new EffectEvent(performer, target, effect, this));  // 延迟
    }
    else
        target.EventQueue.Add(new EffectEvent(performer, target, effect, this));      // 🔴 缺省也是延迟
}
```
```
🎖️🎖️ **即：数据里的 `queue` 字段【决定这个 Effect 是立即还是延迟执行】** ✓
   · **`queue: false`** ⇒ **立即**（`ApplyInstant`）✓
   · **`queue: true`** ⇒ **入队**（稍后 `ApplyQueued`）✓
   · 🔴 **【缺省】也是入队** ⇒ ⚠️ **不写 `queue` 就等于延迟** ✓
⇒ 🎖️ **这解释了 `44_*.md` 抽到的 `queue` 字段（出现 447 次）的用途** ✓
   📌 而它**不是"要不要排队"这种琐碎开关** ——
      **它决定【执行时机】**，而时机又决定**同回合内的先后** ✓
   🎖️ **判据（第 84 条）**：**"数据里这个布尔开关 —— 它影响【什么】？
      ⇒ 不是"要不要做"，而是"【什么时候做】""** ✓
```

## 3. 🎖️ 而 `Execute()` 还有一套**融合（Fuse）系统**（本件新发现）

```csharp
// Raid/Events/EffectEvent.cs:28
public void Execute() {
    if (**StackParameter > 0**)
        SubEffect.**ApplyFused**(Performer, Target, Effect, StackParameter);   // 🔴 融合执行
    else
        SubEffect.**ApplyQueued**(Performer, Target, Effect);                  // 普通执行
}
// 而 StackParameter 由 Fuse 累加：
public void Fuse(EffectEvent next) {
    StackParameter += next.SubEffect.**Fuse**(next.Performer, next.Target, next.Effect);
}
public void FuseSelf() { StackParameter = SubEffect.Fuse(Performer, Target, Effect); }
```
```
🎖️ **即：多个同类 Effect 可以被【合并成一个】执行** ✓
   · `SubEffect.Fusable`（基类默认 **`false`**）⇒ **默认不可融合** ✓
   · `SubEffect.Fuse(...)`（基类返回 **`0`**）⇒ **默认不贡献融合量** ✓
   ⇒ 📌 **所以融合是【子类选择性覆盖】的机制** ✓
   🎖️ **判据（第 85 条）**：**"这个基类方法默认返回 0/false ——
      是【占位】还是【"默认不做"的语义】？⇒ 看子类有没有覆盖"** ✓
```

## 4. 🎖️ 而**入队点只有 7 个**（全在 `SubEffect` 的子类里）

| 文件 | 行 | 说明 |
|---|---|---|
| **`SubEffect.cs`** | **13** · **16** | **基类**（所有 Effect 都走这里）✓ |
| `ClearGuardEffect.cs` | 14 | — |
| **`CombatStatBuffEffect.cs`** | **50** · **53** | 属性 buff |
| **`GuardEffect.cs`** | **20** · **23** | 守护 |

```
🎖️ **即：只有 4 个子类【自己入队】** ⇒
   📌 其余 Effect 都走**基类的 `Apply`** ⇒ ✅ **7 处覆盖全部** ✓
   🎖️ **判据（第 86 条）**：**"这个基类方法有 N 个子类覆盖 ——
      是【N 个各写一遍】还是【少数覆盖 + 多数继承】？"** ✓
```

## 5. 🎖️ 于是「延迟效果」这一层**与 A6a 的关系**

```
📊 **完整链**：
   `Effects.txt` 的 `queue` 字段（447 次）
     ⇒ `Effect.BooleanParams[Queue]`（解析时读入）
     ⇒ **`SubEffect.Apply`**（`queue: false` ⇒ 立即 · 其余 ⇒ 入队）
     ⇒ `target.EventQueue`（`List<EffectEvent>`）
     ⇒ `RaidSceneManager` 的 `#region Effects`（`do…while` 连锁执行）
     ⇒ **`EffectEvent.Execute()`**（融合 or 普通）
⇒ 🎖️ **即：A6a 的 `queue` 字段【不是装饰】，它接进一个完整的延迟系统** ✓
   📌 **采用 A6a 时必须一并实现** ⇒ ⚠️ **否则 447 处 `queue` 语义全错** ✓
```

## 6. 诚实边界

```
✅ **能验**：**两个同名队列的区分（类型 + 拥有者）** ·
   **`SubEffect.Apply` 的 41 行原文（含 `queue` 三分支）** ·
   **`EffectEvent.Execute` 与 `Fuse`/`FuseSelf`** ·
   **`Fusable` 默认 `false` · `Fuse` 默认返回 `0`** ·
   **入队点 7 处（4 个文件）** —— **全部当场跑出** ✓
🎖️ 并**首次读通"延迟效果"这一层**，且把它与 `queue` 字段接上 ✓
🔴 **不能验**：**哪些子类【覆盖了 `Fusable`/`Fuse`】** ⇒ 本件只见基类默认值 ⇒ 记**未查** ✓
   📌 **那是"融合系统实际用在哪"的关键** ⇒ ⚠️ **记为重点待办** ✓
🔴 **不能验**：**`ApplyInstant` vs `ApplyQueued` 的实际差异** ⇒
   📌 从 `PoisonEffect` 看：`ApplyQueued` **包了提示与表现**（`94_*.md` 已读）⇒ 记**部分已知** ✓
🔴 **不能验**：**`EffectBoolParams.Queue` 的解析** ⇒ 从 `44_*.md` 知 `queue` 是 447 次 ⇒ 记**未读那处 case** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/eq_disamb.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **`EventQueue`【读通】**：`queue` 字段决定立即/延迟 · 带融合系统 · 入队点 7 处 ✓
🆕 **可做**：① 🔴 **查哪些子类覆盖 `Fusable`/`Fuse`**（融合系统的实际用点）
   ② 读 `EffectBoolParams.Queue` 的解析（关掉 §6 的口子）
   ③ 把第 83~86 条判据补进 `observe_list` D11
   ④ **把 `queue` 的语义补进 `PLAN_adoption` 的 A6a 行**（447 处不是装饰）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
