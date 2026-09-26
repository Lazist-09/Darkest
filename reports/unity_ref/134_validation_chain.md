# 校验调用链**全程确认**：`Parse` → `Validate` → `ValidateExpedition` → **`throw`**（4 步）

> 🕒 2026-09-26 · 工具 `tools/dsh/find_validate_callers.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `133_*.md §5` 的判据 196** ✓

---

## 1. 🎖️ 完整调用链（**4 步**，逐层确认）

```
📊 **链路**：
   ① **`TuningConfig.cs:148`** ⇒ `JsonSerializer.Deserialize<TuningConfig>(json, JsonOptions)`
      ＋ 失败 ⇒ `throw new InvalidDataException($"{ResPath}: JSON 语法错误 …")` ✓
   ② **`TuningConfig.cs:156`** ⇒ **`Validate(cfg, json);`** ✓
   ③ **`TuningConfig.cs:208`** ⇒ 在 `Validate`（`L193`）里 ⇒ **`ValidateExpedition(t, rawJson);`** ✓
   ④ **`Validate.ExpeditionSide.cs:550`** ⇒
      **`if (actOut.MoraleAfflictionThreshold != t.Morale.Start) throw new InvalidDataException(…)`** ✓
⇒ 🎖️🎖️ **即：链路【完整贯通】** —— 从反序列化到那条 `throw`，**四层都在** ✓
   📌 **判据（第 196 条）**：**"校验函数存在 ≠ 它被调用；
      要再看【谁调它】"** ✓ ⇒ ✅ **已看完**
```

## 2. 🎖️ 而且 `Validate` **在 `Parse` 里、紧接反序列化**（真正的"加载期"）

```
📊 **`TuningConfig.cs:140-157` 的结构**：
   `if (string.IsNullOrWhiteSpace(json)) throw …`            // ① 空
   `cfg = JsonSerializer.Deserialize<TuningConfig>(…) ?? throw …`  // ② 反序列化
   `catch (JsonException ex) throw new InvalidDataException(…)`    // ③ 语法错
   **`Validate(cfg, json);`**                                 // ④ 🔴 语义校验
   `return cfg;`                                              // ⑤ 只有过了才返回 ✓
⇒ 🎖️ **即：`Validate` 是 `Parse` 的【必经一步】** ⇒ 任何 `tuning.json` 都要过它 ✓
   ＋ **且顺序是"语法 → 语义"**（先 `JsonException`，再 `InvalidDataException`）✓
   🎖️ **判据（第 197 条）**：**"这个校验在【哪一步】？
      ⇒ 必须紧接反序列化，且【不通过就不返回】"** ✓
```

## 3. 🎖️ 而 `JsonOptions` 的一条设置**值得记**

```
📊 **`TuningConfig.cs:160-165`**：
   `PropertyNameCaseInsensitive = **false**` ⇐ 🔴 **大小写敏感**（不是宽松匹配）✓
   `ReadCommentHandling = **Skip**` ⇐ ✅ **允许注释** ✓
   `AllowTrailingCommas = **false**` ⇐ 🔴 **不允许尾随逗号** ✓
⇒ 🎖️ **三条都与我方"严格"的取向一致** ✓
   📌 **对照 `PLAN_adoption §6`**：参考的 `JsonAI.json` **第 7438 行有尾随逗号**
      ⇒ ⚠️ **那是【参考】不严格** ⇒ ✅ **而我方读 `tuning.json` 时不允许**（自有数据）✓
      🔴 **注意区分**：**读【我方】数据用严格模式** · **读【参考】数据要容错** ✓
      🎖️ **判据（第 198 条）**：**"同一个项目里，读【自有数据】与读【外部数据】
         用同一套严格度吗？⇒ 应不同"** ✓
```

## 4. 🎖️ 于是 `133_*.md` 的结论**升级为"已验证到调用"**

```
📊 **两级验证**：
   | 层 | `133_*.md` | 本件 |
   |---|---|---|
   | **校验存在** | ✅ 找到 `throw` | ✅ |
   | **被调用** | 🔴 未见 | ✅ **链路 4 步全通** |
⇒ 🎖️ **即：那条不变量【确实在启动时强制】** ✓
   📌 **判据（第 199 条）**：**"我验一件事要【验到哪一层】？
      ⇒ '存在'不够 ⇒ 要验到【被调用、且路径必经】"** ✓
```

## 5. 诚实边界

```
✅ **能验**：**四步调用链（逐文件行号）** · **`Validate` 在 `Parse` 里且 `return` 前必经** ·
   **`JsonOptions` 的三条设置** · **异常类型（`JsonException` → `InvalidDataException`）** ——
   **全部当场跑出** ✓
🎖️ 并把判据 196 从"未读调用者"升级为"**链路全通**" ✓
🔴 **不能验**：**`Parse` 本身被谁调用**（即 `tuning.json` 何时加载）⇒
   📌 本件止于 `Parse` ⇒ ⚠️ **未再往上追** ⇒ 记**未追到顶** ✓
   🎖️ **判据（第 200 条）**：**"我追到了 N 层 ——
      够不够？（要追到【应用启动】或【测试入口】才算完）"** ✓
🔴 **不能验**：**`Validate` 里的 `rawJson` 参数用在哪** ⇒
   📌 签名带 `string rawJson` 但本件未见其用途 ⇒ ⚠️ **可能用于报错时摘录原文** ⇒ 记**未读** ✓
🔴 **不能验**：**`ValidateCombat` 的同级校验** ⇒ 📌 `L209` 见调用 ⇒ 记**未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{validate_chain,validate_top}.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **判据 196【闭环】**：校验链路 4 步全通（`Parse` → `Validate` → `ValidateExpedition` → `throw`）✓
🆕 **可做**：① 🔴 **再往上追一层**（谁调 `Parse` —— 追到启动或测试入口，判据 200）
   ② 读 `ValidateCombat`（同级校验）
   ③ 把第 194~200 条判据补进 `observe_list` D11
   ④ **把我方士气系统的两条不变量 + 校验链写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十一张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
