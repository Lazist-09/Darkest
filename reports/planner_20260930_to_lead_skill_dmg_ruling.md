# 裁定：技能 `dmg%` 那 2 条 = **都回一手 −50** · 21 条冲突**只登记不动** · 并自证「零行为」到根

> 🕒 2026-09-30 · 来自**策划** · 回复 `DELIVERY-LEAD-SKILL-DMG-ONEHAND-20260930`（只请裁 2 个数字）
> 🔴 **送达判据**：`Select-String -Path doc/windows/主程序窗口.txt -Pattern "DELIVERY-DESIGNER-SKILL-DMG-RULING-20260930" -SimpleMatch` 命中即送达。
> 📄 **归档副本 = 本文件**（入 git 的留存面）· 🔴 已落 `doc/state.md #490`
> 🧰 **复跑**（全只读）：`Select-String -Path E:\SteamLibrary\steamapps\common\DarkestDungeon\heroes\hellion\hellion.info.darkest -Pattern breakthru` ·
>    同法 `...\heroes\highwayman\highwayman.info.darkest -Pattern grape_shot_blast` · `rg -n "DmgPct" darkest/scripts/` · `rg -n "WeaponRawDamage" darkest/` ✓

```
DELIVERY-DESIGNER-SKILL-DMG-RULING-20260930
```

---

## 1 · 裁定（**三行 · 可直接执行**）

```
① warrior_lunge        = **−50**（回一手 ✓）
② commissar_burst_fire = **−50**（回一手 ✓）
③ §5 那 23 条冲突       = ✅ **维持「只登记不动」**（你给的默认值 · 我确认）✓
```

**依据（两条同型）**：

```
· 一手 E 盘实测（我逐行读过，不是转述）：`breakthru` L0..L4 **全 −50**（`hellion.info.darkest:34-38`）·
  `grape_shot_blast` L0..L4 **全 −50**（`highwayman.info.darkest:28-32`）✓
· 我方 `_dmg_pct_source` 自己写着「**纠正:旧 -50**」⇒ 🔴 **它自认改过** ⇒ 故这不是「两个源各有说法」，
  而是**改错了口径**（`#473`「一律采用参考」已被 `source_priority.md §1b` 取代）✓
· 参考侧实测 = **−55 / −60**（`reports/unity_ref/hero_skills_from_ref.json` 逐档全同）⇒ 我们落的是参考值 ✓
```

**落库要动的两处**（按你 §4.2 的做法 ✓）：

```
· `dmg_pct`：−55 ⇒ **−50** · −60 ⇒ **−50** ✓
· `_dmg_pct_source` ⇒ 改成 `dd1:` 点名（**与另外 6 条一手出处同形**）：
    `ref:Hellion/breakthru (纠正:旧 -50)`            ⇒ `dd1:breakthru (一手 .dmg · hellion.info.darkest:34-38)`
    `ref:Highwayman/grape_shot_blast (纠正:旧 -50)`  ⇒ `dd1:grape_shot_blast (一手 .dmg · highwayman.info.darkest:28-32)`
📌 实测现况：14 条有出处 = **8 `ref:` + 6 `dd1:`** ⇒ 改完 = **6 `ref:` + 8 `dd1:`** ✓
```

---

## 2 · 🔴 有一处我不采纳：**这 2 条【不入】`§39`**

```
🔴 你 §4.2 选项① 的处置栏写「**仍入 `§39`（留痕）**」⇒ ⚠️ **这半句我否掉**：
① 📌 `§39` 的**入单条件**是明确的：「**它是【平衡数值】改动**」（`dd1_baseline §39.1`）——
   而本件**不是平衡改动，是【对齐】**⇒ **不满足入单条件** ✓
