# `ApplyDamageRounding` 调用点**全列**：定义 1 + 调用 14 —— 改动面**含 2 处配置硬约束**

> 🕒 2026-09-26 · 工具 `tools/dsh/list_damage_rounding_callsites.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `106_*.md §6` 的"改动前必须列调用点"** ✓

---

## 1. 📊 调用点全表（**定义 1 · 调用 14**）

| # | 位置 | 语义 |
|---|---|---|
| **定义** | **`core/math/BattleMath.cs:180`** | `ApplyDamageRounding(double raw, int damageFloor = **1**)` |
| 1 | `BattleMath.cs:107` | 内部包装（**`§2.3`**）|
| 2 | `BattleMath.cs:134` | 内部包装（**`§2.3`**）|
| 3 | **`sim/pipeline/DamageStep.cs:105`** | 🔴 **伤害主路径**：`raw + attacker.DamageCarry` |
| 4 | **`sim/pipeline/WeaponBaseDamage.cs:59`** | 🔴 **武器基础伤害** |
| 5 | **`sim/director/BattleProjector.cs:229`** | 🔴 **投影/预估**（`total +=`）|
| 6~8 | **`tests/FormulaTests.cs:92-96`** | 🔴 **5 条断言**（**含 `-3.2 ⇒ 1`**）|
| 9~11 | `tests/M1cStage3DiffTableTests.cs:83-86` | 3 条 |
| **12~14** | （`FormulaTests` 的其余断言）| — |

```
⇒ 🎖️ **即：生产代码只有 3 处**（`DamageStep` · `WeaponBaseDamage` · `BattleProjector`）✓
   ＋ **其余 11 处是测试** ⇒ ✅ **改动面【小且明确】** ✓
   🎖️ **判据（第 105 条）**：**"改一个共用函数之前 ——
      先列【全部调用点】与【各自的语义】"** ✓
```

## 2. 🔴 而**有 2 处配置级硬约束**（不改这两处，改函数也没用）

```
📊 **下限的【真实来源】不是默认参数**：
   · **`darkest/data/tuning.json:134`** ⇒ **`"damage_floor": 1`** ✓
   · **`data/BalanceTable.cs:17`** ⇒ `public int DamageFloor => Tuning.DamageFloor;` ✓
   · **`data/TuningConfig.cs:116`** ⇒ `[property: JsonPropertyName("damage_floor")]` ✓
   · 🔴 **`data/TuningConfig.Validate.CombatSide.cs:59`** ⇒ **`if (t.DamageFloor < 1)`** ⚠️
     ⇒ ✅ **即：有【校验】强制 `DamageFloor >= 1`** ✓
   · 而**两个生产调用点【显式传】** `balance.DamageFloor` ⇒ **默认参数 `= 1` 形同虚设** ✓
⇒ 🎖️🎖️ **即：要改成"下限 0"，必须同时改【3 处】**：
   ① `BattleMath.ApplyDamageRounding` 的默认值与 clamp 逻辑 ✓
   ② **`tuning.json` 的 `damage_floor` ⇒ `0`** ✓
   ③ 🔴 **`TuningConfig.Validate.CombatSide.cs:59` 的 `if (t.DamageFloor < 1)` 校验** ⚠️
      ⇒ **否则 `0` 会被校验拒绝** ✓
   🎖️ **判据（第 106 条）**：**"这个默认值的【真实来源】在哪？
      ⇒ 若有【校验】把它钉住 ⇒ 改配置会失败 ⇒ 必须一并改校验"** ✓
   📌 与 `EconomyConfig.cs:142`（`light_tier_bonus 必须单调不减`）**同族**：
      **本仓有多处"校验把语义钉住"** ✓
