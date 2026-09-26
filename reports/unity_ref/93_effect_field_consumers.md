# A6a 的 4 个驼峰字段**消费点读通**：`Effect.LoadData` 的**扁平 token 流**解析 一 全部有 case

> 🕒 2026-09-26 · 工具 `tools/dsh/find_effect_field_consumers.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `92_*.md §6①` 的"4 个字段的代码消费点未读"** ✓

---

## 1. 🎖️🎖️ 首先要说清**解析模型**（这是理解这四个字段的关键）

```csharp
// Effect.cs:194
private void LoadData(List<string> data) {
    CombatStatBuffEffect statEffect = null;      // 🔴 跨 token 的【待填对象】
    …
    for (int i = 1; i < data.Count; i++) {       // 🔴 从 1 开始（0 是 "effect:"）
        switch (data[i]) {
            case ".name":   Name = data[++i]; break;                 // 🔴 【吃掉下一个 token】
            case ".target": switch (data[++i]) { … } break;
            case ".on_hit": … BooleanParams[…OnHit] = parseBool; break;
            case ".dotPoison": SubEffects.Add(new PoisonEffect(int.Parse(data[++i]))); break;
            …
        }
    }
}
```
```
🎖️ **即：数据是【扁平 token 流】，键值相邻**：
   `.dotPoison 2 .duration 3` ⇒ tokens `[".dotPoison", "2", ".duration", "3"]` ✓
   · **`.key` 是 token**（**带点**）⇒ `case ".dotPoison":` ✓
   · **`++i` 吃掉它的值** ⇒ 所以键与值【必须相邻】✓
   🎖️ **判据（第 67 条）**：**"这个解析器是【按行抓键值】还是【扁平 token 流】？
      ⇒ 流式的话，键与值之间【不能插别的 token】"** ✓
      📌 **这解释了我抽取器的 `KV` 正则为什么"够用"** ——
         因为它同样是**扫 `键 值` 对**，**不依赖行边界** ✓
         ⇒ ✅ **所以那 4 个驼峰键只要正则放开大写，就能抓对** ✓
```

## 2. 📊 4 个字段的**消费点**（逐个）

| 数据键 | `.case` 行 | 行为 |
|---|---|---|
| **`dotPoison`** | **`L338`** | `SubEffects.Add(new **PoisonEffect**(int.Parse(data[++i])))` ✓ |
| **`dotBleed`** | **`L341`** | `SubEffects.Add(new **BleedEffect**(int.Parse(data[++i])))` ✓ |
| **`keyStatus`** | **`L451`** | 按值设 `statEffect.TargetStatus`（**4 种**）✓ |
| **`monsterType`** | **`L474`** | 按值设 `statEffect.TargetMonsterType`（**4 种**）✓ |

```
⇒ ✅ **4/4 都有 `.case`** ⇒ **无一是"没人读"** ✓
   🎖️ 与 A5 那次的"24 键全有 case"**同形** ✓
```

### 🎖️ 而 `keyStatus` / `monsterType` 是**枚举映射**（不是数值）

```
`.keyStatus` ⇒ 值 → `StatusType`：
   `"tagged"` ⇒ `StatusType.**Marked**` · `"poisoned"` ⇒ `**Poison**` ·
   `"bleeding"` ⇒ `**Bleeding**` · `"stunned"` ⇒ `**Stun**` ✓
   ＋ **`default:` ⇒ `Debug.LogError("Unknown key status…")`** ✓
`.monsterType` ⇒ 值 → `MonsterType`：
   `"unholy"` · `"man"` · `"beast"` · `"eldritch"` ⇒ 对应的 `MonsterType.*` ✓
   ＋ 🔴 **本件只读到 4 个**（`L480-491`），**而 `L312` 的 `.kill_enemy_types` 有 5 个**
      （多一个 **`"corpse"`**）⚠️ ⇒ 📌 **两张表的取值集【不同】** ✓
      🎖️ **判据（第 68 条）**：**"两个同类的枚举映射，取值集【一样】吗？
         ⇒ `keyStatus` 4 个 · `kill_enemy_types` 5 个 ⇒ 不一样"** ✓
