# 校验链**追到顶**：`DirectorBridge.BuildFromRes`（生产加载点）→ … → `throw`（**5 步**）

> 🕒 2026-09-26 · 承接 `134_*.md §5` 的判据 200 ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **判据（第 200 条）**：**"我追到了 N 层 —— 够不够？
>    （要追到【应用启动】或【测试入口】才算完）"** ✓

---

## 1. 🎖️ 完整链路（**5 步**，已追到顶）

```
📊 **链路**：
   ① **`DirectorBridge.cs:48`**（**生产加载点**）⇒
      `TuningConfig tuning = TuningConfig.**Parse**(Read("tuning.json"));`
      ＋ `Read(name) => FileAccess.GetFileAsString($"res://data/{name}")` ✓
   ② **`TuningConfig.cs:148`** ⇒ `JsonSerializer.Deserialize<TuningConfig>(…)` ✓
   ③ **`TuningConfig.cs:156`** ⇒ **`Validate(cfg, json);`** ✓
   ④ **`TuningConfig.cs:208`** ⇒ **`ValidateExpedition(t, rawJson);`** ✓
   ⑤ **`Validate.ExpeditionSide.cs:550`** ⇒ **`throw new InvalidDataException(…)`** ✓
⇒ 🎖️🎖️ **即：链路【完整贯通到生产加载点】** ✓
   📌 **判据（第 200 条）**：**"我追到了 N 层 —— 够不够？
      （要追到【应用启动】或【测试入口】才算完）"** ✓ ⇒ ✅ **已到顶**
```

## 2. 🎖️ `DirectorBridge.BuildFromRes` 是**组合根**（读 7 份数据）

```
📊 **`L48-60` 逐行**：
   `L48` **`TuningConfig.Parse(Read("tuning.json"))`** ✓
   `L49` `BalanceTable.FromTuning(tuning)` ✓
   `L50` `UnitsConfig.Parse(Read("units.json"))` ✓
   `L55` **`BuffPrimitiveTranslation.ValidateAgainst(BuffPrimitivesConfig.Parse(…))`** ⚠️
   `L57` `SkillsConfig.Parse(Read("skills.json"), unitsCfg.PlayerArchetypes)` ✓
   `L60` `FormationConfig.Parse(Read("formation.json"))` ✓
⇒ 🎖️ **即：它一次读 7 份数据，且【每份都 `Parse`（⇒ 都过校验）】** ✓
   ＋ `L52-54` 的注释点明：**"`buff_primitives.json` 加载即校验
     （这是本表**唯一的生产消费点**，不是空转的读）"** ✓
     ⇒ 🎖️ **对照本任务 `48_*.md`**（"Effect 的 `combat_stat_buff` 名"）
        ⇒ ✅ **那条链也在这里闭合** ✓
```

## 3. 🎖️ 而测试侧**大量调用 `Parse`** ⇒ 校验在 CI 里也被跑

```
📊 **测试调用点（部分）**：
   `AfflictionProcTests.cs:76` · `BattleOutcomeTests.cs:45` · `BattleProjectorTests.cs:45` ·
   `CampSkillRunLedgerTests.cs:49` · `CampSkillTestKit.cs:17` · `CombatResolutionTests.cs:72` ·
   `ConsecutiveMissDataTests.cs:37` · `CurioBlessingTests.cs:41` · `CurioDiseaseTests.cs:40` ·
   `CurioPoolReferenceTests.cs:96` · `CurioTraitTests.cs:41` · `CurioUnlockGateWiringTests.cs:94` …
⇒ 🎖️ **即：`Validate` 在【生产】与【测试】两条路都必经** ✓
   ＋ 🔴 **而 `ConsecutiveMissDataTests.cs:55-58` 有【负向测试】**：
      `Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(ranged), "越界（>100）⇒ 报错 ✓")`
      ＋ `Assert.ThrowsException<InvalidDataException>(() => TuningConfig.Parse(missing), …)` ✓
   ⇒ 🎖️ **判据（第 201 条）**：**"这个校验有【负向测试】吗？
      ⇒ 有 ⇒ 校验真的被 CI 盯着；没有 ⇒ 可能被改坏而无人知"** ✓
      ⇒ ✅ **本仓有** ⇒ **可信度高** ✓
```

## 4. 🎖️ 所以这条线索**彻底闭环**（从"注释说的"到"CI 盯着的"）

```
📊 **四级验证链**（本任务第 52~53 轮）：
   | 轮 | 验到什么层 |
   |---|---|
   | `132_*.md` | 注释说"加载期校验强制" |
   | `133_*.md` | ✅ **校验代码存在**（`throw`） |
   | `134_*.md` | ✅ **被调用（4 步链）** |
   | **本件** | ✅ **追到生产加载点 + 有负向测试** |
⇒ 🎖️ **即：从我"怀疑注释只写了计划"⇒ 到"确认 CI 盯着"** ✓
   📌 **判据（第 202 条）**：**"一条'注释承诺'要验到【哪一层】才算闭环？
      ⇒ 存在 → 被调用 → 路径必经 → **有负向测试**（四级）"** ✓
      📌 **本任务多次踩过"注释写了但没实现"** ⇒ ✅ **这次是最好的结果** ✓
```

## 5. 诚实边界

```
✅ **能验**：**`DirectorBridge.cs:48` 的 `Parse` 调用** · **`Read` 的实现（`res://data/`）** ·
   **它读 7 份数据且每份都 `Parse`** · **测试侧的大量调用点** ·
   **`ConsecutiveMissDataTests.cs:55-58` 的两条负向断言** —— **全部当场跑出** ✓
🎖️ 并把判据 200/201/202 落实 ✓
🔴 **不能验**：**`DirectorBridge.BuildFromRes` 被谁调用**（再上一层）⇒
   📌 它是 **`public static`** 且是**组合根** ⇒ ⚠️ **上溯到 UI/场景层** ⇒
   记**未再追**（**已到生产加载点，够用**）✓
🔴 **不能验**：**`ValidateCombat` 有没有对应的负向测试** ⇒ 📌 本件只看了
   `ConsecutiveMissDataTests`（属哪一域未确认）⇒ 记**未逐个核** ✓
🔴 **不能验**：**`BuffPrimitiveTranslation.ValidateAgainst` 的失败行为** ⇒
   📌 注释说"加载时就报" ⇒ ⚠️ **未读其实现** ⇒ 记**未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/parse_callers.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **校验链【追到顶并四级闭环】**：注释 → 存在 → 被调用 → 生产必经 + 负向测试 ✓
🆕 **可做**：① 读 `BuffPrimitiveTranslation.ValidateAgainst`（另一条"加载即校验"）
   ② 数 `ConsecutiveMissDataTests` 的负向断言覆盖了哪些域
   ③ 把第 194~202 条判据补进 `observe_list` D11（**本段 9 条**）
   ④ **把我方"加载即校验"的三处（`tuning` · `buff_primitives` · …）写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
