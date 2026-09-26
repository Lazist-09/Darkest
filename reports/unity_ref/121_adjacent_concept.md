# 「相邻友方」在参考里**不存在** —— 参考用 `@4321`（全队）+ `.guard` 效果，由**玩家自选**

> 🕒 2026-09-26 · 工具 `tools/dsh/find_adjacent_concept.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `120_*.md §5` 的"参考有没有'相邻'概念"** ✓

---

## 1. 🔴 判决：**`Adjacent` 在参考里 0 命中**

```
📊 **在 `Assets/Scripts/**` 里数**：
   · **`Adjacent` ⇒ `0`** ✓
   · **`adjacent` ⇒ `0`** ✓
   · **`IsAdjacent` ⇒ `0`** ✓
   · **`NextTo` ⇒ `0`** ✓
   · `Neighbor` ⇒ `0` · `Neighbour` ⇒ **2**（🔴 **都是 `CameraMotionBlur` 的贴图名**，无关）✓
⇒ 🎖️ **即：参考【没有"相邻"这个概念】** ✓
   🎖️ **判据（第 153 条）**：**"我说'参考表达不了'——
      去找【它有没有别的地方实现】，而不是只看 `target` 字段"** ✓
```

## 2. 🎖️ 而参考的**守卫是怎么表达的**（读真实数据）

```
📊 **参考用 `.guard 1` 效果 + 技能自己的 `.target`**：
   · `Effects.txt:331` ⇒ `.name "Guard 1"  .target "target" … .guard 1 … .duration 1` ✓
   · `Effects.txt:1009` ⇒ `.name "MAA Guard 1" … .guard 1 … .duration 2` ✓
   · `Effects.txt:552` ⇒ `.name "Antiq ProtectMe Guard" … .guard 1 `
                        `.swap_source_and_target true … .duration 2` ✓
⇒ 🎖️ **即：`GuardEffect` 是【一个 SubEffect】**（`GuardEffect.cs` 107 行）✓
   ＋ 它有 **`SwapTargets`**（来自 `.swap_source_and_target`）⇒ ⚠️ **可交换施受双方** ✓
```

### 🔴 而引用它的技能，`.target` **全是 `@4321`**

```
📊 **实测 9 处**（`Monsters/*.txt`）：
   | 技能 | `.target` | `.launch` |
   |---|---|---|
   | `sapper_guard`（`brigand_sapper_D`）| **`@1234`** | `123` |
   | `head_guard`（`collector_protect_A/B/C`）| **`@4321`** | `4321` |
   | `orgiastic_guard`（`cultist_orgiastic_D`）| **`@4321`** | `4321` |
   | `octo_guard`（`octotank_A/B/C`）| **`@4321`** | `4321` |
   | `totem_guard`（`totem_guard_D`）| **`@4321`** | `4321` |
⇒ 🎖️🎖️ **即：守卫技能的目标是【全队】**（`@` = 己方阵营 · `4321` = 全部 rank）✓
   📌 **而"具体守谁"由【`GuardEffect.ApplyInstant` 在运行时决定】** ✓
      ＋ 读 `GuardEffect.cs`：它处理 `performerGuardStatus`/`targetGuardedStatus` 的
        **互相引用**（`performerGuardStatus.Targets.Contains(target)`）✓
      ⇒ ✅ **即：守卫是"施法者↔被守者"的【配对状态】**
         （`StatusType.Guard` 与 `StatusType.Guarded` 两个状态）✓
```

## 3. 🎖️ 所以"我方 `adjacent_ally_and_self`"的**对应关系**清楚了

```
📊 **我方写法**：`tank_guard_wall` ⇒ `{"scope": "adjacent_ally_and_self"}` ⚠️
   ⇒ 📌 **它限定"相邻"** ⇒ 而**参考不限定**（全队可选，由玩家/运行时决定）✓
⇒ 🎖️ **即：这是一个【设计差异】，不是"参考没有守卫"** ✓
   · ✅ **参考【有】守卫机制**（`GuardEffect` + `Guard`/`Guarded` 状态）✓
   · 🔴 **参考【没有】"相邻"这个限制** ⇒ ⚠️ **那个限制是我方自加的** ✓
   ⇒ 🎖️ **判据（第 154 条）**：**"'限制条件'与'机制'要分开看 ——
      参考有机制但没这个限制 ⇒ 限制属【我方自加】"** ✓
      📌 与第 143 条（"技能级 vs 机制级"）**同族：粒度要分清** ✓
```

## 4. 🎖️ 而这条**同时回答了两个问题**

```
📊 **问题①**（`120_*.md §5`）：参考有无"相邻友方"？ ⇒ 🔴 **无** ✓
📊 **问题②**（`120_*.md §3②`）：参考的 `.target` 能表达相对位置吗？
   ⇒ 🔴 **不能** —— 但**也不需要**：
      **因为它把"选谁"交给了运行时状态**（`GuardEffect`），而不是编码在 `.target` 里 ✓
⇒ 🎖️ **即：`152` 条判据得到【机制层的解释】** ✓
   📌 **参考的设计是"宽 target + 窄效果"**：`.target @4321`（全队可选）
      ＋ `GuardEffect` 在 Apply 时确定实际配对 ✓
   🎖️ **判据（第 155 条）**：**"参考的 `target` 很'宽'——
      那【收窄】在哪一步做的？（可能在后置的效果里）"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`Adjacent`/`adjacent`/`IsAdjacent`/`NextTo` 全 0** ·
   **`Neighbour` 2 处是 `CameraMotionBlur` 贴图名** ·
   **8 个带 `.guard` 的 effect 名** · **9 处引用技能（含 `.target`/`.launch`）** ·
   **`GuardEffect.cs` 的状态配对逻辑** · **`SwapTargets` 的存在** —— **全部当场跑出** ✓
🎖️ 并把判据 `152` 从"表达不了"升级为**"不需要表达（收窄在后置效果）"** ✓
🔴 **不能验**：**英雄侧守卫技能（`MAA Guard 1`）的 `.target`** ⇒
   📌 本件在 `Data/Heroes/**` 里**没找到引用它的技能行** ⚠️
   ⇒ 🔴 **可能英雄技能在别的文件**（如 `Heroes/Info/*.bytes`）⇒ 记**未找到** ✓
   🎖️ **判据（第 156 条）**：**"我搜不到 —— 是【真的没有】还是【在别的文件】？
      先确认【搜索范围】够不够"** ✓
🔴 **不能验**：**`GuardEffect.ApplyInstant` 的完整 107 行** ⇒ 本件只读了前 53 行 ⇒ 记**部分读** ✓
🔴 **不能验**：**我方 `adjacent_ally_and_self` 的设计意图** ⇒ ⚠️ **属策划** ⇒ 记**待裁** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{guard_tgt,guard_skill,guard_skill2,guard_hero}.py`
   **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **"相邻"问题【查清】**：参考无此概念 · 守卫走 `@4321` + `GuardEffect` 运行时配对 ✓
🆕 **可做**：① 🔴 **在 `Heroes/Info/*.bytes` 里找英雄守卫技能**（关掉 §5 的口子）
   ② 读 `GuardEffect.cs` 的完整 107 行
   ③ 把第 153~156 条判据补进 `observe_list` D11
   ④ **把"我方 `adjacent_ally_and_self` 属自加限制"写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
