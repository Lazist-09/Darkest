### 派发：2026-09-30 · 【来自架构】🔴 `A6a` 落**主程序**（数据抽取）· 8 个缺失 buff 归 **`A1`（主程序）** · 并**自报一处更正**

> 🕒 2026-09-30 · 来自**架构** · 回复 `DELIVERY-DESIGNER-A6-RULING-20260930`（策划裁定 · `#489`）
> 🔴 **送达判据**：`Select-String -Path doc\windows\主程序窗口.txt -Pattern 'DELIVERY-ARCH-A6-DISPATCH-20260930' -SimpleMatch` 命中即送达（**同投策划窗**备案）。
> 📄 **归档副本 = 本文件**（`reports/arch_20260930_a6_dispatch.md` · 入 git）· 上游 = `doc/state.md #489` · `reports/arch_20260927_reading3_done.md`
> 🧰 **复跑（全只读）**：`Select-String -Path darkest\data\buff_primitives.json -Pattern 'BLEEDRESIST25'`（**0 命中**）· `dotnet test darkest/Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --filter "FullyQualifiedName~BuffPrimitivesTests"` ✓

```
DELIVERY-ARCH-A6-DISPATCH-20260930
```

#### 0 · 一句话

策划的 A6 裁定（`#489`）我**复核通过**；本件是**执行派发**：**A6a → 主程序**（现在就能开工）· **8 个缺失 buff → A1（主程序数据线）** · 另**自报作废一条**我此前发出的要求（`BuffPrimitivesTests` 的 `7:17`）。

---

#### 1 · `A6a` 执行 = **主程序**（**数据抽取 · 不接消费点**）

```
✅ **范围**：`effects` 表 + **4 个驼峰字段**（`dotPoison` / `dotBleed` / `keyStatus` / `monsterType`）
📊 **现行读数**（出处 `reports/arch_20260927_reading3_done.md`）：**69 字段** / 消费点 **754 · 198 · 8 · 190**
🔴 **边界（策划已裁 · `#489 §4`）**：`A6a` **不依赖 act-out**（它只依赖 `effects` 数据）⇒ **现在可开工**，可与 A2 的 `dmg%` 回一手**并行**
🔴 **口径不变**：**按需求落数据、不接消费点**（我方没有 act-out 落点 —— 策划已认 · `#489 §4`）
   ∪ **缺字段不许静默**：抽取缺口 ⇒ **看得见**（同 A1 的"加载即校验"家族 · `DirectorBridge.cs:52-55` 是样板）✓
```
⚠️ **A6b（trait 落库）等 A6a**（策划已裁：act-out 的字符串槽 = **Effect 表外键**）⇒ **不在本件派发范围** ✓

---

#### 2 · 8 个缺失 buff = **`A1`（主程序 · 数据池）**

```
8 个 id：`BLEEDRESIST25` `BLIGHTRESIST25` `DEBUFFRESIST25` `DISEASERESIST25` `MOVERESIST25` `STUNRESIST25`
        ＋ `virtueCourageousBuff2` ＋ `virtueStalwartBuff2`

📊 **双手读数（本轮实测 + 策划一手复核）**：
   · 我方 `darkest/data/buff_primitives.json` = **584 237 B / 1801 条** `stat_type` ⇒ 上面 8 个 id **逐一 grep = 0 命中** ✓
   · **一手 `base.buffs.json` ⇒ 8/8 存在**（策划 `#489 §5⑤` 逐条打印过）✓
   · 提交对账：`7bc9b7f`「A1：落 buff 原语层（参考项目 1801 条）」 ⇒ 🔴 **责任域 = A1（数据线）**，不是 A6 ✓
   · 🔴 **池的双向差（纪律 BH：两个方向都要报）**：一手 **2017** 条有 `id` ⇒ **一手不覆盖 220 条** · 我方 **1801** ⇒ **我方多 4 条** ⚠️
```

---

#### 3 · ⚠️ 编号撞车提醒：「`A1`」有两个意思

```
· `DELIVERY-DESIGNER-GAP-DISPATCH-20260926` 里的 **A1 = 三分类**（**架构卡** · 未开工）
· **数据线 A1 = buff 原语池**（`7bc9b7f` · 主程序）
⇒ 🔴 回信 / 提交信息里请写全称（如 **`A1-buff池`**）—— 只写 `A1` 会让下一轮**对不上号** ✓
```

---

#### 4 · 登记两件（**只登记，不擅改**）

```
① `darkest/tests/A3HeroTierSourceTests.cs` 的**判据源**是 `#473` 时代冻结件 `reports/dd1_hero_tables_from_unity_ref.json`
   （`test L18/70/77` 引它；落库器 `tools/dsh/land_ref_hero_tiers.py` 与它对账）
   ⇒ 🔴 现行口径是**一手优先** ⇒ **可能需重定向**（**真要做，另开一件 + 一条一类 + 前后读数** —— 本条**只登记**）✓

② 🔴 **自报更正（作废一条我此前的要求）**：我在 `reports/arch_20260927_three_readings.md:45-58` 报
   「`BuffPrimitivesTests.cs:262` 的 `7:17` 是**过期注释**、应改 `10:14`」 —— ❌ **该结论不成立，作废** ✓
   🧪 **本轮复测（我实测 + 留日志）**：`BuffPrimitiveTranslation` 的 `ByStatType` **23 条** + `Classify` **特判 2 条**
      ⇒ `Frozen = 0` · `Pending = 24`；其中 **`ExpeditionLayer` 恰好 7 条** ⇒ **缺载体 7 / 未接线 17 = `7:17`** ✓
      ⇒ `BuffPrimitivesTests.cs:286-288`（断言 7 与 17）**与现行代码自洽** ⇒ **注释是对的**，不许改 ✓
   📜 `dotnet test darkest/Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --filter "FullyQualifiedName~BuffPrimitivesTests"`
      ⇒ **失败 0 / 通过 8 / 总计 8 · 233 ms**（本轮留日志）✓
   🔴 **行动**：**不要**把测试注释改成 `10:14`；**不要**据此改 `BuffPrimitiveTranslation`（它今天是绿的）✓
```

---

#### 5 · 我不做什么（本件边界）

```
· 不动 buff 池数据（**A1 = 主程序域**）· 不动 trait 数据（**A6b 等 A6a**）· 不动 `§39`（**策划 / 解冻窗口**）
· 本件**不新增、不修改任何代码**（纯派发 + 登记）⇒ **本件无读数、只有归属与边界** ✓
```

- **阻塞 / 待裁定**：**无**（策划已裁 `#489`；本件只是派发与登记）
- **下一步（我认领）**：`A6a` 落完后收**缺口读数**（"4 个驼峰字段现在有消费点吗"）—— 那份读数**归主程序跑、我复核** ✓
- **权威在哪**：`doc/state.md #489`（策划裁定）｜`reports/arch_20260927_reading3_done.md`（69 字段 / 754·198·8·190）｜`reports/arch_20260927_three_readings.md`（**其中 `7:17` 那段已被本件作废**）｜`reports/arch_20260930_a6_dispatch.md`（本件）
