# `BuffPrimitivesTests` **存在且双向** —— 注释承诺**再次属实**（且断言了我量到的数）

> 🕒 2026-09-26 · 承接 `137_*.md §6①` 的判据 210 ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **判据（第 210 条）**：**"注释说'由 X 测试断言'——
>    X 真的在吗？（本任务已多次验这条）"** ✓

---

## 1. 🎖️ 判决：**测试存在，且是真双向**

```
📊 **`darkest/tests/` 里有 3 个相关文件**：
   · **`BuffPrimitivesTests.cs`** ✅（就是注释点名的那一个）
   · `M2HealPercentActivationTests.cs` ✅
   · `PrimitiveSourceCoverageTests.cs` ✅
⇒ 🎖️ **即：注释说的测试【真的在】** ✓
```

### 而它断言的是**两个方向**（`L203-204` 的注释原文）

```
📊 **`BuffPrimitivesTests.cs:203-204`**：
   > 🔴 **M2 清单必须与池子的 `stat_type` 闭集**逐一相等（双向）——
   >   少一条 = **上游新增了原语而我们没登记**；
   >   多一条 = **清单里有上游不存在的名字**（红线 21）✓
```
```csharp
// L207-221
public void M2Checklist_CoversExactlyThePoolVocabulary() {
    var checklist = new HashSet<string>(BuffPrimitiveTranslation.Checklist, StringComparer.Ordinal);
    string[] missingFromChecklist = cfg.StatTypes.**Except**(checklist)…;      // 🔴 方向①：上游有而我们没登记
    Assert.AreEqual(0, missingFromChecklist.Length,
        $"上游有而清单没有：{…}（必须显式登记）✓");
    …  
    Assert.AreEqual(**1**, BuffPrimitiveTranslation.Activated.Count, "已激活 = 1（hp_heal_percent）✓");
    Assert.AreEqual(**24**, BuffPrimitiveTranslation.Pending.Count, "待接 = 24（25 − 1）✓");
}
```
```
⇒ 🎖️🎖️ **即：**
   · **方向①** ⇒ `cfg.StatTypes.Except(checklist)` ⇒ **上游有而清单无** ⇒ 断言为 0 ✓
   · **方向②** ⇒ （`ValidateAgainst` 里）`checklist` 有而池子无 ⇒ **`throw`** ✓
     ＋ 而 **`ValidateAgainst_Throws_WhenThePoolLacksAChecklistName`**（`L52`）
       是那条的**负向测试** ✓
   ＋ 🎖️ **并硬断言 `Activated == 1` 与 `Pending == 24`** ⇒
     ✅ **与我本件量到的数【完全一致】** ✓
⇒ ✅ **判据（第 210 条）得到肯定答案** ✓
```

## 2. 🎖️ 而我上两件量的数**被测试【钉住】了**（这很有意义）

```
📊 **三处一致的数**：
   | 来源 | `Activated` | `Pending` |
   |---|---|---|
   | **本任务实测**（`136`/`137_*.md`） | **1** | **24** |
   | **代码注释**（`L158`/`L163`） | 1 | 24 |
   | **测试硬断言**（`L220`/`L221`） | **1** | **24** |
⇒ 🎖️ **即：同一个数在【数据 · 注释 · 测试】三处一致** ✓
   📌 **判据（第 211 条）**：**"我量到的数有没有被【测试断言】钉住？
      ⇒ 有 ⇒ 那个数【不会悄悄变】"** ✓
      🎖️ **这是最强的一种"可信"** —— 比注释强，因为**测试会红** ✓
```

## 3. 🎖️ 所以本轮**两次"注释承诺"检验，两次都属实**

```
📊 **本任务的两条记录**：
   | 注释承诺 | 结果 |
   |---|---|
   | `tuning.json`「加载期校验强制」 | ✅ **四级齐**（存在/被调用/必经/负向测试）|
   | **本件**「由 `BuffPrimitivesTests` 双向断言」 | ✅ **存在且真双向 + 硬断言** |
⇒ 🎖️ **即：这 2 处都是【真的】** ✓
   📌 **而本任务早期确实抓过"注释写了但没实现"**（如 `130_*.md` 那条
      `retreat_success_with_death` **无消费点**）⇒ 
      ✅ **所以"要验"是对的 —— 只是这次验出来是好的** ✓
   🎖️ **判据（第 212 条）**：**"我验了 N 条'注释承诺'——
      几真几假？⇒ 假的那些【才是要报的】"** ✓
```

## 4. 诚实边界

```
✅ **能验**：**3 个相关测试文件的存在** · **`M2Checklist_CoversExactlyThePoolVocabulary`
   的双向断言（`Except` + 两处 `AreEqual`）** ·
   **`L220`/`L221` 硬断言 1/24** · **`ValidateAgainst_Throws_…` 的负向测试** ·
   **三处数字一致** —— **全部当场跑出** ✓
🎖️ 并把判据 210/211/212 落实 ✓
🔴 **不能验**：**`PrimitiveSourceCoverageTests` / `M2HealPercentActivationTests` 的断言内容** ⇒
   📌 本件只看了它们的**相关行** ⇒ 记**未全读** ✓
✅ **能验（并已实跑）**：**那 3 个测试类【实际绿】**
   ⇒ `dotnet test --filter "BuffPrimitives|M2HealPercent|PrimitiveSourceCoverage"`
   ⇒ 🎖️ **失败 0 · 通过 11 · 总计 11**（272 ms）✓
   📌 **即：把上一版的"推断绿"变成了"实测绿"** ✓
   🎖️ **判据（第 213 条）**：**"我说'推断它是绿的'——
      能不能【直接跑一遍】？⇒ 能跑就别推断"** ✓
🔴 **不能验**：**`missingFromChecklist` 之外还有没有别的断言**（`L217-219` 未读）⇒
   记**未读全** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/buff_tests.py` **不入库**（scratch）✓
```

## 5. 下一步

```
✅ **判据 210【闭环】**：`BuffPrimitivesTests` 存在且真双向 + 硬断言 1/24 ✓
🆕 **一条可做**：🔴 **实跑那 3 个 buff 相关测试类**（把"推断绿"变成"实测绿"）
🆕 **可做**：① 🔴 **跑 `--filter BuffPrimitives`** 确认绿
   ② 读 `PrimitiveSourceCoverageTests` / `M2HealPercentActivationTests` 全文
   ③ 把第 203~212 条判据补进 `observe_list` D11（**本段 10 条**）
   ④ **把"1/25 进度 + 双向断言 + 三处一致"写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