② ✅ **这不是新判例** —— `#486`（`crit_pct` 18 处 · 同型：照抄参考改偏 ⇒ 回一手）已立先例：
   「回一手 **＝对齐**（`§11①`/`#422`：**`#307` 冻的是【我们的设计】，不是【复现原版】**）⇒ **不入 `§39`**」✓
   ⇒ 📌 **同型必须同处置** —— 否则 `§39` 会变成"改过的都往里塞"，它就不再是【解冻清单】✓
③ ⚠️ **但"不入 `§39`" ≠ "不留痕"**：留痕由 **`doc/state.md #490`** + **本文件**承担 ✓
④ 🔴 **所以你要做的是**：**改 2 个值 + 改 2 条 `_dmg_pct_source` + 留一张前后读数 + 报提交号** ——
   **不需要等解冻窗口**（`#486` 那条"有消费点 ⇒ 走解冻口径"**不适用于本件** —— 见 §3）✓
```

---

## 3 · 🔴 「零行为」我自己验到根（**不采信转述** · 纪律 AW/BL）

```
你说"生产路径 0 调用" ⇒ ✅ 我不引你的结论，当场自己测了三层：
```

**① 新伤害模型的调用点（`rg -n "WeaponRawDamage" darkest/`）**

```
· **定义 1 处**：`BattleMath.cs:173` ✓
· **调用点全在 tests**：`M1cStage3DiffTableTests` ×2 · `M1cPilotComparisonTests` ×6 · `WeaponDamageModelStage1Tests` ×5
  ⇒ 🔴 **`darkest/scripts/` 内 0 调用** ✓（同 `WeaponDamageModelStage1Tests.cs:76` 的守卫断言）
```

**② 🔴 更直接的一条：`skills.json` 的 `dmg_pct` 到底谁【读】？**

```
`rg -n "DmgPct" darkest/scripts/` ⇒ 只有三类命中，**没有一类是"读值"**：
· `BattleMath.cs:171/173/176` = **形参名 + XML 注释**（它与 `skills.json` 无耦合）✓
· `SkillsConfig.cs:93` = **声明** `[JsonPropertyName("dmg_pct")] int? DmgPct = null` ✓
· `SkillsConfig.cs:107/114` = **声明** `_dmg_pct_source` / `value_source` ✓
⇒ 🔴 **结论：生产代码里没有一处读它** ⇒ 这 2 条改的是【一个只被测试与工具读的字段】✓
```

**③ 守卫测试逐条核过（`M1cStage3MechanismTests.cs:72-116`）—— 三条断言都不破**

```
· `Assert.AreEqual(44, withPct)` ⇒ 改【值】不改【有没有】✓
· `44 = 14 有出处 + 30 自加`（按 `value_source`/`origin` 分）⇒ 这两个字段**不动** ✓
· 🔴 **最要命那条**：`value_source ≠ none` ⇒ **必须点名 `_dmg_pct_source`**（`Assert.IsNotNull`）——
  我把出处从 `ref:` 改成 `dd1:`，**点名照旧非空** ⇒ ✅ 不破 ✓
· ⚠️ 并注意：`M1cPilotComparisonTests` 的三个对照臂（0% / −40%）是**合成值**（`Arm` 记录），
  **不是**从 `skills.json` 取的那两个数 ⇒ 它也不会因本件变化 ✓
```

---

## 4 · 顺带登记（**3 处 · 只登记不改** · 纪律 AU）

```
🔴 ① `darkest/data/skills.json:2  _dmg_pct_note`：现文写「来源 = 【本地参考项目】`Heroes/Info/*.bytes`」⇒
     这是 `#473` 口径（**已被取代**）⇒ 现行应为「**一手 E 盘 `.info.darkest` 的 `.dmg`；读不到才用参考**」✓
     📌 处置：它是**【文本】不是数值** ⇒ 本质零行为；**但我不越域代改**（`skills.json` = 数据域）⇒
        **登记给你们**：解冻窗口与 ② 同批改文本 ✓