```

## 3. 🎖️ 而**测试里有 5 条断言会被打破**（改动清单要带上）

```
📊 **`tests/FormulaTests.cs:92-96`**：
   `L92` `ApplyDamageRounding(0.5)` ⇒ **期望 1**（`round(0.5)=1`）⚠️
   `L93` `ApplyDamageRounding(1.49)` ⇒ **期望 1** ⚠️
   `L94` `ApplyDamageRounding(1.5)` ⇒ **期望 2** ⚠️
   `L95` `ApplyDamageRounding(-3.2)` ⇒ **期望 1**（"任何来源伤害最低 1 点"）🔴
   `L96` `ApplyDamageRounding(7.68)` ⇒ **期望 8** ⚠️
⇒ 🔴 **改成 `Ceil` + 下限 0 后**：
   · `0.5` ⇒ **1**（Ceil）✅ **仍过** ✓
   · `1.49` ⇒ **2**（Ceil）🔴 **变** ✓
   · `1.5` ⇒ **2** ✅ **仍过** ✓
   · **`-3.2` ⇒ 0**（下限 0）🔴 **变** ✓
   · `7.68` ⇒ **8** ✅ **仍过** ✓
⇒ 🎖️ **即：5 条里 2 条会红**（`1.49` · `-3.2`）✓
   📌 **判据（第 107 条）**：**"改了行为 ⇒ 先算【哪几条测试会红】，
      并想清"红了是【对的】还是【错的】""** ✓
   ⇒ ✅ **这 2 条红了是【对的】**（因为语义要跟参考一致）⇒ **要改断言，不是改实现** ✓
```

## 4. 🎖️ 所以①的**完整改动包**（4 项，缺一不可）

```
📊 **`BattleMath.ApplyDamageRounding` 要改 `Ceil` + 下限 0** ⇒ 需要：
   ① **函数体**：`Round(AwayFromZero)` ⇒ **`Ceil`** ＋ clamp 用 `damageFloor`（**不硬编码 1**）✓
   ② **`tuning.json:134`**：`damage_floor` **1 ⇒ 0** ✓
   ③ 🔴 **`TuningConfig.Validate.CombatSide.cs:59`**：`if (t.DamageFloor < 1)` ⇒ 放宽到 `< 0` ✓
   ④ 🔴 **`tests/FormulaTests.cs:93/95`** 两条断言按新语义改 ✓
⇒ 🎖️ **即：一个"看起来一行"的改动，实际牵动 4 个文件** ✓
   📌 本件把这件事**量出来了** ⇒ ✅ 交给实现时不会漏 ✓
```

## 5. 诚实边界

```
✅ **能验**：**定义 1 处 + 调用 14 处（逐个文件行号）** · **生产代码只 3 处** ·
   **`tuning.json:134` 的 `damage_floor: 1`** · **`BalanceTable.cs:17`** ·
   **`Validate.CombatSide.cs:59` 的 `if (< 1)` 校验** ·
   **5 条测试断言与其在新语义下的结果** —— **全部当场跑出** ✓
🎖️ 并**把"一行改动"展开成 4 文件的改动包** ✓
🔴 **不能验**：**`BattleMath.cs:107`/`134` 那两个内部包装的【具体语义】** ⇒
   📌 只见它们转发 `ApplyDamageRounding` ⇒ 记**未读上下文** ✓
🔴 **不能验**：**`M1cStage3DiffTableTests` 的 3 处会不会红** ⇒
   📌 它们用 `u.Attack * sumMult`，多含 `sumMult` ⇒ ⚠️ **未算** ⇒ 记**未核** ✓
🔴 **不能验**：**改 `damage_floor` 会不会影响别的平衡**（如"最低伤害"的设计意图）⇒
   📌 **那是【策划裁定】的范畴**（第 8 张单）⇒ 记**待裁** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/df_cfg.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **①的改动包【量清】**：4 项（函数 · 配置 · 校验 · 测试）+ 测试红名单 ✓
🆕 **可做**：① 核 `M1cStage3DiffTableTests` 那 3 处会不会红
   ② 把第 100~107 条判据补进 `observe_list` D11
   ③ **把"①的 4 件改动包"写进 `PLAN_adoption` 的 `§7.1`** ✓
   ④ 给策划第 8 张单**补上这份"改动包"**（让裁定能一次拍到底）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