```

## 3. 🎖️ 而**两个 Effect 类**的构造签名对上了

```
`.dotPoison` ⇒ `new PoisonEffect(**int**)` · `.dotBleed` ⇒ `new BleedEffect(**int**)` ✓
📊 **而 `PoisonEffect.cs` 里 `DotPoison` 出现 4 次 · `BleedEffect.cs` 里 `DotBleed` 4 次** ✓
⇒ 🎖️ **即：数值被【存进 Effect 对象】，后续由战斗流程结算** ✓
   📌 与 `30_*.md` 那批"Effect 是效果的唯一表达层"**一致** ✓
```

## 4. 🎖️ 顺带量出：`monsterType` 是**第二多**的字段（73 处）

```
📊 `monsterType`/`MonsterType` ⇒ **73 处**（**分布最广**）：
   `CharacterLocalizationHelper.cs` 15 · `CharacterHelper.cs` 11（`MonsterType` + `monsterType` 5）·
   `Effect.cs` 9 · **`KillEnemyTypeEffect.cs` 7** · **`CombatStatBuffEffect.cs` 6** ·
   `RaidSceneManager.cs` 3 · `Monster.cs`/`MonsterData.cs` 各 2 ✓
⇒ 🎖️ **即：它是"按怪物类型生效"的通用条件** ⇒ ⚠️ **跨多个 Effect 类** ✓
   📌 **判据**：**"这个字段的消费点是【一处】还是【多处】？
      ⇒ 多处 ⇒ 它是【通用条件】而不是【某个 Effect 专属】"** ✓
```

## 5. 🔴 而这条对**采用**的意义

```
✅ **A6a 现在【可用于实现】**：
   ① **数据全了**（4 个驼峰字段已修）✓
   ② **消费点清楚了**（`Effect.LoadData` 的 4 个 `.case`）✓
   ③ **解析模型清楚了**（扁平 token 流 · `键 值` 相邻 · 点前缀）✓
⇒ 🎖️ **即：`dotPoison`/`dotBleed` 可以落库了**（DoT 的核心数值）✓
🔴 **而仍缺**：`PoisonEffect`/`BleedEffect` **的结算逻辑**（每回合扣多少）⇒ 记**未读** ✓
   📌 那是"按其逻辑对齐"的另一半 ⇒ ⚠️ **记为重点待办** ✓
```

## 6. 诚实边界

```
✅ **能验**：**`Effect.LoadData` 的解析模型（L194-210 原文）** · **4 个 `.case` 的行号与体** ·
   **`keyStatus` 的 4 个映射** · **`monsterType` 的 4 个映射** ·
   **`.kill_enemy_types` 有 5 个（多 `corpse`）** · **`monsterType` 的 73 处分布** ——
   **全部当场跑出** ✓
🎖️ 并**把 A6a 补到"可用于实现"**（数据 + 消费点 + 解析模型三者齐）✓
🔴 **不能验**：**`PoisonEffect`/`BleedEffect` 的【结算逻辑】**（每回合扣多少、持续几回合怎么算）⇒
   📌 那是"按参考逻辑对齐"的另一半 ⇒ 记**未读** ✓
🔴 **不能验**：**`dotPoison` 的值（如 2/3）是【每回合】伤害还是【总量】** ⇒ 记**未判** ✓
🔴 **不能验**：**`monsterType` 的 4 vs `kill_enemy_types` 的 5** 是否有意 ⇒ 记**未判** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 无 scratch 依赖（工具本身入库）✓
```

## 7. 下一步

```
✅ **A6a 的 4 个驼峰字段【消费点读通】**：4/4 有 case · 解析模型清楚 ✓
🆕 **可做**：① 🔴 **读 `PoisonEffect`/`BleedEffect` 的结算逻辑**（"按其逻辑对齐"的另一半）
   ② 核 `dotPoison` 的值语义（每回合 vs 总量）
   ③ 把第 67/68 条判据补进 `observe_list` D11
   ④ 把 A6a 的"已可落库"更新进 `PLAN_adoption`（含 69 字段的真值）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