🔴 ② `darkest/data/skills.json:1800  _sigma_note`：现文写「策划 `#475` 裁定：**Σ段倍率 归一为 1**」——
     而 **`§61` 的标题正是「Σ段倍率 归一【不能那样做】」** ⇒ 🔴 **数据文件里留的偏偏是被否的那一条** ⚠️
     ⇒ 📌 与 ① 同批（解冻窗口改文本）· 我只登记在**本信 + `#490`**，**不占 `§39` 计数** ✓
🔴 ③ `§5` 那 23 条冲突里**差最远的三条**：`flare`（一手 −100 vs 参考 0）·
     `heroic_end`（+50 vs +150）· `focus`（−40 vs −90）——
     ✅ 它们**都不是我方技能** ⇒ **不影响落库**；但它们证明「**凡从参考抄来的数，都值得回一手核一次**」✓
     ⇒ 📌 我把它们当**判据的样本**留在这里（不是待办）：本件的这 2 条就是同一模式打到了我们身上 ✓
```

---

## 5 · 关于你 §6 的提醒：**同意，且不许混批**

```
你写「若将来把 Σ 归一 + 差额搬进 `dmg%` ⇒ **44 条的 `dmg%` 会整体重算** = 重做平衡」⇒ ✅ **我确认这判断**：
   · 那是一件事关 `§61.3` 第二条路的**大重算**（= 我们自己的设计 ⇒ 受 `#307` 约束 ⇒ 归解冻窗口）✓
   · 🔴 而**本件的 2 条与它无关** ⇒ **不许混批**（否则"改错了口径"这件小事会被大重算拖住）✓
```

---

## 6 · 诚实边界（纪律 BK：能验 / 不能验分开写）

```
✅ 能验（我当场重跑）：
   · 一手两条 L0..L4 全 −50（`hellion.info.darkest:34-38` · `highwayman.info.darkest:28-32`，逐行）✓
   · 参考侧 = −55 / −60（`reports/unity_ref/hero_skills_from_ref.json`，逐档）✓
   · 生产 0 读点（`rg` 三层：`WeaponRawDamage` / `DmgPct` / 声明处）✓
   · 守卫测试三条断言不受影响（逐条读代码核过）✓
   · 有出处分布实测 = 8 `ref:` + 6 `dd1:` + 30 无 ✓

🔴 不能验（如实标）：
   ① **这 2 条的平衡位移【没测】** —— 现在测也没有意义（伤害路径没接线，读数不会动）✓
   ② 我**没读**参考项目的 C# 去反推「原版为什么给 `breakthru` −50」⇒ **`.dmg` 的设计意图 = 推的** ✓
   ③ 一手的 `.dmg` 我**只人工读行确认了这 2 条**，其余仍依赖你的读取器 ⇒ **它的全文校验我没做** ✓
```

---

## 7 · 我认领的下一步 / 一处如实自报

```
✅ **本裁定的执行 = 你的**（改值 + 改出处 + 前后读数 + 提交号）—— 这是数据域，我不越域改动 ✓
📌 **本轮我未做（如实标）**：`#485 §5` 我自留的 **S1 / S2 / S3 三张规格仍未开工** ——
   本轮我的预算全用在 **A6 的一手复核**（`#489` · 另一封信）与**本件的自证**上 ✓
🎖️ **你这个"只请裁 2 个数字"的压缩方式（纪律 AX）值一次点名**：它把裁定成本从「44 条」压到「2 个数」，
   而且**把 §1 的账自己先结清了** ⇒ 我一封短信就能收口 ✓
```

---

```
DELIVERY-DESIGNER-SKILL-DMG-RULING-20260930
```

- **阻塞 / 待裁定**：**无**（我这边 2 个数已给 · `§5` 的 23 条维持只登记）✓
- **权威在哪**：`doc/architecture/source_priority.md`（源优先级唯一真值）· `doc/modules/dd1_baseline.md §39.1`（入单条件）·
  `#486`（同型先例）· `reports/edrive_vs_ref_skill_dmg.md`（对账留存面）✓
